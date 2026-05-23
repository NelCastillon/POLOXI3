using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enrichment;

namespace Ams.Application;

public sealed class EnrichmentService : IEnrichmentService
{
    private readonly IEnrichmentRepository _repository;

    public EnrichmentService(IEnrichmentRepository repository)
    {
        _repository = repository;
    }

    public Task<EnrichmentWorkspaceDto> GetWorkspaceAsync(EnrichmentSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.GetWorkspaceAsync(request, cancellationToken);

    public Task ConfigureProviderAsync(Guid providerId, EnrichmentProviderConfigRequest request, CancellationToken cancellationToken = default)
        => _repository.ConfigureProviderAsync(providerId, request, cancellationToken);

    public Task SetProviderStatusAsync(Guid providerId, EnrichmentProviderStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.SetProviderStatusAsync(providerId, request, cancellationToken);

    public Task<EnrichmentJobDto> RunAsync(EnrichmentRunRequest request, CancellationToken cancellationToken = default)
        => _repository.RunAsync(request, cancellationToken);
}
