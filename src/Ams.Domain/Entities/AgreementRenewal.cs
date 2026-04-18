using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class AgreementRenewal : AuditableEntity
{
    public Guid AgreementId { get; private set; }
    public string RenewalNumber { get; private set; } = string.Empty;
    public DateOnly NewStartDate { get; private set; }
    public DateOnly? NewEndDate { get; private set; }
    public decimal? TotalContractValue { get; private set; }
    public RenewalStatus Status { get; private set; } = RenewalStatus.Pending;
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedDateUtc { get; private set; }

    private AgreementRenewal() { }

    public AgreementRenewal(Guid tenantId, Guid agreementId, string renewalNumber, DateOnly newStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AgreementId = agreementId;
        RenewalNumber = renewalNumber;
        NewStartDate = newStartDate;
        Status = RenewalStatus.Pending;
    }
}
