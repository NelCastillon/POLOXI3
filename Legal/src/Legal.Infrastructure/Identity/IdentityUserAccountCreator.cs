using Legal.Application.Abstractions.Services;
using Microsoft.AspNetCore.Identity;

namespace Legal.Infrastructure.Identity;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Identity-backed account creator.
//
// Creates the underlying ASP.NET Core Identity account (correct password hashing)
// for admin-driven provisioning. UserManager<ApplicationUser> is registered by the
// API host (AddIdentityCore) and resolved within the request scope.
//   • CreateAsync  — direct creation with an admin-supplied temporary password.
//   • InviteAsync  — creates an unconfirmed account with a random unusable password
//                    and issues an email verification code so the user sets access.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class IdentityUserAccountCreator(
    UserManager<ApplicationUser> userManager,
    IEmailVerificationService emailVerification) : IUserAccountCreator
{
    public async Task<Guid> CreateAsync(string email, string firstName, string lastName, string temporaryPassword, bool emailConfirmed, CancellationToken ct = default)
    {
        var user = BuildUser(email, firstName, lastName, emailConfirmed);
        var result = await userManager.CreateAsync(user, temporaryPassword);
        EnsureSucceeded(result);
        return user.Id;
    }

    public async Task<Guid> InviteAsync(string email, string firstName, string lastName, CancellationToken ct = default)
    {
        var user = BuildUser(email, firstName, lastName, emailConfirmed: false);
        var result = await userManager.CreateAsync(user, GenerateRandomPassword());
        EnsureSucceeded(result);
        await emailVerification.IssueCodeAsync(user.Id, email, ct);
        return user.Id;
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The identity account for this member was not found.");

        // Replace any existing password: remove first (if present), then add the new one.
        if (await userManager.HasPasswordAsync(user))
            EnsureSucceeded(await userManager.RemovePasswordAsync(user));

        EnsureSucceeded(await userManager.AddPasswordAsync(user, newPassword));
    }

    private static ApplicationUser BuildUser(string email, string firstName, string lastName, bool emailConfirmed)
        => new()
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            EmailConfirmed = emailConfirmed
        };

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // A random, complex, single-use password for invited accounts (never shared; user verifies instead).
    private static string GenerateRandomPassword()
        => $"Aa1!{Guid.NewGuid():N}{Guid.NewGuid():N}";
}
