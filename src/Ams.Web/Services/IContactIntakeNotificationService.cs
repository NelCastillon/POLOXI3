using Ams.Application.Features.ContactIntake;

namespace Ams.Web.Services;

public interface IContactIntakeNotificationService
{
    Task SendSubmissionNotificationAsync(CreateContactDemoRequest request, ContactDemoSubmissionResult result, ContactDemoRequestContext context, CancellationToken cancellationToken = default);
}
