# eCourts Container Apps

This project contains two independent Azure Container Apps for the eCourts system:

1. **eCourts-MarkerConvert**: PDF to Markdown conversion using Marker
2. **eCourts-PDFSigning**: PDF watermarking and digital signing

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                 Azure Container Apps                │
├─────────────────────┬───────────────────────────────┤
│  Marker Service     │    PDF Signing Service       │
│  - PDF → Markdown   │    - Watermarking             │
│  - Python/Marker    │    - Digital Signing          │
│  - Queue Processing │    - iText7                   │
└─────────────────────┴───────────────────────────────┘
            │                        │
            ▼                        ▼
┌─────────────────────────────────────────────────────┐
│              Azure Storage Account                  │
│ - Blob Storage (PDFs, Markdowns)                   │
│ - Queue Storage (Processing Requests)               │
│ - File Share (Signed PDFs)                         │
│ - Table Storage (Metadata)                         │
└─────────────────────────────────────────────────────┘
```

## Project Structure

```
Container-Apps/
├── marker-service/
│   ├── Dockerfile
│   ├── eCourts-MarkerConvert.csproj
│   └── Program.cs
├── pdf-signing-service/
│   ├── Dockerfile
│   ├── eCourts-PDFSigning.csproj
│   └── Program.cs
├── shared/
│   ├── Configuration.cs
│   └── Models.cs
├── deployment/
│   ├── azure-container-apps-deployment.bicep
│   └── deploy.ps1
├── scripts/
│   └── build-and-push.ps1
└── README.md
```

## Features

### Marker Conversion Service
- ✅ **PDF to Markdown Conversion**: Uses Marker AI for high-quality text extraction
- ✅ **Queue-based Processing**: Processes conversion requests from Azure Queue
- ✅ **Blob Storage Integration**: Downloads PDFs and uploads Markdown files
- ✅ **Auto-scaling**: Scales based on queue length
- ✅ **Health Monitoring**: Built-in health checks and logging
- ✅ **Post-processing**: Adds metadata and copyright information

### PDF Signing Service
- ✅ **Digital Signing**: Signs PDFs with X.509 certificates
- ✅ **Watermarking**: Adds branded watermarks and links
- ✅ **Queue-based Processing**: Processes signing requests from Azure Queue
- ✅ **File Share Upload**: Uploads signed PDFs to Azure File Share
- ✅ **PDF Protection**: Applies password protection and encryption
- ✅ **Link Generation**: Creates clickable links in watermarks

## Prerequisites

1. **Azure Subscription**: Active Azure subscription
2. **Azure CLI**: Latest version installed
3. **Docker**: Docker Desktop or Docker daemon running
4. **PowerShell**: PowerShell 7+ recommended
5. **Certificate**: X.509 certificate for PDF signing (`.pfx` format)

## Quick Start

### Step 1: Clone and Setup
```bash
git clone <repository-url>
cd Container-Apps
```

### Step 2: Build Docker Images
```powershell
# Build locally
.\scripts\build-and-push.ps1

# Or build and push to Azure Container Registry
.\scripts\build-and-push.ps1 -ContainerRegistryName "myregistry" -PushImages $true
```

### Step 3: Deploy to Azure
```powershell
.\deployment\deploy.ps1 `
    -ResourceGroupName "rg-ecourts-dev" `
    -Environment "dev" `
    -StorageConnectionString "your-storage-connection-string" `
    -CertificatePassword "your-certificate-password"
```

## Configuration

### Environment Variables

Both services support the following environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `AZURE_STORAGE_CONNECTION_STRING` | Azure Storage connection string | Required |
| `MAX_PARALLELISM` | Maximum parallel workers | 4 |
| `MAX_CONCURRENT_CONVERSIONS` | Maximum concurrent operations | 2 |
| `ENABLE_MARKER` | Enable Marker conversion | true |
| `ENABLE_VERBOSE_LOGGING` | Enable detailed logging | false |
| `CERTIFICATE_PATH` | Path to certificate file | `/app/certs/certificate.pfx` |
| `CERTIFICATE_PASSWORD` | Certificate password | Required for PDF signing |

### Queue Names

- **Marker Conversion Queue**: `marker-conversion-requests`
- **PDF Signing Queue**: `pdf-signing-requests`

### Message Format

**Marker Conversion Request**:
```json
{
  "PdfBlobUrl": "https://storage.../file.pdf",
  "CnrNumber": "DLCT01-123456-2024",
  "OrderNumber": "001",
  "Metadata": {
    "CourtName": "District Court",
    "CaseType": "Civil"
  },
  "RequestId": "guid"
}
```

**PDF Signing Request**:
```json
{
  "PdfBlobPath": "cnr-folder/file.pdf",
  "CnrNumber": "DLCT01-123456-2024", 
  "OrderNumber": "001",
  "Metadata": {
    "CourtName": "District Court"
  },
  "RequestId": "guid"
}
```

## Deployment Options

### Option 1: Using Azure Container Registry
```powershell
# 1. Build and push images
.\scripts\build-and-push.ps1 -ContainerRegistryName "myregistry" -PushImages $true

