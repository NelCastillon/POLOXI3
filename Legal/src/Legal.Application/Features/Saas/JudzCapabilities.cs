namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Frozen capability registry (architecture freeze §2).
//
// These identifiers are the stable contract between the SaaS authorization layer
// and the DB-backed capability registry (SaaS.Platform_Capability, seeded in
// migration 0245). Display names may change; these codes MUST NOT once customers
// exist. Each constant equals the capability's Code column exactly.
//
// This is a typed alias over the database-backed codes — it is NOT a hardcoded
// catalog. The database remains the source of truth for capability metadata
// (entitlement, meter, metered/async flags, RequiresMatter). These constants only
// remove magic strings from call sites and guarantee spelling stability.
// ─────────────────────────────────────────────────────────────────────────────

public static class JudzCapabilities
{
    // Research
    public const string GeneralSearch = "research.search";
    public const string PoloxiResearch = "research.poloxi";

    // Legal
    public const string LegalSearch = "legal.search";
    public const string LegalResearch = "legal.research";
    public const string Matters = "legal.matters";
    public const string LegalDecision = "legal.decision";

    // Math
    public const string MathFormalization = "math.formalization";
    public const string MathSolver = "math.solver";

    // Platform
    public const string Configuration = "platform.configuration";
}
