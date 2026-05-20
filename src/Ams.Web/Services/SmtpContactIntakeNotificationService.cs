using System.Net;
using System.Net.Mail;
using System.Text;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.ContactIntake;
using Microsoft.Extensions.Options;

namespace Ams.Web.Services;

public sealed class SmtpContactIntakeNotificationService : IContactIntakeNotificationService
{
    private const string RecipientSettingKey = "Platform.ContactIntakeNotificationRecipientEmail";

    private readonly ContactIntakeNotificationOptions _options;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<SmtpContactIntakeNotificationService> _logger;

    public SmtpContactIntakeNotificationService(
        IOptions<ContactIntakeNotificationOptions> options,
        IConfigurationService configurationService,
        ILogger<SmtpContactIntakeNotificationService> logger)
    {
        _options = options.Value;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task SendSubmissionNotificationAsync(CreateContactDemoRequest request, ContactDemoSubmissionResult result, ContactDemoRequestContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        var recipientEmail = await ResolveRecipientEmailAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromEmail) || string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Contact intake notification skipped because SMTP notification settings are incomplete.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = $"AgencyBinder demo request {result.RequestNumber} - {request.AgencyName}",
            Body = BuildBody(request, result, context),
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        message.To.Add(recipientEmail);
        message.ReplyToList.Add(new MailAddress(request.WorkEmail, $"{request.FirstName} {request.LastName}"));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task<string> ResolveRecipientEmailAsync(CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _configurationService.GetByKeyAsync(RecipientSettingKey, "Platform", null, cancellationToken);
            var configuredEmail = setting?.SettingValue ?? setting?.DefaultValue;

            if (!string.IsNullOrWhiteSpace(configuredEmail))
                return configuredEmail.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve contact intake notification recipient from configuration settings. Falling back to application options.");
        }

        return _options.ToEmail.Trim();
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
