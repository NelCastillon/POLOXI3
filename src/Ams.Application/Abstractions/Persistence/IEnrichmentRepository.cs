using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enrichment;

namespace Ams.Application.Abstractions.Persistence;

public interface IEnrichmentRepository
{
    Task<EnrichmentWorkspaceDto> GetWorkspaceAsync(EnrichmentSearchRequest request, CancellationToken cancellationToken = default);

    Task ConfigureProviderAsync(Guid providerId, EnrichmentProviderConfigRequest request, CancellationToken cancellationToken = default);

    Task SetProviderStatusAsync(Guid providerId, EnrichmentProviderStatusRequest request, CancellationToken cancellationToken = default);

    Task<EnrichmentJobDto> RunAsync(EnrichmentRunRequest request, CancellationToken cancellationToken = default);
}
