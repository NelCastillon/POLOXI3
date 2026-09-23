SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ParentDecisionSessionId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD ParentDecisionSessionId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'POLOXI.FK_Legal_DecisionSession_Parent', N'F') IS NULL
	EXEC(N'ALTER TABLE POLOXI.Legal_DecisionSession ADD CONSTRAINT FK_Legal_DecisionSession_Parent
		FOREIGN KEY (ParentDecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId);');

IF OBJECT_ID(N'POLOXI.IX_Legal_DecisionSession_Parent', N'IX') IS NULL
	EXEC(N'CREATE INDEX IX_Legal_DecisionSession_Parent
		ON POLOXI.Legal_DecisionSession (TenantId, ParentDecisionSessionId, CreatedDateUtc)
		WHERE IsDeleted = 0 AND ParentDecisionSessionId IS NOT NULL;');

IF OBJECT_ID(N'POLOXI.Legal_DecisionClarification', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionClarification
	(
		DecisionClarificationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionClarification PRIMARY KEY DEFAULT NEWID(),
		DecisionSessionId UNIQUEIDENTIFIER NOT NULL,
		ParentDecisionSessionId UNIQUEIDENTIFIER NOT NULL,
		Question NVARCHAR(MAX) NULL,
		Target NVARCHAR(300) NULL,
		Answer NVARCHAR(500) NOT NULL,
		DecisionBranchId UNIQUEIDENTIFIER NULL,
		DecisionCandidateId UNIQUEIDENTIFIER NULL,
		DecisionGraphNodeId UNIQUEIDENTIFIER NULL,
		DecisionGraphEdgeId UNIQUEIDENTIFIER NULL,
		ScopeCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionClarification_Scope DEFAULT N'SESSION_LINEAGE',
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionClarification_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionClarification_IsDeleted DEFAULT 0,
		CONSTRAINT FK_Legal_DecisionClarification_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
		CONSTRAINT FK_Legal_DecisionClarification_Parent FOREIGN KEY (ParentDecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
		CONSTRAINT CK_Legal_DecisionClarification_Answer CHECK (LEN(LTRIM(RTRIM(Answer))) > 0)
	);
END;

IF OBJECT_ID(N'POLOXI.IX_Legal_DecisionClarification_Lineage', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionClarification_Lineage
		ON POLOXI.Legal_DecisionClarification (TenantId, ParentDecisionSessionId, CreatedDateUtc, DecisionClarificationId)
		INCLUDE (DecisionSessionId, Target, ScopeCode, DecisionBranchId, DecisionCandidateId, DecisionGraphNodeId, DecisionGraphEdgeId)
		WHERE IsDeleted = 0;

IF OBJECT_ID(N'POLOXI.IX_Legal_DecisionClarification_Session', N'IX') IS NULL
	CREATE UNIQUE INDEX IX_Legal_DecisionClarification_Session
		ON POLOXI.Legal_DecisionClarification (TenantId, DecisionSessionId)
		WHERE IsDeleted = 0;

COMMIT TRANSACTION;
