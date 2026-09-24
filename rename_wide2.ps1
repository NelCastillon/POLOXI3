$root = 'C:\Users\agenc\source\repos\POLOXI3LEGALPI\Legal'
Set-Location $root

$files = @(
  'src\Legal.Application\Abstractions\Services\IIntelligenceWide2Service.cs',
  'src\Legal.Application\Abstractions\Persistence\IIntelligenceWide2Repository.cs',
  'src\Legal.Application\IntelligenceWide2Service.cs',
  'src\Legal.Application\IntelligenceWide2Service.LegalAuthorities.cs',
  'src\Legal.Application\IntelligenceWide2Service.Astra.cs',
  'src\Legal.Infrastructure\Persistence\Repositories\IntelligenceWide2Repository.cs',
  'src\Legal.Api\Services\Wide2SearchOperationStore.cs',
  'src\Legal.Api\Controllers\IntelligenceWide2Controller.cs'
)

foreach ($f in $files) {
	$text = Get-Content $f -Raw
	# Order matters: rename the more specific store name first, then the generic Wide->Wide2 for the key types.
	$text = $text -replace 'IIntelligenceWideService', 'IIntelligenceWide2Service'
	$text = $text -replace 'IntelligenceWideService', 'IntelligenceWide2Service'
	$text = $text -replace 'IIntelligenceWideRepository', 'IIntelligenceWide2Repository'
	$text = $text -replace 'IntelligenceWideRepository', 'IntelligenceWide2Repository'
	$text = $text -replace 'IntelligenceWideController', 'IntelligenceWide2Controller'
	$text = $text -replace 'WideSearchOperationStore', 'Wide2SearchOperationStore'
	Set-Content -Path $f -Value $text -NoNewline
}
Write-Output 'types renamed'
