namespace Ams.Application.Common.Dtos;

public sealed class UserAuditTrailDto
{
    public Guid AuditTrailId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? ActionName { get; set; }
    public string? CategoryCode { get; set; }
    public string? SeverityCode { get; set; }
    public string? ActionDescription { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public string? ChangedByFullName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
