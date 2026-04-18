namespace Ams.Web.Services;

/// <summary>
/// Scoped service that manages in-app notification items for the current user circuit.
/// Inject and call <see cref="Add"/> from any component or service to push a notification.
/// </summary>
public sealed class NotificationService
{
    /// <summary>A single in-app notification.</summary>
    public record NotificationItem(
        Guid      Id,
        string    Title,
        string?   Body      = null,
        string    Icon      = "bi-bell",
        string    TypeKey   = "info",    // info | success | warning | danger
        string?   ActionUrl = null,
        DateTime? DateUtc   = null,
        bool      IsRead    = false
    );

    private readonly List<NotificationItem> _items = [];

    public IReadOnlyList<NotificationItem> Items      => _items;
    public int                             UnreadCount => _items.Count(n => !n.IsRead);

    public event Action? OnChange;

    /// <summary>Prepend a new notification to the list.</summary>
    public void Add(NotificationItem item)
    {
        _items.Insert(0, item);
        OnChange?.Invoke();
    }

    /// <summary>Mark all notifications as read.</summary>
    public void MarkAllRead()
    {
        for (var i = 0; i < _items.Count; i++)
            _items[i] = _items[i] with { IsRead = true };
        OnChange?.Invoke();
    }

    /// <summary>Remove a single notification by id.</summary>
    public void Remove(Guid id)
    {
        _items.RemoveAll(n => n.Id == id);
        OnChange?.Invoke();
    }
}
