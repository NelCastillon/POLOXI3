using Legal.Application.Features.Intelligence;

namespace Legal.Application;

public sealed partial class IntelligenceWide2Service
{
    public const string AstraModelCode="gpt-6-astra";

    // The registry migration uses the same schemas passed to the provider, not copied JSON contracts.
    public static IReadOnlyDictionary<string,(string OutputSchemaJson,string Instructions)> AstraPromptContracts { get; }=
        new Dictionary<string,(string,string)>
        {
            [IntelligencePromptCodes.WideQueryContract]=(QueryContractSchema,"Extract the query contract only. Preserve the requested answer kind, candidate identity, constraints, and output requirements; do not answer or rank candidates in this stage."),
            [IntelligencePromptCodes.WideIntent]=(IntentSchema,"Return the initial concept and branches only. Named candidates remain candidates, not branches. Distinguish jointly applicable dimensions from mutually exclusive alternatives."),
            [IntelligencePromptCodes.WideHierarchyStep]=(LevelSchema,"Return child branches only, linked to the supplied parent branch codes. Preserve the fixed query contract and branch roles; do not restart the hierarchy or invent candidate rankings."),
            [IntelligencePromptCodes.WideAnswer]=(AnswerSchema,"Populate answer with the substantive user-facing response, not a plan or JSON description. Populate interpretiveResults only as the canonical task requires; preserve branch attribution and candidate identity. The downstream Candidate × Branch competition owns the final ranking. Do not claim model-generated content is verified evidence."),
            [IntelligencePromptCodes.WideLlmOnlyAnswer]=(AnswerSchema,"Populate answer with the complete direct response. This path has no enterprise grounding; retain INTERPRETIVE verification and do not invent records or evidence."),
            [IntelligencePromptCodes.WideLlmRawAnswer]=(AnswerSchema,"Populate answer and the canonical interpretive result list from the user's query alone. This is an ungrounded comparison baseline, not the pipeline's authoritative ranking."),
            [IntelligencePromptCodes.WideCandidateEnumeration]=(CandidateEnumerationSchema,"Propose specific candidate identities that satisfy the fixed query contract. A proposal is not evidence admission; do not invent support or replace the downstream scoring process."),
            [IntelligencePromptCodes.WideChallengeRound]=(ChallengeVerdictSchema,"Evaluate the supplied candidates against the stated challenge. Preserve identities and evidence attribution; do not treat unsupported speculation as a validated contradiction."),
            [IntelligencePromptCodes.WideInformationValue]=(InformationValueSchema,"Estimate the supplied targets using the schema's categorical values. Ranking-change predictions are falsifiable estimates, not measured gains or permission to override deterministic stopping rules."),
            [IntelligencePromptCodes.WideCandidateMatrix]=(CandidateScoringSchema,"Score only the supplied candidate and branch pairs against actual decision criteria. Preserve candidate names and branch attribution. Never convert context or disambiguation labels into unsupported scoring criteria."),
            [IntelligencePromptCodes.WideLegalAnswer]=(LegalAnswerSchema,"Populate the legal answer fields with the substantive conclusion, analysis, application, and bottom line. Attribute legal propositions only to the supplied verified authorities; state missing facts and uncertainty without fabricating citations."),
            [IntelligencePromptCodes.WideLegalAuthorityProposal]=(LegalAuthorityProposalSchema,"Propose authorities for targeted retrieval only. A proposed authority is not verified evidence. Preserve the distinction between authority identity and support for a particular legal proposition.")
        };

    private static bool UsesAstra(WideSearchRequest request)=>string.Equals(request.ModelCode?.Trim(),AstraModelCode,StringComparison.OrdinalIgnoreCase);

    // Astra runs reuse the canonical Sol prompt set: the model differs (gpt-6-astra via the model
    // override), but the staged specifications are identical. The '_ASTRA' prompt clones remain in
    // the registry for operator editing but are no longer dispatched at runtime, because their
    // prepended directive degraded every non-SINGLE_ANSWER answer-kind pipeline.
    private Task<string> GetWideSystemPromptAsync(WideSearchRequest request,string promptCode,CancellationToken cancellationToken)=>
        promptCatalog.GetSystemPromptAsync(request.TenantId,promptCode,cancellationToken);

    // Astra reuses the shared Wide answer policy (same budgets as Sol); the Astra model still runs
    // through the model override, so no route or deployment changes.
    private static string AnswerFeatureCode(WideSearchRequest request)=>"INTELLIGENCE_WIDE_ANSWER";
}

