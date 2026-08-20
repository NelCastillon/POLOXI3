SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'AI.SearchDocument',N'U') IS NOT NULL AND OBJECT_ID(N'Search.EntityProjection',N'U') IS NOT NULL
BEGIN
	UPDATE projection SET NavigationRoute=CONCAT(N'/client/contacts/',projection.EntityId),ModifiedDateUtc=SYSUTCDATETIME()
	FROM Search.EntityProjection projection
	WHERE projection.EntityTypeCode=N'Contact'
	  AND projection.IsDeleted=0
	  AND projection.NavigationRoute<>CONCAT(N'/client/contacts/',projection.EntityId);

	UPDATE projection
	SET SearchText=CONCAT_WS(N' ',account.AccountNumber,account.AccountName,account.DbaName,account.MainEmail,account.MainPhone,account.Industry,account.Website,contactEvidence.ContactSearchText),
		NormalizedFieldsJson=(SELECT account.AccountName DisplayName,CONCAT_WS(N' ',account.AccountNumber,account.AccountName,account.DbaName,account.MainEmail,account.MainPhone,account.Industry,account.Website,contactEvidence.ContactSearchText) SearchText,account.AccountName BusinessName,account.MainEmail Email,account.MainPhone Phone,account.DbaName,account.Industry,contactEvidence.ContactSearchText Contacts FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
		SourceModifiedDateUtc=COALESCE(account.ModifiedDateUtc,account.CreatedDateUtc,contactEvidence.LastContactModifiedDateUtc),
		ModifiedDateUtc=SYSUTCDATETIME()
	FROM Search.EntityProjection projection
	JOIN Client.Account account ON account.TenantId=projection.TenantId AND account.AccountId=projection.EntityId AND account.IsDeleted=0
	OUTER APPLY(SELECT STRING_AGG(CONCAT_WS(N' ',contact.FirstName,contact.LastName,contact.Email,contact.Phone,contact.JobTitle),N' ') WITHIN GROUP(ORDER BY contact.LastName,contact.FirstName) ContactSearchText,MAX(COALESCE(contact.ModifiedDateUtc,contact.CreatedDateUtc)) LastContactModifiedDateUtc FROM Client.Contact contact WHERE contact.TenantId=account.TenantId AND contact.AccountId=account.AccountId AND contact.IsDeleted=0 AND (contact.StatusCode IS NULL OR contact.StatusCode=N'Active')) contactEvidence
	WHERE projection.EntityTypeCode=N'Account'
	  AND projection.IsDeleted=0;

	;WITH SearchSource AS
	(
		SELECT projection.TenantId,
			   projection.EntityTypeCode,
			   projection.EntityId,
			   projection.SourceSchemaName ModuleCode,
			   projection.DisplayName Title,
			   COALESCE(projection.SearchText,N'') ContentText,
			   CONCAT_WS(N' ',projection.DisplayName,projection.SecondaryText,projection.ExactIdentifiersJson) Keywords,
			   projection.SourceModifiedDateUtc,
			   CONVERT(char(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONCAT_WS(N'|',projection.DisplayName,projection.SecondaryText,projection.SearchText,projection.NormalizedFieldsJson,projection.ExactIdentifiersJson))),2) ContentHash
		FROM Search.EntityProjection projection
		WHERE projection.EntityTypeCode IN(N'Account',N'Contact',N'Lead',N'Submission',N'Policy',N'Claim',N'Document',N'Certificate',N'Carrier',N'Location',N'Vehicle',N'ClaimParty',N'CommissionLine')
		  AND projection.IsActive=1
		  AND projection.IsDeleted=0
		  AND (projection.EntityTypeCode<>N'Contact' OR NOT EXISTS(SELECT 1 FROM Client.Contact contact WHERE contact.TenantId=projection.TenantId AND contact.ContactId=projection.EntityId AND contact.IsDeleted=0 AND contact.StatusCode IS NOT NULL AND contact.StatusCode<>N'Active'))
	)
	MERGE AI.SearchDocument target USING SearchSource source
	ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityId=source.EntityId AND target.IsDeleted=0
	WHEN MATCHED AND (target.ContentHash<>source.ContentHash OR target.SourceModifiedDateUtc<>source.SourceModifiedDateUtc OR target.SourceModifiedDateUtc IS NULL) THEN
		UPDATE SET ModuleCode=source.ModuleCode,
				   Title=source.Title,
				   ContentText=source.ContentText,
				   Keywords=source.Keywords,
				   SecurityScopeJson=N'{"permissionCode":"Intelligence.Search"}',
				   ContentHash=source.ContentHash,
				   SourceModifiedDateUtc=source.SourceModifiedDateUtc,
				   SourceCreatedDateUtc=COALESCE(target.SourceCreatedDateUtc,source.SourceModifiedDateUtc),
				   IndexedDateUtc=SYSUTCDATETIME(),
				   ModifiedDateUtc=SYSUTCDATETIME(),
				   IsDeleted=0
	WHEN NOT MATCHED THEN
		INSERT(SearchDocumentId,TenantId,EntityTypeCode,EntityId,ModuleCode,Title,ContentText,Keywords,ConceptIdsJson,SecurityScopeJson,ContentHash,IndexedDateUtc,SourceModifiedDateUtc,SourceCreatedDateUtc,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.EntityId,source.ModuleCode,source.Title,source.ContentText,source.Keywords,N'[]',N'{"permissionCode":"Intelligence.Search"}',source.ContentHash,SYSUTCDATETIME(),source.SourceModifiedDateUtc,source.SourceModifiedDateUtc,SYSUTCDATETIME(),0);

	UPDATE document SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME()
	FROM AI.SearchDocument document
	JOIN Client.Contact contact ON contact.TenantId=document.TenantId AND contact.ContactId=document.EntityId
	WHERE document.EntityTypeCode=N'Contact'
	  AND document.IsDeleted=0
	  AND contact.IsDeleted=0
	  AND contact.StatusCode IS NOT NULL
	  AND contact.StatusCode<>N'Active';

	IF OBJECT_ID(N'IAM.RolePermission',N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Role',N'U') IS NOT NULL AND OBJECT_ID(N'AI.SearchPermission',N'U') IS NOT NULL
	BEGIN
		MERGE AI.SearchPermission target USING
		(
			SELECT DISTINCT document.TenantId,document.SearchDocumentId,N'ROLE' PrincipalTypeCode,rolePermission.RoleId PrincipalId,N'READ' PermissionCode
			FROM AI.SearchDocument document
			JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
			JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=document.TenantId AND rolePermission.PermissionCode=projection.PermissionCode AND rolePermission.IsDeleted=0
			JOIN IAM.Role role ON role.TenantId=rolePermission.TenantId AND role.RoleId=rolePermission.RoleId AND role.IsDeleted=0
			WHERE document.IsDeleted=0
			  AND projection.EntityTypeCode IN(N'Account',N'Contact',N'Lead',N'Submission',N'Policy',N'Claim',N'Document',N'Certificate',N'Carrier',N'Location',N'Vehicle',N'ClaimParty',N'CommissionLine')
		) source
		ON target.TenantId=source.TenantId AND target.SearchDocumentId=source.SearchDocumentId AND target.PrincipalTypeCode=source.PrincipalTypeCode AND target.PrincipalId=source.PrincipalId AND target.PermissionCode=source.PermissionCode
		WHEN MATCHED AND target.IsDeleted=1 THEN
			UPDATE SET IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
		WHEN NOT MATCHED THEN
			INSERT(SearchPermissionId,TenantId,SearchDocumentId,PrincipalTypeCode,PrincipalId,PermissionCode,CreatedDateUtc,IsDeleted)
			VALUES(NEWID(),source.TenantId,source.SearchDocumentId,source.PrincipalTypeCode,source.PrincipalId,source.PermissionCode,SYSUTCDATETIME(),0);
	END;
END;

