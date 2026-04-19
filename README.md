# Nebula-Grid
IdleGame

## Version and Change Log

- Current app version is defined in `NebulaGrid.Client/NebulaGrid.Client.csproj` via the `Version` property.
- Every accepted change request should increment the version and add a short summary entry to `CHANGELOG.md`.
- Keep entries concise and in English.

## Dev Watch Workflow

Use the VS Code tasks instead of manually starting multiple `dotnet watch` instances:

- `Nebula: Watch Server`
- `Nebula: Watch Client`
- `Nebula: Watch All`

These tasks run the scripts in `scripts/` which first stop stale NebulaGrid watch processes for the same project before starting a fresh watcher. This avoids the recurring `MSB3027` / `MSB3021` file-lock errors on `NebulaGrid.Shared.dll` and `NebulaGrid.Server.exe`.

### Stable Browser Testing

For normal browser testing, use the hosted server on `http://localhost:5237`.

- `http://localhost:5237/accounts`
- `http://localhost:5237/playerselection`

The server now serves the Blazor WebAssembly client directly, so `watch-server.cmd` is the stable path for running and testing the app in the browser.

`http://localhost:5028` is still the standalone client dev server, but it can be flaky in the integrated browser. Use it only if you explicitly want the client-only watcher.

### Fix: Failed to Start Platform (dotnet.js)

If you see:

- `Startup error: Failed to start platform`
- `TypeError: error loading dynamically imported module .../_framework/dotnet.js`

and `dotnet clean` + `dotnet build` did not fix it, use this full reset sequence.

1. Stop all running `dotnet` / Nebula processes.
2. Delete `bin` and `obj` folders for all projects.
3. Delete static web assets cache files.
4. Restore and rebuild.
5. Start only the server watch and use `http://localhost:5237`.

or 

Restart Visual Studio Code

PowerShell example from repo root:

```powershell
Get-Process -Name dotnet,NebulaGrid -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item -Recurse -Force .\NebulaGrid.Client\bin, .\NebulaGrid.Client\obj, .\NebulaGrid.Server\bin, .\NebulaGrid.Server\obj, .\NebulaGrid.Shared\bin, .\NebulaGrid.Shared\obj -ErrorAction SilentlyContinue

Get-ChildItem -Recurse -File -Filter "staticwebassets*.cache" | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem -Recurse -File -Filter "*.staticwebassets.*" | Remove-Item -Force -ErrorAction SilentlyContinue

dotnet restore .\NebulaGrid.sln
dotnet build .\NebulaGrid.sln
```

Then run:

- `./watch-server.cmd`

Open:

- `http://localhost:5237`

If you must use `http://localhost:5028`, do a hard reload (`Ctrl+F5`) after startup.

### Important

`Nebula: Watch All` is a VS Code task label, not a PowerShell command.

Use it like this inside VS Code:

1. `Terminal` -> `Run Task`
2. Choose `Nebula: Watch All`

or:

1. `Ctrl+Shift+P`
2. Run `Tasks: Run Task`
3. Choose `Nebula: Watch All`

### Direct Terminal Commands

If you want to start watchers from a PowerShell terminal instead of a VS Code task, use these commands:

- `./watch-server.cmd`
- `./watch-client.cmd`

If you only want one process for normal development and browser checks, start `./watch-server.cmd` and open the app through `http://localhost:5237`.

Or call the PowerShell scripts directly:

- `powershell -ExecutionPolicy Bypass -File .\scripts\Start-NebulaServerWatch.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\Start-NebulaClientWatch.ps1`

`Watch All` cannot work as one normal shell command in a single terminal because both watchers are long-running foreground processes. For both together, use the VS Code task `Nebula: Watch All`.
