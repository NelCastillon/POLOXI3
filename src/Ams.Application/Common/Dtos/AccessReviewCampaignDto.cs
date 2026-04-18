namespace Ams.Application.Common.Dtos;

public sealed class AccessReviewCampaignDto
{
    public Guid    CampaignId         { get; set; }
    public Guid    TenantId           { get; set; }
    public string  CampaignName       { get; set; } = string.Empty;
    public string? Description        { get; set; }
    public string  ScopeTypeCode      { get; set; } = "AllUsers";
    public Guid?   ScopeReferenceId   { get; set; }
    public string? ScopeReferenceName { get; set; }
    public Guid    ReviewerUserId     { get; set; }
    public string? ReviewerFullName   { get; set; }
    public DateTime  StartDateUtc     { get; set; }
    public DateTime  EndDateUtc       { get; set; }
    public string  StatusCode         { get; set; } = "Draft";
    public int     TotalItemCount     { get; set; }
    public int     ReviewedItemCount  { get; set; }
    public int     KeepCount          { get; set; }
    public int     RemoveCount        { get; set; }
    public int     EscalateCount      { get; set; }
    public Guid    CreatedByUserId    { get; set; }
    public string? CreatedByFullName  { get; set; }
    public DateTime  CreatedDateUtc   { get; set; }
    public DateTime? ModifiedDateUtc  { get; set; }
}
