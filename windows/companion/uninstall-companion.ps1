$TaskName = 'ControlHub Companion'
Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
Write-Host "Companion task removed."
