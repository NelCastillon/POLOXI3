# API Integration Patterns - CRM Pages

## Overview
This document provides guidance on how the three CRM pages have been updated to use real database APIs instead of mock data.

---

## Standard API Integration Pattern

### 1. Dependency Injection
```csharp
@inject ApiClient Api
@inject NavigationManager Nav
@inject BreadcrumbService Breadcrumbs
```

### 2. Tenant Context
```csharp
private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
```

### 3. Loading Data Pattern
```csharp
private async Task LoadAsync()
{
    _loading = true;
    try
    {
        // Call API
        var result = await Api.SomeMethodAsync(_tenantId);

        // Map to local data structure
        if (result?.Items != null)
        {
            _localData = result.Items
                .Select(dto => new LocalModel { ... })
                .ToList();
        }

        // Update KPI/calculations
        UpdateKpi();
    }
    catch (Exception ex)
    {
        await ShowToastAsync($"Error: {ex.Message}", "e-toast-danger");
    }
    finally
    {
        _loading = false;
    }
}
```

---

## Specific Implementation Examples

### Lead Scoring Page Pattern

#### Data Loading
```csharp
private async Task LoadAsync()
{
    // Load Leads with Scores
    var leadsResult = await Api.SearchLeadsAsync(_tenantId);

    if (leadsResult?.Items != null)
    {
        _leads = leadsResult.Items
            .Select(l => new LeadScoreRow
            {
                LeadId = l.LeadId,
                Name = $"{l.FirstName} {l.LastName}",
                Company = l.AccountName ?? "Unknown",
                Source = l.SourceCode ?? "Direct",
                Score = l.Score ?? 0,
                EngagementScore = (l.Score ?? 0) > 0 ? Math.Max((l.Score.Value / 4), 0) : 0,
                // ... other score components
            })
            .ToList();
    }
}
```

#### DTO to ViewModel Mapping
```
LeadDto Properties               →  LeadScoreRow
┌─────────────────────┐           ┌──────────────────┐
│ LeadId              │  ────────→ │ LeadId           │
│ FirstName + LastName│  ────────→ │ Name             │
│ AccountName         │  ────────→ │ Company          │
│ SourceCode          │  ────────→ │ Source           │
│ Score               │  ────────→ │ Score            │
│                     │            │ EngagementScore  │
│                     │            │ ProfileScore     │
│                     │            │ BehaviorScore    │
│                     │            │ RecencyScore     │
└─────────────────────┘           └──────────────────┘
```

---

### Lead Assignment Page Pattern

#### Multi-Source Data Loading
```csharp
private async Task LoadAsync()
{
    // Load Leads
    var leadsResult = await Api.SearchLeadsAsync(_tenantId);
    if (leadsResult?.Items != null)
    {
        _leads = leadsResult.Items
            .Where(l => l.AssignedToUserId == null)  // Filter unassigned
            .Select(l => new LeadRow(...))
            .ToList();
    }

    // Load Producers/Users
    var usersResult = await Api.SearchUsersAsync(_tenantId);
    if (usersResult?.Items != null)
    {
        _producers = usersResult.Items
            .Where(u => u.StatusCode == "Active")  // Filter active
            .Select(u => new ProducerRow(...))
            .ToList();
    }
}
```

#### Filtering Pattern
```csharp
// Filter unassigned leads
_leads = leadsResult.Items
    .Where(l => l.AssignedToUserId == null)
    .ToList();

// Filter active users
_producers = usersResult.Items
    .Where(u => u.StatusCode == "Active")
    .ToList();
```

#### Action Pattern (with API placeholder)
```csharp
private async Task AssignLead()
{
    try
    {
        // TODO: Call API when ready
        // var request = new AssignLeadRequest { ... };
        // await Api.AssignLeadAsync(request);

        // For now: Update local state
        _leads.Remove(_selectedLead);
        _history.Insert(0, new HistoryRow {...});

        UpdateKpi();
        _drawerOpen = false;
        await ShowToast("success", "Lead Assigned", "...");
    }
    catch (Exception ex)
    {
        await ShowToast("error", "Assignment Failed", ex.Message);
    }
}
```

---

### Lead Follow-up Page Pattern

#### Simple List with Dropdown
```csharp
private async Task LoadAsync()
{
    // Load leads for dropdown (lightweight)
    var leadsResult = await Api.SearchLeadsAsync(_tenantId);

    if (leadsResult?.Items != null)
    {
        _leadOptions = leadsResult.Items
            .Select(l => new LeadOption(
                (int)l.LeadId.GetHashCode(),
                $"{l.FirstName} {l.LastName}",
                l.AccountName ?? "Unknown"
            ))
            .ToList();
    }

    // TODO: Load follow-ups from dedicated API
    _allFollowUps = [];  // Empty for now
}
```

---

## Error Handling Pattern

### Standard Try-Catch-Finally
```csharp
private async Task LoadAsync()
{
    _loading = true;
    try
    {
        // API calls here
        var result = await Api.SearchLeadsAsync(_tenantId);
        // Process result
    }
    catch (Exception ex)
    {
        // User-friendly error message
        await ShowToast("error", "Failed to load data", ex.Message);
    }
    finally
    {
        _loading = false;  // Always stop loading
    }
}
```

### Toast Notification Pattern
```csharp
await ShowToast("success", "Title", "Message");    // Success
await ShowToast("warning", "Title", "Message");    // Warning
await ShowToast("error", "Title", "Message");      // Error
await ShowToastAsync($"Error: {ex.Message}", "e-toast-danger");  // Alternative
```

