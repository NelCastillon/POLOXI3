namespace Ams.Application.Common.Dtos;

public sealed record AcordFormDto
{
    public Guid AcordFormId { get; init; }
    public Guid TenantId { get; init; }
    public string FormNumber { get; init; } = string.Empty;
    public string FormName { get; init; } = string.Empty;
    public string LineOfBusiness { get; init; } = string.Empty;
    public string Edition { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? PolicyNumber { get; init; }
    public bool AiPrefilled { get; init; }
    public int? PrefillFieldCount { get; init; }
    public int? PrefillConfidence { get; init; }
    public string? OwnerName { get; init; }
    public string? Description { get; init; }
    public DateTime LastModifiedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
