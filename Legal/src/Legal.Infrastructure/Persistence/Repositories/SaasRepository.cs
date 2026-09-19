using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Saas;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Dapper persistence over the SaaS/Commerce/Platform
// tables (migrations 0241-0245). Tenant-scoped and soft-delete aware.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SaasRepository(ISqlConnectionFactory connectionFactory) : ISaasRepository
{
    // ── Email verification challenge ──────────────────────────────────────────
    public async Task CreateVerificationChallengeAsync(Guid userId, string purpose, byte[] codeHash, DateTime expiresAtUtc, int maxAttempts, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.Identity_EmailVerificationChallenge
                SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
                WHERE UserId = @UserId AND Purpose = @Purpose AND IsDeleted = 0 AND ConsumedAtUtc IS NULL;
            INSERT SaaS.Identity_EmailVerificationChallenge (UserId, Purpose, CodeHash, ExpiresAtUtc, MaxAttempts, LastSentAtUtc)
            VALUES (@UserId, @Purpose, @CodeHash, @ExpiresAtUtc, @MaxAttempts, SYSUTCDATETIME());
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Purpose = purpose, CodeHash = codeHash, ExpiresAtUtc = expiresAtUtc, MaxAttempts = maxAttempts }, cancellationToken: ct));
    }

    public async Task<VerificationChallengeRow?> GetActiveChallengeAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 ChallengeId, UserId, CodeHash, ExpiresAtUtc, AttemptCount, MaxAttempts, ConsumedAtUtc
            FROM SaaS.Identity_EmailVerificationChallenge
            WHERE UserId = @UserId AND Purpose = @Purpose AND IsDeleted = 0 AND ConsumedAtUtc IS NULL
            ORDER BY CreatedDateUtc DESC;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<VerificationChallengeRow>(new CommandDefinition(sql, new { UserId = userId, Purpose = purpose }, cancellationToken: ct));
    }

    public async Task IncrementChallengeAttemptAsync(Guid challengeId, CancellationToken ct = default)
    {
        const string sql = "UPDATE SaaS.Identity_EmailVerificationChallenge SET AttemptCount = AttemptCount + 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE ChallengeId = @ChallengeId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { ChallengeId = challengeId }, cancellationToken: ct));
    }

    public async Task ConsumeChallengeAsync(Guid challengeId, CancellationToken ct = default)
    {
        const string sql = "UPDATE SaaS.Identity_EmailVerificationChallenge SET ConsumedAtUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME() WHERE ChallengeId = @ChallengeId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { ChallengeId = challengeId }, cancellationToken: ct));
    }

    public async Task TouchChallengeResendAsync(Guid challengeId, DateTime lastSentAtUtc, CancellationToken ct = default)
    {
        const string sql = "UPDATE SaaS.Identity_EmailVerificationChallenge SET ResendCount = ResendCount + 1, LastSentAtUtc = @LastSentAtUtc, ModifiedDateUtc = SYSUTCDATETIME() WHERE ChallengeId = @ChallengeId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { ChallengeId = challengeId, LastSentAtUtc = lastSentAtUtc }, cancellationToken: ct));
    }

    // ── Provisioning state ────────────────────────────────────────────────────
    public async Task<string?> GetProvisioningStatusAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 StatusCode FROM SaaS.Identity_ProvisioningState WHERE UserId = @UserId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
    }

    public async Task UpsertProvisioningStatusAsync(Guid userId, string statusCode, Guid? tenantId, string? failureReason, CancellationToken ct = default)
    {
        const string sql = """
            MERGE SaaS.Identity_ProvisioningState AS target
            USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId AND target.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET
                StatusCode = @StatusCode,
                TenantId = COALESCE(@TenantId, target.TenantId),
                FailureReason = @FailureReason,
                StartedAtUtc = CASE WHEN @StatusCode = N'InProgress' AND target.StartedAtUtc IS NULL THEN SYSUTCDATETIME() ELSE target.StartedAtUtc END,
                CompletedAtUtc = CASE WHEN @StatusCode = N'Completed' THEN SYSUTCDATETIME() ELSE target.CompletedAtUtc END,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (UserId, StatusCode, TenantId, FailureReason, StartedAtUtc)
                VALUES (@UserId, @StatusCode, @TenantId, @FailureReason, CASE WHEN @StatusCode = N'InProgress' THEN SYSUTCDATETIME() ELSE NULL END);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, StatusCode = statusCode, TenantId = tenantId, FailureReason = failureReason }, cancellationToken: ct));
    }

    // ── Tenancy ───────────────────────────────────────────────────────────────
    public async Task<Guid> CreateTenantAsync(string name, string slug, Guid ownerUserId, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @TenantId UNIQUEIDENTIFIER = NEWID();
            INSERT SaaS.SaaS_Tenant (TenantId, Name, Slug, StatusCode, CreatedByUserId)
            VALUES (@TenantId, @Name, @Slug, N'Active', @OwnerUserId);
            SELECT @TenantId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Name = name, Slug = slug, OwnerUserId = ownerUserId }, cancellationToken: ct));
    }

    public async Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = @Code AND TenantId IS NULL AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { Code = roleCode }, cancellationToken: ct));
    }

    public async Task CreateMembershipAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.SaaS_TenantMembership (TenantId, UserId, RoleId, StatusCode, CreatedByUserId)
            SELECT @TenantId, @UserId, @RoleId, N'Active', @UserId
            WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantMembership WHERE TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, RoleId = roleId }, cancellationToken: ct));
    }

    public async Task CreateTenantPlacementAsync(Guid tenantId, string regionCode, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.SaaS_TenantPlacement (TenantId, RegionCode)
            SELECT @TenantId, @RegionCode
            WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantPlacement WHERE TenantId = @TenantId AND IsDeleted = 0);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RegionCode = regionCode }, cancellationToken: ct));
    }

    public async Task<TenantMembershipDto?> GetPrimaryMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 m.MembershipId, m.TenantId, m.UserId, m.RoleId, r.Code AS RoleCode, m.StatusCode
            FROM SaaS.SaaS_TenantMembership m
            JOIN SaaS.SaaS_Role r ON r.RoleId = m.RoleId AND r.IsDeleted = 0
            WHERE m.UserId = @UserId AND m.IsDeleted = 0 AND m.StatusCode = N'Active'
            ORDER BY m.JoinedAtUtc ASC;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<TenantMembershipDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Code
            FROM SaaS.SaaS_RolePermission rp
            JOIN SaaS.SaaS_Permission p ON p.PermissionId = rp.PermissionId AND p.IsDeleted = 0
            WHERE rp.RoleId = @RoleId AND rp.IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        // Permissions are the additive union of the user's primary membership role and the roles
        // carried by every active group they belong to within the tenant. The single-role
        // TenantMembership invariant is preserved; group roles only add permissions.
        const string sql = """
            SELECT DISTINCT p.Code
            FROM SaaS.SaaS_RolePermission rp
            JOIN SaaS.SaaS_Permission p ON p.PermissionId = rp.PermissionId AND p.IsDeleted = 0
            WHERE rp.IsDeleted = 0
              AND rp.RoleId IN (
                    SELECT m.RoleId
                    FROM SaaS.SaaS_TenantMembership m
                    WHERE m.UserId = @UserId AND m.TenantId = @TenantId
                      AND m.IsDeleted = 0 AND m.StatusCode = N'Active'
                    UNION
                    SELECT gr.RoleId
                    FROM SaaS.SaaS_TenantGroupMember gm
                    JOIN SaaS.SaaS_TenantGroup g ON g.GroupId = gm.GroupId AND g.IsDeleted = 0 AND g.StatusCode = N'Active'
                    JOIN SaaS.SaaS_TenantGroupRole gr ON gr.GroupId = g.GroupId AND gr.IsDeleted = 0
                    WHERE gm.UserId = @UserId AND gm.TenantId = @TenantId AND gm.IsDeleted = 0
              );
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { UserId = userId, TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    // ── User management ──────────────────────────────────────────────────────
    private const string MemberSelect = """
        SELECT m.MembershipId, m.UserId, m.TenantId, t.Name AS TenantName,
               ISNULL(u.FirstName, N'') AS FirstName, ISNULL(u.LastName, N'') AS LastName,
               ISNULL(u.Email, N'') AS Email, u.EmailConfirmed,
               m.RoleId, r.Code AS RoleCode, r.DisplayName AS RoleName,
               m.StatusCode, m.JoinedAtUtc, m.AuthorizationVersion, m.ProvisioningSource
        FROM SaaS.SaaS_TenantMembership m
        JOIN SaaS.SaaS_Tenant t ON t.TenantId = m.TenantId AND t.IsDeleted = 0
        JOIN SaaS.SaaS_Role r ON r.RoleId = m.RoleId AND r.IsDeleted = 0
        JOIN dbo.AspNetUsers u ON u.Id = m.UserId
        WHERE m.IsDeleted = 0
        """;

    public async Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var sql = MemberSelect
            + (tenantId.HasValue ? " AND m.TenantId = @TenantId" : string.Empty)
            + " ORDER BY t.Name, u.Email;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<ManagedMemberDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ManagedMemberDto?> GetMemberAsync(Guid membershipId, CancellationToken ct = default)
    {
        var sql = MemberSelect + " AND m.MembershipId = @MembershipId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<ManagedMemberDto>(new CommandDefinition(sql, new { MembershipId = membershipId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool includeSystemRoles, CancellationToken ct = default)
    {
        // Platform-catalog roles (TenantId NULL). When includeSystemRoles is false, exclude the SUPERADMIN system role.
        var sql = """
            SELECT RoleId, Code, DisplayName, SortOrder
            FROM SaaS.SaaS_Role
            WHERE TenantId IS NULL AND IsDeleted = 0
            """
            + (includeSystemRoles ? string.Empty : " AND Code <> N'SUPERADMIN'")
            + " ORDER BY SortOrder, DisplayName;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<AssignableRoleDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT TenantId, Name, Slug, StatusCode
            FROM SaaS.SaaS_Tenant
            WHERE IsDeleted = 0
            ORDER BY Name;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<TenantOptionDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 Name FROM SaaS.SaaS_Tenant WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<Guid?> FindUserIdByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = @NormalizedEmail;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { NormalizedEmail = normalizedEmail }, cancellationToken: ct));
    }

    public async Task<Guid?> FindActiveMembershipIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 MembershipId FROM SaaS.SaaS_TenantMembership WHERE TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: ct));
    }

    public async Task<Guid> UpsertMembershipAsync(Guid tenantId, Guid userId, Guid roleId, Guid? actorUserId, CancellationToken ct = default)
    {
        // Reactivate an existing membership (unique index on TenantId+UserId where IsDeleted=0) or create a new one.
        const string sql = """
            DECLARE @MembershipId UNIQUEIDENTIFIER = (
                SELECT TOP 1 MembershipId FROM SaaS.SaaS_TenantMembership
                WHERE TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0);

            IF @MembershipId IS NOT NULL
            BEGIN
                UPDATE SaaS.SaaS_TenantMembership
                SET RoleId = @RoleId, StatusCode = N'Active',
                    ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE MembershipId = @MembershipId;
            END
            ELSE
            BEGIN
                SET @MembershipId = NEWID();
                INSERT SaaS.SaaS_TenantMembership (MembershipId, TenantId, UserId, RoleId, StatusCode, CreatedByUserId)
                VALUES (@MembershipId, @TenantId, @UserId, @RoleId, N'Active', @ActorUserId);
            END

            SELECT @MembershipId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, RoleId = roleId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task<int> CountActiveOwnersAsync(Guid tenantId, Guid excludeMembershipId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM SaaS.SaaS_TenantMembership m
            JOIN SaaS.SaaS_Role r ON r.RoleId = m.RoleId AND r.IsDeleted = 0
            WHERE m.TenantId = @TenantId AND m.IsDeleted = 0
              AND m.StatusCode = N'Active' AND r.Code = N'OWNER'
              AND m.MembershipId <> @ExcludeMembershipId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { TenantId = tenantId, ExcludeMembershipId = excludeMembershipId }, cancellationToken: ct));
    }

    public async Task UpdateMembershipRoleAsync(Guid membershipId, Guid roleId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantMembership
            SET RoleId = @RoleId, AuthorizationVersion = AuthorizationVersion + 1,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE MembershipId = @MembershipId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { MembershipId = membershipId, RoleId = roleId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task UpdateMembershipStatusAsync(Guid membershipId, string statusCode, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantMembership
            SET StatusCode = @StatusCode, AuthorizationVersion = AuthorizationVersion + 1,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE MembershipId = @MembershipId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { MembershipId = membershipId, StatusCode = statusCode, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task RemoveMembershipAsync(Guid membershipId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantMembership
            SET IsDeleted = 1, StatusCode = N'Removed', AuthorizationVersion = AuthorizationVersion + 1,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE MembershipId = @MembershipId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { MembershipId = membershipId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    // ── Invitations (Phase B) ────────────────────────────────────────────────
    public async Task<Guid> CreateInvitationAsync(Guid tenantId, string normalizedEmail, Guid roleId, byte[] tokenHash, DateTime expiresAtUtc, Guid? invitedByUserId, IReadOnlyList<Guid> additionalRoleIds, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @InvitationId UNIQUEIDENTIFIER = NEWID();

            -- Supersede any existing pending invitation for the same tenant + email.
            UPDATE SaaS.SaaS_TenantInvitation
            SET StatusCode = N'Revoked', RevokedAtUtc = SYSUTCDATETIME(),
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @InvitedByUserId
            WHERE TenantId = @TenantId AND EmailNormalized = @Email AND IsDeleted = 0 AND StatusCode = N'Pending';

            INSERT SaaS.SaaS_TenantInvitation
                (InvitationId, TenantId, EmailNormalized, RoleId, TokenHash, StatusCode, InvitedByUserId, ExpiresAtUtc, CreatedByUserId)
            VALUES
                (@InvitationId, @TenantId, @Email, @RoleId, @TokenHash, N'Pending', @InvitedByUserId, @ExpiresAtUtc, @InvitedByUserId);

            INSERT SaaS.SaaS_TenantInvitationRole (InvitationRoleId, InvitationId, RoleId, CreatedByUserId)
            VALUES (NEWID(), @InvitationId, @RoleId, @InvitedByUserId);

            SELECT @InvitationId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var invitationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,
            new { TenantId = tenantId, Email = normalizedEmail, RoleId = roleId, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc, InvitedByUserId = invitedByUserId }, cancellationToken: ct));

        foreach (var additionalRoleId in additionalRoleIds)
        {
            if (additionalRoleId == roleId)
                continue;
            const string roleSql = """
                IF NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantInvitationRole WHERE InvitationId = @InvitationId AND RoleId = @RoleId AND IsDeleted = 0)
                INSERT SaaS.SaaS_TenantInvitationRole (InvitationRoleId, InvitationId, RoleId, CreatedByUserId)
                VALUES (NEWID(), @InvitationId, @RoleId, @InvitedByUserId);
                """;
            await connection.ExecuteAsync(new CommandDefinition(roleSql,
                new { InvitationId = invitationId, RoleId = additionalRoleId, InvitedByUserId = invitedByUserId }, cancellationToken: ct));
        }

        return invitationId;
    }

    private const string InvitationSelect = """
        SELECT i.InvitationId, i.TenantId, i.EmailNormalized AS Email, i.RoleId,
               r.Code AS RoleCode, r.DisplayName AS RoleName, i.StatusCode,
               i.InvitedByUserId, i.ExpiresAtUtc, i.AcceptedAtUtc, i.RevokedAtUtc, i.CreatedDateUtc
        FROM SaaS.SaaS_TenantInvitation i
        JOIN SaaS.SaaS_Role r ON r.RoleId = i.RoleId
        WHERE i.IsDeleted = 0
        """;

    public async Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var sql = InvitationSelect + " AND i.TenantId = @TenantId ORDER BY i.CreatedDateUtc DESC;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<TenantInvitationDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TenantInvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        var sql = InvitationSelect + " AND i.InvitationId = @InvitationId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<TenantInvitationDto>(new CommandDefinition(sql, new { InvitationId = invitationId }, cancellationToken: ct));
    }

    public async Task<InvitationRow?> GetInvitationByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 InvitationId, TenantId, EmailNormalized, RoleId, StatusCode,
                   ExpiresAtUtc, AcceptedAtUtc, RevokedAtUtc
            FROM SaaS.SaaS_TenantInvitation
            WHERE TokenHash = @TokenHash AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<InvitationRow>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Guid>> GetInvitationRoleIdsAsync(Guid invitationId, CancellationToken ct = default)
    {
        const string sql = "SELECT RoleId FROM SaaS.SaaS_TenantInvitationRole WHERE InvitationId = @InvitationId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(sql, new { InvitationId = invitationId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task MarkInvitationAcceptedAsync(Guid invitationId, Guid acceptedByUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantInvitation
            SET StatusCode = N'Accepted', AcceptedAtUtc = SYSUTCDATETIME(), AcceptedByUserId = @AcceptedByUserId,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AcceptedByUserId
            WHERE InvitationId = @InvitationId AND IsDeleted = 0 AND StatusCode = N'Pending';
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { InvitationId = invitationId, AcceptedByUserId = acceptedByUserId }, cancellationToken: ct));
    }

    public async Task RevokeInvitationAsync(Guid invitationId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantInvitation
            SET StatusCode = N'Revoked', RevokedAtUtc = SYSUTCDATETIME(),
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE InvitationId = @InvitationId AND IsDeleted = 0 AND StatusCode = N'Pending';
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { InvitationId = invitationId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task UpdateInvitationTokenAsync(Guid invitationId, byte[] tokenHash, DateTime expiresAtUtc, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantInvitation
            SET TokenHash = @TokenHash, ExpiresAtUtc = @ExpiresAtUtc, StatusCode = N'Pending',
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE InvitationId = @InvitationId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { InvitationId = invitationId, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    // ── Transactional outbox ──────────────────────────────────────────────────
    public async Task<Guid> EnqueueOutboxAsync(string messageType, string payloadJson, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @OutboxId UNIQUEIDENTIFIER = NEWID();
            INSERT SaaS.SaaS_Outbox (OutboxId, MessageType, PayloadJson, StatusCode)
            VALUES (@OutboxId, @MessageType, @PayloadJson, N'Pending');
            SELECT @OutboxId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { MessageType = messageType, PayloadJson = payloadJson }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OutboxMessageDto>> DequeueOutboxBatchAsync(int batchSize, CancellationToken ct = default)
    {
        const string sql = """
            WITH cte AS (
                SELECT TOP (@BatchSize) *
                FROM SaaS.SaaS_Outbox WITH (ROWLOCK, READPAST, UPDLOCK)
                WHERE IsDeleted = 0 AND StatusCode = N'Pending' AND NextAttemptUtc <= SYSUTCDATETIME()
                ORDER BY NextAttemptUtc
            )
            UPDATE cte
            SET AttemptCount = AttemptCount + 1, ModifiedDateUtc = SYSUTCDATETIME()
            OUTPUT inserted.OutboxId, inserted.MessageType, inserted.PayloadJson, inserted.AttemptCount, inserted.MaxAttempts;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<OutboxMessageDto>(new CommandDefinition(sql, new { BatchSize = batchSize }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task MarkOutboxSentAsync(Guid outboxId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_Outbox
            SET StatusCode = N'Sent', ProcessedAtUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
            WHERE OutboxId = @OutboxId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { OutboxId = outboxId }, cancellationToken: ct));
    }

    public async Task MarkOutboxFailedAsync(Guid outboxId, string error, DateTime nextAttemptUtc, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_Outbox
            SET StatusCode = CASE WHEN AttemptCount >= MaxAttempts THEN N'Failed' ELSE N'Pending' END,
                LastError = @Error, NextAttemptUtc = @NextAttemptUtc, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE OutboxId = @OutboxId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { OutboxId = outboxId, Error = error, NextAttemptUtc = nextAttemptUtc }, cancellationToken: ct));
    }

    // ── Commerce ──────
    public async Task<Guid?> GetPlanIdByCodeAsync(string planCode, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 PlanId FROM SaaS.Commerce_ProductPlan WHERE Code = @Code AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { Code = planCode }, cancellationToken: ct));
    }

    public async Task CreateSubscriptionAsync(Guid tenantId, Guid planId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.Commerce_Subscription (TenantId, PlanId, StatusCode, StartedAtUtc, CurrentPeriodStartUtc)
            SELECT @TenantId, @PlanId, N'Active', SYSUTCDATETIME(), SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM SaaS.Commerce_Subscription WHERE TenantId = @TenantId AND StatusCode = N'Active' AND IsDeleted = 0);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, PlanId = planId }, cancellationToken: ct));
    }

    public async Task<Guid?> GetActiveSubscriptionPlanIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 PlanId FROM SaaS.Commerce_Subscription WHERE TenantId = @TenantId AND StatusCode = N'Active' AND IsDeleted = 0 ORDER BY StartedAtUtc DESC;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<EntitlementResolution>> ResolveEntitlementsAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Plan grants merged with tenant overrides (override wins when present and not expired).
        const string sql = """
            DECLARE @PlanId UNIQUEIDENTIFIER = (
                SELECT TOP 1 PlanId FROM SaaS.Commerce_Subscription
                WHERE TenantId = @TenantId AND StatusCode = N'Active' AND IsDeleted = 0
                ORDER BY StartedAtUtc DESC);

            SELECT
                e.Code,
                e.EntitlementKind AS Kind,
                CAST(COALESCE(o.IsEnabled, pe.IsEnabled, 0) AS BIT) AS IsEnabled,
                COALESCE(o.LimitValue, pe.LimitValue) AS LimitValue,
                COALESCE(o.LimitPeriodCode, pe.LimitPeriodCode) AS LimitPeriodCode
            FROM SaaS.Commerce_Entitlement e
            LEFT JOIN SaaS.Commerce_PlanEntitlement pe
                ON pe.EntitlementId = e.EntitlementId AND pe.PlanId = @PlanId AND pe.IsDeleted = 0
            LEFT JOIN SaaS.Commerce_TenantEntitlementOverride o
                ON o.EntitlementId = e.EntitlementId AND o.TenantId = @TenantId AND o.IsDeleted = 0
                   AND (o.ExpiresAtUtc IS NULL OR o.ExpiresAtUtc > SYSUTCDATETIME())
            WHERE e.IsDeleted = 0 AND (pe.PlanEntitlementId IS NOT NULL OR o.OverrideId IS NOT NULL);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<EntitlementResolution>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    // ── Capability registry ───────────────────────────────────────────────────
    public async Task<CapabilityDefinition?> GetCapabilityAsync(string code, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 Code, DisplayName, EntitlementCode, Permission, CustomerMeterCode, RequiresMatter, IsMetered, IsAsync
            FROM SaaS.Platform_Capability WHERE Code = @Code AND IsActive = 1 AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<CapabilityDefinition>(new CommandDefinition(sql, new { Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CapabilityDefinition>> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Code, DisplayName, EntitlementCode, Permission, CustomerMeterCode, RequiresMatter, IsMetered, IsAsync
            FROM SaaS.Platform_Capability WHERE IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<CapabilityDefinition>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    // ── Usage ─────────────────────────────────────────────────────────────────
    public async Task<long> GetMonthlyUsageAsync(Guid tenantId, string meterCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ISNULL(SUM(Quantity), 0)
            FROM SaaS.Commerce_UsageLedger
            WHERE TenantId = @TenantId AND MeterCode = @MeterCode AND UsageClass = N'Customer' AND IsDeleted = 0
              AND OccurredAtUtc >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return (long)await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(sql, new { TenantId = tenantId, MeterCode = meterCode }, cancellationToken: ct));
    }

    public async Task RecordUsageAsync(Guid tenantId, Guid? userId, Guid? executionId, Guid? matterId, string meterCode, string usageClass, decimal quantity, string? correlationId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.Commerce_UsageLedger (TenantId, UserId, ExecutionId, MatterId, MeterCode, UsageClass, Quantity, CorrelationId, CreatedByUserId)
            VALUES (@TenantId, @UserId, @ExecutionId, @MatterId, @MeterCode, @UsageClass, @Quantity, @CorrelationId, @UserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ExecutionId = executionId, MatterId = matterId, MeterCode = meterCode, UsageClass = usageClass, Quantity = quantity, CorrelationId = correlationId }, cancellationToken: ct));
    }

    public async Task<Guid> ReserveUsageAsync(Guid tenantId, Guid? executionId, string meterCode, decimal quantity, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @ReservationId UNIQUEIDENTIFIER = NEWID();
            INSERT SaaS.Commerce_UsageReservation (ReservationId, TenantId, ExecutionId, MeterCode, Quantity, StatusCode, ExpiresAtUtc)
            VALUES (@ReservationId, @TenantId, @ExecutionId, @MeterCode, @Quantity, N'Reserved', @ExpiresAtUtc);
            SELECT @ReservationId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId, ExecutionId = executionId, MeterCode = meterCode, Quantity = quantity, ExpiresAtUtc = expiresAtUtc }, cancellationToken: ct));
    }

    public async Task UpdateReservationStatusAsync(Guid reservationId, string statusCode, CancellationToken ct = default)
    {
        const string sql = "UPDATE SaaS.Commerce_UsageReservation SET StatusCode = @StatusCode, ModifiedDateUtc = SYSUTCDATETIME() WHERE ReservationId = @ReservationId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { ReservationId = reservationId, StatusCode = statusCode }, cancellationToken: ct));
    }

    // ── Execution + idempotency ───────────────────────────────────────────────
    public async Task<Guid> CreateExecutionAsync(Guid tenantId, Guid userId, Guid? matterId, string capabilityCode, string correlationId, Guid? parentExecutionId, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @ExecutionId UNIQUEIDENTIFIER = NEWID();
            INSERT SaaS.Platform_IntelligenceExecution (ExecutionId, TenantId, RequestedByUserId, MatterId, CapabilityCode, StatusCode, CorrelationId, ParentExecutionId, CreatedByUserId)
            VALUES (@ExecutionId, @TenantId, @UserId, @MatterId, @CapabilityCode, N'Created', @CorrelationId, @ParentExecutionId, @UserId);
            SELECT @ExecutionId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, MatterId = matterId, CapabilityCode = capabilityCode, CorrelationId = correlationId, ParentExecutionId = parentExecutionId }, cancellationToken: ct));
    }

    public async Task UpdateExecutionStatusAsync(Guid executionId, string statusCode, string? failureCode, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.Platform_IntelligenceExecution
            SET StatusCode = @StatusCode,
                FailureCode = @FailureCode,
                StartedAtUtc = CASE WHEN @StatusCode IN (N'Queued', N'Running') AND StartedAtUtc IS NULL THEN SYSUTCDATETIME() ELSE StartedAtUtc END,
                CompletedAtUtc = CASE WHEN @StatusCode IN (N'Completed', N'Failed', N'Cancelled') THEN SYSUTCDATETIME() ELSE CompletedAtUtc END,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE ExecutionId = @ExecutionId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { ExecutionId = executionId, StatusCode = statusCode, FailureCode = failureCode }, cancellationToken: ct));
    }

    public async Task<IntelligenceExecutionDto?> GetExecutionAsync(Guid tenantId, Guid executionId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ExecutionId, TenantId, RequestedByUserId, MatterId, CapabilityCode, StatusCode, CorrelationId, FailureCode, CreatedDateUtc, StartedAtUtc, CompletedAtUtc, ParentExecutionId
            FROM SaaS.Platform_IntelligenceExecution
            WHERE ExecutionId = @ExecutionId AND TenantId = @TenantId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IntelligenceExecutionDto>(new CommandDefinition(sql, new { ExecutionId = executionId, TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<Guid?> GetIdempotentExecutionAsync(Guid tenantId, string operation, string idempotencyKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 ExecutionId FROM SaaS.Platform_IdempotencyRecord
            WHERE TenantId = @TenantId AND Operation = @Operation AND IdempotencyKey = @Key AND IsDeleted = 0
              AND ExpiresAtUtc > SYSUTCDATETIME();
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId, Operation = operation, Key = idempotencyKey }, cancellationToken: ct));
    }

    public async Task CreateIdempotencyRecordAsync(Guid tenantId, string operation, string idempotencyKey, Guid executionId, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.Platform_IdempotencyRecord (TenantId, Operation, IdempotencyKey, ExecutionId, ResponseCode, ExpiresAtUtc)
            SELECT @TenantId, @Operation, @Key, @ExecutionId, 202, @ExpiresAtUtc
            WHERE NOT EXISTS (SELECT 1 FROM SaaS.Platform_IdempotencyRecord WHERE TenantId = @TenantId AND Operation = @Operation AND IdempotencyKey = @Key AND IsDeleted = 0);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Operation = operation, Key = idempotencyKey, ExecutionId = executionId, ExpiresAtUtc = expiresAtUtc }, cancellationToken: ct));
    }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public async Task WriteAuditAsync(Guid? tenantId, Guid? userId, string eventType, Guid? executionId, string? resourceType, Guid? resourceId, string? dataJson, string? correlationId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.Platform_AuditEvent (TenantId, UserId, EventType, ExecutionId, ResourceType, ResourceId, DataJson, CorrelationId, CreatedByUserId)
            VALUES (@TenantId, @UserId, @EventType, @ExecutionId, @ResourceType, @ResourceId, @DataJson, @CorrelationId, @UserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, EventType = eventType, ExecutionId = executionId, ResourceType = resourceType, ResourceId = resourceId, DataJson = dataJson, CorrelationId = correlationId }, cancellationToken: ct));
    }

    // ── Groups (Phase C) ─────────────────────────────────────────────────────────
    private const string GroupSelect = """
        SELECT g.GroupId, g.TenantId, g.Name, g.Description, g.StatusCode, g.SortOrder,
               (SELECT COUNT(*) FROM SaaS.SaaS_TenantGroupRole gr WHERE gr.GroupId = g.GroupId AND gr.IsDeleted = 0) AS RoleCount,
               (SELECT COUNT(*) FROM SaaS.SaaS_TenantGroupMember gm WHERE gm.GroupId = g.GroupId AND gm.IsDeleted = 0) AS MemberCount,
               g.CreatedDateUtc
        FROM SaaS.SaaS_TenantGroup g
        WHERE g.IsDeleted = 0
        """;

    public async Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var sql = GroupSelect + " AND g.TenantId = @TenantId ORDER BY g.SortOrder, g.Name;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<TenantGroupDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TenantGroupDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        var sql = GroupSelect + " AND g.TenantId = @TenantId AND g.GroupId = @GroupId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<TenantGroupDto>(new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AssignableRoleDto>> GetGroupRolesAsync(Guid groupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.RoleId, r.Code, r.DisplayName, r.SortOrder
            FROM SaaS.SaaS_TenantGroupRole gr
            JOIN SaaS.SaaS_Role r ON r.RoleId = gr.RoleId AND r.IsDeleted = 0
            WHERE gr.GroupId = @GroupId AND gr.IsDeleted = 0
            ORDER BY r.SortOrder, r.DisplayName;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<AssignableRoleDto>(new CommandDefinition(sql, new { GroupId = groupId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetGroupRoleIdsAsync(Guid groupId, CancellationToken ct = default)
    {
        const string sql = "SELECT RoleId FROM SaaS.SaaS_TenantGroupRole WHERE GroupId = @GroupId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(sql, new { GroupId = groupId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> CreateGroupAsync(Guid tenantId, string name, string? description, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @GroupId UNIQUEIDENTIFIER = NEWID();
            INSERT SaaS.SaaS_TenantGroup (GroupId, TenantId, Name, Description, StatusCode, CreatedByUserId)
            VALUES (@GroupId, @TenantId, @Name, @Description, N'Active', @ActorUserId);
            SELECT @GroupId;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var groupId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,
            new { TenantId = tenantId, Name = name, Description = description, ActorUserId = actorUserId }, cancellationToken: ct));

        await ReplaceGroupRolesAsync(connection, groupId, roleIds, actorUserId, ct);
        return groupId;
    }

    public async Task UpdateGroupAsync(Guid tenantId, Guid groupId, string name, string? description, string statusCode, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantGroup
            SET Name = @Name, Description = @Description, StatusCode = @StatusCode,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, GroupId = groupId, Name = name, Description = description, StatusCode = statusCode, ActorUserId = actorUserId }, cancellationToken: ct));

        await ReplaceGroupRolesAsync(connection, groupId, roleIds, actorUserId, ct);
    }

    private static async Task ReplaceGroupRolesAsync(System.Data.IDbConnection connection, Guid groupId, IReadOnlyList<Guid> roleIds, Guid? actorUserId, CancellationToken ct)
    {
        const string clearSql = """
            UPDATE SaaS.SaaS_TenantGroupRole
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND IsDeleted = 0;
            """;
        await connection.ExecuteAsync(new CommandDefinition(clearSql, new { GroupId = groupId, ActorUserId = actorUserId }, cancellationToken: ct));

        const string addSql = """
            IF EXISTS (SELECT 1 FROM SaaS.SaaS_TenantGroupRole WHERE GroupId = @GroupId AND RoleId = @RoleId AND IsDeleted = 1)
                UPDATE SaaS.SaaS_TenantGroupRole SET IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE GroupId = @GroupId AND RoleId = @RoleId AND IsDeleted = 1;
            ELSE IF NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantGroupRole WHERE GroupId = @GroupId AND RoleId = @RoleId AND IsDeleted = 0)
                INSERT SaaS.SaaS_TenantGroupRole (GroupRoleId, GroupId, RoleId, CreatedByUserId)
                VALUES (NEWID(), @GroupId, @RoleId, @ActorUserId);
            """;
        foreach (var roleId in roleIds.Distinct())
            await connection.ExecuteAsync(new CommandDefinition(addSql, new { GroupId = groupId, RoleId = roleId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task DeleteGroupAsync(Guid tenantId, Guid groupId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantGroup
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0;

            UPDATE SaaS.SaaS_TenantGroupRole
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND IsDeleted = 0;

            UPDATE SaaS.SaaS_TenantGroupMember
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT gm.GroupMemberId, gm.GroupId, gm.UserId,
                   ISNULL(u.FirstName, N'') AS FirstName, ISNULL(u.LastName, N'') AS LastName,
                   ISNULL(u.Email, N'') AS Email, gm.CreatedDateUtc
            FROM SaaS.SaaS_TenantGroupMember gm
            JOIN dbo.AspNetUsers u ON u.Id = gm.UserId
            WHERE gm.GroupId = @GroupId AND gm.TenantId = @TenantId AND gm.IsDeleted = 0
            ORDER BY u.Email;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<GroupMemberDto>(new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AddGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM SaaS.SaaS_TenantGroupMember WHERE GroupId = @GroupId AND UserId = @UserId AND IsDeleted = 1)
                UPDATE SaaS.SaaS_TenantGroupMember SET IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE GroupId = @GroupId AND UserId = @UserId AND IsDeleted = 1;
            ELSE IF NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantGroupMember WHERE GroupId = @GroupId AND UserId = @UserId AND IsDeleted = 0)
                INSERT SaaS.SaaS_TenantGroupMember (GroupMemberId, TenantId, GroupId, UserId, CreatedByUserId)
                VALUES (NEWID(), @TenantId, @GroupId, @UserId, @ActorUserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId, UserId = userId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task RemoveGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.SaaS_TenantGroupMember
            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
            WHERE GroupId = @GroupId AND UserId = @UserId AND TenantId = @TenantId AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId, UserId = userId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    // ── Invitation groups (Phase C) ──────────────────────────────────────────────
    public async Task AddInvitationGroupsAsync(Guid invitationId, IReadOnlyList<Guid> groupIds, Guid? actorUserId, CancellationToken ct = default)
    {
        if (groupIds.Count == 0)
            return;
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM SaaS.SaaS_TenantInvitationGroup WHERE InvitationId = @InvitationId AND GroupId = @GroupId AND IsDeleted = 0)
                INSERT SaaS.SaaS_TenantInvitationGroup (InvitationGroupId, InvitationId, GroupId, CreatedByUserId)
                VALUES (NEWID(), @InvitationId, @GroupId, @ActorUserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        foreach (var groupId in groupIds.Distinct())
            await connection.ExecuteAsync(new CommandDefinition(sql, new { InvitationId = invitationId, GroupId = groupId, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Guid>> GetInvitationGroupIdsAsync(Guid invitationId, CancellationToken ct = default)
    {
        const string sql = "SELECT GroupId FROM SaaS.SaaS_TenantInvitationGroup WHERE InvitationId = @InvitationId AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(sql, new { InvitationId = invitationId }, cancellationToken: ct));
        return rows.ToList();
    }
}
