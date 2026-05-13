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

    private async Task EnsureCampaignDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.Campaign', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.Campaign (CampaignId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL, Type NVARCHAR(80) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', Segment NVARCHAR(200) NOT NULL, StartDate DATETIME2 NOT NULL, Reached INT NOT NULL DEFAULT 0, OpenRate DECIMAL(9,2) NOT NULL DEFAULT 0, Conversions INT NOT NULL DEFAULT 0, Revenue DECIMAL(18,2) NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF NOT EXISTS (SELECT 1 FROM Comms.Campaign WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Home+Auto Bundle Push',N'Email',N'Active',N'Personal Lines Households',DATEADD(day,-21,SYSUTCDATETIME()),11200,28.9,412,206000,DATEADD(day,-28,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Q2 Cross-Sell — Umbrella',N'Multi-Channel',N'Active',N'Active Commercial Clients',DATEADD(day,-16,SYSUTCDATETIME()),4820,31.4,187,94000,DATEADD(day,-24,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Lapsed Policy Win-Back',N'Email',N'Scheduled',N'Lapsed — 60–180d',DATEADD(day,4,SYSUTCDATETIME()),6300,24.6,231,115500,DATEADD(day,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Google Review Request — NPS 9+',N'SMS',N'Paused',N'NPS Promoters',DATEADD(day,-5,SYSUTCDATETIME()),2100,41.2,680,0,DATEADD(day,-12,SYSUTCDATETIME()),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(tenantId, cancellationToken);
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

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CommunicationCampaignDto request, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@Name,@Type,@Status,@Segment,@StartDate,@Reached,@OpenRate,@Conversions,@Revenue,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Name, request.Type, request.Status, request.Segment, request.StartDate, request.Reached, request.OpenRate, request.Conversions, request.Revenue }, cancellationToken: cancellationToken));
        return Ok(new { id });
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
