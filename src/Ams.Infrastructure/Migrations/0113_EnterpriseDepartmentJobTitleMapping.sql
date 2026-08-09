SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'Agency.Department',N'U') IS NULL OR OBJECT_ID(N'IAM.JobTitle',N'U') IS NULL
	THROW 51070,N'Agency.Department and IAM.JobTitle must exist before department/title normalization.',1;

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

INSERT Agency.Branch(BranchId,TenantId,BranchName,BranchCode,StreetAddress,City,State,ZipCode,Country,IsActive,IsHeadquarters,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,N'Headquarters',CONCAT(N'HQ-',LEFT(REPLACE(CONVERT(NVARCHAR(36),tenant.TenantId),N'-',N''),8)),N'N/A',N'N/A',N'N/A',N'N/A',N'United States',1,1,SYSUTCDATETIME(),0
FROM Core.Tenant tenant
WHERE tenant.IsDeleted=0
  AND NOT EXISTS(SELECT 1 FROM Agency.Branch branch WHERE branch.TenantId=tenant.TenantId AND branch.IsDeleted=0);

DECLARE @DepartmentCatalog TABLE(DepartmentCode NVARCHAR(50),DepartmentName NVARCHAR(255),Description NVARCHAR(1000),SortOrder INT);
INSERT @DepartmentCatalog VALUES
(N'EXECUTIVE',N'Executive Leadership',N'Enterprise strategy, governance, and executive management.',10),
(N'LEADERSHIP',N'Regional and Branch Leadership',N'Regional, branch, and departmental leadership.',20),
(N'PRODUCTION',N'Sales and Production',N'New business development, producer operations, and organic growth.',30),
(N'ACCOUNT_MANAGEMENT',N'Account Management',N'Client relationship strategy and account management.',40),
(N'CLIENT_SERVICE',N'Client Service',N'Day-to-day client servicing, certificates, endorsements, and renewals.',50),
(N'COMMERCIAL_LINES',N'Commercial Lines',N'Commercial property and casualty production and service.',60),
(N'PERSONAL_LINES',N'Personal Lines',N'Personal insurance production and service.',70),
(N'EMPLOYEE_BENEFITS',N'Employee Benefits',N'Employee benefits production, consulting, and service.',80),
(N'LIFE_HEALTH',N'Life and Health',N'Life, health, and related individual products.',90),
(N'MARKETING_PLACEMENT',N'Marketing and Placement',N'Carrier marketing, submissions, placement, and remarketing.',100),
(N'UNDERWRITING',N'Underwriting',N'Risk selection, underwriting analysis, and authority decisions.',110),
(N'RISK_MANAGEMENT',N'Risk Management and Loss Control',N'Risk consulting, loss control, and client risk strategy.',120),
(N'CLAIMS',N'Claims Advocacy',N'Claims intake, advocacy, analysis, and resolution support.',130),
(N'FINANCE',N'Finance and Accounting',N'Accounting, billing, premium finance, commissions, and financial controls.',140),
(N'COMPLIANCE',N'Compliance and Licensing',N'Regulatory compliance, licensing, appointments, and governance.',150),
(N'OPERATIONS',N'Operations and Quality',N'Agency operations, quality assurance, process improvement, and document control.',160),
(N'TECHNOLOGY',N'Information Technology',N'Infrastructure, applications, cybersecurity, and end-user support.',170),
(N'DATA',N'Data and Analytics',N'Data governance, reporting, analytics, and business intelligence.',180),
(N'HUMAN_RESOURCES',N'Human Resources and Training',N'People operations, talent management, and learning and development.',190),
(N'ADMINISTRATION',N'Administration',N'Office administration, executive support, and front-office services.',200),
(N'OTHER',N'Consulting and Other',N'Consulting, internship, and other enterprise support assignments.',210);

;WITH TenantBranch AS
(
	SELECT tenant.TenantId,branch.BranchId,
		ROW_NUMBER() OVER(PARTITION BY tenant.TenantId ORDER BY branch.IsHeadquarters DESC,branch.CreatedDateUtc,branch.BranchId) RowNumber
	FROM Core.Tenant tenant
	JOIN Agency.Branch branch ON branch.TenantId=tenant.TenantId AND branch.IsDeleted=0
	WHERE tenant.IsDeleted=0
)
INSERT Agency.Department(DepartmentId,TenantId,BranchId,DepartmentName,DepartmentCode,Description,IsActive,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenantBranch.TenantId,tenantBranch.BranchId,catalog.DepartmentName,catalog.DepartmentCode,catalog.Description,1,SYSUTCDATETIME(),0
FROM TenantBranch tenantBranch CROSS JOIN @DepartmentCatalog catalog
WHERE tenantBranch.RowNumber=1
	AND NOT EXISTS
  (
	SELECT 1 FROM Agency.Department existing
	WHERE existing.TenantId=tenantBranch.TenantId
	  AND (UPPER(LTRIM(RTRIM(existing.DepartmentCode)))=UPPER(catalog.DepartmentCode)
		OR UPPER(LTRIM(RTRIM(existing.DepartmentName)))=UPPER(catalog.DepartmentName))
  );

;WITH LegacyDepartment AS
(
	SELECT DISTINCT userRecord.TenantId,LTRIM(RTRIM(userRecord.Department)) DepartmentName
	FROM IAM.[User] userRecord
	WHERE userRecord.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(userRecord.Department)),N'') IS NOT NULL
), TenantBranch AS
(
	SELECT branch.TenantId,branch.BranchId,ROW_NUMBER() OVER(PARTITION BY branch.TenantId ORDER BY branch.IsHeadquarters DESC,branch.CreatedDateUtc,branch.BranchId) RowNumber
	FROM Agency.Branch branch WHERE branch.IsDeleted=0
)
INSERT Agency.Department(DepartmentId,TenantId,BranchId,DepartmentName,DepartmentCode,Description,IsActive,CreatedDateUtc,IsDeleted)
SELECT NEWID(),legacy.TenantId,tenantBranch.BranchId,legacy.DepartmentName,
	CONCAT(N'CUSTOM_',LEFT(CONVERT(VARCHAR(64),HASHBYTES('SHA2_256',CONVERT(VARBINARY(MAX),UPPER(legacy.DepartmentName))),2),32)),
	N'Migrated from an existing IAM user profile.',1,SYSUTCDATETIME(),0
