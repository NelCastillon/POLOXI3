-- ============================================================
-- AMS Enterprise Platform – Core, CRM, Client, OPS & Billing
-- Seed Data (run after 00_schema_migration and 02_iam_audit_trail_and_seed)
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- SHARED ID DECLARATIONS (must match 02_ seed file)
-- ============================================================
DECLARE @TenantId       UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId    UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @ManagerUserId  UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000003';
DECLARE @UserUserId     UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000004';

-- Lookup seeded user IDs
DECLARE @SalesUserId    UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'michael.sales@enterprise.com');
DECLARE @FinanceUserId  UNIQUEIDENTIFIER = (SELECT UserId FROM IAM.[User] WHERE UserName = 'emily.finance@enterprise.com');

-- ============================================================
-- CORE.TENANT
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Core.Tenant (
        TenantId, TenantCode, TenantName, StatusCode, PlanCode,
        RegionCode, IsolationMode, PrimaryDomain, ActiveUsers, IsActive,
        Locale, CurrencyCode, TimeZoneId, GoLiveDateUtc, CreatedDateUtc
    ) VALUES
        (@TenantId, 'DEMO', 'Demo Agency', 'Active', 'Enterprise',
         'US-EAST', 'Dedicated', 'demo.agency', 7, 1,
         'en-US', 'USD', 'Eastern Standard Time', DATEADD(MONTH, -12, SYSUTCDATETIME()), DATEADD(MONTH, -13, SYSUTCDATETIME())),
        (NEWID(), 'ENT-002', 'Acme Insurance Group', 'Active', 'Professional',
         'US-WEST', 'Shared', 'acme-insurance.com', 3, 1,
         'en-US', 'USD', 'Pacific Standard Time', DATEADD(MONTH, -6, SYSUTCDATETIME()), DATEADD(MONTH, -7, SYSUTCDATETIME())),
        (NEWID(), 'ENT-003', 'Midwest Brokerage LLC', 'Active', 'Standard',
         'US-CENTRAL', 'Shared', 'midwestbrokerage.com', 2, 1,
         'en-US', 'USD', 'Central Standard Time', DATEADD(MONTH, -3, SYSUTCDATETIME()), DATEADD(MONTH, -4, SYSUTCDATETIME())),
        (NEWID(), 'ENT-004', 'Legacy Assurance Co.', 'Suspended', 'Standard',
         'US-EAST', 'Shared', 'legacy-assurance.com', 0, 0,
         'en-US', 'USD', 'Eastern Standard Time', DATEADD(MONTH, -24, SYSUTCDATETIME()), DATEADD(MONTH, -25, SYSUTCDATETIME()));
END

UPDATE Core.Tenant
SET TenantCode = 'DEMO',
    TenantName = 'Demo Agency',
    StatusCode = 'Active',
    PlanCode = 'Enterprise',
    RegionCode = 'US-EAST',
    IsolationMode = 'Dedicated',
    PrimaryDomain = 'demo.agency',
    ActiveUsers = 7,
    IsActive = 1,
    Locale = 'en-US',
    CurrencyCode = 'USD',
    TimeZoneId = 'Eastern Standard Time',
    ModifiedDateUtc = SYSUTCDATETIME(),
    IsDeleted = 0
WHERE TenantId = @TenantId;

