Dism /online /cleanup-image /startcomponentcleanup
sfc /scannow
dism /online /cleanup-image /restorehealth
sfc /scannow
chkdsk /x /f $env:SystemDrive
cleanmgr /verylowdiskspace
gpupdate /force
lodctr /r
