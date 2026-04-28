-- ============================================================
-- FIX MIGRATION 0042: Delete failed migration record
-- ============================================================
-- This script removes the 0042_IAM_AuditTrail_create migration record
-- from the tracking table, allowing it to re-run with the corrected code.

USE Ams;

-- Delete the failed migration record
DELETE FROM dbo._Migrations 
WHERE Name = '0042_IAM_AuditTrail_create';

-- Verify deletion
SELECT * FROM dbo._Migrations WHERE Name LIKE '004%' ORDER BY MigrationId DESC;
