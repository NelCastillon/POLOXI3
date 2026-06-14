using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Contacts;

namespace Ams.Application;

public sealed class ContactService : IContactService
{
    private readonly IContactRepository _repository;

    public ContactService(IContactRepository repository) => _repository = repository;

    public Task<Guid> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<ContactDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<ContactDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<ContactDto>> GetByAccountIdAsync(Guid accountId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetByAccountIdAsync(accountId, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyList<ContactWorkflowEventDto>> GetWorkflowEventsAsync(Guid contactId, CancellationToken cancellationToken = default)
        => _repository.GetWorkflowEventsAsync(contactId, cancellationToken);

    public Task<Guid> CreateWorkflowEventAsync(CreateContactWorkflowEventRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateWorkflowEventAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, userId, cancellationToken);
}
