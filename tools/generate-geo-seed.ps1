# Generates src/Ams.Infrastructure/Migrations/0124_GeoCityFullUsSeed.sql from the
# public us-state-county-zip dataset (state_fips,state,state_abbr,zipcode,county,city).
param(
	[string]$CsvPath = "$env:TEMP\us_zips.csv",
	[string]$OutPath = "src\Ams.Infrastructure\Migrations\0124_GeoCityFullUsSeed.sql"
)

$rows = Import-Csv $CsvPath
$clean = $rows | Where-Object { $_.city -and $_.state_abbr -and $_.zipcode } | ForEach-Object {
	[pscustomobject]@{
		State  = $_.state_abbr.Trim().ToUpperInvariant()
		City   = ($_.city.Trim() -replace '\s+', ' ')
		County = if ([string]::IsNullOrWhiteSpace($_.county)) { $null } else { ($_.county.Trim() -replace '\s+', ' ') }
		Zip    = $_.zipcode.Trim().PadLeft(5, '0')
	}
} | Where-Object { $_.State.Length -le 3 -and $_.City.Length -le 120 -and $_.Zip -match '^\d{5}$' }

function Esc([string]$s) { if ($null -eq $s) { return $null } $s -replace "'", "''" }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("-- 0124_GeoCityFullUsSeed.sql")
[void]$sb.AppendLine("-- Full US city/county/ZIP reference seed generated from the public")
[void]$sb.AppendLine("-- us-state-county-zip dataset. Idempotent set-based inserts; rows already")
[void]$sb.AppendLine("-- present (including 0123 seed data) are left untouched.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("IF OBJECT_ID(N'tempdb..#GeoSeed') IS NOT NULL DROP TABLE #GeoSeed;")
[void]$sb.AppendLine("CREATE TABLE #GeoSeed (StateCode NVARCHAR(3) NOT NULL, CityName NVARCHAR(120) NOT NULL, County NVARCHAR(120) NULL, PostalCode NVARCHAR(12) NOT NULL);")
[void]$sb.AppendLine("")

$batchSize = 1000
for ($i = 0; $i -lt $clean.Count; $i += $batchSize) {
	$batch = $clean[$i..([Math]::Min($i + $batchSize - 1, $clean.Count - 1))]
	$values = $batch | ForEach-Object {
		$county = if ($null -eq $_.County) { 'NULL' } else { "N'$(Esc $_.County)'" }
		"(N'$($_.State)', N'$(Esc $_.City)', $county, N'$($_.Zip)')"
	}
	[void]$sb.AppendLine("INSERT INTO #GeoSeed (StateCode, CityName, County, PostalCode) VALUES")
	[void]$sb.AppendLine(($values -join ",`r`n"))
	[void]$sb.AppendLine(";")
}

[void]$sb.AppendLine(@"

-- Insert cities that do not exist yet (matches UQ_GeoCity_Country_State_City).
INSERT INTO Location.GeoCity (GeoCityId, CountryCode, StateCode, CityName, County, SourceCode)
SELECT NEWID(), N'US', s.StateCode, s.CityName, MIN(s.County), N'Seed'
FROM #GeoSeed s
WHERE NOT EXISTS (
	SELECT 1 FROM Location.GeoCity c
	WHERE c.CountryCode = N'US' AND c.StateCode = s.StateCode AND c.CityName = s.CityName)
GROUP BY s.StateCode, s.CityName;

-- Backfill county where missing.
UPDATE c SET c.County = x.County, c.ModifiedDateUtc = SYSUTCDATETIME()
FROM Location.GeoCity c
JOIN (SELECT StateCode, CityName, MIN(County) AS County FROM #GeoSeed WHERE County IS NOT NULL GROUP BY StateCode, CityName) x
	ON c.CountryCode = N'US' AND c.StateCode = x.StateCode AND c.CityName = x.CityName
WHERE c.County IS NULL;

-- Insert postal codes that do not exist yet (matches UQ_GeoPostalCode_City_Postal).
INSERT INTO Location.GeoPostalCode (GeoPostalCodeId, GeoCityId, PostalCode, SourceCode)
SELECT NEWID(), c.GeoCityId, s.PostalCode, N'Seed'
FROM (SELECT DISTINCT StateCode, CityName, PostalCode FROM #GeoSeed) s
JOIN Location.GeoCity c
	ON c.CountryCode = N'US' AND c.StateCode = s.StateCode AND c.CityName = s.CityName
WHERE NOT EXISTS (
	SELECT 1 FROM Location.GeoPostalCode p
	WHERE p.GeoCityId = c.GeoCityId AND p.PostalCode = s.PostalCode);

DROP TABLE #GeoSeed;
"@)

Set-Content -Path $OutPath -Value $sb.ToString() -Encoding UTF8
"Generated $OutPath with $($clean.Count) rows ($((($clean | Select-Object State, City -Unique).Count)) distinct cities)."
