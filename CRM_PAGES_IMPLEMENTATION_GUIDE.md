# CRM Pages Implementation Guide

## Overview

This document provides complete implementation details for the CRM module pages, including the newly created Segmentation Rules and Data Enrichment pages, as well as the existing Segmentation and Duplicates management pages.

---

## Pages Summary

### 1. Customer Segmentation (`/crm/segments`)
**File:** `CustomerSegmentation.razor`
**Purpose:** Define and manage account segments for targeting and analysis
**Features:**
- Segment creation with custom colors and types (Tier, Industry, Size, Risk, Lifecycle, Geographic, Custom)
- Auto-scoring rules for automatic segmentation
- KPI display showing total segments, classified accounts, premium, and active rules
- Segment detail view with metrics and accounts list
- Edit and export capabilities

**Key Components:**
- `SegmentModel` record with properties: SegmentId, Name, Description, SegmentType, Color, IsAutomated, AccountCount, TotalPremium, AvgPremium, RetentionRate, LossRatio, AvgRelationshipYears
- `SegmentAccount` record for account listing
- Dialog-based add/edit forms
- SfGrid for account display with sorting and pagination

---

### 2. Segmentation Rules (`/crm/segments/rules`) [NEW]
**File:** `SegmentationRules.razor`
**Purpose:** Define automatic segmentation scoring rules for accounts
**Features:**
- Rule creation with field-based criteria
- Support for multiple operators (equals, not_equals, greater_than, less_than, contains)
- Point-based scoring system
- Active/inactive rule toggling
- Accuracy metrics showing rule effectiveness
- Account match counts

**Key Components:**
- `SegmentationRuleModel` record with: RuleId, RuleName, Description, Segment, Criteria, AccountsMatched, Accuracy, IsActive, CreatedDate
- `SegmentationRuleForm` for creating/editing rules
- `SegmentationCriterion` for individual scoring criteria
- Dynamic criterion addition/removal
- SfGrid display with filtering and sorting

**Data Model Example:**
```
{
  "RuleId": "guid",
  "RuleName": "VIP Enterprise Clients",
  "Description": "Automatically segment enterprise clients with high annual revenue",
  "Segment": "VIP Tier",
  "Criteria": "Annual Revenue > $10M AND Employees > 500",
  "AccountsMatched": 247,
  "Accuracy": 0.94,
  "IsActive": true
}
```

---

### 3. Duplicate Management (`/crm/duplicates`)
**File:** `DuplicateManagement.razor`
**Purpose:** Detect, review, and resolve duplicate account and contact records
**Features:**
- Duplicate group detection with confidence scoring (0-100%)
- Entity type filtering (Accounts/Contacts)
- Confidence-based grouping (High >= 90%, Needs Review 70-89%)
- Side-by-side comparison view
- Master record designation
- Bulk merge and dismiss operations
- Resolved job tracking

**Key Components:**
- `DuplicateGroup` record with: GroupId, EntityType, PrimaryName, Records, ConfidenceScore, Status, DetectedAt, MatchReasons
- `DuplicateRecord` with: Id, Name, IsPrimary, Similarity
- Expandable group rows with comparison tables
- Field-level match highlighting
- Confidence scoring visualization

---

### 4. Data Enrichment (`/crm/enrichment`) [NEW]
**File:** `DataEnrichment.razor`
**Purpose:** Enrich account and contact data with third-party data providers
**Features:**
- Multiple data provider integration (ZoomInfo, LinkedIn, Apollo.io, Hunter.io, Clearbit, etc.)
- Provider connection management with API key storage
- Field selection for selective enrichment
- Auto-enrichment scheduling options
- Job history tracking with success rates
- Detailed enrichment job monitoring
- Multi-tab interface (Providers / Job History)

**Key Components:**
- `DataProviderModel` record: Name, Description, Icon, AvailableFields, IsConnected
- `EnrichmentJobModel`: JobId, JobName, ProviderName, Status, RecordsRequested, RecordsEnriched, SuccessRate, StartedAt, CompletedAt
- `ProviderConfigModel`: ApiKey, SelectedFields, EnableAutoEnrich
- Provider card grid with connection status
- Job history SfGrid with filtering

**Supported Providers:**
- **ZoomInfo** - Company and contact intelligence
- **LinkedIn** - Professional network data
- **Apollo.io** - Sales intelligence platform
- **Hunter.io** - Email finder and verifier
- **Clearbit** - B2B data enrichment

---

## Data Models & Records

