# CRM Opportunities Enhancement - Implementation Guide

## Overview

This implementation adds three new comprehensive views for managing sales opportunities in the CRM module:

1. **New Opportunity** (`/crm/opportunities/new`) - Create new sales opportunities
2. **Opportunity Board** (`/crm/opportunities/board`) - Kanban-style board for managing opportunities by stage
3. **Pipeline View** (`/crm/opportunities/pipeline`) - Analytics and visualization of sales pipeline

## Files Created

### Frontend Components (Blazor)

#### 1. OpportunityNew.razor
- **Route**: `/crm/opportunities/new`
- **Purpose**: Form to create new opportunities
- **Features**:
  - Opportunity name, account selection
  - Amount and win probability inputs
  - Close date selection
  - Forecast category assignment
  - Owner assignment
  - Description/notes
  - Form validation
  - Reference data loading (accounts, users, leads)

**File Location**: `src\Ams.Web\Components\Pages\Crm\OpportunityNew.razor`

#### 2. OpportunityBoard.razor
- **Route**: `/crm/opportunities/board`
- **Purpose**: Kanban board view for managing opportunities by stage
- **Features**:
  - Drag-and-drop to move opportunities between stages
  - Filter by owner
  - Stage metrics (count, total value)
  - Color-coded cards for hot opportunities (>75% win probability)
  - Quick view of opportunity details
  - Real-time stage transitions

**File Location**: `src\Ams.Web\Components\Pages\Crm\OpportunityBoard.razor`

#### 3. OpportunityPipeline.razor
- **Route**: `/crm/opportunities/pipeline`
- **Purpose**: Detailed pipeline analytics and forecasting
- **Features**:
  - Pipeline summary (total, weighted, average win rate)
  - Stage breakdown with visualization
  - Forecast distribution analysis
  - Win rate analysis by probability ranges
  - Comprehensive data table with sorting/filtering
  - Multiple timeframe options (This month, quarter, year)

**File Location**: `src\Ams.Web\Components\Pages\Crm\OpportunityPipeline.razor`

### Styling

#### OpportunityNew.razor.css
- Form styling and layout
- Input validation states
- Button styles
- Responsive grid layout

#### OpportunityBoard.razor.css
- Kanban column styling
- Card animations and hover states
- Stage badges with colors
- Drag-and-drop visual feedback
- Color-coded win probability indicators

#### OpportunityPipeline.razor.css
- Summary card styling
- Pipeline bar charts
- Forecast distribution cards
- Win rate visualization
- Responsive grid layouts

### API Controller

**OpportunitiesExtendedController.cs**

Located at: `src\Ams.Api\Controllers\OpportunitiesExtendedController.cs`

New endpoints:

```
GET  /api/opportunities/board         - Get opportunities for board view
GET  /api/opportunities/pipeline      - Get opportunities for pipeline view
PUT  /api/opportunities/{id}/stage    - Update opportunity stage
GET  /api/opportunities/metrics       - Get pipeline metrics and analytics
GET  /api/opportunities/stages        - Get stage options
GET  /api/opportunities/forecasts     - Get forecast categories
```

### Database

**Migration Script**: `database\migrations\001_CRM_Opportunities_Enhancement.sql`

#### New Tables:
1. **CRM.OpportunityStage** - Lookup table for opportunity stages
   - Columns: StageId, TenantId, Code, Label, Description, StageOrder, IsActive
   - Seed Data: Qualified, Proposal, Negotiation, Closed Won, Closed Lost

2. **CRM.ForecastCategory** - Lookup table for forecast categories
   - Columns: CategoryId, TenantId, Code, Label, Description, ForecastPercent, IsActive
   - Seed Data: Pipeline (10%), Best Case (50%), Commitment (75%), Forecast (100%), Omitted (0%)

3. **CRM.OpportunityActivity** - Audit trail for opportunity changes
   - Tracks stage changes, amount updates, owner assignments, notes

#### Enhanced Tables:
- **CRM.Opportunity** - Added columns:
  - Stage NVARCHAR(50)
  - Description NVARCHAR(MAX)
  - CampaignId UNIQUEIDENTIFIER

