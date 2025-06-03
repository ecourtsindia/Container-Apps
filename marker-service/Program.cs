using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using eCourts.Shared;
using eCourts.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("/app/logs/marker-service-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add Azure clients
builder.Services.AddSingleton(provider =>
    new BlobContainerClient(Configuration.ConnectionString, Configuration.PdfContainerName));
builder.Services.AddSingleton(provider =>
{
    var markdownBlobClient = new BlobContainerClient(Configuration.ConnectionString, Configuration.MarkdownContainerName);
    return markdownBlobClient;
});

// Add conversion service
builder.Services.AddSingleton<MarkerConversionService>();

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
    var markdownBlobClient = new BlobContainerClient(Configuration.ConnectionString, Configuration.MarkdownContainerName);
    
    await blobClient.CreateIfNotExistsAsync();
    await markdownBlobClient.CreateIfNotExistsAsync();
    
    Log.Information("Azure resources initialized successfully");
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize Azure resources");
}

// Add API endpoints
app.MapPost("/api/convert", async ([FromBody] ConvertPdfRequest request, MarkerConversionService conversionService) =>
{
    try
    {
        Log.Information("Received conversion request {RequestId} for CNR: {CnrNumber}", request.RequestId, request.CnrNumber);
        
        var result = await conversionService.ConvertPdfToMarkdown(request);
        
        Log.Information("Successfully converted PDF {RequestId}. Markdown URL: {MarkdownUrl}", request.RequestId, result.MarkdownUrl);
        
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to convert PDF {RequestId}", request.RequestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/convert-from-url", async ([FromBody] ConvertFromUrlRequest request, MarkerConversionService conversionService) =>
{
    try
    {
        Log.Information("Received URL conversion request {RequestId} for URL: {PdfUrl}", request.RequestId, request.PdfUrl);
        
        var result = await conversionService.ConvertPdfFromUrl(request);
        
        Log.Information("Successfully converted PDF from URL {RequestId}. Markdown URL: {MarkdownUrl}", request.RequestId, result.MarkdownUrl);
        
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to convert PDF from URL {RequestId}", request.RequestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/status/{requestId}", async (string requestId, MarkerConversionService conversionService) =>
{
    try
    {
        var status = await conversionService.GetConversionStatus(requestId);
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to get status for {RequestId}", requestId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

Log.Information("eCourts Marker Conversion API starting up...");
await app.RunAsync();

// Request/Response Models
public class ConvertPdfRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string PdfBlobPath { get; set; } = string.Empty;
    public string CnrNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string ClientDomain { get; set; } = "ecourts.gov.in";
    public bool EnableOcr { get; set; } = true;
    public string[] OcrLanguages { get; set; } = { "hin", "eng" };
}

public class ConvertFromUrlRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string PdfUrl { get; set; } = string.Empty;
    public string CnrNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string ClientDomain { get; set; } = "ecourts.gov.in";
    public bool EnableOcr { get; set; } = true;
    public string[] OcrLanguages { get; set; } = { "hin", "eng" };
}

public class ConversionResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MarkdownUrl { get; set; } = string.Empty;
    public string MarkdownContent { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}

public class ConversionStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MarkdownUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class MarkerConversionService
{
    private readonly BlobContainerClient _pdfBlobClient;
    private readonly BlobContainerClient _markdownBlobClient;
    private readonly ILogger<MarkerConversionService> _logger;
    private readonly SemaphoreSlim _conversionSemaphore;

    public MarkerConversionService(
        BlobContainerClient pdfBlobClient,
        ILogger<MarkerConversionService> logger)
    {
        _pdfBlobClient = pdfBlobClient;
        _markdownBlobClient = new BlobContainerClient(Configuration.ConnectionString, Configuration.MarkdownContainerName);
        _logger = logger;
        _conversionSemaphore = new SemaphoreSlim(Configuration.MaxConcurrentConversions, Configuration.MaxConcurrentConversions);
    }

    public async Task<ConversionResult> ConvertPdfToMarkdown(ConvertPdfRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        await _conversionSemaphore.WaitAsync();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"conversion_{request.RequestId}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Download PDF from blob storage
                var pdfPath = Path.Combine(tempDir, "input.pdf");
                var blobClient = _pdfBlobClient.GetBlobClient(request.PdfBlobPath);
                
                using (var fileStream = new FileStream(pdfPath, FileMode.Create))
                {
                    await blobClient.DownloadToAsync(fileStream);
                }

                _logger.LogInformation("Downloaded PDF for conversion: {PdfPath}", pdfPath);

                // Convert using Marker
                var markdownContent = await ConvertWithMarker(pdfPath, request, tempDir);

                // Upload to blob storage
                var fileName = $"{request.CnrNumber}-orderno-{request.OrderNumber}.md";
                var markdownBlobClient = _markdownBlobClient.GetBlobClient(fileName);
                
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdownContent)))
                {
                    await markdownBlobClient.UploadAsync(stream, overwrite: true);
                }

                var markdownUrl = markdownBlobClient.Uri.ToString();
                stopwatch.Stop();

                return new ConversionResult
                {
                    RequestId = request.RequestId,
                    Status = "Completed",
                    MarkdownUrl = markdownUrl,
                    MarkdownContent = markdownContent,
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
            _conversionSemaphore.Release();
        }
    }

    public async Task<ConversionResult> ConvertPdfFromUrl(ConvertFromUrlRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        await _conversionSemaphore.WaitAsync();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"conversion_{request.RequestId}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Download PDF from URL
                var pdfPath = Path.Combine(tempDir, "input.pdf");
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(request.PdfUrl);
                response.EnsureSuccessStatusCode();
                
                using var fileStream = new FileStream(pdfPath, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);

                _logger.LogInformation("Downloaded PDF from URL: {PdfUrl}", request.PdfUrl);

                // Convert using Marker
                var convertRequest = new ConvertPdfRequest
                {
                    RequestId = request.RequestId,
                    CnrNumber = request.CnrNumber,
                    OrderNumber = request.OrderNumber,
                    ClientDomain = request.ClientDomain,
                    EnableOcr = request.EnableOcr,
                    OcrLanguages = request.OcrLanguages
                };

                var markdownContent = await ConvertWithMarker(pdfPath, convertRequest, tempDir);

                // Upload to blob storage
                var fileName = $"{request.CnrNumber}-orderno-{request.OrderNumber}.md";
                var markdownBlobClient = _markdownBlobClient.GetBlobClient(fileName);
                
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdownContent)))
                {
                    await markdownBlobClient.UploadAsync(stream, overwrite: true);
                }

                var markdownUrl = markdownBlobClient.Uri.ToString();
                stopwatch.Stop();

                return new ConversionResult
                {
                    RequestId = request.RequestId,
                    Status = "Completed",
                    MarkdownUrl = markdownUrl,
                    MarkdownContent = markdownContent,
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
            _conversionSemaphore.Release();
        }
    }

    private async Task<string> ConvertWithMarker(string pdfPath, ConvertPdfRequest request, string tempDir)
    {
        var outputDir = Path.Combine(tempDir, "marker_output");
        Directory.CreateDirectory(outputDir);

        // Activate virtual environment and run Marker
        var activateScript = "/app/marker_env/bin/activate";
        var markerScript = $"source {activateScript} && cd /app && python -m marker.convert '{pdfPath}' '{outputDir}' --batch_multiplier 1";

        if (request.EnableOcr && request.OcrLanguages?.Length > 0)
        {
            var languages = string.Join(",", request.OcrLanguages);
            markerScript += $" --languages {languages}";
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{markerScript}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start Marker conversion process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Marker conversion failed: {Error}", error);
            throw new InvalidOperationException($"Marker conversion failed: {error}");
        }

        // Find the generated markdown file
        var markdownFiles = Directory.GetFiles(outputDir, "*.md");
        if (markdownFiles.Length == 0)
            throw new InvalidOperationException("No markdown file generated by Marker");

        var markdownContent = await File.ReadAllTextAsync(markdownFiles[0]);

        // Add footer with metadata
        var footer = $"\n\n---\n**Document Information:**\n- CNR Number: {request.CnrNumber}\n- Order Number: {request.OrderNumber}\n- Domain: {request.ClientDomain}\n- Converted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n- Processed by: eCourts Marker Conversion Service";
        markdownContent += footer;

        return markdownContent;
    }

    public async Task<ConversionStatus> GetConversionStatus(string requestId)
    {
        // For now, return a simple status - in a real implementation, you might store this in a database
        return await Task.FromResult(new ConversionStatus
        {
            RequestId = requestId,
            Status = "Unknown - Status tracking not implemented in this demo",
            CreatedAt = DateTime.UtcNow
        });
    }
} 