### Segmentation Models
```csharp
record SegmentModel
{
    Guid SegmentId { get; init; }
    string Name { get; init; }
    string Description { get; init; }
    string SegmentType { get; init; } // Tier, Industry, Size, Risk, Lifecycle, Geographic, Custom
    string Color { get; init; }
    bool IsAutomated { get; init; }
    bool IsVisible { get; init; }
    bool IsCampaignAudience { get; init; }
    int AccountCount { get; init; }
    decimal TotalPremium { get; init; }
    decimal AvgPremium { get; init; }
    double RetentionRate { get; init; }
    double LossRatio { get; init; }
    double AvgRelationshipYears { get; init; }
    List<ScoringRule> Rules { get; init; }
}

record SegmentationRuleModel
{
    Guid RuleId { get; init; }
    string RuleName { get; init; }
    string Description { get; init; }
    string Segment { get; init; }
    string SegmentColor { get; init; }
    string Criteria { get; init; }
    int AccountsMatched { get; init; }
    double Accuracy { get; init; }
    bool IsActive { get; init; }
    DateTime CreatedDate { get; init; }
}
```

### Enrichment Models
```csharp
record DataProviderModel
{
    string Name { get; init; }
    string Description { get; init; }
    string Icon { get; init; }
    List<string> AvailableFields { get; init; }
    bool IsConnected { get; init; }
}

record EnrichmentJobModel
{
    Guid JobId { get; init; }
    string JobName { get; init; }
    string ProviderName { get; init; }
    string Status { get; init; } // Completed, In Progress, Failed, Pending
    int RecordsRequested { get; init; }
    int RecordsEnriched { get; init; }
    double SuccessRate { get; init; }
    DateTime StartedAt { get; init; }
    DateTime? CompletedAt { get; init; }
}
```

---

## API Integration Points

### Segmentation Rules API Endpoints (To be implemented)
```
GET    /api/crm/segmentation-rules              - List all rules
POST   /api/crm/segmentation-rules              - Create new rule
GET    /api/crm/segmentation-rules/{id}         - Get rule details
PUT    /api/crm/segmentation-rules/{id}         - Update rule
DELETE /api/crm/segmentation-rules/{id}         - Delete rule
POST   /api/crm/segmentation-rules/{id}/activate - Activate rule
POST   /api/crm/segmentation-rules/{id}/deactivate - Deactivate rule
POST   /api/crm/segmentation-rules/{id}/execute - Execute rule manually
```

### Data Enrichment API Endpoints (To be implemented)
```
GET    /api/crm/enrichment/providers            - List providers
POST   /api/crm/enrichment/providers/{name}/connect    - Connect provider
POST   /api/crm/enrichment/providers/{name}/configure  - Save configuration
GET    /api/crm/enrichment/jobs                 - List enrichment jobs
POST   /api/crm/enrichment/jobs/run              - Run enrichment job
GET    /api/crm/enrichment/jobs/{id}            - Get job details
```

---

## UI Components & Styling

### KPI Cards
- 4-card strip showing key metrics
- Color-coded icons (blue, orange, light-blue, green)
- Large value text with descriptive labels
- Responsive grid layout (auto-fit, minmax 200px)

### Filter Bar
- Search input with icon
- Dropdown filters (Segment, Status)
- Responsive flex layout with wrapping
- Bulk action buttons (disabled state management)

### Data Grids
- Syncfusion SfGrid components
- Configurable pagination (10-25 items per page)
- Sorting on all columns
- Hover effects on rows
- Custom cell templates for complex data
- Toolbar with search/filter

### Dialog Forms
- Modal dialogs with header, content, footer
- Width: 500px-600px
- Form validation with error messages
- Submit button disabled state based on validation
- Criterion management with add/remove buttons
- Grid-based layout for form fields

---

## Implementation Checklist

### Phase 1: API Controllers
- [ ] Create `SegmentationRulesController` with CRUD endpoints
- [ ] Create `DataEnrichmentController` with provider and job endpoints
- [ ] Add authorization checks for CRM module
- [ ] Implement validation for rule creation
- [ ] Add tenant filtering for multi-tenant support

### Phase 2: Database Schema
- [ ] Create `CRM.SegmentationRule` table
- [ ] Create `CRM.SegmentationCriterion` table
- [ ] Create `CRM.DataProvider` table
- [ ] Create `CRM.EnrichmentJob` table
- [ ] Create `CRM.ProviderConfiguration` table
- [ ] Add relationships and indexes
- [ ] Include audit fields (CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted)

### Phase 3: Service Layer
- [ ] Create `ISegmentationRuleService` interface
- [ ] Implement `SegmentationRuleService`
- [ ] Create `IDataEnrichmentService` interface
- [ ] Implement `DataEnrichmentService`
- [ ] Add business logic for rule validation
- [ ] Add provider connection management logic

