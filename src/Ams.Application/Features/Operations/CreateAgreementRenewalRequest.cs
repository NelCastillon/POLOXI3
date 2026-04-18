namespace Ams.Application.Features.Operations;

public sealed class CreateAgreementRenewalRequest
{
    public Guid TenantId { get; set; }
    public Guid AgreementId { get; set; }
    public string RenewalNumber { get; set; } = string.Empty;
    public DateOnly NewStartDate { get; set; }
    public DateOnly? NewEndDate { get; set; }
    public decimal? TotalContractValue { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
