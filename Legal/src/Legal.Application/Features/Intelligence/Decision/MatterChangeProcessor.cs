using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Matter Change Processor (Phase 1: Change Awareness).
//
// Steps 1-6 of the change pipeline (steps 7-9, verify/reevaluate/recalculate, are later phases):
//   1. Register immutable source version   (Legal_MatterChangeEvent, idempotent on the version).
//   2. Extract candidate facts             (deterministic passage-derived candidate propositions).
//   3. Match existing propositions         (semantic-ish token match against Legal_PropositionEvidenceLink).
//   4. Determine material impact           (classify NO_MATERIAL / POTENTIAL / CONTRADICTION / NEW_FACT).
//   5. Record impact                       (Legal_DecisionImpact rows, before/after states).
//   6. Invalidate affected reliance        (set snapshot RelianceStatus; create Legal_DecisionReviewTask).
//
// It never deletes history and never silently reverses an attorney-approved conclusion — it only flags
// reliance and creates review tasks. POLOXI Core is untouched; targeted reevaluation is Phase 2.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MatterChangeProcessor(
    IDecisionIntegrityRepository integrityRepository,
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILogger<MatterChangeProcessor> logger) : IMatterChangeProcessor
{
    // Tokens that hint a new source contradicts a prior acceptance/conclusion (deterministic, auditable).
    private static readonly string[] ContradictionCues =
    [
        "counteroffer", "counter-offer", "reject", "rejected", "denied", "dispute", "disputed",
        "revoke", "revoked", "withdraw", "withdrawn", "rescind", "rescinded", "not accept",
        "did not accept", "no agreement", "terminated", "void", "invalid", "contrary", "however"
    ];

    public async Task<MatterChangeProcessingResult> ProcessDocumentChangeAsync(
        Guid tenantId,
        Guid userId,
        Guid decisionMatterId,
        Guid legalDocumentId,
        Guid legalDocumentVersionId,
        string sourceHash,
        string? sourceLabel,
        DateTime? documentDateUtc,
        CancellationToken cancellationToken = default)
    {
        // ── Step 1: register the immutable source version as a change event (idempotent). ──────────
        var idempotencyKey = $"DOC:{legalDocumentVersionId:N}:{sourceHash}";
        var changeEventId = Guid.NewGuid();
        var (eventId, alreadyExisted) = await integrityRepository.CreateChangeEventAsync(
            new MatterChangeEventPersistence(
                changeEventId, decisionMatterId, MatterChangeSource.DocumentUpload, legalDocumentId, legalDocumentVersionId,
                sourceHash, sourceLabel, documentDateUtc, idempotencyKey, ClassificationCode: null, Summary: null,
                CandidateFactsJson: null, MatterChangeProcessingStatus.Pending, ProcessingError: null,
                AffectedPropositionCount: 0, AffectedCandidateCount: 0, ProcessedDateUtc: null, tenantId, userId),
            cancellationToken);

        if (alreadyExisted)
        {
            logger.LogInformation("Matter change for document version {VersionId} already processed (event {EventId}); skipping.", legalDocumentVersionId, eventId);
            var existing = await integrityRepository.GetChangeEventAsync(tenantId, eventId, cancellationToken);
            var existingTasks = await integrityRepository.GetReviewTasksForEventAsync(tenantId, eventId, cancellationToken);
            return new MatterChangeProcessingResult(
                eventId, existing?.ClassificationCode ?? MatterChangeClassification.NoMaterialImpact,
                existing?.AffectedPropositionCount ?? 0, existing?.AffectedCandidateCount ?? 0,
                existingTasks.Select(t => t.DecisionReviewTaskId).ToArray());
        }

        try
        {
            // ── Step 2: extract candidate facts from the new source's passages. ────────────────────
            var passages = await documentCorpusRepository.GetDocumentPassagesAsync(tenantId, legalDocumentVersionId, cancellationToken);
            var candidateFacts = ExtractCandidateFacts(passages);

            // ── Step 3: match candidate facts against existing proposition→evidence links. ─────────
            var existingLinks = await integrityRepository.GetPropositionEvidenceLinksAsync(tenantId, decisionMatterId, cancellationToken);
            var latestSnapshot = await integrityRepository.GetLatestMatterSnapshotAsync(tenantId, decisionMatterId, cancellationToken);

            var matches = MatchPropositions(candidateFacts, existingLinks);

            // ── Step 4: determine material impact + classify the change. ───────────────────────────
            var hasContradictionCue = candidateFacts.Any(f => ContainsAnyCue(f));
            var classification = Classify(matches, hasContradictionCue, existingLinks.Count);

            // ── Step 5: record impact rows (before/after) for matched propositions/candidates. ─────
            var impacts = new List<DecisionImpactPersistence>();
            var affectedCandidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var match in matches)
            {
                var severity = hasContradictionCue ? DecisionImpactSeverity.Material : DecisionImpactSeverity.Potential;
                var previous = match.LinkKindCode == PropositionEvidenceLinkKind.Support ? "Supported" : match.LinkKindCode;
                var current = hasContradictionCue ? "Disputed" : "Review pending";
                impacts.Add(new DecisionImpactPersistence(
                    Guid.NewGuid(), eventId, decisionMatterId, latestSnapshot?.DecisionSnapshotId,
                    DecisionImpactKind.Proposition, match.PropositionKey, match.PropositionStatement,
                    previous, current, severity,
                    $"New source '{sourceLabel ?? "document"}' matched proposition '{match.PropositionKey}'.",
                    tenantId, userId));

                // Traverse the bounded dependency graph: which candidates depend on this proposition?
                var dependents = await integrityRepository.GetDependentKeysForPropositionAsync(
                    tenantId, decisionMatterId, DecisionImpactKind.Proposition, match.PropositionKey, cancellationToken);
                foreach (var dependent in dependents)
                {
                    if (!affectedCandidateKeys.Add(dependent))
                        continue;
                    impacts.Add(new DecisionImpactPersistence(
                        Guid.NewGuid(), eventId, decisionMatterId, latestSnapshot?.DecisionSnapshotId,
                        DecisionImpactKind.Candidate, dependent, dependent,
                        "Previously evaluated", "Reevaluation required",
                        hasContradictionCue ? DecisionImpactSeverity.Material : DecisionImpactSeverity.Potential,
                        $"Candidate '{dependent}' depends on affected proposition '{match.PropositionKey}'.",
                        tenantId, userId));
                }
            }

            await integrityRepository.SaveImpactsAsync(impacts, cancellationToken);

            var affectedPropositionCount = matches.Count;
            var affectedCandidateCount = affectedCandidateKeys.Count;

            // ── Step 6: invalidate affected reliance + create the attorney review task. ────────────
            var reviewTaskIds = new List<Guid>();
            if (classification is not MatterChangeClassification.NoMaterialImpact)
            {
                if (latestSnapshot is not null)
                {
                    var relianceStatus = classification switch
                    {
                        MatterChangeClassification.MaterialContradiction => DecisionRelianceStatus.ReassessmentRequired,
                        MatterChangeClassification.NewMaterialFact => DecisionRelianceStatus.ReassessmentRequired,
                        _ => DecisionRelianceStatus.ReviewPending
                    };
                    await integrityRepository.UpdateSnapshotRelianceAsync(
                        tenantId, userId, latestSnapshot.DecisionSnapshotId, relianceStatus,
                        $"New source '{sourceLabel ?? "document"}' ({classification}) may affect this conclusion.",
                        cancellationToken);
                }

                // Mark the specific supporting links as contested (NOT invalid — evidence integrity preserved).
                if (hasContradictionCue && matches.Count > 0)
                {
                    var contested = matches
                        .Where(m => m.LinkKindCode == PropositionEvidenceLinkKind.Support)
                        .Select(m => m with { StatusCode = PropositionEvidenceLinkStatus.Contested, DecisionSnapshotId = latestSnapshot?.DecisionSnapshotId })
                        .ToArray();
                    // Contested links are recorded as NEW rows so the original ACTIVE link history is preserved.
                    if (contested.Length > 0)
                        await integrityRepository.SavePropositionEvidenceLinksAsync(
                            contested.Select(c => c with { PropositionEvidenceLinkId = Guid.NewGuid() }).ToArray(), cancellationToken);
                }

                var priority = classification is MatterChangeClassification.MaterialContradiction
                    ? DecisionReviewTaskPriority.High
                    : DecisionReviewTaskPriority.Normal;
                var taskId = await integrityRepository.CreateReviewTaskAsync(new DecisionReviewTaskPersistence(
                    Guid.NewGuid(), decisionMatterId, eventId, latestSnapshot?.DecisionSnapshotId,
                    "CHANGE_REVIEW",
                    BuildTaskTitle(classification, sourceLabel),
                    BuildTaskDetail(classification, matches, affectedCandidateCount),
                    "Review the conflicting passages, confirm the operative terms, and reassess the affected candidate outcomes.",
                    priority, DecisionReviewTaskStatus.Open,
                    AssignedToUserId: null, ResolvedByUserId: null, ResolvedDateUtc: null, ResolutionNotes: null,
                    tenantId, userId), cancellationToken);
                reviewTaskIds.Add(taskId);
            }

            // Persist the classification + candidate-fact summary back onto the change event.
            var summary = BuildEventSummary(classification, matches.Count, affectedCandidateCount, sourceLabel);
            var candidateFactsJson = JsonSerializer.Serialize(candidateFacts);
            await integrityRepository.UpdateChangeEventOutcomeAsync(
                tenantId, userId, eventId, classification, MatterChangeProcessingStatus.Processed,
                processingError: null, summary, candidateFactsJson, affectedPropositionCount, affectedCandidateCount, cancellationToken);

            logger.LogInformation(
                "Processed matter change {EventId} for matter {MatterId}: classification={Classification}, propositions={Props}, candidates={Cands}.",
                eventId, decisionMatterId, classification, affectedPropositionCount, affectedCandidateCount);

            return new MatterChangeProcessingResult(eventId, classification, affectedPropositionCount, affectedCandidateCount, reviewTaskIds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Matter change processing failed for event {EventId}.", eventId);
            await integrityRepository.UpdateChangeEventOutcomeAsync(
                tenantId, userId, eventId, MatterChangeClassification.PotentialImpact, MatterChangeProcessingStatus.Failed,
                ex.Message, summary: null, candidateFactsJson: null, affectedPropositionCount: 0, affectedCandidateCount: 0, CancellationToken.None);
            throw;
        }
    }

    private static IReadOnlyList<string> ExtractCandidateFacts(IReadOnlyCollection<LegalDocumentPassageDto> passages)
    {
        return passages
            .Select(p => p.Text?.Trim() ?? string.Empty)
            .Where(t => t.Length >= 24)
            .Take(200)
            .ToArray();
    }

    private static IReadOnlyList<PropositionEvidenceLinkPersistence> MatchPropositions(
        IReadOnlyList<string> candidateFacts,
        IReadOnlyCollection<PropositionEvidenceLinkPersistence> existingLinks)
    {
        if (existingLinks.Count == 0 || candidateFacts.Count == 0)
            return [];

        var factTokenSets = candidateFacts.Select(Tokenize).ToArray();
        var matched = new List<PropositionEvidenceLinkPersistence>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in existingLinks.Where(l => l.StatusCode == PropositionEvidenceLinkStatus.Active))
        {
            var propositionText = link.PropositionStatement ?? link.PropositionKey;
            var propTokens = Tokenize(propositionText);
            if (propTokens.Count == 0)
                continue;
            var isMatch = factTokenSets.Any(factTokens => JaccardOverlap(propTokens, factTokens) >= 0.18);
            if (isMatch && seen.Add(link.PropositionKey))
                matched.Add(link);
        }
        return matched;
    }

    private static string Classify(IReadOnlyList<PropositionEvidenceLinkPersistence> matches, bool hasContradictionCue, int existingLinkCount)
    {
        if (existingLinkCount == 0)
            return MatterChangeClassification.NoMaterialImpact;
        if (matches.Count == 0)
            // Material new content that matches no existing proposition may need a new/reopened branch.
            return hasContradictionCue ? MatterChangeClassification.NewMaterialFact : MatterChangeClassification.NoMaterialImpact;
        return hasContradictionCue
            ? MatterChangeClassification.MaterialContradiction
            : MatterChangeClassification.PotentialImpact;
    }

    private static bool ContainsAnyCue(string text)
    {
        var lowered = text.ToLowerInvariant();
        return ContradictionCues.Any(cue => lowered.Contains(cue, StringComparison.Ordinal));
    }

    private static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return text
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', ';', ':', '(', ')', '"', '\'', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static double JaccardOverlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;
        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static string BuildTaskTitle(string classification, string? sourceLabel) => classification switch
    {
        MatterChangeClassification.MaterialContradiction => $"New document may contradict a prior conclusion",
        MatterChangeClassification.NewMaterialFact => $"New material fact detected in '{sourceLabel ?? "document"}'",
        _ => "New document may affect a prior conclusion"
    };

    private static string BuildTaskDetail(string classification, IReadOnlyList<PropositionEvidenceLinkPersistence> matches, int affectedCandidateCount)
    {
        var propNote = matches.Count == 0
            ? "No existing proposition matched; a new or dormant decision branch may need to open."
            : $"Affected propositions: {string.Join("; ", matches.Take(5).Select(m => m.PropositionStatement ?? m.PropositionKey))}.";
        return $"{classification}. {propNote} Linked candidate outcomes potentially affected: {affectedCandidateCount}.";
    }

    private static string BuildEventSummary(string classification, int propositions, int candidates, string? sourceLabel) =>
        $"New source '{sourceLabel ?? "document"}' classified {classification}: {propositions} proposition(s), {candidates} candidate(s) potentially affected.";
}
