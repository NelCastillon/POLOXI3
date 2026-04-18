namespace Ams.Application.Features.Operations;

public sealed class CreateAgreementAmendmentRequest
{
    public Guid TenantId { get; set; }
    public Guid AgreementId { get; set; }
    public string AmendmentNumber { get; set; } = string.Empty;
    public string AmendmentTypeCode { get; set; } = "Amendment";
    public DateOnly EffectiveDate { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
