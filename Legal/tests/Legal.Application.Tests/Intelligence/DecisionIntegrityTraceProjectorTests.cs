using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Decision Integrity Trace projector tests.
//
// Pins the deterministic projection of authoritative decision state into the nine-stage trace and the
// three independent top-level concepts. Key invariants:
//   • A fully-functioning run with insufficient support is HEALTHY + NOT READY (never a fault).
//   • A rolled-back research round is HEALTHY (proves atomicity), reported as RolledBack, not FAILED.
//   • An unclean final audit forces Integrity=FAILED and OutputIntegrity=ATTENTION.
//   • A winner id absent from candidates invalidates state consistency.
//   • Stages with no backing data report NotRun.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionIntegrityTraceProjectorTests
{
    [Fact]
    public void Project_HealthyRunWithoutSupport_IsHealthyButNotReady()
    {
        var id = Guid.NewGuid();
        var response = BaseResponse() with
        {
            StatusCode = DecisionStatusCodes.ResearchExhausted,
            Candidates = [Candidate(id)],
            WinnerCandidateId = id,
            // Every required control was observed or legitimately skipped: proposal ran, research was
            // skipped with a reason (frontier below threshold), and the output was audited clean. The run is
            // HEALTHY even though it is NOT READY (insufficient verified support to decide).
            ProposalIntegrity = new DecisionProposalIntegritySummaryDto(
                Mode: "ACTIVE", Disposition: "ACCEPT", Attempt: 1,
                RecoveryAttempted: false, Recovered: false, StructurallyValid: true, Defects: []),
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: true, UseGraph: true, V2Available: true,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.FrontierBelowThreshold,
                FrontierCount: 2, HighestFrontierInformationValue: 0.10m,
                MinFrontierInformationValue: 0.15m, RetrievalBudget: true),
            FinalAnswer = "There is not yet enough verified support to decide.",
            GovernanceVerdict = Verdict(outputClean: true, violations: []),
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(
                    Guid.NewGuid(), "claim", "SUPPORTED", "FULL", "ALLOW", false, "ok", false),
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityState.Healthy, trace.Integrity);
        Assert.Equal(TraceReadinessState.NotReady, trace.Readiness);
    }

    [Fact]
    public void Project_DecisionReady_IsReady()
    {
        var response = BaseResponse() with { StatusCode = DecisionStatusCodes.DecisionReady };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(TraceReadinessState.Ready, trace.Readiness);
    }

    [Fact]
    public void Project_RolledBackResearchRound_IsHealthyAndReportedAsRolledBack()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 1, RoundsCommitted: 0, RoundsRolledBack: 1,
                TotalRetrievals: 2, StopReason: DecisionResearchLoopStopReasons.RoundFailed,
                Rounds: []),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.RolledBack, trace.Research.Status);
        // A rolled-back round is a legitimate, atomic outcome — attention, never FAILED.
        Assert.Equal(IntegrityState.AttentionRequired, trace.Integrity);
    }

    [Fact]
    public void Project_ResearchEligibility_SettingDisabled_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: false, UseGraph: true, V2Available: true,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.SettingDisabled),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · SETTING DISABLED", trace.Research.CompactSummary);
        Assert.Contains(trace.Research.Detail, d => d.Label == "Not-run reason" && d.Value == DecisionResearchNotRunReasons.SettingDisabled);
    }

    [Fact]
    public void Project_ResearchEligibility_GraphDisabled_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: false, UseGraph: false, V2Available: false,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.GraphDisabled),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · GRAPH DISABLED", trace.Research.CompactSummary);
    }

    [Fact]
    public void Project_ResearchEligibility_V2Unavailable_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: false, UseGraph: true, V2Available: false,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.V2Unavailable),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · V2 UNAVAILABLE", trace.Research.CompactSummary);
    }

    [Fact]
    public void Project_ResearchEligibility_Absent_ReportsNotObserved()
    {
        var response = BaseResponse();

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Research.Status);
        Assert.Equal("NOT OBSERVED · NO EXECUTION EVIDENCE", trace.Research.CompactSummary);
    }

    [Fact]
    public void Project_ResearchEligibility_NoFrontier_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: true, UseGraph: true, V2Available: true,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.NoFrontier,
                FrontierCount: 0, HighestFrontierInformationValue: 0m,
                MinFrontierInformationValue: 0.15m, RetrievalBudget: true),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · NO FRONTIER", trace.Research.CompactSummary);
        Assert.Contains(trace.Research.Detail, d => d.Label == "Frontier count" && d.Value == "0");
    }

    [Fact]
    public void Project_ResearchEligibility_FrontierBelowThreshold_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: true, UseGraph: true, V2Available: true,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.FrontierBelowThreshold,
                FrontierCount: 3, HighestFrontierInformationValue: 0.10m,
                MinFrontierInformationValue: 0.15m, RetrievalBudget: true),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · FRONTIER BELOW THRESHOLD", trace.Research.CompactSummary);
        Assert.Contains(trace.Research.Detail, d => d.Label == "Highest frontier IV" && d.Value == "0.1");
    }

    [Fact]
    public void Project_ResearchEligibility_SettingsUnavailable_ReportsExplicitReason()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: false, UseGraph: true, V2Available: true,
                Eligible: false, NotRunReason: DecisionResearchNotRunReasons.SettingsUnavailable,
                FrontierCount: 6, HighestFrontierInformationValue: 0.70m),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Equal("SKIPPED · SETTINGS UNAVAILABLE", trace.Research.CompactSummary);
    }

    [Fact]
    public void Project_UncleanFinalAudit_ForcesFailedAndOutputAttention()
    {
        var response = BaseResponse() with
        {
            GovernanceVerdict = Verdict(outputClean: false, violations: ["unauthorized assertion"]),
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(
                    Guid.NewGuid(), "claim", "UNSUPPORTED", "NONE", "SUPPRESS", false, "no support", true),
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Failed, trace.FinalAudit.Status);
        Assert.Equal(IntegrityState.Failed, trace.Integrity);
        Assert.Equal(OutputIntegrityState.AttentionRequired, trace.OutputIntegrity);
    }

    [Fact]
    public void Project_CleanOutputAudit_IsCleanAndHealthy()
    {
        var response = BaseResponse() with
        {
            GovernanceVerdict = Verdict(outputClean: true, violations: []),
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(
                    Guid.NewGuid(), "claim", "SUPPORTED", "FULL", "ALLOW", false, "ok", false),
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(OutputIntegrityState.Clean, trace.OutputIntegrity);
        // Output is clean, but Proposal and Research were not observed in this fixture, so the run cannot be
        // vouched for as fully HEALTHY — the correct top-level state is INCOMPLETE (a coverage gap, not a fault).
        Assert.Equal(IntegrityState.Incomplete, trace.Integrity);
    }

    [Fact]
    public void Project_SubstantiveAnswerButNoClaimsExtracted_IsNotEvaluatedNotClean()
    {
        // A composed answer exists but nothing was claim-extracted/authorized. OutputClean defaults
        // true on the verdict, but the auditor never saw a claim — so this must NOT be reported CLEAN.
        var response = BaseResponse() with
        {
            FinalAnswer = "The plaintiff presented credible evidence of misclassification.",
            GovernanceVerdict = new DecisionGovernanceVerdictDto(
                OverrideMode: "ADVISORY",
                V2ReadinessSatisfied: true,
                EaReady: true,
                OutputClean: true,
                EffectiveReadinessSatisfied: true,
                OverrideApplied: false,
                ProjectedClaimCount: 0,
                AuthorizedClaimCount: 0,
                Blockers: [],
                OutputViolations: [],
                InvolvedClaims: [],
                Narrative: []),
            OutputAuthorizations = [],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(OutputIntegrityState.NotRun, trace.OutputIntegrity);
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.FinalAudit.Status);
        Assert.Contains("NOT EVALUATED", trace.FinalAudit.CompactSummary, StringComparison.OrdinalIgnoreCase);
        // System machinery still ran soundly, but the un-audited output is a coverage gap: the run is
        // INCOMPLETE (not evaluated), which must not read as a fault and must not read as fully HEALTHY.
        Assert.Equal(IntegrityState.Incomplete, trace.Integrity);
    }

    [Fact]
    public void Project_WinnerNotAmongCandidates_IsInvalidConsistencyAndFailed()
    {
        var candidate = Candidate(Guid.NewGuid());
        var response = BaseResponse() with
        {
            Candidates = [candidate],
            WinnerCandidateId = Guid.NewGuid(), // not the candidate above
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(StateConsistency.Invalid, trace.Consistency);
        Assert.Equal(IntegrityState.Failed, trace.Integrity);
    }

    [Fact]
    public void Project_ValidWinner_IsConsistent()
    {
        var id = Guid.NewGuid();
        var response = BaseResponse() with
        {
            Candidates = [Candidate(id)],
            WinnerCandidateId = id,
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(StateConsistency.Valid, trace.Consistency);
    }

    [Fact]
    public void Project_NoBackingData_StagesAreNotRunOrSkipped()
    {
        var trace = DecisionIntegrityTraceProjector.Project(BaseResponse());

        // Live-input stages with no observation report NotRun...
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Proposal.Status);
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Research.Status);
        // ...but legitimately-unnecessary stages report Skipped with a reason (nothing to do).
        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Verification.Status);
        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Recompetition.Status);
        Assert.Equal(IntegrityTraceStatus.Skipped, trace.OutputControl.Status);
        // With unobserved required stages present, the top-level state is INCOMPLETE.
        Assert.Equal(IntegrityState.Incomplete, trace.Integrity);
    }

    [Fact]
    public void Project_VerifiedEvidence_VerificationPassesWithProvenanceChild()
    {
        var response = BaseResponse() with
        {
            Evidence =
            [
                new DecisionEvidenceDto(
                    Guid.NewGuid(), null, "src-1", "Source One", "snippet", 1.0m,
                    DecisionVerificationStates.Verified)
                {
                    SupportedObjective = "motor carrier exemption",
                    SupportingPassage = "the passage",
                },
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Passed, trace.Verification.Status);
        Assert.Single(trace.Verification.Children);
    }

    [Fact]
    public void Project_VerificationUsesStoredLifecycleDistribution()
    {
        var response = BaseResponse() with
        {
            Evidence =
            [
                new DecisionEvidenceDto(Guid.NewGuid(), null, "a", "A", "snippet", 0m, DecisionVerificationStates.Unverified)
                    { LifecycleState = DecisionEvidenceLifecycleStates.Unsupported },
                new DecisionEvidenceDto(Guid.NewGuid(), null, "b", "B", "snippet", 0m, DecisionVerificationStates.Unverified)
                    { LifecycleState = DecisionEvidenceLifecycleStates.PartiallySupported },
                new DecisionEvidenceDto(Guid.NewGuid(), null, "c", "C", "snippet", 0m, DecisionVerificationStates.Unverified)
                    { LifecycleState = DecisionEvidenceLifecycleStates.Unverifiable },
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Contains(trace.Verification.Detail, d => d.Label == "Evaluated" && d.Value == "3");
        Assert.Contains(trace.Verification.Detail, d => d.Label == DecisionEvidenceLifecycleStates.Unsupported && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == DecisionEvidenceLifecycleStates.PartiallySupported && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == DecisionEvidenceLifecycleStates.Unverifiable && d.Value == "1");
        Assert.DoesNotContain(trace.Verification.Detail, d => d.Label == "Other/partial");
    }

    [Fact]
    public void Project_VerificationUsesPersistedFactorResultsAndBlockers()
    {
        var evidenceId = Guid.NewGuid();
        var response = BaseResponse() with
        {
            Evidence =
            [
                new DecisionEvidenceDto(
                    evidenceId, null, "https://example.test/source", "Source", "snippet", 0m,
                    DecisionVerificationStates.Unverified)
                {
                    LifecycleState = DecisionEvidenceLifecycleStates.Unsupported,
                    SupportedObjective = "claimed proposition",
                },
            ],
            EvidenceVerifications =
            [
                new DecisionEvidenceVerificationDto(
                    Guid.NewGuid(), evidenceId, null, "CASE_LAW", "CASE_LAW_FULL",
                    "UNSUPPORTED", IsVerified: false, IsDecisionAuthorized: false,
                    BlockingReasons: ["PROPOSITION_SUPPORT:UNSUPPORTED"], DateTime.UtcNow,
                    Factors:
                    [
                        new("IDENTITY", "PASSED", "IDENTITY_CONFIRMED", "Source identity confirmed.",
                            "Source", "https://example.test/source", null, "PLAYWRIGHT"),
                        new("PROPOSITION_SUPPORT", "UNSUPPORTED", "PROPOSITION_NOT_SUPPORTED",
                            "The passage does not support the proposition.", null,
                            "https://example.test/source", "unrelated passage", "DETERMINISTIC"),
                    ])
                {
                    RetrievedCount = 1,
                    PreScreenRejectedCount = 1,
                    MechanicalVerificationCount = 4,
                    SemanticVerificationCount = 0,
                },
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);
        var child = Assert.Single(trace.Verification.Children);

        Assert.Contains(trace.Verification.Detail, d => d.Label == "Verification runs" && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "Retrieved candidates" && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "Pre-screen rejected" && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "Decision-authorized runs" && d.Value == "0");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "Disposition · UNSUPPORTED" && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "IDENTITY · PASSED" && d.Value == "1");
        Assert.Contains(trace.Verification.Detail, d => d.Label == "PROPOSITION_SUPPORT · UNSUPPORTED" && d.Value == "1");
        Assert.Contains(child.Detail, d => d.Label == "Decision authorized" && d.Value == "NO");
        Assert.Contains(child.Detail, d => d.Label == "Factor · PROPOSITION_SUPPORT"
            && d.Value.Contains("PROPOSITION_NOT_SUPPORTED"));
        Assert.Contains(child.Detail, d => d.Label == "Blocker"
            && d.Value == "PROPOSITION_SUPPORT:UNSUPPORTED");
    }

    [Fact]
    public void OutputControl_SubstantiveAnswerWithEmptyExtraction_ReportsExplicitFailure()
    {
        var response = BaseResponse() with
        {
            FinalAnswer = "This substantive legal answer contains material assertions requiring authorization.",
            OutputClaimExtraction = new DecisionOutputClaimExtractionDto(
                Attempted: true, AnswerLength: 82, SubstantiveAnswer: true, ClaimsReturned: 0,
                StatusCode: "EMPTY", FailureReason: "OUTPUT_CLAIM_EXTRACTION_EMPTY"),
            OutputAuthorizations = [],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.NotRun, trace.OutputControl.Status);
        Assert.Equal("NOT EVALUATED · NO CLAIMS EXTRACTED", trace.OutputControl.CompactSummary);
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Extraction attempted" && d.Value == "YES");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Extractor result" && d.Value == "EMPTY");
        Assert.Equal(IntegrityState.Incomplete, trace.Integrity);
    }

    [Fact]
    public void Project_CarriesDecisionStateVersion()
    {
        var response = BaseResponse() with { DecisionStateVersion = 3 };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(3, trace.DecisionStateVersion);
    }

    [Fact]
    public void Project_ReadbackWithoutLiveInputs_StillProjectsPersistedStages()
    {
        // Mirrors the GetSessionResultAsync read-back: the live-only inputs (proposal gate summary,
        // in-request research audit, output authorizations) are absent, but persisted state must still
        // produce a meaningful trace so a reloaded session shows the same panel as the live run.
        var id = Guid.NewGuid();
        var response = BaseResponse() with
        {
            StatusCode = DecisionStatusCodes.DecisionReady,
            Candidates = [Candidate(id)],
            WinnerCandidateId = id,
            Evidence =
            [
                new DecisionEvidenceDto(
                    Guid.NewGuid(), null, "src-1", "Source One", "snippet", 1.0m,
                    DecisionVerificationStates.Verified)
                {
                    SupportedObjective = "objective",
                    SupportingPassage = "passage",
                },
            ],
            LastRecompetition = new DecisionRecompetitionDto(
                Guid.NewGuid(), null, id, WinnerChanged: false,
                PreviousEntropy: 0.4m, CurrentEntropy: 0.3m, PreviousMargin: 0.1m, CurrentMargin: 0.2m,
                ReopenedBranchCount: 0, ReasonCode: "VERIFICATION_CHANGE", CreatedDateUtc: DateTime.UtcNow),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        // Live-only stages report NotRun (their inputs weren't persisted)...
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Proposal.Status);
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Research.Status);
        // ...but persisted stages still project.
        Assert.Equal(IntegrityTraceStatus.Passed, trace.Verification.Status);
        Assert.Equal(IntegrityTraceStatus.Passed, trace.Promotion.Status);
        Assert.Equal(IntegrityTraceStatus.Passed, trace.Recompetition.Status);
        Assert.Equal(StateConsistency.Valid, trace.Consistency);
    }

    [Fact]
    public void Research_NullSummaryAndNoEligibility_ReportsNotObserved()
    {
        var trace = DecisionIntegrityTraceProjector.Project(BaseResponse());

        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Research.Status);
        Assert.Contains("NOT OBSERVED", trace.Research.CompactSummary!);
    }

    [Fact]
    public void Research_DisabledLoop_ReportsLoopDisabled()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: false, RoundsExecuted: 0, RoundsCommitted: 0, RoundsRolledBack: 0,
                TotalRetrievals: 0, StopReason: "DISABLED", Rounds: []),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Contains("LOOP DISABLED", trace.Research.CompactSummary!);
    }

    [Fact]
    public void Research_EnabledButNoRounds_ReportsInvokedZeroRounds()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 0, RoundsCommitted: 0, RoundsRolledBack: 0,
                TotalRetrievals: 0, StopReason: "NONE", Rounds: []),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Research.Status);
        Assert.Contains("INVOKED · 0 ROUNDS", trace.Research.CompactSummary!);
    }

    [Fact]
    public void Propagation_ResearchRoundWithoutAuthoritativeChange_IsSkipped()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 1, RoundsCommitted: 1, RoundsRolledBack: 0,
                TotalRetrievals: 1, StopReason: DecisionResearchLoopStopReasons.NoStateChange,
                Rounds: []),
            LastRecompetition = null,
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Skipped, trace.Propagation.Status);
        Assert.Equal("SKIPPED · NO AUTHORITATIVE EVIDENCE CHANGE", trace.Propagation.CompactSummary);
    }

    [Fact]
    public void OutputControl_UnauthorizedAssertionsRemain_IsFailedWithHonestDiagnostics()
    {
        // The real-world bug: three QUALIFY claims required restatement but none could be applied to the
        // prose. The Output Control stage must report applied<required, remaining>0, and post-transform
        // audit FAILED — never silently PASSED.
        var response = BaseResponse() with
        {
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c1", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c2", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c3", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
            ],
            OutputTransformSummary = new DecisionOutputTransformSummaryDto(
                RequiredCount: 3, AppliedCount: 0, PostTransformClean: false),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Failed, trace.OutputControl.Status);
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Transformations required" && d.Value == "3");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Transformations applied" && d.Value == "0");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Original assertions remain" && d.Value == "3");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Post-transform audit" && d.Value == "FAILED");
    }

    [Fact]
    public void OutputControl_AllTransformationsApplied_IsPassedAndClean()
    {
        var response = BaseResponse() with
        {
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c1", "UNVERIFIED", "NONE", "QUALIFY", false, "r", true),
            ],
            OutputTransformSummary = new DecisionOutputTransformSummaryDto(
                RequiredCount: 1, AppliedCount: 1, PostTransformClean: true),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Passed, trace.OutputControl.Status);
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Post-transform audit" && d.Value == "CLEAN");
    }

    [Fact]
    public void OutputControl_SuppressedClaimsWithCleanFinalAudit_AreSuccessfulControls()
    {
        var response = BaseResponse() with
        {
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c1", "UNVERIFIED", "NONE", "SUPPRESS", false, "r", true),
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c2", "UNVERIFIED", "NONE", "SUPPRESS", false, "r", true),
            ],
            GovernanceVerdict = Verdict(outputClean: true, violations: []),
            OutputTransformSummary = new DecisionOutputTransformSummaryDto(
                RequiredCount: 2, AppliedCount: 2, PostTransformClean: true),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Passed, trace.OutputControl.Status);
        Assert.Equal("ENFORCED · 0 unauthorized · 0A · 0Q · 2S · 0C", trace.OutputControl.CompactSummary);
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Transformations applied" && d.Value == "2");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Original assertions remain" && d.Value == "0");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Post-transform audit" && d.Value == "CLEAN");
    }

    [Fact]
    public void OutputControl_QualifiedClaimsWithCleanFinalAudit_AreSuccessfulControls()
    {
        var response = BaseResponse() with
        {
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c1", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c2", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
                new DecisionOutputAuthorizationDto(Guid.NewGuid(), "c3", "UNVERIFIED", "NONE", "QUALIFY", false, "r", false),
            ],
            GovernanceVerdict = Verdict(outputClean: true, violations: []),
            OutputTransformSummary = null,
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Passed, trace.OutputControl.Status);
        Assert.Equal("ENFORCED · 0 unauthorized · 0A · 3Q · 0S · 0C", trace.OutputControl.CompactSummary);
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Transformations applied" && d.Value == "3");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Original assertions remain" && d.Value == "0");
        Assert.Contains(trace.OutputControl.Detail, d => d.Label == "Post-transform audit" && d.Value == "CLEAN");
    }

    [Fact]
    public void Research_RoundProjectsVerificationAndAttachmentDispositionCounts()
    {
        var round = new DecisionResearchRoundDto(
            RoundNumber: 1,
            TargetBranchId: Guid.NewGuid(),
            TargetBranchLabel: "Employee Classification Analysis",
            TargetInformationValue: 0.70m,
            VerifiedEdgeId: null,
            EdgeVerificationStatus: DecisionVerificationStates.Unverified,
            EvidenceLifecycleState: DecisionEvidenceLifecycleStates.Unsupported,
            SourcesRetrieved: 5,
            WinnerChanged: false,
            EntropyBefore: 0.8m,
            EntropyAfter: 0.8m,
            Narrative: [])
        {
            PropositionToResolve = "Employee does not meet exempt classification criteria",
            SourcesEvaluated = 5,
            VerificationDispositionCounts = new Dictionary<string, int>
            {
                [DecisionEvidenceLifecycleStates.PartiallySupported] = 1,
                [DecisionEvidenceLifecycleStates.Unsupported] = 3,
                [DecisionEvidenceLifecycleStates.Unverifiable] = 1,
            },
            AttachmentStateCounts = new Dictionary<string, int>
            {
                [DecisionEvidenceAttachmentStates.PartiallySupportedBy] = 1,
                [DecisionEvidenceAttachmentStates.Unsupported] = 4,
            },
            AuthoritativeChanges = 0,
        };
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 1, RoundsCommitted: 1, RoundsRolledBack: 0,
                TotalRetrievals: 1, StopReason: DecisionResearchLoopStopReasons.NoStateChange,
                Rounds: [round]),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);
        var child = Assert.Single(trace.Research.Children);

        Assert.Contains(child.Detail, d => d.Label == "Proposition" && d.Value == round.PropositionToResolve);
        Assert.Contains(child.Detail, d => d.Label == "Retrieved" && d.Value == "5");
        Assert.Contains(child.Detail, d => d.Label == "Evaluated" && d.Value == "5");
        Assert.Contains(child.Detail, d => d.Label == DecisionEvidenceLifecycleStates.PartiallySupported && d.Value == "1");
        Assert.Contains(child.Detail, d => d.Label == DecisionEvidenceLifecycleStates.Unsupported && d.Value == "3");
        Assert.Contains(child.Detail, d => d.Label == DecisionEvidenceLifecycleStates.Unverifiable && d.Value == "1");
        Assert.Contains(child.Detail, d => d.Label == DecisionEvidenceAttachmentStates.PartiallySupportedBy && d.Value == "1");
        Assert.Contains(child.Detail, d => d.Label == DecisionEvidenceAttachmentStates.Unsupported && d.Value == "4");
        Assert.Contains(child.Detail, d => d.Label == "Authoritative changes" && d.Value == "0");
        Assert.Contains(child.Detail, d => d.Label == "State change" && d.Value == "NO");
    }

    [Fact]
    public void Research_RoundDistinguishesAttemptedFrontierFromNextRecommendedFrontier()
    {
        var attemptedBranchId = Guid.NewGuid();
        var nextBranchId = Guid.NewGuid();
        var attemptedNeedId = Guid.NewGuid();
        var selectedNeedId = Guid.NewGuid();
        var propositionId = Guid.NewGuid();
        var searchPlanId = Guid.NewGuid();
        var transformation = new DecisionResearchTransformationDto(
            attemptedBranchId, "C5.B1", "Binding agreement and authority to settle", [],
            "C5.B1.legal-rule", "LEGAL_AUTHORITY_PREFERRED_THEN_CANDIDATE_DISCRIMINATION",
            "settlement authority enforceability")
        {
            AttemptedResearchNeedId = attemptedNeedId,
            SelectedResearchNeedId = selectedNeedId,
            PropositionId = propositionId,
            SearchPlanId = searchPlanId,
        };
        var round = new DecisionResearchRoundDto(
            1, attemptedBranchId, "Binding agreement and authority to settle", 0.82m,
            null, DecisionVerificationStates.Unverified, DecisionEvidenceLifecycleStates.Unsupported,
            0, false, 0.75m, 0.75m, [])
        {
            Transformation = transformation,
            NextRecommendedFrontierBranchId = nextBranchId,
            NextRecommendedFrontierBranchCode = "C3.B1",
            NextRecommendedFrontierLabel = "Emily's causal fault exceeds defendants' combined fault",
        };
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                true, 1, 1, 0, 1, DecisionResearchLoopStopReasons.NoStateChange, [round]),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);
        var roundChild = Assert.Single(trace.Research.Children);

        Assert.Contains(roundChild.Detail, detail =>
            detail.Label == "Attempted frontier" &&
            detail.Value.Contains("Binding agreement and authority to settle") &&
            detail.Value.Contains(attemptedBranchId.ToString()));
        Assert.Contains(roundChild.Detail, detail =>
            detail.Label == "Next recommended frontier" &&
            detail.Value.Contains("Emily's causal fault exceeds defendants' combined fault") &&
            detail.Value.Contains(nextBranchId.ToString()));
        Assert.Contains(roundChild.Detail, detail => detail.Label == "Attempted Research Need ID" && detail.Value == attemptedNeedId.ToString());
        Assert.Contains(roundChild.Detail, detail => detail.Label == "Selected Research Need ID" && detail.Value == selectedNeedId.ToString());
        Assert.Contains(roundChild.Detail, detail => detail.Label == "Dependency Proposition ID" && detail.Value == propositionId.ToString());
        Assert.Contains(roundChild.Detail, detail => detail.Label == "Search Plan ID" && detail.Value == searchPlanId.ToString());
    }

    [Fact]
    public void Research_EligibleButNoSummary_ReportsNotObservedExecutionResultMissing()
    {
        var response = BaseResponse() with
        {
            ResearchEligibility = new DecisionResearchEligibilityDto(
                EnabledSetting: true, UseGraph: true, V2Available: true,
                Eligible: true, NotRunReason: DecisionResearchNotRunReasons.Eligible,
                FrontierCount: 4, HighestFrontierInformationValue: 0.70m,
                MinFrontierInformationValue: 0.15m, RetrievalBudget: true),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        // Eligible but no execution result: NEVER infer INVOKED from eligibility alone — this is a
        // coverage gap (NotRun), never a definitive "did not run".
        Assert.Equal(IntegrityTraceStatus.NotRun, trace.Research.Status);
        Assert.Contains("NOT OBSERVED · EXECUTION RESULT MISSING", trace.Research.CompactSummary!);
        Assert.Equal(IntegrityState.Incomplete, trace.Integrity);
    }

    [Fact]
    public void Research_LoopFaulted_ReportsFailedWithReason()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 1, RoundsCommitted: 0, RoundsRolledBack: 1,
                TotalRetrievals: 1, StopReason: DecisionResearchLoopStopReasons.RoundFailed,
                Rounds: [], Failure: new DecisionResearchFailureDto(
                    RoundNumber: 1, Stage: "VERIFICATION", ExceptionType: "InvalidOperationException",
                    Reason: "Sequence contains no matching element", AuthoritativeStateChanged: false)),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Failed, trace.Research.Status);
        Assert.Equal("FAILED · ROUND 1", trace.Research.CompactSummary);
        Assert.Contains(trace.Research.Detail, d => d.Label == "Failure stage" && d.Value == "VERIFICATION");
        Assert.Contains(trace.Research.Detail, d => d.Label == "Failure type" && d.Value == "InvalidOperationException");
        Assert.Contains(trace.Research.Detail, d => d.Label == "Reason" && d.Value.Contains("no matching element"));
        Assert.Contains(trace.Research.Detail, d => d.Label == "Authoritative state changed" && d.Value == "NO");
        Assert.Equal(IntegrityState.AttentionRequired, trace.Integrity);
    }

    [Fact]
    public void Research_PreRoundFault_ReportsPreRoundWithSafeDiagnostics()
    {
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 0, RoundsCommitted: 0, RoundsRolledBack: 0,
                TotalRetrievals: 0, StopReason: DecisionResearchLoopStopReasons.RoundFailed,
                Rounds: [], Failure: new DecisionResearchFailureDto(
                    RoundNumber: 0, Stage: "EDGE_SELECTION", ExceptionType: "InvalidOperationException",
                    Reason: "Sequence contains no matching element", AuthoritativeStateChanged: false)),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityTraceStatus.Failed, trace.Research.Status);
        Assert.Equal("FAILED · PRE-ROUND", trace.Research.CompactSummary);
        Assert.Contains(trace.Research.Detail, d => d.Label == "Round" && d.Value == "PRE-ROUND");
        Assert.Contains(trace.Research.Detail, d => d.Label == "Failure stage" && d.Value == "EDGE_SELECTION");
        Assert.Contains(trace.Research.Detail, d => d.Label == "Failure type" && d.Value == "InvalidOperationException");
        Assert.Contains(trace.Research.Detail, d => d.Label == "Reason" && d.Value.Contains("no matching element"));
        Assert.Contains(trace.Research.Detail, d => d.Label == "Authoritative state changed" && d.Value == "NO");
        Assert.Equal(IntegrityState.AttentionRequired, trace.Integrity);
    }

    [Fact]
    public void Research_PreRoundRepairFailure_ProjectsTransformationAttempts()
    {
        var transformation = new DecisionResearchTransformationDto(
            FrontierBranchId: Guid.NewGuid(), FrontierBranchCode: "C3.B1.frontier",
            FrontierLabel: "Allocation at or below the statutory threshold",
            Attempts:
            [
                new DecisionResearchTransformationAttemptDto(
                    Attempt: 1, ModelCode: "Astra", Status: "GATE_REJECTED", LeavesProduced: 1,
                    Disposition: "REPAIR", Defects: ["C3.B1.frontier:PROPOSITION_MUST_BE_DECLARATIVE"],
                    Leaves:
                    [
                        new DecisionResearchTransformationLeafDto(
                            "C3.B1.frontier", DecisionResearchNeedTypes.Mixed,
                            "Allocation at or below the statutory threshold",
                            DecisionResearchSourceClasses.None, false, "REJECT", null, []),
                    ]),
                new DecisionResearchTransformationAttemptDto(
                    Attempt: 2, ModelCode: "Astra", Status: "MALFORMED_JSON", LeavesProduced: 0,
                    Disposition: "REPAIR", Defects: ["JSON_PARSE_FAILED"], Leaves: []),
            ],
            SelectedResearchKey: null, SelectionReason: null, SearchQuery: null);
        var response = BaseResponse() with
        {
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 0, RoundsCommitted: 0, RoundsRolledBack: 0,
                TotalRetrievals: 0, StopReason: DecisionResearchLoopStopReasons.ResearchNeedUnresolved,
                Rounds: [], Failure: new DecisionResearchFailureDto(
                    RoundNumber: 0, Stage: "RESEARCH_NEED_SELECTION", ExceptionType: "RESEARCHABILITY_GATE",
                    Reason: "REPAIR: JSON_PARSE_FAILED", AuthoritativeStateChanged: false)
                {
                    Transformation = transformation,
                }),
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityState.AttentionRequired, trace.Integrity);
        Assert.Contains(trace.Research.Children, child =>
            child.Title == "Research Need Attempt 1" && child.Summary == "GATE_REJECTED · REPAIR");
        Assert.Contains(trace.Research.Children, child =>
            child.Title == "Research Need Attempt 2" && child.Summary == "MALFORMED_JSON · REPAIR");
        Assert.Contains(trace.Research.Children.SelectMany(child => child.Detail), detail =>
            detail.Label == "Defect" && detail.Value == "JSON_PARSE_FAILED");
    }

    [Fact]
    public void Project_FullyExecutedRun_IsHealthy()
    {
        // A run where every stage executed or was legitimately skipped, output is clean, and research ran
        // to a decision-ready stop — the only case that should read as fully HEALTHY.
        var id = Guid.NewGuid();
        var response = BaseResponse() with
        {
            StatusCode = DecisionStatusCodes.DecisionReady,
            Candidates = [Candidate(id)],
            WinnerCandidateId = id,
            FinalAnswer = "The motor carrier exemption applies.",
            ProposalIntegrity = new DecisionProposalIntegritySummaryDto(
                Mode: "ACTIVE", Disposition: "ACCEPT", Attempt: 1,
                RecoveryAttempted: false, Recovered: false, StructurallyValid: true, Defects: []),
            ResearchSummary = new DecisionResearchLoopSummaryDto(
                Enabled: true, RoundsExecuted: 2, RoundsCommitted: 2, RoundsRolledBack: 0,
                TotalRetrievals: 5, StopReason: DecisionResearchLoopStopReasons.DecisionReady, Rounds: []),
            Evidence =
            [
                new DecisionEvidenceDto(
                    Guid.NewGuid(), null, "src-1", "Source One", "snippet", 1.0m,
                    DecisionVerificationStates.Verified)
                {
                    SupportedObjective = "motor carrier exemption",
                    SupportingPassage = "the passage",
                },
            ],
            LastRecompetition = new DecisionRecompetitionDto(
                Guid.NewGuid(), null, id, WinnerChanged: false,
                PreviousEntropy: 0.4m, CurrentEntropy: 0.3m, PreviousMargin: 0.1m, CurrentMargin: 0.2m,
                ReopenedBranchCount: 0, ReasonCode: "VERIFICATION_CHANGE", CreatedDateUtc: DateTime.UtcNow),
            GovernanceVerdict = Verdict(outputClean: true, violations: []),
            OutputAuthorizations =
            [
                new DecisionOutputAuthorizationDto(
                    Guid.NewGuid(), "claim", "SUPPORTED", "FULL", "ALLOW", false, "ok", false),
            ],
        };

        var trace = DecisionIntegrityTraceProjector.Project(response);

        Assert.Equal(IntegrityState.Healthy, trace.Integrity);
    }

    // ── Builders

    private static DecisionSearchResponse BaseResponse() => new(
        DecisionSessionId: Guid.NewGuid(),
        Query: "test",
        StatusCode: DecisionStatusCodes.Running,
        TerminalStateCode: null,
        TerminationReasonCode: "NONE",
        DepthReached: 0,
        LlmCallCount: 0,
        CandidateEntropy: 0m,
        DecisionMargin: 0m,
        ContractCompleteness: 0m,
        FinalAnswer: null,
        WinnerCandidateId: null,
        Candidates: [],
        Branches: [],
        Evidence: [],
        FlipPoints: [],
        DurationMilliseconds: 10);

    private static DecisionCandidateDto Candidate(Guid id) => new(
        id, "C1", "Candidate 1", "Outcome 1",
        0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0.5m, 1m, 1, true, false);

    private static DecisionGovernanceVerdictDto Verdict(bool outputClean, IReadOnlyCollection<string> violations) => new(
        OverrideMode: "ADVISORY",
        V2ReadinessSatisfied: true,
        EaReady: true,
        OutputClean: outputClean,
        EffectiveReadinessSatisfied: true,
        OverrideApplied: false,
        ProjectedClaimCount: 1,
        AuthorizedClaimCount: outputClean ? 1 : 0,
        Blockers: [],
        OutputViolations: violations,
        InvolvedClaims: [],
        Narrative: []);
}
