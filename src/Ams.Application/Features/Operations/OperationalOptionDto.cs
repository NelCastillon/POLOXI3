namespace Ams.Application.Features.Operations;

public sealed record OperationalOptionDto(
    Guid OperationalOptionId,
    Guid? TenantId,
    string OptionGroupCode,
    string OptionCode,
    string DisplayName,
    string? Description,
    string MetadataJson,
    int SortOrder,
    bool IsDefault);
