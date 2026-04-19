namespace Ams.Application.Common.Dtos;

public sealed class PlatformUsageDto
{
    public int     TotalTenants         { get; set; }
    public int     ActiveTenants        { get; set; }
    public int     SuspendedTenants     { get; set; }
    public int     TerminatedTenants    { get; set; }
    public int     TotalActiveUsers     { get; set; }
    public decimal TotalStorageUsedGb   { get; set; }
    public long    TotalApiCallsToday   { get; set; }
    public long    TotalJobsProcessed   { get; set; }
    public long    TotalExportsGenerated { get; set; }
    public DateTime SnapshotDateUtc     { get; set; }

    public IReadOnlyList<TenantUsageSummaryDto> Tenants { get; set; } = [];
}

public sealed class TenantUsageSummaryDto
{
    public Guid    TenantId             { get; set; }
    public string  TenantCode           { get; set; } = string.Empty;
    public string  TenantName           { get; set; } = string.Empty;
    public string  StatusCode           { get; set; } = string.Empty;
    public string  PlanCode             { get; set; } = string.Empty;
    public int     ActiveUsers          { get; set; }
    public decimal StorageUsedGb        { get; set; }
    public long    ApiCallsToday        { get; set; }
    public long    JobsProcessed        { get; set; }
    public long    ExportsGenerated     { get; set; }
    public DateTime CreatedDateUtc      { get; set; }
    public DateTime? LastActivityDateUtc { get; set; }
}

public sealed class UsageEventDto
{
    public Guid      EventId       { get; set; }
    public DateTime  EventTimeUtc  { get; set; }
    public Guid      TenantId      { get; set; }
    public string    TenantCode    { get; set; } = string.Empty;
    public string    TenantName    { get; set; } = string.Empty;
    public string    MetricType    { get; set; } = string.Empty;
    public decimal   Quantity      { get; set; }
    public string    SourceService { get; set; } = string.Empty;
    public string?   CorrelationId { get; set; }
}
