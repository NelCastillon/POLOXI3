using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

public sealed record AiProviderDto(Guid ProviderId,string ProviderCode,string DisplayName,string ProviderTypeCode,bool SupportsChat,bool SupportsEmbeddings,bool SupportsVision,bool SupportsStructuredOutput,int Priority,bool IsActive,byte[] RowVersion);
public sealed record AiModelDeploymentDto(Guid ModelDeploymentId,Guid ProviderId,string ProviderCode,string ModelCode,string DeploymentName,string ModelFamily,string CapabilityCode,int ContextWindowTokens,int MaximumOutputTokens,decimal? InputCostPerMillionTokens,decimal? OutputCostPerMillionTokens,string CurrencyCode,int Priority,bool IsFallback,bool IsActive,byte[] RowVersion);
public sealed record AiFeaturePolicyDto(Guid FeaturePolicyId,Guid TenantId,string FeatureCode,string ModuleCode,Guid? PrimaryModelDeploymentId,Guid? FallbackModelDeploymentId,decimal Temperature,int MaximumInputTokens,int MaximumOutputTokens,int TimeoutSeconds,decimal? DailyCostLimit,decimal? MonthlyCostLimit,decimal MinimumConfidence,bool RequiresHumanReview,bool IsEnabled,byte[] RowVersion);
public sealed record SaveAiFeaturePolicyRequest(Guid TenantId,[Required,StringLength(100)]string FeatureCode,[Required,StringLength(100)]string ModuleCode,Guid? PrimaryModelDeploymentId,Guid? FallbackModelDeploymentId,[Range(0,2)]decimal Temperature,[Range(1,1000000)]int MaximumInputTokens,[Range(1,1000000)]int MaximumOutputTokens,[Range(1,900)]int TimeoutSeconds,[Range(0,double.MaxValue)]decimal? DailyCostLimit,[Range(0,double.MaxValue)]decimal? MonthlyCostLimit,[Range(0,1)]decimal MinimumConfidence,bool RequiresHumanReview,bool IsEnabled,Guid ActorUserId,byte[]? RowVersion);

public sealed record AiExecutionSummaryDto(Guid ExecutionId,Guid TenantId,string FeatureCode,string ModuleCode,string? EntityTypeCode,Guid? EntityId,string StatusCode,string? ProviderCode,string? ModelCode,string? PromptVersion,long? DurationMilliseconds,int? InputTokenCount,int? OutputTokenCount,decimal? EstimatedCost,string? CurrencyCode,decimal? Confidence,int GroundingSourceCount,Guid? RequestedByUserId,DateTime StartedDateUtc,DateTime? CompletedDateUtc,string CorrelationId,string? ErrorCode,string? ErrorMessage,byte[] RowVersion);
public sealed record AiExecutionDetailDto(AiExecutionSummaryDto Execution,IReadOnlyCollection<AiGroundingSourceDto> GroundingSources,IReadOnlyCollection<AiExecutionFeedbackDto> Feedback);
public sealed record AiGroundingSourceDto(Guid GroundingSourceId,string SourceTypeCode,string? SourceEntityTypeCode,Guid? SourceEntityId,string SourceReference,string? Title,decimal? RelevanceScore);
public sealed record AiExecutionFeedbackDto(Guid FeedbackId,string FeedbackTypeCode,short? Rating,string? CorrectionReference,string? Comment,string? DecisionCode,Guid? ReviewedByUserId,DateTime? ReviewedDateUtc,DateTime CreatedDateUtc);
public sealed record SearchAiExecutionsQuery(Guid TenantId,string? SearchTerm,string? FeatureCode,string? StatusCode,DateTime? FromUtc,DateTime? ToUtc,int PageNumber=1,int PageSize=50);
public sealed record SubmitAiExecutionFeedbackRequest(Guid TenantId,Guid ExecutionId,[Required,StringLength(50)]string FeedbackTypeCode,[Range(1,5)]short? Rating,[StringLength(2000)]string? CorrectionReference,[StringLength(2000)]string? Comment,Guid ActorUserId);

