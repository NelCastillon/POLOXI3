SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'Policy.EndorsementType', N'U') IS NULL OR OBJECT_ID(N'Policy.EndorsementTypeProfile', N'U') IS NULL
	THROW 52620, N'The enterprise endorsement catalog must exist before catalog completion runs.', 1;
GO

IF OBJECT_ID(N'Policy.EndorsementTypeApplicability', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeApplicability
	(
		EndorsementTypeApplicabilityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeApplicability PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		LobId UNIQUEIDENTIFIER NULL,
		LineOfBusinessCode NVARCHAR(100) NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		ProductCode NVARCHAR(100) NULL,
		FormCode NVARCHAR(100) NULL,
		CountryCode NCHAR(2) NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Country DEFAULT N'US',
		StateCode NVARCHAR(3) NULL,
		EffectiveFromDate DATE NULL,
		EffectiveToDate DATE NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeApplicability_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_EndorsementTypeApplicability_Dates CHECK (EffectiveToDate IS NULL OR EffectiveFromDate IS NULL OR EffectiveToDate >= EffectiveFromDate),
		CONSTRAINT CK_EndorsementTypeApplicability_Scope CHECK (LobId IS NOT NULL OR LineOfBusinessCode IS NOT NULL),
		CONSTRAINT FK_EndorsementTypeApplicability_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE INDEX IX_EndorsementTypeApplicability_Resolve ON Policy.EndorsementTypeApplicability(TenantId,EndorsementTypeId,LobId,LineOfBusinessCode,CarrierId,ProductCode,StateCode,EffectiveFromDate,EffectiveToDate,IsActive,IsDeleted);
END;
GO

IF OBJECT_ID(N'Agency.LineOfBusiness', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementTypeApplicability_Lob')
	ALTER TABLE Policy.EndorsementTypeApplicability WITH CHECK ADD CONSTRAINT FK_EndorsementTypeApplicability_Lob FOREIGN KEY (LobId) REFERENCES Agency.LineOfBusiness(LobId);
GO

IF OBJECT_ID(N'Agency.Carrier', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementTypeApplicability_Carrier')
	ALTER TABLE Policy.EndorsementTypeApplicability WITH CHECK ADD CONSTRAINT FK_EndorsementTypeApplicability_Carrier FOREIGN KEY (CarrierId) REFERENCES Agency.Carrier(CarrierId);
GO

IF OBJECT_ID(N'tempdb..#EndorsementCatalogCompletion') IS NOT NULL DROP TABLE #EndorsementCatalogCompletion;
CREATE TABLE #EndorsementCatalogCompletion
(
	TypeCode NVARCHAR(50) NOT NULL PRIMARY KEY,
	TypeName NVARCHAR(120) NOT NULL,
	Description NVARCHAR(500) NOT NULL,
	CategoryCode NVARCHAR(50) NOT NULL,
	DefaultOperationCode NVARCHAR(50) NOT NULL,
	LobCode NVARCHAR(100) NOT NULL,
	IsPremiumBearing BIT NOT NULL,
	IsHighRisk BIT NOT NULL,
	IsCertificateRelated BIT NOT NULL,
	RequiresSignedRequest BIT NOT NULL,
	SortOrder INT NOT NULL
);

INSERT #EndorsementCatalogCompletion VALUES
(N'DirectorsOfficersChange',N'Directors and Officers Coverage Change',N'Add, remove, or revise directors and officers liability coverage, limits, retentions, or forms.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2000),
(N'EmploymentPracticesChange',N'Employment Practices Liability Change',N'Change employment practices liability coverage, limits, retentions, or endorsements.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2010),
(N'FiduciaryLiabilityChange',N'Fiduciary Liability Change',N'Change fiduciary liability coverage, limits, retentions, or covered plans.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2020),
(N'CrimeCoverageChange',N'Crime Coverage Change',N'Change employee theft, forgery, funds transfer, social engineering, or other crime coverage.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2030),
(N'ProfessionalServicesChange',N'Professional Services Change',N'Change scheduled professional services, disciplines, revenue, or professional liability terms.',N'Commercial',N'Update',N'PROF-LI',1,1,0,1,2040),
(N'MediaLiabilityChange',N'Media Liability Change',N'Change media activities, content exposures, limits, retentions, or covered services.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2050),
(N'PollutionCoverageChange',N'Pollution Coverage Change',N'Change pollution liability coverage, operations, locations, limits, or remediation terms.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2060),
(N'PollutionLocationChange',N'Pollution Location Change',N'Add, remove, or revise an environmental risk location or scheduled site.',N'Property',N'Update',N'COMM-PC',1,1,0,1,2070),
(N'LiquorLiabilityChange',N'Liquor Liability Change',N'Change liquor liability operations, receipts, limits, or scheduled locations.',N'Commercial',N'Update',N'COMM-PC',1,1,0,1,2080),
(N'BuildersRiskProjectAdd',N'Add Builders Risk Project',N'Add a construction project, location, values, parties, and project term.',N'Property',N'Add',N'COMM-PC',1,1,1,1,2090),
(N'BuildersRiskProjectChange',N'Builders Risk Project Change',N'Change project values, completion date, location, contractors, or construction details.',N'Property',N'Update',N'COMM-PC',1,1,1,1,2100),
(N'InstallationFloaterChange',N'Installation Floater Change',N'Change installation projects, transit/storage exposure, limits, or scheduled property.',N'Property',N'Update',N'COMM-PC',1,1,0,1,2110),
(N'InlandMarineScheduleChange',N'Inland Marine Schedule Change',N'Add, remove, or revise scheduled inland marine property, equipment, or floaters.',N'Property',N'Update',N'COMM-PC',1,1,0,1,2120),
(N'OceanCargoChange',N'Ocean Cargo Change',N'Change cargo interests, voyages, commodities, valuation, limits, or transit terms.',N'Commercial',N'Update',N'COMM-PC',1,1,0,1,2130),
(N'HullMachineryChange',N'Hull and Machinery Change',N'Change scheduled vessels, hull values, navigation territory, or machinery coverage.',N'Property',N'Update',N'COMM-PC',1,1,0,1,2140),
(N'ProtectionIndemnityChange',N'Protection and Indemnity Change',N'Change vessel liability, crew, operations, limits, or navigation territory.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2150),
(N'AviationAircraftAdd',N'Add Aircraft',N'Add an aircraft, insured value, use, territory, pilots, and liability limits.',N'Property',N'Add',N'COMM-PC',1,1,0,1,2160),
(N'AviationAircraftRemove',N'Remove Aircraft',N'Remove a scheduled aircraft from aviation coverage.',N'Property',N'Remove',N'COMM-PC',1,1,0,1,2170),
(N'AviationPilotChange',N'Aviation Pilot Change',N'Add, remove, or revise an approved pilot, qualifications, or restrictions.',N'Driver',N'Update',N'COMM-PC',1,1,0,1,2180),
(N'AirportLiabilityChange',N'Airport Liability Change',N'Change airport, hangarkeepers, products, premises, or operations liability.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2190),
(N'GarageOperationsChange',N'Garage Operations Change',N'Change garage operations, locations, payroll, receipts, limits, or classifications.',N'Commercial',N'Update',N'COMM-PC',1,1,0,1,2200),
(N'DealerInventoryChange',N'Dealer Inventory Change',N'Change dealer inventory, floor-plan interests, locations, or physical damage limits.',N'Vehicle',N'Update',N'COMM-PC',1,1,1,1,2210),
(N'GaragekeepersChange',N'Garagekeepers Coverage Change',N'Change garagekeepers limits, deductibles, locations, or coverage basis.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2220),
(N'SuretyPrincipalChange',N'Surety Principal Change',N'Change principal legal details, ownership, indemnitors, or underwriting information.',N'Insured',N'Update',N'COMM-PC',0,1,0,1,2230),
(N'SuretyBondChange',N'Surety Bond Change',N'Change bond amount, obligee, term, project, or bond conditions.',N'Legal',N'Update',N'COMM-PC',1,1,1,1,2240),
(N'SuretyBondRider',N'Surety Bond Rider',N'Issue a rider changing an active surety bond.',N'Legal',N'Update',N'COMM-PC',1,1,1,1,2250),
(N'FloodCoverageChange',N'Flood Coverage Change',N'Change flood limits, deductibles, building/contents values, mortgagee, or flood form.',N'Coverage',N'Update',N'COMM-PC',1,1,1,1,2260),
(N'EarthquakeCoverageChange',N'Earthquake Coverage Change',N'Change earthquake limits, deductibles, locations, or construction information.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2270),
(N'WindstormCoverageChange',N'Windstorm Coverage Change',N'Change windstorm, named storm, hurricane, or hail terms and deductibles.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2280),
(N'TerrorismCoverageChange',N'Terrorism Coverage Change',N'Accept, reject, or revise terrorism coverage and related premium.',N'Coverage',N'Update',N'COMM-PC',1,1,0,1,2290),
(N'LifeBeneficiaryChange',N'Life Beneficiary Change',N'Add, remove, or revise primary and contingent life insurance beneficiaries.',N'Legal',N'Update',N'LIFE-IN',0,1,0,1,2300),
(N'LifeOwnerChange',N'Life Policy Owner Change',N'Change ownership of a life insurance policy with required authorization.',N'Legal',N'Update',N'LIFE-IN',0,1,0,1,2310),
(N'LifeCoverageAmountChange',N'Life Coverage Amount Change',N'Increase or decrease life insurance face amount or supplemental coverage.',N'Coverage',N'Update',N'LIFE-IN',1,1,0,1,2320),
(N'LifeRiderChange',N'Life Rider Change',N'Add, remove, or revise a life insurance rider.',N'Coverage',N'Update',N'LIFE-IN',1,1,0,1,2330),
(N'LifePremiumModeChange',N'Life Premium Mode Change',N'Change premium frequency, payment method, or billing arrangement.',N'Financial',N'Update',N'LIFE-IN',0,0,0,1,2340),
(N'BenefitsEmployeeEnroll',N'Enroll Benefits Employee',N'Enroll an eligible employee in one or more group benefit plans.',N'Insured',N'Add',N'EMP-BEN',1,0,0,1,2350),
(N'BenefitsEmployeeTerminate',N'Terminate Benefits Employee',N'Terminate employee participation and record the qualifying event and coverage end date.',N'Insured',N'Remove',N'EMP-BEN',1,0,0,1,2360),
(N'BenefitsDependentAdd',N'Add Benefits Dependent',N'Add and enroll an eligible spouse, child, or other dependent.',N'Insured',N'Add',N'EMP-BEN',1,0,0,1,2370),
(N'BenefitsDependentRemove',N'Remove Benefits Dependent',N'Remove a dependent and record the qualifying event and coverage end date.',N'Insured',N'Remove',N'EMP-BEN',1,0,0,1,2380),
(N'BenefitsPlanElectionChange',N'Benefits Plan Election Change',N'Change employee plan, tier, contribution, or coverage election.',N'Coverage',N'Update',N'EMP-BEN',1,0,0,1,2390),
(N'BenefitsEligibilityClassChange',N'Benefits Eligibility Class Change',N'Change employee eligibility class, waiting period, or coverage effective date.',N'Commercial',N'Update',N'EMP-BEN',1,0,0,1,2400),
(N'BenefitsCobraChange',N'COBRA Continuation Change',N'Add, update, or terminate COBRA or state continuation coverage.',N'Coverage',N'Update',N'EMP-BEN',1,1,0,1,2410),
(N'BenefitsContributionChange',N'Benefits Contribution Change',N'Change employer or employee contribution structure.',N'Financial',N'Update',N'EMP-BEN',1,1,0,1,2420),
(N'BenefitsLifeEventChange',N'Benefits Qualifying Life Event',N'Process marriage, birth, divorce, loss of coverage, or another qualifying life event.',N'Administrative',N'Update',N'EMP-BEN',1,0,0,1,2430),
(N'KidnapRansomChange',N'Kidnap and Ransom Change',N'Change covered persons, territories, limits, retentions, or crisis response services.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2440),
(N'PoliticalRiskChange',N'Political Risk Change',N'Change countries, investments, contracts, limits, or political risk perils.',N'Coverage',N'Update',N'PROF-LI',1,1,0,1,2450),
(N'TradeCreditChange',N'Trade Credit Change',N'Change buyers, countries, credit limits, deductibles, or insured receivables.',N'Commercial',N'Update',N'PROF-LI',1,1,0,1,2460);

INSERT Policy.EndorsementType(EndorsementTypeId,TenantId,TypeCode,TypeName,Description,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,catalog.TypeCode,catalog.TypeName,catalog.Description,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM (SELECT TenantId FROM Core.Tenant WHERE IsDeleted=0) tenant CROSS JOIN #EndorsementCatalogCompletion catalog
WHERE NOT EXISTS (SELECT 1 FROM Policy.EndorsementType existing WHERE existing.TenantId=tenant.TenantId AND existing.TypeCode=catalog.TypeCode AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeProfile
(EndorsementTypeProfileId,TenantId,EndorsementTypeId,CategoryCode,DefaultOperationCode,PremiumImpactCode,BillingImpactCode,CommissionImpactCode,AuthorityCode,ApprovalLevelCode,CarrierMethodCode,DocumentDeliveryCode,RequiresCarrierApproval,RequiresUnderwritingReview,RequiresSignedRequest,RequiresClientAuthorization,RequiresCertificateReview,RequiresBrokerOfRecord,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresPolicyVersion,SupportsBackdate,SupportsReversal,IsHighRisk,IsPremiumBearing,IsCertificateRelated,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,catalog.CategoryCode,catalog.DefaultOperationCode,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'PremiumBearing' ELSE N'NonPremium' END,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'AccountingReview' ELSE N'NoBillingImpact' END,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'RecalculateCommission' ELSE N'NoCommissionImpact' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'CarrierApprovalRequired' ELSE N'AgencyAuthority' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'UnderwritingApproval' ELSE N'StandardAuthority' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'CarrierApprovalRequired' ELSE N'AgencyAuthority' END,N'PortalEmail',
	catalog.IsHighRisk,catalog.IsHighRisk,catalog.RequiresSignedRequest,catalog.RequiresSignedRequest,catalog.IsCertificateRelated,0,
	catalog.IsPremiumBearing,catalog.IsPremiumBearing,1,1,0,1,catalog.IsHighRisk,catalog.IsPremiumBearing,catalog.IsCertificateRelated,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN #EndorsementCatalogCompletion catalog ON catalog.TypeCode=type.TypeCode
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeProfile profile WHERE profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsDeleted=0);

INSERT Policy.EndorsementTypeLineOfBusiness
(EndorsementTypeLineOfBusinessId,TenantId,EndorsementTypeId,LineOfBusinessCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,catalog.LobCode,1,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN #EndorsementCatalogCompletion catalog ON catalog.TypeCode=type.TypeCode
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeLineOfBusiness existing WHERE existing.TenantId=type.TenantId AND existing.EndorsementTypeId=type.EndorsementTypeId AND existing.LineOfBusinessCode=catalog.LobCode AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeApplicability
(EndorsementTypeApplicabilityId,TenantId,EndorsementTypeId,LobId,LineOfBusinessCode,CountryCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,lob.LobId,catalog.LobCode,N'US',1,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN #EndorsementCatalogCompletion catalog ON catalog.TypeCode=type.TypeCode
LEFT JOIN Agency.LineOfBusiness lob ON lob.TenantId=type.TenantId AND lob.LobCode=catalog.LobCode AND lob.IsActive=1
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeApplicability existing WHERE existing.TenantId=type.TenantId AND existing.EndorsementTypeId=type.EndorsementTypeId AND existing.LineOfBusinessCode=catalog.LobCode AND existing.CarrierId IS NULL AND existing.ProductCode IS NULL AND existing.FormCode IS NULL AND existing.StateCode IS NULL AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeApplicability
(EndorsementTypeApplicabilityId,TenantId,EndorsementTypeId,LobId,LineOfBusinessCode,CountryCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,CreatedByUserId,ModifiedDateUtc,ModifiedByUserId,IsDeleted)
SELECT NEWID(),scope.TenantId,scope.EndorsementTypeId,lob.LobId,scope.LineOfBusinessCode,N'US',scope.IsDefault,scope.IsActive,scope.SortOrder,scope.CreatedDateUtc,scope.CreatedByUserId,scope.ModifiedDateUtc,scope.ModifiedByUserId,0
FROM Policy.EndorsementTypeLineOfBusiness scope
LEFT JOIN Agency.LineOfBusiness lob ON lob.TenantId=scope.TenantId AND (lob.LobCode=scope.LineOfBusinessCode OR lob.LobName=scope.LineOfBusinessCode) AND lob.IsActive=1
WHERE scope.IsDeleted=0
AND NOT EXISTS
(
	SELECT 1 FROM Policy.EndorsementTypeApplicability existing
	WHERE existing.TenantId=scope.TenantId AND existing.EndorsementTypeId=scope.EndorsementTypeId
	  AND existing.LineOfBusinessCode=scope.LineOfBusinessCode AND existing.CarrierId IS NULL
	  AND existing.ProductCode IS NULL AND existing.FormCode IS NULL AND existing.StateCode IS NULL AND existing.IsDeleted=0
);

INSERT Policy.EndorsementTypeDocumentRequirement
(EndorsementTypeDocumentRequirementId,TenantId,EndorsementTypeId,RequirementCode,DocumentGroupCode,DocumentKindCode,IsRequired,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,N'SignedRequest',N'ENDORSEMENT',N'SIGNED_REQUEST',1,1,10,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile JOIN Policy.EndorsementType type ON type.TenantId=profile.TenantId AND type.EndorsementTypeId=profile.EndorsementTypeId
WHERE profile.RequiresSignedRequest=1 AND profile.IsDeleted=0 AND type.IsDeleted=0
AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeDocumentRequirement existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.RequirementCode=N'SignedRequest' AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeWorkflowRule
(EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,workflowRule.FromStatus,workflowRule.ToStatus,workflowRule.Permission,
	CASE WHEN workflowRule.ToStatus=N'PendingApproval' THEN profile.IsHighRisk ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'SubmittedToCarrier' THEN profile.RequiresCarrierApproval ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresAccountingWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresCommissionWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'Issued' THEN profile.RequiresDocumentWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'Issued' THEN profile.RequiresCertificateReview ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresPolicyVersion ELSE 0 END,1,workflowRule.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile
JOIN #EndorsementCatalogCompletion catalog ON catalog.TypeCode=(SELECT TypeCode FROM Policy.EndorsementType WHERE TenantId=profile.TenantId AND EndorsementTypeId=profile.EndorsementTypeId)
CROSS JOIN (VALUES
	(N'Draft',N'PendingValidation',N'ENDORSEMENT_EDIT_DRAFT',10),(N'PendingValidation',N'Submitted',N'ENDORSEMENT_CREATE',20),(N'Submitted',N'InReview',N'ENDORSEMENT_MANAGE',30),(N'InReview',N'NeedMoreInfo',N'ENDORSEMENT_MANAGE',40),(N'NeedMoreInfo',N'InReview',N'ENDORSEMENT_EDIT_DRAFT',50),(N'InReview',N'PendingApproval',N'ENDORSEMENT_MANAGE',60),(N'PendingApproval',N'Approved',N'ENDORSEMENT_APPROVE',70),(N'Approved',N'SubmittedToCarrier',N'ENDORSEMENT_MANAGE',80),(N'SubmittedToCarrier',N'CarrierProcessing',N'ENDORSEMENT_MANAGE',90),(N'CarrierProcessing',N'CarrierApproved',N'ENDORSEMENT_MANAGE',100),(N'CarrierApproved',N'PolicyUpdated',N'ENDORSEMENT_MANAGE',110),(N'PolicyUpdated',N'Issued',N'ENDORSEMENT_MANAGE',120),(N'Issued',N'Completed',N'ENDORSEMENT_MANAGE',130)
) workflowRule(FromStatus,ToStatus,Permission,SortOrder)
WHERE profile.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeWorkflowRule existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.FromStatusCode=workflowRule.FromStatus AND existing.ToStatusCode=workflowRule.ToStatus AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeCarrierMethod
(EndorsementTypeCarrierMethodId,TenantId,EndorsementTypeId,CarrierMethodCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,profile.CarrierMethodCode,1,1,10,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile JOIN #EndorsementCatalogCompletion catalog ON catalog.TypeCode=(SELECT TypeCode FROM Policy.EndorsementType WHERE TenantId=profile.TenantId AND EndorsementTypeId=profile.EndorsementTypeId)
WHERE profile.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeCarrierMethod existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.CarrierId IS NULL AND existing.LineOfBusinessCode IS NULL AND existing.CarrierMethodCode=profile.CarrierMethodCode AND existing.IsDeleted=0);
GO

