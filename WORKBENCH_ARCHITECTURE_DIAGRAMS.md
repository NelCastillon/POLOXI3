# Workbench Architecture & Flow Diagrams

## 1. Navigation Structure

```
┌─────────────────────────────────────────┐
│          MainLayout                     │
│  ┌──────────────────────────────────┐   │
│  │      NavSidebar.razor            │   │
│  │                                  │   │
│  │  ├─ Dashboard                    │   │
│  │  ├─ Platform (nested)            │   │
│  │  ├─ Dashboards                   │   │
│  │  │                               │   │
│  │  └─ ★ Workbench                  │   │
│  │     ├─ My Workbench              │   │
│  │     ├─ My Tasks                  │   │
│  │     ├─ My Activities             │   │
│  │     ├─ My Calendar               │   │
│  │     ├─ Notifications             │   │
│  │     │                             │   │
│  │     ├─ Producer Workbench ◄──────┼─┐ │
│  │     ├─ CSR Workbench ◄───────────┼─┤ │
│  │     ├─ Service Mgr Workbench ◄───┼─┤ │
│  │     ├─ Accounting Workbench ◄────┼─┤ │
│  │     ├─ Marketing Workbench ◄─────┼─┤ │
│  │     └─ Operations Workbench ◄────┼─┤ │
│  │                                  │ │ │
│  │  ├─ CRM                          │ │ │
│  │  ├─ Accounts                     │ │ │
│  │  ├─ Policies                     │ │ │
│  │  └─ ... (other modules)          │ │ │
│  │                                  │ │ │
│  └──────────────────────────────────┘ │ │
│                                        │ │
│  ┌──────────────────────────────────┐ │ │
│  │      @Body Outlet                │ │ │
│  │  ┌────────────────────────────┐  │ │ │
│  │  │  Routed Component      ◄───┼─┘ │
│  │  │  (Workbench Page)          │  │ │
│  │  └────────────────────────────┘  │ │
│  └──────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

---

## 2. Route Mapping

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Routes.razor (Router)                        │
│         Auto-discovers @page directives from components             │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
        ┌───────────────────────────────────────────────────┐
        │         Blazor Component Discovery                │
        ├───────────────────────────────────────────────────┤
        │                                                   │
        │  @page "/workbench/producer"          ────────┐  │
        │  ↓ ProducerWorkbench.razor                    │  │
        │                                               │  │
        │  @page "/workbench/csr"               ────┐   │  │
        │  ↓ CsrWorkbench.razor                  │   │  │
        │                                        │   │  │
        │  @page "/workbench/service-manager"    ├─┐ │  │
        │  ↓ ServiceManagerWorkbench.razor       │ │ │  │
        │                                        │ │ │  │
        │  @page "/workbench/accounting"     ────┤ │ │  │
        │  ↓ AccountingWorkbench.razor           │ │ │  │
        │                                        │ │ │  │
        │  @page "/workbench/marketing"          ├─┤ │  │
        │  ↓ MarketingWorkbench.razor            │ │ │  │
        │                                        │ │ │  │
        │  @page "/workbench/operations"     ────┤ │ │  │
        │  ↓ OperationsWorkbench.razor           │ │ │  │
        │                                        │ │ │  │
        └────────────────────────────────────────┼─┼─┼──┘
                                                 ↓ ↓ ↓
```

---

## 3. Component Hierarchy - Producer Workbench

