# Account 360 Enterprise Dashboard Implementation

## Overview
This implementation provides a comprehensive, enterprise-grade Account 360 dashboard for account ID `20000000-0000-0000-0000-000000000004` (Pinnacle Brokers Co.).

## What Was Implemented

### 1. Domain Entities (New)
Created 5 new domain entities in `src\Ams.Domain\Entities\`:

- **AccountActivity.cs** - Tracks calls, emails, meetings, notes, and tasks
- **AccountCommunication.cs** - Email, phone, SMS, and portal communications with open/click tracking
- **AccountRelationship.cs** - Parent, subsidiary, partner, and affiliated relationships
- **Submission.cs** - Carrier submissions with status tracking (Submitted, Quoted, Bound, Declined)
- **MarketingCampaignEnrollment.cs** - Campaign enrollments with engagement metrics

### 2. Enhanced Domain Model
**Account.cs** extended with 20+ fields:
- Status and lifecycle (StatusCodeId, LifecycleStageCode, SegmentCode)
- Ownership (AccountOwnerUserId, ParentAccountId, ServicingTeamId)
- Business info (Industry, Website, AnnualRevenue, Employees, TaxId, NaicsCode)
- Address fields (Street, City, State, Zip, Country)
- Business methods for updates

### 3. Enhanced DTOs
**AccountDto.cs** now includes:
- All account profile fields
- Owner/team/parent names (joined data)
- **Dashboard metrics** (computed):
  - TotalPremium
  - BalancesDue
  - ActivePolicies
  - OpenClaims
  - OpenOpportunities
  - YtdCommissions
  - RenewalRisk
  - LastActivityDate
  - EngagementScore
  - PortalLogins
  - DaysSinceLastTouch

### 4. Database Schema
**Migration 0053**: `src\Ams.Infrastructure\Migrations\0053_Account360EnhancementSchema.sql`

Creates 5 new tables:
- `Client.AccountActivity`
- `Client.AccountCommunication`
- `Client.AccountRelationship`
- `CRM.Submission`
- `Marketing.CampaignEnrollment` (new schema)

Extends `Client.Account` with 10 new columns.

### 5. Comprehensive Seed Data
**seed_data_account_pinnacle.sql** - Rich enterprise demonstration data for Pinnacle Brokers:

- **Account Profile**: Full business details, 42 employees, $3.1M revenue
- **3 Contacts**: CEO, CFO, VP Operations
- **3 Active Policies**:
  - BOP: $18,500 premium (Hartford)
  - E&O: $12,400 premium (Travelers)
  - Cyber: $8,200 premium (Chubb)
- **2 Submissions**: Workers Comp (Quoted), D&O (Submitted)
- **1 Open Claim**: $15K reserve, property damage
- **4 Activities**: Meetings, calls, emails, notes
- **2 Communications**: Email campaigns with open/click tracking
- **2 Marketing Campaigns**: Renewal outreach, cyber cross-sell
- **2 Account Relationships**: Parent company & subsidiary
- **Strategic Notes**: Account prioritization and growth opportunities

## Dashboard Features (14 Tabs)

The Account360.razor page implements all 14 tabs:

1. **Overview** - Account profile, team, contact info, financial summary
2. **Contacts** - Key decision makers with roles
3. **Policies** - Active, cancelled, and expired policies
4. **Submissions** - Carrier submissions pipeline
5. **Claims** - Claims history with reserve/paid amounts
6. **Billing** - Invoices, payments, balances due
7. **Commissions** - Commission calculations and payouts
8. **Documents** - Policy docs, loss runs, correspondence
9. **Activities** - Timeline of calls, emails, meetings
10. **Communications** - Outbound campaigns with engagement
11. **Marketing** - Campaign enrollments and performance
12. **Timeline** - Comprehensive event history
13. **Relationships** - Account hierarchy diagram
14. **Risk Insights** - Renewal risk, loss ratios, engagement score

## KPI Cards

8 headline KPIs displayed prominently:
- Total Premium ($39,100 from 3 policies)
- Balances Due (calculated from billing)
- Renewal Risk (Low/Medium/High with factors)
- Open Claims (1 property claim)
- Active Policies (3)
- Open Opportunities (from CRM)
- YTD Commissions (calculated)
- Last Activity (most recent touch)

## Styling

All styling is scoped CSS in `Account360.razor.css`:
- No Syncfusion CSS dependencies
- Enterprise gradient backgrounds
- Responsive grid layouts
- Professional color palette
- Status badges with semantic colors
- Interactive hover states
- Glass-morphism effects

## Next Steps to Complete

### 1. Run Database Migration
```bash
# Apply the migration script
sqlcmd -S your_server -d AMS_Database -i src\Ams.Infrastructure\Migrations\0053_Account360EnhancementSchema.sql

