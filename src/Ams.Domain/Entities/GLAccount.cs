using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class GLAccount : AuditableEntity
{
    public string AccountCode { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public GLAccountType AccountType { get; private set; }
    public Guid? ParentGLAccountId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private GLAccount() { }

    public GLAccount(Guid tenantId, string accountCode, string accountName, GLAccountType accountType, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountCode = accountCode;
        AccountName = accountName;
        AccountType = accountType;
    }
}
