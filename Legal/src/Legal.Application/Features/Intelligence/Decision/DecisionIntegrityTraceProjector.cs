namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Decision Integrity Trace projector.
//
// A PURE, DETERMINISTIC projection: given a fully-populated DecisionSearchResponse (the authoritative
// decision artifact), it derives the nine-stage trace, the three independent top-level concepts
// (System Integrity / Decision Readiness / Output Integrity), state consistency, decision movement,
// and diagnostics. It NEVER recomputes decision semantics or contradicts the response — every value is
// read from fields the response already carries. A stage with no backing data reports NotRun.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionIntegrityTraceProjector
{
    public static DecisionIntegrityTraceDto Project(DecisionSearchResponse r)
    {
        ArgumentNullException.ThrowIfNull(r);

        var proposal = ProjectProposal(r);
        var research = ProjectResearch(r);
        var verification = ProjectVerification(r);
        var promotion = ProjectPromotion(r);
        var propagation = ProjectPropagation(r);
        var recompetition = ProjectRecompetition(r);
        var frontier = ProjectFrontier(r);
        var outputControl = ProjectOutputControl(r);
        var finalAudit = ProjectFinalAudit(r);

        var outputIntegrity = ProjectOutputIntegrity(r);
        var consistency = ProjectConsistency(r);
        var allStages = new[]
        {
            proposal, research, verification, promotion, propagation,
            recompetition, frontier, outputControl, finalAudit,
        };
        var integrity = ProjectIntegrity(
            allStages, research, propagation, finalAudit, outputIntegrity, consistency);
        var readiness = ProjectReadiness(r);

        return new DecisionIntegrityTraceDto
        {
            SessionId = r.DecisionSessionId,
            DecisionStateVersion = r.DecisionStateVersion,
            Integrity = integrity,
            Readiness = readiness,
            OutputIntegrity = outputIntegrity,
            Consistency = consistency,
            Proposal = proposal,
            Research = research,
            Verification = verification,
            Promotion = promotion,
            Propagation = propagation,
            Recompetition = recompetition,
            Frontier = frontier,
            OutputControl = outputControl,
            FinalAudit = finalAudit,
            DecisionMovement = ProjectMovement(r),
            NextResearchTarget = r.PendingResearchNeed?.IssueLabel ?? r.NextBestAction?.Title,
            ResearchStopReason = r.ResearchSummary?.StopReason,
            Diagnostics = ProjectDiagnostics(r),
        };
    }

    // ── Stage 1: Proposal ────────────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectProposal(DecisionSearchResponse r)
    {
        var p = r.ProposalIntegrity;
        if (p is null)
            return Stage("PROPOSAL", "Proposal", IntegrityTraceStatus.NotRun, "not available");

        var status = p.Recovered ? IntegrityTraceStatus.Partial
            : string.Equals(p.Disposition, "ACCEPT", StringComparison.OrdinalIgnoreCase) ? IntegrityTraceStatus.Passed
            : p.StructurallyValid ? IntegrityTraceStatus.Passed
            : IntegrityTraceStatus.Failed;

        var compact = $"{p.Disposition} · Attempt {p.Attempt}";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Mode", p.Mode),
            new("Attempt", p.Attempt.ToString()),
            new("Disposition", p.Disposition),
            new("Structural integrity", p.StructurallyValid ? "PASS" : "FAIL"),
            new("Recovery attempted", p.RecoveryAttempted ? "YES" : "NO"),
            new("Result", p.Recovered ? "RECOVERED" : p.Disposition),
            new("Defects detected", p.Defects.Count.ToString()),
        };
        foreach (var d in p.Defects)
            detail.Add(new IntegrityStageDetailDto("Defect", d));

        return Stage("PROPOSAL", "Proposal", status, compact, detail);
    }

    // ── Stage 2: Research ────────────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectResearch(DecisionSearchResponse r)
    {
        var s = r.ResearchSummary;
        // Explicit NOT-RUN reasons so the panel is never ambiguous:
        //   SETTING DISABLED / GRAPH DISABLED / V2 UNAVAILABLE — from the eligibility snapshot captured
        //                        at the entry gate (the loop was never entered; the precise gate is known).
        //   NO RESEARCH EXECUTION — the loop was not driven this run and no eligibility was captured.
        //   LOOP DISABLED         — the loop feature is off (summary present, Enabled = false).
        //   DATA UNAVAILABLE      — the loop reported execution but no per-round telemetry persisted.
        if (s is null)
        {
            var e = r.ResearchEligibility;
            // INVARIANT (§14): status must derive from authoritative execution evidence, never absence
            // alone. With NO eligibility snapshot AND no summary we cannot prove the loop was not invoked —
            // report NOT OBSERVED (execution not observed), which is still a coverage gap that blocks HEALTHY,
            // rather than asserting a definitive NOT RUN.
            if (e is null)
                return Stage("RESEARCH", "Research", IntegrityTraceStatus.NotRun,
                    "NOT OBSERVED · NO EXECUTION EVIDENCE",
                    [new("Observation", "No research summary or eligibility snapshot was recorded; execution could not be observed.")]);
            // An eligibility snapshot with a concrete blocked reason authoritatively proves the loop was not
            // entered (setting off, graph off, frontier below threshold, …). That is a legitimate SKIP.
            if (!e.Eligible)
                return Stage("RESEARCH", "Research", IntegrityTraceStatus.Skipped,
                    $"SKIPPED · {ResearchEligibilityLabel(e)}", ResearchEligibilityDetail(e));
            // Eligible but no summary: the gate says the loop SHOULD have been entered, but no execution
            // result exists. INVARIANT (§14): never infer INVOKED from eligibility alone — report the
            // missing execution result as an observability gap that blocks HEALTHY.
            return Stage("RESEARCH", "Research", IntegrityTraceStatus.NotRun,
                "NOT OBSERVED · EXECUTION RESULT MISSING", ResearchEligibilityDetail(e));
        }
        if (s.Failure is not null)
        {
            var failurePoint = s.Failure.RoundNumber == 0
                ? "PRE-ROUND"
                : $"ROUND {s.Failure.RoundNumber}";
            // The loop was invoked and faulted before committing anything. Authoritative state is preserved;
            // the trace must say FAILED with the concrete reason, never a generic not-run.
            var failureChildren = ProjectResearchTransformation(s.Failure.Transformation);
            return Stage("RESEARCH", "Research", IntegrityTraceStatus.Failed,
                $"FAILED · {failurePoint}",
                [
                    new("Execution", "INVOKED"),
                    new("Round", s.Failure.RoundNumber == 0 ? "PRE-ROUND" : s.Failure.RoundNumber.ToString()),
                    new("Failure stage", s.Failure.Stage),
                    new("Failure type", s.Failure.ExceptionType),
                    new("Reason", Truncate(s.Failure.Reason)),
                    new("Authoritative state changed", s.Failure.AuthoritativeStateChanged ? "YES" : "NO"),
                    new("Stop reason", s.StopReason),
                ], failureChildren);
        }
        if (!s.Enabled)
            return Stage("RESEARCH", "Research", IntegrityTraceStatus.Skipped, "SKIPPED · LOOP DISABLED",
                [new("Reason", "The bounded research loop feature is disabled.")]);
        if (s.RoundsExecuted == 0)
            // The loop WAS invoked but no round executed — that is materially different from never running.
            return Stage("RESEARCH", "Research", IntegrityTraceStatus.Skipped,
                $"INVOKED · 0 ROUNDS · {s.StopReason}",
                [
                    new("Execution", "INVOKED"),
                    new("Rounds executed", "0"),
                    new("Stop reason", s.StopReason),
                ]);

        var status = string.Equals(s.StopReason, DecisionResearchLoopStopReasons.RoundFailed, StringComparison.OrdinalIgnoreCase)
                ? IntegrityTraceStatus.RolledBack
            : s.RoundsRolledBack > 0 ? IntegrityTraceStatus.Partial
            : string.Equals(s.StopReason, DecisionResearchLoopStopReasons.DecisionReady, StringComparison.OrdinalIgnoreCase)
                ? IntegrityTraceStatus.Passed
            : string.Equals(s.StopReason, DecisionResearchLoopStopReasons.MaxRounds, StringComparison.OrdinalIgnoreCase)
                ? IntegrityTraceStatus.Partial
            : IntegrityTraceStatus.Passed;

        var compact = $"COMPLETED · {s.RoundsExecuted} round(s) · {s.TotalRetrievals} retrieval(s) · {s.StopReason}";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Execution", s.RoundsExecuted > 0 ? "COMPLETED" : "NONE"),
            new("Rounds executed", s.RoundsExecuted.ToString()),
            new("Committed", s.RoundsCommitted.ToString()),
            new("Rolled back", s.RoundsRolledBack.ToString()),
            new("Retrieval operations", s.TotalRetrievals.ToString()),
            new("Stop reason", s.StopReason),
        };
        AddVerificationDiagnostics(detail, r);

        var children = s.Rounds.Select(round =>
        {
            var roundDetail = new List<IntegrityStageDetailDto>
            {
                new("Target", round.TargetBranchLabel ?? "—"),
                new("Proposition", round.PropositionToResolve ?? "—"),
                new("Information value", round.TargetInformationValue.ToString("0.##")),
                new("Retrieved", round.SourcesRetrieved.ToString()),
                new("Evaluated", round.SourcesEvaluated.ToString()),
            };

            AddCountDetails(roundDetail, round.VerificationDispositionCounts,
                DecisionEvidenceLifecycleStates.Verified,
                DecisionEvidenceLifecycleStates.PartiallySupported,
                DecisionEvidenceLifecycleStates.Unsupported,
                DecisionEvidenceLifecycleStates.Contradicted,
                DecisionEvidenceLifecycleStates.Unverifiable,
                DecisionEvidenceLifecycleStates.VerificationFailed,
                DecisionEvidenceLifecycleStates.RetrievalFailed);

            AddCountDetails(roundDetail, round.AttachmentStateCounts,
                DecisionEvidenceAttachmentStates.ProposedSupportFor,
                DecisionEvidenceAttachmentStates.SupportedBy,
                DecisionEvidenceAttachmentStates.PartiallySupportedBy,
                DecisionEvidenceAttachmentStates.ContradictedBy,
                DecisionEvidenceAttachmentStates.Unsupported);

            roundDetail.Add(new("Edge verification", round.EdgeVerificationStatus));
            roundDetail.Add(new("Evidence lifecycle", round.EvidenceLifecycleState));
            roundDetail.Add(new("Authoritative changes", round.AuthoritativeChanges.ToString()));
            roundDetail.Add(new("State change", round.AuthoritativeChanges > 0 ? "YES" : "NO"));
            roundDetail.Add(new("Winner changed", round.WinnerChanged ? "YES" : "NO"));
            roundDetail.Add(new("Entropy", $"{round.EntropyBefore:0.###} → {round.EntropyAfter:0.###}"));

            var transformationDetail = ProjectResearchTransformationDetails(round.Transformation);
            roundDetail.AddRange(transformationDetail);
            return new IntegrityStageChildDto(
                $"Round {round.RoundNumber}",
                round.WinnerChanged ? IntegrityTraceStatus.Partial : IntegrityTraceStatus.Passed,
                round.TargetBranchLabel,
                roundDetail);
        }).ToArray();

        return Stage("RESEARCH", "Research", status, compact, detail, children);
    }

    private static IReadOnlyList<IntegrityStageChildDto> ProjectResearchTransformation(
        DecisionResearchTransformationDto? transformation)
    {
        if (transformation is null)
            return [];

        var children = new List<IntegrityStageChildDto>
        {
            new(
                "Frontier",
                IntegrityTraceStatus.Partial,
                transformation.FrontierLabel,
                [
                    new("Branch", transformation.FrontierBranchCode ?? "—"),
                    new("Target", transformation.FrontierLabel ?? "—"),
                ]),
        };

        foreach (var attempt in transformation.Attempts)
        {
            var attemptStatus = string.Equals(attempt.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                ? IntegrityTraceStatus.Passed
                : string.Equals(attempt.Status, "MODEL_CALL_FAILED", StringComparison.OrdinalIgnoreCase)
                    ? IntegrityTraceStatus.Failed
                    : IntegrityTraceStatus.Partial;
            var attemptDetail = new List<IntegrityStageDetailDto>
            {
                new("Model", attempt.ModelCode),
                new("Status", attempt.Status),
                new("Disposition", attempt.Disposition),
                new("Leaves produced", attempt.LeavesProduced.ToString()),
            };
            foreach (var defect in attempt.Defects)
                attemptDetail.Add(new IntegrityStageDetailDto("Defect", defect));
            foreach (var leaf in attempt.Leaves)
            {
                attemptDetail.Add(new IntegrityStageDetailDto(
                    $"{leaf.ResearchKey} · {leaf.ResearchNeedType}",
                    $"{leaf.GateStatus} · {leaf.SourceClass} · researchable {(leaf.Researchable ? "YES" : "NO")} · {Truncate(leaf.Proposition)}"));
            }

            children.Add(new IntegrityStageChildDto(
                $"Research Need Attempt {attempt.Attempt}",
                attemptStatus,
                $"{attempt.Status} · {attempt.Disposition}",
                attemptDetail));
        }

        if (!string.IsNullOrWhiteSpace(transformation.SelectedResearchKey))
        {
            children.Add(new IntegrityStageChildDto(
                "Selected research leaf",
                IntegrityTraceStatus.Passed,
                transformation.SelectedResearchKey,
                [
                    new("Research key", transformation.SelectedResearchKey),
                    new("Selection reason", transformation.SelectionReason ?? "—"),
                    new("Search query", transformation.SearchQuery ?? "—"),
                ]));
        }

        return children;
    }

    private static IReadOnlyList<IntegrityStageDetailDto> ProjectResearchTransformationDetails(
        DecisionResearchTransformationDto? transformation)
    {
        if (transformation is null)
            return [];

        return
        [
            new("Research Need attempts", transformation.Attempts.Count.ToString()),
            new("Selected research leaf", transformation.SelectedResearchKey ?? "—"),
            new("Selection reason", transformation.SelectionReason ?? "—"),
            new("Search query", transformation.SearchQuery ?? "—"),
        ];
    }

    // ── Stage 3: Verification ────────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectVerification(DecisionSearchResponse r)
    {
        // No evidence retrieved is a LEGITIMATELY UNNECESSARY stage, not a coverage gap: there was
        // nothing to verify. Report Skipped with a reason so it does not block HEALTHY.
        if (r.Evidence.Count == 0)
            return Stage("VERIFY", "Verify", IntegrityTraceStatus.Skipped, "SKIPPED · no evidence retrieved",
                [new("Reason", "No evidence was retrieved, so there was nothing to verify.")]);

        var verified = r.EvidenceVerifications.Count > 0
            ? r.EvidenceVerifications.Count(v => v.IsDecisionAuthorized)
            : r.Evidence.Count(e => IsVerified(e.VerificationStatus));
        var invalidated = r.Evidence.Count(e => IsInvalidated(e.VerificationStatus));
        var lifecycleCounts = r.Evidence
            .GroupBy(e => e.LifecycleState ?? e.VerificationStatus, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var evaluated = r.Evidence.Count(e => !string.IsNullOrWhiteSpace(e.LifecycleState));

        var status = verified > 0 ? IntegrityTraceStatus.Passed
            : invalidated > 0 ? IntegrityTraceStatus.Partial
            : IntegrityTraceStatus.Partial;

        var compact = $"{verified} verified · {invalidated} rejected";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Retrieved", r.Evidence.Count.ToString()),
            new("Evaluated", evaluated.ToString()),
            new("Verified", verified.ToString()),
            new("Invalidated", invalidated.ToString()),
        };
        AddVerificationDiagnostics(detail, r);
        AddCountDetails(detail, lifecycleCounts,
            DecisionEvidenceLifecycleStates.Retrieved,
            DecisionEvidenceLifecycleStates.IdentityVerified,
            DecisionEvidenceLifecycleStates.CitationVerified,
            DecisionEvidenceLifecycleStates.PassageLocated,
            DecisionEvidenceLifecycleStates.PropositionSupportVerified,
            DecisionEvidenceLifecycleStates.HoldingVerified,
            DecisionEvidenceLifecycleStates.AuthorityValidated,
            DecisionEvidenceLifecycleStates.Verified,
            DecisionEvidenceLifecycleStates.PartiallySupported,
            DecisionEvidenceLifecycleStates.Unsupported,
            DecisionEvidenceLifecycleStates.Contradicted,
            DecisionEvidenceLifecycleStates.Unverifiable,
            DecisionEvidenceLifecycleStates.RetrievalFailed,
            DecisionEvidenceLifecycleStates.VerificationFailed,
            DecisionVerificationStates.Unverified,
            DecisionVerificationStates.Invalidated);
        detail.Add(new IntegrityStageDetailDto("Decision-authorized evidence", verified.ToString()));

        var verificationByEvidence = r.EvidenceVerifications
            .GroupBy(v => v.DecisionEvidenceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.EvaluatedDateUtc).First());
        var children = r.Evidence
            .Where(e => !string.IsNullOrWhiteSpace(e.LifecycleState)
                || IsVerified(e.VerificationStatus)
                || IsInvalidated(e.VerificationStatus)
                || verificationByEvidence.ContainsKey(e.DecisionEvidenceId))
            .Select(e =>
            {
                verificationByEvidence.TryGetValue(e.DecisionEvidenceId, out var verification);
                var evidenceDetail = new List<IntegrityStageDetailDto>
                {
                    new("Lifecycle state", e.LifecycleState ?? "NOT RECORDED"),
                    new("Authority state", e.VerificationStatus),
                    new("Supported objective", e.SupportedObjective ?? "—"),
                    new("Supporting passage", string.IsNullOrWhiteSpace(e.SupportingPassage) ? "—" : Truncate(e.SupportingPassage!)),
                };
                if (verification is not null)
                {
                    evidenceDetail.Add(new("Source type", verification.SourceTypeCode));
                    evidenceDetail.Add(new("Profile", verification.ProfileCode));
                    evidenceDetail.Add(new("Disposition", verification.DispositionCode));
                    evidenceDetail.Add(new("Decision authorized", verification.IsDecisionAuthorized ? "YES" : "NO"));
                    foreach (var factor in verification.Factors.OrderBy(f => FactorOrder(f.FactorCode)))
                        evidenceDetail.Add(new($"Factor · {factor.FactorCode}",
                            $"{factor.StateCode} · {factor.ReasonCode}{FormatReason(factor.Reason)}"));
                    foreach (var blocker in verification.BlockingReasons)
                        evidenceDetail.Add(new("Blocker", Truncate(blocker)));
                }

                var passed = verification?.IsDecisionAuthorized ?? IsVerified(e.VerificationStatus);
                return new IntegrityStageChildDto(
                    e.SourceTitle ?? e.SourceRef ?? e.DecisionEvidenceId.ToString()[..8],
                    passed ? IntegrityTraceStatus.Passed : IntegrityTraceStatus.Failed,
                    e.SupportedObjective,
                    evidenceDetail);
            }).ToArray();

        return Stage("VERIFY", "Verify", status, compact, detail, children);
    }

    // ── Stage 4: Evidence Promotion ──────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectPromotion(DecisionSearchResponse r)
    {
        // No evidence to promote is legitimately unnecessary, not a coverage gap.
        if (r.Evidence.Count == 0)
            return Stage("PROMOTE", "Promote", IntegrityTraceStatus.Skipped, "SKIPPED · no evidence retrieved",
                [new("Reason", "No evidence was retrieved, so there was nothing to promote.")]);

        var retrieved = r.Evidence.Count;
        var promoted = r.Evidence.Count(e => IsVerified(e.VerificationStatus));
        var notPromoted = retrieved - promoted;

        // Invariant surface: only VERIFIED evidence grants positive authority, so no unauthorized
        // positive contribution can exist by construction.
        var status = IntegrityTraceStatus.Passed;
        var compact = $"{promoted} / {retrieved} promoted";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Retrieved", retrieved.ToString()),
            new("Verified", promoted.ToString()),
            new("Promoted", promoted.ToString()),
            new("Not promoted", notPromoted.ToString()),
            new("Unauthorized positive contribution", "0"),
        };

        return Stage("PROMOTE", "Promote", status, compact, detail);
    }

    // ── Stage 5: Dependency Propagation ──────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectPropagation(DecisionSearchResponse r)
    {
        // Propagation is observable through its persisted downstream recompetition result. A completed
        // research round alone is not proof of propagation: retrieval/verification may have produced no
        // authoritative evidence change, as in an UNSUPPORTED or otherwise unpromoted round.
        var rc = r.LastRecompetition;
        if (rc is null)
            return Stage("PROPAGATE", "Propagate", IntegrityTraceStatus.Skipped,
                "SKIPPED · NO AUTHORITATIVE EVIDENCE CHANGE",
                [new("Reason", "No authoritative evidence change produced a persisted propagation/recompetition result.")]);

        var reopened = rc?.ReopenedBranchCount ?? 0;
        var status = IntegrityTraceStatus.Passed;
        var compact = reopened > 0 ? $"{reopened} branch(es) reopened" : "propagation complete";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Trigger", "Authoritative verification change"),
            new("Reopened branches", reopened.ToString()),
            new("Propagation", reopened > 0 ? "COMPLETE" : "EXECUTED · NO BRANCHES REOPENED"),
        };

        return Stage("PROPAGATE", "Propagate", status, compact, detail);
    }

    // ── Stage 6: Recompetition ───────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectRecompetition(DecisionSearchResponse r)
    {
        var rc = r.LastRecompetition;
        // No recompetition is legitimately unnecessary when no propagation reopened a branch.
        if (rc is null)
            return Stage("RECOMPETE", "Recompete", IntegrityTraceStatus.Skipped, "SKIPPED · no recompetition",
                [new("Reason", "No propagation reopened a branch, so candidates were not recompeted.")]);

        var status = IntegrityTraceStatus.Passed;
        var compact = rc.WinnerChanged ? "Leader changed" : "Leader unchanged";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Winner changed", rc.WinnerChanged ? "YES" : "NO"),
            new("Entropy", $"{rc.PreviousEntropy:0.###} → {rc.CurrentEntropy:0.###}"),
            new("Margin", $"{rc.PreviousMargin:0.###} → {rc.CurrentMargin:0.###}"),
            new("Reopened branches", rc.ReopenedBranchCount.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(rc.ReasonCode))
            detail.Add(new IntegrityStageDetailDto("Reason", rc.ReasonCode!));

        return Stage("RECOMPETE", "Recompete", status, compact, detail);
    }

    // ── Stage 7: Decision Frontier ───────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectFrontier(DecisionSearchResponse r)
    {
        var unresolved = r.Branches.Count(b => b.IsOnFrontier);
        var decisionReady = string.Equals(r.StatusCode, DecisionStatusCodes.DecisionReady, StringComparison.OrdinalIgnoreCase);

        var status = decisionReady && unresolved == 0 ? IntegrityTraceStatus.Passed
            : unresolved > 0 ? IntegrityTraceStatus.Partial
            : IntegrityTraceStatus.Passed;

        var next = r.PendingResearchNeed?.IssueLabel ?? r.NextBestAction?.Title;
        var compact = unresolved > 0
            ? $"{unresolved} unresolved" + (next is null ? string.Empty : $" · next: {Truncate(next, 40)}")
            : "no open frontier";

        var detail = new List<IntegrityStageDetailDto>
        {
            new("Unresolved material dependencies", unresolved.ToString()),
            new("Next research need", next ?? "—"),
        };
        if (r.NextBestAction is { } nba)
        {
            detail.Add(new IntegrityStageDetailDto("Information value", nba.ExpectedInformationValue.ToString("0.##")));
            detail.Add(new IntegrityStageDetailDto("Flip potential", nba.FlipPotential.ToString("0.##")));
        }

        return Stage("FRONTIER", "Frontier", status, compact, detail);
    }

    // ── Stage 8: Output Control ──────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectOutputControl(DecisionSearchResponse r)
    {
        if (r.OutputAuthorizations.Count == 0)
            // Two very different cases share zero authorizations:
            //   • A substantive answer with NO extracted claims is a COVERAGE GAP: the auditor had nothing
            //     to judge even though the prose contains assertions. That is NotRun (NOT EVALUATED) and it
            //     must block HEALTHY — we cannot vouch for un-audited prose.
            //   • No substantive answer at all is legitimately unnecessary → Skipped with a reason.
            return HasSubstantiveAnswer(r)
                ? Stage("OUTPUT", "Output Control", IntegrityTraceStatus.NotRun,
                    ExtractionCompactSummary(r), ExtractionDetail(r))
                : Stage("OUTPUT", "Output Control", IntegrityTraceStatus.Skipped, "SKIPPED · no answer composed",
                    [new("Reason", "No substantive answer was composed, so there were no assertions to control.")]);

        var allow = CountDisposition(r, "ALLOW");
        var qualify = CountDisposition(r, "QUALIFY");
        var suppress = CountDisposition(r, "SUPPRESS");
        var correct = CountDisposition(r, "CORRECT");

        // Authoritative transform outcome. Prefer the aggregate summary from the enforcement pass; fall
        // back to counting the per-claim ProseTransformed flag (now set from the real applied set).
        var ts = r.OutputTransformSummary;
        var appliedCount = r.OutputAuthorizations.Count(a => a.ProseTransformed);
        var required = ts?.RequiredCount ?? (qualify + suppress + correct);
        var attempted = ts?.RequiredCount ?? required;
        var finalAuditClean = r.GovernanceVerdict is { OutputClean: true, OutputViolations.Count: 0 };
        var applied = ts?.AppliedCount ?? (finalAuditClean ? required : appliedCount);
        var remaining = ts?.UnauthorizedAssertionsRemaining ?? (finalAuditClean ? 0 : Math.Max(0, required - applied));
        var postTransformClean = ts?.PostTransformClean ?? (finalAuditClean || remaining == 0);

        // The stage no longer claims PASSED unconditionally: if restatement was required but could not be
        // applied to the prose, unauthorized assertions may still stand and the stage warrants attention.
        var status = required == 0 ? IntegrityTraceStatus.Passed
            : !postTransformClean ? IntegrityTraceStatus.Failed
            : applied < required ? IntegrityTraceStatus.Partial
            : IntegrityTraceStatus.Passed;

        var dispositionSummary = $"{allow}A · {qualify}Q · {suppress}S · {correct}C";
        var compact = status == IntegrityTraceStatus.Passed
            ? $"ENFORCED · {remaining} unauthorized · {dispositionSummary}"
            : dispositionSummary;
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Claims detected", r.OutputAuthorizations.Count.ToString()),
            new("ALLOW", allow.ToString()),
            new("QUALIFY", qualify.ToString()),
            new("SUPPRESS", suppress.ToString()),
            new("CORRECT", correct.ToString()),
            new("Transformations required", required.ToString()),
            new("Transformations attempted", attempted.ToString()),
            new("Transformations applied", applied.ToString()),
            new("Original assertions remain", remaining.ToString()),
            new("Post-transform audit", postTransformClean ? "CLEAN" : "FAILED"),
        };

        var children = r.OutputAuthorizations
            .Where(a => !string.Equals(a.Disposition, "ALLOW", StringComparison.OrdinalIgnoreCase))
            .Select(a => new IntegrityStageChildDto(
                $"Claim {a.ClaimId.ToString()[..8]}",
                a.Disposition switch
                {
                    "CORRECT" => IntegrityTraceStatus.Failed,
                    "SUPPRESS" => IntegrityTraceStatus.Partial,
                    _ => IntegrityTraceStatus.Partial,
                },
                a.Disposition,
                [
                    new("Disposition", a.Disposition),
                    new("Authoritative state", a.VerificationState),
                    new("Draft", string.IsNullOrWhiteSpace(a.ClaimText) ? "—" : Truncate(a.ClaimText)),
                    new("Prose transformed", a.ProseTransformed ? "APPLIED" : "NOT APPLIED"),
                    new("Reason", a.Reason),
                ])).ToArray();

        return Stage("OUTPUT", "Output Control", status, compact, detail, children);
    }

    // ── Stage 9: Final Audit ─────────────────────────────────────────────────────────────────────
    private static IntegrityStageDto ProjectFinalAudit(DecisionSearchResponse r)
    {
        var gv = r.GovernanceVerdict;
        if (gv is null && r.OutputAuthorizations.Count == 0)
            // With no verdict and no authorizations there is nothing to audit. If a substantive answer was
            // composed this is a COVERAGE GAP (NotRun / NOT EVALUATED) that must block HEALTHY; otherwise
            // the stage was legitimately unnecessary → Skipped.
            return HasSubstantiveAnswer(r)
                ? Stage("AUDIT", "Final Audit", IntegrityTraceStatus.NotRun,
                    ExtractionCompactSummary(r), ExtractionDetail(r))
                : Stage("AUDIT", "Final Audit", IntegrityTraceStatus.Skipped, "SKIPPED · no answer composed",
                    [new("Reason", "No substantive answer was composed, so there was nothing to audit.")]);

        // CLEAN must never be inferred from the ABSENCE of claims. If the run produced a substantive
        // composed answer but no material claims were extracted from it, the auditor had nothing to
        // authorize — that is NOT the same as the answer being clean. Report NOT EVALUATED instead of a
        // false-positive CLEAN, so the trace does not vouch for un-audited prose.
        if (!ClaimsWereExtracted(r) && HasSubstantiveAnswer(r))
        {
            return Stage("AUDIT", "Final Audit", IntegrityTraceStatus.NotRun,
                ExtractionCompactSummary(r), ExtractionDetail(r));
        }

        var clean = gv?.OutputClean ?? true;
        var violations = gv?.OutputViolations.Count ?? 0;

        var status = clean ? IntegrityTraceStatus.Passed : IntegrityTraceStatus.Failed;
        var compact = clean ? "CLEAN · 0 unauthorized assertions" : $"{violations} unauthorized assertion(s)";
        var detail = new List<IntegrityStageDetailDto>
        {
            new("Material claims", r.OutputAuthorizations.Count.ToString()),
            new("Authorized assertions", CountDisposition(r, "ALLOW").ToString()),
            new("Qualified uncertainties", CountDisposition(r, "QUALIFY").ToString()),
            new("Suppressed assertions", CountDisposition(r, "SUPPRESS").ToString()),
            new("Corrected assertions", CountDisposition(r, "CORRECT").ToString()),
            new("Mapped claims", r.OutputAuthorizations.Count(a => string.Equals(a.MappingState, "MAPPED", StringComparison.OrdinalIgnoreCase)).ToString()),
            new("Unmapped claims", r.OutputAuthorizations.Count(a => string.Equals(a.MappingState, "UNMAPPED", StringComparison.OrdinalIgnoreCase)).ToString()),
            new("Scope-exceeded claims", r.OutputAuthorizations.Count(a => string.Equals(a.MappingState, "SCOPEEXCEEDED", StringComparison.OrdinalIgnoreCase)).ToString()),
            new("Unauthorized assertions remaining", clean ? "0" : violations.ToString()),
            new("Result", clean ? "CLEAN" : "ATTENTION REQUIRED"),
        };

        return Stage("AUDIT", "Final Audit", status, compact, detail);
    }

    // ── Top-level concepts ───────────────────────────────────────────────────────────────────────

    private static OutputIntegrityState ProjectOutputIntegrity(DecisionSearchResponse r)
    {
        if (r.GovernanceVerdict is null && r.OutputAuthorizations.Count == 0)
            return OutputIntegrityState.NotRun;
        // A composed answer that was never claim-extracted has not been shown to be clean; do not
        // report Clean on the strength of an empty auditor. NotRun means "not evaluated" to the UI.
        if (!ClaimsWereExtracted(r) && HasSubstantiveAnswer(r))
            return OutputIntegrityState.NotRun;
        var clean = r.GovernanceVerdict?.OutputClean ?? true;
        return clean ? OutputIntegrityState.Clean : OutputIntegrityState.AttentionRequired;
    }

    // True when the output pipeline actually extracted material claims from the composed answer and
    // submitted them for authorization. Zero extracted claims means the auditor had nothing to judge.
    private static bool ClaimsWereExtracted(DecisionSearchResponse r) =>
        r.OutputAuthorizations.Count > 0;

    private static string ExtractionCompactSummary(DecisionSearchResponse r) =>
        r.OutputClaimExtraction?.StatusCode switch
        {
            "FAILED" => "NOT EVALUATED · CLAIM EXTRACTION FAILED",
            "EMPTY" => "NOT EVALUATED · NO CLAIMS EXTRACTED",
            "COMPLETED" when r.OutputClaimExtraction.ClaimsReturned > 0 => "NOT EVALUATED · EXTRACTED CLAIMS UNMAPPED",
            _ => "NOT EVALUATED · CLAIM EXTRACTION NOT OBSERVED",
        };

    private static IReadOnlyList<IntegrityStageDetailDto> ExtractionDetail(DecisionSearchResponse r)
    {
        var extraction = r.OutputClaimExtraction;
        return
        [
            new("Answer length", (extraction?.AnswerLength ?? r.FinalAnswer?.Length ?? 0).ToString()),
            new("Substantive answer", (extraction?.SubstantiveAnswer ?? HasSubstantiveAnswer(r)) ? "YES" : "NO"),
            new("Extraction attempted", extraction?.Attempted == true ? "YES" : "NO"),
            new("Claims returned", (extraction?.ClaimsReturned ?? 0).ToString()),
            new("Extractor result", extraction?.StatusCode ?? "NOT OBSERVED"),
            new("Failure reason", extraction?.FailureReason ?? "—"),
            new("Auditor coverage", "NONE · no extracted claim mapped to an authoritative session claim"),
            new("Result", "NOT EVALUATED"),
        ];
    }

    // True when the run produced a non-trivial answer whose assertions would need auditing.
    private static bool HasSubstantiveAnswer(DecisionSearchResponse r) =>
        !string.IsNullOrWhiteSpace(r.FinalAnswer);

    // Internal-state coherence: the projected trace must not disagree with authoritative fields. We
    // check cross-references that must hold (e.g. a winner id must exist among candidates when present).
    private static StateConsistency ProjectConsistency(DecisionSearchResponse r)
    {
        if (r.Candidates.Count == 0)
            return StateConsistency.Unknown;

        // A declared winner must be a real candidate.
        if (r.WinnerCandidateId is { } winnerId &&
            !r.Candidates.Any(c => c.DecisionCandidateId == winnerId))
            return StateConsistency.Invalid;

        // Recompetition's current winner must agree with the response winner.
        if (r.LastRecompetition is { CurrentWinnerCandidateId: { } rcWinner } &&
            r.WinnerCandidateId is { } respWinner &&
            rcWinner != respWinner)
            return StateConsistency.Invalid;

        return StateConsistency.Valid;
    }

    private static IntegrityState ProjectIntegrity(
        IReadOnlyList<IntegrityStageDto> allStages,
        IntegrityStageDto research,
        IntegrityStageDto propagation,
        IntegrityStageDto finalAudit,
        OutputIntegrityState outputIntegrity,
        StateConsistency consistency)
    {
        // Precedence: FAILED > ATTENTION > INCOMPLETE > HEALTHY. Each level is evaluated in order.

        // FAILED: final audit failed, state inconsistent, or a research-round failure was NOT rolled back
        // (a rolled-back failure is healthy — it proves atomicity).
        if (finalAudit.Status == IntegrityTraceStatus.Failed
            || consistency == StateConsistency.Invalid)
            return IntegrityState.Failed;

        // ATTENTION: an applicable Research stage failed or was partial/rolled back, output requires
        // attention, or propagation failed. Preserving authoritative state prevents FAILED integrity,
        // but it does not make an invoked Research failure HEALTHY.
        if (outputIntegrity == OutputIntegrityState.AttentionRequired
            || research.Status == IntegrityTraceStatus.Failed
            || research.Status == IntegrityTraceStatus.RolledBack
            || research.Status == IntegrityTraceStatus.Partial
            || propagation.Status == IntegrityTraceStatus.Failed)
            return IntegrityState.AttentionRequired;

        // INCOMPLETE: one or more required controls were not evaluated. A stage that is NotRun (including
        // NOT OBSERVED / NOT EVALUATED coverage gaps) means we cannot vouch for the run as fully HEALTHY.
        // Skipped stages are legitimately unnecessary and do NOT trip Incomplete. Output not evaluated is a
        // coverage gap that also downgrades to Incomplete.
        if (outputIntegrity == OutputIntegrityState.NotRun
            || allStages.Any(s => s.Status == IntegrityTraceStatus.NotRun))
            return IntegrityState.Incomplete;

        // HEALTHY: every required control executed soundly or was explicitly skipped with a reason.
        return IntegrityState.Healthy;
    }

    private static TraceReadinessState ProjectReadiness(DecisionSearchResponse r)
    {
        if (string.Equals(r.StatusCode, DecisionStatusCodes.DecisionReady, StringComparison.OrdinalIgnoreCase))
            return TraceReadinessState.Ready;
        if (string.Equals(r.StatusCode, DecisionStatusCodes.ProvisionalDecision, StringComparison.OrdinalIgnoreCase))
            return TraceReadinessState.Provisional;
        if (r.Candidates.Count == 0)
            return TraceReadinessState.Unknown;
        return TraceReadinessState.NotReady;
    }

    private static DecisionMovementDto? ProjectMovement(DecisionSearchResponse r)
    {
        var rc = r.LastRecompetition;
        if (rc is null)
            return null;

        string? Label(Guid? id) => id is null ? null
            : r.Candidates.FirstOrDefault(c => c.DecisionCandidateId == id)?.Outcome
              ?? r.Candidates.FirstOrDefault(c => c.DecisionCandidateId == id)?.DisplayName;

        return new DecisionMovementDto(
            Label(rc.PreviousWinnerCandidateId),
            Label(rc.CurrentWinnerCandidateId),
            rc.WinnerChanged,
            rc.PreviousEntropy, rc.CurrentEntropy,
            rc.PreviousMargin, rc.CurrentMargin);
    }

    private static TraceDiagnosticsDto ProjectDiagnostics(DecisionSearchResponse r)
    {
        var total = r.PhaseTimings.FirstOrDefault(t => t.Phase == "Total")?.Milliseconds ?? r.DurationMilliseconds;
        return new TraceDiagnosticsDto
        {
            SessionId = r.DecisionSessionId,
            CorrelationId = null,
            DecisionStateVersion = r.DecisionStateVersion,
            TotalDurationMs = total,
            PhaseTimings = r.PhaseTimings
                .Select(t => new IntegrityStageDetailDto(t.Phase, $"{t.Milliseconds} ms"))
                .ToArray(),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static int CountDisposition(DecisionSearchResponse r, string code) =>
        r.OutputAuthorizations.Count(a => string.Equals(a.Disposition, code, StringComparison.OrdinalIgnoreCase));

    private static void AddCountDetails(
        ICollection<IntegrityStageDetailDto> detail,
        IReadOnlyDictionary<string, int> counts,
        params string[] states)
    {
        foreach (var state in states)
        {
            detail.Add(new IntegrityStageDetailDto(state, counts.GetValueOrDefault(state).ToString()));
        }
    }

    private static void AddVerificationDiagnostics(
        ICollection<IntegrityStageDetailDto> detail,
        DecisionSearchResponse response)
    {
        if (response.EvidenceVerifications.Count == 0)
            return;

        detail.Add(new("Verification runs", response.EvidenceVerifications.Count.ToString()));
        detail.Add(new("Retrieved candidates",
            response.EvidenceVerifications.Sum(v => v.RetrievedCount).ToString()));
        detail.Add(new("Pre-screen rejected",
            response.EvidenceVerifications.Sum(v => v.PreScreenRejectedCount).ToString()));
        detail.Add(new("Decision-authorized runs",
            response.EvidenceVerifications.Count(v => v.IsDecisionAuthorized).ToString()));
        detail.Add(new("Verification blockers",
            response.EvidenceVerifications.Sum(v => v.BlockingReasons.Count).ToString()));
        detail.Add(new("Mechanical checks",
            response.EvidenceVerifications.Sum(v => v.MechanicalVerificationCount).ToString()));
        detail.Add(new("Semantic evaluations",
            response.EvidenceVerifications.Sum(v => v.SemanticVerificationCount).ToString()));
        detail.Add(new("POLOXI deepening",
            response.EvidenceVerifications.Sum(v => v.PoloxiDeepeningCount).ToString()));
        detail.Add(new("Semantic cache hits",
            response.EvidenceVerifications.Sum(v => v.CacheHitCount).ToString()));
        detail.Add(new("Semantic tokens",
            $"{response.EvidenceVerifications.Sum(v => v.InputTokenCount)} in · {response.EvidenceVerifications.Sum(v => v.OutputTokenCount)} out"));
        detail.Add(new("Verification latency",
            $"{response.EvidenceVerifications.Sum(v => v.LatencyMilliseconds)} ms"));
        foreach (var disposition in response.EvidenceVerifications
                     .GroupBy(v => v.DispositionCode)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            detail.Add(new($"Disposition · {disposition.Key}", disposition.Count().ToString()));

        foreach (var factor in response.EvidenceVerifications
                     .SelectMany(v => v.Factors)
                     .GroupBy(f => new { f.FactorCode, f.StateCode })
                     .OrderBy(g => FactorOrder(g.Key.FactorCode))
                     .ThenBy(g => g.Key.StateCode, StringComparer.OrdinalIgnoreCase))
        {
            detail.Add(new($"{factor.Key.FactorCode} · {factor.Key.StateCode}", factor.Count().ToString()));
        }
    }

    private static int FactorOrder(string factorCode) => factorCode.ToUpperInvariant() switch
    {
        "IDENTITY" => 1,
        "CITATION" => 2,
        "PASSAGE" => 3,
        "PROPOSITION_SUPPORT" => 4,
        "HOLDING" => 5,
        "AUTHORITY" => 6,
        _ => 99,
    };

    private static string FormatReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? string.Empty : $" · {Truncate(reason)}";

    private static bool IsVerified(string? status) =>
        string.Equals(status, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, DecisionEvidenceLifecycleStates.Verified, StringComparison.OrdinalIgnoreCase);

    private static bool IsInvalidated(string? status) =>
        string.Equals(status, DecisionVerificationStates.Invalidated, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, DecisionEvidenceLifecycleStates.Contradicted, StringComparison.OrdinalIgnoreCase);

    private static IntegrityStageDto Stage(
        string key, string label, IntegrityTraceStatus status, string? compact,
        IReadOnlyList<IntegrityStageDetailDto>? detail = null,
        IReadOnlyList<IntegrityStageChildDto>? children = null) => new()    {
        Key = key,
        Label = label,
        Status = status,
        CompactSummary = compact,
        Detail = detail ?? [],
        Children = children ?? [],
    };

    private static string Truncate(string value, int max = 160)
        => value.Length <= max ? value : value[..max].TrimEnd() + "…";

    // Human-readable NOT-RUN label derived from the research-loop eligibility snapshot. When no snapshot
    // was captured (legacy/read-back paths) the generic NO RESEARCH EXECUTION label is used.
    private static string ResearchEligibilityLabel(DecisionResearchEligibilityDto? e) => e?.NotRunReason switch
    {
        DecisionResearchNotRunReasons.SettingDisabled => "SETTING DISABLED",
        DecisionResearchNotRunReasons.GraphDisabled => "GRAPH DISABLED",
        DecisionResearchNotRunReasons.V2Unavailable => "V2 UNAVAILABLE",
        DecisionResearchNotRunReasons.NoFrontier => "NO FRONTIER",
        DecisionResearchNotRunReasons.FrontierBelowThreshold => "FRONTIER BELOW THRESHOLD",
        DecisionResearchNotRunReasons.SettingsUnavailable => "SETTINGS UNAVAILABLE",
        _ => "NO RESEARCH EXECUTION",
    };

    // Per-gate booleans surfaced in the Research stage detail so the exact failing prerequisite is visible.
    private static IReadOnlyList<IntegrityStageDetailDto> ResearchEligibilityDetail(DecisionResearchEligibilityDto? e)
    {
        if (e is null)
            return [];
        return new List<IntegrityStageDetailDto>
        {
            new("Enabled setting", e.EnabledSetting ? "TRUE" : "FALSE"),
            new("Use graph", e.UseGraph ? "TRUE" : "FALSE"),
            new("V2 available", e.V2Available ? "TRUE" : "FALSE"),
            new("Frontier count", e.FrontierCount.ToString()),
            new("Highest frontier IV", e.HighestFrontierInformationValue.ToString("0.##")),
            new("Minimum frontier IV", e.MinFrontierInformationValue.ToString("0.##")),
            new("Retrieval budget", e.RetrievalBudget ? "AVAILABLE" : "NONE"),
            new("Eligible", e.Eligible ? "TRUE" : "FALSE"),
            new("Not-run reason", e.NotRunReason),
        };
    }
}
