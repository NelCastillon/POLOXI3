namespace Ams.Application.Features.Contacts;

public sealed class UpdateContactRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string ContactTypeCode { get; set; } = "Primary";
    public bool IsBillingContact { get; set; }
    public bool IsServiceContact { get; set; }
    public bool IsPortalUser { get; set; }
    public bool IsKeyContact { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string StatusCode { get; set; } = "Active";
    public Guid? ModifiedByUserId { get; set; }
}
