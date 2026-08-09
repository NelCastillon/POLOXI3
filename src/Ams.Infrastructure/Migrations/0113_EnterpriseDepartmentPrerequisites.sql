SET NOCOUNT ON;
SET XACT_ABORT ON;

IF SCHEMA_ID(N'Agency') IS NULL EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'Agency.Branch',N'U') IS NULL
BEGIN
	CREATE TABLE Agency.Branch
	(
		BranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_Branch PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BranchName NVARCHAR(255) NOT NULL,
		BranchCode NVARCHAR(50) NOT NULL,
		BranchType NVARCHAR(100) NULL,
		StreetAddress NVARCHAR(255) NOT NULL,
		City NVARCHAR(100) NOT NULL,
		State NVARCHAR(50) NOT NULL,
		ZipCode NVARCHAR(10) NOT NULL,
		Country NVARCHAR(100) NULL CONSTRAINT DF_Agency_Branch_Country DEFAULT N'United States',
		Phone NVARCHAR(20) NULL,
		Fax NVARCHAR(20) NULL,
		Email NVARCHAR(200) NULL,
		ManagerUserId UNIQUEIDENTIFIER NULL,
		ManagerName NVARCHAR(200) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Agency_Branch_IsActive DEFAULT 1,
		IsHeadquarters BIT NOT NULL CONSTRAINT DF_Agency_Branch_IsHeadquarters DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Agency_Branch_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Agency_Branch_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_Branch_TenantId ON Agency.Branch(TenantId,IsActive,IsDeleted);
	CREATE UNIQUE INDEX UX_Branch_TenantCode ON Agency.Branch(TenantId,BranchCode) WHERE IsDeleted=0;
END;

IF OBJECT_ID(N'Agency.Department',N'U') IS NULL
BEGIN
	CREATE TABLE Agency.Department
	(
		DepartmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_Department PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BranchId UNIQUEIDENTIFIER NOT NULL,
		DepartmentName NVARCHAR(255) NOT NULL,
		DepartmentCode NVARCHAR(50) NULL,
		Description NVARCHAR(1000) NULL,
		ManagerUserId UNIQUEIDENTIFIER NULL,
		ManagerName NVARCHAR(200) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Agency_Department_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Agency_Department_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Agency_Department_IsDeleted DEFAULT 0,
		CONSTRAINT FK_Agency_Department_Branch FOREIGN KEY(BranchId) REFERENCES Agency.Branch(BranchId)
	);
	CREATE INDEX IX_Department_TenantId ON Agency.Department(TenantId,IsActive,IsDeleted);
	CREATE INDEX IX_Department_BranchId ON Agency.Department(BranchId,IsActive,IsDeleted);
	CREATE UNIQUE INDEX UX_Department_TenantCode ON Agency.Department(TenantId,DepartmentCode) WHERE IsDeleted=0 AND DepartmentCode IS NOT NULL;
END;

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
	CREATE INDEX IX_Team_TenantId ON Agency.Team(TenantId,IsActive,IsDeleted);
	CREATE INDEX IX_Team_DepartmentId ON Agency.Team(DepartmentId,IsActive,IsDeleted);
	CREATE UNIQUE INDEX UX_Team_TenantCode ON Agency.Team(TenantId,TeamCode) WHERE IsDeleted=0 AND TeamCode IS NOT NULL;
END;
