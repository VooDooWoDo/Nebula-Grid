$ErrorActionPreference = 'Stop'

$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

Get-CimInstance Win32_Process |
    Where-Object {
        (
            $_.Name -eq 'dotnet.exe' -and (
                $_.CommandLine -like '*NebulaGrid.Server*' -or
                ($_.CommandLine -like '*dotnet watch*' -and $_.CommandLine -like '*NebulaGrid.Server*')
            )
        ) -or (
            $_.Name -eq 'NebulaGrid.Server.exe'
        )
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

dotnet watch --no-hot-reload --project .\NebulaGrid.Server\NebulaGrid.Server.csproj