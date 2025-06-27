
function Run-Discovery {
    Write-Host "Running discovery protocol..."
$g =  Invoke-DiscoveryProtocolCapture  
$g2 = $G | Get-DiscoveryProtocolData
if($g2 -match 'WARNING:' -or $? -eq 'False'){
    Write-Host "Discovery protocol completed with warnings. No discovery data found"
}
else {
    Write-Host "Discovery protocol completed successfully."
    return $g
}
}
$h =  import-module PSDiscoveryProtocol
   if($?){
 $r =  Run-Discovery
return $R
   }

else {
    save-module PSDiscoveryProtocol -path C:\windows\temp  -force
    import-module C:\windows\temp\PSDiscoveryProtocol -force
    $r = Run-Discovery
    return $r


}

