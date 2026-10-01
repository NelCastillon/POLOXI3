using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// Result of a channel-ingestion pass for a matter: how many contributions each channel produced and
// how many were persisted as source-truth. No decision numbers — POLOXI Wide2 owns the outcome.
public sealed record ChannelIngestionResult(
    Guid DecisionMatterId,
    Guid? AuthoritativeHierarchyExecutionId,
    int ContributionsResolved,
    int ContributionsPersisted,
    IReadOnlyDictionary<string, int> ByChannel);

public interface IDecisionChannelOrchestrator
{
    // Resolves qualitative contributions from every registered channel against the matter's
    // authoritative hierarchy and persists them as append-only source-truth. Fail-soft per channel:
    // one channel throwing never blocks the others. Recompute/snapshot/delta remain owned by the
    // existing DecisionReevaluationService and are NOT invoked here.
    Task<ChannelIngestionResult> IngestContributionsAsync(
        Guid tenantId,
        Guid userId,
        Guid decisionMatterId,
        Guid authoritativeHierarchyExecutionId,
        CancellationToken cancellationToken = default);
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Thin orchestrator over the registered IDecisionChannel set. It is a SOURCE-TRUTH ingestion step,
// not a decision engine: it collects each channel's qualitative contributions and persists them.
// The derived node signal/state and any DecisionImpact/DecisionDelta are produced separately by the
// existing POLOXI reevaluation pipeline, which remains the sole authority for candidate competition,
// uncertainty, IV, convergence, and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionChannelOrchestrator(
    IEnumerable<IDecisionChannel> channels,
    IChannelContributionRepository contributionRepository,
    ILegalDecisionRepository decisionRepository,
    IDomainPackResolver domainPackResolver,
    ILogger<DecisionChannelOrchestrator> logger) : IDecisionChannelOrchestrator
{
    public async Task<ChannelIngestionResult> IngestContributionsAsync(
        Guid tenantId,
        Guid userId,
        Guid decisionMatterId,
        Guid authoritativeHierarchyExecutionId,
        CancellationToken cancellationToken = default)
    {
        // Resolve the matter's Domain Pack once so every channel shares the same synonym terminology and
        // evidence-type→signal map. Fail-soft: any failure leaves the pack null and channels fall back to
        // their raw-overlap / default-signal behavior — never blocking ingestion.
        ResolvedDomainPack? resolvedPack = null;
        try
        {
            var matter = await decisionRepository.GetMatterAsync(tenantId, decisionMatterId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(matter?.DomainPackCode))
                resolvedPack = await domainPackResolver.ResolveAsync(tenantId, matter.DomainPackCode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Domain Pack resolution failed for matter {MatterId}; channels run without pack semantics.", decisionMatterId);
        }

        var context = new DecisionChannelResolveContext
        {
            TenantId = tenantId,
            UserId = userId,
            DecisionMatterId = decisionMatterId,
            AuthoritativeHierarchyExecutionId = authoritativeHierarchyExecutionId,
            ResolvedPack = resolvedPack,
        };

        var byChannel = new Dictionary<string, int>(StringComparer.Ordinal);
        var toPersist = new List<ChannelContributionPersistence>();

        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var channelCode = DecisionChannelCodes.ToCode(channel.ChannelType);
            try
            {
                var contributions = await channel.ResolveContributionsAsync(context, cancellationToken);
                byChannel[channelCode] = (byChannel.TryGetValue(channelCode, out var existing) ? existing : 0) + contributions.Count;
                foreach (var contribution in contributions)
                    toPersist.Add(ToPersistence(contribution));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Fail-soft: a misbehaving channel must never block the others or the pipeline.
                logger.LogWarning(ex, "Decision channel {Channel} failed to resolve contributions for matter {MatterId}.", channelCode, decisionMatterId);
            }
        }

        await contributionRepository.SaveContributionsAsync(toPersist, cancellationToken);

        return new ChannelIngestionResult(
            decisionMatterId,
            authoritativeHierarchyExecutionId,
            toPersist.Count,
            toPersist.Count,
            byChannel);
    }

    private static ChannelContributionPersistence ToPersistence(DecisionContribution c) => new(
        ChannelContributionId: c.ContributionId,
        DecisionMatterId: c.DecisionMatterId,
        HierarchyExecutionId: c.HierarchyExecutionId,
        HierarchyNodeId: c.HierarchyNodeId,
        ChannelTypeCode: DecisionChannelCodes.ToCode(c.ChannelType),
        RelationCode: DecisionChannelCodes.ToCode(c.Relation),
        VerificationStateCode: DecisionChannelCodes.ToCode(c.VerificationState),
        TargetSignalCode: c.TargetSignalCode,
        ApplicabilityCode: c.ApplicabilityCode,
        DirectnessCode: c.DirectnessCode,
        SourceTypeCode: c.Provenance.SourceTypeCode,
        SourceId: c.Provenance.SourceId,
        SourceLabel: c.Provenance.SourceLabel,
        LegalDocumentId: c.Provenance.LegalDocumentId,
        LegalDocumentVersionId: c.Provenance.LegalDocumentVersionId,
        LegalDocumentPassageId: c.Provenance.LegalDocumentPassageId,
        ProposedByModel: c.Provenance.ProposedByModel,
        PromptRunId: c.Provenance.PromptRunId,
        VerificationReason: c.Provenance.VerificationReason,
        EffectiveFromUtc: c.EffectiveFromUtc,
        EffectiveToUtc: c.EffectiveToUtc,
        PlacementMagnitude: c.Magnitude,
        TenantId: c.TenantId,
        ActorUserId: c.ActorUserId);
}
