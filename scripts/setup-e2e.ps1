<#
.SYNOPSIS
	One-time local setup for full end-to-end Judz testing (Visual Studio F5 / dotnet run).

.DESCRIPTION
	Judz reads its two external AI dependencies from DIFFERENT sources:

	  1. Azure Document Intelligence  -> IConfiguration section "DocumentIntelligence"
		 (Endpoint / ApiKey). These CAN and SHOULD be stored in .NET User Secrets so they
		 never land in git. This script writes them with `dotnet user-secrets`.

	  2. Azure OpenAI (CHAT / semantic enrichment) -> the provider adapter resolves the
		 endpoint and key by reading OS ENVIRONMENT VARIABLES referenced from the database
		 seed as env://AMS_AZURE_OPENAI_ENDPOINT and env://AMS_AZURE_OPENAI_KEY.
		 User Secrets are NOT read on that path, so these MUST be real environment variables.
		 This script sets them at User scope (persisted for your Windows account).

	IMPORTANT: Environment variables are read at process start. After running this script you
	MUST fully close and reopen Visual Studio (or your terminal) before launching Legal.Api,
	otherwise the new AMS_AZURE_OPENAI_* values will not be visible to the app.

.EXAMPLE
	./scripts/setup-e2e.ps1 `
		-AzureOpenAiEndpoint "https://my-aoai.openai.azure.com/" `
		-AzureOpenAiKey      "<aoai-key>" `
		-DocIntelEndpoint    "https://my-docintel.cognitiveservices.azure.com/" `
		-DocIntelKey         "<docintel-key>"

.NOTES
	Nothing here is committed to source control. Re-run any time credentials rotate.
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)] [string] $AzureOpenAiEndpoint,
	[Parameter(Mandatory = $true)] [string] $AzureOpenAiKey,
	[Parameter(Mandatory = $true)] [string] $DocIntelEndpoint,
	[Parameter(Mandatory = $true)] [string] $DocIntelKey
)

$ErrorActionPreference = 'Stop'

# Resolve the Legal.Api project relative to this script (scripts/ is a sibling of Legal/).
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot 'Legal/src/Legal.Api/Legal.Api.csproj'
if (-not (Test-Path $apiProject)) {
	throw "Could not locate Legal.Api.csproj at '$apiProject'. Run this script from the cloned repo."
}

Write-Host '== Judz end-to-end setup ==' -ForegroundColor Cyan

# ---- 1. Azure Document Intelligence via User Secrets (Legal.Api) ----
Write-Host 'Setting Document Intelligence User Secrets on Legal.Api...' -ForegroundColor Yellow
dotnet user-secrets --project $apiProject set 'DocumentIntelligence:Endpoint' $DocIntelEndpoint | Out-Null
dotnet user-secrets --project $apiProject set 'DocumentIntelligence:ApiKey'   $DocIntelKey      | Out-Null
Write-Host '  DocumentIntelligence:Endpoint / ApiKey stored in User Secrets.' -ForegroundColor Green

# ---- 2. Azure OpenAI via User-scoped environment variables ----
# The DB seed points the provider at env://AMS_AZURE_OPENAI_ENDPOINT and env://AMS_AZURE_OPENAI_KEY.
Write-Host 'Setting Azure OpenAI environment variables (User scope)...' -ForegroundColor Yellow
[Environment]::SetEnvironmentVariable('AMS_AZURE_OPENAI_ENDPOINT', $AzureOpenAiEndpoint, 'User')
[Environment]::SetEnvironmentVariable('AMS_AZURE_OPENAI_KEY',      $AzureOpenAiKey,      'User')
# Also set for the current session so an immediate `dotnet run` from THIS shell works.
$env:AMS_AZURE_OPENAI_ENDPOINT = $AzureOpenAiEndpoint
$env:AMS_AZURE_OPENAI_KEY      = $AzureOpenAiKey
Write-Host '  AMS_AZURE_OPENAI_ENDPOINT / AMS_AZURE_OPENAI_KEY set (User scope + current session).' -ForegroundColor Green

Write-Host ''
Write-Host 'Done.' -ForegroundColor Cyan
Write-Host 'NEXT: fully CLOSE and REOPEN Visual Studio so it inherits the new environment variables,' -ForegroundColor Magenta
Write-Host '      then F5 the Legal.Api project. See docs/e2e-testing.md for the click-through checklist.' -ForegroundColor Magenta
