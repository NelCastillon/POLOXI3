using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

// Contracts for the isolated "Intelligent Search Wide" dynamic progressive disambiguation pipeline.
// Pipeline: Ambiguous Intent -> Dynamic LLM Hierarchy -> Progressive Disambiguation -> Enterprise Grounding
//           -> Candidate Elimination -> Confidence -> Verified Answer / Governed Action.

public sealed record WideSearchRequest(Guid TenantId,Guid UserId,[Required,StringLength(1000,MinimumLength=2)]string Query,[Range(1,100)]int MaximumResults=25,[Required,StringLength(120)]string CorrelationId="")
{
    public IReadOnlyCollection<string> GrantedPermissions{get;init;}=[];
    // 'EPH Engine' filter: true runs the full dynamic disambiguation + enterprise grounding pipeline;
    // false returns a pure LLM answer without hierarchy, grounding, or elimination.
    public bool UseEphEngine{get;init;}=true;
}

public sealed record WideBranchDto(Guid WideBranchId,Guid? ParentWideBranchId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder);

public sealed record WideSearchResponse(Guid WideExecutionId,string Query,string StatusCode,string TerminationReasonCode,int DepthReached,int LlmCallCount,decimal FinalConfidence,string AnswerVerificationCode,string? FinalAnswer,IReadOnlyCollection<WideBranchDto> Branches,IReadOnlyCollection<EphEvidenceDto> Evidence,IReadOnlyCollection<WideActionSuggestionDto> SuggestedActions,long DurationMilliseconds)
{
    // Real-world references produced by the LLM from the top interpretive narrowing paths.
    // Displayed in Authorized Evidence with links to the actual external sites; never enterprise-verified.
    public IReadOnlyCollection<WideExternalReferenceDto> ExternalReferences{get;init;}=[];
    // Actual LLM-answered result sets for the top interpretive narrowing paths (branch sub-header text fed
    // back to the LLM). Displayed in Authorized Evidence as complete result sets, ordered by branch scoring;
    // never enterprise-verified.
    public IReadOnlyCollection<WideInterpretiveResultDto> InterpretiveResults{get;init;}=[];
    // Fresh external snippets that grounded the answer (Stage 2.5); empty when live grounding is disabled.
    public IReadOnlyCollection<WideExternalKnowledgeSnippet> ExternalKnowledge{get;init;}=[];
}

public sealed record WideExternalReferenceDto(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResultDto(string BranchDisplayName,string Interpretation,decimal Confidence,IReadOnlyCollection<WideInterpretiveResultItemDto> Items)
{
    // STABLE: durable knowledge. TIME_SENSITIVE: prices, rates, rankings, availability - figures may be outdated.
    public string DataVolatility{get;init;}="STABLE";
    // True when the result set was composed from live external retrieval (Stage 2.5), not LLM recall.
    public bool IsExternallyGrounded{get;init;}
}

public sealed record WideInterpretiveResultItemDto(int RankNumber,string Name,string Detail);

public sealed record WideActionSuggestionDto(string DisplayName,string NavigationRoute,string Rationale);

// Wide pipeline configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
public sealed record WideConfiguration(decimal TargetConfidence,decimal MinimumBranchConfidence,int MaximumBranchesPerLevel,int AbsoluteDepthCeiling,int MaximumTotalLlmCalls);

// Stage 2.5 external grounding configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
// A blank ApiKey or Enabled=false disables live retrieval; the pipeline degrades to interpretive-only answers.
public sealed record WideExternalGroundingConfiguration(bool Enabled,string ProviderCode,string ApiKey,int MaximumQueriesPerExecution,int MaximumSnippetsPerQuery,int CacheHours,int TimeoutSeconds);

// A fresh real-world snippet retrieved at answer time (live provider call or EPH.ExternalKnowledge cache hit).
public sealed record WideExternalKnowledgeSnippet(string Query,string Title,string Url,string Snippet,decimal Score,DateTime RetrievedDateUtc);

// LLM structured outputs (strict JSON schema payloads).
public sealed record WideProposedBranch(string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,decimal Confidence,bool ContinueNarrowing,string? StopReason,string? ParentBranchCode);

public sealed record WideIntentProposal(string ConceptCode,string DisplayName,decimal AmbiguityScore,IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideLevelProposal(IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideAnswerProposal(string Answer,string VerificationCode,decimal Confidence,IReadOnlyCollection<WideAnswerAction> SuggestedActions,IReadOnlyCollection<int> RelevantEvidenceNumbers)
{
    public IReadOnlyCollection<WideExternalReference> ExternalReferences{get;init;}=[];
    public IReadOnlyCollection<WideInterpretiveResult> InterpretiveResults{get;init;}=[];
}

public sealed record WideExternalReference(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResult(string BranchDisplayName,string Interpretation,IReadOnlyCollection<WideInterpretiveResultItem> Items)
{
    public string DataVolatility{get;init;}="STABLE";
}

public sealed record WideInterpretiveResultItem(int RankNumber,string Name,string Detail);

public sealed record WideAnswerAction(string DisplayName,string NavigationRoute,string Rationale);

// Persistence records.
public sealed record WideExecutionStart(Guid TenantId,Guid UserId,string QueryText,string CorrelationId);

public sealed record WideBranchRecord(Guid WideBranchId,Guid WideExecutionId,Guid? ParentWideBranchId,Guid TenantId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder);
