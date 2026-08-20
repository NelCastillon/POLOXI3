SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPlan', N'U') IS NULL
BEGIN
	CREATE TABLE Commission.CommissionPlan
	(
		CommissionPlanId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommissionPlan PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PlanCode NVARCHAR(50) NOT NULL,
		PlanName NVARCHAR(200) NOT NULL,
		PlanTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_PlanType_0077 DEFAULT N'Standard',
		NewBusinessRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_NewRate_0077 DEFAULT 0,
		RenewalRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_RenewalRate_0077 DEFAULT 0,
		EffectiveStartDate DATE NOT NULL CONSTRAINT DF_CommissionPlan_EffectiveStart_0077 DEFAULT CONVERT(date, SYSUTCDATETIME()),
		EffectiveEndDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_Status_0077 DEFAULT N'Active',
		AllowSplit BIT NOT NULL CONSTRAINT DF_CommissionPlan_AllowSplit_0077 DEFAULT 1,
		HouseAccountRules BIT NOT NULL CONSTRAINT DF_CommissionPlan_House_0077 DEFAULT 0,
		BranchOverrideEligible BIT NOT NULL CONSTRAINT DF_CommissionPlan_Branch_0077 DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPlan_Created_0077 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPlan_IsDeleted_0077 DEFAULT 0
	);
END;

