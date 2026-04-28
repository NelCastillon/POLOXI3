#!/usr/bin/env pwsh
<#
.SYNOPSIS
    PowerShell script to manage AMS CRM database creation and seeding

.DESCRIPTION
    Creates database tables and seeds test data for CRM module
    Works with SQL Server and Azure SQL Database

.EXAMPLE
    # Create tables
    .\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -CreateTables

.EXAMPLE
    # Seed data
    .\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -SeedData

.EXAMPLE
    # Full setup (tables + seed)
    .\Setup-CrmDatabase.ps1 -ServerName localhost -DatabaseName AMS -All

.EXAMPLE
    # Azure SQL Database
    .\Setup-CrmDatabase.ps1 -ServerName server.database.windows.net -DatabaseName AMS -Username sqladmin -Password 'P@ssw0rd!' -SeedData

.NOTES
    Author: AMS Development Team
    Version: 1.0
    Requires: SqlServer PowerShell module
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ServerName,

    [Parameter(Mandatory = $true)]
    [string]$DatabaseName,

    [string]$Username,

    [string]$Password,

    [switch]$CreateTables,

    [switch]$SeedData,

    [switch]$All,

    [switch]$ShowVerification,

    [string]$ScriptPath = $PSScriptRoot
)

# ============================================================================
# FUNCTIONS
# ============================================================================

function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ ERROR: $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ WARNING: $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "• $Message" -ForegroundColor Gray
}

function Test-SqlModule {
    Write-Header "Checking Prerequisites"

    $module = Get-Module -Name SqlServer -ListAvailable
    if (-not $module) {
        Write-Error-Custom "SqlServer PowerShell module not found"
        Write-Info "Install it with: Install-Module -Name SqlServer -Force -AllowClobber"
        return $false
    }

    if (-not (Get-Module -Name SqlServer)) {
        Import-Module SqlServer -ErrorAction Stop
    }

    Write-Success "SqlServer module is installed"
    return $true
}

function Invoke-SqlScript {
    param(
        [string]$ScriptFile,
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential
    )

    if (-not (Test-Path $ScriptFile)) {
        Write-Error-Custom "Script file not found: $ScriptFile"
        return $false
    }

    Write-Info "Executing script: $(Split-Path $ScriptFile -Leaf)"

    try {
        $splat = @{
            ServerInstance = $ServerInstance
            Database       = $DatabaseName
            InputFile      = $ScriptFile
            OutputSqlErrors = $true
        }

        if ($Credential) {
            $splat['Credential'] = $Credential
        }

        Invoke-Sqlcmd @splat -ErrorAction Stop
        Write-Success "Script executed successfully"
        return $true
    }
    catch {
        Write-Error-Custom "Failed to execute script: $_"
        return $false
    }
}

function Get-SqlCredential {
    param([string]$Username, [string]$Password)

    if ($Username -and $Password) {
        $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
        return New-Object System.Management.Automation.PSCredential($Username, $securePassword)
    }

    return $null
}

function Test-DatabaseConnection {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential
    )

    Write-Info "Testing connection to $ServerInstance/$DatabaseName..."

    try {
        $splat = @{
            ServerInstance = $ServerInstance
            Database       = $DatabaseName
            Query          = "SELECT 1"
        }

        if ($Credential) {
            $splat['Credential'] = $Credential
        }

        $result = Invoke-Sqlcmd @splat -ErrorAction Stop
        Write-Success "Connection successful"
        return $true
    }
    catch {
        Write-Error-Custom "Connection failed: $_"
        return $false
    }
}

function Verify-DatabaseSetup {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential
    )

    Write-Header "Verifying Database Setup"

    $query = @"
    SELECT 
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Leads') AS LeadsTableExists,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users') AS UsersTableExists,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadActivities') AS ActivitiesTableExists,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadScoringRules') AS ScoringRulesTableExists,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadAssignmentRules') AS AssignmentRulesTableExists,
        (SELECT COUNT(*) FROM Leads) AS LeadsCount,
        (SELECT COUNT(*) FROM Users) AS UsersCount,
        (SELECT COUNT(*) FROM LeadActivities) AS ActivitiesCount,
        (SELECT COUNT(*) FROM LeadScoringRules) AS ScoringRulesCount,
        (SELECT COUNT(*) FROM LeadAssignmentRules) AS AssignmentRulesCount;
