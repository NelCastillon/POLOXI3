SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Enterprise clickwrap consent (Table layer).
--
-- Introduces two DB-authoritative tables that back the signup clickwrap flow:
--
--   • SaaS.Legal_Agreement      — versioned legal documents (Terms of Service and
--                                 Privacy Policy). One row per (AgreementType,
--                                 Version). Body text lives in the DB and a
--                                 SHA-256 hash of the exact body is stored so a
--                                 consent record can be tied to the precise text
--                                 the user accepted (tamper-evident snapshot).
--
--   • SaaS.Legal_ConsentRecord  — append-only legal evidence of acceptance. Each
--                                 row captures who accepted which agreement
--                                 version, the document hash they saw, the client
--                                 IP address and user-agent, the acceptance method
--                                 (e.g. ClickwrapCheckbox), and a correlation id.
--
-- The Privacy Policy body explicitly discloses that IP addresses are collected
-- and processed for security, audit, and fraud-prevention purposes, so the
-- clickwrap acceptance is also the lawful basis for storing IpAddress on the
-- login-history and consent tables. All base/audit fields are present and the
-- seed is idempotent (guarded by NOT EXISTS on AgreementType + Version).
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Legal_Agreement ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Legal_Agreement', N'U') IS NULL
CREATE TABLE SaaS.Legal_Agreement
(
	AgreementId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_Agreement PRIMARY KEY DEFAULT NEWID(),
	AgreementType     NVARCHAR(40) NOT NULL,    -- TermsOfService | PrivacyPolicy
	Version           NVARCHAR(20) NOT NULL,    -- e.g. 2024-06-01 or 1.0
	Title             NVARCHAR(200) NOT NULL,
	Body              NVARCHAR(MAX) NOT NULL,
	ContentHash       CHAR(64) NOT NULL,        -- SHA-256 hex of Body
	EffectiveAtUtc    DATETIME2 NOT NULL CONSTRAINT DF_Legal_Agreement_Effective DEFAULT SYSUTCDATETIME(),
	IsActive          BIT NOT NULL CONSTRAINT DF_Legal_Agreement_IsActive DEFAULT 1,
	RequiresConsent   BIT NOT NULL CONSTRAINT DF_Legal_Agreement_RequiresConsent DEFAULT 1,
	SortOrder         INT NOT NULL CONSTRAINT DF_Legal_Agreement_SortOrder DEFAULT 0,
	TenantId          UNIQUEIDENTIFIER NULL,    -- NULL = platform-wide agreement
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Legal_Agreement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Legal_Agreement_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Legal_Agreement_TypeVersion', N'UQ') IS NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Legal_Agreement_TypeVersion')
	CREATE UNIQUE INDEX UX_Legal_Agreement_TypeVersion ON SaaS.Legal_Agreement (AgreementType, Version) WHERE IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_Agreement_ActiveType')
	CREATE INDEX IX_Legal_Agreement_ActiveType ON SaaS.Legal_Agreement (AgreementType, IsActive) WHERE IsDeleted = 0;

