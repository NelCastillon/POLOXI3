using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Citator;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper reader for the Citator. Tenant-scoped, non-deleted treatments only (DB is the source of
// truth; no synthesized data). Joins treatment rows to the treatment-code vocabulary for display.
public sealed class CitatorRepository(ISqlConnectionFactory connectionFactory) : ICitatorRepository
{
    public async Task<IReadOnlyList<AuthorityTreatmentDto>> GetTreatmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> citedAuthorityIdentities,
        CancellationToken cancellationToken = default)
    {
        if (citedAuthorityIdentities.Count == 0)
            return [];

        var identities = citedAuthorityIdentities
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (identities.Length == 0)
            return [];

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rows = (await connection.QueryAsync<TreatmentRow>(new CommandDefinition(
            """
            SELECT t.CitationTreatmentId, t.CitedAuthorityIdentity, t.CitedAuthorityName,
                   t.CitingAuthorityName, t.CitingAuthorityRef, t.TreatmentCode, c.DisplayName AS TreatmentDisplayName,
                   c.PolarityCode, t.FlagLevelCode, t.TreatmentDate, t.Jurisdiction, t.SourceProviderCode, t.Notes
            FROM POLOXI.Legal_CitationTreatment t
            INNER JOIN POLOXI.Legal_CitationTreatmentCode c ON c.TreatmentCode = t.TreatmentCode
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
              AND t.CitedAuthorityIdentity IN @Identities
            ORDER BY c.SeverityRank DESC, t.TreatmentDate DESC;
            """,
            new { TenantId = tenantId, Identities = identities },
            cancellationToken: cancellationToken))).ToList();

        return rows
            .Select(r => new AuthorityTreatmentDto(
                r.CitationTreatmentId, r.CitedAuthorityIdentity, r.CitedAuthorityName,
                r.CitingAuthorityName, r.CitingAuthorityRef, r.TreatmentCode, r.TreatmentDisplayName,
                r.PolarityCode, r.FlagLevelCode,
                r.TreatmentDate is { } d ? DateOnly.FromDateTime(d) : null,
                r.Jurisdiction, r.SourceProviderCode, r.Notes))
            .ToList();
    }

    public async Task<IReadOnlyList<CitationFlagLevelDto>> GetFlagLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var levels = await connection.QueryAsync<CitationFlagLevelDto>(new CommandDefinition(
            """
            SELECT FlagLevelCode, DisplayName, ColorHex, SeverityOrder, IsDefault
            FROM POLOXI.Legal_CitationFlagLevel
            ORDER BY SeverityOrder;
            """,
            cancellationToken: cancellationToken));

        return levels.ToList();
    }

    public async Task<IReadOnlyList<CitationTreatmentCodeDto>> GetTreatmentCodesAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var codes = await connection.QueryAsync<CitationTreatmentCodeDto>(new CommandDefinition(
            """
            SELECT TreatmentCode, DisplayName, Description, PolarityCode, SeverityRank, FlagLevelCode
            FROM POLOXI.Legal_CitationTreatmentCode
            WHERE IsActive = 1
            ORDER BY SeverityRank;
            """,
            cancellationToken: cancellationToken));

        return codes.ToList();
    }

    public async Task<IReadOnlyList<AuthorityTreatmentDto>> ListTreatmentsAsync(
        Guid tenantId,
        string citedAuthorityIdentity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(citedAuthorityIdentity))
            return [];

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rows = (await connection.QueryAsync<TreatmentRow>(new CommandDefinition(
            """
            SELECT t.CitationTreatmentId, t.CitedAuthorityIdentity, t.CitedAuthorityName,
                   t.CitingAuthorityName, t.CitingAuthorityRef, t.TreatmentCode, c.DisplayName AS TreatmentDisplayName,
                   c.PolarityCode, t.FlagLevelCode, t.TreatmentDate, t.Jurisdiction, t.SourceProviderCode, t.Notes
            FROM POLOXI.Legal_CitationTreatment t
            INNER JOIN POLOXI.Legal_CitationTreatmentCode c ON c.TreatmentCode = t.TreatmentCode
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
              AND t.CitedAuthorityIdentity = @Identity
            ORDER BY t.TreatmentDate DESC, t.CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, Identity = citedAuthorityIdentity.Trim() },
            cancellationToken: cancellationToken))).ToList();

        return rows
            .Select(r => new AuthorityTreatmentDto(
                r.CitationTreatmentId, r.CitedAuthorityIdentity, r.CitedAuthorityName,
                r.CitingAuthorityName, r.CitingAuthorityRef, r.TreatmentCode, r.TreatmentDisplayName,
                r.PolarityCode, r.FlagLevelCode,
                r.TreatmentDate is { } d ? DateOnly.FromDateTime(d) : null,
                r.Jurisdiction, r.SourceProviderCode, r.Notes))
            .ToList();
    }

    public async Task<Guid> UpsertTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid? citationTreatmentId,
        string flagLevelCode,
        SaveCitationTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var treatmentDate = request.TreatmentDate is { } d ? d.ToDateTime(TimeOnly.MinValue) : (DateTime?)null;

        if (citationTreatmentId is { } existingId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_CitationTreatment
                SET CitedAuthorityIdentity = @CitedAuthorityIdentity,
                    CitedAuthorityName      = @CitedAuthorityName,
                    CitingAuthorityName     = @CitingAuthorityName,
                    CitingAuthorityRef      = @CitingAuthorityRef,
                    TreatmentCode           = @TreatmentCode,
                    FlagLevelCode           = @FlagLevelCode,
                    TreatmentDate           = @TreatmentDate,
                    Jurisdiction            = @Jurisdiction,
                    SourceProviderCode      = @SourceProviderCode,
                    Notes                   = @Notes,
                    ModifiedDateUtc         = SYSUTCDATETIME(),
                    ModifiedByUserId        = @ActorUserId
                WHERE CitationTreatmentId = @Id AND TenantId = @TenantId AND IsDeleted = 0;
                """,
                new
                {
                    Id = existingId,
                    TenantId = tenantId,
                    ActorUserId = actorUserId,
                    CitedAuthorityIdentity = request.CitedAuthorityIdentity.Trim(),
                    request.CitedAuthorityName,
                    request.CitingAuthorityName,
                    request.CitingAuthorityRef,
                    request.TreatmentCode,
                    FlagLevelCode = flagLevelCode,
                    TreatmentDate = treatmentDate,
                    request.Jurisdiction,
                    request.SourceProviderCode,
                    request.Notes
                },
                cancellationToken: cancellationToken));

            return existingId;
        }

        var newId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_CitationTreatment
                (CitationTreatmentId, CitedAuthorityIdentity, CitedAuthorityName, CitingAuthorityName,
                 CitingAuthorityRef, TreatmentCode, FlagLevelCode, TreatmentDate, Jurisdiction,
                 SourceProviderCode, Notes, TenantId, CreatedByUserId)
            VALUES
                (@Id, @CitedAuthorityIdentity, @CitedAuthorityName, @CitingAuthorityName,
                 @CitingAuthorityRef, @TreatmentCode, @FlagLevelCode, @TreatmentDate, @Jurisdiction,
                 @SourceProviderCode, @Notes, @TenantId, @ActorUserId);
            """,
            new
            {
                Id = newId,
                TenantId = tenantId,
                ActorUserId = actorUserId,
                CitedAuthorityIdentity = request.CitedAuthorityIdentity.Trim(),
                request.CitedAuthorityName,
                request.CitingAuthorityName,
                request.CitingAuthorityRef,
                request.TreatmentCode,
                FlagLevelCode = flagLevelCode,
                TreatmentDate = treatmentDate,
                request.Jurisdiction,
                request.SourceProviderCode,
                request.Notes
            },
            cancellationToken: cancellationToken));

        return newId;
    }

    public async Task<bool> SoftDeleteTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid citationTreatmentId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_CitationTreatment
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE CitationTreatmentId = @Id AND TenantId = @TenantId AND IsDeleted = 0;
            """,
            new { Id = citationTreatmentId, TenantId = tenantId, ActorUserId = actorUserId },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    private sealed record TreatmentRow(
        Guid CitationTreatmentId, string CitedAuthorityIdentity, string? CitedAuthorityName,
        string? CitingAuthorityName, string? CitingAuthorityRef, string TreatmentCode, string TreatmentDisplayName,
        string PolarityCode, string FlagLevelCode, DateTime? TreatmentDate, string? Jurisdiction,
        string? SourceProviderCode, string? Notes);
}
