@description('The location of the Container App Environment')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param namePrefix string = 'ecourts'

@description('The environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure Storage connection string')
@secure()
param storageConnectionString string

@description('Certificate password')
@secure()
param certificatePassword string

@description('Container registry login server')
param containerRegistryLoginServer string = ''

@description('Container registry username')
param containerRegistryUsername string = ''

@description('Container registry password')
@secure()
param containerRegistryPassword string = ''

// Variables
var containerAppEnvironmentName = '${namePrefix}-env-${environment}'
var markerAppName = '${namePrefix}-marker-${environment}'
var pdfSigningAppName = '${namePrefix}-pdfsigning-${environment}'
var logAnalyticsWorkspaceName = '${namePrefix}-logs-${environment}'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      searchVersion: 1
      legacy: 0
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Container App Environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvironmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// Marker Conversion Container App (HTTP API)
resource markerContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: markerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: !empty(containerRegistryLoginServer) ? [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ] : []
      secrets: concat([
        {
          name: 'storage-connection-string'
          value: storageConnectionString
        }
      ], !empty(containerRegistryPassword) ? [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ] : [])
    }
    template: {
      containers: [
        {
          image: !empty(containerRegistryLoginServer) ? '${containerRegistryLoginServer}/ecourts-markerconvert:latest' : 'mcr.microsoft.com/dotnet/aspnet:8.0'
          name: 'marker-convert'
          env: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'MAX_PARALLELISM'
              value: '2'
            }
            {
              name: 'MAX_CONCURRENT_CONVERSIONS'
              value: '1'
            }
            {
              name: 'ENABLE_MARKER'
              value: 'true'
            }
            {
              name: 'ENABLE_VERBOSE_LOGGING'
              value: 'false'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:8080'
            }
          ]
          resources: {
            cpu: json('2.0')
            memory: '4Gi'
          }
          probes: [
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 10
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 60
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

// PDF Signing Container App (HTTP API)
resource pdfSigningContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: pdfSigningAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: !empty(containerRegistryLoginServer) ? [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ] : []
      secrets: concat([
        {
          name: 'storage-connection-string'
          value: storageConnectionString
        }
        {
          name: 'certificate-password'
          value: certificatePassword
        }
      ], !empty(containerRegistryPassword) ? [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ] : [])
    }
    template: {
      containers: [
        {
          image: !empty(containerRegistryLoginServer) ? '${containerRegistryLoginServer}/ecourts-pdfsigning:latest' : 'mcr.microsoft.com/dotnet/aspnet:8.0'
          name: 'pdf-signing'
          env: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'CERTIFICATE_PASSWORD'
              secretRef: 'certificate-password'
            }
            {
              name: 'CERTIFICATE_PATH'
              value: '/app/certs/certificate.pfx'
            }
            {
              name: 'MAX_PARALLELISM'
              value: '2'
            }
            {
              name: 'MAX_CONCURRENT_CONVERSIONS'
              value: '1'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:8080'
            }
          ]
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          probes: [
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 10
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 60
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '5'
              }
            }
          }
        ]
      }
    }
  }
}

// Outputs
output containerAppEnvironmentId string = containerAppEnvironment.id
output markerApiUrl string = 'https://${markerContainerApp.properties.configuration.ingress.fqdn}'
output pdfSigningApiUrl string = 'https://${pdfSigningContainerApp.properties.configuration.ingress.fqdn}'
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id 