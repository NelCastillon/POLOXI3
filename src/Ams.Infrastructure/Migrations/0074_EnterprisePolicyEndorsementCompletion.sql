SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name=N'Policy') EXEC(N'CREATE SCHEMA Policy');
GO

IF OBJECT_ID(N'Policy.PolicyCoverageDetail', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'PolicyVersionId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD PolicyVersionId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'SourceEndorsementId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD SourceEndorsementId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'SourceChangeId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD SourceChangeId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Submissions.BoundPolicy',N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.BoundPolicy ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Submissions.BoundPolicy',N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Policy.PolicyVersion', N'U') IS NOT NULL
AND COL_LENGTH(N'Policy.PolicyVersion',N'SourceEndorsementId') IS NULL
	ALTER TABLE Policy.PolicyVersion ADD SourceEndorsementId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Policy.PolicyEndorsement', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsement',N'LastTransitionCorrelationId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD LastTransitionCorrelationId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement',N'AppliedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD AppliedDateUtc DATETIME2 NULL;
END;

IF OBJECT_ID(N'Policy.PolicyEndorsementApproval', N'U') IS NOT NULL
AND COL_LENGTH(N'Policy.PolicyEndorsementApproval',N'RowVersion') IS NULL
	ALTER TABLE Policy.PolicyEndorsementApproval ADD RowVersion ROWVERSION;