#### Helper Views:
- **CRM.vw_OpportunityPipelineAnalysis** - Comprehensive view for analytics

#### Stored Procedures:
- **CRM.sp_InsertSampleOpportunities** - For generating test data

## Implementation Steps

### 1. Deploy Database Migration

```sql
-- Run the migration script in your SQL Server database
sqlcmd -S your_server -d your_database -i "database\migrations\001_CRM_Opportunities_Enhancement.sql"
```

Or execute the script through SQL Server Management Studio.

### 2. Add API Controller

The OpportunitiesExtendedController is already added to the solution. Ensure it's registered in dependency injection if needed.

### 3. Update Navigation

The navigation menu items are already configured in `NavSidebar.razor`:

```csharp
new("New Opportunity",    "/crm/opportunities/new",    "bi bi-plus-circle"),
new("Opportunity Board",  "/crm/opportunities/board",  "bi bi-kanban"),
new("Pipeline View",      "/crm/opportunities/pipeline","bi bi-funnel"),
```

### 4. Build and Test

```bash
# Build solution
dotnet build

# Run tests (if any)
dotnet test

# Run application
dotnet run --project src/Ams.Web
```

## Feature Details

### New Opportunity Form

**Validation Rules**:
- Opportunity Name: Required, max 255 characters
- Account: Required
- Estimated Amount: Required, minimum 0
- Win Probability: 0-100%, defaults to 50%
- Close Date: Required, typically 30-90 days out
- Forecast Category: Optional, defaults to "Pipeline"

**Data Flow**:
1. User fills form with opportunity details
2. Reference data (accounts, users, leads) loaded from API
3. Form validation on client and server
4. POST to `/api/opportunities` with payload
5. New opportunity created in database
6. User redirected to opportunity detail view

### Opportunity Board

**Features**:
- **Drag and Drop**: Move opportunities between columns (stages)
- **Real-time Updates**: Stage changes reflected immediately
- **Visual Indicators**:
  - Hot opportunities (🔥) for >75% win probability
  - Amount displayed on cards
  - Owner initials in avatar
  - Close date countdown
- **Filtering**: Filter by opportunity owner
- **Stage Metrics**: Count and total value per stage