```
┌──────────────────────────────────────────────────────┐
│         ProducerWorkbench.razor                      │
│  @page "/workbench/producer"                         │
│  @inject ApiClient, NavigationManager, etc.          │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ <AppPageHeader />                          │     │
│  │  ├─ Title: "Producer Workbench"            │     │
│  │  ├─ Subtitle: "Your personal revenue..."  │     │
│  │  ├─ Icon: "bi-person-badge"                │     │
│  │  └─ <Actions>                              │     │
│  │      ├─ Refresh button                     │     │
│  │      ├─ Export button                      │     │
│  │      └─ New Lead button                    │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ Goal Progress Strip                        │     │
│  │ ├─ WrittenPremium: $X / $Goal              │     │
│  │ ├─ NewPolicies: N                          │     │
│  │ ├─ RetentionRate: X%                       │     │
│  │ ├─ PipelineValue: $X                       │     │
│  │ └─ UnreadMessages: N                       │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ KPI Strip (6 interactive cards)            │     │
│  │ ├─ [1] Assigned Leads                      │     │
│  │ ├─ [2] Open Opportunities                  │     │
│  │ ├─ [3] Quote Follow-ups                    │     │
│  │ ├─ [4] Renewal Call List                   │     │
│  │ ├─ [5] Cross-sell List                     │     │
│  │ └─ [6] Unread Messages                     │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ Queue Tab Bar (clickable)                  │     │
│  │ [Leads] [Opportunities] [Quotes]...        │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ Filter Row                                 │     │
│  │ [Search___] [LoB dropdown] [Stage dd]      │     │
│  │ [☑ Hot/urgent only]                        │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ Content Panel (switches based on queue)    │     │
│  │                                            │     │
│  │ IF queue == "leads":                       │     │
│  │  ├─ <AppGrid>                              │     │
│  │  │  ├─ Heat | Lead Name | LoB | Source   │     │
│  │  │  ├─ Est.Prem | LastContact | Action   │     │
│  │  │  └─ Due | [>]                          │     │
│  │  └─ </AppGrid>                             │     │
│  │                                            │     │
│  │ IF queue == "opportunities":                │     │
│  │  ├─ [Board view] [Grid view] toggle       │     │
│  │  │                                         │     │
│  │  │ IF oppView == "kanban":                │     │
│  │  │  └─ <enterprise kanban board>                         │     │
│  │  │     ├─ Prospect | Qualified | ...      │     │
│  │  │     └─ [Cards draggable]               │     │
│  │  │                                        │     │
│  │  │ IF oppView == "grid":                  │     │
│  │  │  └─ <AppGrid>                           │     │
│  │  │     ├─ Stage | Opportunity | Account   │     │
│  │  │     └─ WinProb | Amount | CloseDate    │     │
│  │  │                                        │     │
│  │  └─ </AppGrid or enterprise kanban board>                 │     │
│  │                                            │     │
│  │ ... other queue templates                 │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ Detail Panel (RenderFragment)              │     │
│  │ [Shows when _selected is not null]         │     │
│  │                                            │     │
│  │ <div class="wb-overlay">                  │     │
│  │ <div class="wb-detail-panel">             │     │
│  │  ├─ Header: [Status badges] [X close]     │     │
│  │  ├─ Title: Item name                      │     │
│  │  ├─ Body: Property rows                   │     │
│  │  │  ├─ Ref: XXX-123                       │     │
│  │  │  ├─ Account: Account Name              │     │
│  │  │  ├─ LoB: Commercial                    │     │
│  │  │  ├─ Est Premium: $50,000                │     │
│  │  │  └─ ...                                │     │
│  │  └─ Footer: [Open Record] button          │     │
│  │ </div>                                     │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  @code {                                             │
│    private ProducerCounts _counts;                   │
│    private List<ProducerItem> _leads;               │
│    private List<ProducerItem> _opportunities;       │
│    private List<ProducerItem> _quotes;              │
│    ... (other data)                                  │
│  }                                                   │
└──────────────────────────────────────────────────────┘
```

---

## 4. Component Hierarchy - CSR Workbench (using WorkbenchShell)