-- 2. Legal_ConsentRecord ──────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Legal_ConsentRecord', N'U') IS NULL
CREATE TABLE SaaS.Legal_ConsentRecord
(
	ConsentId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ConsentRecord PRIMARY KEY DEFAULT NEWID(),
	UserId            UNIQUEIDENTIFIER NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	Email             NVARCHAR(320) NULL,
	AgreementId       UNIQUEIDENTIFIER NOT NULL,
	AgreementType     NVARCHAR(40) NOT NULL,
	AgreementVersion  NVARCHAR(20) NOT NULL,
	ContentHash       CHAR(64) NOT NULL,        -- hash of the exact body accepted
	AcceptanceMethod  NVARCHAR(40) NOT NULL,    -- ClickwrapCheckbox | ...
	IpAddress         NVARCHAR(64) NULL,
	UserAgent         NVARCHAR(512) NULL,
	CorrelationId     NVARCHAR(120) NULL,
	AcceptedAtUtc     DATETIME2 NOT NULL CONSTRAINT DF_Legal_ConsentRecord_Accepted DEFAULT SYSUTCDATETIME(),
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Legal_ConsentRecord_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Legal_ConsentRecord_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_ConsentRecord_Agreement FOREIGN KEY (AgreementId) REFERENCES SaaS.Legal_Agreement (AgreementId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_ConsentRecord_User')
	CREATE INDEX IX_Legal_ConsentRecord_User ON SaaS.Legal_ConsentRecord (UserId, AcceptedAtUtc) WHERE IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_ConsentRecord_TenantAccepted')
	CREATE INDEX IX_Legal_ConsentRecord_TenantAccepted ON SaaS.Legal_ConsentRecord (TenantId, AcceptedAtUtc) WHERE IsDeleted = 0;

-- 3. Seed: Terms of Service v2024-06-01 ───────────────────────────────────────
DECLARE @TosBody NVARCHAR(MAX) = N'# Judz.ai Early Access — Terms of Service

_Version 2024-06-01_

Welcome to Judz.ai. By creating an account and using the Judz.ai Early Access
platform (the "Service"), you agree to these Terms of Service (the "Terms"). If
you do not agree, do not create an account or use the Service.

## 1. Early Access
The Service is provided on an early-access basis and may change, be interrupted,
or be discontinued at any time. Features are offered "as is" without warranties
of any kind to the fullest extent permitted by law.

## 2. Your Account
You are responsible for maintaining the confidentiality of your credentials and
for all activity that occurs under your account. You must provide accurate
registration information and promptly update it if it changes.

## 3. Acceptable Use
You agree not to misuse the Service, including by attempting to gain unauthorized
access, interfering with normal operation, or using the Service to violate any
applicable law or the rights of others.

## 4. Intelligence Outputs
Judz.ai produces AI-assisted analysis intended to support, not replace,
professional judgment. You are responsible for reviewing and validating any
output before relying on it. Outputs do not constitute legal advice.

## 5. Privacy
Your use of the Service is also governed by our Privacy Policy, which explains
what data we collect and how we process it, including network identifiers such as
your IP address. By accepting these Terms you also acknowledge the Privacy Policy.

## 6. Suspension and Termination
We may suspend or terminate access to the Service for conduct that violates these
Terms or that we reasonably believe is harmful to other users or the Service.

## 7. Changes to These Terms
We may update these Terms from time to time. Continued use of the Service after a
new version becomes effective constitutes acceptance of the updated Terms.

## 8. Contact
Questions about these Terms may be sent to legal@judz.ai.';

IF NOT EXISTS (SELECT 1 FROM SaaS.Legal_Agreement WHERE AgreementType = N'TermsOfService' AND Version = N'2024-06-01' AND IsDeleted = 0)
	INSERT INTO SaaS.Legal_Agreement (AgreementType, Version, Title, Body, ContentHash, IsActive, RequiresConsent, SortOrder)
	VALUES (
		N'TermsOfService',
		N'2024-06-01',
		N'Terms of Service',
		@TosBody,
		LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', @TosBody), 2)),
		1, 1, 1);

-- 4. Seed: Privacy Policy v2024-06-01 (discloses IP capture) ───────────────────
DECLARE @PrivacyBody NVARCHAR(MAX) = N'# Judz.ai Early Access — Privacy Policy

_Version 2024-06-01_

This Privacy Policy explains what personal data Judz.ai ("we", "us") collects
when you use the Judz.ai Early Access platform, why we collect it, and how we
protect it. By creating an account you consent to the practices described here.

## 1. Data We Collect
- **Account data:** your name and email address.
- **Authentication data:** sign-in attempts, including successes and failures.
- **Network identifiers:** your **IP address** and browser **user-agent** string.
- **Usage data:** metered feature usage and audit events within your workspace.

## 2. IP Address Collection and Processing
We collect and store the **IP address** associated with your account activity —
including account creation, sign-in attempts, and consent acceptance. We process
IP addresses to:
- secure your account and detect suspicious or fraudulent activity;
- maintain a tamper-evident audit and login-history trail;
- meet our legal, security, and record-keeping obligations.

By accepting this Privacy Policy you consent to this collection and processing of
your IP address for the purposes described above. IP addresses are retained for
the life of the associated audit record and are not sold to third parties.

## 3. How We Use Your Data
We use the data above to operate, secure, and improve the Service, to provide
support, and to comply with legal obligations. We do not sell your personal data.

## 4. Data Retention
We retain audit, login-history, and consent records for as long as necessary to
provide the Service and satisfy legal and security requirements.

## 5. Security
We use administrative, technical, and organizational safeguards designed to
protect your data. No method of transmission or storage is completely secure.

## 6. Your Rights
Subject to applicable law, you may request access to, correction of, or deletion
of your personal data by contacting us. Some records (such as consent evidence)
may be retained where we have a legal obligation to do so.

## 7. Changes to This Policy
We may update this Policy from time to time. Material changes will be reflected in
a new version, and continued use constitutes acceptance of the updated Policy.

## 8. Contact
Privacy questions may be sent to privacy@judz.ai.';

IF NOT EXISTS (SELECT 1 FROM SaaS.Legal_Agreement WHERE AgreementType = N'PrivacyPolicy' AND Version = N'2024-06-01' AND IsDeleted = 0)
	INSERT INTO SaaS.Legal_Agreement (AgreementType, Version, Title, Body, ContentHash, IsActive, RequiresConsent, SortOrder)
	VALUES (
		N'PrivacyPolicy',
		N'2024-06-01',
		N'Privacy Policy',
		@PrivacyBody,
		LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', @PrivacyBody), 2)),
		1, 1, 2);

COMMIT TRANSACTION;
