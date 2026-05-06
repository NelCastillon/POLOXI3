using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OperationsWorkbenchRepository : IOperationsWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OperationsWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OperationsWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool myItemsOnly, string? assigneeFilter, CancellationToken cancellationToken = default)
    {
        const string tasksSql = @"
SELECT TOP 100
    t.TaskItemId AS ItemId,
    CASE
        WHEN t.TaskTypeCode = 'Endorsement' THEN 'endorsements'
        WHEN t.TaskTypeCode = 'CertificateOfInsurance' THEN 'certificates'
        WHEN t.TaskTypeCode = 'RenewalFollowUp' THEN 'renewal-followups'
        ELSE 'overdue-tasks'
    END AS QueueCode,
    CASE
        WHEN t.TaskTypeCode = 'Endorsement' THEN 'Endorsements'
        WHEN t.TaskTypeCode = 'CertificateOfInsurance' THEN 'Certificates'
        WHEN t.TaskTypeCode = 'RenewalFollowUp' THEN 'Renewal Follow-ups'
        ELSE 'Overdue Tasks'
    END AS QueueName,
    t.Title,
    t.TaskNumber AS RefNumber,
    COALESCE(a.AccountName, CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.accountName') END, '') AS AccountName,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.policyNumber') END, '') AS PolicyNumber,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.certHolder') END, '') AS CertHolder,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.lobCode') END, '') AS LobCode,
    COALESCE(u.DisplayName, u.UserName, 'Tenant Admin') AS AssignedTo,
    CASE t.PriorityCode WHEN 'Critical' THEN 'Critical' WHEN 'Urgent' THEN 'Urgent' WHEN 'High' THEN 'High' WHEN 'Low' THEN 'Low' ELSE 'Normal' END AS Priority,
    CAST(COALESCE(t.DueDate, CAST(t.CreatedDateUtc AS date)) AS DATETIME2) AS DueDate,
    TRY_CONVERT(DATETIME2, CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.followUpDate') END) AS FollowUpDate,
    t.CreatedDateUtc AS CreatedAt,
    DATEDIFF(day, t.CreatedDateUtc, SYSUTCDATETIME()) AS AgeDays,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.premium') END), 0) AS Premium,
    NULL AS ErrorMessage,
    0 AS RetryCount,
    NULL AS AutomationStep,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.renewalStage') END, t.StageCode) AS RenewalStage,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.notes') END, t.Description) AS Notes,
    CAST(0 AS bit) AS CanRetry,
    CAST(CASE WHEN @UserId IS NOT NULL AND t.AssignedToUserId = @UserId THEN 1 ELSE 0 END AS bit) AS IsAssignedToMe,
    COALESCE(CASE WHEN ISJSON(t.Description) = 1 THEN JSON_VALUE(t.Description, '$.detailUrl') END, '/tasks') AS DetailUrl
FROM OPS.TaskItem t
LEFT JOIN Client.Account a ON a.AccountId = t.AccountId
LEFT JOIN IAM.[User] u ON u.UserId = t.AssignedToUserId
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND t.StatusCode NOT IN ('Complete','Completed','Cancelled','Closed')
  AND (
      (t.TaskTypeCode IN ('Endorsement','CertificateOfInsurance','RenewalFollowUp'))
      OR (t.DueDate IS NOT NULL AND CAST(t.DueDate AS date) < CAST(SYSUTCDATETIME() AS date))
  )
  AND (@MyItemsOnly = 0 OR @UserId IS NULL OR t.AssignedToUserId = @UserId)
ORDER BY CAST(COALESCE(t.DueDate, CAST(t.CreatedDateUtc AS date)) AS DATETIME2), t.CreatedDateUtc DESC;";

        const string adminSql = @"
