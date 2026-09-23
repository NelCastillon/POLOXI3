namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Configuration control-plane contracts (freeze §3–§22).
//
// Configuration defines HOW Judz operates. It never grants access: entitlement,
// permission, policy, and resource access remain the gates. These types are a
// typed, metadata-driven layer over the DB-backed registry (SaaS.Config_*). The
// database stays the source of truth; ConfigurationDefinition mirrors a
// SaaS.Config_Definition row.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>The stored value type of a configuration key.</summary>
public enum ConfigurationValueType
{
    String,
    Integer,
    Boolean,
    Decimal,
    Enum,
    Json,
    SecretReference
}

/// <summary>Sensitivity classification (freeze §26). Secret values live in a secret store, not config tables.</summary>
public enum ConfigurationSensitivity
{
    Public,
    Internal,
    Sensitive,
    Secret
}

/// <summary>Scope from which an effective value was resolved (freeze §21/§22).</summary>
public enum ConfigurationScope
{
    CodeDefault,
    Platform,
    Plan,
    Tenant,
    Matter,
    Execution
}

/// <summary>Optional validation constraints declared by a definition (freeze §19).</summary>
public sealed record ConfigurationValidation
{
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
}

/// <summary>Authoritative definition of a configurable key (mirrors SaaS.Config_Definition).</summary>
public sealed record ConfigurationDefinition
{
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required ConfigurationValueType ValueType { get; init; }
    public required string DefaultValueJson { get; init; }
    public ConfigurationScope OwnerScope { get; init; } = ConfigurationScope.Platform;
    public bool TenantConfigurable { get; init; }
    public bool MatterConfigurable { get; init; }
    public bool ExecutionConfigurable { get; init; }
    public bool TenantCanOverride { get; init; } = true;
    public ConfigurationSensitivity Sensitivity { get; init; } = ConfigurationSensitivity.Public;
    public bool RequiresRestart { get; init; }
    public string? RequiredPermission { get; init; }
    public ConfigurationValidation? Validation { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Context that scopes a configuration resolution request (freeze §21).</summary>
public sealed record ConfigurationContext(
    Guid? TenantId,
    Guid? MatterId,
    Guid? UserId,
    string? Environment);

/// <summary>Resolved configuration value plus provenance for enterprise UX (freeze §21).</summary>
public sealed record ResolvedConfiguration<T>(
    T Value,
    ConfigurationScope Source,
    bool Overridden,
    bool CanOverride);

/// <summary>Stable, strongly-typed configuration keys (freeze §4). These are a typed alias over the
/// DB-backed registry; the keys are contracts and MUST NOT be renamed once customers exist.</summary>
public static class JudzConfigurationKeys
{
    public static class General
    {
        public const string DefaultTimeZone = "general.default_timezone";
        public const string DefaultCulture = "general.default_culture";
        public const string ResultsPageSize = "general.results_page_size";
    }

    public static class Research
    {
        public const string DefaultMode = "research.default_mode";
        public const string MaxSources = "research.max_sources";
        public const string RequireVerification = "research.require_verification";
    }

    public static class Legal
    {
        public const string DefaultJurisdiction = "legal.default_jurisdiction";
        public const string DefaultState = "legal.default_state";
        public const string RequireHumanReview = "legal.require_human_review";
    }

    public static class Math
    {
        public const string DefaultSolverMode = "math.default_solver_mode";
        public const string RunFormalizationAuto = "math.run_formalization_auto";
    }
}

// ── UI read model ────────────────────────────────────────────────────────────

/// <summary>A single configuration key surfaced to the tenant admin UI: the definition
/// metadata plus the currently effective value and its provenance.</summary>
public sealed record TenantConfigurationItemDto
{
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string ValueType { get; init; }
    public required string DefaultValueJson { get; init; }
    /// <summary>Effective value JSON after precedence resolution.</summary>
    public string? EffectiveValueJson { get; init; }
    /// <summary>Tenant override value JSON, or null when the tenant has not overridden.</summary>
    public string? TenantValueJson { get; init; }
    /// <summary>Scope the effective value came from: CodeDefault | Platform | Tenant.</summary>
    public required string Source { get; init; }
    public bool IsOverridden { get; init; }
    public bool CanOverride { get; init; }
    public bool Sensitive { get; init; }
    public string? RequiredPermission { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Configuration items grouped by category for the tenant admin UI.</summary>
public sealed record TenantConfigurationCategoryDto(string Category, IReadOnlyList<TenantConfigurationItemDto> Items);

/// <summary>Request to set (or clear) a tenant-scoped configuration value.</summary>
public sealed record SetTenantConfigurationRequest(string Key, string? ValueJson);

// ── Platform (Super Admin) read model ─────────────────────────────────────────

/// <summary>A single configuration key surfaced to the platform (Super Admin) UI:
/// the definition metadata plus the currently effective platform value and its provenance.
/// Editing here sets the platform default that all tenants inherit unless they override.</summary>
public sealed record PlatformConfigurationItemDto
{
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string ValueType { get; init; }
    public required string DefaultValueJson { get; init; }
    /// <summary>Effective platform value JSON (platform value, else code default).</summary>
    public string? EffectiveValueJson { get; init; }
    /// <summary>Platform override value JSON, or null when only the code default applies.</summary>
    public string? PlatformValueJson { get; init; }
    /// <summary>Scope the effective value came from: CodeDefault | Platform.</summary>
    public required string Source { get; init; }
    public bool IsOverridden { get; init; }
    /// <summary>Whether tenants are permitted to override this platform value.</summary>
    public bool TenantCanOverride { get; init; }
    public bool Sensitive { get; init; }
    public string? RequiredPermission { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Configuration items grouped by category for the platform admin UI.</summary>
public sealed record PlatformConfigurationCategoryDto(string Category, IReadOnlyList<PlatformConfigurationItemDto> Items);

/// <summary>Request to set (or clear) a platform-scoped configuration value.</summary>
public sealed record SetPlatformConfigurationRequest(string Key, string? ValueJson);