public sealed record RecommendationTypeDto(Guid RecommendationTypeId,string TypeCode,string DisplayName,string? Description,string TargetModuleCode,string DefaultPriorityCode,int? DefaultExpirationHours,bool RequiresHumanReview,int SortOrder,bool IsActive);
public sealed record RecommendationDto(Guid RecommendationId,Guid TenantId,string TypeCode,string TypeName,string EntityTypeCode,Guid EntityId,string Title,string Summary,string? Rationale,string? ActionCode,string? ActionPayloadJson,string PriorityCode,string StatusCode,decimal Confidence,decimal Score,Guid? AssignedToUserId,DateTime? ExpiresDateUtc,DateTime CreatedDateUtc,byte[] RowVersion);
public sealed record SearchRecommendationsQuery(Guid TenantId,string? SearchTerm,string? TypeCode,string? StatusCode,string? EntityTypeCode,Guid? EntityId,Guid? AssignedToUserId,int PageNumber=1,int PageSize=50);
public sealed record GenerateRecommendationsRequest(Guid TenantId,[Required,StringLength(100)]string EntityTypeCode,Guid EntityId,[Required,StringLength(120)]string CorrelationId,Guid ActorUserId);
public sealed record DecideRecommendationRequest(Guid TenantId,Guid RecommendationId,[Required,StringLength(30)]string DecisionCode,[Required,StringLength(1000)]string Reason,Guid ActorUserId,byte[] RowVersion);

public sealed record IntelligenceSearchRequest(Guid TenantId,Guid UserId,[Required,StringLength(1000,MinimumLength=2)]string Query,[StringLength(100)]string? ModuleCode,[StringLength(100)]string? EntityTypeCode,[Range(1,100)]int MaximumResults=25,[Required,StringLength(120)]string CorrelationId="")
{
    public bool IncludeAiSummary { get; init; }
    public bool IncludeRelatedResults { get; init; } = true;
    public bool IsQuickSearch { get; init; }
    public string? EffectiveSearchText { get; init; }
    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];
}
public sealed record QuickSearchRequest(Guid TenantId,Guid UserId,[Required,StringLength(1000,MinimumLength=2)]string Query,[Range(1,12)]int MaximumResults=8,[Required,StringLength(120)]string CorrelationId="")
{
    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];
}
public sealed record QuickSearchResultDto(Guid SearchDocumentId,string EntityTypeCode,Guid EntityId,string ModuleCode,string Title,string NavigationRoute,decimal CombinedScore);
public sealed record QuickSearchResponse(Guid SearchQueryId,string Query,IReadOnlyCollection<QuickSearchResultDto> Results,long DurationMilliseconds);
public sealed record QuickSearchFastPathResponse(QuickSearchResponse Search,bool IntelligentFallbackRecommended);
public sealed record IntelligenceSearchResultDto(Guid SearchDocumentId,string EntityTypeCode,Guid EntityId,string ModuleCode,string Title,string? Excerpt,decimal KeywordScore,decimal SemanticScore,decimal CombinedScore,IReadOnlyCollection<SemanticConceptMatchDto> Concepts)
{
    public decimal FuzzyScore { get; init; }
    public decimal RelationshipScore { get; init; }
    public decimal RecencyScore { get; init; }
    public decimal BusinessPriorityScore { get; init; }
    public bool IsRelatedResult { get; init; }
    public string? NavigationRoute { get; init; }
    public IReadOnlyCollection<IntelligenceSearchMatchExplanationDto> Explanations { get; init; } = [];
}
public sealed record IntelligenceSearchIntentPatternDto(string PatternCode,string? EntityTypeCode,string? ModuleCode,string ExtractionStrategyCode,IReadOnlyCollection<string> MatchPhrases,IReadOnlyCollection<string> ExtractionPhrases,int Priority,bool IsEntityList);
public sealed record IntelligenceSearchIntentLogRecord(Guid TenantId,Guid UserId,string QueryText,string? EntityTypeCode,string? ModuleCode,string? SearchText,string SourceEngineCode,decimal Confidence,string StatusCode,string? ErrorMessage,string CorrelationId);
public sealed record SemanticConceptMatchDto(Guid ConceptId,string ConceptCode,string PreferredLabel,int VersionNumber,decimal Score,string MatchReasonCode);
public sealed record IntelligenceSearchMatchExplanationDto(string ReasonCode,string DisplayName,string Explanation,decimal Score,string SourceEngineCode);
public sealed record IntelligenceSearchWeightsDto(decimal KeywordWeight,decimal SemanticWeight,decimal FuzzyWeight,decimal RelationshipWeight,decimal RecencyWeight,decimal BusinessPriorityWeight)
{
    public decimal TotalWeight => KeywordWeight+SemanticWeight+FuzzyWeight+RelationshipWeight+RecencyWeight+BusinessPriorityWeight;
}
public sealed record IntelligenceSearchResponse(Guid SearchQueryId,string Query,IReadOnlyCollection<string> ExpandedTerms,IReadOnlyCollection<IntelligenceSearchResultDto> Results,long DurationMilliseconds)
{
    public string NormalizedQuery { get; init; } = string.Empty;
    public IntelligenceSearchWeightsDto EffectiveWeights { get; init; } = new(.25m,.30m,.25m,.10m,.05m,.05m);
    public string? GroundedSummary { get; init; }
    public string SummaryStatusCode { get; init; } = "NOT_REQUESTED";
    public Guid? SummaryExecutionId { get; init; }
}
public sealed record IntelligenceSearchConfiguration(IntelligenceSearchWeightsDto Weights,int RecencyWindowDays,int MaximumRelationshipResults,decimal MinimumUnifiedScore,bool EnableRules,bool EnableRelationships,bool EnableAiSummary,bool EnableLlmIntentFallback,decimal LlmIntentMinimumConfidence,int LlmIntentTimeoutSeconds,bool EnableQuickSearchIntelligentFallback,int QuickSearchFastPathMinimumResults,decimal QuickSearchFastPathMinimumScore);
public sealed record IntelligenceSearchEntityKey(string EntityTypeCode,Guid EntityId);

