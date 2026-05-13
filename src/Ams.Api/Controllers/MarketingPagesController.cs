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

    private async Task EnsureMarketingPageDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Marketing') EXEC(N'CREATE SCHEMA Marketing');

IF OBJECT_ID(N'Marketing.Segment', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.Segment (SegmentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL, Icon NVARCHAR(80) NOT NULL, ColorCss NVARCHAR(80) NOT NULL, Description NVARCHAR(1000) NULL, ContactCount INT NOT NULL DEFAULT 0, IsDynamic BIT NOT NULL DEFAULT 1, Rules NVARCHAR(2000) NULL, UpdatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.EmailBlast', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.EmailBlast (EmailBlastId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NULL, Name NVARCHAR(200) NOT NULL, Subject NVARCHAR(300) NOT NULL, PreviewText NVARCHAR(500) NULL, AudienceSegment NVARCHAR(200) NOT NULL, SenderName NVARCHAR(150) NOT NULL, SenderEmail NVARCHAR(254) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', ScheduledDateUtc DATETIME2 NULL, SentDateUtc DATETIME2 NULL, RecipientCount INT NOT NULL DEFAULT 0, SentCount INT NOT NULL DEFAULT 0, OpenCount INT NOT NULL DEFAULT 0, ClickCount INT NOT NULL DEFAULT 0, BounceCount INT NOT NULL DEFAULT 0, UnsubscribeCount INT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.LandingPage', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.LandingPage (LandingPageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NULL, Name NVARCHAR(200) NOT NULL, Slug NVARCHAR(200) NOT NULL, TemplateName NVARCHAR(150) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', PublishedUrl NVARCHAR(500) NULL, PrimaryCta NVARCHAR(150) NULL, ViewCount INT NOT NULL DEFAULT 0, ConversionCount INT NOT NULL DEFAULT 0, ConversionRate DECIMAL(9,2) NOT NULL DEFAULT 0, LastPublishedDateUtc DATETIME2 NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.CrossSellOpportunity', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.CrossSellOpportunity (OpportunityId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AccountName NVARCHAR(200) NOT NULL, AccountType NVARCHAR(80) NOT NULL, Producer NVARCHAR(150) NOT NULL, OpportunityType NVARCHAR(150) NOT NULL, Score INT NOT NULL DEFAULT 0, EstimatedPremium DECIMAL(18,2) NOT NULL DEFAULT 0, TriggerSignal NVARCHAR(500) NULL, LastContactDate DATETIME2 NOT NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.WinBackAccount', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.WinBackAccount (WinBackId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AccountName NVARCHAR(200) NOT NULL, PolicyType NVARCHAR(150) NOT NULL, LapseDate DATETIME2 NOT NULL, DaysLapsed INT NOT NULL DEFAULT 0, LastPremium DECIMAL(18,2) NOT NULL DEFAULT 0, LapseReason NVARCHAR(200) NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'New', LapseWindow NVARCHAR(50) NOT NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.Referral', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.Referral (ReferralId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, ProspectName NVARCHAR(200) NOT NULL, ReferredBy NVARCHAR(200) NOT NULL, ReferralType NVARCHAR(80) NOT NULL, ReceivedDate DATETIME2 NOT NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PolicyInterest NVARCHAR(200) NULL, EstimatedPremium DECIMAL(18,2) NOT NULL DEFAULT 0, Producer NVARCHAR(150) NOT NULL DEFAULT N'Unassigned', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.Review', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.Review (ReviewId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, ClientName NVARCHAR(200) NOT NULL, Platform NVARCHAR(80) NOT NULL, Rating INT NOT NULL, ReviewDate DATETIME2 NOT NULL, Content NVARCHAR(2000) NOT NULL, Response NVARCHAR(2000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.ReviewRequest', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.ReviewRequest (ReviewRequestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, ClientName NVARCHAR(200) NOT NULL, Channel NVARCHAR(80) NOT NULL, SentDate DATETIME2 NOT NULL, Platform NVARCHAR(80) NOT NULL, ReviewLeft BIT NOT NULL DEFAULT 0, NpsScore INT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.Segment WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.Segment (SegmentId,TenantId,Name,Icon,ColorCss,Description,ContactCount,IsDynamic,Rules,UpdatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Active Commercial Clients',N'bi-building',N'mks-ic-blue',N'All commercial accounts with Active status and at least one policy',3840,1,N'Status = Active|Type = Commercial|Policies >= 1',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Personal Lines Households',N'bi-house',N'mks-ic-purple',N'Household accounts with personal auto or homeowners policies',12400,1,N'Type = Personal|Policy: Auto OR HO',DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'SMB — No Workers Comp',N'bi-briefcase',N'mks-ic-amber',N'Small business accounts without an active Workers Compensation policy',1290,1,N'Type = Commercial|Employees 1-50|NO WC policy',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'NPS Promoters',N'bi-star-fill',N'mks-ic-gold',N'Contacts who gave an NPS score of 9 or 10 in the last 12 months',1750,1,N'NPS >= 9|Survey < 12mo',SYSUTCDATETIME(),0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.EmailBlast WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Home+Auto Bundle Push',N'Bundle your home and auto coverage',N'See if you can save by bundling today.',N'Personal Lines Households',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Sent',DATEADD(day,-20,SYSUTCDATETIME()),DATEADD(day,-20,SYSUTCDATETIME()),11200,11140,3220,708,38,22,DATEADD(day,-28,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Q2 Umbrella Cross-Sell',N'Is your liability protection enough?',N'Protect more with an umbrella policy review.',N'Active Commercial Clients',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Sent',DATEADD(day,-14,SYSUTCDATETIME()),DATEADD(day,-14,SYSUTCDATETIME()),4820,4790,1504,336,16,9,DATEADD(day,-24,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Lapsed Policy Win-Back',N'We can help you get covered again',N'Reactivate coverage with a quick review.',N'Lapsed — 60–180d',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Scheduled',DATEADD(day,4,SYSUTCDATETIME()),NULL,6300,0,0,0,0,0,DATEADD(day,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Google Review Request',N'How did we do?',N'Tell others about your agency experience.',N'NPS Promoters',N'AgencyBinder Team',N'reviews@agencybinder.local',N'Paused',DATEADD(day,2,SYSUTCDATETIME()),NULL,2100,0,0,0,0,0,DATEADD(day,-12,SYSUTCDATETIME()),0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.LandingPage WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Home Auto Bundle Quote',N'home-auto-bundle',N'Modern Promo',N'Published',N'https://agencybinder.local/lp/home-auto-bundle',N'Get My Bundle Quote',5820,412,7.08,DATEADD(day,-21,SYSUTCDATETIME()),DATEADD(day,-28,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Commercial Umbrella Review',N'commercial-umbrella-review',N'Insurance Benefit Card',N'Published',N'https://agencybinder.local/lp/commercial-umbrella-review',N'Schedule a Coverage Review',3040,187,6.15,DATEADD(day,-16,SYSUTCDATETIME()),DATEADD(day,-24,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Win-Back Coverage Check',N'win-back-coverage-check',N'Win-Back Offer',N'Draft',N'',N'Reactivate Coverage',0,0,0,NULL,DATEADD(day,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Review Request Thank You',N'review-thank-you',N'Plain Text',N'Archived',N'https://agencybinder.local/lp/review-thank-you',N'Leave a Review',1290,680,52.71,DATEADD(day,-5,SYSUTCDATETIME()),DATEADD(day,-12,SYSUTCDATETIME()),0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.CrossSellOpportunity WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.CrossSellOpportunity (OpportunityId,TenantId,AccountName,AccountType,Producer,OpportunityType,Score,EstimatedPremium,TriggerSignal,LastContactDate,StatusCode,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Riverside Construction LLC',N'Commercial',N'Beth Nguyen',N'Missing Umbrella',94,4200,N'Payroll > $500k, no umbrella',DATEADD(day,-12,SYSUTCDATETIME()),N'Open',0),
    (NEWID(),@TenantId,N'Harmon Family',N'Personal',N'Jake Park',N'Home + Auto Bundle',88,1800,N'2 separate carriers, bundling discount',DATEADD(day,-25,SYSUTCDATETIME()),N'Open',0),
    (NEWID(),@TenantId,N'Summit Roofing Inc',N'Commercial',N'Beth Nguyen',N'Workers Comp Expansion',85,6400,N'Added 4 employees, no WC update',DATEADD(day,-5,SYSUTCDATETIME()),N'Open',0),
    (NEWID(),@TenantId,N'Tanaka Medical Group',N'Commercial',N'Sara Kim',N'Benefits Upsell',76,12000,N'Enrolled 22 employees, no dental/vision',DATEADD(day,-10,SYSUTCDATETIME()),N'Open',0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.WinBackAccount WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.WinBackAccount (WinBackId,TenantId,AccountName,PolicyType,LapseDate,DaysLapsed,LastPremium,LapseReason,StatusCode,LapseWindow,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Marsh Family',N'Personal Auto',DATEADD(day,-52,SYSUTCDATETIME()),52,1840,N'Price',N'Contacted',N'60–90',0),
    (NEWID(),@TenantId,N'Vega Plumbing LLC',N'BOP',DATEADD(day,-69,SYSUTCDATETIME()),69,4200,N'Price',N'Interested',N'60–90',0),
    (NEWID(),@TenantId,N'Bloom Events Co',N'General Liability',DATEADD(day,-30,SYSUTCDATETIME()),30,2800,N'Went elsewhere',N'New',N'30–60',0),
    (NEWID(),@TenantId,N'Kelly Landscaping',N'Workers Comp',DATEADD(day,-33,SYSUTCDATETIME()),33,5600,N'Price',N'Reactivated',N'30–60',0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.Referral WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.Referral (ReferralId,TenantId,ProspectName,ReferredBy,ReferralType,ReceivedDate,StatusCode,PolicyInterest,EstimatedPremium,Producer,IsDeleted) VALUES
    (NEWID(),@TenantId,N'The Park Family',N'David & Lynn Chen',N'Client',DATEADD(day,-8,SYSUTCDATETIME()),N'Pending',N'Home + Auto',2400,N'Beth Nguyen',0),
    (NEWID(),@TenantId,N'Harbor View Café',N'Sullivan Mfg',N'Client',DATEADD(day,-17,SYSUTCDATETIME()),N'Contacted',N'BOP + Liquor Liab.',3100,N'Jake Park',0),
    (NEWID(),@TenantId,N'Reeves Construction',N'Apex Dist.',N'Partner',DATEADD(day,-22,SYSUTCDATETIME()),N'Converted',N'Workers Comp',7800,N'Sara Kim',0),
    (NEWID(),@TenantId,N'Sato Tech LLC',N'Online Form',N'Online',DATEADD(day,-2,SYSUTCDATETIME()),N'Pending',N'Tech E&O + Cyber',6200,N'Jake Park',0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.Review WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.Review (ReviewId,TenantId,ClientName,Platform,Rating,ReviewDate,Content,Response,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Pamela Torres',N'Google',5,DATEADD(day,-5,SYSUTCDATETIME()),N'Beth and the team made switching so easy. Been a client for 3 years now!',N'Thank you, Pamela! We appreciate your trust.',0),
    (NEWID(),@TenantId,N'Marcus Webb',N'Google',5,DATEADD(day,-9,SYSUTCDATETIME()),N'Great rates, fast claims response. Highly recommend.',N'',0),
    (NEWID(),@TenantId,N'Sato Technologies',N'Google',4,DATEADD(day,-15,SYSUTCDATETIME()),N'Solid commercial coverage, very responsive producer.',N'',0),
    (NEWID(),@TenantId,N'Kaitlyn Bloom',N'Facebook',5,DATEADD(day,-18,SYSUTCDATETIME()),N'Friendly staff, they explained every part of our policy clearly.',N'Thank you Kaitlyn!',0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.ReviewRequest WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.ReviewRequest (ReviewRequestId,TenantId,ClientName,Channel,SentDate,Platform,ReviewLeft,NpsScore,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Pamela Torres',N'SMS',DATEADD(day,-6,SYSUTCDATETIME()),N'Google',1,10,0),
    (NEWID(),@TenantId,N'Marcus Webb',N'Email',DATEADD(day,-10,SYSUTCDATETIME()),N'Google',1,9,0),
    (NEWID(),@TenantId,N'David Kim',N'Email',DATEADD(day,-7,SYSUTCDATETIME()),N'Google',0,9,0),
    (NEWID(),@TenantId,N'Ana Delgado',N'SMS',DATEADD(day,-12,SYSUTCDATETIME()),N'Facebook',0,8,0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    [HttpGet("email-blasts")]
    public async Task<IActionResult> SearchEmailBlasts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
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
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
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

    [HttpGet("segments")]
    public async Task<IActionResult> SearchSegments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT SegmentId,TenantId,Name,Icon,ColorCss,COALESCE(Description,'') AS Description,ContactCount,IsDynamic,COALESCE(Rules,'') AS Rules,UpdatedDateUtc FROM Marketing.Segment WHERE TenantId=@TenantId AND IsDeleted=0 AND (@SearchTerm IS NULL OR @SearchTerm='' OR Name LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%') ORDER BY UpdatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingSegmentDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingSegmentDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("segments")]
    public async Task<IActionResult> CreateSegment([FromBody] MarketingSegmentDto request, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Marketing.Segment (SegmentId,TenantId,Name,Icon,ColorCss,Description,ContactCount,IsDynamic,Rules,UpdatedDateUtc,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@Name,@Icon,@ColorCss,@Description,@ContactCount,@IsDynamic,@Rules,SYSUTCDATETIME(),SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Name, Icon = string.IsNullOrWhiteSpace(request.Icon) ? "bi-pie-chart" : request.Icon, ColorCss = string.IsNullOrWhiteSpace(request.ColorCss) ? "mks-ic-blue" : request.ColorCss, request.Description, request.ContactCount, request.IsDynamic, request.Rules }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("segments/{id:guid}")]
    public async Task<IActionResult> UpdateSegment(Guid id, [FromBody] MarketingSegmentDto request, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"UPDATE Marketing.Segment SET Name=@Name, Icon=@Icon, ColorCss=@ColorCss, Description=@Description, ContactCount=@ContactCount, IsDynamic=@IsDynamic, Rules=@Rules, UpdatedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE SegmentId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Name, Icon = string.IsNullOrWhiteSpace(request.Icon) ? "bi-pie-chart" : request.Icon, ColorCss = string.IsNullOrWhiteSpace(request.ColorCss) ? "mks-ic-blue" : request.ColorCss, request.Description, request.ContactCount, request.IsDynamic, request.Rules }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("cross-sell")]
    public async Task<IActionResult> SearchCrossSell([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT OpportunityId,TenantId,AccountName,AccountType,Producer,OpportunityType,Score,EstimatedPremium,COALESCE(TriggerSignal,'') AS TriggerSignal,LastContactDate,StatusCode FROM Marketing.CrossSellOpportunity WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode <> N'Dismissed' ORDER BY Score DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingCrossSellOpportunityDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingCrossSellOpportunityDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("cross-sell/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissCrossSell(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.CrossSellOpportunity SET StatusCode=N'Dismissed', ModifiedDateUtc=SYSUTCDATETIME() WHERE OpportunityId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("win-back")]
    public async Task<IActionResult> SearchWinBack([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT WinBackId,TenantId,AccountName,PolicyType,LapseDate,DaysLapsed,LastPremium,COALESCE(LapseReason,'') AS LapseReason,StatusCode,LapseWindow FROM Marketing.WinBackAccount WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY DaysLapsed DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingWinBackDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingWinBackDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("win-back/{id:guid}/status")]
    public async Task<IActionResult> UpdateWinBackStatus(Guid id, [FromBody] StatusUpdateRequest request, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.WinBackAccount SET StatusCode=@Status, ModifiedDateUtc=SYSUTCDATETIME() WHERE WinBackId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Status }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("referrals")]
    public async Task<IActionResult> SearchReferrals([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT ReferralId,TenantId,ProspectName,ReferredBy,ReferralType,ReceivedDate,StatusCode,COALESCE(PolicyInterest,'') AS PolicyInterest,EstimatedPremium,Producer FROM Marketing.Referral WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY ReceivedDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingReferralDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<MarketingReferralDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("referrals")]
    public async Task<IActionResult> CreateReferral([FromBody] MarketingReferralDto request, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Marketing.Referral (ReferralId,TenantId,ProspectName,ReferredBy,ReferralType,ReceivedDate,StatusCode,PolicyInterest,EstimatedPremium,Producer,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@ProspectName,@ReferredBy,@ReferralType,SYSUTCDATETIME(),N'Pending',@PolicyInterest,@EstimatedPremium,@Producer,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ProspectName, request.ReferredBy, request.ReferralType, request.PolicyInterest, request.EstimatedPremium, request.Producer }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPost("referrals/{id:guid}/status")]
    public async Task<IActionResult> UpdateReferralStatus(Guid id, [FromBody] StatusUpdateRequest request, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.Referral SET StatusCode=@Status, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReferralId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Status }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> SearchReviews([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        const string reviewsSql = @"SELECT ReviewId,TenantId,ClientName,Platform,Rating,ReviewDate,Content,COALESCE(Response,'') AS Response FROM Marketing.Review WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY ReviewDate DESC;";
        const string requestsSql = @"SELECT ReviewRequestId,TenantId,ClientName,Channel,SentDate,Platform,ReviewLeft,NpsScore FROM Marketing.ReviewRequest WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY SentDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var reviews = (await cn.QueryAsync<MarketingReviewDto>(new CommandDefinition(reviewsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        var requests = (await cn.QueryAsync<MarketingReviewRequestDto>(new CommandDefinition(requestsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        return Ok(new { Reviews = reviews, Requests = requests });
    }

    [HttpPost("reviews/requests")]
    public async Task<IActionResult> CreateReviewRequest([FromBody] MarketingReviewRequestDto request, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Marketing.ReviewRequest (ReviewRequestId,TenantId,ClientName,Channel,SentDate,Platform,ReviewLeft,NpsScore,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@ClientName,@Channel,SYSUTCDATETIME(),@Platform,0,@NpsScore,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClientName, request.Channel, request.Platform, request.NpsScore }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPost("landing-pages/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.LandingPage SET Status = 'Archived', ModifiedDateUtc = SYSUTCDATETIME() WHERE LandingPageId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    public sealed class StatusUpdateRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
