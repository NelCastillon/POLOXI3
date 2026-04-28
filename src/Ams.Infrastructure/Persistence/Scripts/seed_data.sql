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

-- =============================================================================
-- 8. CRM QUOTES
-- =============================================================================
DECLARE @QuoteId1 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000001';
DECLARE @QuoteId2 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000002';
DECLARE @QuoteId3 UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId1)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId1, @TenantId, 'Q-2024-001', @OppId1, @AccId1, 85000.00, DATEADD(DAY, 30, @Now), 'Presented', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId2)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId2, @TenantId, 'Q-2024-002', @OppId2, @AccId2, 42500.00, DATEADD(DAY, 45, @Now), 'Draft', 0, @Now);

IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE QuoteId = @QuoteId3)
    INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES (@QuoteId3, @TenantId, 'Q-2024-003', @OppId5, @AccId2, 27000.00, DATEADD(DAY, -10, @Now), 'Accepted', 0, DATEADD(DAY, -30, @Now));

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
IF NOT EXISTS (SELECT 1 FROM Billing.Payment WHERE TenantId = @TenantId AND ReferenceNumber = 'PAY-2024-001')
    INSERT INTO Billing.Payment
        (PaymentId, TenantId, AccountId, PaymentNumber, PaymentDate, PaymentMethodCode,
         CurrencyCode, TotalAmount, Amount, ReferenceNumber, PaymentStatusCodeId, IsDeleted, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, @AccId2, 'PAY-2024-001', DATEADD(DAY, -5, @Now), 'ACH',
         'USD', 27000.00, 27000.00, 'PAY-2024-001', 2, 0, DATEADD(DAY, -5, @Now));

IF NOT EXISTS (SELECT 1 FROM Billing.Payment WHERE TenantId = @TenantId AND ReferenceNumber = 'PAY-2024-002')
    INSERT INTO Billing.Payment
        (PaymentId, TenantId, AccountId, PaymentNumber, PaymentDate, PaymentMethodCode,
         CurrencyCode, TotalAmount, Amount, ReferenceNumber, PaymentStatusCodeId, IsDeleted, CreatedDateUtc)
    VALUES
        (NEWID(), @TenantId, @AccId3, 'PAY-2024-002', DATEADD(DAY, -15, @Now), 'Check',
         'USD', 22500.00, 22500.00, 'PAY-2024-002', 2, 0, DATEADD(DAY, -15, @Now));

-- =============================================================================
PRINT 'Seed data applied successfully.';
GO
