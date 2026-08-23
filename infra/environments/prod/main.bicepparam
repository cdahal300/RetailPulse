using '../main.bicep'

param environment = 'prod'
param ownerTag = 'platform-engineering'
param costCenter = 'operations'
param dataClassification = 'restricted'
param mvpCostProfile = false
param enableAppConfiguration = true
param enableUserNodePool = true

param aksNodeVmSize = 'Standard_D8ds_v5'
param aksNodeCount = 5
param aksSystemNodeVmSize = 'Standard_D4ds_v5'
param aksSystemNodeCount = 3

param vnetAddressPrefix = '10.63.0.0/16'
param aksSubnetPrefix = '10.63.1.0/24'
param dataSubnetPrefix = '10.63.2.0/24'
param privateEndpointSubnetPrefix = '10.63.3.0/24'

param postgresAdminPassword = ''
