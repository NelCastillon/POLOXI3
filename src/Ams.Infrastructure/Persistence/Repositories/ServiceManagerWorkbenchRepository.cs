using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ServiceManagerWorkbenchRepository : IServiceManagerWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ServiceManagerWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ServiceManagerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
    {
        const string itemsSql = @"
;WITH BaseItems AS (
    SELECT
        sr.ServiceRequestId AS ItemId,
        CASE
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') OR sr.PriorityCode IN ('Critical', 'Urgent') THEN 'escalations'
            WHEN sr.RequestTypeCode IN ('CarrierTicket', 'CarrierService', 'Carrier') THEN 'carrier-tickets'
            WHEN sr.AssignedToUserId IS NULL THEN 'unassigned'
            ELSE 'service'
        END AS QueueCode,
        sr.Subject AS Title,
        sr.RequestNumber AS RefNumber,
        sr.AccountId,
        COALESCE(a.AccountName, 'Unknown Account') AS AccountName,
        sr.AssignedToUserId,
        COALESCE(NULLIF(u.DisplayName, ''), NULLIF(CONCAT(u.FirstName, ' ', u.LastName), ' '), 'Unassigned') AS AssignedTo,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.escalatedBy') END, 'System') AS EscalatedBy,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.carrierName') END, '') AS CarrierName,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.queueName') END,
            CASE
                WHEN sr.RequestTypeCode IN ('CertificateOfInsurance', 'Certificate') THEN 'Certificates'
                WHEN sr.RequestTypeCode IN ('Endorsement', 'PolicyChange') THEN 'Endorsements'
                WHEN sr.RequestTypeCode IN ('Billing', 'BillingInquiry', 'BillingEnquiry') THEN 'Billing'
                WHEN sr.RequestTypeCode IN ('CarrierTicket', 'CarrierService', 'Carrier') THEN 'Carrier Service'
                ELSE 'Service Requests'
            END) AS QueueName,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.auditedBy') END, '') AS AuditedBy,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.qualityNotes') END, '') AS QualityNotes,
        CASE sr.PriorityCode
            WHEN 'Critical' THEN 'Critical'
            WHEN 'Urgent' THEN 'Urgent'
            WHEN 'High' THEN 'High'
            WHEN 'Low' THEN 'Low'
            WHEN 'Medium' THEN 'Normal'
            ELSE COALESCE(sr.PriorityCode, 'Normal')
        END AS Priority,
        CASE
            WHEN sr.StatusCode IN ('Resolved', 'Closed') THEN 'On Track'
            WHEN DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 7 OR (sr.PriorityCode IN ('Critical', 'Urgent') AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2) THEN 'Breached'
            WHEN DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 4 OR (sr.PriorityCode = 'High' AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2) THEN 'At Risk'
            ELSE 'On Track'
        END AS SlaStatus,
        CASE
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') AND sr.PriorityCode IN ('Critical', 'Urgent') THEN 3
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') OR sr.PriorityCode = 'High' THEN 2
            ELSE 1
        END AS Level,
        sr.CreatedDateUtc AS CreatedAt,
        DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) AS AgeDays,
        CASE
            WHEN DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 7 OR (sr.PriorityCode IN ('Critical', 'Urgent') AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2)
            THEN DATEDIFF(MINUTE, DATEADD(DAY, CASE sr.PriorityCode WHEN 'Critical' THEN 1 WHEN 'Urgent' THEN 2 WHEN 'High' THEN 3 ELSE 5 END, sr.CreatedDateUtc), SYSUTCDATETIME())
            ELSE 0
        END AS SlaBreachMins,
        COALESCE(TRY_CONVERT(FLOAT, CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.qualityScore') END), 0) AS QualityScore,
        TRY_CONVERT(DATETIME2, CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.auditedAt') END) AS AuditedAt,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.notes') END, sr.Description) AS Notes,
        CONCAT('/ops/service-requests?searchTerm=', sr.RequestNumber) AS DetailUrl
    FROM OPS.ServiceRequest sr
    LEFT JOIN Client.Account a ON a.AccountId = sr.AccountId
    LEFT JOIN IAM.[User] u ON u.UserId = sr.AssignedToUserId
    WHERE sr.TenantId = @TenantId
      AND sr.IsDeleted = 0
      AND sr.StatusCode NOT IN ('Resolved', 'Closed')
), QueueItems AS (
    SELECT *, CASE WHEN SlaStatus = 'Breached' THEN 'sla-breaches' ELSE QueueCode END AS ManagerQueueCode
    FROM BaseItems
)
SELECT TOP 300 *
FROM QueueItems
ORDER BY
    CASE Priority WHEN 'Critical' THEN 0 WHEN 'Urgent' THEN 1 WHEN 'High' THEN 2 WHEN 'Normal' THEN 3 ELSE 4 END,
    SlaBreachMins DESC,
    CreatedAt DESC;";

        const string capacitySql = @"
