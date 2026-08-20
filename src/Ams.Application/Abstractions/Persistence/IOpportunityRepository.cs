using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;

namespace Ams.Application.Abstractions.Persistence;

public interface IOpportunityRepository
{
    Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default);
    Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OpportunityDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OpportunityConversionLaunchDto?> GetConversionLaunchAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<OpportunityCompetitorLookupDto>> SearchCompetitorLookupsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken cancellationToken = default);
    Task<OpportunityStageUpdateResult> UpdateStageAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertLineAsync(UpsertOpportunityLineRequest request, CancellationToken cancellationToken = default);
    Task SetPrimaryLineAsync(Guid opportunityId, Guid opportunityLineId, Guid? userId, CancellationToken cancellationToken = default);
    Task DeleteLineAsync(Guid opportunityLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertActivityAsync(UpsertOpportunityActivityRequest request, CancellationToken cancellationToken = default);
    Task DeleteActivityAsync(Guid activityId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertSubmissionAsync(UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken = default);
    Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertCompetitorAsync(UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default);
    Task DeleteCompetitorAsync(Guid competitorId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
