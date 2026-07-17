# CRM Pages - Quick Reference Guide

## 📍 New Pages Added

### 1. Segmentation Rules (`/crm/segments/rules`)
**File:** `src/Ams.Web/Components/Pages/Crm/SegmentationRules.razor`

**Key Classes/Records:**
- `SegmentationRuleModel` - Main data model
- `SegmentationRuleForm` - Form for create/edit
- `SegmentationCriterion` - Individual criteria in a rule

**Main Methods:**
```csharp
private async Task LoadAsync()           // Load rules from API
private List<SegmentationRuleModel> GetFilteredRules()  // Apply filters
private void OpenCreateRule()            // Show create dialog
private void EditRule(rule)              // Show edit dialog
private async Task SaveRuleAsync()       // Save rule to API
private async Task DeleteRule(rule)      // Delete rule
private void AddCriterion()              // Add scoring criterion
private void RemoveCriterion(criterion)  // Remove criterion
```

**State Variables:**
- `_rules` - List of all rules
- `_search` - Search term
- `_filterSegment` - Segment filter
- `_filterStatus` - Active/Inactive filter
- `_showDialog` - Dialog visibility
- `_formData` - Form model
- `_editingRule` - Currently editing rule

---

### 2. Data Enrichment (`/crm/enrichment`)
**File:** `src/Ams.Web/Components/Pages/Crm/DataEnrichment.razor`

**Key Classes/Records:**
- `DataProviderModel` - Provider info and connection status
- `EnrichmentJobModel` - Job execution tracking
- `ProviderConfigModel` - Provider API configuration

**Main Methods:**
```csharp
private async Task LoadAsync()              // Load providers and jobs
private List<DataProviderModel> BuildStubProviders()  // Initialize providers
private List<EnrichmentJobModel> BuildStubJobs()      // Initialize jobs
private void ConfigureProvider(provider)    // Show config dialog
private void ConnectProvider(provider)      // Connect new provider
private void DisconnectProvider(provider)   // Disconnect provider
private async Task SaveProviderConfigAsync()         // Save provider config
private async Task RunEnrichmentAsync()     // Execute enrichment job
private void ViewJobDetails(job)            // Navigate to job details
```

**State Variables:**
- `_providers` - Available data providers
- `_jobs` - Enrichment job history
- `_activeTab` - "providers" or "jobs"
- `_selectedProvider` - Selected for config
- `_providerConfig` - Provider configuration
- `_showConfigDialog` - Dialog visibility

---

## 🎯 Component Patterns

### KPI Strip
```razor
<div class="sr-kpi-strip">
    <div class="sr-kpi-card">
        <span class="sr-ki sr-ki1"><i class="bi bi-icon" aria-hidden="true"></i></span>
        <div>
            <div class="sr-kv">@value</div>
            <div class="sr-kl">Label</div>
        </div>
    </div>
</div>
```

### Filter Bar
```razor
<div class="sr-filter-bar">
    <div class="sr-search-wrap">
        <i class="bi bi-search sr-si" aria-hidden="true"></i>
        <input class="sr-search" placeholder="Search…"
               @oninput="@((Microsoft.AspNetCore.Components.ChangeEventArgs e) => _search = e.Value?.ToString() ?? string.Empty)" />
    </div>
    <select class="form-select" @bind="_selectedValue"
                    ShowClearButton="true" Width="160px"
                    Value="_filter" ValueChanged="@((string v) => _filter = v)">
        <DropDownListFieldSettings Value="Value" Text="Label" />
    </select>
</div>
```

### Dialog Form
```razor
<div class="um-modal" role="dialog">
    <DialogTemplates>
        <Header><span class="sr-dlg-hdr"><i class="bi bi-icon"></i> Title</span></Header>
        <Content>
            <div class="sr-dlg-body">
                <div class="sr-dlg-section">Section Title</div>
                <!-- Form content -->
            </div>
        </Content>
        <FooterTemplate>
            <div class="sr-dlg-footer">
                <button class="um-btn um-btn-ghost" @onclick="CloseDialog">Cancel</button>
                <button class="um-btn um-btn-primary" @onclick="SaveAsync">Save</button>
            </div>
        </FooterTemplate>
    </DialogTemplates>
</div>
```