public sealed record AiReviewQueueItemDto(Guid ReviewQueueItemId,string ReviewTypeCode,string SourceEntityTypeCode,Guid SourceEntityId,Guid? ExecutionId,string Title,string? Summary,string PriorityCode,string StatusCode,decimal? Confidence,Guid? AssignedToUserId,DateTime? DueDateUtc,DateTime CreatedDateUtc,byte[] RowVersion);
public sealed record SearchAiReviewQueueQuery(Guid TenantId,string? SearchTerm,string? ReviewTypeCode,string? StatusCode,string? PriorityCode,Guid? AssignedToUserId,int PageNumber=1,int PageSize=50);
public sealed record DecideAiReviewRequest(Guid TenantId,Guid ReviewQueueItemId,[Required,StringLength(30)]string DecisionCode,[Required,StringLength(2000)]string Reason,Guid ActorUserId,byte[] RowVersion);

public sealed record AiEvaluationDefinitionDto(Guid EvaluationDefinitionId,Guid? TenantId,string EvaluationCode,string DisplayName,string FeatureCode,string MetricCode,string CalculationCode,decimal TargetValue,decimal WarningValue,int WindowHours,int MinimumSampleSize,bool IsActive,byte[] RowVersion);
public sealed record AiEvaluationRunDto(Guid EvaluationRunId,Guid? TenantId,string EvaluationCode,string DisplayName,string FeatureCode,string MetricCode,DateTime WindowStartUtc,DateTime WindowEndUtc,string StatusCode,int SampleCount,decimal? MetricValue,bool? Passed,string? DetailsJson,string? ErrorMessage,DateTime StartedDateUtc,DateTime? CompletedDateUtc);
public sealed record QueueAiEvaluationRequest(Guid TenantId,Guid EvaluationDefinitionId,DateTime WindowStartUtc,DateTime WindowEndUtc,Guid ActorUserId);

public sealed record IntelligenceDashboardDto(DateTime GeneratedDateUtc,int ExecutionsToday,int FailedExecutionsToday,decimal EstimatedCostToday,decimal? AverageConfidenceToday,long? AverageDurationMillisecondsToday,int OpenReviewCount,int OpenRecommendationCount,int SearchCountToday,int KnowledgeChangesToday,int ImportJobsInProgress,int WorkerQueueDepth,IReadOnlyCollection<IntelligenceUsageMetricDto> UsageByFeature,IReadOnlyCollection<RecommendationTypeMetricDto> RecommendationsByType);
public sealed record IntelligenceUsageMetricDto(string FeatureCode,int ExecutionCount,int FailedCount,decimal EstimatedCost,decimal? AverageConfidence,long? AverageDurationMilliseconds);
public sealed record RecommendationTypeMetricDto(string TypeCode,string DisplayName,int OpenCount,int AcceptedCount,int DismissedCount);

