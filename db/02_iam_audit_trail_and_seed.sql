-- ============================================================
-- IAM AUDIT TRAIL AND ENTERPRISE SEED DATA
-- ============================================================

-- ============================================================
-- USER AUDIT TRAIL TABLE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserAuditTrail'))
CREATE TABLE IAM.UserAuditTrail (
    AuditTrailId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NOT NULL,
    ActionCode          NVARCHAR(100)    NOT NULL,      -- 'LOGIN', 'LOGOUT', 'ROLE_ASSIGNED', 'ROLE_REMOVED', 'PERMISSION_GRANTED', 'PERMISSION_REVOKED', 'PASSWORD_CHANGED', 'MFA_ENABLED', 'MFA_DISABLED', 'ACCOUNT_LOCKED', 'ACCOUNT_UNLOCKED'
    ActionDescription   NVARCHAR(500)    NULL,
    OldValue            NVARCHAR(MAX)    NULL,          -- JSON format for previous value
    NewValue            NVARCHAR(MAX)    NULL,          -- JSON format for new value
    ChangedByUserId     UNIQUEIDENTIFIER NULL,          -- Who made the change (null for system actions)
    IpAddress           NVARCHAR(50)     NULL,
    UserAgent           NVARCHAR(500)    NULL,
    SessionId           NVARCHAR(200)    NULL,
    StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Success', -- Success, Failed, Attempted
    ErrorDetails        NVARCHAR(MAX)    NULL,          -- Error message if failed
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT IX_UserAuditTrail_User UNIQUE (AuditTrailId)
);

-- Create indexes for faster queries
CREATE NONCLUSTERED INDEX IX_UserAuditTrail_UserId ON IAM.UserAuditTrail(UserId, CreatedDateUtc DESC);
CREATE NONCLUSTERED INDEX IX_UserAuditTrail_TenantId ON IAM.UserAuditTrail(TenantId, CreatedDateUtc DESC);
CREATE NONCLUSTERED INDEX IX_UserAuditTrail_ActionCode ON IAM.UserAuditTrail(ActionCode, CreatedDateUtc DESC);

-- ============================================================
-- LOGIN ATTEMPT TRACKING TABLE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.LoginAttempt'))
CREATE TABLE IAM.LoginAttempt (
    LoginAttemptId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NULL,          -- Null if user not found
    UserName            NVARCHAR(200)    NOT NULL,
    LastName            NVARCHAR(200)    NULL,
    IpAddress           NVARCHAR(50)     NOT NULL,
    UserAgent           NVARCHAR(500)    NULL,
    IsSuccessful        BIT              NOT NULL DEFAULT 0,
    FailureReason       NVARCHAR(500)    NULL,          -- 'InvalidCredentials', 'AccountLocked', 'AccountInactive', 'MFARequired', 'MFAFailed'
    AttemptDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT IX_LoginAttempt_Unique UNIQUE (LoginAttemptId)
);

CREATE NONCLUSTERED INDEX IX_LoginAttempt_UserId ON IAM.LoginAttempt(UserId, AttemptDateUtc DESC);
CREATE NONCLUSTERED INDEX IX_LoginAttempt_UserName ON IAM.LoginAttempt(UserName, AttemptDateUtc DESC);

-- ============================================================
-- SEED DATA - PERMISSION ACTIONS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM IAM.PermissionAction)
BEGIN
    INSERT INTO IAM.PermissionAction (ActionName, Description) VALUES
        ('Manage', 'Full management rights (create, update, delete)'),
        ('View', 'View/read-only access'),
        ('Export', 'Export data'),
        ('Approve', 'Approve requests'),
        ('Lock', 'Lock/unlock accounts'),
        ('Settings', 'Manage system settings');
END

-- ============================================================
-- SEED DATA - PERMISSIONS
-- ============================================================

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @ManagerUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000003';
DECLARE @UserUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000004';

