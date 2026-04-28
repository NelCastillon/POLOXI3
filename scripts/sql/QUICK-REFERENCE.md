# Quick Reference - CRM Seed Data

## Files Overview

| File | Purpose | Safe to Run Multiple Times |
|------|---------|---------------------------|
| `01-create-tables.sql` | Create all CRM database tables | ✓ Yes (checks IF NOT EXISTS) |
| `03-seed-data-crm-3pages.sql` | Insert test data for 3 CRM pages | ✓ Yes (checks for existing data) |

## One-Line Execution

### Local SQL Server
```sql
-- Create tables
sqlcmd -S localhost -d AMS -i "01-create-tables.sql"

-- Seed data
sqlcmd -S localhost -d AMS -i "03-seed-data-crm-3pages.sql"
```

### Azure SQL Database
```sql
-- Create tables
sqlcmd -S servername.database.windows.net -d AMS -U sqluser -P password -i "01-create-tables.sql"

-- Seed data
sqlcmd -S servername.database.windows.net -d AMS -U sqluser -P password -i "03-seed-data-crm-3pages.sql"
```

## Data Summary

### What Gets Created
- **1** Tenant
- **5** Users (Producers)
- **18** Leads (mixed priority levels)
- **9** Scoring Rules
- **4** Assignment Rules
- **19** Follow-up Activities

### Lead Distribution
```
High Priority (80+):   6 leads
Medium Priority (50-79): 7 leads
Low Priority (<50):     5 leads
```

### Follow-up Activities
```
High Priority Calls:     6
Medium Priority Emails:  8
Low Priority Nurture:    5
```

## Verification Queries

### Check Table Creation
```sql
SELECT COUNT(*) AS TableCount FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' 
AND TABLE_NAME IN ('Leads', 'Users', 'LeadScoringRules', 'LeadAssignmentRules', 'LeadActivities');
```

### Check Seed Data
```sql
SELECT 
    (SELECT COUNT(*) FROM Leads) AS TotalLeads,
    (SELECT COUNT(*) FROM Users) AS TotalUsers,
    (SELECT COUNT(*) FROM LeadActivities) AS TotalActivities,
    (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRules,
    (SELECT COUNT(*) FROM LeadAssignmentRules) AS AssignmentRules;
```

### View All Leads with Scores
```sql
SELECT [LeadNumber], [FirstName], [LastName], [Score], [PriorityCode], [Email]
FROM [Leads]
ORDER BY [Score] DESC;
```

### View Assignment Rules
```sql
SELECT [RuleName], [RuleType], [Criteria], [TargetGroup], [IsActive]
FROM [LeadAssignmentRules]
ORDER BY [DisplayOrder];
```

### View Pending Follow-ups
```sql
SELECT 
    l.[LeadNumber],
    l.[FirstName] + ' ' + l.[LastName] AS LeadName,
    la.[ActivityType],
    la.[Subject],
    la.[ScheduledDateUtc],
    la.[Priority]
FROM [LeadActivities] la
JOIN [Leads] l ON la.[LeadId] = l.[Id]
WHERE la.[StatusCode] = 'Pending'
ORDER BY la.[Priority] DESC, la.[ScheduledDateUtc];
```

## Troubleshooting

### Issue: "CREATE TABLE permission denied"
**Solution:** Run as database owner or DBO
```sql
ALTER ROLE [db_owner] ADD MEMBER [username];
```

### Issue: "Lead already exists"
**Solution:** Data is safe - script checks for duplicates. Safe to re-run.

### Issue: "User not found for activities"
**Solution:** Run seed script after table creation. Order matters:
1. Run `01-create-tables.sql` first
2. Run `03-seed-data-crm-3pages.sql` second

### Issue: "Foreign key constraint violated"
**Solution:** Ensure Leads table exists before LeadActivities:
```sql
-- Check table existence
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('Leads', 'Users', 'LeadActivities');
```

## Integration with Blazor Pages

### Lead Scoring Page
Uses: `Leads`, `LeadScoringRules`
```csharp
var highScoreLeads = await context.Leads
    .Where(l => l.TenantId == tenantId && l.Score >= 80)
    .OrderByDescending(l => l.Score)
    .ToListAsync();
```

### Lead Assignment Page
Uses: `Leads`, `LeadAssignmentRules`, `Users`
```csharp
var unassignedLeads = await context.Leads
    .Where(l => l.TenantId == tenantId && l.AssignedToUserId == null)
    .OrderByDescending(l => l.Score)
    .ToListAsync();
```

### Lead Follow-up Page
Uses: `LeadActivities`, `Leads`, `Users`
```csharp
var pendingActivities = await context.LeadActivities
    .Where(la => la.TenantId == tenantId && la.StatusCode == "Pending")
    .OrderBy(la => la.ScheduledDateUtc)
    .ToListAsync();
```

## Reset/Clear Data

### Clear All Data (Careful!)
```sql
-- Disable FK constraints temporarily
ALTER TABLE [LeadActivities] NOCHECK CONSTRAINT FK_LeadActivities_Leads;
ALTER TABLE [LeadAssignmentHistory] NOCHECK CONSTRAINT FK_LeadAssignmentHistory_Leads;

-- Delete all data
DELETE FROM [LeadQualityMetrics] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [LeadActivities] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [LeadAssignmentHistory] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [Leads] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [LeadAssignmentRules] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [LeadScoringRules] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';
DELETE FROM [Users] WHERE [TenantId] = '00000000-0000-0000-0000-000000000001';

-- Re-enable FK constraints
ALTER TABLE [LeadActivities] WITH CHECK CHECK CONSTRAINT FK_LeadActivities_Leads;
ALTER TABLE [LeadAssignmentHistory] WITH CHECK CHECK CONSTRAINT FK_LeadAssignmentHistory_Leads;

-- Re-run seed script
-- EXEC sp_executesql N'...seed data script...'
```

## Performance Tips

- Seed data is minimal for testing (18 leads)
- For production testing, duplicate data:
```sql
INSERT INTO [Leads] SELECT NEWID(), * FROM [Leads] WHERE [Id] != NEWID();
```

- Create test indexes:
```sql
CREATE INDEX IX_Leads_Score_Descending ON [Leads]([Score] DESC);
CREATE INDEX IX_LeadActivities_Scheduled ON [LeadActivities]([ScheduledDateUtc]);
```

---

**Last Updated:** 2024  
**For:** AMS CRM Module - 3 Pages (Lead Scoring, Assignment, Follow-up)
