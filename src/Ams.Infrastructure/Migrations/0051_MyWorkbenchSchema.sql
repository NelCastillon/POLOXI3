-- ============================================================
-- MIGRATION 0051: MY WORKBENCH SCHEMA
-- Creates persisted quick links and starter operational data for /workbench
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'OPS')
BEGIN
	EXEC('CREATE SCHEMA OPS');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Core')
BEGIN
	EXEC('CREATE SCHEMA Core');
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('OPS') AND name = 'WorkbenchQuickLink')
BEGIN
	CREATE TABLE OPS.WorkbenchQuickLink (
		QuickLinkId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId         UNIQUEIDENTIFIER NOT NULL,
		LinkCode         NVARCHAR(80)     NOT NULL,
		Label            NVARCHAR(160)    NOT NULL,
		IconCssClass     NVARCHAR(120)    NOT NULL,
		Url              NVARCHAR(300)    NOT NULL,
		CategoryCode     NVARCHAR(80)     NOT NULL,
		SortOrder        INT              NOT NULL DEFAULT 0,
		IsActive         BIT              NOT NULL DEFAULT 1,
		CreatedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
		CreatedByUserId  UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc  DATETIME2        NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted        BIT              NOT NULL DEFAULT 0
	);

	CREATE UNIQUE NONCLUSTERED INDEX IX_WorkbenchQuickLink_Code ON OPS.WorkbenchQuickLink(TenantId, LinkCode) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_WorkbenchQuickLink_Tenant ON OPS.WorkbenchQuickLink(TenantId, IsDeleted, IsActive, SortOrder);
END
GO

DECLARE @TenantId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 TenantId FROM Core.Tenant ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000001');

DECLARE @Links TABLE
(
	LinkCode NVARCHAR(80),
	Label NVARCHAR(160),
	IconCssClass NVARCHAR(120),
	Url NVARCHAR(300),
	CategoryCode NVARCHAR(80),
	SortOrder INT
);

INSERT INTO @Links VALUES
(N'MY_TASKS', N'My Tasks', N'bi bi-check2-square', N'/workbench/tasks', N'Work', 10),
(N'MY_CALENDAR', N'My Calendar', N'bi bi-calendar-event', N'/workbench/calendar', N'Work', 20),
(N'MY_ACTIVITIES', N'My Activities', N'bi bi-activity', N'/workbench/activities', N'Work', 30),
(N'PRODUCER_WORKBENCH', N'Producer Workbench', N'bi bi-briefcase', N'/workbench/producer', N'Role', 40),
(N'CSR_WORKBENCH', N'CSR Workbench', N'bi bi-headset', N'/workbench/csr', N'Role', 50),
(N'SERVICE_MANAGER', N'Service Manager', N'bi bi-kanban', N'/workbench/service-manager', N'Role', 60),
(N'ACCOUNTING', N'Accounting', N'bi bi-calculator', N'/workbench/accounting', N'Role', 70),
(N'MARKETING', N'Marketing', N'bi bi-megaphone', N'/workbench/marketing', N'Role', 80),
(N'OPERATIONS', N'Operations', N'bi bi-diagram-3', N'/workbench/operations', N'Role', 90);

INSERT INTO OPS.WorkbenchQuickLink (QuickLinkId, TenantId, LinkCode, Label, IconCssClass, Url, CategoryCode, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, l.LinkCode, l.Label, l.IconCssClass, l.Url, l.CategoryCode, l.SortOrder, 1, SYSUTCDATETIME(), 0
FROM @Links l
WHERE NOT EXISTS
(
	SELECT 1
	FROM OPS.WorkbenchQuickLink q
	WHERE q.TenantId = @TenantId
	  AND q.LinkCode = l.LinkCode
	  AND q.IsDeleted = 0
);
GO
