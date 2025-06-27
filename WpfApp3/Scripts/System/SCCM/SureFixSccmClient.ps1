$ErrorLog = 'C:\Windows\Temp\ccm_repair_errors.txt'
Remove-Item $ErrorLog -Force -ErrorAction SilentlyContinue

try {
    Copy-Item -Recurse -Force C:\windows\ccmsetup C:\windows\ccmsetupBak
} catch {
    $_ | Out-File -Append $ErrorLog
}

try {
    Start-Process -Wait C:\Windows\ccmsetup\ccmsetup.exe /uninstall
    taskkill /im ccmsetup.exe /f
    while($(get-process ccmsetup).Responding){
        taskkill /im ccmexec.exe /f
        start-sleep 1000
    }
    Start-Sleep 30
} catch {
    $_ | Out-File -Append $ErrorLog
}

try {
    Remove-Item C:\Windows\CCM -Recurse -Force
} catch {
    $_ | Out-File -Append $ErrorLog
}


try {
    Remove-Item C:\Windows\SMSCFG.ini -Force
} catch {
    $_ | Out-File -Append $ErrorLog
}
try{
$g = test-path C:\windows\ccm
if($g -eq 'true'){
    taskkill /im ccmsetup.exe /f
    taskkill /im msiexec.exe /f
    taskkill /im ccmexec.exe /f
    move-item C:\windows\ccm C:\windows\CCMold
}
}
catch{}
try {
    taskkill /im 'ccmsetup' /f
C:\windows\ccmsetupBak\ccmsetup.exe /service /forceinstall /retry:1 /mp:g46pawcmmp01.state.mn.gov SMSSITECODE=MN1 /BITSPriority:FOREGROUND RESETKEYINFORMATION=TRUE
  Start-Sleep 20
 #   schtasks /Run /TN 'Microsoft\\Configuration Manager\\Configuration Manager Client Retry Task'
}
catch {
    $_ | Out-File -Append $ErrorLog
}

if (-not (Test-Path $ErrorLog)) {
    'No errors detected during SCCM client repair' | Out-File -FilePath $ErrorLog
}
