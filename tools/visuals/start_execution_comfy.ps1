param(
    [string]$ComfyRoot = 'F:\ComfyUI\ComfyUI',
    [int]$Port = 8189
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$scratch = Join-Path $workspace '.cache\execution-remaster-20261115'
foreach ($name in @('input', 'output', 'temp', 'user')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $scratch $name) | Out-Null
}
$env:PYTHONUTF8 = '1'
Push-Location $ComfyRoot
try {
    # Keep dedicated VRAM headroom and avoid the earlier multi-tile shared-memory spill.
    python -u (Join-Path $ComfyRoot 'main.py') --listen 127.0.0.1 --port $Port `
        --reserve-vram 3.0 --disable-pinned-memory --disable-async-offload `
        --cache-lru 3 --preview-method none --disable-auto-launch `
        --input-directory (Join-Path $scratch 'input') `
        --output-directory (Join-Path $scratch 'output') `
        --temp-directory (Join-Path $scratch 'temp') `
        --user-directory (Join-Path $scratch 'user')
} finally {
    Pop-Location
}
