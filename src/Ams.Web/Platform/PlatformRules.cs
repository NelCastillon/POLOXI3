namespace Ams.Web.Platform;

/// <summary>
/// Compile-time constants that encode the Core Platform Foundation rules.
/// Reference these anywhere a rule must be enforced or surfaced in the UI.
/// </summary>
public static class PlatformRules
{
    // ── Tenant lifecycle ──────────────────────────────────────────────────────

    /// <summary>
    /// Rule: No hard-delete for tenant records.
    /// Tenants are transitioned to Terminated status; physical removal requires
    /// an explicit retention-period workflow outside normal UI flows.
    /// </summary>
    public const string NoHardDelete =
        "Tenant records are never hard-deleted. Use status transitions (Suspend → Terminate).";

    /// <summary>
    /// Rule: Every tenant mutation is audited.
    /// All create / update / status-change API calls must emit an audit event.
    /// The UI confirmation dialogs surface this expectation to operators.
    /// </summary>
    public const string AuditAllChanges =
        "Every change to a tenant record must be recorded in the audit log.";

    // ── High-risk action gates ────────────────────────────────────────────────

    /// <summary>
    /// Rule: High-risk actions require explicit operator confirmation.
    /// Use <c>AppConfirmActionDialog</c> with Variant="danger" or "warning"
    /// for Suspend, Terminate, Region Change, Isolation Change, and Provisioning.
    /// </summary>
    public const string ConfirmHighRiskActions =
        "Suspend, Terminate, Region Change, Isolation Change, and Provisioning require confirmation.";

    /// <summary>Status transitions that are considered high-risk and require confirmation.</summary>
    public static readonly IReadOnlySet<string> HighRiskActions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Suspend",
            "Terminate",
            "Activate",
            "ChangeRegion",
            "ChangeIsolation",
            "StartProvisioning",
            "ResetProvisioning",
        };

    // ── Always-visible fields ─────────────────────────────────────────────────

    /// <summary>
    /// Rule: Region, Plan, and Isolation Mode must always be visible on tenant surfaces.
    /// These fields must appear in the stats bar, overview tab, and every summary card
    /// that represents a tenant.
    /// </summary>
    public const string AlwaysVisibleFields =
        "Region, Plan, and Isolation Mode must be visible on every tenant summary surface.";

    /// <summary>The three fields that are always surfaced on every tenant view.</summary>
    public static readonly IReadOnlyList<string> RequiredVisibleFields =
    [
        "RegionCode",
        "PlanCode",
        "IsolationMode",
    ];

    // ── Provisioning ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rule: Provisioning is async and restartable.
    /// The provisioning wizard submits a job; the UI polls or receives a push
    /// notification for completion. A failed provisioning can be retried from
    /// the Tenant Detail page without re-entering all wizard data.
    /// </summary>
    public const string ProvisioningAsyncRestartable =
        "Provisioning is an async background job. The wizard submits intent; the tenant detail page shows job status and allows retry.";

    /// <summary>Provisioning statuses that allow a restart / retry action.</summary>
    public static readonly IReadOnlySet<string> RestartableProvisioningStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Failed",
            "PartiallyProvisioned",
            "Pending",
        };
}
