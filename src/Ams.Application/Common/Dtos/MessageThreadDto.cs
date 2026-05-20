namespace Ams.Application.Common.Dtos;

public sealed class MessageThreadDto
{
    public Guid     ThreadId        { get; init; }
    public Guid     TenantId        { get; init; }
    public string   AccountName     { get; init; } = string.Empty;
    public string?  AccountId       { get; init; }
    public string   ContactName     { get; init; } = string.Empty;
    public string?  ContactEmail    { get; init; }
    public string?  ContactPhone    { get; init; }
    public string   Channel         { get; init; } = string.Empty;
    public string   Subject         { get; init; } = string.Empty;
    public string   BodyPreview     { get; init; } = string.Empty;
    public string   Status          { get; init; } = "Open";
    public string   Priority        { get; init; } = "Normal";
    public string?  AssignedTo      { get; init; }
    public string?  Producer        { get; init; }
    public string?  Branch          { get; init; }
    public bool     IsRead          { get; init; }
    public bool     IsEscalated     { get; init; }
    public bool     OptedOut        { get; init; }
    public int      MessageCount    { get; init; }
    public DateTime LastActivityAt  { get; init; }
    public string   Sentiment       { get; init; } = "Neutral";
    public string?  CsrOwner        { get; init; }
    public string?  AiSummary       { get; init; }
    public string   QueueName       { get; init; } = "General Inbox";
    public string   SlaStatus       { get; init; } = "On Track";
    public int      SlaMinutesRemaining { get; init; }
    public DateTime? DueDateUtc     { get; init; }
    public string   ComplianceStatus { get; init; } = "Clear";
    public string   SourceSystem    { get; init; } = "AMS";
    public DateTime LastSyncedDateUtc { get; init; }
    public IReadOnlyList<ThreadMessageDto> Messages { get; init; } = [];
}
