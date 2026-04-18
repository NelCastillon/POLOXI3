using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _repository;

    public ReportService(IReportRepository repository)
        => _repository = repository;

    public Task<ReportDefinitionDto?> GetByIdAsync(Guid reportDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(reportDefinitionId, cancellationToken);

    public Task<PagedResult<ReportDefinitionDto>> SearchDefinitionsAsync(Guid? tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchDefinitionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<ReportExecutionDto>> SearchExecutionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchExecutionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