SELECT
    pr.PortalAdminRecordId AS ItemId,
    CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.queueCode') END AS QueueCode,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.queueName') END, pr.Name) AS QueueName,
    pr.Name AS Title,
    pr.Code AS RefNumber,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.accountName') END, '') AS AccountName,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.policyNumber') END, '') AS PolicyNumber,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.certHolder') END, '') AS CertHolder,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.lobCode') END, '') AS LobCode,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.assignedTo') END, 'Tenant Admin') AS AssignedTo,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.priority') END, 'Normal') AS Priority,
    COALESCE(TRY_CONVERT(DATETIME2, CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.dueDate') END), pr.CreatedDateUtc) AS DueDate,
    TRY_CONVERT(DATETIME2, CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.followUpDate') END) AS FollowUpDate,
    pr.CreatedDateUtc AS CreatedAt,
    COALESCE(TRY_CONVERT(INT, CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.ageDays') END), DATEDIFF(day, pr.CreatedDateUtc, SYSUTCDATETIME())) AS AgeDays,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.premium') END), 0) AS Premium,
    CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.errorMessage') END AS ErrorMessage,
    COALESCE(TRY_CONVERT(INT, CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.retryCount') END), 0) AS RetryCount,
    CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.automationStep') END AS AutomationStep,
    CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.renewalStage') END AS RenewalStage,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.notes') END, '') AS Notes,
    CAST(COALESCE(TRY_CONVERT(bit, CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.canRetry') END), CASE WHEN CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.queueCode') END IN ('doc-exceptions','failed-downloads','failed-automations') THEN 1 ELSE 0 END) AS bit) AS CanRetry,
    CAST(CASE WHEN COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.assignedTo') END, '') = 'Tenant Admin' THEN 1 ELSE 0 END AS bit) AS IsAssignedToMe,
    COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.detailUrl') END, '/workbench/operations') AS DetailUrl
FROM Portal.AdminRecord pr
WHERE pr.TenantId = @TenantId
  AND pr.Kind = 'OperationsWorkbench'
  AND pr.IsDeleted = 0
  AND (@MyItemsOnly = 0 OR COALESCE(CASE WHEN ISJSON(pr.JsonData) = 1 THEN JSON_VALUE(pr.JsonData, '$.assignedTo') END, 'Tenant Admin') = 'Tenant Admin')
ORDER BY DueDate, CreatedAt DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var p = new { TenantId = tenantId, UserId = userId, MyItemsOnly = myItemsOnly || assigneeFilter == "me" };

        var taskItems = (await cn.QueryAsync<OperationsWorkbenchItemDto>(new CommandDefinition(tasksSql, p, cancellationToken: cancellationToken))).AsList();
        var adminItems = (await cn.QueryAsync<OperationsWorkbenchItemDto>(new CommandDefinition(adminSql, p, cancellationToken: cancellationToken))).AsList();
        var items = taskItems.Concat(adminItems).ToList();

        var overdueTasks = items.Where(i => i.QueueCode == "overdue-tasks").ToList();
        var endorsements = items.Where(i => i.QueueCode == "endorsements").ToList();
        var certificates = items.Where(i => i.QueueCode == "certificates").ToList();
        var renewals = items.Where(i => i.QueueCode == "renewal-followups").ToList();
        var docExceptions = items.Where(i => i.QueueCode == "doc-exceptions").ToList();
        var downloads = items.Where(i => i.QueueCode == "failed-downloads").ToList();
        var automations = items.Where(i => i.QueueCode == "failed-automations").ToList();

        return new OperationsWorkbenchDto
        {
            Counts = new OperationsWorkbenchCountsDto
            {
                OverdueTasks = overdueTasks.Count,
                PendingEndorsements = endorsements.Count,
                CertificateRequests = certificates.Count,
                RenewalFollowups = renewals.Count,
                DocIndexingExceptions = docExceptions.Count,
                FailedDownloads = downloads.Count,
                FailedAutomations = automations.Count,
            },
            OverdueTasks = overdueTasks,
            PendingEndorsements = endorsements,
            CertificateRequests = certificates,
            RenewalFollowups = renewals,
            DocExceptions = docExceptions,
            FailedDownloads = downloads,
            FailedAutomations = automations,
        };
    }

    public Task RetryItemAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
        => UpdateAdminRecordAsync(tenantId, itemId, "Retry queued", "retryQueuedAt", cancellationToken);

    public Task SkipAutomationStepAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
        => UpdateAdminRecordAsync(tenantId, itemId, "Step skipped", "skippedAt", cancellationToken);

    private async Task UpdateAdminRecordAsync(Guid tenantId, Guid itemId, string status, string timestampProperty, CancellationToken cancellationToken)
    {
        var sql = $@"
UPDATE Portal.AdminRecord
SET Status = @Status,
    JsonData = JSON_MODIFY(JSON_MODIFY(JsonData, '$.status', @Status), '$.{timestampProperty}', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126)),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE PortalAdminRecordId = @ItemId
  AND TenantId = @TenantId
  AND Kind = 'OperationsWorkbench'
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ItemId = itemId, Status = status }, cancellationToken: cancellationToken));
    }
}
