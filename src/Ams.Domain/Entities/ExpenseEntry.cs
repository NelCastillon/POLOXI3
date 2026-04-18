using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class ExpenseEntry : AuditableEntity
{
    public Guid? EngagementId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly ExpenseDate { get; private set; }
    public string CategoryCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string? Description { get; private set; }
    public bool IsBillable { get; private set; } = true;
    public string StatusCode { get; private set; } = "Draft";
    public Guid? InvoiceId { get; private set; }

    private ExpenseEntry() { }

    public ExpenseEntry(Guid tenantId, Guid accountId, Guid userId, DateOnly expenseDate, string categoryCode, decimal amount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        UserId = userId;
        ExpenseDate = expenseDate;
        CategoryCode = categoryCode;
        Amount = amount;
    }
}
