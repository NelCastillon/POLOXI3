namespace Ams.Application.Features.TenantQuotas;

public sealed class UpsertTenantQuotaRequest
{
    public string  MetricTypeCode  { get; set; } = string.Empty;
    public decimal LimitValue      { get; set; }
    public string  LimitUnit       { get; set; } = "Count";
    public string  PeriodCode      { get; set; } = "Monthly";
    public bool    IsEnforced      { get; set; } = true;
    public string  StatusCode      { get; set; } = "Active";
    public string? OverrideReason  { get; set; }
    public Guid?   CreatedByUserId { get; set; }
}

public sealed class UpdateTenantQuotaUsageRequest
{
    public decimal CurrentValue { get; set; }
}

public sealed class OverrideLimitRequest
{
    public decimal NewLimitValue   { get; set; }
    public string? OverrideReason  { get; set; }
    public Guid?   ModifiedByUserId { get; set; }
}

public sealed class ResetOverrideRequest
{
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class NotifyTenantQuotaRequest
{
    public string? Message { get; set; }
}
