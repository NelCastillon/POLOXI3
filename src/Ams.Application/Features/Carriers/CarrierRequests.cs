namespace Ams.Application.Features.Carriers;

public sealed record CreateCarrierRequest(
    Guid      TenantId,
    string    CarrierName,
    string    NaicCode,
    string    AmBestRating,
    bool      IsAdmitted,
    DateTime? AppointmentDate,
    Guid?     PerformedByUserId = null,
    string?   PerformedByUserName = null,
    string?   PerformedByRole = null);

public sealed record UpdateCarrierRequest(
    string    CarrierName,
    string    NaicCode,
    string    AmBestRating,
    bool      IsAdmitted,
    DateTime? AppointmentDate,
    bool      IsActive,
    Guid?     PerformedByUserId = null,
    string?   PerformedByUserName = null,
    string?   PerformedByRole = null);