---

## Common DTOs Reference

### LeadDto Properties
```csharp
public Guid LeadId { get; set; }
public Guid TenantId { get; set; }
public string LeadNumber { get; set; }
public string? AccountName { get; set; }           // Company
public string FirstName { get; set; }
public string LastName { get; set; }
public string? Email { get; set; }
public string? Phone { get; set; }
public string? InterestedService { get; set; }
public int? Score { get; set; }                    // Lead score 0-100
public string? PriorityCode { get; set; }
public string? SourceCode { get; set; }            // Lead source
public string? NurturingStageCode { get; set; }
public DateTime? QualifiedDate { get; set; }
public int StatusCode { get; set; }
public Guid? AssignedToUserId { get; set; }        // For filtering
public DateTime CreatedDateUtc { get; set; }
```

### UserDto Properties
```csharp
public Guid UserId { get; set; }
public Guid TenantId { get; set; }
public Guid? BranchId { get; set; }
public string? UserNumber { get; set; }
public string UserName { get; set; }
public string Email { get; set; }
public string FullName { get; set; }               // User name
public string? DisplayName { get; set; }
public string UserTypeCode { get; set; }
public string StatusCode { get; set; }             // For filtering
public string? Region { get; set; }
public bool MfaEnabled { get; set; }
public DateTime? LastLoginDateUtc { get; set; }
public string? PhoneNumber { get; set; }
public string? TimeZoneCode { get; set; }
public string? LocaleCode { get; set; }
public string? Department { get; set; }
public string? JobTitle { get; set; }              // Producer title
public DateTime? PasswordChangedDateUtc { get; set; }
public bool IsLockedOut { get; set; }
public DateTime? LockoutEndDateUtc { get; set; }
public int FailedLoginAttempts { get; set; }
public int AssignedRoleCount { get; set; }
public DateTime CreatedDateUtc { get; set; }
public DateTime? ModifiedDateUtc { get; set; }
```

### PagedResult<T> Structure
```csharp
public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}
```

---

## Best Practices Implemented

✅ **Null Safety**
```csharp
if (leadsResult?.Items != null)  // Check both result and Items
```

✅ **Default Values**
```csharp
l.AccountName ?? "Unknown"       // Fallback for null values
l.Score ?? 0                     // Numeric defaults
```

✅ **LINQ Filtering**
```csharp
.Where(l => l.AssignedToUserId == null)  // Client-side filtering
.Where(u => u.StatusCode == "Active")
```

✅ **DTO to ViewModel Mapping**
```csharp
.Select(l => new LeadScoreRow
{
    LeadId = l.LeadId,
    Name = $"{l.FirstName} {l.LastName}",
    // ... map all properties
})
```

✅ **Loading State Management**
```csharp
_loading = true;   // Before API call
// ... API call
_loading = false;  // In finally block
```

✅ **Error Recovery**
```csharp
try { ... }
catch (Exception ex) { /* log and show error */ }
finally { _loading = false; }
```

---

## Migration Checklist

When creating new API endpoints:

- [ ] Add method to `ApiClient.cs`
- [ ] Create Request/Response DTOs if needed
- [ ] Update page's `LoadAsync()` method
- [ ] Add error handling
- [ ] Test with real data
- [ ] Update toast messages
- [ ] Remove TODO comments
- [ ] Update KPI calculations if needed
- [ ] Test filtering and pagination
- [ ] Performance test with large datasets

---

## Example: Adding a New API Endpoint

### 1. Add to ApiClient
```csharp
public Task<PagedResult<ScoringRuleDto>?> SearchScoringRulesAsync(
    Guid tenantId, 
    CancellationToken cancellationToken = default)
    => _httpClient.GetFromJsonAsync<PagedResult<ScoringRuleDto>>(
        $"api/crm/scoring-rules?tenantId={tenantId}", 
        cancellationToken);
```

### 2. Update Page LoadAsync
```csharp
var rulesResult = await Api.SearchScoringRulesAsync(_tenantId);
if (rulesResult?.Items != null)
{
    _rules = rulesResult.Items
        .Select(r => new ScoringRuleRow 
        { 
            Id = r.Id,
            Name = r.Name,
            // ...
        })
        .ToList();
}
```

### 3. Remove TODO Comment
Replace:
```csharp
// TODO: Load scoring rules from API
_rules = [];
```

With:
```csharp
// Scoring rules loaded from API
```

---

## Performance Considerations

### Pagination
```csharp
// API supports pagination - adjust page size as needed
public Task<PagedResult<T>> SearchAsync(
    Guid tenantId, 
    int pageNumber = 1, 
    int pageSize = 25)
```

### Filtering
- Implement server-side filtering when dataset is large
- Use API query parameters: `?statusCode=Active&tenantId=...`
- Avoid loading all records for client-side filtering

### Caching
- Consider caching producer/user lists if they change infrequently
- Cache scoring rules if immutable during session
- Invalidate cache on create/update/delete operations

---

## Security Notes

- All API calls include `TenantId` parameter (tenant isolation)
- User context should validate lead ownership
- Assignment operations should validate producer access
- Consider role-based authorization for rule management

---

## Related Documentation

- API Docs: See `Ams.Api/Program.cs` for endpoint configuration
- DTOs: `Ams.Application/Common/Dtos/`
- Requests: `Ams.Application/Features/*/`
- Models: `Ams.Application/Common/Models/`