```
┌──────────────────────────────────────────────────────┐
│          CsrWorkbench.razor                          │
│  @page "/workbench/csr"                              │
│                                                      │
│  <WorkbenchShell                                     │
│      Title="CSR Workbench"                           │
│      Subtitle="Customer service queues..."           │
│      Icon="bi-headset"                               │
│      @bind-Scope="scope"                             │
│      BranchId="branchId"                             │
│      ... >                                           │
│                                                      │
│      <ExtraActions>                                  │
│          <button>New Request</button>                │
│      </ExtraActions>                                 │
│                                                      │
│      <Queues>                                        │
│          ├─ KPI Strip (6 cards)                     │
│          │  ├─ Service Requests [6]                 │
│          │  ├─ Endorsements [3]                     │
│          │  ├─ Certificates [2]                     │
│          │  ├─ Billing Enquiries [1]                │
│          │  ├─ Complaints [1]                       │
│          │  └─ Follow-ups [5] (2 overdue)           │
│          │                                          │
│          ├─ Queue Tabs                              │
│          │  [Service Requests] [Endorsements] ...   │
│          │                                          │
│          ├─ Filter Row                              │
│          │  [Search] [Priority] [SLA Status]        │
│          │                                          │
│          └─ Queue Grid                              │
│             (renders based on _activeQueue)         │
│             ├─ Service Requests Grid                │
│             ├─ Endorsements Grid                    │
│             ├─ Certificates Grid                    │
│             ├─ Billing Enquiries Grid               │
│             ├─ Complaints Grid                      │
│             └─ Follow-ups Grid                      │
│                                                      │
│      </Queues>                                       │
│  </WorkbenchShell>                                   │
│                                                      │
│  ┌──────────────────────────────────────────┐       │
│  │     WorkbenchShell Internal Structure:   │       │
│  │                                          │       │
│  │  <header>                                │       │
│  │    [Title] [AI Summary] [Refresh] [Save]│       │
│  │  </header>                               │       │
│  │                                          │       │
│  │  <aside>                                 │       │
│  │    [Scope Selector]                      │       │
│  │    [Branch/Team Filter]                  │       │
│  │    [Saved Views List]                    │       │
│  │    [SLA Heat Map]                        │       │
│  │  </aside>                                │       │
│  │                                          │       │
│  │  <main>                                  │       │
│  │    [@RenderBody (Queues content)]        │       │
│  │  </main>                                 │       │
│  │                                          │       │
│  │  <DetailPanel (RenderFragment)>          │       │
│  │    [Overlay + Detail panel]              │       │
│  │  </DetailPanel>                          │       │
│  └──────────────────────────────────────────┘       │
│                                                      │
└──────────────────────────────────────────────────────┘
```

---

## 5. Data Flow - User Interaction Sequence

```
User Action: Click "CSR Workbench" link in sidebar
        ↓
Navigation to /workbench/csr
        ↓
Routes.razor routes to CsrWorkbench.razor
        ↓
Component initializes: OnInitializedAsync()
        │
        ├─→ Breadcrumbs.SetCrumbs()
        │   └─→ Display: "Home > CSR Workbench"
        │
        └─→ LoadAsync()
            ├─→ Set _loading = true
            │
            ├─→ _serviceRequests = await Api.GetServiceRequestsAsync()
            ├─→ _endorsements = await Api.GetEndorsementsAsync()
            ├─→ _certificates = await Api.GetCertificatesAsync()
            ├─→ _complaints = await Api.GetComplaintsAsync()
            ├─→ _followUps = await Api.GetFollowUpsAsync()
            │
            ├─→ _c = new CsrCounts()
            │   ├─→ _c.ServiceRequests = _serviceRequests.Count
            │   ├─→ _c.Endorsements = _endorsements.Count
            │   └─→ ... (count all items)
            │
            ├─→ RebuildSla()
            │   └─→ Create SLA heat items
            │
            ├─→ Set _loading = false
            │
            └─→ StateHasChanged() → Component re-renders
                    ↓
        Page displays with KPI cards populated:
        ├─ Service Requests: 6
        ├─ Endorsements: 3
        ├─ Certificates: 2
        ├─ Billing Enquiries: 1
        ├─ Complaints: 1
        └─ Follow-ups: 5 (2 overdue)
```

---

## 6. Data Flow - Queue Tab Switch

```
User Action: Click "Endorsements" tab
        ↓
SetActiveQueue("endorsements") called
        ↓
_activeQueue = "endorsements"
        ↓
StateHasChanged()
        ↓
Component re-renders
        ↓
Conditional check: @if (_activeQueue == "endorsements")
        ├─ TRUE: Render endorsements grid
        │        └─ <AppGrid DataSource="@Filtered(_endorsements)" />
        │           ├─ Apply search filter
        │           ├─ Apply priority filter
        │           ├─ Apply SLA filter
        │           └─ Display filtered results
        │
        └─ FALSE: Skip rendering other queues
```

