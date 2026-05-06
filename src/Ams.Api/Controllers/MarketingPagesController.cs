using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/marketing")]
public sealed class MarketingPagesController : ControllerBase
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public MarketingPagesController(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("email-blasts")]
    public async Task<IActionResult> SearchEmailBlasts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EmailBlastId, TenantId, CampaignId, Name, Subject, PreviewText, AudienceSegment, SenderName, SenderEmail,
       Status, ScheduledDateUtc, SentDateUtc, RecipientCount, SentCount, OpenCount, ClickCount, BounceCount, UnsubscribeCount,
       CreatedDateUtc, ModifiedDateUtc
FROM Marketing.EmailBlast
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%' OR Subject LIKE '%' + @SearchTerm + '%' OR AudienceSegment LIKE '%' + @SearchTerm + '%')
ORDER BY COALESCE(SentDateUtc, ScheduledDateUtc, CreatedDateUtc) DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingEmailBlastDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingEmailBlastDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("email-blasts/{id:guid}/send")]
    public async Task<IActionResult> SendEmailBlast(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.EmailBlast
SET Status = 'Sent', SentDateUtc = COALESCE(SentDateUtc, SYSUTCDATETIME()), SentCount = CASE WHEN SentCount = 0 THEN RecipientCount ELSE SentCount END, ModifiedDateUtc = SYSUTCDATETIME()
WHERE EmailBlastId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("email-blasts/{id:guid}/pause")]
    public async Task<IActionResult> PauseEmailBlast(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.EmailBlast SET Status = 'Paused', ModifiedDateUtc = SYSUTCDATETIME() WHERE EmailBlastId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("landing-pages")]
    public async Task<IActionResult> SearchLandingPages([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT lp.LandingPageId, lp.TenantId, lp.CampaignId, COALESCE(c.Name, '') AS CampaignName,
       lp.Name, lp.Slug, lp.TemplateName, lp.Status, lp.PublishedUrl, lp.PrimaryCta,
       lp.ViewCount, lp.ConversionCount, lp.ConversionRate, lp.LastPublishedDateUtc, lp.CreatedDateUtc, lp.ModifiedDateUtc
FROM Marketing.LandingPage lp
LEFT JOIN Comms.Campaign c ON c.CampaignId = lp.CampaignId
WHERE lp.TenantId = @TenantId AND lp.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR lp.Name LIKE '%' + @SearchTerm + '%' OR lp.Slug LIKE '%' + @SearchTerm + '%' OR lp.TemplateName LIKE '%' + @SearchTerm + '%')
ORDER BY COALESCE(lp.LastPublishedDateUtc, lp.CreatedDateUtc) DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingLandingPageDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingLandingPageDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("landing-pages/{id:guid}/publish")]
    public async Task<IActionResult> PublishLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.LandingPage
SET Status = 'Published', LastPublishedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE LandingPageId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("landing-pages/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.LandingPage SET Status = 'Archived', ModifiedDateUtc = SYSUTCDATETIME() WHERE LandingPageId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }
}