public sealed record IntelligencePillarDto(Guid IntelligencePillarId,string PillarCode,string DisplayName,string Description,int SortOrder,bool IsActive,IReadOnlyCollection<IntelligenceCapabilityDto> Capabilities);
public sealed record IntelligenceCapabilityDto(Guid IntelligenceCapabilityId,Guid IntelligencePillarId,string CapabilityCode,string DisplayName,string Description,string EngineKindCode,string OwningModuleCode,bool IsAdvisory,bool RequiresHumanReview,int SortOrder,bool IsActive);
public sealed record IntelligencePlatformSummaryDto(DateTime GeneratedDateUtc,int ActivePillarCount,int ActiveCapabilityCount,int OpenFindingCount,int OpenBusinessSignalCount,int ActiveReasoningSessionCount,int PendingWorkItemCount,IReadOnlyCollection<IntelligencePillarDto> Pillars);
public sealed record PlatformServiceCatalogDto(Guid PlatformServiceId,string ServiceCode,string DisplayName,string Description,string ServiceKindCode,string? OwningSchemaCode,string? ContractReference,string? AdministrationRoute,string MaturityCode,string ImplementationStatusCode,string? ImplementationNotes,bool IsInfrastructureOnly,bool IsActive,int SortOrder);
public sealed record BusinessModuleCatalogDto(Guid BusinessModuleId,string ModuleCode,string DisplayName,string Description,string? OwningSchemaCode,string? NavigationRoute,bool IsActive,int SortOrder,IReadOnlyCollection<ModuleServiceDependencyDto> Dependencies);
public sealed record ModuleServiceDependencyDto(Guid ModuleServiceDependencyId,Guid PlatformServiceId,string ServiceCode,string ServiceName,string UsageCode,string Description,string AdoptionStatusCode,string? ConsumerReference,DateTime? LastVerifiedDateUtc,bool IsRequired,bool IsActive);
public sealed record PlatformMigrationGapDto(Guid MigrationGapId,string GapCode,Guid PlatformServiceId,string ServiceCode,string ServiceName,Guid? BusinessModuleId,string? ModuleCode,string SourceReference,string TargetContractReference,string Description,string PriorityCode,string StatusCode,string RemediationJson,DateTime DetectedDateUtc,DateTime? CompletedDateUtc);
public sealed record PlatformArchitectureDto(DateTime GeneratedDateUtc,IReadOnlyCollection<PlatformServiceCatalogDto> Services,IReadOnlyCollection<BusinessModuleCatalogDto> BusinessModules,IReadOnlyCollection<PlatformMigrationGapDto> MigrationGaps);

