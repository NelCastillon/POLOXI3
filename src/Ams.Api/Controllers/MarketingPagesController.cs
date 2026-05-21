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

IF OBJECT_ID(N'Marketing.AnalyticsMetric', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.AnalyticsMetric (AnalyticsMetricId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignName NVARCHAR(200) NOT NULL, Channel NVARCHAR(80) NOT NULL, CampaignType NVARCHAR(80) NOT NULL, SegmentName NVARCHAR(200) NOT NULL, Goal NVARCHAR(150) NOT NULL, PeriodCode NVARCHAR(20) NOT NULL, PeriodStartDate DATETIME2 NOT NULL, PeriodEndDate DATETIME2 NOT NULL, Reached INT NOT NULL DEFAULT 0, OpenRate DECIMAL(9,2) NOT NULL DEFAULT 0, ClickRate DECIMAL(9,2) NOT NULL DEFAULT 0, Conversions INT NOT NULL DEFAULT 0, Revenue DECIMAL(18,2) NOT NULL DEFAULT 0, Spend DECIMAL(18,2) NOT NULL DEFAULT 0, UnsubscribeRate DECIMAL(9,2) NOT NULL DEFAULT 0, Status NVARCHAR(50) NOT NULL DEFAULT N'Active', UpdatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
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
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.AnalyticsMetric WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.AnalyticsMetric (AnalyticsMetricId,TenantId,CampaignName,Channel,CampaignType,SegmentName,Goal,PeriodCode,PeriodStartDate,PeriodEndDate,Reached,OpenRate,ClickRate,Conversions,Revenue,Spend,UnsubscribeRate,Status,UpdatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Home+Auto Bundle Push',N'Email',N'Email',N'Personal Lines Households',N'Cross-Sell',N'90',DATEADD(day,-90,SYSUTCDATETIME()),SYSUTCDATETIME(),11200,28.90,6.40,412,348000,45200,0.18,N'Active',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Q2 Umbrella Cross-Sell',N'Email',N'Multi-Channel',N'Active Commercial Clients',N'Cross-Sell',N'90',DATEADD(day,-90,SYSUTCDATETIME()),SYSUTCDATETIME(),4820,31.40,7.00,187,274000,31800,0.12,N'Active',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Lapsed Policy Win-Back',N'SMS',N'SMS',N'Lapsed — 60–180d',N'Win-Back',N'90',DATEADD(day,-90,SYSUTCDATETIME()),SYSUTCDATETIME(),6300,24.10,5.20,146,196000,17800,0.24,N'Active',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Google Review Request',N'Email',N'Email',N'NPS Promoters',N'Reviews',N'90',DATEADD(day,-90,SYSUTCDATETIME()),SYSUTCDATETIME(),2100,42.30,9.70,680,92000,6800,0.05,N'Active',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Referral Partner Nurture',N'Direct Mail',N'Direct Mail',N'Partner Referrers',N'Referrals',N'90',DATEADD(day,-90,SYSUTCDATETIME()),SYSUTCDATETIME(),1750,18.60,4.20,74,158000,22100,0.07,N'Active',SYSUTCDATETIME(),0);
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

    [HttpPost("email-blasts/seed")]
    public async Task<IActionResult> EnsureEmailBlastSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("email-blasts")]
    public async Task<IActionResult> CreateEmailBlast([FromBody] MarketingEmailBlastDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.AudienceSegment) || string.IsNullOrWhiteSpace(request.SenderName) || string.IsNullOrWhiteSpace(request.SenderEmail))
        {
            return BadRequest("Tenant, name, subject, audience, sender name, and sender email are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,CampaignId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@CampaignId,@Name,@Subject,@PreviewText,@AudienceSegment,@SenderName,@SenderEmail,@Status,@ScheduledDateUtc,@SentDateUtc,@RecipientCount,@SentCount,@OpenCount,@ClickCount,@BounceCount,@UnsubscribeCount,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CampaignId, request.Name, request.Subject, request.PreviewText, request.AudienceSegment, request.SenderName, request.SenderEmail, Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status, request.ScheduledDateUtc, request.SentDateUtc, request.RecipientCount, request.SentCount, request.OpenCount, request.ClickCount, request.BounceCount, request.UnsubscribeCount }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("email-blasts/{id:guid}")]
    public async Task<IActionResult> UpdateEmailBlast(Guid id, [FromBody] MarketingEmailBlastDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.AudienceSegment) || string.IsNullOrWhiteSpace(request.SenderName) || string.IsNullOrWhiteSpace(request.SenderEmail))
        {
            return BadRequest("Tenant, name, subject, audience, sender name, and sender email are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.EmailBlast
SET CampaignId=@CampaignId, Name=@Name, Subject=@Subject, PreviewText=@PreviewText, AudienceSegment=@AudienceSegment,
    SenderName=@SenderName, SenderEmail=@SenderEmail, Status=@Status, ScheduledDateUtc=@ScheduledDateUtc, SentDateUtc=@SentDateUtc,
    RecipientCount=@RecipientCount, SentCount=@SentCount, OpenCount=@OpenCount, ClickCount=@ClickCount, BounceCount=@BounceCount,
    UnsubscribeCount=@UnsubscribeCount, ModifiedDateUtc=SYSUTCDATETIME()
WHERE EmailBlastId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CampaignId, request.Name, request.Subject, request.PreviewText, request.AudienceSegment, request.SenderName, request.SenderEmail, Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status, request.ScheduledDateUtc, request.SentDateUtc, request.RecipientCount, request.SentCount, request.OpenCount, request.ClickCount, request.BounceCount, request.UnsubscribeCount }, cancellationToken: cancellationToken));
        return NoContent();
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

    [HttpPost("email-blasts/{id:guid}/resume")]
    public async Task<IActionResult> ResumeEmailBlast(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.EmailBlast
SET Status = CASE WHEN ScheduledDateUtc IS NULL THEN 'Draft' ELSE 'Scheduled' END, ModifiedDateUtc = SYSUTCDATETIME()
WHERE EmailBlastId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("email-blasts/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateEmailBlast(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,CampaignId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CampaignId,CONCAT(Name, N' Copy'),Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,N'Draft',NULL,NULL,RecipientCount,0,0,0,0,0,SYSUTCDATETIME(),0
FROM Marketing.EmailBlast
WHERE EmailBlastId = @Id AND IsDeleted = 0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("email-blasts/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveEmailBlast(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.EmailBlast SET Status = N'Archived', IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE EmailBlastId = @Id AND IsDeleted = 0;";
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

    [HttpPost("landing-pages/seed")]
    public async Task<IActionResult> EnsureLandingPageSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("landing-pages")]
    public async Task<IActionResult> CreateLandingPage([FromBody] MarketingLandingPageDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest("Tenant, name, slug, and template are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,CampaignId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@CampaignId,@Name,@Slug,@TemplateName,@Status,@PublishedUrl,@PrimaryCta,@ViewCount,@ConversionCount,@ConversionRate,@LastPublishedDateUtc,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CampaignId, request.Name, request.Slug, request.TemplateName, Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status, request.PublishedUrl, request.PrimaryCta, request.ViewCount, request.ConversionCount, request.ConversionRate, request.LastPublishedDateUtc }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("landing-pages/{id:guid}")]
    public async Task<IActionResult> UpdateLandingPage(Guid id, [FromBody] MarketingLandingPageDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest("Tenant, name, slug, and template are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.LandingPage
SET CampaignId=@CampaignId, Name=@Name, Slug=@Slug, TemplateName=@TemplateName, Status=@Status, PublishedUrl=@PublishedUrl,
    PrimaryCta=@PrimaryCta, ViewCount=@ViewCount, ConversionCount=@ConversionCount, ConversionRate=@ConversionRate,
    LastPublishedDateUtc=@LastPublishedDateUtc, ModifiedDateUtc=SYSUTCDATETIME()
WHERE LandingPageId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CampaignId, request.Name, request.Slug, request.TemplateName, Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status, request.PublishedUrl, request.PrimaryCta, request.ViewCount, request.ConversionCount, request.ConversionRate, request.LastPublishedDateUtc }, cancellationToken: cancellationToken));
        return NoContent();
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

    [HttpPost("landing-pages/{id:guid}/unpublish")]
    public async Task<IActionResult> UnpublishLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.LandingPage SET Status = 'Draft', ModifiedDateUtc = SYSUTCDATETIME() WHERE LandingPageId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("landing-pages/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,CampaignId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CampaignId,CONCAT(Name, N' Copy'),CONCAT(Slug, N'-copy'),TemplateName,N'Draft',N'',PrimaryCta,0,0,0,NULL,SYSUTCDATETIME(),0
FROM Marketing.LandingPage
WHERE LandingPageId = @Id AND IsDeleted = 0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
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

    [HttpPost("segments/seed")]
    public async Task<IActionResult> EnsureSegmentSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
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

    [HttpPost("segments/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateSegment(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.Segment (SegmentId,TenantId,Name,Icon,ColorCss,Description,ContactCount,IsDynamic,Rules,UpdatedDateUtc,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CONCAT(Name, N' Copy'),Icon,ColorCss,Description,ContactCount,IsDynamic,Rules,SYSUTCDATETIME(),SYSUTCDATETIME(),0
FROM Marketing.Segment
WHERE SegmentId = @Id AND IsDeleted = 0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("segments/{id:guid}/recalculate")]
    public async Task<IActionResult> RecalculateSegment(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.Segment
SET ContactCount = CASE WHEN ContactCount <= 0 THEN 250 ELSE ContactCount + ABS(CHECKSUM(NEWID())) % 37 END,
    UpdatedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE SegmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("segments/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveSegment(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.Segment SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE SegmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
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

    [HttpPost("cross-sell/seed")]
    public async Task<IActionResult> EnsureCrossSellSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("cross-sell")]
    public async Task<IActionResult> CreateCrossSell([FromBody] MarketingCrossSellOpportunityDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.OpportunityType))
        {
            return BadRequest("Tenant, account name, and opportunity type are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Marketing.CrossSellOpportunity (OpportunityId,TenantId,AccountName,AccountType,Producer,OpportunityType,Score,EstimatedPremium,TriggerSignal,LastContactDate,StatusCode,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@AccountName,@AccountType,@Producer,@OpportunityType,@Score,@EstimatedPremium,@TriggerSignal,@LastContactDate,@StatusCode,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountName, AccountType = string.IsNullOrWhiteSpace(request.AccountType) ? "Commercial" : request.AccountType, Producer = string.IsNullOrWhiteSpace(request.Producer) ? "Unassigned" : request.Producer, request.OpportunityType, Score = Math.Clamp(request.Score, 0, 100), request.EstimatedPremium, request.TriggerSignal, LastContactDate = request.LastContactDate == default ? DateTime.UtcNow : request.LastContactDate, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Open" : request.StatusCode }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("cross-sell/{id:guid}")]
    public async Task<IActionResult> UpdateCrossSell(Guid id, [FromBody] MarketingCrossSellOpportunityDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.OpportunityType))
        {
            return BadRequest("Tenant, account name, and opportunity type are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.CrossSellOpportunity
SET AccountName=@AccountName, AccountType=@AccountType, Producer=@Producer, OpportunityType=@OpportunityType,
    Score=@Score, EstimatedPremium=@EstimatedPremium, TriggerSignal=@TriggerSignal, LastContactDate=@LastContactDate,
    StatusCode=@StatusCode, ModifiedDateUtc=SYSUTCDATETIME()
WHERE OpportunityId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountName, AccountType = string.IsNullOrWhiteSpace(request.AccountType) ? "Commercial" : request.AccountType, Producer = string.IsNullOrWhiteSpace(request.Producer) ? "Unassigned" : request.Producer, request.OpportunityType, Score = Math.Clamp(request.Score, 0, 100), request.EstimatedPremium, request.TriggerSignal, LastContactDate = request.LastContactDate == default ? DateTime.UtcNow : request.LastContactDate, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Open" : request.StatusCode }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("cross-sell/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateCrossSell(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.CrossSellOpportunity (OpportunityId,TenantId,AccountName,AccountType,Producer,OpportunityType,Score,EstimatedPremium,TriggerSignal,LastContactDate,StatusCode,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CONCAT(AccountName, N' Copy'),AccountType,Producer,OpportunityType,Score,EstimatedPremium,TriggerSignal,LastContactDate,N'Open',SYSUTCDATETIME(),0
FROM Marketing.CrossSellOpportunity
WHERE OpportunityId=@Id AND IsDeleted=0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("cross-sell/{id:guid}/rescore")]
    public async Task<IActionResult> RescoreCrossSell(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.CrossSellOpportunity
SET Score = CASE WHEN Score >= 96 THEN 100 ELSE Score + 1 + ABS(CHECKSUM(NEWID())) % 9 END,
    EstimatedPremium = CASE WHEN EstimatedPremium <= 0 THEN 1500 ELSE EstimatedPremium + (ABS(CHECKSUM(NEWID())) % 900) END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("cross-sell/{id:guid}/status")]
    public async Task<IActionResult> UpdateCrossSellStatus(Guid id, [FromBody] StatusUpdateRequest request, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.CrossSellOpportunity SET StatusCode=@Status, ModifiedDateUtc=SYSUTCDATETIME() WHERE OpportunityId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Status }, cancellationToken: cancellationToken));
        return NoContent();
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

    [HttpPost("win-back/seed")]
    public async Task<IActionResult> EnsureWinBackSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("win-back")]
    public async Task<IActionResult> CreateWinBack([FromBody] MarketingWinBackDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.PolicyType))
        {
            return BadRequest("Tenant, account name, and policy type are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var lapseDate = request.LapseDate == default ? DateTime.UtcNow.Date.AddDays(-Math.Max(request.DaysLapsed, 30)) : request.LapseDate;
        var daysLapsed = request.DaysLapsed <= 0 ? Math.Max(0, (int)(DateTime.UtcNow.Date - lapseDate.Date).TotalDays) : request.DaysLapsed;
        const string sql = @"
INSERT INTO Marketing.WinBackAccount (WinBackId,TenantId,AccountName,PolicyType,LapseDate,DaysLapsed,LastPremium,LapseReason,StatusCode,LapseWindow,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@AccountName,@PolicyType,@LapseDate,@DaysLapsed,@LastPremium,@LapseReason,@StatusCode,@LapseWindow,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountName, request.PolicyType, LapseDate = lapseDate, DaysLapsed = daysLapsed, request.LastPremium, request.LapseReason, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "New" : request.StatusCode, LapseWindow = string.IsNullOrWhiteSpace(request.LapseWindow) ? CalculateLapseWindow(daysLapsed) : request.LapseWindow }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("win-back/{id:guid}")]
    public async Task<IActionResult> UpdateWinBack(Guid id, [FromBody] MarketingWinBackDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.PolicyType))
        {
            return BadRequest("Tenant, account name, and policy type are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var lapseDate = request.LapseDate == default ? DateTime.UtcNow.Date.AddDays(-Math.Max(request.DaysLapsed, 30)) : request.LapseDate;
        var daysLapsed = request.DaysLapsed <= 0 ? Math.Max(0, (int)(DateTime.UtcNow.Date - lapseDate.Date).TotalDays) : request.DaysLapsed;
        const string sql = @"
UPDATE Marketing.WinBackAccount
SET AccountName=@AccountName, PolicyType=@PolicyType, LapseDate=@LapseDate, DaysLapsed=@DaysLapsed,
    LastPremium=@LastPremium, LapseReason=@LapseReason, StatusCode=@StatusCode, LapseWindow=@LapseWindow,
    ModifiedDateUtc=SYSUTCDATETIME()
WHERE WinBackId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountName, request.PolicyType, LapseDate = lapseDate, DaysLapsed = daysLapsed, request.LastPremium, request.LapseReason, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "New" : request.StatusCode, LapseWindow = string.IsNullOrWhiteSpace(request.LapseWindow) ? CalculateLapseWindow(daysLapsed) : request.LapseWindow }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("win-back/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateWinBack(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.WinBackAccount (WinBackId,TenantId,AccountName,PolicyType,LapseDate,DaysLapsed,LastPremium,LapseReason,StatusCode,LapseWindow,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CONCAT(AccountName, N' Copy'),PolicyType,LapseDate,DaysLapsed,LastPremium,LapseReason,N'New',LapseWindow,SYSUTCDATETIME(),0
FROM Marketing.WinBackAccount
WHERE WinBackId=@Id AND IsDeleted=0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("win-back/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveWinBack(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.WinBackAccount SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE WinBackId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
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

    [HttpPost("referrals/seed")]
    public async Task<IActionResult> EnsureReferralSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("referrals")]
    public async Task<IActionResult> CreateReferral([FromBody] MarketingReferralDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProspectName) || string.IsNullOrWhiteSpace(request.ReferredBy))
        {
            return BadRequest("Tenant, prospect name, and referred by are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var receivedDate = request.ReceivedDate == default ? DateTime.UtcNow : request.ReceivedDate;
        const string sql = @"INSERT INTO Marketing.Referral (ReferralId,TenantId,ProspectName,ReferredBy,ReferralType,ReceivedDate,StatusCode,PolicyInterest,EstimatedPremium,Producer,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@ProspectName,@ReferredBy,@ReferralType,@ReceivedDate,@StatusCode,@PolicyInterest,@EstimatedPremium,@Producer,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ProspectName, request.ReferredBy, ReferralType = string.IsNullOrWhiteSpace(request.ReferralType) ? "Client" : request.ReferralType, ReceivedDate = receivedDate, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Pending" : request.StatusCode, request.PolicyInterest, request.EstimatedPremium, Producer = string.IsNullOrWhiteSpace(request.Producer) ? "Unassigned" : request.Producer }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("referrals/{id:guid}")]
    public async Task<IActionResult> UpdateReferral(Guid id, [FromBody] MarketingReferralDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProspectName) || string.IsNullOrWhiteSpace(request.ReferredBy))
        {
            return BadRequest("Tenant, prospect name, and referred by are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var receivedDate = request.ReceivedDate == default ? DateTime.UtcNow : request.ReceivedDate;
        const string sql = @"
UPDATE Marketing.Referral
SET ProspectName=@ProspectName, ReferredBy=@ReferredBy, ReferralType=@ReferralType, ReceivedDate=@ReceivedDate,
    StatusCode=@StatusCode, PolicyInterest=@PolicyInterest, EstimatedPremium=@EstimatedPremium, Producer=@Producer,
    ModifiedDateUtc=SYSUTCDATETIME()
WHERE ReferralId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ProspectName, request.ReferredBy, ReferralType = string.IsNullOrWhiteSpace(request.ReferralType) ? "Client" : request.ReferralType, ReceivedDate = receivedDate, StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Pending" : request.StatusCode, request.PolicyInterest, request.EstimatedPremium, Producer = string.IsNullOrWhiteSpace(request.Producer) ? "Unassigned" : request.Producer }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("referrals/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateReferral(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.Referral (ReferralId,TenantId,ProspectName,ReferredBy,ReferralType,ReceivedDate,StatusCode,PolicyInterest,EstimatedPremium,Producer,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CONCAT(ProspectName, N' Copy'),ReferredBy,ReferralType,SYSUTCDATETIME(),N'Pending',PolicyInterest,EstimatedPremium,Producer,SYSUTCDATETIME(),0
FROM Marketing.Referral
WHERE ReferralId=@Id AND IsDeleted=0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("referrals/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveReferral(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.Referral SET StatusCode=N'Archived', IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReferralId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
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

    [HttpPost("reviews/seed")]
    public async Task<IActionResult> EnsureReviewSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> CreateReview([FromBody] MarketingReviewDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ClientName) || string.IsNullOrWhiteSpace(request.Platform) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Tenant, client name, platform, and content are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Marketing.Review (ReviewId,TenantId,ClientName,Platform,Rating,ReviewDate,Content,Response,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@ClientName,@Platform,@Rating,@ReviewDate,@Content,@Response,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClientName, request.Platform, Rating = Math.Clamp(request.Rating, 1, 5), ReviewDate = request.ReviewDate == default ? DateTime.UtcNow : request.ReviewDate, request.Content, request.Response }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("reviews/{id:guid}")]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] MarketingReviewDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ClientName) || string.IsNullOrWhiteSpace(request.Platform) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Tenant, client name, platform, and content are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.Review
SET ClientName=@ClientName, Platform=@Platform, Rating=@Rating, ReviewDate=@ReviewDate, Content=@Content, Response=@Response,
    ModifiedDateUtc=SYSUTCDATETIME()
WHERE ReviewId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClientName, request.Platform, Rating = Math.Clamp(request.Rating, 1, 5), ReviewDate = request.ReviewDate == default ? DateTime.UtcNow : request.ReviewDate, request.Content, request.Response }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/reply")]
    public async Task<IActionResult> ReplyReview(Guid id, [FromBody] ReviewReplyRequest request, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.Review SET Response=@Response, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReviewId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Response }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveReview(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.Review SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReviewId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("reviews/requests")]
    public async Task<IActionResult> CreateReviewRequest([FromBody] MarketingReviewRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ClientName) || string.IsNullOrWhiteSpace(request.Channel) || string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest("Tenant, client name, channel, and platform are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Marketing.ReviewRequest (ReviewRequestId,TenantId,ClientName,Channel,SentDate,Platform,ReviewLeft,NpsScore,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@ClientName,@Channel,@SentDate,@Platform,@ReviewLeft,@NpsScore,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClientName, request.Channel, SentDate = request.SentDate == default ? DateTime.UtcNow : request.SentDate, request.Platform, request.ReviewLeft, NpsScore = Math.Clamp(request.NpsScore, 0, 10) }, cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("reviews/requests/{id:guid}")]
    public async Task<IActionResult> UpdateReviewRequest(Guid id, [FromBody] MarketingReviewRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ClientName) || string.IsNullOrWhiteSpace(request.Channel) || string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest("Tenant, client name, channel, and platform are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.ReviewRequest
SET ClientName=@ClientName, Channel=@Channel, SentDate=@SentDate, Platform=@Platform, ReviewLeft=@ReviewLeft, NpsScore=@NpsScore,
    ModifiedDateUtc=SYSUTCDATETIME()
WHERE ReviewRequestId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClientName, request.Channel, SentDate = request.SentDate == default ? DateTime.UtcNow : request.SentDate, request.Platform, request.ReviewLeft, NpsScore = Math.Clamp(request.NpsScore, 0, 10) }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("reviews/requests/{id:guid}/complete")]
    public async Task<IActionResult> CompleteReviewRequest(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.ReviewRequest SET ReviewLeft=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReviewRequestId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("reviews/requests/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateReviewRequest(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.ReviewRequest (ReviewRequestId,TenantId,ClientName,Channel,SentDate,Platform,ReviewLeft,NpsScore,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,ClientName,Channel,SYSUTCDATETIME(),Platform,0,NpsScore,SYSUTCDATETIME(),0
FROM Marketing.ReviewRequest
WHERE ReviewRequestId=@Id AND IsDeleted=0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("reviews/requests/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveReviewRequest(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.ReviewRequest SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE ReviewRequestId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> SearchAnalytics([FromQuery] Guid tenantId, [FromQuery] string? period, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        var periodCode = string.IsNullOrWhiteSpace(period) ? "90" : period;
        const string sql = @"
SELECT AnalyticsMetricId,TenantId,CampaignName,Channel,CampaignType,SegmentName,Goal,PeriodCode,PeriodStartDate,PeriodEndDate,
       Reached,OpenRate,ClickRate,Conversions,Revenue,Spend,UnsubscribeRate,Status,UpdatedDateUtc
FROM Marketing.AnalyticsMetric
WHERE TenantId=@TenantId AND IsDeleted=0
  AND (@PeriodCode IS NULL OR @PeriodCode='' OR PeriodCode=@PeriodCode)
  AND (@SearchTerm IS NULL OR @SearchTerm='' OR CampaignName LIKE '%' + @SearchTerm + '%' OR Channel LIKE '%' + @SearchTerm + '%' OR CampaignType LIKE '%' + @SearchTerm + '%' OR SegmentName LIKE '%' + @SearchTerm + '%' OR Goal LIKE '%' + @SearchTerm + '%')
ORDER BY Revenue DESC, Conversions DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<MarketingAnalyticsMetricDto>(new CommandDefinition(sql, new { TenantId = tenantId, PeriodCode = periodCode, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        var totalReached = Math.Max(items.Sum(x => x.Reached), 1);
        var result = new MarketingAnalyticsResult
        {
            Items = items,
            Channels = items.GroupBy(x => x.Channel).Select(g => new MarketingAnalyticsChannelSummaryDto { Name = g.Key, Reached = g.Sum(x => x.Reached), Revenue = g.Sum(x => x.Revenue), Percent = (int)Math.Round(g.Sum(x => x.Reached) * 100m / totalReached) }).OrderByDescending(x => x.Reached).ToList(),
            Opportunities = items.GroupBy(x => x.Goal).Select(g => new MarketingAnalyticsOpportunitySummaryDto { Name = g.Key, Conversions = g.Sum(x => x.Conversions), Revenue = g.Sum(x => x.Revenue) }).OrderByDescending(x => x.Revenue).ToList()
        };
        return Ok(result);
    }

    [HttpPost("analytics/seed")]
    public async Task<IActionResult> EnsureAnalyticsSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureMarketingPageDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("analytics")]
    public async Task<IActionResult> CreateAnalyticsMetric([FromBody] MarketingAnalyticsMetricDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.CampaignName) || string.IsNullOrWhiteSpace(request.Channel))
        {
            return BadRequest("Tenant, campaign name, and channel are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Marketing.AnalyticsMetric (AnalyticsMetricId,TenantId,CampaignName,Channel,CampaignType,SegmentName,Goal,PeriodCode,PeriodStartDate,PeriodEndDate,Reached,OpenRate,ClickRate,Conversions,Revenue,Spend,UnsubscribeRate,Status,UpdatedDateUtc,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@CampaignName,@Channel,@CampaignType,@SegmentName,@Goal,@PeriodCode,@PeriodStartDate,@PeriodEndDate,@Reached,@OpenRate,@ClickRate,@Conversions,@Revenue,@Spend,@UnsubscribeRate,@Status,SYSUTCDATETIME(),SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, NormalizeAnalyticsMetric(id, request), cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("analytics/{id:guid}")]
    public async Task<IActionResult> UpdateAnalyticsMetric(Guid id, [FromBody] MarketingAnalyticsMetricDto request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.CampaignName) || string.IsNullOrWhiteSpace(request.Channel))
        {
            return BadRequest("Tenant, campaign name, and channel are required.");
        }

        await EnsureMarketingPageDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Marketing.AnalyticsMetric
SET CampaignName=@CampaignName, Channel=@Channel, CampaignType=@CampaignType, SegmentName=@SegmentName, Goal=@Goal,
    PeriodCode=@PeriodCode, PeriodStartDate=@PeriodStartDate, PeriodEndDate=@PeriodEndDate, Reached=@Reached,
    OpenRate=@OpenRate, ClickRate=@ClickRate, Conversions=@Conversions, Revenue=@Revenue, Spend=@Spend,
    UnsubscribeRate=@UnsubscribeRate, Status=@Status, UpdatedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
WHERE AnalyticsMetricId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, NormalizeAnalyticsMetric(id, request), cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("analytics/{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateAnalyticsMetric(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Marketing.AnalyticsMetric (AnalyticsMetricId,TenantId,CampaignName,Channel,CampaignType,SegmentName,Goal,PeriodCode,PeriodStartDate,PeriodEndDate,Reached,OpenRate,ClickRate,Conversions,Revenue,Spend,UnsubscribeRate,Status,UpdatedDateUtc,CreatedDateUtc,IsDeleted)
SELECT @NewId,TenantId,CONCAT(CampaignName,N' Copy'),Channel,CampaignType,SegmentName,Goal,PeriodCode,PeriodStartDate,PeriodEndDate,Reached,OpenRate,ClickRate,Conversions,Revenue,Spend,UnsubscribeRate,N'Draft',SYSUTCDATETIME(),SYSUTCDATETIME(),0
FROM Marketing.AnalyticsMetric
WHERE AnalyticsMetricId=@Id AND IsDeleted=0;
SELECT @NewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return Ok(new { id = newId });
    }

    [HttpPost("analytics/{id:guid}/recalculate")]
    public async Task<IActionResult> RecalculateAnalyticsMetric(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Marketing.AnalyticsMetric
SET Reached = CASE WHEN Reached <= 0 THEN 1000 ELSE Reached + ABS(CHECKSUM(NEWID())) % 250 END,
    OpenRate = CASE WHEN OpenRate >= 48 THEN OpenRate ELSE OpenRate + CAST((ABS(CHECKSUM(NEWID())) % 30) AS DECIMAL(9,2)) / 10 END,
    ClickRate = CASE WHEN ClickRate >= 14 THEN ClickRate ELSE ClickRate + CAST((ABS(CHECKSUM(NEWID())) % 15) AS DECIMAL(9,2)) / 10 END,
    Conversions = CASE WHEN Conversions <= 0 THEN 10 ELSE Conversions + ABS(CHECKSUM(NEWID())) % 18 END,
    Revenue = CASE WHEN Revenue <= 0 THEN 5000 ELSE Revenue + ABS(CHECKSUM(NEWID())) % 12000 END,
    UpdatedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
WHERE AnalyticsMetricId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("analytics/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAnalyticsMetric(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.AnalyticsMetric SET Status=N'Archived', IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE AnalyticsMetricId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("landing-pages/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLandingPage(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Marketing.LandingPage SET Status = 'Archived', IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE LandingPageId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    public sealed class StatusUpdateRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public sealed class ReviewReplyRequest
    {
        public string Response { get; set; } = string.Empty;
    }

    private static object NormalizeAnalyticsMetric(Guid id, MarketingAnalyticsMetricDto request)
    {
        var periodCode = string.IsNullOrWhiteSpace(request.PeriodCode) ? "90" : request.PeriodCode;
        var periodEnd = request.PeriodEndDate == default ? DateTime.UtcNow : request.PeriodEndDate;
        var periodStart = request.PeriodStartDate == default ? periodEnd.AddDays(PeriodDays(periodCode) * -1) : request.PeriodStartDate;
        return new
        {
            Id = id,
            request.TenantId,
            CampaignName = request.CampaignName.Trim(),
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "Email" : request.Channel,
            CampaignType = string.IsNullOrWhiteSpace(request.CampaignType) ? "Email" : request.CampaignType,
            SegmentName = string.IsNullOrWhiteSpace(request.SegmentName) ? "All Marketing Contacts" : request.SegmentName,
            Goal = string.IsNullOrWhiteSpace(request.Goal) ? "Engagement" : request.Goal,
            PeriodCode = periodCode,
            PeriodStartDate = periodStart,
            PeriodEndDate = periodEnd,
            Reached = Math.Max(0, request.Reached),
            OpenRate = Math.Clamp(request.OpenRate, 0, 100),
            ClickRate = Math.Clamp(request.ClickRate, 0, 100),
            Conversions = Math.Max(0, request.Conversions),
            Revenue = Math.Max(0, request.Revenue),
            Spend = Math.Max(0, request.Spend),
            UnsubscribeRate = Math.Clamp(request.UnsubscribeRate, 0, 100),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status
        };
    }

    private static int PeriodDays(string periodCode) => periodCode switch
    {
        "30" => 30,
        "ytd" => Math.Max(1, DateTime.UtcNow.DayOfYear),
        "12m" => 365,
        _ => 90
    };

    private static string CalculateLapseWindow(int daysLapsed) => daysLapsed switch
    {
        < 60 => "30–60",
        < 90 => "60–90",
        < 180 => "90–180",
        _ => "180+"
    };
}
