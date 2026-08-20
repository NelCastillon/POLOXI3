IF OBJECT_ID(N'CRM.PhoneEntityLink', N'U') IS NOT NULL
   AND OBJECT_ID(N'CRM.LeadContact', N'U') IS NOT NULL
BEGIN
	UPDATE contactLink
	SET IsDeleted = 1,
		ModifiedDateUtc = SYSUTCDATETIME()
	FROM CRM.PhoneEntityLink contactLink
	JOIN CRM.LeadContact contact
	  ON contact.TenantId = contactLink.TenantId
	 AND contact.ContactId = contactLink.EntityId
	 AND contact.IsDeleted = 0
	JOIN CRM.PhoneEntityLink leadLink
	  ON leadLink.TenantId = contactLink.TenantId
	 AND leadLink.PhoneComplianceProfileId = contactLink.PhoneComplianceProfileId
	 AND leadLink.EntityTypeCode = N'Lead'
	 AND leadLink.EntityId = contact.LeadId
	 AND leadLink.IsDeleted = 0
	WHERE contactLink.EntityTypeCode = N'LeadContact'
	  AND contactLink.IsDeleted = 0;
END;
GO
