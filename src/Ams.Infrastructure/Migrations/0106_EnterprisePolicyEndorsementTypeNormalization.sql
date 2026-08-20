SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.PolicyEndorsement', N'U') IS NOT NULL
   AND OBJECT_ID(N'Policy.EndorsementType', N'U') IS NOT NULL
BEGIN
	;WITH UniqueTenantTypeName AS
	(
		SELECT TenantId, TypeName, MAX(TypeCode) TypeCode
		FROM Policy.EndorsementType
		WHERE IsDeleted = 0
		  AND IsActive = 1
		GROUP BY TenantId, TypeName
		HAVING COUNT(*) = 1
	)
	UPDATE endorsement
	SET EndorsementType = catalog.TypeCode,
		ModifiedDateUtc = SYSUTCDATETIME()
	FROM Policy.PolicyEndorsement endorsement
	JOIN UniqueTenantTypeName catalog
	  ON catalog.TenantId = endorsement.TenantId
	 AND catalog.TypeName = endorsement.EndorsementType
	WHERE endorsement.IsDeleted = 0
	  AND endorsement.EndorsementType <> catalog.TypeCode;
END;
