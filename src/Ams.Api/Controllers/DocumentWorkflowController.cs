using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.DocumentWorkflow;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-workflow")]
public sealed class DocumentWorkflowController : ControllerBase
{
    private readonly IDocumentWorkflowRepository _repository;

    public DocumentWorkflowController(IDocumentWorkflowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> SearchWorkflowTemplates([FromQuery] Guid tenantId, [FromQuery] string? workflowType, [FromQuery] bool? isActive, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchWorkflowTemplatesAsync(tenantId, workflowType, isActive, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("templates/active")]
    public async Task<IActionResult> GetActiveWorkflowTemplates([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _repository.GetActiveWorkflowTemplatesAsync(tenantId, cancellationToken));

    [HttpGet("templates/{id:guid}")]
    public async Task<IActionResult> GetWorkflowTemplate(Guid id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetWorkflowTemplateByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateWorkflowTemplate([FromBody] CreateWorkflowTemplateRequest request, CancellationToken cancellationToken)
        => Ok(await _repository.CreateWorkflowTemplateAsync(request, cancellationToken));

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateWorkflowTemplate(Guid id, [FromBody] UpdateWorkflowTemplateRequest request, CancellationToken cancellationToken)
    {
        if (id != request.WorkflowTemplateId) return BadRequest("Route id does not match request id.");
        await _repository.UpdateWorkflowTemplateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteWorkflowTemplate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _repository.DeleteWorkflowTemplateAsync(new DeleteWorkflowTemplateRequest(id, modifiedByUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("templates/{id:guid}/activate")]
    public async Task<IActionResult> ActivateWorkflowTemplate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _repository.ActivateWorkflowTemplateAsync(new ActivateWorkflowTemplateRequest(id, modifiedByUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("templates/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateWorkflowTemplate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _repository.DeactivateWorkflowTemplateAsync(new DeactivateWorkflowTemplateRequest(id, modifiedByUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("templates/{workflowTemplateId:guid}/steps")]
    public async Task<IActionResult> GetWorkflowTemplateSteps(Guid workflowTemplateId, CancellationToken cancellationToken)
        => Ok(await _repository.GetStepTemplatesByWorkflowIdAsync(workflowTemplateId, cancellationToken));

    [HttpPost("steps")]
    public async Task<IActionResult> CreateWorkflowStepTemplate([FromBody] CreateWorkflowStepTemplateRequest request, CancellationToken cancellationToken)
        => Ok(await _repository.CreateStepTemplateAsync(request, cancellationToken));

    [HttpPut("steps/{id:guid}")]
    public async Task<IActionResult> UpdateWorkflowStepTemplate(Guid id, [FromBody] UpdateWorkflowStepTemplateRequest request, CancellationToken cancellationToken)
    {
        if (id != request.StepTemplateId) return BadRequest("Route id does not match request id.");
        await _repository.UpdateStepTemplateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("steps/{id:guid}")]
    public async Task<IActionResult> DeleteWorkflowStepTemplate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _repository.DeleteStepTemplateAsync(new DeleteWorkflowStepTemplateRequest(id, modifiedByUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("instances")]
    public async Task<IActionResult> SearchWorkflowInstances([FromQuery] Guid tenantId, [FromQuery] string? workflowStatus, [FromQuery] Guid? documentId, [FromQuery] Guid? initiatedByUserId, [FromQuery] DateTime? startDateFrom, [FromQuery] DateTime? startDateTo, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchWorkflowInstancesAsync(tenantId, workflowStatus, documentId, initiatedByUserId, startDateFrom, startDateTo, pageNumber, pageSize, cancellationToken));

    [HttpGet("instances/active")]
    public async Task<IActionResult> GetActiveWorkflowInstances([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _repository.GetActiveWorkflowInstancesAsync(tenantId, cancellationToken));

    [HttpPost("instances")]
    public async Task<IActionResult> CreateWorkflowInstance([FromBody] CreateWorkflowInstanceRequest request, CancellationToken cancellationToken)
        => Ok(await _repository.CreateWorkflowInstanceAsync(request, cancellationToken));

    [HttpPost("instances/{id:guid}/start")]
    public async Task<IActionResult> StartWorkflowInstance(Guid id, [FromBody] StartWorkflowInstanceRequest request, CancellationToken cancellationToken)
    {
        if (id != request.WorkflowInstanceId) return BadRequest("Route id does not match request id.");
        await _repository.StartWorkflowInstanceAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("instances/{id:guid}/complete")]
    public async Task<IActionResult> CompleteWorkflowInstance(Guid id, [FromBody] CompleteWorkflowInstanceRequest request, CancellationToken cancellationToken)
    {
        if (id != request.WorkflowInstanceId) return BadRequest("Route id does not match request id.");
        await _repository.CompleteWorkflowInstanceAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("instances/{id:guid}/cancel")]
    public async Task<IActionResult> CancelWorkflowInstance(Guid id, [FromBody] CancelWorkflowInstanceRequest request, CancellationToken cancellationToken)
    {
        if (id != request.WorkflowInstanceId) return BadRequest("Route id does not match request id.");
        await _repository.CancelWorkflowInstanceAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("approvals")]
    public async Task<IActionResult> SearchApprovals([FromQuery] Guid tenantId, [FromQuery] string? approvalStatus, [FromQuery] Guid? assignedToUserId, [FromQuery] Guid? documentId, [FromQuery] DateTime? dueDateFrom, [FromQuery] DateTime? dueDateTo, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchApprovalsAsync(tenantId, approvalStatus, assignedToUserId, documentId, dueDateFrom, dueDateTo, pageNumber, pageSize, cancellationToken));

    [HttpPost("approvals/{id:guid}/approve")]
    public async Task<IActionResult> ApproveDocument(Guid id, [FromBody] ApproveDocumentRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ApprovalId) return BadRequest("Route id does not match request id.");
        await _repository.ApproveDocumentAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("approvals/{id:guid}/reject")]
    public async Task<IActionResult> RejectDocument(Guid id, [FromBody] RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ApprovalId) return BadRequest("Route id does not match request id.");
        await _repository.RejectDocumentAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> SearchReviews([FromQuery] Guid tenantId, [FromQuery] string? reviewStatus, [FromQuery] Guid? assignedToUserId, [FromQuery] Guid? documentId, [FromQuery] DateTime? dueDateFrom, [FromQuery] DateTime? dueDateTo, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchReviewsAsync(tenantId, reviewStatus, assignedToUserId, documentId, dueDateFrom, dueDateTo, pageNumber, pageSize, cancellationToken));

    [HttpPost("reviews/{id:guid}/start")]
    public async Task<IActionResult> StartReview(Guid id, [FromBody] StartReviewRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ReviewId) return BadRequest("Route id does not match request id.");
        await _repository.StartReviewAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/complete")]
    public async Task<IActionResult> CompleteReview(Guid id, [FromBody] CompleteReviewRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ReviewId) return BadRequest("Route id does not match request id.");
        await _repository.CompleteReviewAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("retention-policies")]
    public async Task<IActionResult> SearchRetentionPolicies([FromQuery] Guid tenantId, [FromQuery] bool? isActive, [FromQuery] string? applicableCategory, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchRetentionPoliciesAsync(tenantId, isActive, applicableCategory, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("retention-policies/active")]
    public async Task<IActionResult> GetActiveRetentionPolicies([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _repository.GetActiveRetentionPoliciesAsync(tenantId, cancellationToken));

    [HttpPost("retention-policies")]
    public async Task<IActionResult> CreateRetentionPolicy([FromBody] CreateRetentionPolicyRequest request, CancellationToken cancellationToken)
        => Ok(await _repository.CreateRetentionPolicyAsync(request, cancellationToken));

    [HttpPut("retention-policies/{id:guid}")]
    public async Task<IActionResult> UpdateRetentionPolicy(Guid id, [FromBody] UpdateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RetentionPolicyId) return BadRequest("Route id does not match request id.");
        await _repository.UpdateRetentionPolicyAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("retention-policies/{id:guid}")]
    public async Task<IActionResult> DeleteRetentionPolicy(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _repository.DeleteRetentionPolicyAsync(new DeleteRetentionPolicyRequest(id, modifiedByUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("audit-trail")]
    public async Task<IActionResult> SearchAuditTrail([FromQuery] Guid tenantId, [FromQuery] Guid? documentId, [FromQuery] Guid? workflowInstanceId, [FromQuery] string? eventType, [FromQuery] Guid? performedByUserId, [FromQuery] DateTime? eventDateFrom, [FromQuery] DateTime? eventDateTo, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchAuditTrailAsync(tenantId, documentId, workflowInstanceId, eventType, performedByUserId, eventDateFrom, eventDateTo, pageNumber, pageSize, cancellationToken));

    [HttpGet("classification")]
    public async Task<IActionResult> SearchClassificationQueue([FromQuery] Guid tenantId, [FromQuery] string? queueStatus, [FromQuery] Guid? assignedToUserId, [FromQuery] string? priority, [FromQuery] DateTime? dueDateFrom, [FromQuery] DateTime? dueDateTo, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _repository.SearchClassificationQueueAsync(tenantId, queueStatus, assignedToUserId, priority, dueDateFrom, dueDateTo, pageNumber, pageSize, cancellationToken));

    [HttpPost("classification")]
    public async Task<IActionResult> CreateClassificationQueue([FromBody] CreateClassificationQueueRequest request, CancellationToken cancellationToken)
        => Ok(await _repository.CreateClassificationQueueAsync(request, cancellationToken));

    [HttpPost("classification/{id:guid}/assign")]
    public async Task<IActionResult> AssignClassification(Guid id, [FromBody] AssignClassificationRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ClassificationQueueId) return BadRequest("Route id does not match request id.");
        await _repository.AssignClassificationAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("classification/{id:guid}/classify")]
    public async Task<IActionResult> ClassifyDocument(Guid id, [FromBody] ClassifyDocumentRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ClassificationQueueId) return BadRequest("Route id does not match request id.");
        await _repository.ClassifyDocumentAsync(request, cancellationToken);
        return NoContent();
    }
}
