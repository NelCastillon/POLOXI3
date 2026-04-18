using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Compliance;

namespace Ams.Application;

public sealed class PolicyDocumentService : IPolicyDocumentService
{
    private readonly IPolicyDocumentRepository _repository;

    public PolicyDocumentService(IPolicyDocumentRepository repository)
    {
        _repository = repository;
    }

    public Task<PolicyDocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repository.GetByIdAsync(id, ct);

    public Task<PagedResult<PolicyDocumentDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? typeCode, string? statusCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _repository.SearchAsync(tenantId, searchTerm, typeCode, statusCode, isActive, pageNumber, pageSize, ct);

    public Task<Guid> CreateAsync(CreatePolicyDocumentRequest request, CancellationToken ct = default)
        => _repository.CreateAsync(request, ct);

    public Task UpdateAsync(Guid id, UpdatePolicyDocumentRequest request, CancellationToken ct = default)
        => _repository.UpdateAsync(id, request, ct);

    public Task<Guid> CreateVersionAsync(Guid id, VersionPolicyDocumentRequest request, CancellationToken ct = default)
        => _repository.CreateVersionAsync(id, request, ct);

    public Task PublishAsync(Guid id, Guid? publishedByUserId, CancellationToken ct = default)
        => _repository.PublishAsync(id, publishedByUserId, ct);

    public Task RetireAsync(Guid id, Guid? retiredByUserId, CancellationToken ct = default)
        => _repository.RetireAsync(id, retiredByUserId, ct);

    public Task<IReadOnlyList<PolicyAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CancellationToken ct = default)
        => _repository.GetAcknowledgementsAsync(id, ct);

    public Task<IReadOnlyList<PolicyDocumentDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default)
        => _repository.GetVersionHistoryAsync(id, ct);

    public Task<IReadOnlyList<PolicyAudienceDto>> GetAudienceAsync(Guid id, CancellationToken ct = default)
        => _repository.GetAudienceAsync(id, ct);

    public Task<Guid> AddAudienceMemberAsync(Guid id, AddAudienceMemberRequest request, CancellationToken ct = default)
        => _repository.AddAudienceMemberAsync(id, request, ct);

    public Task RemoveAudienceMemberAsync(Guid audienceId, CancellationToken ct = default)
        => _repository.RemoveAudienceMemberAsync(audienceId, ct);
}
