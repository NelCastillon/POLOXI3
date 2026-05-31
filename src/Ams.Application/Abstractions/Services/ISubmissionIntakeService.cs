using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Application.Abstractions.Services;

/// <summary>
/// Direct submission intake orchestration. Stages out-of-band submissions
/// (email, portal, API, producer upload, carrier request, walk-in) and normalizes
/// them into the mandatory Account -> Opportunity -> Submission chain so no
/// submission is ever orphaned without an Account context.
/// </summary>
public interface ISubmissionIntakeService
{
    Task<PagedResult<SubmissionIntakeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? source, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<SubmissionIntakeDto?> GetAsync(Guid intakeId, CancellationToken cancellationToken = default);

    Task<Guid> CaptureAsync(CreateSubmissionIntakeRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid intakeId, UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Runs the Account Match engine against the staged intake without promoting it.</summary>
    Task<Features.Accounts.AccountMatchResult> PreviewMatchAsync(Guid intakeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes a staged intake into the enterprise model: match or create a Prospect Account,
    /// create the Opportunity, then create the Submission, and record the linkage on the intake.
    /// </summary>
    Task<PromoteSubmissionIntakeResult> PromoteAsync(Guid intakeId, PromoteSubmissionIntakeRequest request, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(Guid intakeId, UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid intakeId, Guid? userId = null, CancellationToken cancellationToken = default);
}
