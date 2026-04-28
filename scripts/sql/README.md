# AMS Database SQL Scripts & Seed Data

## Overview
This directory contains SQL scripts for the AgencyBinder Management System (AMS) database, specifically designed for the CRM module with focus on three core pages:
- **Lead Scoring** - Evaluate and score leads based on engagement and behavioral metrics
- **Lead Assignment** - Automatically distribute leads to producers using configurable rules
- **Lead Follow-up** - Track and manage follow-up activities for leads

## Files

### 1. `01-create-tables.sql`
Creates the complete database schema for the CRM module.

**Tables Created:**
- `Tenants` - Multi-tenant organization support
- `Users` - System users (Producers, Team Members)
- `Leads` - Lead records with scoring and assignment data
- `LeadScoringRules` - Business rules for lead scoring
- `LeadAssignmentRules` - Rules for automatic lead distribution
- `LeadActivities` - Follow-up activities and tasks for leads
- `LeadAssignmentHistory` - Audit trail for lead assignments
- `LeadQualityMetrics` - Performance metrics for leads
- `Accounts` - Customer accounts

**Features:**
- Proper indexing for performance
- Foreign key relationships
- Audit fields (CreatedDate, ModifiedDate, CreatedBy, ModifiedBy)
- Soft-delete support (IsDeleted column)
- Multi-tenant isolation via TenantId

### 2. `03-seed-data-crm-3pages.sql`
Production-ready seed data specifically for the three CRM pages.

**Data Included:**

#### Tenants (1)
- Default Tenant for testing

#### Users (5 Producers)
- John Spencer - Senior Producer
- Amanda Hayes - Producer
- Ryan Mitchell - Account Executive
- Jessica Brown - Producer
- Thomas Anderson - Senior Producer

#### Leads (18 Total)
- **High Priority (6 leads):** Score 81-91
- **Medium Priority (7 leads):** Score 65-78
- **Low Priority (5 leads):** Score 48-64

Leads include:
- Diverse contact information
- Various service interests (General Liability, Property, Workers Comp, etc.)
- Multiple sources (Web, Direct, Referral, Partner, Organic)
- Nurturing stages (Active, Nurture)

#### Scoring Rules (9)
- Demo Request (20 points)
- Form Submission (15 points)
- Industry Match (10 points)
- Website Visit (10 points)
- Company Size (8 points)
- Page Downloads (7 points)
- Email Opens (5 points)
- Recent Activity (5 points)
- LinkedIn Connection (3 points)

#### Assignment Rules (4)
- High-Score Auto Assign (Score ≥ 80 → Senior Producers)
- Round-Robin Distribution (All Leads → All Producers)
- Medium Priority Assignment (Score 50-79 → All Producers)
- Nurture Queue Assignment (Score < 50 → Nurture Team)

#### Follow-up Activities (19)
- **High Priority Calls (6):** For leads with score ≥ 85
- **Medium Priority Emails (8):** For leads with score 65-79
- **Nurture Emails (5):** For leads with score < 65

**Safety Features:**
- Checks for existing data before inserting
- Won't create duplicates if run multiple times
- Detailed logging of all operations
- Error handling with try-catch blocks

## Usage

### Prerequisites
- SQL Server 2019 or later (or Azure SQL Database)
- Appropriate permissions to create tables and insert data
- .NET 9 project with Entity Framework Core (for production use)

### Step 1: Create Tables
```sql
-- Run in SQL Server Management Studio or Azure Data Studio
-- Execute: 01-create-tables.sql
```

### Step 2: Insert Seed Data
```sql
-- Run in SQL Server Management Studio or Azure Data Studio
-- Execute: 03-seed-data-crm-3pages.sql
-- Safe to run multiple times - will not create duplicates
```

### PowerShell Execution
```powershell
# Install SqlServer module if needed
Install-Module -Name SqlServer -Force

# Define connection
$ServerName = "localhost"
$DatabaseName = "AMS"
$ScriptPath1 = "C:\path\to\01-create-tables.sql"
$ScriptPath2 = "C:\path\to\03-seed-data-crm-3pages.sql"

# Execute table creation
Invoke-Sqlcmd -ServerInstance $ServerName -Database $DatabaseName -InputFile $ScriptPath1

# Execute seed data
Invoke-Sqlcmd -ServerInstance $ServerName -Database $DatabaseName -InputFile $ScriptPath2
```

### Azure DevOps / CI/CD Pipeline
```yaml
- task: SqlAzureDacpacDeployment@1
  inputs:
    azureSubscription: 'YourSubscription'
    AuthenticationType: 'server'
    ServerName: 'yourserver.database.windows.net'
    DatabaseName: 'AMS'
    SqlUsername: 'sqladmin'
    SqlPassword: '$(SqlPassword)'
    deployType: 'SqlTask'
    SqlFile: '01-create-tables.sql'
    IpDetectionMethod: 'AutoDetect'
```

## Data Schema Details

### Leads Table
```sql
Key Columns:
- Id (GUID) - Primary key
- TenantId (GUID) - Tenant isolation
- LeadNumber (VARCHAR) - Unique lead identifier
- Score (INT) - Lead quality score (0-100)
- PriorityCode (VARCHAR) - High/Medium/Low
- SourceCode (VARCHAR) - Lead source (Web, Direct, Referral, etc.)
- AssignedToUserId (GUID) - Assigned producer
- StatusCode (INT) - Lead status
- CreatedDateUtc (DATETIME2) - Record creation time
- ModifiedDateUtc (DATETIME2) - Last update time
```

