SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — ASP.NET Core Identity tables (EF Core default schema).
--
-- Created through the existing script migrator so no dotnet-ef runtime tooling is
-- required. Schema matches Microsoft.AspNetCore.Identity.EntityFrameworkCore with
-- IdentityUser<Guid>/IdentityRole<Guid> (UNIQUEIDENTIFIER keys). Identity owns
-- users, passwords, roles, claims, logins and tokens; Judz SaaS tables reference
-- users by their AspNetUsers.Id (UNIQUEIDENTIFIER).
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'dbo.AspNetRoles',N'U') IS NULL
CREATE TABLE dbo.AspNetRoles
(
	Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
	Name             NVARCHAR(256) NULL,
	NormalizedName   NVARCHAR(256) NULL,
	ConcurrencyStamp NVARCHAR(MAX) NULL
);
IF OBJECT_ID(N'RoleNameIndex',N'IX') IS NULL
	CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;

IF OBJECT_ID(N'dbo.AspNetUsers',N'U') IS NULL
CREATE TABLE dbo.AspNetUsers
(
	Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
	FirstName            NVARCHAR(100) NULL,
	LastName             NVARCHAR(100) NULL,
	UserName             NVARCHAR(256) NULL,
	NormalizedUserName   NVARCHAR(256) NULL,
	Email                NVARCHAR(256) NULL,
	NormalizedEmail      NVARCHAR(256) NULL,
	EmailConfirmed       BIT NOT NULL,
	PasswordHash         NVARCHAR(MAX) NULL,
	SecurityStamp        NVARCHAR(MAX) NULL,
	ConcurrencyStamp     NVARCHAR(MAX) NULL,
	PhoneNumber          NVARCHAR(MAX) NULL,
	PhoneNumberConfirmed BIT NOT NULL,
	TwoFactorEnabled     BIT NOT NULL,
	LockoutEnd           DATETIMEOFFSET NULL,
	LockoutEnabled       BIT NOT NULL,
	AccessFailedCount    INT NOT NULL
);
IF OBJECT_ID(N'UserNameIndex',N'IX') IS NULL
	CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
IF OBJECT_ID(N'EmailIndex',N'IX') IS NULL
	CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);

IF OBJECT_ID(N'dbo.AspNetRoleClaims',N'U') IS NULL
CREATE TABLE dbo.AspNetRoleClaims
(
	Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY,
	RoleId     UNIQUEIDENTIFIER NOT NULL,
	ClaimType  NVARCHAR(MAX) NULL,
	ClaimValue NVARCHAR(MAX) NULL,
	CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
);
IF OBJECT_ID(N'IX_AspNetRoleClaims_RoleId',N'IX') IS NULL
	CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);

IF OBJECT_ID(N'dbo.AspNetUserClaims',N'U') IS NULL
CREATE TABLE dbo.AspNetUserClaims
(
	Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
	UserId     UNIQUEIDENTIFIER NOT NULL,
	ClaimType  NVARCHAR(MAX) NULL,
	ClaimValue NVARCHAR(MAX) NULL,
	CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
IF OBJECT_ID(N'IX_AspNetUserClaims_UserId',N'IX') IS NULL
	CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);

IF OBJECT_ID(N'dbo.AspNetUserLogins',N'U') IS NULL
CREATE TABLE dbo.AspNetUserLogins
(
	LoginProvider       NVARCHAR(128) NOT NULL,
	ProviderKey         NVARCHAR(128) NOT NULL,
	ProviderDisplayName NVARCHAR(MAX) NULL,
	UserId              UNIQUEIDENTIFIER NOT NULL,
	CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
	CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
IF OBJECT_ID(N'IX_AspNetUserLogins_UserId',N'IX') IS NULL
	CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);

IF OBJECT_ID(N'dbo.AspNetUserRoles',N'U') IS NULL
CREATE TABLE dbo.AspNetUserRoles
(
	UserId UNIQUEIDENTIFIER NOT NULL,
	RoleId UNIQUEIDENTIFIER NOT NULL,
	CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
	CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
	CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
);
IF OBJECT_ID(N'IX_AspNetUserRoles_RoleId',N'IX') IS NULL
	CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);

IF OBJECT_ID(N'dbo.AspNetUserTokens',N'U') IS NULL
CREATE TABLE dbo.AspNetUserTokens
(
	UserId        UNIQUEIDENTIFIER NOT NULL,
	LoginProvider NVARCHAR(128) NOT NULL,
	Name          NVARCHAR(128) NOT NULL,
	Value         NVARCHAR(MAX) NULL,
	CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
	CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);

COMMIT TRANSACTION;
