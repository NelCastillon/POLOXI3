namespace Ams.Application.Common.Dtos;

public sealed class CommunicationCampaignDto
{
    public Guid CampaignId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string ReplyToEmail { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string CtaLabel { get; set; } = string.Empty;
    public string SendMode { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public int FollowUpDays { get; set; }
    public bool SendFollowUp { get; set; }
    public bool SuppressRecentContacts { get; set; }
    public bool SuppressOptOut { get; set; }
    public bool AbTestSubject { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ScheduledDateUtc { get; set; }
    public string LandingPageSlug { get; set; } = string.Empty;
    public string LandingPageUrl { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int Reached { get; set; }
    public decimal OpenRate { get; set; }
    public int Conversions { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class CommunicationCampaignBuilderDataDto
{
    public CommunicationCampaignDto Campaign { get; set; } = new();
    public List<MarketingSegmentDto> Segments { get; set; } = [];
    public List<MarketingEmailBlastDto> EmailBlasts { get; set; } = [];
    public List<MarketingLandingPageDto> LandingPages { get; set; } = [];
}

public sealed class CommunicationAppointmentDto
{
    public Guid AppointmentId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string CsrOwner { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string OutcomeNotes { get; set; } = string.Empty;
    public string FollowUp { get; set; } = string.Empty;
    public bool SendConfirmation { get; set; }
    public bool SendReminder { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public string ConfirmationStatus { get; set; } = string.Empty;
    public string ReminderStatus { get; set; } = string.Empty;
    public string SlaStatus { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime? LastReminderSentUtc { get; set; }
    public DateTime? LastSyncedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class UpsertCommunicationAppointmentRequest
{
    public Guid AppointmentId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string CsrOwner { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool SendConfirmation { get; set; }
    public bool SendReminder { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ScheduledTime { get; set; }
}

public sealed record AppointmentOutcomeRequest(string Outcome, string? FollowUp, string? Notes);

public sealed record AppointmentStatusRequest(string Status, string? Reason = null);

public sealed class CommunicationOutreachContactDto
{
    public Guid OutreachContactId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastOutcome { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public bool OptedOut { get; set; }
    public DateTime? LastContactDate { get; set; }
    public DateTime? NextContactDate { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime? LastSyncedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class UpsertCommunicationOutreachRequest
{
    public Guid OutreachContactId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool OptedOut { get; set; }
    public DateTime? NextContactDate { get; set; }
}

public sealed record OutreachLogAttemptRequest(string Outcome, string? NextAction, DateTime? NextContactDate, string? Notes);

public sealed record OutreachAssignRequest(IReadOnlyList<Guid> OutreachContactIds, string AssignedTo);

public sealed record OutreachStatusRequest(string Status, string? Reason = null);

public sealed record OutreachBatchSmsRequest(IReadOnlyList<Guid> OutreachContactIds, string Message, string? TemplateName, bool TcpaConsentConfirmed, bool HonorOptOut);
