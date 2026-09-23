$ErrorActionPreference='Stop'
$repo='C:\Users\agenc\source\repos\POLOXI3'
$srcPages=Join-Path $repo 'src\Ams.Web\Components\Pages\Intelligence'
$dstPages=Join-Path $repo 'Legal\src\Legal.Web\Components\Pages'
New-Item -ItemType Directory -Force -Path $dstPages | Out-Null

function Copy-Page([string]$srcName,[string]$dstName,[string]$route,[string]$dstRoute){
	$c=[System.IO.File]::ReadAllText((Join-Path $srcPages $srcName))
	$c=$c -replace [regex]::Escape("@page ""$route"""),"@page ""$dstRoute"""
	# strip authorization (Legal.Web is a standalone dev host; Legal.Api still enforces policies)
	$c=$c -replace '@attribute \[Authorize[^\]]*\]\r?\n',''
	$c=$c -replace '<AuthorizeView Policy="[^"]*">',''
	$c=$c -replace '</AuthorizeView>',''
	$c=$c -replace '\bAms\.Web\b','Legal.Web'
	$c=$c -replace '\bAms\.Application\b','Legal.Application'
	$c=$c -replace '<IntelligenceWorkspace>','<LegalWorkspace>'
	$c=$c -replace '</IntelligenceWorkspace>','</LegalWorkspace>'
	[System.IO.File]::WriteAllText((Join-Path $dstPages $dstName),$c,[System.Text.UTF8Encoding]::new($true))
	"copied $dstName"
}

Copy-Page 'IntelligenceSearchPoloxiWide.razor' 'LegalSearch.razor' '/intelligence/search/poloxi_wide' '/legal/search'
Copy-Page 'IntelligenceConfiguration.razor' 'LegalConfiguration.razor' '/intelligence/configuration' '/legal/configuration'

# scoped css
Copy-Item (Join-Path $srcPages 'IntelligenceSearchPoloxiWide.razor.css') (Join-Path $dstPages 'LegalSearch.razor.css') -Force
Copy-Item (Join-Path $srcPages 'IntelligenceConfiguration.razor.css') (Join-Path $dstPages 'LegalConfiguration.razor.css') -Force
# workspace shell styles reused for the Legal workspace wrapper
Copy-Item (Join-Path $srcPages 'IntelligenceWorkspace.razor.css') (Join-Path $repo 'Legal\src\Legal.Web\Components\Shared\LegalWorkspace.razor.css') -Force
"copied css"
