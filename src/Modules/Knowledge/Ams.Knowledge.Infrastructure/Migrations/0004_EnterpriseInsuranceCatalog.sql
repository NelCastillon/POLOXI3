SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

DECLARE @Schemes TABLE
(
	SchemeCode VARCHAR(100) PRIMARY KEY,
	Name NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NOT NULL
);
INSERT INTO @Schemes VALUES
('INSURANCE_PRODUCT',N'Insurance Product',N'U.S. property, casualty, benefits, and life product and line hierarchy.'),
('POLICY_STATUS',N'Policy Status',N'Policy lifecycle and servicing states.'),
('CLAIM_CAUSE_OF_LOSS',N'Claim Cause of Loss',N'First-party and third-party causes of loss.'),
('COVERAGE_TYPE',N'Coverage Type',N'Property, casualty, benefits, and life coverage vocabulary.'),
('PARTY_ROLE',N'Party Role',N'Insurance customer, producer, carrier, service, and claim roles.'),
('DOCUMENT_TYPE',N'Document Type',N'Sales, underwriting, policy, billing, claim, compliance, benefits, and life documents.'),
('WORKFLOW_ACTION',N'Workflow Action',N'Canonical agency workflow actions.'),
('ACCOUNTING_TRANSACTION',N'Accounting Transaction',N'Premium, receivable, payable, commission, and trust transactions.'),
('CLAIM_STATUS',N'Claim Status',N'Claim and exposure lifecycle states.'),
('CLAIM_TRANSACTION',N'Claim Transaction',N'Claim reserve, payment, recovery, and expense transactions.'),
('UNDERWRITING_DECISION',N'Underwriting Decision',N'Risk-selection and quote decisions.'),
('RISK_EXPOSURE',N'Risk Exposure',N'Common property, casualty, cyber, benefit, and life exposures.'),
('INSURED_ASSET',N'Insured Asset',N'Assets and subjects of insurance.'),
('BENEFIT_PLAN',N'Benefit Plan',N'Employer and individual health and ancillary benefit products.'),
('LIFE_ANNUITY_PRODUCT',N'Life and Annuity Product',N'Life insurance and annuity product taxonomy.'),
('BILLING_METHOD',N'Billing Method',N'Agency and carrier billing arrangements.'),
('PAYMENT_METHOD',N'Payment Method',N'Customer and agency payment instruments.'),
('COMMISSION_TYPE',N'Commission Type',N'Producer compensation, split, bonus, and adjustment classifications.'),
('DISTRIBUTION_CHANNEL',N'Distribution Channel',N'Insurance sales and servicing channels.'),
('REGULATORY_COMPLIANCE',N'Regulatory and Compliance',N'Licensing, privacy, market-conduct, and compliance concepts.'),
('US_JURISDICTION',N'U.S. Jurisdiction',N'U.S. states, District of Columbia, and territories used for risk and regulatory context.'),
('CANCELLATION_REASON',N'Cancellation and Nonrenewal Reason',N'Common policy cancellation and nonrenewal reasons.'),
('SERVICE_REQUEST',N'Service Request',N'Common policyholder and account service requests.'),
('CONTACT_METHOD',N'Contact Method',N'Communication channel classifications.'),
('PRIORITY',N'Priority',N'Operational priority and urgency classifications.');

MERGE knowledge.ConceptScheme AS target
USING @Schemes AS source
ON target.TenantId IS NULL AND target.SchemeCode = source.SchemeCode AND target.IsDeleted = 0
WHEN MATCHED AND target.IsSystemDefined = 1 THEN
	UPDATE SET Name = source.Name, Description = source.Description, AuthorityCode = 'AMS_REFERENCE',
		VersionLabel = '2.0', StatusCode = 'PUBLISHED', ModifiedByUserId = @SystemUserId, ModifiedDateUtc = @Now
