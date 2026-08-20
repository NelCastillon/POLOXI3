using System.Text.RegularExpressions;

namespace Ams.Knowledge.Domain.Common;

internal static class KnowledgeGuard
{
    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9._-]*$", RegexOptions.CultureInvariant);
    private static readonly Regex WhitespacePattern = new("\\s+", RegexOptions.CultureInvariant);

    public static string Required(string value, string name, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new KnowledgeDomainException($"{name} is required.");
        if (normalized.Length > maximumLength)
            throw new KnowledgeDomainException($"{name} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    public static string Code(string value, string name, int maximumLength)
    {
        var normalized = Required(value, name, maximumLength).ToUpperInvariant();
        if (!CodePattern.IsMatch(normalized))
            throw new KnowledgeDomainException($"{name} may contain only letters, numbers, periods, underscores, and hyphens.");
        return normalized;
    }

    public static string NormalizedLabel(string value)
    {
        var label = Required(value, "Label", 250).Normalize();
        return WhitespacePattern.Replace(label, " ").Trim().ToUpperInvariant();
    }

    public static void EffectiveDates(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveToUtc.HasValue && effectiveToUtc.Value <= effectiveFromUtc)
            throw new KnowledgeDomainException("EffectiveToUtc must be later than EffectiveFromUtc.");
    }

    public static void TenantScope(Guid? tenantId, bool isSystemDefined)
    {
        if (isSystemDefined && tenantId.HasValue)
            throw new KnowledgeDomainException("System-defined knowledge cannot be owned by a tenant.");
        if (!isSystemDefined && !tenantId.HasValue)
            throw new KnowledgeDomainException("Tenant-defined knowledge requires a tenant identifier.");
    }

    public static void SameScope(Guid? firstTenantId, Guid? secondTenantId, string relationship)
    {
        if (firstTenantId.HasValue && secondTenantId.HasValue && firstTenantId != secondTenantId)
            throw new KnowledgeDomainException($"{relationship} cannot cross tenant boundaries.");
    }

}
