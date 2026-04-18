$ErrorActionPreference = 'Stop'

$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'

$serverPatterns = @(
    '*NebulaGrid.Server*',
    '*dotnet watch*'
)

Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'dotnet.exe' -and (
            $_.CommandLine -like '*NebulaGrid.Server*' -or
            ($_.CommandLine -like '*dotnet watch*' -and $_.CommandLine -like '*NebulaGrid.Server*')
        )
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force
    }

dotnet watch --no-hot-reload --project .\NebulaGrid.Server\NebulaGrid.Server.csproj