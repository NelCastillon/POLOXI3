using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/submissions")]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _service;
    public SubmissionsController(ISubmissionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? lineOfBusiness,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubmissionRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignSubmissionRequest request, CancellationToken cancellationToken)
    {
        await _service.AssignAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetActivities(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetActivitiesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddSubmissionNoteRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await _service.AddNoteAsync(id, request, cancellationToken) });

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetDocumentsAsync(id, tenantId, cancellationToken));

    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetTasksAsync(id, tenantId, cancellationToken));

    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> CreateFollowUpTask(Guid id, [FromBody] CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await _service.CreateFollowUpTaskAsync(id, request, cancellationToken) });

    [HttpGet("{id:guid}/lines")]
    public async Task<IActionResult> GetLines(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetLinesAsync(id, cancellationToken));

    [HttpGet("{id:guid}/intake")]
    public async Task<IActionResult> GetIntake(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetIntakeAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/intake/{intakeQuestionId:guid}")]
    public async Task<IActionResult> UpdateIntakeQuestion(Guid id, Guid intakeQuestionId, [FromBody] UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateIntakeQuestionAsync(id, intakeQuestionId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/document-checklist")]
    public async Task<IActionResult> GetDocumentChecklist(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetDocumentChecklistAsync(id, tenantId, cancellationToken));

    [HttpGet("{id:guid}/readiness")]
    public async Task<IActionResult> GetReadiness(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetReadinessAsync(id, tenantId, cancellationToken));

    [HttpGet("task-templates")]
    public async Task<IActionResult> GetTaskTemplates([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetTaskTemplatesAsync(tenantId, cancellationToken));

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetMetricsAsync(tenantId, cancellationToken));

    [HttpPost("{id:guid}/submit-to-market")]
    public async Task<IActionResult> SubmitToMarket(Guid id, [FromBody] SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SubmitToMarketAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/request-quote")]
    public async Task<IActionResult> RequestQuote(Guid id, [FromBody] RequestSubmissionQuoteRequest request, CancellationToken cancellationToken)
        => Ok(await _service.RequestQuoteAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id, [FromBody] CopySubmissionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CopyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, [FromBody] DeclineSubmissionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.DeclineAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/create-policy")]
    public async Task<IActionResult> CreatePolicy(Guid id, [FromBody] CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreatePolicyAsync(id, request, cancellationToken));

    // ── Markets ───────────────────────────────────────────────────────

    [HttpGet("{id:guid}/markets")]
    public async Task<IActionResult> GetMarkets(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetMarketsAsync(id, cancellationToken));

    [HttpGet("{id:guid}/markets/suggestions")]
    public async Task<IActionResult> GetMarketSuggestions(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetMarketSuggestionsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/markets")]
    public async Task<IActionResult> AddMarket(Guid id, [FromBody] AddSubmissionMarketRequest request, CancellationToken cancellationToken)
    {
        var marketId = await _service.AddMarketAsync(request with { SubmissionId = id }, cancellationToken);
        return Ok(new { id = marketId });
    }

    [HttpPatch("markets/{marketId:guid}/status")]
    public async Task<IActionResult> UpdateMarketStatus(Guid marketId, [FromBody] UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateMarketStatusAsync(marketId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("markets/{marketId:guid}/package")]
    public async Task<IActionResult> UpdateMarketPackage(Guid marketId, [FromBody] UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateMarketPackageAsync(request with { SubmissionMarketId = marketId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("markets/{marketId:guid}")]
    public async Task<IActionResult> RemoveMarket(Guid marketId, CancellationToken cancellationToken)
    {
        await _service.RemoveMarketAsync(marketId, cancellationToken);
        return NoContent();
    }

    // ── Bound Policy ──────────────────────────────────────────────────

    [HttpGet("policies")]
    public async Task<IActionResult> SearchPolicies(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? lineOfBusiness,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _service.SearchPoliciesAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken));

    [HttpGet("policies/{policyId:guid}")]
    public async Task<IActionResult> GetPolicyById(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await _service.GetPolicyByIdAsync(policyId, cancellationToken);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicyRegister([FromBody] UpsertPolicyRegisterRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreatePolicyRegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPolicyById), new { policyId = id }, new { id });
    }

    [HttpPut("policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicyRegister(Guid policyId, [FromBody] UpsertPolicyRegisterRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdatePolicyRegisterAsync(policyId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("policies/{policyId:guid}/actions")]
    public async Task<IActionResult> ExecutePolicyRegisterAction(Guid policyId, [FromBody] PolicyRegisterActionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ExecutePolicyRegisterActionAsync(policyId, request, cancellationToken));

    [HttpGet("{id:guid}/quotes")]
    public async Task<IActionResult> GetQuotes(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetQuoteComparisonAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/quotes/{quoteId:guid}")]
    public async Task<IActionResult> UpdateQuote(Guid id, Guid quoteId, [FromBody] UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateQuoteAsync(quoteId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/quotes/select")]
    public async Task<IActionResult> SelectQuote(Guid id, [FromBody] SelectSubmissionQuoteRequest request, CancellationToken cancellationToken)
    {
        await _service.SelectQuoteAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/proposals")]
    public async Task<IActionResult> GetProposals(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetProposalsAsync(id, cancellationToken));

    [HttpGet("proposals/{proposalId:guid}")]
    public async Task<IActionResult> GetProposalById(Guid proposalId, CancellationToken cancellationToken)
    {
        var proposal = await _service.GetProposalByIdAsync(proposalId, cancellationToken);
        return proposal is null ? NotFound() : Ok(proposal);
    }

    [HttpPost("{id:guid}/proposals")]
    public async Task<IActionResult> GenerateProposal(Guid id, [FromBody] GenerateProposalRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await _service.GenerateProposalAsync(request with { SubmissionId = id }, cancellationToken) });

    [HttpPost("proposals/{proposalId:guid}/deliver")]
    public async Task<IActionResult> DeliverProposal(Guid proposalId, [FromBody] ProposalDeliveryRequest request, CancellationToken cancellationToken)
    {
        await _service.DeliverProposalAsync(proposalId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("proposals/{proposalId:guid}/decision")]
    public async Task<IActionResult> RecordProposalDecision(Guid proposalId, [FromBody] ProposalDecisionRequest request, CancellationToken cancellationToken)
    {
        await _service.RecordProposalDecisionAsync(proposalId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/policy")]
    public async Task<IActionResult> GetPolicy(Guid id, CancellationToken cancellationToken)
    {
        var policy = await _service.GetPolicyBySubmissionAsync(id, cancellationToken);
        return policy is null ? NotFound() : Ok(policy);
    }
}

[ApiController]
[Route("api/submissions/reference-options")]
public sealed class SubmissionReferenceOptionsController : ControllerBase
{
    private readonly ISubmissionReferenceOptionService _service;
    public SubmissionReferenceOptionsController(ISubmissionReferenceOptionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid tenantId,
        [FromQuery] string? optionGroup = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(tenantId, optionGroup, cancellationToken));
}
