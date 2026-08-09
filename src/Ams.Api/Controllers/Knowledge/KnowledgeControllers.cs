using Ams.Api.Security;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Application.Services;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Ams.Knowledge.Contracts.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers.Knowledge;

[ApiController]
public abstract class KnowledgeControllerBase : ControllerBase
{
    protected Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");

    protected Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User)
        ?? throw new UnauthorizedAccessException("An authenticated user context is required.");
}

[Route("api/knowledge/workflow-guide")]
[Authorize(Policy = KnowledgePolicies.ConceptsRead)]
public sealed class KnowledgeWorkflowGuideController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;

    public KnowledgeWorkflowGuideController(IKnowledgeQueryRepository queries) => _queries = queries;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? moduleCode, [FromQuery] string? stageName, [FromQuery] bool includeOptional = true, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchWorkflowGuideStepsAsync(new SearchWorkflowGuideStepsQuery(TenantId, searchTerm, moduleCode, stageName, includeOptional, pageNumber, pageSize), cancellationToken));
}

[Route("api/knowledge/dashboard")]
[Authorize(Policy = KnowledgePolicies.ConceptsRead)]
public sealed class KnowledgeDashboardController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    public KnowledgeDashboardController(IKnowledgeQueryRepository queries) => _queries = queries;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _queries.GetDashboardAsync(TenantId, cancellationToken));
}

