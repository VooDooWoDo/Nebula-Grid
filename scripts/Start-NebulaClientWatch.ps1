$ErrorActionPreference = 'Stop'

$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$clientPort = 5028
$blockedByProcessIds = Get-NetTCPConnection -LocalPort $clientPort -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique

foreach ($processId in $blockedByProcessIds)
{
    if ($processId -and $processId -gt 0)
    {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}

Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'dotnet.exe' -and (
            $_.CommandLine -like '*NebulaGrid.Client*' -or
            $_.CommandLine -like '*blazor-devserver*'
        )
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

dotnet watch --no-hot-reload --project .\NebulaGrid.Client\NebulaGrid.Client.csproj --property:DebugType=None --property:DebugSymbols=false