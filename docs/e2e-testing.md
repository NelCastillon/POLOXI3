# Judz — Full End-to-End Testing Guide

This guide takes you from a clean checkout to exercising **every stage** of the Judz
legal-document decision pipeline locally: upload → malware scan → binary store →
DB persistence → Azure Document Intelligence extraction → Azure OpenAI semantic
enrichment (atomic propositions) → SQL search projection → live POLOXI grounding →
decision run → CDC/DCI reevaluation.

The local dev database (site4now `db_ace4f8_deliver888011`) is configured in
`Legal/src/Legal.Api/appsettings.Development.json`. Migrations run automatically on
API startup, so provider metadata, the CHAT model deployment, the semantic-extraction
feature policy, and the POLOXI passage capability are all seeded for you.

---

## 1. Prerequisites

- Visual Studio 2026 (or `dotnet` SDK for .NET 10).
- An **Azure OpenAI** resource with a **CHAT** model deployment.
- An **Azure Document Intelligence** resource (for PDF/scanned OCR; native-text
  documents fall back to the built-in text extractor).

---

## 2. One-time credential setup

Run the setup script from the repo root. It stores Document Intelligence in User
Secrets and Azure OpenAI in User-scope environment variables (the two subsystems read
from different sources — see the script header for why):

```powershell
./scripts/setup-e2e.ps1 `
	-AzureOpenAiEndpoint "https://<your-aoai>.openai.azure.com/" `
	-AzureOpenAiKey      "<aoai-key>" `
	-DocIntelEndpoint    "https://<your-docintel>.cognitiveservices.azure.com/" `
	-DocIntelKey         "<docintel-key>"
```

> **Restart Visual Studio afterwards.** Environment variables are captured at process
> start, so `AMS_AZURE_OPENAI_ENDPOINT` / `AMS_AZURE_OPENAI_KEY` are only visible after
> VS is fully closed and reopened.

### Match the CHAT deployment name

The DB seeds the active CHAT deployment as `gpt-5.6-sol`. Azure requires this to match
a deployment that actually exists in **your** resource. If yours is named differently,
edit `@RealDeploymentName` in `scripts/align-chat-deployment.sql` and run it against the
dev DB. Symptom of a mismatch: uploads complete but show *"no propositions were derived
yet"* and the API log shows HTTP 404 `DeploymentNotFound`.

---

## 3. Launch

1. Set **Legal.Api** and **Legal.Web** as startup projects (Legal.Api first — it runs
   migrations and hosts the API).
2. F5. On first run, watch the API console for `MigrateAsync` completing successfully.
3. Confirm platform health: browse to `https://localhost:<api-port>/health`.
   `Healthy` means the corpus→decision bridge (POLOXI passage capability) is present.

---

## 4. End-to-end click-through

Open the Web app and navigate to **`/legal/personalinjury_decision2`**.

| # | Action | What to verify |
|---|--------|----------------|
| 1 | Open a Matter and go to **Matter Corpus** upload | Upload panel visible |
| 2 | Upload a PDF/DOCX | Realtime intake stepper advances: Scan → Store → Save → Extract → Enrich |
| 3 | Watch the result badge | Green **OK — Pass** = full pipeline incl. enrichment; amber warning = stored but no propositions (enrichment disabled or CHAT route/deployment unavailable) |
| 4 | Inspect the evidence graph refresh | Extracted passages + derived atomic propositions appear |
| 5 | Run a decision | Evidence panel shows **Documents: AVAILABLE / Evidence: (non-zero)** — uploaded corpus passages are now grounded by POLOXI |
| 6 | Change/re-upload a document | Decision Reevaluation worker (CDC/DCI) triggers a fresh run |

---

## 5. Verification SQL (run against the dev DB)

```sql
-- Active CHAT route + the deployment name actually being called
SELECT m.ModelCode, m.DeploymentName, m.CapabilityCode, m.IsActive
FROM AI.Legal_ModelDeployment m
JOIN AI.Legal_Provider p ON p.ProviderId = m.ProviderId
WHERE p.ProviderCode = N'AZURE_OPENAI' AND m.CapabilityCode = N'CHAT' AND m.IsDeleted = 0;

-- Semantic-extraction feature policy exists (enrichment enabled)
SELECT TenantId, FeatureCode, PrimaryModelDeploymentId
FROM AI.Legal_FeaturePolicy
WHERE FeatureCode = N'LEGAL_DOCUMENT_SEMANTIC_EXTRACTION' AND IsDeleted = 0;

-- POLOXI can ground uploaded passages (capability present + active)
SELECT CapabilityCode, EntityTypeCode, ExecutionHandlerCode, IsActive
FROM POLOXI.Legal_Capability
WHERE EntityTypeCode = N'LEGAL_DOCUMENT_PASSAGE' AND IsDeleted = 0;

-- Passages projected into the live search index after upload
SELECT TOP 20 *
FROM AI.Legal_SearchDocument
WHERE EntityTypeCode = N'LEGAL_DOCUMENT_PASSAGE'
ORDER BY CreatedDateUtc DESC;
```

---

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Upload OK but "no propositions were derived yet" | CHAT route unreachable | Verify `AMS_AZURE_OPENAI_*` env vars (restart VS); align deployment name (`scripts/align-chat-deployment.sql`) |
| API log: 404 `DeploymentNotFound` | DeploymentName mismatch | Run `scripts/align-chat-deployment.sql` with your real name |
| Extraction fails on scanned PDFs | Document Intelligence not configured | Re-run `setup-e2e.ps1`; confirm User Secrets set |
| `/health` reports Unhealthy | Passage capability missing | Ensure migrations ran (0346 seeds it) |
| Decision shows Evidence: NONE | Passages not projected/grounded | Check `AI.Legal_SearchDocument` rows + POLOXI capability query above |

> Local uploads use **FileSystem** binary storage and the malware scanner is
> **Disabled** by design for local testing. Azure Blob + Defender scanning are
> production concerns. `SearchProjectionEnabled` stays **false** locally — the SQL
> projection that POLOXI grounds against runs unconditionally; that flag only controls
> the optional Azure AI Search mirror.
