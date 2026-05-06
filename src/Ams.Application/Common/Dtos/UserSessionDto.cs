namespace Ams.Application.Common.Dtos;

public sealed class UserSessionDto
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceType { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoginDateUtc { get; set; }
    public DateTime? LastActivityDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
