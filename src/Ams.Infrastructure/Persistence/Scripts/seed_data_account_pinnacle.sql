-- =============================================================================
-- Enhanced Seed Data for Account 20000000-0000-0000-0000-000000000004
-- Pinnacle Brokers Co. - Enterprise Demo Account
-- =============================================================================

SET NOCOUNT ON;

DECLARE @TenantId  UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId    UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @AccId4    UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000004';
DECLARE @Now       DATETIME2        = GETUTCDATE();

-- =============================================================================
-- UPDATE ACCOUNT WITH FULL ENTERPRISE DATA
-- =============================================================================
UPDATE Client.Account
SET 
	Street = '123 Insurance Plaza',
	City = 'Chicago',
	[State] = 'IL',
	Zip = '60601',
	Country = 'USA',
	Employees = 42,
	TaxId = '36-1234567',
	NaicsCode = '524210',
	SegmentCode = 'Enterprise',
	Website = 'https://pinnaclebrokers.com'
WHERE AccountId = @AccId4;

-- =============================================================================
-- CONTACTS FOR PINNACLE BROKERS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'sarah.mitchell@pinnaclebrokers.com')
	INSERT INTO Client.Contact
		(ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
		 JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Sarah', 'Mitchell', 'sarah.mitchell@pinnaclebrokers.com', '+1 312 555 0301',
		 'CEO & President', 'Primary', 0, 1, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'mike.johnson@pinnaclebrokers.com')
	INSERT INTO Client.Contact
		(ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
		 JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Michael', 'Johnson', 'mike.johnson@pinnaclebrokers.com', '+1 312 555 0302',
		 'CFO', 'Financial', 1, 1, @UserId, 0, @Now);

IF NOT EXISTS (SELECT 1 FROM Client.Contact WHERE TenantId = @TenantId AND Email = 'emily.chen@pinnaclebrokers.com')
	INSERT INTO Client.Contact
		(ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone,
		 JobTitle, ContactTypeCode, IsBillingContact, StatusCodeId, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Emily', 'Chen', 'emily.chen@pinnaclebrokers.com', '+1 312 555 0303',
		 'VP of Operations', 'Operations', 0, 1, @UserId, 0, @Now);

-- =============================================================================
-- POLICIES FOR PINNACLE BROKERS (3 Active Policies)
-- =============================================================================
DECLARE @PolicyId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @PolicyId2 UNIQUEIDENTIFIER = NEWID();
DECLARE @PolicyId3 UNIQUEIDENTIFIER = NEWID();
DECLARE @CarrierId1 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001'; -- Hartford
DECLARE @CarrierId2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002'; -- Travelers
DECLARE @CarrierId3 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003'; -- Chubb

-- BOP Policy
IF NOT EXISTS (SELECT 1 FROM [Policy].Policy WHERE PolicyId = @PolicyId1)
	INSERT INTO [Policy].Policy
		(PolicyId, TenantId, AccountId, CarrierId, PolicyNumber, LineOfBusiness,
		 Premium, EffectiveDate, ExpirationDate, StatusCode,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@PolicyId1, @TenantId, @AccId4, @CarrierId1, 'POL-PBC-001-BOP',
		 'Business Owners Policy', 18500.00,
		 DATEADD(YEAR, -1, @Now), DATEADD(MONTH, 11, @Now), 'Active',
		 @UserId, 0, DATEADD(YEAR, -1, @Now));

-- Professional Liability
IF NOT EXISTS (SELECT 1 FROM [Policy].Policy WHERE PolicyId = @PolicyId2)
	INSERT INTO [Policy].Policy
		(PolicyId, TenantId, AccountId, CarrierId, PolicyNumber, LineOfBusiness,
		 Premium, EffectiveDate, ExpirationDate, StatusCode,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@PolicyId2, @TenantId, @AccId4, @CarrierId2, 'POL-PBC-002-EO',
		 'Errors & Omissions', 12400.00,
		 DATEADD(MONTH, -8, @Now), DATEADD(MONTH, 4, @Now), 'Active',
		 @UserId, 0, DATEADD(MONTH, -8, @Now));

-- Cyber Liability
IF NOT EXISTS (SELECT 1 FROM [Policy].Policy WHERE PolicyId = @PolicyId3)
	INSERT INTO [Policy].Policy
		(PolicyId, TenantId, AccountId, CarrierId, PolicyNumber, LineOfBusiness,
		 Premium, EffectiveDate, ExpirationDate, StatusCode,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@PolicyId3, @TenantId, @AccId4, @CarrierId3, 'POL-PBC-003-CYB',
		 'Cyber Liability', 8200.00,
		 DATEADD(MONTH, -4, @Now), DATEADD(MONTH, 8, @Now), 'Active',
		 @UserId, 0, DATEADD(MONTH, -4, @Now));

