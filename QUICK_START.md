# Account Pages - Quick Start Guide

## 🚀 Getting Started

### Pages Available
1. **Account Notes** - `/client/account-notes` - Manage internal account observations
2. **Account Segments** - `/client/segments` - Define customer segments
3. **Portal Invites** - `/client/portal-invites` - Distribute portal access
4. **Account Ownership** - `/client/account-ownership` - Track ownership transfers

---

## 📍 Navigation

All pages are accessible via the sidebar under **Accounts** section:

```
Accounts (bi-people)
├── Accounts
├── Contacts
├── Account Notes          ← New
├── Segments               ← New
├── Portal Invites         ← New
└── Ownership              ← New

Account 360
└── [Individual account pages]
```

---

## 🎯 Common Page Structure

Every page follows the same pattern:

```
┌─────────────────────────────────┐
│ Page Title | Subtitle [Actions] │
└─────────────────────────────────┘

[KPI Strip with metrics]

[Filter Bar: Search | Dropdown | Dropdown]

[Data Display]
├─ Datagrid with columns
├─ Card list
└─ Empty state if no data

[Modal for Create/Edit]
```

---

## 🔧 Common Features

### Create New Item
1. Click the **+ New [Item]** button in the top-right
2. Fill in the form fields
3. Click **Save**
4. Success toast appears

### Edit Existing Item
1. Click the **Edit** button (pencil icon) on item
2. Update fields in modal
3. Click **Save**
4. Toast notification confirms

### Delete Item
1. Click the **Delete** button (trash icon) on item
2. Confirmation required (future: JS interop)
3. Item removed from list
4. Success toast appears

### Search
1. Type in the search box at top of page
2. List filters in real-time
3. Clear search to see all items

### Filter
1. Use dropdown filters in filter bar
2. Select desired filter value
3. List updates immediately
4. Click "X" on dropdown to clear filter

### Date Range (Ownership page only)
1. Click date range picker
2. Select start and end dates
3. List filters to matching items

---

## 📊 Understanding Each Page

### Account Notes
**What it does:** Stores internal notes about accounts
**Data shown:** Account name, note content, priority, created date
**Key actions:** Create note, edit note, delete note, filter by priority
**Common use:** Sales team notes, follow-ups, opportunities, issues

**Fields:**
- Account (required) - Which account this note is about
- Content (required) - The note text
- Type - Optional categorization
- Priority - Low/Medium/High/Critical

**Filters:**
- Search by account name or note content
- Filter by priority level
- Filter by category

---

### Account Segments
**What it does:** Manages account segmentation strategy
**Data shown:** Segment name, code, description, status
**Key actions:** Create segment, edit segment, delete segment
**Common use:** Define customer tiers (Enterprise, Mid-Market, SMB, etc.)

**Fields:**
- Code (required) - Short identifier (e.g., "ENTERPRISE")
- Name (required) - Full segment name
- Description - What defines this segment
- Active - Is it currently in use?

**Filters:**
- Search by name or code
- Filter by Active/Inactive status

**KPIs:**
- Total Segments
- Active Segments

---

### Portal Invites
**What it does:** Manages client portal access distribution
**Data shown:** Account, contact, email, status, sent date, expires date
**Key actions:** Send invite, resend invite, delete invite
**Common use:** Onboard new portal users, resend expired invites

**Fields:**
- Account (required) - Which account gets access
- Contact Name (required) - Person's name
- Email (required) - Email to send invite to
- Expires In - Days until invite expires (default: 30)
- Message - Custom email message (optional)

**Statuses:**
- 🟢 Accepted - User accepted the invitation
- 🟡 Pending - Waiting for user response
- ⚫ Expired - Invitation expired
- 🔴 Revoked - Invitation was revoked

**Common Actions:**
- Send new invite → User receives email
- Resend pending → Resend to user who hasn't responded
- Delete expired → Clean up old invitations

---

### Account Ownership
**What it does:** Tracks who owns each account and transfer history
**Data shown:** Account, previous owner, new owner, transfer date, reason
**Key actions:** Transfer ownership, view history
**Common use:** Account management changes, team reassignments, handoffs

**Fields:**
- Account (required) - Which account to transfer
- Current Owner - Shows who currently owns it (read-only)
- New Owner (required) - Who should own it now
- Transfer Notes - Reason for transfer (optional)

**Filters:**
- Search by account name or owner name
- Filter by date range
- Date picker shows historical changes

**Common Scenarios:**
- Producer left the company → transfer accounts to new producer
- Team reorganization → reassign accounts to new team
- Seasonal staffing → transfer accounts temporarily

---

## 💡 Tips & Tricks

### Effective Searching
- Search works on multiple fields
- Partial matches work (e.g., "acme" finds "ACME Corp")
- Case-insensitive search
- Combine search + filters for powerful queries

