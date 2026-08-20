-- =============================================================================
-- AMS  ·  Development Seed Data Script
-- =============================================================================
-- Run this script against your local AMS database AFTER the application has
-- started at least once (so DatabaseMigrator has created all schemas/tables).
--
-- All IDs are fixed GUIDs so the script is safe to run multiple times
-- (every INSERT is guarded by IF NOT EXISTS).
--
-- Dev defaults
--   TenantId  = 00000000-0000-0000-0000-000000000001  (Demo Agency)
--   UserId    = 00000000-0000-0000-0000-000000000002  (Alex Johnson – admin)
-- =============================================================================

SET NOCOUNT ON;

DECLARE @TenantId  UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId    UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now       DATETIME2        = GETUTCDATE();

-- =============================================================================
-- 1. AGENCY PROFILE
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Agency.AgencyProfile WHERE TenantId = @TenantId)
    INSERT INTO Agency.AgencyProfile
        (AgencyProfileId, TenantId, DbaName, Npn, Fein, EntityType, LicenseNumber,
         DomicileState, Phone, Email, Website,
         AddressLine1, City, StateProvince, PostalCode, CountryCode,
         EoCarrier, EoPolicyNumber, EoCoverageLimit, EoExpiryDate, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, 'Demo Agency LLC', '1234567', '98-7654321', 'LLC', 'LIC-NY-0042',
         'NY', '+1 212 555 0100', 'info@demoagency.com', 'https://demoagency.com',
         '1 Central Park West', 'New York', 'NY', '10023', 'US',
         'Great American Insurance', 'EO-2024-99001', 2000000.00, '2025-12-31', @Now);

-- =============================================================================
-- 2. CARRIERS
-- =============================================================================
DECLARE @CarrierId1 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @CarrierId2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @CarrierId3 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';
DECLARE @CarrierId4 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000004';
DECLARE @CarrierId5 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000005';

IF NOT EXISTS (SELECT 1 FROM Agency.Carrier WHERE CarrierId = @CarrierId1)
    INSERT INTO Agency.Carrier (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc)
    VALUES (@CarrierId1, @TenantId, 'Hartford Financial Services', '19682', 'A+', 1, '2020-01-15', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.Carrier WHERE CarrierId = @CarrierId2)
    INSERT INTO Agency.Carrier (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc)
    VALUES (@CarrierId2, @TenantId, 'Travelers Companies', '25658', 'A++', 1, '2019-06-01', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.Carrier WHERE CarrierId = @CarrierId3)
    INSERT INTO Agency.Carrier (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc)
    VALUES (@CarrierId3, @TenantId, 'Chubb Limited', '20281', 'A++', 1, '2018-03-10', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.Carrier WHERE CarrierId = @CarrierId4)
    INSERT INTO Agency.Carrier (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc)
    VALUES (@CarrierId4, @TenantId, 'Markel Corporation', '38970', 'A', 0, '2021-09-20', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.Carrier WHERE CarrierId = @CarrierId5)
    INSERT INTO Agency.Carrier (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc)
    VALUES (@CarrierId5, @TenantId, 'Berkshire Hathaway Specialty', '22276', 'A++', 1, '2022-01-01', 1, @Now);

-- =============================================================================
-- 3. LINES OF BUSINESS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'COMM-PC')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'COMM-PC', 'Commercial Property & Casualty', 'Commercial', 'BOP, GL, Property, Auto', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'EMP-BEN')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'EMP-BEN', 'Employee Benefits', 'Group', 'Health, Dental, Vision, Life', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'PROF-LI')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'PROF-LI', 'Professional Liability', 'Specialty', 'E&O, D&O, Cyber', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'WORK-CO')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'WORK-CO', 'Workers Compensation', 'Commercial', 'WC, Employers Liability', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'LIFE-IN')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'LIFE-IN', 'Life Insurance', 'Personal', 'Term, Whole, Universal Life', 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Agency.LineOfBusiness WHERE TenantId = @TenantId AND LobCode = 'CYBER')
    INSERT INTO Agency.LineOfBusiness (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, 'CYBER', 'Cyber Liability', 'Specialty', 'Data Breach, Ransomware, EPLI', 1, @Now);

-- =============================================================================
-- 4. CLIENT ACCOUNTS
-- StatusCodeId: 1=Active, 2=Inactive, 3=Prospect
-- =============================================================================
DECLARE @AccId1 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';
DECLARE @AccId2 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000002';
DECLARE @AccId3 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000003';
DECLARE @AccId4 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000004';
DECLARE @AccId5 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000005';
DECLARE @AccId6 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000006';

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId1)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, Website, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId1, @TenantId, 'ACME-001', 'ACME Corporation', 'CLIENT',
         'contact@acmecorp.com', '+1 312 555 0110', 1, 'Customer',
         'Manufacturing', 'https://acmecorp.com', 18500000.00, @UserId, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId2)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, Website, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId2, @TenantId, 'XYZ-002', 'XYZ Industries', 'CLIENT',
         'ops@xyzindustries.com', '+1 404 555 0123', 1, 'Customer',
         'Logistics', 'https://xyzindustries.com', 9200000.00, @UserId, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId3)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, Website, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId3, @TenantId, 'ABC-003', 'ABC Financial Group', 'CLIENT',
         'risk@abcfinancial.com', '+1 617 555 0199', 1, 'Customer',
         'Financial Services', 'https://abcfinancial.com', 54000000.00, @UserId, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId4)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId4, @TenantId, 'PBC-004', 'Pinnacle Brokers Co.', 'PROSPECT',
         'admin@pinnaclebrokers.com', '+1 312 555 0200', 3, 'Prospect',
         'Insurance', 3100000.00, @UserId, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId5)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId5, @TenantId, 'SUM-005', 'Summit Insurance Group', 'PROSPECT',
         'info@summitins.com', '+1 713 555 0182', 3, 'Prospect',
         'Insurance', 7800000.00, @UserId, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccId6)
    INSERT INTO Client.Account
        (AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
         StatusCodeId, LifecycleStageCode, Industry, AnnualRevenue,
         AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@AccId6, @TenantId, 'CST-006', 'Coastal Risk Management', 'PROSPECT',
         'team@coastalrisk.com', '+1 305 555 0221', 3, 'Lead',
         'Real Estate', 2400000.00, @UserId, @UserId, 0, @Now);

-- =============================================================================
-- 5. CONTACTS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'james.brady@acmecorp.com')
    INSERT INTO Client.Contact
        (ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
         JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, @AccId1, 'James', 'Brady', 'james.brady@acmecorp.com', '+1 312 555 0111',
         'VP of Risk Management', 'Primary', 1, 1, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'lisa.chen@xyzindustries.com')
    INSERT INTO Client.Contact
        (ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
         JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, @AccId2, 'Lisa', 'Chen', 'lisa.chen@xyzindustries.com', '+1 404 555 0124',
         'CFO', 'Primary', 1, 1, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'robert.ward@abcfinancial.com')
    INSERT INTO Client.Contact
        (ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
         JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, @AccId3, 'Robert', 'Ward', 'robert.ward@abcfinancial.com', '+1 617 555 0200',
         'General Counsel', 'Primary', 0, 1, @UserId, 0, @Now);

-- =============================================================================
-- 6. CRM LEADS
-- =============================================================================
DECLARE @LeadId1 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000001';
DECLARE @LeadId2 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000002';
DECLARE @LeadId3 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000003';
DECLARE @LeadId4 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000004';
DECLARE @LeadId5 UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000005';

