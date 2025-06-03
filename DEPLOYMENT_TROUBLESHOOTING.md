# eCourts Container Apps - Deployment Troubleshooting Guide

## 🚨 Common Deployment Issues & Solutions

### 1. **Docker Build Errors**

#### Issue: "COPY failed: file not found"
**Cause**: Missing shared files or incorrect path references
**Solution**:
```bash
# Ensure shared files exist
ls -la shared/
# Should contain Configuration.cs and Models.cs

# Check project references are correct
cat marker-service/eCourts-MarkerConvert.csproj
cat pdf-signing-service/eCourts-PDFSigning.csproj
```

#### Issue: "Package restore failed"
**Cause**: Network issues or incorrect package versions
**Solution**:
```bash
# Test restore locally
cd marker-service
dotnet restore
cd ../pdf-signing-service  
dotnet restore
```

### 2. **Certificate Issues**

#### Issue: "Certificate file not found"
**Cause**: Missing certificate file in pdf-signing-service directory
**Solution**:
```bash
# Place certificate file in correct location
cp your-certificate.pfx pdf-signing-service/certificate.pfx

# Or update Dockerfile to make certificate optional
```

#### Issue: "Invalid certificate password"
**Cause**: Wrong certificate password in GitHub secrets
**Solution**:
1. Verify certificate password locally
2. Update `CERTIFICATE_PASSWORD` secret in GitHub

### 3. **GitHub Actions Failures**

#### Issue: "Azure login failed"
**Cause**: Invalid Azure credentials
**Solution**:
```bash
# Generate new service principal
az ad sp create-for-rbac --name "github-eCourts" --role contributor \
  --scopes /subscriptions/{subscription-id} --sdk-auth

# Update AZURE_CREDENTIALS secret with output
```

#### Issue: "Container registry access denied"
**Cause**: Registry doesn't exist or permissions issue
**Solution**:
```bash
# Manually create registry
az acr create --resource-group rg-ecourts-dev --name ecourtscr --sku Basic
az acr update --name ecourtscr --admin-enabled true
```

### 4. **Container App Deployment Issues**

#### Issue: "Container app fails to start"
**Cause**: Usually environment variables or configuration issues
**Solution**:
```bash
# Check container app logs
az containerapp logs show --name ecourts-marker-dev --resource-group rg-ecourts-dev

# Verify environment variables
az containerapp show --name ecourts-marker-dev --resource-group rg-ecourts-dev \
  --query "properties.template.containers[0].env"
```

#### Issue: "Health check failures"
**Cause**: App not listening on correct port or /health endpoint issues
**Solution**:
```bash
# Test locally first
cd marker-service
dotnet run
curl http://localhost:8080/health

# Check port configuration in Bicep template
```

### 5. **Storage Connection Issues**

#### Issue: "Blob storage access denied"
**Cause**: Invalid connection string or container permissions
**Solution**:
```bash
# Test connection string
az storage container list --connection-string "YOUR_CONNECTION_STRING"

# Create required containers
az storage container create --name pdfs --connection-string "YOUR_CONNECTION_STRING"
az storage container create --name markdown-files --connection-string "YOUR_CONNECTION_STRING"
```

## 🔧 Pre-Deployment Checklist

### ✅ **Required Files**
- [ ] `shared/Configuration.cs` exists
- [ ] `shared/Models.cs` exists  
- [ ] `marker-service/eCourts-MarkerConvert.csproj` exists
- [ ] `marker-service/Program.cs` exists
- [ ] `marker-service/Dockerfile` exists
- [ ] `pdf-signing-service/eCourts-PDFSigning.csproj` exists
- [ ] `pdf-signing-service/Program.cs` exists
- [ ] `pdf-signing-service/Dockerfile` exists
- [ ] `pdf-signing-service/certificate.pfx` exists (or optional)
- [ ] `deployment/azure-container-apps-deployment.bicep` exists

