using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;

namespace Legal.Infrastructure.Intelligence;

// Adapts the full IIntelligenceWideRepository to the narrow ILegalGroundingConfigurationSource so the
// live citator provider only depends on the configuration read it actually needs.
public sealed class LegalGroundingConfigurationSource(IIntelligenceWideRepository wideRepository) : ILegalGroundingConfigurationSource
{
    public Task<WideLegalGroundingConfiguration> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => wideRepository.GetLegalGroundingConfigurationAsync(tenantId, cancellationToken);
}
