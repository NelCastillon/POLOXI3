SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF SCHEMA_ID(N'Agency') IS NULL EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'Agency.Department',N'U') IS NULL
	THROW 51071,N'Agency.Department must exist before Agency.Team can be created.',1;

IF OBJECT_ID(N'Agency.Team',N'U') IS NULL
BEGIN
	CREATE TABLE Agency.Team
	(
		TeamId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_Team PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DepartmentId UNIQUEIDENTIFIER NOT NULL,
		TeamName NVARCHAR(255) NOT NULL,
		TeamCode NVARCHAR(50) NULL,
		Description NVARCHAR(1000) NULL,
		ManagerUserId UNIQUEIDENTIFIER NULL,
		ManagerName NVARCHAR(200) NULL,
		TeamType NVARCHAR(100) NULL,
		MemberCount INT NOT NULL CONSTRAINT DF_Agency_Team_MemberCount DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Agency_Team_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Agency_Team_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Agency_Team_IsDeleted DEFAULT 0,
		CONSTRAINT FK_Agency_Team_Department FOREIGN KEY(DepartmentId) REFERENCES Agency.Department(DepartmentId)
	);
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Agency.Team') AND name=N'IX_Team_TenantId')
	CREATE INDEX IX_Team_TenantId ON Agency.Team(TenantId,IsActive,IsDeleted);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Agency.Team') AND name=N'IX_Team_DepartmentId')
	CREATE INDEX IX_Team_DepartmentId ON Agency.Team(DepartmentId,IsActive,IsDeleted);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Agency.Team') AND name=N'UX_Team_TenantCode')
	CREATE UNIQUE INDEX UX_Team_TenantCode ON Agency.Team(TenantId,TeamCode) WHERE IsDeleted=0 AND TeamCode IS NOT NULL;
