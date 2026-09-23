$ErrorActionPreference='Stop'
$repo='C:\Users\agenc\source\repos\POLOXI3'
$srcDir=Join-Path $repo 'src\Ams.Infrastructure\Migrations'
$dstDir=Join-Path $repo 'Legal\src\Legal.Infrastructure\Migrations'
New-Item -ItemType Directory -Force -Path $dstDir | Out-Null

$aiTables=@('BusinessSignal','ComplianceRequirement','EnginePolicy','EntityRelationship','EntitySimilarity','EvaluationDefinition','EvaluationRun','EvaluationSampleLabel','ExecutionFeedback','ExecutionGroundingSource','Execution','FeaturePolicy','FindingEvidence','IntelligenceCapability','IntelligenceFinding','IntelligencePillar','IntelligenceWorkItem','ModelDeployment','ModuleMetricSnapshot','PromptDefinition','Provider','ReasoningAction','ReasoningConclusion','ReasoningEvidence','ReasoningSession','RecommendationEvidence','RecommendationRule','RecommendationType','RecommendationWorkItem','Recommendation','ReviewQueueItem','SafetyControl','SafetyEvent','SearchDocument','SearchIntentInterpretationLog','SearchIntentPatternPhrase','SearchIntentPattern','SearchPermission','SearchQuery','SearchResultEvidence')
$poloxiTables=@('AbvActionCatalog','AbvDomainPack','AbvIntentTaxonomy','AbvOwnerMapping','AbvResolution','AbvUrgencyPolicy','Abv','AmbiguityModelInvocation','AmbiguityNodeDependency','AmbiguityNode','AmbiguityRun','AmbiguityValidationIssue','AnswerKind','Capability','ExecutionBranchOutcome','ExecutionEvidence','Execution','ExternalKnowledge','HierarchyBranch','Hierarchy','ModelCapabilityProfile','PromptStrategy','WideBranch','WideCandidateBranchScore','WideCandidate','WideExecution','WideInformationPrediction','WideInformationRound','WideInformationTarget','WideNarrowingIteration')

function Rename-Tables([string]$c){
	foreach($t in $aiTables){ $c=$c -replace "\bAI\.$t\b","AI.Legal_$t" }
	foreach($t in $poloxiTables){ $c=$c -replace "\bPOLOXI\.$t\b","POLOXI.Legal_$t" }
	$c=$c -replace '\bCore\.ConfigurationSetting\b','Core.Legal_ConfigurationSetting'
	return $c
}

$names=@('0081_EnterpriseIntelligencePlatform.sql','0082_IntelligenceSearchIntent.sql','0083_AgencyBinderIntelligencePillars.sql','0084_IntelligenceScenarioCoverage.sql','0085_IntelligencePlatformCompletion.sql','0086_IntelligenceDiscoveryConfiguration.sql','0087_IntelligenceScenarioCompletion.sql','0095_UnifiedIntelligenceSearch.sql','0097_IntelligenceSearchProjectionBackfill.sql','0138_IntelligenceSearchAiRouteSeed.sql','0139_IntelligentSearchPoloxi.sql','0141_PoloxiHierarchyDeterministicTemperature.sql','0142_IntelligenceWideDynamicHierarchy.sql','0143_IntelligenceWideAnswerOutputBudget.sql','0144_IntelligenceWideExternalKnowledge.sql','0145_IntelligenceWideAnswerInputBudget.sql','0146_IntelligenceWideV21.sql','0147_IntelligenceWideConcurrency.sql','0148_IntelligenceWideHierarchyOutputBudget.sql','0149_IntelligenceWideInformationValue.sql','0150_IntelligenceWideSemanticEntropy.sql','0151_IntelligenceWideEntropyAudit.sql','0152_IntelligenceWideClarification.sql','0153_IntelligenceWideClarificationCalibration.sql','0154_IntelligenceSafetyOutputBudget.sql','0155_IntelligenceWideAdaptiveNarrowing.sql','0156_IntelligenceWideInformationTargetBreadth.sql','0157_IntelligenceWideAnswerKindRouting.sql','0158_IntelligenceWideClarificationCalibration.sql','0159_IntelligenceWideAnswerKindTable.sql','0160_IntelligenceWideContinuationState.sql','0161_IntelligenceWideModelSelection.sql','0162_IntelligenceQueryLength4000.sql','0163_IntelligenceWideEvidenceSupportCalibration.sql','0164_IntelligenceWideInformationCriterionWeights.sql','0165_IntelligenceWideChallengeRound.sql','0166_IntelligenceWideBranchRoleMarginalStop.sql','0167_IntelligenceWideReasoningModelOutputBudget.sql','0168_IntelligenceWideInterpretationWeightBalance.sql','0169_PoloxiWideConvergenceOutcome.sql','0170_PoloxiAdaptiveAmbiguity.sql','0171_PoloxiMiniHeavyDiscoveryPrompt.sql','0172_PoloxiPremiumLightDiscoveryPrompt.sql','0173_PoloxiAbvActionLayer.sql','0174_IntelligenceWideDisableClarificationGate.sql','0175_IntelligenceWideDeliverableSynthesis.sql','0176_IntelligenceWideDeliverableSynthesisIndicators.sql','0177_IntelligenceWideSemanticRoles.sql','0178_IntelligenceWideEntityRankingHierarchyPrompts.sql','0179_IntelligenceWideDeclarativeRankingContractPrompt.sql')

$leaks=@{}
foreach($n in $names){
	$c=[System.IO.File]::ReadAllText((Join-Path $srcDir $n))
	$c=Rename-Tables $c
	# detect any remaining schema-qualified object references that were NOT renamed (potential shared-table writes)
	$m=[regex]::Matches($c,'\b(AI|POLOXI|Core|Intelligence|dbo|DMS|Master|CRM|Iam|Finance|Documents)\.(?!Legal_)([A-Za-z_][A-Za-z0-9_]*)\b')
	$vals=$m | ForEach-Object { $_.Value } | Where-Object { $_ -notmatch '^(Intelligence)\.' } | Sort-Object -Unique
	if($vals){ $leaks[$n]=$vals }
	[System.IO.File]::WriteAllText((Join-Path $dstDir $n),$c,[System.Text.UTF8Encoding]::new($true))
}
"--- potential unrenamed references ---"
$leaks.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0}: {1}" -f $_.Key, ($_.Value -join ', ') }

