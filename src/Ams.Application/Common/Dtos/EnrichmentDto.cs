namespace Ams.Application.Common.Dtos;

public sealed class EnrichmentProviderDto
{
    public Guid ProviderId { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconCssClass { get; set; } = "bi-plug";
    public string StatusCode { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool EnableAutoEnrich { get; set; }
    public string AvailableFields { get; set; } = string.Empty;
    public string SelectedFields { get; set; } = string.Empty;
    public DateTime? ConnectedDateUtc { get; set; }
    public DateTime? LastRunDateUtc { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public sealed class EnrichmentJobDto
{
    public Guid JobId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProviderId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string TargetEntityType { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int RecordsRequested { get; set; }
    public int RecordsEnriched { get; set; }
    public int RecordsFailed { get; set; }
    public decimal SuccessRate { get; set; }
    public DateTime StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Notes { get; set; }
}

public sealed class EnrichmentWorkspaceDto
{
    public IReadOnlyList<EnrichmentProviderDto> Providers { get; set; } = [];
    public IReadOnlyList<EnrichmentJobDto> Jobs { get; set; } = [];
}
