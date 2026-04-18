using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class User : AuditableEntity
{
    public Guid? BranchId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string UserTypeCode { get; private set; } = "Internal";
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public bool MfaEnabled { get; private set; }
    public DateTime? LastLoginDateUtc { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? TimeZoneCode { get; private set; }
    public string? LocaleCode { get; private set; }
    public string? Department { get; private set; }
    public string? JobTitle { get; private set; }
    public DateTime? PasswordChangedDateUtc { get; private set; }
    public bool IsLockedOut { get; private set; }
    public DateTime? LockoutEndDateUtc { get; private set; }
    public int FailedLoginAttempts { get; private set; }

    private User() { }

    public User(Guid tenantId, string userName, string email, string fullName, string userTypeCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        UserName = userName;
        Email = email;
        FullName = fullName;
        UserTypeCode = userTypeCode;
        Status = UserStatus.Active;
    }

    public void UpdateProfile(string fullName, string? displayName, string? phoneNumber,
        string? department, string? jobTitle, string? timeZoneCode, string? localeCode, Guid? modifiedByUserId)
    {
        FullName = fullName;
        DisplayName = displayName;
        PhoneNumber = phoneNumber;
        Department = department;
        JobTitle = jobTitle;
        TimeZoneCode = timeZoneCode;
        LocaleCode = localeCode;
        MarkModified(modifiedByUserId);
    }

    public void RecordLogin(bool success, Guid? modifiedByUserId)
    {
        if (success)
        {
            LastLoginDateUtc = DateTime.UtcNow;
            FailedLoginAttempts = 0;
            IsLockedOut = false;
            LockoutEndDateUtc = null;
        }
        else
        {
            FailedLoginAttempts++;
            if (FailedLoginAttempts >= 5)
            {
                IsLockedOut = true;
                LockoutEndDateUtc = DateTime.UtcNow.AddMinutes(30);
                Status = UserStatus.Locked;
            }
        }
        MarkModified(modifiedByUserId);
    }

    public void LockOut(DateTime? lockoutEndDateUtc, Guid? modifiedByUserId)
    {
        IsLockedOut = true;
        LockoutEndDateUtc = lockoutEndDateUtc;
        Status = UserStatus.Locked;
        MarkModified(modifiedByUserId);
    }

    public void Unlock(Guid? modifiedByUserId)
    {
        IsLockedOut = false;
        LockoutEndDateUtc = null;
        FailedLoginAttempts = 0;
        Status = UserStatus.Active;
        MarkModified(modifiedByUserId);
    }

    public void EnableMfa(Guid? modifiedByUserId) { MfaEnabled = true; MarkModified(modifiedByUserId); }
    public void DisableMfa(Guid? modifiedByUserId) { MfaEnabled = false; MarkModified(modifiedByUserId); }
    public void SetPasswordChanged(Guid? modifiedByUserId) { PasswordChangedDateUtc = DateTime.UtcNow; MarkModified(modifiedByUserId); }
    public void Activate(Guid? modifiedByUserId) { Status = UserStatus.Active; MarkModified(modifiedByUserId); }
    public void Deactivate(Guid? modifiedByUserId) { Status = UserStatus.Inactive; MarkModified(modifiedByUserId); }
    public void Suspend(Guid? modifiedByUserId) { Status = UserStatus.Suspended; MarkModified(modifiedByUserId); }
    public void Disable(Guid? modifiedByUserId) { Status = UserStatus.Disabled; MarkModified(modifiedByUserId); }
    public void Terminate(Guid? modifiedByUserId) { Status = UserStatus.Terminated; IsLockedOut = true; MarkModified(modifiedByUserId); }
}
