using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using Ams.Api.Controllers;
using Ams.Infrastructure.Persistence.Repositories;

namespace Ams.Application.Abstractions.Services;

/// <summary>
/// Extended interface for opportunity operations
/// </summary>
public interface IOpportunityServiceExtended : IOpportunityService
{
    /// <summary>
    /// Get opportunities for board view
    /// </summary>
    Task<List<OpportunityBoardDto>> GetBoardViewAsync(Guid tenantId, string? ownerFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get opportunities for pipeline view  
    /// </summary>
    Task<List<OpportunityPipelineDto>> GetPipelineViewAsync(Guid tenantId, string timeFilter, string stageFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update opportunity stage
    /// </summary>
    Task<bool> UpdateOpportunityStageAsync(Guid opportunityId, string stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pipeline metrics and analytics
    /// </summary>
    Task<PipelineMetricsDto> GetPipelineMetricsAsync(Guid tenantId, string timeFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get opportunity stages for tenant
    /// </summary>
    Task<List<OpportunityStageDto>> GetStagesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get forecast categories for tenant
    /// </summary>
    Task<List<ForecastCategoryDto>> GetForecastCategoriesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
