using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;

namespace Ams.Application;

public sealed class CommTemplateService : ICommTemplateService
{
    private readonly ICommTemplateRepository _repository;
    public CommTemplateService(ICommTemplateRepository repository) => _repository = repository;

    public Task<IReadOnlyList<CommTemplateDto>> GetByTenantAsync(Guid tenantId, string? channel = null, string? category = null, string? status = null, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, channel, category, status, cancellationToken);

    public Task<CommTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(templateId, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommTemplateRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(UpdateCommTemplateRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task IncrementUsageAsync(Guid templateId, CancellationToken cancellationToken = default)
        => _repository.IncrementUsageAsync(templateId, cancellationToken);

    public Task DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(templateId, cancellationToken);
}
