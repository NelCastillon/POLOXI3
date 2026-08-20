SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @StagedImportStatus TABLE
(
	LookupTypeCode VARCHAR(100),
	ValueCode VARCHAR(100),
	DisplayName NVARCHAR(200),
	Description NVARCHAR(1000),
	SortOrder INT
);

INSERT INTO @StagedImportStatus
VALUES ('IMPORT_STATUS', 'STAGED', N'Staged', N'Parsed and staged successfully; awaiting governed validation and apply processing.', 25);

MERGE knowledge.LookupValue AS target
USING @StagedImportStatus AS source
ON source.LookupTypeCode = target.LookupTypeCode
AND source.ValueCode = target.ValueCode
AND target.TenantId IS NULL
WHEN MATCHED AND target.IsSystemDefined = 1 THEN
	UPDATE SET DisplayName = source.DisplayName,
			   Description = source.Description,
			   SortOrder = source.SortOrder,
			   IsActive = 1,
			   ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT (LookupValueId, LookupTypeCode, ValueCode, DisplayName, Description, SortOrder, TenantId, IsSystemDefined, IsActive, CreatedDateUtc)
	VALUES (NEWID(), source.LookupTypeCode, source.ValueCode, source.DisplayName, source.Description, source.SortOrder, NULL, 1, 1, SYSUTCDATETIME());
