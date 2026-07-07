using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class LoginCredentialDto
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? LockoutEndDateUtc { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginDateUtc { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? PasswordSalt { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SmsPhoneNumber { get; set; }
    public string? AssignedRoleCodes { get; set; }
    public string? AssignedRoleNames { get; set; }
    public string? EffectivePermissionCodes { get; set; }
}

public sealed class LoginValidationResultDto
{
    public bool RequiresTwoFactor { get; set; }
    public AuthenticatedUserDto? User { get; set; }
    public TwoFactorChallengeDto? Challenge { get; set; }
}

public class TwoFactorChallengeDto
{
    public Guid ChallengeId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string DeliveryMethodCode { get; set; } = "SMS";
    public string DestinationMasked { get; set; } = string.Empty;
    public DateTime ExpiresDateUtc { get; set; }
    public int MaxAttemptCount { get; set; }
}

public sealed class TwoFactorChallengeRecordDto : TwoFactorChallengeDto
{
    public string CodeHash { get; set; } = string.Empty;
    public string CodeSalt { get; set; } = string.Empty;
    public DateTime? ConsumedDateUtc { get; set; }
    public int AttemptCount { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool MfaEnabled { get; set; }
    public string? AssignedRoleCodes { get; set; }
    public string? EffectivePermissionCodes { get; set; }
}

public sealed class VerifyTwoFactorRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid ChallengeId { get; set; }

    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public sealed class TwoFactorSmsMessage
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ChallengeId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresDateUtc { get; set; }
}

public sealed class AuthenticatedUserDto
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool MfaEnabled { get; set; }
    public IReadOnlyList<string> RoleCodes { get; set; } = [];
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}

public sealed class RegisterLoginUserRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(300)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DisplayName { get; set; }

    [Phone]
    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Department { get; set; }

    [StringLength(100)]
    public string? JobTitle { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }

    public bool RequireMfa { get; set; } = true;

    public Guid? CreatedByUserId { get; set; }
}