-- Seed Permissions (if not exists)
IF NOT EXISTS (SELECT 1 FROM IAM.Permission WHERE PermissionCode = 'USER_MANAGE')
BEGIN
    INSERT INTO IAM.Permission (PermissionId, PermissionCode, PermissionName, ModuleCode, Description, PermissionActionId) VALUES
        (NEWID(), 'USER_MANAGE', 'Manage Users', 'IAM', 'Create, update, delete users', 1),
        (NEWID(), 'USER_VIEW', 'View Users', 'IAM', 'View user information', 2),
        (NEWID(), 'ROLE_MANAGE', 'Manage Roles', 'IAM', 'Create, update, delete roles', 1),
        (NEWID(), 'ROLE_VIEW', 'View Roles', 'IAM', 'View role information', 2),
        (NEWID(), 'PERMISSION_MANAGE', 'Manage Permissions', 'IAM', 'Manage permissions', 1),
        (NEWID(), 'AUDIT_VIEW', 'View Audit Trails', 'IAM', 'View audit trails and logs', 2),
        (NEWID(), 'AUDIT_EXPORT', 'Export Audit Logs', 'IAM', 'Export audit logs', 3),
        (NEWID(), 'MFA_MANAGE', 'Manage MFA', 'IAM', 'Manage multi-factor authentication', 1),
        (NEWID(), 'LOCK_MANAGE', 'Manage Locks', 'IAM', 'Lock/unlock user accounts', 5),
        (NEWID(), 'SECURITY_POLICY_MANAGE', 'Manage Security Policies', 'IAM', 'Manage security policies', 1),
        (NEWID(), 'ACCESS_REQUEST_APPROVE', 'Approve Access Requests', 'IAM', 'Approve access requests', 4),
        (NEWID(), 'TENANT_MANAGE', 'Manage Tenants', 'Platform', 'Manage tenants', 1),
        (NEWID(), 'REPORT_VIEW', 'View Reports', 'Reports', 'View reports', 2),
        (NEWID(), 'REPORT_EXPORT', 'Export Reports', 'Reports', 'Export reports', 3),
        (NEWID(), 'SETTINGS_MANAGE', 'Manage Settings', 'Platform', 'Manage system settings', 6);
END

-- ============================================================
-- SEED DATA - ROLES
-- ============================================================

DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @ManagerRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @UserRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';
DECLARE @ViewerRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000004';

IF NOT EXISTS (SELECT 1 FROM IAM.Role WHERE RoleCode = 'SYSTEM_ADMIN')
BEGIN
    INSERT INTO IAM.Role (RoleId, TenantId, RoleCode, RoleName, RoleTypeCode, Description, SortOrder, IsBuiltIn, IsSystemRole, IsActive, CreatedDateUtc) VALUES
        (@AdminRoleId, @TenantId, 'SYSTEM_ADMIN', 'System Administrator', 'Internal', 'Full system access with all permissions', 10, 1, 1, 1, SYSUTCDATETIME()),
        (@ManagerRoleId, @TenantId, 'MANAGER', 'Manager', 'Internal', 'Departmental manager with supervisory permissions', 20, 0, 0, 1, SYSUTCDATETIME()),
        (@UserRoleId, @TenantId, 'USER', 'Standard User', 'Internal', 'Regular user with standard permissions', 30, 0, 0, 1, SYSUTCDATETIME()),
        (@ViewerRoleId, @TenantId, 'VIEWER', 'Viewer', 'Internal', 'Read-only access to system', 40, 0, 0, 1, SYSUTCDATETIME());
END

-- ============================================================
-- SEED DATA - ROLE PERMISSIONS
-- ============================================================

