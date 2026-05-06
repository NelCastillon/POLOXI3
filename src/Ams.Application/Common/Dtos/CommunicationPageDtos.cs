namespace Ams.Application.Common.Dtos;

public sealed class CommunicationCampaignDto
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
}

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
}
