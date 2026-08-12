SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Location')
	EXEC(N'CREATE SCHEMA Location');

IF OBJECT_ID(N'Location.AddressProviderConfiguration', N'U') IS NULL
BEGIN
	CREATE TABLE Location.AddressProviderConfiguration
	(
		AddressProviderConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Location_AddressProviderConfiguration PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProviderCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(100) NOT NULL,
		ServiceEndpoint NVARCHAR(300) NOT NULL,
		AutocompletePath NVARCHAR(200) NOT NULL,
		GeocodePath NVARCHAR(200) NOT NULL,
		ApiVersion NVARCHAR(20) NOT NULL,
		AuthenticationScope NVARCHAR(300) NOT NULL,
		MapsClientId NVARCHAR(100) NULL,
		DefaultCountrySet NVARCHAR(200) NULL,
		DefaultLanguage NVARCHAR(20) NULL,
		MinimumQueryLength INT NOT NULL,
		DebounceMilliseconds INT NOT NULL,
		MaximumSuggestions INT NOT NULL,
		RequestTimeoutSeconds INT NOT NULL,
		IsDefault BIT NOT NULL,
		IsEnabled BIT NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Location_AddressProviderConfiguration_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Location_AddressProviderConfiguration_IsDeleted DEFAULT (0),
		CONSTRAINT CK_Location_AddressProviderConfiguration_MinimumQueryLength CHECK (MinimumQueryLength BETWEEN 2 AND 20),
		CONSTRAINT CK_Location_AddressProviderConfiguration_DebounceMilliseconds CHECK (DebounceMilliseconds BETWEEN 100 AND 5000),
		CONSTRAINT CK_Location_AddressProviderConfiguration_MaximumSuggestions CHECK (MaximumSuggestions BETWEEN 1 AND 20),
		CONSTRAINT CK_Location_AddressProviderConfiguration_RequestTimeoutSeconds CHECK (RequestTimeoutSeconds BETWEEN 1 AND 120)
	);

	CREATE UNIQUE INDEX UX_Location_AddressProviderConfiguration_TenantProvider
		ON Location.AddressProviderConfiguration(TenantId, ProviderCode)
		WHERE IsDeleted = 0;

	CREATE UNIQUE INDEX UX_Location_AddressProviderConfiguration_Default
		ON Location.AddressProviderConfiguration(TenantId)
		WHERE IsDefault = 1 AND IsEnabled = 1 AND IsDeleted = 0;
END;

IF OBJECT_ID(N'Location.AddressResolution', N'U') IS NULL
BEGIN
	CREATE TABLE Location.AddressResolution
	(
		AddressResolutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Location_AddressResolution PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		AddressFieldCode NVARCHAR(100) NOT NULL,
		ProviderCode NVARCHAR(50) NULL,
		ProviderPlaceId NVARCHAR(200) NULL,
		QueryText NVARCHAR(300) NULL,
		FormattedAddress NVARCHAR(500) NOT NULL,
		AddressLine1 NVARCHAR(200) NULL,
		AddressLine2 NVARCHAR(200) NULL,
		City NVARCHAR(100) NULL,
		StateCode NVARCHAR(50) NULL,
		PostalCode NVARCHAR(20) NULL,
		CountryCode NVARCHAR(10) NULL,
		County NVARCHAR(100) NULL,
		Latitude DECIMAL(9,6) NULL,
		Longitude DECIMAL(9,6) NULL,
		ResolutionStatusCode NVARCHAR(50) NOT NULL,
		ConfidenceCode NVARCHAR(50) NULL,
		IsProviderValidated BIT NOT NULL,
		ResolvedDateUtc DATETIME2(7) NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Location_AddressResolution_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Location_AddressResolution_IsDeleted DEFAULT (0),
		CONSTRAINT CK_Location_AddressResolution_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
		CONSTRAINT CK_Location_AddressResolution_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
	);

	CREATE UNIQUE INDEX UX_Location_AddressResolution_EntityField
		ON Location.AddressResolution(TenantId, EntityTypeCode, EntityId, AddressFieldCode)
		WHERE IsDeleted = 0;

	CREATE INDEX IX_Location_AddressResolution_Coordinates
		ON Location.AddressResolution(TenantId, Latitude, Longitude)
		INCLUDE (EntityTypeCode, EntityId, AddressFieldCode, County, PostalCode)
		WHERE IsDeleted = 0 AND Latitude IS NOT NULL AND Longitude IS NOT NULL;

	CREATE INDEX IX_Location_AddressResolution_ProviderPlace
		ON Location.AddressResolution(TenantId, ProviderCode, ProviderPlaceId)
		WHERE IsDeleted = 0 AND ProviderPlaceId IS NOT NULL;
