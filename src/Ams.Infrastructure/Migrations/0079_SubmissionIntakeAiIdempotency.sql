SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Submissions.SubmissionIntake', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Submissions.SubmissionIntake', N'SourceIdempotencyKey') IS NULL
		ALTER TABLE Submissions.SubmissionIntake ADD SourceIdempotencyKey NVARCHAR(240) NULL;

	EXEC sys.sp_executesql N'
		;WITH duplicates AS
		(
			SELECT IntakeId, ROW_NUMBER() OVER (PARTITION BY TenantId, SourceIdempotencyKey ORDER BY CreatedDateUtc, IntakeId) AS DuplicateRank
			FROM Submissions.SubmissionIntake
			WHERE SourceIdempotencyKey IS NOT NULL
		)
		UPDATE intake
		SET SourceIdempotencyKey = NULL
		FROM Submissions.SubmissionIntake intake
		JOIN duplicates duplicate ON duplicate.IntakeId = intake.IntakeId
		WHERE duplicate.DuplicateRank > 1;';

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntake') AND name = N'UX_SubmissionIntake_Tenant_SourceIdempotency')
		EXEC sys.sp_executesql N'
			CREATE UNIQUE INDEX UX_SubmissionIntake_Tenant_SourceIdempotency
				ON Submissions.SubmissionIntake(TenantId, SourceIdempotencyKey)
				WHERE SourceIdempotencyKey IS NOT NULL AND IsDeleted = 0;';
END;

COMMIT TRANSACTION;
