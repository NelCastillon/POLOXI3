SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Policy') EXEC(N'CREATE SCHEMA Policy');
GO

IF OBJECT_ID(N'Policy.EndorsementType', N'U') IS NULL
	THROW 52600, N'Policy.EndorsementType must exist before the enterprise endorsement catalog migration runs.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Policy.EndorsementType') AND name = N'UX_EndorsementType_TenantId')
	CREATE UNIQUE INDEX UX_EndorsementType_TenantId ON Policy.EndorsementType(TenantId, EndorsementTypeId);
GO

IF OBJECT_ID(N'Policy.EndorsementTypeProfile', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeProfile
	(
		EndorsementTypeProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeProfile PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		CategoryCode NVARCHAR(50) NOT NULL,
		DefaultOperationCode NVARCHAR(50) NOT NULL,
		PremiumImpactCode NVARCHAR(50) NOT NULL,
		BillingImpactCode NVARCHAR(50) NOT NULL,
		CommissionImpactCode NVARCHAR(50) NOT NULL,
		AuthorityCode NVARCHAR(50) NOT NULL,
		ApprovalLevelCode NVARCHAR(50) NOT NULL,
		CarrierMethodCode NVARCHAR(50) NOT NULL,
		DocumentDeliveryCode NVARCHAR(50) NOT NULL,
		RequiresCarrierApproval BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_CarrierApproval DEFAULT 0,
		RequiresUnderwritingReview BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Underwriting DEFAULT 0,
		RequiresSignedRequest BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_SignedRequest DEFAULT 0,
		RequiresClientAuthorization BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_ClientAuthorization DEFAULT 0,
		RequiresCertificateReview BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_CertificateReview DEFAULT 0,
		RequiresBrokerOfRecord BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_BrokerOfRecord DEFAULT 0,
		RequiresAccountingWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Accounting DEFAULT 0,
		RequiresCommissionWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Commission DEFAULT 0,
		RequiresDocumentWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Document DEFAULT 1,
		RequiresPolicyVersion BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_PolicyVersion DEFAULT 1,
		SupportsBackdate BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Backdate DEFAULT 0,
		SupportsReversal BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Reversal DEFAULT 1,
		IsHighRisk BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_HighRisk DEFAULT 0,
		IsPremiumBearing BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_PremiumBearing DEFAULT 0,
		IsCertificateRelated BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_CertificateRelated DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeProfile_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_EndorsementTypeProfile_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementTypeProfile_Type ON Policy.EndorsementTypeProfile(TenantId, EndorsementTypeId) WHERE IsDeleted = 0;
	CREATE INDEX IX_EndorsementTypeProfile_Category ON Policy.EndorsementTypeProfile(TenantId, CategoryCode, IsActive, IsDeleted, SortOrder);
END;
GO

IF OBJECT_ID(N'tempdb..#EndorsementTenants') IS NOT NULL DROP TABLE #EndorsementTenants;
CREATE TABLE #EndorsementTenants (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
	INSERT #EndorsementTenants(TenantId) SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM #EndorsementTenants)
	INSERT #EndorsementTenants VALUES ('00000000-0000-0000-0000-000000000001');

IF OBJECT_ID(N'tempdb..#EndorsementCatalog') IS NOT NULL DROP TABLE #EndorsementCatalog;
CREATE TABLE #EndorsementCatalog
(
	TypeCode NVARCHAR(50) NOT NULL PRIMARY KEY, TypeName NVARCHAR(120) NOT NULL, Description NVARCHAR(500) NOT NULL,
	CategoryCode NVARCHAR(50) NOT NULL, DefaultOperationCode NVARCHAR(50) NOT NULL, LobCode NVARCHAR(100) NOT NULL,
	IsPremiumBearing BIT NOT NULL, IsHighRisk BIT NOT NULL, IsCertificateRelated BIT NOT NULL, SortOrder INT NOT NULL
);
INSERT #EndorsementCatalog VALUES
(N'NamedInsuredAdd',N'Add Named Insured',N'Add a legal named insured to the policy.',N'Insured',N'Add',N'*',0,1,0,10),
(N'NamedInsuredRemove',N'Remove Named Insured',N'Remove a legal named insured from the policy.',N'Insured',N'Remove',N'*',0,1,0,20),
(N'NamedInsuredCorrect',N'Correct Named Insured',N'Correct a legal name, DBA, FEIN, or entity detail.',N'Insured',N'Correct',N'*',0,0,0,30),
(N'DBAChange',N'DBA / Trade Name Change',N'Add, remove, or correct a DBA or trade name.',N'Insured',N'Update',N'*',0,0,0,40),
(N'EntityTypeChange',N'Entity Type Change',N'Change the insured legal entity type.',N'Insured',N'Update',N'*',0,1,0,50),
(N'OwnershipChange',N'Ownership Change',N'Record merger, acquisition, or ownership changes.',N'Insured',N'Update',N'*',0,1,0,60),
(N'FEINChange',N'FEIN / Tax ID Change',N'Correct the insured tax identifier.',N'Insured',N'Correct',N'*',0,1,0,70),
(N'AdditionalInsuredAdd',N'Add Additional Insured',N'Add additional insured status and applicable wording.',N'Legal',N'Add',N'*',0,0,1,80),
(N'AdditionalInsuredRemove',N'Remove Additional Insured',N'Remove additional insured status.',N'Legal',N'Remove',N'*',0,0,1,90),
(N'AdditionalInsuredChange',N'Change Additional Insured',N'Modify additional insured details or scope.',N'Legal',N'Update',N'*',0,0,1,100),
(N'LossPayeeAdd',N'Add Loss Payee',N'Add a loss payee to covered property or equipment.',N'Legal',N'Add',N'*',0,0,1,110),
(N'LossPayeeRemove',N'Remove Loss Payee',N'Remove a loss payee.',N'Legal',N'Remove',N'*',0,0,1,120),
(N'MortgageeAdd',N'Add Mortgagee',N'Add a lender or mortgagee.',N'Legal',N'Add',N'Commercial Property',0,0,1,130),
(N'MortgageeRemove',N'Remove Mortgagee',N'Remove a lender or mortgagee.',N'Legal',N'Remove',N'Commercial Property',0,0,1,140),
(N'LienholderAdd',N'Add Lienholder',N'Add a vehicle or equipment lienholder.',N'Legal',N'Add',N'Commercial Auto',0,0,1,150),
(N'LienholderRemove',N'Remove Lienholder',N'Remove a vehicle or equipment lienholder.',N'Legal',N'Remove',N'Commercial Auto',0,0,1,160),
(N'WaiverSubrogationAdd',N'Add Waiver of Subrogation',N'Add waiver of subrogation wording.',N'Coverage',N'Add',N'*',1,1,1,170),
(N'PrimaryNonContributoryAdd',N'Add Primary and Non-Contributory',N'Add primary and non-contributory wording.',N'Coverage',N'Add',N'*',1,1,1,180),
(N'ContractualRequirementChange',N'Contractual Requirement Change',N'Change coverage to satisfy a contractual requirement.',N'Coverage',N'Update',N'*',1,1,1,190),
(N'MailingAddressChange',N'Mailing Address Change',N'Change the insured mailing address.',N'Insured',N'Update',N'*',0,0,0,200),
(N'LocationAddressChange',N'Location Address Change',N'Change a covered physical location address.',N'Property',N'Update',N'*',1,1,0,210),
(N'BillingAddressChange',N'Billing Address Change',N'Change the billing address.',N'Financial',N'Update',N'*',0,0,0,220),
(N'ContactChange',N'Contact Change',N'Change policy contact information.',N'Insured',N'Update',N'*',0,0,0,230),
(N'ProducerChange',N'Producer / Servicing Team Change',N'Change producer or servicing assignment.',N'Administrative',N'Update',N'*',0,0,0,240),
(N'AgencyOfRecordChange',N'Agency of Record Change',N'Change agency of record or broker of record.',N'Legal',N'Update',N'*',0,1,0,250),
(N'PolicyCorrection',N'Policy Correction',N'Correct clerical or administrative policy data.',N'Administrative',N'Correct',N'*',0,0,0,260),
(N'CoverageAdd',N'Add Coverage',N'Add coverage, a coverage part, or policy form.',N'Coverage',N'Add',N'*',1,1,0,270),
(N'CoverageRemove',N'Remove Coverage',N'Remove coverage, a coverage part, or policy form.',N'Coverage',N'Remove',N'*',1,1,0,280),
(N'CoverageChange',N'Change Coverage',N'Modify coverage terms or conditions.',N'Coverage',N'Update',N'*',1,1,0,290),
(N'LimitIncrease',N'Increase Limit',N'Increase a policy, coverage, or sublimit.',N'Coverage',N'Update',N'*',1,1,0,300),
(N'LimitDecrease',N'Decrease Limit',N'Decrease a policy, coverage, or sublimit.',N'Coverage',N'Update',N'*',1,1,0,310),
(N'DeductibleIncrease',N'Increase Deductible',N'Increase a deductible or retention.',N'Coverage',N'Update',N'*',1,1,0,320),
(N'DeductibleDecrease',N'Decrease Deductible',N'Decrease a deductible or retention.',N'Coverage',N'Update',N'*',1,1,0,330),
(N'ExclusionAdd',N'Add Exclusion',N'Add an exclusion or restrictive form.',N'Coverage',N'Add',N'*',1,1,0,340),
(N'ExclusionRemove',N'Remove Exclusion',N'Remove an exclusion or restrictive form.',N'Coverage',N'Remove',N'*',1,1,0,350),
(N'FormAdd',N'Add Policy Form',N'Add a carrier, manuscript, or coverage form.',N'Coverage',N'Add',N'*',0,1,0,360),
(N'FormRemove',N'Remove Policy Form',N'Remove a policy form.',N'Coverage',N'Remove',N'*',0,1,0,370),
(N'FormChange',N'Change Policy Form',N'Replace or modify a policy form.',N'Coverage',N'Replace',N'*',0,1,0,380),
(N'VehicleAdd',N'Add Vehicle',N'Add a vehicle or fleet unit.',N'Vehicle',N'Add',N'Commercial Auto',1,0,1,390),
(N'VehicleRemove',N'Remove Vehicle',N'Remove a vehicle or fleet unit.',N'Vehicle',N'Remove',N'Commercial Auto',1,0,1,400),
(N'VehicleReplace',N'Replace Vehicle',N'Replace one vehicle with another.',N'Vehicle',N'Replace',N'Commercial Auto',1,0,1,410),
(N'VehicleChange',N'Change Vehicle Details',N'Change VIN, usage, radius, classification, or garaging.',N'Vehicle',N'Update',N'Commercial Auto',1,0,1,420),
(N'DriverAdd',N'Add Driver',N'Add a driver or operator.',N'Driver',N'Add',N'Commercial Auto',1,1,0,430),
(N'DriverRemove',N'Remove Driver',N'Remove a driver or operator.',N'Driver',N'Remove',N'Commercial Auto',1,1,0,440),
(N'DriverChange',N'Change Driver Details',N'Change driver license or rating information.',N'Driver',N'Update',N'Commercial Auto',1,1,0,450),
(N'DriverExclude',N'Exclude Driver',N'Add a driver exclusion.',N'Driver',N'Update',N'Commercial Auto',1,1,0,460),
(N'DriverReinstate',N'Reinstate Driver',N'Remove a driver exclusion or reinstate a driver.',N'Driver',N'Reinstate',N'Commercial Auto',1,1,0,470),
(N'GaragingChange',N'Garaging Address Change',N'Change vehicle garaging.',N'Vehicle',N'Update',N'Commercial Auto',1,0,0,480),
(N'FleetScheduleChange',N'Fleet Schedule Change',N'Apply a bulk fleet schedule change.',N'Vehicle',N'Update',N'Commercial Auto',1,1,0,490),
(N'LocationAdd',N'Add Location',N'Add a covered premises or location.',N'Property',N'Add',N'Commercial Property',1,1,0,500),
(N'LocationRemove',N'Remove Location',N'Remove a covered premises or location.',N'Property',N'Remove',N'Commercial Property',1,1,0,510),
(N'LocationChange',N'Change Location',N'Modify covered location details.',N'Property',N'Update',N'Commercial Property',1,1,0,520),
(N'BuildingAdd',N'Add Building',N'Add a building to the property schedule.',N'Property',N'Add',N'Commercial Property',1,1,0,530),
(N'BuildingRemove',N'Remove Building',N'Remove a building from the property schedule.',N'Property',N'Remove',N'Commercial Property',1,1,0,540),
(N'BuildingValueChange',N'Building Value Change',N'Change building limit or valuation.',N'Property',N'Update',N'Commercial Property',1,1,0,550),
(N'BusinessPersonalPropertyChange',N'BPP Value Change',N'Change business personal property value.',N'Property',N'Update',N'Commercial Property',1,1,0,560),
(N'EquipmentAdd',N'Add Equipment',N'Add scheduled equipment.',N'Property',N'Add',N'Inland Marine',1,0,0,570),
(N'EquipmentRemove',N'Remove Equipment',N'Remove scheduled equipment.',N'Property',N'Remove',N'Inland Marine',1,0,0,580),
(N'EquipmentValueChange',N'Equipment Value Change',N'Change equipment value or schedule data.',N'Property',N'Update',N'Inland Marine',1,0,0,590),
(N'ProtectiveSafeguardChange',N'Protective Safeguard Change',N'Change alarm, sprinkler, or safeguard requirements.',N'Property',N'Update',N'Commercial Property',1,1,0,600),
(N'ClassCodeAdd',N'Add GL Class Code',N'Add a general liability classification.',N'Commercial',N'Add',N'General Liability',1,1,0,610),
(N'ClassCodeRemove',N'Remove GL Class Code',N'Remove a general liability classification.',N'Commercial',N'Remove',N'General Liability',1,1,0,620),
(N'ClassCodeChange',N'Change GL Class Code',N'Change classification, territory, or exposure basis.',N'Commercial',N'Update',N'General Liability',1,1,0,630),
(N'SalesPayrollChange',N'Sales / Payroll Exposure Change',N'Change liability rating exposure.',N'Commercial',N'Update',N'General Liability',1,0,0,640),
(N'ProductCompletedOpsChange',N'Products / Completed Operations Change',N'Change products or completed operations exposure.',N'Commercial',N'Update',N'General Liability',1,1,0,650),
(N'UmbrellaUnderlyingChange',N'Underlying Schedule Change',N'Change an umbrella underlying policy schedule.',N'Coverage',N'Update',N'Umbrella',1,1,0,660),
(N'UmbrellaLimitChange',N'Umbrella Limit Change',N'Change an umbrella or excess limit.',N'Coverage',N'Update',N'Umbrella',1,1,0,670),
(N'WCClassCodeAdd',N'Add WC Class Code',N'Add a workers compensation classification.',N'Commercial',N'Add',N'Workers Compensation',1,1,0,680),
(N'WCClassCodeRemove',N'Remove WC Class Code',N'Remove a workers compensation classification.',N'Commercial',N'Remove',N'Workers Compensation',1,1,0,690),
(N'WCClassCodeChange',N'Change WC Class Code',N'Change a workers compensation classification.',N'Commercial',N'Update',N'Workers Compensation',1,1,0,700),
(N'WCPayrollChange',N'Workers Compensation Payroll Change',N'Change workers compensation payroll exposure.',N'Commercial',N'Update',N'Workers Compensation',1,0,0,710),
(N'WCStateAdd',N'Add WC State',N'Add workers compensation state coverage.',N'Coverage',N'Add',N'Workers Compensation',1,1,0,720),
(N'WCStateRemove',N'Remove WC State',N'Remove workers compensation state coverage.',N'Coverage',N'Remove',N'Workers Compensation',1,1,0,730),
(N'OfficerIncludeExclude',N'Officer Include / Exclude',N'Include or exclude an officer or member.',N'Commercial',N'Update',N'Workers Compensation',1,1,0,740),
(N'ExperienceModChange',N'Experience Mod Change',N'Change the experience modification factor.',N'Financial',N'Update',N'Workers Compensation',1,1,0,750),
(N'PersonalVehicleAdd',N'Add Personal Auto Vehicle',N'Add a personal auto vehicle.',N'Vehicle',N'Add',N'Personal Auto',1,0,0,760),
(N'PersonalVehicleRemove',N'Remove Personal Auto Vehicle',N'Remove a personal auto vehicle.',N'Vehicle',N'Remove',N'Personal Auto',1,0,0,770),
(N'PersonalVehicleReplace',N'Replace Personal Auto Vehicle',N'Replace a personal auto vehicle.',N'Vehicle',N'Replace',N'Personal Auto',1,0,0,780),
(N'PersonalDriverAdd',N'Add Personal Driver',N'Add a household driver.',N'Driver',N'Add',N'Personal Auto',1,1,0,790),
(N'PersonalDriverRemove',N'Remove Personal Driver',N'Remove a household driver.',N'Driver',N'Remove',N'Personal Auto',1,1,0,800),
(N'HomeLocationChange',N'Home Location Change',N'Change the insured residence location.',N'Property',N'Update',N'Homeowners',1,1,0,810),
(N'HomeCoverageLimitChange',N'Home Coverage Limit Change',N'Change a homeowners coverage limit.',N'Coverage',N'Update',N'Homeowners',1,1,0,820),
(N'MortgageeChange',N'Mortgagee Change',N'Add, remove, or change a mortgagee.',N'Legal',N'Update',N'Homeowners',0,0,1,830),
(N'ScheduledPropertyAdd',N'Add Scheduled Property',N'Add jewelry, fine art, or other scheduled property.',N'Property',N'Add',N'Homeowners',1,0,0,840),
(N'ScheduledPropertyRemove',N'Remove Scheduled Property',N'Remove scheduled property.',N'Property',N'Remove',N'Homeowners',1,0,0,850),
(N'PremiumAdjustment',N'Premium Adjustment',N'Adjust policy premium.',N'Financial',N'Update',N'*',1,1,0,860),
(N'ReturnPremium',N'Return Premium',N'Create a return premium or credit endorsement.',N'Financial',N'Update',N'*',1,1,0,870),
(N'AdditionalPremium',N'Additional Premium',N'Create an additional premium endorsement.',N'Financial',N'Update',N'*',1,1,0,880),
(N'TaxFeeAdjustment',N'Tax / Fee Adjustment',N'Adjust policy taxes, fees, or surcharges.',N'Financial',N'Update',N'*',1,1,0,890),
(N'BillingPlanChange',N'Billing Plan Change',N'Change policy billing plan handling.',N'Financial',N'Update',N'*',0,1,0,900),
(N'CommissionAdjustment',N'Commission Adjustment',N'Change commission handling or amount.',N'Financial',N'Update',N'*',1,1,0,910),
(N'EffectiveDateCorrection',N'Effective Date Correction',N'Correct the policy or endorsement effective date.',N'Administrative',N'Correct',N'*',0,1,0,920),
(N'ExpirationDateChange',N'Expiration Date Change',N'Change the policy expiration date where allowed.',N'Administrative',N'Update',N'*',1,1,0,930),
(N'PolicyTermChange',N'Policy Term Change',N'Change the policy term.',N'Administrative',N'Update',N'*',1,1,0,940),
(N'Reinstatement',N'Reinstatement',N'Reinstate policy or coverage.',N'Coverage',N'Reinstate',N'*',1,1,0,950),
(N'NonRenewalRescind',N'Rescind Non-Renewal',N'Rescind a non-renewal action.',N'Administrative',N'Reinstate',N'*',0,1,0,960),
(N'CancellationCorrection',N'Cancellation Correction',N'Correct cancellation details.',N'Administrative',N'Correct',N'*',1,1,0,970),
(N'ProfessionalServicesChange',N'Professional Services Change',N'Change professional services exposure.',N'Commercial',N'Update',N'Professional Liability',1,1,0,980),
(N'RetroDateChange',N'Retroactive Date Change',N'Change a claims-made retroactive date.',N'Coverage',N'Update',N'Professional Liability',1,1,0,990),
(N'PriorActsChange',N'Prior Acts Change',N'Change prior acts coverage.',N'Coverage',N'Update',N'Professional Liability',1,1,0,1000),
(N'CyberLimitChange',N'Cyber Limit Change',N'Change cyber, privacy, or security limits.',N'Coverage',N'Update',N'Cyber',1,1,0,1010),
(N'CyberControlChange',N'Cyber Controls Change',N'Change security control declarations.',N'Commercial',N'Update',N'Cyber',1,1,0,1020),
(N'BenefitsEligibilityChange',N'Benefits Eligibility Change',N'Change employee eligibility or class rules.',N'Commercial',N'Update',N'Benefits',1,1,0,1030),
(N'BenefitsContributionChange',N'Benefits Contribution Change',N'Change employer or employee contribution.',N'Financial',N'Update',N'Benefits',1,1,0,1040),
(N'BenefitsPlanChange',N'Benefits Plan Change',N'Change benefit plan options.',N'Coverage',N'Update',N'Benefits',1,1,0,1050);

INSERT Policy.EndorsementType(EndorsementTypeId,TenantId,TypeCode,TypeName,Description,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,catalog.TypeCode,catalog.TypeName,catalog.Description,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM #EndorsementTenants tenant CROSS JOIN #EndorsementCatalog catalog
WHERE NOT EXISTS (SELECT 1 FROM Policy.EndorsementType existing WHERE existing.TenantId=tenant.TenantId AND existing.TypeCode=catalog.TypeCode AND existing.IsDeleted=0);
UPDATE existing SET TypeName=catalog.TypeName,Description=catalog.Description,SortOrder=catalog.SortOrder,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.EndorsementType existing JOIN #EndorsementCatalog catalog ON catalog.TypeCode=existing.TypeCode WHERE existing.IsDeleted=0;

INSERT Policy.EndorsementTypeProfile
(EndorsementTypeProfileId,TenantId,EndorsementTypeId,CategoryCode,DefaultOperationCode,PremiumImpactCode,BillingImpactCode,CommissionImpactCode,AuthorityCode,ApprovalLevelCode,CarrierMethodCode,DocumentDeliveryCode,RequiresCarrierApproval,RequiresUnderwritingReview,RequiresSignedRequest,RequiresClientAuthorization,RequiresCertificateReview,RequiresBrokerOfRecord,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresPolicyVersion,SupportsBackdate,SupportsReversal,IsHighRisk,IsPremiumBearing,IsCertificateRelated,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,catalog.CategoryCode,catalog.DefaultOperationCode,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'PremiumBearing' ELSE N'NonPremium' END,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'AccountingReview' ELSE N'NoBillingImpact' END,
	CASE WHEN catalog.IsPremiumBearing=1 THEN N'RecalculateCommission' ELSE N'NoCommissionImpact' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'CarrierApprovalRequired' ELSE N'AgencyAuthority' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'ManagerApproval' ELSE N'StandardAuthority' END,
	CASE WHEN catalog.IsHighRisk=1 THEN N'CarrierApprovalRequired' ELSE N'AgencyAuthority' END,N'PortalEmail',
	catalog.IsHighRisk,catalog.IsHighRisk,CASE WHEN catalog.IsCertificateRelated=1 THEN 1 ELSE 0 END,catalog.IsHighRisk,catalog.IsCertificateRelated,CASE WHEN catalog.TypeCode=N'AgencyOfRecordChange' THEN 1 ELSE 0 END,
	catalog.IsPremiumBearing,catalog.IsPremiumBearing,1,1,CASE WHEN catalog.TypeCode IN(N'EffectiveDateCorrection',N'CancellationCorrection',N'Reinstatement') THEN 1 ELSE 0 END,1,catalog.IsHighRisk,catalog.IsPremiumBearing,catalog.IsCertificateRelated,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN #EndorsementCatalog catalog ON catalog.TypeCode=type.TypeCode
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeProfile profile WHERE profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsDeleted=0);

IF OBJECT_ID(N'tempdb..#EndorsementOptions0103') IS NOT NULL DROP TABLE #EndorsementOptions0103;
CREATE TABLE #EndorsementOptions0103(GroupCode NVARCHAR(50),Code NVARCHAR(80),Name NVARCHAR(160),Description NVARCHAR(500),IsDefault BIT,SortOrder INT,PRIMARY KEY(GroupCode,Code));
INSERT #EndorsementOptions0103 VALUES
(N'Status',N'Draft',N'Draft',N'Endorsement is being prepared.',1,10),(N'Status',N'PendingValidation',N'Pending Validation',N'Awaiting backend readiness validation.',0,20),(N'Status',N'Submitted',N'Submitted',N'Submitted for servicing review.',0,30),(N'Status',N'InReview',N'In Review',N'Agency or underwriting review is active.',0,40),(N'Status',N'NeedMoreInfo',N'Need More Information',N'Additional information is required.',0,50),(N'Status',N'PendingApproval',N'Pending Approval',N'Awaiting required approval.',0,60),(N'Status',N'Approved',N'Approved',N'Approved for carrier handling or issuance.',0,70),(N'Status',N'SubmittedToCarrier',N'Submitted to Carrier',N'Carrier dispatch was queued or sent.',0,80),(N'Status',N'CarrierProcessing',N'Carrier Processing',N'Carrier is processing the request.',0,90),(N'Status',N'CarrierApproved',N'Carrier Approved',N'Carrier approved the change.',0,100),(N'Status',N'PolicyUpdated',N'Policy Updated',N'Policy version and current state were updated.',0,110),(N'Status',N'Issued',N'Issued',N'Issued endorsement was received.',0,120),(N'Status',N'Completed',N'Completed',N'All required work is complete.',0,130),(N'Status',N'Rejected',N'Rejected',N'Internal approval rejected the request.',0,140),(N'Status',N'Declined',N'Declined',N'Carrier declined the request.',0,150),(N'Status',N'Withdrawn',N'Withdrawn',N'Request was withdrawn.',0,160),(N'Status',N'Cancelled',N'Cancelled',N'Request was cancelled.',0,170),(N'Status',N'Reversed',N'Reversed',N'Completed endorsement was reversed.',0,180),(N'Status',N'Failed',N'Failed',N'Processing failed and requires attention.',0,190),
(N'Operation',N'Add',N'Add',N'Add a policy entity or term.',1,10),(N'Operation',N'Remove',N'Remove',N'Remove a policy entity or term.',0,20),(N'Operation',N'Update',N'Update',N'Update an existing policy entity or term.',0,30),(N'Operation',N'Replace',N'Replace',N'Replace an existing entity or term.',0,40),(N'Operation',N'Correct',N'Correct',N'Correct policy data without fabricating history.',0,50),(N'Operation',N'Reinstate',N'Reinstate',N'Reinstate policy coverage or entity.',0,60),(N'Operation',N'Reverse',N'Reverse',N'Reverse a completed endorsement.',0,70),(N'Operation',N'Cancel',N'Cancel',N'Cancel the requested change.',0,80),
(N'ChangeCategory',N'Insured',N'Insured',N'Named insured and contact details.',1,10),(N'ChangeCategory',N'Vehicle',N'Vehicle',N'Vehicle and fleet schedules.',0,20),(N'ChangeCategory',N'Driver',N'Driver',N'Driver schedules and exclusions.',0,30),(N'ChangeCategory',N'Coverage',N'Coverage',N'Coverage, limits, forms, and exclusions.',0,40),(N'ChangeCategory',N'Property',N'Property',N'Locations, buildings, and equipment.',0,50),(N'ChangeCategory',N'Commercial',N'Commercial Exposure',N'Classifications, payroll, sales, and other exposures.',0,60),(N'ChangeCategory',N'Financial',N'Financial',N'Premium, billing, tax, fee, and commission terms.',0,70),(N'ChangeCategory',N'Legal',N'Legal Interest',N'Additional interests, lenders, and contractual parties.',0,80),(N'ChangeCategory',N'Administrative',N'Administrative',N'Administrative policy details.',0,90),
(N'CarrierMethod',N'AgencyAuthority',N'Agency Authority',N'Agency can process within delegated authority.',1,10),(N'CarrierMethod',N'CarrierApprovalRequired',N'Carrier Approval Required',N'Carrier approval is required.',0,20),(N'CarrierMethod',N'CarrierApi',N'Carrier API',N'Dispatch through configured carrier API.',0,30),(N'CarrierMethod',N'CarrierPortal',N'Carrier Portal',N'Create tracked carrier portal work.',0,40),(N'CarrierMethod',N'EmailSubmission',N'Email Submission',N'Send through configured carrier email route.',0,50),(N'CarrierMethod',N'ManualEntry',N'Manual Entry',N'Create tracked manual servicing work.',0,60),(N'CarrierMethod',N'DownloadOnly',N'Download Only',N'Await carrier download transaction.',0,70),
(N'Priority',N'Low',N'Low',N'Routine endorsement with no expedited handling.',0,10),(N'Priority',N'Normal',N'Normal',N'Standard endorsement servicing priority.',1,20),(N'Priority',N'High',N'High',N'Expedited endorsement servicing priority.',0,30),(N'Priority',N'Urgent',N'Urgent',N'Immediate attention is required.',0,40),
(N'PremiumImpact',N'NonPremium',N'Non-Premium',N'The endorsement does not change policy premium.',1,10),(N'PremiumImpact',N'PremiumBearing',N'Premium Bearing',N'The endorsement can create return or additional premium.',0,20),
(N'Authority',N'AgencyAuthority',N'Agency Authority',N'The agency may process within delegated authority.',1,10),(N'Authority',N'CarrierApprovalRequired',N'Carrier Approval Required',N'Carrier authority is required before completion.',0,20),
(N'ApprovalLevel',N'StandardAuthority',N'Standard Authority',N'Standard servicing authority applies.',1,10),(N'ApprovalLevel',N'ManagerApproval',N'Manager Approval',N'Manager approval is required.',0,20),(N'ApprovalLevel',N'UnderwritingApproval',N'Underwriting Approval',N'Underwriting approval is required.',0,30),
(N'DocumentDelivery',N'PortalEmail',N'Portal and Email',N'Deliver through configured portal and email channels.',1,10),(N'DocumentDelivery',N'Portal',N'Portal',N'Deliver through the customer portal.',0,20),(N'DocumentDelivery',N'Email',N'Email',N'Deliver through the configured email provider.',0,30),(N'DocumentDelivery',N'Manual',N'Manual',N'Delivery is tracked as a manual servicing action.',0,40),
(N'BillingImpact',N'NoBillingImpact',N'No Billing Impact',N'No billing transaction is required.',1,10),(N'BillingImpact',N'BillInstallment',N'Bill Installment',N'Apply the change through the installment billing workflow.',0,20),(N'BillingImpact',N'CreditInstallment',N'Credit Installment',N'Apply a credit through the installment billing workflow.',0,30),(N'BillingImpact',N'AccountingReview',N'Accounting Review',N'Accounting determines invoice or credit handling.',0,50),(N'BillingImpact',N'CarrierDirectBill',N'Carrier Direct Bill',N'Carrier controls customer billing.',0,60),(N'BillingImpact',N'AgencyBill',N'Agency Bill',N'Agency receivable and payable handling is required.',0,70),
(N'CommissionImpact',N'NoCommissionImpact',N'No Commission Impact',N'No commission transaction is required.',1,10),(N'CommissionImpact',N'RecalculateCommission',N'Recalculate Commission',N'Recalculate commission from the configured policy plan and splits.',0,20),(N'CommissionImpact',N'ProducerSplitReview',N'Producer Split Review',N'Review producer split allocations.',0,40),(N'CommissionImpact',N'ChargebackReview',N'Chargeback Review',N'Review return commission or chargeback.',0,50),
(N'DocumentRequirement',N'None',N'None',N'No additional document required.',1,10),(N'DocumentRequirement',N'SignedRequest',N'Signed Request',N'Signed insured request or authorization.',0,20),(N'DocumentRequirement',N'CarrierForm',N'Carrier Form',N'Carrier-specific endorsement form.',0,30),(N'DocumentRequirement',N'ACORDForm',N'ACORD Form',N'Applicable ACORD change form.',0,40),(N'DocumentRequirement',N'Contract',N'Contract',N'Contract supporting requested wording.',0,50),(N'DocumentRequirement',N'DriverLicense',N'Driver License',N'Driver license or operator evidence.',0,60),(N'DocumentRequirement',N'VehicleRegistration',N'Vehicle Registration',N'Registration, bill of sale, or VIN evidence.',0,70),(N'DocumentRequirement',N'BORLetter',N'BOR Letter',N'Signed broker of record letter.',0,80),(N'DocumentRequirement',N'UnderwriterApproval',N'Underwriter Approval',N'Underwriter approval evidence.',0,90),(N'DocumentRequirement',N'Invoice',N'Invoice',N'Invoice or credit documentation.',0,100),(N'DocumentRequirement',N'IssuedEndorsement',N'Issued Endorsement',N'Carrier-issued endorsement evidence.',0,110);
INSERT Policy.PolicyEndorsementOption(OptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,optionSeed.GroupCode,optionSeed.Code,optionSeed.Name,optionSeed.Description,optionSeed.IsDefault,1,optionSeed.SortOrder,SYSUTCDATETIME(),0
FROM #EndorsementTenants tenant CROSS JOIN #EndorsementOptions0103 optionSeed
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyEndorsementOption existing WHERE existing.TenantId=tenant.TenantId AND existing.OptionGroupCode=optionSeed.GroupCode AND existing.OptionCode=optionSeed.Code AND existing.IsDeleted=0);
UPDATE existing SET DisplayName=seed.Name,Description=seed.Description,SortOrder=seed.SortOrder,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.PolicyEndorsementOption existing JOIN #EndorsementOptions0103 seed ON seed.GroupCode=existing.OptionGroupCode AND seed.Code=existing.OptionCode WHERE existing.IsDeleted=0;

GO

IF OBJECT_ID(N'Policy.EndorsementTypeLineOfBusiness', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeLineOfBusiness
	(
		EndorsementTypeLineOfBusinessId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeLineOfBusiness PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		LineOfBusinessCode NVARCHAR(100) NOT NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_EndorsementTypeLob_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeLob_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeLob_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeLob_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeLob_Deleted DEFAULT 0,
		CONSTRAINT FK_EndorsementTypeLob_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementTypeLob_TypeLob ON Policy.EndorsementTypeLineOfBusiness(TenantId, EndorsementTypeId, LineOfBusinessCode) WHERE IsDeleted = 0;
	CREATE INDEX IX_EndorsementTypeLob_Lob ON Policy.EndorsementTypeLineOfBusiness(TenantId, LineOfBusinessCode, IsActive, IsDeleted, SortOrder);
END;
GO

IF OBJECT_ID(N'Policy.EndorsementTypeDocumentRequirement', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeDocumentRequirement
	(
		EndorsementTypeDocumentRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeDocumentRequirement PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		RequirementCode NVARCHAR(80) NOT NULL,
		DocumentGroupCode NVARCHAR(80) NULL,
		DocumentKindCode NVARCHAR(80) NULL,
		AcordFormNumber NVARCHAR(50) NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_EndorsementTypeDocumentRequirement_Required DEFAULT 1,
		AppliesWhenJson NVARCHAR(MAX) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeDocumentRequirement_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeDocumentRequirement_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeDocumentRequirement_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeDocumentRequirement_Deleted DEFAULT 0,
		CONSTRAINT CK_EndorsementTypeDocumentRequirement_AppliesWhen CHECK (AppliesWhenJson IS NULL OR ISJSON(AppliesWhenJson) = 1),
		CONSTRAINT FK_EndorsementTypeDocumentRequirement_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementTypeDocumentRequirement_Code ON Policy.EndorsementTypeDocumentRequirement(TenantId, EndorsementTypeId, RequirementCode) WHERE IsDeleted = 0;
	CREATE INDEX IX_EndorsementTypeDocumentRequirement_Type ON Policy.EndorsementTypeDocumentRequirement(TenantId, EndorsementTypeId, IsRequired, IsActive, IsDeleted, SortOrder);
END;
GO

IF OBJECT_ID(N'Policy.EndorsementTypeWorkflowRule', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeWorkflowRule
	(
		EndorsementTypeWorkflowRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeWorkflowRule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		FromStatusCode NVARCHAR(80) NOT NULL,
		ToStatusCode NVARCHAR(80) NOT NULL,
		RequiredPermissionCode NVARCHAR(100) NULL,
		RequiresApproval BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Approval DEFAULT 0,
		RequiresCarrierDispatch BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Carrier DEFAULT 0,
		RequiresAccountingWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Accounting DEFAULT 0,
		RequiresCommissionWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Commission DEFAULT 0,
		RequiresDocumentWork BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Document DEFAULT 0,
		RequiresCertificateReview BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Certificate DEFAULT 0,
		RequiresPolicyVersion BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Version DEFAULT 0,
		RuleJson NVARCHAR(MAX) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeWorkflowRule_Deleted DEFAULT 0,
		CONSTRAINT CK_EndorsementTypeWorkflowRule_RuleJson CHECK (RuleJson IS NULL OR ISJSON(RuleJson) = 1),
		CONSTRAINT FK_EndorsementTypeWorkflowRule_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementTypeWorkflowRule_Transition ON Policy.EndorsementTypeWorkflowRule(TenantId, EndorsementTypeId, FromStatusCode, ToStatusCode) WHERE IsDeleted = 0;
	CREATE INDEX IX_EndorsementTypeWorkflowRule_From ON Policy.EndorsementTypeWorkflowRule(TenantId, EndorsementTypeId, FromStatusCode, IsActive, IsDeleted, SortOrder);
END;
GO

IF OBJECT_ID(N'Policy.EndorsementTypeCarrierMethod', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeCarrierMethod
	(
		EndorsementTypeCarrierMethodId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeCarrierMethod PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusinessCode NVARCHAR(100) NULL,
		CarrierMethodCode NVARCHAR(50) NOT NULL,
		CarrierConfigurationId UNIQUEIDENTIFIER NULL,
		PortalInstructions NVARCHAR(2000) NULL,
		EmailTemplateCode NVARCHAR(100) NULL,
		PayloadTemplateCode NVARCHAR(100) NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_EndorsementTypeCarrierMethod_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeCarrierMethod_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeCarrierMethod_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeCarrierMethod_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeCarrierMethod_Deleted DEFAULT 0,
		CONSTRAINT FK_EndorsementTypeCarrierMethod_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementTypeCarrierMethod_Route ON Policy.EndorsementTypeCarrierMethod(TenantId, EndorsementTypeId, CarrierId, LineOfBusinessCode, CarrierMethodCode) WHERE IsDeleted = 0;
	CREATE INDEX IX_EndorsementTypeCarrierMethod_Resolve ON Policy.EndorsementTypeCarrierMethod(TenantId, EndorsementTypeId, CarrierId, LineOfBusinessCode, IsDefault, IsActive, IsDeleted, SortOrder);
END;
GO

INSERT Policy.EndorsementTypeLineOfBusiness
(EndorsementTypeLineOfBusinessId,TenantId,EndorsementTypeId,LineOfBusinessCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,catalog.LobCode,1,1,catalog.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN #EndorsementCatalog catalog ON catalog.TypeCode=type.TypeCode
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeLineOfBusiness lob WHERE lob.TenantId=type.TenantId AND lob.EndorsementTypeId=type.EndorsementTypeId AND lob.LineOfBusinessCode=catalog.LobCode AND lob.IsDeleted=0);

INSERT Policy.EndorsementTypeDocumentRequirement
(EndorsementTypeDocumentRequirementId,TenantId,EndorsementTypeId,RequirementCode,DocumentGroupCode,DocumentKindCode,AcordFormNumber,IsRequired,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,requirement.RequirementCode,N'ENDORSEMENT',requirement.DocumentKindCode,requirement.AcordFormNumber,1,1,requirement.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type JOIN (VALUES
	(N'AdditionalInsuredAdd',N'Contract',N'CONTRACT',CAST(NULL AS NVARCHAR(50)),10),(N'WaiverSubrogationAdd',N'Contract',N'CONTRACT',NULL,10),(N'PrimaryNonContributoryAdd',N'Contract',N'CONTRACT',NULL,10),
	(N'VehicleAdd',N'VehicleRegistration',N'VEHICLE_REGISTRATION',N'ACORD 127',10),(N'VehicleReplace',N'VehicleRegistration',N'VEHICLE_REGISTRATION',N'ACORD 127',10),(N'DriverAdd',N'DriverLicense',N'DRIVER_LICENSE',N'ACORD 127',10),
	(N'AgencyOfRecordChange',N'BORLetter',N'BOR_LETTER',NULL,10),(N'RetroDateChange',N'UnderwriterApproval',N'UNDERWRITER_APPROVAL',NULL,10),(N'PriorActsChange',N'UnderwriterApproval',N'UNDERWRITER_APPROVAL',NULL,10)
) requirement(TypeCode,RequirementCode,DocumentKindCode,AcordFormNumber,SortOrder) ON requirement.TypeCode=type.TypeCode
WHERE type.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeDocumentRequirement existing WHERE existing.TenantId=type.TenantId AND existing.EndorsementTypeId=type.EndorsementTypeId AND existing.RequirementCode=requirement.RequirementCode AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeWorkflowRule
(EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,workflowRule.FromStatus,workflowRule.ToStatus,workflowRule.Permission,
	CASE WHEN workflowRule.ToStatus=N'PendingApproval' THEN 1 ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'SubmittedToCarrier' THEN profile.RequiresCarrierApproval ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresAccountingWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresCommissionWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'Issued' THEN profile.RequiresDocumentWork ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'Issued' THEN profile.RequiresCertificateReview ELSE 0 END,
	CASE WHEN workflowRule.ToStatus=N'PolicyUpdated' THEN profile.RequiresPolicyVersion ELSE 0 END,1,workflowRule.SortOrder,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile CROSS JOIN (VALUES
	(N'Draft',N'PendingValidation',N'ENDORSEMENT_EDIT_DRAFT',10),(N'PendingValidation',N'Submitted',N'ENDORSEMENT_CREATE',20),(N'Submitted',N'InReview',N'ENDORSEMENT_MANAGE',30),(N'InReview',N'NeedMoreInfo',N'ENDORSEMENT_MANAGE',40),(N'NeedMoreInfo',N'InReview',N'ENDORSEMENT_EDIT_DRAFT',50),(N'InReview',N'PendingApproval',N'ENDORSEMENT_MANAGE',60),(N'PendingApproval',N'Approved',N'ENDORSEMENT_APPROVE',70),(N'Approved',N'SubmittedToCarrier',N'ENDORSEMENT_MANAGE',80),(N'SubmittedToCarrier',N'CarrierProcessing',N'ENDORSEMENT_MANAGE',90),(N'CarrierProcessing',N'CarrierApproved',N'ENDORSEMENT_MANAGE',100),(N'CarrierApproved',N'PolicyUpdated',N'ENDORSEMENT_MANAGE',110),(N'PolicyUpdated',N'Issued',N'ENDORSEMENT_MANAGE',120),(N'Issued',N'Completed',N'ENDORSEMENT_MANAGE',130)
) workflowRule(FromStatus,ToStatus,Permission,SortOrder)
WHERE profile.IsDeleted=0 AND profile.IsActive=1 AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeWorkflowRule existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.FromStatusCode=workflowRule.FromStatus AND existing.ToStatusCode=workflowRule.ToStatus AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeCarrierMethod
(EndorsementTypeCarrierMethodId,TenantId,EndorsementTypeId,CarrierMethodCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,profile.CarrierMethodCode,1,1,10,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile WHERE profile.IsDeleted=0 AND profile.IsActive=1
AND NOT EXISTS (SELECT 1 FROM Policy.EndorsementTypeCarrierMethod existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.CarrierId IS NULL AND existing.LineOfBusinessCode IS NULL AND existing.CarrierMethodCode=profile.CarrierMethodCode AND existing.IsDeleted=0);
GO
