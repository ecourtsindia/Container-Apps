#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test script for eCourts Container Apps

.DESCRIPTION
    This script sends test requests to your deployed eCourts Container Apps to verify they're working correctly.

.PARAMETER StorageConnectionString
    Azure Storage connection string

.PARAMETER TestPdfUrl
    URL of a test PDF file in blob storage (optional - will use a sample if not provided)

.PARAMETER CnrNumber
    CNR number for testing

.PARAMETER OrderNumber
    Order number for testing

.EXAMPLE
    .\test-deployment.ps1 -StorageConnectionString "DefaultEndpointsProtocol=https;..." -CnrNumber "DLHC010001092024" -OrderNumber "12345"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$StorageConnectionString,
    
    [Parameter(Mandatory = $false)]
    [string]$TestPdfUrl = "",
    
    [Parameter(Mandatory = $false)]
    [string]$CnrNumber = "DLHC010001092024",
    
    [Parameter(Mandatory = $false)]
    [string]$OrderNumber = "12345"
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🧪 Testing eCourts Container Apps..." -ForegroundColor Green
Write-Host "📋 Test Configuration:" -ForegroundColor Yellow
Write-Host "  CNR Number: $CnrNumber" -ForegroundColor White
Write-Host "  Order Number: $OrderNumber" -ForegroundColor White

