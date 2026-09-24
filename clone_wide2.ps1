$ns = 'namespace Legal.Application.Features.Intelligence.Wide2;'
$parentUsing = 'using Legal.Application.Features.Intelligence;'

function Set-Wide2Namespace([string]$path, [string]$oldNs) {
	$lines = Get-Content $path
	$out = New-Object System.Collections.Generic.List[string]
	foreach ($l in $lines) {
		if ($l -eq "namespace $oldNs;") { $out.Add($ns) }
		elseif ($l -eq $parentUsing) { }
		else { $out.Add($l) }
	}
	Set-Content -Path $path -Value $out
}

Set-Wide2Namespace 'src\Legal.Application\Abstractions\Services\IIntelligenceWide2Service.cs' 'Legal.Application.Abstractions.Services'
Set-Wide2Namespace 'src\Legal.Application\Abstractions\Persistence\IIntelligenceWide2Repository.cs' 'Legal.Application.Abstractions.Persistence'
Set-Wide2Namespace 'src\Legal.Application\IntelligenceWide2Service.cs' 'Legal.Application'
Set-Wide2Namespace 'src\Legal.Application\IntelligenceWide2Service.LegalAuthorities.cs' 'Legal.Application'
Set-Wide2Namespace 'src\Legal.Application\IntelligenceWide2Service.Astra.cs' 'Legal.Application'
Set-Wide2Namespace 'src\Legal.Infrastructure\Persistence\Repositories\IntelligenceWide2Repository.cs' 'Legal.Infrastructure.Persistence.Repositories'
Set-Wide2Namespace 'src\Legal.Api\Services\Wide2SearchOperationStore.cs' 'Legal.Api.Services'
Set-Wide2Namespace 'src\Legal.Api\Controllers\IntelligenceWide2Controller.cs' 'Legal.Api.Controllers'

Write-Output 'namespaces set'