IF OBJECT_ID(N'Policy.PolicyEndorsementStatusTransition', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementStatusTransition',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementStatusTransition ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementStatusTransition',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementStatusTransition ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Policy.PolicyEndorsementCarrierDispatch', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementCarrierDispatch',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementCarrierDispatch ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementCarrierDispatch',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementCarrierDispatch ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Policy.PolicyEndorsementAccountingWork', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementAccountingWork',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementAccountingWork ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementAccountingWork',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementAccountingWork ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Policy.PolicyEndorsementDocumentWork', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementDocumentWork',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementDocumentWork ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementDocumentWork',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementDocumentWork ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF OBJECT_ID(N'Policy.PolicyCurrentInsured', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCurrentInsured
	(
		PolicyCurrentInsuredId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyCurrentInsured PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		Name NVARCHAR(240) NULL,
		Dba NVARCHAR(240) NULL,
		Fein NVARCHAR(30) NULL,
		Phone NVARCHAR(40) NULL,
		Email NVARCHAR(254) NULL,
		MailingAddress NVARCHAR(1000) NULL,
		GaragingAddress NVARCHAR(1000) NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCurrentInsured_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCurrentInsured_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyCurrentInsured_Current ON Policy.PolicyCurrentInsured(TenantId,PolicyId) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyCurrentInsured_Source ON Policy.PolicyCurrentInsured(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementStatusTransition', N'U') IS NOT NULL
BEGIN
	UPDATE Policy.PolicyEndorsementStatusTransition
	SET CreatesAccountingWork=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FromStatusCode=N'CarrierApproved' AND ToStatusCode=N'PolicyUpdated' AND IsDeleted=0;
	UPDATE Policy.PolicyEndorsementStatusTransition
	SET IsActive=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHERE (FromStatusCode=N'PolicyUpdated' AND ToStatusCode=N'InvoiceCreated')
	   OR (FromStatusCode=N'InvoiceCreated' AND ToStatusCode=N'DocumentsGenerated');
END;
GO

CREATE OR ALTER PROCEDURE Policy.usp_ApplyPolicyEndorsement
	@TenantId UNIQUEIDENTIFIER,
	@PolicyId UNIQUEIDENTIFIER,
	@EndorsementId UNIQUEIDENTIFIER,
	@PolicyVersionId UNIQUEIDENTIFIER,
	@VersionNumber INT,
	@ActorUserId UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @PolicyNumber NVARCHAR(80),@LineOfBusiness NVARCHAR(100),@EffectiveDate DATETIME2,@ExpirationDate DATETIME2,@PremiumDelta DECIMAL(18,2);
	SELECT @PolicyNumber=policy.PolicyNumber,@LineOfBusiness=policy.LineOfBusiness,@EffectiveDate=policy.EffectiveDate,@ExpirationDate=policy.ExpirationDate
	FROM Submissions.BoundPolicy policy WITH(UPDLOCK,HOLDLOCK)
	WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0;
	SELECT @PremiumDelta=PremiumDelta FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND PolicyId=@PolicyId AND IsDeleted=0;
	IF @PolicyNumber IS NULL OR @PremiumDelta IS NULL THROW 52510,N'The policy or endorsement could not be activated.',1;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyCurrentInsured currentState
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND currentState.IsDeleted=0
	  AND EXISTS(SELECT 1 FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementInsuredChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0);
	INSERT Policy.PolicyCurrentInsured(PolicyCurrentInsuredId,TenantId,PolicyId,PolicyVersionId,Name,Dba,Fein,Phone,Email,MailingAddress,GaragingAddress,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(typed.AfterName,typed.BeforeName),COALESCE(typed.AfterDba,typed.BeforeDba),COALESCE(typed.AfterFein,typed.BeforeFein),COALESCE(typed.AfterPhone,typed.BeforePhone),COALESCE(typed.AfterEmail,typed.BeforeEmail),COALESCE(typed.AfterMailingAddress,typed.BeforeMailingAddress),COALESCE(typed.AfterGaragingAddress,typed.BeforeGaragingAddress),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementInsuredChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyVehicle currentState JOIN Policy.PolicyEndorsementChange change ON change.TenantId=currentState.TenantId AND change.EndorsementId=@EndorsementId AND COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),change.ChangeId))=currentState.EntityKey
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND change.CategoryCode=N'Vehicle' AND currentState.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyVehicle(PolicyVehicleId,TenantId,PolicyId,PolicyVersionId,EntityKey,Vin,ModelYear,Make,Model,UsageCode,GaragingAddress,Lienholder,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),COALESCE(typed.AfterVehicleId,typed.BeforeVehicleId,change.ChangeId))),COALESCE(typed.AfterVin,typed.BeforeVin),COALESCE(typed.AfterYear,typed.BeforeYear),COALESCE(typed.AfterMake,typed.BeforeMake),COALESCE(typed.AfterModel,typed.BeforeModel),COALESCE(typed.AfterUsageCode,typed.BeforeUsageCode),COALESCE(typed.AfterGaragingAddress,typed.BeforeGaragingAddress),COALESCE(typed.AfterLienholder,typed.BeforeLienholder),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementVehicleChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyDriver currentState JOIN Policy.PolicyEndorsementChange change ON change.TenantId=currentState.TenantId AND change.EndorsementId=@EndorsementId AND COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),change.ChangeId))=currentState.EntityKey
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND change.CategoryCode=N'Driver' AND currentState.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyDriver(PolicyDriverId,TenantId,PolicyId,PolicyVersionId,EntityKey,DriverName,LicenseNumber,LicenseState,BirthDate,IsExcluded,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),COALESCE(typed.AfterDriverId,typed.BeforeDriverId,change.ChangeId))),COALESCE(typed.AfterName,typed.BeforeName),COALESCE(typed.AfterLicenseNumber,typed.BeforeLicenseNumber),COALESCE(typed.AfterLicenseState,typed.BeforeLicenseState),COALESCE(typed.AfterBirthDate,typed.BeforeBirthDate),COALESCE(typed.AfterExcluded,typed.BeforeExcluded),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementDriverChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE coverage SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyCoverageDetail coverage JOIN Policy.PolicyEndorsementChange change ON change.TenantId=coverage.TenantId AND change.EndorsementId=@EndorsementId JOIN Policy.PolicyEndorsementCoverageChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId AND typed.CoverageCode=coverage.CoverageCode
	WHERE coverage.TenantId=@TenantId AND coverage.PolicyId=@PolicyId AND coverage.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyCoverageDetail(CoverageDetailId,TenantId,PolicyId,PolicyNumber,CoverageCode,CoverageName,LineOfBusinessCode,CoverageCategoryCode,CoverageFormCode,CoverageTriggerCode,OccurrenceLimit,Deductible,Premium,EffectiveDate,ExpirationDate,PolicyVersionId,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyNumber,COALESCE(NULLIF(typed.CoverageCode,N''),CONVERT(NVARCHAR(36),change.ChangeId)),COALESCE(typed.AfterCoverageName,typed.BeforeCoverageName,N'Coverage'),COALESCE(@LineOfBusiness,N''),N'Endorsement',N'Endorsement',N'Occurrence',COALESCE(typed.AfterLimitAmount,typed.BeforeLimitAmount),COALESCE(typed.AfterDeductibleAmount,typed.BeforeDeductibleAmount),COALESCE(typed.AfterPremiumAmount,typed.BeforePremiumAmount,0),@EffectiveDate,@ExpirationDate,@PolicyVersionId,@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementCoverageChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyProperty currentState JOIN Policy.PolicyEndorsementChange change ON change.TenantId=currentState.TenantId AND change.EndorsementId=@EndorsementId AND COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),change.ChangeId))=currentState.EntityKey
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND change.CategoryCode=N'Property' AND currentState.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyProperty(PolicyPropertyId,TenantId,PolicyId,PolicyVersionId,EntityKey,LocationAddress,BuildingNumber,OccupancyCode,ConstructionCode,SquareFeet,BuildingValue,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),COALESCE(typed.AfterPropertyId,typed.BeforePropertyId,change.ChangeId))),COALESCE(typed.AfterLocationAddress,typed.BeforeLocationAddress),COALESCE(typed.AfterBuildingNumber,typed.BeforeBuildingNumber),COALESCE(typed.AfterOccupancyCode,typed.BeforeOccupancyCode),COALESCE(typed.AfterConstructionCode,typed.BeforeConstructionCode),COALESCE(typed.AfterSquareFeet,typed.BeforeSquareFeet),COALESCE(typed.AfterBuildingValue,typed.BeforeBuildingValue),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementPropertyChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyCommercialExposure currentState JOIN Policy.PolicyEndorsementChange change ON change.TenantId=currentState.TenantId AND change.EndorsementId=@EndorsementId AND COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),change.ChangeId))=currentState.EntityKey
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND change.CategoryCode=N'Commercial' AND currentState.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyCommercialExposure(PolicyCommercialExposureId,TenantId,PolicyId,PolicyVersionId,EntityKey,ClassificationCode,PayrollAmount,RevenueAmount,EmployeeCount,EquipmentValue,BlanketLimit,LocationCount,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(NULLIF(change.EntityKey,N''),COALESCE(NULLIF(typed.ClassificationCode,N''),CONVERT(NVARCHAR(36),change.ChangeId))),typed.ClassificationCode,COALESCE(typed.AfterPayrollAmount,typed.BeforePayrollAmount),COALESCE(typed.AfterRevenueAmount,typed.BeforeRevenueAmount),COALESCE(typed.AfterEmployeeCount,typed.BeforeEmployeeCount),COALESCE(typed.AfterEquipmentValue,typed.BeforeEquipmentValue),COALESCE(typed.AfterBlanketLimit,typed.BeforeBlanketLimit),COALESCE(typed.AfterLocationCount,typed.BeforeLocationCount),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementCommercialChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyFinancialTerms currentState
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND currentState.IsDeleted=0
	  AND EXISTS(SELECT 1 FROM Policy.PolicyEndorsementChange change WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.CategoryCode=N'Financial' AND change.IsDeleted=0);
	INSERT Policy.PolicyFinancialTerms(PolicyFinancialTermsId,TenantId,PolicyId,PolicyVersionId,BillingPlanCode,FinancingProvider,InstallmentCount,CommissionRate,CommissionAmount,FinancedAmount,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(typed.AfterBillingPlanCode,typed.BeforeBillingPlanCode),COALESCE(typed.AfterFinancingProvider,typed.BeforeFinancingProvider),COALESCE(typed.AfterInstallmentCount,typed.BeforeInstallmentCount),COALESCE(typed.AfterCommissionRate,typed.BeforeCommissionRate),COALESCE(typed.AfterCommissionAmount,typed.BeforeCommissionAmount),COALESCE(typed.AfterFinancedAmount,typed.BeforeFinancedAmount),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementFinancialChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE currentState SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	FROM Policy.PolicyLegalInterest currentState JOIN Policy.PolicyEndorsementChange change ON change.TenantId=currentState.TenantId AND change.EndorsementId=@EndorsementId AND COALESCE(NULLIF(change.EntityKey,N''),CONVERT(NVARCHAR(36),change.ChangeId))=currentState.EntityKey
	WHERE currentState.TenantId=@TenantId AND currentState.PolicyId=@PolicyId AND change.CategoryCode=N'Legal' AND currentState.IsDeleted=0 AND change.IsDeleted=0;
	INSERT Policy.PolicyLegalInterest(PolicyLegalInterestId,TenantId,PolicyId,PolicyVersionId,EntityKey,PartyTypeCode,PartyName,RelationshipCode,PartyAddress,ReferenceNumber,SourceEndorsementId,SourceChangeId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@TenantId,@PolicyId,@PolicyVersionId,COALESCE(NULLIF(change.EntityKey,N''),CONCAT(typed.PartyTypeCode,N':',COALESCE(typed.AfterReferenceNumber,typed.BeforeReferenceNumber,CONVERT(NVARCHAR(36),change.ChangeId)))),typed.PartyTypeCode,COALESCE(typed.AfterPartyName,typed.BeforePartyName),COALESCE(typed.AfterRelationshipCode,typed.BeforeRelationshipCode),COALESCE(typed.AfterAddress,typed.BeforeAddress),COALESCE(typed.AfterReferenceNumber,typed.BeforeReferenceNumber),@EndorsementId,change.ChangeId,SYSUTCDATETIME(),@ActorUserId,0
	FROM Policy.PolicyEndorsementChange change JOIN Policy.PolicyEndorsementLegalChange typed ON typed.TenantId=change.TenantId AND typed.ChangeId=change.ChangeId
	WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.OperationCode<>N'Remove' AND change.IsDeleted=0;

	UPDATE Submissions.BoundPolicy SET AnnualPremium=COALESCE(AnnualPremium,0)+@PremiumDelta,CurrentPolicyVersionId=@PolicyVersionId,CurrentVersionNumber=@VersionNumber,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
	WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;

	DECLARE @SnapshotJson NVARCHAR(MAX)=(SELECT policy.PolicyId,policy.PolicyNumber,policy.AccountId,policy.CarrierId,policy.LineOfBusiness,policy.EffectiveDate,policy.ExpirationDate,policy.AnnualPremium,@EndorsementId endorsementId,@VersionNumber versionNumber,
		JSON_QUERY((SELECT * FROM Policy.PolicyCurrentInsured WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) insured,
		JSON_QUERY((SELECT * FROM Policy.PolicyVehicle WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) vehicles,
		JSON_QUERY((SELECT * FROM Policy.PolicyDriver WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) drivers,
		JSON_QUERY((SELECT CoverageDetailId,CoverageCode,CoverageName,OccurrenceLimit,AggregateLimit,Sublimit,Deductible,Premium FROM Policy.PolicyCoverageDetail WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) coverages,
		JSON_QUERY((SELECT * FROM Policy.PolicyProperty WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) properties,
		JSON_QUERY((SELECT * FROM Policy.PolicyCommercialExposure WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) commercialExposures,
		JSON_QUERY((SELECT * FROM Policy.PolicyFinancialTerms WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) financialTerms,
		JSON_QUERY((SELECT * FROM Policy.PolicyLegalInterest WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 FOR JSON PATH)) legalInterests
	FROM Submissions.BoundPolicy policy WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
	INSERT Policy.PolicyVersion(PolicyVersionId,TenantId,PolicyId,PolicyTermId,PolicyTransactionId,VersionNumber,VersionReasonCode,SnapshotJson,SourceEndorsementId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT @PolicyVersionId,@TenantId,@PolicyId,(SELECT TOP 1 PolicyTermId FROM Policy.PolicyTerm WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY TermNumber DESC),NULL,@VersionNumber,N'Endorsement',@SnapshotJson,@EndorsementId,SYSUTCDATETIME(),@ActorUserId,0;
	UPDATE Policy.PolicyEndorsement SET PolicyVersionAfterId=@PolicyVersionId,AppliedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
END;
GO

IF OBJECT_ID(N'Policy.PolicyVehicle', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyVehicle
	(
		PolicyVehicleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyVehicle PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		EntityKey NVARCHAR(200) NOT NULL,
		Vin NVARCHAR(50) NULL,
		ModelYear INT NULL,
		Make NVARCHAR(100) NULL,
		Model NVARCHAR(100) NULL,
		UsageCode NVARCHAR(80) NULL,
		GaragingAddress NVARCHAR(1000) NULL,
		Lienholder NVARCHAR(240) NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyVehicle_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyVehicle_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyVehicle_Current ON Policy.PolicyVehicle(TenantId,PolicyId,EntityKey) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyVehicle_Source ON Policy.PolicyVehicle(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyDriver', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyDriver
	(
		PolicyDriverId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyDriver PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		EntityKey NVARCHAR(200) NOT NULL,
		DriverName NVARCHAR(240) NULL,
		LicenseNumber NVARCHAR(100) NULL,
		LicenseState NVARCHAR(10) NULL,
		BirthDate DATE NULL,
		IsExcluded BIT NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyDriver_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyDriver_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyDriver_Current ON Policy.PolicyDriver(TenantId,PolicyId,EntityKey) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyDriver_Source ON Policy.PolicyDriver(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyProperty', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyProperty
	(
		PolicyPropertyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyProperty PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		EntityKey NVARCHAR(200) NOT NULL,
		LocationAddress NVARCHAR(1000) NULL,
		BuildingNumber NVARCHAR(80) NULL,
		OccupancyCode NVARCHAR(100) NULL,
		ConstructionCode NVARCHAR(100) NULL,
		SquareFeet INT NULL,
		BuildingValue DECIMAL(18,2) NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyProperty_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyProperty_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyProperty_Current ON Policy.PolicyProperty(TenantId,PolicyId,EntityKey) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyProperty_Source ON Policy.PolicyProperty(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyCommercialExposure', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCommercialExposure
	(
		PolicyCommercialExposureId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyCommercialExposure PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		EntityKey NVARCHAR(200) NOT NULL,
		ClassificationCode NVARCHAR(100) NULL,
		PayrollAmount DECIMAL(18,2) NULL,
		RevenueAmount DECIMAL(18,2) NULL,
		EmployeeCount INT NULL,
		EquipmentValue DECIMAL(18,2) NULL,
		BlanketLimit DECIMAL(18,2) NULL,
		LocationCount INT NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCommercialExposure_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCommercialExposure_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyCommercialExposure_Current ON Policy.PolicyCommercialExposure(TenantId,PolicyId,EntityKey) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyCommercialExposure_Source ON Policy.PolicyCommercialExposure(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyFinancialTerms', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyFinancialTerms
	(
		PolicyFinancialTermsId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyFinancialTerms PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		BillingPlanCode NVARCHAR(100) NULL,
		FinancingProvider NVARCHAR(240) NULL,
		InstallmentCount INT NULL,
		CommissionRate DECIMAL(9,4) NULL,
		CommissionAmount DECIMAL(18,2) NULL,
		FinancedAmount DECIMAL(18,2) NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyFinancialTerms_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyFinancialTerms_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyFinancialTerms_Current ON Policy.PolicyFinancialTerms(TenantId,PolicyId) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyFinancialTerms_Source ON Policy.PolicyFinancialTerms(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyLegalInterest', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyLegalInterest
	(
		PolicyLegalInterestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyLegalInterest PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyVersionId UNIQUEIDENTIFIER NOT NULL,
		EntityKey NVARCHAR(200) NOT NULL,
		PartyTypeCode NVARCHAR(100) NOT NULL,
		PartyName NVARCHAR(240) NULL,
		RelationshipCode NVARCHAR(100) NULL,
		PartyAddress NVARCHAR(1000) NULL,
		ReferenceNumber NVARCHAR(100) NULL,
		SourceEndorsementId UNIQUEIDENTIFIER NOT NULL,
		SourceChangeId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyLegalInterest_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyLegalInterest_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyLegalInterest_Current ON Policy.PolicyLegalInterest(TenantId,PolicyId,EntityKey) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_PolicyLegalInterest_Source ON Policy.PolicyLegalInterest(TenantId,SourceChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyCoverageDetail', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'PolicyVersionId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD PolicyVersionId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'SourceEndorsementId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD SourceEndorsementId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyCoverageDetail',N'SourceChangeId') IS NULL ALTER TABLE Policy.PolicyCoverageDetail ADD SourceChangeId UNIQUEIDENTIFIER NULL;
	IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Policy.PolicyCoverageDetail') AND name=N'UX_PolicyCoverageDetail_SourceChange')
		CREATE UNIQUE INDEX UX_PolicyCoverageDetail_SourceChange ON Policy.PolicyCoverageDetail(TenantId,SourceChangeId) WHERE SourceChangeId IS NOT NULL;
END;
GO

IF OBJECT_ID(N'Policy.PolicyVersion', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyVersion',N'SourceEndorsementId') IS NULL ALTER TABLE Policy.PolicyVersion ADD SourceEndorsementId UNIQUEIDENTIFIER NULL;
	IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Policy.PolicyVersion') AND name=N'UX_PolicyVersion_Endorsement')
		CREATE UNIQUE INDEX UX_PolicyVersion_Endorsement ON Policy.PolicyVersion(TenantId,SourceEndorsementId) WHERE SourceEndorsementId IS NOT NULL AND IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsement', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsement',N'LastTransitionCorrelationId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD LastTransitionCorrelationId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement',N'AppliedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD AppliedDateUtc DATETIME2 NULL;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementApproval', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementApproval',N'RowVersion') IS NULL ALTER TABLE Policy.PolicyEndorsementApproval ADD RowVersion ROWVERSION;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementEvent', N'U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Policy.PolicyEndorsementEvent') AND name=N'UX_EndorsementEvent_Command')
	CREATE UNIQUE INDEX UX_EndorsementEvent_Command ON Policy.PolicyEndorsementEvent(TenantId,EndorsementId,CorrelationId,EventTypeCode);
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCarrierDispatch', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementCarrierDispatch',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementCarrierDispatch ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementCarrierDispatch',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementCarrierDispatch ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementAccountingWork', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementAccountingWork',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementAccountingWork ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementAccountingWork',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementAccountingWork ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementDocumentWork', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsementDocumentWork',N'ModifiedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsementDocumentWork ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsementDocumentWork',N'ModifiedByUserId') IS NULL ALTER TABLE Policy.PolicyEndorsementDocumentWork ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;
GO