### ✅ **GitHub Secrets**
- [ ] `AZURE_CREDENTIALS` - Service principal JSON
- [ ] `AZURE_STORAGE_CONNECTION_STRING` - Storage account connection
- [ ] `CERTIFICATE_PASSWORD` - Certificate password

### ✅ **Local Testing**
```bash
# Test marker service build
cd marker-service
dotnet build
dotnet run

# Test PDF signing service build  
cd ../pdf-signing-service
dotnet build
dotnet run

# Test Docker builds
docker build -f marker-service/Dockerfile -t marker-test .
docker build -f pdf-signing-service/Dockerfile -t pdf-test .
```

## 🐛 Debug Commands

### Check GitHub Actions Logs
1. Go to GitHub repository → Actions tab
2. Click on failed workflow run
3. Expand failed step to see detailed logs

### Check Azure Resources
```bash
# List resource groups
az group list --query "[?contains(name, 'ecourts')]"

# Check container apps
az containerapp list --resource-group rg-ecourts-dev

# Check container registry
az acr list --resource-group rg-ecourts-dev

# View container app details
az containerapp show --name ecourts-marker-dev --resource-group rg-ecourts-dev

# Get container app logs
az containerapp logs show --name ecourts-marker-dev --resource-group rg-ecourts-dev --tail 50
```

### Test API Endpoints
```bash
# Get container app URLs
MARKER_URL=$(az containerapp show --name ecourts-marker-dev --resource-group rg-ecourts-dev \
  --query "properties.configuration.ingress.fqdn" -o tsv)
PDF_URL=$(az containerapp show --name ecourts-pdfsigning-dev --resource-group rg-ecourts-dev \
  --query "properties.configuration.ingress.fqdn" -o tsv)

# Test health endpoints
curl https://$MARKER_URL/health
curl https://$PDF_URL/health

# Test API endpoints
curl -X POST https://$MARKER_URL/api/convert \
  -H "Content-Type: application/json" \
  -d '{"pdfBlobPath":"test.pdf","cnrNumber":"TEST123","orderNumber":"456"}'
```

## 🚀 Manual Deployment Steps

If GitHub Actions continue to fail, deploy manually:

```bash
# 1. Login to Azure
az login

# 2. Set variables
RESOURCE_GROUP="rg-ecourts-dev"
REGISTRY_NAME="ecourtscr"
ENVIRONMENT="dev"

# 3. Create resource group
az group create --name $RESOURCE_GROUP --location "East US"

# 4. Create container registry
az acr create --resource-group $RESOURCE_GROUP --name $REGISTRY_NAME --sku Basic --admin-enabled true

# 5. Build and push images
az acr build --registry $REGISTRY_NAME --image ecourts-markerconvert:latest -f marker-service/Dockerfile .
az acr build --registry $REGISTRY_NAME --image ecourts-pdfsigning:latest -f pdf-signing-service/Dockerfile .

# 6. Deploy with Bicep
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file deployment/azure-container-apps-deployment.bicep \
  --parameters \
    namePrefix=ecourts \
    environment=$ENVIRONMENT \
    storageConnectionString="YOUR_STORAGE_CONNECTION_STRING" \
    certificatePassword="YOUR_CERT_PASSWORD" \
    containerRegistryLoginServer="${REGISTRY_NAME}.azurecr.io" \
    containerRegistryUsername=$REGISTRY_NAME \
    containerRegistryPassword="$(az acr credential show --name $REGISTRY_NAME --query passwords[0].value -o tsv)"
```

## 📞 Getting Help

If issues persist:

1. **Check logs** in GitHub Actions and Azure Container Apps
2. **Verify all prerequisites** are met
3. **Test locally** before deploying
4. **Use manual deployment** as fallback
5. **Check Azure service health** for any platform issues

Common error patterns to look for:
- `COPY failed` → File path issues
- `login failed` → Authentication issues  
- `access denied` → Permission issues
- `container failed to start` → Runtime configuration issues
- `health check failed` → Application startup issues 