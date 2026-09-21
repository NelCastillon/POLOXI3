namespace Legal.Application.Features.Intelligence.Decision.Core;

public static class EvidenceVerificationCodes
{
    public static string Factor(EvidenceVerificationFactor factor) => factor switch
    {
        EvidenceVerificationFactor.Identity => "IDENTITY",
        EvidenceVerificationFactor.Provenance => "PROVENANCE",
        EvidenceVerificationFactor.Citation => "CITATION",
        EvidenceVerificationFactor.Passage => "PASSAGE",
        EvidenceVerificationFactor.PropositionSupport => "PROPOSITION_SUPPORT",
        EvidenceVerificationFactor.StatementRole => "STATEMENT_ROLE",
        EvidenceVerificationFactor.Holding => "HOLDING",
        EvidenceVerificationFactor.Authority => "AUTHORITY",
        _ => factor.ToString().ToUpperInvariant(),
    };

    public static string State(VerificationCheckState state) => state switch
    {
        VerificationCheckState.NotEvaluated => "NOT_EVALUATED",
        VerificationCheckState.Passed => "PASSED",
        VerificationCheckState.Failed => "FAILED",
        VerificationCheckState.NotApplicable => "NOT_APPLICABLE",
        VerificationCheckState.Inconclusive => "INCONCLUSIVE",
        VerificationCheckState.Error => "ERROR",
        _ => state.ToString().ToUpperInvariant(),
    };

    public static string State(PropositionSupportState state) => state switch
    {
        PropositionSupportState.NotEvaluated => "NOT_EVALUATED",
        PropositionSupportState.Supported => "SUPPORTED",
        PropositionSupportState.PartiallySupported => "PARTIALLY_SUPPORTED",
        PropositionSupportState.Unsupported => "UNSUPPORTED",
        PropositionSupportState.Contradicted => "CONTRADICTED",
        PropositionSupportState.Unverifiable => "UNVERIFIABLE",
        PropositionSupportState.Error => "ERROR",
        _ => state.ToString().ToUpperInvariant(),
    };

    public static string Disposition(EvidenceSupportDisposition disposition) => disposition switch
    {
        EvidenceSupportDisposition.Supported => "SUPPORTED",
        EvidenceSupportDisposition.PartiallySupported => "PARTIALLY_SUPPORTED",
        EvidenceSupportDisposition.Unsupported => "UNSUPPORTED",
        EvidenceSupportDisposition.Contradicted => "CONTRADICTED",
        EvidenceSupportDisposition.Unverifiable => "UNVERIFIABLE",
        EvidenceSupportDisposition.Error => "ERROR",
        _ => disposition.ToString().ToUpperInvariant(),
    };
}

public sealed class EvidenceVerificationAggregator : IEvidenceVerificationAggregator
{
    public EvidenceVerificationResult Aggregate(
        EvidenceVerificationRequest request,
        EvidenceSourceType sourceType,
        VerificationProfile profile,
        VerificationCheckResult identity,
        VerificationCheckResult provenance,
        VerificationCheckResult citation,
        VerificationCheckResult passage,
        PropositionSupportResult propositionSupport,
        VerificationCheckResult statementRole,
        VerificationCheckResult holding,
        VerificationCheckResult authority)
    {
        var blockers = new List<string>();
        Require(profile.RequireIdentity, EvidenceVerificationFactor.Identity, identity.State, blockers);
        Require(profile.RequireProvenance, EvidenceVerificationFactor.Provenance, provenance.State, blockers);
        Require(profile.RequireCitation, EvidenceVerificationFactor.Citation, citation.State, blockers);
        Require(profile.RequirePassage, EvidenceVerificationFactor.Passage, passage.State, blockers);
        if (profile.RequirePropositionSupport && propositionSupport.State != PropositionSupportState.Supported)
            blockers.Add($"PROPOSITION_SUPPORT:{EvidenceVerificationCodes.State(propositionSupport.State)}:{propositionSupport.ReasonCode}");
        Require(profile.RequireStatementRole, EvidenceVerificationFactor.StatementRole, statementRole.State, blockers);
        Require(profile.RequireHolding, EvidenceVerificationFactor.Holding, holding.State, blockers);
        Require(profile.RequireAuthority, EvidenceVerificationFactor.Authority, authority.State, blockers);

        var disposition = propositionSupport.State switch
        {
            PropositionSupportState.Supported => EvidenceSupportDisposition.Supported,
            PropositionSupportState.PartiallySupported => EvidenceSupportDisposition.PartiallySupported,
            PropositionSupportState.Contradicted => EvidenceSupportDisposition.Contradicted,
            PropositionSupportState.Unsupported => EvidenceSupportDisposition.Unsupported,
            PropositionSupportState.Error => EvidenceSupportDisposition.Error,
            _ => EvidenceSupportDisposition.Unverifiable,
        };
        var verified = blockers.Count == 0;
        var result = new EvidenceVerificationResult
        {
            DecisionEvidenceId = request.DecisionEvidenceId,
            DecisionBranchId = request.DecisionBranchId,
            SourceType = sourceType,
            Profile = profile,
            Identity = identity,
            Provenance = provenance,
            Citation = citation,
            Passage = passage,
            PropositionSupport = propositionSupport,
            StatementRole = statementRole,
            Holding = holding,
            Authority = authority,
            Disposition = disposition,
            IsVerified = verified,
            BlockingReasons = blockers,
        };
        return result with
        {
            IsDecisionAuthorized = EvidenceVerificationInvariants.GrantsPositiveDecisionAuthority(result),
        };
    }

