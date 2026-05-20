# Azure Document Storage Setup

The AMS DMS stores document metadata in SQL and document binaries in Azure Blob Storage.

## Configured application values

The API is configured for managed identity / Azure credential authentication:

```json
"DocumentStorage": {
  "ConnectionString": "",
  "AccountUri": "https://amsdmsdocsdev.blob.core.windows.net",
  "ContainerName": "documents"
}
```

- Storage account: `amsdmsdocsdev`
- Blob endpoint: `https://amsdmsdocsdev.blob.core.windows.net`
- Container: `documents`
- Authentication: `DefaultAzureCredential` through managed identity or local Azure sign-in

Do not commit storage account keys or connection strings. Use managed identity in Azure-hosted environments.

## Azure setup with Azure CLI

Install Azure CLI and sign in:

```powershell
az login
az account set --subscription "<subscription-id>"
```

Set deployment variables:

```powershell
$location = "eastus"
$resourceGroup = "rg-ams-dms-dev"
$storageAccount = "amsdmsdocsdev"
$container = "documents"
```

Create the resource group:

```powershell
az group create --name $resourceGroup --location $location
```

Create the storage account:

```powershell
az storage account create `
  --name $storageAccount `
  --resource-group $resourceGroup `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2 `
  --https-only true `
  --min-tls-version TLS1_2 `
  --allow-blob-public-access false
```

Create the private documents container:

```powershell
az storage container create `
  --name $container `
  --account-name $storageAccount `
  --auth-mode login `
  --public-access off
```

## Grant access to the API

### Local development

Grant your signed-in Azure user blob data access:

```powershell
$scope = az storage account show --name $storageAccount --resource-group $resourceGroup --query id -o tsv
$assignee = az ad signed-in-user show --query id -o tsv
az role assignment create `
  --assignee $assignee `
  --role "Storage Blob Data Contributor" `
  --scope $scope
```

Restart Visual Studio after role assignment propagation if local uploads fail at first. RBAC propagation can take several minutes.

### Azure-hosted API

Enable a system-assigned managed identity on the Azure App Service, Container App, VM, or other hosting resource running `Ams.Api`. Then assign that identity to the storage account:

```powershell
$scope = az storage account show --name $storageAccount --resource-group $resourceGroup --query id -o tsv
$principalId = "<api-managed-identity-principal-id>"
az role assignment create `
  --assignee $principalId `
  --role "Storage Blob Data Contributor" `
  --scope $scope
```

Set these app settings on the API host:

```powershell
DocumentStorage__ConnectionString=
DocumentStorage__AccountUri=https://amsdmsdocsdev.blob.core.windows.net
DocumentStorage__ContainerName=documents
```

## Optional connection string fallback

The implementation supports `DocumentStorage:ConnectionString`, but it should only be used for local troubleshooting or controlled development environments. Store secrets in user secrets, Key Vault, or Azure app settings; do not commit them to source control.

Example user secret:

```powershell
dotnet user-secrets set "DocumentStorage:ConnectionString" "<storage-connection-string>" --project src/Ams.Api/Ams.Api.csproj
```

## Verification

1. Start `Ams.Api`.
2. Start the Blazor web app.
3. Open the Documents page.
4. Upload a document file.
5. Confirm a blob appears under `documents/<tenant-id>/...` in `amsdmsdocsdev`.
6. Download the document from the UI/API and confirm the file streams from Azure Blob Storage.