-- StatusCodeId: 1=New, 2=Contacted, 3=Qualified, 4=Converted, 5=Disqualified
IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadId = @LeadId1)
    INSERT INTO CRM.Lead
        (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
         InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode,
         StatusCodeId, AssignedToUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@LeadId1, @TenantId, 'LDR-0001', 'Pinnacle Brokers Co.',
         'Sarah', 'Mitchell', 'sarah.mitchell@pinnaclebrokers.com', '+1 312 555 0300',
         'Commercial P&C', 88, 'High', 'Referral', 'Proposal',
         2, @UserId, @UserId, 0, DATEADD(DAY, -14, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadId = @LeadId2)
    INSERT INTO CRM.Lead
        (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
         InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode,
         StatusCodeId, AssignedToUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@LeadId2, @TenantId, 'LDR-0002', 'Summit Insurance Group',
         'James', 'Harrington', 'james.h@summitins.com', '+1 404 555 0301',
         'Employee Benefits', 72, 'High', 'Web', 'Contacted',
         2, @UserId, @UserId, 0, DATEADD(DAY, -10, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadId = @LeadId3)
    INSERT INTO CRM.Lead
        (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
         InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode,
         StatusCodeId, AssignedToUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@LeadId3, @TenantId, 'LDR-0003', 'BlueSky Partners',
         'Linda', 'Torres', 'ltorres@bluesky.co', '+1 713 555 0302',
         'Life Insurance', 64, 'Normal', 'LinkedIn', 'Nurturing',
         1, @UserId, @UserId, 0, DATEADD(DAY, -7, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadId = @LeadId4)
    INSERT INTO CRM.Lead
        (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
         InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode,
         StatusCodeId, AssignedToUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@LeadId4, @TenantId, 'LDR-0004', 'Meridian Capital',
         'Robert', 'Chen', 'rchen@meridiancap.com', '+1 617 555 0303',
         'Professional Liability', 55, 'Normal', 'Conference', 'New',
         1, @UserId, @UserId, 0, DATEADD(DAY, -5, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadId = @LeadId5)
    INSERT INTO CRM.Lead
        (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
         InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode,
         StatusCodeId, AssignedToUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@LeadId5, @TenantId, 'LDR-0005', 'Coastal Risk Management',
         'Amy', 'Nguyen', 'anguyen@coastalrisk.com', '+1 305 555 0304',
         'Workers Compensation', 41, 'Low', 'Cold Call', 'New',
         1, @UserId, @UserId, 0, DATEADD(DAY, -2, @Now));

-- =============================================================================
-- 6b. CRM OPPORTUNITY STAGES (required reference data)
-- =============================================================================
DECLARE @StageProspect    UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000001';
DECLARE @StageQualify     UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000002';
DECLARE @StageProposal    UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000003';
DECLARE @StageNegotiate   UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000004';
DECLARE @StageClosedWon   UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000005';
DECLARE @StageClosedLost  UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000006';

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageProspect)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageProspect,   @TenantId, 'PROSPECT',   'Prospect',    1, 10, 0, 0, 1);

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageQualify)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageQualify,    @TenantId, 'QUALIFY',    'Qualify',     2, 25, 0, 0, 1);

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageProposal)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageProposal,   @TenantId, 'PROPOSAL',   'Proposal',    3, 50, 0, 0, 1);

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageNegotiate)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageNegotiate,  @TenantId, 'NEGOTIATE',  'Negotiation', 4, 75, 0, 0, 1);

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageClosedWon)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageClosedWon,  @TenantId, 'CLOSED_WON',  'Closed Won',  5, 100, 1, 1, 1);

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageClosedLost)
    INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
    VALUES (@StageClosedLost, @TenantId, 'CLOSED_LOST', 'Closed Lost', 6, 0,   1, 0, 1);

-- =============================================================================
-- 7. CRM OPPORTUNITIES
-- =============================================================================
DECLARE @OppId1 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
DECLARE @OppId2 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002';
DECLARE @OppId3 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000003';
DECLARE @OppId4 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000004';
DECLARE @OppId5 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000005';
DECLARE @OppId6 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000006';
DECLARE @OppId7 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000007';
DECLARE @OppId8 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000008';
DECLARE @OppEnterpriseId UNIQUEIDENTIFIER = 'c2000000-0000-0000-0000-000000000004';

-- StatusCodeId: 1=Open, 2=Negotiation, 3=Won, 4=Lost
IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId1)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId1, @TenantId, 'OPP-0001', @AccId1, 'ACME Corp – Commercial Package Renewal',
         85000.00, 75, 'BestCase', DATEADD(DAY, 15, @Now),
         @StageNegotiate, 1, @UserId, NULL, @UserId, 0, DATEADD(DAY, -30, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId2)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId2, @TenantId, 'OPP-0002', @AccId2, 'XYZ Industries – Employee Benefits Bundle',
         42500.00, 60, 'Pipeline', DATEADD(DAY, 30, @Now),
         @StageProposal, 1, @UserId, NULL, @UserId, 0, DATEADD(DAY, -22, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId3)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId3, @TenantId, 'OPP-0003', @AccId3, 'ABC Financial – D&O Policy Upgrade',
         31000.00, 50, 'Pipeline', DATEADD(DAY, 45, @Now),
         @StageProposal, 1, @UserId, NULL, @UserId, 0, DATEADD(DAY, -18, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId4)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId4, @TenantId, 'OPP-0004', @AccId1, 'ACME Corp – Cyber Liability Add-on',
         18750.00, 85, 'Commit', DATEADD(DAY, 7, @Now),
         @StageNegotiate, 1, @UserId, NULL, @UserId, 0, DATEADD(DAY, -10, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId5)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId5, @TenantId, 'OPP-0005', @AccId2, 'XYZ Industries – Workers Comp Renewal',
         27000.00, 100, 'ClosedWon', DATEADD(DAY, -5, @Now),
         @StageClosedWon, 3, @UserId, NULL, @UserId, 0, DATEADD(DAY, -60, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId6)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId6, @TenantId, 'OPP-0006', @AccId3, 'ABC Financial – General Liability New',
         22500.00, 100, 'ClosedWon', DATEADD(DAY, -15, @Now),
         @StageClosedWon, 3, @UserId, NULL, @UserId, 0, DATEADD(DAY, -45, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId7)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId7, @TenantId, 'OPP-0007', @AccId4, 'Pinnacle – BOP New Business',
         15200.00, 40, 'Pipeline', DATEADD(DAY, 60, @Now),
         @StageQualify, 1, @UserId, @LeadId1, @UserId, 0, DATEADD(DAY, -8, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppId8)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StatusCodeId, OwnerUserId, LeadId, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppId8, @TenantId, 'OPP-0008', @AccId5, 'Summit – E&O Coverage Expansion',
         9800.00, 25, 'Pipeline', DATEADD(DAY, 90, @Now),
         @StageProspect, 1, @UserId, @LeadId2, @UserId, 0, DATEADD(DAY, -3, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OppEnterpriseId)
    INSERT INTO CRM.Opportunity
        (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
         EstimatedAmount, WinProbability, ForecastCategoryCode, CloseDate,
         OpportunityStageId, StageName, StatusCodeId, OwnerUserId, LeadId, Description, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@OppEnterpriseId, @TenantId, 'ENT-OPP-1004', @AccId6, 'Global manufacturing risk program',
         425000.00, 68, 'Best Case', DATEADD(DAY, 38, @Now),
         @StageProposal, 'Proposal', 1, @UserId, NULL,
         'Enterprise opportunity seeded for the polished CRM opportunity dashboard and workflow sync.', @UserId, 0, DATEADD(DAY, -18, @Now));

IF OBJECT_ID(N'CRM.OpportunityLine', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityId = @OppEnterpriseId AND IsDeleted = 0)
BEGIN
    INSERT INTO CRM.OpportunityLine (OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @OppEnterpriseId, 'Workers Comp', 'Travelers Companies', 185000.00, 'High', DATEADD(DAY, -6, @Now), @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Commercial Auto', 'Chubb Limited', 142000.00, 'High', DATEADD(DAY, -6, @Now), @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Umbrella / Excess', 'Berkshire Hathaway Specialty', 98000.00, 'Medium', DATEADD(DAY, -6, @Now), @UserId, 0);
END

IF OBJECT_ID(N'CRM.OpportunityActivity', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityActivity WHERE OpportunityId = @OppEnterpriseId AND IsDeleted = 0)
BEGIN
    INSERT INTO CRM.OpportunityActivity (ActivityId, TenantId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @OppEnterpriseId, 'Meeting', 'Executive risk review completed', 'Confirmed workers comp, auto, and excess strategy with finance and operations stakeholders.', DATEADD(DAY, -5, @Now), DATEADD(DAY, -5, @Now), @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Email', 'Carrier submission package distributed', 'Sent updated loss runs, schedules, and target premiums to selected markets.', DATEADD(DAY, -3, @Now), DATEADD(DAY, -3, @Now), @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Call', 'Pricing checkpoint scheduled', 'Scheduled pricing checkpoint before proposal presentation.', DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), @UserId, 0);
END

