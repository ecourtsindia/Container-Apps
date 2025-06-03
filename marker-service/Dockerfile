# Use Python base image with .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install Python and dependencies for Marker
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    && rm -rf /var/lib/apt/lists/*

# Create Python virtual environment
RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Install Marker and dependencies
RUN pip install --no-cache-dir \
    marker-pdf \
    azure-storage-blob \
    azure-storage-queue \
    azure-data-tables

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["eCourts-MarkerConvert.csproj", "."]
RUN dotnet restore "eCourts-MarkerConvert.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "eCourts-MarkerConvert.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eCourts-MarkerConvert.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy Python environment
COPY --from=base /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

ENTRYPOINT ["dotnet", "eCourts-MarkerConvert.dll"] 