using Ams.Application.Features.ContactIntake;

namespace Ams.Application.Abstractions.Services;

public interface IContactIntakeService
{
    Task<ContactDemoSubmissionResult> SubmitDemoRequestAsync(CreateContactDemoRequest request, ContactDemoRequestContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactIntakeOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);
}
