param(
    [ValidateSet('install', 'configure', 'status')]
    [string]$Action = 'status'
)

$ErrorActionPreference = 'Stop'

switch ($Action) {
    'install' {
        if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
            throw 'Install uv first (https://docs.astral.sh/uv/), then rerun this script.'
        }
        uv tool install graphifyy
        if ($LASTEXITCODE -ne 0) { throw 'Graphify CLI installation failed.' }
        if (Get-Command graphify -ErrorAction SilentlyContinue) {
            graphify install --project --platform codex
        } else {
            uv tool run --from graphifyy graphify install --project --platform codex
        }
        if ($LASTEXITCODE -ne 0) { throw 'Graphify Codex project setup failed.' }
        Write-Host 'Graphify is installed for this project. In Codex, run /graphify . to build the full project graph.'
    }
    'configure' {
        if (-not (Get-Command graphify -ErrorAction SilentlyContinue)) {
            if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
                throw 'Graphify CLI and uv are missing. Run .\scripts\graphify.ps1 install first.'
            }
            uv tool run --from graphifyy graphify install --project --platform codex
            if ($LASTEXITCODE -ne 0) { throw 'Graphify Codex project setup failed.' }
            break
        }
        graphify install --project --platform codex
        if ($LASTEXITCODE -ne 0) { throw 'Graphify Codex project setup failed.' }
    }
    'status' {
        if (Test-Path 'graphify-out\graph.json') {
            Write-Host 'Graph data found: graphify-out\graph.json'
            if (Get-Command graphify -ErrorAction SilentlyContinue) {
                graphify query 'What are the main modules and how do they connect?' --budget 500 --depth 1
            }
        } else {
            Write-Host 'No project graph yet. Install/configure Graphify, then run /graphify . in Codex.'
        }
    }
}
