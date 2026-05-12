using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Validation;

namespace Ams.Web.Components.Pages.Contacts;

internal static class ContactPageConstants
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly string[] ContactTypes = ["Primary", "Secondary", "Executive", "Technical", "Billing", "Claims", "Decision Maker"];
    public static readonly string[] Statuses = ["Active", "Inactive", "Left Company"];
    public static readonly string[] Methods = ["Email", "Phone", "Text", "Portal", "LinkedIn"];

    public static string FullName(ContactDto contact) => $"{contact.FirstName} {contact.LastName}".Trim();
    public static string Initials(string firstName, string lastName) => $"{(string.IsNullOrWhiteSpace(firstName) ? "" : firstName[0])}{(string.IsNullOrWhiteSpace(lastName) ? "" : lastName[0])}".ToUpperInvariant();
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string StatusBadge(string status) => status switch
    {
        "Active" => "um-badge um-badge-success",
        "Left Company" => "um-badge um-badge-warning",
        "Inactive" => "um-badge um-badge-neutral",
        _ => "um-badge um-badge-neutral"
    };
}

internal sealed class ContactFormModel
{
    [Required(ErrorMessage = "Account is required.")]
    public Guid? AccountId { get; set; }

    [Required(ErrorMessage = "First Name is required.")]
    [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last Name is required.")]
    [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    [AmsEmailAddress]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string? Email { get; set; }

    [AmsPhone]
    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    [StringLength(100, ErrorMessage = "Job Title cannot exceed 100 characters.")]
    public string? JobTitle { get; set; }

    [Required(ErrorMessage = "Contact Type is required.")]
    [StringLength(50, ErrorMessage = "Contact Type cannot exceed 50 characters.")]
    public string ContactTypeCode { get; set; } = "Primary";

    [StringLength(50, ErrorMessage = "Preferred Method cannot exceed 50 characters.")]
    public string? PreferredContactMethod { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
    public string StatusCode { get; set; } = "Active";

    public bool IsBillingContact { get; set; }
    public bool IsServiceContact { get; set; }
    public bool IsPortalUser { get; set; }
    public bool IsKeyContact { get; set; }
}

internal sealed record ContactTimelineItem(string Type, string Title, string Description, DateTime DateUtc, string Icon, string Tone);

internal static class ContactPageData
{
    public static List<ContactTimelineItem> BuildTimeline(ContactDto contact) =>
    [
        new("Contact", "Contact created", $"{ContactPageConstants.FullName(contact)} was created for {contact.AccountName ?? "the account"}.", contact.CreatedDateUtc, "bi-person-plus", "info"),
        new("Role", $"Role set to {contact.ContactTypeCode}", "Tenant Admin contact role and responsibility flags were synchronized.", contact.CreatedDateUtc.AddDays(2), "bi-shield", "success"),
        new("Activity", "Service touchpoint recorded", "Account team reviewed preferred contact method and communication preferences.", DateTime.UtcNow.AddDays(-6), "bi-lightning", "warning"),
        new("Portal", contact.IsPortalUser ? "Portal access enabled" : "Portal access pending", contact.IsPortalUser ? "Contact is marked as a portal user." : "Portal invite can be issued when needed.", DateTime.UtcNow.AddDays(-15), "bi-globe", contact.IsPortalUser ? "success" : "info"),
        new("Account", "Account relationship verified", $"Linked to {contact.AccountName ?? "account"} as {contact.ContactTypeCode}.", DateTime.UtcNow.AddDays(-30), "bi-building", "info")
    ];
}