-- ============================================================
-- CORE.TENANTDOMAIN
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.TenantDomain WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Core.TenantDomain (
        TenantDomainId, TenantId, DomainName, IsPrimary,
        SslStatusCode, VerificationStatusCode, VerifiedDateUtc,
        IsActive, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, 'demo.agency',       1, 'Valid',   'Verified',  DATEADD(MONTH, -12, SYSUTCDATETIME()), 1, DATEADD(MONTH, -13, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, 'app.demo.agency',   0, 'Valid',   'Verified',  DATEADD(MONTH, -12, SYSUTCDATETIME()), 1, DATEADD(MONTH, -13, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, 'portal.demo.agency',0, 'Pending', 'Pending',   NULL,                                  1, DATEADD(DAY,   -5,  SYSUTCDATETIME()), @AdminUserId);
END

IF EXISTS (SELECT 1 FROM Core.TenantDomain WHERE TenantId = @TenantId AND IsPrimary = 1 AND DomainName <> 'demo.agency')
BEGIN
    UPDATE Core.TenantDomain
    SET IsPrimary = 0,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND DomainName <> 'demo.agency';
END

IF EXISTS (SELECT 1 FROM Core.TenantDomain WHERE TenantId = @TenantId AND DomainName = 'demo.agency')
BEGIN
    UPDATE Core.TenantDomain
    SET IsPrimary = 1,
        SslStatusCode = 'Valid',
        VerificationStatusCode = 'Verified',
        VerifiedDateUtc = COALESCE(VerifiedDateUtc, SYSUTCDATETIME()),
        IsActive = 1,
        ModifiedDateUtc = SYSUTCDATETIME(),
        IsDeleted = 0
    WHERE TenantId = @TenantId
      AND DomainName = 'demo.agency';
END
ELSE
BEGIN
    INSERT INTO Core.TenantDomain (TenantDomainId, TenantId, DomainName, IsPrimary, SslStatusCode, VerificationStatusCode, VerifiedDateUtc, IsActive, CreatedDateUtc, CreatedByUserId)
    VALUES (NEWID(), @TenantId, 'demo.agency', 1, 'Valid', 'Verified', SYSUTCDATETIME(), 1, SYSUTCDATETIME(), @AdminUserId);
END

IF NOT EXISTS (SELECT 1 FROM Core.TenantDomain WHERE TenantId = @TenantId AND DomainName = 'app.demo.agency')
    INSERT INTO Core.TenantDomain (TenantDomainId, TenantId, DomainName, IsPrimary, SslStatusCode, VerificationStatusCode, VerifiedDateUtc, IsActive, CreatedDateUtc, CreatedByUserId)
    VALUES (NEWID(), @TenantId, 'app.demo.agency', 0, 'Valid', 'Verified', SYSUTCDATETIME(), 1, SYSUTCDATETIME(), @AdminUserId);

IF NOT EXISTS (SELECT 1 FROM Core.TenantDomain WHERE TenantId = @TenantId AND DomainName = 'portal.demo.agency')
    INSERT INTO Core.TenantDomain (TenantDomainId, TenantId, DomainName, IsPrimary, SslStatusCode, VerificationStatusCode, IsActive, CreatedDateUtc, CreatedByUserId)
    VALUES (NEWID(), @TenantId, 'portal.demo.agency', 0, 'Pending', 'Pending', 1, SYSUTCDATETIME(), @AdminUserId);

-- ============================================================
-- CORE.BRANCH
-- ============================================================
DECLARE @HeadquartersBranchId  UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';
DECLARE @WestBranchId          UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000002';
DECLARE @SouthBranchId         UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM Core.Branch WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Core.Branch (
        BranchId, TenantId, BranchCode, BranchName,
        City, StateProvince, CountryCode, IsActive, CreatedDateUtc
    ) VALUES
        (@HeadquartersBranchId, @TenantId, 'HQ',    'Headquarters',          'New York',    'NY', 'US', 1, DATEADD(MONTH, -13, SYSUTCDATETIME())),
        (@WestBranchId,         @TenantId, 'WEST',  'West Coast Branch',     'Los Angeles', 'CA', 'US', 1, DATEADD(MONTH, -10, SYSUTCDATETIME())),
        (@SouthBranchId,        @TenantId, 'SOUTH', 'Southern Region Office','Houston',     'TX', 'US', 1, DATEADD(MONTH,  -8, SYSUTCDATETIME()));
END

-- ============================================================
-- COMMERCIAL.PLAN
-- ============================================================
DECLARE @StandardPlanId     UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000001';
DECLARE @ProfessionalPlanId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000002';
DECLARE @EnterprisePlanId   UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM Commercial.[Plan] WHERE PlanCode = 'STANDARD')
BEGIN
    INSERT INTO Commercial.[Plan] (
        PlanId, PlanCode, PlanName, BillingFrequency, BasePrice,
        IncludedUsers, IncludedStorageGb, IncludedApiCallsPerDay,
        IsEnterprise, IsActive, CreatedDateUtc
    ) VALUES
        (@StandardPlanId,     'STANDARD',     'Standard',     'Monthly', 99.00,    10,  50.00,  10000, 0, 1, SYSUTCDATETIME()),
        (@ProfessionalPlanId, 'PROFESSIONAL', 'Professional', 'Monthly', 299.00,   50,  250.00, 50000, 0, 1, SYSUTCDATETIME()),
        (@EnterprisePlanId,   'ENTERPRISE',   'Enterprise',   'Monthly', 999.00,   500, 2000.00,500000,1, 1, SYSUTCDATETIME());
END

-- ============================================================
-- COMMERCIAL.SUBSCRIPTION
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Commercial.Subscription WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Commercial.Subscription (
        SubscriptionId, TenantId, PlanId, StatusCode, RenewalType,
        BillingCycle, BaseAmount, StartDateUtc, EndDateUtc, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @EnterprisePlanId, 'Active', 'Auto',
         'Annual', 999.00, DATEADD(MONTH, -12, SYSUTCDATETIME()), DATEADD(MONTH, 0, SYSUTCDATETIME()), DATEADD(MONTH, -12, SYSUTCDATETIME()), @AdminUserId);
END

-- ============================================================
-- CLIENT.ACCOUNT
-- ============================================================
DECLARE @Account1Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
DECLARE @Account2Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002';
DECLARE @Account3Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000003';
DECLARE @Account4Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000004';
DECLARE @Account5Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000005';

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Client.Account (
        AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
        MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Account1Id, @TenantId, 'ACC-0001', 'Pinnacle Financial Group',     'Commercial', 'contact@pinnacle-fin.com',  '212-555-0101', 'Active', 'Enterprise', @SalesUserId,   DATEADD(MONTH, -11, SYSUTCDATETIME()), @AdminUserId),
        (@Account2Id, @TenantId, 'ACC-0002', 'Sunrise Healthcare Partners',  'Commercial', 'info@sunrisehealth.com',    '310-555-0202', 'Active', 'Mid-Market',  @SalesUserId,   DATEADD(MONTH, -9,  SYSUTCDATETIME()), @AdminUserId),
        (@Account3Id, @TenantId, 'ACC-0003', 'Patterson Construction LLC',   'Commercial', 'ops@pattersonconst.com',   '713-555-0303', 'Active', 'SMB',         @ManagerUserId, DATEADD(MONTH, -7,  SYSUTCDATETIME()), @ManagerUserId),
        (@Account4Id, @TenantId, 'ACC-0004', 'Horizon Retail Solutions',     'Commercial', 'billing@horizonretail.com','615-555-0404', 'Active', 'Mid-Market',  @SalesUserId,   DATEADD(MONTH, -5,  SYSUTCDATETIME()), @AdminUserId),
        (@Account5Id, @TenantId, 'ACC-0005', 'Clearwater Technology Inc.',   'Commercial', 'admin@clearwatertech.com', '206-555-0505', 'Inactive','SMB',        @UserUserId,    DATEADD(MONTH, -3,  SYSUTCDATETIME()), @ManagerUserId);
END

-- ============================================================
-- CLIENT.CONTACT
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Client.Contact (
        ContactId, TenantId, AccountId, FirstName, LastName,
        Email, Phone, JobTitle, ContactTypeCode, IsBillingContact,
        IsPortalUser, StatusCode, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Account1Id, 'James',    'Crawford',  'james.crawford@pinnacle-fin.com',   '212-555-1001', 'CFO',               'Primary',   1, 1, 'Active', DATEADD(MONTH, -11, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @Account1Id, 'Patricia', 'Wells',     'p.wells@pinnacle-fin.com',          '212-555-1002', 'Risk Manager',      'Secondary', 0, 0, 'Active', DATEADD(MONTH, -11, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @Account2Id, 'Marcus',   'Thompson',  'marcus.t@sunrisehealth.com',        '310-555-2001', 'Operations Director','Primary',  1, 1, 'Active', DATEADD(MONTH, -9,  SYSUTCDATETIME()), @SalesUserId),
        (NEWID(), @TenantId, @Account2Id, 'Sandra',   'Liu',       's.liu@sunrisehealth.com',           '310-555-2002', 'Finance Manager',   'Billing',   1, 0, 'Active', DATEADD(MONTH, -9,  SYSUTCDATETIME()), @SalesUserId),
        (NEWID(), @TenantId, @Account3Id, 'David',    'Patterson', 'dave@pattersonconst.com',           '713-555-3001', 'Owner',             'Primary',   1, 1, 'Active', DATEADD(MONTH, -7,  SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Account4Id, 'Olivia',   'Chen',      'o.chen@horizonretail.com',          '615-555-4001', 'CEO',               'Primary',   1, 1, 'Active', DATEADD(MONTH, -5,  SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @Account4Id, 'Kevin',    'Morris',    'k.morris@horizonretail.com',        '615-555-4002', 'Accounts Payable',  'Billing',   1, 0, 'Active', DATEADD(MONTH, -5,  SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, @Account5Id, 'Rachel',   'Nguyen',    'r.nguyen@clearwatertech.com',       '206-555-5001', 'IT Director',       'Primary',   1, 0, 'Inactive',DATEADD(MONTH,-3,  SYSUTCDATETIME()), @ManagerUserId);
END

-- ============================================================
-- CRM.LEAD
-- ============================================================
DECLARE @Lead1Id UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000001';
DECLARE @Lead2Id UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000002';
DECLARE @Lead3Id UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO CRM.Lead (
        LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName,
        Email, Phone, InterestedService, Score, PriorityCode,
        AssignedToUserId, StatusCodeId, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Lead1Id, @TenantId, 'LED-0001', 'Apex Logistics',        'Brian',    'Foster',    'b.foster@apexlogistics.com',   '404-555-6001', 'Commercial Auto',       82, 'High',   @SalesUserId,   1, DATEADD(DAY, -20, SYSUTCDATETIME()), @SalesUserId),
        (@Lead2Id, @TenantId, 'LED-0002', 'BlueSky Manufacturing', 'Nicole',   'Harmon',    'n.harmon@bluesky-mfg.com',     '312-555-6002', 'Workers Comp',          65, 'Medium', @SalesUserId,   1, DATEADD(DAY, -14, SYSUTCDATETIME()), @SalesUserId),
        (@Lead3Id, @TenantId, 'LED-0003', 'Crestview Apartments',  'Timothy',  'Grant',     't.grant@crestview-apts.com',   '702-555-6003', 'Property & Casualty',   44, 'Low',    @ManagerUserId, 1, DATEADD(DAY,  -7, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(),  @TenantId, 'LED-0004', 'TechVenture Capital',   'Stephanie','Nguyen',    's.nguyen@techventurecap.com',  '415-555-6004', 'D&O Liability',         91, 'High',   @SalesUserId,   1, DATEADD(DAY,  -3, SYSUTCDATETIME()), @SalesUserId),
        (NEWID(),  @TenantId, 'LED-0005', 'Metro Dental Group',    'Charles',  'Ruiz',      'c.ruiz@metrodental.com',       '818-555-6005', 'Medical Malpractice',   58, 'Medium', @SalesUserId,   2, DATEADD(DAY, -30, SYSUTCDATETIME()), @SalesUserId);
END

-- ============================================================
-- CRM.OPPORTUNITY
-- ============================================================
DECLARE @Opp1Id UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000001';
DECLARE @Opp2Id UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000002';
DECLARE @Opp3Id UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000003';
DECLARE @Opp4Id UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000004';

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO CRM.Opportunity (
        OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
        EstimatedAmount, OwnerUserId, CloseDate, StatusCodeId,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Opp1Id, @TenantId, 'OPP-0001', @Account1Id, 'Pinnacle Commercial Package Renewal',  125000.00, @SalesUserId,   DATEADD(DAY,  30, GETDATE()), 1, DATEADD(MONTH, -2, SYSUTCDATETIME()), @SalesUserId),
        (@Opp2Id, @TenantId, 'OPP-0002', @Account2Id, 'Sunrise Healthcare Benefits Expansion', 87500.00, @SalesUserId,   DATEADD(DAY,  45, GETDATE()), 1, DATEADD(MONTH, -1, SYSUTCDATETIME()), @SalesUserId),
        (@Opp3Id, @TenantId, 'OPP-0003', @Account3Id, 'Patterson GL & Workers Comp Bundle',    42000.00, @ManagerUserId, DATEADD(DAY,  15, GETDATE()), 1, DATEADD(DAY,  -20, SYSUTCDATETIME()), @ManagerUserId),
        (@Opp4Id, @TenantId, 'OPP-0004', @Account4Id, 'Horizon Retail Property Coverage',      68000.00, @SalesUserId,   DATEADD(DAY,  60, GETDATE()), 2, DATEADD(MONTH, -3, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, 'OPP-0005', @Account1Id, 'Pinnacle Cyber Liability Add-on',       28500.00, @SalesUserId,   DATEADD(DAY,  10, GETDATE()), 1, DATEADD(DAY,   -5, SYSUTCDATETIME()), @SalesUserId);
END

-- ============================================================
-- CRM.QUOTE
-- ============================================================
DECLARE @Quote1Id UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000001';
DECLARE @Quote2Id UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO CRM.Quote (
        QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId,
        TotalAmount, ValidUntilDate, StatusCode,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Quote1Id, @TenantId, 'QUO-0001', @Opp1Id, @Account1Id, 125000.00, DATEADD(DAY, 30, GETDATE()), 'Sent',   DATEADD(DAY, -10, SYSUTCDATETIME()), @SalesUserId),
        (@Quote2Id, @TenantId, 'QUO-0002', @Opp2Id, @Account2Id,  87500.00, DATEADD(DAY, 30, GETDATE()), 'Draft',  DATEADD(DAY,  -5, SYSUTCDATETIME()), @SalesUserId),
        (NEWID(),  @TenantId,  'QUO-0003', @Opp3Id, @Account3Id,  42000.00, DATEADD(DAY, 30, GETDATE()), 'Draft',  DATEADD(DAY,  -3, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(),  @TenantId,  'QUO-0004', @Opp4Id, @Account4Id,  68000.00, DATEADD(DAY,-15, GETDATE()), 'Expired',DATEADD(MONTH,-3, SYSUTCDATETIME()),  @AdminUserId);
END

-- ============================================================
-- FINANCE.AGREEMENT
-- ============================================================
DECLARE @Agree1Id UNIQUEIDENTIFIER = '80000000-0000-0000-0000-000000000001';
DECLARE @Agree2Id UNIQUEIDENTIFIER = '80000000-0000-0000-0000-000000000002';
DECLARE @Agree3Id UNIQUEIDENTIFIER = '80000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM Finance.Agreement WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Finance.Agreement (
        AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId,
        EffectiveStartDate, EffectiveEndDate, TotalContractValue, StatusCodeId,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Agree1Id, @TenantId, 'AGR-0001', @Account1Id, @Opp1Id, DATEADD(MONTH, -11, GETDATE()), DATEADD(MONTH,  1, GETDATE()), 125000.00, 1, DATEADD(MONTH, -11, SYSUTCDATETIME()), @AdminUserId),
        (@Agree2Id, @TenantId, 'AGR-0002', @Account2Id, @Opp2Id, DATEADD(MONTH,  -9, GETDATE()), DATEADD(MONTH,  3, GETDATE()),  87500.00, 1, DATEADD(MONTH,  -9, SYSUTCDATETIME()), @AdminUserId),
        (@Agree3Id, @TenantId, 'AGR-0003', @Account3Id, @Opp3Id, DATEADD(MONTH,  -7, GETDATE()), DATEADD(MONTH,  5, GETDATE()),  42000.00, 1, DATEADD(MONTH,  -7, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(),   @TenantId, 'AGR-0004', @Account4Id, NULL,    DATEADD(MONTH, -24, GETDATE()), DATEADD(MONTH, -1, GETDATE()),  68000.00, 3, DATEADD(MONTH, -24, SYSUTCDATETIME()), @AdminUserId);
END

-- ============================================================
-- OPS.ENGAGEMENT
-- ============================================================
DECLARE @Eng1Id UNIQUEIDENTIFIER = '90000000-0000-0000-0000-000000000001';
DECLARE @Eng2Id UNIQUEIDENTIFIER = '90000000-0000-0000-0000-000000000002';
DECLARE @Eng3Id UNIQUEIDENTIFIER = '90000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM OPS.Engagement WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO OPS.Engagement (
        EngagementId, TenantId, EngagementNumber, AccountId, AgreementId,
        EngagementName, EngagementTypeCode, OwnerUserId,
        StartDate, EndDate, StatusCode,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Eng1Id, @TenantId, 'ENG-0001', @Account1Id, @Agree1Id, 'Pinnacle Annual Review & Servicing',  'Servicing', @ManagerUserId, DATEADD(MONTH, -11, GETDATE()), DATEADD(MONTH,  1, GETDATE()), 'Active',    DATEADD(MONTH, -11, SYSUTCDATETIME()), @AdminUserId),
        (@Eng2Id, @TenantId, 'ENG-0002', @Account2Id, @Agree2Id, 'Sunrise Benefits Implementation',     'Project',   @ManagerUserId, DATEADD(MONTH,  -9, GETDATE()), DATEADD(MONTH,  3, GETDATE()), 'Active',    DATEADD(MONTH,  -9, SYSUTCDATETIME()), @AdminUserId),
        (@Eng3Id, @TenantId, 'ENG-0003', @Account3Id, @Agree3Id, 'Patterson Workers Comp Onboarding',   'Project',   @UserUserId,    DATEADD(MONTH,  -7, GETDATE()), DATEADD(MONTH,  5, GETDATE()), 'Active',    DATEADD(MONTH,  -7, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, 'ENG-0004', @Account4Id, NULL,      'Horizon Q1 Policy Review',            'Servicing', @ManagerUserId, DATEADD(MONTH,  -2, GETDATE()), DATEADD(MONTH,  1, GETDATE()), 'Active',    DATEADD(MONTH,  -2, SYSUTCDATETIME()), @AdminUserId),
        (NEWID(), @TenantId, 'ENG-0005', @Account1Id, @Agree1Id, 'Pinnacle Cyber Risk Assessment',      'Advisory',  @SalesUserId,   DATEADD(MONTH,  -1, GETDATE()), DATEADD(MONTH,  2, GETDATE()), 'Active',    DATEADD(MONTH,  -1, SYSUTCDATETIME()), @AdminUserId);
END

-- ============================================================
-- OPS.ENGAGEMENTMILESTONE
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM OPS.EngagementMilestone WHERE EngagementId = @Eng1Id)
BEGIN
    INSERT INTO OPS.EngagementMilestone (
        MilestoneId, TenantId, EngagementId, MilestoneName,
        DueDate, CompletedDate, StatusCode, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Eng1Id, 'Kickoff & Data Collection',           DATEADD(MONTH, -10, GETDATE()), DATEADD(MONTH, -10, GETDATE()), 'Completed', DATEADD(MONTH, -11, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Coverage Analysis & Recommendations', DATEADD(MONTH,  -8, GETDATE()), DATEADD(MONTH,  -8, GETDATE()), 'Completed', DATEADD(MONTH, -11, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Carrier Submission & Negotiation',    DATEADD(MONTH,  -3, GETDATE()), DATEADD(MONTH,  -3, GETDATE()), 'Completed', DATEADD(MONTH, -11, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Policy Binding & Documentation',      DATEADD(DAY,    15, GETDATE()), NULL,                           'InProgress',DATEADD(MONTH, -11, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, 'Requirements Gathering',              DATEADD(MONTH,  -8, GETDATE()), DATEADD(MONTH,  -8, GETDATE()), 'Completed', DATEADD(MONTH,  -9, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, 'Carrier Selection',                   DATEADD(MONTH,  -6, GETDATE()), DATEADD(MONTH,  -6, GETDATE()), 'Completed', DATEADD(MONTH,  -9, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, 'Benefits Plan Configuration',         DATEADD(MONTH,  -2, GETDATE()), NULL,                           'InProgress',DATEADD(MONTH,  -9, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng3Id, 'Initial Risk Assessment',             DATEADD(MONTH,  -6, GETDATE()), DATEADD(MONTH,  -6, GETDATE()), 'Completed', DATEADD(MONTH,  -7, SYSUTCDATETIME()), @UserUserId),
        (NEWID(), @TenantId, @Eng3Id, 'Policy Proposal Review',              DATEADD(DAY,    30, GETDATE()), NULL,                           'Pending',   DATEADD(MONTH,  -7, SYSUTCDATETIME()), @UserUserId);
END

-- ============================================================
-- OPS.ENGAGEMENTTASK
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM OPS.EngagementTask WHERE EngagementId = @Eng1Id)
BEGIN
    INSERT INTO OPS.EngagementTask (
        TaskId, TenantId, EngagementId, TaskTitle,
        AssignedToUserId, DueDate, CompletedDate, StatusCode, Priority,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Eng1Id, 'Collect expiring policy documents',     @UserUserId,    DATEADD(DAY, -10, GETDATE()), DATEADD(DAY, -12, GETDATE()), 'Completed', 'High',   DATEADD(MONTH, -1, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Review loss runs for last 5 years',     @ManagerUserId, DATEADD(DAY,  -5, GETDATE()), DATEADD(DAY,  -5, GETDATE()), 'Completed', 'High',   DATEADD(MONTH, -1, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Prepare renewal submission packet',     @ManagerUserId, DATEADD(DAY,   5, GETDATE()), NULL,                         'InProgress','High',   DATEADD(DAY,  -3, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, 'Obtain 3 carrier quotes',               @SalesUserId,   DATEADD(DAY,  10, GETDATE()), NULL,                         'Open',      'Medium', DATEADD(DAY,  -3, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, 'Conduct employee census review',        @UserUserId,    DATEADD(DAY,  -7, GETDATE()), DATEADD(DAY,  -7, GETDATE()), 'Completed', 'High',   DATEADD(MONTH, -2, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, 'Compare 5 benefits plan options',       @SalesUserId,   DATEADD(DAY,  14, GETDATE()), NULL,                         'Open',      'Medium', DATEADD(MONTH, -2, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng3Id, 'Gather payroll records for audit',      @UserUserId,    DATEADD(DAY,  20, GETDATE()), NULL,                         'Open',      'Medium', DATEADD(MONTH, -3, SYSUTCDATETIME()), @UserUserId),
        (NEWID(), @TenantId, @Eng3Id, 'Schedule site safety inspection',       @ManagerUserId, DATEADD(DAY,  30, GETDATE()), NULL,                         'Open',      'Low',    DATEADD(MONTH, -3, SYSUTCDATETIME()), @UserUserId);
END

-- ============================================================
-- OPS.SERVICEREQUEST
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO OPS.ServiceRequest (
        ServiceRequestId, TenantId, AccountId, AgreementId, EngagementId,
        RequestNumber, RequestTypeCode, Subject, Description,
        PriorityCode, AssignedToUserId, StatusCode,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Account1Id, @Agree1Id, @Eng1Id, 'SR-0001', 'EndorsementRequest', 'Add new vehicle to commercial auto fleet',     'Client acquired 3 new delivery vans and needs them added to the existing fleet policy.',  'High',   @ManagerUserId, 'Open',       DATEADD(DAY, -4, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Account2Id, @Agree2Id, @Eng2Id, 'SR-0002', 'COIRequest',         'Certificate of Insurance for new vendor',      'Vendor contract requires COI naming ABC Vendor Services as additional insured.',          'Medium', @UserUserId,    'InProgress', DATEADD(DAY, -7, SYSUTCDATETIME()), @UserUserId),
        (NEWID(), @TenantId, @Account3Id, @Agree3Id, @Eng3Id, 'SR-0003', 'ClaimAssistance',    'Workplace injury claim - employee #1042',      'Employee reported slip and fall in warehouse on 3rd. EMT report attached.',              'High',   @ManagerUserId, 'Open',       DATEADD(DAY, -2, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Account4Id, NULL,      NULL,    'SR-0004', 'PolicyQuestion',     'Coverage question on flood exclusion clause',  'Client needs clarification on paragraph 14(b) of the property policy.',                 'Low',    @UserUserId,    'Resolved',   DATEADD(DAY,-15, SYSUTCDATETIME()), @UserUserId);
END

-- ============================================================
-- FINANCE.INVOICE
-- ============================================================
DECLARE @Inv1Id UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000001';
DECLARE @Inv2Id UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000002';
DECLARE @Inv3Id UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000003';
DECLARE @Inv4Id UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000004';

IF NOT EXISTS (SELECT 1 FROM Finance.Invoice WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Finance.Invoice (
        InvoiceId, TenantId, InvoiceNumber, AccountId, AgreementId,
        TotalAmount, BalanceAmount, InvoiceDate, DueDate, StatusCodeId,
        CreatedDateUtc, CreatedByUserId
    ) VALUES
        (@Inv1Id, @TenantId, 'INV-0001', @Account1Id, @Agree1Id, 10416.67,     0.00, DATEADD(MONTH, -10, GETDATE()), DATEADD(MONTH,  -9, GETDATE()), 3, DATEADD(MONTH, -10, SYSUTCDATETIME()), @FinanceUserId),
        (@Inv2Id, @TenantId, 'INV-0002', @Account1Id, @Agree1Id, 10416.67,     0.00, DATEADD(MONTH,  -9, GETDATE()), DATEADD(MONTH,  -8, GETDATE()), 3, DATEADD(MONTH,  -9, SYSUTCDATETIME()), @FinanceUserId),
        (@Inv3Id, @TenantId, 'INV-0003', @Account2Id, @Agree2Id,  7291.67,     0.00, DATEADD(MONTH,  -8, GETDATE()), DATEADD(MONTH,  -7, GETDATE()), 3, DATEADD(MONTH,  -8, SYSUTCDATETIME()), @FinanceUserId),
        (@Inv4Id, @TenantId, 'INV-0004', @Account3Id, @Agree3Id,  3500.00,  3500.00, DATEADD(MONTH,  -1, GETDATE()), DATEADD(DAY,    15, GETDATE()), 1, DATEADD(MONTH,  -1, SYSUTCDATETIME()), @FinanceUserId),
        (NEWID(), @TenantId, 'INV-0005', @Account4Id, NULL,       5666.67,  5666.67, DATEADD(DAY,   -10, GETDATE()), DATEADD(DAY,    20, GETDATE()), 1, DATEADD(DAY,   -10, SYSUTCDATETIME()), @FinanceUserId),
        (NEWID(), @TenantId, 'INV-0006', @Account1Id, @Agree1Id, 10416.67, 10416.67, DATEADD(DAY,    -5, GETDATE()), DATEADD(DAY,    25, GETDATE()), 1, DATEADD(DAY,    -5, SYSUTCDATETIME()), @FinanceUserId);
END

-- ============================================================
-- BILLING.PAYMENT
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Billing.Payment WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Billing.Payment (
        PaymentId, TenantId, AccountId, InvoiceId,
        PaymentDate, Amount, PaymentMethodCode, ReferenceNumber,
        StatusCode, Notes, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Account1Id, @Inv1Id, DATEADD(MONTH,  -9, GETDATE()), 10416.67, 'ACH',  'ACH-20240115-001', 'Applied', NULL, DATEADD(MONTH, -9, SYSUTCDATETIME()), @FinanceUserId),
        (NEWID(), @TenantId, @Account1Id, @Inv2Id, DATEADD(MONTH,  -8, GETDATE()), 10416.67, 'ACH',  'ACH-20240215-001', 'Applied', NULL, DATEADD(MONTH, -8, SYSUTCDATETIME()), @FinanceUserId),
        (NEWID(), @TenantId, @Account2Id, @Inv3Id, DATEADD(MONTH,  -7, GETDATE()),  7291.67, 'Check','CHK-00442',        'Applied', NULL, DATEADD(MONTH, -7, SYSUTCDATETIME()), @FinanceUserId);
END

-- ============================================================
-- BILLING.TIMEENTRY
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Billing.TimeEntry WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Billing.TimeEntry (
        TimeEntryId, TenantId, EngagementId, AccountId, UserId,
        EntryDate, Hours, BillableHours, RateAmount, Description,
        StatusCode, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Eng1Id, @Account1Id, @ManagerUserId, DATEADD(DAY, -10, GETDATE()), 2.5, 2.5, 225.00, 'Renewal strategy planning call',          'Approved', DATEADD(DAY, -10, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, @Account1Id, @ManagerUserId, DATEADD(DAY,  -8, GETDATE()), 3.0, 3.0, 225.00, 'Loss run analysis and summary report',     'Approved', DATEADD(DAY,  -8, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, @Account1Id, @UserUserId,    DATEADD(DAY,  -7, GETDATE()), 1.5, 1.5, 125.00, 'Policy document collection and indexing',  'Approved', DATEADD(DAY,  -7, SYSUTCDATETIME()), @UserUserId),
        (NEWID(), @TenantId, @Eng2Id, @Account2Id, @ManagerUserId, DATEADD(DAY,  -6, GETDATE()), 2.0, 2.0, 225.00, 'Benefits carrier comparison presentation', 'Approved', DATEADD(DAY,  -6, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, @Account2Id, @SalesUserId,   DATEADD(DAY,  -5, GETDATE()), 1.0, 1.0, 175.00, 'Employee census data verification',        'Approved', DATEADD(DAY,  -5, SYSUTCDATETIME()), @SalesUserId),
        (NEWID(), @TenantId, @Eng3Id, @Account3Id, @UserUserId,    DATEADD(DAY,  -4, GETDATE()), 2.0, 1.5, 125.00, 'Workplace safety checklist review',        'Draft',    DATEADD(DAY,  -4, SYSUTCDATETIME()), @UserUserId),
        (NEWID(), @TenantId, @Eng3Id, @Account3Id, @ManagerUserId, DATEADD(DAY,  -2, GETDATE()), 1.5, 1.5, 225.00, 'Workers comp mod factor analysis',         'Draft',    DATEADD(DAY,  -2, SYSUTCDATETIME()), @ManagerUserId);
END

-- ============================================================
-- BILLING.EXPENSEENTRY
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Billing.ExpenseEntry WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Billing.ExpenseEntry (
        ExpenseId, TenantId, EngagementId, AccountId, UserId,
        ExpenseDate, CategoryCode, Amount, Description,
        IsBillable, StatusCode, CreatedDateUtc, CreatedByUserId
    ) VALUES
        (NEWID(), @TenantId, @Eng1Id, @Account1Id, @ManagerUserId, DATEADD(DAY, -9, GETDATE()), 'Travel',   125.40, 'Roundtrip mileage to client site (180 mi @ $0.67)',  1, 'Approved', DATEADD(DAY, -9, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng1Id, @Account1Id, @ManagerUserId, DATEADD(DAY, -8, GETDATE()), 'Meals',     48.75, 'Working lunch with client stakeholders',             1, 'Approved', DATEADD(DAY, -8, SYSUTCDATETIME()), @ManagerUserId),
        (NEWID(), @TenantId, @Eng2Id, @Account2Id, @SalesUserId,   DATEADD(DAY, -5, GETDATE()), 'Travel',    84.20, 'Uber to client office for kickoff meeting',         1, 'Approved', DATEADD(DAY, -5, SYSUTCDATETIME()), @SalesUserId),
        (NEWID(), @TenantId, @Eng3Id, @Account3Id, @UserUserId,    DATEADD(DAY, -4, GETDATE()), 'Printing',  22.50, 'Policy document printing and binding (x3 sets)',    1, 'Draft',    DATEADD(DAY, -4, SYSUTCDATETIME()), @UserUserId);
END

PRINT 'Core, CRM, Client, OPS and Billing seed data inserted successfully!';
