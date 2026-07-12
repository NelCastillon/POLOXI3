namespace Ams.Application.Common.Dtos;

public sealed class LeadContactDto
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class LeadInterestLineDto
{
    public Guid InterestLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? CurrentCarrier { get; set; }
    public decimal EstPremium { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Priority { get; set; } = "Medium";
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class LeadCommunicationDto
{
    public Guid CommunicationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? MessageThreadId { get; set; }
    public Guid? ThreadMessageId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string Direction { get; set; } = "Outbound";
    public string DeliveryStatus { get; set; } = "Delivered";
    public string EngagementStatus { get; set; } = "Open";
    public Guid? SentByUserId { get; set; }
    public string? SentByName { get; set; }
    public DateTime SentAt { get; set; }
    public bool Opened { get; set; }
    public bool Clicked { get; set; }
    public bool IsAutomated { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class LeadCampaignEnrollmentDto
{
    public Guid EnrollmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string? CampaignType { get; set; }
    public string? Segment { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime EnrolledAt { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsOpen { get; set; }
    public int Clicks { get; set; }
    public decimal OpenRate { get; set; }
    public int Conversions { get; set; }
    public decimal Revenue { get; set; }
    public DateTime? LastTouch { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class LeadEngagementOptionDto
{
    public Guid OptionId { get; set; }
    public Guid TenantId { get; set; }
    public string OptionType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class LeadCampaignOptionDto
{
    public Guid CampaignId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int Reached { get; set; }
    public decimal OpenRate { get; set; }
    public int Conversions { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class LeadDocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SizeKb { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
