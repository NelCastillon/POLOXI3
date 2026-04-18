using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Branch : AuditableEntity
{
    public string BranchCode { get; private set; } = string.Empty;
    public string BranchName { get; private set; } = string.Empty;
    public string? City { get; private set; }
    public string? StateProvince { get; private set; }
    public string? CountryCode { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Branch() { }

    public Branch(Guid tenantId, string branchCode, string branchName, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        BranchCode = branchCode;
        BranchName = branchName;
    }
}
