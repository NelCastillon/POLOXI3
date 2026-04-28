# 🛠️ PRACTICAL CSS FIX - HOW TO UPDATE YOUR PAGES

## 📋 OVERVIEW

Your pages exist but use inline styles instead of the CSS framework. This guide shows exactly how to convert them.

---

## 🔄 CONVERSION EXAMPLES

### BEFORE: Using Inline Styles

```razor
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1.5rem; margin-bottom: 2rem;">
    <div style="background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);">
        <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem;">
            <span style="width: 48px; height: 48px; border-radius: 12px; background: linear-gradient(135deg, #3b82f6, #1d4ed8); display: flex; align-items: center; justify-content: center; color: white; font-size: 1.5rem;">
                <i class="bi bi-building-fill"></i>
            </span>
            <i class="bi bi-chevron-right" style="color: #d1d5db; font-size: 1.5rem;"></i>
        </div>
        <h3 style="margin: 0 0 0.5rem 0; color: #111827; font-weight: 600;">Agency Profile</h3>
        <p style="margin: 0; color: #6b7280; font-size: 0.875rem; line-height: 1.5;">
            Configure agency legal information, contact details, and E&O coverage settings
        </p>
    </div>
</div>
```

### AFTER: Using CSS Framework

```razor
<div class="ap-kpi-strip ap-mb-4">
    <div class="ap-card">
        <div class="ap-flex ap-items-center ap-justify-between ap-mb-3">
            <span class="ap-kpi-icon ap-kpi-icon--primary">
                <i class="bi bi-building-fill"></i>
            </span>
            <i class="bi bi-chevron-right ap-text-tertiary"></i>
        </div>
        <h3 class="ap-font-semibold ap-m-0 ap-mb-2 ap-text-primary">Agency Profile</h3>
        <p class="ap-m-0 ap-text-secondary ap-font-normal">
            Configure agency legal information, contact details, and E&O coverage settings
        </p>
    </div>
</div>
```

---

## 🎯 CONVERSION GUIDE

### Step 1: Page Header

**BEFORE:**
```razor
<!-- Professional Header -->
<div class="ap-page-header">
    <div>
        <h1 class="ap-page-header__title">
            <i class="bi bi-diagram-3-fill" style="margin-right: 0.75rem;"></i>Branches
        </h1>
        <p class="ap-page-header__subtitle">
            Manage agency office locations and branch codes used throughout the system
        </p>
    </div>
    <div class="ap-page-header__actions">
        <button class="ap-btn ap-btn--ghost" title="Refresh data" @onclick="RefreshAsync">
            <i class="bi bi-arrow-clockwise"></i> Refresh
        </button>
```

**AFTER:** (Same! Already correct)
This structure is perfect as-is.

### Step 2: KPI Strip

**BEFORE:**
```razor
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1.5rem; margin-bottom: 2rem;">
    <div style="background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 1.5rem; display: flex; align-items: center; gap: 1.25rem;">
        <span style="width: 3rem; height: 3rem; border-radius: 12px; background: linear-gradient(135deg, rgba(59, 130, 246, 0.1) 0%, rgba(59, 130, 246, 0.05) 100%); display: flex; align-items: center; justify-content: center; color: #3b82f6; font-size: 1.5rem;">
            <i class="bi bi-diagram-3-fill"></i>
        </span>
        <div>
            <div style="font-size: 1.875rem; font-weight: 700; color: #111827;">4</div>
            <div style="font-size: 0.875rem; color: #6b7280;">Total Branches</div>
        </div>
    </div>
</div>
```

**AFTER:**
```razor
<div class="ap-kpi-strip ap-mb-4">
    <div class="ap-kpi-card">
        <span class="ap-kpi-icon ap-kpi-icon--primary">
            <i class="bi bi-diagram-3-fill"></i>
        </span>
        <div>
            <div class="ap-kpi-value">4</div>
            <div class="ap-kpi-label">Total Branches</div>
        </div>
    </div>
</div>
```

### Step 3: Filter Bar

**BEFORE:**
```razor
<div style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem; padding: 1.25rem; background: white; border: 1px solid #e5e7eb; border-radius: 12px; align-items: center;">
    <div style="display: flex; align-items: center; gap: 0.5rem; flex: 1; min-width: 250px; background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 0.5rem 1rem;">
        <i class="bi bi-search"></i>
        <input type="text" placeholder="Search branch name or code…" 
               @bind="_search" @bind:event="oninput" />
    </div>
</div>
```