-- =============================================================================
-- SUBMISSIONS FOR PINNACLE BROKERS
-- =============================================================================
DECLARE @SubId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @SubId2 UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT 1 FROM CRM.Submission WHERE SubmissionId = @SubId1)
	INSERT INTO CRM.Submission
		(SubmissionId, TenantId, AccountId, CarrierId, SubmissionNumber,
		 LineOfBusiness, StatusCode, SubmittedAtUtc, DueDateUtc, Notes,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@SubId1, @TenantId, @AccId4, @CarrierId1, 'SUB-PBC-2025-001',
		 'Workers Compensation', 'Quoted', DATEADD(DAY, -15, @Now), DATEADD(DAY, 15, @Now),
		 'WC renewal quote for 42 employees, clean loss history',
		 @UserId, 0, DATEADD(DAY, -15, @Now));

IF NOT EXISTS (SELECT 1 FROM CRM.Submission WHERE SubmissionId = @SubId2)
	INSERT INTO CRM.Submission
		(SubmissionId, TenantId, AccountId, CarrierId, SubmissionNumber,
		 LineOfBusiness, StatusCode, SubmittedAtUtc, DueDateUtc, Notes,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@SubId2, @TenantId, @AccId4, @CarrierId3, 'SUB-PBC-2025-002',
		 'Directors & Officers', 'Submitted', DATEADD(DAY, -8, @Now), DATEADD(DAY, 7, @Now),
		 'D&O coverage for expanding board - seeking $5M limit',
		 @UserId, 0, DATEADD(DAY, -8, @Now));

-- =============================================================================
-- CLAIMS FOR PINNACLE BROKERS (1 Open Claim)
-- =============================================================================
DECLARE @ClaimId1 UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT 1 FROM Claims.Claim WHERE ClaimId = @ClaimId1)
	INSERT INTO Claims.Claim
		(ClaimId, TenantId, AccountId, PolicyId, ClaimNumber,
		 LineOfBusiness, LossDate, StatusCode, ReserveAmount, PaidAmount,
		 Adjuster, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@ClaimId1, @TenantId, @AccId4, @PolicyId1, 'CLM-PBC-2024-001',
		 'Business Owners Policy', DATEADD(DAY, -45, @Now), 'Open',
		 15000.00, 0.00, 'Jane Smith - Hartford Claims',
		 @UserId, 0, DATEADD(DAY, -45, @Now));

