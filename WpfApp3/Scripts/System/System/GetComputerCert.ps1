
# Get domain and hostname info
$domain = ([System.DirectoryServices.ActiveDirectory.Domain]::GetCurrentDomain()).Name
$computerName = $env:COMPUTERNAME

# Cleanup old cert files
Remove-Item -Force -ErrorAction SilentlyContinue -Path "C:\drivers\newcer*"

# Discover the CA host name
$certca = ((certutil -adca | findstr /I dnshostname) | Out-String).Trim().Replace('dNSHostName = ', '')

# Discover the full CA config (e.g., CAHost\CAName)
$caConfigRaw = certutil | findstr Config | Out-String
$caConfig = $caConfigRaw.Substring(26).Trim()  # fragile but OK if format is stable

# Discover template name by parsing certutil output
$certTemplates = certutil -template | Out-String
$templateBlocks = $certTemplates -split "Template\[\d+\]:" | Where-Object { $_ -match "TemplatePropCommonName" }

$myTemplate = $null

foreach ($block in $templateBlocks) {
    if (
        $block -match "(?i)Allow Auto-Enroll.*?Domain Computers" -and
        $block -notmatch "Cisco" -and
        $block -notmatch "DomainIsolationWorkstat" -and
        $block -match "TemplatePropCommonName\s*=\s*(\S+)"
    ) {
        $myTemplate = $matches[1].Trim()
        break
    }
}

if (-not $myTemplate) {
    Write-Error "❌ No valid certificate template found for Domain Computers with Auto-Enroll."
    exit 1
}

# Build INF contents dynamically
$infContent = @"
[Version]
Signature="\$Windows NT\$"

[NewRequest]
Subject = "CN=$computerName.$domain"
KeySpec = 1
KeyLength = 4096
Exportable = FALSE
MachineKeySet = TRUE
SMIME = FALSE
PrivateKeyArchive = FALSE
UserProtected = FALSE
UseExistingKeySet = FALSE
ProviderName = "Microsoft RSA SChannel Cryptographic Provider"
ProviderType = 12
RequestType = PKCS10
KeyUsage = 0xa0

[RequestAttributes]
CertificateTemplate = $myTemplate
"@

# Write to file
$infPath = "C:\drivers\newcer.inf"
$reqPath = "C:\drivers\newcertreq.req"
$certPath = "C:\drivers\newcert.cer"
$infContent | Set-Content -Path $infPath -Encoding ASCII -Force

# Generate CSR
certreq -f -new "$infPath" "$reqPath"

# Submit request to CA
certreq -submit -q -kerberos `
  -config $caConfig `
  -nochallenge -adminforcemachine `
  "$reqPath" "$certPath"

# Accept the certificate and install it
certreq -accept "$certPath"

Write-Host "`n✅ Certificate issued and installed successfully from template '$myTemplate'."
