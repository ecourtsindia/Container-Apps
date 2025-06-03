# How to Use eCourts Container Apps

This guide shows you exactly how to send requests for PDF conversion and signing to your deployed eCourts Container Apps.

## 🏗️ System Architecture

```
You → Azure Storage Queue → Container App → Azure Storage (Results)
```

The system processes requests asynchronously using Azure Storage Queues:

1. **You send** JSON messages to queues
2. **Container Apps** automatically pick up and process messages
3. **Results** are stored in Azure Storage (blobs/file shares)

## 📥 Queue Names

- **`marker-conversion-requests`** → PDF to Markdown conversion
- **`pdf-signing-requests`** → PDF signing and watermarking

## 🔧 Method 1: Using Azure CLI

### Install Azure CLI
```bash
# Windows
winget install Microsoft.AzureCLI

# macOS
brew install azure-cli

# Login
az login
```

### Send PDF Conversion Request
```bash
# Convert PDF to Markdown
az storage message put \
  --queue-name "marker-conversion-requests" \
  --content '{
    "RequestId": "req-001",
    "PdfBlobUrl": "https://yourstorageaccount.blob.core.windows.net/pdfs/document.pdf",
    "CnrNumber": "DLHC010001092024",
    "OrderNumber": "12345",
    "ClientDomain": "ecourts.gov.in",
    "ConversionType": "marker"
  }' \
  --connection-string "YOUR_STORAGE_CONNECTION_STRING"
```

### Send PDF Signing Request
```bash
# Sign and watermark PDF
az storage message put \
  --queue-name "pdf-signing-requests" \
  --content '{
    "RequestId": "req-002", 
    "PdfBlobPath": "pdfs/document.pdf",
    "CnrNumber": "DLHC010001092024",
    "OrderNumber": "12345"
  }' \
  --connection-string "YOUR_STORAGE_CONNECTION_STRING"
```

## 🔧 Method 2: Using PowerShell

### Install Azure PowerShell
```powershell
Install-Module -Name Az -AllowClobber -Scope CurrentUser
Connect-AzAccount
```

### Send Requests
```powershell
# PDF Conversion Request
$conversionMessage = @{
    RequestId = "req-001"
    PdfBlobUrl = "https://yourstorageaccount.blob.core.windows.net/pdfs/document.pdf"
    CnrNumber = "DLHC010001092024"
    OrderNumber = "12345"
    ClientDomain = "ecourts.gov.in"
    ConversionType = "marker"
} | ConvertTo-Json

$ctx = New-AzStorageContext -ConnectionString "YOUR_STORAGE_CONNECTION_STRING"
$queue = Get-AzStorageQueue -Name "marker-conversion-requests" -Context $ctx
$queue.CloudQueue.AddMessageAsync($conversionMessage)

# PDF Signing Request
$signingMessage = @{
    RequestId = "req-002"
    PdfBlobPath = "pdfs/document.pdf"
    CnrNumber = "DLHC010001092024"
    OrderNumber = "12345"
} | ConvertTo-Json

$signingQueue = Get-AzStorageQueue -Name "pdf-signing-requests" -Context $ctx
$signingQueue.CloudQueue.AddMessageAsync($signingMessage)
```

## 🔧 Method 3: Using C# Code

```csharp
using Azure.Storage.Queues;
using Newtonsoft.Json;

// Connection
var queueClient = new QueueClient("YOUR_CONNECTION_STRING", "marker-conversion-requests");

// PDF Conversion Request
var conversionRequest = new
{
    RequestId = "req-001",
    PdfBlobUrl = "https://yourstorageaccount.blob.core.windows.net/pdfs/document.pdf",
    CnrNumber = "DLHC010001092024",
    OrderNumber = "12345",
    ClientDomain = "ecourts.gov.in",
    ConversionType = "marker"
};

await queueClient.SendMessageAsync(JsonConvert.SerializeObject(conversionRequest));

// PDF Signing Request
var signingClient = new QueueClient("YOUR_CONNECTION_STRING", "pdf-signing-requests");
var signingRequest = new
{
    RequestId = "req-002",
    PdfBlobPath = "pdfs/document.pdf",
    CnrNumber = "DLHC010001092024",
    OrderNumber = "12345"
};

await signingClient.SendMessageAsync(JsonConvert.SerializeObject(signingRequest));
```

## 🔧 Method 4: Using REST API

