Get-USB -computerName $TargetPC|
		Select-Object SystemName,Manufacturer,Name|
		Sort-Object Manufacturer|
		Format-Table -AutoSize|Out-String