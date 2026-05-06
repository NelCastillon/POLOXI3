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
