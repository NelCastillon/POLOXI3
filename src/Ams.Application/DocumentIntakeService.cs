using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentIntake;
using Ams.Application.Features.Submissions;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Application;

public sealed class DocumentIntakeService : IDocumentIntakeService
{
    private readonly IDocumentIntakeRepository _repository;
    private readonly ISubmissionIntakeService _submissionIntakeService;
    private readonly IDocumentIntakeOperationsRepository _operations;
    private readonly ILineOfBusinessRepository _lineOfBusinessRepository;
    private readonly ISubmissionService _submissionService;

    public DocumentIntakeService(IDocumentIntakeRepository repository, ISubmissionIntakeService submissionIntakeService, IDocumentIntakeOperationsRepository operations, ILineOfBusinessRepository lineOfBusinessRepository, ISubmissionService submissionService)
    {
        _repository = repository;
        _submissionIntakeService = submissionIntakeService;
        _operations = operations;
        _lineOfBusinessRepository = lineOfBusinessRepository;
        _submissionService = submissionService;
    }

    public Task<PagedResult<DocumentIntakeSessionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? moduleCode, string? statusCode, Guid? assignedToUserId, Guid? targetEntityId = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, moduleCode, statusCode, assignedToUserId, targetEntityId, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>> GetDocumentStatusesAsync(Guid tenantId, string moduleCode, Guid targetEntityId, CancellationToken cancellationToken = default)
    {
        if (!DocumentIntakeModules.All.Contains(moduleCode))
            throw new System.ComponentModel.DataAnnotations.ValidationException("A supported document intake module is required.");
        if (targetEntityId == Guid.Empty)
            throw new System.ComponentModel.DataAnnotations.ValidationException("A target entity is required.");
        return _repository.GetDocumentStatusesAsync(tenantId, moduleCode, targetEntityId, cancellationToken);
    }

    public async Task<DocumentIntakeDetailDto?> GetAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetAsync(tenantId, intakeSessionId, cancellationToken);
        if (detail is null || !string.Equals(detail.Session.ModuleCode, DocumentIntakeModules.Submission, StringComparison.OrdinalIgnoreCase))
        {
            return detail;
        }