IF OBJECT_ID(N'CRM.OpportunitySubmission', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunitySubmission WHERE OpportunityId = @OppEnterpriseId AND IsDeleted = 0)
    INSERT INTO CRM.OpportunitySubmission (SubmissionId, TenantId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES ('c3000000-0000-0000-0000-000000000004', @TenantId, @OppEnterpriseId, 'SUB-ENT-1004', 'Workers Comp', 'In Review', 185000.00, DATEADD(DAY, -4, @Now), @UserId, 0);

IF OBJECT_ID(N'CRM.OpportunityCompetitor', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityCompetitor WHERE OpportunityId = @OppEnterpriseId AND IsDeleted = 0)
BEGIN
    INSERT INTO CRM.OpportunityCompetitor (CompetitorId, TenantId, OpportunityId, Name, Strength, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @OppEnterpriseId, 'National Broker Inc.', 'Strong', DATEADD(DAY, -7, @Now), @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Regional Risk Partners', 'Moderate', DATEADD(DAY, -6, @Now), @UserId, 0);
END

IF OBJECT_ID(N'CRM.OpportunityWorkflowEvent', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityWorkflowEvent WHERE OpportunityId = @OppEnterpriseId AND IsDeleted = 0)
BEGIN
    INSERT INTO CRM.OpportunityWorkflowEvent (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @OppEnterpriseId, 'Seed', 'Enterprise opportunity synchronized', 'Opportunity, account, submission, quote, and workflow seed data synchronized for the enterprise detail page.', 'Opportunity', @OppEnterpriseId, DATEADD(DAY, -6, @Now), @Now, @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Submission', 'Submission synced to underwriting', 'Opportunity submission synchronized to the enterprise submissions workflow.', 'Submission', 'c3000000-0000-0000-0000-000000000004', DATEADD(DAY, -4, @Now), @Now, @UserId, 0),
        (NEWID(), @TenantId, @OppEnterpriseId, 'Quote', 'Quote presented', 'Presented quote synchronized back to the opportunity workflow timeline.', 'Quote', 'c5000000-0000-0000-0000-000000000004', DATEADD(DAY, -1, @Now), @Now, @UserId, 0);
END

-- =============================================================================
-- 8. CRM QUOTES
-- =============================================================================
DECLARE @QuoteId1 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000001';
DECLARE @QuoteId2 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000002';
DECLARE @QuoteId3 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000003';
DECLARE @QuoteEnterpriseId UNIQUEIDENTIFIER = 'c5000000-0000-0000-0000-000000000004';

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId1)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId1, @TenantId, 'Q-2024-001', @OppId1, @AccId1, 85000.00, DATEADD(DAY, 30, @Now), 'Presented', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId2)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId2, @TenantId, 'Q-2024-002', @OppId2, @AccId2, 42500.00, DATEADD(DAY, 45, @Now), 'Draft', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId3)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId3, @TenantId, 'Q-2024-003', @OppId5, @AccId2, 27000.00, DATEADD(DAY, -10, @Now), 'Accepted', 0, DATEADD(DAY, -30, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteEnterpriseId)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc, CreatedByUserId)
    VALUES (@QuoteEnterpriseId, @TenantId, 'Q-ENT-1004', @OppEnterpriseId, @AccId6, 181750.00, DATEADD(DAY, 21, @Now), 'Presented', 0, DATEADD(DAY, -1, @Now), @UserId);

-- Quote Lines
IF NOT EXISTS (SELECT 1 FROM CRM.QuoteLine WHERE QuoteId = @QuoteId1 AND LineOrder = 1)
BEGIN
    INSERT INTO CRM.QuoteLine (QuoteLineId, TenantId, QuoteId, LineOrder, ItemCode, Description, Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @QuoteId1, 1, 'COMM-PC-GL', 'General Liability – $2M Limit', 1, 48000.00, 5.00, 0, 45600.00, @Now);
    INSERT INTO CRM.QuoteLine (QuoteLineId, TenantId, QuoteId, LineOrder, ItemCode, Description, Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @QuoteId1, 2, 'COMM-PC-PROP', 'Commercial Property – Building & Contents', 1, 32000.00, 5.00, 0, 30400.00, @Now);
    INSERT INTO CRM.QuoteLine (QuoteLineId, TenantId, QuoteId, LineOrder, ItemCode, Description, Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @QuoteId1, 3, 'COMM-PC-AUTO', 'Commercial Auto – Fleet (12 vehicles)', 12, 750.00, 0, 0, 9000.00, @Now);
END

-- =============================================================================
-- 9. CRM LEAD ACTIVITIES
-- =============================================================================
-- Open tasks (IsCompleted = 0)
IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Follow-up call – Pinnacle Brokers')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId1, @OppId7, 'Call', 'Follow-up call – Pinnacle Brokers', 'Discuss BOP coverage options and pricing.', DATEADD(DAY, 1, @Now), 30, 0, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Send proposal – ACME Cyber Add-on')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId4, 'Email', 'Send proposal – ACME Cyber Add-on', 'Attach updated quote and coverage comparison doc.', DATEADD(DAY, 1, @Now), 20, 0, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Prepare carrier comparison – XYZ Benefits')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId2, 'Task', 'Prepare carrier comparison – XYZ Benefits', 'Build 3-carrier comparison spreadsheet for Lisa Chen.', DATEADD(DAY, 2, @Now), 90, 0, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Call ABC Ltd – D&O underwriting review')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId3, @OppId3, 'Call', 'Call ABC Ltd – D&O underwriting review', 'Confirm underwriting requirements with Robert Ward.', DATEADD(DAY, 3, @Now), 30, 0, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Review pricing – Summit E&O Expansion')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId2, @OppId8, 'Task', 'Review pricing – Summit E&O Expansion', 'Compare Markel, Chubb, Hartford markets for best premium.', DATEADD(DAY, 4, @Now), 60, 0, @UserId, 0, @Now);

-- Completed activities (recent feed, IsCompleted = 1)
IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Email sent – ACME renewal binder package')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId1, 'Email', 'Email sent – ACME renewal binder package', 'Sent binder, invoice Q-2024-001, and coverage summary to James Brady.', DATEADD(DAY, -1, @Now), 15, 'Sent', 1, @UserId, 0, DATEADD(DAY, -1, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Kick-off meeting – XYZ Benefits renewal')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId2, 'Meeting', 'Kick-off meeting – XYZ Benefits renewal', 'Reviewed plan options; client wants 3 carrier quotes. Next: compare Cigna, Aetna, BlueCross.', DATEADD(DAY, -2, @Now), 60, 'Positive', 1, @UserId, 0, DATEADD(DAY, -2, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Quote created – ABC D&O Q-2024-003')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId3, @OppId3, 'Task', 'Quote created – ABC D&O Q-2024-003', 'Generated quote #Q-2024-003 for $31,000. Awaiting client approval.', DATEADD(DAY, -3, @Now), 45, 'Completed', 1, @UserId, 0, DATEADD(DAY, -3, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Discovery call – Pinnacle Brokers BOP')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId1, @OppId7, 'Call', 'Discovery call – Pinnacle Brokers BOP', 'Sarah Mitchell confirmed budget. Lead scored 88. Ready for proposal stage.', DATEADD(DAY, -4, @Now), 25, 'Positive', 1, @UserId, 0, DATEADD(DAY, -4, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Policy bound – XYZ Workers Comp Renewal')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId5, 'Task', 'Policy bound – XYZ Workers Comp Renewal', 'Hartford bound WC policy effective 12/1. Commission processed.', DATEADD(DAY, -5, @Now), 10, 'Won', 1, @UserId, 0, DATEADD(DAY, -5, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'GL policy bound – ABC Financial')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId6, 'Task', 'GL policy bound – ABC Financial', 'Travelers GL policy issued. Premium $22,500. Client satisfied.', DATEADD(DAY, -15, @Now), 10, 'Won', 1, @UserId, 0, DATEADD(DAY, -15, @Now));

-- Producer Workbench contact history seeded for Log Contact enterprise workflow
IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Producer contact – Pinnacle discovery touchpoint')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId1, @OppId7, 'Call', 'Producer contact – Pinnacle discovery touchpoint', 'Logged from Producer Workbench. Confirmed incumbent renewal timing, decision committee, and BOP target premium range.', DATEADD(HOUR, -6, @Now), 18, 'Contacted', 1, @UserId, 0, DATEADD(HOUR, -6, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Producer contact – Summit benefits follow-up')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @LeadId2, @OppId8, 'Email', 'Producer contact – Summit benefits follow-up', 'Logged from Producer Workbench. Sent benefits benchmark packet and requested current census for E&O expansion underwriting.', DATEADD(HOUR, -3, @Now), 12, 'Sent', 1, @UserId, 0, DATEADD(HOUR, -3, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = 'Producer contact – ACME cyber pricing checkpoint')
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode, IsCompleted, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, NULL, @OppId4, 'Meeting', 'Producer contact – ACME cyber pricing checkpoint', 'Logged from Producer Workbench. Reviewed cyber liability retention options and agreed to final carrier comparison before proposal.', DATEADD(HOUR, -2, @Now), 30, 'Positive', 1, @UserId, 0, DATEADD(HOUR, -2, @Now));

