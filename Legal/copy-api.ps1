$ErrorActionPreference='Stop'
$repo='C:\Users\agenc\source\repos\POLOXI3'
$src=Join-Path $repo 'src\Ams.Api'
$dst=Join-Path $repo 'Legal\src\Legal.Api'

function Copy-Renamed([string]$rel,[string]$relOut){
	$c=[System.IO.File]::ReadAllText((Join-Path $src $rel))
	$c=$c -replace '\bAms\.Api\b','Legal.Api'
	$c=$c -replace '\bAms\.Application\b','Legal.Application'
	$c=$c -replace '\bAms\.Infrastructure\b','Legal.Infrastructure'
	$out=Join-Path $dst $relOut
	New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
	[System.IO.File]::WriteAllText($out,$c,[System.Text.UTF8Encoding]::new($true))
	"copied $relOut"
}

Copy-Renamed 'Security\AuthenticatedRequestContext.cs' 'Security\AuthenticatedRequestContext.cs'
Copy-Renamed 'Security\DevelopmentAuthenticationHandler.cs' 'Security\DevelopmentAuthenticationHandler.cs'
Copy-Renamed 'Security\IntelligenceAuthorization.cs' 'Security\IntelligenceAuthorization.cs'
Copy-Renamed 'Services\WideSearchOperationStore.cs' 'Services\WideSearchOperationStore.cs'
Copy-Renamed 'Middlewares\ExceptionHandlingMiddleware.cs' 'Middlewares\ExceptionHandlingMiddleware.cs'
Copy-Renamed 'Controllers\IntelligenceWideController.cs' 'Controllers\IntelligenceWideController.cs'