### Grid with Filtering
```razor
<div class="app-datagrid sr-grid">
    <AppGrid TValue="Model" DataSource="GetFilteredData()" AllowPaging="true" AllowSorting="true">
        <GridPageSettings PageSize="15" />
        <GridColumns>
            <GridColumn Field="@nameof(Model.Name)" HeaderText="Name" MinWidth="180" />
            <GridColumn Field="@nameof(Model.Status)" HeaderText="Status" Width="90">
                <Template>
                    @{ var item = (Model)context; }
                    <span class="@StatusBadge(item.Status)">@item.Status</span>
                </Template>
            </GridColumn>
        </GridColumns>
    </AppGrid>
</div>
```

---

## 🔌 API Integration Points

### Segmentation Rules API
```csharp
// To implement when backend is ready:
GET    /api/crm/segmentation-rules
POST   /api/crm/segmentation-rules
GET    /api/crm/segmentation-rules/{id}
PUT    /api/crm/segmentation-rules/{id}
DELETE /api/crm/segmentation-rules/{id}
POST   /api/crm/segmentation-rules/{id}/execute
```

**Replace this:**
```csharp
_rules = BuildStubRules();

// With this:
var result = await HttpClient.GetFromJsonAsync<List<SegmentationRuleModel>>("api/crm/segmentation-rules");
_rules = result ?? [];
```

### Data Enrichment API
```csharp
// To implement when backend is ready:
GET    /api/crm/enrichment/providers
POST   /api/crm/enrichment/providers/{name}/connect
GET    /api/crm/enrichment/jobs
POST   /api/crm/enrichment/jobs/run
```

**Replace this:**
```csharp
_providers = BuildStubProviders();
_jobs = BuildStubJobs();

// With this:
var providers = await HttpClient.GetFromJsonAsync<List<DataProviderModel>>("api/crm/enrichment/providers");
var jobs = await HttpClient.GetFromJsonAsync<List<EnrichmentJobModel>>("api/crm/enrichment/jobs");
```

---

## 🎨 CSS Classes Reference

### Segmentation Rules CSS Prefix: `sr-`

| Class | Purpose |
|-------|---------|
| `sr-kpi-strip` | KPI card container grid |
| `sr-kpi-card` | Individual KPI card |
| `sr-ki` `sr-ki1/2/3/4` | KPI icons with colors |
| `sr-filter-bar` | Filter controls container |
| `sr-search-wrap` | Search input wrapper |
| `sr-search` | Search input |
| `sr-grid` | Data grid container |
| `sr-segment-badge` | Segment name badge |
| `sr-accuracy-high/med/low` | Accuracy color coding |
| `sr-dlg-hdr` | Dialog header styling |
| `sr-dlg-body` | Dialog body styling |
| `sr-dlg-section` | Section heading in dialog |

### Data Enrichment CSS Prefix: `de-`

| Class | Purpose |
|-------|---------|
| `de-kpi-strip` | KPI card container |
| `de-entity-tabs` | Tab navigation |
| `de-entity-tab--active` | Active tab styling |
| `de-providers-grid` | Provider cards grid |
| `de-provider-card` | Individual provider card |
| `de-provider-card--active` | Connected provider styling |
| `de-field-tags` | Field display tags |
| `de-status-completed/progress/failed` | Job status colors |
| `de-success-high/med/low` | Success rate colors |

---

## 📐 Layout Patterns

### Responsive Grid
```css
.sr-kpi-strip {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 1rem;
}
```

### Provider Cards Grid
```css
.de-providers-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    gap: 1.5rem;
}
```