### Direct HTTP to Azure Storage Queue
```bash
# Get SAS token first, then:
curl -X POST \
  "https://yourstorageaccount.queue.core.windows.net/marker-conversion-requests/messages?YOUR_SAS_TOKEN" \
  -H "Content-Type: application/xml" \
  -d '<QueueMessage>
    <MessageText>{
      "RequestId": "req-001",
      "PdfBlobUrl": "https://yourstorageaccount.blob.core.windows.net/pdfs/document.pdf", 
      "CnrNumber": "DLHC010001092024",
      "OrderNumber": "12345",
      "ClientDomain": "ecourts.gov.in",
      "ConversionType": "marker"
    }</MessageText>
  </QueueMessage>'
```

## 📝 Request Format Examples

### PDF Conversion Request
```json
{
  "RequestId": "conv-001-2024",
  "PdfBlobUrl": "https://yourstorageaccount.blob.core.windows.net/pdfs/court-order-123.pdf",
  "CnrNumber": "DLHC010001092024", 
  "OrderNumber": "12345",
  "ClientDomain": "ecourts.gov.in",
  "ConversionType": "marker",
  "EnableOcr": true,
  "OcrLanguages": ["hin", "eng"]
}
```

### PDF Signing Request
```json
{
  "RequestId": "sign-001-2024",
  "PdfBlobPath": "pdfs/court-order-123.pdf",
  "CnrNumber": "DLHC010001092024",
  "OrderNumber": "12345"
}
```

## 📤 Where Results Go

### PDF Conversion Results
- **Markdown File**: `markdown-files/{CnrNumber}-orderno-{OrderNumber}.md`
- **Blob Container**: `markdown-files`
- **Access URL**: `https://yourstorageaccount.blob.core.windows.net/markdown-files/{filename}`

### PDF Signing Results  
- **Signed PDF**: `{CnrNumber}-orderno-{OrderNumber}.pdf`
- **File Share**: Your configured file share
- **Access URL**: `https://truecopy.ecourtsindia.com/{filename}`

## 🔍 Monitoring & Checking Results

### Check Queue Status
```bash
# See how many messages are pending
az storage queue metadata show \
  --name "marker-conversion-requests" \
  --connection-string "YOUR_CONNECTION_STRING"
```

### Check Container App Logs
```bash
# View processing logs
az containerapp logs show \
  --name "ecourts-marker-dev" \
  --resource-group "rg-ecourts-dev" \
  --tail 50
```

### Check if Files Were Created
```bash
# List markdown files
az storage blob list \
  --container-name "markdown-files" \
  --connection-string "YOUR_CONNECTION_STRING"

# List signed PDFs  
az storage file list \
  --share-name "your-file-share" \
  --connection-string "YOUR_CONNECTION_STRING"
```

## 🚀 Example Workflow

### Complete Example: Convert and Sign a PDF

```bash
# 1. Upload your PDF to blob storage
az storage blob upload \
  --file "my-court-order.pdf" \
  --container-name "pdfs" \
  --name "court-order-123.pdf" \
  --connection-string "YOUR_CONNECTION_STRING"

# 2. Request conversion to markdown
az storage message put \
  --queue-name "marker-conversion-requests" \
  --content '{
    "RequestId": "conv-123",
    "PdfBlobUrl": "https://yourstorageaccount.blob.core.windows.net/pdfs/court-order-123.pdf",
    "CnrNumber": "DLHC010001092024",
    "OrderNumber": "12345",
    "ClientDomain": "ecourts.gov.in", 
    "ConversionType": "marker"
  }' \
  --connection-string "YOUR_CONNECTION_STRING"

# 3. Request PDF signing
az storage message put \
  --queue-name "pdf-signing-requests" \
  --content '{
    "RequestId": "sign-123",
    "PdfBlobPath": "pdfs/court-order-123.pdf",
    "CnrNumber": "DLHC010001092024", 
    "OrderNumber": "12345"
  }' \
  --connection-string "YOUR_CONNECTION_STRING"

# 4. Wait a few minutes, then check results
az storage blob list --container-name "markdown-files" --connection-string "YOUR_CONNECTION_STRING"
az storage file list --share-name "your-file-share" --connection-string "YOUR_CONNECTION_STRING"
```

## ⚡ Auto-Scaling

The Container Apps automatically scale based on queue length:
- **No messages**: Scales down to minimum replicas (1)
- **5+ messages**: Scales up (up to 3 replicas)
- **Processing**: Each app processes messages in parallel

## 🔄 Processing Flow

1. **You** → Send JSON message to queue
2. **Container App** → Picks up message within seconds
3. **Processing** → Downloads PDF, processes it
4. **Results** → Uploads result to storage
5. **Cleanup** → Deletes queue message when done

Your eCourts system is now ready to process PDFs at scale! 🎉 