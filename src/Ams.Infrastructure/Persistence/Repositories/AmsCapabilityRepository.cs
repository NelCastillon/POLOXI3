using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enterprise;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AmsCapabilityRepository : IAmsCapabilityRepository
{
    private readonly ISqlConnectionFactory _cf;

    public AmsCapabilityRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Projection = @"c.CapabilityId,
        c.TenantId,
        c.DomainCode,
        c.DomainName,
        c.CapabilityCode,
        c.CapabilityName,
        c.MarketBenchmark,
        c.CurrentState,
        c.StatusCode,
        c.PriorityCode,
        c.MaturityScore,
        c.ExistingModuleRoute,
        c.RecommendedAction,
        c.DataSource,
        c.ConfigurationJson,
        c.SortOrder,
        c.IsActive,
        c.CreatedDateUtc,
        COALESCE(live.RelatedRecordCount, 0) AS RelatedRecordCount";

    private const string LiveApply = @"
OUTER APPLY
(
    SELECT RelatedRecordCount =
        CASE c.CapabilityCode
            WHEN N'CARRIER_DOWNLOAD_RECONCILIATION' THEN COALESCE((SELECT COUNT(1) FROM Integration.CarrierDownloadItem i WHERE i.TenantId = c.TenantId AND i.IsDeleted = 0), 0)
            WHEN N'CARRIER_DOWNLOAD_MAPPING' THEN COALESCE((SELECT COUNT(1) FROM Agency.CarrierDownloadMapping m WHERE m.TenantId = c.TenantId AND m.IsDeleted = 0), 0)
            WHEN N'COMMISSION_ACCOUNTING' THEN COALESCE((SELECT COUNT(1) FROM Commission.CommissionTransaction t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0), 0)
            WHEN N'COMMISSION_PAYOUTS' THEN COALESCE((SELECT COUNT(1) FROM Commission.CommissionPayout p WHERE p.TenantId = c.TenantId AND p.IsDeleted = 0), 0)
            WHEN N'CERTIFICATE_MANAGEMENT' THEN COALESCE((SELECT COUNT(1) FROM OPS.TaskItem t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0 AND t.TaskTypeCode = N'CertificateOfInsurance'), 0)
            WHEN N'GLOBAL_WORK_QUEUE' THEN COALESCE((SELECT COUNT(1) FROM OPS.TaskItem t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind = N'OperationsWorkbench' AND pr.IsDeleted = 0), 0)
            WHEN N'DOCUMENT_MANAGEMENT' THEN COALESCE((SELECT COUNT(1) FROM DMS.DocumentGroup g WHERE g.TenantId = c.TenantId AND g.IsDeleted = 0), 0)
            WHEN N'ACORD_GENERATION' THEN COALESCE((SELECT COUNT(1) FROM DMS.AcordForm f WHERE f.TenantId = c.TenantId AND f.IsDeleted = 0), 0)
            WHEN N'DOCUMENT_WORKFLOW_RETENTION' THEN COALESCE((SELECT COUNT(1) FROM DMS.DocumentWorkflowTemplate wt WHERE wt.TenantId = c.TenantId AND wt.IsDeleted = 0), 0)
            WHEN N'REPORT_BUILDER' THEN COALESCE((SELECT COUNT(1) FROM Reporting.ReportDefinition rd WHERE (rd.TenantId = c.TenantId OR rd.TenantId IS NULL) AND rd.IsDeleted = 0), 0)
            WHEN N'RENEWAL_RETENTION' THEN COALESCE((SELECT COUNT(1) FROM CRM.Opportunity o WHERE o.TenantId = c.TenantId AND o.IsDeleted = 0 AND (o.OpportunityName LIKE N''%renewal%'' OR o.TypeCode LIKE N''%renewal%'')), 0)
            WHEN N'BILLING_ACCOUNTING' THEN COALESCE((SELECT COUNT(1) FROM Billing.Invoice i WHERE i.TenantId = c.TenantId AND i.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Billing.Payment p WHERE p.TenantId = c.TenantId AND p.IsDeleted = 0), 0)
            WHEN N'COMMUNICATION_CAPTURE' THEN COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind LIKE N'%Communication%' AND pr.IsDeleted = 0), 0)
            WHEN N'QUOTE_RATING_BIND' THEN COALESCE((SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId = c.TenantId AND q.IsDeleted = 0), 0)
            WHEN N'AGENCY_PRODUCER_APPOINTMENTS' THEN COALESCE((SELECT COUNT(1) FROM Agency.CarrierAppointment ca WHERE ca.TenantId = c.TenantId AND ca.IsDeleted = 0), 0)
            WHEN N'ACCOUNT_CONTACT_360' THEN COALESCE((SELECT COUNT(1) FROM Client.Account a WHERE a.TenantId = c.TenantId AND a.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Client.Contact cc WHERE cc.TenantId = c.TenantId AND cc.IsDeleted = 0), 0)
            WHEN N'MOBILE_CLIENT_EXPERIENCE' THEN COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind LIKE N'%Portal%' AND pr.IsDeleted = 0), 0)
            ELSE 0
        END
) live";

    public async Task<AmsCapabilityDto?> GetByIdAsync(Guid capabilityId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<AmsCapabilityDto>(new CommandDefinition($@"
SELECT {Projection}
FROM Enterprise.AmsCapability c
{LiveApply}
WHERE c.CapabilityId = @CapabilityId AND c.IsDeleted = 0;", new { CapabilityId = capabilityId }, cancellationToken: ct));
    }

    public async Task<AmsCapabilityPageDto> SearchAsync(SearchAmsCapabilitiesRequest request, CancellationToken ct = default)
    {
        const string sql = @"
SELECT c.CapabilityId,
       c.TenantId,
       c.DomainCode,
       c.DomainName,
       c.CapabilityCode,
       c.CapabilityName,
       c.MarketBenchmark,
       c.CurrentState,
       c.StatusCode,
       c.PriorityCode,
       c.MaturityScore,
       c.ExistingModuleRoute,
       c.RecommendedAction,
       c.DataSource,
       c.ConfigurationJson,
       c.SortOrder,
       c.IsActive,
       c.CreatedDateUtc,
       COALESCE(live.RelatedRecordCount, 0) AS RelatedRecordCount
FROM Enterprise.AmsCapability c
OUTER APPLY
(
    SELECT RelatedRecordCount =
        CASE c.CapabilityCode
            WHEN N'CARRIER_DOWNLOAD_RECONCILIATION' THEN COALESCE((SELECT COUNT(1) FROM Integration.CarrierDownloadItem i WHERE i.TenantId = c.TenantId AND i.IsDeleted = 0), 0)
            WHEN N'CARRIER_DOWNLOAD_MAPPING' THEN COALESCE((SELECT COUNT(1) FROM Agency.CarrierDownloadMapping m WHERE m.TenantId = c.TenantId AND m.IsDeleted = 0), 0)
            WHEN N'COMMISSION_ACCOUNTING' THEN COALESCE((SELECT COUNT(1) FROM Commission.CommissionTransaction t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0), 0)
            WHEN N'COMMISSION_PAYOUTS' THEN COALESCE((SELECT COUNT(1) FROM Commission.CommissionPayout p WHERE p.TenantId = c.TenantId AND p.IsDeleted = 0), 0)
            WHEN N'CERTIFICATE_MANAGEMENT' THEN COALESCE((SELECT COUNT(1) FROM OPS.TaskItem t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0 AND t.TaskTypeCode = N'CertificateOfInsurance'), 0)
            WHEN N'GLOBAL_WORK_QUEUE' THEN COALESCE((SELECT COUNT(1) FROM OPS.TaskItem t WHERE t.TenantId = c.TenantId AND t.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind = N'OperationsWorkbench' AND pr.IsDeleted = 0), 0)
            WHEN N'DOCUMENT_MANAGEMENT' THEN COALESCE((SELECT COUNT(1) FROM DMS.DocumentGroup g WHERE g.TenantId = c.TenantId AND g.IsDeleted = 0), 0)
            WHEN N'ACORD_GENERATION' THEN COALESCE((SELECT COUNT(1) FROM DMS.AcordForm f WHERE f.TenantId = c.TenantId AND f.IsDeleted = 0), 0)
            WHEN N'DOCUMENT_WORKFLOW_RETENTION' THEN COALESCE((SELECT COUNT(1) FROM DMS.DocumentWorkflowTemplate wt WHERE wt.TenantId = c.TenantId AND wt.IsDeleted = 0), 0)
            WHEN N'REPORT_BUILDER' THEN COALESCE((SELECT COUNT(1) FROM Reporting.ReportDefinition rd WHERE (rd.TenantId = c.TenantId OR rd.TenantId IS NULL) AND rd.IsDeleted = 0), 0)
            WHEN N'RENEWAL_RETENTION' THEN COALESCE((SELECT COUNT(1) FROM CRM.Opportunity o WHERE o.TenantId = c.TenantId AND o.IsDeleted = 0 AND (o.OpportunityName LIKE N'%renewal%' OR o.TypeCode LIKE N'%renewal%')), 0)
            WHEN N'BILLING_ACCOUNTING' THEN COALESCE((SELECT COUNT(1) FROM Billing.Invoice i WHERE i.TenantId = c.TenantId AND i.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Billing.Payment p WHERE p.TenantId = c.TenantId AND p.IsDeleted = 0), 0)
            WHEN N'COMMUNICATION_CAPTURE' THEN COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind LIKE N'%Communication%' AND pr.IsDeleted = 0), 0)
            WHEN N'QUOTE_RATING_BIND' THEN COALESCE((SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId = c.TenantId AND q.IsDeleted = 0), 0)
            WHEN N'AGENCY_PRODUCER_APPOINTMENTS' THEN COALESCE((SELECT COUNT(1) FROM Agency.CarrierAppointment ca WHERE ca.TenantId = c.TenantId AND ca.IsDeleted = 0), 0)
            WHEN N'ACCOUNT_CONTACT_360' THEN COALESCE((SELECT COUNT(1) FROM Client.Account a WHERE a.TenantId = c.TenantId AND a.IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Client.Contact cc WHERE cc.TenantId = c.TenantId AND cc.IsDeleted = 0), 0)
            WHEN N'MOBILE_CLIENT_EXPERIENCE' THEN COALESCE((SELECT COUNT(1) FROM Portal.AdminRecord pr WHERE pr.TenantId = c.TenantId AND pr.Kind LIKE N'%Portal%' AND pr.IsDeleted = 0), 0)
            ELSE 0
        END
) live
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND (@ActiveOnly = 0 OR c.IsActive = 1)
  AND (@DomainCode = N'' OR c.DomainCode = @DomainCode)
  AND (@StatusCode = N'' OR c.StatusCode = @StatusCode)
  AND (@PriorityCode = N'' OR c.PriorityCode = @PriorityCode)
  AND (@SearchTerm = N'' OR c.CapabilityName LIKE N'%' + @SearchTerm + N'%' OR c.MarketBenchmark LIKE N'%' + @SearchTerm + N'%' OR c.CurrentState LIKE N'%' + @SearchTerm + N'%')
ORDER BY c.SortOrder, c.DomainName, c.CapabilityName;

SELECT c.DomainCode,
       c.DomainName,
       COUNT(1) AS TotalCount,
       SUM(CASE WHEN c.StatusCode = N'Implemented' THEN 1 ELSE 0 END) AS ImplementedCount,
       SUM(CASE WHEN c.StatusCode = N'Partial' THEN 1 ELSE 0 END) AS PartialCount,
       SUM(CASE WHEN c.StatusCode = N'Gap' THEN 1 ELSE 0 END) AS GapCount,
       CONVERT(int, AVG(CONVERT(float, c.MaturityScore))) AS AverageMaturityScore
FROM Enterprise.AmsCapability c
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND (@ActiveOnly = 0 OR c.IsActive = 1)
  AND (@DomainCode = N'' OR c.DomainCode = @DomainCode)
  AND (@StatusCode = N'' OR c.StatusCode = @StatusCode)
  AND (@PriorityCode = N'' OR c.PriorityCode = @PriorityCode)
  AND (@SearchTerm = N'' OR c.CapabilityName LIKE N'%' + @SearchTerm + N'%' OR c.MarketBenchmark LIKE N'%' + @SearchTerm + N'%' OR c.CurrentState LIKE N'%' + @SearchTerm + N'%')
GROUP BY c.DomainCode, c.DomainName
ORDER BY MIN(c.SortOrder);

SELECT COUNT(1) AS TotalCount,
       SUM(CASE WHEN c.StatusCode = N'Implemented' THEN 1 ELSE 0 END) AS ImplementedCount,
       SUM(CASE WHEN c.StatusCode = N'Partial' THEN 1 ELSE 0 END) AS PartialCount,
       SUM(CASE WHEN c.StatusCode = N'Gap' THEN 1 ELSE 0 END) AS GapCount,
       SUM(CASE WHEN c.PriorityCode = N'Critical' THEN 1 ELSE 0 END) AS CriticalCount,
       COALESCE(CONVERT(int, AVG(CONVERT(float, c.MaturityScore))), 0) AS AverageMaturityScore
FROM Enterprise.AmsCapability c
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND (@ActiveOnly = 0 OR c.IsActive = 1)
  AND (@DomainCode = N'' OR c.DomainCode = @DomainCode)
  AND (@StatusCode = N'' OR c.StatusCode = @StatusCode)
  AND (@PriorityCode = N'' OR c.PriorityCode = @PriorityCode)
  AND (@SearchTerm = N'' OR c.CapabilityName LIKE N'%' + @SearchTerm + N'%' OR c.MarketBenchmark LIKE N'%' + @SearchTerm + N'%' OR c.CurrentState LIKE N'%' + @SearchTerm + N'%');";

        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var args = new
        {
            request.TenantId,
            DomainCode = request.DomainCode ?? string.Empty,
            StatusCode = request.StatusCode ?? string.Empty,
            PriorityCode = request.PriorityCode ?? string.Empty,
            SearchTerm = request.SearchTerm ?? string.Empty,
            request.ActiveOnly
        };
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, args, cancellationToken: ct));
        var items = (await multi.ReadAsync<AmsCapabilityDto>()).AsList();
        var domains = (await multi.ReadAsync<AmsCapabilityDomainSummaryDto>()).AsList();
        var summary = await multi.ReadSingleAsync<AmsCapabilitySummaryDto>();
        summary.Domains = domains;
        return new AmsCapabilityPageDto { Items = items, Summary = summary };
    }

    public async Task UpdateAsync(Guid capabilityId, UpdateAmsCapabilityRequest request, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Enterprise.AmsCapability
SET CurrentState = @CurrentState,
    StatusCode = @StatusCode,
    PriorityCode = @PriorityCode,
    MaturityScore = @MaturityScore,
    ExistingModuleRoute = @ExistingModuleRoute,
    RecommendedAction = @RecommendedAction,
    DataSource = @DataSource,
    ConfigurationJson = @ConfigurationJson,
    IsActive = @IsActive,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CapabilityId = @CapabilityId AND IsDeleted = 0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CapabilityId = capabilityId, request.CurrentState, request.StatusCode, request.PriorityCode, request.MaturityScore, request.ExistingModuleRoute, request.RecommendedAction, request.DataSource, request.ConfigurationJson, request.IsActive }, cancellationToken: ct));
    }
}
