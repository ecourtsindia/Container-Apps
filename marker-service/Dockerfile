# Use Python base image with .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install Python and dependencies for Marker
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    git \
    && rm -rf /var/lib/apt/lists/*

# Create Python virtual environment and install Marker
RUN python3 -m venv /app/marker_env
ENV PATH="/app/marker_env/bin:$PATH"

# Install Marker and dependencies
RUN pip install --no-cache-dir \
    marker-pdf \
    azure-storage-blob \
    azure-storage-queue \
    azure-data-tables

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared files first
COPY ["shared/", "shared/"]

# Copy marker service files
COPY ["marker-service/eCourts-MarkerConvert.csproj", "marker-service/"]
RUN dotnet restore "marker-service/eCourts-MarkerConvert.csproj"

# Copy all marker service source files
COPY ["marker-service/", "marker-service/"]
WORKDIR "/src/marker-service"
RUN dotnet build "eCourts-MarkerConvert.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eCourts-MarkerConvert.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy the published .NET application
COPY --from=publish /app/publish .

# Create directories for logs
RUN mkdir -p /app/logs

# Set environment for Python
ENV PATH="/app/marker_env/bin:$PATH"

ENTRYPOINT ["dotnet", "eCourts-MarkerConvert.dll"] 