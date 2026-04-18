# Nebula-Grid
IdleGame

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
