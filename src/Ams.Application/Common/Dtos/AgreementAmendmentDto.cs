namespace Ams.Application.Common.Dtos;

public sealed class AgreementAmendmentDto
{
    public Guid AmendmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgreementId { get; set; }
    public string AmendmentNumber { get; set; } = string.Empty;
    public string AmendmentTypeCode { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string? Description { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
