# Use .NET runtime as base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Create directories for certificates and logs
RUN mkdir -p /app/certs /app/logs

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["eCourts-PDFSigning.csproj", "."]
RUN dotnet restore "eCourts-PDFSigning.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "eCourts-PDFSigning.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eCourts-PDFSigning.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy certificate file if it exists
# Place your certificate.pfx file in the pdf-signing-service folder before building
RUN if [ -f certificate.pfx ]; then cp certificate.pfx /app/certs/certificate.pfx; fi

ENTRYPOINT ["dotnet", "eCourts-PDFSigning.dll"] 