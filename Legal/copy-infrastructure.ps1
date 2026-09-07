$ErrorActionPreference='Stop'
$repo='C:\Users\agenc\source\repos\POLOXI3'
$src=Join-Path $repo 'src\Ams.Infrastructure'
$dst=Join-Path $repo 'Legal\src\Legal.Infrastructure'

$aiTables=@('BusinessSignal','ComplianceRequirement','EnginePolicy','EntityRelationship','EntitySimilarity','EvaluationDefinition','EvaluationRun','EvaluationSampleLabel','ExecutionFeedback','ExecutionGroundingSource','Execution','FeaturePolicy','FindingEvidence','IntelligenceCapability','IntelligenceFinding','IntelligencePillar','IntelligenceWorkItem','ModelDeployment','ModuleMetricSnapshot','PromptDefinition','Provider','ReasoningAction','ReasoningConclusion','ReasoningEvidence','ReasoningSession','RecommendationEvidence','RecommendationRule','RecommendationType','RecommendationWorkItem','Recommendation','ReviewQueueItem','SafetyControl','SafetyEvent','SearchDocument','SearchIntentInterpretationLog','SearchIntentPatternPhrase','SearchIntentPattern','SearchPermission','SearchQuery','SearchResultEvidence')
$poloxiTables=@('AbvActionCatalog','AbvDomainPack','AbvIntentTaxonomy','AbvOwnerMapping','AbvResolution','AbvUrgencyPolicy','Abv','AmbiguityModelInvocation','AmbiguityNodeDependency','AmbiguityNode','AmbiguityRun','AmbiguityValidationIssue','AnswerKind','Capability','ExecutionBranchOutcome','ExecutionEvidence','Execution','ExternalKnowledge','HierarchyBranch','Hierarchy','ModelCapabilityProfile','PromptStrategy','WideBranch','WideCandidateBranchScore','WideCandidate','WideExecution','WideInformationPrediction','WideInformationRound','WideInformationTarget','WideNarrowingIteration')

function Rename-Tables([string]$c){
	foreach($t in $aiTables){ $c=$c -replace "\bAI\.$t\b","AI.Legal_$t" }
	foreach($t in $poloxiTables){ $c=$c -replace "\bPOLOXI\.$t\b","POLOXI.Legal_$t" }
	$c=$c -replace '\bCore\.ConfigurationSetting\b','Core.Legal_ConfigurationSetting'
	return $c
}

function Copy-Cs([string]$from,[string]$to){
	New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null
	$c=[System.IO.File]::ReadAllText($from)
	$c=$c -replace 'namespace Ams\.','namespace Legal.'
	$c=$c -replace 'using Ams\.','using Legal.'
	$c=$c -replace 'Ams\.Application','Legal.Application'
	$c=$c -replace 'Ams\.Infrastructure','Legal.Infrastructure'
	$c=Rename-Tables $c
	[System.IO.File]::WriteAllText($to,$c,[System.Text.UTF8Encoding]::new($true))
}

$files=@(
 'Persistence\ConnectionFactory\SqlConnectionFactory.cs',
 'Persistence\Repositories\IntelligenceRepository.cs',
 'Persistence\Repositories\IntelligenceRepository.Abv.cs',
 'Persistence\Repositories\IntelligenceRepository.Ambiguity.cs',
 'Persistence\Repositories\IntelligenceRepository.Platform.cs',
 'Persistence\Repositories\IntelligenceRepository.Poloxi.cs',
 'Persistence\Repositories\IntelligenceWideRepository.cs',
 'Persistence\Repositories\AiProviderRouteRepository.cs',
 'Services\AiProviderRouter.cs',
 'Services\AzureOpenAiProvider.cs',
 'Services\PromptCatalog.cs',
 'Intelligence\TavilyExternalKnowledgeProvider.cs'
)
foreach($f in $files){ Copy-Cs (Join-Path $src $f) (Join-Path $dst $f) }
Write-Output "copied $($files.Count) files"

