IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Client')
	EXEC(N'CREATE SCHEMA Client');

IF OBJECT_ID(N'Client.AccountNumberConfiguration', N'U') IS NULL
BEGIN
	CREATE TABLE Client.AccountNumberConfiguration
	(
		AccountNumberConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AccountNumberConfiguration PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		Prefix NVARCHAR(20) NOT NULL,
		NextNumber BIGINT NOT NULL,
		PaddingLength INT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AccountNumberConfiguration_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AccountNumberConfiguration_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_AccountNumberConfiguration_Tenant UNIQUE (TenantId),
		CONSTRAINT CK_AccountNumberConfiguration_NextNumber CHECK (NextNumber > 0),
		CONSTRAINT CK_AccountNumberConfiguration_PaddingLength CHECK (PaddingLength BETWEEN 1 AND 18)
	);
END;

;WITH Tenants AS
(
	SELECT TenantId
	FROM Core.Tenant
	WHERE IsDeleted = 0
	UNION
	SELECT DISTINCT TenantId
	FROM Client.Account
	WHERE IsDeleted = 0
), ExistingMaximum AS
(
	SELECT tenant.TenantId,
		COALESCE(MAX(TRY_CONVERT(BIGINT, SUBSTRING(account.AccountNumber, 5, 50))), 0) AS MaximumNumber
	FROM Tenants tenant
	LEFT JOIN Client.Account account
		ON account.TenantId = tenant.TenantId
		AND account.IsDeleted = 0
		AND account.AccountNumber LIKE N'ACC-%'
		AND TRY_CONVERT(BIGINT, SUBSTRING(account.AccountNumber, 5, 50)) IS NOT NULL
	GROUP BY tenant.TenantId
)
INSERT INTO Client.AccountNumberConfiguration
(
	AccountNumberConfigurationId, TenantId, Prefix, NextNumber, PaddingLength,
	CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted
)
SELECT NEWID(), maximum.TenantId, N'ACC-', maximum.MaximumNumber + 1, 6,
	SYSUTCDATETIME(), NULL, NULL, NULL, 0
FROM ExistingMaximum maximum
WHERE NOT EXISTS
(
	SELECT 1
	FROM Client.AccountNumberConfiguration configuration
	WHERE configuration.TenantId = maximum.TenantId
);

EXEC(N'
CREATE OR ALTER PROCEDURE Client.AllocateAccountNumber
	@TenantId UNIQUEIDENTIFIER,
	@ActorUserId UNIQUEIDENTIFIER = NULL,
	@AccountNumber NVARCHAR(50) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @Prefix NVARCHAR(20), @AllocatedNumber BIGINT, @PaddingLength INT;

	IF NOT EXISTS
	(
		SELECT 1
		FROM Client.AccountNumberConfiguration WITH (UPDLOCK, HOLDLOCK)
		WHERE TenantId = @TenantId AND IsDeleted = 0
	)
	BEGIN
		DECLARE @ExistingMaximum BIGINT =
		(
			SELECT COALESCE(MAX(TRY_CONVERT(BIGINT, SUBSTRING(AccountNumber, 5, 50))), 0)
			FROM Client.Account WITH (UPDLOCK, HOLDLOCK)
			WHERE TenantId = @TenantId
			  AND IsDeleted = 0
			  AND AccountNumber LIKE N''''ACC-%''''
			  AND TRY_CONVERT(BIGINT, SUBSTRING(AccountNumber, 5, 50)) IS NOT NULL
		);

		INSERT INTO Client.AccountNumberConfiguration
		(
			AccountNumberConfigurationId, TenantId, Prefix, NextNumber, PaddingLength,
			CreatedDateUtc, CreatedByUserId, IsDeleted
		)
		VALUES (NEWID(), @TenantId, N''''ACC-'''', @ExistingMaximum + 1, 6, SYSUTCDATETIME(), @ActorUserId, 0);
	END;

	SELECT @Prefix = Prefix, @AllocatedNumber = NextNumber, @PaddingLength = PaddingLength
	FROM Client.AccountNumberConfiguration WITH (UPDLOCK, HOLDLOCK)
	WHERE TenantId = @TenantId AND IsDeleted = 0;

	IF @AllocatedNumber IS NULL
		THROW 51000, N''''Account number configuration is unavailable for this tenant.'''', 1;

	SET @AccountNumber = CONCAT(@Prefix, RIGHT(REPLICATE(N''''0'''', @PaddingLength) + CONVERT(NVARCHAR(20), @AllocatedNumber), @PaddingLength));

	IF LEN(@AccountNumber) > 50
		THROW 51000, N''''The generated account number exceeds the Client.Account column length.'''', 1;

	IF EXISTS (SELECT 1 FROM Client.Account WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND AccountNumber = @AccountNumber AND IsDeleted = 0)
		THROW 51000, N''''The configured next account number already exists. Update the tenant account numbering configuration.'''', 1;

	UPDATE Client.AccountNumberConfiguration
	SET NextNumber = @AllocatedNumber + 1,
		ModifiedDateUtc = SYSUTCDATETIME(),
		ModifiedByUserId = @ActorUserId
	WHERE TenantId = @TenantId AND IsDeleted = 0;
END;
');
