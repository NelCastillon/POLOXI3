using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application;

// Per-matter Atomic Propositions (migration 0442). Parent targets are validated against the matter's
// Domain Pack catalog (Evidence Types + Concepts); a proposition can never be attached to an unknown parent.
public sealed partial class LegalDecisionService
{
    public Task<IReadOnlyCollection<MatterAtomicPropositionDto>> GetMatterAtomicPropositionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterAtomicPropositionsAsync(tenantId, decisionMatterId, cancellationToken);

    public async Task<MatterAtomicPropositionOptionsDto> GetMatterAtomicPropositionOptionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        var (packCode, pack) = await ResolveMatterPackAsync(tenantId, decisionMatterId, cancellationToken);
        var statuses = await repository.GetMatterAtomicPropositionStatusesAsync(tenantId, cancellationToken);
        var deciders = await repository.GetMatterAtomicPropositionDecidersAsync(tenantId, cancellationToken);
        var parents = new List<MatterEvidenceScopeTargetOptionDto>();
        if (pack is not null)
        {
            parents.AddRange(pack.EvidenceTypes.Select(e => new MatterEvidenceScopeTargetOptionDto("EVIDENCE_TYPE", e.EvidenceTypeCode, e.DimensionCode ?? string.Empty, string.IsNullOrWhiteSpace(e.Name) ? e.EvidenceTypeCode : e.Name, e.Description, e.IsRequiredCoverage)));
            parents.AddRange(pack.Concepts.Select(c => new MatterEvidenceScopeTargetOptionDto("CONCEPT", c.ConceptCode, c.DimensionCode, string.IsNullOrWhiteSpace(c.Name) ? c.ConceptCode : c.Name, c.Description, c.IsRequiredCoverage)));
        }
        return new MatterAtomicPropositionOptionsDto
        {
            PackCode = packCode,
            Statuses = statuses,
            Deciders = deciders,
            Parents = parents.OrderBy(t => t.DimensionCode).ThenBy(t => t.DisplayName).ToArray()
        };
    }

    public async Task<Guid> UpsertMatterAtomicPropositionAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterAtomicPropositionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePropositionStatusAsync(tenantId, request.ScopeStatusCode, cancellationToken);
        var (packCode, pack) = await ResolveMatterPackAsync(tenantId, decisionMatterId, cancellationToken);
        if (pack is null || string.IsNullOrWhiteSpace(packCode))
            throw new InvalidOperationException("The matter has no resolvable Domain Pack; atomic propositions cannot be edited.");

        string parentKind, parentCode, parentName, dimension; int sortOrder = 0;
        if (string.Equals(request.ParentTargetKindCode, "EVIDENCE_TYPE", StringComparison.OrdinalIgnoreCase))
        {
            var e = pack.EvidenceTypes.FirstOrDefault(x => string.Equals(x.EvidenceTypeCode, request.ParentTargetCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Evidence type '{request.ParentTargetCode}' is not part of Domain Pack '{packCode}'.");
            parentKind = "EVIDENCE_TYPE"; parentCode = e.EvidenceTypeCode; parentName = string.IsNullOrWhiteSpace(e.Name) ? e.EvidenceTypeCode : e.Name; dimension = e.DimensionCode ?? string.Empty;
        }
        else if (string.Equals(request.ParentTargetKindCode, "CONCEPT", StringComparison.OrdinalIgnoreCase))
        {
            var c = pack.Concepts.FirstOrDefault(x => string.Equals(x.ConceptCode, request.ParentTargetCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Concept '{request.ParentTargetCode}' is not part of Domain Pack '{packCode}'.");
            parentKind = "CONCEPT"; parentCode = c.ConceptCode; parentName = string.IsNullOrWhiteSpace(c.Name) ? c.ConceptCode : c.Name; dimension = c.DimensionCode; sortOrder = c.SortOrder;
        }
        else
            throw new InvalidOperationException($"Unsupported parent kind '{request.ParentTargetKindCode}'. Expected EVIDENCE_TYPE or CONCEPT.");

        var code = NormalizePropositionCode(request.PropositionCode);
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Proposition code is required.");

        var row = new MatterAtomicPropositionEvaluation(packCode, parentKind, parentCode, parentName, dimension, null, code, 3,
            request.DisplayName.Trim(), request.Statement.Trim(), null, null, Math.Clamp(request.Confidence, 0m, 1m),
            string.IsNullOrWhiteSpace(request.SemanticTypeCode) ? null : request.SemanticTypeCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(request.BranchRoleCode) ? null : request.BranchRoleCode.Trim().ToUpperInvariant(),
            false, null, null, null, 0, null, "Attorney", 0, 1, null, null, null, null, null, null, null, Guid.Empty, Guid.Empty, sortOrder);
        return await repository.UpsertUserMatterAtomicPropositionAsync(tenantId, userId, decisionMatterId, row, request.ScopeStatusCode.Trim().ToUpperInvariant(), request.UserNote, cancellationToken);
    }

    public async Task<int> SetMatterAtomicPropositionStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterAtomicPropositionBulkStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePropositionStatusAsync(tenantId, request.ScopeStatusCode, cancellationToken);
        return await repository.SetMatterAtomicPropositionStatusAsync(tenantId, userId, decisionMatterId, request.MatterAtomicPropositionIds, request.ScopeStatusCode.Trim().ToUpperInvariant(), request.UserNote, cancellationToken);
    }

    public Task<bool> DeleteMatterAtomicPropositionAsync(Guid tenantId, Guid userId, Guid decisionMatterId, Guid matterAtomicPropositionId, CancellationToken cancellationToken = default)
        => repository.SoftDeleteMatterAtomicPropositionAsync(tenantId, userId, decisionMatterId, matterAtomicPropositionId, cancellationToken);

    private async Task EnsurePropositionStatusAsync(Guid tenantId, string statusCode, CancellationToken cancellationToken)
    {
        var statuses = await repository.GetMatterAtomicPropositionStatusesAsync(tenantId, cancellationToken);
        if (!statuses.Any(s => string.Equals(s.StatusCode, statusCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unknown atomic proposition status '{statusCode}'.");
    }

    private static string NormalizePropositionCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var chars = code.Trim().ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
