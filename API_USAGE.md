# eCourts HTTP APIs Usage Guide

Your eCourts Container Apps now provide **HTTP REST APIs** for on-demand PDF conversion and signing. No queues needed!

## 🌐 API Endpoints

After deployment, you'll have two main API services:

### Marker Conversion API
- **Base URL**: `https://ecourts-marker-{environment}.{region}.azurecontainerapps.io`
- **Purpose**: Convert PDFs to Markdown using Marker

### PDF Signing API  
- **Base URL**: `https://ecourts-pdfsigning-{environment}.{region}.azurecontainerapps.io`
- **Purpose**: Sign and watermark PDFs

## 📋 API Reference

### 1. PDF to Markdown Conversion

#### Convert PDF from Blob Storage
```http
POST /api/convert
Content-Type: application/json

{
  "requestId": "conv-001",
  "pdfBlobPath": "pdfs/document.pdf",
  "cnrNumber": "DLHC010001092024",
  "orderNumber": "12345",
  "clientDomain": "ecourts.gov.in",
  "enableOcr": true,
  "ocrLanguages": ["hin", "eng"]
}
```

#### Convert PDF from URL
```http
POST /api/convert-from-url
Content-Type: application/json

{
  "requestId": "conv-002",
  "pdfUrl": "https://example.com/document.pdf",
  "cnrNumber": "DLHC010001092024", 
  "orderNumber": "12345",
  "clientDomain": "ecourts.gov.in",
  "enableOcr": true,
  "ocrLanguages": ["hin", "eng"]
}
```

#### Response
```json
{
  "requestId": "conv-001",
  "status": "Completed",
  "markdownUrl": "https://yourstorageaccount.blob.core.windows.net/markdown-files/DLHC010001092024-orderno-12345.md",
  "markdownContent": "# Document Title\n\nDocument content...",
  "processedAt": "2024-01-15T10:30:00Z",
  "processingTime": "00:02:45"
}
```

### 2. PDF Signing & Watermarking

#### Sign PDF from Blob Storage
```http
POST /api/sign
Content-Type: application/json

{
  "requestId": "sign-001",
  "pdfBlobPath": "pdfs/document.pdf",
  "cnrNumber": "DLHC010001092024",
  "orderNumber": "12345"
}
```

#### Sign PDF from URL
```http
POST /api/sign-from-url
Content-Type: application/json

{
  "requestId": "sign-002",
  "pdfUrl": "https://example.com/document.pdf",
  "cnrNumber": "DLHC010001092024",
  "orderNumber": "12345"
}
```

#### Response
```json
{
  "requestId": "sign-001",
  "status": "Completed", 
  "signedPdfUrl": "https://truecopy.ecourtsindia.com/DLHC010001092024-orderno-12345.pdf",
  "processedAt": "2024-01-15T10:30:00Z",
  "processingTime": "00:01:30"
}
```

### 3. Status Check
```http
GET /api/status/{requestId}
```

```json
{
  "requestId": "conv-001",
  "status": "Completed",
  "createdAt": "2024-01-15T10:28:00Z",
  "completedAt": "2024-01-15T10:30:00Z"
}
```

## 💻 Usage Examples

### Using cURL

#### Convert PDF to Markdown
```bash
curl -X POST "https://ecourts-marker-dev.eastus.azurecontainerapps.io/api/convert" \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "conv-001",
    "pdfBlobPath": "pdfs/court-order.pdf",
    "cnrNumber": "DLHC010001092024",
    "orderNumber": "12345"
  }'
```

#### Sign PDF
```bash
curl -X POST "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io/api/sign" \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "sign-001", 
    "pdfBlobPath": "pdfs/court-order.pdf",
    "cnrNumber": "DLHC010001092024",
    "orderNumber": "12345"
  }'
```

### Using PowerShell

```powershell
# Convert PDF to Markdown
$conversionRequest = @{
    requestId = "conv-001"
    pdfBlobPath = "pdfs/court-order.pdf"
    cnrNumber = "DLHC010001092024"
    orderNumber = "12345"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://ecourts-marker-dev.eastus.azurecontainerapps.io/api/convert" `
    -Method POST `
    -ContentType "application/json" `
    -Body $conversionRequest

Write-Host "Conversion completed: $($response.markdownUrl)"

