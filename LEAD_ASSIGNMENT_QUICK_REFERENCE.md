# Lead Assignment — Quick Reference

## Routes & Navigation
- **URL**: `https://localhost:7061/crm/leads/assignment`
- **Menu Path**: CRM & Demand → Lead Management → Lead Assignment
- **Icon**: `bi-person-check`
- **Breadcrumb**: CRM → Leads → Assignment

## Views

| View | Purpose | Key Features |
|------|---------|--------------|
| **Leads** | Manage unassigned leads | Search, filter, select, assign individually |
| **Workload** | Producer capacity view | Capacity bars, stats, edit capacity |
| **Rules** | Assignment automation | Create, edit, toggle, delete rules |
| **History** | Assignment audit trail | Timeline, methods, period filter |

## Key Metrics (KPI)

| Metric | Demo Value | Meaning |
|--------|-----------|---------|
| Unassigned Leads | 15 | Leads awaiting assignment |
| Active Producers | 5 | Available producers |
| Avg Leads/Producer | 3.0 | Average workload per producer |
| Workload Balance | 74% | Capacity utilization percentage |

## Mock Data

### Leads (15 total)
- Names: Sarah Anderson, Michael Chen, Jennifer Martinez, David Thompson, Emily Watson, Robert Jackson, Amanda Price, James Mitchell, Lisa Graham, Christopher Davis, Michelle Brown, Kevin Wilson, Rachel Santos, Marcus Taylor, Victoria Kim
- Scores: 62-91 (Hot ≥80, Warm 50-79, Cold <50)
- Sources: Web, Referral, Direct, Partner, Organic

### Producers (5 total)
- John Spencer (18/25 leads) - Senior Producer
- Amanda Hayes (12/20 leads) - Producer
- Ryan Mitchell (21/20 leads) - Account Executive (over capacity)
- Jessica Brown (8/20 leads) - Producer
- Thomas Anderson (24/25 leads) - Senior Producer

### Rules (3 total)
1. **High-Score Auto Assign** (Score-Based, Active)
2. **Round-Robin Distribution** (Round-Robin, Active)
3. **Territory Routing** (Territory-Based, Inactive)

### History (5 records)
- Recent assignments showing Manual, Score-Based, and Round-Robin methods

## Common Tasks

### Assign a Single Lead
1. Switch to **Leads** view
2. Find lead in grid
3. Click the arrow icon → Drawer opens on right
4. Select producer from dropdown
5. (Optional) Add notes
6. Click "Assign Lead"

### Bulk Assign Leads
1. In **Leads** view, check multiple leads
2. Click "Assign [N]" button
3. Choose strategy: Manual / Round-Robin / Score-Based / Territory-Based
4. If Manual: select single producer; If Round-Robin: select group
5. Click "Assign [N] Leads"

### Create Assignment Rule
1. Switch to **Rules** view
2. Click "New Rule"
3. Define: Name, Strategy, Criteria, Target Group, Max Assignments
4. Save rule
5. Rule becomes available for automated assignments

### View Workload
1. Switch to **Workload** view
2. See all producers with capacity bars
3. Red bar = over capacity, Yellow = high, Green = low
4. Click "Capacity" button to adjust max assignments

### Check Assignment History
1. Switch to **History** view
2. Filter by time period (optional)
3. View timeline of all assignments
4. See assignment method for each operation

## UI Elements

### Buttons
- **View Toggle**: Switch between Leads, Workload, Rules, History
- **Refresh**: Reload data
- **Assign [N]**: Bulk assign selected leads (appears when leads selected)
- **New Rule**: Create assignment rule
- **Edit/Delete**: Rule management
- **Cancel/Confirm**: Drawer operations

### Filters (Leads View)
- **Search**: By lead name or company
- **Source**: Web, Referral, Direct, Partner, Organic
- **Score Range**: 80+, 50-79, <50

### Checkboxes
- **Select All**: Top left checkbox in leads grid
- **Individual**: Each row has own checkbox

### Drawers
- **Assign Lead**: Right-side panel for single assignment
- **Bulk Assign**: Right-side panel for batch operations