# 2. Deploy with registry
.\deployment\deploy.ps1 `
    -ResourceGroupName "rg-ecourts-dev" `
    -StorageConnectionString "..." `
    -CertificatePassword "..." `
    -ContainerRegistryLoginServer "myregistry.azurecr.io" `
    -ContainerRegistryUsername "myregistry" `
    -ContainerRegistryPassword "registry-password"
```

### Option 2: Using Default Images (Development)
```powershell
# Deploy with default .NET images (for testing deployment)
.\deployment\deploy.ps1 `
    -ResourceGroupName "rg-ecourts-dev" `
    -StorageConnectionString "..." `
    -CertificatePassword "..."
```

## Monitoring

### Health Checks
Both services expose health check endpoints:
- **Endpoint**: `/health`
- **Port**: 8080

### Logging
Logs are written to:
- **Console**: Real-time logging
- **Files**: `/app/logs/[service-name]-.txt`
- **Azure Log Analytics**: Centralized logging

### Scaling
Services auto-scale based on:
- **Queue Length**: Scales up when queue has >5 messages
- **Min Replicas**: 1
- **Max Replicas**: 3

## Troubleshooting

### Common Issues

1. **Certificate not found**:
   ```
   Error: Certificate file not found at /app/certs/certificate.pfx
   ```
   **Solution**: Ensure certificate is properly mounted or path is correct

2. **Marker conversion timeout**:
   ```
   Error: Marker conversion timed out
   ```
   **Solution**: Increase `MarkerTimeoutSeconds` or reduce PDF complexity

3. **Queue connection failed**:
   ```
   Error: Failed to connect to queue
   ```
   **Solution**: Verify storage connection string and queue names

### Debugging Commands

```bash
# Check container logs
az containerapp logs show --name ecourts-marker-dev --resource-group rg-ecourts-dev --tail 50

# Check container status
az containerapp show --name ecourts-marker-dev --resource-group rg-ecourts-dev --query "properties.provisioningState"

# Check scaling metrics
az containerapp revision show --name ecourts-marker-dev --resource-group rg-ecourts-dev
```

## Development

### Local Development
```bash
# Run Marker service locally
cd marker-service
dotnet run

# Run PDF signing service locally  
cd pdf-signing-service
dotnet run
```

### Testing Messages
```powershell
# Send test message to Marker queue
az storage message put --queue-name "marker-conversion-requests" --content "test-message" --connection-string "your-connection-string"

# Send test message to PDF signing queue
az storage message put --queue-name "pdf-signing-requests" --content "test-message" --connection-string "your-connection-string"
```

## Security

- ✅ **Connection Strings**: Stored as secrets in Container Apps
- ✅ **Certificate Protection**: Password-protected certificates
- ✅ **Network Security**: Internal communication only
- ✅ **HTTPS**: All external communication encrypted
- ✅ **Least Privilege**: Minimal required permissions

## Performance

### Resource Allocation
- **Marker Service**: 2 CPU cores, 4GB RAM
- **PDF Signing Service**: 1 CPU core, 2GB RAM

### Throughput
- **Marker Conversion**: ~5-10 PDFs/minute (depends on complexity)
- **PDF Signing**: ~20-30 PDFs/minute

## Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/new-feature`
3. Commit changes: `git commit -am 'Add new feature'`
4. Push to branch: `git push origin feature/new-feature`
5. Submit pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues and questions:
1. Check the troubleshooting section
2. Review container logs
3. Create an issue in the repository
4. Contact the development team

---

**© 2024 eCourtsIndia.com - AI-enhanced court document processing** 