using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Services;

public sealed class LoggingTwoFactorSmsSender : ITwoFactorSmsSender
{
    private readonly ILogger<LoggingTwoFactorSmsSender> _logger;

    public LoggingTwoFactorSmsSender(ILogger<LoggingTwoFactorSmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendCodeAsync(TwoFactorSmsMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SMS 2FA code generated for TenantId {TenantId}, UserId {UserId}, ChallengeId {ChallengeId}, Phone {PhoneNumber}, Expires {ExpiresDateUtc}. Development code: {Code}",
            message.TenantId,
            message.UserId,
            message.ChallengeId,
            MaskPhoneNumber(message.PhoneNumber),
            message.ExpiresDateUtc,
            message.Code);

        return Task.CompletedTask;
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "****" : $"***-***-{digits[^4..]}";
    }
}