# Sign PDF
$signingRequest = @{
    requestId = "sign-001"
    pdfBlobPath = "pdfs/court-order.pdf"
    cnrNumber = "DLHC010001092024"
    orderNumber = "12345"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io/api/sign" `
    -Method POST `
    -ContentType "application/json" `
    -Body $signingRequest

Write-Host "Signing completed: $($response.signedPdfUrl)"
```

### Using C#

```csharp
using System.Text;
using System.Text.Json;

public class ECourtApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _markerApiBaseUrl;
    private readonly string _signingApiBaseUrl;

    public ECourtApiClient(string markerApiBaseUrl, string signingApiBaseUrl)
    {
        _httpClient = new HttpClient();
        _markerApiBaseUrl = markerApiBaseUrl;
        _signingApiBaseUrl = signingApiBaseUrl;
    }

    public async Task<ConversionResult> ConvertPdfAsync(ConvertPdfRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_markerApiBaseUrl}/api/convert", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ConversionResult>(responseJson)!;
    }

    public async Task<SigningResult> SignPdfAsync(SignPdfRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_signingApiBaseUrl}/api/sign", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SigningResult>(responseJson)!;
    }
}

// Usage
var client = new ECourtApiClient(
    "https://ecourts-marker-dev.eastus.azurecontainerapps.io",
    "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io"
);

var conversionRequest = new ConvertPdfRequest
{
    RequestId = "conv-001",
    PdfBlobPath = "pdfs/court-order.pdf",
    CnrNumber = "DLHC010001092024",
    OrderNumber = "12345"
};

var result = await client.ConvertPdfAsync(conversionRequest);
Console.WriteLine($"Markdown URL: {result.MarkdownUrl}");
```

### Using Python

```python
import requests
import json

class ECourtApiClient:
    def __init__(self, marker_api_url, signing_api_url):
        self.marker_api_url = marker_api_url
        self.signing_api_url = signing_api_url
    
    def convert_pdf(self, request):
        response = requests.post(
            f"{self.marker_api_url}/api/convert",
            json=request,
            headers={"Content-Type": "application/json"}
        )
        response.raise_for_status()
        return response.json()
    
    def sign_pdf(self, request):
        response = requests.post(
            f"{self.signing_api_url}/api/sign", 
            json=request,
            headers={"Content-Type": "application/json"}
        )
        response.raise_for_status()
        return response.json()

# Usage
client = ECourtApiClient(
    "https://ecourts-marker-dev.eastus.azurecontainerapps.io",
    "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io"
)

conversion_request = {
    "requestId": "conv-001",
    "pdfBlobPath": "pdfs/court-order.pdf", 
    "cnrNumber": "DLHC010001092024",
    "orderNumber": "12345"
}

result = client.convert_pdf(conversion_request)
print(f"Markdown URL: {result['markdownUrl']}")
```

## 🚀 Complete Example Workflow

```bash
# 1. Upload PDF to blob storage (if needed)
az storage blob upload \
  --file "court-order.pdf" \
  --container-name "pdfs" \
  --name "court-order-123.pdf" \
  --connection-string "YOUR_CONNECTION_STRING"

# 2. Convert PDF to Markdown
CONVERSION_RESPONSE=$(curl -s -X POST "https://ecourts-marker-dev.eastus.azurecontainerapps.io/api/convert" \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "conv-'$(date +%s)'",
    "pdfBlobPath": "pdfs/court-order-123.pdf",
    "cnrNumber": "DLHC010001092024", 
    "orderNumber": "12345"
  }')

echo "Conversion Result: $CONVERSION_RESPONSE"

# 3. Sign the PDF
SIGNING_RESPONSE=$(curl -s -X POST "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io/api/sign" \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "sign-'$(date +%s)'",
    "pdfBlobPath": "pdfs/court-order-123.pdf",
    "cnrNumber": "DLHC010001092024",
    "orderNumber": "12345"
  }')

echo "Signing Result: $SIGNING_RESPONSE"
```

## ⚡ Performance & Scaling

- **Auto-scaling**: APIs scale based on HTTP request load
- **Concurrent requests**: 
  - Marker API: Up to 10 concurrent requests per instance
  - PDF Signing API: Up to 5 concurrent requests per instance
- **Processing time**:
  - PDF Conversion: 30 seconds - 5 minutes (depending on PDF size)
  - PDF Signing: 10-60 seconds
- **Maximum instances**: Marker (5), PDF Signing (3)

## 🔍 Monitoring

### Health Checks
```bash
curl https://ecourts-marker-dev.eastus.azurecontainerapps.io/health
curl https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io/health
```

### View Logs
```bash
az containerapp logs show --name "ecourts-marker-dev" --resource-group "rg-ecourts-dev" --tail 20
az containerapp logs show --name "ecourts-pdfsigning-dev" --resource-group "rg-ecourts-dev" --tail 20
```

## 🎯 Integration Options

### 1. Direct API Calls
- Call APIs directly from your application
- Synchronous processing with immediate results
- Perfect for interactive applications

### 2. Batch Processing
- Send multiple requests in parallel
- Use async/await patterns for better performance
- Implement retry logic for failed requests

### 3. Webhook Integration
- APIs can be extended to support webhook callbacks
- Notify your application when processing completes
- Useful for long-running conversions

Your eCourts system is now ready for on-demand API calls! 🎉 