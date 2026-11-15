param(
    [Parameter(Mandatory = $true)][int]$ComfyProcessId,
    [int]$Port = 8189
)
$ErrorActionPreference = 'Stop'
# Monitor only this remaster server; interrupt its queue if shared GPU memory spills.
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$report = Join-Path $workspace '.cache\execution-remaster-20261115\gpu-memory.json'
$state = @{ pid = $ComfyProcessId; dedicated_peak_bytes = 0L; shared_peak_bytes = 0L; samples = 0; stopped_for_shared_memory = $false }
$excessive = 0
while (Get-Process -Id $ComfyProcessId -ErrorAction SilentlyContinue) {
    $rows = Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory |
        Where-Object { $_.Name -match ('pid_' + $ComfyProcessId + '_') }
    $dedicated = ($rows | Measure-Object DedicatedUsage -Sum).Sum
    $shared = ($rows | Measure-Object SharedUsage -Sum).Sum
    $state.dedicated_peak_bytes = [Math]::Max($state.dedicated_peak_bytes, $dedicated)
    $state.shared_peak_bytes = [Math]::Max($state.shared_peak_bytes, $shared)
    $state.samples++
    $state.dedicated_bytes = $dedicated
    $state.shared_bytes = $shared
    $state.updated = (Get-Date).ToString('o')
    if ($shared -gt 1GB) { $excessive++ } else { $excessive = 0 }
    if ($excessive -ge 2) {
        Invoke-RestMethod -Uri ("http://127.0.0.1:$Port/queue") -Method Post -ContentType 'application/json' -Body '{"clear":true}' | Out-Null
        Invoke-RestMethod -Uri ("http://127.0.0.1:$Port/interrupt") -Method Post -ContentType 'application/json' -Body '{}' | Out-Null
        $state.stopped_for_shared_memory = $true
    }
    $state | ConvertTo-Json | Set-Content -LiteralPath ($report + '.tmp') -Encoding utf8
    Move-Item -LiteralPath ($report + '.tmp') -Destination $report -Force
    if ($state.stopped_for_shared_memory) { break }
    Start-Sleep -Seconds 2
}
