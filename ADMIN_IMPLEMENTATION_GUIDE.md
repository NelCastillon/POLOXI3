# Admin & Agency Pages - Complete Modernization Implementation Guide

## 📋 Overview

This guide provides step-by-step instructions to complete the modernization of all admin and agency pages in the AMS Blazor application. The framework has been created with professional styling, services, repositories, and API controllers ready for full integration.

---

## ✅ What's Already Completed

### 1. **Professional CSS Framework** 
- File: `src/Ams.Web/Css/admin-professional.css`
- Features:
  - Complete design system with CSS variables
  - KPI card components with interactive states
  - Modern data tables with hover effects
  - Modal dialogs and form styling
  - Responsive grid layouts
  - Utility classes for spacing, colors, and alignment
  - Accessibility features (WCAG AA compliant)

### 2. **Service & Repository Layer**
- DTOs: `src/Ams.Application/Common/Dtos/AdminPagesDto.cs`
  - BusinessRuleDto
  - DepartmentTeamDto
  - ProducerStaffDto
  - SystemSettingsDto
  - NotificationPolicyDto
  - QueueRoutingRuleDto
  - DataQualityRuleDto
  - DataCenterConfigDto
  - SlaPolicySetupDto

- Interfaces: 
  - `src/Ams.Application/Abstractions/Services/IAdminPagesServices.cs`
  - `src/Ams.Application/Abstractions/Persistence/IAdminPagesRepositories.cs`

- Implementations:
  - `src/Ams.Application/AdminPagesService.cs` (Unified service)
  - `src/Ams.Infrastructure/Persistence/Repositories/AdminRepositories.cs` (In-memory repositories)

### 3. **API Controllers**
- File: `src/Ams.Api/Controllers/Admin/AdminPagesControllers.cs`
- Controllers:
  - AdminBusinessRulesController
  - AdminTeamsController
  - AdminStaffController
- Features:
  - Full CRUD endpoints
  - Tenant isolation
  - User context capture
  - Error handling
  - Logging

### 4. **Example Modern Page**
- File: `src/Ams.Web/Components/Pages/AdminBusinessRulesModern.razor`
- Demonstrates:
  - Professional UI with KPI strip
  - Interactive filtering
  - Data table with actions
  - Create/Edit modal dialog
  - Real-time search
  - Status badges with color coding

---

## 🚀 Next Steps - Implementation Checklist

### Step 1: Register Services in Dependency Injection (Program.cs)

Add to your `Program.cs` in the services configuration section:

```csharp
// Admin Pages Services
services.AddScoped<AdminPagesService>();

// Register as individual services
services.AddScoped<IBusinessRuleService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<IDepartmentTeamService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<IProducerStaffService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<ISystemSettingsService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<INotificationPolicyService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<IQueueRoutingService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<IDataQualityService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<IDataCenterService>(sp => sp.GetRequiredService<AdminPagesService>());
services.AddScoped<ISlaPolicyService>(sp => sp.GetRequiredService<AdminPagesService>());

// Admin Repositories
services.AddScoped<IBusinessRuleRepository, BusinessRuleRepository>();
services.AddScoped<IDepartmentTeamRepository, DepartmentTeamRepository>();
services.AddScoped<IProducerStaffRepository, ProducerStaffRepository>();
services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
services.AddScoped<INotificationPolicyRepository, NotificationPolicyRepository>();
services.AddScoped<IQueueRoutingRepository, QueueRoutingRepository>();
services.AddScoped<IDataQualityRepository, DataQualityRepository>();
services.AddScoped<IDataCenterRepository, DataCenterRepository>();
services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
```

### Step 2: Include CSS in Layout

Add to `src/Ams.Web/Components/App.razor` or your main layout:

```html
<link rel="stylesheet" href="css/admin-professional.css" />
```

### Step 3: Update ApiClient for Admin Endpoints

Add to `src/Ams.Web/Services/ApiClient.cs`:

