targetScope = 'subscription'

type appServiceConfiguration = {
  skuName: string
  environment: {
    apiClientId: string
    swaggerClientId: string
  }
}

@description('Azure region for the resource group and App Service resources.')
param location string

@description('Deployment environment used in CAF resource names.')
param environmentName string

@description('Workload name used in CAF resource names.')
param workloadName string

@description('App Service plan and application environment configuration.')
param appService appServiceConfiguration

var resourceGroupName = 'rg-${workloadName}-${environmentName}'
var appServicePlanName = 'asp-${workloadName}-${environmentName}'
var appServiceName = 'app-${workloadName}-${environmentName}'
var deploymentSuffix = uniqueString(deployment().name, deployment().location)
var tags = {
  Environment: environmentName
  ManagedBy: 'Bicep'
  Workload: workloadName
}

module resourceGroupDeployment 'br/public:avm/res/resources/resource-group:0.4.4' = {
  name: 'resource-group-${deploymentSuffix}'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

module appServicePlan 'br/public:avm/res/web/serverfarm:0.7.0' = {
  name: 'app-service-plan-${deploymentSuffix}'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: appServicePlanName
    location: location
    kind: 'linux'
    reserved: true
    skuCapacity: 1
    skuName: appService.skuName
    tags: tags
    zoneRedundant: false
  }
  dependsOn: [
    resourceGroupDeployment
  ]
}

module webApp 'br/public:avm/res/web/site:0.24.0' = {
  name: 'app-service-${deploymentSuffix}'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: appServiceName
    location: location
    kind: 'app,linux'
    serverFarmResourceId: appServicePlan.outputs.resourceId
    clientAffinityEnabled: false
    configs: [
      {
        name: 'appsettings'
        properties: {
          AzureAd__ClientId: appService.environment.apiClientId
          AzureAd__Instance: environment().authentication.loginEndpoint
          AzureAd__TenantId: deployer().tenantId
          DirectoryProfiles__Path: 'Profiles'
          Swagger__ClientId: appService.environment.swaggerClientId
        }
      }
    ]
    httpsOnly: true
    managedIdentities: {
      systemAssigned: true
    }
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
    }
    tags: tags
  }
}

output appServiceName string = webApp.outputs.name
output appServicePlanName string = appServicePlan.outputs.name
output appServiceUrl string = 'https://${webApp.outputs.defaultHostname}'
output managedIdentityPrincipalId string? = webApp.outputs.?systemAssignedMIPrincipalId
output resourceGroupName string = resourceGroupDeployment.outputs.name
output swaggerClientId string = appService.environment.swaggerClientId
