$cs = "Data Source=sql5111.site4now.net;Initial Catalog=db_ace4f8_deliver888011;User Id=db_ace4f8_deliver888011_admin;Password=Rocky#12345;Encrypt=True;TrustServerCertificate=True"

$q = @"
SELECT TOP 3 WideExecutionId, StatusCode, CreatedDateUtc FROM POLOXI.Legal_WideExecution ORDER BY CreatedDateUtc DESC;

SELECT TOP 1 WideExecutionId INTO #last FROM POLOXI.Legal_WideExecution ORDER BY CreatedDateUtc DESC;
SELECT
  SUM(CASE WHEN IsConstraintViolation=1 THEN 1 ELSE 0 END) AS Violations,
  SUM(CASE WHEN IsConstraintViolation=0 THEN 1 ELSE 0 END) AS Admitted,
  COUNT(*) AS Total
FROM POLOXI.Legal_WideCandidate WHERE WideExecutionId=(SELECT WideExecutionId FROM #last) AND IsDeleted=0;

SELECT TOP 12 DisplayName, CompositeScore, RankNumber, IsConstraintViolation
FROM POLOXI.Legal_WideCandidate WHERE WideExecutionId=(SELECT WideExecutionId FROM #last) AND IsDeleted=0
ORDER BY IsConstraintViolation, RankNumber;
DROP TABLE #last;
"@

try {
	$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
	$cmd = $conn.CreateCommand(); $cmd.CommandText = $q
	$da = New-Object System.Data.SqlClient.SqlDataAdapter($cmd); $ds = New-Object System.Data.DataSet; [void]$da.Fill($ds)
	foreach ($t in $ds.Tables) { $t | Format-Table -AutoSize | Out-String -Width 160 | Write-Host; Write-Host "====" }
	$conn.Close()
} catch { Write-Host "ERR: $($_.Exception.Message)" }
