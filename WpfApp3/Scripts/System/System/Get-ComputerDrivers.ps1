param(
    [string]$Class,
    [string]$ComputerName

)

get-wmiobject -class $class -computername $computername