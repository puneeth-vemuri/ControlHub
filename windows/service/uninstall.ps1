#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$ServiceName = 'ControlHubService'
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName
    Write-Host "Service removed."
} else {
    Write-Host "Service not found."
}
