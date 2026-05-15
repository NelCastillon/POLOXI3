using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IAuthRepository
{
    Task<LoginCredentialDto?> GetLoginCredentialAsync(Guid tenantId, string userNameOrEmail, CancellationToken cancellationToken = default);
    Task<Guid> RegisterLoginUserAsync(RegisterLoginUserRequest request, string passwordHash, string passwordSalt, CancellationToken cancellationToken = default);
    Task RecordLoginAttemptAsync(Guid tenantId, Guid? userId, string userName, string? ipAddress, string? userAgent, bool isSuccessful, string? failureReason, CancellationToken cancellationToken = default);
    Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordLoginFailureAsync(Guid userId, int maxFailedAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken = default);
}
