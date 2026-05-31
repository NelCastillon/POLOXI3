namespace Ams.Application.Common.Guards;

/// <summary>
/// Centralizes the enterprise rule that workflow records must never be orphaned and must
/// always be owned by a parent within the same tenant. Services use these helpers instead of
/// duplicating inline validation so the guardrails stay consistent across the application layer.
/// </summary>
public static class TenantGuard
{
    /// <summary>
    /// Validates that a required parent was supplied, exists, and belongs to the same tenant.
    /// Returns the resolved parent so callers can run any additional relationship checks.
    /// </summary>
    public static async Task<TParent> EnsureParentAsync<TParent>(
        Guid parentId,
        Guid tenantId,
        Func<Guid, CancellationToken, Task<TParent?>> fetchAsync,
        Func<TParent, Guid> tenantSelector,
        string parentLabel,
        string childLabel,
        CancellationToken cancellationToken = default)
        where TParent : class
    {
        ArgumentNullException.ThrowIfNull(fetchAsync);
        ArgumentNullException.ThrowIfNull(tenantSelector);

        if (parentId == Guid.Empty)
        {
            throw new InvalidOperationException($"{Article(childLabel)} {childLabel} requires a parent {parentLabel}. {parentLabel}Id was not supplied.");
        }

        var parent = await fetchAsync(parentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Parent {parentLabel.ToLowerInvariant()} '{parentId}' was not found.");

        if (tenantId != Guid.Empty && tenantSelector(parent) != tenantId)
        {
            throw new InvalidOperationException($"Parent {parentLabel.ToLowerInvariant()} belongs to a different tenant and cannot own this {childLabel.ToLowerInvariant()}.");
        }

        return parent;
    }

    /// <summary>
    /// Validates that an optional parent, when supplied, exists and belongs to the same tenant.
    /// Returns the resolved parent (or <c>null</c> when none was supplied) so callers can run any
    /// additional relationship checks.
    /// </summary>
    public static async Task<TParent?> EnsureOptionalParentAsync<TParent>(
        Guid? parentId,
        Guid tenantId,
        Func<Guid, CancellationToken, Task<TParent?>> fetchAsync,
        Func<TParent, Guid> tenantSelector,
        string parentLabel,
        string childLabel,
        CancellationToken cancellationToken = default)
        where TParent : class
    {
        ArgumentNullException.ThrowIfNull(fetchAsync);
        ArgumentNullException.ThrowIfNull(tenantSelector);

        if (!parentId.HasValue || parentId.Value == Guid.Empty)
        {
            return null;
        }

        var parent = await fetchAsync(parentId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{parentLabel} '{parentId.Value}' was not found.");

        if (tenantId != Guid.Empty && tenantSelector(parent) != tenantId)
        {
            throw new InvalidOperationException($"{parentLabel} belongs to a different tenant and cannot be linked to this {childLabel.ToLowerInvariant()}.");
        }

        return parent;
    }

    private static string Article(string word)
        => !string.IsNullOrEmpty(word) && "AEIOU".IndexOf(char.ToUpperInvariant(word[0])) >= 0 ? "An" : "A";
}
