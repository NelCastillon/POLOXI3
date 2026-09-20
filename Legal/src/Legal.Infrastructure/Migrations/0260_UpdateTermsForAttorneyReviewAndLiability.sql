SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Version the Terms rather than changing an agreement users previously accepted.
DECLARE @TermsBody NVARCHAR(MAX) = N'# Judz.ai Early Access — Terms of Service

_Version 2.0_

Welcome to Judz.ai. These Terms of Service (the "Terms") govern access to and use of the Judz.ai Early Access platform (the "Service"). By creating an account, selecting the acceptance checkbox, or using the Service, you agree to these Terms. If you do not agree, do not create an account or use the Service.

## 1. Early Access Service
The Service is provided on an early-access basis. Features, outputs, integrations, availability, and functionality may change, be interrupted, or be discontinued at any time.

## 2. Account Responsibilities
You must provide accurate registration information, protect your credentials, restrict unauthorized access, and remain responsible for activity performed through your account. You must notify Judz promptly of suspected unauthorized use.

## 3. Permitted Use
You may use the Service only for lawful purposes and in accordance with these Terms. You may not attempt unauthorized access, interfere with operation of the Service, introduce malicious content, misuse another person''s information, or use the Service to violate applicable law or third-party rights.

## 4. AI-Assisted Outputs; No Legal Advice
The Service uses artificial intelligence and automated systems to produce research, summaries, classifications, recommendations, and other outputs. These outputs may be incomplete, inaccurate, outdated, misleading, or unsuitable for a particular matter. Judz is a technology provider and does not provide legal advice, legal representation, or legal services. Use of the Service does not create an attorney-client relationship with Judz or any of its affiliates, personnel, or providers.

## 5. Mandatory Attorney Review
All outputs must be independently reviewed and approved by a qualified, licensed attorney before they are relied upon to make, communicate, implement, or execute any legal, business, compliance, filing, contractual, or other consequential decision. You must independently verify relevant facts, authorities, citations, deadlines, jurisdictions, and legal requirements. You remain solely responsible for every decision, action, filing, communication, and outcome arising from use of the Service.

## 6. No Warranties
To the fullest extent permitted by applicable law, the Service and all outputs are provided "as is" and "as available," without warranties of any kind, whether express, implied, statutory, or otherwise. Judz disclaims warranties of accuracy, completeness, reliability, merchantability, fitness for a particular purpose, title, non-infringement, availability, and results.

## 7. Limitation of Liability
To the fullest extent permitted by applicable law, Judz and its affiliates, officers, employees, contractors, licensors, and service providers will not be liable for any direct, indirect, incidental, special, exemplary, punitive, or consequential damages, or for any loss of profits, revenue, data, business, opportunity, goodwill, or anticipated savings, arising from or related to the Service, its outputs, reliance on an output, or inability to use the Service, regardless of the legal theory and even if advised that such damages were possible. Where liability cannot lawfully be excluded, Judz''s aggregate liability will not exceed the amount paid by you for the Service during the twelve months preceding the event giving rise to the claim.

## 8. Third-Party Services and Content
The Service may use or reference third-party services, sources, models, integrations, or content. Judz does not control and is not responsible for their availability, accuracy, security, terms, or conduct.

## 9. Privacy
Use of the Service is also governed by the Privacy Policy, which explains how information is collected and processed, including account data, usage data, IP addresses, and browser user-agent information.

## 10. Suspension and Termination
Judz may suspend or terminate access when use violates these Terms, creates legal or security risk, threatens the Service or other users, or is otherwise harmful.

## 11. Changes to These Terms
Judz may update these Terms. A new version may require renewed acceptance before continued use. The version and content hash recorded with your acceptance identify the exact Terms accepted.

## 12. Contact
Questions about these Terms may be sent to legal@judz.ai.';

IF NOT EXISTS
(
	SELECT 1
	FROM SaaS.Legal_Agreement
	WHERE AgreementType = N'TermsOfService'
	  AND Version = N'2.0'
	  AND IsDeleted = 0
)
BEGIN
	INSERT INTO SaaS.Legal_Agreement
		(AgreementType, Version, Title, Body, ContentHash, EffectiveAtUtc,
		 IsActive, RequiresConsent, SortOrder, TenantId,
		 CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
	VALUES
		(N'TermsOfService', N'2.0', N'Terms of Service', @TermsBody,
		 LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', @TermsBody), 2)), SYSUTCDATETIME(),
		 1, 1, 1, NULL,
		 SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;

UPDATE SaaS.Legal_Agreement
SET IsActive = CASE WHEN Version = N'2.0' THEN 1 ELSE 0 END,
	ModifiedDateUtc = SYSUTCDATETIME()
WHERE AgreementType = N'TermsOfService'
  AND IsDeleted = 0;

COMMIT TRANSACTION;
