# Database Setup Summary - AMS CRM Module

## 📋 What Was Created

### SQL Scripts (3 files)

| File | Purpose | Safe to Rerun |
|------|---------|--------------|
| **01-create-tables.sql** | Creates all database tables with proper indexing | ✅ Yes |
| **03-seed-data-crm-3pages.sql** | Inserts test data for 3 CRM pages | ✅ Yes |
| **Setup-CrmDatabase.ps1** | PowerShell automation script | ✅ Yes |

### Documentation (3 files)

| File | Content |
|------|---------|
| **README.md** | Complete technical documentation |
| **QUICK-REFERENCE.md** | Common commands and queries |
| **SETUP-GUIDE.md** | Integration guide with Blazor examples |

## 🎯 For the 3 CRM Pages

### 1️⃣ Lead Scoring Page (`/crm/leads/scoring`)
- **Tables Used:** `Leads`, `LeadScoringRules`
- **Data:** 18 leads with scores ranging 48-91
- **Rules:** 9 scoring rules (Demo Request, Form Submission, etc.)
- **Display:** Leads sorted by score with priority indicators

### 2️⃣ Lead Assignment Page (`/crm/leads/assignment`)
- **Tables Used:** `Leads`, `Users`, `LeadAssignmentRules`, `LeadAssignmentHistory`
- **Data:** 18 unassigned leads ready for distribution
- **Producers:** 5 available for assignment
- **Rules:** 4 assignment strategies (Score-based, Round-robin, etc.)
- **Action:** Assign leads to producers using rules or manual selection

### 3️⃣ Lead Follow-up Page (`/crm/leads/follow-up`)
- **Tables Used:** `LeadActivities`, `Leads`, `Users`
- **Data:** 19 pre-created follow-up activities
- **Activities:** Phone calls (6), Emails (8), Nurture campaigns (5)
- **Priority:** Organized by High/Medium/Low
- **Action:** Track and manage follow-up status

## 📊 Test Data Included

```
✓ 1 Tenant (Default)
✓ 5 Users/Producers (John Spencer, Amanda Hayes, Ryan Mitchell, Jessica Brown, Thomas Anderson)
✓ 18 Leads (6 High, 7 Medium, 5 Low Priority)
✓ 9 Scoring Rules (3-20 points each)
✓ 4 Assignment Rules (auto-distribution strategies)
✓ 19 Follow-up Activities (Phone calls, Emails, Nurture)
```

## 🚀 Quick Start

### Option 1: PowerShell (Recommended)
```powershell
cd "C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql"
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -All
```

### Option 2: SQL Server Management Studio
1. Open SSMS
2. Connect to your database
3. File → Open → `01-create-tables.sql` → Execute
4. File → Open → `03-seed-data-crm-3pages.sql` → Execute

### Option 3: Command Line
```batch
cd scripts\sql
sqlcmd -S localhost -d AMS -i 01-create-tables.sql
sqlcmd -S localhost -d AMS -i 03-seed-data-crm-3pages.sql
```

## ✅ Verification

After setup, verify with these queries:

```sql
-- Check tables created
SELECT COUNT(*) AS Tables FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('Leads', 'Users', 'LeadActivities', 'LeadScoringRules', 'LeadAssignmentRules');

-- Check data inserted
SELECT 
    (SELECT COUNT(*) FROM Leads) AS Leads,
    (SELECT COUNT(*) FROM Users) AS Users,
    (SELECT COUNT(*) FROM LeadActivities) AS Activities;

-- View lead scores
SELECT LeadNumber, Score, PriorityCode FROM Leads ORDER BY Score DESC;
```

## 📁 File Locations

```
C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql\
├── 01-create-tables.sql              (292 lines - Database schema)
├── 03-seed-data-crm-3pages.sql       (378 lines - Test data)
├── Setup-CrmDatabase.ps1              (PowerShell automation)
├── README.md                          (Full documentation)
├── QUICK-REFERENCE.md                 (Common commands)
├── SETUP-GUIDE.md                     (Integration examples)
└── SUMMARY.md                         (This file)
```

## 🔄 Script Flow

```
01-create-tables.sql (IF NOT EXISTS checks)
    ↓
    Creates: Tenants, Users, Leads, LeadActivities, LeadScoringRules, LeadAssignmentRules, etc.
    ↓
03-seed-data-crm-3pages.sql (Duplicate prevention)
    ↓
    Inserts: 1 Tenant, 5 Users, 18 Leads, 9 Rules, 4 Assignment Rules, 19 Activities
    ↓
Database Ready for Blazor Pages
```