**AFTER:**
```razor
<div class="ap-filter-bar ap-mb-4">
    <div class="ap-search-box">
        <i class="bi bi-search"></i>
        <input type="text" placeholder="Search branch name or code…" 
               @bind="_search" @bind:event="oninput" />
    </div>
</div>
```

### Step 4: Data Table

**BEFORE:**
```razor
<div style="background: white; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);">
    <table style="width: 100%; border-collapse: collapse; font-size: 0.9375rem;">
        <thead style="background: #f9fafb; border-bottom: 2px solid #e5e7eb;">
            <tr>
                <th style="padding: 1rem; text-align: left; font-weight: 600; color: #111827;">Branch Name</th>
```

**AFTER:**
```razor
<div class="ap-table-wrapper">
    <table class="ap-table">
        <thead>
            <tr>
                <th>Branch Name</th>
```

### Step 5: Modal Dialog

**BEFORE:**
```razor
<div style="position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;">
    <div style="background: white; border: 1px solid #e5e7eb; border-radius: 12px; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1); overflow: hidden; width: 90%; max-width: 700px;">
        <div style="padding: 1.5rem; border-bottom: 1px solid #e5e7eb; display: flex; justify-content: space-between; align-items: center;">
            <h2 style="font-size: 1.25rem; font-weight: 700; color: #111827; margin: 0;">Create New Branch</h2>
```

**AFTER:**
```razor
<div style="position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;">
    <div class="ap-modal" style="width: 90%; max-width: 700px;">
        <div class="ap-modal-header">
            <h2 class="ap-modal-title">Create New Branch</h2>
```

### Step 6: Form Fields

**BEFORE:**
```razor
<div style="display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1.5rem;">
    <label style="font-size: 0.875rem; font-weight: 600; color: #111827;">* Branch Name</label>
    <input type="text" style="padding: 0.625rem 0.875rem; border: 1px solid #e5e7eb; border-radius: 8px; font-size: 0.9375rem; color: #111827;" 
           @bind="_editingBranch.BranchName" placeholder="e.g. New York Downtown" />
</div>
```

**AFTER:**
```razor
<div class="ap-form-group">
    <label class="ap-form-label ap-required">Branch Name</label>
    <input type="text" class="ap-form-input" 
           @bind="_editingBranch.BranchName" placeholder="e.g. New York Downtown" />
</div>
```

### Step 7: Buttons

**BEFORE:**
```razor
<button style="display: inline-flex; align-items: center; justify-content: center; gap: 0.5rem; padding: 0.625rem 1.25rem; border: none; border-radius: 8px; font-size: 0.9375rem; font-weight: 600; cursor: pointer; background: linear-gradient(135deg, #3b82f6, #1d4ed8); color: white; box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05);">
    <i class="bi bi-pencil"></i> Edit
</button>
```

**AFTER:**
```razor
<button class="ap-btn ap-btn--primary">
    <i class="bi bi-pencil"></i> Edit
</button>
```

### Step 8: Badges

**BEFORE:**
```razor
<span style="display: inline-flex; align-items: center; justify-content: center; padding: 0.25rem 0.75rem; border-radius: 999px; font-size: 0.75rem; font-weight: 700; background: rgba(16, 185, 129, 0.1); color: #047857;">
    Active
</span>
```

**AFTER:**
```razor
<span class="ap-badge ap-badge--success">Active</span>
```

---

## 🚀 FULL PAGE EXAMPLE

Here's a complete page using the CSS framework:

