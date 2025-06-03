# Use .NET runtime as base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Create directories for certificates and logs
RUN mkdir -p /app/certs /app/logs

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared files first
COPY ["shared/", "shared/"]

# Copy PDF signing service files
COPY ["pdf-signing-service/eCourts-PDFSigning.csproj", "pdf-signing-service/"]
RUN dotnet restore "pdf-signing-service/eCourts-PDFSigning.csproj"

# Copy all PDF signing service source files
COPY ["pdf-signing-service/", "pdf-signing-service/"]
WORKDIR "/src/pdf-signing-service"
RUN dotnet build "eCourts-PDFSigning.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eCourts-PDFSigning.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy the published .NET application
COPY --from=publish /app/publish .

# Copy certificate file if it exists (using shell to avoid Docker build failure)
COPY ["pdf-signing-service/", "/tmp/pdf-service-files/"]
RUN if [ -f /tmp/pdf-service-files/certificate.pfx ]; then \
      cp /tmp/pdf-service-files/certificate.pfx /app/certs/certificate.pfx; \
    fi && \
    rm -rf /tmp/pdf-service-files

ENTRYPOINT ["dotnet", "eCourts-PDFSigning.dll"] 