using '../main.bicep'

param environment = 'test'
param ownerTag = 'platform-engineering'
param costCenter = 'engineering'
param dataClassification = 'internal'
param mvpCostProfile = true
param enableAppConfiguration = false
param enableUserNodePool = false

param aksNodeVmSize = 'Standard_B2s'
param aksNodeCount = 1
param aksSystemNodeVmSize = 'Standard_B2s'
param aksSystemNodeCount = 1

param vnetAddressPrefix = '10.61.0.0/16'
param aksSubnetPrefix = '10.61.1.0/24'
param dataSubnetPrefix = '10.61.2.0/24'
param privateEndpointSubnetPrefix = '10.61.3.0/24'

param postgresAdminPassword = ''
