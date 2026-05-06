using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Agency;

namespace Ams.Application;

public sealed class AgencySetupService : IAgencySetupService
{
    private readonly IAgencySetupRepository _repository;

    public AgencySetupService(IAgencySetupRepository repository) => _repository = repository;

    public Task<PagedResult<AgencyDepartmentDto>> SearchDepartmentsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchDepartmentsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateDepartmentAsync(CreateAgencyDepartmentRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateDepartmentAsync(request, cancellationToken);

    public Task UpdateDepartmentAsync(Guid id, UpdateAgencyDepartmentRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateDepartmentAsync(id, request, cancellationToken);

    public Task<PagedResult<AgencyTeamDto>> SearchTeamsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchTeamsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateTeamAsync(CreateAgencyTeamRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateTeamAsync(request, cancellationToken);

    public Task UpdateTeamAsync(Guid id, UpdateAgencyTeamRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateTeamAsync(id, request, cancellationToken);

    public Task<PagedResult<AgencyStaffDto>> SearchStaffAsync(Guid tenantId, string role, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchStaffAsync(tenantId, role, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> UpsertStaffAsync(Guid? staffId, UpsertAgencyStaffRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertStaffAsync(staffId, request, cancellationToken);
}
