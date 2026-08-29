$ErrorActionPreference='Stop'
$root='C:\Users\agenc\source\repos\POLOXI3'
$wide=Join-Path $root 'src\Ams.Application\IntelligenceWideService.cs'
$svc=Join-Path $root 'src\Ams.Application\IntelligenceService.cs'

$consts=New-Object System.Collections.Generic.List[string]
$codes=New-Object System.Collections.Generic.List[string]
$all=New-Object System.Collections.Generic.List[string]

function Add-Entry([string]$name,[string]$code,[string]$display,[string]$literal){
	$script:consts.Add('    public const string '+$name+'='+$literal+';')
	$script:codes.Add('    public const string '+$name+'="'+$code+'";')
	$script:all.Add('        [IntelligencePromptCodes.'+$name+']=("'+$display+'",'+$name+'),')
}

# ---- multi-line sites in IntelligenceWideService.cs (prompt is the full line, ends with ",) ----
$lines=[System.IO.File]::ReadAllLines($wide)
$map=@(
 @{L=1157;N='WideLlmOnlyAnswer';C='WIDE_LLM_ONLY_ANSWER';D='Wide LLM-only answer'},
 @{L=1183;N='WideLlmRawAnswer';C='WIDE_LLM_RAW_ANSWER';D='Wide raw LLM ranking'},
 @{L=1222;N='WideIntent';C='WIDE_INTENT';D='Wide intent proposal'},
 @{L=1237;N='WideHierarchyStep';C='WIDE_HIERARCHY_STEP';D='Wide hierarchy step'},
 @{L=1323;N='WideAnswer';C='WIDE_ANSWER';D='Wide final answer'},
 @{L=1751;N='WideQueryContract';C='WIDE_QUERY_CONTRACT';D='Wide query contract'},
 @{L=2178;N='WideCandidateEnumeration';C='WIDE_CANDIDATE_ENUMERATION';D='Wide candidate enumeration'},
 @{L=2214;N='WideChallengeRound';C='WIDE_CHALLENGE_ROUND';D='Wide challenge round'},
 @{L=2307;N='WideInformationValue';C='WIDE_INFORMATION_VALUE';D='Wide information value'},
 @{L=2764;N='WideCandidateMatrix';C='WIDE_CANDIDATE_MATRIX';D='Wide candidate matrix'}
)
foreach($m in $map){
	$i=[int]$m.L-1
	$t=$lines[$i].Trim()
	if(-not($t.StartsWith('"') -and $t.EndsWith('",'))){throw ('Unexpected line '+$m.L+': '+$t.Substring(0,[Math]::Min(80,$t.Length)))}
	$literal=$t.Substring(0,$t.Length-1)
	Add-Entry $m.N $m.C $m.D $literal
	$lines[$i]='                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.'+$m.N+',cancellationToken),'
}
[System.IO.File]::WriteAllLines($wide,$lines)

# ---- inline single-line sites: prompt is 3rd argument, no embedded double quotes ----
function Convert-Inline([string]$path,[int]$lineNumber,[string]$name,[string]$code,[string]$display,[string]$token){
	$ls=[System.IO.File]::ReadAllLines($path)
	$i=$lineNumber-1
	$rx=[regex]'(GenerateAsync\(request\.TenantId,"[A-Z_]+",)"([^"]*)",'
	$m=$rx.Match($ls[$i])
	if(-not $m.Success){throw ('No inline prompt at '+$path+':'+$lineNumber)}
	Add-Entry $name $code $display ('"'+$m.Groups[2].Value+'"')
	$repl=$m.Groups[1].Value+'await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.'+$name+','+$token+'),'
	$ls[$i]=$ls[$i].Substring(0,$m.Index)+$repl+$ls[$i].Substring($m.Index+$m.Length)
	[System.IO.File]::WriteAllLines($path,$ls)
}
Convert-Inline $wide 66 'WidePoloxiExplanation' 'WIDE_POLOXI_EXPLANATION' 'Wide POLOXI evidence explanation' 'cancellationToken'
Convert-Inline $wide 144 'WidePoloxiHierarchy' 'WIDE_POLOXI_HIERARCHY' 'Wide POLOXI hierarchy proposal' 'cancellationToken'
Convert-Inline $svc 81 'SearchSummary' 'SEARCH_SUMMARY' 'Search evidence summary' 'cancellationToken'
Convert-Inline $svc 144 'PoloxiExplanation' 'POLOXI_EXPLANATION' 'POLOXI evidence explanation' 'cancellationToken'
Convert-Inline $svc 220 'PoloxiHierarchy' 'POLOXI_HIERARCHY' 'POLOXI hierarchy proposal' 'cancellationToken'
Convert-Inline $svc 476 'SearchIntent' 'SEARCH_INTENT' 'Search intent interpretation' 'cancellationToken'

# ---- emit defaults file ----
$out=New-Object System.Collections.Generic.List[string]
$out.Add('namespace Ams.Application;')
$out.Add('')
$out.Add('// Prompt codes for every LLM system prompt. Prompts are user-managed in AI.PromptDefinition')
$out.Add('// (Intelligence prompt registry); these codes key the lookup.')
$out.Add('public static class IntelligencePromptCodes')
$out.Add('{')
foreach($c in $codes){$out.Add($c)}
$out.Add('}')
$out.Add('')
$out.Add('// Default system prompts, seeded into AI.PromptDefinition at startup and used as fallback when')
$out.Add('// no approved prompt row exists, so out-of-the-box behavior is unchanged.')
$out.Add('public static class IntelligencePromptDefaults')
$out.Add('{')
foreach($c in $consts){$out.Add($c)}
$out.Add('')
$out.Add('    public static readonly IReadOnlyDictionary<string,(string DisplayName,string SystemPrompt)> All=new Dictionary<string,(string DisplayName,string SystemPrompt)>')
$out.Add('    {')
foreach($a in $all){$out.Add($a)}
$out.Add('    };')
$out.Add('}')
[System.IO.File]::WriteAllLines((Join-Path $root 'src\Ams.Application\IntelligencePromptDefaults.cs'),$out)
Write-Output 'DONE'
