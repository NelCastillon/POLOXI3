using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-2.5 domain ↔ persistence mapping.
//
// Round-trips the rich domain records (ClaimProposition, ClaimSupportRef, ClaimVerificationChangedEvent)
// to and from the flat, string-coded persistence rows. All enum ↔ code translation is delegated to
// ClaimCodes so persistence and domain never drift. Support edges are split into supporting vs
// contradicting on rehydrate (Contradicts → contradicting; everything else → supporting) so the
// authority gate can recompute its negative signal after a reload.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class ClaimPersistenceMapper
{
    public static ClaimPropositionPersistence ToPersistence(ClaimProposition claim, Guid tenantId, Guid? actorUserId) => new(
        claim.ClaimId,
        claim.SessionId,
        claim.MatterId,
        claim.Text,
        claim.NormalizedText,
        ClaimCodes.ToCode(claim.ClaimType),
        ClaimCodes.ToCode(claim.Origin),
        ClaimCodes.ToCode(claim.VerificationState),
        ClaimCodes.ToCode(claim.DecisionAuthority),
        claim.VerificationStrength,
        claim.Materiality,
        claim.DecisionImpact,
        claim.Discrimination,
        claim.Uncertainty,
        claim.IsEssential,
        claim.SourceBranchId,
        claim.SourceCandidateId,
        claim.ProposedByModel,
        claim.PromptRunId,
        claim.VerificationReason,
        claim.Version,
        tenantId,
        actorUserId);

    // Rehydrates a proposition from its persisted row and (optionally) its support edges.
    public static ClaimProposition ToDomain(
        ClaimPropositionPersistence row,
        IReadOnlyList<ClaimSupportPersistence>? support = null)
    {
        var edges = (support ?? []).Select(ToDomain).ToArray();

        return new ClaimProposition
        {
            ClaimId = row.ClaimId,
            SessionId = row.DecisionSessionId,
            MatterId = row.MatterId,
            Text = row.Text,
            NormalizedText = row.NormalizedText,
            ClaimType = ClaimCodes.ParseClaimType(row.ClaimTypeCode),
            Origin = ClaimCodes.ParseClaimOrigin(row.ClaimOriginCode),
            VerificationState = ClaimCodes.ParseVerificationState(row.VerificationStateCode),
            DecisionAuthority = ClaimCodes.ParseDecisionAuthority(row.DecisionAuthorityCode),
            VerificationStrength = row.VerificationStrength,
            Materiality = row.Materiality,
            DecisionImpact = row.DecisionImpact,
            Discrimination = row.Discrimination,
            Uncertainty = row.Uncertainty,
            IsEssential = row.IsEssential,
            SourceBranchId = row.SourceBranchId,
            SourceCandidateId = row.SourceCandidateId,
            SupportingEvidence = edges
                .Where(e => e.Relationship != ClaimSupportRelationship.Contradicts)
                .ToArray(),
            ContradictingEvidence = edges
                .Where(e => e.Relationship == ClaimSupportRelationship.Contradicts)
                .ToArray(),
            VerificationReason = row.VerificationReason,
            ProposedByModel = row.ProposedByModel,
            PromptRunId = row.PromptRunId,
            Version = row.Version,
        };
    }

    public static ClaimSupportPersistence ToPersistence(ClaimSupportRef support, Guid tenantId, Guid? actorUserId) => new(
        support.SupportId,
        support.ClaimId,
        support.EvidenceId,
        support.AuthorityId,
        ClaimCodes.ToCode(support.Relationship),
        support.Strength,
        support.IndependentlyVerified,
        support.SourceLocation,
        support.VerificationReason,
        tenantId,
        actorUserId);

    public static ClaimSupportRef ToDomain(ClaimSupportPersistence row) => new()
    {
        SupportId = row.ClaimSupportId,
        ClaimId = row.ClaimId,
        EvidenceId = row.EvidenceId,
        AuthorityId = row.AuthorityId,
        Relationship = ClaimCodes.ParseSupportRelationship(row.RelationshipCode),
        Strength = row.Strength,
        IndependentlyVerified = row.IndependentlyVerified,
        SourceLocation = row.SourceLocation,
        VerificationReason = row.VerificationReason,
    };

    public static ClaimVerificationEventPersistence ToPersistence(
        ClaimVerificationChangedEvent changed,
        Guid tenantId,
        Guid? actorUserId) => new(
        changed.EventId,
        changed.ClaimId,
        ClaimCodes.ToCode(changed.PreviousState),
        ClaimCodes.ToCode(changed.NewState),
        changed.Reason,
        changed.EvidenceIds.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(changed.EvidenceIds),
        changed.AuthorityIds.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(changed.AuthorityIds),
        changed.IdempotencyKey,
        changed.OccurredAt,
        tenantId,
        actorUserId);
}
