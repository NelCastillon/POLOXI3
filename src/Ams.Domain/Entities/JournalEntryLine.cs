namespace Ams.Domain.Entities;

public sealed class JournalEntryLine
{
    public Guid LineId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid GLAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public int LineOrder { get; set; } = 1;

    private JournalEntryLine() { }

    public JournalEntryLine(Guid journalEntryId, Guid glAccountId, decimal debitAmount, decimal creditAmount, int lineOrder)
    {
        LineId = Guid.NewGuid();
        JournalEntryId = journalEntryId;
        GLAccountId = glAccountId;
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
        LineOrder = lineOrder;
    }
}
