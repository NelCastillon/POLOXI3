using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Account : AuditableEntity
{
    public string AccountNumber { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public string AccountTypeCode { get; private set; } = string.Empty;
    public string? MainEmail { get; private set; }
    public string? MainPhone { get; private set; }

    private Account() { }

    public Account(Guid tenantId, string accountNumber, string accountName, string accountTypeCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountNumber = accountNumber;
        AccountName = accountName;
        AccountTypeCode = accountTypeCode;
    }
}
