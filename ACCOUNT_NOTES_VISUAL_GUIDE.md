# Account Notes Page - Visual Guide & Feature Overview

## 🎨 Page Layout

```
┌──────────────────────────────────────────────────────────────────────────┐
│  ACCOUNT NOTES                                              🔄    ➕ NEW  │
│  Internal notes and observations across all accounts                    │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────┬──────────────┬──────────────┬──────────────────────────────┐
│ TOTAL: 42    │ CRITICAL: 3  │ TODAY: 8     │ ACCOUNTS: 12                 │
│ Notes        │ Notes        │ Notes        │ Unique                       │
└──────────────┴──────────────┴──────────────┴──────────────────────────────┘

┌────────────────────────┬──────────────┬──────────────┐
│ 🔍 Search notes...     │ Priority ✕   │ Category ✕   │
└────────────────────────┴──────────────┴──────────────┘

Notes List:

┌────────────────────────────────────────────────────────────┐
│ AB  ACME Corp                    [High] [Opportunity]     │
│     Jan 15, 2024 · 2:30 PM                                │
├────────────────────────────────────────────────────────────┤
│ Great opportunity for expanding our insurance services     │
│ with ACME Corp. They want to add 50 employees to their    │
│ plan. Expected close: Q2 2024. Contact: John Smith.       │
├────────────────────────────────────────────────────────────┤
│ by System              [✏️]  [🗑️]                         │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│ TC  TechCorp Inc                 [Critical] [Risk]        │
│     Jan 14, 2024 · 9:15 AM                                │
├────────────────────────────────────────────────────────────┤
│ URGENT: TechCorp may be consolidating with another vendor.│
│ They've requested pricing for competitor services. Need to │
│ follow up today to understand their requirements.          │
├────────────────────────────────────────────────────────────┤
│ by System              [✏️]  [🗑️]                         │
└────────────────────────────────────────────────────────────┘

Showing 2 of 42 notes
```

---

## 🔍 Search Examples

```
Search: "acme"
Result: All notes for ACME account

Search: "opportunity"
Result: All notes containing "opportunity"

Search: "follow up"
Result: All notes mentioning follow up

Search: "urgent"
Result: All notes with urgent content
```

---

## 🎯 Filter Examples

### Single Filter
```
Priority: Critical
Result: All critical priority notes

Category: Follow-up
Result: All follow-up category notes
```

### Combined Filters
```
Priority: Critical + Category: Risk
Result: Critical risks only

Search: "acme" + Priority: High + Category: Opportunity
Result: High opportunity notes for ACME
```

---

## ✏️ Create/Edit Modal

```
┌────────────────────────────────────────────────┐
│ ➕ New Note                                    │
├────────────────────────────────────────────────┤
│                                                │
│  Account *                                     │
│  [Select account ▼]                            │
│                                                │
│  Note Content *                                │
│  ┌──────────────────────────────────────────┐ │
│  │ Enter your note here...                  │ │
│  │                                          │ │
│  │                                          │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  Priority              │ Category             │
│  [Select ▼]           │ [Select ▼]           │
│                                                │
│         [Cancel]  [✓ Save]                   │
└────────────────────────────────────────────────┘
```

---

## 📊 Priority System

### Visual Hierarchy
```
🔴 CRITICAL - Red (#dc2626)     - Most urgent, immediate action
🟠 HIGH     - Orange (#b45309)  - Important, address soon
🟡 MEDIUM   - Amber (#b45309)   - Normal priority, schedule
🟢 LOW      - Green (#047857)   - Nice to have, can wait
```

### Usage Examples
```
CRITICAL: URGENT: System down, revenue impact
HIGH:     Important follow-up needed within 48 hours
MEDIUM:   Standard account maintenance note
LOW:      FYI information for future reference
```

---

## 🏷️ Category System

### Available Categories
```
📋 GENERAL        - Miscellaneous, general information
⤴️  FOLLOW-UP      - Action item, needs follow-up
⚠️  ISSUE          - Problem reported, needs resolution
💡 OPPORTUNITY    - Business opportunity identified
⛔ RISK            - Potential risk or concern
```

### Usage Examples
```
FOLLOW-UP:    "Call client next week for update"
ISSUE:        "Invoice discrepancy needs resolution"
OPPORTUNITY:  "Client mentioned expansion plans"
RISK:         "Payment issues in last quarter"
GENERAL:      "New contact info for ACME Corp"
```

---

## 🔄 Common Workflows

