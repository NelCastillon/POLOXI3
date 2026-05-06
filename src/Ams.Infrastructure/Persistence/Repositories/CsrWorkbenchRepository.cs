using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CsrWorkbenchRepository : ICsrWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CsrWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CsrWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH QueueItems AS (
    SELECT
        sr.ServiceRequestId AS ItemId,
        CASE
            WHEN sr.RequestTypeCode IN ('CertificateOfInsurance', 'Certificate') THEN 'certificates'
            WHEN sr.RequestTypeCode IN ('Endorsement', 'PolicyChange') THEN 'endorsements'
            WHEN sr.RequestTypeCode IN ('Billing', 'BillingInquiry', 'BillingEnquiry') THEN 'billing-enquiries'
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') THEN 'complaints'
            WHEN sr.RequestTypeCode IN ('FollowUp', 'RenewalFollowUp') THEN 'follow-ups'
            ELSE 'service-requests'
        END AS QueueCode,
        sr.Subject AS Title,
        sr.RequestNumber AS RefNumber,
        sr.AccountId,
        COALESCE(a.AccountName, 'Unknown Account') AS AccountName,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.policyNumber') END, '') AS PolicyNumber,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.certHolder') END, '') AS CertHolder,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.category') END, sr.RequestTypeCode) AS Category,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.channel') END, 'Portal') AS Channel,
        sr.AssignedToUserId,
        COALESCE(NULLIF(u.DisplayName, ''), NULLIF(CONCAT(u.FirstName, ' ', u.LastName), ' '), 'Unassigned') AS AssignedTo,
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
            WHEN DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 7 OR sr.PriorityCode IN ('Critical', 'Urgent') AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2 THEN 'Breached'
            WHEN DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 4 OR sr.PriorityCode = 'High' AND DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) >= 2 THEN 'At Risk'
            ELSE 'On Track'
        END AS SlaStatus,
        CASE
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') AND sr.PriorityCode IN ('Critical', 'Urgent') THEN 3
            WHEN sr.RequestTypeCode IN ('Complaint', 'Escalation') OR sr.PriorityCode = 'High' THEN 2
            ELSE 1
        END AS EscalationLevel,
        COALESCE(TRY_CONVERT(DATETIME2, CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.dueDate') END), DATEADD(DAY,
            CASE sr.PriorityCode WHEN 'Critical' THEN 1 WHEN 'Urgent' THEN 2 WHEN 'High' THEN 3 ELSE 5 END,
            sr.CreatedDateUtc)) AS DueDate,
        sr.CreatedDateUtc AS CreatedAt,
        DATEDIFF(DAY, sr.CreatedDateUtc, SYSUTCDATETIME()) AS AgeDays,
        COALESCE(TRY_CONVERT(DECIMAL(18,2), CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.amount') END), 0) AS Amount,
        COALESCE(CASE WHEN ISJSON(sr.Description) = 1 THEN JSON_VALUE(sr.Description, '$.notes') END, sr.Description) AS Notes,
        CONCAT('/ops/service-requests?searchTerm=', sr.RequestNumber) AS DetailUrl
    FROM OPS.ServiceRequest sr
    LEFT JOIN Client.Account a ON a.AccountId = sr.AccountId
    LEFT JOIN IAM.[User] u ON u.UserId = sr.AssignedToUserId
    WHERE sr.TenantId = @TenantId
      AND sr.IsDeleted = 0
      AND sr.StatusCode NOT IN ('Resolved', 'Closed')
      AND (@TeamScope = 1 OR @UserId IS NULL OR sr.AssignedToUserId = @UserId)
)
SELECT TOP 200 *
FROM QueueItems
ORDER BY
    CASE Priority WHEN 'Critical' THEN 0 WHEN 'Urgent' THEN 1 WHEN 'High' THEN 2 WHEN 'Normal' THEN 3 ELSE 4 END,
    DueDate,
    CreatedAt DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CsrWorkbenchItemDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, TeamScope = teamScope }, cancellationToken: cancellationToken))).AsList();

        var serviceRequests = items.Where(i => i.QueueCode == "service-requests").ToList();
        var endorsements = items.Where(i => i.QueueCode == "endorsements").ToList();
        var certificates = items.Where(i => i.QueueCode == "certificates").ToList();
        var billingEnquiries = items.Where(i => i.QueueCode == "billing-enquiries").ToList();
        var complaints = items.Where(i => i.QueueCode == "complaints").ToList();
        var followUps = items.Where(i => i.QueueCode == "follow-ups").ToList();

        return new CsrWorkbenchDto
        {
            Counts = new CsrWorkbenchCountsDto
            {
                ServiceRequests = serviceRequests.Count,
                Endorsements = endorsements.Count,
                Certificates = certificates.Count,
                BillingEnquiries = billingEnquiries.Count,
                Complaints = complaints.Count,
                FollowUps = followUps.Count,
                OverdueFollowUps = followUps.Count(i => i.DueDate.Date < DateTime.Today),
            },
            ServiceRequests = serviceRequests,
            Endorsements = endorsements,
            Certificates = certificates,
            BillingEnquiries = billingEnquiries,
            Complaints = complaints,
            FollowUps = followUps,
        };
    }
}