[Route("api/knowledge/schemes")]
public sealed class KnowledgeSchemesController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;

    public KnowledgeSchemesController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration)
    {
        _queries = queries;
        _administration = administration;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchSchemesAsync(new SearchConceptSchemesQuery(TenantId, searchTerm, statusCode, pageNumber, pageSize), cancellationToken));

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> Create([FromBody] CreateConceptSchemeCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { ContextTenantId = TenantId, TenantId = TenantId, IsSystemDefined = false, ActorUserId = ActorUserId };
        var id = await _administration.CreateSchemeAsync(secured, cancellationToken);
        return Created($"api/knowledge/schemes/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConceptSchemeCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateSchemeAsync(command with { ConceptSchemeId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("CONCEPT_SCHEME", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }
}

[Route("api/knowledge/concepts")]
public sealed class KnowledgeConceptsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;
    private readonly IKnowledgeHierarchyService _hierarchy;

    public KnowledgeConceptsController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration, IKnowledgeHierarchyService hierarchy)
    {
        _queries = queries;
        _administration = administration;
        _hierarchy = hierarchy;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Search([FromQuery] Guid? conceptSchemeId, [FromQuery] string? searchTerm, [FromQuery] string? conceptTypeCode, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchConceptsAsync(new SearchKnowledgeConceptsQuery(TenantId, conceptSchemeId, searchTerm, conceptTypeCode, statusCode, pageNumber, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => await _queries.GetConceptAsync(TenantId, id, cancellationToken) is { } concept ? Ok(concept) : NotFound();

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeConceptCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { ContextTenantId = TenantId, TenantId = TenantId, IsSystemDefined = false, ActorUserId = ActorUserId };
        var id = await _administration.CreateConceptAsync(secured, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateKnowledgeConceptDraftCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { KnowledgeConceptId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId };
        await _administration.UpdateConceptDraftAsync(secured, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/labels")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> GetLabels(Guid id, CancellationToken cancellationToken)
        => Ok(await _queries.GetLabelsAsync(TenantId, id, cancellationToken));

    [HttpPost("{id:guid}/labels")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> AddLabel(Guid id, [FromBody] AddConceptLabelCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { KnowledgeConceptId = id, ContextTenantId = TenantId, TenantId = TenantId, IsSystemDefined = false, ActorUserId = ActorUserId };
        var labelId = await _administration.AddLabelAsync(secured, cancellationToken);
        return Created($"api/knowledge/concepts/{id}/labels/{labelId}", new { id = labelId });
    }

    [HttpPut("{id:guid}/labels/{labelId:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> UpdateLabel(Guid id, Guid labelId, [FromBody] UpdateConceptLabelCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateLabelAsync(command with { ConceptLabelId = labelId, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/labels/{labelId:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> DeleteLabel(Guid id, Guid labelId, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("CONCEPT_LABEL", command with { EntityId = labelId, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("relationships")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> AddRelationship([FromBody] AddConceptRelationshipCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { ContextTenantId = TenantId, TenantId = TenantId, IsSystemDefined = false, ActorUserId = ActorUserId };
        var id = await _administration.AddRelationshipAsync(secured, cancellationToken);
        return Created($"api/knowledge/concepts/relationships/{id}", new { id });
    }

    [HttpGet("relationships")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> GetRelationships([FromQuery] Guid? conceptId, CancellationToken cancellationToken)
        => Ok(await _queries.GetRelationshipsAsync(TenantId, conceptId, cancellationToken));

    [HttpGet("relationship-predicates")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> GetRelationshipPredicates(CancellationToken cancellationToken)
        => Ok(await _queries.GetRelationshipPredicatesAsync(cancellationToken));

    [HttpPut("relationships/{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> UpdateRelationship(Guid id, [FromBody] UpdateConceptRelationshipCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateRelationshipAsync(command with { ConceptRelationshipId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("relationships/{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> DeleteRelationship(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("CONCEPT_RELATIONSHIP", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsManage)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("KNOWLEDGE_CONCEPT", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/ancestors")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> GetAncestors(Guid id, CancellationToken cancellationToken)
        => Ok(await _hierarchy.GetAncestorsAsync(TenantId, id, cancellationToken));

    [HttpGet("{id:guid}/descendants")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> GetDescendants(Guid id, CancellationToken cancellationToken)
        => Ok(await _hierarchy.GetDescendantsAsync(TenantId, id, cancellationToken));

    [HttpGet("{id:guid}/is-descendant-of/{ancestorId:guid}")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> IsDescendant(Guid id, Guid ancestorId, CancellationToken cancellationToken)
        => Ok(new { isDescendant = await _hierarchy.IsDescendantOfAsync(TenantId, id, ancestorId, cancellationToken) });
}

[Route("api/knowledge/mappings")]
public sealed class KnowledgeMappingsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;
    private readonly IExternalMappingService _mappingService;

    public KnowledgeMappingsController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration, IExternalMappingService mappingService)
    {
        _queries = queries;
        _administration = administration;
        _mappingService = mappingService;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.MappingsRead)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? sourceSystemTypeCode, [FromQuery] bool? isApproved, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchMappingsAsync(new SearchMappingsQuery(TenantId, searchTerm, sourceSystemTypeCode, isApproved, pageNumber, pageSize), cancellationToken));

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.MappingsManage)]
    public async Task<IActionResult> Create([FromBody] CreateExternalMappingCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { TenantId = TenantId, ActorUserId = ActorUserId };
        var id = await _administration.CreateMappingAsync(secured, cancellationToken);
        return Created($"api/knowledge/mappings/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.MappingsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExternalMappingCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateMappingAsync(command with { ExternalConceptMappingId = id, TenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.MappingsManage)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("EXTERNAL_CONCEPT_MAPPING", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("resolve")]
    [Authorize(Policy = KnowledgePolicies.MappingsRead)]
    public async Task<IActionResult> Resolve([FromBody] ExternalMappingRequest request, CancellationToken cancellationToken)
        => Ok(await _mappingService.ResolveMappingAsync(request with { TenantId = TenantId }, cancellationToken));
}

[Route("api/knowledge/reviews")]
public sealed class KnowledgeMappingReviewsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;

    public KnowledgeMappingReviewsController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration)
    {
        _queries = queries;
        _administration = administration;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.MappingsRead)]
    public async Task<IActionResult> Search([FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchMappingReviewsAsync(new SearchMappingReviewsQuery(TenantId, statusCode, pageNumber, pageSize), cancellationToken));

    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = KnowledgePolicies.MappingsApprove)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewExternalMappingCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { MappingReviewId = id, TenantId = TenantId, ReviewerUserId = ActorUserId };
        await _administration.ReviewMappingAsync(secured, cancellationToken);
        return NoContent();
    }
}

[Route("api/knowledge/imports")]
public sealed class KnowledgeImportsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;
    public KnowledgeImportsController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration)
    {
        _queries = queries;
        _administration = administration;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchImportsAsync(new SearchKnowledgeImportsQuery(TenantId, searchTerm, statusCode, pageNumber, pageSize), cancellationToken));

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.Import)]
    public async Task<IActionResult> Queue([FromBody] QueueKnowledgeImportCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { TenantId = TenantId, InitialStatusCode = "QUEUED", ActorUserId = ActorUserId };
        var id = await _administration.QueueImportAsync(secured, cancellationToken);
        return Accepted($"api/knowledge/imports/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.Import)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKnowledgeImportCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateImportAsync(command with { ImportJobId = id, TenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.Import)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("KNOWLEDGE_IMPORT", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }
}

[Route("api/knowledge/publications")]
public sealed class KnowledgePublicationsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;
    public KnowledgePublicationsController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration)
    {
        _queries = queries;
        _administration = administration;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchPublicationsAsync(new SearchKnowledgePublicationsQuery(TenantId, searchTerm, statusCode, pageNumber, pageSize), cancellationToken));

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.Publish)]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgePublicationCommand command, CancellationToken cancellationToken)
    {
        var id = await _administration.CreatePublicationAsync(command with { ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return Created($"api/knowledge/publications/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.Publish)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKnowledgePublicationCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdatePublicationAsync(command with { PublicationId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.Publish)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("KNOWLEDGE_PUBLICATION", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = KnowledgePolicies.Publish)]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishKnowledgeCommand command, CancellationToken cancellationToken)
    {
        var secured = command with { PublicationId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId };
        await _administration.PublishAsync(secured, cancellationToken);
        return NoContent();
    }

[Route("api/knowledge/rules")]
public sealed class KnowledgeRulesController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    private readonly IKnowledgeAdministrationService _administration;

    public KnowledgeRulesController(IKnowledgeQueryRepository queries, IKnowledgeAdministrationService administration)
    {
        _queries = queries;
        _administration = administration;
    }

    [HttpGet]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchValidationRulesAsync(TenantId, searchTerm, statusCode, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    [Authorize(Policy = KnowledgePolicies.RulesManage)]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken)
    {
        var id = await _administration.CreateValidationRuleAsync(command with { ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return Created($"api/knowledge/rules/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.RulesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken)
    {
        await _administration.UpdateValidationRuleAsync(command with { ConceptValidationRuleId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = KnowledgePolicies.RulesManage)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken)
    {
        await _administration.SoftDeleteAsync("CONCEPT_VALIDATION_RULE", command with { EntityId = id, ContextTenantId = TenantId, ActorUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }
}
}

[Route("api/knowledge/semantic")]
public sealed class KnowledgeSemanticServicesController : KnowledgeControllerBase
{
    private readonly IConceptResolver _resolver;
    private readonly IKnowledgeValidationService _validation;

    public KnowledgeSemanticServicesController(IConceptResolver resolver, IKnowledgeValidationService validation)
    {
        _resolver = resolver;
        _validation = validation;
    }

    [HttpPost("resolve")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Resolve([FromBody] ConceptResolutionRequest request, CancellationToken cancellationToken)
        => Ok(await _resolver.ResolveAsync(request with { TenantId = TenantId }, cancellationToken));

    [HttpPost("validate")]
    [Authorize(Policy = KnowledgePolicies.ConceptsRead)]
    public async Task<IActionResult> Validate([FromBody] SemanticValidationRequest request, CancellationToken cancellationToken)
        => Ok(await _validation.ValidateAsync(request with { TenantId = TenantId }, cancellationToken));
}

[Route("api/knowledge/lookups")]
[Authorize(Policy = KnowledgePolicies.ConceptsRead)]
public sealed class KnowledgeLookupsController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    public KnowledgeLookupsController(IKnowledgeQueryRepository queries) => _queries = queries;

    [HttpGet("{lookupTypeCode}")]
    public async Task<IActionResult> Get(string lookupTypeCode, CancellationToken cancellationToken)
        => Ok(await _queries.GetLookupsAsync(new GetKnowledgeLookupsQuery(lookupTypeCode, TenantId), cancellationToken));
}

[Route("api/knowledge/audit")]
[Authorize(Policy = KnowledgePolicies.AuditRead)]
public sealed class KnowledgeAuditController : KnowledgeControllerBase
{
    private readonly IKnowledgeQueryRepository _queries;
    public KnowledgeAuditController(IKnowledgeQueryRepository queries) => _queries = queries;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? actionType, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _queries.SearchAuditAsync(new SearchKnowledgeAuditQuery(TenantId, searchTerm, actionType, pageNumber, pageSize), cancellationToken));
}
