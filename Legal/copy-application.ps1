$ErrorActionPreference='Stop'
$repo='C:\Users\agenc\source\repos\POLOXI3'
$src=Join-Path $repo 'src\Ams.Application'
$dst=Join-Path $repo 'Legal\src\Legal.Application'

function Copy-Cs([string]$from,[string]$to){
	New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null
	$c=[System.IO.File]::ReadAllText($from)
	$c=$c -replace 'namespace Ams\.','namespace Legal.'
	$c=$c -replace 'using Ams\.','using Legal.'
	$c=$c -replace 'Ams\.Application','Legal.Application'
	[System.IO.File]::WriteAllText($to,$c,[System.Text.UTF8Encoding]::new($true))
}

# folder copies
foreach($folder in @('Abstractions\Intelligence','Features\Intelligence')){
	$base=Join-Path $src $folder
	Get-ChildItem $base -Recurse -Filter *.cs | ForEach-Object {
		$rel=$_.FullName.Substring($base.Length).TrimStart('\')
		Copy-Cs $_.FullName (Join-Path (Join-Path $dst $folder) $rel)
	}
}

# single files
$files=@(
 'Abstractions\Persistence\IIntelligenceAbvRepository.cs',
 'Abstractions\Persistence\IIntelligenceAmbiguityRepository.cs',
 'Abstractions\Persistence\IIntelligenceRepository.cs',
 'Abstractions\Persistence\IIntelligenceWideRepository.cs',
 'Abstractions\Persistence\ISqlConnectionFactory.cs',
 'Abstractions\Services\IIntelligenceWideService.cs',
 'IntelligenceWideService.cs',
 'StandardPoloxiRetriever.cs'
)
foreach($f in $files){ Copy-Cs (Join-Path $src $f) (Join-Path $dst $f) }

Get-ChildItem $dst -Recurse -Filter *.cs | Measure-Object | Select-Object -ExpandProperty Count
