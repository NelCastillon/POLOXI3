namespace Ams.Application.Common.Dtos;

public sealed class JournalEntryLineDto
{
    public Guid LineId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid GLAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public int LineOrder { get; set; }
}
