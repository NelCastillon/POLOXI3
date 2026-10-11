using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Citator;

namespace Legal.Application.Features.Intelligence.Citator;

// Computes a good-law validity flag for each requested authority: the worst flag (highest severity)
// among its recorded treatments, using the DB-backed flag-level vocabulary. Authorities with no
// citator data resolve to the "Unknown" flag. Advisory only — never blocks a decision.
public sealed class CitatorService(ICitatorRepository repository) : ICitatorService
{
    private const string UnknownFlagCode = "Unknown";

    public async Task<IReadOnlyList<AuthorityValidityDto>> GetValidityAsync(
        Guid tenantId,
        IReadOnlyCollection<string> authorityIdentities,
        CancellationToken cancellationToken = default)
    {
        var identities = authorityIdentities
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (identities.Length == 0)
            return [];

        var flagLevels = await repository.GetFlagLevelsAsync(cancellationToken);
        var flagByCode = flagLevels.ToDictionary(f => f.FlagLevelCode, StringComparer.OrdinalIgnoreCase);

        var treatments = await repository.GetTreatmentsAsync(tenantId, identities, cancellationToken);
        var treatmentsByIdentity = treatments
            .GroupBy(t => t.CitedAuthorityIdentity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AuthorityTreatmentDto>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<AuthorityValidityDto>(identities.Length);
        foreach (var identity in identities)
        {
            if (!treatmentsByIdentity.TryGetValue(identity, out var rows) || rows.Count == 0)
            {
                results.Add(BuildValidity(identity, UnknownFlagCode, flagByCode, hasNegative: false, count: 0, []));
                continue;
            }

            // Worst flag = highest SeverityOrder among the treatments' implied flags.
            var worstCode = rows
                .Select(r => r.FlagLevelCode)
                .OrderByDescending(code => flagByCode.TryGetValue(code, out var f) ? f.SeverityOrder : int.MaxValue)
                .First();

            var hasNegative = rows.Any(r =>
                string.Equals(r.PolarityCode, "Negative", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.PolarityCode, "Fatal", StringComparison.OrdinalIgnoreCase));

            results.Add(BuildValidity(identity, worstCode, flagByCode, hasNegative, rows.Count, rows));
        }

        return results;
    }

    private static AuthorityValidityDto BuildValidity(
        string identity, string flagCode,
        IReadOnlyDictionary<string, CitationFlagLevelDto> flagByCode,
        bool hasNegative, int count, IReadOnlyList<AuthorityTreatmentDto> treatments)
    {
        var display = flagByCode.TryGetValue(flagCode, out var flag) ? flag.DisplayName : flagCode;
        var color = flag?.ColorHex ?? "#6c757d";
        return new AuthorityValidityDto(identity, flagCode, display, color, hasNegative, count, treatments);
    }

    public Task<IReadOnlyList<CitationTreatmentCodeDto>> GetTreatmentCodesAsync(
        CancellationToken cancellationToken = default)
        => repository.GetTreatmentCodesAsync(cancellationToken);

    public Task<IReadOnlyList<AuthorityTreatmentDto>> ListTreatmentsAsync(
        Guid tenantId,
        string citedAuthorityIdentity,
        CancellationToken cancellationToken = default)
        => repository.ListTreatmentsAsync(tenantId, citedAuthorityIdentity, cancellationToken);

    public async Task<Guid> SaveTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveCitationTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CitedAuthorityIdentity))
            throw new ArgumentException("A cited authority identity is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TreatmentCode))
            throw new ArgumentException("A treatment code is required.", nameof(request));

        // Validate the treatment code against the DB vocabulary and derive its implied flag.
        var codes = await repository.GetTreatmentCodesAsync(cancellationToken);
        var code = codes.FirstOrDefault(c =>
            string.Equals(c.TreatmentCode, request.TreatmentCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Unknown or inactive treatment code '{request.TreatmentCode}'.", nameof(request));

        return await repository.UpsertTreatmentAsync(
            tenantId, actorUserId, request.CitationTreatmentId, code.FlagLevelCode, request, cancellationToken);
    }

    public Task<bool> DeleteTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid citationTreatmentId,
        CancellationToken cancellationToken = default)
        => repository.SoftDeleteTreatmentAsync(tenantId, actorUserId, citationTreatmentId, cancellationToken);
}