public sealed record IntelligenceEnginePolicyDto(Guid EnginePolicyId,Guid? TenantId,Guid IntelligenceCapabilityId,string CapabilityCode,string PolicyCode,string DisplayName,string Description,string ExecutionModeCode,string ConfigurationJson,decimal MinimumConfidence,bool RequiresHumanReview,bool FailClosed,DateTime EffectiveFromUtc,DateTime? EffectiveToUtc,int VersionNumber,bool IsActive,byte[] RowVersion);
public sealed record SaveIntelligenceEnginePolicyRequest(Guid TenantId,Guid IntelligenceCapabilityId,[Required,StringLength(120)]string PolicyCode,[Required,StringLength(200)]string DisplayName,[Required,StringLength(2000)]string Description,[Required,StringLength(30)]string ExecutionModeCode,[Required]string ConfigurationJson,[Range(0,1)]decimal MinimumConfidence,bool RequiresHumanReview,bool FailClosed,DateTime EffectiveFromUtc,DateTime? EffectiveToUtc,[Range(1,int.MaxValue)]int VersionNumber,bool IsActive,Guid ActorUserId,byte[]? RowVersion);
public sealed record IntelligenceSafetyControlDto(Guid SafetyControlId,Guid? TenantId,string ControlCode,string DisplayName,string Description,string ControlTypeCode,string EnforcementStageCode,string ConfigurationJson,string ViolationActionCode,bool RequiresHumanReview,int SortOrder,bool IsActive,byte[] RowVersion);
public sealed record SaveIntelligenceSafetyControlRequest(Guid TenantId,[Required,StringLength(120)]string ControlCode,[Required,StringLength(200)]string DisplayName,[Required,StringLength(2000)]string Description,[Required,StringLength(50)]string ControlTypeCode,[Required,StringLength(50)]string EnforcementStageCode,[Required]string ConfigurationJson,[Required,StringLength(50)]string ViolationActionCode,bool RequiresHumanReview,[Range(1,int.MaxValue)]int SortOrder,bool IsActive,Guid ActorUserId,byte[]? RowVersion);
public sealed record IntelligenceComplianceRequirementDto(Guid ComplianceRequirementId,string RequirementCode,string DisplayName,string Description,string RequirementScopeCode,string? JurisdictionCode,Guid? CarrierId,string? LineOfBusinessCode,string EntityTypeCode,string RequirementTypeCode,string SeverityCode,bool BlocksTransaction,bool CanBeWaived,string? WaiverPermissionCode,string? ApprovalPermissionCode,int VersionNumber,bool IsActive,byte[] RowVersion);
public sealed record IntelligenceSafetyEventDto(Guid SafetyEventId,string ControlCode,string ControlName,string EventTypeCode,string EnforcementStageCode,string ActionCode,string SeverityCode,bool RequiresHumanReview,string? ReviewStatusCode,DateTime DetectedDateUtc);
public sealed record IntelligencePromptDefinitionDto(Guid PromptDefinitionId,Guid? TenantId,Guid IntelligenceCapabilityId,string CapabilityCode,string PromptCode,string VersionLabel,string DisplayName,string SystemInstructions,string InputSchemaJson,string OutputSchemaJson,string StatusCode,Guid? ApprovedByUserId,DateTime? ApprovedDateUtc,DateTime? EffectiveFromUtc,DateTime? EffectiveToUtc,byte[] RowVersion);
public sealed record SaveIntelligencePromptDefinitionRequest(Guid TenantId,Guid IntelligenceCapabilityId,[Required,StringLength(120)]string PromptCode,[Required,StringLength(30)]string VersionLabel,[Required,StringLength(200)]string DisplayName,[Required]string SystemInstructions,[Required]string InputSchemaJson,[Required]string OutputSchemaJson,[Required,StringLength(30)]string StatusCode,DateTime? EffectiveFromUtc,DateTime? EffectiveToUtc,Guid ActorUserId,byte[]? RowVersion);
public sealed record SubmitEvaluationSampleLabelRequest(Guid TenantId,Guid ExecutionId,Guid? EvaluationDefinitionId,bool PredictedPositive,bool ActualPositive,bool IsHallucination,bool IsAccurate,[Required,StringLength(50)]string LabelSourceCode,[StringLength(2000)]string? Notes,Guid ActorUserId);

public sealed record IntelligenceFindingDto(Guid IntelligenceFindingId,Guid TenantId,string CapabilityCode,string CapabilityName,string EntityTypeCode,Guid EntityId,string FindingTypeCode,string SeverityCode,string StatusCode,string Title,string Summary,string Explanation,decimal? Score,decimal? Confidence,string? RuleVersion,DateTime DetectedDateUtc,DateTime? DueDateUtc,DateTime? ResolvedDateUtc,string? ResolutionCode,byte[] RowVersion);
public sealed record IntelligenceFindingEvidenceDto(Guid FindingEvidenceId,string EvidenceTypeCode,string SourceModuleCode,string? SourceEntityTypeCode,Guid? SourceEntityId,string SourceReference,string Description,string? EvidenceValueJson,decimal? RelevanceScore);
public sealed record IntelligenceFindingDetailDto(IntelligenceFindingDto Finding,IReadOnlyCollection<IntelligenceFindingEvidenceDto> Evidence);
public sealed record SearchIntelligenceFindingsQuery(Guid TenantId,string? SearchTerm,string? CapabilityCode,string? EntityTypeCode,Guid? EntityId,string? SeverityCode,string? StatusCode,int PageNumber=1,int PageSize=50);
public sealed record DecideIntelligenceFindingRequest(Guid TenantId,Guid IntelligenceFindingId,[Required,StringLength(50)]string ResolutionCode,[Required,StringLength(2000)]string ResolutionNotes,Guid ActorUserId,byte[] RowVersion);