```razor
@page "/admin/agency/branches"
@namespace Ams.Web.Components.Pages.AgencyAdmin
@inject BreadcrumbService Breadcrumbs

<PageTitle>Branches — Agency Setup</PageTitle>

<div class="ap-page-container">
    <!-- Header -->
    <div class="ap-page-header">
        <div>
            <h1 class="ap-page-header__title">
                <i class="bi bi-diagram-3-fill"></i> Branches
            </h1>
            <p class="ap-page-header__subtitle">
                Manage agency office locations and branch codes
            </p>
        </div>
        <div class="ap-page-header__actions">
            <button class="ap-btn ap-btn--ghost" @onclick="RefreshAsync">
                <i class="bi bi-arrow-clockwise"></i> Refresh
            </button>
            <button class="ap-btn ap-btn--primary" @onclick="OpenCreateModal">
                <i class="bi bi-plus-lg"></i> Add Branch
            </button>
        </div>
    </div>

    <!-- Content -->
    <div class="ap-content">
        <!-- KPI Strip -->
        <div class="ap-kpi-strip ap-mb-4">
            <div class="ap-kpi-card">
                <span class="ap-kpi-icon ap-kpi-icon--primary">
                    <i class="bi bi-diagram-3-fill"></i>
                </span>
                <div>
                    <div class="ap-kpi-value">@_branches.Count</div>
                    <div class="ap-kpi-label">Total</div>
                </div>
            </div>
            <div class="ap-kpi-card">
                <span class="ap-kpi-icon ap-kpi-icon--success">
                    <i class="bi bi-check-circle-fill"></i>
                </span>
                <div>
                    <div class="ap-kpi-value">@_branches.Count(b => b.IsActive)</div>
                    <div class="ap-kpi-label">Active</div>
                </div>
            </div>
        </div>

        <!-- Filter Bar -->
        <div class="ap-filter-bar ap-mb-4">
            <div class="ap-search-box">
                <i class="bi bi-search"></i>
                <input type="text" placeholder="Search..." @bind="_search" />
            </div>
        </div>

        <!-- Table -->
        <div class="ap-table-wrapper">
            <table class="ap-table">
                <thead>
                    <tr>
                        <th>Branch Name</th>
                        <th>Code</th>
                        <th>City</th>
                        <th style="text-align: right;">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var branch in _branches)
                    {
                        <tr>
                            <td><strong>@branch.BranchName</strong></td>
                            <td>@branch.BranchCode</td>
                            <td>@branch.City</td>
                            <td style="text-align: right;">
                                <button class="ap-btn ap-btn--ghost ap-btn--sm" @onclick="@(() => EditBranch(branch))">
                                    <i class="bi bi-pencil"></i>
                                </button>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
</div>

<!-- Modal -->
@if (_showModal)
{
    <div style="position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;">
        <div class="ap-modal" style="width: 90%; max-width: 600px;">
            <div class="ap-modal-header">
                <h2 class="ap-modal-title">@(_editingBranch?.Id == 0 ? "New Branch" : "Edit Branch")</h2>
                <button class="ap-modal-close" @onclick="CloseModal">✕</button>
            </div>
            <div class="ap-modal-body">
                <div class="ap-form-group">
                    <label class="ap-form-label ap-required">Branch Name</label>
                    <input type="text" class="ap-form-input" @bind="_editingBranch.BranchName" />
                </div>
                <div class="ap-form-group">
                    <label class="ap-form-label ap-required">Branch Code</label>
                    <input type="text" class="ap-form-input" @bind="_editingBranch.BranchCode" />
                </div>
            </div>
            <div class="ap-modal-footer">
                <button class="ap-btn ap-btn--ghost" @onclick="CloseModal">Cancel</button>
                <button class="ap-btn ap-btn--primary" @onclick="SaveBranch">Save</button>
            </div>
        </div>
    </div>
}

@code {
    // Your code here
}
```

---

## ✅ CHECKLIST FOR UPDATE

- [ ] Replace header `<div style="...">` with `.ap-page-header`
- [ ] Replace KPI strip `<div style="display: grid">` with `.ap-kpi-strip`
- [ ] Replace filter bar with `.ap-filter-bar`
- [ ] Replace table wrapper with `.ap-table-wrapper`
- [ ] Replace modal with `.ap-modal`
- [ ] Replace form groups with `.ap-form-group`
- [ ] Replace buttons with `.ap-btn` classes
- [ ] Replace badges with `.ap-badge` classes
- [ ] Add `.ap-mb-4` for margins between sections
- [ ] Remove all inline style attributes
- [ ] Build and test

---

## 🎯 RESULT

After conversion, your pages will:
- ✅ Look professional
- ✅ Be responsive
- ✅ Have consistent styling
- ✅ Be easier to maintain
- ✅ Load faster
- ✅ Be more accessible

---

## 🚀 NEXT STEPS

1. **Update one page at a time**
2. **Build after each change**
3. **Test responsiveness**
4. **Verify all features work**
5. **Deploy when ready**

---

**Build Status**: ✅ SUCCESSFUL
**Framework**: ✅ READY
**Implementation**: ✅ EASY

Start converting your pages today! 🎉
