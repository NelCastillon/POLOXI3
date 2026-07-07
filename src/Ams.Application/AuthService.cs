using System.Security.Cryptography;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class AuthService : IAuthService
{
    private const int PasswordHashBytes = 32;
    private const int PasswordSaltBytes = 16;
    private const int PasswordIterations = 210_000;
    private const int TwoFactorCodeDigits = 6;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TwoFactorChallengeDuration = TimeSpan.FromMinutes(10);

    private readonly IAuthRepository _repository;
    private readonly ITwoFactorSmsSender _smsSender;

    public AuthService(IAuthRepository repository, ITwoFactorSmsSender smsSender)
    {
        _repository = repository;
        _smsSender = smsSender;
    }

    public async Task<LoginValidationResultDto?> ValidateCredentialsAsync(Guid tenantId, string userNameOrEmail, string password, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var userName = userNameOrEmail.Trim();
        var credential = await _repository.GetLoginCredentialAsync(tenantId, userName, cancellationToken);

        if (credential is null)
        {
            await _repository.RecordLoginAttemptAsync(tenantId, null, userName, ipAddress, userAgent, false, "InvalidCredentials", cancellationToken);
            return null;
        }

        if (!string.Equals(credential.StatusCode, "Active", StringComparison.OrdinalIgnoreCase) || credential.IsLockedOut)
        {
            await _repository.RecordLoginAttemptAsync(tenantId, credential.UserId, userName, ipAddress, userAgent, false, "AccountUnavailable", cancellationToken);
            return null;
        }

        if (!VerifyPassword(password, credential.PasswordHash, credential.PasswordSalt))
        {
            await _repository.RecordLoginFailureAsync(credential.UserId, MaxFailedAttempts, LockoutDuration, cancellationToken);
            await _repository.RecordLoginAttemptAsync(tenantId, credential.UserId, userName, ipAddress, userAgent, false, "InvalidCredentials", cancellationToken);
            return null;
        }

        if (credential.MfaEnabled)
        {
            var phoneNumber = ResolveTwoFactorPhoneNumber(credential);
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                await _repository.RecordLoginAttemptAsync(tenantId, credential.UserId, userName, ipAddress, userAgent, false, "TwoFactorPhoneMissing", cancellationToken);
                return null;
            }

            var code = GenerateTwoFactorCode();
            var salt = RandomNumberGenerator.GetBytes(PasswordSaltBytes);
            var expiresDateUtc = DateTime.UtcNow.Add(TwoFactorChallengeDuration);
            var challenge = await _repository.CreateTwoFactorChallengeAsync(
                tenantId,
                credential.UserId,
                MaskPhoneNumber(phoneNumber),
                Convert.ToBase64String(HashPassword(code, salt)),
                Convert.ToBase64String(salt),
                expiresDateUtc,
                ipAddress,
                userAgent,
                cancellationToken);

            await _smsSender.SendCodeAsync(new TwoFactorSmsMessage
            {
                TenantId = tenantId,
                UserId = credential.UserId,
                ChallengeId = challenge.ChallengeId,
                PhoneNumber = phoneNumber,
                Code = code,
                ExpiresDateUtc = expiresDateUtc
            }, cancellationToken);

            await _repository.RecordLoginAttemptAsync(tenantId, credential.UserId, userName, ipAddress, userAgent, false, "TwoFactorRequired", cancellationToken);

            return new LoginValidationResultDto
            {
                RequiresTwoFactor = true,
                Challenge = challenge
            };
        }

        await _repository.RecordLoginSuccessAsync(credential.UserId, cancellationToken);
        await _repository.RecordLoginAttemptAsync(tenantId, credential.UserId, userName, ipAddress, userAgent, true, null, cancellationToken);

        return new LoginValidationResultDto
        {
            RequiresTwoFactor = false,
            User = CreateAuthenticatedUser(credential)
        };
    }

    public async Task<AuthenticatedUserDto?> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var challenge = await _repository.GetTwoFactorChallengeAsync(request.TenantId, request.ChallengeId, cancellationToken);
        if (challenge is null)
        {
            await _repository.RecordLoginAttemptAsync(request.TenantId, null, request.ChallengeId.ToString(), ipAddress, userAgent, false, "TwoFactorChallengeNotFound", cancellationToken);
            return null;
        }

        if (challenge.ConsumedDateUtc.HasValue)
        {
            await _repository.RecordLoginAttemptAsync(challenge.TenantId, challenge.UserId, challenge.UserName, ipAddress, userAgent, false, "TwoFactorChallengeConsumed", cancellationToken);
            return null;
        }

        if (challenge.ExpiresDateUtc <= DateTime.UtcNow)
        {
            await _repository.RecordTwoFactorFailureAsync(challenge.ChallengeId, "Expired", cancellationToken);
            await _repository.RecordLoginAttemptAsync(challenge.TenantId, challenge.UserId, challenge.UserName, ipAddress, userAgent, false, "TwoFactorExpired", cancellationToken);
            return null;
        }

        if (challenge.AttemptCount >= challenge.MaxAttemptCount)
        {
            await _repository.RecordLoginAttemptAsync(challenge.TenantId, challenge.UserId, challenge.UserName, ipAddress, userAgent, false, "TwoFactorAttemptsExceeded", cancellationToken);
            return null;
        }

        if (!VerifyPassword(request.Code.Trim(), challenge.CodeHash, challenge.CodeSalt))
        {
            await _repository.RecordTwoFactorFailureAsync(challenge.ChallengeId, "InvalidCode", cancellationToken);
            await _repository.RecordLoginAttemptAsync(challenge.TenantId, challenge.UserId, challenge.UserName, ipAddress, userAgent, false, "TwoFactorInvalidCode", cancellationToken);
            return null;
        }

        await _repository.ConsumeTwoFactorChallengeAsync(challenge.ChallengeId, cancellationToken);
        await _repository.RecordLoginSuccessAsync(challenge.UserId, cancellationToken);
        await _repository.RecordLoginAttemptAsync(challenge.TenantId, challenge.UserId, challenge.UserName, ipAddress, userAgent, true, null, cancellationToken);

        return new AuthenticatedUserDto
        {
            UserId = challenge.UserId,
            TenantId = challenge.TenantId,
            UserName = challenge.UserName,
            Email = challenge.Email,
            FullName = challenge.FullName,
            DisplayName = challenge.DisplayName,
            MfaEnabled = challenge.MfaEnabled,
            RoleCodes = SplitCsv(challenge.AssignedRoleCodes),
            PermissionCodes = SplitCsv(challenge.EffectivePermissionCodes)
        };
    }

    private static AuthenticatedUserDto CreateAuthenticatedUser(LoginCredentialDto credential) =>
        new()
        {
            UserId = credential.UserId,
            TenantId = credential.TenantId,
            UserName = credential.UserName,
            Email = credential.Email,
            FullName = credential.FullName,
            DisplayName = credential.DisplayName,
            MfaEnabled = credential.MfaEnabled,
            RoleCodes = SplitCsv(credential.AssignedRoleCodes),
            PermissionCodes = SplitCsv(credential.EffectivePermissionCodes)
        };

    public Task<Guid> RegisterLoginUserAsync(RegisterLoginUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRegistration(request);
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltBytes);
        var hash = HashPassword(request.Password, salt);
        return _repository.RegisterLoginUserAsync(request, Convert.ToBase64String(hash), Convert.ToBase64String(salt), cancellationToken);
    }

    private static void ValidateRegistration(RegisterLoginUserRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        if (request.RoleId == Guid.Empty)
        {
            throw new InvalidOperationException("A role is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Full name, username, and email are required.");
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password confirmation does not match.");
        }

        if (!IsStrongPassword(request.Password))
        {
            throw new InvalidOperationException("Password must be at least 12 characters and include upper case, lower case, number, and symbol characters.");
        }
    }

    private static bool VerifyPassword(string password, string passwordHash, string? passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(passwordSalt))
        {
            return false;
        }

        var salt = Convert.FromBase64String(passwordSalt);
        var expected = Convert.FromBase64String(passwordHash);
        var actual = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashBytes);

    private static string GenerateTwoFactorCode()
    {
        var min = (int)Math.Pow(10, TwoFactorCodeDigits - 1);
        var max = (int)Math.Pow(10, TwoFactorCodeDigits);
        return RandomNumberGenerator.GetInt32(min, max).ToString();
    }

    private static string? ResolveTwoFactorPhoneNumber(LoginCredentialDto credential) =>
        !string.IsNullOrWhiteSpace(credential.SmsPhoneNumber)
            ? credential.SmsPhoneNumber.Trim()
            : string.IsNullOrWhiteSpace(credential.PhoneNumber) ? null : credential.PhoneNumber.Trim();

    private static string MaskPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "****" : $"***-***-{digits[^4..]}";
    }

    private static bool IsStrongPassword(string password) =>
        password.Length >= 12 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(ch => !char.IsLetterOrDigit(ch));

    private static IReadOnlyList<string> SplitCsv(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
