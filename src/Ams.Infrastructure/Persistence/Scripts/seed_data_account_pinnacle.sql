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
	SegmentCode = 'Enterprise',
	Website = 'https://pinnaclebrokers.com',
	WebsiteUrl = 'https://pinnaclebrokers.com',
	CountryCode = 'US',
	IsVip = 1,
	DbaName = 'Pinnacle Brokers Co.',
	ModifiedDateUtc = @Now,
	ModifiedByUserId = @UserId
WHERE TenantId=@TenantId AND AccountId = @AccId4;

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
DECLARE @PolicyId1 UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000001';
DECLARE @PolicyId2 UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000002';
DECLARE @PolicyId3 UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000003';
DECLARE @CarrierId1 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001'; -- Hartford
DECLARE @CarrierId2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002'; -- Travelers
DECLARE @CarrierId3 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003'; -- Chubb

-- BOP Policy
IF NOT EXISTS (SELECT 1 FROM Submissions.BoundPolicy WHERE PolicyId = @PolicyId1)
	INSERT INTO Submissions.BoundPolicy
		(PolicyId,TenantId,AccountId,CarrierId,PolicyNumber,Status,AnnualPremium,EffectiveDate,ExpirationDate,BoundDateUtc,IsDeleted,PolicySourceCode,PolicySourceReason,LineOfBusiness,DataCompletenessCode,VerificationStatusCode,IssueStatus,CoverageStatus)
	VALUES
		(@PolicyId1,@TenantId,@AccId4,@CarrierId1,'POL-PBC-001-BOP','Active',18500.00,DATEADD(YEAR,-1,@Now),DATEADD(MONTH,11,@Now),DATEADD(YEAR,-1,@Now),0,'ManualExistingPolicy','DevelopmentSeed','Business Owners Policy','Complete','Verified','Issued','Active');

-- Professional Liability
IF NOT EXISTS (SELECT 1 FROM Submissions.BoundPolicy WHERE PolicyId = @PolicyId2)
	INSERT INTO Submissions.BoundPolicy
		(PolicyId,TenantId,AccountId,CarrierId,PolicyNumber,Status,AnnualPremium,EffectiveDate,ExpirationDate,BoundDateUtc,IsDeleted,PolicySourceCode,PolicySourceReason,LineOfBusiness,DataCompletenessCode,VerificationStatusCode,IssueStatus,CoverageStatus)
	VALUES
		(@PolicyId2,@TenantId,@AccId4,@CarrierId2,'POL-PBC-002-EO','Active',12400.00,DATEADD(MONTH,-8,@Now),DATEADD(MONTH,4,@Now),DATEADD(MONTH,-8,@Now),0,'ManualExistingPolicy','DevelopmentSeed','Errors & Omissions','Complete','Verified','Issued','Active');

-- Cyber Liability
IF NOT EXISTS (SELECT 1 FROM Submissions.BoundPolicy WHERE PolicyId = @PolicyId3)
	INSERT INTO Submissions.BoundPolicy
		(PolicyId,TenantId,AccountId,CarrierId,PolicyNumber,Status,AnnualPremium,EffectiveDate,ExpirationDate,BoundDateUtc,IsDeleted,PolicySourceCode,PolicySourceReason,LineOfBusiness,DataCompletenessCode,VerificationStatusCode,IssueStatus,CoverageStatus)
	VALUES
		(@PolicyId3,@TenantId,@AccId4,@CarrierId3,'POL-PBC-003-CYB','Active',8200.00,DATEADD(MONTH,-4,@Now),DATEADD(MONTH,8,@Now),DATEADD(MONTH,-4,@Now),0,'ManualExistingPolicy','DevelopmentSeed','Cyber Liability','Complete','Verified','Issued','Active');

