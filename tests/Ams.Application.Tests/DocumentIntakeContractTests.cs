using System.Net;
using System.Text;
using Ams.Application.Features.DocumentIntake;
using Ams.Infrastructure.Persistence;
using Ams.Web.Services;
using Xunit;

namespace Ams.Application.Tests;

public sealed class DocumentIntakeContractTests
{
    [Fact]
    public void StateMachine_EnforcesGovernedTransitionsAndRetrySchedule()
    {
        Assert.True(DocumentIntakeStateMachine.CanTransitionSession(DocumentIntakeStatuses.Draft, DocumentIntakeStatuses.Queued));
        Assert.False(DocumentIntakeStateMachine.CanTransitionSession(DocumentIntakeStatuses.Draft, DocumentIntakeStatuses.Completed));
        Assert.True(DocumentIntakeStateMachine.CanTransitionWorkItem(DocumentIntakeWorkStatuses.Processing, DocumentIntakeWorkStatuses.RetryScheduled));
        Assert.Equal(TimeSpan.FromSeconds(30), DocumentIntakeStateMachine.GetRetryDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(2), DocumentIntakeStateMachine.GetRetryDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(10), DocumentIntakeStateMachine.GetRetryDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(30), DocumentIntakeStateMachine.GetRetryDelay(4));
        Assert.Equal(TimeSpan.FromHours(2), DocumentIntakeStateMachine.GetRetryDelay(5));
    }

    [Fact]
    public void MalwareWorker_PersistsAuthorizationFailuresAndDefersDatabaseBackedRetries()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var provider = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Services", "DefenderStorageMalwareStatusProvider.cs"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeOperationsRepository.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "Ams.Worker", "Documents", "DocumentIntakeMalwareWorkerService.cs"));

