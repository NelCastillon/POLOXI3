using Ams.Application.Features.ContactIntake;

namespace Ams.Application.Abstractions.Persistence;

public interface IContactIntakeRepository
{
    Task<ContactDemoSubmissionResult> CreateDemoRequestAsync(CreateContactDemoRequest request, ContactDemoRequestContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactIntakeOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);
}
