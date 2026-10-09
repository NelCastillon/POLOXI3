namespace Legal.Application.Abstractions.Persistence;

// A single curated statute-text record served from POLOXI.Legal_AuthorityStatuteText when a live web
// authority source is blocked/unreachable. Carries the provenance (SourceUrl/SourceLabel/VerifiedDateUtc)
// required so an admitted curated snippet has the same verifiable attribution as live-retrieved evidence.
public sealed record CuratedStatuteText(
    string SectionNumber,
    string StatuteText,
    string SourceUrl,
    string SourceLabel,
    DateTime VerifiedDateUtc);

// DB-backed curated statute-text store (POLOXI.Legal_AuthorityStatuteText). Proxy-free fallback used by
// the official-authority retriever for descriptors whose ExtractionStrategyCode is CURATED_STORE. The
// store is data-driven: statute coverage is extended by inserting rows, never by code changes.
public interface ILegalCuratedStatuteStore
{
    // Resolve the curated text for a provider + jurisdiction + statute section. Prefers a tenant-scoped
    // row over the platform (TenantId NULL) row. Returns null when the section is not curated.
    Task<CuratedStatuteText?> GetAsync(Guid tenantId,string providerCode,string jurisdictionCode,string sectionNumber,CancellationToken cancellationToken=default);
}