---

## 7. Data Flow - Detail Panel Open

```
User Action: Click row in grid
        ↓
OpenDetail(item) called
        ↓
_selected = item
        ↓
StateHasChanged()
        ↓
Component re-renders
        ↓
DetailPanel() RenderFragment executes
        ├─ @if (_selected is not null)
        │  ├─ Render overlay: <div class="wb-overlay">
        │  └─ Render detail panel:
        │     ├─ Header with status badges
        │     ├─ Title from item
        │     ├─ Body with property rows
        │     └─ Footer with "Open Record" button
        │
        └─ @else
           └─ No content rendered
```

---

## 8. Filtering Logic Flow

```
User types in search box: "XYZ Corp"
        ↓
@oninput event fires
        ↓
_search = "XYZ Corp"
        ↓
StateHasChanged()
        ↓
Grid re-renders
        ↓
Grid calls: DataSource="@Filtered(_endorsements)"
        ↓
Filtered() method executes:
        ├─ Loop through _endorsements
        ├─ For each item:
        │  ├─ Check: _search in item.Title?
        │  ├─ Check: _search in item.AccountName?
        │  ├─ Check: _search in item.RefNumber?
        │  └─ If ANY match: include item
        │
        └─ Return filtered list
        ↓
Grid displays matching items only
```

---

## 9. Workbench Comparison Matrix

```
┌─────────────────┬──────────┬──────────┬────────────┬───────────┬───────────┬─────────────┐
│ Feature         │ Producer │ CSR      │ Service    │ Accounting│ Marketing │ Operations  │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Main Component  │ Standalone│WorkbenchShell   │Standalone│         WorkbenchShell        │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Page Header     │ AppPage  │Workbench│Workbench   │ Workbench │Workbench  │AppPageHeader│
│                 │ Header   │Shell    │Shell       │ Shell     │Shell      │             │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Queues          │ 6        │ 6        │ Multi      │ 6         │ 6         │ 7           │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Primary Grid    │ AppGrid   │ AppGrid   │ AppGrid     │ AppGrid    │ AppGrid    │ AppGrid      │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Alt View        │ enterprise kanban board │ None     │ None       │ None      │ None      │ None        │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ Detail Panel    │ RenderFrag│RenderFrag│RenderFrag │ RenderFrag│RenderFrag │ RenderFrag  │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ View Persistence│ Shell    │ Shell    │ Shell      │ Shell     │ Shell     │ Custom      │
├─────────────────┼──────────┼──────────┼────────────┼───────────┼───────────┼─────────────┤
│ AI Summary      │ Shell    │ Shell    │ Shell      │ Shell     │ Shell     │ Custom      │
└─────────────────┴──────────┴──────────┴────────────┴───────────┴───────────┴─────────────┘
```

---

## 10. State Management Flow

```
┌─────────────────────────────────────────────────────────────┐
│        Component-Level State (no external services)         │
└─────────────────────────────────────────────────────────────┘

Private Fields in Each Component:

User Actions:
    ├─ _search (string) ───────────────────→ Filter input
    ├─ _activeQueue (string) ──────────────→ Current queue tab
    ├─ _selected (WbItem?) ────────────────→ Detail panel item
    ├─ _filterPriority (string) ───────────→ Priority filter
    ├─ _filterSla (string) ────────────────→ SLA filter
    ├─ _loading (bool) ────────────────────→ Load spinner
    ├─ _scope (string) ────────────────────→ Scope selection
    ├─ _branchId (string) ─────────────────→ Branch selection
    └─ _teamId (string) ───────────────────→ Team selection

Data Collections:
    ├─ List<T> _queue1 ───────────────────→ Queue data
    ├─ List<T> _queue2 ───────────────────→ Queue data
    ├─ ... _queueN ────────────────────────→ Queue data
    └─ XxxCounts _c ───────────────────────→ KPI counters

UI State:
    ├─ string? _aiSummary ────────────────→ AI-generated summary
    ├─ bool _aiLoading ────────────────────→ AI generation progress
    ├─ DateTime? _aiGeneratedAt ──────────→ AI summary timestamp
    └─ List<SavedView> _savedViews ───────→ Persisted views

View Model:
    └─ RenderFragment DetailPanel() ──────→ Conditional rendering
                └─ Depends on: _selected

Data Binding:
    ├─ Two-way: @bind="_search"
    ├─ Two-way: @bind="_filterPriority"
    ├─ One-way: DataSource="@_queue"
    └─ Event: @oninput="OnSearchChanged"

State Update Cycle:
    User Action
        ↓
    Event Handler Updates Field
        ↓
    StateHasChanged() [explicit or implicit]
        ↓
    Component Re-renders
        ↓
    Conditionals Re-evaluate
        ↓
    Bindings Update
        ↓
    UI Reflects New State
```