    private static void Require(
        bool required,
        EvidenceVerificationFactor factor,
        VerificationCheckState state,
        ICollection<string> blockers)
    {
        if (required && state != VerificationCheckState.Passed)
            blockers.Add($"{EvidenceVerificationCodes.Factor(factor)}:{EvidenceVerificationCodes.State(state)}");
    }
}

public sealed class IndependentEvidenceVerificationPipeline(
    IEvidenceSourceClassifier sourceClassifier,
    IVerificationProfileProvider profileProvider,
    IIdentityEvidenceVerifier identityVerifier,
    ICitationEvidenceVerifier citationVerifier,
    IPassageEvidenceVerifier passageVerifier,
    IVerificationCandidatePreScreen candidatePreScreen,
    ISemanticEvidenceVerifier semanticVerifier,
    IPoloxiVerificationDeepener poloxiDeepener,
    IAuthorityEvidenceVerifier authorityVerifier,
    IEvidenceVerificationAggregator aggregator)
    : IIndependentEvidenceVerificationPipeline
{
    public async Task<EvidenceVerificationResult> VerifyAsync(
        EvidenceVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceType = sourceClassifier.Classify(request);
        var profile = profileProvider.GetProfile(sourceType);

        var identity = await SafeCheckAsync(
            () => identityVerifier.VerifyAsync(request, cancellationToken),
            "IDENTITY_VERIFICATION_ERROR", request.SourceRef);
        if (profile.RequireIdentity && identity.State != VerificationCheckState.Passed)
            return AggregateShortCircuit(request, sourceType, profile, identity,
                "IDENTITY_PREREQUISITE_NOT_PASSED");

        var provenance = profile.RequireProvenance
            ? (!string.IsNullOrWhiteSpace(request.SourceRef)
                ? new VerificationCheckResult { State = VerificationCheckState.Passed, ReasonCode = "SOURCE_PROVENANCE_RECORDED", VerifiedValue = request.SourceRef, SourceRef = request.SourceRef, VerificationMethod = "DETERMINISTIC_PROVENANCE_V1" }
                : new VerificationCheckResult { State = VerificationCheckState.Failed, ReasonCode = "SOURCE_PROVENANCE_MISSING", VerificationMethod = "DETERMINISTIC_PROVENANCE_V1" })
            : VerificationCheckResult.NotApplicable("PROVENANCE_NOT_REQUIRED", "Provenance is not required by the source profile.");
        if (profile.RequireProvenance && provenance.State != VerificationCheckState.Passed)
            return AggregateShortCircuit(request, sourceType, profile, identity,
                "PROVENANCE_PREREQUISITE_NOT_PASSED", provenance: provenance);

        var citation = profile.RequireCitation
            ? await SafeCheckAsync(() => citationVerifier.VerifyAsync(request, cancellationToken),
                "CITATION_VERIFICATION_ERROR", request.SourceRef)
            : VerificationCheckResult.NotApplicable("CITATION_NOT_REQUIRED", "Citation is not required by the source profile.");
        if (profile.RequireCitation && citation.State != VerificationCheckState.Passed)
            return AggregateShortCircuit(request, sourceType, profile, identity,
                "CITATION_PREREQUISITE_NOT_PASSED", provenance: provenance, citation: citation);

        var passage = await SafeCheckAsync(
            () => passageVerifier.VerifyAsync(request, cancellationToken),
            "PASSAGE_VERIFICATION_ERROR", request.SourceRef);
        if (profile.RequirePassage && passage.State != VerificationCheckState.Passed)
            return AggregateShortCircuit(request, sourceType, profile, identity,
                "PASSAGE_PREREQUISITE_NOT_PASSED", provenance: provenance, citation: citation, passage: passage);

        var preScreen = candidatePreScreen.Evaluate(request, sourceType);
        if (!preScreen.ShouldVerify)
        {
            var semanticNotEvaluated = SemanticNotEvaluated(request.Proposition, preScreen.ReasonCode,
                "Candidate pre-screen rejected semantic token expenditure; no support conclusion was inferred.");
            var authorityNotEvaluated = profile.RequireAuthority
                ? VerificationCheckResult.NotEvaluated(preScreen.ReasonCode, "Authority was not evaluated after pre-screen rejection.")
                : VerificationCheckResult.NotApplicable("AUTHORITY_NOT_REQUIRED", "Authority applicability is not required by this source profile.");
            return aggregator.Aggregate(request, sourceType, profile, identity, provenance, citation, passage,
                semanticNotEvaluated.PropositionSupport,
                profile.RequireStatementRole ? semanticNotEvaluated.StatementRole : VerificationCheckResult.NotApplicable("STATEMENT_ROLE_NOT_REQUIRED", "Statement role is not applicable to this source profile."),
                profile.RequireHolding ? semanticNotEvaluated.Holding : VerificationCheckResult.NotApplicable("HOLDING_NOT_REQUIRED", "Holding is not applicable to this source profile."),
                authorityNotEvaluated) with
            {
                SourceSnapshot = CreateSnapshot(request, passage),
                Telemetry = new VerificationTelemetry { MechanicalVerificationCount = 4, PreScreenRejectedCount = 1 },
            };
        }

        var semantic = await SafeSemanticAsync(
            () => semanticVerifier.VerifyAsync(request, profile, passage, cancellationToken), request.Proposition);
        if (request.DecisionMaterial && semantic.Ambiguous)
        {
            var contract = new PoloxiVerificationContract
            {
                Proposition = request.Proposition,
                SourceType = sourceType,
                Passages = [passage.SupportingPassage!],
                AllowedOutcomes = [PropositionSupportState.Supported, PropositionSupportState.PartiallySupported,
                    PropositionSupportState.Unsupported, PropositionSupportState.Contradicted, PropositionSupportState.Unverifiable],
                SemanticFactors = [EvidenceVerificationFactor.PropositionSupport, EvidenceVerificationFactor.StatementRole, EvidenceVerificationFactor.Holding],
                UnresolvedDiscriminators = semantic.PropositionSupport.UnsupportedComponents
                    .Concat(semantic.PropositionSupport.ContradictedComponents).Distinct().ToArray(),
                MaxInputTokens = 2500,
                MaxOutputTokens = 700,
                AllowExternalRetrieval = false,
            };
            semantic = await SafeSemanticAsync(
                () => poloxiDeepener.DeepenAsync(contract, semantic, cancellationToken), request.Proposition);
        }
        var proposition = semantic.PropositionSupport;
        var statementRole = profile.RequireStatementRole
            ? semantic.StatementRole
            : VerificationCheckResult.NotApplicable("STATEMENT_ROLE_NOT_REQUIRED", "Statement role is not applicable to this source profile.");
        var holding = profile.RequireHolding
            ? semantic.Holding
            : VerificationCheckResult.NotApplicable("HOLDING_NOT_REQUIRED", "Holding is not applicable to this source profile.");

        var authority = profile.RequireAuthority
            ? await SafeCheckAsync(() => authorityVerifier.VerifyAsync(request, cancellationToken),
                "AUTHORITY_VERIFICATION_ERROR", request.SourceRef)
            : VerificationCheckResult.NotApplicable("AUTHORITY_NOT_REQUIRED", "Authority applicability is not required by this source profile.");

        return aggregator.Aggregate(request, sourceType, profile, identity, provenance, citation, passage,
            proposition, statementRole, holding, authority) with
        {
            SourceSnapshot = CreateSnapshot(request, passage),
            Telemetry = new VerificationTelemetry
            {
                RetrievedCount = 1,
                MechanicalVerificationCount = 4,
                SemanticVerificationCount = semantic.CacheHit ? 0 : 1,
                PoloxiDeepeningCount = semantic.Deepened ? 1 : 0,
                CacheHitCount = semantic.CacheHit ? 1 : 0,
                InputTokens = semantic.InputTokenCount,
                OutputTokens = semantic.OutputTokenCount,
                TotalLatencyMilliseconds = (long)semantic.Duration.TotalMilliseconds,
            },
        };
    }

    private static EvidenceSourceSnapshot CreateSnapshot(
        EvidenceVerificationRequest request,
        VerificationCheckResult passage)
    {
        static string Hash(string? value) => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty)));

        return new EvidenceSourceSnapshot
        {
            SourceSnapshotId = Guid.NewGuid(),
            ContentHash = Hash(request.SourceText),
            PassageHash = Hash(passage.SupportingPassage),
            SourceProvider = request.SourceProvider,
            SourceVersion = request.SourceVersion,
            SourceRef = request.SourceRef,
            PassageRef = request.PassageRef,
            ExtractionVersion = request.ExtractionVersion,
        };
    }

    private EvidenceVerificationResult AggregateShortCircuit(
        EvidenceVerificationRequest request,
        EvidenceSourceType sourceType,
        VerificationProfile profile,
        VerificationCheckResult identity,
        string reasonCode,
        VerificationCheckResult? provenance = null,
        VerificationCheckResult? citation = null,
        VerificationCheckResult? passage = null)
    {
        provenance ??= profile.RequireProvenance
            ? VerificationCheckResult.NotEvaluated(reasonCode, "Provenance was not evaluated because a prerequisite did not pass.")
            : VerificationCheckResult.NotApplicable("PROVENANCE_NOT_REQUIRED", "Provenance is not required by the source profile.");
        citation ??= profile.RequireCitation
            ? VerificationCheckResult.NotEvaluated(reasonCode, "Citation was not evaluated because a prerequisite did not pass.")
            : VerificationCheckResult.NotApplicable("CITATION_NOT_REQUIRED", "Citation is not required by the source profile.");
        passage ??= VerificationCheckResult.NotEvaluated(reasonCode, "Passage was not evaluated because a prerequisite did not pass.");
        var proposition = PropositionSupportResult.NotEvaluated(request.Proposition, reasonCode,
            "Proposition support was not evaluated because a prerequisite did not pass.");
        var holding = profile.RequireHolding
            ? VerificationCheckResult.NotEvaluated(reasonCode, "Holding was not evaluated because a prerequisite did not pass.")
            : VerificationCheckResult.NotApplicable("HOLDING_NOT_REQUIRED", "Holding is not applicable to this source profile.");
        var authority = profile.RequireAuthority
            ? VerificationCheckResult.NotEvaluated(reasonCode, "Authority was not evaluated because a prerequisite did not pass.")
            : VerificationCheckResult.NotApplicable("AUTHORITY_NOT_REQUIRED", "Authority is not required by this source profile.");
        var statementRole = profile.RequireStatementRole
            ? VerificationCheckResult.NotEvaluated(reasonCode, "Statement role was not evaluated because a prerequisite did not pass.")
            : VerificationCheckResult.NotApplicable("STATEMENT_ROLE_NOT_REQUIRED", "Statement role is not required by the source profile.");
        return aggregator.Aggregate(request, sourceType, profile, identity, provenance, citation, passage,
            proposition, statementRole, holding, authority);
    }

    private static async Task<VerificationCheckResult> SafeCheckAsync(
        Func<Task<VerificationCheckResult>> action,
        string reasonCode,
        string? sourceRef)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Error,
                ReasonCode = reasonCode,
                Reason = ex.GetType().Name,
                SourceRef = sourceRef,
                VerificationMethod = "VERIFIER_ERROR_BOUNDARY",
            };
        }
    }

    private static SemanticVerificationResult SemanticNotEvaluated(string proposition, string reasonCode, string reason) => new()
    {
        PropositionSupport = PropositionSupportResult.NotEvaluated(proposition, reasonCode, reason),
        StatementRole = VerificationCheckResult.NotEvaluated(reasonCode, reason),
        Holding = VerificationCheckResult.NotEvaluated(reasonCode, reason),
    };

    private static async Task<SemanticVerificationResult> SafeSemanticAsync(
        Func<Task<SemanticVerificationResult>> action,
        string proposition)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SemanticVerificationResult
            {
                PropositionSupport = new PropositionSupportResult { State = PropositionSupportState.Error, Proposition = proposition, ReasonCode = "SEMANTIC_VERIFIER_FAILED", Reason = ex.GetType().Name, VerificationMethod = "SEMANTIC_ERROR_BOUNDARY" },
                StatementRole = new VerificationCheckResult { State = VerificationCheckState.Error, ReasonCode = "SEMANTIC_VERIFIER_FAILED", Reason = ex.GetType().Name, VerificationMethod = "SEMANTIC_ERROR_BOUNDARY" },
                Holding = new VerificationCheckResult { State = VerificationCheckState.Error, ReasonCode = "SEMANTIC_VERIFIER_FAILED", Reason = ex.GetType().Name, VerificationMethod = "SEMANTIC_ERROR_BOUNDARY" },
            };
        }
    }
}
