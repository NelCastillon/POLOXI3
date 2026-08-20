IF OBJECT_ID(N'Client.AccountReferenceOption', N'U') IS NOT NULL
BEGIN
	;WITH Tenants AS
	(
		SELECT DISTINCT TenantId
		FROM Client.AccountReferenceOption
		WHERE IsDeleted = 0
		UNION
		SELECT DISTINCT TenantId
		FROM Client.Account
		WHERE IsDeleted = 0
	), IndustryOptions AS
	(
		SELECT *
		FROM (VALUES
			(N'Agriculture', N'Agriculture, Forestry, Fishing and Hunting', 10),
			(N'Mining', N'Mining, Quarrying, and Oil and Gas Extraction', 20),
			(N'Utilities', N'Utilities', 30),
			(N'Construction', N'Construction', 40),
			(N'Manufacturing', N'Manufacturing', 50),
			(N'Wholesale Trade', N'Wholesale Trade', 60),
			(N'Retail Trade', N'Retail Trade', 70),
			(N'Transportation', N'Transportation and Warehousing', 80),
			(N'Information', N'Information', 90),
			(N'Finance and Insurance', N'Finance and Insurance', 100),
			(N'Real Estate', N'Real Estate and Rental and Leasing', 110),
			(N'Professional Services', N'Professional, Scientific, and Technical Services', 120),
			(N'Management', N'Management of Companies and Enterprises', 130),
			(N'Administrative Services', N'Administrative and Support and Waste Management and Remediation Services', 140),
			(N'Education', N'Educational Services', 150),
			(N'Health Care', N'Health Care and Social Assistance', 160),
			(N'Arts and Entertainment', N'Arts, Entertainment, and Recreation', 170),
			(N'Accommodation and Food', N'Accommodation and Food Services', 180),
			(N'Other Services', N'Other Services except Public Administration', 190),
			(N'Public Administration', N'Public Administration', 200)
		) value(OptionCode, OptionName, SortOrder)
	)
	INSERT INTO Client.AccountReferenceOption
	(
		AccountReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName,
		Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
	)
	SELECT NEWID(), tenant.TenantId, N'Industry', industry.OptionCode, industry.OptionName,
		N'Industry classification used for account master records.', 0, 1, industry.SortOrder, SYSUTCDATETIME(), 0
	FROM Tenants tenant
	CROSS JOIN IndustryOptions industry
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM Client.AccountReferenceOption existing
		WHERE existing.TenantId = tenant.TenantId
		  AND existing.OptionGroup = N'Industry'
		  AND existing.OptionCode = industry.OptionCode
	);
END;