```csharp
// Business Rules
public async Task<IReadOnlyList<BusinessRuleDto>?> GetBusinessRulesAsync(Guid tenantId, string? category = null)
    => await GetFromJsonAsync<IReadOnlyList<BusinessRuleDto>>($"api/admin/business-rules?tenantId={tenantId}&category={category}");

public async Task<BusinessRuleDto?> CreateBusinessRuleAsync(BusinessRuleDto rule, Guid tenantId)
    => await PostAsJsonAsync<BusinessRuleDto>("api/admin/business-rules", rule);

public async Task UpdateBusinessRuleAsync(BusinessRuleDto rule)
    => await PutAsJsonAsync($"api/admin/business-rules/{rule.BusinessRuleId}", rule);

public async Task DeleteBusinessRuleAsync(Guid ruleId)
    => await DeleteAsync($"api/admin/business-rules/{ruleId}");

public async Task ToggleBusinessRuleStatusAsync(Guid ruleId)
    => await PatchAsync($"api/admin/business-rules/{ruleId}/toggle-status", null);

// Teams
public async Task<IReadOnlyList<DepartmentTeamDto>?> GetTeamsAsync(Guid tenantId)
    => await GetFromJsonAsync<IReadOnlyList<DepartmentTeamDto>>($"api/admin/teams?tenantId={tenantId}");

public async Task<Guid> CreateTeamAsync(DepartmentTeamDto team, Guid tenantId)
    => await PostAsJsonAsync<Guid>("api/admin/teams", team);

// Staff
public async Task<IReadOnlyList<ProducerStaffDto>?> GetStaffAsync(Guid tenantId)
    => await GetFromJsonAsync<IReadOnlyList<ProducerStaffDto>>($"api/admin/staff?tenantId={tenantId}");

public async Task<IReadOnlyList<ProducerStaffDto>?> GetExpiringLicensesAsync(Guid tenantId, int days = 30)
    => await GetFromJsonAsync<IReadOnlyList<ProducerStaffDto>>($"api/admin/staff/expiring-licenses?tenantId={tenantId}&days={days}");

public async Task<Guid> CreateStaffAsync(ProducerStaffDto staff, Guid tenantId)
    => await PostAsJsonAsync<Guid>("api/admin/staff", staff);

// Add similar methods for other admin entities...
```

### Step 4: Create/Update Blazor Pages

For each admin page, use the pattern shown in `AdminBusinessRulesModern.razor`:

```razor
@page "/admin/section/page"
@using Ams.Application.Common.Dtos
@namespace Ams.Web.Components.Pages
@inject ApiClient Api

<!-- Professional header with title and actions -->
<div class="ap-page-header">
    <h1 class="ap-page-header__title">Page Title</h1>
    <div class="ap-page-header__actions">
        <button class="ap-btn ap-btn--primary" @onclick="OpenCreateModal">
            <i class="bi bi-plus-lg"></i> New Item
        </button>
    </div>
</div>

<!-- KPI Cards -->
<div class="ap-kpi-strip">
    <!-- KPI items here -->
</div>

<!-- Filter Bar -->
<div class="ap-filter-bar">
    <!-- Search and filters -->
</div>

<!-- Data Table -->
<div class="ap-table-wrapper">
    <table class="ap-table">
        <!-- Table content -->
    </table>
</div>

<!-- Modal for Create/Edit -->
<!-- Modal implementation -->

@code {
    // Component logic
}
```

### Step 5: Database Schema & Migration

Create migration `0044_Admin_Schema_Create` with these tables:

