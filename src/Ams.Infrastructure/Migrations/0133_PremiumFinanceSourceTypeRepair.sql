-- 0133_PremiumFinanceSourceTypeRepair.sql
-- Repairs database-backed Premium Finance source types for all active tenants. Idempotent.

SET NOCOUNT ON;

DECLARE @SourceTypes TABLE
(
	OptionCode NVARCHAR(100) NOT NULL,
	DisplayName NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NOT NULL,
	IsDefault BIT NOT NULL,
	SortOrder INT NOT NULL
);

INSERT INTO @SourceTypes (OptionCode, DisplayName, Description, IsDefault, SortOrder)
VALUES
	(N'Quote', N'Selected / accepted quote', N'Create a premium finance request from an eligible selected or accepted quote.', 1, 10),
	(N'Policy', N'Bound policy', N'Create a premium finance request from an eligible bound policy.', 0, 20),
	(N'Renewal', N'Renewal', N'Create a premium finance request from an eligible renewal record.', 0, 30);

UPDATE existing
SET
	existing.DisplayName = source.DisplayName,
	existing.Description = source.Description,
	existing.IsTerminal = 0,
	existing.IsDefault = source.IsDefault,
	existing.IsActive = 1,
	existing.SortOrder = source.SortOrder,
	existing.ModifiedDateUtc = SYSUTCDATETIME(),
	existing.IsDeleted = 0
FROM Billing.PremiumFinanceReferenceOption existing
JOIN Core.Tenant tenant
	ON tenant.TenantId = existing.TenantId
	AND tenant.IsDeleted = 0
JOIN @SourceTypes source
	ON source.OptionCode = existing.OptionCode
WHERE existing.OptionGroupCode = N'SourceType';

INSERT INTO Billing.PremiumFinanceReferenceOption
(
	PremiumFinanceReferenceOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName,
	Description, IsTerminal, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
)
SELECT
	NEWID(), tenant.TenantId, N'SourceType', source.OptionCode, source.DisplayName,
	source.Description, 0, source.IsDefault, 1, source.SortOrder, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN @SourceTypes source
WHERE tenant.IsDeleted = 0
	AND NOT EXISTS
	(
		SELECT 1
		FROM Billing.PremiumFinanceReferenceOption existing
		WHERE existing.TenantId = tenant.TenantId
			AND existing.OptionGroupCode = N'SourceType'
			AND existing.OptionCode = source.OptionCode
	);