FROM LegacyDepartment legacy
JOIN TenantBranch tenantBranch ON tenantBranch.TenantId=legacy.TenantId AND tenantBranch.RowNumber=1
WHERE NOT EXISTS
(
	SELECT 1 FROM Agency.Department existing
	WHERE existing.TenantId=legacy.TenantId
	  AND (UPPER(LTRIM(RTRIM(existing.DepartmentName)))=UPPER(legacy.DepartmentName)
		OR UPPER(LTRIM(RTRIM(existing.DepartmentCode)))=CONCAT(N'CUSTOM_',LEFT(CONVERT(VARCHAR(64),HASHBYTES('SHA2_256',CONVERT(VARBINARY(MAX),UPPER(legacy.DepartmentName))),2),32)))
);

IF OBJECT_ID(N'Agency.DepartmentJobTitle',N'U') IS NULL
BEGIN
	CREATE TABLE Agency.DepartmentJobTitle
	(
		DepartmentJobTitleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_DepartmentJobTitle PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DepartmentId UNIQUEIDENTIFIER NOT NULL,
		JobTitleId UNIQUEIDENTIFIER NOT NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_Agency_DepartmentJobTitle_IsDefault DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Agency_DepartmentJobTitle_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Agency_DepartmentJobTitle_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Agency_DepartmentJobTitle_IsDeleted DEFAULT 0,
		CONSTRAINT FK_Agency_DepartmentJobTitle_Department FOREIGN KEY(DepartmentId) REFERENCES Agency.Department(DepartmentId),
		CONSTRAINT FK_Agency_DepartmentJobTitle_JobTitle FOREIGN KEY(JobTitleId) REFERENCES IAM.JobTitle(JobTitleId)
	);
	CREATE UNIQUE INDEX UX_Agency_DepartmentJobTitle ON Agency.DepartmentJobTitle(TenantId,DepartmentId,JobTitleId) WHERE IsDeleted=0;
	CREATE INDEX IX_Agency_DepartmentJobTitle_Department ON Agency.DepartmentJobTitle(TenantId,DepartmentId,IsActive,IsDeleted);
	CREATE INDEX IX_Agency_DepartmentJobTitle_JobTitle ON Agency.DepartmentJobTitle(TenantId,JobTitleId,IsActive,IsDeleted);
