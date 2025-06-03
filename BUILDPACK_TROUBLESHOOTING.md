# Buildpack Troubleshooting Guide for eCourts Container Apps

## 🚨 Issue: Buildpack Detection Errors in GitHub Actions

**Error Pattern**:
```
Buildpack: Retrying detect phase (attempt 4) (4.7s)
(X) Fail: Buildpack: Retrying detect phase (attempt 4) (4.7s)
Starting detector...
Parsing inputs...
```

## 🔍 Root Cause Analysis

The buildpack error occurs when Azure Container Apps tries to use **source-to-cloud deployment** instead of our pre-built Docker images. This happens when:

1. **Container Registry parameters are not properly passed**
2. **Images don't exist in the registry yet**
3. **Azure Container Apps falls back to buildpack detection**
4. **Bicep template doesn't enforce container image usage**

## ✅ Solutions Implemented

### 1. **Updated GitHub Workflow Structure**

**Problem**: Single job tries to build and deploy simultaneously
**Solution**: Split into separate jobs with proper dependencies

```yaml
jobs:
  build-images:     # Build and push images first
    outputs:
      acr-server: ${{ steps.acr-creds.outputs.acr-server }}
      # ... other outputs
  
  deploy-apps:      # Deploy only after images are built
    needs: build-images
    # Uses outputs from build-images job
```

### 2. **Added Image Verification**

**Problem**: Deployment proceeds even if images aren't pushed
**Solution**: Verify images exist before deployment

```yaml
- name: Verify images were pushed
  run: |
    az acr repository show --name ${{ env.CONTAINER_REGISTRY }} --repository ecourts-markerconvert
    az acr repository show --name ${{ env.CONTAINER_REGISTRY }} --repository ecourts-pdfsigning
```

### 3. **Explicit Container Registry Configuration**

**Problem**: Registry credentials not properly passed to Bicep
**Solution**: Use job outputs and explicit parameter passing

```yaml
containerRegistryLoginServer="${{ needs.build-images.outputs.acr-server }}"
containerRegistryUsername="${{ needs.build-images.outputs.acr-username }}"
containerRegistryPassword="${{ needs.build-images.outputs.acr-password }}"
```

### 4. **Platform Specification**

**Problem**: Multi-platform builds can cause issues
**Solution**: Specify Linux platform explicitly

```yaml
platforms: linux/amd64
```

## 🛠️ Alternative Deployment Methods

### Method 1: Simple Workflow (Recommended)

Use the new **`.github/workflows/deploy-simple.yml`** which:
- Uses direct `az containerapp create` commands
- Avoids Bicep template complexity
- Ensures explicit image usage
- No buildpack detection

**Trigger**: Manual deployment via GitHub Actions UI

### Method 2: Local Docker Test

Use **`scripts/test-docker-build.ps1`** to test locally:

```powershell
# Run from project root
.\scripts\test-docker-build.ps1
```

This verifies Docker builds work before GitHub deployment.

### Method 3: Manual Azure CLI Deployment

```bash
# 1. Build images locally
docker build -f marker-service/Dockerfile -t ecourts-markerconvert .
docker build -f pdf-signing-service/Dockerfile -t ecourts-pdfsigning .

# 2. Tag and push to ACR
az acr login --name ecourtscr
docker tag ecourts-markerconvert ecourtscr.azurecr.io/ecourts-markerconvert:latest
docker tag ecourts-pdfsigning ecourtscr.azurecr.io/ecourts-pdfsigning:latest
docker push ecourtscr.azurecr.io/ecourts-markerconvert:latest
docker push ecourtscr.azurecr.io/ecourts-pdfsigning:latest

# 3. Deploy with Azure CLI (see deploy-simple.yml for commands)
```

## 🔧 Troubleshooting Steps

### 1. **Check Current GitHub Actions Failure**

1. Go to GitHub → Actions tab
2. Click failed workflow
3. Look for this error pattern:
   ```
   Buildpack: Retrying detect phase
   ```

### 2. **Verify Required Secrets**

Ensure these GitHub secrets are set:
- `AZURE_CREDENTIALS` - Service principal JSON
- `AZURE_STORAGE_CONNECTION_STRING` - Storage connection
- `CERTIFICATE_PASSWORD` - Certificate password

### 3. **Test Docker Build Locally**

```powershell
# Test marker service
docker build -f marker-service/Dockerfile -t test-marker .

# Test PDF service  
docker build -f pdf-signing-service/Dockerfile -t test-pdf .
```

### 4. **Use Simple Deployment Workflow**

1. Go to GitHub → Actions
2. Select "Deploy eCourts Container Apps (Simple)"
3. Click "Run workflow"
4. Choose environment (dev/staging/prod)
5. Monitor progress

### 5. **Check Azure Resources**

```bash
# List container apps
az containerapp list --resource-group rg-ecourts-dev

# Check app status
az containerapp show --name ecourts-marker-dev --resource-group rg-ecourts-dev

# View logs
az containerapp logs show --name ecourts-marker-dev --resource-group rg-ecourts-dev
```

## 🎯 Quick Fix Commands

### Force Container Image Usage in Existing Apps

If apps are deployed but using wrong images:

```bash
# Update marker service image
az containerapp update \
  --name ecourts-marker-dev \
  --resource-group rg-ecourts-dev \
  --image ecourtscr.azurecr.io/ecourts-markerconvert:latest

# Update PDF service image  
az containerapp update \
  --name ecourts-pdfsigning-dev \
  --resource-group rg-ecourts-dev \
  --image ecourtscr.azurecr.io/ecourts-pdfsigning:latest
```

### Clean Up and Redeploy

```bash
# Delete existing container apps
az containerapp delete --name ecourts-marker-dev --resource-group rg-ecourts-dev --yes
az containerapp delete --name ecourts-pdfsigning-dev --resource-group rg-ecourts-dev --yes

# Run simple deployment workflow
```

## 💡 Prevention Tips

1. **Always verify images are pushed** before deployment
2. **Use explicit container registry parameters** 
3. **Test Docker builds locally** before GitHub Actions
4. **Monitor GitHub Actions logs** for early error detection
5. **Use simple workflow** for reliable deployment

## 🆘 When All Else Fails

1. **Delete resource group**: `az group delete --name rg-ecourts-dev`
2. **Use simple workflow**: `.github/workflows/deploy-simple.yml`
3. **Start fresh**: Create new container registry and apps
4. **Contact support**: Check Azure Container Apps service health

The buildpack issue is now resolved with the updated workflows and alternative deployment methods! 🎉 