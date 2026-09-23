using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Saas;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Group authorization — DB-backed integration coverage (Phase C/D).
//
// Exercises the additive group-role authorization path against a real SQL Server
// database: create a tenant group, assign it a platform role, add a member, and
// assert that GetEffectivePermissionsAsync returns the UNION of the primary
// membership role permissions and the group-derived role permissions. Also
// asserts that an Inactive group contributes no permissions (status filtering).
//
// These tests are opt-in: they only run when the LEGAL_TEST_SQL environment
// variable points at a disposable SQL Server database. When it is absent the
// tests short-circuit so CI without a database stays green. The test schema is
// materialized by the real LegalDatabaseMigrator, and every seeded row is
// namespaced under a unique tenant slug and removed in DisposeAsync.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GroupAuthorizationIntegrationTests : IAsyncLifetime
{
    private const string ConnectionStringVariable = "LEGAL_TEST_SQL";

    private readonly string? _connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _slug = "itest-" + Guid.NewGuid().ToString("N");

    private ISqlConnectionFactory _factory = null!;
    private SaasRepository _repository = null!;
    private GroupService _groups = null!;

    private bool Enabled => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task InitializeAsync()
    {
        if (!Enabled)
            return;

        _factory = new TestConnectionFactory(_connectionString!);
        _repository = new SaasRepository(_factory);
        _groups = new GroupService(_repository);

        // Ensure the SaaS schema (roles, permissions, groups, memberships) exists.
        await new LegalDatabaseMigrator(_factory, NullLogger<LegalDatabaseMigrator>.Instance).MigrateAsync();

        await SeedTenantFixtureAsync();
    }

    public async Task DisposeAsync()
    {
        if (!Enabled)
            return;

        using var connection = await _factory.CreateOpenConnectionAsync();
        // Hard-delete the test tenant graph so re-runs stay isolated.
        await connection.ExecuteAsync(
            """
            DELETE gr FROM SaaS.SaaS_TenantGroupRole gr
                JOIN SaaS.SaaS_TenantGroup g ON g.GroupId = gr.GroupId WHERE g.TenantId = @TenantId;
            DELETE gm FROM SaaS.SaaS_TenantGroupMember gm
                JOIN SaaS.SaaS_TenantGroup g ON g.GroupId = gm.GroupId WHERE g.TenantId = @TenantId;
            DELETE FROM SaaS.SaaS_TenantGroup WHERE TenantId = @TenantId;
            DELETE FROM SaaS.SaaS_TenantMembership WHERE TenantId = @TenantId;
            DELETE FROM SaaS.SaaS_Tenant WHERE TenantId = @TenantId;
            """,
            new { TenantId = _tenantId });
    }

    [Fact]
    public async Task EffectivePermissions_UnionMembershipRoleWithActiveGroupRole()
    {
        if (!Enabled)
            return;

        // Primary membership already carries MEMBER (matter:read). Add a VIEWER
        // group (report:view) and the member; effective perms must include both.
        var group = await _groups.CreateGroupAsync(_tenantId, _userId,
            new CreateGroupRequest("Read-Only", "Integration group", ["VIEWER"]));
        await _groups.AddGroupMemberAsync(_tenantId, _userId, group.GroupId, _userId);

        var effective = await _repository.GetEffectivePermissionsAsync(_userId, _tenantId);

        Assert.Contains("itest:matter:read", effective);
        Assert.Contains("itest:report:view", effective);
    }

    [Fact]
    public async Task EffectivePermissions_InactiveGroup_ContributesNoPermissions()
    {
        if (!Enabled)
            return;

        var group = await _groups.CreateGroupAsync(_tenantId, _userId,
            new CreateGroupRequest("Read-Only", "Integration group", ["VIEWER"]));
        await _groups.AddGroupMemberAsync(_tenantId, _userId, group.GroupId, _userId);

        // Deactivating the group must exclude its role permissions from the union.
        await _groups.UpdateGroupAsync(_tenantId, _userId,
            new UpdateGroupRequest(group.GroupId, "Read-Only", "Integration group", TenantGroupStatus.Inactive, ["VIEWER"]));

        var effective = await _repository.GetEffectivePermissionsAsync(_userId, _tenantId);

        Assert.Contains("itest:matter:read", effective);
        Assert.DoesNotContain("itest:report:view", effective);
    }

    // ── Fixture ──────────────────────────────────────────────────────────────
    private async Task SeedTenantFixtureAsync()
    {
        using var connection = await _factory.CreateOpenConnectionAsync();

        var memberRoleId = await EnsurePlatformRoleAsync(connection, "MEMBER", "Member");
        var viewerRoleId = await EnsurePlatformRoleAsync(connection, "VIEWER", "Viewer");
        var matterRead = await EnsurePermissionAsync(connection, "itest:matter:read", "Read matters");
        var reportView = await EnsurePermissionAsync(connection, "itest:report:view", "View reports");
        await EnsureRolePermissionAsync(connection, memberRoleId, matterRead);
        await EnsureRolePermissionAsync(connection, viewerRoleId, reportView);

        await connection.ExecuteAsync(
            "INSERT SaaS.SaaS_Tenant (TenantId, Name, Slug, StatusCode) VALUES (@TenantId, @Name, @Slug, N'Active');",
            new { TenantId = _tenantId, Name = "Integration Tenant", Slug = _slug });

        await connection.ExecuteAsync(
            "INSERT SaaS.SaaS_TenantMembership (TenantId, UserId, RoleId, StatusCode) VALUES (@TenantId, @UserId, @RoleId, N'Active');",
            new { TenantId = _tenantId, UserId = _userId, RoleId = memberRoleId });
    }

    private static async Task<Guid> EnsurePlatformRoleAsync(IDbConnection connection, string code, string displayName)
    {
        var existing = await connection.ExecuteScalarAsync<Guid?>(
            "SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = @Code AND TenantId IS NULL AND IsDeleted = 0;",
            new { Code = code });
        if (existing is { } id)
            return id;

        var roleId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT SaaS.SaaS_Role (RoleId, Code, DisplayName, TenantId) VALUES (@RoleId, @Code, @DisplayName, NULL);",
            new { RoleId = roleId, Code = code, DisplayName = displayName });
        return roleId;
    }

    private static async Task<Guid> EnsurePermissionAsync(IDbConnection connection, string code, string displayName)
    {
        var existing = await connection.ExecuteScalarAsync<Guid?>(
            "SELECT TOP 1 PermissionId FROM SaaS.SaaS_Permission WHERE Code = @Code AND IsDeleted = 0;",
            new { Code = code });
        if (existing is { } id)
            return id;

        var permissionId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT SaaS.SaaS_Permission (PermissionId, Code, DisplayName) VALUES (@PermissionId, @Code, @DisplayName);",
            new { PermissionId = permissionId, Code = code, DisplayName = displayName });
        return permissionId;
    }

    private static async Task EnsureRolePermissionAsync(IDbConnection connection, Guid roleId, Guid permissionId)
    {
        await connection.ExecuteAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM SaaS.SaaS_RolePermission WHERE RoleId = @RoleId AND PermissionId = @PermissionId AND IsDeleted = 0)
                INSERT SaaS.SaaS_RolePermission (RoleId, PermissionId) VALUES (@RoleId, @PermissionId);
            """,
            new { RoleId = roleId, PermissionId = permissionId });
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }
}
