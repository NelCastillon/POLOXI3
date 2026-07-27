IF OBJECT_ID(N'CRM.PhoneComplianceProfile', N'U') IS NOT NULL
BEGIN
	UPDATE CRM.PhoneComplianceProfile
	SET OverallStatusCode = N'PendingScreening',
		IsCallAllowed = 0,
		IsSmsAllowed = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE IsDeleted = 0
	  AND NOT EXISTS
	  (
		  SELECT 1
		  FROM CRM.PhoneScreeningResult r
		  WHERE r.TenantId = CRM.PhoneComplianceProfile.TenantId
			AND r.PhoneComplianceProfileId = CRM.PhoneComplianceProfile.PhoneComplianceProfileId
			AND r.ResultCode = N'Clear'
			AND r.ScreenedDateUtc <= SYSUTCDATETIME()
			AND COALESCE(r.ValidThroughDateUtc, DATEADD(DAY, 31, r.ScreenedDateUtc)) > SYSUTCDATETIME()
			AND r.IsDeleted = 0
	  )
	  AND NOT EXISTS
	  (
		  SELECT 1
		  FROM CRM.PhoneSuppression s
		  WHERE s.TenantId = CRM.PhoneComplianceProfile.TenantId
			AND s.PhoneComplianceProfileId = CRM.PhoneComplianceProfile.PhoneComplianceProfileId
			AND s.StatusCode = N'Active'
			AND s.EffectiveDateUtc <= SYSUTCDATETIME()
			AND (s.ExpirationDateUtc IS NULL OR s.ExpirationDateUtc > SYSUTCDATETIME())
			AND s.IsDeleted = 0
	  );
END;
GO

IF OBJECT_ID(N'CRM.PhoneEntityLink', N'U') IS NOT NULL
BEGIN
	;WITH RankedLinks AS
	(
		SELECT PhoneEntityLinkId,
			   ROW_NUMBER() OVER
			   (
				   PARTITION BY TenantId, EntityTypeCode, EntityId
				   ORDER BY CreatedDateUtc DESC, PhoneEntityLinkId DESC
			   ) AS RowNumber
		FROM CRM.PhoneEntityLink
		WHERE IsDeleted = 0
	)
	UPDATE link
	SET IsDeleted = 1,
		ModifiedDateUtc = SYSUTCDATETIME()
	FROM CRM.PhoneEntityLink link
	JOIN RankedLinks ranked ON ranked.PhoneEntityLinkId = link.PhoneEntityLinkId
	WHERE ranked.RowNumber > 1;

	IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneEntityLink') AND name = N'UX_PhoneEntityLink_Entity_Profile')
		DROP INDEX UX_PhoneEntityLink_Entity_Profile ON CRM.PhoneEntityLink;

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneEntityLink') AND name = N'UX_PhoneEntityLink_ActiveEntity')
		CREATE UNIQUE INDEX UX_PhoneEntityLink_ActiveEntity
			ON CRM.PhoneEntityLink(TenantId, EntityTypeCode, EntityId) WHERE IsDeleted = 0;
END;
GO