### Criterion Row Grid
```css
.sr-criterion-row {
    display: grid;
    grid-template-columns: 140px 110px 1fr 80px 40px;
    gap: 0.5rem;
    align-items: center;
}
```

---

## 🔐 Breadcrumb Integration

```csharp
protected override async Task OnInitializedAsync()
{
    Breadcrumbs.SetCrumbs(
        new("Home", "/"),
        new("CRM", "/crm/opportunities"),
        new("Segments", "/crm/segments"),
        new("Rules", "/crm/segments/rules")
    );
    await LoadAsync();
}
```

**RecordType:**
```csharp
record BreadcrumbItem(string Label, string? Url = null, string? Icon = null);
```

---

## 🧪 Testing Data

### Stub Providers (Data Enrichment)
```csharp
new() {
    Name = "ZoomInfo",
    Description = "Real-time B2B database",
    Icon = "bi-globe",
    IsConnected = true,
    AvailableFields = new() { "Company Size", "Industry", "Revenue", ... }
}
```

### Stub Rules (Segmentation)
```csharp
new() {
    RuleId = Guid.NewGuid(),
    RuleName = "VIP Enterprise Clients",
    Segment = "VIP Tier",
    Criteria = "Annual Revenue > $10M AND Employees > 500",
    AccountsMatched = 247,
    Accuracy = 0.94,
    IsActive = true
}
```

---

## ⚠️ Common Gotchas

1. **ChangeEventArgs Ambiguity**
   - Use fully qualified: `Microsoft.AspNetCore.Components.ChangeEventArgs`
   - The previous third-party component model also had a `ChangeEventArgs`

2. **HttpClient Injection**
   - Current pages inject `HttpClient` directly
   - Don't access private ApiClient._httpClient field (it's private!)

3. **Form State Management**
   - Remember to clear form data when closing dialog
   - Set `_editingRule = null` for create vs edit logic

4. **Null Safety**
   - Use `?.` operator for optional data
   - Check `_selectedProvider != null` in dialog

5. **Grid Filtering**
   - Implement GetFilteredData() to apply client-side filters
   - Consider pagination performance for large datasets

---

## 🚀 Getting Started Checklist

- [x] Pages created and compiling
- [x] Navigation integrated
- [x] Styling complete
- [x] Stub data working
- [ ] Create API controllers
- [ ] Create service layer
- [ ] Create database schema
- [ ] Connect UI to APIs
- [ ] Add error handling
- [ ] Implement authorization
- [ ] Add unit tests
- [ ] Deploy to production

---

## 📞 Troubleshooting

### Build Fails with "ChangeEventArgs is ambiguous"
**Solution:** Use fully qualified type name:
```csharp
@oninput="@((Microsoft.AspNetCore.Components.ChangeEventArgs e) => ...)"
```

### Grid not showing data
**Solution:** Ensure DataSource is not null and GetFilteredData() returns data:
```csharp
<AppGrid TValue="Model" DataSource="GetFilteredRules()" ...>
```

### Dialog won't close
**Solution:** Call CloseDialog() which sets _showDialog = false:
```csharp
private void CloseDialog()
{
    _showDialog = false;
    _editingRule = null;
    _formData = new();
}
```

### Toast not showing
**Solution:** Check if _toast is not null before calling:
```csharp
if (_toast is not null)
    await _toast.ShowAsync(new ToastModel { Content = message });
```

---

## 🔗 Related Files

- **Navigation:** `src/Ams.Web/Components/Layout/NavSidebar.razor`
- **Reference Page:** `src/Ams.Web/Components/Pages/Crm/CustomerSegmentation.razor`
- **Reference Page:** `src/Ams.Web/Components/Pages/Crm/DuplicateManagement.razor`
- **Documentation:** `CRM_PAGES_IMPLEMENTATION_GUIDE.md`
- **Summary:** `CRM_IMPLEMENTATION_SUMMARY.md`

---

**Last Updated:** January 15, 2025
**Status:** ✅ Frontend Complete - Ready for Backend
