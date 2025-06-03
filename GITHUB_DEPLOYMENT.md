# GitHub Actions Deployment for eCourts Container Apps

This guide explains how to set up automated deployment of your eCourts Container Apps using GitHub Actions.

## 🚀 Quick Setup

### 1. Create Azure Service Principal

First, create a service principal for GitHub Actions to authenticate with Azure:

```bash
# Login to Azure
az login

# Create service principal (replace with your subscription ID)
az ad sp create-for-rbac --name "github-actions-ecourts" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```

**Important**: Copy the entire JSON output - you'll need it for GitHub secrets.

### 2. Set Up GitHub Repository Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions, then add these secrets:

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `AZURE_CREDENTIALS` | Service principal JSON from step 1 | `{"clientId": "...", "clientSecret": "...", ...}` |
| `AZURE_STORAGE_CONNECTION_STRING` | Azure Storage connection string | `DefaultEndpointsProtocol=https;AccountName=...` |
| `CERTIFICATE_PASSWORD` | Certificate password | `LetsImproveLaw` |

### 3. Push Your Code

```bash
# Make sure your certificate is in place
cp /path/to/your/certificate.pfx pdf-signing-service/certificate.pfx

# Commit and push
git add .
git commit -m "Add eCourts Container Apps with GitHub Actions deployment"
git push origin main
```

## 🎯 How It Works

### Deployment Triggers

The workflow runs on:
- **Push to main/master**: Automatic deployment to dev environment
- **Pull Request**: Build validation (no deployment)
- **Manual Trigger**: Deploy to any environment (dev/staging/prod)

### What Gets Deployed

1. **Azure Container Registry**: Automatically created if it doesn't exist
2. **Docker Images**: Built and pushed for both services
   - `ecourts-markerconvert:latest`
   - `ecourts-pdfsigning:latest`
3. **Container Apps**: Deployed with auto-scaling and health checks
4. **Infrastructure**: Log Analytics, Container App Environment

### Environment-Specific Deployments

- **Dev**: `rg-ecourts-dev`, `ecourtscr dev`
- **Staging**: `rg-ecourts-staging`, `ecourtscrstaging`
- **Prod**: `rg-ecourts-prod`, `ecourtscr prod`

## 🔧 Manual Deployment

### Deploy to Specific Environment

1. Go to Actions tab in your GitHub repository
2. Click "Deploy eCourts Container Apps"
3. Click "Run workflow"
4. Select environment (dev/staging/prod)
5. Click "Run workflow"

### Monitor Deployment

The workflow provides:
- ✅ Build status for each service
- 🔍 Deployment verification
- 🏥 Health check results
- 📋 Deployment summary with service URLs

## 🛠️ Customization

### Modify Environments

Edit `.github/workflows/deploy-container-apps.yml`:

```yaml
env:
  AZURE_RESOURCE_GROUP: rg-ecourts-${{ github.event.inputs.environment || 'dev' }}
  AZURE_LOCATION: 'East US'  # Change region here
  CONTAINER_REGISTRY: ecourtscr${{ github.event.inputs.environment || 'dev' }}
```

### Add New Environments

Add to the workflow inputs:

```yaml
workflow_dispatch:
  inputs:
    environment:
      type: choice
      options:
        - dev
        - staging
        - prod
        - testing  # Add new environment
```

### Modify Resource Sizing

Edit `deployment/azure-container-apps-deployment.bicep`:

```bicep
resources: {
  cpu: json('2.0')     # Increase CPU
  memory: '4Gi'        # Increase memory
}
```

## 📊 Monitoring & Troubleshooting

### View Logs

```bash
# Get recent logs from deployed services
az containerapp logs show --name "ecourts-marker-dev" --resource-group "rg-ecourts-dev" --tail 50

az containerapp logs show --name "ecourts-pdfsigning-dev" --resource-group "rg-ecourts-dev" --tail 50
```

### Check Service Health

The workflow automatically tests health endpoints, but you can check manually:

```bash
# Get service URLs
az containerapp show --name "ecourts-marker-dev" --resource-group "rg-ecourts-dev" --query "properties.configuration.ingress.fqdn"

# Test health endpoint
curl https://your-service-url/health
```

### Common Issues

1. **Certificate not found**: Ensure `certificate.pfx` is in `pdf-signing-service/` folder
2. **Storage connection**: Verify `AZURE_STORAGE_CONNECTION_STRING` secret is correct
3. **Permissions**: Service principal needs Contributor role on subscription
4. **Registry naming**: Container registry names must be globally unique

## 🔐 Security Best Practices

### Certificate Security

- ✅ Certificate is embedded in Docker image (not in source code)
- ✅ Password passed via secure environment variables
- ✅ `.gitignore` excludes `*.pfx` files

### Secrets Management

- ✅ All sensitive data stored in GitHub Secrets
- ✅ Service principal with minimal required permissions
- ✅ Secrets are masked in logs

### Network Security

- ✅ Container Apps use internal networking
- ✅ Only health endpoints exposed externally
- ✅ Queue-based processing (no direct HTTP access)

## 🎉 Success!

Once deployed, your services will:

1. **Auto-scale** based on queue message count
2. **Process queues** independently:
   - `marker-conversion-requests` → PDF to Markdown
   - `pdf-signing-requests` → PDF signing and watermarking
3. **Provide health endpoints** for monitoring
4. **Log to Azure Monitor** for troubleshooting

Your eCourts system is now fully automated with GitHub Actions! 🚀 