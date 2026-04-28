# Lead Assignment Page Implementation

**File**: `src/Ams.Web/Components/Pages/Crm/LeadAssignment.razor`  
**Route**: `/crm/leads/assignment`  
**Navigation**: CRM & Demand → Lead Management → Lead Assignment

## Overview

The Lead Assignment page is a comprehensive lead-to-producer assignment management system designed to:
- Display unassigned leads with searchable, filterable grid
- Visualize producer workload and capacity
- Configure automated assignment rules
- Track assignment history with audit trail
- Support single and bulk assignment operations

## Features

### 1. **Unassigned Leads View** (Default)
Displays all leads awaiting producer assignment with:
- **Grid Display**: 15 mock leads with columns for Name, Company, Score, Source, Contact, and Created Date
- **Search & Filter**: Real-time search by name/company, filter by source (Web, Referral, Direct, Partner, Organic) and score range (80+, 50-79, <50)
- **Score Badges**: Color-coded score indicators (Hot ≥80, Warm 50-79, Cold <50)
- **Bulk Selection**: Select all visible leads or individual leads for batch assignment
- **Single Assignment**: Click action button to assign individual lead to producer via drawer

### 2. **Producer Workload View**
Visual dashboard showing producer capacity and lead distribution:
- **Workload Cards**: For each producer (John Spencer, Amanda Hayes, Ryan Mitchell, Jessica Brown, Thomas Anderson)
- **Capacity Metrics**: Current leads, capacity limit, and available slots
- **Visual Progress Bar**: Color-coded workload indicators (Low/Med/High/Over)
- **Status Labels**: Percentage of capacity utilization
- **Edit Capacity**: Button to adjust producer capacity settings

### 3. **Assignment Rules View**
Configuration interface for automated assignment rules:
- **Rule Types**: Score-Based, Round-Robin, Territory-Based
- **Rule Management**: Create, edit, activate/deactivate rules
- **Rule Cards**: Display rule name, strategy, criteria, target group, and max assignments
- **Toggle Controls**: Enable/disable rules without deleting
- **Delete Function**: Remove obsolete rules
- **Pre-configured Rules**: 3 sample rules (High-Score Auto Assign, Round-Robin Distribution, Territory Routing)

### 4. **Assignment History View**
Audit trail of all assignments with timeline:
- **History Items**: Timeline view showing lead name, assigned producer, date/time, and assignment method
- **Assignment Methods**: Manual, Auto (Score-Based), Round-Robin, Auto (Territory)
- **Period Filtering**: Last 7 Days, Last 30 Days, Last 90 Days, All Time
- **Visual Timeline**: Icons and color-coded badges for quick identification

## KPI Dashboard

Four key performance indicators at page top:
1. **Unassigned Leads**: Count of leads waiting for assignment (15 in mock data)
2. **Active Producers**: Number of available producers (5 in mock data)
3. **Avg Leads per Producer**: Average workload distribution (3.0 in mock data)
4. **Workload Balance**: Percentage of total capacity in use (74% in mock data)

## User Interactions

### Assign Single Lead
1. Click action button in leads grid → Opens right sidebar drawer
2. Lead summary displays in drawer with company name
3. Select target producer from dropdown
4. Optionally add assignment notes
5. Click "Assign Lead" to complete
6. Lead removed from unassigned list, history record created

### Bulk Assign Leads
1. Select multiple leads using checkboxes
2. Click "Assign [N]" button to open bulk drawer
3. Choose assignment strategy:
   - **Manual**: Assign all to single selected producer
   - **Round-Robin**: Distribute across producer group
   - **Score-Based**: Auto-assign based on lead score thresholds
   - **Territory-Based**: Route based on territory mapping
4. Select target producer/group based on strategy
5. Click "Assign [N] Leads" to execute bulk operation

### Configure Rules
1. Switch to Rules view
2. Click "New Rule" to open rule creation drawer
3. Define rule name, strategy, criteria, and target group
4. Save rule - immediately available for automated assignment
5. Toggle rule active/inactive without deletion
6. Delete rule when no longer needed

### View Assignment History
1. Switch to History view
2. Filter by time period using dropdown
3. Review timeline of all assignments
4. See assignment method for each operation
5. Verify proper workload distribution

## Data Models

### LeadRow (Mock Data: 15 leads)
```csharp
record LeadRow(
    int Id,                    // 1-15
    string Name,               // e.g., "Sarah Anderson"
    string Company,            // e.g., "Tech Innovations Inc"
    int Score,                 // 62-91
    string Source,             // Web, Referral, Direct, Partner, Organic
    string Email,              // e.g., "sarah@techinnovations.com"
    DateTime CreatedDate       // Recent dates
);
```

### ProducerRow (Mock Data: 5 producers)
```csharp
record ProducerRow(
    int Id,                    // 1-5
    string Name,               // Senior/Regular Producer names
    string Title,              // Senior Producer, Producer, Account Executive
    int LeadCount,             // Current assigned leads (8-24)
    int Capacity               // Max capacity (20-25)
);
```

### RuleRow (Mock Data: 3 rules)
```csharp
record RuleRow(
    int Id,
    string Name,               // Human-readable rule name
    string Strategy,           // Score-Based, Round-Robin, Territory-Based
    string Criteria,           // Assignment condition
    string ProducerGroup,      // Target group or "All"
    int MaxAssignments,        // 0 = unlimited
    bool Active                // Rule enabled/disabled
);
```

### HistoryRow (Mock Data: 5 history records)
```csharp
record HistoryRow(
    int Id,
    string LeadName,
    string ProducerName,
    DateTime AssignedDate,
    string Method              // Manual, Auto (Score-Based), Round-Robin
);
```

