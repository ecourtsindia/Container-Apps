# Certificate Setup for PDF Signing Service

## Simple Certificate Placement

To use your certificate with the PDF signing service, simply place your certificate file in this folder:

```
pdf-signing-service/
├── certificate.pfx  <- Place your certificate file here
├── Dockerfile
├── Program.cs
└── ... other files
```

## How it Works

1. **Place Certificate**: Copy your `certificate.pfx` file to the `pdf-signing-service/` folder
2. **Build Docker Image**: When building the Docker image, the certificate is automatically copied into the container at `/app/certs/certificate.pfx`
3. **Application Loads**: The PDF signing service loads the certificate from this path using the password from the `CERTIFICATE_PASSWORD` environment variable

## Security Notes

- The certificate password is passed securely via environment variables (not hardcoded)
- The certificate file is embedded in the Docker image during build time
- Ensure your certificate file is not committed to version control (.gitignore should exclude *.pfx files)

## Build and Deploy Process

1. Place your certificate:
   ```bash
   cp /path/to/your/certificate.pfx pdf-signing-service/certificate.pfx
   ```

2. Build the Docker image:
   ```bash
   cd pdf-signing-service
   docker build -t ecourts-pdfsigning .
   ```

3. Deploy using the deployment script:
   ```bash
   .\deployment\deploy.ps1 -ResourceGroupName "rg-ecourts-dev" -StorageConnectionString "..." -CertificatePassword "LetsImproveLaw"
   ```

## Original Configuration

Based on your original program, the certificate should:
- Be password protected with: `LetsImproveLaw`
- Be a .pfx file containing both the certificate and private key
- Be suitable for PDF digital signing

That's it! No complex Azure Key Vault setup needed. 