### 1. Create a Quick Note
```
1. Click "+ New Note"
2. Select account (required)
3. Type note content (required)
4. Optionally set Priority and Category
5. Click Save
6. Success toast appears
7. Modal closes, list refreshes
```

### 2. Search for Account Notes
```
1. Type account name in search box
2. Results filter in real-time
3. View all notes for that account
4. Click edit/delete as needed
```

### 3. Find Critical Items
```
1. Click Priority dropdown
2. Select "Critical"
3. List shows only critical notes
4. Add Category filter if needed
5. Search further if needed
6. Click X to clear filter
```

### 4. Filter by Opportunity
```
1. Click Category dropdown
2. Select "Opportunity"
3. Results show business opportunities
4. Filter by Priority if needed
5. Search for specific accounts
```

### 5. Edit a Note
```
1. Click pencil icon on note
2. Modal opens with current data
3. Update content/priority/category
4. Click Save
5. Note updated, list refreshes
```

### 6. Delete a Note
```
1. Click trash icon on note
2. Confirmation prompt
3. Note removed
4. Success message shown
5. List refreshes
```

---

## 💬 Note Content Examples

```
ACME Corp - Opportunity
"Client mentioned they're expanding to 3 new locations. 
Could mean 50+ new employees needing coverage. 
John Smith is the decision maker. 
Follow up mid-March."

TechCorp - Critical/Risk
"URGENT: They contacted competitors today.
Our renewal is in 2 weeks. 
Need to call CFO immediately to discuss rate improvements.
May lose $50K annual revenue if we don't act."

Global Industries - General
"Updated contact info: Sarah Johnson 555-1234
Email: sarah.johnson@global.com
New title: VP of Operations"

MidSize LLC - Follow-up
"Need to schedule quarterly review meeting.
Last contact was Nov 2023. Due for updates."

StartupXYZ - Issue
"Invoice #INV-2024-001 has duplicate charge.
Amount: $500. Client disputes charge.
Need accounting review."
```

---

## 📱 Mobile View

### Mobile Card Layout
```
┌──────────────────────────────┐
│ AB  ACME Corp               │
│     Jan 15 · 2:30 PM        │
│ [High] [Opportunity]        │
├──────────────────────────────┤
│ Great opportunity for       │
│ expanding our insurance     │
│ services with ACME Corp.    │
│ They want to add 50         │
│ employees...                │
├──────────────────────────────┤
│ by System  [✏️]  [🗑️]      │
└──────────────────────────────┘
```

### Mobile Filter Stack
```
🔍 Search...

[All Priorities ✕]

[All Categories ✕]
```

---

## 🎨 Badge Colors

### Priority Badges
```
Low     - Green background, white text
Medium  - Amber background, black text
High    - Orange background, dark text
Critical - Red background, white text
```

### Category Badges
```
General      - Purple background, white text
Follow-up    - Purple background, white text
Issue        - Purple background, white text
Opportunity  - Purple background, white text
Risk         - Purple background, white text
```

---

## ⌚ Metadata Display

```
Account Name    : ACME Corp
Date Created    : January 15, 2024
Time Created    : 2:30 PM
Created By      : System (or user name)
Priority Level  : High
Category        : Opportunity
Note Content    : Full text with formatting preserved
```

---

## 🎊 Design Highlights

### Colors
```
Primary Blue    #1d4ed8  (Accents, links)
Red (Critical)  #dc2626  (Urgent items)
Green (Low)     #047857  (Low priority)
Orange (High)   #b45309  (High priority)
Gray            #6b7280  (Secondary)
```

### Typography
```
Account Name:   0.95rem, Bold, Primary
Metadata:       0.75rem, Muted
Note Content:   0.9rem, Regular, Readable
Labels:         0.8rem, Semi-bold
```

### Spacing
```
Between Cards:  0.8rem gap
Card Padding:   0.9rem
Internal Gap:   0.6rem
Filter Gap:     0.65rem
```

---

## ✨ Special Features

### Smart Account Badges
Avatar shows first letter of account name in random gradient color

### Real-Time Search
Searches across account names and note content instantly

### Multi-Filter
Combine priority + category + search for powerful queries

### Rich Metadata
Shows date, time, and creator for audit trail

### Professional Cards
Clean layout with clear visual hierarchy

---

## 🚀 Next Steps

1. **API Integration** - Connect to real backend
2. **Data Testing** - Verify with production data
3. **User Training** - Show team how to use
4. **Deployment** - Move to production
5. **Monitoring** - Track usage and performance

---

**Visual Guide Created**: 2024  
**Status**: ✅ Complete  
**Version**: 1.0
