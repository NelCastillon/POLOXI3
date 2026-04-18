using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Compliance;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyDocumentService
{
    Task<PolicyDocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PolicyDocumentDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? typeCode, string? statusCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePolicyDocumentRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePolicyDocumentRequest request, CancellationToken ct = default);
    Task<Guid> CreateVersionAsync(Guid id, VersionPolicyDocumentRequest request, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid? publishedByUserId, CancellationToken ct = default);
    Task RetireAsync(Guid id, Guid? retiredByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyDocumentDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyAudienceDto>> GetAudienceAsync(Guid id, CancellationToken ct = default);
    Task<Guid> AddAudienceMemberAsync(Guid id, AddAudienceMemberRequest request, CancellationToken ct = default);
    Task RemoveAudienceMemberAsync(Guid audienceId, CancellationToken ct = default);
}
