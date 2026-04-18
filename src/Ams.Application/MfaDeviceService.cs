using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;

namespace Ams.Application;

public sealed class MfaDeviceService : IMfaDeviceService
{
    private readonly IMfaDeviceRepository _repository;
    public MfaDeviceService(IMfaDeviceRepository repository) => _repository = repository;
    public Task<MfaDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<MfaDeviceDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, userId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<PagedResult<UserMfaStatusDto>> SearchUsersWithMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchUsersWithMfaAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<PagedResult<UserMfaStatusDto>> SearchUsersWithoutMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchUsersWithoutMfaAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<IReadOnlyList<MfaDeviceDto>> GetUserDevicesAsync(Guid userId, CancellationToken cancellationToken = default) => _repository.GetUserDevicesAsync(userId, cancellationToken);
    public Task<Guid> AddMethodAsync(AddMfaMethodRequest request, CancellationToken cancellationToken = default) => _repository.AddMethodAsync(request, cancellationToken);
    public Task VerifyMethodAsync(VerifyMfaMethodRequest request, CancellationToken cancellationToken = default) => _repository.VerifyMethodAsync(request, cancellationToken);
    public Task DisableMethodAsync(DisableMfaMethodRequest request, CancellationToken cancellationToken = default) => _repository.DisableMethodAsync(request, cancellationToken);
    public Task ResetMfaAsync(ResetMfaRequest request, CancellationToken cancellationToken = default) => _repository.ResetMfaAsync(request, cancellationToken);
    public Task RequireMfaAsync(RequireMfaRequest request, CancellationToken cancellationToken = default) => _repository.RequireMfaAsync(request, cancellationToken);
}
