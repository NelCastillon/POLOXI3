using Legal.Application.Features.Intelligence.Citator;

namespace Legal.Application.Abstractions.Intelligence;

// Live (external) citator provider. Supplies advisory good-law treatment signals for cited authorities
// from an external legal data source (e.g. CourtListener later-citing opinions). This is ADVISORY only
// and NEVER authoritative: it supplements the DB-backed tenant treatments merged in CitatorService.
//
// Contract guarantees every implementation MUST honour:
//  - FAIL-SOFT: any provider/transport/timeout error returns an empty result; it never throws and never
//    blocks the validity computation. Absence of a provider signal leaves the authority as "Unknown".
//  - NO FABRICATION: the provider only reports treatments it can actually evidence from the source. It
//    must not invent a "Good law" status; when it cannot verify an authority it simply omits it.
//  - PROVENANCE: every returned treatment carries SourceProviderCode identifying the live provider so the
//    UI can show that the signal is externally sourced rather than tenant-authored.
public interface ICitatorProvider
{
    // Returns zero or more advisory treatments per requested authority identity. Authorities the provider
    // cannot evidence are simply absent from the dictionary (they remain Unknown). Keyed by the SAME
    // authority identity string that was requested (case-insensitive), so results merge cleanly with the
    // DB-backed treatments in CitatorService.
    Task<IReadOnlyDictionary<string, IReadOnlyList<AuthorityTreatmentDto>>> GetTreatmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> authorityIdentities,
        CancellationToken cancellationToken = default);
}
