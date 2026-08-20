IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL
   AND EXISTS
   (
	   SELECT 1
	   FROM sys.check_constraints
	   WHERE parent_object_id = OBJECT_ID(N'Client.Account')
		 AND name = N'CK_Account_AccountType'
   )
BEGIN
	ALTER TABLE Client.Account DROP CONSTRAINT CK_Account_AccountType;
END;
GO

IF OBJECT_ID(N'Client.AccountType', N'U') IS NOT NULL
BEGIN
	INSERT INTO Client.AccountType
	(AccountTypeId, TenantId, TypeCode, TypeName, Category, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), tenant.TenantId, N'Both', N'Personal & Commercial', N'Both',
		   N'Account with both personal-lines and commercial-lines insurance interests.', 0, 1, 30, SYSUTCDATETIME(), 0
	FROM
	(
		SELECT DISTINCT TenantId FROM Client.AccountType WHERE IsDeleted = 0
		UNION
		SELECT DISTINCT TenantId FROM CRM.Lead WHERE IsDeleted = 0
	) tenant
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM Client.AccountType existing
		WHERE existing.TenantId = tenant.TenantId
		  AND existing.TypeCode = N'Both'
		  AND existing.IsDeleted = 0
	);
END;
GO