        var readiness = await GetPromotionReadinessAsync(detail, cancellationToken);
        return detail with { PromotionReadiness = readiness };
    }

    public Task<Guid> CreateAsync(CreateDocumentIntakeSessionCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.CreateSessionAsync(command, cancellationToken);
    }

    public Task AttachDocumentAsync(AttachDocumentToIntakeCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.AttachDocumentAsync(command, cancellationToken);
    }

    public async Task QueueAsync(QueueDocumentIntakeCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        var settings=await _operations.GetSettingsAsync(command.TenantId,cancellationToken);
        if(settings.MalwareEnabled)await _operations.EnsureDocumentCleanAsync(command.TenantId,command.IntakeSessionId,settings.MalwareFailClosed,cancellationToken);
        await _repository.QueueAsync(command, cancellationToken);
    }

    public Task ReviewFieldAsync(ReviewDocumentIntakeFieldCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.ReviewFieldAsync(command, cancellationToken);
    }

    public Task ResolveIssueAsync(ResolveDocumentIntakeIssueCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.ResolveIssueAsync(command, cancellationToken);
    }

    public Task ReprocessAsync(ReprocessDocumentIntakeCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.ReprocessAsync(command, cancellationToken);
    }

    public Task CancelAsync(CancelDocumentIntakeCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        return _repository.CancelAsync(command, cancellationToken);
    }

    public async Task<DocumentIntakePromotionResult> PromoteAsync(PromoteDocumentIntakeCommand command, CancellationToken cancellationToken = default)
    {
        DocumentIntakeValidator.Validate(command);
        var existing = await _repository.GetPromotionAsync(command.TenantId, command.IntakeSessionId, command.IdempotencyKey, cancellationToken);
        if (existing?.TargetEntityId is Guid existingTarget)
            return new(command.IntakeSessionId, existingTarget, DocumentIntakeModules.Submission, true, "The reviewed intake was already promoted.");

        var detail = await GetAsync(command.TenantId, command.IntakeSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Document intake session was not found for the current tenant.");
        if (!string.Equals(detail.Session.ModuleCode, DocumentIntakeModules.Submission, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Promotion for module '{detail.Session.ModuleCode}' is not available. Reviewed draft data remains preserved.");
        if (detail.PromotionReadiness is not { CanPromote: true } readiness)
            throw new InvalidOperationException($"Document intake cannot be promoted: {string.Join("; ", detail.PromotionReadiness?.Blockers ?? ["Promotion readiness could not be determined."])}");

        var draft = await _repository.BuildReviewedSubmissionDraftAsync(command.TenantId, command.IntakeSessionId, cancellationToken);
        var configuration = await _repository.GetPromotionConfigurationAsync(command.TenantId, detail.Session.ModuleCode, cancellationToken)
            ?? throw new InvalidOperationException("Submission promotion configuration is not active for this tenant.");
        var requestJson = JsonSerializer.Serialize(draft);
        var promotionStart = existing is null
            ? await _repository.BeginPromotionAsync(command, requestJson, cancellationToken)
            : new DocumentIntakePromotionStart(existing.IntakePromotionId, false);
        var promotionId = promotionStart.IntakePromotionId;
        if (!promotionStart.Created && existing is null)
        {
            existing = await _repository.GetPromotionAsync(command.TenantId, command.IntakeSessionId, command.IdempotencyKey, cancellationToken);
        }
        if (!promotionStart.Created && existing?.TargetEntityId is Guid concurrentTarget)
            return new(command.IntakeSessionId, concurrentTarget, DocumentIntakeModules.Submission, true, "The reviewed intake was already promoted.");
        if (!promotionStart.Created && existing?.SubmissionIntakeId is null)
            throw new InvalidOperationException("Promotion for this intake session is already in progress.");

        var submissionIntakeId = existing?.SubmissionIntakeId ?? await _submissionIntakeService.CaptureAsync(new CreateSubmissionIntakeRequest
        {
            TenantId = command.TenantId,
            Source = draft.Source,
            ApplicantName = draft.ApplicantName,
            BusinessName = draft.BusinessName,
            Fein = draft.Fein,
            Email = draft.Email,
            Phone = draft.Phone,
            AddressLine = draft.AddressLine,
            City = draft.City,
            State = draft.State,
            PostalCode = draft.PostalCode,
            ExistingPolicyNumber = draft.ExistingPolicyNumber,
            ProducerCode = draft.ProducerCode,
            LineOfBusiness = draft.LineOfBusiness,
            RequestedEffectiveDate = draft.RequestedEffectiveDate,
            EstimatedPremium = draft.EstimatedPremium,
            Attachments = string.Join(',', detail.Documents.Select(document => document.DocumentId)),
            RawPayload = requestJson,
            Notes = draft.Notes,
            AssignedToUserId = detail.Session.AssignedToUserId,
            CreatedByUserId = command.ActorUserId
            ,SourceIdempotencyKey = $"AI-DOCUMENT-INTAKE:{command.IntakeSessionId:D}:SUBMISSION"
        }, cancellationToken);
        await _repository.UpdatePromotionProgressAsync(command.TenantId, promotionId, submissionIntakeId, existing?.AccountId, existing?.OpportunityId, readiness.LobId, null, cancellationToken);

        PromoteSubmissionIntakeResult promoted;
        try
        {
            promoted = await _submissionIntakeService.PromoteAsync(submissionIntakeId, new PromoteSubmissionIntakeRequest
            {
                TenantId = command.TenantId,
                AccountId = command.ExistingAccountId ?? existing?.AccountId,
                CreateNewAccount = command.CreateNewAccount,
                LobId = readiness.LobId!.Value,
                OpportunityLinePriorityCode = configuration.OpportunityLinePriorityCode,
                OpportunityLineStatusCode = configuration.OpportunityLineStatusCode,
                OpportunityCloseDays = configuration.OpportunityCloseDays,
                OpportunityWinProbability = configuration.OpportunityWinProbability,
                SubmissionTermMonths = configuration.SubmissionTermMonths,
                ProcessedByUserId = command.ActorUserId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _repository.UpdatePromotionProgressAsync(command.TenantId, promotionId, submissionIntakeId, existing?.AccountId, existing?.OpportunityId, readiness.LobId, ex.Message, cancellationToken);
            throw;
        }

        await _repository.UpdatePromotionProgressAsync(command.TenantId, promotionId, submissionIntakeId, promoted.AccountId, promoted.OpportunityId, readiness.LobId, null, cancellationToken);

        if (configuration.LinkSourceDocuments)
        {
            await _repository.LinkDocumentsToSubmissionAsync(command.TenantId, command.IntakeSessionId, promotionId, promoted.SubmissionId, command.ActorUserId, cancellationToken);
        }

        if (configuration.CreateFollowUpTask)
        {
            await _submissionService.CreateFollowUpTaskAsync(promoted.SubmissionId, new CreateSubmissionFollowUpTaskRequest(
                command.TenantId,
                configuration.FollowUpTaskTitle ?? throw new InvalidOperationException("Configured intake follow-up task title is required."),
                configuration.FollowUpTaskDescription,
                configuration.FollowUpTaskPriorityCode,
                detail.Session.AssignedToUserId,
                configuration.FollowUpDueDays.HasValue ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(configuration.FollowUpDueDays.Value)) : null,
                command.ActorUserId), cancellationToken);
        }

        await _submissionService.AddNoteAsync(promoted.SubmissionId, new AddSubmissionNoteRequest(
            command.TenantId,
            $"Created from governed Document Intake session {detail.Session.SessionNumber}. {detail.Documents.Count} source document(s) linked.",
            command.ActorUserId), cancellationToken);

        var resultJson = JsonSerializer.Serialize(promoted);
        await _repository.CompletePromotionAsync(command.TenantId, command.IntakeSessionId, promotionId, promoted.SubmissionId, resultJson, command.ActorUserId, command.RowVersion, cancellationToken);
        return new(command.IntakeSessionId, promoted.SubmissionId, DocumentIntakeModules.Submission, false, promoted.Message);
    }

    private async Task<DocumentIntakePromotionReadinessDto> GetPromotionReadinessAsync(DocumentIntakeDetailDto detail, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var configuration = await _repository.GetPromotionConfigurationAsync(detail.Session.TenantId, detail.Session.ModuleCode, cancellationToken);
        if (configuration is null)
        {
            blockers.Add("Submission promotion configuration is not active for this tenant.");
        }
        else if (configuration.RequireReadyStatus && !string.Equals(detail.Session.StatusCode, DocumentIntakeStatuses.Ready, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("The intake must complete review and reach READY status.");
        }

        if (detail.Issues.Any(issue => issue.SeverityCode == "ERROR" && issue.StatusCode == "OPEN"))
        {
            blockers.Add("All blocking intake errors must be resolved.");
        }

        if (detail.Documents.Count == 0)
        {
            blockers.Add("At least one source document is required.");
        }

        LineOfBusinessDto? lob = null;
        var lobField = detail.DraftFields.FirstOrDefault(field => string.Equals(field.FieldPath, "submission.lineOfBusiness", StringComparison.OrdinalIgnoreCase));
        var lobValue = lobField?.ReviewedValue ?? lobField?.NormalizedValue ?? lobField?.ExtractedValue;
        if (string.IsNullOrWhiteSpace(lobValue))
        {
            blockers.Add("A reviewed line of business is required.");
        }
        else
        {
            var matches = await _lineOfBusinessRepository.FindExactAsync(detail.Session.TenantId, lobValue, cancellationToken);
            if (matches.Count == 1)
            {
                lob = matches[0];
            }
            else
            {
                blockers.Add(matches.Count == 0
                    ? $"Line of business '{lobValue}' does not match an active tenant configuration record."
                    : $"Line of business '{lobValue}' matches multiple active tenant records and must be corrected.");
            }
        }

        return new(blockers.Count == 0, lob?.LobId, lob?.LobCode, lob?.LobName, blockers);
    }
}
