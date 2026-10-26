using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Intelligence;

// Narrow read-only accessor for the tenant's DB-backed legal-grounding configuration (CourtListener base
// URL/token, timeouts, enablement). Exists so components that only need the configuration (e.g. the live
// citator provider) do not take a dependency on the full IIntelligenceWideRepository surface, which keeps
// them unit-testable with a trivial stub. Backed by the same Core.ConfigurationSetting rows.
public interface ILegalGroundingConfigurationSource
{
    Task<WideLegalGroundingConfiguration> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