```sql
-- ============================================================
-- ADMIN SCHEMA - BUSINESS RULES
-- ============================================================
CREATE SCHEMA Admin;

CREATE TABLE Admin.BusinessRule (
    BusinessRuleId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    Description         NVARCHAR(1000)   NULL,
    Category            NVARCHAR(100)    NOT NULL,
    Trigger             NVARCHAR(200)    NOT NULL,
    [Condition]         NVARCHAR(2000)   NULL,
    [Action]            NVARCHAR(200)    NOT NULL,
    Priority            NVARCHAR(50)     NOT NULL DEFAULT 'Medium',
    Status              NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    IsSystemRule        BIT              NOT NULL DEFAULT 0,
    ExecutionOrder      INT              NOT NULL DEFAULT 0,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    ModifiedDateUtc     DATETIME2        NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

CREATE NONCLUSTERED INDEX IX_BusinessRule_TenantId ON Admin.BusinessRule(TenantId, Status, IsDeleted);
CREATE NONCLUSTERED INDEX IX_BusinessRule_Category ON Admin.BusinessRule(Category, Status, IsDeleted);

-- ============================================================
-- ADMIN SCHEMA - DEPARTMENTS & TEAMS
-- ============================================================
CREATE TABLE Admin.DepartmentTeam (
    TeamId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    TeamName            NVARCHAR(200)    NOT NULL,
    Description         NVARCHAR(500)    NULL,
    DepartmentId        UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

CREATE NONCLUSTERED INDEX IX_DepartmentTeam_TenantId ON Admin.DepartmentTeam(TenantId, IsActive, IsDeleted);

-- ============================================================
-- ADMIN SCHEMA - PRODUCER/STAFF
-- ============================================================
CREATE TABLE Admin.ProducerStaff (
    StaffId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    FirstName           NVARCHAR(100)    NOT NULL,
    LastName            NVARCHAR(100)    NOT NULL,
    Email               NVARCHAR(200)    NULL,
    Phone               NVARCHAR(50)     NULL,
    Role                NVARCHAR(100)    NOT NULL,
    NpnLicense          NVARCHAR(100)    NULL,
    LicenseExpiryDate   DATETIME2        NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

CREATE NONCLUSTERED INDEX IX_ProducerStaff_TenantId ON Admin.ProducerStaff(TenantId, Role, IsActive, IsDeleted);
CREATE NONCLUSTERED INDEX IX_ProducerStaff_LicenseExpiry ON Admin.ProducerStaff(LicenseExpiryDate) WHERE LicenseExpiryDate IS NOT NULL AND IsDeleted = 0;

-- ============================================================
-- ADMIN SCHEMA - SYSTEM SETTINGS
-- ============================================================
CREATE TABLE Admin.SystemSettings (
    SettingId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    SettingKey          NVARCHAR(200)    NOT NULL,
    SettingValue        NVARCHAR(MAX)    NOT NULL,
    Category            NVARCHAR(100)    NOT NULL,
    Description         NVARCHAR(500)    NULL,
    DataType            NVARCHAR(50)     NULL DEFAULT 'String',
    IsEncrypted         BIT              NOT NULL DEFAULT 0,
    ModifiedDateUtc     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    UNIQUE (TenantId, SettingKey, IsDeleted)
);

-- ============================================================
-- ADMIN SCHEMA - NOTIFICATION POLICIES
-- ============================================================
CREATE TABLE Admin.NotificationPolicy (
    PolicyId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    PolicyName          NVARCHAR(200)    NOT NULL,
    Description         NVARCHAR(1000)   NULL,
    TriggerEvent        NVARCHAR(200)    NOT NULL,
    NotificationChannels NVARCHAR(500)   NULL,
    Recipients          NVARCHAR(MAX)    NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- ADMIN SCHEMA - QUEUE ROUTING RULES
-- ============================================================
CREATE TABLE Admin.QueueRoutingRule (
    RuleId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    RoutingKey          NVARCHAR(200)    NOT NULL,
    SourceQueue         NVARCHAR(200)    NOT NULL,
    DestinationQueue    NVARCHAR(200)    NOT NULL,
    Priority            INT              NOT NULL DEFAULT 0,
    [Condition]         NVARCHAR(1000)   NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

CREATE NONCLUSTERED INDEX IX_QueueRoutingRule_TenantId ON Admin.QueueRoutingRule(TenantId, Priority DESC, IsActive, IsDeleted);

-- ============================================================
-- ADMIN SCHEMA - DATA QUALITY RULES
-- ============================================================
CREATE TABLE Admin.DataQualityRule (
    RuleId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    RuleName            NVARCHAR(200)    NOT NULL,
    TableName           NVARCHAR(200)    NOT NULL,
    RuleDefinition      NVARCHAR(MAX)    NOT NULL,
    Category            NVARCHAR(100)    NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- ADMIN SCHEMA - DATA CENTER CONFIG
-- ============================================================
CREATE TABLE Admin.DataCenterConfig (
    ConfigId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    DataCenterName      NVARCHAR(200)    NOT NULL,
    Region              NVARCHAR(100)    NULL,
    Environment         NVARCHAR(100)    NULL,
    ConnectionString    NVARCHAR(MAX)    NULL,
    IsPrimary           BIT              NOT NULL DEFAULT 0,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- ADMIN SCHEMA - SLA POLICIES
-- ============================================================
CREATE TABLE Admin.SLAPolicy (
    PolicyId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    PolicyName          NVARCHAR(200)    NOT NULL,
    Description         NVARCHAR(1000)   NULL,
    SeverityLevel       NVARCHAR(50)     NOT NULL,
    ResponseTimeMinutes INT              NOT NULL,
    ResolutionTimeMinutes INT            NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);
```

