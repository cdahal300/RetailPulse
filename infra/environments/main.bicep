targetScope = 'resourceGroup'

@description('Deployment environment name.')
@allowed([
  'dev'
  'test'
  'staging'
  'prod'
])
param environment string

@description('Primary Azure location.')
param location string = resourceGroup().location

@description('Workload short name used for resource naming.')
param workloadName string = 'retailpulse'

@description('Tag: owner/team responsible for the environment.')
param ownerTag string

@description('Tag: cost center or budget owner.')
param costCenter string = 'retailpulse'

@description('Tag: data classification for the environment.')
@allowed([
  'public'
  'internal'
  'confidential'
  'restricted'
])
param dataClassification string = 'internal'

@description('Enable MVP cost profile (smaller SKUs and conservative defaults).')
param mvpCostProfile bool = true

@description('Enable Azure App Configuration resource creation.')
param enableAppConfiguration bool = false

@description('AKS Kubernetes version.')
param kubernetesVersion string = '1.34'

@description('AKS user node pool VM size.')
param aksNodeVmSize string = 'Standard_B2s'

@description('AKS user node pool node count.')
@minValue(1)
param aksNodeCount int = 1

@description('AKS system node pool VM size.')
param aksSystemNodeVmSize string = 'Standard_B2s'

@description('AKS system node pool node count.')
@minValue(1)
param aksSystemNodeCount int = 1

@description('Create a separate AKS user node pool for application workloads.')
param enableUserNodePool bool = false

@description('Admin username for PostgreSQL Flexible Server.')
param postgresAdminLogin string = 'rpadmin'

@description('Admin password for PostgreSQL Flexible Server.')
@secure()
param postgresAdminPassword string

@description('Address space for the environment VNet.')
param vnetAddressPrefix string = '10.60.0.0/16'

@description('Address prefix for AKS subnet.')
param aksSubnetPrefix string = '10.60.1.0/24'

@description('Address prefix for PostgreSQL delegated subnet.')
param dataSubnetPrefix string = '10.60.2.0/24'

@description('Address prefix for private endpoints subnet.')
param privateEndpointSubnetPrefix string = '10.60.3.0/24'

var suffix = take(uniqueString(resourceGroup().id, environment, workloadName), 6)
var namePrefix = '${workloadName}-${environment}'
var storageName = toLower(take('rp${environment}${suffix}st', 24))
var acrSkuName = mvpCostProfile ? 'Basic' : (environment == 'prod' ? 'Premium' : 'Standard')
var commonTags = {
  owner: ownerTag
  environment: environment
  costCenter: costCenter
  dataClassification: dataClassification
  workload: workloadName
}

