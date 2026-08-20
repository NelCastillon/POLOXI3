-- AgencyBinder Address & Location Engine: geography reference data for manual fallback.
-- Hybrid model: statically seeded US states, plus city/postal-code rows auto-learned from
-- every successful Azure Maps resolution via Location.LearnGeoResolution.
-- Geography facts are global reference data (not tenant-scoped).

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Location') EXEC(N'CREATE SCHEMA Location');
GO

IF OBJECT_ID(N'Location.GeoState') IS NULL
BEGIN
	CREATE TABLE Location.GeoState
	(
		GeoStateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GeoState PRIMARY KEY DEFAULT NEWID(),
		CountryCode NVARCHAR(2) NOT NULL,
		StateCode NVARCHAR(3) NOT NULL,
		StateName NVARCHAR(100) NOT NULL,
		DisplayOrder INT NOT NULL CONSTRAINT DF_GeoState_DisplayOrder DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_GeoState_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GeoState_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_GeoState_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_GeoState_Country_State UNIQUE (CountryCode, StateCode)
	);
END;

IF OBJECT_ID(N'Location.GeoCity') IS NULL
BEGIN
	CREATE TABLE Location.GeoCity
	(
		GeoCityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GeoCity PRIMARY KEY DEFAULT NEWID(),
		CountryCode NVARCHAR(2) NOT NULL,
		StateCode NVARCHAR(3) NOT NULL,
		CityName NVARCHAR(120) NOT NULL,
		County NVARCHAR(120) NULL,
		SourceCode NVARCHAR(30) NOT NULL CONSTRAINT DF_GeoCity_SourceCode DEFAULT N'Learned',
		IsActive BIT NOT NULL CONSTRAINT DF_GeoCity_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GeoCity_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_GeoCity_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_GeoCity_Country_State_City UNIQUE (CountryCode, StateCode, CityName)
	);

	CREATE INDEX IX_GeoCity_CityName ON Location.GeoCity (CityName) INCLUDE (CountryCode, StateCode, County) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'Location.GeoPostalCode') IS NULL
BEGIN
	CREATE TABLE Location.GeoPostalCode
	(
		GeoPostalCodeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GeoPostalCode PRIMARY KEY DEFAULT NEWID(),
		GeoCityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_GeoPostalCode_GeoCity REFERENCES Location.GeoCity (GeoCityId),
		PostalCode NVARCHAR(12) NOT NULL,
		Latitude DECIMAL(9,6) NULL,
		Longitude DECIMAL(9,6) NULL,
		SourceCode NVARCHAR(30) NOT NULL CONSTRAINT DF_GeoPostalCode_SourceCode DEFAULT N'Learned',
		IsActive BIT NOT NULL CONSTRAINT DF_GeoPostalCode_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GeoPostalCode_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_GeoPostalCode_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_GeoPostalCode_City_Postal UNIQUE (GeoCityId, PostalCode)
	);

	CREATE INDEX IX_GeoPostalCode_PostalCode ON Location.GeoPostalCode (PostalCode) WHERE IsDeleted = 0;
END;
GO

