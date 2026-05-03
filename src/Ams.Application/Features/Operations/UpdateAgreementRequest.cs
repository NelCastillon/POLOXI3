namespace Ams.Application.Features.Operations;

public sealed class UpdateAgreementRequest
{
    public string AgreementNumber { get; set; } = string.Empty;
    public string AgreementTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
