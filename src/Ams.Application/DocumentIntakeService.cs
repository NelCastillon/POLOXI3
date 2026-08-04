using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentIntake;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Application;

public sealed class DocumentIntakeService : IDocumentIntakeService
{
    private readonly IDocumentIntakeRepository _repository;
    private readonly ISubmissionIntakeService _submissionIntakeService;
    private readonly IDocumentIntakeOperationsRepository _operations;

    public DocumentIntakeService(IDocumentIntakeRepository repository, ISubmissionIntakeService submissionIntakeService, IDocumentIntakeOperationsRepository operations)
    {
        _repository = repository;
        _submissionIntakeService = submissionIntakeService;
        _operations = operations;
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

    public Task<DocumentIntakeDetailDto?> GetAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default)
        => _repository.GetAsync(tenantId, intakeSessionId, cancellationToken);

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
        var existing = await _repository.GetPromotionAsync(command.TenantId, command.IdempotencyKey, cancellationToken);
        if (existing?.TargetEntityId is Guid existingTarget)
            return new(command.IntakeSessionId, existingTarget, DocumentIntakeModules.Submission, true, "The reviewed intake was already promoted.");

        var detail = await _repository.GetAsync(command.TenantId, command.IntakeSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Document intake session was not found for the current tenant.");
        if (!string.Equals(detail.Session.ModuleCode, DocumentIntakeModules.Submission, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Promotion for module '{detail.Session.ModuleCode}' is not available. Reviewed draft data remains preserved.");
        if (!string.Equals(detail.Session.StatusCode, DocumentIntakeStatuses.Ready, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a fully reviewed READY intake session can be promoted.");
        if (detail.Issues.Any(issue => issue.SeverityCode == "ERROR" && issue.StatusCode == "OPEN"))
            throw new InvalidOperationException("Resolve all blocking intake errors before promotion.");

        var draft = await _repository.BuildReviewedSubmissionDraftAsync(command.TenantId, command.IntakeSessionId, cancellationToken);
        var requestJson = JsonSerializer.Serialize(draft);
        var promotionId = existing?.IntakePromotionId ?? await _repository.BeginPromotionAsync(command, requestJson, cancellationToken);

        var submissionIntakeId = await _submissionIntakeService.CaptureAsync(new CreateSubmissionIntakeRequest
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

        var promoted = await _submissionIntakeService.PromoteAsync(submissionIntakeId, new PromoteSubmissionIntakeRequest
        {
            TenantId = command.TenantId,
            AccountId = command.ExistingAccountId,
            CreateNewAccount = command.CreateNewAccount,
            ProcessedByUserId = command.ActorUserId
        }, cancellationToken);

        var resultJson = JsonSerializer.Serialize(promoted);
        await _repository.CompletePromotionAsync(command.TenantId, command.IntakeSessionId, promotionId, promoted.SubmissionId, resultJson, command.ActorUserId, command.RowVersion, cancellationToken);
        return new(command.IntakeSessionId, promoted.SubmissionId, DocumentIntakeModules.Submission, false, promoted.Message);
    }
}
