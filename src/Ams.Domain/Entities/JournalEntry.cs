using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class JournalEntry : AuditableEntity
{
    public string JournalNumber { get; private set; } = string.Empty;
    public DateOnly EntryDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = "Draft";
    public DateTime? PostedDateUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }

    private JournalEntry() { }

    public JournalEntry(Guid tenantId, string journalNumber, DateOnly entryDate, string description, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        JournalNumber = journalNumber;
        EntryDate = entryDate;
        Description = description;
        StatusCode = "Draft";
    }
}