resource nsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: '${namePrefix}-nsg'
  location: location
  tags: commonTags
  properties: {
    securityRules: []
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: '${namePrefix}-vnet'
  location: location
  tags: commonTags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'aks'
        properties: {
          addressPrefix: aksSubnetPrefix
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
      {
        name: 'data'
        properties: {
          addressPrefix: dataSubnetPrefix
          networkSecurityGroup: {
            id: nsg.id
          }
          delegations: [
            {
              name: 'postgres-delegation'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Disabled'
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
    ]
  }
}

resource aksSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: vnet
  name: 'aks'
}

resource dataSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: vnet
  name: 'data'
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-log'
  location: location
  tags: commonTags
  properties: {
    retentionInDays: mvpCostProfile ? 30 : (environment == 'prod' ? 90 : 30)
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource workloadIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-uami'
  location: location
  tags: commonTags
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: toLower(replace('${workloadName}${environment}${suffix}acr', '-', ''))
  location: location
  tags: commonTags
  sku: {
    name: acrSkuName
  }
  properties: union(
    {
      adminUserEnabled: false
      publicNetworkAccess: 'Enabled'
      zoneRedundancy: (environment == 'prod' && !mvpCostProfile) ? 'Enabled' : 'Disabled'
    },
    acrSkuName == 'Basic' ? {} : {
      policies: {
      quarantinePolicy: {
        status: 'enabled'
      }
      retentionPolicy: {
        days: 14
        status: 'enabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
    }
    }
  )
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: toLower(take('${workloadName}${environment}kv${suffix}', 24))
  location: location
  tags: commonTags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    softDeleteRetentionInDays: 90
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${namePrefix}-sb-${suffix}'
  location: location
  tags: commonTags
  sku: {
    name: environment == 'prod' ? 'Premium' : 'Standard'
    tier: environment == 'prod' ? 'Premium' : 'Standard'
    capacity: environment == 'prod' ? 1 : 0
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    zoneRedundant: environment == 'prod'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  tags: commonTags
  sku: {
    name: environment == 'prod' ? 'Standard_ZRS' : 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Disabled'
    supportsHttpsTrafficOnly: true
  }
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = if (enableAppConfiguration) {
  name: '${namePrefix}-appcs-${suffix}'
  location: location
  tags: commonTags
  sku: {
    name: environment == 'prod' ? 'standard' : 'free'
  }
  properties: {
    disableLocalAuth: true
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    softDeleteRetentionInDays: 30
  }
}

resource postgresPrivateDns 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.postgres.database.azure.com'
  location: 'global'
  tags: commonTags
}

resource postgresPrivateDnsVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: postgresPrivateDns
  name: '${namePrefix}-postgres-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: '${namePrefix}-pg-${suffix}'
  location: location
  tags: commonTags
  sku: {
    name: mvpCostProfile ? 'Standard_B1ms' : (environment == 'prod' ? 'Standard_D4ds_v5' : 'Standard_B2s')
    tier: mvpCostProfile ? 'Burstable' : (environment == 'prod' ? 'GeneralPurpose' : 'Burstable')
  }
  properties: {
    version: '15'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    availabilityZone: '1'
    backup: {
      backupRetentionDays: mvpCostProfile ? 7 : (environment == 'prod' ? 35 : 7)
      geoRedundantBackup: (environment == 'prod' && !mvpCostProfile) ? 'Enabled' : 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: dataSubnet.id
      privateDnsZoneArmResourceId: postgresPrivateDns.id
    }
    storage: {
      storageSizeGB: mvpCostProfile ? 32 : (environment == 'prod' ? 256 : 64)
      autoGrow: 'Enabled'
    }
  }
  dependsOn: [
    postgresPrivateDnsVnetLink
  ]
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgres
  name: 'retailpulse'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: '${namePrefix}-aks'
  location: location
  tags: commonTags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: kubernetesVersion
    dnsPrefix: '${namePrefix}-${suffix}'
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    addonProfiles: {
      azurepolicy: {
        enabled: true
      }
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalytics.id
        }
      }
    }
    agentPoolProfiles: concat(
      [
        {
          name: 'system'
          mode: 'System'
          count: aksSystemNodeCount
          vmSize: aksSystemNodeVmSize
          osType: 'Linux'
          type: 'VirtualMachineScaleSets'
          vnetSubnetID: aksSubnet.id
        }
      ],
      enableUserNodePool
        ? [
            {
              name: 'usernp'
              mode: 'User'
              count: aksNodeCount
              vmSize: aksNodeVmSize
              osType: 'Linux'
              type: 'VirtualMachineScaleSets'
              vnetSubnetID: aksSubnet.id
            }
          ]
        : []
    )
    networkProfile: {
      networkPlugin: 'azure'
      networkPolicy: 'azure'
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'
    }
  }
}

resource aksAcrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, aks.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

output environmentName string = environment
output aksName string = aks.name
output aksOidcIssuerUrl string = aks.properties.oidcIssuerProfile.issuerURL
output aksClusterPrincipalId string = aks.identity.principalId
output workloadIdentityClientId string = workloadIdentity.properties.clientId
output acrLoginServer string = containerRegistry.properties.loginServer
output keyVaultName string = keyVault.name
output postgresServerName string = postgres.name
output postgresDatabaseName string = postgresDb.name
output serviceBusNamespace string = serviceBus.name
output storageAccountName string = storage.name
output appConfigurationName string = appConfig.?name ?? ''