public sealed record EntityRelationshipDto(Guid EntityRelationshipId,string SourceEntityTypeCode,Guid SourceEntityId,string RelationshipTypeCode,string TargetEntityTypeCode,Guid TargetEntityId,string SourceModuleCode,string SourceReference,decimal Strength,DateTime? EffectiveFromUtc,DateTime? EffectiveToUtc,DateTime LastSynchronizedDateUtc);
public sealed record EntityRelationshipGraphDto(string RootEntityTypeCode,Guid RootEntityId,int MaximumDepth,IReadOnlyCollection<EntityRelationshipDto> Relationships);
public sealed record EntitySimilarityDto(Guid EntitySimilarityId,string EntityTypeCode,Guid SourceEntityId,Guid SimilarEntityId,string SimilarityModelCode,string SimilarityModelVersion,decimal SimilarityScore,string FeatureEvidenceJson,DateTime CalculatedDateUtc,DateTime? ExpiresDateUtc);
public sealed record RelationshipQuery(Guid TenantId,Guid UserId,[Required,StringLength(100)]string EntityTypeCode,Guid EntityId,[Range(1,10)]int MaximumDepth=3);
public sealed record SimilarityQuery(Guid TenantId,Guid UserId,[Required,StringLength(100)]string EntityTypeCode,Guid EntityId,[Range(0,1)]decimal MinimumScore=0.7m,[Range(1,100)]int MaximumResults=25);

public sealed record BusinessIntelligenceSignalDto(Guid BusinessSignalId,Guid TenantId,string CapabilityCode,string CapabilityName,string EntityTypeCode,Guid EntityId,string SignalTypeCode,DateTime SignalDateUtc,string SeverityCode,decimal? Score,decimal? Confidence,string Title,string Summary,string EvidenceJson,string? RecommendedActionCode,string StatusCode,Guid? AssignedToUserId,DateTime? DueDateUtc,byte[] RowVersion);
public sealed record SearchBusinessIntelligenceSignalsQuery(Guid TenantId,string? SearchTerm,string? CapabilityCode,string? EntityTypeCode,Guid? EntityId,string? SeverityCode,string? StatusCode,Guid? AssignedToUserId,int PageNumber=1,int PageSize=50);
public sealed record DecideBusinessIntelligenceSignalRequest(Guid TenantId,Guid BusinessSignalId,[Required,StringLength(50)]string DecisionCode,[Required,StringLength(2000)]string DecisionNotes,Guid ActorUserId,byte[] RowVersion);

public sealed record InsuranceReasoningRequest(Guid TenantId,Guid UserId,[Required,StringLength(100)]string EntityTypeCode,Guid EntityId,[Required,StringLength(2000,MinimumLength=3)]string Question,[Required,StringLength(120)]string CorrelationId,IReadOnlyCollection<string> GrantedPermissions);
public sealed record InsuranceReasoningEvidenceDto(Guid ReasoningEvidenceId,string EvidenceTypeCode,string SourceModuleCode,string? SourceEntityTypeCode,Guid? SourceEntityId,string SourceReference,string Title,string Summary,string? EvidenceValueJson,decimal RelevanceScore,bool IsAuthoritative);
public sealed record InsuranceReasoningConclusionDto(Guid ReasoningConclusionId,string ConclusionCode,int SequenceNumber,string Title,string Explanation,string? RuleCode,string? RuleVersion,decimal Confidence,bool IsBlocking,bool CanBeWaived,string? WaiverPermissionCode);
public sealed record InsuranceReasoningActionDto(Guid ReasoningActionId,int SequenceNumber,string ActionCode,string DisplayName,string Description,string? TargetRoute,string? RequiredPermissionCode,bool RequiresConfirmation,bool IsAvailable,string? UnavailableReason);
public sealed record InsuranceReasoningResponse(Guid ReasoningSessionId,string EntityTypeCode,Guid EntityId,string Question,string IntentCode,string StatusCode,string CorrelationId,decimal? Confidence,bool RequiresHumanReview,DateTime StartedDateUtc,DateTime? CompletedDateUtc,IReadOnlyCollection<InsuranceReasoningEvidenceDto> Evidence,IReadOnlyCollection<InsuranceReasoningConclusionDto> Conclusions,IReadOnlyCollection<InsuranceReasoningActionDto> Actions);
