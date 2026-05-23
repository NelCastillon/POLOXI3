using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Enrichment;

public sealed class EnrichmentSearchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public string? SearchTerm { get; set; }

    public string? ProviderStatus { get; set; }

    public string? JobStatus { get; set; }

    public string? EntityType { get; set; }
}

public sealed class EnrichmentProviderConfigRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [StringLength(200)]
    public string? ApiKey { get; set; }

    [Required]
    public IReadOnlyList<string> SelectedFields { get; set; } = [];

    public bool EnableAutoEnrich { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class EnrichmentProviderStatusRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(40)]
    public string StatusCode { get; set; } = string.Empty;

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class EnrichmentRunRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? ProviderId { get; set; }

    [Required]
    [StringLength(40)]
    public string TargetEntityType { get; set; } = "All";

    [StringLength(200)]
    public string JobName { get; set; } = "Manual enrichment run";

    public Guid? CreatedByUserId { get; set; }
}
