DECLARE @SubmissionId UNIQUEIDENTIFIER = 'e1000000-0000-0000-0000-000000000011';
DECLARE @PropertyLineId UNIQUEIDENTIFIER = 'e3100000-0000-0000-0000-000000000011';
DECLARE @LiabilityLineId UNIQUEIDENTIFIER = 'e3100000-0000-0000-0000-000000000012';
DECLARE @MarketId UNIQUEIDENTIFIER = 'e3200000-0000-0000-0000-000000000011';
DECLARE @PrimaryQuoteId UNIQUEIDENTIFIER = 'e2000000-0000-0000-0000-000000000011';
DECLARE @AlternateQuoteId UNIQUEIDENTIFIER = 'e2000000-0000-0000-0000-000000000012';
DECLARE @TenantId UNIQUEIDENTIFIER = COALESCE(
	(SELECT TOP 1 TenantId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId),
	(SELECT TOP 1 TenantId FROM IAM.[User] WHERE IsDeleted = 0 ORDER BY CreatedDateUtc),
	'00000000-0000-0000-0000-000000000001');
DECLARE @AdminUserId UNIQUEIDENTIFIER = (SELECT TOP 1 UserId FROM IAM.[User] WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
DECLARE @AccountId UNIQUEIDENTIFIER = (SELECT TOP 1 AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
DECLARE @OpportunityId UNIQUEIDENTIFIER = (SELECT TOP 1 OpportunityId FROM CRM.Opportunity WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(
	(SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = N'Travelers' AND IsDeleted = 0),
	(SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));

IF @AccountId IS NULL THROW 52200, 'Multi-line quote demo seed requires an active account.', 1;
IF @CarrierId IS NULL THROW 52201, 'Multi-line quote demo seed requires an active carrier.', 1;

IF EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId)
BEGIN
	UPDATE Submissions.Submission
	SET TenantId = @TenantId,
		AccountId = @AccountId,
		OpportunityId = COALESCE(OpportunityId, @OpportunityId),
		SubmissionNumber = N'SUB-MLQ-DEMO-001',
		LineOfBusiness = N'Commercial Package',
		Status = N'Quotes Received',
		Priority = N'High',
		AssignedToUserId = COALESCE(AssignedToUserId, @AdminUserId),
		EffectiveDate = DATEADD(day, 28, CONVERT(date, SYSUTCDATETIME())),
		ExpirationDate = DATEADD(day, 393, CONVERT(date, SYSUTCDATETIME())),
		TargetPremium = 128500,
		MarketCount = 1,
		QuoteCount = 2,
		ModifiedDateUtc = SYSUTCDATETIME(),
		ModifiedByUserId = @AdminUserId,
		IsDeleted = 0
	WHERE SubmissionId = @SubmissionId;
END
ELSE
BEGIN
	INSERT INTO Submissions.Submission
		(SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority,
		 AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount,
		 CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
	VALUES
		(@SubmissionId, @TenantId, @AccountId, @OpportunityId, N'SUB-MLQ-DEMO-001', N'Commercial Package', N'Quotes Received', N'High',
		 @AdminUserId, DATEADD(day, 28, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 393, CONVERT(date, SYSUTCDATETIME())),
		 128500, 1, 2, SYSUTCDATETIME(), @AdminUserId, SYSUTCDATETIME(), @AdminUserId, 0);
END;

MERGE Submissions.SubmissionLine AS target
USING
(
	VALUES
		(@PropertyLineId, N'Commercial Property', CONVERT(decimal(18,2), 88500), 0),
		(@LiabilityLineId, N'General Liability', CONVERT(decimal(18,2), 40000), 1)
) AS source(SubmissionLineId, LineOfBusiness, TargetPremium, SortOrder)
ON target.SubmissionLineId = source.SubmissionLineId
WHEN MATCHED THEN UPDATE SET
	TenantId = @TenantId, SubmissionId = @SubmissionId, OpportunityId = @OpportunityId,
	OpportunityLineId = NULL, LineOfBusiness = source.LineOfBusiness, TargetPremium = source.TargetPremium,
	ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AdminUserId, IsDeleted = 0
WHEN NOT MATCHED THEN INSERT
	(SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium,
	 CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
	(source.SubmissionLineId, @TenantId, @SubmissionId, @OpportunityId, NULL, source.LineOfBusiness, source.TargetPremium,
	 SYSUTCDATETIME(), @AdminUserId, SYSUTCDATETIME(), @AdminUserId, 0);

IF EXISTS (SELECT 1 FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId)
BEGIN
	UPDATE Submissions.SubmissionMarket
	SET TenantId = @TenantId, SubmissionId = @SubmissionId, CarrierId = @CarrierId, Status = N'Quoted', AppetiteScore = 93,
		IsRecommended = 1, RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
		ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AdminUserId, IsDeleted = 0
	WHERE SubmissionMarketId = @MarketId;
END
ELSE
BEGIN
	INSERT INTO Submissions.SubmissionMarket
		(SubmissionMarketId, TenantId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended,
		 AddedDateUtc, RespondedDateUtc, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
	VALUES
		(@MarketId, @TenantId, @SubmissionId, @CarrierId, N'Quoted', 93, 1,
		 DATEADD(day, -8, SYSUTCDATETIME()), DATEADD(day, -2, SYSUTCDATETIME()), SYSUTCDATETIME(), @AdminUserId, 0);
END;

MERGE Submissions.SubmissionMarketLine AS target
USING
(
	VALUES
		('e3300000-0000-0000-0000-000000000011', @PropertyLineId, N'Commercial Property', CONVERT(decimal(18,2), 88500)),
		('e3300000-0000-0000-0000-000000000012', @LiabilityLineId, N'General Liability', CONVERT(decimal(18,2), 40000))
) AS source(SubmissionMarketLineId, SubmissionLineId, LineOfBusiness, TargetPremium)
ON target.SubmissionMarketId = @MarketId
AND target.SubmissionLineId = source.SubmissionLineId
AND target.IsDeleted = 0
WHEN MATCHED THEN UPDATE SET
	TenantId = @TenantId, SubmissionMarketId = @MarketId, SubmissionId = @SubmissionId,
	SubmissionLineId = source.SubmissionLineId, LineOfBusiness = source.LineOfBusiness, TargetPremium = source.TargetPremium,
	ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AdminUserId, IsDeleted = 0
WHEN NOT MATCHED THEN INSERT
	(SubmissionMarketLineId, TenantId, SubmissionMarketId, SubmissionId, SubmissionLineId, LineOfBusiness, TargetPremium,
	 CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
	(source.SubmissionMarketLineId, @TenantId, @MarketId, @SubmissionId, source.SubmissionLineId, source.LineOfBusiness, source.TargetPremium,
	 SYSUTCDATETIME(), @AdminUserId, SYSUTCDATETIME(), @AdminUserId, 0);

MERGE Submissions.Quote AS target
USING
(
	VALUES
		(@PrimaryQuoteId, N'QT-MLQ-DEMO-001', N'Approved for Presentation', CONVERT(decimal(18,2), 128500), CONVERT(decimal(18,2), 5000), CONVERT(decimal(18,2), 3000000), CONVERT(decimal(9,4), 12.5), 1, 1),
		(@AlternateQuoteId, N'QT-MLQ-DEMO-002', N'Received', CONVERT(decimal(18,2), 134250), CONVERT(decimal(18,2), 7500), CONVERT(decimal(18,2), 3500000), CONVERT(decimal(9,4), 11.0), 0, 0)
) AS source(QuoteId, QuoteNumber, Status, AnnualPremium, Deductible, CoverageLimit, CommissionPercent, IsBindable, IsRecommended)
ON target.QuoteId = source.QuoteId
WHEN MATCHED THEN UPDATE SET
	SubmissionId = @SubmissionId, SubmissionMarketId = @MarketId, CarrierId = @CarrierId,
	QuoteNumber = source.QuoteNumber, Status = source.Status, AnnualPremium = source.AnnualPremium,
	Deductible = source.Deductible, [Limit] = source.CoverageLimit, CommissionPercent = source.CommissionPercent,
	CoverageForms = N'ISO commercial package forms', Subjectivities = N'Signed application and updated loss runs required before binding.',
	Exclusions = N'Standard policy exclusions apply.', CarrierRating = N'A', PaymentTerms = N'Annual',
	MinimumEarnedPremium = source.AnnualPremium * 0.25, TaxesAndFees = 1800, BrokerFee = 500,
	TriaIncluded = 1, IsBindable = source.IsBindable, IsRecommended = source.IsRecommended,
	RecommendationScore = CASE WHEN source.IsRecommended = 1 THEN 94 ELSE 86 END,
	RecommendationReason = N'DB-backed multi-line quote demonstration.',
	CoverageNotes = N'Persisted package quote containing property and general liability coverage lines.',
	EffectiveDate = DATEADD(day, 28, CONVERT(date, SYSUTCDATETIME())),
	QuotedDateUtc = DATEADD(day, -2, SYSUTCDATETIME()), QuoteReceivedDateUtc = DATEADD(day, -2, SYSUTCDATETIME()),
	ExpiresDateUtc = DATEADD(day, 28, SYSUTCDATETIME()), ResponseVersion = 1, ResponseSourceCode = N'SeedMigration',
	CarrierReferenceNumber = source.QuoteNumber, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AdminUserId, IsDeleted = 0
WHEN NOT MATCHED THEN INSERT
	(QuoteId, SubmissionId, SubmissionMarketId, CarrierId, QuoteNumber, Status, AnnualPremium, EffectiveDate,
	 Deductible, [Limit], CoverageForms, CommissionPercent, Subjectivities, Exclusions, CarrierRating, PaymentTerms,
	 MinimumEarnedPremium, TaxesAndFees, BrokerFee, TriaIncluded, IsBindable, IsRecommended, RecommendationScore,
	 RecommendationReason, CoverageNotes, QuotedDateUtc, QuoteReceivedDateUtc, ExpiresDateUtc, ResponseVersion,
	 ResponseSourceCode, CarrierReferenceNumber, CreatedDateUtc, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
	(source.QuoteId, @SubmissionId, @MarketId, @CarrierId, source.QuoteNumber, source.Status, source.AnnualPremium,
	 DATEADD(day, 28, CONVERT(date, SYSUTCDATETIME())), source.Deductible, source.CoverageLimit, N'ISO commercial package forms',
	 source.CommissionPercent, N'Signed application and updated loss runs required before binding.', N'Standard policy exclusions apply.',
	 N'A', N'Annual', source.AnnualPremium * 0.25, 1800, 500, 1, source.IsBindable, source.IsRecommended,
	 CASE WHEN source.IsRecommended = 1 THEN 94 ELSE 86 END, N'DB-backed multi-line quote demonstration.',
	 N'Persisted package quote containing property and general liability coverage lines.', DATEADD(day, -2, SYSUTCDATETIME()),
	 DATEADD(day, -2, SYSUTCDATETIME()), DATEADD(day, 28, SYSUTCDATETIME()), 1, N'SeedMigration', source.QuoteNumber,
	 SYSUTCDATETIME(), SYSUTCDATETIME(), @AdminUserId, 0);

MERGE Submissions.QuoteLine AS target
USING
(
	VALUES
		('e3400000-0000-0000-0000-000000000011', @PrimaryQuoteId, @PropertyLineId, N'Commercial Property', CONVERT(decimal(18,2), 88500), CONVERT(decimal(18,2), 5000), CONVERT(decimal(18,2), 2000000), CONVERT(decimal(9,4), 12.5), 0),
		('e3400000-0000-0000-0000-000000000012', @PrimaryQuoteId, @LiabilityLineId, N'General Liability', CONVERT(decimal(18,2), 40000), CONVERT(decimal(18,2), 0), CONVERT(decimal(18,2), 1000000), CONVERT(decimal(9,4), 12.5), 1),
		('e3400000-0000-0000-0000-000000000021', @AlternateQuoteId, @PropertyLineId, N'Commercial Property', CONVERT(decimal(18,2), 92000), CONVERT(decimal(18,2), 7500), CONVERT(decimal(18,2), 2500000), CONVERT(decimal(9,4), 11.0), 0),
		('e3400000-0000-0000-0000-000000000022', @AlternateQuoteId, @LiabilityLineId, N'General Liability', CONVERT(decimal(18,2), 42250), CONVERT(decimal(18,2), 0), CONVERT(decimal(18,2), 1000000), CONVERT(decimal(9,4), 11.0), 1)
) AS source(QuoteLineId, QuoteId, SubmissionLineId, LineOfBusiness, QuotedPremium, Deductible, CoverageLimit, CommissionPercent, SortOrder)
ON target.QuoteId = source.QuoteId
AND target.SubmissionLineId = source.SubmissionLineId
AND target.IsDeleted = 0
WHEN MATCHED THEN UPDATE SET
	TenantId = @TenantId, QuoteId = source.QuoteId, SubmissionId = @SubmissionId,
	SubmissionLineId = source.SubmissionLineId, OpportunityLineId = NULL, LineOfBusiness = source.LineOfBusiness,
	QuotedPremium = source.QuotedPremium, Deductible = source.Deductible, [Limit] = source.CoverageLimit,
	CommissionPercent = source.CommissionPercent, CoverageForms = N'ISO occurrence and property forms',
	Subjectivities = N'Signed application and updated loss runs.', Exclusions = N'Standard exclusions apply.',
	PaymentTerms = N'Annual', MinimumEarnedPremium = source.QuotedPremium * 0.25,
	TaxesAndFees = CASE WHEN source.SortOrder = 0 THEN 1200 ELSE 600 END, BrokerFee = CASE WHEN source.SortOrder = 0 THEN 300 ELSE 200 END,
	TriaIncluded = 1, IsBindable = 1, CoverageNotes = N'Persisted line-specific enterprise quote terms.',
	Status = N'Quoted', SortOrder = source.SortOrder, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @AdminUserId, IsDeleted = 0
WHEN NOT MATCHED THEN INSERT
	(QuoteLineId, TenantId, QuoteId, SubmissionId, SubmissionLineId, OpportunityLineId, LineOfBusiness, QuotedPremium,
	 Deductible, [Limit], CommissionPercent, CoverageForms, Subjectivities, Exclusions, PaymentTerms, MinimumEarnedPremium,
	 TaxesAndFees, BrokerFee, TriaIncluded, IsBindable, CoverageNotes, Status, SortOrder,
	 CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
	(source.QuoteLineId, @TenantId, source.QuoteId, @SubmissionId, source.SubmissionLineId, NULL, source.LineOfBusiness,
	 source.QuotedPremium, source.Deductible, source.CoverageLimit, source.CommissionPercent, N'ISO occurrence and property forms',
	 N'Signed application and updated loss runs.', N'Standard exclusions apply.', N'Annual', source.QuotedPremium * 0.25,
	 CASE WHEN source.SortOrder = 0 THEN 1200 ELSE 600 END, CASE WHEN source.SortOrder = 0 THEN 300 ELSE 200 END,
	 1, 1, N'Persisted line-specific enterprise quote terms.', N'Quoted', source.SortOrder,
	 SYSUTCDATETIME(), @AdminUserId, SYSUTCDATETIME(), @AdminUserId, 0);
