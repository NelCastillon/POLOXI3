namespace Ams.Application.Common.Dtos;

public sealed class AgreementDto
{
    public Guid AgreementId { get; set; }
    public Guid TenantId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Guid? OpportunityId { get; set; }
    public string AgreementTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
    public int StatusCode { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
