using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IReportService
{
    Task<ReportDefinitionDto?> GetByIdAsync(Guid reportDefinitionId, CancellationToken cancellationToken = default);
    Task<PagedResult<ReportDefinitionDto>> SearchDefinitionsAsync(Guid? tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<ReportExecutionDto>> SearchExecutionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
