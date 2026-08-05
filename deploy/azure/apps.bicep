@description('Lowercase environment prefix used in resource names.')
param environmentName string

@description('Azure region of the existing Container Apps environment.')
param location string

@description('Existing Azure Container Apps managed environment name.')
param containerEnvironmentName string

@description('Resource group containing the managed environment.')
param containerEnvironmentResourceGroup string

@description('Immutable API image reference.')
param apiImage string

@description('Immutable frontend image reference.')
param frontendImage string

@description('Private container registry server.')
param registryServer string = 'ghcr.io'

@description('Container registry username.')
param registryUsername string

@secure()
@description('Container registry token with package read permission.')
param registryPassword string

@secure()
@description('Npgsql PostgreSQL connection string.')
param databaseConnectionString string

@secure()
@description('JWT HMAC signing key.')
param jwtSigningKey string

@secure()
@description('Environment-specific password pepper.')
param passwordPepper string

@minValue(0)
@maxValue(5)
@description('Minimum replicas per application.')
param minReplicas int = 0

@minValue(1)
@maxValue(1)
@description('Maximum API replicas. Keep at one until SignalR uses a distributed backplane.')
param maxReplicas int = 1

var normalizedName = toLower(replace(environmentName, '_', '-'))
var apiName = '${normalizedName}-api'
var frontendName = '${normalizedName}-web'

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerEnvironmentName
  scope: resourceGroup(containerEnvironmentResourceGroup)
}

var apiUrl = 'https://${apiName}.${containerEnvironment.properties.defaultDomain}'
var frontendUrl = 'https://${frontendName}.${containerEnvironment.properties.defaultDomain}'

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        {
          name: 'registry-password'
          value: registryPassword
        }
        {
          name: 'database-connection-string'
          value: databaseConnectionString
        }
        {
          name: 'jwt-signing-key'
          value: jwtSigningKey
        }
        {
          name: 'password-pepper'
          value: passwordPepper
        }
      ]
      registries: [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'ConnectionStrings__Database'
              secretRef: 'database-connection-string'
            }
            {
              name: 'Jwt__Issuer'
              value: 'ScrumBoard.Api'
            }
            {
              name: 'Jwt__Audience'
              value: 'ScrumBoard.Web'
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt-signing-key'
            }
            {
              name: 'Jwt__LifetimeMinutes'
              value: '30'
            }
            {
              name: 'Password__Pepper'
              secretRef: 'password-pepper'
            }
            {
              name: 'Password__Iterations'
              value: '210000'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: frontendUrl
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              failureThreshold: 12
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

resource frontend 'Microsoft.App/containerApps@2024-03-01' = {
  name: frontendName
  location: location
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        {
          name: 'registry-password'
          value: registryPassword
        }
      ]
      registries: [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: frontendImage
          env: [
            {
              name: 'APP_PORT'
              value: '8080'
            }
            {
              name: 'API_BASE_URL'
              value: '${apiUrl}/api'
            }
            {
              name: 'HUB_URL'
              value: '${apiUrl}/hubs/boards'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/'
                port: 8080
              }
              initialDelaySeconds: 3
              periodSeconds: 5
              failureThreshold: 12
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

output apiName string = api.name
output apiUrl string = apiUrl
output frontendName string = frontend.name
output frontendUrl string = frontendUrl
