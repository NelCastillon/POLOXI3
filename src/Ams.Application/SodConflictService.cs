using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;

namespace Ams.Application;

public sealed class SodConflictService : ISodConflictService
{
    private readonly ISodConflictRepository _repository;

    public SodConflictService(ISodConflictRepository repository)
    {
        _repository = repository;
    }

    public Task<SodConflictDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repository.GetByIdAsync(id, ct);

    public Task<PagedResult<SodConflictDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? statusCode, string? severityCode, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, severityCode, pageNumber, pageSize, ct);

    public Task AssignReviewerAsync(Guid id, AssignSodConflictReviewerRequest request, CancellationToken ct = default)
        => _repository.AssignReviewerAsync(id, request, ct);

    public Task RemediateAsync(Guid id, RemediateSodConflictRequest request, CancellationToken ct = default)
        => _repository.RemediateAsync(id, request, ct);

    public Task ResolveAsync(Guid id, ResolveSodConflictRequest request, CancellationToken ct = default)
        => _repository.ResolveAsync(id, request, ct);

    public Task CreateExceptionAsync(Guid id, CreateSodExceptionRequest request, CancellationToken ct = default)
        => _repository.CreateExceptionAsync(id, request, ct);
}