-- =============================================================================
-- 10. CRM FORECAST ENTRIES
-- =============================================================================
DECLARE @Period NVARCHAR(10) = CONCAT(YEAR(@Now), '-Q', DATEPART(QUARTER, @Now));
DECLARE @NextQ  NVARCHAR(10) = CONCAT(YEAR(DATEADD(MONTH, 3, @Now)), '-Q', DATEPART(QUARTER, DATEADD(MONTH, 3, @Now)));

IF NOT EXISTS (SELECT 1 FROM CRM.ForecastEntry WHERE TenantId = @TenantId AND OpportunityId = @OppId1)
    INSERT INTO CRM.ForecastEntry (ForecastEntryId, TenantId, OpportunityId, OwnerUserId, ForecastPeriod, ForecastAmount, PipelineAmount, CategoryCode, CloseDate, WinProbability, Notes, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @OppId1, @UserId, @Period, 63750.00, 85000.00, 'BestCase', DATEADD(DAY, 15, @Now), 75, 'Renewal – strong relationship.', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.ForecastEntry WHERE TenantId = @TenantId AND OpportunityId = @OppId4)
    INSERT INTO CRM.ForecastEntry (ForecastEntryId, TenantId, OpportunityId, OwnerUserId, ForecastPeriod, ForecastAmount, PipelineAmount, CategoryCode, CloseDate, WinProbability, Notes, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @OppId4, @UserId, @Period, 15937.50, 18750.00, 'Commit', DATEADD(DAY, 7, @Now), 85, 'Cyber add-on – almost closed.', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.ForecastEntry WHERE TenantId = @TenantId AND OpportunityId = @OppId2)
    INSERT INTO CRM.ForecastEntry (ForecastEntryId, TenantId, OpportunityId, OwnerUserId, ForecastPeriod, ForecastAmount, PipelineAmount, CategoryCode, CloseDate, WinProbability, Notes, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @OppId2, @UserId, @NextQ, 25500.00, 42500.00, 'Pipeline', DATEADD(DAY, 30, @Now), 60, 'Benefits renewal pending carrier quotes.', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.ForecastEntry WHERE TenantId = @TenantId AND OpportunityId = @OppId3)
    INSERT INTO CRM.ForecastEntry (ForecastEntryId, TenantId, OpportunityId, OwnerUserId, ForecastPeriod, ForecastAmount, PipelineAmount, CategoryCode, CloseDate, WinProbability, Notes, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @OppId3, @UserId, @NextQ, 15500.00, 31000.00, 'Pipeline', DATEADD(DAY, 45, @Now), 50, 'D&O upgrade – underwriting in progress.', 0, @Now);

-- =============================================================================
-- 11. OPS – ENGAGEMENTS
-- EngagementTypeId: 1=Renewal, 2=Service, 3=Consulting
-- StatusCodeId:     1=Active, 2=Completed, 3=OnHold
-- =============================================================================
DECLARE @EngId1 UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000001';
DECLARE @EngId2 UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000002';
DECLARE @EngId3 UNIQUEIDENTIFIER = '60000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM OPS.Engagement WHERE EngagementId = @EngId1)
    INSERT INTO OPS.Engagement
        (EngagementId, TenantId, EngagementNumber, AccountId, EngagementName,
         EngagementTypeId, StatusCodeId, EngagementManagerUserId, StartDate, EndDate,
         CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@EngId1, @TenantId, 'ENG-0001', @AccId1, 'ACME Commercial Package – Annual Service',
         1, 1, @UserId, DATEADD(MONTH, -6, @Now), DATEADD(MONTH, 6, @Now),
         @UserId, 0, DATEADD(MONTH, -6, @Now));

IF NOT EXISTS (SELECT 1 FROM OPS.Engagement WHERE EngagementId = @EngId2)
    INSERT INTO OPS.Engagement
        (EngagementId, TenantId, EngagementNumber, AccountId, EngagementName,
         EngagementTypeId, StatusCodeId, EngagementManagerUserId, StartDate, EndDate,
         CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@EngId2, @TenantId, 'ENG-0002', @AccId2, 'XYZ Industries – Benefits Plan Administration',
         2, 1, @UserId, DATEADD(MONTH, -3, @Now), DATEADD(MONTH, 9, @Now),
         @UserId, 0, DATEADD(MONTH, -3, @Now));

IF NOT EXISTS (SELECT 1 FROM OPS.Engagement WHERE EngagementId = @EngId3)
    INSERT INTO OPS.Engagement
        (EngagementId, TenantId, EngagementNumber, AccountId, EngagementName,
         EngagementTypeId, StatusCodeId, EngagementManagerUserId, StartDate, EndDate,
         CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES
        (@EngId3, @TenantId, 'ENG-0003', @AccId3, 'ABC Financial – Risk Consulting Retainer',
         3, 1, @UserId, DATEADD(MONTH, -1, @Now), DATEADD(MONTH, 11, @Now),
         @UserId, 0, DATEADD(MONTH, -1, @Now));

-- =============================================================================
-- 12. OPS – SERVICE REQUESTS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = 'SR-0001')
    INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @AccId1, @EngId1, 'SR-0001', 'CertificateOfInsurance', 'COI request – ACME vendor requirement', 'Need COI for new vendor contract with Accenture. GL + WC required.', 'High', @UserId, 'Open', @UserId, 0, DATEADD(DAY, -3, @Now));

IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = 'SR-0002')
    INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @AccId2, @EngId2, 'SR-0002', 'PolicyChange', 'Add new employee to benefits plan – XYZ', 'New hire: Marcus Lee, start date 01/15. Add to medical, dental, vision.', 'Normal', @UserId, 'Open', @UserId, 0, DATEADD(DAY, -1, @Now));

IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = 'SR-0003')
    INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @AccId3, @EngId3, 'SR-0003', 'LossRun', 'Loss runs request – ABC Financial past 5 years', '5-year loss run required for D&O underwriting submission.', 'Normal', @UserId, 'Resolved', DATEADD(DAY, -2, @Now), @UserId, 0, DATEADD(DAY, -7, @Now));

IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = 'SR-0004')
    INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedByUserId, IsDeleted, CreatedDateUtc)
    VALUES (NEWID(), @TenantId, @AccId1, @EngId1, 'SR-0004', 'Endorsement', 'Add newly acquired warehouse – ACME Prop endorsement', 'ACME acquired 12,000 sqft warehouse in Chicago. Add to property schedule.', 'High', @UserId, 'InProgress', @UserId, 0, DATEADD(DAY, -2, @Now));

-- =============================================================================
-- 13. FINANCE – INVOICES  (schema: Billing.Invoice / Billing.Payment)
-- =============================================================================
DECLARE @InvId1 UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000001';
DECLARE @InvId2 UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000002';
DECLARE @InvId3 UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000003';
DECLARE @InvId4 UNIQUEIDENTIFIER = '70000000-0000-0000-0000-000000000004';

-- InvoiceStatusCodeId: 1=Draft, 2=Sent, 3=Paid, 4=Void, 5=Overdue
IF NOT EXISTS (SELECT 1 FROM Billing.Invoice WHERE InvoiceId = @InvId1)
    INSERT INTO Billing.Invoice
        (InvoiceId, TenantId, AccountId, InvoiceNumber, InvoiceDate, DueDate,
         InvoiceStatusCodeId, CurrencyCode, TotalAmount, BalanceAmount, IsDeleted, CreatedDateUtc)
    VALUES
        (@InvId1, @TenantId, @AccId1, 'INV-2024-001',
         DATEADD(DAY, -15, @Now), DATEADD(DAY, 15, @Now),
         2, 'USD', 85000.00, 85000.00, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Billing.Invoice WHERE InvoiceId = @InvId2)
    INSERT INTO Billing.Invoice
        (InvoiceId, TenantId, AccountId, InvoiceNumber, InvoiceDate, DueDate,
         InvoiceStatusCodeId, CurrencyCode, TotalAmount, BalanceAmount, IsDeleted, CreatedDateUtc)
    VALUES
        (@InvId2, @TenantId, @AccId2, 'INV-2024-002',
         DATEADD(DAY, -45, @Now), DATEADD(DAY, -15, @Now),
         3, 'USD', 27000.00, 0.00, 0, DATEADD(DAY, -45, @Now));

