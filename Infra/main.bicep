param location string = 'EastUS2'
param appName string = 'app-SBOMViewer-eastus-001'
param stagingAppName string = 'app-SBOMViewer-staging-eastus-001'
param repositoryUrl string = 'https://github.com/NavneetHegde/SBOMViewer'
param branch string = 'main'
param skuTier string = 'Free'
param skuSize string = 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: appName
  location: location
  sku: {
    name: skuTier
    tier: skuSize
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: branch
    buildProperties: {
      apiLocation: null
      appLocation: 'wwwroot'  // Blazor WASM output
      outputLocation: ''
    }
  }
}

// Staging SWA — deploys from release/* branches → prerelease.sbomviewer.com
// Deployed via GitHub Actions using AZURE_STATIC_WEB_APPS_API_TOKEN_STAGING secret.
// The branch value below is decorative; actual deployments are token-driven.
resource stagingStaticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: stagingAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: 'release/main'
    buildProperties: {
      apiLocation: null
      appLocation: 'wwwroot'
      outputLocation: ''
    }
  }
}

output productionDefaultHostname string = staticWebApp.properties.defaultHostname
output stagingDefaultHostname string = stagingStaticWebApp.properties.defaultHostname

// Initial Steps

//**  Create Resource **//
// PS /home/navneet> az  group create --name rg-sbomviewer-eastus2-001 --location EASTUS2                                                                                                      

//**  Create Static Web App **//
// PS /home/navneet> az deployment group create --resource-group rg-sbomviewer-eastus2-001 --template-file main.bicep --parameters appName=app-sbomviewer-eastus-001 repositoryUrl=https://github.com/NavneetHegde/SBOMViewer
