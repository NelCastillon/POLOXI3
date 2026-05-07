using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Components.Pages.Accounts;

internal static class AccountPageConstants
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly string[] Statuses = ["Active", "Prospect", "Inactive", "Suspended"];
    public static readonly string[] Types = ["Commercial", "Personal", "Non-Profit", "Government", "Partner"];
    public static readonly string[] Segments = ["Enterprise", "Mid-Market", "SMB", "Key Account", "Startup"];
    public static readonly string[] LifecycleStages = ["Lead", "Prospect", "Customer", "Renewal", "At Risk", "Inactive"];

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string Initials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : name.Length > 0 ? name[..1] : "?";
    }

    public static string StatusBadge(string status) => status switch
    {
        "Active" => "um-badge um-badge-success",
        "Prospect" => "um-badge um-badge-info",
        "Suspended" => "um-badge um-badge-warning",
        "Inactive" => "um-badge um-badge-neutral",
        _ => "um-badge um-badge-neutral"
    };
}

internal sealed class AccountFormModel
{
    [Required(ErrorMessage = "Account Number is required.")]
    [StringLength(50, ErrorMessage = "Account Number cannot exceed 50 characters.")]
    public string AccountNumber { get; set; } = $"ACC-{DateTime.UtcNow:yyyyMMddHHmmss}";

    [Required(ErrorMessage = "Account Name is required.")]
    [StringLength(200, ErrorMessage = "Account Name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account Type is required.")]
    [StringLength(50, ErrorMessage = "Account Type cannot exceed 50 characters.")]
    public string AccountTypeCode { get; set; } = "Commercial";

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string? MainEmail { get; set; }

    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? MainPhone { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
    public string StatusCode { get; set; } = "Active";

    [StringLength(50, ErrorMessage = "Segment cannot exceed 50 characters.")]
    public string? SegmentCode { get; set; }

    [StringLength(50, ErrorMessage = "Lifecycle Stage cannot exceed 50 characters.")]
    public string? LifecycleStageCode { get; set; } = "Prospect";

    [StringLength(100, ErrorMessage = "Industry cannot exceed 100 characters.")]
    public string? Industry { get; set; }

    [Url(ErrorMessage = "Enter a valid website URL.")]
    [StringLength(200, ErrorMessage = "Website cannot exceed 200 characters.")]
    public string? Website { get; set; }

    [Range(0, 999999999999, ErrorMessage = "Annual Revenue must be 0 or greater.")]
    public decimal? AnnualRevenue { get; set; }
}

internal sealed record AccountTimelineItem(string Type, string Title, string Description, DateTime DateUtc, string Icon, string Tone);
internal sealed record AccountRelationshipItem(Guid AccountId, string AccountName, string AccountNumber, string RelationshipType, string StatusCode, string AccountTypeCode);

internal static class AccountPageData
{
    public static List<AccountTimelineItem> BuildTimeline(AccountDto account) =>
    [
        new("Account", "Account created", $"{account.AccountName} was created in AMS.", account.CreatedDateUtc, "bi-building-add", "info"),
        new("Lifecycle", $"Lifecycle set to {account.LifecycleStageCode ?? "Prospect"}", "Lifecycle state updated from account profile.", account.ModifiedDateUtc ?? account.CreatedDateUtc.AddDays(4), "bi-arrow-repeat", "success"),
        new("Activity", "Account review completed", "Producer reviewed service team, revenue profile, and renewal readiness.", DateTime.UtcNow.AddDays(-8), "bi-lightning", "warning"),
        new("Policy", "Policy portfolio synchronized", "Policy, claims, billing, and commission summaries are available from Account 360.", DateTime.UtcNow.AddDays(-20), "bi-shield-check", "success"),
        new("Relationship", "Relationship map refreshed", "Parent, child, and related account links were evaluated.", DateTime.UtcNow.AddDays(-35), "bi-link-45deg", "info")
    ];

    public static List<AccountRelationshipItem> BuildRelationships(AccountDto account, IReadOnlyList<AccountDto> allAccounts)
    {
        var results = new List<AccountRelationshipItem>();
        if (account.ParentAccountId.HasValue)
        {
            var parent = allAccounts.FirstOrDefault(a => a.AccountId == account.ParentAccountId.Value);
            if (parent is not null)
            {
                results.Add(new(parent.AccountId, parent.AccountName, parent.AccountNumber, "Parent", parent.StatusCode, parent.AccountTypeCode));
            }
        }

        results.AddRange(allAccounts.Where(a => a.ParentAccountId == account.AccountId).Select(a => new AccountRelationshipItem(a.AccountId, a.AccountName, a.AccountNumber, "Child", a.StatusCode, a.AccountTypeCode)));
        results.AddRange(allAccounts.Where(a => a.AccountId != account.AccountId && a.ParentAccountId != account.AccountId && a.AccountId != account.ParentAccountId).Take(3).Select(a => new AccountRelationshipItem(a.AccountId, a.AccountName, a.AccountNumber, "Related", a.StatusCode, a.AccountTypeCode)));
        return results;
    }
}