### Phase 4: Testing
- [ ] Unit tests for service layer
- [ ] Integration tests for API endpoints
- [ ] UI component tests for Blazor components
- [ ] E2E tests for critical workflows

### Phase 5: Documentation & Deployment
- [ ] API documentation
- [ ] User guide for segmentation rules
- [ ] User guide for data enrichment
- [ ] Deployment scripts
- [ ] Configuration documentation

---

## Navigation Integration

Both pages are integrated in `NavSidebar.razor` under the CRM section:

```csharp
new("crm", "CRM", "bi bi-funnel", [
    new("crm", "CRM", "bi bi-funnel", [
        new("Segmentation",       "/crm/segments",         "bi bi-pie-chart-fill"),
        new("Segmentation Rules", "/crm/segments/rules",   "bi bi-sliders2"),
        new("Data Enrichment",    "/crm/enrichment",       "bi bi-puzzle"),
        // ... other CRM items
    ]),
]),
```

---

## File Structure

```
src/Ams.Web/Components/Pages/Crm/
├── SegmentationRules.razor
├── SegmentationRules.razor.css
├── DataEnrichment.razor
├── DataEnrichment.razor.css
├── CustomerSegmentation.razor (existing)
├── CustomerSegmentation.razor.css (existing)
├── DuplicateManagement.razor (existing)
└── DuplicateManagement.razor.css (existing)
```

---

## Styling Standards

### CSS Classes
- Prefix: `sr-` (Segmentation Rules), `de-` (Data Enrichment)
- KPI strip: Grid-based responsive layout
- Cards: Flexbox with gap and padding
- Icons: Bootstrap icons (bi-*)
- Colors: CSS variables for theming (--color-bg-secondary, --color-border, etc.)

### Color Schemes
**Segmentation Rules:**
- Primary: #3b82f6 (Blue)
- Success: #22c55e (Green)
- Warning: #f59e0b (Orange)

**Data Enrichment:**
- Primary: #3b82f6 (Blue)
- Success: #22c55e (Green)
- Warning: #f59e0b (Orange)
- Secondary: #ec4899 (Pink)

---

## State Management

### Component State
- `_loading` bool - Page load state
- `_search` string - Search/filter input
- `_filterSegment`, `_filterStatus` - Filter selections
- `_showDialog` bool - Dialog visibility
- `_selectedRule/Provider` - Selected item for editing
- `_formData` - Form model for create/edit operations

### Data Collections
- `_rules` - List of segmentation rules
- `_providers` - List of data providers
- `_jobs` - List of enrichment jobs

---

## Performance Considerations

1. **Pagination:** Use SfGrid paging (15 items per page) for large datasets
2. **Filtering:** Client-side filtering for now, migrate to server when dataset grows
3. **Caching:** Consider caching provider list and rules periodically
4. **Debouncing:** Implement search debouncing to reduce filter operations
5. **Lazy Loading:** Load enrichment job details only when needed

---

## Security Considerations

1. **Authorization:** Restrict to users with CRM.SegmentationRules and CRM.Enrichment permissions
2. **API Key Storage:** Never expose API keys in client code; store securely server-side
3. **Data Validation:** Validate all user inputs on both client and server
4. **Audit Logging:** Log all rule changes and enrichment jobs for compliance
5. **Tenant Isolation:** Ensure multi-tenant data segregation with TenantId

---

## Future Enhancements

1. **Advanced Analytics:**
   - Rule effectiveness dashboards
   - Enrichment ROI tracking
   - Provider performance comparison

2. **Automation:**
   - Scheduled rule execution
   - Automatic enrichment batching
   - Workflow integration for rule-triggered actions

3. **Machine Learning:**
   - Predictive rule recommendations
   - Automated accuracy optimization
   - Anomaly detection in enrichment data

4. **Integration:**
   - Two-way sync with external data sources
   - Webhook support for real-time updates
   - API key rotation and management

5. **Reporting:**
   - Rule execution reports
   - Enrichment job analytics
   - Data quality metrics

---

## Support & Troubleshooting

### Common Issues

**Enrichment Job Fails**
- Check provider API key validity
- Verify rate limit settings
- Review error logs for specific failure reasons
- Retry job with smaller batch size

**Rules Not Executing**
- Verify rule is active (IsActive = true)
- Check rule syntax for criteria errors
- Ensure criteria fields exist in data model
- Review account data for criteria match

**Slow Rule Evaluation**
- Check index coverage on criteria fields
- Consider breaking complex rules into multiple simpler rules
- Use scheduling for non-critical rules

---

## Contact & Documentation

For questions or issues:
- Review the implementation guide
- Check test files for usage examples
- Review API documentation
- Contact the CRM module team

**Last Updated:** 2025-01-15
**Version:** 1.0