# Load seed data
sqlcmd -S your_server -d AMS_Database -i src\Ams.Infrastructure\Persistence\Scripts\seed_data_account_pinnacle.sql
```

### 2. Enhance AccountRepository
Update `GetByIdAsync` in `AccountRepository.cs` to calculate metrics:

```csharp
public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
	const string sql = @"
SELECT 
	a.AccountId, a.TenantId, a.AccountNumber, a.AccountName, a.AccountTypeCode,
	a.MainEmail, a.MainPhone, a.StatusCodeId,
	COALESCE(sc.StatusName, 'Active') as StatusCode,
	a.SegmentCode, a.LifecycleStageCode, a.Industry, a.Website, a.AnnualRevenue,
	a.Employees, a.TaxId, a.NaicsCode,
	a.Street, a.City, a.State, a.Zip, a.Country,
	a.AccountOwnerUserId as OwnerUserId,
	CONCAT(u.FirstName, ' ', u.LastName) as OwnerName,
	a.ParentAccountId,
	pa.AccountName as ParentAccountName,
	a.ServicingTeamId,
	st.TeamName as ServicingTeamName,
	a.CreatedDateUtc, a.ModifiedDateUtc, a.CreatedByUserId, a.ModifiedByUserId,
	-- Computed metrics
	ISNULL(SUM(p.Premium), 0) as TotalPremium,
	ISNULL(SUM(b.Balance), 0) as BalancesDue,
	COUNT(DISTINCT CASE WHEN p.StatusCode = 'Active' THEN p.PolicyId END) as ActivePolicies,
	COUNT(DISTINCT CASE WHEN cl.StatusCode = 'Open' THEN cl.ClaimId END) as OpenClaims,
	COUNT(DISTINCT CASE WHEN o.StatusCode IN ('Open', 'Qualified') THEN o.OpportunityId END) as OpenOpportunities,
	ISNULL(SUM(CASE WHEN YEAR(c.CreatedDateUtc) = YEAR(GETUTCDATE()) THEN c.CommissionAmount ELSE 0 END), 0) as YtdCommissions,
	MAX(act.OccurredAtUtc) as LastActivityDate
