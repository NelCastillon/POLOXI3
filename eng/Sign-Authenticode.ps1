param(
	[Parameter(Mandatory = $true)]
	[string]$Thumbprint,

	[Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
	[string[]]$Files,

	[string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$normalizedThumbprint = $Thumbprint.Replace(" ", "").ToUpperInvariant()
$certificate = Get-ChildItem Cert:\CurrentUser\My |
	Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
	Select-Object -First 1

if ($null -eq $certificate) {
	throw "Code-signing certificate '$normalizedThumbprint' was not found in Cert:\CurrentUser\My."
}

if (-not $certificate.HasPrivateKey) {
	throw "Code-signing certificate '$normalizedThumbprint' does not have an accessible private key."
}

$codeSigningOid = "1.3.6.1.5.5.7.3.3"
$enhancedKeyUsageOids = $certificate.Extensions |
	Where-Object { $_.Oid.Value -eq "2.5.29.37" } |
	ForEach-Object { ([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]$_).EnhancedKeyUsages.Value }
if ($enhancedKeyUsageOids -notcontains $codeSigningOid) {
	throw "Certificate '$normalizedThumbprint' is not valid for code signing."
}

foreach ($file in $Files | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) {
	if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
		continue
	}

	$signature = Set-AuthenticodeSignature -FilePath $file -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
	if ($signature.Status -notin @("Valid", "UnknownError")) {
		throw "Authenticode signing failed for '$file': $($signature.StatusMessage)"
	}
}
