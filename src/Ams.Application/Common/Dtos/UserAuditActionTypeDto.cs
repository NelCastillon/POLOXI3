namespace Ams.Application.Common.Dtos;

public sealed class UserAuditActionTypeDto
{
    public Guid UserAuditActionTypeId { get; set; }
    public Guid TenantId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