-- SYSTEM_ADMIN gets all permissions
IF NOT EXISTS (SELECT 1 FROM IAM.RolePermission WHERE RoleId = @AdminRoleId AND PermissionCode = 'USER_MANAGE')
BEGIN
    INSERT INTO IAM.RolePermission (RolePermissionId, RoleId, PermissionCode, PermissionId) VALUES
        (NEWID(), @AdminRoleId, 'USER_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'USER_MANAGE')),
        (NEWID(), @AdminRoleId, 'USER_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'USER_VIEW')),
        (NEWID(), @AdminRoleId, 'ROLE_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ROLE_MANAGE')),
        (NEWID(), @AdminRoleId, 'ROLE_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ROLE_VIEW')),
        (NEWID(), @AdminRoleId, 'PERMISSION_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'PERMISSION_MANAGE')),
        (NEWID(), @AdminRoleId, 'AUDIT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'AUDIT_VIEW')),
        (NEWID(), @AdminRoleId, 'AUDIT_EXPORT', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'AUDIT_EXPORT')),
        (NEWID(), @AdminRoleId, 'MFA_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'MFA_MANAGE')),
        (NEWID(), @AdminRoleId, 'LOCK_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'LOCK_MANAGE')),
        (NEWID(), @AdminRoleId, 'SECURITY_POLICY_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'SECURITY_POLICY_MANAGE')),
        (NEWID(), @AdminRoleId, 'ACCESS_REQUEST_APPROVE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ACCESS_REQUEST_APPROVE')),
        (NEWID(), @AdminRoleId, 'TENANT_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'TENANT_MANAGE')),
        (NEWID(), @AdminRoleId, 'REPORT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'REPORT_VIEW')),
        (NEWID(), @AdminRoleId, 'REPORT_EXPORT', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'REPORT_EXPORT')),
        (NEWID(), @AdminRoleId, 'SETTINGS_MANAGE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'SETTINGS_MANAGE'));
END

-- MANAGER permissions
IF NOT EXISTS (SELECT 1 FROM IAM.RolePermission WHERE RoleId = @ManagerRoleId AND PermissionCode = 'USER_VIEW')
BEGIN
    INSERT INTO IAM.RolePermission (RolePermissionId, RoleId, PermissionCode, PermissionId) VALUES
        (NEWID(), @ManagerRoleId, 'USER_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'USER_VIEW')),
        (NEWID(), @ManagerRoleId, 'ROLE_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ROLE_VIEW')),
        (NEWID(), @ManagerRoleId, 'AUDIT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'AUDIT_VIEW')),
        (NEWID(), @ManagerRoleId, 'REPORT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'REPORT_VIEW')),
        (NEWID(), @ManagerRoleId, 'ACCESS_REQUEST_APPROVE', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ACCESS_REQUEST_APPROVE'));
END

-- USER permissions
IF NOT EXISTS (SELECT 1 FROM IAM.RolePermission WHERE RoleId = @UserRoleId AND PermissionCode = 'USER_VIEW')
BEGIN
    INSERT INTO IAM.RolePermission (RolePermissionId, RoleId, PermissionCode, PermissionId) VALUES
        (NEWID(), @UserRoleId, 'USER_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'USER_VIEW')),
        (NEWID(), @UserRoleId, 'ROLE_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ROLE_VIEW')),
        (NEWID(), @UserRoleId, 'REPORT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'REPORT_VIEW'));
END

-- VIEWER permissions (read-only)
IF NOT EXISTS (SELECT 1 FROM IAM.RolePermission WHERE RoleId = @ViewerRoleId AND PermissionCode = 'USER_VIEW')
BEGIN
    INSERT INTO IAM.RolePermission (RolePermissionId, RoleId, PermissionCode, PermissionId) VALUES
        (NEWID(), @ViewerRoleId, 'USER_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'USER_VIEW')),
        (NEWID(), @ViewerRoleId, 'ROLE_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'ROLE_VIEW')),
        (NEWID(), @ViewerRoleId, 'REPORT_VIEW', (SELECT PermissionId FROM IAM.Permission WHERE PermissionCode = 'REPORT_VIEW'));
END

-- ============================================================
-- SEED DATA - USERS
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM IAM.[User] WHERE UserName = 'admin@enterprise.com')
BEGIN
    INSERT INTO IAM.[User] (UserId, TenantId, UserName, Email, FullName, FirstName, LastName, UserTypeCode, StatusCode, MfaEnabled, CreatedDateUtc) VALUES
        (@AdminUserId, @TenantId, 'admin@enterprise.com', 'admin@enterprise.com', 'System Administrator', 'System', 'Administrator', 'Internal', 'Active', 1, SYSUTCDATETIME()),
        (@ManagerUserId, @TenantId, 'john.manager@enterprise.com', 'john.manager@enterprise.com', 'John Manager', 'John', 'Manager', 'Internal', 'Active', 1, SYSUTCDATETIME()),
        (@UserUserId, @TenantId, 'sarah.user@enterprise.com', 'sarah.user@enterprise.com', 'Sarah User', 'Sarah', 'User', 'Internal', 'Active', 0, SYSUTCDATETIME()),
        (NEWID(), @TenantId, 'viewer@enterprise.com', 'viewer@enterprise.com', 'View Only User', 'View', 'Only User', 'Internal', 'Active', 0, SYSUTCDATETIME()),
        (NEWID(), @TenantId, 'michael.sales@enterprise.com', 'michael.sales@enterprise.com', 'Michael Sales', 'Michael', 'Sales', 'Internal', 'Active', 0, SYSUTCDATETIME()),
        (NEWID(), @TenantId, 'emily.finance@enterprise.com', 'emily.finance@enterprise.com', 'Emily Finance', 'Emily', 'Finance', 'Internal', 'Active', 1, SYSUTCDATETIME()),
        (NEWID(), @TenantId, 'robert.ops@enterprise.com', 'robert.ops@enterprise.com', 'Robert Operations', 'Robert', 'Operations', 'Internal', 'Inactive', 0, SYSUTCDATETIME());
END

-- ============================================================
-- SEED DATA - USER ROLES
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM IAM.UserRole WHERE UserId = @AdminUserId)
BEGIN
    INSERT INTO IAM.UserRole (UserRoleId, TenantId, UserId, RoleId, AssignedDateUtc, AssignedByUserId) VALUES
        (NEWID(), @TenantId, @AdminUserId, @AdminRoleId, SYSUTCDATETIME(), NULL),
        (NEWID(), @TenantId, @ManagerUserId, @ManagerRoleId, DATEADD(DAY, -30, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @UserUserId, @UserRoleId, DATEADD(DAY, -60, SYSUTCDATETIME()), @AdminUserId);

    -- Viewer and other users
    DECLARE @ViewerUserId UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'viewer@enterprise.com');
    DECLARE @SalesUserId UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'michael.sales@enterprise.com');
    DECLARE @FinanceUserId UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'emily.finance@enterprise.com');
    DECLARE @OpsUserId UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'robert.ops@enterprise.com');

    INSERT INTO IAM.UserRole (UserRoleId, TenantId, UserId, RoleId, AssignedDateUtc, AssignedByUserId) VALUES
        (NEWID(), @TenantId, @ViewerUserId, @ViewerRoleId, DATEADD(DAY, -45, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @SalesUserId, @UserRoleId, DATEADD(DAY, -50, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @FinanceUserId, @ManagerRoleId, DATEADD(DAY, -25, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @OpsUserId, @UserRoleId, DATEADD(DAY, -35, SYSUTCDATETIME()), @ManagerUserId);
END

-- ============================================================
-- SEED DATA - USER AUDIT TRAIL (Sample historical data)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM IAM.UserAuditTrail WHERE UserId = @AdminUserId)
BEGIN
    INSERT INTO IAM.UserAuditTrail (
        AuditTrailId, TenantId, UserId, ActionCode, ActionDescription, OldValue, NewValue, ChangedByUserId, IpAddress, UserAgent, SessionId, StatusCode, ErrorDetails, CreatedDateUtc
    ) VALUES
        (NEWID(), @TenantId, @AdminUserId, 'LOGIN', 'Admin logged in', NULL, NULL, NULL, '192.168.1.100', 'Mozilla/5.0 Chrome 91.0', NULL, 'Success', NULL, DATEADD(DAY, -5, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @AdminUserId, 'ROLE_ASSIGNED', 'Assigned SYSTEM_ADMIN role', NULL, NULL, NULL, NULL, NULL, NULL, 'Success', NULL, DATEADD(DAY, -90, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @AdminUserId, 'MFA_ENABLED', 'Multi-factor authentication enabled', NULL, NULL, @AdminUserId, '192.168.1.100', 'Mozilla/5.0 Chrome 91.0', NULL, 'Success', NULL, DATEADD(DAY, -45, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @AdminUserId, 'LOGIN', 'Admin logged in', NULL, NULL, NULL, '192.168.1.100', 'Mozilla/5.0 Chrome 91.0', NULL, 'Success', NULL, DATEADD(DAY, -4, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @AdminUserId, 'LOGIN', 'Admin logged in', NULL, NULL, NULL, '192.168.1.105', 'Mozilla/5.0 Safari 15.0', NULL, 'Success', NULL, DATEADD(DAY, -2, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'LOGIN', 'Manager logged in', NULL, NULL, NULL, '192.168.1.110', 'Mozilla/5.0 Firefox 88.0', NULL, 'Success', NULL, DATEADD(DAY, -3, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'ROLE_ASSIGNED', 'Assigned MANAGER role by admin', NULL, NULL, @AdminUserId, NULL, NULL, NULL, 'Success', NULL, DATEADD(DAY, -30, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'MFA_ENABLED', 'Multi-factor authentication enabled', NULL, NULL, @ManagerUserId, '192.168.1.110', 'Mozilla/5.0 Firefox 88.0', NULL, 'Success', NULL, DATEADD(DAY, -20, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'LOGIN', 'Manager logged in', NULL, NULL, NULL, '192.168.1.110', 'Mozilla/5.0 Firefox 88.0', NULL, 'Success', NULL, DATEADD(DAY, -1, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'PERMISSION_GRANTED', 'Granted ACCESS_REQUEST_APPROVE permission', NULL, NULL, @AdminUserId, NULL, NULL, NULL, 'Success', NULL, DATEADD(DAY, -28, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'LOGIN', 'Standard user logged in', NULL, NULL, NULL, '192.168.1.120', 'Mozilla/5.0 Edge 91.0', NULL, 'Success', NULL, DATEADD(DAY, -6, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'ROLE_ASSIGNED', 'Assigned USER role by admin', NULL, NULL, @AdminUserId, NULL, NULL, NULL, 'Success', NULL, DATEADD(DAY, -60, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'PASSWORD_CHANGED', 'User changed their password', NULL, NULL, @UserUserId, '192.168.1.120', 'Mozilla/5.0 Edge 91.0', NULL, 'Success', NULL, DATEADD(DAY, -15, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'LOGIN', 'Standard user logged in', NULL, NULL, NULL, '192.168.1.120', 'Mozilla/5.0 Edge 91.0', NULL, 'Success', NULL, DATEADD(DAY, -3, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'LOGIN', 'Standard user logged in', NULL, NULL, NULL, '192.168.1.120', 'Mozilla/5.0 Edge 91.0', NULL, 'Success', NULL, DATEADD(HOUR, -18, SYSUTCDATETIME()));
END

-- ============================================================
-- SEED DATA - LOGIN ATTEMPTS (Sample historical data)
-- ============================================================

-- SEED DATA - LOGIN ATTEMPTS
IF NOT EXISTS (SELECT 1 FROM IAM.LoginAttempt WHERE UserName = 'admin@enterprise.com')
BEGIN
    INSERT INTO IAM.LoginAttempt (LoginAttemptId, TenantId, UserId, UserName, LastName, IpAddress, UserAgent, IsSuccessful, FailureReason, AttemptDateUtc) VALUES
        (NEWID(), @TenantId, @AdminUserId, 'admin@enterprise.com', 'Administrator', '192.168.1.100', 'Mozilla/5.0 Chrome 91.0', 1, NULL, DATEADD(DAY, -5, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @ManagerUserId, 'john.manager@enterprise.com', 'Manager', '192.168.1.110', 'Mozilla/5.0 Firefox 88.0', 0, 'InvalidCredentials', DATEADD(DAY, -3, SYSUTCDATETIME())),
        (NEWID(), @TenantId, @UserUserId, 'sarah.user@enterprise.com', 'User', '192.168.1.120', 'Mozilla/5.0 Edge 90.0', 0, 'AccountLocked', DATEADD(DAY, -2, SYSUTCDATETIME()));
END

PRINT 'IAM Audit Trail and Seed Data inserted successfully!'
