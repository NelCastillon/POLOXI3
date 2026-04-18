namespace Ams.Application.Common.Dtos;

public sealed class AgreementRenewalDto
{
    public Guid RenewalId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgreementId { get; set; }
    public string RenewalNumber { get; set; } = string.Empty;
    public DateOnly NewStartDate { get; set; }
    public DateOnly? NewEndDate { get; set; }
    public decimal? TotalContractValue { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
