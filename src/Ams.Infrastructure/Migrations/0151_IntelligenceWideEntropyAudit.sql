SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- EPH V2.5: full entropy audit trail per information round.
-- Raw Shannon entropy (bits) is only interpretable against the maximum
-- entropy of the SAME distribution: normalized percentages can move while
-- ActualInformationGain reads ~0 bits when the measured population changes
-- between measurements. Persisting MaxEntropy (log2 N) and the population
-- count before/after makes every round auditable: whether a "0 bits" round
-- is genuine, rounding, or an incomparable-population artifact.
-- ============================================================================

IF OBJECT_ID(N'EPH.WideInformationRound',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'EPH.WideInformationRound',N'MaxEntropyBefore') IS NULL
		ALTER TABLE EPH.WideInformationRound ADD MaxEntropyBefore DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'EPH.WideInformationRound',N'MaxEntropyAfter') IS NULL
		ALTER TABLE EPH.WideInformationRound ADD MaxEntropyAfter DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'EPH.WideInformationRound',N'PopulationCountBefore') IS NULL
		ALTER TABLE EPH.WideInformationRound ADD PopulationCountBefore INT NULL;
	IF COL_LENGTH(N'EPH.WideInformationRound',N'PopulationCountAfter') IS NULL
		ALTER TABLE EPH.WideInformationRound ADD PopulationCountAfter INT NULL;
END;

COMMIT TRANSACTION;