# Check if Azure CLI is installed
try {
    $azVersion = az --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI not found"
    }
    Write-Host "✅ Azure CLI is available" -ForegroundColor Green
}
catch {
    Write-Error "❌ Azure CLI is required. Install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Generate unique request IDs
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$conversionRequestId = "conv-test-$timestamp"
$signingRequestId = "sign-test-$timestamp"

Write-Host "🔄 Generated Request IDs:" -ForegroundColor Cyan
Write-Host "  Conversion: $conversionRequestId" -ForegroundColor White
Write-Host "  Signing: $signingRequestId" -ForegroundColor White

# Use default test PDF if not provided
if (-not $TestPdfUrl) {
    # Extract storage account name from connection string
    if ($StorageConnectionString -match "AccountName=([^;]+)") {
        $storageAccountName = $Matches[1]
        $TestPdfUrl = "https://$storageAccountName.blob.core.windows.net/pdfs/test-document.pdf"
        Write-Host "📄 Using default test PDF URL: $TestPdfUrl" -ForegroundColor Cyan
        Write-Host "   (Make sure you have uploaded a test PDF to the 'pdfs' container)" -ForegroundColor Gray
    } else {
        Write-Error "❌ Could not extract storage account name from connection string"
        exit 1
    }
}

# Test 1: Send PDF Conversion Request
Write-Host "🔄 Test 1: Sending PDF Conversion Request..." -ForegroundColor Yellow

$conversionRequest = @{
    RequestId = $conversionRequestId
    PdfBlobUrl = $TestPdfUrl
    CnrNumber = $CnrNumber
    OrderNumber = $OrderNumber
    ClientDomain = "ecourts.gov.in"
    ConversionType = "marker"
    EnableOcr = $true
    OcrLanguages = @("hin", "eng")
} | ConvertTo-Json -Compress

try {
    az storage message put `
        --queue-name "marker-conversion-requests" `
        --content $conversionRequest `
        --connection-string $StorageConnectionString `
        --output none
    
    Write-Host "✅ PDF Conversion request sent successfully" -ForegroundColor Green
    Write-Host "   Request ID: $conversionRequestId" -ForegroundColor Gray
}
catch {
    Write-Error "❌ Failed to send PDF conversion request: $_"
}

# Test 2: Send PDF Signing Request
Write-Host "🔄 Test 2: Sending PDF Signing Request..." -ForegroundColor Yellow

$signingRequest = @{
    RequestId = $signingRequestId
    PdfBlobPath = "pdfs/test-document.pdf"
    CnrNumber = $CnrNumber
    OrderNumber = $OrderNumber
} | ConvertTo-Json -Compress

try {
    az storage message put `
        --queue-name "pdf-signing-requests" `
        --content $signingRequest `
        --connection-string $StorageConnectionString `
        --output none
    
    Write-Host "✅ PDF Signing request sent successfully" -ForegroundColor Green
    Write-Host "   Request ID: $signingRequestId" -ForegroundColor Gray
}
catch {
    Write-Error "❌ Failed to send PDF signing request: $_"
}

# Check queue status
Write-Host "📊 Checking queue status..." -ForegroundColor Yellow

try {
    Write-Host "🔍 Marker Conversion Queue:" -ForegroundColor Cyan
    $markerQueueInfo = az storage queue metadata show `
        --name "marker-conversion-requests" `
        --connection-string $StorageConnectionString `
        --output json | ConvertFrom-Json
    
    if ($markerQueueInfo) {
        Write-Host "   Queue exists and is accessible" -ForegroundColor Green
    }
    
    Write-Host "🔍 PDF Signing Queue:" -ForegroundColor Cyan
    $signingQueueInfo = az storage queue metadata show `
        --name "pdf-signing-requests" `
        --connection-string $StorageConnectionString `
        --output json | ConvertFrom-Json
    
    if ($signingQueueInfo) {
        Write-Host "   Queue exists and is accessible" -ForegroundColor Green
    }
}
catch {
    Write-Warning "⚠️ Could not retrieve queue status: $_"
}

# Instructions for monitoring
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Wait 2-5 minutes for processing to complete" -ForegroundColor White
Write-Host "2. Check the results using these commands:" -ForegroundColor White
Write-Host "" -ForegroundColor White

Write-Host "   # Check for converted markdown file:" -ForegroundColor Gray
Write-Host "   az storage blob list --container-name 'markdown-files' --connection-string '$StorageConnectionString'" -ForegroundColor Gray
Write-Host "" -ForegroundColor White

Write-Host "   # Check for signed PDF file:" -ForegroundColor Gray
Write-Host "   az storage file list --share-name 'your-file-share-name' --connection-string '$StorageConnectionString'" -ForegroundColor Gray
Write-Host "" -ForegroundColor White

Write-Host "   # Monitor container app logs:" -ForegroundColor Gray
Write-Host "   az containerapp logs show --name 'ecourts-marker-dev' --resource-group 'rg-ecourts-dev' --tail 20" -ForegroundColor Gray
Write-Host "   az containerapp logs show --name 'ecourts-pdfsigning-dev' --resource-group 'rg-ecourts-dev' --tail 20" -ForegroundColor Gray

Write-Host "🎉 Test requests sent successfully!" -ForegroundColor Green
Write-Host "Expected results:" -ForegroundColor Cyan
Write-Host "  📄 Markdown file: markdown-files/$CnrNumber-orderno-$OrderNumber.md" -ForegroundColor White
Write-Host "  🔐 Signed PDF: $CnrNumber-orderno-$OrderNumber.pdf" -ForegroundColor White

# Optional: Wait and check results
$checkResults = Read-Host "Do you want to wait and check for results? (y/N)"
if ($checkResults -eq "y" -or $checkResults -eq "Y") {
    Write-Host "⏳ Waiting 3 minutes for processing..." -ForegroundColor Yellow
    Start-Sleep -Seconds 180
    
    Write-Host "🔍 Checking for results..." -ForegroundColor Yellow
    
    # Check for markdown file
    try {
        $markdownFiles = az storage blob list `
            --container-name "markdown-files" `
            --connection-string $StorageConnectionString `
            --output json | ConvertFrom-Json
        
        $expectedMarkdownFile = "$CnrNumber-orderno-$OrderNumber.md"
        $foundMarkdown = $markdownFiles | Where-Object { $_.name -eq $expectedMarkdownFile }
        
        if ($foundMarkdown) {
            Write-Host "✅ Markdown file created: $expectedMarkdownFile" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Markdown file not found yet. Check logs for any issues." -ForegroundColor Yellow
        }
    }
    catch {
        Write-Warning "⚠️ Could not check markdown files: $_"
    }
    
    Write-Host "💡 Check Container App logs for detailed processing information" -ForegroundColor Cyan
} 