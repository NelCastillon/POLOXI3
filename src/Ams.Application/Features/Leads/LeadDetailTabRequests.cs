using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Leads;

public class CreateLeadContactRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }

    [AmsRequiredString(ErrorMessage = "First name is required."), StringLength(150)]
    public string FirstName { get; set; } = string.Empty;

    [AmsRequiredString(ErrorMessage = "Last name is required."), StringLength(150)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(300), AmsEmailAddress]
    public string? Email { get; set; }

    [StringLength(50), AmsPhone]
    public string? Phone { get; set; }

    public bool IsPrimary { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateLeadContactRequest : CreateLeadContactRequest
{
    public Guid ContactId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateLeadInterestLineRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Carrier { get; set; }

    [StringLength(200)]
    public string? CurrentCarrier { get; set; }

    [Range(0, 999999999)]
    public decimal EstPremium { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Required, StringLength(50)]
    public string Priority { get; set; } = "Medium";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateLeadInterestLineRequest : CreateLeadInterestLineRequest
{
    public Guid InterestLineId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateLeadCommunicationRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? MessageThreadId { get; set; }

    [Required, StringLength(50)]
    public string Channel { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Preview { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Direction { get; set; } = "Outbound";

    [Required, StringLength(50)]
    public string DeliveryStatus { get; set; } = "Delivered";

    [Required, StringLength(50)]
    public string EngagementStatus { get; set; } = "Open";

    public Guid? SentByUserId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool Opened { get; set; }
    public bool Clicked { get; set; }
    public bool IsAutomated { get; set; }
}

public sealed class UpdateLeadCommunicationRequest : CreateLeadCommunicationRequest
{
    public Guid CommunicationId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateLeadCampaignEnrollmentRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? CampaignId { get; set; }

    [Required, StringLength(200)]
    public string CampaignName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string? CampaignType { get; set; }

    [Required, StringLength(150)]
    public string? Segment { get; set; }

    [Required, StringLength(50)]
    public string Status { get; set; } = "Active";

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    [Range(0, int.MaxValue)]
    public int EmailsSent { get; set; }

    [Range(0, int.MaxValue)]
    public int EmailsOpen { get; set; }

    [Range(0, int.MaxValue)]
    public int Clicks { get; set; }

    [Range(0, 100)]
    public decimal OpenRate { get; set; }

    [Range(0, int.MaxValue)]
    public int Conversions { get; set; }

    [Range(0, 999999999)]
    public decimal Revenue { get; set; }

    public DateTime? LastTouch { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateLeadCampaignEnrollmentRequest : CreateLeadCampaignEnrollmentRequest
{
    public Guid EnrollmentId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateLeadDocumentRequest
{
    public Guid? DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Extension { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SizeKb { get; set; }

    public Guid? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public sealed class UpdateLeadDocumentRequest : CreateLeadDocumentRequest
{
    public new Guid DocumentId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
