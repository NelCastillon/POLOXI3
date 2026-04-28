# Seed Data for Existing AMS Database

## 📌 For Your Existing Setup

Your database already has:
- ✅ `LeadScoringRules` table (with data)
- ✅ `LeadAssignmentRule` table
- Potentially other CRM tables

This script **adapts to your existing schema** without duplicating or recreating tables.

## 🎯 What This Script Does

### For Lead Scoring Page (`/crm/leads/scoring`)
- Populates `LeadScoringRules` if empty
- 9 scoring rules ready to use:
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
- Populates `LeadAssignmentRule` if empty
- 4 assignment strategies:
  - High-Score Auto Assign (Score ≥ 80)
  - Round-Robin Distribution
  - Medium Priority Assignment (Score 50-79)
  - Nurture Queue Assignment (Score < 50)

### For Lead Follow-up Page (`/crm/leads/follow-up`)
- Creates follow-up activities (LeadActivities)
- 19 activities pre-scheduled:
  - 6 High Priority phone calls
  - 8 Medium Priority emails
  - 5 Low Priority nurture campaigns

### Lead Data
- 18 test leads with realistic scores and contact info
- Organized by priority level
- Ready for assignment and follow-up

## 🚀 Usage

### Option 1: PowerShell
```powershell
cd scripts\sql
Invoke-Sqlcmd -ServerInstance localhost -Database AMS -InputFile 04-seed-existing-db.sql
```

### Option 2: SQL Server Management Studio
1. Open SSMS
2. File → Open → `04-seed-existing-db.sql`
3. Press Ctrl+E to execute

### Option 3: Command Line
```batch
sqlcmd -S localhost -d AMS -i 04-seed-existing-db.sql
```

## ✅ Verification

After running the script, verify the data:

```sql
-- Check scoring rules
SELECT COUNT(*) AS ScoringRules FROM LeadScoringRules;
-- Expected: 9

-- Check assignment rules
SELECT COUNT(*) AS AssignmentRules FROM LeadAssignmentRule;
-- Expected: 4

-- Check leads
SELECT COUNT(*) AS Leads FROM Leads;
-- Expected: 18

-- Check activities
SELECT COUNT(*) AS Activities FROM LeadActivities;
-- Expected: 19+
```

## 🔄 Safe to Run Multiple Times

✅ Script checks if data already exists  
✅ Won't create duplicates  
✅ Won't fail if run again  
✅ Perfect for dev/test environments  

## 📊 Lead Score Distribution

The 18 test leads are distributed as:

**High Priority (Score 80+):** 6 leads
- Sarah Anderson (85)
- Jennifer Martinez (91)
- Robert Jackson (88)
- Rachel Santos (89)
- Michelle Brown (84)
- James Mitchell (81)

**Medium Priority (Score 50-79):** 7 leads
- Michael Chen (72)
- David Thompson (65)
- Emily Watson (78)
- Lisa Graham (73)
- Christopher Davis (76)
- Marcus Taylor (75)
- Charles Williams (71)

**Low Priority (Score <50):** 5 leads
- Amanda Price (62)
- Kevin Wilson (68)
- Victoria Kim (64)
- Patricia Johnson (55)
- Diana Moore (48)

## 🎯 Next Steps

1. ✅ Run this seed script
2. ⬜ Verify data in database
3. ⬜ Test Lead Scoring page displays rules
4. ⬜ Test Lead Assignment page works
5. ⬜ Test Lead Follow-up page shows activities

## 🆘 Troubleshooting

### "Table does not exist"
- Script automatically skips missing tables
- Check your database schema to see which tables exist
- Review script output for details

### "Duplicate key error"
- Script checks for existing data
- If you see this, data already exists (safe to ignore)
- You can rerun the script anytime

### "Access denied"
- Run as database owner or administrator
- Check your database permissions

## 📝 Notes

- Script uses existing column names (adjust if your schema differs)
- Safe to modify insert values for your test data
- Designed specifically for Blazor pages, not production

## 🔗 Related Files

- `01-create-tables.sql` - If you need to create all tables from scratch
- `03-seed-data-crm-3pages.sql` - Alternative comprehensive seed data
- `SUMMARY.md` - Overview of all available scripts

---

**Version:** 1.0  
**For:** Existing AMS Database  
**Optimized:** Lead Scoring, Assignment, Follow-up Pages