;WITH AgentItems AS (
    SELECT
        sr.AssignedToUserId,
        SUM(CASE WHEN sr.StatusCode NOT IN ('Resolved', 'Closed') THEN 1 ELSE 0 END) AS OpenItems,
        SUM(CASE WHEN sr.StatusCode NOT IN ('Resolved', 'Closed') AND (DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 7 OR (sr.PriorityCode IN ('Critical', 'Urgent') AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2)) THEN 1 ELSE 0 END) AS OverdueItems
    FROM OPS.ServiceRequest sr
    WHERE sr.TenantId = @TenantId AND sr.IsDeleted = 0 AND sr.AssignedToUserId IS NOT NULL
    GROUP BY sr.AssignedToUserId
)
SELECT TOP 20
    COALESCE(NULLIF(u.DisplayName, ''), NULLIF(CONCAT(u.FirstName, ' ', u.LastName), ' '), u.UserName, 'Team Member') AS AgentName,
    COALESCE(NULLIF(u.Department, ''), 'Service Team') AS TeamName,
    CASE WHEN COALESCE(ai.OpenItems, 0) >= 9 THEN 'Online' WHEN COALESCE(ai.OpenItems, 0) >= 5 THEN 'Away' ELSE 'Online' END AS Status,
    COALESCE(ai.OpenItems, 0) AS OpenItems,
    COALESCE(ai.OverdueItems, 0) AS OverdueItems,
    CAST(CASE WHEN COALESCE(ai.OpenItems, 0) >= 12 THEN 100 ELSE COALESCE(ai.OpenItems, 0) * 100.0 / 12 END AS FLOAT) AS UtilPct
FROM IAM.[User] u
LEFT JOIN AgentItems ai ON ai.AssignedToUserId = u.UserId
WHERE u.TenantId = @TenantId
  AND (COALESCE(ai.OpenItems, 0) > 0 OR u.UserId = @UserId)
ORDER BY OpenItems DESC, AgentName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var p = new { TenantId = tenantId, UserId = userId };
        var allItems = (await cn.QueryAsync<ServiceManagerWorkbenchItemDto>(new CommandDefinition(itemsSql, p, cancellationToken: cancellationToken))).AsList();
        var capacity = (await cn.QueryAsync<ServiceManagerAgentCapacityDto>(new CommandDefinition(capacitySql, p, cancellationToken: cancellationToken))).AsList();

        var escalations = allItems.Where(i => i.ManagerQueueCode() == "escalations").ToList();
        var slaBreaches = allItems.Where(i => i.ManagerQueueCode() == "sla-breaches").ToList();
        var qualityAudits = allItems.Where(i => i.QualityScore > 0).ToList();
        var carrierTickets = allItems.Where(i => i.ManagerQueueCode() == "carrier-tickets").ToList();
        var unassigned = allItems.Where(i => i.ManagerQueueCode() == "unassigned").ToList();

        return new ServiceManagerWorkbenchDto
        {
            Counts = new ServiceManagerWorkbenchCountsDto
            {
                Escalations = escalations.Count,
                SlaBreaches = slaBreaches.Count,
                AgentsOnline = capacity.Count(a => a.Status == "Online"),
                AgentsTotal = capacity.Count,
                TeamCapacityPct = capacity.Count == 0 ? 0 : capacity.Average(a => a.UtilPct),
                QualityAudits = qualityAudits.Count,
                AvgQualityScore = qualityAudits.Count == 0 ? 0 : qualityAudits.Average(i => i.QualityScore),
                CarrierTickets = carrierTickets.Count,
                Unassigned = unassigned.Count,
            },
            Escalations = escalations,
            SlaBreaches = slaBreaches,
            AgentCapacity = capacity,
            QualityAudits = qualityAudits,
            CarrierTickets = carrierTickets,
            Unassigned = unassigned,
        };
    }

    public async Task AssignAsync(Guid tenantId, Guid itemId, Guid assignedToUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.ServiceRequest
SET AssignedToUserId = @AssignedToUserId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @AssignedToUserId
WHERE TenantId = @TenantId AND ServiceRequestId = @ItemId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ItemId = itemId, AssignedToUserId = assignedToUserId }, cancellationToken: cancellationToken));
    }
}

file static class ServiceManagerWorkbenchItemExtensions
{
    public static string ManagerQueueCode(this ServiceManagerWorkbenchItemDto item)
        => item.SlaStatus == "Breached" ? "sla-breaches" : item.QueueCode;
}