IF NOT EXISTS (SELECT 1 FROM Billing.Invoice WHERE InvoiceId = @InvId3)
    INSERT INTO Billing.Invoice
        (InvoiceId, TenantId, AccountId, InvoiceNumber, InvoiceDate, DueDate,
         InvoiceStatusCodeId, CurrencyCode, TotalAmount, BalanceAmount, IsDeleted, CreatedDateUtc)
    VALUES
        (@InvId3, @TenantId, @AccId3, 'INV-2024-003',
         DATEADD(DAY, -60, @Now), DATEADD(DAY, -30, @Now),
         3, 'USD', 22500.00, 0.00, 0, DATEADD(DAY, -60, @Now));

IF NOT EXISTS (SELECT 1 FROM Billing.Invoice WHERE InvoiceId = @InvId4)
    INSERT INTO Billing.Invoice
        (InvoiceId, TenantId, AccountId, InvoiceNumber, InvoiceDate, DueDate,
         InvoiceStatusCodeId, CurrencyCode, TotalAmount, BalanceAmount, IsDeleted, CreatedDateUtc)
    VALUES
        (@InvId4, @TenantId, @AccId2, 'INV-2024-004',
         DATEADD(DAY, -55, @Now), DATEADD(DAY, -25, @Now),
         5, 'USD', 42500.00, 42500.00, 0, DATEADD(DAY, -55, @Now));

-- Payments
-- PaymentStatusCodeId: 1=Pending, 2=Applied, 3=Voided
DECLARE @PaymentId1 UNIQUEIDENTIFIER = '71000000-0000-0000-0000-000000000001';
DECLARE @PaymentId2 UNIQUEIDENTIFIER = '71000000-0000-0000-0000-000000000002';
IF NOT EXISTS (SELECT 1 FROM Billing.Payment WHERE TenantId = @TenantId AND ReferenceNumber = 'PAY-2024-001')
    INSERT INTO Billing.Payment
        (PaymentId, TenantId, AccountId, PaymentNumber, PaymentDate, PaymentMethodCode,
         CurrencyCode, TotalAmount, Amount, ReferenceNumber, PaymentStatusCodeId, IsDeleted, CreatedDateUtc)
    VALUES
        (@PaymentId1, @TenantId, @AccId2, 'PAY-2024-001', DATEADD(DAY, -5, @Now), 'ACH',
         'USD', 27000.00, 27000.00, 'PAY-2024-001', 2, 0, DATEADD(DAY, -5, @Now));

IF NOT EXISTS (SELECT 1 FROM Billing.Payment WHERE TenantId = @TenantId AND ReferenceNumber = 'PAY-2024-002')
    INSERT INTO Billing.Payment
        (PaymentId, TenantId, AccountId, PaymentNumber, PaymentDate, PaymentMethodCode,
         CurrencyCode, TotalAmount, Amount, ReferenceNumber, PaymentStatusCodeId, IsDeleted, CreatedDateUtc)
    VALUES
        (@PaymentId2, @TenantId, @AccId3, 'PAY-2024-002', DATEADD(DAY, -15, @Now), 'Check',
         'USD', 22500.00, 22500.00, 'PAY-2024-002', 2, 0, DATEADD(DAY, -15, @Now));

-- Applied payments must use the authoritative PaymentApplication bridge.
SELECT @PaymentId1=PaymentId FROM Billing.Payment WHERE TenantId=@TenantId AND ReferenceNumber='PAY-2024-001' AND IsDeleted=0;
SELECT @PaymentId2=PaymentId FROM Billing.Payment WHERE TenantId=@TenantId AND ReferenceNumber='PAY-2024-002' AND IsDeleted=0;

IF NOT EXISTS (SELECT 1 FROM Billing.PaymentApplication WHERE PaymentId=@PaymentId1 AND InvoiceId=@InvId2)
    INSERT Billing.PaymentApplication(PaymentApplicationId,PaymentId,InvoiceId,AppliedAmount,AppliedDateUtc,CreatedByUserId)
    VALUES('72000000-0000-0000-0000-000000000001',@PaymentId1,@InvId2,27000.00,DATEADD(DAY,-5,@Now),@UserId);

IF NOT EXISTS (SELECT 1 FROM Billing.PaymentApplication WHERE PaymentId=@PaymentId2 AND InvoiceId=@InvId3)
    INSERT Billing.PaymentApplication(PaymentApplicationId,PaymentId,InvoiceId,AppliedAmount,AppliedDateUtc,CreatedByUserId)
    VALUES('72000000-0000-0000-0000-000000000002',@PaymentId2,@InvId3,22500.00,DATEADD(DAY,-15,@Now),@UserId);

-- =============================================================================
-- DOCUMENT WORKFLOW TEMPLATES
-- =============================================================================
DECLARE @WfTemplate1 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
DECLARE @WfTemplate2 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002';
DECLARE @WfTemplate3 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentWorkflowTemplate WHERE WorkflowTemplateId = @WfTemplate1)
    INSERT INTO DMS.DocumentWorkflowTemplate 
        (WorkflowTemplateId, TenantId, TemplateName, TemplateCode, Description, WorkflowType, 
         IsSequential, RequiresAllApprovals, AutoArchiveOnComplete, NotifyOnStart, NotifyOnComplete,
         TriggerOnUpload, TriggerOnCategory, TriggerOnDocType, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES 
        (@WfTemplate1, @TenantId, 'Contract Review Approval', 'CONTRACT-REVIEW', 
         'Multi-stage approval workflow for all client contracts requiring legal and management review.', 
         'Approval', 1, 1, 0, 1, 1, 0, 'Contract', NULL, 1, 1, @Now, 0);

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentWorkflowTemplate WHERE WorkflowTemplateId = @WfTemplate2)
    INSERT INTO DMS.DocumentWorkflowTemplate 
        (WorkflowTemplateId, TenantId, TemplateName, TemplateCode, Description, WorkflowType, 
         IsSequential, RequiresAllApprovals, AutoArchiveOnComplete, NotifyOnStart, NotifyOnComplete,
         TriggerOnUpload, TriggerOnCategory, TriggerOnDocType, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES 
        (@WfTemplate2, @TenantId, 'Compliance Document Approval', 'COMPLIANCE-APPROVAL', 
         'Regulatory compliance workflow for E&O policies, audit reports, and carrier appointments.', 
         'Approval', 1, 1, 1, 1, 1, 0, 'Compliance', NULL, 1, 2, @Now, 0);

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentWorkflowTemplate WHERE WorkflowTemplateId = @WfTemplate3)
    INSERT INTO DMS.DocumentWorkflowTemplate 
        (WorkflowTemplateId, TenantId, TemplateName, TemplateCode, Description, WorkflowType, 
         IsSequential, RequiresAllApprovals, AutoArchiveOnComplete, NotifyOnStart, NotifyOnComplete,
         TriggerOnUpload, TriggerOnCategory, TriggerOnDocType, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES 
        (@WfTemplate3, @TenantId, 'Policy Document Review', 'POLICY-REVIEW', 
         'Quality assurance review for policy documents, endorsements, and certificates.', 
         'Review', 0, 0, 0, 1, 1, 1, 'Policy', NULL, 1, 3, @Now, 0);

-- =============================================================================
-- DOCUMENT RETENTION POLICIES
-- =============================================================================
DECLARE @RetPolicy1 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000001';
DECLARE @RetPolicy2 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000002';
DECLARE @RetPolicy3 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentRetentionPolicy WHERE RetentionPolicyId = @RetPolicy1)
    INSERT INTO DMS.DocumentRetentionPolicy
        (RetentionPolicyId, TenantId, PolicyName, PolicyCode, Description, ApplicableCategory, 
         RetentionPeriodYears, RetentionStartTrigger, ActionOnExpiry, RequireApprovalToDelete,
         NotifyBeforeDays, NotifyRoleCode, RegulatoryBasis, IsActive, EffectiveDate, CreatedDateUtc, IsDeleted)
    VALUES
        (@RetPolicy1, @TenantId, 'Policy Documents - 7 Years', 'POLICY-7YR',
         'Standard retention for policy documents, certificates, and endorsements per state regulations.',
         'Policy', 7, 'PolicyExpiry', 'Archive', 1, 30, 'Admin',
         'Most states require 7-year retention for policy records (varies by state).', 1, '2024-01-01', @Now, 0);

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentRetentionPolicy WHERE RetentionPolicyId = @RetPolicy2)
    INSERT INTO DMS.DocumentRetentionPolicy
        (RetentionPolicyId, TenantId, PolicyName, PolicyCode, Description, ApplicableCategory, 
         RetentionPeriodYears, RetentionStartTrigger, ActionOnExpiry, RequireApprovalToDelete,
         NotifyBeforeDays, NotifyRoleCode, RegulatoryBasis, IsActive, EffectiveDate, CreatedDateUtc, IsDeleted)
    VALUES
        (@RetPolicy2, @TenantId, 'Claims Files - 10 Years', 'CLAIM-10YR',
         'Extended retention for claims documentation per carrier agreements and state law.',
         'Claim', 10, 'ClaimClosure', 'Archive', 1, 60, 'Admin',
         'Claims files must be retained 10 years from closure date per insurance department regulations.', 1, '2024-01-01', @Now, 0);

