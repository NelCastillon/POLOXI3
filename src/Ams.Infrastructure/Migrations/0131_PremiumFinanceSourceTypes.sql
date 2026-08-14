-- 0131_PremiumFinanceSourceTypes.sql
-- Database-backed source types for Premium Finance request creation. Idempotent.

SET NOCOUNT ON;

;WITH SourceTypeSeed AS
(
	SELECT * FROM (VALUES
		(N'Quote', N'Selected / accepted quote', N'Create a premium finance request from an eligible selected or accepted quote.', 1, 10),
		(N'Policy', N'Bound policy', N'Create a premium finance request from an eligible bound policy.', 0, 20),
		(N'Renewal', N'Renewal', N'Create a premium finance request from an eligible renewal record.', 0, 30)
	) seed(OptionCode, DisplayName, Description, IsDefault, SortOrder)
)
INSERT INTO Billing.PremiumFinanceReferenceOption
(
	PremiumFinanceReferenceOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName,
	Description, IsTerminal, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
)
SELECT NEWID(), tenant.TenantId, N'SourceType', seed.OptionCode, seed.DisplayName,
	seed.Description, 0, seed.IsDefault, 1, seed.SortOrder, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN SourceTypeSeed seed
WHERE tenant.IsDeleted = 0
  AND NOT EXISTS
  (
	SELECT 1
	FROM Billing.PremiumFinanceReferenceOption existing
	WHERE existing.TenantId = tenant.TenantId
	  AND existing.OptionGroupCode = N'SourceType'
	  AND existing.OptionCode = seed.OptionCode
  );
