using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/communications")]
public sealed class CommunicationPagesController : ControllerBase
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommunicationPagesController(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT CampaignId, TenantId, Name, Type, Status, Segment, StartDate, Reached, OpenRate, Conversions, Revenue
FROM Comms.Campaign
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%' OR Segment LIKE '%' + @SearchTerm + '%' OR Type LIKE '%' + @SearchTerm + '%')
ORDER BY StartDate DESC, Name;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationCampaignDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationCampaignDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT AppointmentId, TenantId, AccountName, ContactName, Type, Channel, Status, Duration, Producer, CsrOwner, Branch, Notes, Outcome, OutcomeNotes, FollowUp, SendConfirmation, SendReminder, ScheduledDate, ScheduledTime
FROM Comms.Appointment
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountName LIKE '%' + @SearchTerm + '%' OR ContactName LIKE '%' + @SearchTerm + '%' OR Type LIKE '%' + @SearchTerm + '%')
ORDER BY ScheduledDate, ScheduledTime;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationAppointmentDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationAppointmentDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpGet("outreach")]
    public async Task<IActionResult> GetOutreach([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT OutreachContactId, TenantId, AccountName, ContactName, Email, Phone, Reason, Priority, AssignedTo, Producer, Branch, Status, LastOutcome, Notes, Attempts, OptedOut, LastContactDate, NextContactDate
FROM Comms.OutreachContact
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountName LIKE '%' + @SearchTerm + '%' OR ContactName LIKE '%' + @SearchTerm + '%' OR Reason LIKE '%' + @SearchTerm + '%')
ORDER BY CASE Priority WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 ELSE 3 END, NextContactDate;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationOutreachContactDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationOutreachContactDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }
}
