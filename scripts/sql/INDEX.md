# AMS CRM Database Scripts - Complete Index

> **SQL Scripts and Seed Data for Lead Scoring, Assignment, and Follow-up Pages**

## 📍 Location
```
C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql\
```

## 📚 Documentation Map

### Start Here → [SUMMARY.md](SUMMARY.md)
**30-second overview** of what was created and how to use it.

```
Quick Start | Test Data | Common Issues | Next Steps
```

---

### For Quick Commands → [QUICK-REFERENCE.md](QUICK-REFERENCE.md)
**Copy-paste commands** for common tasks.

```sql
-- View all leads ordered by score
SELECT LeadNumber, Score FROM Leads ORDER BY Score DESC;

-- Check pending follow-ups
SELECT * FROM LeadActivities WHERE StatusCode = 'Pending';
```

---

### For Setup & Integration → [SETUP-GUIDE.md](SETUP-GUIDE.md)
**Step-by-step guide** with Blazor code examples.

- PowerShell automation
- SSMS step-by-step
- Complete Blazor examples
- Database integration code

---

### For Complete Documentation → [README.md](README.md)
**Full technical reference** with all details.

- Complete schema documentation
- Reporting queries
- Performance tips
- Security guidelines

---

## 🗂️ SQL Scripts

### [01-create-tables.sql](01-create-tables.sql)
Creates the complete database schema.

**Contains:**
- 9 database tables
- Proper indexes (IX_*)
- Foreign key constraints
- Audit trail fields
- Soft-delete support

**Usage:**
```sql
sqlcmd -S localhost -d AMS -i 01-create-tables.sql
```

**Safe to rerun:** ✅ Yes (IF NOT EXISTS checks)

---

### [03-seed-data-crm-3pages.sql](03-seed-data-crm-3pages.sql)
Inserts test data for the 3 CRM pages.

**Inserts:**
- 1 Tenant
- 5 Users (Producers)
- 18 Leads (varied scores)
- 9 Scoring Rules
- 4 Assignment Rules
- 19 Follow-up Activities

**Usage:**
```sql
sqlcmd -S localhost -d AMS -i 03-seed-data-crm-3pages.sql
```

**Safe to rerun:** ✅ Yes (duplicate prevention)

---

### [Setup-CrmDatabase.ps1](Setup-CrmDatabase.ps1)
PowerShell automation for complete database setup.

**Features:**
- Test database connection
- Run both scripts automatically
- Verify results
- Show data distribution
- Full error handling

**Usage:**
```powershell
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -All
```

**Examples:**
```powershell
# Create tables only
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -CreateTables

# Seed data only
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -SeedData -ShowVerification

# Azure SQL Database
.\Setup-CrmDatabase.ps1 -ServerName "server.database.windows.net" `
    -DatabaseName AMS -Username sqladmin -Password "P@ss!" -All
