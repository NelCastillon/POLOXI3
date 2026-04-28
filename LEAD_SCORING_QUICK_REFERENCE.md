# Lead Scoring Page - Quick Reference Guide

## Access Points

### URL
```
https://localhost:7061/crm/leads/scoring
```

### Navigation
From the sidebar: **CRM > Lead Scoring** (icon: 🔥)

## Component Files
- **Razor Component**: `src/Ams.Web/Components/Pages/Crm/LeadScoring.razor`
- **Stylesheet**: `src/Ams.Web/Components/Pages/Crm/LeadScoring.razor.css`

## Three Main Views

### 1. Rules View
**Purpose**: Manage scoring rules that calculate lead scores

**Features**:
- View all active/inactive scoring rules
- Create new rule: `_showRuleDrawer = true`
- Edit rule: `EditRule(rule)`
- Delete rule: `DeleteRule(ruleId)`
- Toggle active status: `ToggleRuleActive(rule, isActive)`

**Add Rule Button**: `+  Add Rule` (top-right)

### 2. Scores View
**Purpose**: View individual lead scores and their breakdown

**Features**:
- Search leads by name or company
- Filter by score range (80+, 50-79, 0-49)
- Filter by lead source (Website, Email, Referral, etc.)
- View score breakdown (E, P, B, R)
- Quick link to full lead record

**Score Breakdown**:
- E = Engagement Score
- P = Profile Score
- B = Behavior Score
- R = Recency Score

### 3. Analytics View
**Purpose**: Analyze scoring effectiveness and performance

**Sections**:
1. **Score Distribution** - How many leads in each score range
2. **Top Sources** - Which sources produce highest scoring leads
3. **Rule Effectiveness** - Which rules are most valuable

## Key Data Models

### LeadScoreRow
```csharp
public Guid LeadId { get; set; }
public string Name { get; set; }
public string Company { get; set; }
public string Source { get; set; }
public int Score { get; set; }           // 0-100
public int EngagementScore { get; set; }
public int ProfileScore { get; set; }
public int BehaviorScore { get; set; }
public int RecencyScore { get; set; }
```

### ScoringRuleRow
```csharp
public Guid Id { get; set; }
public string Name { get; set; }
public string Type { get; set; }         // Engagement, Profile, Behavior, Recency, Activity
public int Points { get; set; }          // 0-100
public string Description { get; set; }
public string Condition { get; set; }
public bool IsActive { get; set; }
```

## Score Ranges & Ratings

| Range | Rating | Icon | Color | Meaning |
|-------|--------|------|-------|---------|
| 80-100 | Hot | 🔥 | Red (#dc2626) | High priority leads |
| 50-79 | Warm | 🌡️ | Orange (#f59e0b) | Medium priority |
| 0-49 | Cold | ❄️ | Gray (#64748b) | Low priority |

## KPI Metrics Displayed

```
┌─────────────────────┬──────────────┬──────────────┬──────────────┬──────────────┐
│  Total Leads Scored │  Hot Leads   │  Warm Leads  │  Cold Leads  │ Average Score│
│      (count)        │   (80+)      │  (50-79)     │    (<50)     │   (0-100)    │
└─────────────────────┴──────────────┴──────────────┴──────────────┴──────────────┘
```

## Common Tasks

### Add a New Scoring Rule
1. Click **+ Add Rule** button
2. Fill in drawer form:
   - Rule Name (required)
   - Rule Type dropdown (required)
   - Points to Award (required, 0-100)
   - Description (optional)
   - Condition details (optional)
3. Click Confirm to save

### View Lead Scores
1. Navigate to **Scores** view
2. Use search/filters to find leads
3. See score breakdown in "Breakdown" column
4. Click arrow icon to view full lead record

### Analyze Effectiveness
1. Navigate to **Analytics** view
2. Review score distribution chart
3. Check which sources produce best leads
4. See which rules are most effective

### Deactivate a Rule
1. In **Rules** view, find the rule
2. Toggle the "Active" checkbox
3. Status saves immediately

## CSS Classes Reference

```css
.ls-rating--hot          /* Hot lead badge styling */
.ls-rating--warm         /* Warm lead badge styling */
.ls-rating--cold         /* Cold lead badge styling */
.ls-score-bar            /* Score progress bar */
.ls-breakdown-item       /* Individual score component */
.ls-badge-website        /* Source badge for website */
.ls-badge-email          /* Source badge for email */
.ls-badge-referral       /* Source badge for referral */
.ls-badge-call           /* Source badge for cold call */
.ls-badge-event          /* Source badge for event */
.ls-badge-social         /* Source badge for social */
```

## API Integration Points

Replace mock data by updating these methods:

### LoadAsync()
```csharp
private async Task LoadAsync()
{
    var rulesResponse = await Api.GetScoringRulesAsync(_tenantId);
    var leadsResponse = await Api.GetLeadScoresAsync(_tenantId);
    // ... map and populate data
}
```

### SaveRuleAsync()
```csharp
private async Task SaveRuleAsync()
{
    if (_editingRuleId == Guid.Empty)
    {
        await Api.CreateScoringRuleAsync(_tenantId, _ruleForm);
    }
    else
    {
        await Api.UpdateScoringRuleAsync(_tenantId, _editingRuleId, _ruleForm);
    }
}
```

### DeleteRule()
```csharp
private async Task DeleteRule(Guid ruleId)
{
    await Api.DeleteScoringRuleAsync(_tenantId, ruleId);
}
```

## Troubleshooting

### Rules not showing
- Verify `_rules` list is populated in `LoadAsync()`
- Check if rules are filtered by `_filterCat` or `_search`
- Ensure mock data is being called

### Scores not calculating
- Verify score components sum to displayed score
- Check `LeadScoreRow` properties are populated correctly
- Ensure score is between 0-100

### Grid not displaying
- Check if data source is bound correctly: `DataSource="_filteredLeads"`
- Verify grid columns match data model properties
- Check SfGrid reference is not null

## Performance Tips

1. **Pagination**: Grid shows 25 items per page by default
2. **Filtering**: Applied client-side, consider server-side for 1000+ leads
3. **Sorting**: SfGrid supports multi-column sorting
4. **Caching**: Consider caching scoring rules (change infrequently)

## Future Enhancements

- [ ] Export leads to CSV
- [ ] Bulk rule updates
- [ ] Score recalculation scheduler
- [ ] Historical score tracking
- [ ] Score change alerts
- [ ] A/B test different rules
- [ ] Machine learning score suggestions
- [ ] Competitor analysis scoring

---
**Last Updated**: 2024  
**Component Version**: 1.0  
**Framework**: Blazor Server (.NET 9)
