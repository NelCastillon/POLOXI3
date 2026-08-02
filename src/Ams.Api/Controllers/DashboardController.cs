using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDashboardService _service;
    private readonly ISqlConnectionFactory _connectionFactory;

    public DashboardController(IDashboardService service, ISqlConnectionFactory connectionFactory)
    {
        _service = service;
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureDashboardDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Analytics') EXEC(N'CREATE SCHEMA Analytics');

IF OBJECT_ID(N'Analytics.DashboardRecord', N'U') IS NULL
BEGIN
    CREATE TABLE Analytics.DashboardRecord
    (
        DashboardRecordId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Kind NVARCHAR(100) NOT NULL,
        Code NVARCHAR(200) NOT NULL,
        Name NVARCHAR(250) NOT NULL,
        Status NVARCHAR(80) NOT NULL,
        JsonData NVARCHAR(MAX) NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_DashboardRecord_Tenant_Kind ON Analytics.DashboardRecord(TenantId, Kind, IsDeleted);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        var executive = new ExecutiveDashboardPageDto
        {
            Kpi = new ExecutiveDashboardKpiDto { WrittenPremium = 1842500, WrittenPremiumDelta = 12.4, RetentionRate = 88.7, RetentionDelta = 2.1, NewBusinessPremium = 426000, NewBusinessDelta = 9.3, RenewalAtRiskCount = 12, RenewalAtRiskPremium = 318000, OpenClaimsCount = 18, TotalIncurredLoss = 740000, OverdueReceivables = 96000, OverduePct = 8.4 },
            PremiumTrend = [new() { Label = "Jan", Value = 210000, PriorValue = 188000 }, new() { Label = "Feb", Value = 235000, PriorValue = 198000 }, new() { Label = "Mar", Value = 298000, PriorValue = 255000 }, new() { Label = "Apr", Value = 318000, PriorValue = 276000 }, new() { Label = "May", Value = 372000, PriorValue = 326000 }, new() { Label = "Jun", Value = 409000, PriorValue = 355000 }],
            NewBusinessTrend = [new() { Label = "Jan", NewBiz = 54000, Renewal = 156000 }, new() { Label = "Feb", NewBiz = 61000, Renewal = 174000 }, new() { Label = "Mar", NewBiz = 72000, Renewal = 226000 }, new() { Label = "Apr", NewBiz = 79000, Renewal = 239000 }, new() { Label = "May", NewBiz = 82000, Renewal = 290000 }, new() { Label = "Jun", NewBiz = 78000, Renewal = 331000 }],
            RetentionSegments = [new() { Label = "Renewed", Value = 88.7 }, new() { Label = "At Risk", Value = 7.4 }, new() { Label = "Lost", Value = 3.9 }],
            RenewalsAtRisk = [new() { AccountName = "Riverside Construction LLC", LobCode = "BOP", ExpiryDate = now.AddDays(18), Premium = 84200, RiskLevel = "High" }, new() { AccountName = "Chen Family", LobCode = "Home", ExpiryDate = now.AddDays(24), Premium = 3200, RiskLevel = "Medium" }, new() { AccountName = "Sato Tech LLC", LobCode = "Cyber", ExpiryDate = now.AddDays(36), Premium = 21400, RiskLevel = "High" }],
            ClaimsBySeverity = [new() { Label = "High", Value = 4 }, new() { Label = "Medium", Value = 8 }, new() { Label = "Low", Value = 6 }],
            ReceivablesAging = [new() { Label = "Current", Value = 380000 }, new() { Label = "1-30", Value = 72000 }, new() { Label = "31-60", Value = 18000 }, new() { Label = "61-90", Value = 6000 }, new() { Label = "90+", Value = 1200 }],
            Campaigns = [new() { Name = "Home+Auto Bundle", Leads = 420, Quoted = 184, Bound = 72, ConversionPct = 17.1, Premium = 206000 }, new() { Name = "Umbrella Cross-Sell", Leads = 188, Quoted = 96, Bound = 44, ConversionPct = 23.4, Premium = 94000 }, new() { Name = "Win-Back", Leads = 231, Quoted = 88, Bound = 29, ConversionPct = 12.6, Premium = 115500 }],
            Producers = [new() { ProducerName = "Beth Nguyen", Branch = "Downtown", WrittenPremium = 412000, PoliciesWritten = 38, NewBusiness = 126000, RetentionRate = 91.2, GoalPct = 118, Lob = "Commercial" }, new() { ProducerName = "Jake Park", Branch = "North", WrittenPremium = 356000, PoliciesWritten = 44, NewBusiness = 98000, RetentionRate = 87.5, GoalPct = 101, Lob = "Personal" }, new() { ProducerName = "Sara Kim", Branch = "West", WrittenPremium = 322000, PoliciesWritten = 31, NewBusiness = 112000, RetentionRate = 84.3, GoalPct = 94, Lob = "Benefits" }]
        };

        var dashboards = new[]
        {
            new DashboardDefinitionDto { Name = "Executive Snapshot", Description = "Top-level KPIs for leadership: premium, retention, loss ratio, pipeline.", Icon = "bi-speedometer2", IconCss = "db-di-blue", Audience = "Executive", WidgetCount = 8, IsDefault = true, LastEdited = now.AddDays(-2), Widgets = ["Revenue KPI", "Retention Rate", "Loss Ratio", "Pipeline", "New Business", "Claims Open", "Producer Top 5", "Renewal 30d"] },
            new DashboardDefinitionDto { Name = "Producer Workbench", Description = "Per-producer pipeline, tasks, expiring policies, and activity feed.", Icon = "bi-person-badge", IconCss = "db-di-purple", Audience = "Producer", WidgetCount = 6, IsDefault = false, LastEdited = now.AddDays(-5), Widgets = ["My Pipeline", "Expiring 30d", "Open Tasks", "Recent Activity", "Commission MTD", "Goal Progress"] },
            new DashboardDefinitionDto { Name = "Financial Overview", Description = "Revenue, AR aging, commission payable, and collections summary.", Icon = "bi-bank", IconCss = "db-di-green", Audience = "Accounting", WidgetCount = 7, IsDefault = false, LastEdited = now.AddDays(-3), Widgets = ["Revenue MTD", "AR Aging", "Commission Payable", "Collections", "Invoices Due"] }
        };

        var kpis = new[]
        {
            new DashboardKpiDefinitionDto { Name = "Retention Rate", Domain = "Retention", Description = "Percentage of expiring policies renewed in the period.", Formula = "Renewed ÷ Expiring", Target = "≥ 88%", Warning = "≥ 80%", Critical = "< 80%", Direction = "Higher is better", Frequency = "Monthly", Owner = "Operations VP", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "New Business Premium", Domain = "Sales", Description = "Total written premium from new accounts in the period.", Formula = "SUM(NewPolicies.Premium)", Target = "$500K", Warning = "$400K", Critical = "$300K", Direction = "Higher is better", Frequency = "Monthly", Owner = "Sales Director", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "Loss Ratio", Domain = "Claims", Description = "Incurred losses as a percentage of earned premium.", Formula = "Losses ÷ Earned Premium", Target = "≤ 65%", Warning = "≤ 75%", Critical = "> 75%", Direction = "Lower is better", Frequency = "Monthly", Owner = "Principals", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "AR Days Outstanding", Domain = "Finance", Description = "Average days invoices remain unpaid.", Formula = "AR Balance ÷ Daily Revenue", Target = "≤ 25d", Warning = "≤ 40d", Critical = "> 40d", Direction = "Lower is better", Frequency = "Monthly", Owner = "CFO", IsActive = false }
        };

        var salesAnalytics = new[]
        {
            new SalesAnalyticsRecordDto
            {
                Category = "Pipeline",
                Priority = "Critical",
                Title = "Enterprise commercial pipeline",
                Owner = "Sales Director",
                Segment = "Commercial",
                Stage = "Proposal",
                Status = "At Risk",
                Amount = 684000,
                Count = 18,
                ConversionPct = 31.4,
                TrendPct = 12.8,
                DueDateUtc = now.AddDays(10),
                NextAction = "Review proposal blockers and assign producer follow-up for top opportunities.",
                Insight = "Largest weighted pipeline segment; two accounts need carrier appetite confirmation before bind."
            },
            new SalesAnalyticsRecordDto
            {
                Category = "Pipeline",
                Priority = "High",
                Title = "Personal lines bundle opportunities",
                Owner = "Personal Lines Manager",
                Segment = "Personal",
                Stage = "Quoted",
                Status = "On Track",
                Amount = 214500,
                Count = 62,
                ConversionPct = 24.6,
                TrendPct = 8.1,
                DueDateUtc = now.AddDays(16),
                NextAction = "Launch bundle follow-up sequence for quoted home and auto accounts.",
                Insight = "Bundle conversion is outperforming standalone personal lines by 6.3 points."
            },
            new SalesAnalyticsRecordDto
            {
                Category = "Risk",
                Priority = "Critical",
                Title = "Stalled high-value opportunities",
                Owner = "Revenue Operations",
                Segment = "All",
                Stage = "Negotiation",
                Status = "Escalate",
                Amount = 391000,
                Count = 9,
                ConversionPct = 18.2,
                TrendPct = -4.7,
                DueDateUtc = now.AddDays(5),
                NextAction = "Escalate to producer leads and schedule executive sponsor outreach.",
                Insight = "Nine deals have no activity in seven days while expected close dates are inside 30 days."
            },
            new SalesAnalyticsRecordDto
            {
                Category = "Play",
                Priority = "High",
                Title = "Cross-sell account expansion",
                Owner = "Marketing + Sales",
                Segment = "Existing Accounts",
                Stage = "Targeting",
                Status = "Ready",
                Amount = 286000,
                Count = 44,
                ConversionPct = 19.8,
                TrendPct = 5.6,
                DueDateUtc = now.AddDays(21),
                NextAction = "Export target list and send warm producer introduction campaign.",
                Insight = "Commercial accounts with monoline coverage show strong umbrella and cyber fit."
            },
            new SalesAnalyticsRecordDto
            {
                Category = "Producer",
                Priority = "Normal",
                Title = "Goal attainment coaching queue",
                Owner = "Branch Managers",
                Segment = "Producer Team",
                Stage = "Coaching",
                Status = "Monitor",
                Amount = 157000,
                Count = 6,
                ConversionPct = 72.0,
                TrendPct = -2.2,
                DueDateUtc = now.AddDays(14),
                NextAction = "Review goal gaps and assign activity targets for producers under 95% goal.",
                Insight = "Most under-goal producers have healthy quote volume but lower bind rate."
            }
        };

        var claimsAnalytics = new[]
        {
            new ClaimsAnalyticsRecordDto
            {
                Category = "Severity",
                Priority = "Critical",
                Title = "Large-loss reserve adequacy review",
                Owner = "Claims Director",
                Segment = "Commercial",
                LossType = "Property",
                Status = "Escalate",
                OpenClaims = 14,
                TotalIncurred = 1240000,
                TotalReserves = 820000,
                TotalPaid = 420000,
                LossRatioPct = 78.6,
                AverageDaysOpen = 64,
                LitigationCount = 3,
                CatastropheCount = 2,
                TrendPct = 9.4,
                DueDateUtc = now.AddDays(4),
                NextAction = "Validate reserves on all claims over $75K and schedule carrier strategy calls for disputed files.",
                Insight = "High-severity property losses are driving reserve volatility and need executive claim review this week."
            },
            new ClaimsAnalyticsRecordDto
            {
                Category = "Litigation",
                Priority = "Critical",
                Title = "Litigated and disputed claim strategy queue",
                Owner = "Coverage Counsel",
                Segment = "All Lines",
                LossType = "Liability",
                Status = "At Risk",
                OpenClaims = 9,
                TotalIncurred = 735000,
                TotalReserves = 510000,
                TotalPaid = 225000,
                LossRatioPct = 71.2,
                AverageDaysOpen = 118,
                LitigationCount = 9,
                CatastropheCount = 0,
                TrendPct = 6.8,
                DueDateUtc = now.AddDays(6),
                NextAction = "Review counsel notes, confirm coverage position, and assign next settlement milestone.",
                Insight = "Litigated liability files are aging fastest and represent the highest controllable severity exposure."
            },
            new ClaimsAnalyticsRecordDto
            {
                Category = "Catastrophe",
                Priority = "High",
                Title = "CAT response and FNOL outreach",
                Owner = "CAT Response Lead",
                Segment = "Property",
                LossType = "Wind/Hail",
                Status = "Mobilize",
                OpenClaims = 31,
                TotalIncurred = 614000,
                TotalReserves = 460000,
                TotalPaid = 154000,
                LossRatioPct = 66.9,
                AverageDaysOpen = 19,
                LitigationCount = 0,
                CatastropheCount = 31,
                TrendPct = 14.1,
                DueDateUtc = now.AddDays(2),
                NextAction = "Geo-tag exposed insureds, send CAT blast, and fast-file FNOL for unreported losses.",
                Insight = "New storm activity has the highest client-impact urgency and needs proactive outreach before claim volume spikes."
            },
            new ClaimsAnalyticsRecordDto
            {
                Category = "Follow Up",
                Priority = "High",
                Title = "Overdue adjuster follow-up queue",
                Owner = "Claims Advocacy Team",
                Segment = "Personal + Commercial",
                LossType = "Auto/Property",
                Status = "Delayed",
                OpenClaims = 46,
                TotalIncurred = 392000,
                TotalReserves = 248000,
                TotalPaid = 144000,
                LossRatioPct = 58.4,
                AverageDaysOpen = 42,
                LitigationCount = 1,
                CatastropheCount = 6,
                TrendPct = -2.6,
                DueDateUtc = now.AddDays(7),
                NextAction = "Call carrier adjusters on claims with no activity inside seven days and log client updates.",
                Insight = "Follow-up discipline is improving, but delayed adjuster responses remain the largest service friction point."
            },
            new ClaimsAnalyticsRecordDto
            {
                Category = "Subrogation",
                Priority = "Normal",
                Title = "Subrogation recovery opportunity review",
                Owner = "Recovery Desk",
                Segment = "Auto",
                LossType = "Collision",
                Status = "Monitor",
                OpenClaims = 18,
                TotalIncurred = 218000,
                TotalReserves = 96000,
                TotalPaid = 122000,
                LossRatioPct = 49.5,
                AverageDaysOpen = 37,
                LitigationCount = 0,
                CatastropheCount = 0,
                TrendPct = 3.2,
                DueDateUtc = now.AddDays(14),
                NextAction = "Validate third-party liability evidence and push recovery packages to carriers.",
                Insight = "Recoverable auto losses can offset paid claim leakage when documentation is completed promptly."
            }
        };

        var retentionAnalytics = new[]
        {
            new RetentionAnalyticsRecordDto
            {
                Category = "At Risk",
                Priority = "Critical",
                Title = "Executive renewal save queue",
                Owner = "Retention Desk",
                Segment = "Commercial",
                Driver = "Premium shock + incomplete remarket",
                Status = "Escalate",
                Premium = 638000,
                Count = 17,
                RetentionPct = 62.5,
                RiskScore = 91,
                TrendPct = -7.8,
                DueDateUtc = now.AddDays(6),
                NextAction = "Escalate accounts with expiring premium over $25K and assign producer save calls.",
                Insight = "High-premium commercial renewals have the largest book-at-risk exposure and need same-week intervention."
            },
            new RetentionAnalyticsRecordDto
            {
                Category = "Save Play",
                Priority = "High",
                Title = "Personal lines bundle retention campaign",
                Owner = "Personal Lines Manager",
                Segment = "Personal",
                Driver = "Monoline churn risk",
                Status = "Ready",
                Premium = 214000,
                Count = 54,
                RetentionPct = 78.4,
                RiskScore = 68,
                TrendPct = 5.2,
                DueDateUtc = now.AddDays(14),
                NextAction = "Launch home-auto-umbrella bundle offer for monoline clients before renewal quote delivery.",
                Insight = "Bundled households are retaining 9 points higher than monoline accounts and should be prioritized."
            },
            new RetentionAnalyticsRecordDto
            {
                Category = "Churn Driver",
                Priority = "Critical",
                Title = "Non-pay and billing friction churn drivers",
                Owner = "Billing + Service",
                Segment = "All Lines",
                Driver = "Payment friction",
                Status = "At Risk",
                Premium = 186500,
                Count = 29,
                RetentionPct = 55.1,
                RiskScore = 87,
                TrendPct = -4.9,
                DueDateUtc = now.AddDays(5),
                NextAction = "Coordinate payment plan outreach and resolve aged billing exceptions before cancellation notices mature.",
                Insight = "Billing friction is the fastest-growing retention driver and overlaps with cancellation notice exposure."
            },
            new RetentionAnalyticsRecordDto
            {
                Category = "Producer",
                Priority = "High",
                Title = "Producer renewal follow-up accountability",
                Owner = "Branch Managers",
                Segment = "Producer Team",
                Driver = "Late producer touch",
                Status = "Monitor",
                Premium = 342000,
                Count = 22,
                RetentionPct = 81.7,
                RiskScore = 64,
                TrendPct = 2.4,
                DueDateUtc = now.AddDays(11),
                NextAction = "Review producers with no renewal touch inside 45 days and assign weekly save targets.",
                Insight = "Producer touch cadence is improving, but several branches still lag on high-value expirations."
            },
            new RetentionAnalyticsRecordDto
            {
                Category = "Client Health",
                Priority = "Normal",
                Title = "Client health sentiment and service recovery",
                Owner = "Service Operations",
                Segment = "Strategic Accounts",
                Driver = "Service sentiment",
                Status = "On Track",
                Premium = 258000,
                Count = 16,
                RetentionPct = 86.9,
                RiskScore = 48,
                TrendPct = 3.8,
                DueDateUtc = now.AddDays(20),
                NextAction = "Document recovery plans for negative sentiment accounts and schedule renewal stewardship reviews.",
                Insight = "Strategic accounts show healthy retention where service recovery plans are documented before renewal."
            }
        };

        var financeAnalytics = new[]
        {
            new FinanceAnalyticsRecordDto
            {
                Category = "Cash",
                Priority = "Critical",
                Title = "Cash acceleration and AR exposure command",
                Owner = "Controller",
                Segment = "Agency Wide",
                Workflow = "Collections",
                Status = "Escalate",
                Amount = 624000,
                Count = 41,
                HealthPct = 68.4,
                TrendPct = -5.8,
                DueDateUtc = now.AddDays(3),
                NextAction = "Prioritize 60+ day receivables, assign collector ownership, and reconcile unapplied cash before month-end close.",
                Insight = "Aged receivables and unapplied receipts represent the largest near-term cash and retention risk."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Close",
                Priority = "Critical",
                Title = "Month-end close readiness exceptions",
                Owner = "Finance Operations",
                Segment = "Corporate",
                Workflow = "Close",
                Status = "At Risk",
                Amount = 318500,
                Count = 18,
                HealthPct = 72.0,
                TrendPct = -3.4,
                DueDateUtc = now.AddDays(5),
                NextAction = "Clear unposted journals, review suspense accounts, and confirm period lock dependencies.",
                Insight = "Open journal and reconciliation exceptions can delay executive reporting if not resolved before cutoff."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Revenue",
                Priority = "High",
                Title = "Deferred revenue recognition review",
                Owner = "Revenue Accounting",
                Segment = "Subscriptions + Services",
                Workflow = "Revenue Recognition",
                Status = "Review",
                Amount = 482000,
                Count = 26,
                HealthPct = 81.5,
                TrendPct = 4.2,
                DueDateUtc = now.AddDays(8),
                NextAction = "Validate recognition schedules, remaining balances, and GL mapping for active deferred revenue items.",
                Insight = "Recognition accuracy is healthy but high remaining balances require schedule validation before reporting."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Payables",
                Priority = "High",
                Title = "AP payment timing and vendor exposure",
                Owner = "Accounts Payable",
                Segment = "Vendors",
                Workflow = "Payables",
                Status = "Monitor",
                Amount = 276000,
                Count = 33,
                HealthPct = 76.8,
                TrendPct = 2.7,
                DueDateUtc = now.AddDays(10),
                NextAction = "Sequence vendor payments by due date, discount window, and service-critical dependency.",
                Insight = "Payment timing can preserve cash while avoiding service disruption for operational vendors."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Controls",
                Priority = "Normal",
                Title = "Bank reconciliation and GL control quality",
                Owner = "Accounting Manager",
                Segment = "Treasury",
                Workflow = "Reconciliation",
                Status = "On Track",
                Amount = 94000,
                Count = 9,
                HealthPct = 88.2,
                TrendPct = 6.1,
                DueDateUtc = now.AddDays(14),
                NextAction = "Resolve outstanding deposits/checks and document reconciliation approvals for audit readiness.",
                Insight = "Control quality is improving, with remaining risk concentrated in older reconciliation discrepancies."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Cash",
                Priority = "High",
                Title = "Unapplied cash and receipt clearing desk",
                Owner = "Cash Applications",
                Segment = "Treasury",
                Workflow = "Collections",
                Status = "Review",
                Amount = 142500,
                Count = 27,
                HealthPct = 74.6,
                TrendPct = -2.9,
                DueDateUtc = now.AddDays(4),
                NextAction = "Match unapplied receipts to invoices, resolve short-pay exceptions, and document cash application blockers.",
                Insight = "Cash is present but not fully applied, suppressing accurate AR aging and collections prioritization."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Reports",
                Priority = "High",
                Title = "Executive finance reporting delivery readiness",
                Owner = "FP&A Lead",
                Segment = "Executive Pack",
                Workflow = "Reporting",
                Status = "Ready",
                Amount = 835000,
                Count = 12,
                HealthPct = 84.3,
                TrendPct = 7.5,
                DueDateUtc = now.AddDays(2),
                NextAction = "Run AR, AP, revenue, close, and cash reports; export the finance pack; confirm recurring delivery schedules.",
                Insight = "Leadership-ready reporting depends on current finance data, completed schedules, and validated report output formats."
            },
            new FinanceAnalyticsRecordDto
            {
                Category = "Controls",
                Priority = "Critical",
                Title = "Suspense and exception account remediation",
                Owner = "Accounting Manager",
                Segment = "GL Controls",
                Workflow = "Reconciliation",
                Status = "Escalate",
                Amount = 218750,
                Count = 14,
                HealthPct = 61.9,
                TrendPct = -6.4,
                DueDateUtc = now.AddDays(3),
                NextAction = "Review suspense balances, assign account owners, and clear unsupported entries before close certification.",
                Insight = "Suspense balances create close and audit risk and should be cleared before executive financial statements are finalized."
            }
        };

        var producerAnalytics = new[]
        {
            new ProducerAnalyticsRecordDto
            {
                Category = "Goal",
                Priority = "Critical",
                Title = "Producer goal attainment command",
                Owner = "Sales Director",
                Segment = "Producer Team",
                Workflow = "Goal Coaching",
                Status = "Escalate",
                Premium = 812000,
                Pipeline = 1280000,
                Count = 7,
                ConversionPct = 24.5,
                RetentionPct = 83.2,
                GoalPct = 86.4,
                TrendPct = -4.8,
                DueDateUtc = now.AddDays(5),
                NextAction = "Review producers under 90% goal, assign weekly premium targets, and inspect activity quality.",
                Insight = "Goal gap is concentrated in producers with healthy pipeline but lower quote-to-bind conversion."
            },
            new ProducerAnalyticsRecordDto
            {
                Category = "Pipeline",
                Priority = "Critical",
                Title = "High-value producer pipeline acceleration",
                Owner = "Revenue Operations",
                Segment = "Commercial",
                Workflow = "Pipeline Review",
                Status = "At Risk",
                Premium = 948000,
                Pipeline = 1735000,
                Count = 18,
                ConversionPct = 31.6,
                RetentionPct = 88.1,
                GoalPct = 104.0,
                TrendPct = 7.3,
                DueDateUtc = now.AddDays(7),
                NextAction = "Prioritize opportunities closing inside 30 days and assign producer follow-up on stalled submissions.",
                Insight = "Commercial producers hold the largest weighted pipeline and need close-plan discipline to protect forecast."
            },
            new ProducerAnalyticsRecordDto
            {
                Category = "Quotes",
                Priority = "High",
                Title = "Quote conversion coaching queue",
                Owner = "Branch Managers",
                Segment = "Personal + Small Commercial",
                Workflow = "Quote Follow-up",
                Status = "Review",
                Premium = 354000,
                Pipeline = 612000,
                Count = 42,
                ConversionPct = 18.4,
                RetentionPct = 86.0,
                GoalPct = 92.5,
                TrendPct = -2.1,
                DueDateUtc = now.AddDays(9),
                NextAction = "Audit quote follow-up timeliness and coach producers below 20% conversion.",
                Insight = "Quote volume is healthy, but inconsistent follow-up is suppressing bind rates in two branches."
            },
            new ProducerAnalyticsRecordDto
            {
                Category = "Retention",
                Priority = "High",
                Title = "Producer renewal retention accountability",
                Owner = "Retention Desk",
                Segment = "Renewals",
                Workflow = "Renewal Save",
                Status = "Monitor",
                Premium = 516000,
                Pipeline = 702000,
                Count = 24,
                ConversionPct = 36.8,
                RetentionPct = 78.9,
                GoalPct = 97.0,
                TrendPct = -3.6,
                DueDateUtc = now.AddDays(11),
                NextAction = "Assign producers to renewal saves over $10K and require documented client contact before quote delivery.",
                Insight = "Renewal retention gaps overlap with late producer touches and payment friction accounts."
            },
            new ProducerAnalyticsRecordDto
            {
                Category = "Commissions",
                Priority = "Normal",
                Title = "Commission payout and split exception review",
                Owner = "Commission Manager",
                Segment = "Compensation",
                Workflow = "Commission Review",
                Status = "On Track",
                Premium = 224000,
                Pipeline = 310000,
                Count = 13,
                ConversionPct = 41.2,
                RetentionPct = 90.4,
                GoalPct = 108.5,
                TrendPct = 4.4,
                DueDateUtc = now.AddDays(16),
                NextAction = "Validate split rules, pending statements, and payout exceptions before commission close.",
                Insight = "Commission readiness is healthy, with remaining risk in split-rule exceptions and pending statements."
            }
        };

        var marketingAnalytics = new[]
        {
            new MarketingAnalyticsRecordDto
            {
                Category = "ROI",
                Priority = "Critical",
                Title = "Campaign ROI and attribution command",
                Owner = "Marketing Director",
                Segment = "All Campaigns",
                Channel = "Multi-channel",
                Workflow = "ROI Review",
                Status = "Escalate",
                Revenue = 684000,
                Spend = 122000,
                Count = 18,
                ConversionPct = 14.8,
                EngagementPct = 31.6,
                TrendPct = 9.7,
                DueDateUtc = now.AddDays(4),
                NextAction = "Shift budget into high-ROAS channels, pause underperforming creative, and validate attribution for bound policies.",
                Insight = "Revenue is concentrated in three campaigns; paid spend needs same-week reallocation to protect acquisition cost."
            },
            new MarketingAnalyticsRecordDto
            {
                Category = "Leads",
                Priority = "Critical",
                Title = "Lead source conversion acceleration",
                Owner = "Demand Generation",
                Segment = "New Business",
                Channel = "Web + Paid",
                Workflow = "Lead Conversion",
                Status = "At Risk",
                Revenue = 412000,
                Spend = 84000,
                Count = 236,
                ConversionPct = 11.2,
                EngagementPct = 24.9,
                TrendPct = -3.4,
                DueDateUtc = now.AddDays(6),
                NextAction = "Audit top landing pages, route hot leads to producers, and launch nurture for uncontacted submissions.",
                Insight = "Lead volume is healthy, but conversion drop-off after form completion indicates follow-up and landing page friction."
            },
            new MarketingAnalyticsRecordDto
            {
                Category = "Cross-sell",
                Priority = "High",
                Title = "Cross-sell account expansion plays",
                Owner = "Account Marketing",
                Segment = "Existing Accounts",
                Channel = "Email + Producer",
                Workflow = "Cross-sell Play",
                Status = "Ready",
                Revenue = 356000,
                Spend = 36000,
                Count = 74,
                ConversionPct = 18.6,
                EngagementPct = 38.4,
                TrendPct = 6.2,
                DueDateUtc = now.AddDays(10),
                NextAction = "Rescore cross-sell candidates, assign producer follow-up, and send monoline bundle sequences.",
                Insight = "Monoline personal and commercial accounts show the highest premium lift with low campaign spend."
            },
            new MarketingAnalyticsRecordDto
            {
                Category = "Retention",
                Priority = "High",
                Title = "Win-back and renewal nurture queue",
                Owner = "Retention Marketing",
                Segment = "Lapsed + At Risk",
                Channel = "SMS + Email",
                Workflow = "Win-back",
                Status = "Monitor",
                Revenue = 224000,
                Spend = 28000,
                Count = 58,
                ConversionPct = 9.7,
                EngagementPct = 29.1,
                TrendPct = 2.5,
                DueDateUtc = now.AddDays(12),
                NextAction = "Prioritize recently lapsed policies, trigger save offers, and coordinate producer callback tasks.",
                Insight = "Recent lapses respond best inside 45 days; older win-back records need different offers and expectations."
            },
            new MarketingAnalyticsRecordDto
            {
                Category = "Reputation",
                Priority = "Normal",
                Title = "Review generation and referral reputation loop",
                Owner = "Client Experience",
                Segment = "Promoters",
                Channel = "Referral + Review",
                Workflow = "Reputation",
                Status = "On Track",
                Revenue = 148000,
                Spend = 14500,
                Count = 43,
                ConversionPct = 22.4,
                EngagementPct = 44.2,
                TrendPct = 4.1,
                DueDateUtc = now.AddDays(18),
                NextAction = "Send review requests to high-NPS clients, respond to new reviews, and convert warm referrals.",
                Insight = "Promoter-driven referrals remain the lowest-cost acquisition path and should be operationalized weekly."
            }
        };

        var policyAnalytics = new[]
        {
            new PolicyAnalyticsRecordDto
            {
                Category = "Renewal",
                Priority = "Critical",
                Title = "High-premium renewals requiring retention action",
                Owner = "Policy Operations",
                Segment = "Commercial",
                Workflow = "Renewal Review",
                Status = "At Risk",
                Premium = 742000,
                Count = 24,
                CompletionPct = 58.5,
                TrendPct = -6.4,
                DueDateUtc = now.AddDays(12),
                NextAction = "Assign producer and service owner outreach for renewals expiring inside 30 days.",
                Insight = "Commercial renewal exposure is concentrated in construction, cyber, and habitational accounts with incomplete market responses."
            },
            new PolicyAnalyticsRecordDto
            {
                Category = "Bind",
                Priority = "High",
                Title = "Bind backlog awaiting policy issuance",
                Owner = "Processing Team",
                Segment = "All Lines",
                Workflow = "Bind to Issue",
                Status = "Delayed",
                Premium = 516000,
                Count = 31,
                CompletionPct = 63.0,
                TrendPct = 4.8,
                DueDateUtc = now.AddDays(7),
                NextAction = "Prioritize carrier document follow-up and finalize missing billing setup.",
                Insight = "Backlog is improving but several bound accounts still lack issued policy documents and invoice confirmation."
            },
            new PolicyAnalyticsRecordDto
            {
                Category = "Cancellation",
                Priority = "Critical",
                Title = "Cancellation and non-pay exposure queue",
                Owner = "Retention Desk",
                Segment = "Personal + Commercial",
                Workflow = "Cancel Save",
                Status = "Escalate",
                Premium = 284500,
                Count = 19,
                CompletionPct = 41.2,
                TrendPct = -9.1,
                DueDateUtc = now.AddDays(4),
                NextAction = "Open save workflow, contact insureds, and coordinate billing resolution before cancellation effective date.",
                Insight = "Non-pay cancellation notices are rising and need same-week save actions to protect retention."
            },
            new PolicyAnalyticsRecordDto
            {
                Category = "Endorsement",
                Priority = "High",
                Title = "Endorsement activity impacting premium accuracy",
                Owner = "Service Operations",
                Segment = "Commercial",
                Workflow = "Policy Change",
                Status = "In Progress",
                Premium = 128000,
                Count = 42,
                CompletionPct = 71.8,
                TrendPct = 3.3,
                DueDateUtc = now.AddDays(15),
                NextAction = "Review open endorsements older than seven days and reconcile premium-bearing changes.",
                Insight = "Premium-bearing endorsements require tighter completion controls to keep book and billing totals aligned."
            },
            new PolicyAnalyticsRecordDto
            {
                Category = "Compliance",
                Priority = "Normal",
                Title = "Policy document compliance and acknowledgement follow-up",
                Owner = "Compliance Manager",
                Segment = "All Lines",
                Workflow = "Document Compliance",
                Status = "Monitor",
                Premium = 96000,
                Count = 28,
                CompletionPct = 82.4,
                TrendPct = 2.7,
                DueDateUtc = now.AddDays(21),
                NextAction = "Verify issued policy packets, certificate settings, and pending acknowledgements.",
                Insight = "Compliance readiness is healthy, but certificate and acknowledgement exceptions should be cleared before month end."
            }
        };

        var records = new List<DashboardSeedRecord>
        {
            Record(tenantId, "ExecutiveDashboard", "executive", "Executive Dashboard", "Active", executive)
        };
        records.AddRange(dashboards.Select(x => Record(tenantId, "CustomDashboard", Slug(x.Name), x.Name, x.IsDefault ? "Default" : "Active", x)));
        records.AddRange(kpis.Select(x => Record(tenantId, "KpiDefinition", Slug(x.Name), x.Name, x.IsActive ? "Active" : "Inactive", x)));
        records.AddRange(salesAnalytics.Select(x => Record(tenantId, "SalesAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(policyAnalytics.Select(x => Record(tenantId, "PolicyAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(retentionAnalytics.Select(x => Record(tenantId, "RetentionAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(claimsAnalytics.Select(x => Record(tenantId, "ClaimsAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(financeAnalytics.Select(x => Record(tenantId, "FinanceAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(producerAnalytics.Select(x => Record(tenantId, "ProducerAnalytics", Slug(x.Title), x.Title, x.Status, x)));
        records.AddRange(marketingAnalytics.Select(x => Record(tenantId, "MarketingAnalytics", Slug(x.Title), x.Title, x.Status, x)));

        const string seedSql = """
INSERT INTO Analytics.DashboardRecord (DashboardRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted)
SELECT NEWID(),source.TenantId,source.Kind,source.Code,source.Name,source.Status,source.JsonData,SYSUTCDATETIME(),0
FROM OPENJSON(@RecordsJson)
WITH
(
    TenantId UNIQUEIDENTIFIER '$.tenantId',
    Kind NVARCHAR(100) '$.kind',
    Code NVARCHAR(200) '$.code',
    Name NVARCHAR(250) '$.name',
    Status NVARCHAR(80) '$.status',
    JsonData NVARCHAR(MAX) '$.jsonData'
) source
WHERE NOT EXISTS
(
    SELECT 1 FROM Analytics.DashboardRecord existing
    WHERE existing.TenantId=source.TenantId AND existing.Kind=source.Kind AND existing.IsDeleted=0
);
""";
        var recordsJson = JsonSerializer.Serialize(records, JsonOptions);
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { RecordsJson = recordsJson }, cancellationToken: cancellationToken));
    }

    private static DashboardSeedRecord Record<T>(Guid tenantId, string kind, string code, string name, string status, T data)
        => new(tenantId, kind, code, name, status, JsonSerializer.Serialize(data, JsonOptions));
    private static string Slug(string value) => value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-");

    private sealed record DashboardSeedRecord(Guid TenantId, string Kind, string Code, string Name, string Status, string JsonData);

    [HttpGet]
    public async Task<IActionResult> GetKpi([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        return Ok(await _service.GetKpiAsync(tenantId, cancellationToken));
    }

    [HttpGet("executive")]
    public async Task<IActionResult> GetExecutive([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        var item = await ReadSingleAsync<ExecutiveDashboardPageDto>(tenantId, "ExecutiveDashboard", "executive", cancellationToken);
        return Ok(item);
    }

    [HttpGet("records/{kind}")]
    public async Task<IActionResult> SearchRecords(string kind, [FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT DashboardRecordId, JsonData FROM Analytics.DashboardRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0 AND (@SearchTerm IS NULL OR @SearchTerm='' OR Name LIKE '%' + @SearchTerm + '%' OR JsonData LIKE '%' + @SearchTerm + '%') ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await cn.QueryAsync<(Guid DashboardRecordId, string JsonData)>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).ToList();
        return Ok(new PagedResult<DashboardRecordEnvelope> { Items = rows.Select(r => new DashboardRecordEnvelope { Id = r.DashboardRecordId, JsonData = r.JsonData }).ToList(), TotalCount = rows.Count, PageNumber = 1, PageSize = rows.Count });
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] UpsertDashboardRecordRequest request, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Analytics.DashboardRecord (DashboardRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Status,@JsonData,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: cancellationToken));
        return Ok(new IdResult { Id = id });
    }

    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> UpdateRecord(Guid id, [FromBody] UpsertDashboardRecordRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Analytics.DashboardRecord SET Code=@Code, Name=@Name, Status=@Status, JsonData=@JsonData, ModifiedDateUtc=SYSUTCDATETIME() WHERE DashboardRecordId=@Id AND TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> DeleteRecord(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Analytics.DashboardRecord SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE DashboardRecordId=@Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    private async Task<T?> ReadSingleAsync<T>(Guid tenantId, string kind, string code, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT TOP 1 JsonData FROM Analytics.DashboardRecord WHERE TenantId=@TenantId AND Kind=@Kind AND Code=@Code AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var json = await cn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, Code = code }, cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public sealed class DashboardRecordEnvelope
    {
        public Guid Id { get; set; }
        public string JsonData { get; set; } = string.Empty;
    }

    private sealed class SalesAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public double ConversionPct { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class PolicyAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Workflow { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Premium { get; set; }
        public int Count { get; set; }
        public double CompletionPct { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class RetentionAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Premium { get; set; }
        public int Count { get; set; }
        public double RetentionPct { get; set; }
        public int RiskScore { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class ClaimsAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string LossType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int OpenClaims { get; set; }
        public decimal TotalIncurred { get; set; }
        public decimal TotalReserves { get; set; }
        public decimal TotalPaid { get; set; }
        public double LossRatioPct { get; set; }
        public int AverageDaysOpen { get; set; }
        public int LitigationCount { get; set; }
        public int CatastropheCount { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class FinanceAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Workflow { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public double HealthPct { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class ProducerAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Workflow { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Premium { get; set; }
        public decimal Pipeline { get; set; }
        public int Count { get; set; }
        public double ConversionPct { get; set; }
        public double RetentionPct { get; set; }
        public double GoalPct { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }

    private sealed class MarketingAnalyticsRecordDto
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Workflow { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Spend { get; set; }
        public int Count { get; set; }
        public double ConversionPct { get; set; }
        public double EngagementPct { get; set; }
        public double TrendPct { get; set; }
        public DateTime DueDateUtc { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
    }
}
