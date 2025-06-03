# eCourts Container Apps - Deployment Fixes Applied

## 🎯 Issues Fixed

### 1. **Project File Structure**
**Problem**: Project files had incorrect namespace and missing shared references.

**Fix Applied**:
- Updated `RootNamespace` in both project files to use proper naming (`eCourts.MarkerConvert`, `eCourts.PDFSigning`)
- Added proper shared file references in project files:
  ```xml
  <ItemGroup>
    <Compile Include="../shared/Configuration.cs" Link="Shared/Configuration.cs" />
    <Compile Include="../shared/Models.cs" Link="Shared/Models.cs" />
  </ItemGroup>
  ```
- Removed unnecessary queue-related packages

### 2. **Docker Build Context**
**Problem**: Dockerfiles couldn't access shared files due to incorrect build context.

**Fix Applied**:
- Updated GitHub workflow to use root context: `context: .` instead of `context: ./marker-service`
- Fixed Dockerfiles to copy from correct paths:
  ```dockerfile
  # Copy shared files first
  COPY ["shared/", "shared/"]
  
  # Copy service files
  COPY ["marker-service/eCourts-MarkerConvert.csproj", "marker-service/"]
  ```

### 3. **Service Registration Issues**
**Problem**: Incorrect dependency injection syntax for named services.

**Fix Applied** (Marker Service):
```csharp
// Old (broken)
builder.Services.AddSingleton<BlobContainerClient>("MarkdownBlobClient", provider => ...)

// New (working)
builder.Services.AddSingleton(provider =>
    new BlobContainerClient(Configuration.ConnectionString, Configuration.PdfContainerName));
```

### 4. **Missing Configuration Property**
**Problem**: `Configuration.MarkdownContainerName` was referenced but didn't exist.

**Fix Applied**:
Added to `shared/Configuration.cs`:
```csharp
public static readonly string MarkdownContainerName = "markdown-files";
```

### 5. **Namespace Conflicts**
**Problem**: PDF signing service had conflicts between `iText.Kernel.Geom.Path` and `System.IO.Path`.

**Fix Applied**:
```csharp
// Explicit namespace qualification
var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"signing_{request.RequestId}");
```

### 6. **Certificate File Handling**
**Problem**: Dockerfile would fail if certificate file didn't exist.

**Fix Applied**:
```dockerfile
# Copy certificate file if it exists (using shell to avoid Docker build failure)
COPY ["pdf-signing-service/", "/tmp/pdf-service-files/"]
RUN if [ -f /tmp/pdf-service-files/certificate.pfx ]; then \
      cp /tmp/pdf-service-files/certificate.pfx /app/certs/certificate.pfx; \
    fi && \
    rm -rf /tmp/pdf-service-files
```

### 7. **GitHub Workflow Updates**
**Problem**: Workflow referenced old queue-based functionality.

**Fix Applied**:
- Updated deployment summary to show HTTP API endpoints instead of queue names
- Fixed service descriptions to mention API functionality

## ✅ Verification Results

### Build Tests ✅
```bash
# Marker Service
cd marker-service
dotnet build  # ✅ Success

# PDF Signing Service  
cd pdf-signing-service
dotnet build  # ✅ Success
```

### Key Files Updated ✅
- `marker-service/eCourts-MarkerConvert.csproj` - Fixed project references
- `pdf-signing-service/eCourts-PDFSigning.csproj` - Fixed project references
- `marker-service/Program.cs` - Fixed service registration
- `pdf-signing-service/Program.cs` - Fixed namespace conflicts
- `shared/Configuration.cs` - Added MarkdownContainerName
- `marker-service/Dockerfile` - Fixed build context and copy paths
- `pdf-signing-service/Dockerfile` - Fixed certificate handling
- `.github/workflows/deploy-container-apps.yml` - Fixed build context and descriptions

## 🚀 Deployment Ready

The eCourts Container Apps are now ready for GitHub Actions deployment with:

### ✅ **Requirements Met**
- [x] Both services build successfully
- [x] Docker contexts configured correctly
- [x] Shared files properly referenced
- [x] Certificate handling implemented
- [x] Service registration fixed
- [x] Namespace conflicts resolved
- [x] GitHub workflow updated for HTTP APIs

### 📋 **Prerequisites for Deployment**
1. **GitHub Secrets Required**:
   - `AZURE_CREDENTIALS` - Azure service principal JSON
   - `AZURE_STORAGE_CONNECTION_STRING` - Storage account connection
   - `CERTIFICATE_PASSWORD` - PDF signing certificate password

2. **Azure Resources Created**:
   - Storage containers: `district-courts-cnr-master`, `markdown-files`
   - File share: `ecourtsndiadocs`

3. **Certificate File** (Optional):
   - Place `certificate.pfx` in `pdf-signing-service/` directory
   - Or deploy without certificate (will show warning but won't fail)

## 🎯 Next Steps

1. **Push changes to GitHub** - All fixes are applied
2. **Set up GitHub secrets** - Add required Azure credentials
3. **Trigger deployment** - Use GitHub Actions workflow
4. **Test APIs** - Verify endpoints work after deployment

The system will deploy as HTTP REST APIs for on-demand PDF conversion and signing! 