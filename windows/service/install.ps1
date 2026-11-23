#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Install the ControlHub Windows Service.
.DESCRIPTION
    Builds and installs the ControlHub Service as a Windows Service running as LocalService.
    Requires .NET 8 SDK and administrator privileges.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\ControlHub\Service",
    [string]$OwnerSid = ""
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'ControlHubService'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir 'ControlHub.Service'

# Resolve owner SID: from parameter, or from logged-in interactive user, or current user
if (-not $OwnerSid) {
    # Check for active explorer.exe session owner
    $explorerProc = Get-CimInstance Win32_Process -Filter "Name = 'explorer.exe'" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($explorerProc) {
        $owner = Invoke-CimMethod -InputObject $explorerProc -MethodName GetOwner -ErrorAction SilentlyContinue
        if ($owner -and $owner.User) {
            try {
                $ntAccount = New-Object System.Security.Principal.NTAccount($owner.Domain, $owner.User)
                $OwnerSid = $ntAccount.Translate([System.Security.Principal.SecurityIdentifier]).Value
            } catch {}
        }
    }
}
if (-not $OwnerSid) {
    # Fallback to current user SID
    $OwnerSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
}
Write-Host "Configuring service for Owner SID: $OwnerSid"

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping existing service to unlock binary files..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe delete failed with exit code $LASTEXITCODE."
    }
    Start-Sleep -Seconds 2
}

Write-Host "Building ControlHub Service..."
dotnet publish "$ProjectDir\ControlHub.Service.csproj" -c Release -o "$InstallDir" --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$ServiceExe = Join-Path $InstallDir 'ControlHub.Service.exe'
if (-not (Test-Path $ServiceExe)) {
    throw "Build failed to produce service executable at $ServiceExe."
}

Write-Host "Installing service: $ServiceName"
& sc.exe create $ServiceName binPath= "`"$ServiceExe`"" start= auto obj= "NT AUTHORITY\LocalService" DisplayName= "ControlHub Service"
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe create failed with exit code $LASTEXITCODE."
}

& sc.exe description $ServiceName "ControlHub remote management agent (machine service)."
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe description failed with exit code $LASTEXITCODE."
}

& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe failure failed with exit code $LASTEXITCODE."
}

# Provision Owner SID into service registry key
$ParamsKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName\Parameters"
if (-not (Test-Path $ParamsKey)) {
    New-Item -Path $ParamsKey -Force | Out-Null
}
Set-ItemProperty -Path $ParamsKey -Name "OwnerSid" -Value $OwnerSid -Type String
Write-Host "Configured OwnerSid in registry: $OwnerSid"

# 1. Register EventLog source under Application while elevated so LocalService does not need admin rights to log
if (-not [System.Diagnostics.EventLog]::SourceExists("ControlHub Service")) {
    Write-Host "Registering EventLog source 'ControlHub Service' under Application..."
    [System.Diagnostics.EventLog]::CreateEventSource("ControlHub Service", "Application")
    Write-Host "Registered EventLog source 'ControlHub Service'."
}

# 2. Ensure persistent log directory exists with restricted least-privilege ACL
$LogDir = Join-Path $env:ProgramData "ControlHub\logs"
if (-not (Test-Path $LogDir)) {
    New-Item -Path $LogDir -ItemType Directory -Force | Out-Null
}
# Strip inherited permissions (e.g. BUILTIN\Users) and grant explicit least-privilege access
& icacls.exe "$LogDir" /inheritance:r /q
& icacls.exe "$LogDir" /grant:r "NT AUTHORITY\LocalService:(OI)(CI)(M)" /q
& icacls.exe "$LogDir" /grant:r "NT AUTHORITY\SYSTEM:(OI)(CI)(F)" /q
& icacls.exe "$LogDir" /grant:r "BUILTIN\Administrators:(OI)(CI)(F)" /q
& icacls.exe "$LogDir" /grant:r "*${OwnerSid}:(OI)(CI)(RX)" /q
Write-Host "Configured restricted log directory: $LogDir with permissions for LocalService, SYSTEM, Administrators, and Owner SID ($OwnerSid)"

Write-Host "Starting service..."
Start-Service -Name $ServiceName
Write-Host "Done. Service status:"
Get-Service -Name $ServiceName | Format-List Name, Status, StartType
