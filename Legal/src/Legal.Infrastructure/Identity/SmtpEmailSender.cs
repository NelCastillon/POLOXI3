using System.Net;
using System.Net.Mail;
using Legal.Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Identity;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — SMTP email sender.
//
// Reads SMTP settings from configuration (Email:Smtp:*). When no host is
// configured (local/dev), it logs the verification code / reset URL instead of
// sending, so signup flows remain testable without a mail server.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IJudzEmailSender
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
        => SendAsync(email, "Verify your Judz.ai account",
            $"Your Judz.ai verification code is {code}. It expires in 10 minutes.", ct);

    public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default)
        => SendAsync(email, "Reset your Judz.ai password",
            $"To reset your password, open this link: {resetUrl}", ct);

    public Task SendInvitationAsync(string email, string tenantName, string roleName, string acceptUrl, CancellationToken ct = default)
        => SendAsync(email, $"You've been invited to {tenantName} on Judz.ai",
            $"You've been invited to join {tenantName} as {roleName}. Accept your invitation here: {acceptUrl}", ct);

    private async Task SendAsync(string email, string subject, string body, CancellationToken ct)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // Dev/log-only fallback (never log real production secrets here).
            logger.LogInformation("[Email:log-only] To={Email} Subject={Subject} Body={Body}", email, subject, body);
            return;
        }

        var port = int.TryParse(configuration["Email:Smtp:Port"], out var p) ? p : 587;
        var enableSsl = !bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var ssl) || ssl;
        var user = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var from = configuration["Email:Smtp:From"] ?? user ?? "no-reply@judz.ai";

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, password);

        using var message = new MailMessage(from, email, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
