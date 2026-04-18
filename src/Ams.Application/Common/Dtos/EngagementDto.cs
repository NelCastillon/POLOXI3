namespace Ams.Application.Common.Dtos;

public sealed class EngagementDto
{
    public Guid EngagementId { get; set; }
    public Guid TenantId { get; set; }
    public string EngagementNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public string EngagementName { get; set; } = string.Empty;
    public string EngagementTypeCode { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
