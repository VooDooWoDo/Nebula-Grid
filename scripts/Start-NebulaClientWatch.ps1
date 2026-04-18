$ErrorActionPreference = 'Stop'

$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'

Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'dotnet.exe' -and (
            $_.CommandLine -like '*NebulaGrid.Client*' -or
            $_.CommandLine -like '*blazor-devserver*'
        )
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force
    }

dotnet watch --no-hot-reload --project .\NebulaGrid.Client\NebulaGrid.Client.csproj