### Lead Scoring Rules
```sql
Columns:
- RuleName - Name of the scoring rule
- RuleType - Category (Engagement, Behavior, Profile, Recency)
- Points - Points awarded (0-20)
- Condition - Description of when rule applies
- IsActive - Whether rule is currently used
- DisplayOrder - Order in UI
```

### Lead Assignment Rules
```sql
Columns:
- RuleName - Name of assignment rule
- RuleType - Method (Score-Based, Round-Robin, Territory-Based)
- Criteria - Rule criteria (e.g., "Score >= 80")
- TargetGroup - Target producer group
- MaxAssignments - Max leads per producer (0 = unlimited)
- IsActive - Whether rule is active
```

### Lead Activities (Follow-ups)
```sql
Columns:
- LeadId - Reference to lead
- ActivityType - Phone Call, Email, Meeting, etc.
- ContactMethod - How contact was/will be made
- Subject - Activity subject
- ScheduledDateUtc - When activity is scheduled
- CompletedDateUtc - When activity was completed
- Priority - High/Medium/Low
- StatusCode - Pending/Completed/Cancelled
- AssignedToUserId - Who this activity is assigned to
```

## Reporting Queries

### Lead Scoring Distribution
```sql
SELECT 
    CASE 
        WHEN [Score] >= 80 THEN 'High (80+)'
        WHEN [Score] >= 50 THEN 'Medium (50-79)'
        ELSE 'Low (<50)'
    END AS PriorityLevel,
    COUNT(*) AS LeadCount,
    AVG([Score]) AS AvgScore,
    MAX([Score]) AS MaxScore,
    MIN([Score]) AS MinScore
FROM [dbo].[Leads]
WHERE [TenantId] = '00000000-0000-0000-0000-000000000001'
GROUP BY 
    CASE 
        WHEN [Score] >= 80 THEN 'High (80+)'
        WHEN [Score] >= 50 THEN 'Medium (50-79)'
        ELSE 'Low (<50)'
    END;
```

### Pending Follow-ups by Producer
```sql
SELECT 
    u.[FullName] AS Producer,
    COUNT(la.[Id]) AS PendingActivities,
    COUNT(CASE WHEN la.[Priority] = 'High' THEN 1 END) AS HighPriority,
    COUNT(CASE WHEN la.[Priority] = 'Medium' THEN 1 END) AS MediumPriority,
    COUNT(CASE WHEN la.[Priority] = 'Low' THEN 1 END) AS LowPriority
FROM [dbo].[LeadActivities] la
JOIN [dbo].[Users] u ON la.[AssignedToUserId] = u.[Id]
WHERE la.[StatusCode] = 'Pending'
    AND la.[TenantId] = '00000000-0000-0000-0000-000000000001'
GROUP BY u.[FullName]
ORDER BY PendingActivities DESC;
```

### Lead Assignment Effectiveness
```sql
SELECT 
    u.[FullName] AS Producer,
    COUNT(DISTINCT l.[Id]) AS TotalLeadsAssigned,
    AVG(l.[Score]) AS AvgLeadScore,
    SUM(CASE WHEN l.[Score] >= 80 THEN 1 ELSE 0 END) AS HighScoreLeads
FROM [dbo].[Leads] l
LEFT JOIN [dbo].[Users] u ON l.[AssignedToUserId] = u.[Id]
WHERE l.[TenantId] = '00000000-0000-0000-0000-000000000001'
GROUP BY u.[FullName]
ORDER BY TotalLeadsAssigned DESC;
```

## Troubleshooting

### Duplicate Key Violation
If you get a duplicate key error:
1. Check if data already exists: `SELECT COUNT(*) FROM [dbo].[Leads]`
2. Clear existing data (if needed): `DELETE FROM [dbo].[Leads] WHERE TenantId = '00000000-0000-0000-0000-000000000001'`
3. Re-run the seed script

### Foreign Key Errors
Ensure tables are created in this order:
1. Tenants
2. Users
3. Accounts
4. Leads
5. LeadActivities
6. LeadAssignmentHistory
7. LeadQualityMetrics

### Connection Issues
- Verify SQL Server is running
- Check connection string in application
- Verify firewall allows connection
- Test with Azure Data Studio or SSMS first

## Integration with Blazor

### Entity Models
The seed data maps to these EF Core entities:
```csharp
// In Ams.Domain/Entities/
- Lead.cs
- User.cs
- LeadActivity.cs
- LeadScoringRule.cs
- LeadAssignmentRule.cs
```

### DbContext Configuration
```csharp
// In Ams.Infrastructure
modelBuilder.Entity<Lead>()
    .HasKey(l => l.Id)
    .HasIndex(l => new { l.TenantId, l.LeadNumber })
    .IsUnique();

modelBuilder.Entity<LeadActivity>()
    .HasOne<Lead>()
    .WithMany()
    .HasForeignKey(la => la.LeadId);
```

## Performance Considerations

- Indexes on frequently queried columns (TenantId, Score, StatusCode)
- Partitioning recommended for tables > 10M rows
- Archive historical data periodically
- Statistics maintenance via SQL Server Agent

## Security

- All user inputs should be parameterized
- Use row-level security (RLS) for multi-tenant isolation
- Encrypt sensitive columns (email, phone)
- Audit sensitive operations via triggers
- Implement column-level encryption for PII

## Support & Questions

For issues or questions:
1. Check existing queries in this README
2. Review Entity Framework configuration
3. Consult Blazor page implementation
4. Check application logs for detailed errors

---

**Version:** 1.0  
**Last Updated:** 2024  
**Target Database:** SQL Server 2019+, Azure SQL Database  
**Target Framework:** .NET 9