### Form Validation
- Required fields show `*` asterisk
- Form won't submit if required fields are empty
- Toast warning explains what's missing
- Red border indicates validation error

### Keyboard Shortcuts
- Tab through form fields
- Enter to submit in modal
- Escape to close modal
- No custom shortcuts (standard browser)

### Mobile Usage
- All pages are mobile-responsive
- Touch-friendly button sizes
- Swipe-friendly grids
- Portrait/landscape compatible

---

## 🔔 Status Indicators

### Priority Levels (Account Notes)
```
🟢 Low      - Green background
🟡 Medium   - Amber/Orange background
🟠 High     - Orange background
🔴 Critical - Red background
```

### Portal Invite Status
```
🟢 Accepted - Green (completed)
🟡 Pending  - Amber (waiting)
⚫ Expired   - Gray (no longer valid)
🔴 Revoked  - Red (manually disabled)
```

### Segment Status
```
🟢 Active   - Green (in use)
⚫ Inactive - Gray (not in use)
```

---

## ⚠️ Common Issues

### "No items found"
- Check your filters
- Try clearing the search
- Verify data exists in system

### Form won't submit
- Check all required fields (marked with *)
- Look for validation messages
- Ensure no toast error appeared

### Modal won't close
- Click Cancel button
- Try pressing Escape key
- Refresh page if stuck

### Filters not working
- Click the X to clear current filter
- Re-select desired filter value
- Check search box isn't also filtering

---

## 📱 Responsive Design

### Desktop (1200px+)
- Full sidebar visible
- All columns visible in grids
- Optimal spacing and typography

### Tablet (768px - 1199px)
- Sidebar collapsible
- Some columns may stack
- Adjusted spacing

### Mobile (< 768px)
- Sidebar hidden/hamburger
- Cards stack vertically
- Touch-friendly sizing
- Readable font sizes

---

## 🎨 Visual Design System

### Colors Used
- **Blue** - Primary actions, information
- **Green** - Success, active, positive
- **Amber** - Warning, pending, attention
- **Red** - Danger, critical, delete
- **Purple** - Special, secondary
- **Gray** - Inactive, disabled, muted

### Icons
- 📝 bi-sticky - Notes
- 🎯 bi-pie-chart - Segments
- 📧 bi-envelope - Invites
- 👤 bi-diagram-2 - Ownership
- ➕ bi-plus-lg - Add new
- ✏️ bi-pencil - Edit
- 🗑️ bi-trash - Delete
- 🔄 bi-arrow-repeat - Refresh/Resend

### Typography
- Headers: Large, bold, dark
- Labels: Small, bold, muted
- Body text: Regular, readable
- Monospace: For codes/IDs

---

## 🚀 API Integration (For Developers)

Each page needs these API methods implemented:

### Account Notes
```csharp
// In ApiClient class
public async Task<SearchResult<AccountNoteDto>> SearchAccountNotesAsync(
    Guid tenantId, 
    string searchTerm)
```

### Account Segments
```csharp
public async Task<SearchResult<AccountSegmentDto>> SearchAccountSegmentsAsync(
    string searchTerm)
```

### Portal Invites
```csharp
public async Task<SearchResult<PortalInviteDto>> SearchPortalInvitesAsync(
    Guid tenantId, 
    string searchTerm)
```

### Account Ownership
```csharp
public async Task<SearchResult<AccountOwnerHistoryDto>> SearchAccountOwnershipAsync(
    Guid tenantId,
    Guid? accountId, 
    string searchTerm)
```

See `ACCOUNT_PAGES_IMPLEMENTATION.md` for complete integration guide.

---

## 📚 Additional Resources

- **Implementation Guide:** `ACCOUNT_PAGES_IMPLEMENTATION.md`
- **Styling Reference:** `STYLING_REFERENCE.md`
- **File Structure:** `FILES_STRUCTURE.md`
- **Delivery Summary:** `DELIVERY_SUMMARY.md`

---

## ✅ Checklist for First Use

- [ ] Navigate to Account Notes page
- [ ] View the KPI strip metrics
- [ ] Try the search functionality
- [ ] Open the filter dropdowns
- [ ] Click "+ New Note" button
- [ ] Review the form fields
- [ ] Cancel the modal
- [ ] Click refresh button
- [ ] Verify breadcrumb navigation
- [ ] Test responsive on mobile

---

## 🆘 Getting Help

1. **Check the tooltips** - Hover over fields for hints
2. **Read error messages** - Toasts explain what went wrong
3. **Look at documentation** - See reference guides above
4. **Contact developer** - For API integration issues

---

**Version:** 1.0
**Last Updated:** 2024
**Status:** ✅ Ready to Use

Enjoy using your new account management pages! 🎉