-- Seed US states, DC, and territories (static reference data).
MERGE Location.GeoState AS target
USING (VALUES
	(N'US', N'AL', N'Alabama', 10), (N'US', N'AK', N'Alaska', 20), (N'US', N'AZ', N'Arizona', 30),
	(N'US', N'AR', N'Arkansas', 40), (N'US', N'CA', N'California', 50), (N'US', N'CO', N'Colorado', 60),
	(N'US', N'CT', N'Connecticut', 70), (N'US', N'DE', N'Delaware', 80), (N'US', N'DC', N'District of Columbia', 90),
	(N'US', N'FL', N'Florida', 100), (N'US', N'GA', N'Georgia', 110), (N'US', N'HI', N'Hawaii', 120),
	(N'US', N'ID', N'Idaho', 130), (N'US', N'IL', N'Illinois', 140), (N'US', N'IN', N'Indiana', 150),
	(N'US', N'IA', N'Iowa', 160), (N'US', N'KS', N'Kansas', 170), (N'US', N'KY', N'Kentucky', 180),
	(N'US', N'LA', N'Louisiana', 190), (N'US', N'ME', N'Maine', 200), (N'US', N'MD', N'Maryland', 210),
	(N'US', N'MA', N'Massachusetts', 220), (N'US', N'MI', N'Michigan', 230), (N'US', N'MN', N'Minnesota', 240),
	(N'US', N'MS', N'Mississippi', 250), (N'US', N'MO', N'Missouri', 260), (N'US', N'MT', N'Montana', 270),
	(N'US', N'NE', N'Nebraska', 280), (N'US', N'NV', N'Nevada', 290), (N'US', N'NH', N'New Hampshire', 300),
	(N'US', N'NJ', N'New Jersey', 310), (N'US', N'NM', N'New Mexico', 320), (N'US', N'NY', N'New York', 330),
	(N'US', N'NC', N'North Carolina', 340), (N'US', N'ND', N'North Dakota', 350), (N'US', N'OH', N'Ohio', 360),
	(N'US', N'OK', N'Oklahoma', 370), (N'US', N'OR', N'Oregon', 380), (N'US', N'PA', N'Pennsylvania', 390),
	(N'US', N'RI', N'Rhode Island', 400), (N'US', N'SC', N'South Carolina', 410), (N'US', N'SD', N'South Dakota', 420),
	(N'US', N'TN', N'Tennessee', 430), (N'US', N'TX', N'Texas', 440), (N'US', N'UT', N'Utah', 450),
	(N'US', N'VT', N'Vermont', 460), (N'US', N'VA', N'Virginia', 470), (N'US', N'WA', N'Washington', 480),
	(N'US', N'WV', N'West Virginia', 490), (N'US', N'WI', N'Wisconsin', 500), (N'US', N'WY', N'Wyoming', 510),
	(N'US', N'AS', N'American Samoa', 600), (N'US', N'GU', N'Guam', 610), (N'US', N'MP', N'Northern Mariana Islands', 620),
	(N'US', N'PR', N'Puerto Rico', 630), (N'US', N'VI', N'U.S. Virgin Islands', 640)
) AS source (CountryCode, StateCode, StateName, DisplayOrder)
ON target.CountryCode = source.CountryCode AND target.StateCode = source.StateCode
WHEN NOT MATCHED THEN
	INSERT (GeoStateId, CountryCode, StateCode, StateName, DisplayOrder)
	VALUES (NEWID(), source.CountryCode, source.StateCode, source.StateName, source.DisplayOrder);
GO

-- Upserts city/postal-code rows from a successful provider resolution so the local
-- geography cache grows with usage (hybrid learn model).
CREATE OR ALTER PROCEDURE Location.LearnGeoResolution
	@CountryCode NVARCHAR(2),
	@StateCode NVARCHAR(3),
	@CityName NVARCHAR(120),
	@County NVARCHAR(120) = NULL,
	@PostalCode NVARCHAR(12) = NULL,
	@Latitude DECIMAL(9,6) = NULL,
	@Longitude DECIMAL(9,6) = NULL,
	@UserId UNIQUEIDENTIFIER = NULL
AS
BEGIN
	SET NOCOUNT ON;

	SET @CountryCode = UPPER(LTRIM(RTRIM(ISNULL(@CountryCode, N'US'))));
	SET @StateCode = UPPER(LTRIM(RTRIM(@StateCode)));
	SET @CityName = LTRIM(RTRIM(@CityName));
	SET @PostalCode = NULLIF(LTRIM(RTRIM(@PostalCode)), N'');

	IF @CityName IS NULL OR @CityName = N'' OR @StateCode IS NULL OR @StateCode = N'' RETURN;
	IF NOT EXISTS (SELECT 1 FROM Location.GeoState WHERE CountryCode = @CountryCode AND StateCode = @StateCode AND IsDeleted = 0) RETURN;

	DECLARE @GeoCityId UNIQUEIDENTIFIER;

	SELECT @GeoCityId = GeoCityId
	FROM Location.GeoCity WITH (UPDLOCK, HOLDLOCK)
	WHERE CountryCode = @CountryCode AND StateCode = @StateCode AND CityName = @CityName AND IsDeleted = 0;

	IF @GeoCityId IS NULL
	BEGIN
		SET @GeoCityId = NEWID();
		INSERT Location.GeoCity (GeoCityId, CountryCode, StateCode, CityName, County, SourceCode, CreatedByUserId)
		VALUES (@GeoCityId, @CountryCode, @StateCode, @CityName, @County, N'Learned', @UserId);
	END
	ELSE IF @County IS NOT NULL
	BEGIN
		UPDATE Location.GeoCity
		SET County = COALESCE(County, @County),
			ModifiedDateUtc = SYSUTCDATETIME(),
			ModifiedByUserId = @UserId
		WHERE GeoCityId = @GeoCityId AND County IS NULL;
	END;

	IF @PostalCode IS NOT NULL
	   AND NOT EXISTS (SELECT 1 FROM Location.GeoPostalCode WITH (UPDLOCK, HOLDLOCK) WHERE GeoCityId = @GeoCityId AND PostalCode = @PostalCode AND IsDeleted = 0)
	BEGIN
		INSERT Location.GeoPostalCode (GeoPostalCodeId, GeoCityId, PostalCode, Latitude, Longitude, SourceCode, CreatedByUserId)
		VALUES (NEWID(), @GeoCityId, @PostalCode, @Latitude, @Longitude, N'Learned', @UserId);
	END;
END;