IF COL_LENGTH(N'Commission.CommissionPlan', N'PlanCode') IS NULL ALTER TABLE Commission.CommissionPlan ADD PlanCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'PlanName') IS NULL ALTER TABLE Commission.CommissionPlan ADD PlanName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'PlanTypeCode') IS NULL ALTER TABLE Commission.CommissionPlan ADD PlanTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_PlanType_Add_0077 DEFAULT N'Standard';
IF COL_LENGTH(N'Commission.CommissionPlan', N'NewBusinessRatePct') IS NULL ALTER TABLE Commission.CommissionPlan ADD NewBusinessRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_NewRate_Add_0077 DEFAULT 0;
IF COL_LENGTH(N'Commission.CommissionPlan', N'RenewalRatePct') IS NULL ALTER TABLE Commission.CommissionPlan ADD RenewalRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_RenewalRate_Add_0077 DEFAULT 0;
IF COL_LENGTH(N'Commission.CommissionPlan', N'EffectiveStartDate') IS NULL ALTER TABLE Commission.CommissionPlan ADD EffectiveStartDate DATE NOT NULL CONSTRAINT DF_CommissionPlan_EffectiveStart_Add_0077 DEFAULT CONVERT(date, SYSUTCDATETIME());
IF COL_LENGTH(N'Commission.CommissionPlan', N'EffectiveEndDate') IS NULL ALTER TABLE Commission.CommissionPlan ADD EffectiveEndDate DATE NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPlan ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_Status_Add_0077 DEFAULT N'Active';
IF COL_LENGTH(N'Commission.CommissionPlan', N'StatusCodeId') IS NULL ALTER TABLE Commission.CommissionPlan ADD StatusCodeId INT NOT NULL CONSTRAINT DF_CommissionPlan_StatusCodeId_Add_0077 DEFAULT 1;
IF COL_LENGTH(N'Commission.CommissionPlan', N'AllowSplit') IS NULL ALTER TABLE Commission.CommissionPlan ADD AllowSplit BIT NOT NULL CONSTRAINT DF_CommissionPlan_AllowSplit_Add_0077 DEFAULT 1;
IF COL_LENGTH(N'Commission.CommissionPlan', N'HouseAccountRules') IS NULL ALTER TABLE Commission.CommissionPlan ADD HouseAccountRules BIT NOT NULL CONSTRAINT DF_CommissionPlan_House_Add_0077 DEFAULT 0;
IF COL_LENGTH(N'Commission.CommissionPlan', N'BranchOverrideEligible') IS NULL ALTER TABLE Commission.CommissionPlan ADD BranchOverrideEligible BIT NOT NULL CONSTRAINT DF_CommissionPlan_Branch_Add_0077 DEFAULT 0;
IF COL_LENGTH(N'Commission.CommissionPlan', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPlan ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPlan_Created_Add_0077 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Commission.CommissionPlan', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionPlan ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionPlan ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionPlan ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPlan', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPlan ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPlan_IsDeleted_Add_0077 DEFAULT 0;

IF OBJECT_ID(N'Commission.CommissionPayee', N'U') IS NULL
BEGIN
	CREATE TABLE Commission.CommissionPayee
	(
		CommissionPayeeId UNIQUEIDENTIFIER NULL,
		PayeeId UNIQUEIDENTIFIER NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PayeeCode NVARCHAR(50) NULL,
		PayeeName NVARCHAR(255) NULL,
		UserId UNIQUEIDENTIFIER NULL,
		CommissionPlanId UNIQUEIDENTIFIER NOT NULL,
		PayeeTypeCode NVARCHAR(50) NOT NULL,
		SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPayee_Split_0077 DEFAULT 100,
		EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionPayee_Effective_0077 DEFAULT CONVERT(date, SYSUTCDATETIME()),
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPayee_Status_0077 DEFAULT N'Active',
		IsActive BIT NOT NULL CONSTRAINT DF_CommissionPayee_Active_0077 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPayee_Created_0077 DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPayee_IsDeleted_0077 DEFAULT 0
	);
END;

IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPayeeId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPayeeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeName') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeName NVARCHAR(255) NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPayeeTypeId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPayeeTypeId INT NOT NULL CONSTRAINT DF_CommissionPayee_TypeId_Add_0077 DEFAULT 1;
IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Commission.CommissionPayee', N'CurrencyCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_CommissionPayee_Currency_Add_0077 DEFAULT N'USD';
IF COL_LENGTH(N'Commission.CommissionPayee', N'CurrencyCode') IS NOT NULL
   AND NOT EXISTS
   (
	   SELECT 1
	   FROM sys.default_constraints dc
	   INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
	   WHERE dc.parent_object_id = OBJECT_ID(N'Commission.CommissionPayee')
		 AND c.name = N'CurrencyCode'
   )
BEGIN
	ALTER TABLE Commission.CommissionPayee ADD CONSTRAINT DF_CommissionPayee_Currency_Default_0077 DEFAULT N'USD' FOR CurrencyCode;
END;
IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPayee_Split_Add_0077 DEFAULT 100;
IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionPayee_Effective_Add_0077 DEFAULT CONVERT(date, SYSUTCDATETIME());
IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPayee_Status_Add_0077 DEFAULT N'Active';
IF COL_LENGTH(N'Commission.CommissionPayee', N'IsActive') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsActive BIT NOT NULL CONSTRAINT DF_CommissionPayee_Active_Add_0077 DEFAULT 1;
IF COL_LENGTH(N'Commission.CommissionPayee', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayee ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPayee_Created_Add_0077 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPayee_IsDeleted_Add_0077 DEFAULT 0;

IF OBJECT_ID(N'Commission.CommissionSplitRule', N'U') IS NULL
BEGIN
	CREATE TABLE Commission.CommissionSplitRule
	(
		SplitRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommissionSplitRule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CommissionPlanId UNIQUEIDENTIFIER NOT NULL,
		RuleName NVARCHAR(200) NOT NULL,
		SplitTypeCode NVARCHAR(50) NOT NULL,
		PayeeId UNIQUEIDENTIFIER NULL,
		SplitPct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionSplitRule_Split_0077 DEFAULT 100,
		OverrideRatePct DECIMAL(9,4) NULL,
		Priority INT NOT NULL CONSTRAINT DF_CommissionSplitRule_Priority_0077 DEFAULT 100,
		EffectiveStartDate DATE NOT NULL CONSTRAINT DF_CommissionSplitRule_Effective_0077 DEFAULT CONVERT(date, SYSUTCDATETIME()),
		EffectiveEndDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionSplitRule_Status_0077 DEFAULT N'Active',
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionSplitRule_Created_0077 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionSplitRule_IsDeleted_0077 DEFAULT 0
	);
END;

IF COL_LENGTH(N'Commission.CommissionSplitRule', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD PayeeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'SplitPct') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD SplitPct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionSplitRule_Split_Add_0077 DEFAULT 100;
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'OverrideRatePct') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD OverrideRatePct DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'Priority') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD Priority INT NOT NULL CONSTRAINT DF_CommissionSplitRule_Priority_Add_0077 DEFAULT 100;
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'EffectiveStartDate') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD EffectiveStartDate DATE NOT NULL CONSTRAINT DF_CommissionSplitRule_Effective_Add_0077 DEFAULT CONVERT(date, SYSUTCDATETIME());
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'EffectiveEndDate') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD EffectiveEndDate DATE NULL;
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionSplitRule_Status_Add_0077 DEFAULT N'Active';
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionSplitRule_Created_Add_0077 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Commission.CommissionSplitRule', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionSplitRule ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionSplitRule_IsDeleted_Add_0077 DEFAULT 0;

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @PlanId UNIQUEIDENTIFIER = '77000000-0000-0000-0000-000000000077';
DECLARE @ActiveStatusCodeId INT = 1;
DECLARE @ProducerPayeeTypeId INT = 1;

IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND PlanCode = N'DEMO-PRODUCER-STD' AND IsDeleted = 0)
BEGIN
	IF COL_LENGTH(N'Commission.CommissionPlan', N'StatusCodeId') IS NOT NULL
	BEGIN
		INSERT INTO Commission.CommissionPlan
			(CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, EffectiveEndDate, StatusCode, StatusCodeId, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc, IsDeleted)
		VALUES
			(@PlanId, @TenantId, N'DEMO-PRODUCER-STD', N'Demo Standard Producer Commission Plan', N'Standard', 12.5000, 10.0000, '2020-01-01', NULL, N'Active', @ActiveStatusCodeId, 1, 0, 0, SYSUTCDATETIME(), 0);
	END
	ELSE
	BEGIN
		INSERT INTO Commission.CommissionPlan
			(CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, EffectiveEndDate, StatusCode, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc, IsDeleted)
		VALUES
			(@PlanId, @TenantId, N'DEMO-PRODUCER-STD', N'Demo Standard Producer Commission Plan', N'Standard', 12.5000, 10.0000, '2020-01-01', NULL, N'Active', 1, 0, 0, SYSUTCDATETIME(), 0);
	END;
END
ELSE
BEGIN
	SELECT @PlanId = CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND PlanCode = N'DEMO-PRODUCER-STD' AND IsDeleted = 0;
	UPDATE Commission.CommissionPlan
	   SET StatusCode = N'Active',
		   StatusCodeId = CASE WHEN COL_LENGTH(N'Commission.CommissionPlan', N'StatusCodeId') IS NOT NULL THEN @ActiveStatusCodeId ELSE StatusCodeId END,
		   NewBusinessRatePct = CASE WHEN NewBusinessRatePct = 0 THEN 12.5000 ELSE NewBusinessRatePct END,
		   RenewalRatePct = CASE WHEN RenewalRatePct = 0 THEN 10.0000 ELSE RenewalRatePct END,
		   EffectiveStartDate = CASE WHEN EffectiveStartDate > '2020-01-01' THEN '2020-01-01' ELSE EffectiveStartDate END,
		   EffectiveEndDate = NULL
	 WHERE CommissionPlanId = @PlanId;
END;

DECLARE @ProducerUsers TABLE (UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
INSERT INTO @ProducerUsers (UserId)
SELECT DISTINCT AssignedToUserId
FROM Submissions.Submission
WHERE TenantId = @TenantId
  AND AssignedToUserId IS NOT NULL
  AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM @ProducerUsers WHERE UserId = '00000000-0000-0000-0000-000000000002')
BEGIN
	INSERT INTO @ProducerUsers (UserId) VALUES ('00000000-0000-0000-0000-000000000002');
END;