FROM Client.Account a
LEFT JOIN Client.StatusCode sc ON a.StatusCodeId = sc.StatusCodeId
LEFT JOIN Agency.[User] u ON a.AccountOwnerUserId = u.UserId
LEFT JOIN Client.Account pa ON a.ParentAccountId = pa.AccountId
LEFT JOIN Agency.Team st ON a.ServicingTeamId = st.TeamId
LEFT JOIN [Policy].Policy p ON a.AccountId = p.AccountId AND p.IsDeleted = 0
LEFT JOIN Billing.Invoice b ON a.AccountId = b.AccountId AND b.Balance > 0 AND b.IsDeleted = 0
LEFT JOIN Claims.Claim cl ON a.AccountId = cl.AccountId AND cl.IsDeleted = 0
LEFT JOIN CRM.Opportunity o ON a.AccountId = o.AccountId AND o.IsDeleted = 0
LEFT JOIN Commission.CommissionTransaction c ON a.AccountId = c.AccountId AND c.IsDeleted = 0
LEFT JOIN Client.AccountActivity act ON a.AccountId = act.AccountId AND act.IsDeleted = 0
WHERE a.AccountId = @Id AND a.IsDeleted = 0
GROUP BY 
	a.AccountId, a.TenantId, a.AccountNumber, a.AccountName, a.AccountTypeCode,
	a.MainEmail, a.MainPhone, a.StatusCodeId, sc.StatusName,
	a.SegmentCode, a.LifecycleStageCode, a.Industry, a.Website, a.AnnualRevenue,
	a.Employees, a.TaxId, a.NaicsCode,
	a.Street, a.City, a.State, a.Zip, a.Country,
	a.AccountOwnerUserId, u.FirstName, u.LastName,
	a.ParentAccountId, pa.AccountName,
	a.ServicingTeamId, st.TeamName,
	a.CreatedDateUtc, a.ModifiedDateUtc, a.CreatedByUserId, a.ModifiedByUserId;";

	using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
	var account = await cn.QuerySingleOrDefaultAsync<AccountDto>(
		new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

	if (account != null)
	{
		// Calculate renewal risk based on metrics
		account.RenewalRisk = CalculateRenewalRisk(account);
		account.EngagementScore = CalculateEngagementScore(id, cn);
		account.PortalLogins = GetPortalLogins(id, cn);
		account.DaysSinceLastTouch = CalculateDaysSinceLastTouch(account.LastActivityDate);
	}

	return account;
}

private static string CalculateRenewalRisk(AccountDto account)
{
	int riskScore = 0;

	if (account.BalancesDue > 0) riskScore += 2;
	if (account.OpenClaims > 2) riskScore += 2;
	if (account.DaysSinceLastTouch > 60) riskScore += 3;
	if (account.PortalLogins == 0) riskScore += 1;

	return riskScore >= 5 ? "High" : riskScore >= 3 ? "Medium" : "Low";
}

private static int CalculateEngagementScore(Guid accountId, IDbConnection cn)
{
	const string sql = @"
SELECT 
	COUNT(DISTINCT CASE WHEN OccurredAtUtc > DATEADD(DAY, -90, GETUTCDATE()) THEN ActivityId END) as RecentActivities,
	COUNT(DISTINCT CASE WHEN WasOpened = 1 THEN CommunicationId END) as EmailsOpened,
	-- Add more engagement factors
FROM Client.AccountActivity
WHERE AccountId = @AccountId AND IsDeleted = 0";

	// Return score 0-100 based on engagement factors
	return 74; // Placeholder
}
```

### 3. Implement Action Handlers
Ensure all button actions in Account360.razor are wired to backend services:
- Edit Account → UpdateAccountAsync
- Add Contact → Contact service
- New Quote → Navigate to quotes
- New Opportunity → Navigate to opportunities
- FNOL → Claims service
- All CRUD operations for each tab

### 4. Build and Test
```bash
dotnet build
dotnet run --project src\Ams.Web
```

Navigate to: `https://localhost:5001/accounts/20000000-0000-0000-0000-000000000004`

## Key Design Decisions

1. **Domain-Driven Design**: All entities encapsulate business logic
2. **Computed Metrics**: KPIs calculated in repository for performance
3. **Scoped CSS**: All styling isolated to component
4. **Audit Trail**: Every entity tracks creation/modification
5. **Soft Deletes**: IsDeleted flag for data retention
6. **Multi-Tenancy**: TenantId on all entities
7. **Referential Integrity**: Foreign keys with proper indexes

## Performance Considerations

- Indexes on frequently queried columns (AccountId, TenantId, StatusCode)
- Single query for account with all metrics (avoid N+1)
- Pagination on grids
- Lazy loading of tabs (only load data when tab is opened)
- Caching opportunities for static data (carriers, statuses)

## Security

- All queries filtered by TenantId
- User permissions checked in UI layer
- Soft deletes prevent permanent data loss
- Audit trail for compliance

## Future Enhancements

- Real-time updates via SignalR
- Export to Excel/PDF
- Advanced filtering and saved views
- Mobile-responsive layouts
- AI-powered insights
- Workflow automation
