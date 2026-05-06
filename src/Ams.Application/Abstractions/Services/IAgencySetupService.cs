using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Agency;

namespace Ams.Application.Abstractions.Services;

public interface IAgencySetupService
{
    Task<PagedResult<AgencyDepartmentDto>> SearchDepartmentsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateDepartmentAsync(CreateAgencyDepartmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateDepartmentAsync(Guid id, UpdateAgencyDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AgencyTeamDto>> SearchTeamsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateTeamAsync(CreateAgencyTeamRequest request, CancellationToken cancellationToken = default);
    Task UpdateTeamAsync(Guid id, UpdateAgencyTeamRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AgencyStaffDto>> SearchStaffAsync(Guid tenantId, string role, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> UpsertStaffAsync(Guid? staffId, UpsertAgencyStaffRequest request, CancellationToken cancellationToken = default);
}