-- =============================================================================
-- SUBMISSIONS FOR PINNACLE BROKERS
-- =============================================================================
DECLARE @SubId1 UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000001';
DECLARE @SubId2 UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubId1)
	INSERT INTO Submissions.Submission
		(SubmissionId,TenantId,AccountId,SubmissionNumber,LineOfBusiness,Status,Priority,EffectiveDate,ExpirationDate,TargetPremium,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(@SubId1,@TenantId,@AccId4,'SUB-PBC-2025-001','Workers Compensation','Quotes Received','Normal',DATEADD(DAY,30,@Now),DATEADD(YEAR,1,@Now),18500.00,@UserId,0,DATEADD(DAY,-15,@Now));

IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubId2)
	INSERT INTO Submissions.Submission
		(SubmissionId,TenantId,AccountId,SubmissionNumber,LineOfBusiness,Status,Priority,EffectiveDate,ExpirationDate,TargetPremium,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(@SubId2,@TenantId,@AccId4,'SUB-PBC-2025-002','Directors & Officers','Marketing','High',DATEADD(DAY,45,@Now),DATEADD(DAY,410,@Now),25000.00,@UserId,0,DATEADD(DAY,-8,@Now));

-- =============================================================================
-- CLAIMS FOR PINNACLE BROKERS (1 Open Claim)
-- =============================================================================
DECLARE @ClaimId1 UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Claims.Claim WHERE ClaimId = @ClaimId1)
	INSERT INTO Claims.Claim
		(ClaimId,TenantId,AccountId,PolicyId,CarrierId,ClaimNumber,PolicyNumber,AccountName,Lob,Carrier,Status,LossType,PrimaryClaimant,DateOfLoss,DateReported,TotalIncurred,TotalReserves,TotalPaid,AssignedHandler,Priority,PolicyLinkStatusCode,AccountLinkStatusCode,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(@ClaimId1,@TenantId,@AccId4,@PolicyId1,@CarrierId1,'CLM-PBC-2024-001','POL-PBC-001-BOP','Pinnacle Brokers Co.','Business Owners Policy','Hartford Financial Services','Open','Water Damage','Pinnacle Brokers Co.',CONVERT(date,DATEADD(DAY,-45,@Now)),CONVERT(date,DATEADD(DAY,-44,@Now)),15000.00,15000.00,0.00,'Jane Smith - Hartford Claims','Standard','Linked','Linked',@UserId,0,DATEADD(DAY,-45,@Now));

-- =============================================================================
-- ACTIVITIES FOR PINNACLE BROKERS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Q4 Business Review')
	INSERT INTO Client.AccountActivity
		(ActivityId,TenantId,AccountId,ActivityType,Title,Description,Subject,Notes,
		 OccurredAtUtc, DurationMinutes, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,'Meeting','Q4 Business Review','Reviewed renewal strategy, discussed expansion plans, identified cyber liability gap.','Q4 Business Review',
		 'Reviewed renewal strategy, discussed expansion plans, identified cyber liability gap.',
		 DATEADD(DAY, -12, @Now), 60, @UserId, 0, DATEADD(DAY, -12, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Follow-up on WC Quote')
	INSERT INTO Client.AccountActivity
		(ActivityId,TenantId,AccountId,ActivityType,Title,Description,Subject,Notes,
		 OccurredAtUtc, DurationMinutes, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,'Call','Follow-up on WC Quote','Discussed Hartford WC quote, client reviewing with CFO, expects decision by end of week.','Follow-up on WC Quote',
		 'Discussed Hartford WC quote, client reviewing with CFO, expects decision by end of week.',
		 DATEADD(DAY, -6, @Now), 15, @UserId, 0, DATEADD(DAY, -6, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'D&O Submission Sent')
	INSERT INTO Client.AccountActivity
		(ActivityId,TenantId,AccountId,ActivityType,Title,Description,Subject,Notes,
		 OccurredAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,'Email','D&O Submission Sent','Sent completed D&O application to Chubb for board expansion coverage.','D&O Submission Sent',
		 'Sent completed D&O application to Chubb for board expansion coverage.',
		 DATEADD(DAY, -8, @Now), @UserId, 0, DATEADD(DAY, -8, @Now));

IF NOT EXISTS (SELECT 1 FROM Client.AccountActivity WHERE TenantId = @TenantId AND AccountId = @AccId4 AND Subject = 'Claim Notification')
	INSERT INTO Client.AccountActivity
		(ActivityId,TenantId,AccountId,ActivityType,Title,Description,Subject,Notes,
		 OccurredAtUtc, CreatedByUserId, IsDeleted, CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,'Note','Claim Notification','Client reported water damage to server room. Filed claim with Hartford, adjuster assigned.','Claim Notification',
		 'Client reported water damage to server room. Filed claim with Hartford, adjuster assigned.',
		 DATEADD(DAY, -45, @Now), @UserId, 0, DATEADD(DAY, -45, @Now));

-- =============================================================================
-- ACCOUNT RELATIONSHIPS FOR PINNACLE BROKERS
-- =============================================================================
DECLARE @RelAccId1 UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000001';
DECLARE @RelAccId2 UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000002';

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
		(RelationshipId,TenantId,SourceAccountId,AccountId,RelatedAccountId,RelationshipType,
		 Description,IsActive,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,@AccId4,@RelAccId1,'Parent',
		 'Pinnacle Brokers Holdings is the parent company.',1,@UserId,0,DATEADD(YEAR,-2,@Now));

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
		(RelationshipId,TenantId,SourceAccountId,AccountId,RelatedAccountId,RelationshipType,
		 Description,IsActive,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,@AccId4,@RelAccId2,'Subsidiary',
		 'Pinnacle Risk Solutions is a wholly-owned subsidiary.',1,@UserId,0,DATEADD(YEAR,-1,@Now));

-- =============================================================================
-- ACCOUNT NOTES
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountNote WHERE TenantId=@TenantId AND AccountId=@AccId4 AND NoteTypeCode='StrategicPriority' AND IsDeleted=0)
	INSERT INTO Client.AccountNote
		(AccountNoteId,TenantId,AccountId,NoteText,NoteTypeCode,CreatedByUserId,IsDeleted,CreatedDateUtc)
	VALUES
		(NEWID(),@TenantId,@AccId4,
		 'Pinnacle Brokers is a strategic account with strong growth potential. CEO Sarah Mitchell has expressed interest in expanding cyber coverage and exploring employee benefits for their growing team. Maintain monthly touch points and prioritize service excellence.',
		 'StrategicPriority',@UserId,0,DATEADD(DAY,-60,@Now));

-- =============================================================================
PRINT 'Enhanced seed data for Pinnacle Brokers Co. (Account 20000000-0000-0000-0000-000000000004) applied successfully.';
GO
