using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using eCourts.Shared;
using eCourts.Shared.Models;
using iText.Bouncycastleconnector;
using iText.Commons.Bouncycastle.Cert;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas;
using iText.Signatures;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("/app/logs/pdf-signing-service-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add Azure clients
builder.Services.AddSingleton<BlobContainerClient>(provider =>
    new BlobContainerClient(Configuration.ConnectionString, Configuration.PdfContainerName));
builder.Services.AddSingleton<ShareClient>(provider =>
    new ShareClient(Configuration.ConnectionString, Configuration.FileShareName));

// Add certificate
builder.Services.AddSingleton<X509Certificate2>(provider =>
{
    if (!File.Exists(Configuration.CertificatePath))
        throw new FileNotFoundException($"Certificate file not found at {Configuration.CertificatePath}");
    return new X509Certificate2(Configuration.CertificatePath, Configuration.OwnerPassword);
});

// Add PDF signing service
builder.Services.AddSingleton<PdfSigningService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHealthChecks("/health");
app.UseRouting();
app.MapControllers();

// Initialize Azure resources
try
{
    var blobClient = app.Services.GetRequiredService<BlobContainerClient>();
    var shareClient = app.Services.GetRequiredService<ShareClient>();
    
    await blobClient.CreateIfNotExistsAsync();
    await shareClient.CreateIfNotExistsAsync();
    
    Log.Information("Azure resources initialized successfully");
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize Azure resources");
}

// Add API endpoints
app.MapPost("/api/sign", async ([FromBody] SignPdfRequest request, PdfSigningService signingService) =>
{
    try
    {
        Log.Information("Received signing request {RequestId} for CNR: {CnrNumber}", request.RequestId, request.CnrNumber);
        
        var result = await signingService.SignPdf(request);
        
        Log.Information("Successfully signed PDF {RequestId}. Signed URL: {SignedUrl}", request.RequestId, result.SignedPdfUrl);
        
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to sign PDF {RequestId}", request.RequestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/sign-from-url", async ([FromBody] SignFromUrlRequest request, PdfSigningService signingService) =>
{
    try
    {
        Log.Information("Received URL signing request {RequestId} for URL: {PdfUrl}", request.RequestId, request.PdfUrl);
        
        var result = await signingService.SignPdfFromUrl(request);
        
        Log.Information("Successfully signed PDF from URL {RequestId}. Signed URL: {SignedUrl}", request.RequestId, result.SignedPdfUrl);
        
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to sign PDF from URL {RequestId}", request.RequestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/status/{requestId}", async (string requestId, PdfSigningService signingService) =>
{
    try
    {
        var status = await signingService.GetSigningStatus(requestId);
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to get status for {RequestId}", requestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

Log.Information("eCourts PDF Signing API starting up...");
await app.RunAsync();

// Request/Response Models
public class SignPdfRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string PdfBlobPath { get; set; } = string.Empty;
    public string CnrNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
}

public class SignFromUrlRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string PdfUrl { get; set; } = string.Empty;
    public string CnrNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
}

public class SigningResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SignedPdfUrl { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}

public class SigningStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SignedPdfUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PdfSigningService
{
    private readonly BlobContainerClient _blobClient;
    private readonly ShareClient _shareClient;
    private readonly X509Certificate2 _certificate;
    private readonly ILogger<PdfSigningService> _logger;
    private readonly SemaphoreSlim _signingSemaphore;

    public PdfSigningService(
        BlobContainerClient blobClient,
        ShareClient shareClient,
        X509Certificate2 certificate,
        ILogger<PdfSigningService> logger)
    {
        _blobClient = blobClient;
        _shareClient = shareClient;
        _certificate = certificate;
        _logger = logger;
        _signingSemaphore = new SemaphoreSlim(Configuration.MaxConcurrentConversions, Configuration.MaxConcurrentConversions);
    }

    public async Task<SigningResult> SignPdf(SignPdfRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _signingSemaphore.WaitAsync();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"signing_{request.RequestId}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Download PDF from blob storage
                var originalPdfPath = Path.Combine(tempDir, $"{request.CnrNumber}-{request.OrderNumber}_original.pdf");
                var blobClient = _blobClient.GetBlobClient(request.PdfBlobPath);
                
                using (var fileStream = new FileStream(originalPdfPath, FileMode.Create))
                {
                    await blobClient.DownloadToAsync(fileStream);
                }

                _logger.LogInformation("Downloaded PDF for signing: {PdfPath}", originalPdfPath);

                // Apply watermark and sign
                var signedPdfUrl = await ProcessPdf(originalPdfPath, request.CnrNumber, request.OrderNumber, tempDir);
                
                stopwatch.Stop();

                return new SigningResult
                {
                    RequestId = request.RequestId,
                    Status = "Completed",
                    SignedPdfUrl = signedPdfUrl,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
                }
            }
        }
        finally
        {
            _signingSemaphore.Release();
        }
    }

    public async Task<SigningResult> SignPdfFromUrl(SignFromUrlRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _signingSemaphore.WaitAsync();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"signing_{request.RequestId}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Download PDF from URL
                var originalPdfPath = Path.Combine(tempDir, $"{request.CnrNumber}-{request.OrderNumber}_original.pdf");
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(request.PdfUrl);
                response.EnsureSuccessStatusCode();
                
                using var fileStream = new FileStream(originalPdfPath, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);

                _logger.LogInformation("Downloaded PDF from URL: {PdfUrl}", request.PdfUrl);

                // Apply watermark and sign
                var signedPdfUrl = await ProcessPdf(originalPdfPath, request.CnrNumber, request.OrderNumber, tempDir);
                
                stopwatch.Stop();

                return new SigningResult
                {
                    RequestId = request.RequestId,
                    Status = "Completed",
                    SignedPdfUrl = signedPdfUrl,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
                }
            }
        }
        finally
        {
            _signingSemaphore.Release();
        }
    }

    private async Task<string> ProcessPdf(string originalPdfPath, string cnrNumber, string orderNumber, string tempDir)
    {
        // Apply watermark
        var watermarkedPath = Path.Combine(tempDir, $"{cnrNumber}-{orderNumber}_watermarked.pdf");
        await ApplyWatermark(originalPdfPath, watermarkedPath, cnrNumber, orderNumber);

        // Sign PDF
        var signedPath = Path.Combine(tempDir, $"{cnrNumber}-{orderNumber}_signed.pdf");
        await SignPdf(watermarkedPath, signedPath, cnrNumber, orderNumber);

        // Upload to file share
        var fileName = $"{cnrNumber}-orderno-{orderNumber}.pdf";
        var fileClient = _shareClient.GetRootDirectoryClient().GetFileClient(fileName);
        
        using (var signedStream = new FileStream(signedPath, FileMode.Open, FileAccess.Read))
        {
            await fileClient.CreateAsync(signedStream.Length);
            await fileClient.UploadAsync(signedStream);
        }

        var signedPdfUrl = $"{Configuration.TrueCopyDomain}/{fileName}";
        _logger.LogInformation("Successfully uploaded signed PDF: {SignedPdfUrl}", signedPdfUrl);

        return signedPdfUrl;
    }

    private async Task ApplyWatermark(string inputPath, string outputPath, string cnrNumber, string orderNumber)
    {
        using var reader = new PdfReader(inputPath);
        using var writer = new PdfWriter(outputPath);
        using var pdfDoc = new PdfDocument(reader, writer);

        var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            var page = pdfDoc.GetPage(i);
            var pageSize = page.GetPageSize();
            var canvas = new PdfCanvas(page);

            canvas.SaveState();

            // Draw charcoal grey strip on LHS (10 units wide)
            float stripWidth = 10;
            canvas.SetFillColorRgb(0.4f, 0.4f, 0.4f); // Charcoal grey color
            canvas.Rectangle(0, 0, stripWidth, pageSize.GetHeight());
            canvas.Fill();

            // Add repeated watermark text in white color on the left band
            string repeatedText = "www.ecourtsindia.com";
            float textSpacing = 150; // Space between repetitions
            float startY = 10; // Start close to bottom
            float maxY = pageSize.GetHeight() - 10; // Go almost to the top

            canvas.SetFillColorRgb(1f, 1f, 1f); // White color for text in strip
            canvas.BeginText();
            canvas.SetFontAndSize(regularFont, 8);

            // Calculate how many repetitions we need
            float availableHeight = maxY - startY;
            int repetitions = (int)(availableHeight / textSpacing) + 1; // +1 to ensure coverage

            for (int j = 0; j < repetitions; j++)
            {
                float y = startY + (j * textSpacing);

                // Save state before rotation
                canvas.SaveState();

                // Move to position and rotate 90 degrees
                float a = 0f; // cos(90°)
                float b = 1f; // sin(90°)
                float c = -1f; // -sin(90°)
                float d = 0f; // cos(90°)
                float e = 7f; // x position (centered in strip)
                float f = y; // y position

                canvas.SetTextMatrix(a, b, c, d, e, f);
                canvas.ShowText(repeatedText);

                // Restore state after rotation
                canvas.RestoreState();
            }
            canvas.EndText();

            // Add main watermark text at the bottom in charcoal grey
            string watermarkText = $"This is a True Copy of the Court Records Online. Proofed @ eCourtsIndia.com/TrueCopy/{cnrNumber}-orderno-{orderNumber}.pdf";
            string urlPrefix = "eCourtsIndia.com/TrueCopy/";
            int urlStartIndex = watermarkText.IndexOf(urlPrefix);
            string urlText = $"eCourtsIndia.com/TrueCopy/{cnrNumber}-orderno-{orderNumber}.pdf";
            string fullUrl = $"https://{urlText}";

            // Calculate text width for center alignment
            float textWidth = boldFont.GetWidth(watermarkText, 8);
            float bottomMargin = 20;

            // Calculate center position for the entire watermark text
            float centerX = (pageSize.GetWidth() - textWidth) / 2;

            // Add bottom text in charcoal grey
            canvas.SetFillColorRgb(0.4f, 0.4f, 0.4f); // Charcoal grey color
            canvas.BeginText();
            canvas.SetFontAndSize(boldFont, 8);
            canvas.MoveText(centerX, bottomMargin);
            canvas.ShowText(watermarkText);
            canvas.EndText();

            // Add hyperlink to the URL part
            if (urlStartIndex >= 0)
            {
                string textBeforeUrl = watermarkText.Substring(0, urlStartIndex);
                float textBeforeWidth = boldFont.GetWidth(textBeforeUrl, 8);
                float urlTextWidth = boldFont.GetWidth(urlText, 8);

                float urlStartX = centerX + textBeforeWidth;
                float urlEndX = urlStartX + urlTextWidth;
                float urlStartY = bottomMargin;
                float urlHeight = 8;
                float urlEndY = urlStartY + urlHeight;

                PdfLinkAnnotation linkAnnotation = new PdfLinkAnnotation(new Rectangle(urlStartX, urlStartY, urlTextWidth, urlHeight));
                linkAnnotation.SetAction(PdfAction.CreateURI(fullUrl));
                linkAnnotation.SetBorder(new PdfArray(new float[] { 0, 0, 0 }));
                page.AddAnnotation(linkAnnotation);
            }

            canvas.RestoreState();
        }

        await Task.CompletedTask; // Make method async
    }

    private async Task SignPdf(string inputPath, string outputPath, string cnrNumber, string orderNumber)
    {
        var rsaPrivateKey = _certificate.GetRSAPrivateKey();
        if (rsaPrivateKey == null)
            throw new InvalidOperationException("Failed to get RSA private key from certificate");

        using var reader = new PdfReader(inputPath);
        
        WriterProperties props = new WriterProperties();
        props.SetStandardEncryption(
            null,
            Encoding.UTF8.GetBytes(Configuration.OwnerPassword),
            EncryptionConstants.ALLOW_PRINTING,
            EncryptionConstants.ENCRYPTION_AES_128);

        using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new PdfWriter(outputStream, props);
        
        var signer = new PdfSigner(reader, writer, new StampingProperties());
        IExternalSignature signature = new CertificateSignature(rsaPrivateKey);

        X509Chain chainBuilder = new X509Chain();
        chainBuilder.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chainBuilder.Build(_certificate);
        var certChain = new List<IX509Certificate>();
        var bouncyCastleFactory = BouncyCastleFactoryCreator.GetFactory();

        foreach (var chainElement in chainBuilder.ChainElements)
        {
            var certInChain = bouncyCastleFactory.CreateX509Certificate(chainElement.Certificate.RawData);
            if (certInChain != null)
                certChain.Add(certInChain);
        }

        if (certChain.Count == 0)
            throw new InvalidOperationException("No certificates found in the chain");

        var pdfDoc = signer.GetDocument();
        pdfDoc.GetDocumentInfo().SetMoreInfo("Available", $"eCourtsIndia.com/TrueCopy/{cnrNumber}-orderno-{orderNumber}.pdf");
        pdfDoc.GetDocumentInfo().SetMoreInfo("Owner", "eCourtsIndia.com");

        signer.SignDetached(signature, certChain.ToArray(), null, null, null, 0, PdfSigner.CryptoStandard.CMS);

        await Task.CompletedTask; // Make method async
    }

    public async Task<SigningStatus> GetSigningStatus(string requestId)
    {
        // For now, return a simple status - in a real implementation, you might store this in a database
        return await Task.FromResult(new SigningStatus
        {
            RequestId = requestId,
            Status = "Unknown - Status tracking not implemented in this demo",
            CreatedAt = DateTime.UtcNow
        });
    }

    private class CertificateSignature : IExternalSignature
    {
        private readonly RSA rsaPrivateKey;

        public CertificateSignature(RSA rsaPrivateKey)
        {
            this.rsaPrivateKey = rsaPrivateKey ?? throw new ArgumentNullException(nameof(rsaPrivateKey));
        }

        public string GetDigestAlgorithmName() => "SHA256";

        public string GetSignatureAlgorithmName() => "RSA";

        public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

        public byte[] Sign(byte[] message)
        {
            return rsaPrivateKey.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
} 