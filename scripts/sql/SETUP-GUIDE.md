# AMS CRM Database - Setup & Integration Guide

## Quick Start (5 Minutes)

### Option 1: PowerShell (Recommended)
```powershell
# Make script executable (first time only)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Run full setup
cd "C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql"
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -All
```

### Option 2: SQL Server Management Studio (SSMS)
```sql
-- Open SSMS and connect to your database
-- File → Open → Script File
-- Select: 01-create-tables.sql
-- Execute (Ctrl+E)

-- Then:
-- File → Open → Script File
-- Select: 03-seed-data-crm-3pages.sql
-- Execute (Ctrl+E)
```

### Option 3: Command Line (sqlcmd)
```batch
REM Windows Command Prompt
cd C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql

REM Create tables
sqlcmd -S localhost -d AMS -i 01-create-tables.sql

REM Seed data
sqlcmd -S localhost -d AMS -i 03-seed-data-crm-3pages.sql
```

## PowerShell Script Usage

### Full Documentation
```powershell
Get-Help .\Setup-CrmDatabase.ps1 -Full
```

### Examples

#### Local SQL Server - Create Everything
```powershell
.\Setup-CrmDatabase.ps1 `
    -ServerName localhost `
    -DatabaseName AMS `
    -All
```

#### Local SQL Server - Tables Only
```powershell
.\Setup-CrmDatabase.ps1 `
    -ServerName localhost `
    -DatabaseName AMS `
    -CreateTables
```

#### Local SQL Server - Seed Data Only
```powershell
.\Setup-CrmDatabase.ps1 `
    -ServerName localhost `
    -DatabaseName AMS `
    -SeedData `
    -ShowVerification
```

#### Azure SQL Database
```powershell
.\Setup-CrmDatabase.ps1 `
    -ServerName "myserver.database.windows.net" `
    -DatabaseName AMS `
    -Username sqladmin `
    -Password "YourPassword123!" `
    -All
```

#### With Results Verification
```powershell
.\Setup-CrmDatabase.ps1 `
    -ServerName localhost `
    -DatabaseName AMS `
    -CreateTables `
    -SeedData `
    -ShowVerification
```

## What Gets Created

### Database Tables (9 total)

```
Tenants
├─ Id (PK)
├─ TenantCode
├─ TenantName
└─ StatusCode

Users
├─ Id (PK)
├─ TenantId (FK)
├─ UserName (UX)
├─ Email
├─ FullName
├─ JobTitle
└─ Department

Leads
├─ Id (PK)
├─ TenantId (FK)
├─ LeadNumber (UX)
├─ FirstName
├─ LastName
├─ Email
├─ Score
├─ PriorityCode
├─ SourceCode
├─ AssignedToUserId (FK)
└─ StatusCode

LeadActivities (Follow-ups)
├─ Id (PK)
├─ LeadId (FK)
├─ ActivityType
├─ Subject
├─ ScheduledDateUtc
├─ Priority
└─ StatusCode

LeadScoringRules
├─ Id (PK)
├─ RuleName
├─ RuleType
├─ Points
├─ Condition
└─ IsActive

LeadAssignmentRules
├─ Id (PK)
├─ RuleName
├─ RuleType
├─ Criteria
├─ TargetGroup
└─ IsActive

LeadAssignmentHistory
├─ Id (PK)
├─ LeadId (FK)
├─ AssignedToUserId (FK)
└─ AssignmentDateUtc

LeadQualityMetrics
├─ Id (PK)
├─ LeadId (FK)
├─ CompletedActivities
├─ TotalActivities
└─ ResponseRate

Accounts
├─ Id (PK)
├─ AccountNumber
├─ AccountName
└─ AccountTypeCode
```

### Test Data

**1 Tenant:** Default Tenant

**5 Producers:**
- John Spencer (Senior Producer)
- Amanda Hayes (Producer)
- Ryan Mitchell (Account Executive)
- Jessica Brown (Producer)
- Thomas Anderson (Senior Producer)

**18 Leads:**
- 6 High Priority (Score 80+)
- 7 Medium Priority (Score 50-79)
- 5 Low Priority (Score <50)

**9 Scoring Rules:**
- Demo Request (20 pts)
- Form Submission (15 pts)
- Industry Match (10 pts)
- Website Visit (10 pts)
- Company Size (8 pts)
- Page Downloads (7 pts)
- Email Opens (5 pts)
- Recent Activity (5 pts)
- LinkedIn Connection (3 pts)