**Color Coding**:
- Qualified: Blue (#3b82f6)
- Proposal: Purple (#8b5cf6)
- Negotiation: Amber (#f59e0b)
- Closed Won: Green (#22c55e)
- Closed Lost: Red (#ef4444)

### Pipeline View

**Sections**:

1. **Summary Dashboard**
   - Total Pipeline: Sum of all opportunity amounts
   - Weighted Pipeline: Amount × Win Probability
   - Average Win Probability
   - Current Month Close

2. **Stage Breakdown**
   - Horizontal bar charts showing pipeline by stage
   - Average amount per stage
   - Weighted amount per stage

3. **Forecast Distribution**
   - Distribution across forecast categories
   - Visual breakdown of pipeline
   - Average opportunity value per category

4. **Win Rate Analysis**
   - Distribution of opportunities by win probability ranges (0-25%, 26-50%, 51-75%, 76-100%)
   - Count and total value for each range

5. **Data Table**
   - All opportunities with sortable columns
   - Pagination (25 per page)
   - Columns: Name, Account, Stage, Amount, Win %, Weighted, Close Date

**Timeframe Filters**:
- All Time (default)
- This Month
- Next Month
- This Quarter
- This Year

## Data Model

### Opportunity Entity Fields

```csharp
public sealed class Opportunity : AuditableEntity
{
    public string OpportunityNumber { get; set; }              // Unique identifier
    public Guid AccountId { get; set; }                        // Reference to Account
    public string OpportunityName { get; set; }                // Opportunity name/title
    public decimal EstimatedAmount { get; set; }               // Deal value
    public Guid? OwnerUserId { get; set; }                     // Sales rep ownership
    public int WinProbability { get; set; }                    // 0-100%
    public DateTime CloseDate { get; set; }                    // Expected close date
    public string Stage { get; set; }                          // Current stage
    public string ForecastCategoryCode { get; set; }           // Forecast category
    public Guid? LeadId { get; set; }                          // Source lead
    public string? Description { get; set; }                   // Additional notes
    public Guid? CampaignId { get; set; }                      // Marketing campaign
}
```

## API Response Examples

### Board View Response
```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Acme Corp Annual Renewal",
      "accountName": "Acme Corporation",
      "amount": 250000,
      "winProbability": 85,
      "closeDate": "2024-03-31",
      "stage": "Proposal",
      "ownerInitials": "JD"
    }
  ]
}
```

### Pipeline Metrics Response
```json
{
  "success": true,
  "data": {
    "totalPipeline": 5250000,
    "weightedPipeline": 3187500,
    "totalOpportunities": 24,
    "avgWinProbability": 60.8,
    "thisMonthClose": 850000,
    "closingThisMonth": 3,
    "byStage": [
      {
        "stageName": "Proposal",
        "count": 8,
        "totalAmount": 1850000,
        "averageAmount": 231250,
        "weightedAmount": 1232500
      }
    ],
    "byForecast": [...],
    "winRateDistribution": [...]
  }
}
```

## Security Considerations

1. **Tenant Isolation**: All queries filtered by TenantId
2. **User Authorization**: Verify user has access to opportunities
3. **Data Validation**: Server-side validation on all inputs
4. **Audit Trail**: OpportunityActivity table tracks all changes
5. **Rate Limiting**: Consider implementing rate limits on APIs

## Performance Optimization

1. **Indexed Queries**:
   - TenantId on all tables
   - Stage, ForecastCategory codes
   - CloseDate for timeframe filtering

2. **Caching**:
   - Stage and forecast lookups (rarely change)
   - User/account reference data

3. **Pagination**:
   - Pipeline table limited to 25 records per page
   - API endpoints support pageSize parameter

4. **Lazy Loading**:
   - Reference data loaded on component initialization
   - Opportunity details loaded on navigation

## Testing Recommendations

### Unit Tests
- Stage transition validation
- Amount calculations
- Win probability ranges
- Date filtering logic

### Integration Tests
- Create opportunity endpoint
- Update stage endpoint
- Pipeline metrics calculation
- Board view data retrieval

### E2E Tests
- Create new opportunity flow
- Drag-drop stage transition on board
- Filter and sort on pipeline view
- Navigate between views

## Sample Test Data

Run the stored procedure to generate sample opportunities:

```sql
EXEC CRM.sp_InsertSampleOpportunities 
    @TenantId = '550e8400-e29b-41d4-a716-446655440000',
    @NumberOfOpportunities = 25;
```

This creates 25 test opportunities across all stages with realistic data.

## Troubleshooting

### Issue: "Stage not found" error
**Solution**: Verify OpportunityStage lookup table has been populated. Run the migration script to seed default stages.

### Issue: Opportunities not appearing on board
**Solution**: Check:
1. OpportunityActivity records exist for opportunities
2. User has tenant access
3. Opportunities have valid Stage values

### Issue: Pipeline metrics showing zero
**Solution**: Ensure:
1. Opportunities have CloseDate values
2. ForecastCategory codes match lookup table
3. WinProbability is set (not null)

## Future Enhancements

1. **Bulk Operations**
   - Bulk stage updates
   - Bulk owner reassignment

2. **Advanced Analytics**
   - Win/loss rate by stage
   - Average sales cycle duration
   - Forecast accuracy
   - Revenue by source/campaign

3. **Notifications**
   - Opportunity stage changes
   - Close date reminders
   - Forecast threshold alerts

4. **Integrations**
   - Email sync with opportunities
   - Calendar integration for close dates
   - Slack notifications

5. **Mobile View**
   - Mobile-optimized board view
   - Touch-friendly drag-drop

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review the database migration logs
3. Check application event logs
4. Verify API response status codes
5. Contact development team with error details

---

**Version**: 1.0  
**Last Updated**: 2024  
**Status**: Production Ready
