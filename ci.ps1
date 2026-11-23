#!/usr/bin/env pwsh
<#
.SYNOPSIS
    ControlHub Local CI Verification Script (M0 / M1)
.DESCRIPTION
    Runs formatters, linters, and automated test suites across all repository components.
#>

[CmdletBinding()]
param()

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$PythonExe = Join-Path $Root 'backend\.venv\Scripts\python.exe'
$failures = @()

# Ensure localappdata dotnet path is prioritized if installed there
if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
}

function Run-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "`n==> Step: $Name" -ForegroundColor Cyan
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "Command exited with status $LASTEXITCODE"
        }
        Write-Host "  [PASS] $Name" -ForegroundColor Green
    }
    catch {
        Write-Host "  [FAIL] $Name - $_" -ForegroundColor Red
        $script:failures += $Name
    }
}

# 1. Python Backend Verification
Run-Step "Python Backend Tests & Assertions" {
    if (-not (Test-Path $PythonExe)) {
        throw "Python virtual environment not found at $PythonExe."
    }
    Set-Location (Join-Path $Root 'backend')
    & $PythonExe -m pytest tests/ -v --tb=short
    Set-Location $Root
}

# 2. Flutter Mobile Verification
Run-Step "Flutter Mobile Lint & Static Analysis" {
    Set-Location (Join-Path $Root 'apps\mobile')
    flutter pub get
    flutter analyze .
    Set-Location $Root
}

Run-Step "Flutter Mobile Unit Tests" {
    Set-Location (Join-Path $Root 'apps\mobile')
    flutter test
    Set-Location $Root
}

# 3. .NET Windows Components Verification
Run-Step ".NET Windows Solution Build Check" {
    $sdkCheck = dotnet --list-sdks 2>&1
    if (-not $sdkCheck) {
        throw ".NET SDK is missing! .NET 8 SDK is required to build Windows components."
    }
    $sln = Join-Path $Root 'windows\ControlHub.Windows.sln'
    dotnet build $sln --nologo --configuration Debug
}

Run-Step ".NET Windows Integration Tests" {
    $testProj = Join-Path $Root 'windows\tests\ControlHub.IntegrationTests\ControlHub.IntegrationTests.csproj'
    dotnet test $testProj --nologo --configuration Release --logger "console;verbosity=normal"
}

Write-Host "`n----------------------------------------"
if ($failures.Count -eq 0) {
    Write-Host "ALL CHECKS PASSED SUCCESSFULLY!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "CI RUN FAILED in the following steps: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}
