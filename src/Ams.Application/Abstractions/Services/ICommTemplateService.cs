using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;

namespace Ams.Application.Abstractions.Services;

public interface ICommTemplateService
{
    Task<IReadOnlyList<CommTemplateDto>> GetByTenantAsync(Guid tenantId, string? channel = null, string? category = null, string? status = null, CancellationToken cancellationToken = default);
    Task<CommTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommTemplateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateCommTemplateRequest request, CancellationToken cancellationToken = default);
    Task IncrementUsageAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid templateId, CancellationToken cancellationToken = default);
}
