using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Legal.Infrastructure.Identity;

/// <summary>
/// EF Core Identity store context for Judz. Maps to the standard AspNet Identity
/// tables (created by migration 0246 via the script migrator). Only Identity's own
/// entities are tracked here; all other SaaS data uses Dapper repositories.
/// </summary>
public sealed class JudzIdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public JudzIdentityDbContext(DbContextOptions<JudzIdentityDbContext> options)
        : base(options)
    {
    }
}
