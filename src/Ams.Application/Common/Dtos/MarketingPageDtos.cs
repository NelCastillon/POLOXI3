namespace Ams.Application.Common.Dtos;

public sealed class MarketingEmailBlastDto
{
    public Guid EmailBlastId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public string AudienceSegment { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledDateUtc { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int OpenCount { get; set; }
    public int ClickCount { get; set; }
    public int BounceCount { get; set; }
    public int UnsubscribeCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class MarketingSegmentDto
{
    public Guid SegmentId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ColorCss { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ContactCount { get; set; }
    public bool IsDynamic { get; set; }
    public string Rules { get; set; } = string.Empty;
    public DateTime UpdatedDateUtc { get; set; }
}

public sealed class MarketingCrossSellOpportunityDto
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string OpportunityType { get; set; } = string.Empty;
    public int Score { get; set; }
    public decimal EstimatedPremium { get; set; }
    public string TriggerSignal { get; set; } = string.Empty;
    public DateTime LastContactDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
}

public sealed class MarketingWinBackDto
{
    public Guid WinBackId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public DateTime LapseDate { get; set; }
    public int DaysLapsed { get; set; }
    public decimal LastPremium { get; set; }
    public string LapseReason { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string LapseWindow { get; set; } = string.Empty;
}

public sealed class MarketingReferralDto
{
    public Guid ReferralId { get; set; }
    public Guid TenantId { get; set; }
    public string ProspectName { get; set; } = string.Empty;
    public string ReferredBy { get; set; } = string.Empty;
    public string ReferralType { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string PolicyInterest { get; set; } = string.Empty;
    public decimal EstimatedPremium { get; set; }
    public string Producer { get; set; } = string.Empty;
}

public sealed class MarketingReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid TenantId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public int Rating { get; set; }
    public DateTime ReviewDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
}

public sealed class MarketingReviewRequestDto
{
    public Guid ReviewRequestId { get; set; }
    public Guid TenantId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime SentDate { get; set; }
    public string Platform { get; set; } = string.Empty;
    public bool ReviewLeft { get; set; }
    public int NpsScore { get; set; }
}

public sealed class MarketingReviewsResult
{
    public List<MarketingReviewDto> Reviews { get; set; } = [];
    public List<MarketingReviewRequestDto> Requests { get; set; } = [];
}

public sealed class MarketingLandingPageDto
{
    public Guid LandingPageId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublishedUrl { get; set; } = string.Empty;
    public string PrimaryCta { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int ConversionCount { get; set; }
    public decimal ConversionRate { get; set; }
    public DateTime? LastPublishedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
