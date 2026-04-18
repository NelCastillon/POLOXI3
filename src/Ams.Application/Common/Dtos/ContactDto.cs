namespace Ams.Application.Common.Dtos;

public sealed class ContactDto
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string ContactTypeCode { get; set; } = string.Empty;
    public bool IsBillingContact { get; set; }
    public bool IsPortalUser { get; set; }
    public bool IsKeyContact { get; set; }
    public bool IsServiceContact { get; set; }
    public Guid? ParentContactId { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
