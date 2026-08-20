using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public WorkflowRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WorkflowInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT WorkflowInstanceId, TenantId, TargetEntityName, TargetEntityId, StatusCodeId AS StatusCode, SubmittedDateUtc FROM Workflow.WorkflowInstance WHERE WorkflowInstanceId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowInstanceDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkflowInstanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT WorkflowInstanceId, TenantId, TargetEntityName, TargetEntityId, StatusCodeId AS StatusCode, SubmittedDateUtc
    FROM Workflow.WorkflowInstance
    WHERE TenantId = @TenantId
      AND (COL_LENGTH('Workflow.WorkflowInstance', 'IsDeleted') IS NULL OR IsDeleted = 0)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TargetEntityName LIKE '%' + @SearchTerm + '%' OR CONVERT(NVARCHAR(50), TargetEntityId) LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY SubmittedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Workflow.WorkflowInstance
WHERE TenantId = @TenantId
  AND (COL_LENGTH('Workflow.WorkflowInstance', 'IsDeleted') IS NULL OR IsDeleted = 0)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TargetEntityName LIKE '%' + @SearchTerm + '%' OR CONVERT(NVARCHAR(50), TargetEntityId) LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<WorkflowInstanceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<WorkflowInstanceDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? workflowDefinitionId, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @Id UNIQUEIDENTIFIER = NEWID(), @ResolvedWorkflowDefinitionId UNIQUEIDENTIFIER;

IF @WorkflowDefinitionId IS NOT NULL
BEGIN
    SELECT @ResolvedWorkflowDefinitionId = WorkflowDefinitionId
    FROM Workflow.WorkflowDefinition
    WHERE WorkflowDefinitionId = @WorkflowDefinitionId
      AND IsActive = 1
      AND IsDeleted = 0
      AND TargetEntityName = @TargetEntityName
      AND (TenantId = @TenantId OR TenantId IS NULL);
END
ELSE
BEGIN
    SELECT TOP 1 @ResolvedWorkflowDefinitionId = WorkflowDefinitionId
    FROM Workflow.WorkflowDefinition
    WHERE IsActive = 1
      AND IsDeleted = 0
      AND TargetEntityName = @TargetEntityName
      AND (TenantId = @TenantId OR TenantId IS NULL)
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, Version DESC, CreatedDateUtc DESC;
END;

IF @ResolvedWorkflowDefinitionId IS NULL
    THROW 52300, 'No active workflow definition is configured for this tenant and target entity.', 1;

INSERT INTO Workflow.WorkflowInstance (WorkflowInstanceId, TenantId, TargetEntityName, TargetEntityId, StatusCodeId, SubmittedDateUtc, CreatedDateUtc, CreatedByUserId, WorkflowDefinitionId, IsDeleted)
VALUES (@Id, @TenantId, @TargetEntityName, @TargetEntityId, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), @UserId, @ResolvedWorkflowDefinitionId, 0);
SELECT @Id;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                TargetEntityName = targetEntityName.Trim(),
                TargetEntityId = targetEntityId,
                WorkflowDefinitionId = workflowDefinitionId,
                UserId = userId
            }, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number == 52300)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    public async Task UpdateStatusAsync(Guid workflowInstanceId, int statusCode, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Workflow.WorkflowInstance
SET StatusCodeId = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
WHERE WorkflowInstanceId = @WorkflowInstanceId
  AND (COL_LENGTH('Workflow.WorkflowInstance', 'IsDeleted') IS NULL OR IsDeleted = 0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowInstanceId = workflowInstanceId,
            StatusCode = statusCode,
            UserId = userId
        }, cancellationToken: cancellationToken));
    }

    public async Task LogHistoryAsync(Guid tenantId, Guid workflowInstanceId, Guid? actorUserId, string actionCode, string? notes, string? previousStatusCode, string? newStatusCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Audit.WorkflowApprovalHistory (Id, TenantId, WorkflowInstanceId, ActorUserId, ActionCode, Notes, PreviousStatusCode, NewStatusCode, IsDelegated, ActionDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @WorkflowInstanceId, @ActorUserId, @ActionCode, @Notes, @PreviousStatusCode, @NewStatusCode, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), @ActorUserId, 0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            ActorUserId = actorUserId,
            ActionCode = actionCode,
            Notes = notes,
            PreviousStatusCode = previousStatusCode,
            NewStatusCode = newStatusCode
        }, cancellationToken: cancellationToken));
    }
}
