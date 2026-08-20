using Ams.Application.Common.Dtos;
using Ams.Application.Features.NonRenewals;

namespace Ams.Application.Abstractions.Persistence;

public interface INonRenewalRepository
{
    Task<NonRenewalCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<NonRenewalDetailDto?> GetDetailAsync(Guid nonRenewalId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateNonRenewalRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid nonRenewalId, UpdateNonRenewalRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid nonRenewalId, UpdateNonRenewalStatusRequest request, CancellationToken cancellationToken = default);
    Task RecordInsuredNotificationAsync(Guid nonRenewalId, RecordInsuredNotificationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddNonRenewalActivityRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid nonRenewalId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