### Step 6: Seed Test Data

In migration `0044`, add seed data:

```sql
-- Seed Business Rules
INSERT INTO Admin.BusinessRule (BusinessRuleId, TenantId, Name, Category, Trigger, [Action], Priority, Status, ExecutionOrder)
VALUES
    (NEWID(), @TenantId, 'Require prior carrier', 'Policy', 'On record save', 'Block save', 'High', 'Active', 1),
    (NEWID(), @TenantId, 'Calculate commission', 'Billing', 'On status change', 'Auto-set field', 'Medium', 'Active', 2),
    (NEWID(), @TenantId, 'Validate claim dates', 'Claims', 'On record save', 'Show warning', 'High', 'Active', 3),
    (NEWID(), @TenantId, 'Check compliance', 'Compliance', 'On record save', 'Block save', 'High', 'Active', 4),
    (NEWID(), @TenantId, 'Route workflow', 'Workflow', 'On status change', 'Create task', 'Medium', 'Active', 5);

-- Seed Teams
INSERT INTO Admin.DepartmentTeam (TeamId, TenantId, TeamName, IsActive)
VALUES
    (NEWID(), @TenantId, 'Sales Team', 1),
    (NEWID(), @TenantId, 'Claims Team', 1),
    (NEWID(), @TenantId, 'Operations Team', 1);

-- Seed Staff
INSERT INTO Admin.ProducerStaff (StaffId, TenantId, FirstName, LastName, Email, Phone, Role, NpnLicense, LicenseExpiryDate, IsActive)
VALUES
    (NEWID(), @TenantId, 'John', 'Smith', 'john@example.com', '555-1234', 'Producer', 'NPN123456', DATEADD(YEAR, 1, GETUTCDATE()), 1),
    (NEWID(), @TenantId, 'Sarah', 'Johnson', 'sarah@example.com', '555-5678', 'CSR', NULL, NULL, 1),
    (NEWID(), @TenantId, 'Mike', 'Davis', 'mike@example.com', '555-9012', 'Producer', 'NPN789012', DATEADD(YEAR, 2, GETUTCDATE()), 1);

-- Seed System Settings
INSERT INTO Admin.SystemSettings (SettingId, TenantId, SettingKey, SettingValue, Category, DataType)
VALUES
    (NEWID(), @TenantId, 'default_commission_rate', '10', 'Billing', 'Integer'),
    (NEWID(), @TenantId, 'enable_notifications', 'true', 'System', 'Boolean'),
    (NEWID(), @TenantId, 'max_policy_value', '500000', 'Policy', 'Integer');
```

### Step 7: Update Navigation

Add links to admin pages in `NavSidebar.razor`:

```razor
<!-- Admin section in navigation -->
<a href="/admin/system/rules" class="nav-item">
    <i class="bi bi-node-plus"></i> Business Rules
</a>
<a href="/admin/agency/teams" class="nav-item">
    <i class="bi bi-diagram-3"></i> Teams
</a>
<a href="/admin/agency/staff" class="nav-item">
    <i class="bi bi-person-badge"></i> Staff
</a>
<a href="/admin/system/settings" class="nav-item">
    <i class="bi bi-gear"></i> Settings
</a>
```

### Step 8: Apply CSS Classes to Existing Pages

Update existing admin pages with CSS classes:
- Replace old styling with `ap-*` classes
- Use KPI cards for metrics
- Apply `ap-table` classes to tables
- Use `ap-btn--primary`, `ap-btn--ghost` for buttons
- Apply badge classes for status indicators

---

## 📊 Component Reference

### KPI Card
```html
<div class="ap-kpi-card">
    <span class="ap-kpi-icon ap-kpi-icon--success">
        <i class="bi bi-icon"></i>
    </span>
    <div>
        <div class="ap-kpi-value">123</div>
        <div class="ap-kpi-label">Label</div>
    </div>
</div>
```

### Button Styles
```html
<button class="ap-btn ap-btn--primary">Primary</button>
<button class="ap-btn ap-btn--secondary">Secondary</button>
<button class="ap-btn ap-btn--danger">Danger</button>
<button class="ap-btn ap-btn--ghost">Ghost</button>
<button class="ap-btn ap-btn--sm">Small</button>
<button class="ap-btn ap-btn--lg">Large</button>
```

