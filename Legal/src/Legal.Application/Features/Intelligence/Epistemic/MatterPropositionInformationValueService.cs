using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — matter-proposition Information Value (advisory overlay).
//
// Scores the atomic matter fact-propositions of a matter on POLOXI's single VIV scale by projecting
// each into a ClaimProposition and running it through the EXISTING ClaimVerificationPrioritizer with
// the DB-backed EpistemicAuthoritySettings thresholds. This is advisory/display-only: it never blocks
// the authoritative decision, never touches the branch-target information-round loop, and — like the
// rest of the semantic layer — degrades gracefully if it fails.
//
// Derived signals are persisted into POLOXI.Legal_ClaimProposition (via IEpistemicClaimRepository) so
// the scored propositions have a durable, auditable home alongside the branch/candidate claims.
// ─────────────────────────────────────────────────────────────────────────────────────────────

// One scored proposition on the shared VIV scale, ready for cockpit display.
public sealed record MatterPropositionInformationValue(
    Guid PropositionId,
    string PropositionText,
    string FactStateCode,
    string VerificationStateCode,
    decimal InformationValue,
    bool IsEssential,
    bool IsExecutable,
    decimal Materiality,
    decimal Uncertainty,
    decimal DecisionImpact,
    decimal Discrimination,
    decimal RedundancyPenalty,
    // v4.0 advisory §9 Proposition Integrity Gate overlay. IntegrityDisposition is null when the gate is
    // disabled or the proposition could not be evaluated; structural/evidence eligibility default to false.
    string? IntegrityDisposition = null,
    bool StructurallyEligible = false,
    bool EvidenceEligible = false);

public sealed record MatterPropositionInformationValueResult(
    Guid MatterId,
    IReadOnlyList<MatterPropositionInformationValue> Propositions,
    bool VerificationExhausted);

public interface IMatterPropositionInformationValueService
{
    Task<MatterPropositionInformationValueResult> ScoreAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        bool persist = true,
        CancellationToken cancellationToken = default);
}

public sealed class MatterPropositionInformationValueService(
    ILegalDocumentCorpusRepository corpusRepository,
    IIntelligenceWideRepository intelligenceWideRepository,
    IClaimVerificationPrioritizer prioritizer,
    IEpistemicClaimRepository claimRepository) : IMatterPropositionInformationValueService
{
    public async Task<MatterPropositionInformationValueResult> ScoreAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        var graph = await corpusRepository.GetMatterEvidenceGraphAsync(tenantId, matterId, cancellationToken);
        var settings = await intelligenceWideRepository.ResolveEpistemicSettingsAsync(tenantId, cancellationToken);

        // Duplicate-source detection input: map each evidence item to its originating source document so
        // the projection can tell independent corroboration from the same source echoed many times.
        var evidenceDocumentSource = (graph.Evidence ?? [])
            .GroupBy(e => e.LegalEvidenceItemId)
            .ToDictionary(g => g.Key, g => g.First().LegalDocumentId);

        // Project every matter proposition into the authoritative ClaimProposition shape so the existing
        // prioritizer scores it on the same VIV scale used for branch/candidate verification actions.
        var signals = (graph.Propositions ?? [])
            .Select(p => MatterPropositionClaimProjection.ToSignalInput(p, evidenceDocumentSource))
            .ToArray();
        var redundancyById = signals.ToDictionary(s => s.PropositionId, s => s.RedundancyPenalty);
        var claims = signals
            .Select(signal => MatterPropositionClaimProjection.Project(signal, decisionSessionId))
            .ToArray();

        // The prioritizer already applies the MinimumVerificationIV threshold and per-round cap; use it
        // to flag which propositions are worth investigating this round (executable + above the bar).
        var prioritized = prioritizer
            .Prioritize(claims, settings)
            .ToDictionary(action => action.ClaimId);

        // v4.0 advisory overlay: run the §9 Proposition Integrity Gate (APR + MECE + Light Evidence Graph)
        // when enabled. Display-only — it never changes IV or blocks the decision, and fails soft.
        var integrityById = new Dictionary<Guid, MatterPropositionIntegrityResult>();
        if (settings.UsePropositionIntegrityGate)
        {
            try
            {
                foreach (var integrity in LegalPropositionIntegrityBridge.Evaluate(graph.Propositions, graph.Evidence, evidenceDocumentSource))
                    integrityById[integrity.PropositionId] = integrity;
            }
            catch
            {
                // Advisory overlay must never break scoring; leave the overlay empty on failure.
                integrityById.Clear();
            }
        }

        var scored = claims
            .Select(claim =>
            {
                var iv = prioritizer.ComputeInformationValue(claim);
                var hasIntegrity = integrityById.TryGetValue(claim.ClaimId, out var integrity);
                return new MatterPropositionInformationValue(
                    claim.ClaimId,
                    claim.Text,
                    ResolveFactStateCode(graph.Propositions, claim.ClaimId),
                    ClaimCodes.ToCode(claim.VerificationState),
                    iv,
                    claim.IsEssential,
                    prioritized.ContainsKey(claim.ClaimId),
                    claim.Materiality,
                    claim.Uncertainty,
                    claim.DecisionImpact,
                    claim.Discrimination,
                    redundancyById.TryGetValue(claim.ClaimId, out var rp) ? rp : 0m,
                    hasIntegrity ? integrity!.Disposition.ToString() : null,
                    hasIntegrity && integrity!.StructuralEligible,
                    hasIntegrity && integrity!.EvidenceEligible);
            })
            .OrderByDescending(p => p.InformationValue)
            .ThenBy(p => p.PropositionId)
            .ToArray();

        if (persist)
        {
            foreach (var claim in claims)
            {
                await claimRepository.UpsertClaimAsync(
                    ClaimPersistenceMapper.ToPersistence(claim, tenantId, actorUserId),
                    cancellationToken);
            }
        }

        var exhausted = prioritizer.IsVerificationExhausted(claims, settings);
        return new MatterPropositionInformationValueResult(matterId, scored, exhausted);
    }

    private static string ResolveFactStateCode(
        IReadOnlyCollection<LegalEvidenceGraphPropositionDto>? propositions,
        Guid propositionId)
        => propositions?.FirstOrDefault(p => p.LegalFactPropositionId == propositionId)?.FactStateCode
           ?? string.Empty;
}