DECLARE @UserId UNIQUEIDENTIFIER;
DECLARE producer_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT UserId FROM @ProducerUsers;
OPEN producer_cursor;
FETCH NEXT FROM producer_cursor INTO @UserId;
WHILE @@FETCH_STATUS = 0
BEGIN
	DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 COALESCE(PayeeId, CommissionPayeeId) FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND UserId = @UserId AND CommissionPlanId = @PlanId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
	IF @PayeeId IS NULL SET @PayeeId = NEWID();

	IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND UserId = @UserId AND CommissionPlanId = @PlanId AND IsDeleted = 0)
	BEGIN
		IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPayeeTypeId') IS NOT NULL
		BEGIN
			INSERT INTO Commission.CommissionPayee
				(CommissionPayeeId, PayeeId, TenantId, PayeeCode, PayeeName, UserId, CommissionPlanId, CommissionPayeeTypeId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, IsActive, CreatedDateUtc, IsDeleted)
			VALUES
				(@PayeeId, @PayeeId, @TenantId, CONCAT(N'DEMO-PROD-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @UserId), N'-', N''), 8)), N'Demo Producer', @UserId, @PlanId, @ProducerPayeeTypeId, N'Producer', 100.0000, '2020-01-01', N'Active', 1, SYSUTCDATETIME(), 0);
		END
		ELSE
		BEGIN
			INSERT INTO Commission.CommissionPayee
				(CommissionPayeeId, PayeeId, TenantId, PayeeCode, PayeeName, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, IsActive, CreatedDateUtc, IsDeleted)
			VALUES
				(@PayeeId, @PayeeId, @TenantId, CONCAT(N'DEMO-PROD-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @UserId), N'-', N''), 8)), N'Demo Producer', @UserId, @PlanId, N'Producer', 100.0000, '2020-01-01', N'Active', 1, SYSUTCDATETIME(), 0);
		END;
	END
	ELSE
	BEGIN
		UPDATE Commission.CommissionPayee
		   SET PayeeId = COALESCE(PayeeId, @PayeeId),
			   CommissionPayeeId = COALESCE(CommissionPayeeId, @PayeeId),
			   StatusCode = N'Active',
			   IsActive = 1,
			   SplitPercentage = CASE WHEN SplitPercentage = 0 THEN 100.0000 ELSE SplitPercentage END,
			   EffectiveDate = CASE WHEN EffectiveDate > '2020-01-01' THEN '2020-01-01' ELSE EffectiveDate END
		 WHERE TenantId = @TenantId AND UserId = @UserId AND CommissionPlanId = @PlanId AND IsDeleted = 0;
	END;

	IF NOT EXISTS (SELECT 1 FROM Commission.CommissionSplitRule WHERE TenantId = @TenantId AND CommissionPlanId = @PlanId AND PayeeId = @PayeeId AND IsDeleted = 0)
	BEGIN
		INSERT INTO Commission.CommissionSplitRule
			(SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, PayeeId, SplitPct, OverrideRatePct, Priority, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc, IsDeleted)
		VALUES
			(NEWID(), @TenantId, @PlanId, N'Demo Producer 100% Split', N'Producer', @PayeeId, 100.0000, NULL, 10, '2020-01-01', NULL, N'Active', SYSUTCDATETIME(), 0);
	END
	ELSE
	BEGIN
		UPDATE Commission.CommissionSplitRule
		   SET StatusCode = N'Active',
			   SplitPct = CASE WHEN SplitPct = 0 THEN 100.0000 ELSE SplitPct END,
			   EffectiveStartDate = CASE WHEN EffectiveStartDate > '2020-01-01' THEN '2020-01-01' ELSE EffectiveStartDate END,
			   EffectiveEndDate = NULL
		 WHERE TenantId = @TenantId AND CommissionPlanId = @PlanId AND PayeeId = @PayeeId AND IsDeleted = 0;
	END;

	FETCH NEXT FROM producer_cursor INTO @UserId;
END;
CLOSE producer_cursor;
DEALLOCATE producer_cursor;