END;

DECLARE @CategoryDepartment TABLE(CategoryCode NVARCHAR(80),DepartmentCode NVARCHAR(50),IsDefault BIT);
INSERT @CategoryDepartment VALUES
(N'Executive',N'EXECUTIVE',1),(N'Leadership',N'LEADERSHIP',1),(N'Production',N'PRODUCTION',1),
(N'AccountManagement',N'ACCOUNT_MANAGEMENT',1),(N'Service',N'CLIENT_SERVICE',1),(N'Benefits',N'EMPLOYEE_BENEFITS',1),
(N'Marketing',N'MARKETING_PLACEMENT',1),(N'Underwriting',N'UNDERWRITING',1),(N'RiskManagement',N'RISK_MANAGEMENT',1),
(N'Claims',N'CLAIMS',1),(N'Finance',N'FINANCE',1),(N'Compliance',N'COMPLIANCE',1),(N'Operations',N'OPERATIONS',1),
(N'Technology',N'TECHNOLOGY',1),(N'Data',N'DATA',1),(N'HumanResources',N'HUMAN_RESOURCES',1),
(N'Administration',N'ADMINISTRATION',1),(N'Other',N'OTHER',1),(N'Custom',N'OTHER',0);

INSERT Agency.DepartmentJobTitle(DepartmentJobTitleId,TenantId,DepartmentId,JobTitleId,IsDefault,IsActive,CreatedDateUtc,IsDeleted)
SELECT NEWID(),title.TenantId,department.DepartmentId,title.JobTitleId,mapping.IsDefault,1,SYSUTCDATETIME(),0
FROM IAM.JobTitle title
JOIN @CategoryDepartment mapping ON mapping.CategoryCode=title.CategoryCode
JOIN Agency.Department department ON department.TenantId=title.TenantId AND department.DepartmentCode=mapping.DepartmentCode AND department.IsDeleted=0
WHERE title.IsDeleted=0
  AND NOT EXISTS(SELECT 1 FROM Agency.DepartmentJobTitle existing WHERE existing.TenantId=title.TenantId AND existing.DepartmentId=department.DepartmentId AND existing.JobTitleId=title.JobTitleId AND existing.IsDeleted=0);

IF COL_LENGTH(N'IAM.[User]',N'DepartmentId') IS NULL ALTER TABLE IAM.[User] ADD DepartmentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'IAM.[User]',N'JobTitleId') IS NULL ALTER TABLE IAM.[User] ADD JobTitleId UNIQUEIDENTIFIER NULL;

DECLARE @DropDepartmentForeignKeys NVARCHAR(MAX)=N'';
SELECT @DropDepartmentForeignKeys=STRING_AGG(
	N'ALTER TABLE IAM.[User] DROP CONSTRAINT '+QUOTENAME(foreignKey.name)+N';',NCHAR(10))
FROM sys.foreign_keys foreignKey
JOIN sys.foreign_key_columns foreignKeyColumn ON foreignKeyColumn.constraint_object_id=foreignKey.object_id
JOIN sys.columns parentColumn ON parentColumn.object_id=foreignKeyColumn.parent_object_id AND parentColumn.column_id=foreignKeyColumn.parent_column_id
WHERE foreignKey.parent_object_id=OBJECT_ID(N'IAM.[User]') AND parentColumn.name=N'DepartmentId';
IF NULLIF(@DropDepartmentForeignKeys,N'') IS NOT NULL EXEC sys.sp_executesql @DropDepartmentForeignKeys;