### Badge Styles
```html
<span class="ap-badge ap-badge--success">Success</span>
<span class="ap-badge ap-badge--warning">Warning</span>
<span class="ap-badge ap-badge--danger">Danger</span>
<span class="ap-badge ap-badge--info">Info</span>
<span class="ap-badge ap-badge--neutral">Neutral</span>
```

### Form Elements
```html
<div class="ap-form-group">
    <label class="ap-form-label ap-required">Field Label</label>
    <input type="text" class="ap-form-input" placeholder="Placeholder" />
    <div class="ap-form-hint">Helper text</div>
    <div class="ap-form-error">Error message</div>
</div>
```

---

## 🎨 Design System

### Colors
- **Primary**: #3b82f6 (Blue)
- **Success**: #10b981 (Green)
- **Warning**: #f59e0b (Amber)
- **Danger**: #ef4444 (Red)
- **Info**: #0ea5e9 (Cyan)

### Typography
- **Headings**: Bold, 1.875rem (30px)
- **Subtitles**: Regular, 0.9375rem (15px)
- **Body**: Regular, 0.9375rem (15px)
- **Small**: Regular, 0.875rem (14px)

### Spacing
- **xs**: 0.5rem
- **sm**: 1rem
- **md**: 1.5rem
- **lg**: 2rem
- **xl**: 2.5rem

---

## ✨ Features & Capabilities

✅ Multi-tenant support (TenantId)
✅ User context capture
✅ Soft deletes (IsDeleted flag)
✅ Audit trail ready (CreatedDateUtc, CreatedByUserId)
✅ Status management
✅ Search & filtering
✅ Pagination ready
✅ Real-time validation
✅ Responsive design
✅ Accessibility (WCAG AA)
✅ Dark mode compatible
✅ Mobile-first approach

---

## 🔗 File Structure

```
src/
├── Ams.Application/
│   ├── Common/Dtos/AdminPagesDto.cs
│   ├── Abstractions/Services/IAdminPagesServices.cs
│   ├── Abstractions/Persistence/IAdminPagesRepositories.cs
│   └── AdminPagesService.cs
├── Ams.Infrastructure/
│   └── Persistence/Repositories/AdminRepositories.cs
├── Ams.Api/
│   └── Controllers/Admin/AdminPagesControllers.cs
├── Ams.Web/
│   ├── Css/admin-professional.css
│   ├── Components/Pages/
│   │   ├── AdminBusinessRulesModern.razor
│   │   ├── AdminBusinessRules.razor
│   │   ├── AdminSystemSettings.razor
│   │   ├── AdminNotificationPolicies.razor
│   │   ├── AdminQueueRouting.razor
│   │   ├── AdminDataQuality.razor
│   │   ├── AdminDataCenter.razor
│   │   ├── AdminSlaPolicySetup.razor
│   │   ├── Agency/AgencyProfile.razor
│   │   ├── Agency/DepartmentsTeams.razor
│   │   └── Agency/ProducersStaff.razor
│   └── Services/ApiClient.cs
```

---

## 🧪 Testing Checklist

- [ ] Build solution successfully
- [ ] Services register without errors
- [ ] API endpoints respond correctly
- [ ] Admin pages render without errors
- [ ] Create new record works
- [ ] Edit record updates correctly
- [ ] Delete record removes from list
- [ ] Filtering works (search, category, status)
- [ ] KPI cards display correct counts
- [ ] Modal opens/closes properly
- [ ] Form validation works
- [ ] Error messages display
- [ ] Responsive on mobile/tablet/desktop
- [ ] Accessibility check (keyboard navigation, screen reader)
- [ ] Database migrations run successfully
- [ ] Seed data loads correctly

---

## 📞 Support & References

- Bootstrap Icons: https://icons.getbootstrap.com/
- Tailwind CSS Color Reference: https://tailwindcss.com/docs/customizing-colors
- Blazor Documentation: https://learn.microsoft.com/aspnet/core/blazor
- ASP.NET Core API Best Practices: https://learn.microsoft.com/aspnet/core/web-api

---

## 📝 Notes

- All repositories currently use in-memory storage for testing
- Update repositories to use Dapper + SQL Server for production
- Add unit tests for services and repositories
- Implement proper error handling and validation
- Add rate limiting to API endpoints
- Consider implementing caching for frequently accessed data
- Add audit logging for changes to critical entities

---

**Document Version**: 1.0
**Last Updated**: 2024
**Status**: Ready for Implementation
