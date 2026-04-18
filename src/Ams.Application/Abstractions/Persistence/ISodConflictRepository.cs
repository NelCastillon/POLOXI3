using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;

namespace Ams.Application.Abstractions.Persistence;

public interface ISodConflictRepository
{
    Task<SodConflictDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<SodConflictDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? statusCode, string? severityCode, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default);
    Task AssignReviewerAsync(Guid id, AssignSodConflictReviewerRequest request, CancellationToken ct = default);
    Task RemediateAsync(Guid id, RemediateSodConflictRequest request, CancellationToken ct = default);
    Task ResolveAsync(Guid id, ResolveSodConflictRequest request, CancellationToken ct = default);
    Task CreateExceptionAsync(Guid id, CreateSodExceptionRequest request, CancellationToken ct = default);
}
