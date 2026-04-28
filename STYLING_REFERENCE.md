# Account Pages - Styling Reference Guide

## Color Palette & Classes

### Badge Variants

#### Priority Badges (Account Notes)
```css
.acn-priority--low      /* Green - Low priority */
.acn-priority--medium   /* Amber - Medium priority */
.acn-priority--high     /* Orange - High priority */
.acn-priority--critical /* Red - Critical priority */
```

#### Status Badges (Portal Invites)
```css
.api-status--accepted   /* Green - Accepted invites */
.api-status--pending    /* Amber - Pending invites */
.api-status--expired    /* Gray - Expired invites */
.api-status--revoked    /* Red - Revoked invites */
```

#### Status Badges (Account Segments)
```css
.acs-status--active     /* Green - Active segments */
.acs-status--inactive   /* Gray - Inactive segments */
```

### KPI Card Icons

#### Color Scheme
```css
.acn-ki1 / .acs-ki1 / .api-ki1  /* Blue (#dbeafe) */
.acn-ki2 / .acs-ki2 / .api-ki2  /* Red (#fee2e2) or Green (#d1fae5) */
.acn-ki3 / .api-ki3             /* Purple (#ede9fe) or Amber (#fef3c7) */
.acn-ki4 / .api-ki4             /* Amber (#fef3c7) */
```

### Avatar Colors
```css
.acl-av1 /* Blue */
.acl-av2 /* Purple */
.acl-av3 /* Green */
.acl-av4 /* Amber */
.acl-av5 /* Red */
.acl-av6 /* Pink */
```

## Component Structure

### Filter Bar
```html
<div class="[prefix]-filter-bar">
    <div class="[prefix]-search-wrap">
        <i class="bi bi-search [prefix]-si"></i>
        <input class="[prefix]-search" type="search" />
    </div>
    <SfDropDownList>...</SfDropDownList>
</div>
```

### KPI Strip
```html
<div class="[prefix]-kpi-strip">
    <div class="[prefix]-kpi-card">
        <span class="[prefix]-ki [prefix]-ki1"><i></i></span>
        <div>
            <div class="[prefix]-kv">Value</div>
            <div class="[prefix]-kl">Label</div>
        </div>
    </div>
</div>
```

### Card Layout (Notes)
```html
<div class="[prefix]-card">
    <div class="[prefix]-card-header">
        <!-- Title and metadata -->
        <div class="[prefix]-badges"><!-- Badges --></div>
    </div>
    <div class="[prefix]-content"><!-- Main content --></div>
    <div class="[prefix]-card-footer"><!-- Actions --></div>
</div>
```

### Grid Card (Segments, Invites)
```html
<div class="[prefix]-grid-card app-datagrid">
    <SfGrid>...</SfGrid>
</div>
```

### Dialog/Modal
```html
<SfDialog>
    <DialogTemplates>
        <Header>
            <span class="[prefix]-dlg-hdr">
                <i></i> Title
            </span>
        </Header>
        <Content>
            <div class="[prefix]-dlg-body"><!-- Form fields --></div>
        </Content>
        <FooterTemplate>
            <div class="[prefix]-dlg-footer"><!-- Buttons --></div>
        </FooterTemplate>
    </DialogTemplates>
</SfDialog>
```

## Responsive Utilities

### Spacing Variables
```css
/* Standard gap/margin values */
gap: 0.75rem;      /* Card spacing */
gap: 0.65rem;      /* Filter bar items */
padding: 1rem;     /* Card padding */
margin-bottom: 1.1rem; /* Section spacing */
```

### Flex Layouts
```css
/* Horizontal flex (filter bar, actions) */
display: flex;
flex-wrap: wrap;
gap: 0.65rem;
align-items: center;

/* Vertical flex (cards) */
display: flex;
flex-direction: column;
gap: 0.6rem;
```

### Grid Layouts
```css
/* Two-column form */
grid-template-columns: 1fr 1fr;
gap: 0.75rem;

/* Full-width field */
grid-column: span 2;
```

## Animation Classes

### Spinner Rotation
```css
.acn-spin    /* Account Notes spinner */
.acs-spin    /* Account Segments spinner */
.api-spin    /* Portal Invites spinner */
.aow-spin    /* Account Ownership spinner */

@keyframes [prefix]-rot {
    to { transform: rotate(360deg); }
}
```

### States
```css
/* Hover states */
border-color: var(--um-primary);
box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);

/* Focus states */
outline: none;
border-color: var(--um-primary);
box-shadow: 0 0 0 2px var(--um-primary-pale);
```

## Typography Classes

```css
.acn-account        /* Account name - bold, small */
.acn-meta           /* Metadata - muted, tiny */
.acn-author         /* Author/user - semibold */
.acn-date           /* Date - italic, muted */
.acn-item-name      /* Item name - bold */
.acn-item-code      /* Item code/sub - muted */
.acn-content        /* Main content - normal, readable */
```

## Component Prefixes
- **acn** - Account Notes
- **acs** - Account Segments
- **api** - Portal Invites (Account Portal Invites)
- **aow** - Account Ownership

## CSS Variables Used

```css
--um-primary        /* Primary brand color */
--um-primary-pale   /* Light version of primary */
--um-surface        /* Card/surface background */
--um-border         /* Border color */
--um-text-primary   /* Main text */
--um-text-muted     /* Secondary/muted text */
```

## Empty State Pattern

```html
<div class="[prefix]-empty">
    <i class="bi bi-[icon]"></i>
    <span>Message text</span>
</div>
```

## Loading State Pattern

```html
<div class="[prefix]-loading">
    <div class="um-spinner"></div>
    <span>Loading text…</span>
</div>
```

## Modal Form Pattern

```csharp
// Set up form data
_editingItem = new();
_showModal = true;

// Save with validation
if (string.IsNullOrEmpty(field))
{
    await toast.ShowAsync(warning);
    return;
}

// Success feedback
await toast.ShowAsync(success);
await LoadAsync();
```

## Grid Column Configuration

### Standard Column Widths
```csharp
MinWidth="180"  /* Name/description columns */
MinWidth="160"  /* Type/status columns */
MinWidth="220"  /* Email columns */
Width="120"     /* Date columns */
Width="100"     /* Small status columns */
Width="80"      /* Action button columns */
```

### Text Alignment
```csharp
TextAlign="Syncfusion.Blazor.Grids.TextAlign.Center"   /* Centered */
TextAlign="Syncfusion.Blazor.Grids.TextAlign.Right"    /* Right-aligned (numbers) */
/* Default is left */
```

---

**Note:** All components use the application's design system variables. Ensure any new components follow these patterns for consistency.
