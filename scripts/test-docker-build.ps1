# Test Docker Build Script for eCourts Container Apps
# This script tests if the Docker images can be built successfully

Write-Host "🐳 Testing Docker Build for eCourts Container Apps" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Check if Docker is running
try {
    docker version | Out-Null
    Write-Host "✅ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not running or not installed" -ForegroundColor Red
    exit 1
}

# Check if required files exist
Write-Host "`n📁 Checking required files..." -ForegroundColor Yellow

$requiredFiles = @(
    "shared/Configuration.cs",
    "shared/Models.cs",
    "marker-service/eCourts-MarkerConvert.csproj",
    "marker-service/Program.cs",
    "marker-service/Dockerfile",
    "pdf-signing-service/eCourts-PDFSigning.csproj", 
    "pdf-signing-service/Program.cs",
    "pdf-signing-service/Dockerfile"
)

$allFilesExist = $true
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
    } else {
        Write-Host "❌ $file (missing)" -ForegroundColor Red
        $allFilesExist = $false
    }
}

if (-not $allFilesExist) {
    Write-Host "`n❌ Some required files are missing. Please ensure all files are present." -ForegroundColor Red
    exit 1
}

# Check certificate file (optional)
if (Test-Path "pdf-signing-service/certificate.pfx") {
    Write-Host "✅ pdf-signing-service/certificate.pfx (found)" -ForegroundColor Green
} else {
    Write-Host "⚠️ pdf-signing-service/certificate.pfx (not found - optional)" -ForegroundColor Yellow
}

Write-Host "`n🔨 Building Docker Images..." -ForegroundColor Yellow

# Build Marker Service
Write-Host "`n1️⃣ Building Marker Conversion Service..." -ForegroundColor Cyan
try {
    docker build -f marker-service/Dockerfile -t ecourts-markerconvert:test .
    Write-Host "✅ Marker service build successful" -ForegroundColor Green
} catch {
    Write-Host "❌ Marker service build failed" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Build PDF Signing Service
Write-Host "`n2️⃣ Building PDF Signing Service..." -ForegroundColor Cyan
try {
    docker build -f pdf-signing-service/Dockerfile -t ecourts-pdfsigning:test .
    Write-Host "✅ PDF signing service build successful" -ForegroundColor Green
} catch {
    Write-Host "❌ PDF signing service build failed" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# List built images
Write-Host "`n📋 Built Images:" -ForegroundColor Yellow
docker images | findstr "ecourts"

Write-Host "`n🎉 All Docker builds completed successfully!" -ForegroundColor Green
Write-Host "`n💡 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test the images locally if needed:" -ForegroundColor White
Write-Host "     docker run -p 8080:8080 ecourts-markerconvert:test" -ForegroundColor Gray
Write-Host "     docker run -p 8081:8080 ecourts-pdfsigning:test" -ForegroundColor Gray
Write-Host "  2. Push to GitHub to trigger automated deployment" -ForegroundColor White
Write-Host "  3. Monitor GitHub Actions for deployment status" -ForegroundColor White

# Cleanup test images (optional)
Write-Host "`n🧹 Cleanup test images? (y/n): " -ForegroundColor Yellow -NoNewline
$cleanup = Read-Host
if ($cleanup -eq "y" -or $cleanup -eq "Y") {
    docker rmi ecourts-markerconvert:test ecourts-pdfsigning:test
    Write-Host "✅ Test images cleaned up" -ForegroundColor Green
} else {
    Write-Host "ℹ️ Test images kept for local testing" -ForegroundColor Blue
} 