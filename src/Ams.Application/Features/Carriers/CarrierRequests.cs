namespace Ams.Application.Features.Carriers;

public sealed record CreateCarrierRequest(
    Guid      TenantId,
    string    CarrierName,
    string    NaicCode,
    string    AmBestRating,
    bool      IsAdmitted,
    DateTime? AppointmentDate);

public sealed record UpdateCarrierRequest(
    string    CarrierName,
    string    NaicCode,
    string    AmBestRating,
    bool      IsAdmitted,
    DateTime? AppointmentDate,
    bool      IsActive);
