namespace Ams.Application.Features.CarrierConfig;

public sealed record CreateMgaWholesalerRequest(Guid TenantId, string MgaCode, string MgaName, string? Type, string? Website, int SortOrder);
public sealed record UpdateMgaWholesalerRequest(string MgaCode, string MgaName, string? Type, string? Website, bool IsActive, int SortOrder);

public sealed record CreateCarrierContactRequest(Guid TenantId, Guid? CarrierId, string ContactName, string? Title, string? Email, string? Phone, string? Department, bool IsPrimary);
public sealed record UpdateCarrierContactRequest(Guid? CarrierId, string ContactName, string? Title, string? Email, string? Phone, string? Department, bool IsPrimary, bool IsActive);

public sealed record CreateCarrierAppointmentRequest(Guid TenantId, Guid? CarrierId, string AppointmentCode, string StateCode, string? LineOfBusiness, DateTime? AppointmentDate, DateTime? ExpirationDate);
public sealed record UpdateCarrierAppointmentRequest(Guid? CarrierId, string AppointmentCode, string StateCode, string? LineOfBusiness, DateTime? AppointmentDate, DateTime? ExpirationDate, bool IsActive);

public sealed record CreateCarrierPerformanceRequest(Guid TenantId, Guid? CarrierId, string Period, decimal WrittenPremium, decimal LossRatio, decimal HitRatio, int QuoteCount, int BindCount);
public sealed record UpdateCarrierPerformanceRequest(Guid? CarrierId, string Period, decimal WrittenPremium, decimal LossRatio, decimal HitRatio, int QuoteCount, int BindCount, bool IsActive);