## Status Colors

### Lead Score Badges
- **Red** (≥80): Hot lead
- **Yellow** (50-79): Warm lead
- **Gray** (<50): Cold lead

### Workload Status
- **Green**: Low workload (<50%)
- **Yellow**: Medium (50-80%)
- **Red**: High (80%+)
- **Gray**: Over capacity (100%+)

### Source Badge
- **Blue background**: Source type indicator

## Code Locations

| Component | File |
|-----------|------|
| View Component | `src/Ams.Web/Components/Pages/Crm/LeadAssignment.razor` |
| Styling | `src/Ams.Web/Components/Pages/Crm/LeadAssignment.razor.css` |
| Navigation | `src/Ams.Web/Components/Layout/NavSidebar.razor` |

## Data Types

```csharp
// Lead to assign
record LeadRow(int Id, string Name, string Company, int Score, 
               string Source, string Email, DateTime CreatedDate);

// Producer target
record ProducerRow(int Id, string Name, string Title, 
                   int LeadCount, int Capacity);

// Assignment rule
record RuleRow(int Id, string Name, string Strategy, string Criteria, 
               string ProducerGroup, int MaxAssignments, bool Active);

// Assignment record
record HistoryRow(int Id, string LeadName, string ProducerName, 
                  DateTime AssignedDate, string Method);
```

## API Integration Points

To connect to real backend, update:

1. **LoadAsync()** - Replace mock data with API call
   ```csharp
   var leads = await Api.GetUnassignedLeads();
   var producers = await Api.GetProducers();
   var rules = await Api.GetAssignmentRules();
   var history = await Api.GetAssignmentHistory();
   ```

2. **AssignLead()** - POST single assignment
   ```csharp
   await Api.PostAssignment(leadId, producerId, notes);
   ```

3. **BulkAssignLeads()** - POST bulk assignment
   ```csharp
   await Api.PostBulkAssignment(leadIds, producerId, strategy);
   ```

4. **DeleteRule()** - DELETE rule
   ```csharp
   await Api.DeleteRule(ruleId);
   ```

5. **ToggleRuleActive()** - PATCH rule status
   ```csharp
   await Api.PatchRuleStatus(ruleId, active);
   ```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Page not found | Verify route `/crm/leads/assignment` in NavSidebar |
| No leads display | Check BuildMockData() is called in LoadAsync() |
| Drawer stuck open | Click overlay to close or X button |
| Filters not working | Verify GetVisibleLeads() filter logic |
| Notifications missing | Check _toast ref is not null before calling ShowToast() |

## Responsive Design

| Breakpoint | Adjustment |
|-----------|-----------|
| **≤768px** | Drawer full-width, single column grids |
| **≤600px** | KPI cards stack 2-per-row, drawer footer buttons stack |

## Performance Notes

- Grid displays 15 leads (pageable via enterprise CSS grid)
- All filtering is client-side (suitable for <1000 records)
- Mock data loads in 300ms (simulated API delay)
- For large datasets, implement server-side filtering

## Future API Endpoints (Suggested)

```
GET    /api/crm/leads/unassigned
GET    /api/crm/producers
GET    /api/crm/assignment-rules
GET    /api/crm/assignment-history
POST   /api/crm/assignments
POST   /api/crm/assignments/bulk
DELETE /api/crm/assignment-rules/{id}
PATCH  /api/crm/assignment-rules/{id}/status
PATCH  /api/crm/producers/{id}/capacity
```

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial implementation with 4 views, mock data, all CRUD operations |

## Related Pages

- **Leads** (`/crm/leads`) - All leads with ratings and conversion data
- **Lead Scoring** (`/crm/leads/scoring`) - Score configuration and analytics
- **Forecast** (`/crm/forecast`) - Pipeline forecasting
- **Opportunities** (`/crm/opportunities`) - Sales pipeline

## Support

For issues or questions, check:
1. Build log for compilation errors
2. Browser console for runtime errors
3. Mock data in BuildMockData() for data structure
4. CSS classes in LeadAssignment.razor.css for styling
