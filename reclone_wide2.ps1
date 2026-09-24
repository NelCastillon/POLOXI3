$root = 'C:\Users\agenc\source\repos\POLOXI3LEGALPI\Legal'
Set-Location $root

# source -> dest pairs (all Wide-pipeline orchestration files, contracts EXCLUDED - shared)
$pairs = @(
  @('src\Legal.Application\Abstractions\Services\IIntelligenceWideService.cs',                 'src\Legal.Application\Abstractions\Services\IIntelligenceWide2Service.cs'),
  @('src\Legal.Application\Abstractions\Persistence\IIntelligenceWideRepository.cs',           'src\Legal.Application\Abstractions\Persistence\IIntelligenceWide2Repository.cs'),
  @('src\Legal.Application\IntelligenceWideService.cs',                                        'src\Legal.Application\IntelligenceWide2Service.cs'),
  @('src\Legal.Application\IntelligenceWideService.LegalAuthorities.cs',                       'src\Legal.Application\IntelligenceWide2Service.LegalAuthorities.cs'),
  @('src\Legal.Application\IntelligenceWideService.Astra.cs',                                  'src\Legal.Application\IntelligenceWide2Service.Astra.cs'),
  @('src\Legal.Infrastructure\Persistence\Repositories\IntelligenceWideRepository.cs',        'src\Legal.Infrastructure\Persistence\Repositories\IntelligenceWide2Repository.cs'),
  @('src\Legal.Api\Services\WideSearchOperationStore.cs',                                      'src\Legal.Api\Services\Wide2SearchOperationStore.cs'),
  @('src\Legal.Api\Controllers\IntelligenceWideController.cs',                                 'src\Legal.Api\Controllers\IntelligenceWide2Controller.cs')
)

foreach ($p in $pairs) {
	$text = Get-Content $p[0] -Raw
	$text = $text -replace 'IIntelligenceWideService',  'IIntelligenceWide2Service'
	$text = $text -replace 'IntelligenceWideService',   'IntelligenceWide2Service'
	$text = $text -replace 'IIntelligenceWideRepository','IIntelligenceWide2Repository'
	$text = $text -replace 'IntelligenceWideRepository', 'IntelligenceWide2Repository'
	$text = $text -replace 'IntelligenceWideController', 'IntelligenceWide2Controller'
	$text = $text -replace 'WideSearchOperationStore',   'Wide2SearchOperationStore'
	$text = $text -replace 'api/intelligence_wide',      'api/intelligence_wide2'
	Set-Content -Path $p[1] -Value $text
	Write-Output ("wrote {0} bytes -> {1}" -f (Get-Item $p[1]).Length, $p[1])
}
Write-Output 'DONE'