IF NOT EXISTS (SELECT 1 FROM DMS.DocumentRetentionPolicy WHERE RetentionPolicyId = @RetPolicy3)
    INSERT INTO DMS.DocumentRetentionPolicy
        (RetentionPolicyId, TenantId, PolicyName, PolicyCode, Description, ApplicableCategory, 
         RetentionPeriodYears, RetentionStartTrigger, ActionOnExpiry, RequireApprovalToDelete,
         NotifyBeforeDays, NotifyRoleCode, RegulatoryBasis, IsActive, EffectiveDate, CreatedDateUtc, IsDeleted)
    VALUES
        (@RetPolicy3, @TenantId, 'Compliance & Audit - Permanent', 'COMPLIANCE-PERM',
         'Permanent retention for E&O policies, carrier appointments, and regulatory audit documents.',
         'Compliance', 99, 'Creation', 'Review', 1, 90, 'Admin',
         'Agency compliance documents must be retained permanently for regulatory audit purposes.', 1, '2024-01-01', @Now, 0);

-- =============================================================================
-- TENANT AI FEATURE SETTINGS
-- =============================================================================
IF OBJECT_ID(N'AI.AiConfigItem', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-ACCOUNT-SUMMARY')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-ACCOUNT-SUMMARY', 'Account AI Summary', 'CRM', 'Generate governed account summaries using policy, activity, claim, billing, and relationship context.', '{"workflow":"Summaries","rollout":"General","dailyLimit":250,"approvalRequired":false,"safety":"PII redaction","workflowUrl":"/tenant/ai/summaries"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-NEXT-BEST-ACTION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-NEXT-BEST-ACTION', 'Next Best Action', 'Producer', 'Recommend producer and service actions from pipeline, renewal, claims, and engagement signals.', '{"workflow":"Next Best Action","rollout":"Pilot - Producer Team","dailyLimit":150,"approvalRequired":true,"audit":"Action rationale required","workflowUrl":"/tenant/ai/nba"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-RENEWAL-RISK')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-RENEWAL-RISK', 'Renewal Risk Scoring', 'Analytics', 'Score renewal retention risk using service volume, payment status, loss activity, and producer touchpoints.', '{"workflow":"Renewal Risk","rollout":"General","dailyLimit":100,"approvalRequired":false,"signals":"service,payment,claims,activity","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-CROSS-SELL')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-CROSS-SELL', 'Cross-sell Opportunity AI', 'CRM', 'Identify coverage gaps and recommended cross-sell opportunities from active accounts and policies.', '{"workflow":"CrossSell","rollout":"Pilot - Tenant Admin Review","dailyLimit":75,"approvalRequired":true,"safety":"No automatic outreach","workflowUrl":"/tenant/ai/cross-sell"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-SERVICE-TRIAGE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-SERVICE-TRIAGE', 'Service Request Triage', 'Service', 'Classify incoming service requests, suggest priority, and route work to the right service queue.', '{"workflow":"Service Triage","rollout":"Pilot - CSR Team","dailyLimit":200,"approvalRequired":true,"audit":"Routing changes tracked","workflowUrl":"/workbench/service"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-GUARDRAILS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-GUARDRAILS', 'Tenant AI Guardrails', 'Governance', 'Enforce tenant approval, audit, prompt safety, privacy controls, and human-in-the-loop requirements.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full","workflowUrl":"/tenant/ai/features"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-PROMPT-LIBRARY')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-PROMPT-LIBRARY', 'Prompt Template Library', 'Governance', 'Manage reusable prompts, system instructions, output format controls, and approval workflow.', '{"workflow":"Prompts","rollout":"General","dailyLimit":0,"approvalRequired":true,"audit":"Template versioning","workflowUrl":"/tenant/ai/prompts"}', 1, 70, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeatureSetting' AND Code = 'AI-USAGE-MONITORING')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeatureSetting', 'AI-USAGE-MONITORING', 'AI Usage Monitoring', 'Governance', 'Track AI usage limits, adoption, feedback, quality review, and tenant-level governance metrics.', '{"workflow":"Usage","rollout":"General","dailyLimit":0,"approvalRequired":false,"audit":"Usage analytics","workflowUrl":"/tenant/ai/usage"}', 1, 80, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-CONTEXT')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-CONTEXT', 'Account Context Summary', 'Account Context', 'Generate Tenant Admin governed account summaries from account, contact, opportunity, submission, quote, policy, activity, and service context.', '{"workflow":"AccountSummary","rollout":"General","dailyLimit":250,"approvalRequired":false,"contextSources":"account,contacts,opportunities,submissions,quotes,activities,service","safety":"PII redaction","workflowUrl":"/tenant/ai/summaries"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-CRM')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-CRM', 'CRM Relationship Summary', 'CRM', 'Summarize relationship health from lead history, opportunity pipeline, producer activity, contact roles, and recent touchpoints.', '{"workflow":"AccountSummary","rollout":"General","dailyLimit":200,"approvalRequired":false,"contextSources":"leads,opportunities,contacts,activities","explainability":"source citations required","workflowUrl":"/tenant/ai/summaries"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-POLICY')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-POLICY', 'Policy and Coverage Summary', 'Policy', 'Highlight active coverage, carrier relationships, renewal dates, coverage gaps, and quote/submission movement for account reviews.', '{"workflow":"AccountSummary","rollout":"Pilot - Tenant Admin Review","dailyLimit":150,"approvalRequired":true,"contextSources":"submissions,quotes,carriers,linesOfBusiness","audit":"coverage assumptions must cite source records","workflowUrl":"/tenant/ai/summaries"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-CLAIMS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-CLAIMS', 'Claims and Risk Summary', 'Claims', 'Summarize account risk posture from loss-run requests, claims documents, service activity, underwriting notes, and renewal risk indicators.', '{"workflow":"AccountSummary","rollout":"Pilot - Claims Review","dailyLimit":75,"approvalRequired":true,"contextSources":"lossRuns,claimsDocuments,serviceRequests,activities","safety":"sensitive claim notes require review","workflowUrl":"/tenant/ai/summaries"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-BILLING')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-BILLING', 'Billing and Payment Summary', 'Billing', 'Surface invoice status, payment behavior, overdue balance, renewal billing exposure, and financial service actions for account summaries.', '{"workflow":"AccountSummary","rollout":"General","dailyLimit":125,"approvalRequired":false,"contextSources":"invoices,payments,balance,serviceRequests","audit":"financial values must match Billing schema","workflowUrl":"/tenant/ai/summaries"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AccountSummarySetting' AND Code = 'AI-SUMMARY-GUARDRAILS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AccountSummarySetting', 'AI-SUMMARY-GUARDRAILS', 'Summary Approval Guardrails', 'Governance', 'Enforce Tenant Admin approval, source citation, audit capture, PII redaction, and human review rules before publishing sensitive summaries.', '{"workflow":"Approval Queue","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full summary generation trace","workflowUrl":"/tenant/ai/summaries"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-SIGNALS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-SIGNALS', 'Renewal Signal Weight Model', 'Signal Weights', 'Score retention risk using service volume, payment status, claims activity, producer touchpoints, open opportunities, and renewal timing.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":250,"approvalRequired":false,"signals":"service,payment,claims,activity,opportunity,renewalDate","weights":"service:25,payment:20,claims:20,activity:15,opportunity:10,renewalDate:10","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-THRESHOLDS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-THRESHOLDS', 'Risk Threshold Bands', 'Thresholds', 'Define low, moderate, high, and critical renewal-risk bands used by dashboards, account summaries, and producer workflows.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":0,"approvalRequired":true,"low":"0-39","moderate":"40-64","high":"65-84","critical":"85-100","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-ALERTS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-ALERTS', 'Renewal Alert Routing', 'Alerts', 'Route high-risk renewals to Tenant Admin review, producer follow-up, CSR service triage, and next-best-action workflows.', '{"workflow":"Threshold Alerts","rollout":"Pilot - Tenant Admin Review","dailyLimit":150,"approvalRequired":true,"routes":"tenantAdmin,producer,csr,nextBestAction","notifyBeforeDays":90,"workflowUrl":"/tenant/ai/renewal-risk"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-SERVICE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-SERVICE', 'Service Friction Signals', 'Service', 'Track unresolved service requests, high-priority endorsements, COI requests, and aging support items as renewal retention risk inputs.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":200,"approvalRequired":false,"signals":"openServiceRequests,highPriorityRequests,agedRequests,endorsements,lossRuns","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-BILLING')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-BILLING', 'Billing Risk Signals', 'Billing', 'Use overdue invoices, unpaid balances, late payment behavior, and payment method history as renewal-risk inputs.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":125,"approvalRequired":false,"signals":"overdueInvoices,balanceAmount,paymentStatus,paymentMethod","audit":"financial values must match Billing schema","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'RenewalRiskSetting' AND Code = 'AI-RENEWAL-RISK-GOVERNANCE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'RenewalRiskSetting', 'AI-RENEWAL-RISK-GOVERNANCE', 'Risk Scoring Governance', 'Governance', 'Require Tenant Admin approval, source traceability, threshold audit, and human review for high-impact renewal risk score changes.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full renewal risk scoring trace","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-PRODUCER-LEAD')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-PRODUCER-LEAD', 'Producer Lead Follow-up', 'Producer', 'Recommend next producer actions from lead status, score, priority, recent contact activity, and opportunity linkage.', '{"workflow":"NextBestAction","rollout":"General","dailyLimit":250,"approvalRequired":false,"signals":"leadScore,priority,lastContact,opportunityStage,owner","actions":"call,email,assign,convert","workflowUrl":"/tenant/ai/nba"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-SERVICE-TRIAGE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-SERVICE-TRIAGE', 'Service Request Triage Actions', 'Service', 'Recommend CSR next steps from open service requests, priority, account status, engagement, and unresolved workflow age.', '{"workflow":"Service Routing","rollout":"Pilot - CSR Team","dailyLimit":200,"approvalRequired":true,"signals":"openServiceRequests,priority,age,engagement,status","actions":"route,escalate,requestInfo,complete","workflowUrl":"/workbench/service"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-RENEWAL-RETENTION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-RENEWAL-RETENTION', 'Renewal Retention Playbook', 'Retention', 'Recommend retention actions from renewal risk, payment status, service friction, producer touchpoints, and opportunity pipeline.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":150,"approvalRequired":false,"signals":"renewalRisk,paymentStatus,serviceFriction,producerTouchpoints,opportunity","actions":"scheduleReview,logContact,createTask,escalate","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-CROSS-SELL')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-CROSS-SELL', 'Coverage Gap Cross-sell Actions', 'Cross-sell', 'Recommend governed cross-sell actions from account industry, active opportunities, quotes, submissions, lines of business, and coverage gaps.', '{"workflow":"NextBestAction","rollout":"Pilot - Tenant Admin Review","dailyLimit":75,"approvalRequired":true,"signals":"industry,opportunities,quotes,submissions,linesOfBusiness,coverageGaps","actions":"createOpportunity,prepareSummary,producerReview","workflowUrl":"/tenant/ai/nba"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-PRIORITY-SCORING')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-PRIORITY-SCORING', 'Action Priority Scoring', 'Priority', 'Prioritize recommendations using urgency, revenue impact, customer risk, service impact, and Tenant Admin workflow readiness.', '{"workflow":"NextBestAction","rollout":"General","dailyLimit":0,"approvalRequired":false,"weights":"urgency:30,revenue:25,risk:20,service:15,readiness:10","priorityBands":"low,normal,high,critical","workflowUrl":"/tenant/ai/nba"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'NextBestActionRule' AND Code = 'AI-NBA-GOVERNANCE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'NextBestActionRule', 'AI-NBA-GOVERNANCE', 'Next Best Action Guardrails', 'Governance', 'Require Tenant Admin governance, audit traceability, human review, and no automatic outreach for sensitive next-best-action recommendations.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full next best action trace","humanReview":"Sensitive recommendations","workflowUrl":"/tenant/ai/nba"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-GAP-DETECTION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-GAP-DETECTION', 'Coverage Gap Detection', 'Coverage Gaps', 'Detect cross-sell opportunities from account industry, current opportunities, submissions, quotes, carriers, and missing lines of business.', '{"workflow":"CrossSell","rollout":"General","dailyLimit":250,"approvalRequired":false,"signals":"industry,opportunities,submissions,quotes,carriers,linesOfBusiness","target":"coverageGaps","workflowUrl":"/tenant/ai/cross-sell"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-ELIGIBILITY')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-ELIGIBILITY', 'Account Eligibility Rules', 'Eligibility', 'Control eligible accounts and prospects using lifecycle stage, account status, recent activity, active policies, and revenue tier.', '{"workflow":"CrossSell","rollout":"General","dailyLimit":0,"approvalRequired":false,"eligibleStages":"Customer,Prospect","excludeStatuses":"Inactive,Disqualified","signals":"lifecycleStage,status,activity,policy,revenue","workflowUrl":"/tenant/ai/cross-sell"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-SUPPRESSION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-SUPPRESSION', 'Suppression and Cooldown Controls', 'Suppression', 'Prevent duplicate or inappropriate recommendations using recent outreach, open opportunities, declined quotes, and tenant suppression windows.', '{"workflow":"CrossSell","rollout":"General","dailyLimit":0,"approvalRequired":true,"cooldownDays":45,"suppressWhen":"openOpportunity,declinedQuote,recentOutreach,doNotMarket","workflowUrl":"/tenant/ai/cross-sell"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-TARGET-LOB')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-TARGET-LOB', 'Target Line of Business Model', 'Target LOB', 'Prioritize target lines of business from industry appetite, account size, existing coverage, quote history, and producer specialization.', '{"workflow":"CrossSell","rollout":"Pilot - Producer Team","dailyLimit":125,"approvalRequired":true,"targets":"Cyber,Workers Comp,Professional Liability,Commercial Auto,Umbrella","ranking":"appetite,accountSize,coverageGap,quoteHistory,producerSpecialty","workflowUrl":"/tenant/ai/cross-sell"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-OPPORTUNITY-SYNC')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-OPPORTUNITY-SYNC', 'Marketing Opportunity Sync', 'Opportunity Sync', 'Sync approved cross-sell recommendations into marketing and CRM workflows for producer review and opportunity creation.', '{"workflow":"Marketing Cross-Sell","rollout":"Pilot - Tenant Admin Review","dailyLimit":100,"approvalRequired":true,"syncTargets":"marketingCrossSell,crmOpportunity,nextBestAction","workflowUrl":"/marketing/cross-sell"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'CrossSellAiSetting' AND Code = 'AI-CROSS-SELL-GOVERNANCE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'CrossSellAiSetting', 'AI-CROSS-SELL-GOVERNANCE', 'Cross-Sell AI Guardrails', 'Governance', 'Require Tenant Admin approval, source traceability, no automatic outreach, and audit controls for cross-sell recommendations.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"No automatic outreach","audit":"Full cross-sell trace","humanReview":"Required before producer action","workflowUrl":"/tenant/ai/cross-sell"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-SYSTEM-GUARDRAILS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-SYSTEM-GUARDRAILS', 'Tenant System Guardrail Prompt', 'System Instruction', 'Reusable Tenant Admin system instruction template that enforces tenant scope, privacy, source citation, and human review rules.', '{"workflow":"PromptTemplate","rollout":"General","dailyLimit":0,"approvalRequired":true,"templateType":"system","safety":"Required","audit":"Template versioning","workflowUrl":"/tenant/ai/prompts"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-ACCOUNT-SUMMARY')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-ACCOUNT-SUMMARY', 'Account Summary Prompt', 'Account Summary', 'Prompt template for account summaries using account, contacts, opportunities, submissions, quotes, service, billing, and activity context.', '{"workflow":"AccountSummary","rollout":"General","dailyLimit":250,"approvalRequired":false,"contextSources":"account,contacts,opportunities,submissions,quotes,activities,service,billing","outputFormat":"executiveSummary","workflowUrl":"/tenant/ai/summaries"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-RENEWAL-RISK')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-RENEWAL-RISK', 'Renewal Risk Explanation Prompt', 'Renewal Risk', 'Prompt template that explains renewal risk scores with service, payment, claims, producer touchpoint, opportunity, and renewal timing evidence.', '{"workflow":"RenewalRisk","rollout":"General","dailyLimit":150,"approvalRequired":false,"signals":"service,payment,claims,activity,opportunity,renewalDate","outputFormat":"riskNarrative","workflowUrl":"/tenant/ai/renewal-risk"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-NBA-RATIONALE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-NBA-RATIONALE', 'Next Best Action Rationale Prompt', 'Next Best Action', 'Prompt template for producing explainable next-best-action recommendations for producer, service, renewal, and cross-sell workflows.', '{"workflow":"NextBestAction","rollout":"Pilot - Producer Team","dailyLimit":150,"approvalRequired":true,"outputFormat":"actionRationale","audit":"Action rationale required","workflowUrl":"/tenant/ai/nba"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-CROSS-SELL')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-CROSS-SELL', 'Cross-sell Recommendation Prompt', 'Cross-sell', 'Prompt template for identifying coverage gaps and preparing governed cross-sell recommendations for Tenant Admin and producer review.', '{"workflow":"CrossSell","rollout":"Pilot - Tenant Admin Review","dailyLimit":75,"approvalRequired":true,"safety":"No automatic outreach","outputFormat":"coverageGapRecommendation","workflowUrl":"/tenant/ai/cross-sell"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'PromptTemplate' AND Code = 'AI-PROMPT-OUTPUT-STANDARD')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'PromptTemplate', 'AI-PROMPT-OUTPUT-STANDARD', 'Enterprise Output Format Prompt', 'Output Format', 'Prompt template that standardizes concise executive output, source citations, action labels, risk flags, and Tenant Admin approval notes.', '{"workflow":"PromptTemplate","rollout":"General","dailyLimit":0,"approvalRequired":true,"templateType":"outputFormat","audit":"Output format versioning","workflowUrl":"/tenant/ai/prompts"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-DAILY-LIMITS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-DAILY-LIMITS', 'Tenant Daily Usage Limits', 'Limits', 'Set tenant-wide daily AI usage limits for summaries, prompts, renewal risk scoring, next-best-action recommendations, and cross-sell workflows.', '{"workflow":"Usage","rollout":"General","dailyLimit":500,"approvalRequired":false,"limits":"summaries:250,prompts:300,renewalRisk:150,nextBestAction:150,crossSell:75","workflowUrl":"/tenant/ai/usage"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-MONITORING-THRESHOLDS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-MONITORING-THRESHOLDS', 'Usage Monitoring Thresholds', 'Monitoring', 'Monitor tenant AI activity, adoption, limit utilization, quality-review queue volume, and workflow readiness metrics.', '{"workflow":"Usage Monitoring","rollout":"General","dailyLimit":0,"approvalRequired":false,"thresholds":"warning:75,critical:90,blocked:100","signals":"requests,tokens,workflow,qualityReview,feedback","workflowUrl":"/tenant/ai/usage"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-ALERT-ROUTING')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-ALERT-ROUTING', 'Usage Alert Routing', 'Alerts', 'Route high usage, quality review backlog, failed workflow sync, and governance exceptions to Tenant Admin review.', '{"workflow":"Usage Monitoring","rollout":"Pilot - Tenant Admin Review","dailyLimit":0,"approvalRequired":true,"routes":"tenantAdmin,governance,qualityReview","alerts":"limitWarning,limitCritical,workflowFailure,reviewBacklog","workflowUrl":"/tenant/ai/usage"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-RETENTION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-RETENTION', 'Usage Audit Retention', 'Retention', 'Control AI usage audit retention windows for prompt execution metadata, feedback, approvals, and tenant governance evidence.', '{"workflow":"Usage","rollout":"General","dailyLimit":0,"approvalRequired":true,"retentionDays":365,"audit":"promptMetadata,workflowEvents,approvals,feedback","workflowUrl":"/tenant/ai/usage"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-COST-CONTROL')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-COST-CONTROL', 'AI Cost Control Model', 'Cost Control', 'Track high-volume workflows, tenant budget guardrails, daily request caps, and administrative review triggers.', '{"workflow":"Usage Monitoring","rollout":"General","dailyLimit":0,"approvalRequired":false,"budgetGuardrail":"enabled","signals":"workflowVolume,requestCaps,reviewTriggers","workflowUrl":"/tenant/ai/usage"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiUsageSetting' AND Code = 'AI-USAGE-GOVERNANCE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiUsageSetting', 'AI-USAGE-GOVERNANCE', 'Usage Governance Controls', 'Governance', 'Require Tenant Admin audit review, traceability, quality review, and exception handling for AI usage governance.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full usage trace","humanReview":"Usage exceptions","workflowUrl":"/tenant/ai/usage"}', 1, 60, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-CAPTURE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-CAPTURE', 'AI Feedback Capture', 'Feedback Capture', 'Capture tenant user ratings, correction notes, low-confidence flags, and workflow-specific feedback for AI outputs.', '{"workflow":"Feedback","rollout":"General","dailyLimit":500,"approvalRequired":false,"capture":"rating,comment,correction,workflow,confidence","workflowUrl":"/tenant/ai/feedback"}', 1, 10, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-QUALITY-REVIEW')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-QUALITY-REVIEW', 'Quality Review Queue', 'Quality Review', 'Route negative feedback, low-confidence responses, and user corrections into a Tenant Admin quality review queue.', '{"workflow":"Feedback Review","rollout":"General","dailyLimit":150,"approvalRequired":true,"queue":"tenantAdminQualityReview","signals":"negativeRating,lowConfidence,userCorrection,missingCitation","workflowUrl":"/tenant/ai/feedback"}', 1, 20, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-ESCALATION')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-ESCALATION', 'Feedback Escalation Routing', 'Escalation', 'Escalate sensitive, repeated, or high-impact AI feedback to Tenant Admin governance and workflow owners.', '{"workflow":"Feedback Review","rollout":"Pilot - Tenant Admin Review","dailyLimit":100,"approvalRequired":true,"routes":"tenantAdmin,governance,workflowOwner","triggers":"sensitive,repeated,highImpact,compliance","workflowUrl":"/tenant/ai/feedback"}', 1, 30, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-IMPROVEMENT-BACKLOG')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-IMPROVEMENT-BACKLOG', 'Prompt Improvement Backlog', 'Improvement Backlog', 'Convert approved feedback into prompt template improvements, feature tuning, and workflow backlog items.', '{"workflow":"Feedback","rollout":"Pilot - Tenant Admin Review","dailyLimit":75,"approvalRequired":true,"syncTargets":"promptLibrary,aiFeatures,usageMonitoring","workflowUrl":"/tenant/ai/feedback"}', 1, 40, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-DRIFT-SIGNALS')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-DRIFT-SIGNALS', 'Model Drift Feedback Signals', 'Model Drift', 'Track feedback trends that suggest outdated prompts, weak context sources, incorrect citations, or degraded recommendation quality.', '{"workflow":"Feedback Review","rollout":"General","dailyLimit":0,"approvalRequired":false,"signals":"trend,incorrectCitation,staleContext,irrelevantRecommendation,lowSatisfaction","workflowUrl":"/tenant/ai/feedback"}', 1, 50, 0, @Now);

    IF NOT EXISTS (SELECT 1 FROM AI.AiConfigItem WHERE TenantId = @TenantId AND Kind = 'AiFeedbackSetting' AND Code = 'AI-FEEDBACK-GOVERNANCE')
        INSERT INTO AI.AiConfigItem (AiConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, IsDeleted, CreatedDateUtc)
        VALUES (NEWID(), @TenantId, 'AiFeedbackSetting', 'AI-FEEDBACK-GOVERNANCE', 'Feedback Governance Controls', 'Governance', 'Require Tenant Admin traceability, audit review, privacy controls, and human review for AI feedback workflows.', '{"workflow":"Governance","rollout":"General","dailyLimit":0,"approvalRequired":true,"safety":"Required","audit":"Full feedback trace","humanReview":"Sensitive feedback","workflowUrl":"/tenant/ai/feedback"}', 1, 60, 0, @Now);
END

-- =============================================================================
PRINT 'Seed data applied successfully.';
GO

