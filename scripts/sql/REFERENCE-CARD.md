# 📋 Reference Card - SQL Scripts & Seed Data

## Your Situation
```
✅ Database exists
✅ LeadScoringRules table exists
✅ LeadAssignmentRule table exists
✅ Need test data for 3 pages
```

---

## 🎯 The ONE Script You Need

### **04-seed-existing-db.sql**

```sql
-- Run this ONE command:
sqlcmd -S localhost -d AMS -i 04-seed-existing-db.sql

-- Or in PowerShell:
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "scripts/sql/04-seed-existing-db.sql"

-- Or in SSMS:
-- File → Open → 04-seed-existing-db.sql → Ctrl+E
```

---

## 📊 What It Adds

| Table | Records | Purpose |
|-------|---------|---------|
| LeadScoringRules | 9 | Lead Scoring page |
| LeadAssignmentRule | 4 | Lead Assignment page |
| Leads | 18 | Test data (mixed scores) |
| LeadActivities | 19+ | Lead Follow-up page |

---

## ✅ Verify (Paste This Query)

```sql
SELECT 
    (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRules,
    (SELECT COUNT(*) FROM LeadAssignmentRule) AS AssignmentRules,
    (SELECT COUNT(*) FROM Leads) AS Leads,
    (SELECT COUNT(*) FROM LeadActivities) AS Activities;
```

**Expected:** 9, 4, 18, 19+

---

## 📚 Documentation Map

```
START HERE
    ↓
00-START-HERE.md (this file)
    ↓
EXISTING-DB-QUICKSTART.md (2 minutes)
    ↓
FOR-EXISTING-DB.md (complete docs)
    ↓
Reference:
- QUICK-REFERENCE.md (SQL queries)
- README-FOR-YOU.md (tailored summary)
```

---

## 🚀 Three Ways to Run

### 1️⃣ SQL Server Management Studio (Easiest)
```
1. Open SSMS
2. File → Open → 04-seed-existing-db.sql
3. Ctrl+E
4. ✅ Done
```

### 2️⃣ PowerShell
```powershell
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "scripts/sql/04-seed-existing-db.sql"
```

### 3️⃣ Command Line
```batch
sqlcmd -S localhost -d AMS -i scripts/sql/04-seed-existing-db.sql
```

---

## 🎯 For Your 3 Pages

### Lead Scoring (`/crm/leads/scoring`)
```
Provides: 9 scoring rules
From table: LeadScoringRules
Status: Ready ✅
```

### Lead Assignment (`/crm/leads/assignment`)
```
Provides: 4 assignment rules + 18 test leads
From tables: LeadAssignmentRule, Leads
Status: Ready ✅
```

### Lead Follow-up (`/crm/leads/follow-up`)
```
Provides: 19 follow-up activities
From table: LeadActivities
Status: Ready ✅
```

---

## 🔄 Safety

✅ Won't create duplicates  
✅ Safe to run multiple times  
✅ Skips missing tables  
✅ Preserves existing data  

---

## ⚡ One-Liners

### Run Everything
```powershell
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "scripts/sql/04-seed-existing-db.sql"
```

### Verify Everything
```sql
SELECT COUNT(*) FROM LeadScoringRules; SELECT COUNT(*) FROM LeadAssignmentRule; SELECT COUNT(*) FROM Leads;
```

### View Leads
```sql
SELECT FirstName, LastName, Score, Status FROM Leads ORDER BY Score DESC;
```

### View Rules
```sql
SELECT Name, Value FROM LeadScoringRules ORDER BY Value DESC;
```

---

## 🎓 Quick Facts

- **Time to setup:** 5 minutes
- **Effort level:** 1 command
- **Data inserted:** 18 leads + 9 rules + 4 rules + 19 activities
- **Safe to rerun:** Yes
- **Breaks existing data:** No
- **For production:** No (test only)

---

## 📞 FAQ

**Q: Which file should I run?**  
A: `04-seed-existing-db.sql`

**Q: Will it work with my existing DB?**  
A: Yes, it's designed for it

**Q: Can I run it twice?**  
A: Yes, won't create duplicates

**Q: What if a table is missing?**  
A: Script skips it automatically

**Q: How long does it take?**  
A: Less than 1 minute

---

## 🎯 Next 5 Minutes

```
1. Run: 04-seed-existing-db.sql (1 min)
2. Verify: Run verification query (1 min)
3. Test: Visit Lead Scoring page (1 min)
4. Test: Visit Lead Assignment page (1 min)
5. Test: Visit Lead Follow-up page (1 min)
```

---

## 📁 All Available Files

| File | Type | Use |
|------|------|-----|
| **04-seed-existing-db.sql** | SQL | For your DB ⭐ |
| **EXISTING-DB-QUICKSTART.md** | Docs | Quick start |
| **FOR-EXISTING-DB.md** | Docs | Full reference |
| **00-START-HERE.md** | Docs | Overview |
| **QUICK-REFERENCE.md** | Docs | SQL queries |
| 01-create-tables.sql | SQL | Fresh setup |
| 03-seed-data-crm-3pages.sql | SQL | Alternative |
| Setup-CrmDatabase.ps1 | PS1 | Automation |

---

## ✨ You're Ready!

**Next Action:**
```powershell
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "scripts/sql/04-seed-existing-db.sql"
```

**Then Test:**
- https://localhost:7061/crm/leads/scoring
- https://localhost:7061/crm/leads/assignment
- https://localhost:7061/crm/leads/follow-up

**Done!** ✅

---

**Print this page as your reference card!**