```

---

## 🎯 The 3 Pages & Their Data

### 1. Lead Scoring (`/crm/leads/scoring`)
| Aspect | Details |
|--------|---------|
| **Tables** | Leads, LeadScoringRules |
| **Records** | 18 Leads, 9 Rules |
| **Data** | Leads with scores 48-91 |
| **Display** | Sorted by score, color-coded priority |
| **Purpose** | View and understand lead quality |

**Key Leads:**
- 6 High priority (Score 80+)
- 7 Medium priority (Score 50-79)
- 5 Low priority (Score <50)

**Scoring Rules:**
- Demo Request (20 pts)
- Form Submission (15 pts)
- Industry Match (10 pts)
- Website Visit (10 pts)
- Company Size (8 pts)
- Page Downloads (7 pts)
- Email Opens (5 pts)
- Recent Activity (5 pts)
- LinkedIn Connection (3 pts)

---

### 2. Lead Assignment (`/crm/leads/assignment`)
| Aspect | Details |
|--------|---------|
| **Tables** | Leads, Users, LeadAssignmentRules, LeadAssignmentHistory |
| **Records** | 18 Unassigned Leads, 5 Producers, 4 Rules |
| **Action** | Assign leads to producers using rules or manual selection |
| **Purpose** | Distribute leads fairly and strategically |

**Producers Available:**
1. John Spencer - Senior Producer
2. Amanda Hayes - Producer
3. Ryan Mitchell - Account Executive
4. Jessica Brown - Producer
5. Thomas Anderson - Senior Producer

**Assignment Rules:**
1. High-Score Auto Assign (Score ≥ 80 → Senior Producers)
2. Round-Robin Distribution (All Leads → All Producers)
3. Medium Priority Assignment (Score 50-79 → All Producers)
4. Nurture Queue Assignment (Score < 50 → Nurture Team)

---

### 3. Lead Follow-up (`/crm/leads/follow-up`)
| Aspect | Details |
|--------|---------|
| **Tables** | LeadActivities, Leads, Users |
| **Records** | 19 Follow-up Activities |
| **Priority** | High (6), Medium (8), Low (5) |
| **Types** | Phone Calls, Emails, Nurture Campaigns |
| **Purpose** | Track and manage outreach to leads |

**Activities Breakdown:**
- **High Priority Phone Calls (6)** - For top leads (Score ≥ 85)
- **Medium Priority Emails (8)** - For medium leads (Score 65-79)
- **Low Priority Nurture (5)** - For nurture leads (Score < 65)

---

## 🚀 Getting Started

### Fastest Way (3 minutes)
```powershell
cd scripts\sql
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -All
```

### Traditional Way (5 minutes)
```bash
# Terminal 1: Create tables
sqlcmd -S localhost -d AMS -i 01-create-tables.sql

