using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantSettingsWorkflowRepository : ITenantSettingsWorkflowRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantSettingsWorkflowRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<TenantSettingsWorkflowItemDto>> GetByPageAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT WorkflowItemId, TenantId, PageCode, Title, Description, Category, Stage, Status,
                   Priority, OwnerName, DueDateUtc, RiskCode, ControlCode, SortOrder, CreatedDateUtc, ModifiedDateUtc
            FROM Core.TenantSettingsWorkflowItem
            WHERE TenantId = @TenantId
              AND PageCode = @PageCode
              AND IsDeleted = 0
            ORDER BY SortOrder, DueDateUtc, Title;
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<TenantSettingsWorkflowItemDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, PageCode = pageCode }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<Guid> CreateAsync(CreateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Core.TenantSettingsWorkflowItem
                (WorkflowItemId, TenantId, PageCode, Title, Description, Category, Stage, Status,
                 Priority, OwnerName, DueDateUtc, RiskCode, ControlCode, SortOrder, CreatedByUserId, CreatedDateUtc, IsDeleted)
            VALUES
                (@WorkflowItemId, @TenantId, @PageCode, @Title, @Description, @Category, @Stage, @Status,
                 @Priority, @OwnerName, @DueDateUtc, @RiskCode, @ControlCode, @SortOrder, @CreatedByUserId, SYSUTCDATETIME(), 0);
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowItemId = id,
            request.TenantId,
            request.PageCode,
            request.Title,
            request.Description,
            request.Category,
            request.Stage,
            request.Status,
            request.Priority,
            request.OwnerName,
            request.DueDateUtc,
            request.RiskCode,
            request.ControlCode,
            request.SortOrder,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task UpdateAsync(Guid workflowItemId, UpdateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantSettingsWorkflowItem
            SET Title = @Title,
                Description = @Description,
                Category = @Category,
                Stage = @Stage,
                Status = @Status,
                Priority = @Priority,
                OwnerName = @OwnerName,
                DueDateUtc = @DueDateUtc,
                RiskCode = @RiskCode,
                ControlCode = @ControlCode,
                SortOrder = @SortOrder,
                ModifiedByUserId = @ModifiedByUserId,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE WorkflowItemId = @WorkflowItemId
              AND IsDeleted = 0;
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowItemId = workflowItemId,
            request.Title,
            request.Description,
            request.Category,
            request.Stage,
            request.Status,
            request.Priority,
            request.OwnerName,
            request.DueDateUtc,
            request.RiskCode,
            request.ControlCode,
            request.SortOrder,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task AdvanceAsync(Guid workflowItemId, AdvanceTenantSettingsWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantSettingsWorkflowItem
            SET Stage = COALESCE(@Stage, Stage),
                Status = COALESCE(@Status, Status),
                ModifiedByUserId = @ModifiedByUserId,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE WorkflowItemId = @WorkflowItemId
              AND IsDeleted = 0;
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowItemId = workflowItemId,
            request.Stage,
            request.Status,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantSettingsWorkflowItem
            SET IsDeleted = 1,
                ModifiedByUserId = @ModifiedByUserId,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE WorkflowItemId = @WorkflowItemId
              AND IsDeleted = 0;
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { WorkflowItemId = workflowItemId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
