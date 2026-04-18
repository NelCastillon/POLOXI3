namespace Ams.Application.Features.Sod;

public sealed class CloneSodRuleRequest
{
    public string  NewRuleCode      { get; set; } = string.Empty;
    public string  NewRuleName      { get; set; } = string.Empty;
    public Guid?   ClonedByUserId   { get; set; }
}
