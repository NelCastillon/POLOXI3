using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Carrier : AuditableEntity
{
    public string CarrierName    { get; private set; } = string.Empty;
    public string NaicCode       { get; private set; } = string.Empty;
    public string AmBestRating   { get; private set; } = "NR";
    public bool   IsAdmitted     { get; private set; }
    public DateTime? AppointmentDate { get; private set; }
    public bool   IsActive       { get; private set; } = true;

    private Carrier() { }

    public Carrier(Guid tenantId, string carrierName, string naicCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        CarrierName = carrierName;
        NaicCode    = naicCode;
    }
}
