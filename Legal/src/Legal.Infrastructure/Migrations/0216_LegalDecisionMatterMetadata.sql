SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — enrich the Acme v. Globex demo matter (M1) with structured Type / Jurisdiction /
-- Posture metadata and a fuller Description, per the three-field decision-contract model:
--   Type        = What kind of dispute?      (hierarchical: Type + Subtype)
--   Jurisdiction= Which legal system controls? (Court System / State / Court Level / County / Governing Law)
--   Posture     = What decision is being asked NOW? (motion, movant, target, requested disposition)
-- The Legal_DecisionMatter columns (MatterTypeCode, Jurisdiction, Posture, Description) remain the
-- source of truth; this migration only UPDATEs values, so it is safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @M1     UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000001';

IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M1 AND TenantId = @Tenant)
	UPDATE POLOXI.Legal_DecisionMatter
	   SET MatterTypeCode = N'Civil Litigation - Contract / Commercial Dispute',
		   Jurisdiction   = N'United States - California State - Superior Court - Los Angeles County - Governing law: California',
		   Posture        = N'Summary Judgment - Movant: Acme - Target: Contract Claim - Alt. relief: Summary Adjudication - Requested disposition: Judgment for Acme without trial',
		   Description    = N'Type: Civil Litigation (Contract / Commercial Dispute).' + NCHAR(10)
						  + N'Jurisdiction: United States, California State Court, Los Angeles County Superior Court; governing substantive law: California.' + NCHAR(10)
						  + N'Posture: Summary Judgment. Moving party: Acme. Responding party: Globex. Motion target: contract claim (alternatively summary adjudication of the non-use covenant). Requested disposition: judgment for Acme without trial.' + NCHAR(10)
						  + N'Core question: not merely who has the better case, but whether the court can resolve the contract claim at this procedural stage - i.e. whether there is a triable issue of material fact under Cal. Rule of Court 3.1350 over whether Globex used the confidential specifications after the NDA terminated, which the non-use covenant expressly survives.',
		   ModifiedDateUtc = SYSUTCDATETIME(),
		   ModifiedByUserId = @User
	 WHERE DecisionMatterId = @M1 AND TenantId = @Tenant;

COMMIT TRANSACTION;
