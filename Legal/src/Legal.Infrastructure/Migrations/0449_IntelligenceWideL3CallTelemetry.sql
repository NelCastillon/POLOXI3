SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0449: Per-call L3 telemetry on the processing ledger.
--   The last measured run (146 IN_SCOPE parents) dispatched 51 L3 batches + 8 focused retries with an average
--   fill of 2.86 parents per call against L3BatchSize=8, and more than half the parents returned INFORMATION_GAP.
--   Per-call duration/tokens were only persisted on Legal_MatterAtomicProposition (accepted propositions), so
--   calls that returned only gaps/thin results left no latency record. Persist the provider call facts on every
--   POLOXI.Legal_WideBranchProcessing row so p50/p95 latency, batch fill, token cost and seconds-per-accepted-
--   proposition can be computed from one query. Additive, nullable; no behavior change.
--   One provider call is shared by all parents in a batch: group by ProviderRequestId to count distinct calls.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_WideBranchProcessing',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'ParentsInBatch') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD ParentsInBatch INT NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'BatchTierCode') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD BatchTierCode NVARCHAR(40) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'DurationMs') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD DurationMs INT NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'InputTokenCount') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD InputTokenCount INT NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'OutputTokenCount') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD OutputTokenCount INT NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'ModelCode') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD ModelCode NVARCHAR(120) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'ProviderCode') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD ProviderCode NVARCHAR(60) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'ProviderRequestId') IS NULL
		ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD ProviderRequestId NVARCHAR(200) NULL;

	IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_WideBranchProcessing_Execution_Request' AND object_id=OBJECT_ID(N'POLOXI.Legal_WideBranchProcessing'))
		CREATE INDEX IX_Legal_WideBranchProcessing_Execution_Request ON POLOXI.Legal_WideBranchProcessing(WideExecutionId,ProviderRequestId) INCLUDE(DurationMs,InputTokenCount,OutputTokenCount,PropositionCount,ProcessingResultCode) WHERE IsDeleted=0;
END;

COMMIT TRANSACTION;