---

## 11. Routing & Navigation Path

```
URL Structure:

Browser URL Bar
    │
    ├─→ /workbench
    │       └─→ Redirects to /workbench/producer or user's default
    │
    ├─→ /workbench/producer
    │       └─→ Routes to ProducerWorkbench.razor
    │
    ├─→ /workbench/csr
    │       └─→ Routes to CsrWorkbench.razor
    │
    ├─→ /workbench/service-manager
    │       └─→ Routes to ServiceManagerWorkbench.razor
    │
    ├─→ /workbench/accounting
    │       └─→ Routes to AccountingWorkbench.razor
    │
    ├─→ /workbench/marketing
    │       └─→ Routes to MarketingWorkbench.razor
    │
    └─→ /workbench/operations
            └─→ Routes to OperationsWorkbench.razor

Navigation Methods:

1. Sidebar Click
   NavSidebar.razor
       └─→ <NavLink href="/workbench/producer">
           └─→ Triggers navigation
           └─→ Routes to component

2. Detail Panel Open
   Nav.NavigateTo("/leads/{id}")
   Nav.NavigateTo("/endorsements/{id}")
   etc.

3. Action Buttons
   <button @onclick='() => Nav.NavigateTo("/leads/new")'>
       └─→ Navigate to new item form
```

---

## 12. CSS Class Hierarchy

```
Workbench Main Container
    └─ .wb-workbench
        ├─ .wb-header
        │   ├─ .app-page-header
        │   └─ .wb-actions
        │
        ├─ .wb-body
        │   ├─ .wb-kpi-strip
        │   │   ├─ .wb-kpi-card
        │   │   │   ├─ .wb-kpi-card--active
        │   │   │   ├─ .wb-kpi-icon
        │   │   │   ├─ .wb-kpi-value
        │   │   │   ├─ .wb-kpi-value--alert
        │   │   │   └─ .wb-kpi-value--warn
        │   │   └─ .wb-kpi-dot
        │   │
        │   ├─ .wb-queue-tabs
        │   │   ├─ .wb-queue-tab
        │   │   ├─ .wb-queue-tab--active
        │   │   └─ .wb-tab-badge
        │   │
        │   ├─ .wb-filter-row
        │   │   ├─ .wb-search-box
        │   │   └─ [dropdown controls]
        │   │
        │   └─ .wb-grid-card
        │       ├─ app-datagrid
        │       ├─ [AppGrid rendered here]
        │       │   ├─ .wb-pri (Priority badge)
        │       │   │   ├─ .wb-pri--critical
        │       │   │   ├─ .wb-pri--urgent
        │       │   │   ├─ .wb-pri--high
        │       │   │   └─ .wb-pri--normal
        │       │   │
        │       │   ├─ .wb-sla (SLA badge)
        │       │   │   ├─ .wb-sla--ok
        │       │   │   ├─ .wb-sla--warn
        │       │   │   └─ .wb-sla--breach
        │       │   │
        │       │   ├─ .wb-age (Age badge)
        │       │   │   ├─ .wb-age--ok
        │       │   │   ├─ .wb-age--mid
        │       │   │   └─ .wb-age--old
        │       │   │
        │       │   └─ .wb-mono (Monospace)
        │       │
        │       └─ .wb-loading
        │
        └─ .wb-detail-panel
            ├─ .wb-overlay
            ├─ .wb-detail-hdr
            ├─ .wb-detail-title
            ├─ .wb-detail-body
            │   └─ .wb-detail-row
            │       └─ .wb-detail-lbl
            ├─ .wb-detail-notes
            └─ .wb-detail-footer
```