**4 Assignment Rules:**
- High-Score Auto Assign
- Round-Robin Distribution
- Medium Priority Assignment
- Nurture Queue Assignment

**19 Follow-up Activities:**
- 6 High Priority Calls
- 8 Medium Priority Emails
- 5 Low Priority Nurture Emails

## Integration with Blazor Pages

### Page 1: Lead Scoring

**File:** `Pages/Crm/LeadScoringPage.razor`

**Uses Tables:**
- `Leads` - Get leads with scores
- `LeadScoringRules` - Display scoring rules

**Sample Code:**
```csharp
@page "/crm/leads/scoring"
@inject ILeadService LeadService

<div class="page">
    <h1>Lead Scoring</h1>

    @if (leads != null)
    {
        <table>
            <thead>
                <tr>
                    <th>Lead Number</th>
                    <th>Name</th>
                    <th>Email</th>
                    <th>Score</th>
                    <th>Priority</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var lead in leads)
                {
                    <tr>
                        <td>@lead.LeadNumber</td>
                        <td>@lead.FirstName @lead.LastName</td>
                        <td>@lead.Email</td>
                        <td>@lead.Score</td>
                        <td>@lead.PriorityCode</td>
                    </tr>
                }
            </tbody>
        </table>
    }
</div>

@code {
    private List<Lead> leads;

    protected override async Task OnInitializedAsync()
    {
        leads = await LeadService.GetLeadsOrderedByScore();
    }
}
```

**Service Implementation:**
```csharp
public interface ILeadService
{
    Task<List<Lead>> GetLeadsOrderedByScore();
    Task<List<LeadScoringRule>> GetScoringRules();
}

public class LeadService : ILeadService
{
    private readonly IApplicationDbContext _context;

    public async Task<List<Lead>> GetLeadsOrderedByScore()
    {
        return await _context.Leads
            .OrderByDescending(l => l.Score)
            .ToListAsync();
    }

    public async Task<List<LeadScoringRule>> GetScoringRules()
    {
        return await _context.LeadScoringRules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Points)
            .ToListAsync();
    }
}
```

### Page 2: Lead Assignment

**File:** `Pages/Crm/LeadAssignmentPage.razor`

**Uses Tables:**
- `Leads` - Get unassigned leads
- `Users` - Get available producers
- `LeadAssignmentRules` - Get assignment rules
- `LeadAssignmentHistory` - Track assignments

**Sample Code:**
```csharp
@page "/crm/leads/assignment"
@inject ILeadAssignmentService AssignmentService

<div class="page">
    <h1>Lead Assignment</h1>

    <div class="assignment-rules">
        @if (rules != null)
        {
            <h3>Active Rules</h3>
            @foreach (var rule in rules)
            {
                <div class="rule">
                    <h4>@rule.RuleName</h4>
                    <p>@rule.Criteria</p>
                    <button @onclick="() => ApplyRule(rule)">Apply Rule</button>
                </div>
            }
        }
    </div>

    <div class="unassigned-leads">
        @if (unassignedLeads != null)
        {
            <h3>Unassigned Leads (@unassignedLeads.Count)</h3>
            <table>
                <thead>
                    <tr>
                        <th>Lead</th>
                        <th>Score</th>
                        <th>Assign To</th>
                        <th>Action</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var lead in unassignedLeads)
                    {
                        <tr>
                            <td>@lead.FirstName @lead.LastName</td>
                            <td>@lead.Score</td>
                            <td>
                                <select @bind="selectedProducers[lead.Id]">
                                    <option value="">-- Select Producer --</option>
                                    @foreach (var producer in producers)
                                    {
                                        <option value="@producer.Id">@producer.FullName</option>
                                    }
                                </select>
                            </td>
                            <td>
                                <button @onclick="() => AssignLead(lead)">Assign</button>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>

@code {
    private List<Lead> unassignedLeads;
    private List<User> producers;
    private List<LeadAssignmentRule> rules;
    private Dictionary<Guid, string> selectedProducers = new();

    protected override async Task OnInitializedAsync()
    {
        unassignedLeads = await AssignmentService.GetUnassignedLeads();
        producers = await AssignmentService.GetProducers();
        rules = await AssignmentService.GetAssignmentRules();
    }

    private async Task AssignLead(Lead lead)
    {
        if (selectedProducers.TryGetValue(lead.Id, out var producerId) && !string.IsNullOrEmpty(producerId))
        {
            await AssignmentService.AssignLead(lead.Id, Guid.Parse(producerId));
            unassignedLeads = await AssignmentService.GetUnassignedLeads();
        }
    }
}
```

