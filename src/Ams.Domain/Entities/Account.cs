using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Account : AuditableEntity
{
    public string AccountNumber { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public string AccountTypeCode { get; private set; } = string.Empty;
    public string? MainEmail { get; private set; }
    public string? MainPhone { get; private set; }

    // Status and Lifecycle
    public int StatusCodeId { get; private set; } = 1; // Active by default
    public string? LifecycleStageCode { get; private set; }
    public string? SegmentCode { get; private set; }

    // Ownership and Assignment
    public Guid? AccountOwnerUserId { get; private set; }
    public Guid? ParentAccountId { get; private set; }
    public Guid? ServicingTeamId { get; private set; }

    // Business Information
    public string? Industry { get; private set; }
    public string? Website { get; private set; }
    public decimal? AnnualRevenue { get; private set; }
    public int? Employees { get; private set; }
    public string? TaxId { get; private set; }
    public string? NaicsCode { get; private set; }

    // Address Information
    public string? Street { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Zip { get; private set; }
    public string? Country { get; private set; }

    private Account() { }

    public Account(Guid tenantId, string accountNumber, string accountName, string accountTypeCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountNumber = accountNumber;
        AccountName = accountName;
        AccountTypeCode = accountTypeCode;
        StatusCodeId = 1; // Active
        Country = "USA";
    }

    public void UpdateProfile(
        string accountName,
        string accountTypeCode,
        string? mainEmail,
        string? mainPhone,
        string? industry,
        string? website,
        decimal? annualRevenue,
        int? employees,
        string? taxId,
        string? naicsCode,
        Guid? modifiedByUserId)
    {
        AccountName = accountName;
        AccountTypeCode = accountTypeCode;
        MainEmail = mainEmail;
        MainPhone = mainPhone;
        Industry = industry;
        Website = website;
        AnnualRevenue = annualRevenue;
        Employees = employees;
        TaxId = taxId;
        NaicsCode = naicsCode;
        MarkModified(modifiedByUserId);
    }

    public void UpdateAddress(string? street, string? city, string? state, string? zip, string? country, Guid? modifiedByUserId)
    {
        Street = street;
        City = city;
        State = state;
        Zip = zip;
        Country = country;
        MarkModified(modifiedByUserId);
    }

    public void UpdateStatus(int statusCodeId, Guid? modifiedByUserId)
    {
        StatusCodeId = statusCodeId;
        MarkModified(modifiedByUserId);
    }

    public void UpdateLifecycle(string? lifecycleStageCode, Guid? modifiedByUserId)
    {
        LifecycleStageCode = lifecycleStageCode;
        MarkModified(modifiedByUserId);
    }

    public void UpdateSegment(string? segmentCode, Guid? modifiedByUserId)
    {
        SegmentCode = segmentCode;
        MarkModified(modifiedByUserId);
    }

    public void AssignOwner(Guid ownerUserId, Guid? modifiedByUserId)
    {
        AccountOwnerUserId = ownerUserId;
        MarkModified(modifiedByUserId);
    }

    public void AssignTeam(Guid teamId, Guid? modifiedByUserId)
    {
        ServicingTeamId = teamId;
        MarkModified(modifiedByUserId);
    }

    public void SetParent(Guid parentAccountId, Guid? modifiedByUserId)
    {
        ParentAccountId = parentAccountId;
        MarkModified(modifiedByUserId);
    }
}
