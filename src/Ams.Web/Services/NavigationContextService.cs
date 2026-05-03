namespace Ams.Web.Services;

/// <summary>
/// Scoped service that tracks the currently-viewed entity context (account, contact, policy, etc.)
/// so that the NavSidebar can build dynamic deep-links without requiring each page to pass parameters.
/// </summary>
public sealed class NavigationContextService
{
    // ── Current Account ──────────────────────────────────────────────────────
    public Guid?   CurrentAccountId   { get; private set; }
    public string? CurrentAccountName { get; private set; }

    // ── Current Contact ──────────────────────────────────────────────────────
    public Guid?   CurrentContactId   { get; private set; }
    public string? CurrentContactName { get; private set; }

    // ── Current Policy ───────────────────────────────────────────────────────
    public Guid?   CurrentPolicyId    { get; private set; }
    public string? CurrentPolicyName  { get; private set; }

    public event Action? OnChange;

    public void SetAccount(Guid id, string? name = null)
    {
        CurrentAccountId   = id;
        CurrentAccountName = name;
        Notify();
    }

    public void SetContact(Guid id, string? name = null)
    {
        CurrentContactId   = id;
        CurrentContactName = name;
        Notify();
    }

    public void SetPolicy(Guid id, string? name = null)
    {
        CurrentPolicyId   = id;
        CurrentPolicyName = name;
        Notify();
    }

    public void ClearAccount()
    {
        CurrentAccountId   = null;
        CurrentAccountName = null;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
