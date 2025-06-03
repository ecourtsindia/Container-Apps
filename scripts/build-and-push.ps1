#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build and push Docker images for eCourts Container Apps

.DESCRIPTION
    This script builds Docker images for both the Marker Conversion and PDF Signing services
    and optionally pushes them to Azure Container Registry.

.PARAMETER ContainerRegistryName
    The name of the Azure Container Registry

.PARAMETER PushImages
    Whether to push images to the registry (default: false)

.PARAMETER TagVersion
    The version tag for the images (default: latest)

.EXAMPLE
    .\build-and-push.ps1 -ContainerRegistryName "myregistry" -PushImages $true -TagVersion "v1.0.0"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistryName = "",
    
    [Parameter(Mandatory = $false)]
    [bool]$PushImages = $false,
    
    [Parameter(Mandatory = $false)]
    [string]$TagVersion = "latest"
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🐳 Building eCourts Container Images..." -ForegroundColor Green
Write-Host "📋 Configuration:" -ForegroundColor Yellow
$registryDisplay = if ($ContainerRegistryName) { "$ContainerRegistryName.azurecr.io" } else { "Local only" }
Write-Host "  Registry: $registryDisplay" -ForegroundColor White
Write-Host "  Push Images: $PushImages" -ForegroundColor White
Write-Host "  Tag Version: $TagVersion" -ForegroundColor White

# Check if Docker is running
try {
    docker version > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker not running"
    }
    Write-Host "✅ Docker is running" -ForegroundColor Green
}
catch {
    Write-Error "❌ Docker is not running. Please start Docker Desktop or Docker daemon."
    exit 1
}

# Navigate to project root
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot

try {
    # Build Marker Conversion Service
    Write-Host "🔨 Building Marker Conversion Service..." -ForegroundColor Yellow
    $markerImageName = if ($ContainerRegistryName) { 
        "$ContainerRegistryName.azurecr.io/ecourts-markerconvert:$TagVersion" 
    } else { 
        "ecourts-markerconvert:$TagVersion" 
    }
    
    docker build -t $markerImageName -f "marker-service/Dockerfile" "marker-service/"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Marker Conversion Service"
    }
    Write-Host "✅ Marker Conversion Service built successfully" -ForegroundColor Green
    
    # Build PDF Signing Service
    Write-Host "🔨 Building PDF Signing Service..." -ForegroundColor Yellow
    $pdfSigningImageName = if ($ContainerRegistryName) { 
        "$ContainerRegistryName.azurecr.io/ecourts-pdfsigning:$TagVersion" 
    } else { 
        "ecourts-pdfsigning:$TagVersion" 
    }
    
    docker build -t $pdfSigningImageName -f "pdf-signing-service/Dockerfile" "pdf-signing-service/"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build PDF Signing Service"
    }
    Write-Host "✅ PDF Signing Service built successfully" -ForegroundColor Green
    
    # List built images
    Write-Host "📋 Built images:" -ForegroundColor Yellow
    docker images | Select-String -Pattern "ecourts-" | ForEach-Object { 
        Write-Host "  $_" -ForegroundColor White 
    }
    
    # Push images if requested
    if ($PushImages -and $ContainerRegistryName) {
        Write-Host "📤 Pushing images to registry..." -ForegroundColor Yellow
        
        # Login to Azure Container Registry
        Write-Host "🔐 Logging in to Azure Container Registry..." -ForegroundColor Yellow
        az acr login --name $ContainerRegistryName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to login to Azure Container Registry"
        }
        
        # Push Marker Conversion Service
        Write-Host "📤 Pushing Marker Conversion Service..." -ForegroundColor Yellow
        docker push $markerImageName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push Marker Conversion Service"
        }
        Write-Host "✅ Marker Conversion Service pushed successfully" -ForegroundColor Green
        
        # Push PDF Signing Service
        Write-Host "📤 Pushing PDF Signing Service..." -ForegroundColor Yellow
        docker push $pdfSigningImageName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push PDF Signing Service"
        }
        Write-Host "✅ PDF Signing Service pushed successfully" -ForegroundColor Green
        
        Write-Host "🎉 All images pushed successfully!" -ForegroundColor Green
    }
    elseif ($PushImages -and -not $ContainerRegistryName) {
        Write-Warning "⚠️ Cannot push images without specifying ContainerRegistryName"
    }
    else {
        Write-Host "ℹ️ Images built locally. Use -PushImages `$true to push to registry." -ForegroundColor Cyan
    }
    
    # Display next steps
    Write-Host "📝 Next steps:" -ForegroundColor Yellow
    if ($PushImages -and $ContainerRegistryName) {
        Write-Host "  1. Your images are now available in Azure Container Registry" -ForegroundColor White
        Write-Host "  2. Deploy using: .\deployment\deploy.ps1 with registry parameters" -ForegroundColor White
        Write-Host "  3. Registry URLs:" -ForegroundColor White
        Write-Host "     - Marker: $markerImageName" -ForegroundColor Gray
        Write-Host "     - PDF Signing: $pdfSigningImageName" -ForegroundColor Gray
    }
    else {
        Write-Host "  1. Push images to registry: .\scripts\build-and-push.ps1 -ContainerRegistryName 'yourregistry' -PushImages `$true" -ForegroundColor White
        Write-Host "  2. Or deploy locally built images (testing only)" -ForegroundColor White
    }
}
catch {
    Write-Error "❌ Build process failed: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host "🎉 Build process completed!" -ForegroundColor Green 