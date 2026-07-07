using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IAuthService
{
    Task<LoginValidationResultDto?> ValidateCredentialsAsync(Guid tenantId, string userNameOrEmail, string password, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthenticatedUserDto?> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<Guid> RegisterLoginUserAsync(RegisterLoginUserRequest request, CancellationToken cancellationToken = default);
}