END;

IF COL_LENGTH(N'Client.Account', N'Street') IS NULL
	ALTER TABLE Client.Account ADD Street NVARCHAR(200) NULL;
IF COL_LENGTH(N'Client.Account', N'City') IS NULL
	ALTER TABLE Client.Account ADD City NVARCHAR(100) NULL;
IF COL_LENGTH(N'Client.Account', N'State') IS NULL
	ALTER TABLE Client.Account ADD [State] NVARCHAR(50) NULL;
IF COL_LENGTH(N'Client.Account', N'Zip') IS NULL
	ALTER TABLE Client.Account ADD Zip NVARCHAR(20) NULL;
IF COL_LENGTH(N'Client.Account', N'Country') IS NULL
	ALTER TABLE Client.Account ADD Country NVARCHAR(50) NULL;
IF COL_LENGTH(N'Client.Account', N'AddressResolutionId') IS NULL
	ALTER TABLE Client.Account ADD AddressResolutionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Client.Account', N'County') IS NULL
	ALTER TABLE Client.Account ADD County NVARCHAR(100) NULL;
IF COL_LENGTH(N'Client.Account', N'Latitude') IS NULL
	ALTER TABLE Client.Account ADD Latitude DECIMAL(9,6) NULL;
IF COL_LENGTH(N'Client.Account', N'Longitude') IS NULL
	ALTER TABLE Client.Account ADD Longitude DECIMAL(9,6) NULL;
IF COL_LENGTH(N'Client.Account', N'AddressValidationStatusCode') IS NULL
	ALTER TABLE Client.Account ADD AddressValidationStatusCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Client.Account', N'AddressProviderCode') IS NULL
	ALTER TABLE Client.Account ADD AddressProviderCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Client.Account', N'AddressProviderPlaceId') IS NULL
	ALTER TABLE Client.Account ADD AddressProviderPlaceId NVARCHAR(200) NULL;

GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Client_Account_AddressResolution')
	ALTER TABLE Client.Account WITH CHECK ADD CONSTRAINT FK_Client_Account_AddressResolution
		FOREIGN KEY (AddressResolutionId) REFERENCES Location.AddressResolution(AddressResolutionId);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Client_Account_Latitude')
	ALTER TABLE Client.Account ADD CONSTRAINT CK_Client_Account_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Client_Account_Longitude')
	ALTER TABLE Client.Account ADD CONSTRAINT CK_Client_Account_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Client.Account') AND name = N'IX_Client_Account_Geography')
	CREATE INDEX IX_Client_Account_Geography ON Client.Account(TenantId, [State], County, Zip)
		INCLUDE (Latitude, Longitude, AddressValidationStatusCode)
		WHERE IsDeleted = 0;

GO

CREATE OR ALTER PROCEDURE Location.EnsureAddressProviderConfiguration
	@TenantId UNIQUEIDENTIFIER,
	@CreatedByUserId UNIQUEIDENTIFIER = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF NOT EXISTS
	(
		SELECT 1
		FROM Location.AddressProviderConfiguration WITH (UPDLOCK, HOLDLOCK)
		WHERE TenantId = @TenantId AND ProviderCode = N'AzureMaps' AND IsDeleted = 0
	)
	BEGIN
		INSERT Location.AddressProviderConfiguration
		(
			AddressProviderConfigurationId, TenantId, ProviderCode, DisplayName,
			ServiceEndpoint, AutocompletePath, GeocodePath, ApiVersion, AuthenticationScope,
			MapsClientId, DefaultCountrySet, DefaultLanguage, MinimumQueryLength,
			DebounceMilliseconds, MaximumSuggestions, RequestTimeoutSeconds,
			IsDefault, IsEnabled, CreatedByUserId
		)
		VALUES
		(
			NEWID(), @TenantId, N'AzureMaps', N'Azure Maps',
			N'https://atlas.microsoft.com', N'/search/address/json?api-version=1.0&typeahead=true', N'/geocode', N'2025-01-01', N'https://atlas.microsoft.com/.default',
			NULL, N'US', N'en-US', 3,
			300, 8, 15,
			1, 1, @CreatedByUserId
		);
	END;
END;

GO

DECLARE @SeedTenantId UNIQUEIDENTIFIER;
DECLARE tenant_cursor CURSOR LOCAL FAST_FORWARD FOR
	SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0;
OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @SeedTenantId;
WHILE @@FETCH_STATUS = 0
BEGIN
	EXEC Location.EnsureAddressProviderConfiguration @TenantId = @SeedTenantId;
	FETCH NEXT FROM tenant_cursor INTO @SeedTenantId;
END;
CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;
