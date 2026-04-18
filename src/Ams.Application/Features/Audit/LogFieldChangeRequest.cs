namespace Ams.Application.Features.Audit;

public sealed class LogFieldChangeRequest
{
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangeSource { get; set; }
    public string? IpAddress { get; set; }
}
