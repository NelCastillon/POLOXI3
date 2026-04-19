namespace Ams.Application.Common.Dtos;

public sealed class PlanAddOnDto
{
    public Guid    PlanAddOnId    { get; set; }
    public Guid    PlanId         { get; set; }
    public string  AddOnCode      { get; set; } = string.Empty;
    public string  AddOnName      { get; set; } = string.Empty;
    public decimal Price           { get; set; }
    public string  BillingFrequency { get; set; } = string.Empty;
    public string  Description    { get; set; } = string.Empty;
    public bool    IsActive       { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
