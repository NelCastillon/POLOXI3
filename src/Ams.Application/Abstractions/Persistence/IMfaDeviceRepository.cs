using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;

namespace Ams.Application.Abstractions.Persistence;

public interface IMfaDeviceRepository
{
    Task<MfaDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<MfaDeviceDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<UserMfaStatusDto>> SearchUsersWithMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<UserMfaStatusDto>> SearchUsersWithoutMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MfaDeviceDto>> GetUserDevicesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> AddMethodAsync(AddMfaMethodRequest request, CancellationToken cancellationToken = default);
    Task VerifyMethodAsync(VerifyMfaMethodRequest request, CancellationToken cancellationToken = default);
    Task DisableMethodAsync(DisableMfaMethodRequest request, CancellationToken cancellationToken = default);
    Task ResetMfaAsync(ResetMfaRequest request, CancellationToken cancellationToken = default);
    Task RequireMfaAsync(RequireMfaRequest request, CancellationToken cancellationToken = default);
}
