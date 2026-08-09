SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.EndorsementTypeAlias', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementTypeAlias
	(
		EndorsementTypeAliasId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementTypeAlias PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		LegacyTypeValue NVARCHAR(160) NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		CanonicalTypeCode NVARCHAR(100) NOT NULL,
		DescriptionContains NVARCHAR(200) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTypeAlias_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTypeAlias_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTypeAlias_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTypeAlias_IsDeleted DEFAULT 0,
		CONSTRAINT FK_EndorsementTypeAlias_Type FOREIGN KEY (TenantId, EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId, EndorsementTypeId)
	);

	CREATE UNIQUE INDEX UX_EndorsementTypeAlias_Match
		ON Policy.EndorsementTypeAlias(TenantId, LegacyTypeValue, CanonicalTypeCode, DescriptionContains)
		WHERE IsDeleted = 0;

	CREATE INDEX IX_EndorsementTypeAlias_Resolve
		ON Policy.EndorsementTypeAlias(TenantId, LegacyTypeValue, IsActive, IsDeleted, SortOrder);
END;

;WITH AliasSeed AS
(
	SELECT type.TenantId,N'Coverage Change' LegacyTypeValue,type.EndorsementTypeId,type.TypeCode CanonicalTypeCode,CAST(NULL AS NVARCHAR(200)) DescriptionContains,10 SortOrder
	FROM Policy.EndorsementType type
	WHERE type.TypeCode=N'CoverageChange' AND type.IsActive=1 AND type.IsDeleted=0
	UNION ALL
	SELECT type.TenantId,N'Add Insured',type.EndorsementTypeId,type.TypeCode,N'additional insured',20
	FROM Policy.EndorsementType type
	WHERE type.TypeCode=N'AdditionalInsured' AND type.IsActive=1 AND type.IsDeleted=0
	UNION ALL
	SELECT type.TenantId,N'Change Limit',type.EndorsementTypeId,type.TypeCode,N'increase',30
	FROM Policy.EndorsementType type
	WHERE type.TypeCode=N'LimitIncrease' AND type.IsActive=1 AND type.IsDeleted=0
	UNION ALL
	SELECT type.TenantId,N'Change Limit',type.EndorsementTypeId,type.TypeCode,N'decrease',40
	FROM Policy.EndorsementType type
	WHERE type.TypeCode=N'LimitDecrease' AND type.IsActive=1 AND type.IsDeleted=0
)
INSERT Policy.EndorsementTypeAlias
(
	EndorsementTypeAliasId,TenantId,LegacyTypeValue,EndorsementTypeId,CanonicalTypeCode,DescriptionContains,
	IsActive,SortOrder,CreatedDateUtc,IsDeleted
)
SELECT NEWID(),seed.TenantId,seed.LegacyTypeValue,seed.EndorsementTypeId,seed.CanonicalTypeCode,seed.DescriptionContains,
	   1,seed.SortOrder,SYSUTCDATETIME(),0
FROM AliasSeed seed
WHERE NOT EXISTS
(
	SELECT 1
	FROM Policy.EndorsementTypeAlias existing
	WHERE existing.TenantId=seed.TenantId
	  AND existing.LegacyTypeValue=seed.LegacyTypeValue
	  AND existing.CanonicalTypeCode=seed.CanonicalTypeCode
	  AND ISNULL(existing.DescriptionContains,N'')=ISNULL(seed.DescriptionContains,N'')
	  AND existing.IsDeleted=0
);

UPDATE endorsement
SET EndorsementType=resolved.CanonicalTypeCode,
	ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.PolicyEndorsement endorsement
CROSS APPLY
(
	SELECT TOP (1) alias.CanonicalTypeCode
	FROM Policy.EndorsementTypeAlias alias
	JOIN Policy.EndorsementType type
	  ON type.TenantId=alias.TenantId
	  AND type.EndorsementTypeId=alias.EndorsementTypeId
	 AND type.IsActive=1
	 AND type.IsDeleted=0
	WHERE alias.TenantId=endorsement.TenantId
	  AND alias.LegacyTypeValue=endorsement.EndorsementType
	  AND alias.IsActive=1
	  AND alias.IsDeleted=0
	  AND (alias.DescriptionContains IS NULL OR endorsement.Description LIKE N'%' + alias.DescriptionContains + N'%')
	ORDER BY CASE WHEN alias.DescriptionContains IS NULL THEN 1 ELSE 0 END,alias.SortOrder
) resolved
WHERE endorsement.IsDeleted=0
  AND endorsement.EndorsementType<>resolved.CanonicalTypeCode;