-- =============================================================================
-- ACTIVITIES FOR PINNACLE BROKERS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Q4 Business Review')
	INSERT INTO Client.AccountActivity
		(ActivityId, TenantId, AccountId, ActivityType, Subject, Notes,
		 OccurredAtUtc, DurationMinutes, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Meeting', 'Q4 Business Review',
		 'Reviewed renewal strategy, discussed expansion plans, identified cyber liability gap.',
		 DATEADD(DAY, -12, @Now), 60, @UserId, 0, DATEADD(DAY, -12, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Follow-up on WC Quote')
	INSERT INTO Client.AccountActivity
		(ActivityId, TenantId, AccountId, ActivityType, Subject, Notes,
		 OccurredAtUtc, DurationMinutes, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Call', 'Follow-up on WC Quote',
		 'Discussed Hartford WC quote, client reviewing with CFO, expects decision by end of week.',
		 DATEADD(DAY, -6, @Now), 15, @UserId, 0, DATEADD(DAY, -6, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'D&O Submission Sent')
	INSERT INTO Client.AccountActivity
		(ActivityId, TenantId, AccountId, ActivityType, Subject, Notes,
		 OccurredAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Email', 'D&O Submission Sent',
		 'Sent completed D&O application to Chubb for board expansion coverage.',
		 DATEADD(DAY, -8, @Now), @UserId, 0, DATEADD(DAY, -8, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Claim Notification')
	INSERT INTO Client.AccountActivity
		(ActivityId, TenantId, AccountId, ActivityType, Subject, Notes,
		 OccurredAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Note', 'Claim Notification',
		 'Client reported water damage to server room. Filed claim with Hartford, adjuster assigned.',
		 DATEADD(DAY, -45, @Now), @UserId, 0, DATEADD(DAY, -45, @Now));

-- =============================================================================
-- COMMUNICATIONS FOR PINNACLE BROKERS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountCommunication WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Renewal Reminder - BOP Policy')
	INSERT INTO Client.AccountCommunication
		(CommunicationId, TenantId, AccountId, Channel, Direction, Subject,
		 MessagePreview, SentAtUtc, WasOpened, OpenedAtUtc,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Email', 'Outbound', 'Renewal Reminder - BOP Policy',
		 'Your Business Owners Policy is up for renewal in 60 days. Let''s schedule a review...',
		 DATEADD(DAY, -25, @Now), 1, DATEADD(DAY, -24, @Now),
		 @UserId, 0, DATEADD(DAY, -25, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountCommunication WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Monthly Insurance Newsletter')
	INSERT INTO Client.AccountCommunication
		(CommunicationId, TenantId, AccountId, Channel, Direction, Subject,
		 MessagePreview, SentAtUtc, WasOpened, WasClicked, OpenedAtUtc, ClickedAtUtc,
		 CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Email', 'Outbound', 'Monthly Insurance Newsletter',
		 'February 2025 Insurance Insights: Cyber Threats, Workers Comp Trends...',
		 DATEADD(DAY, -18, @Now), 1, 1, DATEADD(DAY, -17, @Now), DATEADD(DAY, -17, @Now),
		 @UserId, 0, DATEADD(DAY, -18, @Now));

-- =============================================================================
-- MARKETING CAMPAIGNS FOR PINNACLE BROKERS
-- =============================================================================
DECLARE @CampaignId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @CampaignId2 UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT 1 FROM Marketing.CampaignEnrollment WHERE TenantId = @TenantId AND AccountId = @AccId4 AND CampaignName = 'Renewal Outreach 2025')
	INSERT INTO Marketing.CampaignEnrollment
		(EnrollmentId, TenantId, AccountId, CampaignId, CampaignName,
		 StatusCode, EnrolledAtUtc, EmailsSent, EmailsOpened, EmailsClicked,
		 LastContactUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, @CampaignId1, 'Renewal Outreach 2025',
		 'Active', DATEADD(DAY, -30, @Now), 3, 2, 1,
		 DATEADD(DAY, -6, @Now), @UserId, 0, DATEADD(DAY, -30, @Now));

IF NOT EXISTS (SELECT 1 FROM Marketing.CampaignEnrollment WHERE TenantId = @TenantId AND AccountId = @AccId4 AND CampaignName = 'Cross-Sell Cyber')
	INSERT INTO Marketing.CampaignEnrollment
		(EnrollmentId, TenantId, AccountId, CampaignId, CampaignName,
		 StatusCode, EnrolledAtUtc, EmailsSent, EmailsOpened, EmailsClicked,
		 LastContactUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, @CampaignId2, 'Cross-Sell Cyber',
		 'Active', DATEADD(DAY, -15, @Now), 2, 2, 0,
		 DATEADD(DAY, -8, @Now), @UserId, 0, DATEADD(DAY, -15, @Now));

-- =============================================================================
-- ACCOUNT RELATIONSHIPS FOR PINNACLE BROKERS
-- =============================================================================
DECLARE @RelAccId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @RelAccId2 UNIQUEIDENTIFIER = NEWID();

-- Create related account for parent company
IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @RelAccId1)
	INSERT INTO Client.Account
		(AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
		 StatusCodeId, LifecycleStageCode, Industry, AnnualRevenue,
		 AccountOwnerUserId, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@RelAccId1, @TenantId, 'PBH-PARENT', 'Pinnacle Brokers Holdings LLC', 'CLIENT',
		 'ir@pinnaclehold.com', '+1 312 555 0400', 1, 'Customer',
		 'Insurance', 12500000.00, @UserId, @UserId, 0, @Now);

-- Create relationship
IF NOT EXISTS (SELECT 1 FROM Client.AccountRelationship WHERE TenantId = @TenantId AND AccountId = @AccId4 AND RelatedAccountId = @RelAccId1)
	INSERT INTO Client.AccountRelationship
		(RelationshipId, TenantId, AccountId, RelatedAccountId, RelationshipType,
		 Description, IsActive, StartedAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, @RelAccId1, 'Parent',
		 'Pinnacle Brokers Holdings is the parent company.', 1,
		 DATEADD(YEAR, -2, @Now), @UserId, 0, @Now);

-- Create subsidiary
IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @RelAccId2)
	INSERT INTO Client.Account
		(AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone,
		 StatusCodeId, LifecycleStageCode, Industry, AnnualRevenue,
		 AccountOwnerUserId, ParentAccountId, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(@RelAccId2, @TenantId, 'PBC-SUB-001', 'Pinnacle Risk Solutions', 'CLIENT',
		 'info@pinnaclerisk.com', '+1 312 555 0500', 1, 'Customer',
		 'Insurance', 850000.00, @UserId, @AccId4, @UserId, 0, @Now);

-- Create relationship for subsidiary
IF NOT EXISTS (SELECT 1 FROM Client.AccountRelationship WHERE TenantId = @TenantId AND AccountId = @AccId4 AND RelatedAccountId = @RelAccId2)
	INSERT INTO Client.AccountRelationship
		(RelationshipId, TenantId, AccountId, RelatedAccountId, RelationshipType,
		 Description, IsActive, StartedAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, @RelAccId2, 'Subsidiary',
		 'Pinnacle Risk Solutions is a wholly-owned subsidiary.', 1,
		 DATEADD(YEAR, -1, @Now), @UserId, 0, @Now);

-- =============================================================================
-- ACCOUNT NOTES
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountNote WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Title = 'Strategic Account Priority')
	INSERT INTO Client.AccountNote
		(NoteId, TenantId, AccountId, Title, NoteText, NoteCategory,
		 IsPinned, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(), @TenantId, @AccId4, 'Strategic Account Priority',
		 'Pinnacle Brokers is a strategic account with strong growth potential. CEO Sarah Mitchell has expressed interest in expanding cyber coverage and exploring employee benefits for their growing team. Maintain monthly touch points and prioritize service excellence.',
		 'General', 1, @UserId, 0, DATEADD(DAY, -60, @Now));

-- =============================================================================
PRINT 'Enhanced seed data for Pinnacle Brokers Co. (Account 20000000-0000-0000-0000-000000000004) applied successfully.';
GO
