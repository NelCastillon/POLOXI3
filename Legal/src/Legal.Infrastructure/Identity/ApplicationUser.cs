using Microsoft.AspNetCore.Identity;

namespace Legal.Infrastructure.Identity;

/// <summary>
/// Judz application user. Extends the ASP.NET Core Identity user with the profile
/// fields captured at signup. Uses a <see cref="Guid"/> primary key so SaaS tables
/// can reference the user id uniformly.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
