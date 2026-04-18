namespace Ams.Application.Common.Dtos;

public sealed class FieldChangeLogDto
{
    public Guid FieldChangeLogId { get; set; }
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedDateUtc { get; set; }
    public string? ChangeSource { get; set; }
    public string? IpAddress { get; set; }
}
