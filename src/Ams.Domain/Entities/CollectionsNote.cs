using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CollectionsNote : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly NoteDate { get; private set; }
    public string NoteText { get; private set; } = string.Empty;
    public string ActionCode { get; private set; } = "CallMade";
    public DateOnly? NextFollowUpDate { get; private set; }

    private CollectionsNote() { }

    public CollectionsNote(Guid tenantId, Guid accountId, DateOnly noteDate, string noteText, string actionCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        NoteDate = noteDate;
        NoteText = noteText;
        ActionCode = actionCode;
    }
}
