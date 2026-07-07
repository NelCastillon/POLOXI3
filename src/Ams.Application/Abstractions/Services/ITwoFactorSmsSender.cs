using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface ITwoFactorSmsSender
{
    Task SendCodeAsync(TwoFactorSmsMessage message, CancellationToken cancellationToken = default);
}
