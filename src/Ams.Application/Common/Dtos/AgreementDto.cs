namespace Ams.Application.Common.Dtos;

public sealed class AgreementDto
{
    public Guid AgreementId { get; set; }
    public Guid TenantId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Guid? OpportunityId { get; set; }
    public int StatusCode { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