EXEC(N'
UPDATE userRecord
SET DepartmentId=matched.DepartmentId,
	Department=matched.DepartmentName
FROM IAM.[User] userRecord
CROSS APPLY
(
	SELECT TOP(1) department.DepartmentId,department.DepartmentName
	FROM Agency.Department department
	WHERE department.TenantId=userRecord.TenantId AND department.IsDeleted=0 AND UPPER(department.DepartmentName)=UPPER(LTRIM(RTRIM(userRecord.Department)))
	ORDER BY department.IsActive DESC,department.CreatedDateUtc,department.DepartmentId
) matched
WHERE (userRecord.DepartmentId IS NULL OR NOT EXISTS(SELECT 1 FROM Agency.Department currentDepartment WHERE currentDepartment.DepartmentId=userRecord.DepartmentId))
  AND NULLIF(LTRIM(RTRIM(userRecord.Department)),N'''') IS NOT NULL;

UPDATE userRecord
SET DepartmentId=NULL
FROM IAM.[User] userRecord
WHERE userRecord.DepartmentId IS NOT NULL
  AND NOT EXISTS
  (
	SELECT 1 FROM Agency.Department department
	WHERE department.DepartmentId=userRecord.DepartmentId AND department.TenantId=userRecord.TenantId
  );

UPDATE userRecord
SET JobTitleId=matched.JobTitleId
FROM IAM.[User] userRecord
CROSS APPLY
(
	SELECT TOP(1) title.JobTitleId
	FROM IAM.JobTitle title
	WHERE title.TenantId=userRecord.TenantId AND title.IsDeleted=0 AND UPPER(title.JobTitleName)=UPPER(LTRIM(RTRIM(userRecord.JobTitle)))
	ORDER BY title.IsActive DESC,title.SortOrder,title.JobTitleId
) matched
WHERE userRecord.JobTitleId IS NULL AND NULLIF(LTRIM(RTRIM(userRecord.JobTitle)),N'''') IS NOT NULL;
');

IF NOT EXISTS
(
	SELECT 1 FROM sys.foreign_keys foreignKey
	JOIN sys.foreign_key_columns foreignKeyColumn ON foreignKeyColumn.constraint_object_id=foreignKey.object_id
	JOIN sys.columns parentColumn ON parentColumn.object_id=foreignKeyColumn.parent_object_id AND parentColumn.column_id=foreignKeyColumn.parent_column_id
	WHERE foreignKey.parent_object_id=OBJECT_ID(N'IAM.[User]') AND parentColumn.name=N'DepartmentId'
	  AND foreignKey.referenced_object_id=OBJECT_ID(N'Agency.Department')
)
	EXEC(N'ALTER TABLE IAM.[User] WITH CHECK ADD CONSTRAINT FK_User_Department FOREIGN KEY(DepartmentId) REFERENCES Agency.Department(DepartmentId); ALTER TABLE IAM.[User] CHECK CONSTRAINT FK_User_Department;');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IAM_User_JobTitle')
	EXEC(N'ALTER TABLE IAM.[User] ADD CONSTRAINT FK_IAM_User_JobTitle FOREIGN KEY(JobTitleId) REFERENCES IAM.JobTitle(JobTitleId);');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'IAM.[User]') AND name=N'IX_IAM_User_Department')
	EXEC(N'CREATE INDEX IX_IAM_User_Department ON IAM.[User](TenantId,DepartmentId) WHERE IsDeleted=0;');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'IAM.[User]') AND name=N'IX_IAM_User_JobTitle')
	EXEC(N'CREATE INDEX IX_IAM_User_JobTitle ON IAM.[User](TenantId,JobTitleId) WHERE IsDeleted=0;');
