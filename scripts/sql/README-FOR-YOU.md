# 📦 SQL Scripts Summary - What You Have

## Your Situation
✅ Database already exists  
✅ LeadScoringRules table exists (with data)  
✅ LeadAssignmentRule table exists  
✅ Need test data for 3 Blazor pages  

---

## 📁 Available Scripts

### For Your Existing Database (👈 USE THIS)
| File | Purpose | Use Case |
|------|---------|----------|
| **04-seed-existing-db.sql** | Add seed data to existing DB | Your situation |
| **EXISTING-DB-QUICKSTART.md** | Quick start guide | Read first |
| **FOR-EXISTING-DB.md** | Full documentation | Reference |

### For Fresh Setup (if needed later)
| File | Purpose |
|------|---------|
| `01-create-tables.sql` | Create all CRM tables from scratch |
| `03-seed-data-crm-3pages.sql` | Comprehensive seed data |

---

## 🎯 For Your 3 Pages

### Page 1: Lead Scoring (`https://localhost:7061/crm/leads/scoring`)
**Uses:** LeadScoringRules table  
**Status:** ✅ Already has data  
**What you're getting:**
- 9 detailed scoring rules
- Sorted by point value
- Ready to display

### Page 2: Lead Assignment (`/crm/leads/assignment`)
**Uses:** LeadAssignmentRule table  
**What you're getting:**
- 4 assignment strategies
- Rules for auto-distribution
- Ready to implement

### Page 3: Lead Follow-up (`/crm/leads/follow-up`)
**Uses:** LeadActivities table  
**What you're getting:**
- 18 test leads
- 19 follow-up activities
- Scheduled by priority

---

## ⚡ One-Line Execution

### PowerShell
```powershell
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql\04-seed-existing-db.sql"
```

### Command Line
```batch
sqlcmd -S localhost -d AMS -i "C:\Users\agenc\source\repos\AMS - Copy (2)\scripts\sql\04-seed-existing-db.sql"
```

### SSMS
```
Open: 04-seed-existing-db.sql
Press: Ctrl+E
```

---

## 📊 Test Data Included

```
Leads:                18 records (scores 48-91)
LeadScoringRules:     9 rules (3-20 points each)
LeadAssignmentRule:   4 strategies
LeadActivities:       19 follow-up activities
```

---

## ✅ Verification Query

```sql
-- Copy & paste this to verify
SELECT 
    (SELECT COUNT(*) FROM LeadScoringRules) AS [Scoring Rules],
    (SELECT COUNT(*) FROM LeadAssignmentRule) AS [Assignment Rules],
    (SELECT COUNT(*) FROM Leads) AS [Leads],
    (SELECT COUNT(*) FROM LeadActivities) AS [Activities];

-- Expected: 9, 4, 18, 19+
```

---

## 🔄 Safe Features

✅ Checks for existing data  
✅ Won't duplicate if rerun  
✅ Skips missing tables  
✅ Works with your existing schema  

---

## 📋 Files Created

```
scripts/sql/
├── 04-seed-existing-db.sql          ← Use this!
├── EXISTING-DB-QUICKSTART.md        ← Quick start
├── FOR-EXISTING-DB.md               ← Full docs
└── ... (other files from before)
```

---

## 🚀 Next Steps

1. **Run the script:**
   ```powershell
   Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile "scripts/sql/04-seed-existing-db.sql"
   ```

2. **Verify the data:**
   ```sql
   SELECT COUNT(*) FROM LeadScoringRules;  -- Should be 9
   SELECT COUNT(*) FROM LeadAssignmentRule; -- Should be 4
   SELECT COUNT(*) FROM Leads;             -- Should be 18
   SELECT COUNT(*) FROM LeadActivities;    -- Should be 19+
   ```

3. **Test your Blazor pages:**
   - https://localhost:7061/crm/leads/scoring
   - https://localhost:7061/crm/leads/assignment  
   - https://localhost:7061/crm/leads/follow-up

---

## 🎓 Understanding the Data

### LeadScoringRules (9 total)
Rules that score leads based on engagement metrics.
- Highest: Demo Request (20 pts)
- Lowest: LinkedIn Connection (3 pts)

### LeadAssignmentRule (4 total)
Strategies to assign leads to sales reps.
- Score-based routing
- Round-robin distribution
- Nurture queue

### Leads (18 total)
Test leads with realistic scores:
- High: 80-91 (6 leads)
- Medium: 65-79 (7 leads)
- Low: 48-68 (5 leads)

### LeadActivities (19+ total)
Follow-up tasks for leads:
- Phone calls (high priority)
- Emails (medium priority)
- Nurture campaigns (low priority)

---

## 💡 Pro Tips

1. **Safe to rerun** - Script skips existing data
2. **Database-agnostic** - Works if some tables don't exist
3. **Easy to customize** - Edit the INSERT values if needed
4. **Perfect for testing** - Realistic test data included

---

## ❌ If Something Goes Wrong

### "Table does not exist"
- Script automatically skips it (check output)
- Your database only has some tables

### "Column does not exist"
- Your schema might differ from the script
- Edit column names in the INSERT statements

### "Access denied"
- Run as database owner
- Or use admin credentials

### "Duplicate key"
- Data already exists (safe!)
- Script won't create duplicates

---

## 📚 Documentation Files

| File | Content | Read Time |
|------|---------|-----------|
| **EXISTING-DB-QUICKSTART.md** | How to run for your DB | 2 min |
| **FOR-EXISTING-DB.md** | Complete documentation | 5 min |
| **README.md** | All technical details | 15 min |
| **QUICK-REFERENCE.md** | Common SQL queries | 3 min |

---

## 🎯 Success Checklist

- [ ] Read EXISTING-DB-QUICKSTART.md
- [ ] Run 04-seed-existing-db.sql
- [ ] Verify with SQL query above
- [ ] Test Lead Scoring page
- [ ] Test Lead Assignment page
- [ ] Test Lead Follow-up page
- [ ] Review test data in database

---

**Status:** ✅ Ready to Use  
**For:** Your existing database  
**Time:** 5 minutes to setup  
**Result:** Fully populated test data for 3 Blazor pages

---

**Start Here:** [EXISTING-DB-QUICKSTART.md](EXISTING-DB-QUICKSTART.md)