# Terminal 2: Seed data
sqlcmd -S localhost -d AMS -i 03-seed-data-crm-3pages.sql
```

### SSMS GUI Way
1. Open SQL Server Management Studio
2. Connect to your database
3. Open & execute `01-create-tables.sql`
4. Open & execute `03-seed-data-crm-3pages.sql`

---

## ✅ Verification

```sql
-- Quick check (should show 5 tables with data)
SELECT 
    (SELECT COUNT(*) FROM Leads) AS Leads,
    (SELECT COUNT(*) FROM Users) AS Users,
    (SELECT COUNT(*) FROM LeadActivities) AS Activities,
    (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRules,
    (SELECT COUNT(*) FROM LeadAssignmentRules) AS AssignmentRules;
```

Expected output:
```
Leads | Users | Activities | ScoringRules | AssignmentRules
18    | 5     | 19         | 9            | 4
```

---

## 📊 Data Flow Diagram

```
01-create-tables.sql
        ↓
   Creates Tables
   ├─ Leads
   ├─ Users
   ├─ LeadScoringRules
   ├─ LeadAssignmentRules
   ├─ LeadActivities
   └─ LeadAssignmentHistory
        ↓
03-seed-data-crm-3pages.sql
        ↓
   Inserts Test Data
   ├─ 5 Producers
   ├─ 18 Leads
   ├─ 9 Scoring Rules
   ├─ 4 Assignment Rules
   └─ 19 Follow-up Activities
        ↓
Blazor Pages Ready
├─ Lead Scoring Page
├─ Lead Assignment Page
└─ Lead Follow-up Page
```

---

## 🔗 File References

| Documentation | Purpose | Read Time |
|--------------|---------|-----------|
| **SUMMARY.md** | Overview & quick start | 3 min |
| **QUICK-REFERENCE.md** | Common queries & tasks | 2 min |
| **SETUP-GUIDE.md** | Integration with Blazor | 10 min |
| **README.md** | Complete technical docs | 20 min |
| This **INDEX.md** | Navigation & overview | 5 min |

---

## 🎓 For Different Roles

### For Developers
1. Read: SUMMARY.md
2. Review: 01-create-tables.sql
3. Study: SETUP-GUIDE.md (Blazor examples)
4. Reference: README.md (technical details)

### For QA/Testers
1. Read: SUMMARY.md
2. Follow: SETUP-GUIDE.md (setup steps)
3. Use: QUICK-REFERENCE.md (verification queries)
4. Test: The 3 pages once setup

### For Database Admins
1. Review: 01-create-tables.sql (schema)
2. Check: 03-seed-data-crm-3pages.sql (data)
3. Read: README.md (indexing, security)
4. Use: QUICK-REFERENCE.md (maintenance queries)

### For DevOps/CI-CD
1. Integrate: Setup-CrmDatabase.ps1 into pipeline
2. Reference: SETUP-GUIDE.md (Azure SQL example)
3. Monitor: README.md (performance tips)
4. Backup: README.md (reset procedures)

---

## 🛠️ Customization

### Add More Leads
Edit `03-seed-data-crm-3pages.sql`, insert into `@LeadsToInsert`:
```sql
('LEAD-019', 'NewFirst', 'NewLast', 'email@company.com', '555-3019', 'Company', 'Service', 87, 'High', 'Web', 'Active', 0),
```

### Modify Scoring Rules
Edit `03-seed-data-crm-3pages.sql`, modify `@RulesToInsert`:
```sql
('Demo Request', 'Engagement', 25, 'Requested product demo', 'Demo request submitted'),
```

### Change Assignment Logic
Edit `03-seed-data-crm-3pages.sql`, update `LeadAssignmentRules`:
```sql
INSERT INTO LeadAssignmentRules VALUES
(NEWID(), @DefaultTenantId, 'Custom Rule', 'Score-Based', 'Score >= 75', 'Team A', 0, 1, 1, GETUTCDATE(), @SystemUserId);
```

---

## ❌ Safety & Rollback

### Safe to Run Multiple Times
✅ Both scripts check for existing data before inserting  
✅ No duplicates will be created  
✅ No data loss if rerun  

### If You Need to Reset
```sql
-- Careful: This deletes all data for this tenant!
DELETE FROM LeadActivities WHERE TenantId = '00000000-0000-0000-0000-000000000001';
DELETE FROM Leads WHERE TenantId = '00000000-0000-0000-0000-000000000001';
DELETE FROM Users WHERE TenantId = '00000000-0000-0000-0000-000000000001';

-- Then rerun: 03-seed-data-crm-3pages.sql
```

---

## 📞 Quick Support

| Issue | Solution |
|-------|----------|
| Module not found | `Install-Module SqlServer -Force` |
| Access denied | Run as admin or use DBO account |
| Connection failed | Check server name, firewall, credentials |
| Data not appearing | Rerun seed script (checks for duplicates) |
| Blank Blazor page | Check DbContext connection string |

---

## 🎯 Success Checklist

- [ ] Read SUMMARY.md
- [ ] Run Setup-CrmDatabase.ps1 (or SQL scripts)
- [ ] Verify data with Quick-Reference queries
- [ ] Review DbContext models match schema
- [ ] Test Lead Scoring page loads correctly
- [ ] Test Lead Assignment page functions
- [ ] Test Lead Follow-up page shows activities
- [ ] Deploy to test/production environment

---

## 📦 What's Included

```
scripts/sql/
├── SQL Scripts (2)
│   ├── 01-create-tables.sql
│   └── 03-seed-data-crm-3pages.sql
│
├── Automation (1)
│   └── Setup-CrmDatabase.ps1
│
├── Documentation (5)
│   ├── INDEX.md (← you are here)
│   ├── SUMMARY.md
│   ├── QUICK-REFERENCE.md
│   ├── SETUP-GUIDE.md
│   └── README.md
```

---

## 🌐 Next: Blazor Pages

Once database is ready, implement:
1. **LeadScoringPage.razor** - Display leads with scores
2. **LeadAssignmentPage.razor** - Assign leads to producers
3. **LeadFollowUpPage.razor** - Track follow-up activities

See SETUP-GUIDE.md for complete code examples.

---

**Version:** 1.0  
**Created:** 2024  
**Target:** .NET 9 Blazor + SQL Server  
**Status:** ✅ Production Ready

---

**Start:** Read [SUMMARY.md](SUMMARY.md)  
**Execute:** Run [Setup-CrmDatabase.ps1](Setup-CrmDatabase.ps1)  
**Reference:** Use [QUICK-REFERENCE.md](QUICK-REFERENCE.md)  
**Integrate:** Follow [SETUP-GUIDE.md](SETUP-GUIDE.md)  
**Deep Dive:** Study [README.md](README.md)
