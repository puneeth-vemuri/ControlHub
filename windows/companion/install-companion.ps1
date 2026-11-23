<#
.SYNOPSIS
    Install the ControlHub Companion to run at user sign-in via Task Scheduler.
.DESCRIPTION
    Builds the companion and registers a Task Scheduler logon trigger.
    Runs for the current user. No elevation required for registration,
    but build output requires the install directory to be writable.
    NOTE: Pre-login features are unavailable. Companion starts only after user sign-in.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\ControlHub\Companion"
)

$ErrorActionPreference = 'Stop'
$TaskName = 'ControlHub Companion'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir 'ControlHub.Companion'

# Stop existing companion process if running to release file locks
Stop-Process -Name "ControlHub.Companion" -Force -ErrorAction SilentlyContinue

Write-Host "Building ControlHub Companion..."
dotnet publish "$ProjectDir\ControlHub.Companion.csproj" -c Release -o "$InstallDir" --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$ExePath = Join-Path $InstallDir 'ControlHub.Companion.exe'
if (-not (Test-Path $ExePath)) {
    throw "Build failed to produce companion executable at $ExePath."
}

# Remove existing task if present
Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue

$Action = New-ScheduledTaskAction -Execute $ExePath
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit '00:00:00' -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -RunLevel Limited -Force

Write-Host "Companion registered. It will start at next sign-in."
Write-Host "To start it now for testing: Start-ScheduledTask -TaskName '$TaskName'"