WHEN NOT MATCHED THEN
	INSERT (ConceptSchemeId, SchemeCode, Name, Description, AuthorityCode, VersionLabel, StatusCode, TenantId,
		IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
	VALUES (NEWID(), source.SchemeCode, source.Name, source.Description, 'AMS_REFERENCE', '2.0', 'PUBLISHED', NULL,
		1, @SystemUserId, @Now, 0);

DECLARE @Concepts TABLE
(
	SeedId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
	SchemeCode VARCHAR(100) NOT NULL,
	ConceptCode VARCHAR(100) NOT NULL,
	ConceptTypeCode VARCHAR(50) NOT NULL,
	PreferredLabel NVARCHAR(250) NOT NULL,
	ParentCode VARCHAR(100) NULL,
	Depth INT NOT NULL,
	IsAbstract BIT NOT NULL DEFAULT 0,
	PRIMARY KEY (SchemeCode, ConceptCode)
);

INSERT INTO @Concepts (SchemeCode,ConceptCode,ConceptTypeCode,PreferredLabel,ParentCode,Depth,IsAbstract) VALUES
-- Products and lines of business
('INSURANCE_PRODUCT','PRODUCT.BENEFITS','INSURANCE_PRODUCT',N'Employee Benefits', 'PRODUCT.INSURANCE',1,1),
('INSURANCE_PRODUCT','PRODUCT.LIFE_HEALTH','INSURANCE_PRODUCT',N'Life and Health', 'PRODUCT.INSURANCE',1,1),
('INSURANCE_PRODUCT','PRODUCT.SPECIALTY','INSURANCE_PRODUCT',N'Specialty Insurance', 'PRODUCT.INSURANCE',1,1),
('INSURANCE_PRODUCT','LOB.HOMEOWNERS','LINE_OF_BUSINESS',N'Homeowners','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.RENTERS','LINE_OF_BUSINESS',N'Renters','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.CONDOMINIUM','LINE_OF_BUSINESS',N'Condominium Unit Owners','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.DWELLING_FIRE','LINE_OF_BUSINESS',N'Dwelling Fire','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.PERSONAL_UMBRELLA','LINE_OF_BUSINESS',N'Personal Umbrella','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.MOTORCYCLE','LINE_OF_BUSINESS',N'Motorcycle','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.RECREATIONAL_VEHICLE','LINE_OF_BUSINESS',N'Recreational Vehicle','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.WATERCRAFT','LINE_OF_BUSINESS',N'Personal Watercraft','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.FLOOD','LINE_OF_BUSINESS',N'Flood','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.INLAND_MARINE_PERSONAL','LINE_OF_BUSINESS',N'Personal Inland Marine','PRODUCT.PERSONAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.COMMERCIAL_PROPERTY','LINE_OF_BUSINESS',N'Commercial Property','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.BUSINESS_OWNERS','LINE_OF_BUSINESS',N'Businessowners Policy','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.WORKERS_COMPENSATION','LINE_OF_BUSINESS',N'Workers Compensation','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.PROFESSIONAL_LIABILITY','LINE_OF_BUSINESS',N'Professional Liability','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.CYBER_LIABILITY','LINE_OF_BUSINESS',N'Cyber Liability','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.COMMERCIAL_UMBRELLA','LINE_OF_BUSINESS',N'Commercial Umbrella','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.INLAND_MARINE_COMMERCIAL','LINE_OF_BUSINESS',N'Commercial Inland Marine','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.EQUIPMENT_BREAKDOWN','LINE_OF_BUSINESS',N'Equipment Breakdown','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.CRIME','LINE_OF_BUSINESS',N'Commercial Crime','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.SURETY','LINE_OF_BUSINESS',N'Surety','PRODUCT.COMMERCIAL_LINES',2,0),
('INSURANCE_PRODUCT','LOB.MARINE','LINE_OF_BUSINESS',N'Ocean Marine','PRODUCT.SPECIALTY',2,0),
('INSURANCE_PRODUCT','LOB.AVIATION','LINE_OF_BUSINESS',N'Aviation','PRODUCT.SPECIALTY',2,0),
('INSURANCE_PRODUCT','LOB.EVENT','LINE_OF_BUSINESS',N'Special Event','PRODUCT.SPECIALTY',2,0),
('INSURANCE_PRODUCT','LOB.PET','LINE_OF_BUSINESS',N'Pet Insurance','PRODUCT.SPECIALTY',2,0),
('INSURANCE_PRODUCT','LOB.TRAVEL','LINE_OF_BUSINESS',N'Travel Insurance','PRODUCT.SPECIALTY',2,0),
-- Coverage families
('COVERAGE_TYPE','COVERAGE.ROOT','COVERAGE',N'Insurance Coverage',NULL,0,1),
('COVERAGE_TYPE','COVERAGE.PROPERTY','COVERAGE',N'Property Coverage','COVERAGE.ROOT',1,1),
('COVERAGE_TYPE','COVERAGE.LIABILITY','COVERAGE',N'Liability Coverage','COVERAGE.ROOT',1,1),
('COVERAGE_TYPE','COVERAGE.AUTO','COVERAGE',N'Automobile Coverage','COVERAGE.ROOT',1,1),
('COVERAGE_TYPE','COVERAGE.BENEFITS','COVERAGE',N'Benefits Coverage','COVERAGE.ROOT',1,1),
('COVERAGE_TYPE','COVERAGE.LIFE','COVERAGE',N'Life and Annuity Coverage','COVERAGE.ROOT',1,1),
('COVERAGE_TYPE','COVERAGE.BUILDING','COVERAGE',N'Building','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.BUSINESS_PERSONAL_PROPERTY','COVERAGE',N'Business Personal Property','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.BUSINESS_INCOME','COVERAGE',N'Business Income','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.EXTRA_EXPENSE','COVERAGE',N'Extra Expense','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.EQUIPMENT_BREAKDOWN','COVERAGE',N'Equipment Breakdown','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.DEBRIS_REMOVAL','COVERAGE',N'Debris Removal','COVERAGE.PROPERTY',2,0),
('COVERAGE_TYPE','COVERAGE.GENERAL_LIABILITY','COVERAGE',N'General Liability','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.PRODUCTS_COMPLETED','COVERAGE',N'Products and Completed Operations','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.PROFESSIONAL','COVERAGE',N'Professional Liability','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.EMPLOYMENT_PRACTICES','COVERAGE',N'Employment Practices Liability','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.DIRECTORS_OFFICERS','COVERAGE',N'Directors and Officers Liability','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.UMBRELLA','COVERAGE',N'Umbrella Liability','COVERAGE.LIABILITY',2,0),
('COVERAGE_TYPE','COVERAGE.BODILY_INJURY','COVERAGE',N'Bodily Injury Liability','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.PROPERTY_DAMAGE','COVERAGE',N'Property Damage Liability','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.COLLISION','COVERAGE',N'Collision','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.COMPREHENSIVE','COVERAGE',N'Comprehensive','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.UNINSURED_MOTORIST','COVERAGE',N'Uninsured Motorist','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.UNDERINSURED_MOTORIST','COVERAGE',N'Underinsured Motorist','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.PERSONAL_INJURY_PROTECTION','COVERAGE',N'Personal Injury Protection','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.RENTAL_REIMBURSEMENT','COVERAGE',N'Rental Reimbursement','COVERAGE.AUTO',2,0),
('COVERAGE_TYPE','COVERAGE.HEALTH_MEDICAL','COVERAGE',N'Medical Benefits','COVERAGE.BENEFITS',2,0),
('COVERAGE_TYPE','COVERAGE.DENTAL','COVERAGE',N'Dental Benefits','COVERAGE.BENEFITS',2,0),
('COVERAGE_TYPE','COVERAGE.VISION','COVERAGE',N'Vision Benefits','COVERAGE.BENEFITS',2,0),
('COVERAGE_TYPE','COVERAGE.SHORT_TERM_DISABILITY','COVERAGE',N'Short-Term Disability','COVERAGE.BENEFITS',2,0),
('COVERAGE_TYPE','COVERAGE.LONG_TERM_DISABILITY','COVERAGE',N'Long-Term Disability','COVERAGE.BENEFITS',2,0),
('COVERAGE_TYPE','COVERAGE.ACCIDENTAL_DEATH','COVERAGE',N'Accidental Death and Dismemberment','COVERAGE.LIFE',2,0),
('COVERAGE_TYPE','COVERAGE.DEATH_BENEFIT','COVERAGE',N'Death Benefit','COVERAGE.LIFE',2,0),
('COVERAGE_TYPE','COVERAGE.CASH_VALUE','COVERAGE',N'Cash Value','COVERAGE.LIFE',2,0),
('COVERAGE_TYPE','COVERAGE.WAIVER_OF_PREMIUM','COVERAGE',N'Waiver of Premium','COVERAGE.LIFE',2,0),
-- Causes of loss and perils
('CLAIM_CAUSE_OF_LOSS','PERIL.ROOT','PERIL',N'Cause of Loss',NULL,0,1),
('CLAIM_CAUSE_OF_LOSS','PERIL.FIRE','PERIL',N'Fire','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.LIGHTNING','PERIL',N'Lightning','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.WIND','PERIL',N'Windstorm','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.HAIL','PERIL',N'Hail','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.WATER','PERIL',N'Water Damage','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.FLOOD','PERIL',N'Flood','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.EARTHQUAKE','PERIL',N'Earthquake','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.THEFT','PERIL',N'Theft','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.VANDALISM','PERIL',N'Vandalism','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.COLLISION','PERIL',N'Collision','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.GLASS_BREAKAGE','PERIL',N'Glass Breakage','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.BODILY_INJURY','PERIL',N'Bodily Injury','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.PRODUCT_DEFECT','PERIL',N'Product Defect','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.SLIP_FALL','PERIL',N'Slip and Fall','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.CYBER_BREACH','PERIL',N'Data Breach','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.RANSOMWARE','PERIL',N'Ransomware','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.EMPLOYEE_INJURY','PERIL',N'Employee Injury','PERIL.ROOT',1,0),
('CLAIM_CAUSE_OF_LOSS','PERIL.PROFESSIONAL_ERROR','PERIL',N'Professional Error or Omission','PERIL.ROOT',1,0),
-- Policy and claim lifecycle
('POLICY_STATUS','POLICY.STATUS.ROOT','STATUS',N'Policy Lifecycle Status',NULL,0,1),
('POLICY_STATUS','POLICY.STATUS.QUOTE','STATUS',N'Quoted','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.BOUND','STATUS',N'Bound','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.ACTIVE','STATUS',N'Active','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.PENDING_CANCEL','STATUS',N'Pending Cancellation','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.CANCELLED','STATUS',N'Cancelled','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.EXPIRED','STATUS',N'Expired','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.NONRENEWED','STATUS',N'Nonrenewed','POLICY.STATUS.ROOT',1,0),
('POLICY_STATUS','POLICY.STATUS.REINSTATED','STATUS',N'Reinstated','POLICY.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.ROOT','STATUS',N'Claim Status',NULL,0,1),
('CLAIM_STATUS','CLAIM.STATUS.REPORTED','STATUS',N'Reported','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.OPEN','STATUS',N'Open','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.INVESTIGATION','STATUS',N'Under Investigation','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.LITIGATION','STATUS',N'In Litigation','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.CLOSED_PAID','STATUS',N'Closed with Payment','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.CLOSED_NO_PAYMENT','STATUS',N'Closed without Payment','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.REOPENED','STATUS',N'Reopened','CLAIM.STATUS.ROOT',1,0),
('CLAIM_STATUS','CLAIM.STATUS.DENIED','STATUS',N'Denied','CLAIM.STATUS.ROOT',1,0),
-- Benefits and life products
('BENEFIT_PLAN','BENEFIT.ROOT','INSURANCE_PRODUCT',N'Benefit Plan',NULL,0,1),
('BENEFIT_PLAN','BENEFIT.MEDICAL.PPO','INSURANCE_PRODUCT',N'Preferred Provider Organization Medical Plan','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.MEDICAL.HMO','INSURANCE_PRODUCT',N'Health Maintenance Organization Medical Plan','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.MEDICAL.HDHP','INSURANCE_PRODUCT',N'High-Deductible Health Plan','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.DENTAL','INSURANCE_PRODUCT',N'Dental Plan','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.VISION','INSURANCE_PRODUCT',N'Vision Plan','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.FSA','FINANCIAL_CONCEPT',N'Flexible Spending Account','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.HSA','FINANCIAL_CONCEPT',N'Health Savings Account','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.GROUP_LIFE','INSURANCE_PRODUCT',N'Group Life','BENEFIT.ROOT',1,0),
('BENEFIT_PLAN','BENEFIT.GROUP_DISABILITY','INSURANCE_PRODUCT',N'Group Disability','BENEFIT.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','LIFE.ROOT','INSURANCE_PRODUCT',N'Life and Annuity Product',NULL,0,1),
('LIFE_ANNUITY_PRODUCT','LIFE.TERM','INSURANCE_PRODUCT',N'Term Life','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','LIFE.WHOLE','INSURANCE_PRODUCT',N'Whole Life','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','LIFE.UNIVERSAL','INSURANCE_PRODUCT',N'Universal Life','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','LIFE.VARIABLE','INSURANCE_PRODUCT',N'Variable Life','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','LIFE.FINAL_EXPENSE','INSURANCE_PRODUCT',N'Final Expense Life','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','ANNUITY.FIXED','INSURANCE_PRODUCT',N'Fixed Annuity','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','ANNUITY.INDEXED','INSURANCE_PRODUCT',N'Fixed Indexed Annuity','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','ANNUITY.VARIABLE','INSURANCE_PRODUCT',N'Variable Annuity','LIFE.ROOT',1,0),
('LIFE_ANNUITY_PRODUCT','ANNUITY.IMMEDIATE','INSURANCE_PRODUCT',N'Immediate Annuity','LIFE.ROOT',1,0),
-- Underwriting, claim, and accounting operations
('UNDERWRITING_DECISION','UW.DECISION.ROOT','STATUS',N'Underwriting Decision',NULL,0,1),
('UNDERWRITING_DECISION','UW.DECISION.QUOTE','STATUS',N'Quote','UW.DECISION.ROOT',1,0),
('UNDERWRITING_DECISION','UW.DECISION.REFER','STATUS',N'Refer','UW.DECISION.ROOT',1,0),
('UNDERWRITING_DECISION','UW.DECISION.DECLINE','STATUS',N'Decline','UW.DECISION.ROOT',1,0),
('UNDERWRITING_DECISION','UW.DECISION.BIND','STATUS',N'Bind','UW.DECISION.ROOT',1,0),
('UNDERWRITING_DECISION','UW.DECISION.SUBJECTIVITY','STATUS',N'Quote Subject to Requirements','UW.DECISION.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.ROOT','TRANSACTION_TYPE',N'Claim Transaction',NULL,0,1),
('CLAIM_TRANSACTION','CLAIM.TXN.RESERVE_SET','TRANSACTION_TYPE',N'Reserve Set','CLAIM.TXN.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.RESERVE_CHANGE','TRANSACTION_TYPE',N'Reserve Change','CLAIM.TXN.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.INDEMNITY_PAYMENT','TRANSACTION_TYPE',N'Indemnity Payment','CLAIM.TXN.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.EXPENSE_PAYMENT','TRANSACTION_TYPE',N'Expense Payment','CLAIM.TXN.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.SALVAGE','TRANSACTION_TYPE',N'Salvage Recovery','CLAIM.TXN.ROOT',1,0),
('CLAIM_TRANSACTION','CLAIM.TXN.SUBROGATION','TRANSACTION_TYPE',N'Subrogation Recovery','CLAIM.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.ROOT','TRANSACTION_TYPE',N'Insurance Accounting Transaction',NULL,0,1),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.PREMIUM_RECEIVABLE','TRANSACTION_TYPE',N'Premium Receivable','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.PREMIUM_PAYMENT','TRANSACTION_TYPE',N'Premium Payment','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.CARRIER_PAYABLE','TRANSACTION_TYPE',N'Carrier Payable','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.COMMISSION_RECEIVABLE','TRANSACTION_TYPE',N'Commission Receivable','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.PRODUCER_PAYABLE','TRANSACTION_TYPE',N'Producer Payable','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.RETURN_PREMIUM','TRANSACTION_TYPE',N'Return Premium','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.WRITE_OFF','TRANSACTION_TYPE',N'Write-Off','ACCOUNTING.TXN.ROOT',1,0),
('ACCOUNTING_TRANSACTION','ACCOUNTING.TXN.REFUND','TRANSACTION_TYPE',N'Refund','ACCOUNTING.TXN.ROOT',1,0);

DECLARE @FlatConcepts TABLE
(
	SchemeCode VARCHAR(100),
	CodePrefix VARCHAR(40),
	ConceptTypeCode VARCHAR(50),
	ValueCode VARCHAR(60),
	DisplayName NVARCHAR(250),
	PRIMARY KEY (SchemeCode, ValueCode)
);
INSERT INTO @FlatConcepts VALUES
('BILLING_METHOD','BILLING','FINANCIAL_CONCEPT','AGENCY_BILL',N'Agency Bill'),('BILLING_METHOD','BILLING','FINANCIAL_CONCEPT','DIRECT_BILL',N'Direct Bill'),('BILLING_METHOD','BILLING','FINANCIAL_CONCEPT','LIST_BILL',N'List Bill'),('BILLING_METHOD','BILLING','FINANCIAL_CONCEPT','INDIVIDUAL_BILL',N'Individual Bill'),
('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','ACH',N'ACH'),('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','CHECK',N'Check'),('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','CREDIT_CARD',N'Credit Card'),('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','DEBIT_CARD',N'Debit Card'),('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','WIRE',N'Wire Transfer'),('PAYMENT_METHOD','PAYMENT','FINANCIAL_CONCEPT','EFT',N'Electronic Funds Transfer'),
('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','NEW_BUSINESS',N'New Business Commission'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','RENEWAL',N'Renewal Commission'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','CONTINGENT',N'Contingent Commission'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','BONUS',N'Production Bonus'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','SPLIT',N'Commission Split'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','CHARGEBACK',N'Commission Chargeback'),('COMMISSION_TYPE','COMMISSION','FINANCIAL_CONCEPT','OVERRIDE',N'Commission Override'),
('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','INDEPENDENT_AGENT',N'Independent Agent'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','BROKER',N'Broker'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','WHOLESALE_BROKER',N'Wholesale Broker'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','MGA',N'Managing General Agent'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','DIRECT',N'Direct to Consumer'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','AFFINITY',N'Affinity Group'),('DISTRIBUTION_CHANNEL','CHANNEL','ENTITY','DIGITAL',N'Digital Marketplace'),
('CONTACT_METHOD','CONTACT','ENTITY','EMAIL',N'Email'),('CONTACT_METHOD','CONTACT','ENTITY','PHONE',N'Phone'),('CONTACT_METHOD','CONTACT','ENTITY','SMS',N'Text Message'),('CONTACT_METHOD','CONTACT','ENTITY','MAIL',N'Postal Mail'),('CONTACT_METHOD','CONTACT','ENTITY','PORTAL',N'Customer Portal'),('CONTACT_METHOD','CONTACT','ENTITY','IN_PERSON',N'In Person'),
('PRIORITY','PRIORITY','STATUS','LOW',N'Low'),('PRIORITY','PRIORITY','STATUS','NORMAL',N'Normal'),('PRIORITY','PRIORITY','STATUS','HIGH',N'High'),('PRIORITY','PRIORITY','STATUS','URGENT',N'Urgent'),('PRIORITY','PRIORITY','STATUS','CRITICAL',N'Critical'),
('CANCELLATION_REASON','CANCEL_REASON','STATUS','NONPAYMENT',N'Nonpayment of Premium'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','INSURED_REQUEST',N'Insured Request'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','UNDERWRITING',N'Underwriting Reason'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','MATERIAL_MISREPRESENTATION',N'Material Misrepresentation'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','RISK_CHANGE',N'Material Change in Risk'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','REPLACED_COVERAGE',N'Coverage Replaced'),('CANCELLATION_REASON','CANCEL_REASON','STATUS','CARRIER_WITHDRAWAL',N'Carrier Market Withdrawal'),
('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','CERTIFICATE',N'Issue Certificate'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','ID_CARD',N'Issue Identification Card'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','ENDORSEMENT',N'Process Endorsement'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','CANCELLATION',N'Process Cancellation'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','REINSTATEMENT',N'Process Reinstatement'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','AUDIT',N'Process Premium Audit'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','CLAIM_REPORT',N'Report Claim'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','PAYMENT',N'Process Payment'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','LOSS_RUN',N'Request Loss Runs'),('SERVICE_REQUEST','SERVICE','WORKFLOW_ACTION','POLICY_CHANGE',N'Policy Change Request');

INSERT INTO @FlatConcepts VALUES
('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','NAMED_INSURED',N'Named Insured'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','ADDITIONAL_INSURED',N'Additional Insured'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','APPLICANT',N'Applicant'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','POLICYHOLDER',N'Policyholder'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','CLAIMANT',N'Claimant'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','BENEFICIARY',N'Beneficiary'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','PRODUCER',N'Producer'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','ACCOUNT_MANAGER',N'Account Manager'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','UNDERWRITER',N'Underwriter'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','ADJUSTER',N'Claim Adjuster'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','CARRIER',N'Insurance Carrier'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','MORTGAGEE',N'Mortgagee'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','LOSS_PAYEE',N'Loss Payee'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','CERTIFICATE_HOLDER',N'Certificate Holder'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','EMPLOYER',N'Employer'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','EMPLOYEE',N'Employee'),('PARTY_ROLE','PARTY_ROLE','PARTY_ROLE','DEPENDENT',N'Dependent'),
('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','QUOTE',N'Quote'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','PROPOSAL',N'Proposal'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','POLICY',N'Policy'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','APPLICATION_SUPPLEMENT',N'Application Supplement'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','VEHICLE_SCHEDULE',N'Vehicle Schedule'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','DRIVER_SCHEDULE',N'Driver Schedule'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','LOCATION_SCHEDULE',N'Location Schedule'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','PROPERTY_STATEMENT_OF_VALUES',N'Statement of Values'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','EXPERIENCE_MOD_WORKSHEET',N'Experience Modification Worksheet'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','AUDIT_REPORT',N'Premium Audit Report'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','CLAIM_FORM',N'Claim Form'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','EXPLANATION_OF_BENEFITS',N'Explanation of Benefits'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','BENEFIT_SUMMARY',N'Summary of Benefits and Coverage'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','BENEFICIARY_FORM',N'Beneficiary Designation'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','RENEWAL_NOTICE',N'Renewal Notice'),('DOCUMENT_TYPE','DOCUMENT','DOCUMENT_TYPE','NONRENEWAL_NOTICE',N'Nonrenewal Notice'),
('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','CREATE_SUBMISSION',N'Create Submission'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','MARKET_SUBMISSION',N'Market Submission'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','REQUEST_QUOTE',N'Request Quote'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','COMPARE_QUOTES',N'Compare Quotes'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','CREATE_PROPOSAL',N'Create Proposal'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','OBTAIN_SIGNATURE',N'Obtain Signature'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','BIND_COVERAGE',N'Bind Coverage'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','ISSUE_POLICY',N'Issue Policy'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','RENEW_POLICY',N'Renew Policy'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','REMARKET_POLICY',N'Remarket Policy'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','CLOSE_ACTIVITY',N'Close Activity'),('WORKFLOW_ACTION','WORKFLOW','WORKFLOW_ACTION','ESCALATE',N'Escalate'),
('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','PRODUCER_LICENSE',N'Producer License'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','APPOINTMENT',N'Carrier Appointment'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','CONTINUING_EDUCATION',N'Continuing Education'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','SURPLUS_LINES',N'Surplus Lines Compliance'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','PRIVACY',N'Insurance Data Privacy'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','CYBERSECURITY',N'Insurance Cybersecurity'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','AML',N'Anti-Money Laundering'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','OFAC',N'Sanctions Screening'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','MARKET_CONDUCT',N'Market Conduct'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','RECORD_RETENTION',N'Record Retention'),('REGULATORY_COMPLIANCE','COMPLIANCE','REGULATORY_CONCEPT','CONSENT',N'Electronic Consent'),
('RISK_EXPOSURE','EXPOSURE','EXPOSURE','PROPERTY_VALUE',N'Property Value'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','PAYROLL',N'Payroll'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','SALES',N'Gross Sales'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','AREA',N'Building Area'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','VEHICLE_COUNT',N'Vehicle Count'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','DRIVER_COUNT',N'Driver Count'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','EMPLOYEE_COUNT',N'Employee Count'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','CYBER_RECORD_COUNT',N'Sensitive Record Count'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','TOTAL_INSURED_VALUE',N'Total Insured Value'),('RISK_EXPOSURE','EXPOSURE','EXPOSURE','BUSINESS_INCOME',N'Business Income Exposure'),
('INSURED_ASSET','ASSET','ASSET_TYPE','BUILDING',N'Building'),('INSURED_ASSET','ASSET','ASSET_TYPE','PERSONAL_PROPERTY',N'Personal Property'),('INSURED_ASSET','ASSET','ASSET_TYPE','VEHICLE',N'Vehicle'),('INSURED_ASSET','ASSET','ASSET_TYPE','EQUIPMENT',N'Equipment'),('INSURED_ASSET','ASSET','ASSET_TYPE','INVENTORY',N'Inventory'),('INSURED_ASSET','ASSET','ASSET_TYPE','WATERCRAFT',N'Watercraft'),('INSURED_ASSET','ASSET','ASSET_TYPE','AIRCRAFT',N'Aircraft'),('INSURED_ASSET','ASSET','ASSET_TYPE','JEWELRY',N'Jewelry'),('INSURED_ASSET','ASSET','ASSET_TYPE','ELECTRONIC_DATA',N'Electronic Data'),('INSURED_ASSET','ASSET','ASSET_TYPE','LIFE',N'Insured Life');

DECLARE @Jurisdictions TABLE (Code CHAR(2) PRIMARY KEY, Name NVARCHAR(100));
INSERT INTO @Jurisdictions VALUES
('AL',N'Alabama'),('AK',N'Alaska'),('AZ',N'Arizona'),('AR',N'Arkansas'),('CA',N'California'),('CO',N'Colorado'),('CT',N'Connecticut'),('DE',N'Delaware'),('DC',N'District of Columbia'),('FL',N'Florida'),('GA',N'Georgia'),('HI',N'Hawaii'),('ID',N'Idaho'),('IL',N'Illinois'),('IN',N'Indiana'),('IA',N'Iowa'),('KS',N'Kansas'),('KY',N'Kentucky'),('LA',N'Louisiana'),('ME',N'Maine'),('MD',N'Maryland'),('MA',N'Massachusetts'),('MI',N'Michigan'),('MN',N'Minnesota'),('MS',N'Mississippi'),('MO',N'Missouri'),('MT',N'Montana'),('NE',N'Nebraska'),('NV',N'Nevada'),('NH',N'New Hampshire'),('NJ',N'New Jersey'),('NM',N'New Mexico'),('NY',N'New York'),('NC',N'North Carolina'),('ND',N'North Dakota'),('OH',N'Ohio'),('OK',N'Oklahoma'),('OR',N'Oregon'),('PA',N'Pennsylvania'),('RI',N'Rhode Island'),('SC',N'South Carolina'),('SD',N'South Dakota'),('TN',N'Tennessee'),('TX',N'Texas'),('UT',N'Utah'),('VT',N'Vermont'),('VA',N'Virginia'),('WA',N'Washington'),('WV',N'West Virginia'),('WI',N'Wisconsin'),('WY',N'Wyoming'),('AS',N'American Samoa'),('GU',N'Guam'),('MP',N'Northern Mariana Islands'),('PR',N'Puerto Rico'),('VI',N'U.S. Virgin Islands');
INSERT INTO @Concepts (SchemeCode,ConceptCode,ConceptTypeCode,PreferredLabel,ParentCode,Depth,IsAbstract)
SELECT 'US_JURISDICTION', 'JURISDICTION.' + Code, 'REGULATORY_CONCEPT', Name, NULL, 0, 0 FROM @Jurisdictions;

INSERT INTO @Concepts (SchemeCode,ConceptCode,ConceptTypeCode,PreferredLabel,ParentCode,Depth,IsAbstract)
SELECT SchemeCode, CodePrefix + '.' + ValueCode, ConceptTypeCode, DisplayName, NULL, 0, 0
FROM @FlatConcepts;

DECLARE @CurrentDepth INT = 0;
WHILE @CurrentDepth <= (SELECT MAX(Depth) FROM @Concepts)
BEGIN
	INSERT INTO knowledge.KnowledgeConcept
	(KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, NormalizedPreferredLabel,
	 Definition, ParentConceptId, IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc,
	 VersionNumber, SupersedesConceptId, TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId,
	 TechnicalStewardUserId, DefinitionSource, LicensingNotes, CreatedByUserId, CreatedDateUtc, IsDeleted)
	SELECT source.SeedId, scheme.ConceptSchemeId, source.ConceptCode, source.ConceptTypeCode, source.PreferredLabel,
		   UPPER(source.PreferredLabel), N'Canonical synthetic AMS reference concept for ' + source.PreferredLabel + N'.',
		   parent.KnowledgeConceptId, source.IsAbstract, IIF(source.IsAbstract = 1, 0, 1), 'PUBLISHED', @Now, NULL,
		   1, NULL, NULL, 1, @SystemUserId, @SystemUserId, @SystemUserId,
		   N'AMS enterprise synthetic insurance reference catalog',
		   N'AMS-authored synthetic reference terminology; no proprietary standard or carrier dataset is reproduced.',
		   @SystemUserId, @Now, 0
	FROM @Concepts source
	INNER JOIN knowledge.ConceptScheme scheme ON scheme.SchemeCode = source.SchemeCode AND scheme.TenantId IS NULL AND scheme.IsDeleted = 0
	LEFT JOIN knowledge.KnowledgeConcept parent ON parent.ConceptSchemeId = scheme.ConceptSchemeId
		AND parent.ConceptCode = source.ParentCode AND parent.VersionNumber = 1 AND parent.IsDeleted = 0
	WHERE source.Depth = @CurrentDepth
	  AND (source.ParentCode IS NULL OR parent.KnowledgeConceptId IS NOT NULL)
	  AND NOT EXISTS
	  (
		  SELECT 1 FROM knowledge.KnowledgeConcept target
		  WHERE target.ConceptSchemeId = scheme.ConceptSchemeId
			AND target.ConceptCode = source.ConceptCode AND target.VersionNumber = 1
	  );
	SET @CurrentDepth += 1;
END;

-- Every concept receives a searchable preferred label and selected common AMS abbreviations/synonyms.
INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source,
 IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), concept.KnowledgeConceptId, concept.PreferredLabel, UPPER(concept.PreferredLabel), 'PREFERRED', 'en-US',
	   N'AMS enterprise synthetic reference catalog', 1, 0, NULL, 1, @SystemUserId, @Now, 0
FROM knowledge.KnowledgeConcept concept
WHERE concept.TenantId IS NULL AND concept.IsSystemDefined = 1 AND concept.IsDeleted = 0
  AND NOT EXISTS
  (
	  SELECT 1 FROM knowledge.ConceptLabel label
	  WHERE label.KnowledgeConceptId = concept.KnowledgeConceptId AND label.LanguageCode = 'en-US'
		AND label.NormalizedLabel = UPPER(concept.PreferredLabel) AND label.IsDeleted = 0 AND label.IsDeprecated = 0
  );

DECLARE @Synonyms TABLE (ConceptCode VARCHAR(100), Label NVARCHAR(250), LabelTypeCode VARCHAR(30), PRIMARY KEY (ConceptCode, Label));
INSERT INTO @Synonyms VALUES
('LOB.BUSINESS_OWNERS',N'BOP','ABBREVIATION'),('LOB.COMMERCIAL_GENERAL_LIABILITY',N'CGL','ABBREVIATION'),
('LOB.WORKERS_COMPENSATION',N'Workers Comp','ALTERNATIVE'),('LOB.WORKERS_COMPENSATION',N'WC','ABBREVIATION'),
('LOB.PROFESSIONAL_LIABILITY',N'Errors and Omissions','ALTERNATIVE'),('LOB.PROFESSIONAL_LIABILITY',N'E&O','ABBREVIATION'),
('LOB.CYBER_LIABILITY',N'Cyber Insurance','ALTERNATIVE'),('LOB.PERSONAL_AUTO',N'Personal Automobile','ALTERNATIVE'),
('LOB.COMMERCIAL_AUTO',N'Business Auto','ALTERNATIVE'),('LOB.INLAND_MARINE_COMMERCIAL',N'Commercial Floater','ALTERNATIVE'),
('COVERAGE.PERSONAL_INJURY_PROTECTION',N'PIP','ABBREVIATION'),('COVERAGE.UNINSURED_MOTORIST',N'UM','ABBREVIATION'),
('COVERAGE.UNDERINSURED_MOTORIST',N'UIM','ABBREVIATION'),('COVERAGE.BUSINESS_PERSONAL_PROPERTY',N'BPP','ABBREVIATION'),
('COVERAGE.EMPLOYMENT_PRACTICES',N'EPLI','ABBREVIATION'),('COVERAGE.DIRECTORS_OFFICERS',N'D&O','ABBREVIATION'),
('COVERAGE.ACCIDENTAL_DEATH',N'AD&D','ABBREVIATION'),('BENEFIT.MEDICAL.HDHP',N'HDHP','ABBREVIATION'),
('BENEFIT.MEDICAL.PPO',N'PPO','ABBREVIATION'),('BENEFIT.MEDICAL.HMO',N'HMO','ABBREVIATION'),
('BENEFIT.FSA',N'FSA','ABBREVIATION'),('BENEFIT.HSA',N'HSA','ABBREVIATION'),
('ANNUITY.INDEXED',N'FIA','ABBREVIATION'),('PAYMENT.ACH',N'Automated Clearing House','ALTERNATIVE'),
('CHANNEL.MGA',N'MGA','ABBREVIATION'),('DOCUMENT.CERTIFICATE',N'COI','ABBREVIATION');
INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source,
 IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), concept.KnowledgeConceptId, source.Label, UPPER(source.Label), source.LabelTypeCode, 'en-US',
	   N'AMS enterprise synthetic reference catalog', 1, 0, NULL, 1, @SystemUserId, @Now, 0
FROM @Synonyms source
INNER JOIN knowledge.KnowledgeConcept concept ON concept.ConceptCode = source.ConceptCode
	AND concept.TenantId IS NULL AND concept.VersionNumber = 1 AND concept.IsDeleted = 0
WHERE NOT EXISTS
(
	SELECT 1 FROM knowledge.ConceptLabel target
	WHERE target.KnowledgeConceptId = concept.KnowledgeConceptId AND target.LanguageCode = 'en-US'
	  AND target.NormalizedLabel = UPPER(source.Label) AND target.IsDeleted = 0 AND target.IsDeprecated = 0
);

-- Governed snapshots for all newly available system concepts.
INSERT INTO knowledge.ConceptVersion
(ConceptVersionId, KnowledgeConceptId, VersionNumber, StatusCode, SnapshotJson, ChangeReason, CreatedByUserId, CreatedDateUtc)
SELECT NEWID(), concept.KnowledgeConceptId, concept.VersionNumber, concept.StatusCode,
	   (SELECT concept.KnowledgeConceptId, concept.ConceptSchemeId, concept.ConceptCode, concept.ConceptTypeCode,
			   concept.PreferredLabel, concept.Definition, concept.ParentConceptId, concept.StatusCode,
			   concept.EffectiveFromUtc, concept.VersionNumber FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
	   N'Enterprise insurance reference catalog publication.', @SystemUserId, @Now
FROM knowledge.KnowledgeConcept concept
WHERE concept.TenantId IS NULL AND concept.IsSystemDefined = 1 AND concept.IsDeleted = 0
  AND NOT EXISTS
  (
	  SELECT 1 FROM knowledge.ConceptVersion version
	  WHERE version.KnowledgeConceptId = concept.KnowledgeConceptId AND version.VersionNumber = concept.VersionNumber
  );

DECLARE @Relationships TABLE (SubjectCode VARCHAR(100), PredicateCode VARCHAR(100), ObjectCode VARCHAR(100), PRIMARY KEY (SubjectCode, PredicateCode, ObjectCode));
INSERT INTO @Relationships VALUES
('LOB.BUSINESS_OWNERS','COVERS','COVERAGE.BUILDING'),('LOB.BUSINESS_OWNERS','COVERS','COVERAGE.BUSINESS_PERSONAL_PROPERTY'),
('LOB.BUSINESS_OWNERS','COVERS','COVERAGE.GENERAL_LIABILITY'),('LOB.COMMERCIAL_PROPERTY','COVERS','COVERAGE.BUILDING'),
('LOB.COMMERCIAL_PROPERTY','COVERS','COVERAGE.BUSINESS_INCOME'),('LOB.COMMERCIAL_GENERAL_LIABILITY','COVERS','COVERAGE.GENERAL_LIABILITY'),
('LOB.PROFESSIONAL_LIABILITY','COVERS','COVERAGE.PROFESSIONAL'),('LOB.COMMERCIAL_AUTO','COVERS','COVERAGE.BODILY_INJURY'),
('LOB.PERSONAL_AUTO','COVERS','COVERAGE.COLLISION'),('LOB.PERSONAL_AUTO','COVERS','COVERAGE.COMPREHENSIVE'),
('LOB.CYBER_LIABILITY','COVERS','COVERAGE.CYBER'),('PERIL.CYBER_BREACH','APPLIES_TO','LOB.CYBER_LIABILITY'),
('PERIL.RANSOMWARE','APPLIES_TO','LOB.CYBER_LIABILITY'),('PERIL.FIRE','APPLIES_TO','LOB.COMMERCIAL_PROPERTY'),
('PERIL.WIND','APPLIES_TO','LOB.HOMEOWNERS'),('PERIL.EMPLOYEE_INJURY','APPLIES_TO','LOB.WORKERS_COMPENSATION'),
('BENEFIT.MEDICAL.HDHP','RELATED_TO','BENEFIT.HSA'),('COVERAGE.DEATH_BENEFIT','APPLIES_TO','LIFE.TERM');
INSERT INTO knowledge.ConceptRelationship
(ConceptRelationshipId, SubjectConceptId, PredicateCode, ObjectConceptId, RelationshipStrength, Source,
 EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), subject.KnowledgeConceptId, source.PredicateCode, object.KnowledgeConceptId, 1.0000,
	   N'AMS enterprise synthetic reference catalog', @Now, NULL, 'PUBLISHED', NULL, 1, @SystemUserId, @Now, 0
FROM @Relationships source
INNER JOIN knowledge.KnowledgeConcept subject ON subject.ConceptCode = source.SubjectCode AND subject.TenantId IS NULL AND subject.IsDeleted = 0
INNER JOIN knowledge.KnowledgeConcept object ON object.ConceptCode = source.ObjectCode AND object.TenantId IS NULL AND object.IsDeleted = 0
WHERE NOT EXISTS
(
	SELECT 1 FROM knowledge.ConceptRelationship target
	WHERE target.SubjectConceptId = subject.KnowledgeConceptId AND target.PredicateCode = source.PredicateCode
	  AND target.ObjectConceptId = object.KnowledgeConceptId AND target.IsDeleted = 0
);

DECLARE @Rules TABLE
(
	ConceptCode VARCHAR(100), RuleCode VARCHAR(100), RuleTypeCode VARCHAR(50), PropertyPath NVARCHAR(500),
	OperatorCode VARCHAR(50), ExpectedValue NVARCHAR(MAX), MinimumCount INT, MaximumCount INT,
	SeverityCode VARCHAR(30), Message NVARCHAR(1000), PRIMARY KEY (RuleCode)
);
INSERT INTO @Rules VALUES
('PRODUCT.INSURANCE','RULE.POLICY.EFFECTIVE_BEFORE_EXPIRATION','DATECONSTRAINT',N'policy.expirationDate',N'GREATER_THAN',N'policy.effectiveDate',NULL,NULL,'HARD_BLOCKER',N'Policy expiration must be after policy effective date.'),
('PRODUCT.INSURANCE','RULE.POLICY.INSURED_REQUIRED','ROLEREQUIRED',N'policy.parties',N'CONTAINS_ROLE',N'NAMED_INSURED',1,NULL,'HARD_BLOCKER',N'At least one named insured is required.'),
('PRODUCT.COMMERCIAL_LINES','RULE.COMMERCIAL.BUSINESS_CLASS_REQUIRED','REQUIREDPROPERTY',N'account.businessClassification',N'NOT_EMPTY',NULL,NULL,NULL,'SOFT_BLOCKER',N'Business classification is required for commercial business.'),
('LOB.COMMERCIAL_AUTO','RULE.AUTO.VEHICLE_REQUIRED','MINIMUMCOUNT',N'policy.vehicles',N'COUNT',NULL,1,NULL,'HARD_BLOCKER',N'At least one vehicle is required for automobile coverage.'),
('LOB.WORKERS_COMPENSATION','RULE.WC.PAYROLL_REQUIRED','REQUIREDPROPERTY',N'risk.payroll',N'GREATER_THAN',N'0',NULL,NULL,'HARD_BLOCKER',N'Workers compensation submissions require payroll exposure.'),
('LOB.COMMERCIAL_PROPERTY','RULE.PROPERTY.LOCATION_REQUIRED','MINIMUMCOUNT',N'policy.locations',N'COUNT',NULL,1,NULL,'HARD_BLOCKER',N'At least one insured location is required.'),
('BENEFIT.ROOT','RULE.BENEFIT.ELIGIBILITY_REQUIRED','REQUIREDPROPERTY',N'plan.eligibility',N'NOT_EMPTY',NULL,NULL,NULL,'SOFT_BLOCKER',N'Benefit plan eligibility rules are required.'),
('LIFE.ROOT','RULE.LIFE.BENEFICIARY_REQUIRED','ROLEREQUIRED',N'policy.parties',N'CONTAINS_ROLE',N'BENEFICIARY',1,NULL,'SOFT_BLOCKER',N'Life insurance should identify at least one beneficiary.'),
('CLAIM.STATUS.REPORTED','RULE.CLAIM.LOSS_DATE_REQUIRED','REQUIREDPROPERTY',N'claim.lossDate',N'NOT_EMPTY',NULL,NULL,NULL,'HARD_BLOCKER',N'Claim loss date is required.'),
('CLAIM.STATUS.REPORTED','RULE.CLAIM.POLICY_REQUIRED','REQUIREDPROPERTY',N'claim.policyId',N'NOT_EMPTY',NULL,NULL,NULL,'HARD_BLOCKER',N'A claim must be associated with a policy.');
INSERT INTO knowledge.ConceptValidationRule
(ConceptValidationRuleId, AppliesToConceptId, RuleCode, RuleTypeCode, PropertyPath, OperatorCode, ExpectedValue,
 MinimumCount, MaximumCount, SeverityCode, Message, EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId,
 IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), concept.KnowledgeConceptId, source.RuleCode, source.RuleTypeCode, source.PropertyPath, source.OperatorCode,
	   source.ExpectedValue, source.MinimumCount, source.MaximumCount, source.SeverityCode, source.Message,
	   @Now, NULL, 'PUBLISHED', NULL, 1, @SystemUserId, @Now, 0
FROM @Rules source
INNER JOIN knowledge.KnowledgeConcept concept ON concept.ConceptCode = source.ConceptCode AND concept.TenantId IS NULL AND concept.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM knowledge.ConceptValidationRule target WHERE target.RuleCode = source.RuleCode AND target.TenantId IS NULL AND target.IsDeleted = 0);

DECLARE @PublicationId UNIQUEIDENTIFIER = 'a4000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM knowledge.Publication WHERE TenantId IS NULL AND PublicationCode = 'AMS_ENTERPRISE_INSURANCE' AND VersionLabel = '2.0' AND IsDeleted = 0)
BEGIN
	INSERT INTO knowledge.Publication
	(PublicationId, PublicationCode, Name, VersionLabel, StatusCode, TenantId, IsSystemDefined, PublishedByUserId,
	 PublishedDateUtc, CreatedByUserId, CreatedDateUtc, IsDeleted)
	VALUES (@PublicationId, 'AMS_ENTERPRISE_INSURANCE', N'AMS Enterprise Insurance Reference Catalog', '2.0',
			'PUBLISHED', NULL, 1, @SystemUserId, @Now, @SystemUserId, @Now, 0);
END
ELSE
	SELECT @PublicationId = PublicationId FROM knowledge.Publication
	WHERE TenantId IS NULL AND PublicationCode = 'AMS_ENTERPRISE_INSURANCE' AND VersionLabel = '2.0' AND IsDeleted = 0;

INSERT INTO knowledge.PublicationItem (PublicationItemId, PublicationId, EntityTypeCode, EntityId, VersionNumber, SnapshotJson)
SELECT NEWID(), @PublicationId, 'KNOWLEDGE_CONCEPT', concept.KnowledgeConceptId, concept.VersionNumber, version.SnapshotJson
FROM knowledge.KnowledgeConcept concept
INNER JOIN knowledge.ConceptVersion version ON version.KnowledgeConceptId = concept.KnowledgeConceptId AND version.VersionNumber = concept.VersionNumber
WHERE concept.TenantId IS NULL AND concept.IsSystemDefined = 1 AND concept.StatusCode = 'PUBLISHED' AND concept.IsDeleted = 0
  AND NOT EXISTS
  (
	  SELECT 1 FROM knowledge.PublicationItem item
	  WHERE item.PublicationId = @PublicationId AND item.EntityTypeCode = 'KNOWLEDGE_CONCEPT'
		AND item.EntityId = concept.KnowledgeConceptId AND item.VersionNumber = concept.VersionNumber
  );

;WITH Hierarchy AS
(
	SELECT concept.KnowledgeConceptId AS AncestorConceptId, concept.KnowledgeConceptId AS DescendantConceptId, 0 AS Depth
	FROM knowledge.KnowledgeConcept concept WHERE concept.IsDeleted = 0 AND concept.StatusCode = 'PUBLISHED'
	UNION ALL
	SELECT hierarchy.AncestorConceptId, child.KnowledgeConceptId, hierarchy.Depth + 1
	FROM Hierarchy hierarchy
	INNER JOIN knowledge.KnowledgeConcept child ON child.ParentConceptId = hierarchy.DescendantConceptId
	WHERE child.IsDeleted = 0 AND child.StatusCode = 'PUBLISHED'
)
MERGE knowledge.ConceptHierarchyClosure AS target
USING (SELECT AncestorConceptId, DescendantConceptId, MIN(Depth) AS Depth FROM Hierarchy GROUP BY AncestorConceptId, DescendantConceptId) AS source
ON source.AncestorConceptId = target.AncestorConceptId AND source.DescendantConceptId = target.DescendantConceptId
WHEN MATCHED THEN UPDATE SET Depth = source.Depth, RefreshedDateUtc = @Now
WHEN NOT MATCHED THEN INSERT (AncestorConceptId, DescendantConceptId, Depth, RefreshedDateUtc)
VALUES (source.AncestorConceptId, source.DescendantConceptId, source.Depth, @Now);

-- Tenant overlays are created only for active tenants already owned by Core.
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
BEGIN
	DECLARE @DemoTenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY, TenantCode VARCHAR(100));
	INSERT INTO @DemoTenants (TenantId, TenantCode)
	SELECT TenantId, TenantCode
	FROM Core.Tenant
	WHERE IsActive = 1 AND ISNULL(IsDeleted, 0) = 0
	  AND (TenantCode = 'DEMO' OR TenantCode LIKE 'ENT-%');

	DECLARE @TenantConfigurations TABLE
	(
		ConfigurationCode VARCHAR(150) PRIMARY KEY,
		ConfigurationValue NVARCHAR(MAX),
		DataTypeCode VARCHAR(30),
		Description NVARCHAR(1000)
	);
	INSERT INTO @TenantConfigurations VALUES
	('FEATURE_ENABLED',N'true','BOOLEAN',N'Enables semantic enrichment for seeded demonstration tenants.'),
	('RESOLUTION_AUTO_THRESHOLD',N'0.95','DECIMAL',N'Automatic deterministic resolution threshold.'),
	('RESOLUTION_REVIEW_THRESHOLD',N'0.80','DECIMAL',N'Review candidate threshold.'),
	('RESOLUTION_MAX_CANDIDATES',N'20','INTEGER',N'Maximum enterprise catalog candidates returned.'),
	('VALIDATION_BLOCKING_SEVERITIES',N'["HARD_BLOCKER"]','JSON',N'Blocking semantic validation severities.');

	INSERT INTO knowledge.Configuration
	(ConfigurationId, TenantId, ConfigurationCode, ConfigurationValue, DataTypeCode, Description,
	 IsSystemDefined, IsActive, CreatedDateUtc)
	SELECT NEWID(), tenant.TenantId, config.ConfigurationCode, config.ConfigurationValue, config.DataTypeCode,
		   config.Description, 0, 1, @Now
	FROM @DemoTenants tenant
	CROSS JOIN @TenantConfigurations config
	WHERE NOT EXISTS
	(
		SELECT 1 FROM knowledge.Configuration target
		WHERE target.TenantId = tenant.TenantId AND target.ConfigurationCode = config.ConfigurationCode
	);

	DECLARE @MappingTerms TABLE
	(
		ConceptCode VARCHAR(100), ExternalCode NVARCHAR(150), ExternalValue NVARCHAR(500),
		ExternalPath NVARCHAR(500), PRIMARY KEY (ConceptCode, ExternalCode)
	);
	INSERT INTO @MappingTerms VALUES
	('LOB.PERSONAL_AUTO',N'PA',N'Personal Auto',N'Lines/Personal/Auto'),
	('LOB.HOMEOWNERS',N'HO',N'Homeowners',N'Lines/Personal/Home'),
	('LOB.COMMERCIAL_AUTO',N'CA',N'Commercial Auto',N'Lines/Commercial/Auto'),
	('LOB.COMMERCIAL_GENERAL_LIABILITY',N'CGL',N'Commercial General Liability',N'Lines/Commercial/Liability'),
	('LOB.BUSINESS_OWNERS',N'BOP',N'Businessowners Policy',N'Lines/Commercial/Package'),
	('LOB.WORKERS_COMPENSATION',N'WC',N'Workers Compensation',N'Lines/Commercial/WorkersComp'),
	('LOB.COMMERCIAL_PROPERTY',N'CP',N'Commercial Property',N'Lines/Commercial/Property'),
	('LOB.PROFESSIONAL_LIABILITY',N'EO',N'Errors and Omissions',N'Lines/Commercial/Professional'),
	('LOB.CYBER_LIABILITY',N'CYBER',N'Cyber Liability',N'Lines/Commercial/Cyber'),
	('BENEFIT.MEDICAL.PPO',N'PPO',N'PPO Medical',N'Benefits/Medical/PPO'),
	('BENEFIT.MEDICAL.HMO',N'HMO',N'HMO Medical',N'Benefits/Medical/HMO'),
	('LIFE.TERM',N'TERM',N'Term Life',N'Life/Term'),
	('LIFE.WHOLE',N'WHOLE',N'Whole Life',N'Life/Permanent/Whole'),
	('ANNUITY.FIXED',N'FIXEDANN',N'Fixed Annuity',N'Life/Annuity/Fixed'),
	('DOCUMENT.CERTIFICATE',N'COI',N'Certificate of Insurance',N'Documents/Policy/Certificate'),
	('DOCUMENT.LOSS_RUN',N'LOSSRUN',N'Loss Runs',N'Documents/Underwriting/LossRuns'),
	('COVERAGE.PERSONAL_INJURY_PROTECTION',N'PIP',N'Personal Injury Protection',N'Coverage/Auto/PIP'),
	('COVERAGE.UNINSURED_MOTORIST',N'UM',N'Uninsured Motorist',N'Coverage/Auto/UM'),
	('COVERAGE.BUSINESS_PERSONAL_PROPERTY',N'BPP',N'Business Personal Property',N'Coverage/Property/BPP');

	INSERT INTO knowledge.ExternalConceptMapping
	(ExternalConceptMappingId, KnowledgeConceptId, SourceSystemTypeCode, SourceSystemId, ExternalCode,
	 ExternalValue, NormalizedExternalValue, ExternalPath, MappingDirectionCode, MatchTypeCode, ConfidenceScore,
	 StateCode, LineOfBusinessConceptId, CarrierProductId, EffectiveFromUtc, EffectiveToUtc, IsApproved,
	 ApprovedByUserId, ApprovedDateUtc, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), concept.KnowledgeConceptId, 'LEGACY_AMS', NULL, mapping.ExternalCode, mapping.ExternalValue,
		   UPPER(mapping.ExternalValue), mapping.ExternalPath, 'BIDIRECTIONAL', 'EXACT_EXTERNAL_CODE', 1.0000,
		   NULL, CASE WHEN concept.ConceptTypeCode = 'LINE_OF_BUSINESS' THEN concept.KnowledgeConceptId END,
		   NULL, @Now, NULL, 1, @SystemUserId, @Now, tenant.TenantId, 0, @SystemUserId, @Now, 0
	FROM @DemoTenants tenant
	CROSS JOIN @MappingTerms mapping
	INNER JOIN knowledge.KnowledgeConcept concept ON concept.ConceptCode = mapping.ConceptCode
		AND concept.TenantId IS NULL AND concept.VersionNumber = 1 AND concept.IsDeleted = 0
	WHERE NOT EXISTS
	(
		SELECT 1 FROM knowledge.ExternalConceptMapping target
		WHERE target.TenantId = tenant.TenantId AND target.SourceSystemTypeCode = 'LEGACY_AMS'
		  AND target.ExternalCode = mapping.ExternalCode AND target.IsDeleted = 0
	);
END;
