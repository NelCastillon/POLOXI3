SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0439: EMPLOYMENT pack evidence-type linkage (corrects 0438, whose employment rules targeted dimension codes
--   and therefore inserted nothing). Uses the 12 real EMPLOYMENT EvidenceTypeCode rows:
--   SEPARATION_DOC, WITNESS_STATEMENTS, COMMUNICATIONS, COMPARATOR_DATA, RESTRICTIVE_COVENANT, PERFORMANCE_REVIEW,
--   POLICY_HANDBOOK, AGENCY_CHARGE, HR_COMPLAINT, EMPLOYMENT_CONTRACT, PERSONNEL_FILE, PAY_TIME_RECORDS.
--   Seeds MatterTypeCodes (still-NULL rows only), appends curated keywords, and adds PRIORITIZE rules. Idempotent.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
DECLARE @PackId UNIQUEIDENTIFIER=(SELECT TOP(1) DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack WHERE PackCode=N'EMPLOYMENT' AND IsDeleted=0);

IF @PackId IS NOT NULL
BEGIN
	-- 1) MatterTypeCodes
	DECLARE @MT TABLE(EvidenceTypeCode NVARCHAR(240) NOT NULL, MatterTypeLike NVARCHAR(200) NOT NULL);
	INSERT @MT VALUES
	(N'SEPARATION_DOC',N'%Wrongful Termination%'),(N'SEPARATION_DOC',N'%Retaliation%'),(N'SEPARATION_DOC',N'%Discrimination%'),(N'SEPARATION_DOC',N'%Whistleblower%'),(N'SEPARATION_DOC',N'%Leave%'),
	(N'PAY_TIME_RECORDS',N'%Wage%'),(N'PAY_TIME_RECORDS',N'%Wrongful Termination%'),
	(N'EMPLOYMENT_CONTRACT',N'%Non-Compete%'),(N'EMPLOYMENT_CONTRACT',N'%Trade Secret%'),(N'EMPLOYMENT_CONTRACT',N'%Wrongful Termination%'),(N'EMPLOYMENT_CONTRACT',N'%Wage%'),
	(N'POLICY_HANDBOOK',N'%Harassment%'),(N'POLICY_HANDBOOK',N'%Discrimination%'),(N'POLICY_HANDBOOK',N'%Leave%'),(N'POLICY_HANDBOOK',N'%Wrongful Termination%'),(N'POLICY_HANDBOOK',N'%Wage%'),(N'POLICY_HANDBOOK',N'%Retaliation%');

	;WITH chosen AS
	(
		SELECT DISTINCT e.DecisionDomainPackEvidenceTypeId, m.MatterTypeCode
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		JOIN @MT x ON x.EvidenceTypeCode=e.EvidenceTypeCode
		JOIN POLOXI.Legal_DecisionDomainPackMatterType m ON m.DecisionDomainPackId=e.DecisionDomainPackId AND m.IsDeleted=0 AND m.IsActive=1 AND m.MatterTypeCode LIKE x.MatterTypeLike
		WHERE e.DecisionDomainPackId=@PackId AND e.IsDeleted=0
	),
	agg AS (SELECT DecisionDomainPackEvidenceTypeId, STRING_AGG(MatterTypeCode,N',') WITHIN GROUP (ORDER BY MatterTypeCode) Codes FROM chosen GROUP BY DecisionDomainPackEvidenceTypeId)
	UPDATE e SET e.MatterTypeCodes=LEFT(a.Codes,400), e.ModifiedDateUtc=@Now, e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e JOIN agg a ON a.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId
	 WHERE NULLIF(LTRIM(RTRIM(e.MatterTypeCodes)),N'') IS NULL;

	-- 2) Keywords (appended, dedup)
	DECLARE @Kw TABLE(EvidenceTypeCode NVARCHAR(240) NOT NULL, Keywords NVARCHAR(600) NOT NULL);
	INSERT @Kw VALUES
	(N'SEPARATION_DOC',N'termination letter,terminated,fired,let go,laid off,layoff,rif,separation agreement,severance,final paycheck,exit interview,resignation,constructive discharge,cobra notice,last day'),
	(N'WITNESS_STATEMENTS',N'coworker,co-worker,colleague,witnessed,overheard,declaration,statement,saw,heard,told me'),
	(N'COMMUNICATIONS',N'email,text,slack,teams,memo,message,said,wrote,told,comment,remark,joke,stray remark,direct evidence'),
	(N'COMPARATOR_DATA',N'comparator,similarly situated,treated differently,other employees,younger,male,female,outside protected class,replacement,replaced by,statistics,disparate'),
	(N'RESTRICTIVE_COVENANT',N'non-compete,noncompete,non-solicit,nonsolicitation,confidentiality agreement,nda,16600,restrictive covenant,trade secret,proprietary,customer list,solicited,competitor,garden leave'),
	(N'PERFORMANCE_REVIEW',N'performance review,evaluation,appraisal,pip,performance improvement plan,write-up,written warning,verbal warning,discipline,disciplinary,met expectations,exceeds,needs improvement,rating'),
	(N'POLICY_HANDBOOK',N'handbook,employee handbook,policy,anti-harassment policy,complaint procedure,progressive discipline,at-will,at will,acknowledgment,code of conduct,eeo policy'),
	(N'AGENCY_CHARGE',N'eeoc,dfeh,crd,civil rights department,charge of discrimination,right to sue,right-to-sue,administrative complaint,exhaustion,300 days,intake questionnaire'),
	(N'HR_COMPLAINT',N'hr complaint,complained to hr,human resources,internal complaint,reported,hotline,grievance,protected activity,investigation,investigated,complaint of discrimination,complaint of harassment,whistleblower,1102.5'),
	(N'EMPLOYMENT_CONTRACT',N'employment agreement,offer letter,contract,term of employment,for cause,good cause,compensation,bonus plan,commission agreement,arbitration clause,at-will disclaimer'),
	(N'PERSONNEL_FILE',N'personnel file,personnel records,1198.5,employee file,hr file,hire date,job title,promotion,demotion,transfer,attendance,disciplinary history'),
	(N'PAY_TIME_RECORDS',N'timecards,time records,timesheets,punch,clock in,clock out,overtime,meal break,rest break,meal period,wage statements,pay stubs,226,paystub,unpaid wages,misclassified,exempt,non-exempt,hours worked,off the clock,waiting time penalties');

	;WITH merged AS
	(
		SELECT e.DecisionDomainPackEvidenceTypeId, STRING_AGG(t.Token,N',') WITHIN GROUP (ORDER BY t.Token) Keywords
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		JOIN @Kw k ON k.EvidenceTypeCode=e.EvidenceTypeCode
		CROSS APPLY (SELECT DISTINCT LOWER(LTRIM(RTRIM(s.value))) Token FROM STRING_SPLIT(ISNULL(e.RelevanceKeywords,N'')+N','+k.Keywords,N',') s WHERE LEN(LTRIM(RTRIM(s.value)))>1) t
		WHERE e.DecisionDomainPackId=@PackId AND e.IsDeleted=0
		GROUP BY e.DecisionDomainPackEvidenceTypeId
	)
	UPDATE e SET e.RelevanceKeywords=LEFT(m.Keywords,1000), e.ModifiedDateUtc=@Now, e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e JOIN merged m ON m.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId
	 WHERE ISNULL(e.RelevanceKeywords,N'')<>LEFT(m.Keywords,1000);

	-- 3) Rules
	DECLARE @Rules TABLE(TargetCode NVARCHAR(240) NOT NULL, SignalKindCode NVARCHAR(80) NOT NULL, SignalValue NVARCHAR(800) NOT NULL, Priority INT NOT NULL, Reason NVARCHAR(2000) NOT NULL);
	INSERT @Rules VALUES
	(N'PERSONNEL_FILE',      N'DOCUMENT_PRESENT', N'SEPARATION_DOC',     10, N'Termination documented: the personnel file tests the stated reason against history.'),
	(N'PERFORMANCE_REVIEW',  N'DOCUMENT_PRESENT', N'SEPARATION_DOC',     20, N'Termination documented: prior reviews prove or disprove the performance rationale (pretext).'),
	(N'COMPARATOR_DATA',     N'DOCUMENT_PRESENT', N'SEPARATION_DOC',     30, N'Termination documented: comparator treatment is the core circumstantial proof.'),
	(N'PAY_TIME_RECORDS',    N'DOCUMENT_PRESENT', N'SEPARATION_DOC',     40, N'Termination documented: final-pay timing triggers waiting-time penalties.'),
	(N'COMMUNICATIONS',      N'DOCUMENT_PRESENT', N'HR_COMPLAINT',       10, N'Protected complaint on file: communications establish decision-maker knowledge and animus.'),
	(N'SEPARATION_DOC',      N'DOCUMENT_PRESENT', N'HR_COMPLAINT',       20, N'Protected complaint on file: temporal proximity to adverse action is the retaliation anchor.'),
	(N'POLICY_HANDBOOK',     N'DOCUMENT_PRESENT', N'HR_COMPLAINT',       30, N'Complaint on file: handbook procedure measures adequacy of employer response (avoidable consequences).'),
	(N'WITNESS_STATEMENTS',  N'DOCUMENT_PRESENT', N'HR_COMPLAINT',       40, N'Complaint on file: coworker witnesses corroborate the underlying conduct.'),
	(N'HR_COMPLAINT',        N'DOCUMENT_PRESENT', N'AGENCY_CHARGE',      10, N'Agency charge filed: internal complaint history frames exhaustion scope and notice.'),
	(N'SEPARATION_DOC',      N'DOCUMENT_PRESENT', N'AGENCY_CHARGE',      20, N'Agency charge filed: adverse action dates control the limitations analysis.'),
	(N'PERFORMANCE_REVIEW',  N'DOCUMENT_PRESENT', N'PERSONNEL_FILE',     10, N'Personnel file on file: review trajectory is the pretext battleground.'),
	(N'EMPLOYMENT_CONTRACT', N'DOCUMENT_PRESENT', N'RESTRICTIVE_COVENANT',10,N'Covenant on file: the governing employment agreement supplies consideration and term.'),
	(N'COMMUNICATIONS',      N'DOCUMENT_PRESENT', N'RESTRICTIVE_COVENANT',20,N'Covenant on file: communications with customers/competitor prove or disprove breach.'),
	(N'EMPLOYMENT_CONTRACT', N'DOCUMENT_PRESENT', N'PAY_TIME_RECORDS',   10, N'Time records on file: contract/offer letter fixes rate, exemption and schedule.'),
	(N'POLICY_HANDBOOK',     N'DOCUMENT_PRESENT', N'PAY_TIME_RECORDS',   20, N'Time records on file: meal/rest break policy adequacy is a class-wide issue.'),
	(N'PAY_TIME_RECORDS',    N'MATTER_SIGNAL',    N'overtime',           10, N'Overtime referenced: time and pay records are core.'),
	(N'PAY_TIME_RECORDS',    N'MATTER_SIGNAL',    N'meal break',         10, N'Meal break referenced: timekeeping records are core.'),
	(N'PAY_TIME_RECORDS',    N'MATTER_SIGNAL',    N'misclassified',      10, N'Misclassification referenced: duties and pay records decide exemption.'),
	(N'HR_COMPLAINT',        N'MATTER_SIGNAL',    N'retaliation',        10, N'Retaliation referenced: protected activity proof is the threshold element.'),
	(N'HR_COMPLAINT',        N'MATTER_SIGNAL',    N'whistleblower',      10, N'Whistleblower referenced: the protected disclosure must be documented.'),
	(N'HR_COMPLAINT',        N'MATTER_SIGNAL',    N'harassment',         20, N'Harassment referenced: employer notice via complaint is central.'),
	(N'COMPARATOR_DATA',     N'MATTER_SIGNAL',    N'discrimination',     10, N'Discrimination referenced: comparator evidence is the core circumstantial proof.'),
	(N'COMPARATOR_DATA',     N'MATTER_SIGNAL',    N'replaced',           20, N'Replacement referenced: replacement demographics support the inference.'),
	(N'SEPARATION_DOC',      N'MATTER_SIGNAL',    N'terminated',         10, N'Termination referenced: separation documents are the adverse-action anchor.'),
	(N'SEPARATION_DOC',      N'MATTER_SIGNAL',    N'fired',              10, N'Termination referenced: separation documents are the adverse-action anchor.'),
	(N'SEPARATION_DOC',      N'MATTER_SIGNAL',    N'severance',          20, N'Severance referenced: release terms and consideration govern claims.'),
	(N'RESTRICTIVE_COVENANT',N'MATTER_SIGNAL',    N'non compete',        10, N'Non-compete referenced: covenant text and enforceability (B&P 16600) are core.'),
	(N'RESTRICTIVE_COVENANT',N'MATTER_SIGNAL',    N'trade secret',       10, N'Trade secret referenced: confidentiality terms and secrecy measures are core.'),
	(N'RESTRICTIVE_COVENANT',N'MATTER_SIGNAL',    N'solicit',            20, N'Solicitation referenced: non-solicit scope is core.'),
	(N'AGENCY_CHARGE',       N'MATTER_SIGNAL',    N'eeoc',               10, N'EEOC referenced: exhaustion and right-to-sue timing are threshold.'),
	(N'AGENCY_CHARGE',       N'MATTER_SIGNAL',    N'dfeh',               10, N'DFEH/CRD referenced: exhaustion is threshold.'),
	(N'AGENCY_CHARGE',       N'MATTER_SIGNAL',    N'right sue',          10, N'Right-to-sue referenced: filing deadline runs from issuance.'),
	(N'PERFORMANCE_REVIEW',  N'MATTER_SIGNAL',    N'performance',        10, N'Performance rationale referenced: reviews test pretext.'),
	(N'PERFORMANCE_REVIEW',  N'MATTER_SIGNAL',    N'pip',                10, N'PIP referenced: improvement plan timing and content test pretext.'),
	(N'POLICY_HANDBOOK',     N'MATTER_SIGNAL',    N'handbook',           10, N'Handbook referenced: policy terms frame at-will status and complaint procedure.'),
	(N'EMPLOYMENT_CONTRACT', N'MATTER_SIGNAL',    N'independent contractor',10,N'Contractor status referenced: the agreement is the threshold relationship evidence.'),
	(N'EMPLOYMENT_CONTRACT', N'MATTER_SIGNAL',    N'arbitration',        10, N'Arbitration referenced: agreement arbitration clause is a threshold forum issue.'),
	(N'COMMUNICATIONS',      N'MATTER_SIGNAL',    N'email',              20, N'Emails referenced: communications are direct evidence of motive.'),
	(N'COMMUNICATIONS',      N'MATTER_SIGNAL',    N'text message',       20, N'Texts referenced: communications are direct evidence of motive.'),
	(N'PERSONNEL_FILE',      N'MATTER_SIGNAL',    N'personnel file',     10, N'Personnel file referenced: Labor Code 1198.5 request and contents are core.');

	INSERT POLOXI.Legal_DecisionDomainPackRelevanceRule
		  (DecisionDomainPackRelevanceRuleId,DecisionDomainPackId,TargetKindCode,TargetCode,SignalKindCode,SignalValue,EffectCode,Priority,Reason,IsActive,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(), e.DecisionDomainPackId, N'EVIDENCE_TYPE', e.EvidenceTypeCode, r.SignalKindCode, r.SignalValue, N'PRIORITIZE', r.Priority, r.Reason, 1, e.TenantId, @Now, @SystemUserId, 0
	  FROM @Rules r
	  JOIN POLOXI.Legal_DecisionDomainPackEvidenceType e ON e.DecisionDomainPackId=@PackId AND e.EvidenceTypeCode=r.TargetCode AND e.IsDeleted=0 AND e.IsActive=1
	 WHERE (r.SignalKindCode<>N'DOCUMENT_PRESENT'
			OR EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceType s WHERE s.DecisionDomainPackId=@PackId AND s.EvidenceTypeCode=r.SignalValue AND s.IsDeleted=0))
	   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackRelevanceRule x
						WHERE x.DecisionDomainPackId=@PackId AND x.TargetKindCode=N'EVIDENCE_TYPE' AND x.TargetCode=e.EvidenceTypeCode
						  AND x.SignalKindCode=r.SignalKindCode AND x.SignalValue=r.SignalValue AND x.IsDeleted=0);
END;

COMMIT TRANSACTION;
