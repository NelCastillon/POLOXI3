namespace Ams.Application.Features.Operations;

public sealed class CreateAgreementRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public string AgreementTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
