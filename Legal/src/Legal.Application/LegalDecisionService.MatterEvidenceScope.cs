using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application;

// Per-matter Evidence Type scope (migration 0441). Every target written here is validated against the
// matter's Domain Pack catalog (Evidence Types + Concepts); nothing outside the catalog can be inserted.
public sealed partial class LegalDecisionService
{
    public Task<IReadOnlyCollection<MatterEvidenceScopeDto>> GetMatterEvidenceScopesAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterEvidenceScopesAsync(tenantId, decisionMatterId, cancellationToken);

    public async Task<MatterEvidenceScopeOptionsDto> GetMatterEvidenceScopeOptionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        var (packCode, pack) = await ResolveMatterPackAsync(tenantId, decisionMatterId, cancellationToken);
        var statuses = await repository.GetMatterEvidenceScopeStatusesAsync(tenantId, cancellationToken);
        var deciders = await repository.GetMatterEvidenceScopeDecidersAsync(tenantId, cancellationToken);
        var targets = new List<MatterEvidenceScopeTargetOptionDto>();
        if (pack is not null)
        {
            targets.AddRange(pack.EvidenceTypes.Select(e => new MatterEvidenceScopeTargetOptionDto("EVIDENCE_TYPE", e.EvidenceTypeCode, e.DimensionCode ?? string.Empty, string.IsNullOrWhiteSpace(e.Name) ? e.EvidenceTypeCode : e.Name, e.Description, e.IsRequiredCoverage)));
            targets.AddRange(pack.Concepts.Select(c => new MatterEvidenceScopeTargetOptionDto("CONCEPT", c.ConceptCode, c.DimensionCode, string.IsNullOrWhiteSpace(c.Name) ? c.ConceptCode : c.Name, c.Description, c.IsRequiredCoverage)));
        }
        return new MatterEvidenceScopeOptionsDto
        {
            PackCode = packCode,
            Statuses = statuses,
            Deciders = deciders,
            Targets = targets.OrderBy(t => t.DimensionCode).ThenBy(t => t.DisplayName).ToArray()
        };
    }

    public async Task<Guid> UpsertMatterEvidenceScopeAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterEvidenceScopeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureScopeStatusAsync(tenantId, request.ScopeStatusCode, cancellationToken);
        var (packCode, pack) = await ResolveMatterPackAsync(tenantId, decisionMatterId, cancellationToken);
        if (pack is null || string.IsNullOrWhiteSpace(packCode))
            throw new InvalidOperationException("The matter has no resolvable Domain Pack; evidence scope cannot be edited.");

        MatterEvidenceScopeEvaluation row;
        if (string.Equals(request.TargetKindCode, "EVIDENCE_TYPE", StringComparison.OrdinalIgnoreCase))
        {
            var e = pack.EvidenceTypes.FirstOrDefault(x => string.Equals(x.EvidenceTypeCode, request.TargetCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Evidence type '{request.TargetCode}' is not part of Domain Pack '{packCode}'.");
            row = new MatterEvidenceScopeEvaluation(packCode, "EVIDENCE_TYPE", e.EvidenceTypeCode, e.DimensionCode ?? string.Empty, string.IsNullOrWhiteSpace(e.Name) ? e.EvidenceTypeCode : e.Name,
                request.ScopeStatusCode, MatterEvidenceScopeDeciderCodes.User, null, null, null, null, null, null, e.IsRequiredCoverage, false, false, 0, null, 0);
        }
        else if (string.Equals(request.TargetKindCode, "CONCEPT", StringComparison.OrdinalIgnoreCase))
        {
            var c = pack.Concepts.FirstOrDefault(x => string.Equals(x.ConceptCode, request.TargetCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Concept '{request.TargetCode}' is not part of Domain Pack '{packCode}'.");
            row = new MatterEvidenceScopeEvaluation(packCode, "CONCEPT", c.ConceptCode, c.DimensionCode, string.IsNullOrWhiteSpace(c.Name) ? c.ConceptCode : c.Name,
                request.ScopeStatusCode, MatterEvidenceScopeDeciderCodes.User, null, null, null, null, null, null, c.IsRequiredCoverage, false, false, 0, null, c.SortOrder);
        }
        else
            throw new InvalidOperationException($"Unsupported target kind '{request.TargetKindCode}'. Expected EVIDENCE_TYPE or CONCEPT.");

        return await repository.UpsertUserMatterEvidenceScopeAsync(tenantId, userId, decisionMatterId, row, request.UserNote, cancellationToken);
    }

    public async Task<int> SetMatterEvidenceScopeStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterEvidenceScopeBulkStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureScopeStatusAsync(tenantId, request.ScopeStatusCode, cancellationToken);
        return await repository.SetMatterEvidenceScopeStatusAsync(tenantId, userId, decisionMatterId, request.MatterEvidenceScopeIds, request.ScopeStatusCode, request.UserNote, cancellationToken);
    }

    public Task<bool> DeleteMatterEvidenceScopeAsync(Guid tenantId, Guid userId, Guid decisionMatterId, Guid matterEvidenceScopeId, CancellationToken cancellationToken = default)
        => repository.SoftDeleteMatterEvidenceScopeAsync(tenantId, userId, decisionMatterId, matterEvidenceScopeId, cancellationToken);

    private async Task EnsureScopeStatusAsync(Guid tenantId, string statusCode, CancellationToken cancellationToken)
    {
        var statuses = await repository.GetMatterEvidenceScopeStatusesAsync(tenantId, cancellationToken);
        if (!statuses.Any(s => string.Equals(s.StatusCode, statusCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unknown evidence scope status '{statusCode}'.");
    }

    // Same resolution the Wide2 pipeline uses: matter.DomainPackCode, else PERSONAL_INJURY fallback.
    private async Task<(string? PackCode, DecisionDomainPackDto? Pack)> ResolveMatterPackAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken)
    {
        var matter = await repository.GetMatterAsync(tenantId, decisionMatterId, cancellationToken)
            ?? throw new InvalidOperationException("Matter not found for this tenant.");
        var packCode = string.IsNullOrWhiteSpace(matter.DomainPackCode) ? DecisionDomainPackCodes.PersonalInjury : matter.DomainPackCode;
        var pack = await repository.GetDomainPackAsync(tenantId, packCode, cancellationToken);
        if (pack is null && !string.Equals(packCode, DecisionDomainPackCodes.PersonalInjury, StringComparison.OrdinalIgnoreCase))
        {
            packCode = DecisionDomainPackCodes.PersonalInjury;
            pack = await repository.GetDomainPackAsync(tenantId, packCode, cancellationToken);
        }
        return (pack?.PackCode ?? packCode, pack);
    }
}
