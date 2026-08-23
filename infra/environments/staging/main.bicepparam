using '../main.bicep'

param environment = 'staging'
param ownerTag = 'platform-engineering'
param costCenter = 'engineering'
param dataClassification = 'confidential'
param mvpCostProfile = true
param enableAppConfiguration = false
param enableUserNodePool = true

param aksNodeVmSize = 'Standard_B2s'
param aksNodeCount = 2
param aksSystemNodeVmSize = 'Standard_B2s'
param aksSystemNodeCount = 1

param vnetAddressPrefix = '10.62.0.0/16'
param aksSubnetPrefix = '10.62.1.0/24'
param dataSubnetPrefix = '10.62.2.0/24'
param privateEndpointSubnetPrefix = '10.62.3.0/24'

param postgresAdminPassword = ''
