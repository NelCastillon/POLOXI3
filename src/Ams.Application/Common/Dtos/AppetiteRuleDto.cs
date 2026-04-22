namespace Ams.Application.Common.Dtos;

public sealed class AppetiteRuleDto
{
    public Guid     AppetiteRuleId  { get; set; }
    public Guid     TenantId        { get; set; }
    public string   RuleName        { get; set; } = string.Empty;
    public string   LobCode         { get; set; } = string.Empty;
    public string?  CarrierNaic     { get; set; }
    public string   RuleJson        { get; set; } = "{}";
    public string   AppetiteLevel   { get; set; } = "Standard";
    public int      Priority        { get; set; } = 100;
    public bool     IsActive        { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}
