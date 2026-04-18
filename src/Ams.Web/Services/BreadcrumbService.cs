namespace Ams.Web.Services;

/// <summary>
/// Scoped service that holds the current page breadcrumb trail.
/// Pages call <see cref="SetCrumbs"/> from <c>OnInitialized</c>;
/// layout components subscribe to <see cref="OnChange"/> and call StateHasChanged.
/// </summary>
public sealed class BreadcrumbService
{
    /// <summary>A single segment of the breadcrumb trail.</summary>
    public record BreadcrumbItem(string Label, string? Url = null, string? Icon = null);

    private IReadOnlyList<BreadcrumbItem> _crumbs = [];

    public IReadOnlyList<BreadcrumbItem> Crumbs => _crumbs;

    public event Action? OnChange;

    /// <summary>Set the breadcrumb trail for the current page.  Call from OnInitialized.</summary>
    public void SetCrumbs(params BreadcrumbItem[] crumbs)
    {
        _crumbs = crumbs;
        OnChange?.Invoke();
    }

    /// <summary>Remove all crumbs (e.g. when navigating to a root page).</summary>
    public void Clear()
    {
        _crumbs = [];
        OnChange?.Invoke();
    }
}
