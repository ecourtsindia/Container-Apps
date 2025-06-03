# eCourts Container Apps - Quick Integration Guide

## 🎯 What These APIs Do

### 1. **Marker Conversion API** 
Converts PDF documents to Markdown using AI-powered Marker library with OCR support.

### 2. **PDF Signing API**
Applies watermarks and digitally signs PDFs with X.509 certificates for legal verification.

## 🚀 Quick Start

```bash
# Convert PDF to Markdown
curl -X POST "https://ecourts-marker-dev.eastus.azurecontainerapps.io/api/convert" \
  -H "Content-Type: application/json" \
  -d '{
    "pdfBlobPath": "pdfs/document.pdf",
    "cnrNumber": "DLHC010001092024", 
    "orderNumber": "12345"
  }'

# Sign PDF  
curl -X POST "https://ecourts-pdfsigning-dev.eastus.azurecontainerapps.io/api/sign" \
  -H "Content-Type: application/json" \
  -d '{
    "pdfBlobPath": "pdfs/document.pdf",
    "cnrNumber": "DLHC010001092024",
    "orderNumber": "12345"
  }'
```

## 📋 Key API Endpoints

| Service | Endpoint | Purpose |
|---------|----------|---------|
| Marker | `POST /api/convert` | Convert PDF from blob storage |
| Marker | `POST /api/convert-from-url` | Convert PDF from any URL |
| Signing | `POST /api/sign` | Sign PDF from blob storage |
| Signing | `POST /api/sign-from-url` | Sign PDF from any URL |
| Both | `GET /api/status/{id}` | Check processing status |
| Both | `GET /health` | Health check |

## 💡 Integration Options

### Option 1: Direct HTTP Calls
Use any HTTP client to call the APIs directly.

### Option 2: Ready-Made Client Libraries
Use the provided C#, Python, or JavaScript client classes from `ECOURTS_API_SUBMISSION.md`.

### Option 3: Language-Specific Examples
Copy the complete integration examples for your preferred language.

## 🔑 Required Parameters

### Conversion Request
```json
{
  "pdfBlobPath": "pdfs/document.pdf",    // OR pdfUrl for URL endpoint
  "cnrNumber": "DLHC010001092024",       // Court CNR number
  "orderNumber": "12345"                 // Court order number
}
```

### Signing Request  
```json
{
  "pdfBlobPath": "pdfs/document.pdf",    // OR pdfUrl for URL endpoint  
  "cnrNumber": "DLHC010001092024",       // Court CNR number
  "orderNumber": "12345"                 // Court order number
}
```

## 📤 Response Format

### Conversion Response
```json
{
  "requestId": "conv-12345",
  "status": "Completed",
  "markdownUrl": "https://storage.blob.core.windows.net/markdown/file.md",
  "markdownContent": "# Document content...",
  "processingTime": "00:02:45"
}
```

### Signing Response
```json
{
  "requestId": "sign-12345", 
  "status": "Completed",
  "signedPdfUrl": "https://truecopy.ecourtsindia.com/file.pdf",
  "processingTime": "00:01:30"
}
```

## ⚡ Performance

- **Marker Service**: 30 seconds - 5 minutes per conversion
- **PDF Signing**: 10-60 seconds per document
- **Auto-scaling**: Handles multiple concurrent requests
- **Availability**: 99.9% uptime with health monitoring

## 🔒 Security & Compliance

- **Digital Signatures**: X.509 certificate-based signing
- **Watermarking**: Legal verification URLs embedded
- **Encryption**: Password-protected output PDFs
- **Audit Trail**: CNR/Order number tracking
- **Compliance**: Suitable for legal document processing

## 📚 Complete Documentation

For full implementation details, client libraries, error handling, and advanced features, see `ECOURTS_API_SUBMISSION.md`.

---

**Ready to integrate?** The APIs are production-ready and can handle court document processing at scale. 