# Create-Certificate.ps1
# Creates a self-signed certificate for Azure AD app authentication
# Run this script as Administrator

param(
    [string]$CertificateName = "MCP-Enterprise-Client",
    [string]$ExportPath = ".\certificate",
    [int]$ValidityYears = 2
)

Write-Host "=== Creating Self-Signed Certificate for Azure AD App ===" -ForegroundColor Cyan
Write-Host ""

# Create the certificate
$cert = New-SelfSignedCertificate `
    -Subject "CN=$CertificateName" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears($ValidityYears)

Write-Host "✓ Certificate created successfully!" -ForegroundColor Green
Write-Host "  Subject: $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Expires: $($cert.NotAfter)"
Write-Host ""

# Export the public key (.cer) - this is uploaded to Azure AD
$cerPath = "$ExportPath.cer"
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
Write-Host "✓ Public key exported: $cerPath" -ForegroundColor Green
Write-Host "  Upload this file to Azure AD App Registration > Certificates & secrets"
Write-Host ""

# Export the private key (.pfx) - keep this secure!
$pfxPassword = Read-Host -AsSecureString "Enter password for .pfx file (private key)"
$pfxPath = "$ExportPath.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword | Out-Null
Write-Host "✓ Private key exported: $pfxPath" -ForegroundColor Green
Write-Host "  Keep this file secure! It contains the private key."
Write-Host ""

# Output configuration
Write-Host "=== Configuration ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "Option 1: Use thumbprint (certificate already in store)"
Write-Host "  Add to appsettings.json:"
Write-Host "    `"CertificateThumbprint`": `"$($cert.Thumbprint)`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "Option 2: Use certificate file"
Write-Host "  Add to appsettings.json:"
Write-Host "    `"CertificatePath`": `"$pfxPath`"" -ForegroundColor Cyan
Write-Host "    `"CertificatePassword`": `"<your-password>`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "=== Azure AD Setup ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Go to Azure Portal > App Registrations > Your App"
Write-Host "2. Click 'Certificates & secrets' > 'Certificates'"
Write-Host "3. Click 'Upload certificate'"
Write-Host "4. Select: $cerPath"
Write-Host "5. Click 'Add'"
Write-Host ""
Write-Host "Done! You can now remove the ClientSecret from appsettings.json" -ForegroundColor Green