        Assert.Contains("exception.Status is 401 or 403", provider, StringComparison.Ordinal);
        Assert.Contains("Storage Blob Data Reader", provider, StringComparison.Ordinal);
        Assert.Contains("DATEADD(MINUTE,-@ErrorRetryMinutes", repository, StringComparison.Ordinal);
        Assert.Contains("settings.MalwarePendingTimeoutMinutes", worker, StringComparison.Ordinal);
        Assert.Contains("UpsertMalwareStatusAsync", worker, StringComparison.Ordinal);
        Assert.Contains("result.StatusCode==\"ERROR\"", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsUnsupportedModuleAndInvalidCorrection()
    {
        var unsupported = new CreateDocumentIntakeSessionCommand(Guid.NewGuid(), "source-1", "UNKNOWN", "MANUAL_UPLOAD", "NORMAL", null, null, "correlation", Guid.NewGuid());
        var invalidCorrection = new ReviewDocumentIntakeFieldCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DocumentIntakeReviewStatuses.Corrected, null, "Correction", "correlation", Guid.NewGuid(), new byte[8]);

        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => DocumentIntakeValidator.Validate(unsupported));
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => DocumentIntakeValidator.Validate(invalidCorrection));
    }

    [Fact]
    public void Migrations_DefineTenantSafeIdempotentIntakeContracts()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var intakeResource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0078_EnterpriseAiDocumentIntake.sql", StringComparison.Ordinal));
        var idempotencyResource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0079_SubmissionIntakeAiIdempotency.sql", StringComparison.Ordinal));
        var intake = Read(assembly, intakeResource);
        var idempotency = Read(assembly, idempotencyResource);

        Assert.Contains("UX_DMS_IntakeSession_Tenant_Idempotency", intake, StringComparison.Ordinal);
        Assert.Contains("UX_DMS_IntakeWorkItem_Idempotency", intake, StringComparison.Ordinal);
        Assert.Contains("LeaseExpiresDateUtc", intake, StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeReviewHistory", intake, StringComparison.Ordinal);
        Assert.Contains("DMS.AiExecution", intake, StringComparison.Ordinal);
        Assert.Contains("MERGE DMS.AiPromptDefinition", intake, StringComparison.Ordinal);
        Assert.Contains("FROM Core.Tenant", intake, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO Core.Tenant", intake, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_SubmissionIntake_Tenant_SourceIdempotency", idempotency, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiClient_UsesTenantFreeRoutesAndPreservesConcurrencyTokens()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, "{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":25}"),
            new(HttpStatusCode.NoContent),
            new(HttpStatusCode.NoContent));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });
        var sessionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var targetEntityId = Guid.NewGuid();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await client.SearchDocumentIntakeAsync(targetEntityId: targetEntityId, pageSize: 25);
        await client.ReviewDocumentIntakeFieldAsync(sessionId, fieldId, new(tenantId, sessionId, fieldId, "APPROVED", "value", "reason", "correlation", Guid.NewGuid(), rowVersion));
        await client.QueueDocumentIntakeAsync(sessionId, new(tenantId, sessionId, "queue", "correlation", Guid.NewGuid(), rowVersion));

        Assert.Equal($"api/document-intake?searchTerm=&moduleCode=&statusCode=&assignedToUserId=&targetEntityId={targetEntityId}&pageNumber=1&pageSize=25", requests[0].Path);
        Assert.Equal($"api/document-intake/{sessionId}/fields/{fieldId}/review", requests[1].Path);
        Assert.Equal($"api/document-intake/{sessionId}/queue", requests[2].Path);
        Assert.All(requests, request => Assert.DoesNotContain("tenantId", request.Path, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[1].Body);
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[2].Body);
    }

    [Fact]
    public void SubmissionDocuments_UseStableIntakeKeysAndExistingDmsEvidence()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Web", "Components", "Pages", "SubmissionDetail.razor"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("$\"SUBMISSION:{SubmissionId:D}:DOCUMENT:{document.DocumentId:D}\"", source, StringComparison.Ordinal);
        Assert.Contains("$\"SUBMISSION:{SubmissionId:D}:PACKAGE\"", source, StringComparison.Ordinal);
        Assert.Contains("Api.AttachDocumentToIntakeAsync", source, StringComparison.Ordinal);
        Assert.Contains("document.DocumentId", source, StringComparison.Ordinal);
        Assert.Contains("<EnterpriseDocumentUpload", source, StringComparison.Ordinal);
        Assert.Contains("targetEntityId: SubmissionId", source, StringComparison.Ordinal);
        Assert.Contains("Submission analysis history", source, StringComparison.Ordinal);
        Assert.Contains("LoadDocumentIntakeSessionsAsync", source, StringComparison.Ordinal);
        Assert.Contains("Nav.NavigateTo($\"/operations/document-intake/{intakeId}\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_TargetFilterRemainsTenantScopedForRowsAndCount()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Equal(2, source.Split("AND (@TargetEntityId IS NULL OR s.TargetEntityId=@TargetEntityId)", StringSplitOptions.None).Length - 1);
        Assert.Contains("WHERE s.TenantId=@TenantId", source, StringComparison.Ordinal);
        Assert.Contains("TargetEntityId=targetEntityId", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiClient_UsesTenantFreePerDocumentStatusRoute()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests, Json(HttpStatusCode.OK, "[]"));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });
        var targetEntityId = Guid.NewGuid();

        await client.GetDocumentIntakeStatusesAsync(DocumentIntakeModules.Submission, targetEntityId);

        Assert.Equal($"api/document-intake/document-statuses?moduleCode=SUBMISSION&targetEntityId={targetEntityId}", requests[0].Path);
        Assert.DoesNotContain("tenantId", requests[0].Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PerDocumentStatusQuery_IsTenantTargetAndDeletionFenced()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));
        var submissionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Web", "Components", "Pages", "SubmissionDetail.razor"));
        var repository = File.ReadAllText(repositoryPath);
        var submission = File.ReadAllText(submissionPath);

        Assert.Contains("session.TenantId=@TenantId AND session.ModuleCode=@ModuleCode AND session.TargetEntityId=@TargetEntityId", repository, StringComparison.Ordinal);
        Assert.Contains("document.IsDeleted=0", repository, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY link.DocumentId", repository, StringComparison.Ordinal);
        Assert.Contains("GetDocumentIntakeStatusesAsync", submission, StringComparison.Ordinal);
        Assert.Contains("DocumentIntakeProgress(intakeStatus)", submission, StringComparison.Ordinal);
        Assert.Contains("Not analyzed", submission, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_HardensLeaseRecoveryCancellationAndMultiDocumentCompletion()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("WORK_LEASE_EXPIRED", source, StringComparison.Ordinal);
        Assert.Contains("work.StatusCode=N'PROCESSING' AND work.LeaseExpiresDateUtc<SYSUTCDATETIME()", source, StringComparison.Ordinal);
        Assert.Contains("session.StatusCode IN(N'QUEUED',N'PROCESSING')", source, StringComparison.Ordinal);
        Assert.Contains("StatusCode IN(N'PENDING',N'PROCESSING',N'RETRY_SCHEDULED',N'FAILED',N'DEAD_LETTERED')", source, StringComparison.Ordinal);
        Assert.Contains("AND s.StatusCode=N'PROCESSING'", source, StringComparison.Ordinal);
        Assert.Contains("AND NOT EXISTS(SELECT 1 FROM DMS.IntakeWorkItem WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode NOT IN(N'COMPLETED',N'CANCELLED')", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessingContext_IsDocumentAndPromptTenantFenced()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("e.ExecutionTypeCode=N'OCR' AND e.DocumentId=w.DocumentId", source, StringComparison.Ordinal);
        Assert.Contains("(p.TenantId=w.TenantId OR p.TenantId IS NULL)", source, StringComparison.Ordinal);
        Assert.Contains("ORDER BY CASE WHEN p.TenantId=w.TenantId THEN 0 ELSE 1 END", source, StringComparison.Ordinal);
        Assert.Contains("d.IsDeleted=0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexer_UsesPerDocumentIdentity()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ams.Infrastructure", "Services", "AzureDocumentSearchIndexer.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("$\"{context.Session.IntakeSessionId:N}-{documentId.Value:N}\"", source, StringComparison.Ordinal);
        Assert.Contains("{\"intakeSessionId\",context.Session.IntakeSessionId.ToString(\"D\")}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PrdReview_DocumentsCriticalAndOperationalGaps()
    {
        var reviewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "enterprise-ai-document-intake-prd-review.md"));
        var review = File.ReadAllText(reviewPath);

        Assert.Contains("## Compliance matrix", review, StringComparison.Ordinal);
        Assert.Contains("## Confirmed critical findings", review, StringComparison.Ordinal);
        Assert.Contains("## Prioritized residual roadmap", review, StringComparison.Ordinal);
        Assert.Contains("External validation required", review, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionReadinessMigration_UsesExistingSettingsAndIdempotentOperationalSchema()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(name=>name.EndsWith("0080_DocumentIntakeProductionReadiness.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);

        Assert.Contains("MERGE Core.ConfigurationSetting",sql,StringComparison.Ordinal);
        Assert.Contains("WHEN MATCHED THEN UPDATE SET DefaultValue=source.DefaultValue",sql,StringComparison.Ordinal);
        Assert.Contains("WHEN NOT MATCHED THEN INSERT",sql,StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE SET SettingValue=source.DefaultValue",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeMalwareScan",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakePayloadGovernance",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeWorkReplayHistory",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.AiPromptEvaluationRun",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeTelemetrySnapshot",sql,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeAlertIncident",sql,StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionControls_AreFailClosedAuditedAndBackgroundHosted()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var service=File.ReadAllText(Path.Combine(root,"src","Ams.Application","DocumentIntakeService.cs"));
        var operations=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","DocumentIntakeOperationsRepository.cs"));
        var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Program.cs"));

        Assert.Contains("EnsureDocumentCleanAsync",service,StringComparison.Ordinal);
        Assert.Contains("All evidence documents must have a CLEAN malware scan",operations,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakePayloadAccessAudit",operations,StringComparison.Ordinal);
        Assert.Contains("A passing evaluation run is required before prompt approval",operations,StringComparison.Ordinal);
        Assert.Contains("DMS.IntakeWorkReplayHistory",operations,StringComparison.Ordinal);
        Assert.Contains("AddHostedService<DocumentIntakeMalwareWorkerService>",worker,StringComparison.Ordinal);
        Assert.Contains("AddHostedService<DocumentIntakeRetentionWorkerService>",worker,StringComparison.Ordinal);
        Assert.Contains("AddHostedService<DocumentIntakePromptEvaluationWorkerService>",worker,StringComparison.Ordinal);
        Assert.Contains("AddHostedService<DocumentIntakeTelemetryWorkerService>",worker,StringComparison.Ordinal);
    }

    [Fact]
    public void SubmissionPromotion_IsTenantConfiguredResumableAndLinksSourceEvidence()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0102_DocumentIntakeSubmissionPromotion.sql", StringComparison.Ordinal));
        var sql = Read(assembly, resource);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "DocumentIntakeService.cs"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));

        Assert.Contains("DMS.IntakePromotionConfiguration", sql, StringComparison.Ordinal);
        Assert.Contains("DMS.IntakePromotedDocument", sql, StringComparison.Ordinal);
        Assert.Contains("SubmissionIntakeId", sql, StringComparison.Ordinal);
        Assert.Contains("OpportunityLinePriorityCode", sql, StringComparison.Ordinal);
        Assert.Contains("OpportunityLineStatusCode", sql, StringComparison.Ordinal);
        Assert.Contains("GetPromotionReadinessAsync", service, StringComparison.Ordinal);
        Assert.Contains("FindExactAsync", service, StringComparison.Ordinal);
        Assert.Contains("UpdatePromotionProgressAsync", service, StringComparison.Ordinal);
        Assert.Contains("LinkDocumentsToSubmissionAsync", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO DMS.IntakePromotedDocument", repository, StringComparison.Ordinal);
        Assert.Contains("EntityName = N'Submission'", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowHardeningMigration_CoversEveryModuleAndTenantRelationship()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0130_DocumentIntakeWorkflowHardening.sql", StringComparison.Ordinal));
        var sql = Read(assembly, resource);

        foreach (var module in DocumentIntakeModules.All)
            Assert.Contains($"N'{module}.EXTRACTION'", sql, StringComparison.Ordinal);

        Assert.Contains("required\":[\"entityTypeCode\",\"entityKey\",\"path\",\"value\",\"valueTypeCode\",\"confidence\"]", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId)", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (TenantId, IntakeSessionId, IntakeWorkItemId) REFERENCES DMS.IntakeWorkItem(TenantId, IntakeSessionId, IntakeWorkItemId)", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (TenantId, IntakeSessionId, IntakeDraftFieldId) REFERENCES DMS.IntakeDraftField(TenantId, IntakeSessionId, IntakeDraftFieldId)", sql, StringComparison.Ordinal);
        Assert.Contains("UX_DMS_IntakePromotion_Session", sql, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) > 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAndPayloadContracts_UseDatabaseSettingsAndTenantSessionScope()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var worker = File.ReadAllText(Path.Combine(root, "Ams.Worker", "Documents", "DocumentIntakeWorkerService.cs"));
        var processor = File.ReadAllText(Path.Combine(root, "Ams.Worker", "Documents", "DocumentIntakeProcessor.cs"));
        var payload = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Services", "DocumentIntakePayloadStore.cs"));

        Assert.Contains("settings.WorkerBatchSize", worker, StringComparison.Ordinal);
        Assert.Contains("settings.WorkerPollIntervalSeconds", worker, StringComparison.Ordinal);
        Assert.Contains("settings.LeaseDurationSeconds", worker, StringComparison.Ordinal);
        Assert.Contains("ReadJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId", processor, StringComparison.Ordinal);
        Assert.Contains("expectedPrefix", payload, StringComparison.Ordinal);
        Assert.Contains("does not belong to the requested tenant and session scope", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewReprocessAndPromotion_AreCanonicalAtomicAndSessionUnique()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "DocumentIntakeRepository.cs"));
        var detail = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "DocumentIntake", "DocumentIntakeDetail.razor"));

        Assert.Contains("COALESCE(@Previous,@Normalized,@Extracted)", repository, StringComparison.Ordinal);
        Assert.Contains("IF @FromWorkTypeCode NOT IN", repository, StringComparison.Ordinal);
        Assert.Contains("ExecuteTransactionAsync", repository, StringComparison.Ordinal);
        Assert.Contains("WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId", repository, StringComparison.Ordinal);
        Assert.Contains("_correctionContext.Validate()", detail, StringComparison.Ordinal);
        Assert.Contains("_reprocessContext.Validate()", detail, StringComparison.Ordinal);
        Assert.Contains("_issueContext.Validate()", detail, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@_saving\"", detail, StringComparison.Ordinal);
    }

    private static string Read(System.Reflection.Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(string Path, string Body);
    private sealed class StubHandler(List<CapturedRequest> requests, params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(new(request.RequestUri!.PathAndQuery.TrimStart('/'), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responses[_index++];
        }
    }
}
