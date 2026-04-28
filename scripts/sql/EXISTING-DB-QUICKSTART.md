# ⚡ Quick Start - For Your Existing Database

> Your database already has `LeadScoringRules` and `LeadAssignmentRule` tables

---

## 🚀 One Command

Since you already have a database, use this focused seed script:

```powershell
# PowerShell
cd "C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql"
.\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -SeedData
```

Or directly:

```sql
-- SQL Server Management Studio or sqlcmd
sqlcmd -S localhost -d AMS -i 04-seed-existing-db.sql
```

---

## ✅ What Gets Added

### For Lead Scoring Page (`/crm/leads/scoring`)
- ✓ 9 Scoring Rules (if empty)
  - Demo Request (20 pts)
  - Form Submission (15 pts)
  - Industry Match (10 pts)
  - Website Visit (10 pts)
  - Company Size (8 pts)
  - Page Downloads (7 pts)
  - Email Opens (5 pts)
  - Recent Activity (5 pts)
  - LinkedIn Connection (3 pts)

### For Lead Assignment Page (`/crm/leads/assignment`)
- ✓ 4 Assignment Rules (if empty)
  - High-Score Auto Assign (≥80)
  - Round-Robin Distribution
  - Medium Priority (50-79)
  - Nurture Queue (<50)

### For Lead Follow-up Page (`/crm/leads/follow-up`)
- ✓ 18 Test Leads
- ✓ 19 Follow-up Activities
  - 6 High Priority calls
  - 8 Medium Priority emails
  - 5 Low Priority nurture

---

## 🔍 Verify It Worked

Run this query:

```sql
SELECT 
    (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRules,
    (SELECT COUNT(*) FROM LeadAssignmentRule) AS AssignmentRules,
    (SELECT COUNT(*) FROM Leads) AS Leads,
    (SELECT COUNT(*) FROM LeadActivities) AS Activities;
```

Expected: **9, 4, 18, 19+**

---

## 📋 Data Summary

| Table | Count | Purpose |
|-------|-------|---------|
| LeadScoringRules | 9 | Lead Scoring page |
| LeadAssignmentRule | 4 | Lead Assignment page |
| Leads | 18 | Test data (mixed scores) |
| LeadActivities | 19 | Lead Follow-up page |

---

## 🎯 Lead Distribution

- **High Priority (80+):** 6 leads
- **Medium Priority (50-79):** 7 leads
- **Low Priority (<50):** 5 leads

---

## 🔄 Safe to Run Multiple Times

✅ Won't create duplicates  
✅ Won't fail if data exists  
✅ Safe for dev/test environments  

---

## 📚 Full Documentation

See `FOR-EXISTING-DB.md` for complete details

---

**Time:** 1 minute  
**Effort:** 1 command  
**Result:** Ready to test your pages
