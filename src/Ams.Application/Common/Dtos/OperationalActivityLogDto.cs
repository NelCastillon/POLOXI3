namespace Ams.Application.Common.Dtos;

public sealed class OperationalActivityLogDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? EngagementId { get; set; }
    public Guid? AgreementId { get; set; }
    public DateOnly ActivityDate { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
