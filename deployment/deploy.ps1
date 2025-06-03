#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Deploy eCourts Container Apps to Azure

.DESCRIPTION
    This script deploys the eCourts Marker Conversion and PDF Signing services as Azure Container Apps.

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group

.PARAMETER Location
    The Azure region where resources will be deployed

.PARAMETER Environment
    The environment name (dev, staging, prod)

.PARAMETER StorageConnectionString
    The Azure Storage connection string

.PARAMETER CertificatePassword
    The certificate password for PDF signing

.PARAMETER ContainerRegistryLoginServer
    Container registry login server (optional)

.PARAMETER ContainerRegistryUsername
    Container registry username (optional)

.PARAMETER ContainerRegistryPassword
    Container registry password (optional)

.EXAMPLE
    .\deploy.ps1 -ResourceGroupName "rg-ecourts-dev" -Location "East US" -Environment "dev" -StorageConnectionString "..." -CertificatePassword "..."
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory = $true)]
    [string]$StorageConnectionString,
    
    [Parameter(Mandatory = $true)]
    [string]$CertificatePassword,
    
    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistryLoginServer = "",
    
    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistryUsername = "",
    
    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistryPassword = ""
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting eCourts Container Apps deployment..." -ForegroundColor Green
Write-Host "📋 Configuration:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  Environment: $Environment" -ForegroundColor White
$registryText = if ($ContainerRegistryLoginServer) { $ContainerRegistryLoginServer } else { "Not specified" }
Write-Host "  Container Registry: $registryText" -ForegroundColor White

# Check if Azure CLI is installed
try {
    $azVersion = az --version
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI not found"
    }
    Write-Host "✅ Azure CLI is installed" -ForegroundColor Green
}
catch {
    Write-Error "❌ Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if logged in to Azure
try {
    $account = az account show --output json | ConvertFrom-Json
    if (-not $account) {
        throw "Not logged in"
    }
    Write-Host "✅ Logged in to Azure as: $($account.user.name)" -ForegroundColor Green
    Write-Host "  Subscription: $($account.name) ($($account.id))" -ForegroundColor White
}
catch {
    Write-Error "❌ Not logged in to Azure. Run 'az login' first."
    exit 1
}

# Check if Container Apps extension is installed
try {
    $extensions = az extension list --output json | ConvertFrom-Json
    $containerAppExtension = $extensions | Where-Object { $_.name -eq "containerapp" }
    
    if (-not $containerAppExtension) {
        Write-Host "⚙️ Installing Azure Container Apps extension..." -ForegroundColor Yellow
        az extension add --name containerapp --upgrade
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install Container Apps extension"
        }
    }
    Write-Host "✅ Container Apps extension is available" -ForegroundColor Green
}
catch {
    Write-Error "❌ Failed to setup Container Apps extension: $_"
    exit 1
}

# Create or check resource group
Write-Host "🏗️ Checking resource group..." -ForegroundColor Yellow
try {
    $rg = az group show --name $ResourceGroupName --output json 2>$null | ConvertFrom-Json
    if ($rg) {
        Write-Host "✅ Resource group '$ResourceGroupName' exists" -ForegroundColor Green
    }
}
catch {
    Write-Host "⚙️ Creating resource group '$ResourceGroupName'..." -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Failed to create resource group"
        exit 1
    }
    Write-Host "✅ Resource group created successfully" -ForegroundColor Green
}

# Prepare deployment parameters
$deploymentParams = @{
    namePrefix = "ecourts"
    environment = $Environment
    storageConnectionString = $StorageConnectionString
    certificatePassword = $CertificatePassword
}

if ($ContainerRegistryLoginServer) {
    $deploymentParams["containerRegistryLoginServer"] = $ContainerRegistryLoginServer
}
if ($ContainerRegistryUsername) {
    $deploymentParams["containerRegistryUsername"] = $ContainerRegistryUsername
}
if ($ContainerRegistryPassword) {
    $deploymentParams["containerRegistryPassword"] = $ContainerRegistryPassword
}

# Convert parameters to JSON for Azure CLI
$parametersJson = $deploymentParams | ConvertTo-Json -Compress

# Deploy the Bicep template
Write-Host "🚀 Deploying Container Apps infrastructure..." -ForegroundColor Yellow
$deploymentName = "ecourts-containerapp-deployment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

try {
    $deployment = az deployment group create `
        --resource-group $ResourceGroupName `
        --template-file "azure-container-apps-deployment.bicep" `
        --parameters $parametersJson `
        --name $deploymentName `
        --output json | ConvertFrom-Json
    
    if ($LASTEXITCODE -ne 0) {
        throw "Deployment failed"
    }
    
    Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
    
    # Display deployment outputs
    if ($deployment.properties.outputs) {
        Write-Host "📄 Deployment Outputs:" -ForegroundColor Yellow
        $deployment.properties.outputs.PSObject.Properties | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Value.value)" -ForegroundColor White
        }
    }
}
catch {
    Write-Error "❌ Deployment failed: $_"
    
    # Get deployment error details
    Write-Host "🔍 Getting deployment error details..." -ForegroundColor Yellow
    az deployment group show --resource-group $ResourceGroupName --name $deploymentName --query "properties.error" --output table
    exit 1
}

# Verify the deployment
Write-Host "🔍 Verifying Container Apps deployment..." -ForegroundColor Yellow

try {
    $markerApp = az containerapp show --name "ecourts-marker-$Environment" --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $pdfSigningApp = az containerapp show --name "ecourts-pdfsigning-$Environment" --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    Write-Host "✅ Container Apps verification:" -ForegroundColor Green
    Write-Host "  Marker Service: $($markerApp.properties.provisioningState)" -ForegroundColor White
    Write-Host "  PDF Signing Service: $($pdfSigningApp.properties.provisioningState)" -ForegroundColor White
}
catch {
    Write-Warning "⚠️ Could not verify all Container Apps status"
}

Write-Host "🎉 Deployment completed successfully!" -ForegroundColor Green
Write-Host "📝 Next steps:" -ForegroundColor Yellow
Write-Host "  1. Make sure your certificate.pfx file is in the pdf-signing-service folder before building the Docker image" -ForegroundColor White
Write-Host "  2. Test the services by sending messages to the respective queues" -ForegroundColor White
Write-Host "  3. Monitor the applications through Azure portal" -ForegroundColor White

# Certificate information
Write-Host "🔐 Certificate Setup Information:" -ForegroundColor Yellow
Write-Host "  Place your certificate file as: pdf-signing-service/certificate.pfx" -ForegroundColor White
Write-Host "  Container Path: /app/certs/certificate.pfx" -ForegroundColor White
Write-Host "  Password: Set via CERTIFICATE_PASSWORD environment variable" -ForegroundColor White

# Optional: Display container logs
$showLogs = Read-Host "Do you want to see recent logs from the services? (y/N)"
if ($showLogs -eq "y" -or $showLogs -eq "Y") {
    Write-Host "📋 Recent logs from Marker service:" -ForegroundColor Yellow
    az containerapp logs show --name "ecourts-marker-$Environment" --resource-group $ResourceGroupName --tail 20 --follow $false
    
    Write-Host "📋 Recent logs from PDF Signing service:" -ForegroundColor Yellow
    az containerapp logs show --name "ecourts-pdfsigning-$Environment" --resource-group $ResourceGroupName --tail 20 --follow $false
}