## Styling

**CSS Prefix**: `.la-` (Lead Assignment)

### Key Style Classes
- `.la-view-toggle`: View switcher button group
- `.la-kpi-strip`: KPI card container
- `.la-leads-container`: Leads view wrapper
- `.la-filter-bar`: Search and filter controls
- `.la-leads-table`: Data grid styling
- `.la-score--hot/warm/cold`: Score badge colors
- `.la-workload-grid`: Producer card grid
- `.la-workload-bar`: Capacity visualization bars
- `.la-rules-grid`: Rule card grid
- `.la-history-list`: Timeline item list
- `.la-drawer-overlay/.la-drawer-panel`: Drawer component styling
- `.la-form-group/.la-textarea`: Form element styling

### Responsive Breakpoints
- **768px**: Adjust grid to single column, drawer full-width
- **600px**: Stack KPI cards 2-per-row, collapse drawer footer buttons

## Mock Data Strategy

All data is generated in `BuildMockData()` method:
- **15 unassigned leads** with realistic names, companies, and scores
- **5 producers** with varying workload (8-24 leads) against 20-25 capacity
- **3 assignment rules** demonstrating different strategies
- **5 history records** showing recent assignments

To connect to real API:
1. Replace `await Task.Delay(300)` with actual API call
2. Update `LoadAsync()` to populate `_leads`, `_producers`, `_rules`, `_history` from API
3. Update `AssignLead()` and `BulkAssignLeads()` to POST assignment to backend
4. Update `DeleteRule()` to DELETE rule via API

## Code Organization

### @page Directive
```razor
@page "/crm/leads/assignment"
```

### Injected Services
- `ApiClient`: For data operations (currently using mock data)
- `NavigationManager`: For URL navigation
- `BreadcrumbService`: For breadcrumb trail management

### Lifecycle
1. `OnInitializedAsync()`: Sets breadcrumb, calls `LoadAsync()`
2. `LoadAsync()`: Simulates API call, builds mock data, updates KPI

### State Management
- `_view`: Current active view (leads, workload, rules, history)
- `_selectedLeads`: HashSet of selected lead IDs for bulk operations
- `_drawerOpen`: Boolean controlling drawer visibility
- `_drawerMode`: String indicating drawer purpose (assign-single, assign-bulk, rule)

### Filter & Search
- `GetVisibleLeads()`: Apply search query and filters
- `ClearFilters()`: Reset all filters
- `ToggleLead()`: Add/remove from selection
- `SelectAllLeads()`: Select all visible leads

### Assignment Operations
- `OpenAssignDrawer()`: Open single-lead assignment drawer
- `OpenBulkAssignDrawer()`: Open bulk assignment drawer
- `AssignLead()`: Process single assignment
- `BulkAssignLeads()`: Process bulk assignment
- `UpdateKpi()`: Recalculate KPI metrics after operation

### Rule Management
- `OpenRuleDrawer()`: Open rule creation drawer
- `EditRule()`: Open rule editing drawer
- `ToggleRuleActive()`: Enable/disable rule
- `DeleteRule()`: Remove rule

### Utilities
- `GetAvatarColor()`: Consistent avatar color assignment
- `GetScoreBadgeClass()`: Score-to-CSS-class mapping
- `ShowToast()`: Display toast notifications

## Avatar Color System

Six-color rotation for consistent avatar display:
```csharp
_avatarColors = ["#dc2626", "#f59e0b", "#10b981", "#0369a1", "#7c3aed", "#db2777"]
```
Colors assigned via: `avatarColors[id % avatarColors.Length]`

## Toast Notifications

Uses `SfToast` component with standard messages:
- Success: "Lead Assigned", "Bulk Assignment Complete", "Rule Deleted"
- Warning: "Invalid Selection"
- Error: "Failed to load data"

## Future Enhancements

1. **Real API Integration**: Connect to backend endpoints for CRUD operations
2. **Advanced Filters**: Add territory, producer tier, time-in-system filters
3. **Drag-Drop Assignment**: Allow drag-drop from leads to producers
4. **Automated Assignment**: Configure time-based automation
5. **Performance Analytics**: Track assignment success rates
6. **Capacity Planning**: Forecast capacity needs based on lead velocity
7. **Assignment Preferences**: Allow producers to set assignment preferences
8. **Notification System**: Notify producers of new assignments
9. **Edit Capacity Drawer**: Full implementation of capacity editor
10. **Export/Import**: Bulk operations via CSV import

## Testing Checklist

- [x] Page loads without errors
- [x] All 4 views switch correctly
- [x] Search filters leads by name/company
- [x] Source and score filters work independently
- [x] Select all/individual leads toggle correctly
- [x] Single assignment drawer opens/closes
- [x] Bulk assignment drawer shows strategy options
- [x] KPI updates after assignment operations
- [x] History records display in timeline
- [x] Rules can be toggled and deleted
- [x] Toast notifications display
- [x] Responsive layout on mobile/tablet
- [x] Breadcrumb trail displays correctly

## Build Status

✅ **Build Successful** - No compilation errors  
✅ **Page Routing** - `/crm/leads/assignment` configured  
✅ **Navigation Integration** - Listed in NavSidebar.razor  
✅ **Styling** - CSS isolation working correctly  
✅ **Mock Data** - All test data loading  
✅ **Components** - All UI elements rendering

## Files

- `src/Ams.Web/Components/Pages/Crm/LeadAssignment.razor` - Component (850+ lines)
- `src/Ams.Web/Components/Pages/Crm/LeadAssignment.razor.css` - Styling (600+ lines)