## 🛡️ Safety Features

✅ **IF NOT EXISTS checks** - Won't fail if tables already exist  
✅ **Duplicate prevention** - Checks before inserting data  
✅ **Foreign key constraints** - Maintains data integrity  
✅ **Audit fields** - CreatedDate, ModifiedDate, CreatedBy tracking  
✅ **Soft deletes** - IsDeleted column for data retention  
✅ **Multi-tenant** - TenantId for isolation  

## 📈 Database Structure

### Core Tables

**Leads** (18 records)
```
LeadNumber | FirstName | LastName | Email | Score | PriorityCode | SourceCode | StatusCode
```

**Users** (5 records)
```
UserName | Email | FullName | JobTitle | Department | StatusCode
```

**LeadActivities** (19 records)
```
LeadId | ActivityType | Subject | ScheduledDateUtc | Priority | StatusCode | AssignedToUserId
```

**LeadScoringRules** (9 records)
```
RuleName | RuleType | Points | Condition | IsActive
```

**LeadAssignmentRules** (4 records)
```
RuleName | RuleType | Criteria | TargetGroup | IsActive
```

## 🔍 Common Queries

### High-Priority Leads
```sql
SELECT * FROM Leads WHERE Score >= 80 ORDER BY Score DESC;
```

### Pending Activities
```sql
SELECT l.LeadNumber, la.ActivityType, la.Priority, u.FullName AS AssignedTo
FROM LeadActivities la
JOIN Leads l ON la.LeadId = l.Id
JOIN Users u ON la.AssignedToUserId = u.Id
WHERE la.StatusCode = 'Pending'
ORDER BY la.Priority DESC, la.ScheduledDateUtc;
```

### Lead Distribution
```sql
SELECT 
    CASE WHEN Score >= 80 THEN 'High' WHEN Score >= 50 THEN 'Medium' ELSE 'Low' END AS Priority,
    COUNT(*) AS Count,
    AVG(Score) AS AvgScore
FROM Leads
GROUP BY CASE WHEN Score >= 80 THEN 'High' WHEN Score >= 50 THEN 'Medium' ELSE 'Low' END;
```

## 📦 Dependencies

- SQL Server 2019+ or Azure SQL Database
- .NET 9 (for Entity Framework)
- SqlServer PowerShell module (for automation)
- Azure Data Studio or SQL Server Management Studio (optional, for GUI)

## 🎓 Next Steps

### For Developers
1. ✅ Review created schema in `01-create-tables.sql`
2. ✅ Check seed data in `03-seed-data-crm-3pages.sql`
3. ⬜ Update Entity Framework DbContext models
4. ⬜ Create/update Blazor page components
5. ⬜ Implement services (ILeadService, ILeadAssignmentService, etc.)

### For QA/Testing
1. ✅ Verify all 5 tables created with proper columns
2. ✅ Verify all seed data populated correctly
3. ⬜ Test Lead Scoring page displays leads correctly
4. ⬜ Test Lead Assignment page assigns leads to producers
5. ⬜ Test Lead Follow-up page shows pending activities

### For DevOps
1. ✅ Add scripts to version control (Git)
2. ⬜ Integrate into CI/CD pipeline
3. ⬜ Configure for different environments (Dev, Test, Prod)
4. ⬜ Set up backup/restore procedures

## 🆘 Troubleshooting

**Error: "SqlServer module not found"**
```powershell
Install-Module -Name SqlServer -Force
```

**Error: "Access denied"**
- Run as administrator or use database owner credentials

**Error: "Cannot insert duplicate key"**
- Data already exists; script is safe to rerun
- Check with verification queries above

**Blank pages in Blazor**
- Verify database connection in appsettings.json
- Check Entity Framework models are updated
- Review browser console for errors

## 📞 Support Resources

- **Full Docs:** See README.md
- **Quick Ref:** See QUICK-REFERENCE.md
- **Setup Help:** See SETUP-GUIDE.md
- **Git Repo:** https://github.com/NelCastillon/AMS

## ✨ Features

✅ Multi-tenant support  
✅ Soft-delete enabled  
✅ Audit trail fields  
✅ Proper indexing  
✅ Foreign key constraints  
✅ Duplicate prevention  
✅ Safe to run multiple times  
✅ Production-ready  

---

**Created:** 2024  
**Version:** 1.0  
**Target Framework:** .NET 9  
**Database:** SQL Server 2019+ / Azure SQL Database  
**Status:** ✅ Ready for Use
