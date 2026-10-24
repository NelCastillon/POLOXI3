$cs = "Data Source=sql5111.site4now.net;Initial Catalog=db_ace4f8_deliver888011;User Id=db_ace4f8_deliver888011_admin;Password=Rocky#12345;Encrypt=True;TrustServerCertificate=True"

$q = @"
SELECT WideExecutionId, StatusCode, CreatedDateUtc, CorrelationId
FROM POLOXI.Legal_WideExecution
WHERE CorrelationId IN (N'959fc234e5b549a29798b625debda0bf')
ORDER BY CreatedDateUtc DESC;

SELECT TOP 6 WideExecutionId, StatusCode, CreatedDateUtc, CorrelationId
FROM POLOXI.Legal_WideExecution
ORDER BY CreatedDateUtc DESC;

SELECT GETUTCDATE() AS ServerUtcNow;
"@

try {
	$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
	$cmd = $conn.CreateCommand(); $cmd.CommandText = $q
	$da = New-Object System.Data.SqlClient.SqlDataAdapter($cmd); $ds = New-Object System.Data.DataSet; [void]$da.Fill($ds)
	foreach ($t in $ds.Tables) { $t | Format-Table -AutoSize | Out-String -Width 200 | Write-Host; Write-Host "====" }
	$conn.Close()
} catch { Write-Host "ERR: $($_.Exception.Message)" }