### Page 3: Lead Follow-up

**File:** `Pages/Crm/LeadFollowUpPage.razor`

**Uses Tables:**
- `LeadActivities` - Get follow-up activities
- `Leads` - Get lead details
- `Users` - Get assigned users

**Sample Code:**
```csharp
@page "/crm/leads/follow-up"
@inject ILeadActivityService ActivityService

<div class="page">
    <h1>Lead Follow-up</h1>

    <div class="filters">
        <select @onchange="FilterByPriority">
            <option value="">All Priorities</option>
            <option value="High">High Priority</option>
            <option value="Medium">Medium Priority</option>
            <option value="Low">Low Priority</option>
        </select>
    </div>

    @if (pendingActivities != null)
    {
        <div class="activities-by-priority">
            <h3>High Priority (@GetCountByPriority("High"))</h3>
            <ActivityList Activities="GetActivitiesByPriority('High')" />

            <h3>Medium Priority (@GetCountByPriority("Medium"))</h3>
            <ActivityList Activities="GetActivitiesByPriority('Medium')" />

            <h3>Low Priority (@GetCountByPriority("Low"))</h3>
            <ActivityList Activities="GetActivitiesByPriority('Low')" />
        </div>
    }
</div>

@code {
    private List<LeadActivityDto> pendingActivities;

    protected override async Task OnInitializedAsync()
    {
        pendingActivities = await ActivityService.GetPendingActivities();
    }

    private IEnumerable<LeadActivityDto> GetActivitiesByPriority(string priority)
    {
        return pendingActivities?.Where(a => a.Priority == priority) ?? new List<LeadActivityDto>();
    }

    private int GetCountByPriority(string priority)
    {
        return GetActivitiesByPriority(priority).Count();
    }
}
```

## Verification Commands

### Check Table Creation
```sql
SELECT COUNT(*) AS Tables FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN 
('Leads', 'Users', 'LeadScoringRules', 'LeadAssignmentRules', 'LeadActivities');
```

### Check Seed Data
```sql
SELECT 
    (SELECT COUNT(*) FROM Leads) AS Leads,
    (SELECT COUNT(*) FROM Users) AS Users,
    (SELECT COUNT(*) FROM LeadActivities) AS Activities,
    (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRules,
    (SELECT COUNT(*) FROM LeadAssignmentRules) AS AssignmentRules;
```

### View All Leads
```sql
SELECT LeadNumber, FirstName, LastName, Score, PriorityCode, SourceCode
FROM Leads
ORDER BY Score DESC;
```

### View Pending Follow-ups
```sql
SELECT 
    l.LeadNumber,
    l.FirstName + ' ' + l.LastName AS LeadName,
    la.ActivityType,
    la.ScheduledDateUtc,
    la.Priority
FROM LeadActivities la
JOIN Leads l ON la.LeadId = l.Id
WHERE la.StatusCode = 'Pending'
ORDER BY la.Priority DESC;
```

## Troubleshooting

### SqlServer Module Not Found
```powershell
Install-Module -Name SqlServer -Force -AllowClobber
Import-Module SqlServer
```

### Access Denied Error
- Ensure you have database owner (dbo) permissions
- Or run script as administrator

### Tables Already Exist
- Script checks for existing tables with IF NOT EXISTS
- Safe to run multiple times
- Use provided verification queries to check current state

### Data Not Appearing
1. Verify table creation ran successfully
2. Check TenantId matches in seed script (default: '00000000-0000-0000-0000-000000000001')
3. Review SQL errors in output

## Next Steps

1. ✓ Run database setup script
2. ✓ Verify tables and data
3. □ Update Entity Framework DbContext
4. □ Create/Update Blazor pages
5. □ Add services (ILeadService, ILeadAssignmentService, etc.)
6. □ Build and run application

## Support

For issues:
1. Check README.md for full documentation
2. Review QUICK-REFERENCE.md for common tasks
3. Check verification queries above
4. Review application logs for Entity Framework errors

---

**Version:** 1.0  
**Framework:** .NET 9  
**Database:** SQL Server 2019+ / Azure SQL Database
