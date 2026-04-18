using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class AgreementAmendment : AuditableEntity
{
    public Guid AgreementId { get; private set; }
    public string AmendmentNumber { get; private set; } = string.Empty;
    public AmendmentType AmendmentType { get; private set; } = AmendmentType.Amendment;
    public DateOnly EffectiveDate { get; private set; }
    public string? Description { get; private set; }
    public string StatusCode { get; private set; } = "Draft";

    private AgreementAmendment() { }

    public AgreementAmendment(Guid tenantId, Guid agreementId, string amendmentNumber, AmendmentType amendmentType, DateOnly effectiveDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AgreementId = agreementId;
        AmendmentNumber = amendmentNumber;
        AmendmentType = amendmentType;
        EffectiveDate = effectiveDate;
        StatusCode = "Draft";
    }
}
