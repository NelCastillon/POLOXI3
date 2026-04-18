using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class AccountSegmentService : IAccountSegmentService
{
    private readonly IAccountSegmentRepository _repository;

    public AccountSegmentService(IAccountSegmentRepository repository) => _repository = repository;

    public Task<AccountSegmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AccountSegmentDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);
}
