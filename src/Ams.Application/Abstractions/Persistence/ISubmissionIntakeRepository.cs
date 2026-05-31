using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Application.Abstractions.Persistence;

public interface ISubmissionIntakeRepository
{
    Task<PagedResult<SubmissionIntakeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? source, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<SubmissionIntakeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSubmissionIntakeRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the outcome of normalization: links the intake to the resulting account,
    /// opportunity, and submission and sets the intake status to Processed.
    /// </summary>
    Task MarkPromotedAsync(Guid id, int matchScore, Guid matchedAccountId, Guid accountId, Guid opportunityId, Guid submissionId, Guid? processedByUserId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
}
