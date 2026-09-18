namespace Legal.Application.Abstractions.Services;

// Ambient tenant for resolving DB-backed EpistemicAuthoritySettings per request scope. Hosts (API/Web)
// provide an implementation that reads the authenticated tenant claim; the infrastructure default
// returns null so resolution falls back to Platform defaults + code defaults.
public interface IEpistemicTenantAccessor
{
    Guid? TenantId { get; }
}
