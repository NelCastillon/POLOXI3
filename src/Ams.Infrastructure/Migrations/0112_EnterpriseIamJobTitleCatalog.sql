SET NOCOUNT ON;
SET XACT_ABORT ON;

IF SCHEMA_ID(N'IAM') IS NULL EXEC(N'CREATE SCHEMA IAM');

IF OBJECT_ID(N'IAM.JobTitle',N'U') IS NULL
BEGIN
	CREATE TABLE IAM.JobTitle
	(
		JobTitleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IAM_JobTitle PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		JobTitleCode NVARCHAR(100) NOT NULL,
		JobTitleName NVARCHAR(150) NOT NULL,
		CategoryCode NVARCHAR(80) NOT NULL,
		Description NVARCHAR(500) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_IAM_JobTitle_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_IAM_JobTitle_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_IAM_JobTitle_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_IAM_JobTitle_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_IAM_JobTitle_Code ON IAM.JobTitle(TenantId,JobTitleCode) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_IAM_JobTitle_Name ON IAM.JobTitle(TenantId,JobTitleName) WHERE IsDeleted=0;
	CREATE INDEX IX_IAM_JobTitle_List ON IAM.JobTitle(TenantId,IsActive,IsDeleted,CategoryCode,SortOrder,JobTitleName);
END;

DECLARE @Catalog TABLE(JobTitleCode NVARCHAR(100),JobTitleName NVARCHAR(150),CategoryCode NVARCHAR(80),Description NVARCHAR(500),SortOrder INT);
INSERT @Catalog VALUES
(N'PRESIDENT',N'President',N'Executive',N'Agency president responsible for enterprise strategy and performance.',10),
(N'CHIEF_EXECUTIVE_OFFICER',N'Chief Executive Officer',N'Executive',N'Executive accountable for agency operations and growth.',20),
(N'CHIEF_OPERATING_OFFICER',N'Chief Operating Officer',N'Executive',N'Executive accountable for operating performance and service delivery.',30),
(N'CHIEF_FINANCIAL_OFFICER',N'Chief Financial Officer',N'Executive',N'Executive accountable for finance, controls, and financial reporting.',40),
(N'CHIEF_REVENUE_OFFICER',N'Chief Revenue Officer',N'Executive',N'Executive accountable for revenue strategy and producer performance.',50),
(N'CHIEF_INFORMATION_OFFICER',N'Chief Information Officer',N'Executive',N'Executive accountable for technology strategy and information systems.',60),
(N'CHIEF_COMPLIANCE_OFFICER',N'Chief Compliance Officer',N'Executive',N'Executive accountable for regulatory and compliance governance.',70),
(N'AGENCY_PRINCIPAL',N'Agency Principal',N'Executive',N'Agency owner or principal.',80),
(N'MANAGING_PARTNER',N'Managing Partner',N'Executive',N'Partner responsible for agency leadership and governance.',90),
(N'EXECUTIVE_VICE_PRESIDENT',N'Executive Vice President',N'Executive',N'Senior executive leader.',100),
(N'VICE_PRESIDENT',N'Vice President',N'Executive',N'Executive leader responsible for an agency function or market.',110),
(N'REGIONAL_VICE_PRESIDENT',N'Regional Vice President',N'Leadership',N'Leader responsible for a geographic region.',120),
(N'BRANCH_MANAGER',N'Branch Manager',N'Leadership',N'Manager responsible for branch performance and staffing.',130),
(N'DEPARTMENT_MANAGER',N'Department Manager',N'Leadership',N'Manager responsible for a functional department.',140),
(N'OPERATIONS_MANAGER',N'Operations Manager',N'Operations',N'Manager responsible for agency operational processes.',150),
(N'SERVICE_MANAGER',N'Service Manager',N'Service',N'Manager responsible for account service teams and standards.',160),
(N'COMMERCIAL_LINES_MANAGER',N'Commercial Lines Manager',N'Service',N'Manager responsible for commercial lines operations.',170),
(N'PERSONAL_LINES_MANAGER',N'Personal Lines Manager',N'Service',N'Manager responsible for personal lines operations.',180),
(N'BENEFITS_MANAGER',N'Employee Benefits Manager',N'Benefits',N'Manager responsible for employee benefits operations.',190),
(N'PRODUCER',N'Producer',N'Production',N'Licensed producer responsible for sales and client relationships.',200),
(N'SENIOR_PRODUCER',N'Senior Producer',N'Production',N'Senior licensed producer with advanced account responsibilities.',210),
(N'COMMERCIAL_LINES_PRODUCER',N'Commercial Lines Producer',N'Production',N'Producer specializing in commercial insurance.',220),
(N'PERSONAL_LINES_PRODUCER',N'Personal Lines Producer',N'Production',N'Producer specializing in personal insurance.',230),
(N'EMPLOYEE_BENEFITS_PRODUCER',N'Employee Benefits Producer',N'Benefits',N'Producer specializing in employee benefits.',240),
(N'LIFE_HEALTH_PRODUCER',N'Life and Health Producer',N'Production',N'Producer specializing in life and health products.',250),
(N'SALES_EXECUTIVE',N'Sales Executive',N'Production',N'Sales professional responsible for new business development.',260),
(N'BUSINESS_DEVELOPMENT_MANAGER',N'Business Development Manager',N'Production',N'Manager responsible for growth initiatives and partnerships.',270),
(N'ACCOUNT_EXECUTIVE',N'Account Executive',N'AccountManagement',N'Senior client relationship and account strategy professional.',280),
(N'SENIOR_ACCOUNT_EXECUTIVE',N'Senior Account Executive',N'AccountManagement',N'Senior account executive for complex accounts.',290),
(N'ACCOUNT_MANAGER',N'Account Manager',N'AccountManagement',N'Primary account servicing and relationship manager.',300),
(N'SENIOR_ACCOUNT_MANAGER',N'Senior Account Manager',N'AccountManagement',N'Senior manager for complex or strategic accounts.',310),
(N'COMMERCIAL_LINES_ACCOUNT_MANAGER',N'Commercial Lines Account Manager',N'AccountManagement',N'Account manager specializing in commercial lines.',320),
(N'PERSONAL_LINES_ACCOUNT_MANAGER',N'Personal Lines Account Manager',N'AccountManagement',N'Account manager specializing in personal lines.',330),
(N'BENEFITS_ACCOUNT_MANAGER',N'Employee Benefits Account Manager',N'Benefits',N'Account manager specializing in employee benefits.',340),
(N'ACCOUNT_COORDINATOR',N'Account Coordinator',N'AccountManagement',N'Coordinator supporting account management activities.',350),
(N'CLIENT_SERVICE_REPRESENTATIVE',N'Client Service Representative',N'Service',N'CSR responsible for day-to-day client service.',360),
(N'SENIOR_CLIENT_SERVICE_REPRESENTATIVE',N'Senior Client Service Representative',N'Service',N'Senior CSR for complex service needs.',370),
(N'COMMERCIAL_LINES_CSR',N'Commercial Lines CSR',N'Service',N'CSR specializing in commercial lines.',380),
(N'PERSONAL_LINES_CSR',N'Personal Lines CSR',N'Service',N'CSR specializing in personal lines.',390),
(N'CERTIFICATE_SPECIALIST',N'Certificate Specialist',N'Service',N'Specialist responsible for certificates of insurance.',400),
(N'ENDORSEMENT_SPECIALIST',N'Endorsement Specialist',N'Service',N'Specialist responsible for policy change processing.',410),
(N'RENEWAL_SPECIALIST',N'Renewal Specialist',N'Service',N'Specialist responsible for renewal preparation and execution.',420),
(N'REMARKETING_SPECIALIST',N'Remarketing Specialist',N'Marketing',N'Specialist responsible for renewal remarketing.',430),
(N'PLACEMENT_SPECIALIST',N'Placement Specialist',N'Marketing',N'Specialist responsible for carrier placement.',440),
(N'MARKETING_MANAGER',N'Marketing Manager',N'Marketing',N'Manager responsible for carrier marketing and placement.',450),
(N'MARKETING_SPECIALIST',N'Marketing Specialist',N'Marketing',N'Specialist supporting submissions and carrier marketing.',460),
(N'UNDERWRITER',N'Underwriter',N'Underwriting',N'Underwriter responsible for risk evaluation and authority decisions.',470),
(N'SENIOR_UNDERWRITER',N'Senior Underwriter',N'Underwriting',N'Senior underwriter for complex risks.',480),
(N'UNDERWRITING_ASSISTANT',N'Underwriting Assistant',N'Underwriting',N'Assistant supporting underwriting workflows.',490),
(N'RISK_MANAGER',N'Risk Manager',N'RiskManagement',N'Professional responsible for client risk management strategy.',500),
(N'LOSS_CONTROL_SPECIALIST',N'Loss Control Specialist',N'RiskManagement',N'Specialist responsible for loss-control analysis and recommendations.',510),
(N'CLAIMS_MANAGER',N'Claims Manager',N'Claims',N'Manager responsible for claims advocacy and operations.',520),
(N'CLAIMS_ADVOCATE',N'Claims Advocate',N'Claims',N'Client advocate supporting claims resolution.',530),
(N'CLAIMS_SPECIALIST',N'Claims Specialist',N'Claims',N'Specialist responsible for claims intake and follow-up.',540),
(N'CLAIMS_ANALYST',N'Claims Analyst',N'Claims',N'Analyst responsible for claims data and trend analysis.',550),
(N'CONTROLLER',N'Controller',N'Finance',N'Leader responsible for accounting operations and controls.',560),
(N'ACCOUNTING_MANAGER',N'Accounting Manager',N'Finance',N'Manager responsible for agency accounting operations.',570),
(N'SENIOR_ACCOUNTANT',N'Senior Accountant',N'Finance',N'Senior accounting professional.',580),
(N'ACCOUNTANT',N'Accountant',N'Finance',N'Accounting professional responsible for financial transactions.',590),
(N'BOOKKEEPER',N'Bookkeeper',N'Finance',N'Professional responsible for bookkeeping and reconciliations.',600),
(N'BILLING_MANAGER',N'Billing Manager',N'Finance',N'Manager responsible for agency billing operations.',610),
(N'BILLING_SPECIALIST',N'Billing Specialist',N'Finance',N'Specialist responsible for invoicing and receivables.',620),
(N'PREMIUM_ACCOUNTING_SPECIALIST',N'Premium Accounting Specialist',N'Finance',N'Specialist responsible for premium accounting and carrier payables.',630),
(N'COMMISSION_MANAGER',N'Commission Manager',N'Finance',N'Manager responsible for commission accounting and reconciliation.',640),
(N'COMMISSION_SPECIALIST',N'Commission Specialist',N'Finance',N'Specialist responsible for producer and carrier commissions.',650),
(N'COMPLIANCE_MANAGER',N'Compliance Manager',N'Compliance',N'Manager responsible for insurance compliance operations.',660),
(N'COMPLIANCE_SPECIALIST',N'Compliance Specialist',N'Compliance',N'Specialist responsible for regulatory compliance activities.',670),
(N'LICENSING_MANAGER',N'Licensing Manager',N'Compliance',N'Manager responsible for producer and agency licensing.',680),
(N'LICENSING_SPECIALIST',N'Licensing Specialist',N'Compliance',N'Specialist responsible for licenses and appointments.',690),
(N'QUALITY_ASSURANCE_MANAGER',N'Quality Assurance Manager',N'Operations',N'Manager responsible for quality standards and reviews.',700),
(N'QUALITY_ASSURANCE_ANALYST',N'Quality Assurance Analyst',N'Operations',N'Analyst responsible for operational quality reviews.',710),
(N'PROCESS_IMPROVEMENT_MANAGER',N'Process Improvement Manager',N'Operations',N'Manager responsible for workflow optimization.',720),
(N'DOCUMENT_CONTROL_SPECIALIST',N'Document Control Specialist',N'Operations',N'Specialist responsible for document governance and records.',730),
(N'IT_DIRECTOR',N'IT Director',N'Technology',N'Leader responsible for agency technology operations.',740),
(N'IT_MANAGER',N'IT Manager',N'Technology',N'Manager responsible for IT services and systems.',750),
(N'SYSTEM_ADMINISTRATOR',N'System Administrator',N'Technology',N'Administrator responsible for enterprise systems.',760),
(N'APPLICATION_ADMINISTRATOR',N'Application Administrator',N'Technology',N'Administrator responsible for AMS applications and configuration.',770),
(N'SECURITY_ADMINISTRATOR',N'Security Administrator',N'Technology',N'Administrator responsible for identity and security operations.',780),
(N'SUPPORT_SPECIALIST',N'IT Support Specialist',N'Technology',N'Specialist responsible for end-user technology support.',790),
(N'DATA_MANAGER',N'Data Manager',N'Data',N'Manager responsible for data quality and governance.',800),
(N'DATA_ANALYST',N'Data Analyst',N'Data',N'Analyst responsible for agency reporting and insights.',810),
(N'BUSINESS_INTELLIGENCE_ANALYST',N'Business Intelligence Analyst',N'Data',N'Analyst responsible for business intelligence and dashboards.',820),
(N'REPORTING_ANALYST',N'Reporting Analyst',N'Data',N'Analyst responsible for operational and management reporting.',830),
(N'HUMAN_RESOURCES_DIRECTOR',N'Human Resources Director',N'HumanResources',N'Leader responsible for human resources strategy.',840),
(N'HUMAN_RESOURCES_MANAGER',N'Human Resources Manager',N'HumanResources',N'Manager responsible for human resources operations.',850),
(N'HUMAN_RESOURCES_GENERALIST',N'Human Resources Generalist',N'HumanResources',N'Professional supporting employee lifecycle and policies.',860),
(N'TRAINING_MANAGER',N'Training Manager',N'HumanResources',N'Manager responsible for employee learning and development.',870),
(N'TRAINING_SPECIALIST',N'Training Specialist',N'HumanResources',N'Specialist responsible for training delivery and materials.',880),
(N'OFFICE_MANAGER',N'Office Manager',N'Administration',N'Manager responsible for office administration.',890),
(N'EXECUTIVE_ASSISTANT',N'Executive Assistant',N'Administration',N'Assistant supporting executive leadership.',900),
(N'ADMINISTRATIVE_ASSISTANT',N'Administrative Assistant',N'Administration',N'Assistant supporting agency administration.',910),
(N'RECEPTIONIST',N'Receptionist',N'Administration',N'Professional responsible for front-office communications.',920),
(N'INTERN',N'Intern',N'Administration',N'Temporary learning and support position.',930),
(N'CONSULTANT',N'Consultant',N'Other',N'External or internal consulting professional.',940);

INSERT IAM.JobTitle(JobTitleId,TenantId,JobTitleCode,JobTitleName,CategoryCode,Description,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,catalog.JobTitleCode,catalog.JobTitleName,catalog.CategoryCode,catalog.Description,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Core.Tenant tenant CROSS JOIN @Catalog catalog
WHERE tenant.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM IAM.JobTitle existing WHERE existing.TenantId=tenant.TenantId AND existing.JobTitleCode=catalog.JobTitleCode AND existing.IsDeleted=0);

;WITH LegacyTitle AS
(
	SELECT DISTINCT userRecord.TenantId,LTRIM(RTRIM(userRecord.JobTitle)) JobTitleName
	FROM IAM.[User] userRecord
	WHERE userRecord.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(userRecord.JobTitle)),N'') IS NOT NULL
)
INSERT IAM.JobTitle(JobTitleId,TenantId,JobTitleCode,JobTitleName,CategoryCode,Description,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),legacy.TenantId,CONCAT(N'CUSTOM_',LEFT(CONVERT(VARCHAR(64),HASHBYTES('SHA2_256',CONVERT(VARBINARY(MAX),UPPER(legacy.JobTitleName))),2),32)),legacy.JobTitleName,N'Custom',N'Migrated from an existing IAM user profile.',1,10000,SYSUTCDATETIME(),0
FROM LegacyTitle legacy
WHERE NOT EXISTS(SELECT 1 FROM IAM.JobTitle existing WHERE existing.TenantId=legacy.TenantId AND UPPER(existing.JobTitleName)=UPPER(legacy.JobTitleName) AND existing.IsDeleted=0);
