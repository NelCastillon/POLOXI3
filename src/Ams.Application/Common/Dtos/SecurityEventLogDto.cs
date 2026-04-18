namespace Ams.Application.Common.Dtos;

public sealed class SecurityEventLogDto
{
    public Guid SecurityEventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid?   UserId        { get; set; }
    public string? UserFullName  { get; set; }
    public string? UserEmail     { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; }
    public int? RiskScore { get; set; }
    public string? SessionId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
