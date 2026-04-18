namespace Ams.Web.Services;

/// <summary>
/// Scoped service that owns sidebar / mobile-nav / quick-search / theme state for the enterprise shell.
/// Components subscribe to <see cref="OnChange"/> and call StateHasChanged.
/// </summary>
public sealed class ShellStateService
{
    public bool NavCollapsed    { get; private set; }
    public bool MobileNavOpen   { get; private set; }
    public bool QuickSearchOpen { get; private set; }
    public bool ThemeDark       { get; private set; }

    public event Action? OnChange;

    public void ToggleNav()
    {
        NavCollapsed = !NavCollapsed;
        Notify();
    }

    public void OpenMobileNav()
    {
        MobileNavOpen = true;
        Notify();
    }

    public void CloseMobileNav()
    {
        MobileNavOpen = false;
        Notify();
    }

    public void OpenQuickSearch()
    {
        QuickSearchOpen = true;
        Notify();
    }

    public void CloseQuickSearch()
    {
        QuickSearchOpen = false;
        Notify();
    }

    public void ToggleTheme()
    {
        ThemeDark = !ThemeDark;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
