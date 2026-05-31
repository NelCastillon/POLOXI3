using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AccountCommunication : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? ContactId { get; private set; }
    public string Channel { get; private set; } = string.Empty; // Email, Phone, SMS, Portal, Chat
    public string Direction { get; private set; } = "Outbound"; // Inbound, Outbound
    public string Subject { get; private set; } = string.Empty;
    public string? MessagePreview { get; private set; }
    public string? FullMessageBody { get; private set; }
    public DateTime SentAtUtc { get; private set; }
    public bool? WasOpened { get; private set; }
    public DateTime? OpenedAtUtc { get; private set; }
    public bool? WasClicked { get; private set; }
    public DateTime? ClickedAtUtc { get; private set; }
    public string? ExternalMessageId { get; private set; }

    private AccountCommunication() { }

    public AccountCommunication(
        Guid tenantId,
        Guid accountId,
        string channel,
        string direction,
        string subject,
        DateTime sentAtUtc,
        Guid? createdByUserId,
        Guid? contactId = null,
        string? messagePreview = null,
        string? fullMessageBody = null,
        string? externalMessageId = null)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        ContactId = contactId;
        Channel = channel;
        Direction = direction;
        Subject = subject;
        MessagePreview = messagePreview;
        FullMessageBody = fullMessageBody;
        SentAtUtc = sentAtUtc;
        ExternalMessageId = externalMessageId;
    }

    public void MarkOpened(DateTime openedAtUtc)
    {
        WasOpened = true;
        OpenedAtUtc = openedAtUtc;
    }

    public void MarkClicked(DateTime clickedAtUtc)
    {
        WasClicked = true;
        ClickedAtUtc = clickedAtUtc;
    }
}
