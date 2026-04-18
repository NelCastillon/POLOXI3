namespace Ams.Application.Common.Dtos;

public sealed class AccessReviewItemDto
{
    public Guid    ReviewItemId       { get; set; }
    public Guid    CampaignId         { get; set; }
    public Guid    UserId             { get; set; }
    public string? UserFullName       { get; set; }
    public string? UserEmail          { get; set; }
    public string  AccessTypeCode     { get; set; } = "Role";
    public Guid?   AccessReferenceId  { get; set; }
    public string? AccessName         { get; set; }
    public string? RiskLevel          { get; set; }
    public string? DecisionCode       { get; set; }
    public string? ReviewerNotes      { get; set; }
    public Guid?   ReviewedByUserId   { get; set; }
    public string? ReviewedByFullName { get; set; }
    public DateTime? ReviewedDateUtc  { get; set; }
    public DateTime  CreatedDateUtc   { get; set; }
}
