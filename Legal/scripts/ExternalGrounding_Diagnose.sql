/*
	Legal Authority / Citation visibility diagnostics.

	There are TWO independent things to "show":

	A. PROPOSED authorities (UNVERIFIED citations) - these are the AI-proposed leads.
	   They render TODAY on the Legal Authority tab and in Overview citations (warn badge)
	   as long as ProposedLegalAuthorities is populated. That requires:
		 - Config flag Intelligence.SearchWide.EnableLegalAuthorityProposal = true (default true)
		 - The decision was run in the LEGAL context (request ContextCode = Legal)
	   If NO citations appear at all, this flag/context (STEP 0) is the usual cause - NOT grounding.

	B. VERIFIED authorities - a proposed authority is promoted to VERIFIED only when a
	   retrieved external snippet matches its identity. That requires external grounding:
		 Gate 1: no snippets retrieved   -> grounding disabled or ApiKey blank (STEP 1/2)
		 Gate 2: identity not matched    -> LegalAuthorityReference.VerificationTokens empty/too strict
		 Gate 3: jurisdiction ineligible -> snippet names a different US-state sovereign than the matter

	A Verified Authority REQUIRES a retrieved snippet whose identity matches. The LLM prompt
	can never create one. STEP 0 restores the UNVERIFIED citations; STEP 1/2 enable verification.
*/

-- STEP 0: Confirm the AI legal-authority PROPOSAL is enabled (controls UNVERIFIED citations).
--         If this is false/off, NO citations appear on any surface, verified or not.
SELECT SettingKey, SettingValue, DefaultValue, TenantId, IsDeleted
FROM Core.ConfigurationSetting
WHERE SettingKey = N'Intelligence.SearchWide.EnableLegalAuthorityProposal'
  AND IsDeleted = 0
ORDER BY TenantId;

-- Enable the proposal pass if STEP 0 shows it disabled (default is true, so a row may be absent).
UPDATE Core.ConfigurationSetting
   SET SettingValue = N'true'
 WHERE SettingKey = N'Intelligence.SearchWide.EnableLegalAuthorityProposal'
   AND IsDeleted = 0;

-- STEP 1: Inspect current external-grounding configuration (tenant + global rows).
SELECT SettingKey, SettingValue, DefaultValue, TenantId, IsDeleted
FROM Core.ConfigurationSetting
WHERE SettingKey LIKE N'Intelligence.SearchWide.ExternalGrounding.%'
  AND IsDeleted = 0
ORDER BY SettingKey, TenantId;

/*
	STEP 2: Enable grounding + set a real provider key.
	Review the STEP 1 output first. If the rows exist, UPDATE them; if a key is missing,
	INSERT it. Replace <TENANT_ID> and <YOUR_PROVIDER_API_KEY> before running.
*/

-- Enable
UPDATE Core.ConfigurationSetting
   SET SettingValue = N'true'
 WHERE SettingKey = N'Intelligence.SearchWide.ExternalGrounding.Enabled'
   AND IsDeleted = 0;

-- Provider API key (Tavily by default; ProviderCode row controls the provider)
UPDATE Core.ConfigurationSetting
   SET SettingValue = N'<YOUR_PROVIDER_API_KEY>'
 WHERE SettingKey = N'Intelligence.SearchWide.ExternalGrounding.ApiKey'
   AND IsDeleted = 0;

/*
	STEP 3: After enabling, run one decision, then confirm snippets are actually landing
	and whether they pass identity/jurisdiction gates.
*/

-- 3a. Were any external snippets retrieved at all? (Gate 1)
SELECT TOP (50)
	   TenantId, NormalizedQuery, Title, AuthorityKind,
	   AuthorityIdentityVerified, PropositionSupportScore, PropositionSupportStatus,
	   RetrievedDateUtc
FROM POLOXI.Legal_ExternalKnowledge
WHERE IsDeleted = 0
ORDER BY RetrievedDateUtc DESC;

-- 3b. Retrieved but never identity-verified? (Gate 2 / Gate 3)
--     If rows appear here with AuthorityIdentityVerified = 0, the problem is NOT config —
--     inspect the LEGAL-TRACE stage=5-identity logs (identityMatched / status) and the
--     authority VerificationTokens, not this script.
SELECT PropositionSupportStatus, COUNT(*) AS SnippetCount,
	   SUM(CAST(AuthorityIdentityVerified AS INT)) AS IdentityVerifiedCount
FROM POLOXI.Legal_ExternalKnowledge
WHERE IsDeleted = 0
GROUP BY PropositionSupportStatus
ORDER BY SnippetCount DESC;
