SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionFlipPoint',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DecisionFlipPoint',N'TargetCandidateId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionFlipPoint ADD TargetCandidateId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionFlipPoint',N'TargetCandidateCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionFlipPoint ADD TargetCandidateCode NVARCHAR(80) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionFlipPoint',N'PolarityCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionFlipPoint ADD PolarityCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_Polarity DEFAULT N'UNRESOLVED';
END;

COMMIT TRANSACTION;