"@

    try {
        $splat = @{
            ServerInstance = $ServerInstance
            Database       = $DatabaseName
            Query          = $query
        }

        if ($Credential) {
            $splat['Credential'] = $Credential
        }

        $result = Invoke-Sqlcmd @splat -ErrorAction Stop

        Write-Host "`nDatabase Objects:"
        Write-Host "  Tables:" -ForegroundColor Cyan
        Write-Host "    ✓ Leads: $(if($result.LeadsTableExists -eq 1) {'Created'} else {'Missing'})"
        Write-Host "    ✓ Users: $(if($result.UsersTableExists -eq 1) {'Created'} else {'Missing'})"
        Write-Host "    ✓ LeadActivities: $(if($result.ActivitiesTableExists -eq 1) {'Created'} else {'Missing'})"
        Write-Host "    ✓ LeadScoringRules: $(if($result.ScoringRulesTableExists -eq 1) {'Created'} else {'Missing'})"
        Write-Host "    ✓ LeadAssignmentRules: $(if($result.AssignmentRulesTableExists -eq 1) {'Created'} else {'Missing'})"

        Write-Host "`n  Data Counts:" -ForegroundColor Cyan
        Write-Host "    ✓ Leads: $($result.LeadsCount)"
        Write-Host "    ✓ Users: $($result.UsersCount)"
        Write-Host "    ✓ LeadActivities: $($result.ActivitiesCount)"
        Write-Host "    ✓ ScoringRules: $($result.ScoringRulesCount)"
        Write-Host "    ✓ AssignmentRules: $($result.AssignmentRulesCount)"

        return $true
    }
    catch {
        Write-Error-Custom "Verification failed: $_"
        return $false
    }
}

function Show-LeadDistribution {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential
    )

    Write-Header "Lead Score Distribution"

    $query = @"
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
    FROM [Leads]
    GROUP BY 
        CASE 
            WHEN [Score] >= 80 THEN 'High (80+)'
            WHEN [Score] >= 50 THEN 'Medium (50-79)'
            ELSE 'Low (<50)'
        END
    ORDER BY 
        CASE 
            WHEN [Score] >= 80 THEN 1
            WHEN [Score] >= 50 THEN 2
            ELSE 3
        END;
"@

    try {
        $splat = @{
            ServerInstance = $ServerInstance
            Database       = $DatabaseName
            Query          = $query
        }

        if ($Credential) {
            $splat['Credential'] = $Credential
        }

        $result = Invoke-Sqlcmd @splat -ErrorAction Stop

        Write-Host "`nLead Distribution:" -ForegroundColor Cyan
        foreach ($row in $result) {
            Write-Host "  $($row.PriorityLevel): $($row.LeadCount) leads (Avg Score: $([math]::Round($row.AvgScore, 2)))"
        }
    }
    catch {
        Write-Warning-Custom "Could not retrieve distribution: $_"
    }
}

# ============================================================================
# MAIN SCRIPT
# ============================================================================

# If -All is specified, set both switches
if ($All) {
    $CreateTables = $true
    $SeedData = $true
    $ShowVerification = $true
}

# Check prerequisites
if (-not (Test-SqlModule)) {
    exit 1
}

Write-Header "AMS CRM Database Setup"
Write-Info "Server: $ServerName"
Write-Info "Database: $DatabaseName"

# Test connection
$credential = Get-SqlCredential $Username $Password
if (-not (Test-DatabaseConnection $ServerName $DatabaseName $credential)) {
    exit 1
}

# Create tables
if ($CreateTables) {
    Write-Header "Creating Database Tables"
    $tableScript = Join-Path $ScriptPath "01-create-tables.sql"
    if (-not (Invoke-SqlScript $tableScript $ServerName $DatabaseName $credential)) {
        exit 1
    }
}

# Seed data
if ($SeedData) {
    Write-Header "Seeding Test Data"
    $seedScript = Join-Path $ScriptPath "03-seed-data-crm-3pages.sql"
    if (-not (Invoke-SqlScript $seedScript $ServerName $DatabaseName $credential)) {
        exit 1
    }
}

# Verification
if ($ShowVerification -or $CreateTables -or $SeedData) {
    if (-not (Verify-DatabaseSetup $ServerName $DatabaseName $credential)) {
        exit 1
    }

    Show-LeadDistribution $ServerName $DatabaseName $credential
}

Write-Header "Setup Complete"
Write-Success "All operations completed successfully"
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review seed data in the database"
Write-Host "  2. Update Entity Framework models if needed"
Write-Host "  3. Run Blazor pages for Lead Scoring, Assignment, and Follow-up`n"
