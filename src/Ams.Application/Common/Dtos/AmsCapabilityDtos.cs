namespace Ams.Application.Common.Dtos;

public sealed class AmsCapabilityDto
{
    public Guid CapabilityId { get; set; }
    public Guid TenantId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string CapabilityCode { get; set; } = string.Empty;
    public string CapabilityName { get; set; } = string.Empty;
    public string MarketBenchmark { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public int MaturityScore { get; set; }
    public string ExistingModuleRoute { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public string? ConfigurationJson { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int RelatedRecordCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class AmsCapabilitySummaryDto
{
    public int TotalCount { get; set; }
    public int ImplementedCount { get; set; }
    public int PartialCount { get; set; }
    public int GapCount { get; set; }
    public int CriticalCount { get; set; }
    public int AverageMaturityScore { get; set; }
    public IReadOnlyList<AmsCapabilityDomainSummaryDto> Domains { get; set; } = [];
}

public sealed class AmsCapabilityDomainSummaryDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ImplementedCount { get; set; }
    public int PartialCount { get; set; }
    public int GapCount { get; set; }
    public int AverageMaturityScore { get; set; }
}

public sealed class AmsCapabilityPageDto
{
    public AmsCapabilitySummaryDto Summary { get; set; } = new();
    public IReadOnlyList<AmsCapabilityDto> Items { get; set; } = [];
}
