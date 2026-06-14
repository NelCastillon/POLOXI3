using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.CarrierConfig;

public sealed record CreateMgaWholesalerRequest(Guid TenantId, string MgaCode, string MgaName, string? Type, string? Website, int SortOrder);
public sealed record UpdateMgaWholesalerRequest(string MgaCode, string MgaName, string? Type, string? Website, bool IsActive, int SortOrder);

public sealed record CreateCarrierContactRequest(Guid TenantId, Guid? CarrierId, string ContactName, string? Title, string? Email, string? Phone, string? Department, bool IsPrimary);
public sealed record UpdateCarrierContactRequest(Guid? CarrierId, string ContactName, string? Title, string? Email, string? Phone, string? Department, bool IsPrimary, bool IsActive);

public sealed record CreateCarrierAppointmentRequest(Guid TenantId, Guid? CarrierId, string AppointmentCode, string StateCode, string? LineOfBusiness, DateTime? AppointmentDate, DateTime? ExpirationDate);
public sealed record UpdateCarrierAppointmentRequest(Guid? CarrierId, string AppointmentCode, string StateCode, string? LineOfBusiness, DateTime? AppointmentDate, DateTime? ExpirationDate, bool IsActive);

public sealed record CreateCarrierPerformanceRequest(Guid TenantId, Guid? CarrierId, string Period, decimal WrittenPremium, decimal LossRatio, decimal HitRatio, int QuoteCount, int BindCount);
public sealed record UpdateCarrierPerformanceRequest(Guid? CarrierId, string Period, decimal WrittenPremium, decimal LossRatio, decimal HitRatio, int QuoteCount, int BindCount, bool IsActive);

public sealed record CreateCarrierSettingRequest(
    Guid TenantId,
    Guid? CarrierId,
    [property: Required, StringLength(100)] string SettingCode,
    [property: Required, StringLength(240)] string SettingName,
    [property: Required, StringLength(80)] string CategoryCode,
    [property: Required, StringLength(80)] string ScopeCode,
    [property: Required, StringLength(50)] string DataTypeCode,
    string? SettingValue,
    string? DefaultValue,
    [property: StringLength(1000)] string? Description,
    string ValidationJson,
    string UiSchemaJson,
    [property: StringLength(240)] string? AppliesToExecutorType,
    bool IsRequired,
    bool IsSecret,
    [property: Range(0, 9999)] int SortOrder);

public sealed record UpdateCarrierSettingRequest(
    Guid? CarrierId,
    [property: Required, StringLength(100)] string SettingCode,
    [property: Required, StringLength(240)] string SettingName,
    [property: Required, StringLength(80)] string CategoryCode,
    [property: Required, StringLength(80)] string ScopeCode,
    [property: Required, StringLength(50)] string DataTypeCode,
    string? SettingValue,
    string? DefaultValue,
    [property: StringLength(1000)] string? Description,
    string ValidationJson,
    string UiSchemaJson,
    [property: StringLength(240)] string? AppliesToExecutorType,
    bool IsRequired,
    bool IsSecret,
    bool IsActive,
    [property: Range(0, 9999)] int SortOrder);
