using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.ProviderPortal;

namespace Legal.Infrastructure.Persistence.Repositories;

public sealed class ProviderPortalRepository(ISqlConnectionFactory connectionFactory) : IProviderPortalRepository
{
    // ── Sharing policy ──────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<ProviderSharingPolicyDto>> GetSharingPoliciesAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SharingPolicyRow>(new CommandDefinition(
            SharingPolicySelect + " WHERE TenantId=@TenantId AND MatterId=@MatterId AND IsDeleted=0 ORDER BY ProviderDisplayName;",
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<ProviderSharingPolicyDto?> GetSharingPolicyAsync(
        Guid tenantId, Guid matterId, string providerKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SharingPolicyRow>(new CommandDefinition(
            SharingPolicySelect + " WHERE TenantId=@TenantId AND MatterId=@MatterId AND ProviderKey=@ProviderKey AND IsDeleted=0;",
            new { TenantId = tenantId, MatterId = matterId, ProviderKey = providerKey },
            cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<ProviderSharingPolicyDto> UpsertSharingPolicyAsync(
        Guid tenantId, Guid userId, SaveProviderSharingPolicyRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE POLOXI.Legal_ProviderSharingPolicy AS target
            USING (SELECT @TenantId AS TenantId, @MatterId AS MatterId, @ProviderKey AS ProviderKey) AS src
            ON target.TenantId = src.TenantId AND target.MatterId = src.MatterId
               AND target.ProviderKey = src.ProviderKey AND target.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET
                ProviderDisplayName = @ProviderDisplayName,
                PortalEnabled = @PortalEnabled,
                ShareMatterStatus = @ShareMatterStatus,
                ShareCurrentStage = @ShareCurrentStage,
                SharePatientTreatment = @SharePatientTreatment,
                ShareOwnRecords = @ShareOwnRecords,
                ShareOwnBills = @ShareOwnBills,
                ShareFirmRequests = @ShareFirmRequests,
                ShareOtherProviders = @ShareOtherProviders,
                ShareSettlementInfo = @ShareSettlementInfo,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHEN NOT MATCHED THEN INSERT
                (TenantId, MatterId, ProviderKey, ProviderDisplayName, PortalEnabled,
                 ShareMatterStatus, ShareCurrentStage, SharePatientTreatment, ShareOwnRecords,
                 ShareOwnBills, ShareFirmRequests, ShareOtherProviders, ShareSettlementInfo,
                 CreatedByUserId)
                VALUES
                (@TenantId, @MatterId, @ProviderKey, @ProviderDisplayName, @PortalEnabled,
                 @ShareMatterStatus, @ShareCurrentStage, @SharePatientTreatment, @ShareOwnRecords,
                 @ShareOwnBills, @ShareFirmRequests, @ShareOtherProviders, @ShareSettlementInfo,
                 @UserId);
            """,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                request.MatterId,
                request.ProviderKey,
                request.ProviderDisplayName,
                request.PortalEnabled,
                request.ShareMatterStatus,
                request.ShareCurrentStage,
                request.SharePatientTreatment,
                request.ShareOwnRecords,
                request.ShareOwnBills,
                request.ShareFirmRequests,
                request.ShareOtherProviders,
                request.ShareSettlementInfo
            },
            cancellationToken: cancellationToken));

        return (await GetSharingPolicyAsync(tenantId, request.MatterId, request.ProviderKey, cancellationToken))!;
    }

    // ── Firm → provider requests ────────────────────────────────────────────

    public async Task<IReadOnlyCollection<ProviderRequestDto>> GetRequestsAsync(
        Guid tenantId, Guid matterId, string? providerKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProviderRequestRow>(new CommandDefinition(
            ProviderRequestSelect +
            " WHERE TenantId=@TenantId AND MatterId=@MatterId AND IsDeleted=0" +
            " AND (@ProviderKey IS NULL OR ProviderKey=@ProviderKey) ORDER BY RequestedDateUtc DESC;",
            new { TenantId = tenantId, MatterId = matterId, ProviderKey = providerKey },
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<ProviderRequestDto> CreateRequestAsync(
        Guid tenantId, Guid userId, CreateProviderRequestRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT POLOXI.Legal_ProviderRequest
                (ProviderRequestId, TenantId, MatterId, ProviderKey, Title, Detail, RequestKind, CreatedByUserId)
            VALUES
                (@Id, @TenantId, @MatterId, @ProviderKey, @Title, @Detail, @RequestKind, @UserId);
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                UserId = userId,
                request.MatterId,
                request.ProviderKey,
                request.Title,
                request.Detail,
                RequestKind = string.IsNullOrWhiteSpace(request.RequestKind) ? "DOCUMENT" : request.RequestKind
            },
            cancellationToken: cancellationToken));

        var row = await connection.QuerySingleAsync<ProviderRequestRow>(new CommandDefinition(
            ProviderRequestSelect + " WHERE ProviderRequestId=@Id;",
            new { Id = id },
            cancellationToken: cancellationToken));
        return Map(row);
    }

    public async Task<ProviderRequestDto?> UpdateRequestStatusAsync(
        Guid tenantId, Guid userId, UpdateProviderRequestStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_ProviderRequest
            SET StatusCode = @StatusCode,
                FulfilledDateUtc = CASE WHEN @StatusCode = N'FULFILLED' THEN SYSUTCDATETIME() ELSE FulfilledDateUtc END,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHERE ProviderRequestId = @Id AND TenantId = @TenantId AND IsDeleted = 0;
            """,
            new { Id = request.ProviderRequestId, request.StatusCode, TenantId = tenantId, UserId = userId },
            cancellationToken: cancellationToken));
        if (affected == 0) return null;

        var row = await connection.QuerySingleAsync<ProviderRequestRow>(new CommandDefinition(
            ProviderRequestSelect + " WHERE ProviderRequestId=@Id;",
            new { Id = request.ProviderRequestId },
            cancellationToken: cancellationToken));
        return Map(row);
    }

    // ── Review state ────────────────────────────────────────────────────────

    public async Task<MatterReviewStateDto?> GetReviewStateAsync(
        Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ReviewStateRow>(new CommandDefinition(
            "SELECT MatterId, UserId, LastOpenedUtc, PreviousOpenedUtc FROM POLOXI.Legal_MatterReviewState" +
            " WHERE TenantId=@TenantId AND UserId=@UserId AND MatterId=@MatterId AND IsDeleted=0;",
            new { TenantId = tenantId, UserId = userId, MatterId = matterId },
            cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<MatterReviewStateDto?> RecordMatterOpenedAsync(
        Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Capture prior boundary before advancing it.
        var prior = await connection.QuerySingleOrDefaultAsync<ReviewStateRow>(new CommandDefinition(
            "SELECT MatterId, UserId, LastOpenedUtc, PreviousOpenedUtc FROM POLOXI.Legal_MatterReviewState" +
            " WHERE TenantId=@TenantId AND UserId=@UserId AND MatterId=@MatterId AND IsDeleted=0;",
            new { TenantId = tenantId, UserId = userId, MatterId = matterId },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE POLOXI.Legal_MatterReviewState AS target
            USING (SELECT @TenantId AS TenantId, @MatterId AS MatterId, @UserId AS UserId) AS src
            ON target.TenantId = src.TenantId AND target.MatterId = src.MatterId
               AND target.UserId = src.UserId AND target.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET
                PreviousOpenedUtc = target.LastOpenedUtc,
                LastOpenedUtc = SYSUTCDATETIME(),
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHEN NOT MATCHED THEN INSERT
                (TenantId, MatterId, UserId, LastOpenedUtc, PreviousOpenedUtc, CreatedByUserId)
                VALUES (@TenantId, @MatterId, @UserId, SYSUTCDATETIME(), NULL, @UserId);
            """,
            new { TenantId = tenantId, MatterId = matterId, UserId = userId },
            cancellationToken: cancellationToken));

        return prior is null ? null : Map(prior);
    }

    // ── Mapping ─────────────────────────────────────────────────────────────

    private const string SharingPolicySelect =
        "SELECT ProviderSharingPolicyId, MatterId, ProviderKey, ProviderDisplayName, PortalEnabled," +
        " ShareMatterStatus, ShareCurrentStage, SharePatientTreatment, ShareOwnRecords, ShareOwnBills," +
        " ShareFirmRequests, ShareOtherProviders, ShareSettlementInfo FROM POLOXI.Legal_ProviderSharingPolicy";

    private const string ProviderRequestSelect =
        "SELECT ProviderRequestId, MatterId, ProviderKey, Title, Detail, RequestKind, StatusCode," +
        " RequestedDateUtc, FulfilledDateUtc FROM POLOXI.Legal_ProviderRequest";

    private static ProviderSharingPolicyDto Map(SharingPolicyRow r) => new(
        r.ProviderSharingPolicyId, r.MatterId, r.ProviderKey, r.ProviderDisplayName, r.PortalEnabled,
        r.ShareMatterStatus, r.ShareCurrentStage, r.SharePatientTreatment, r.ShareOwnRecords, r.ShareOwnBills,
        r.ShareFirmRequests, r.ShareOtherProviders, r.ShareSettlementInfo);

    private static ProviderRequestDto Map(ProviderRequestRow r) => new(
        r.ProviderRequestId, r.MatterId, r.ProviderKey, r.Title, r.Detail, r.RequestKind, r.StatusCode,
        new DateTimeOffset(DateTime.SpecifyKind(r.RequestedDateUtc, DateTimeKind.Utc)),
        r.FulfilledDateUtc is { } f ? new DateTimeOffset(DateTime.SpecifyKind(f, DateTimeKind.Utc)) : null);

    private static MatterReviewStateDto Map(ReviewStateRow r) => new(
        r.MatterId, r.UserId,
        new DateTimeOffset(DateTime.SpecifyKind(r.LastOpenedUtc, DateTimeKind.Utc)),
        r.PreviousOpenedUtc is { } p ? new DateTimeOffset(DateTime.SpecifyKind(p, DateTimeKind.Utc)) : null);

    private sealed record SharingPolicyRow(
        Guid ProviderSharingPolicyId, Guid MatterId, string ProviderKey, string? ProviderDisplayName,
        bool PortalEnabled, bool ShareMatterStatus, bool ShareCurrentStage, bool SharePatientTreatment,
        bool ShareOwnRecords, bool ShareOwnBills, bool ShareFirmRequests, bool ShareOtherProviders,
        bool ShareSettlementInfo);

    private sealed record ProviderRequestRow(
        Guid ProviderRequestId, Guid MatterId, string ProviderKey, string Title, string? Detail,
        string RequestKind, string StatusCode, DateTime RequestedDateUtc, DateTime? FulfilledDateUtc);

    private sealed record ReviewStateRow(
        Guid MatterId, Guid UserId, DateTime LastOpenedUtc, DateTime? PreviousOpenedUtc);
}
