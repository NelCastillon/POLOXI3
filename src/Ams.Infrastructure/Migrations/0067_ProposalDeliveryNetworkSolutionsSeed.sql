SET XACT_ABORT ON;
GO

DECLARE @SeedUserId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 UserId FROM IAM.[User] WHERE IsDeleted = 0 ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000002');
DECLARE @Tenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY);

INSERT INTO @Tenants(TenantId)
SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0
UNION SELECT DISTINCT TenantId FROM Submissions.Proposal
UNION SELECT DISTINCT TenantId FROM Submissions.ProposalDeliveryProvider;

INSERT INTO Submissions.ProposalDeliveryProvider
	(ProposalDeliveryProviderId, TenantId, DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName, EndpointUri, SenderAddress, SecretReference, ConfigurationJson, IsConfigured, IsActive, MaxAttempts, RetryDelaySeconds, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), tenant.TenantId, N'Email', N'TenantSmtp', N'Smtp', N'NetworkSolutions SMTP',
	   N'smtp://netsol-smtp-oxcs.hostingplatform.com:587', N'ams_admin@agencybinder.com', N'AMS_PROPOSAL_SMTP_PASSWORD',
	   N'{"username":"ams_admin@agencybinder.com","enableSsl":"true"}', 1, 1, 5, 300, SYSUTCDATETIME(), @SeedUserId, 0
FROM @Tenants tenant
WHERE NOT EXISTS
(
	SELECT 1
	FROM Submissions.ProposalDeliveryProvider provider
	WHERE provider.TenantId = tenant.TenantId
	  AND provider.DeliveryMethodCode = N'Email'
	  AND provider.IsDeleted = 0
);

UPDATE provider
SET ProviderCode = CASE WHEN provider.ProviderCode IN (N'SMTP', N'TenantSmtp') THEN N'TenantSmtp' ELSE provider.ProviderCode END,
	DisplayName = CASE WHEN provider.DisplayName IN (N'Email (SMTP)', N'Tenant SMTP') THEN N'NetworkSolutions SMTP' ELSE provider.DisplayName END,
	EndpointUri = CASE WHEN provider.EndpointUri IN (N'smtp://mail.agencybinder.com:587', N'mail.agencybinder.com') THEN N'smtp://netsol-smtp-oxcs.hostingplatform.com:587' ELSE COALESCE(NULLIF(provider.EndpointUri, N''), N'smtp://netsol-smtp-oxcs.hostingplatform.com:587') END,
	SenderAddress = COALESCE(NULLIF(provider.SenderAddress, N''), N'ams_admin@agencybinder.com'),
	SecretReference = COALESCE(NULLIF(provider.SecretReference, N''), N'AMS_PROPOSAL_SMTP_PASSWORD'),
	ConfigurationJson = COALESCE(NULLIF(provider.ConfigurationJson, N''), N'{"username":"ams_admin@agencybinder.com","enableSsl":"true"}'),
	IsConfigured = CASE
		WHEN COALESCE(NULLIF(provider.EndpointUri, N''), N'smtp://netsol-smtp-oxcs.hostingplatform.com:587') IS NOT NULL
		 AND COALESCE(NULLIF(provider.SenderAddress, N''), N'ams_admin@agencybinder.com') IS NOT NULL
		 AND COALESCE(NULLIF(provider.SecretReference, N''), N'AMS_PROPOSAL_SMTP_PASSWORD') IS NOT NULL THEN 1
		ELSE provider.IsConfigured
	END,
	IsActive = 1,
	MaxAttempts = CASE WHEN provider.MaxAttempts BETWEEN 1 AND 25 THEN provider.MaxAttempts ELSE 5 END,
	RetryDelaySeconds = CASE WHEN provider.RetryDelaySeconds BETWEEN 10 AND 86400 THEN provider.RetryDelaySeconds ELSE 300 END,
	ModifiedDateUtc = SYSUTCDATETIME(),
	ModifiedByUserId = @SeedUserId
FROM Submissions.ProposalDeliveryProvider provider
WHERE provider.DeliveryMethodCode = N'Email'
  AND provider.HandlerCode = N'Smtp'
  AND provider.IsDeleted = 0
  AND (
		provider.EndpointUri IS NULL OR provider.EndpointUri = N''
	 OR provider.SenderAddress IS NULL OR provider.SenderAddress = N''
	 OR provider.SecretReference IS NULL OR provider.SecretReference = N''
	 OR provider.ConfigurationJson IS NULL OR provider.ConfigurationJson = N''
	 OR provider.ProviderCode IN (N'SMTP', N'TenantSmtp')
	 OR provider.DisplayName IN (N'Email (SMTP)', N'Tenant SMTP')
	 OR provider.IsConfigured = 0
  );

UPDATE optionRow
SET DisplayName = N'Email',
	Description = N'Deliver proposals by email through the tenant SMTP provider.',
	IsDefault = 1,
	IsActive = 1,
	SortOrder = CASE WHEN optionRow.SortOrder <= 0 THEN 10 ELSE optionRow.SortOrder END,
	ModifiedDateUtc = SYSUTCDATETIME(),
	ModifiedByUserId = @SeedUserId
FROM Submissions.ProposalWorkflowOption optionRow
WHERE optionRow.OptionGroupCode = N'DeliveryMethod'
  AND optionRow.OptionCode = N'Email'
  AND optionRow.IsDeleted = 0;
GO
