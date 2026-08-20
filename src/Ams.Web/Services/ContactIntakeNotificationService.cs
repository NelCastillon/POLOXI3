using System.Text;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Ams.Application.Features.ContactIntake;

namespace Ams.Web.Services;

public sealed class ContactIntakeNotificationService : IContactIntakeNotificationService
{
    private const string RecipientSettingKey = "Platform.ContactIntakeNotificationRecipientEmail";
    private const string TenantSettingKey = "Platform.ContactIntakeNotificationTenantId";

    private readonly IConfigurationService _configurationService;
    private readonly INotificationDeliveryService _deliveryService;
    private readonly ILogger<ContactIntakeNotificationService> _logger;

    public ContactIntakeNotificationService(
        IConfigurationService configurationService,
        INotificationDeliveryService deliveryService,
        ILogger<ContactIntakeNotificationService> logger)
    {
        _configurationService = configurationService;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    public async Task SendSubmissionNotificationAsync(CreateContactDemoRequest request, ContactDemoSubmissionResult result, ContactDemoRequestContext context, CancellationToken cancellationToken = default)
    {
        var route = await ResolveRouteAsync(cancellationToken);
        if (route is null)
        {
            _logger.LogWarning("Contact intake notification skipped because its database-backed Notification Platform route is incomplete.");
            return;
        }

        await _deliveryService.QueueEmailAsync(new QueueEmailNotificationRequest(
            route.Value.TenantId,
            route.Value.RecipientEmail,
            request.WorkEmail,
            $"AgencyBinder demo request {result.RequestNumber} - {request.AgencyName}",
            BuildBody(request, result, context),
            false,
            "CONTACT_INTAKE",
            "Marketing.ContactDemoRequest",
            result.RequestId,
            $"contact-intake:{result.RequestId:N}",
            "High",
            "ContactIntake",
            null,
            []), cancellationToken);
    }

    private async Task<(Guid TenantId, string RecipientEmail)?> ResolveRouteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recipientSetting = await _configurationService.GetByKeyAsync(RecipientSettingKey, "Platform", null, cancellationToken);
            var tenantSetting = await _configurationService.GetByKeyAsync(TenantSettingKey, "Platform", null, cancellationToken);
            var recipientEmail = recipientSetting?.SettingValue ?? recipientSetting?.DefaultValue;
            var tenantValue = tenantSetting?.SettingValue ?? tenantSetting?.DefaultValue;
            if (Guid.TryParse(tenantValue, out var tenantId) && tenantId != Guid.Empty && !string.IsNullOrWhiteSpace(recipientEmail))
                return (tenantId, recipientEmail.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve the contact intake Notification Platform route from configuration settings.");
        }

        return null;
    }

    private static string BuildBody(CreateContactDemoRequest request, ContactDemoSubmissionResult result, ContactDemoRequestContext context)
    {
        var body = new StringBuilder();
        body.AppendLine("New AgencyBinder enterprise demo request received.");
        body.AppendLine();
        body.AppendLine($"Request Number: {result.RequestNumber}");
        body.AppendLine($"Request Id: {result.RequestId}");
        body.AppendLine($"Submitted UTC: {DateTime.UtcNow:O}");
        body.AppendLine();
        body.AppendLine("Contact Details");
        body.AppendLine($"Name: {request.FirstName} {request.LastName}");
        body.AppendLine($"Business Email: {request.WorkEmail}");
        body.AppendLine($"Phone: {request.Phone ?? "N/A"}");
        body.AppendLine($"Title: {request.Title ?? "N/A"}");
        body.AppendLine($"Agency: {request.AgencyName}");
        body.AppendLine();
        body.AppendLine("Agency Profile");
        body.AppendLine($"User Count: {request.AgencySize}");
        body.AppendLine($"Branch Count: {request.Branches}");
        body.AppendLine($"Business Lines: {request.BusinessLines}");
        body.AppendLine($"Current System: {request.CurrentSystem ?? "N/A"}");
        body.AppendLine();
        body.AppendLine("Solution Priorities");
        body.AppendLine(request.Priorities.Count == 0 ? "N/A" : string.Join(", ", request.Priorities));
        body.AppendLine();
        body.AppendLine("Planning");
        body.AppendLine($"Timeline: {request.Timeline}");
        body.AppendLine($"Budget: {request.Budget}");
        body.AppendLine();
        body.AppendLine("Project Goals / Notes");
        body.AppendLine(request.Message ?? "N/A");
        body.AppendLine();
        body.AppendLine("Request Metadata");
        body.AppendLine($"Origin: {context.Origin ?? "N/A"}");
        body.AppendLine($"Referrer: {context.Referrer ?? "N/A"}");
        body.AppendLine($"Remote IP: {context.RemoteIpAddress ?? "N/A"}");
        body.AppendLine($"User Agent: {context.UserAgent ?? "N/A"}");
        return body.ToString();
    }
}
