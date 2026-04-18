namespace Ams.Application.Features.Governance;

public sealed class CreateAccessReviewCampaignRequest
{
    public Guid     TenantId         { get; set; }
    public string   CampaignName     { get; set; } = string.Empty;
    public string?  Description      { get; set; }
    public string   ScopeTypeCode    { get; set; } = "AllUsers";
    public Guid?    ScopeReferenceId { get; set; }
    public Guid     ReviewerUserId   { get; set; }
    public DateTime StartDateUtc     { get; set; }
    public DateTime EndDateUtc       { get; set; }
    public Guid     CreatedByUserId  { get; set; }
}
