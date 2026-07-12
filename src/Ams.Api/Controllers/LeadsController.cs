using Ams.Api.Hubs;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Leads;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LeadsController : ControllerBase
{
    private readonly ILeadService _service;
    private readonly IHubContext<LeadScoringHub> _leadScoringHub;

    public LeadsController(ILeadService service, IHubContext<LeadScoringHub> leadScoringHub)
    {
        _service = service;
        _leadScoringHub = leadScoringHub;
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Ams.Application.Features.Leads.CreateLeadRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/score-factors")]
    public async Task<IActionResult> GetScoreFactors(Guid id, CancellationToken cancellationToken)
    {
        var items = await _service.GetScoreFactorsAsync(id, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}/engagement")]
    public async Task<IActionResult> GetEngagementSummary(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetEngagementSummaryAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("engagement-options")]
    public async Task<IActionResult> GetEngagementOptions([FromQuery] Guid tenantId, [FromQuery] string? optionType, CancellationToken cancellationToken)
    {
        var items = await _service.GetEngagementOptionsAsync(tenantId, optionType, cancellationToken);
        return Ok(items);
    }

    [HttpGet("campaign-options")]
    public async Task<IActionResult> GetCampaignOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var items = await _service.GetCampaignOptionsAsync(tenantId, cancellationToken);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = id;
        await _service.UpdateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{leadId:guid}/contacts")]
    public async Task<IActionResult> GetContacts(Guid leadId, CancellationToken cancellationToken) => Ok(await _service.GetContactsAsync(leadId, cancellationToken));

    [HttpPost("{leadId:guid}/contacts")]
    public async Task<IActionResult> CreateContact(Guid leadId, [FromBody] CreateLeadContactRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        var id = await _service.CreateContactAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetContacts), new { leadId }, id);
    }

    [HttpPut("{leadId:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> UpdateContact(Guid leadId, Guid contactId, [FromBody] UpdateLeadContactRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        request.ContactId = contactId;
        await _service.UpdateContactAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid contactId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteContactAsync(contactId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{leadId:guid}/interest-lines")]
    public async Task<IActionResult> GetInterestLines(Guid leadId, CancellationToken cancellationToken) => Ok(await _service.GetInterestLinesAsync(leadId, cancellationToken));

    [HttpPost("{leadId:guid}/interest-lines")]
    public async Task<IActionResult> CreateInterestLine(Guid leadId, [FromBody] CreateLeadInterestLineRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        var id = await _service.CreateInterestLineAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInterestLines), new { leadId }, id);
    }

    [HttpPut("{leadId:guid}/interest-lines/{interestLineId:guid}")]
    public async Task<IActionResult> UpdateInterestLine(Guid leadId, Guid interestLineId, [FromBody] UpdateLeadInterestLineRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        request.InterestLineId = interestLineId;
        await _service.UpdateInterestLineAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("interest-lines/{interestLineId:guid}")]
    public async Task<IActionResult> DeleteInterestLine(Guid interestLineId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteInterestLineAsync(interestLineId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{leadId:guid}/communications")]
    public async Task<IActionResult> GetCommunications(Guid leadId, CancellationToken cancellationToken) => Ok(await _service.GetCommunicationsAsync(leadId, cancellationToken));

    [HttpPost("{leadId:guid}/communications")]
    public async Task<IActionResult> CreateCommunication(Guid leadId, [FromBody] CreateLeadCommunicationRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        var id = await _service.CreateCommunicationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCommunications), new { leadId }, id);
    }

    [HttpPut("{leadId:guid}/communications/{communicationId:guid}")]
    public async Task<IActionResult> UpdateCommunication(Guid leadId, Guid communicationId, [FromBody] UpdateLeadCommunicationRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        request.CommunicationId = communicationId;
        await _service.UpdateCommunicationAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("communications/{communicationId:guid}")]
    public async Task<IActionResult> DeleteCommunication(Guid communicationId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteCommunicationAsync(communicationId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{leadId:guid}/campaigns")]
    public async Task<IActionResult> GetCampaigns(Guid leadId, CancellationToken cancellationToken) => Ok(await _service.GetCampaignEnrollmentsAsync(leadId, cancellationToken));

    [HttpPost("{leadId:guid}/campaigns")]
    public async Task<IActionResult> CreateCampaign(Guid leadId, [FromBody] CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        var id = await _service.CreateCampaignEnrollmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCampaigns), new { leadId }, id);
    }

    [HttpPut("{leadId:guid}/campaigns/{enrollmentId:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid leadId, Guid enrollmentId, [FromBody] UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        request.EnrollmentId = enrollmentId;
        await _service.UpdateCampaignEnrollmentAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("campaigns/{enrollmentId:guid}")]
    public async Task<IActionResult> DeleteCampaign(Guid enrollmentId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteCampaignEnrollmentAsync(enrollmentId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{leadId:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid leadId, CancellationToken cancellationToken) => Ok(await _service.GetDocumentsAsync(leadId, cancellationToken));

    [HttpPost("{leadId:guid}/documents")]
    public async Task<IActionResult> CreateDocument(Guid leadId, [FromBody] CreateLeadDocumentRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        var id = await _service.CreateDocumentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDocuments), new { leadId }, id);
    }

    [HttpPut("{leadId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid leadId, Guid documentId, [FromBody] UpdateLeadDocumentRequest request, CancellationToken cancellationToken)
    {
        request.LeadId = leadId;
        request.DocumentId = documentId;
        await _service.UpdateDocumentAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteDocumentAsync(documentId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("scoring-rules")]
    public async Task<IActionResult> GetScoringRules([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var rules = await _service.GetScoringRulesAsync(tenantId, cancellationToken);
        return Ok(rules);
    }

    [HttpPost("scoring-rules")]
    public async Task<IActionResult> CreateScoringRule([FromBody] CreateLeadScoringRuleRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateScoringRuleAsync(request, cancellationToken);
        await NotifyLeadScoresChangedAsync(request.TenantId, cancellationToken);
        return CreatedAtAction(nameof(GetScoringRules), new { tenantId = request.TenantId }, id);
    }

    [HttpPut("scoring-rules/{scoringRuleId:guid}")]
    public async Task<IActionResult> UpdateScoringRule(Guid scoringRuleId, [FromBody] UpdateLeadScoringRuleRequest request, CancellationToken cancellationToken)
    {
        request.ScoringRuleId = scoringRuleId;
        await _service.UpdateScoringRuleAsync(request, cancellationToken);
        await NotifyLeadScoresChangedAsync(request.TenantId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("scoring-rules/{scoringRuleId:guid}")]
    public async Task<IActionResult> DeleteScoringRule(Guid scoringRuleId, CancellationToken cancellationToken)
    {
        var tenantId = await _service.GetScoringRuleTenantIdAsync(scoringRuleId, cancellationToken);
        await _service.DeleteScoringRuleAsync(scoringRuleId, cancellationToken);
        if (tenantId.HasValue)
        {
            await NotifyLeadScoresChangedAsync(tenantId.Value, cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("engagement-factors")]
    public async Task<IActionResult> GetEngagementFactors([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var factors = await _service.GetEngagementFactorsAsync(tenantId, cancellationToken);
        return Ok(factors);
    }

    [HttpPost("engagement-factors")]
    public async Task<IActionResult> CreateEngagementFactor([FromBody] CreateLeadEngagementFactorRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateEngagementFactorAsync(request, cancellationToken);
        await NotifyLeadScoresChangedAsync(request.TenantId, cancellationToken);
        return CreatedAtAction(nameof(GetEngagementFactors), new { tenantId = request.TenantId }, id);
    }

    [HttpPut("engagement-factors/{engagementFactorId:guid}")]
    public async Task<IActionResult> UpdateEngagementFactor(Guid engagementFactorId, [FromBody] UpdateLeadEngagementFactorRequest request, CancellationToken cancellationToken)
    {
        request.EngagementFactorId = engagementFactorId;
        await _service.UpdateEngagementFactorAsync(request, cancellationToken);
        await NotifyLeadScoresChangedAsync(request.TenantId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("engagement-factors/{engagementFactorId:guid}")]
    public async Task<IActionResult> DeleteEngagementFactor(Guid engagementFactorId, CancellationToken cancellationToken)
    {
        await _service.DeleteEngagementFactorAsync(engagementFactorId, cancellationToken);
        return NoContent();
    }

    private Task NotifyLeadScoresChangedAsync(Guid tenantId, CancellationToken cancellationToken)
        => _leadScoringHub.Clients.Group(LeadScoringHub.TenantGroup(tenantId)).SendAsync("LeadScoresChanged", tenantId, cancellationToken);
}