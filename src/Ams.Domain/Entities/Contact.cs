using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Contact : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? JobTitle { get; private set; }
    public string ContactTypeCode { get; private set; } = "Primary";
    public bool IsBillingContact { get; private set; }
    public bool IsPortalUser { get; private set; }
    public string StatusCode { get; private set; } = "Active";

    private Contact() { }

    public Contact(Guid tenantId, Guid accountId, string firstName, string lastName, string? email, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }
}
