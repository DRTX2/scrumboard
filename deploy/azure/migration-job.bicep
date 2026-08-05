@description('Lowercase environment prefix used in resource names.')
param environmentName string

@description('Azure region of the existing Container Apps environment.')
param location string

@description('Existing Azure Container Apps managed environment name.')
param containerEnvironmentName string

@description('Resource group containing the managed environment.')
param containerEnvironmentResourceGroup string

@description('Immutable migrator image reference.')
param migrationImage string

@secure()
@description('Npgsql PostgreSQL connection string.')
param databaseConnectionString string

@secure()
@description('Environment-specific password pepper.')
param passwordPepper string

@description('Bootstrap administrator display name.')
param bootstrapAdminName string

@secure()
@description('Bootstrap administrator email.')
param bootstrapAdminEmail string

@secure()
@description('Bootstrap administrator password.')
param bootstrapAdminPassword string

@description('Remove the deterministic demo project after production bootstrap.')
param removeDemoWorkspace bool = false

var normalizedName = toLower(replace(environmentName, '_', '-'))

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerEnvironmentName
  scope: resourceGroup(containerEnvironmentResourceGroup)
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: '${normalizedName}-migrations'
  location: location
  properties: {
    environmentId: containerEnvironment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      secrets: [
        {
          name: 'database-connection-string'
          value: databaseConnectionString
        }
        {
          name: 'password-pepper'
          value: passwordPepper
        }
        {
          name: 'bootstrap-admin-email'
          value: bootstrapAdminEmail
        }
        {
          name: 'bootstrap-admin-password'
          value: bootstrapAdminPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrations'
          image: migrationImage
          env: [
            {
              name: 'DOTNET_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ConnectionStrings__Database'
              secretRef: 'database-connection-string'
            }
            {
              name: 'Password__Pepper'
              secretRef: 'password-pepper'
            }
            {
              name: 'BootstrapAdmin__Enabled'
              value: 'true'
            }
            {
              name: 'BootstrapAdmin__Name'
              value: bootstrapAdminName
            }
            {
              name: 'BootstrapAdmin__Email'
              secretRef: 'bootstrap-admin-email'
            }
            {
              name: 'BootstrapAdmin__Password'
              secretRef: 'bootstrap-admin-password'
            }
            {
              name: 'BootstrapAdmin__RemoveDemoWorkspace'
              value: string(removeDemoWorkspace)
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

output migrationJobName string = migrationJob.name