---

## 13. Component Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│           Blazor Component Lifecycle                        │
│              (Per Workbench Component)                      │
└─────────────────────────────────────────────────────────────┘

1. SetParametersAsync()
   └─→ Receive parameters from parent (if any)

2. OnInitialized / OnInitializedAsync()
   ├─→ Initialize component state
   ├─→ Set breadcrumbs
   ├─→ Start data loading
   └─→ First StateHasChanged() (automatic)

3. OnParametersSet / OnParametersSetAsync()
   ├─→ React to parameter changes
   └─→ (Usually called again on navigation)

4. OnAfterRender / OnAfterRenderAsync()
   ├─→ Component rendered in DOM
   ├─→ Safe to use JS interop
   └─→ Typically: setState({ firstRender: false })

5. Event Handlers
   ├─→ User clicks queue tab → SetActiveQueue()
   ├─→ User types in search → OnSearchInput()
   ├─→ User clicks row → OpenDetail()
   ├─→ User clicks "New" → Nav.NavigateTo()
   └─→ User filters → StateHasChanged()

6. StateHasChanged() [when called]
   ├─→ Component marks as dirty
   ├─→ Blazor queues re-render
   └─→ Update occurs after event handler completes

7. Component Rendering
   ├─→ Evaluate all @if/@foreach conditionals
   ├─→ Bind all @bind values
   ├─→ Attach all @onXXX event handlers
   └─→ Output new HTML to DOM

8. Dispose [when navigation away]
   ├─→ IDisposable.Dispose() called
   └─→ (Usually clean up event subscriptions)

Repeat cycle on any user interaction...
```

---

## 14. Data Loading Process

```
LoadAsync() Method Execution:

START: LoadAsync()
    │
    ├─→ _loading = true
    ├─→ StateHasChanged() → Show spinner
    │
    ├─→ Clear data:
    │   ├─ _queue1.Clear()
    │   ├─ _queue2.Clear()
    │   └─ ... _queueN.Clear()
    │
    ├─→ Initialize counts:
    │   └─ _c = new XxxCounts { ... }
    │
    ├─→ Load queue data [can be from API or mock]:
    │   ├─ _queue1 = await Api.GetQueue1Async(...)
    │   ├─ _queue2 = await Api.GetQueue2Async(...)
    │   └─ ... _queueN = await Api.GetQueueNAsync(...)
    │
    ├─→ Populate counts:
    │   ├─ _c.Queue1Count = _queue1.Count
    │   ├─ _c.Queue2Count = _queue2.Count
    │   └─ ... _c.QueueNCount = _queueN.Count
    │
    ├─→ Rebuild SLA heat map:
    │   └─ RebuildSla() → Populate _slaItems
    │
    ├─→ _loading = false
    ├─→ StateHasChanged() → Hide spinner, show data
    │
    └─→ END: LoadAsync()
        └─→ Return to caller

Exception Handling:
    ├─→ try { LoadAsync(); }
    ├─→ catch (Exception ex) { ShowError(ex); }
    └─→ finally { _loading = false; StateHasChanged(); }
```

---

## Summary

The workbench architecture follows these key patterns:

1. **Routing**: Direct @page routes for each workbench
2. **Navigation**: Sidebar menu with hierarchical structure
3. **Components**: Standalone (Producer, Operations) or Shell-based (CSR, Accounting, Marketing, Service Manager)
4. **State**: Component-level with no external state services
5. **Data**: Collections populated from API or mock data
6. **UI**: Conditional rendering based on _activeQueue
7. **Filtering**: Client-side Filtered() method
8. **Detail View**: RenderFragment for conditional panel display
9. **Events**: Standard Blazor event handling
10. **Styling**: CSS classes with consistent naming

All workbenches follow these patterns ensuring consistency, maintainability, and scalability.

