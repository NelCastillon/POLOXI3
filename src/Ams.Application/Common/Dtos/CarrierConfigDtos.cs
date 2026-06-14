namespace Ams.Application.Common.Dtos;

public sealed class MgaWholesalerDto
{
    public Guid     MgaWholesalerId { get; set; }
    public Guid     TenantId        { get; set; }
    public string   MgaCode         { get; set; } = string.Empty;
    public string   MgaName         { get; set; } = string.Empty;
    public string?  Type            { get; set; }
    public string?  Website         { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class CarrierContactDto
{
    public Guid     CarrierContactId { get; set; }
    public Guid     TenantId         { get; set; }
    public Guid?    CarrierId        { get; set; }
    public string   ContactName      { get; set; } = string.Empty;
    public string?  Title            { get; set; }
    public string?  Email            { get; set; }
    public string?  Phone            { get; set; }
    public string?  Department       { get; set; }
    public bool     IsPrimary        { get; set; }
    public bool     IsActive         { get; set; }
    public DateTime CreatedDateUtc   { get; set; }
}

public sealed class CarrierAppointmentDto
{
    public Guid      CarrierAppointmentId { get; set; }
    public Guid      TenantId             { get; set; }
    public Guid?     CarrierId            { get; set; }
    public string    AppointmentCode      { get; set; } = string.Empty;
    public string    StateCode            { get; set; } = string.Empty;
    public string?   LineOfBusiness       { get; set; }
    public DateTime? AppointmentDate      { get; set; }
    public DateTime? ExpirationDate       { get; set; }
    public bool      IsActive             { get; set; }
    public DateTime  CreatedDateUtc       { get; set; }
}

public sealed class CarrierPerformanceDto
{
    public Guid     CarrierPerformanceId { get; set; }
    public Guid     TenantId             { get; set; }
    public Guid?    CarrierId            { get; set; }
    public string   Period               { get; set; } = string.Empty;
    public decimal  WrittenPremium       { get; set; }
    public decimal  LossRatio            { get; set; }
    public decimal  HitRatio             { get; set; }
    public int      QuoteCount           { get; set; }
    public int      BindCount            { get; set; }
    public bool     IsActive             { get; set; }
    public DateTime CreatedDateUtc       { get; set; }
}

public sealed class CarrierSettingDto
{
    public Guid CarrierSettingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CarrierId { get; set; }
    public string SettingCode { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string ScopeCode { get; set; } = string.Empty;
    public string DataTypeCode { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public string ValidationJson { get; set; } = "{}";
    public string UiSchemaJson { get; set; } = "{}";
    public string? AppliesToExecutorType { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSecret { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
