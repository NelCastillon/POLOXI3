namespace Ams.Application.Common.Dtos;

public sealed class ThreadMessageDto
{
    public Guid     MessageId       { get; init; }
    public Guid     ThreadId        { get; init; }
    public string   SenderName      { get; init; } = string.Empty;
    public string   Channel         { get; init; } = string.Empty;
    public string   Direction       { get; init; } = "Inbound";
    public string   Body            { get; init; } = string.Empty;
    public DateTime SentAt          { get; init; }
    public string   DeliveryStatus  { get; init; } = "Delivered";
    public bool     IsAutomated     { get; init; }
    public string   ExternalMessageId { get; init; } = string.Empty;
    public string   ProviderName    { get; init; } = string.Empty;
    public DateTime? DeliveredAtUtc { get; init; }
    public DateTime? ReadAtUtc      { get; init; }
    public IReadOnlyList<string> Attachments { get; init; } = [];
}
