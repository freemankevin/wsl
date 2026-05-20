# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WSL Manager is a WPF desktop application for managing Windows Subsystem for Linux distributions. It provides a GUI for starting/stopping distributions, importing/exporting TAR backups, monitoring resource usage with real-time charts, and controlling the WSL service.

## Build & Run Commands

| Task | Command |
|------|---------|
| Build solution | `dotnet build WSLManager.sln` |
| Run app | `dotnet run --project src/WSLManager` |
| Full release + MSI | `powershell -ExecutionPolicy Bypass -File build.ps1` (requires Admin) |
| Restore packages | `dotnet restore WSLManager.sln` |

**Prerequisites:** .NET 8 SDK, WiX Toolset v4 (`dotnet tool install --global wix`). The build script and WPF app require Windows.

## Solution Architecture

### Projects

- **`src/WSLManager`** — WPF entry point (MVVM, views, converters, themes). References Core.
- **`src/WSLManager.Core`** — Models and services. No WPF dependency; can be reused or tested independently.
- **`src/WSLManager.Setup`** — WiX v4 MSI installer. Publishes `WSLManager.exe` as a single file and packages it into an `.msi`.

### Dependency Injection

`App.xaml.cs` is the composition root. Services are registered as singletons:

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<IWslService, WslService>();
services.AddSingleton<IResourceMonitorService, ResourceMonitorService>();
services.AddSingleton<MainViewModel>();
```

ViewModels access services via the static `App.Services` property.

### Key Services

- **`WslService`** — Shells out to `wsl.exe` for all WSL operations (list, start, stop, export, import, unregister, shutdown). Also controls Windows services (`LxssManager`, `wslservice`) via `ServiceController`.
- **`ResourceMonitorService`** — Polls `Process.GetProcesses()` for WSL-related processes (`wsl`, `vmcompute`, `vmmem`) and exposes resource info via `ResourceInfoUpdated` event. Stores a rolling history of ~120 samples.
- **`SettingsService`** — Persists `AppSettings` to JSON in the user's app data folder.

### UI & MVVM

- `MainViewModel` drives the main window. Uses `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).
- Charts use **LiveCharts2** (`LiveChartsCore.SkiaSharpView.WPF`). Series data is stored in `ObservableCollection<ObservableValue>` and updated on a timer.
- System tray icon via **Hardcodet.NotifyIcon.Wpf**. Closing the main window hides it to tray instead of exiting.
- Themes are switched by replacing merged dictionaries at runtime (`Themes/Dark.xaml`, `Themes/Light.xaml`).

### Concurrency Notes

- `MainViewModel` uses a `System.Timers.Timer` for auto-refresh. UI updates are dispatched via `Application.Current.Dispatcher.Invoke` because the timer fires on a thread-pool thread.
- `ResourceMonitorService` uses a `lock` around timer start/stop and a `ConcurrentQueue` for history.
- `WslService.RunCommandAsync` registers a cancellation callback that kills the `wsl.exe` process.

## Adding New WSL Operations

To add a new WSL command:
1. Add the method signature to `IWslService` in `Core/Services/`.
2. Implement it in `WslService` by calling `RunCommandAsync` with the appropriate `wsl.exe` arguments.
3. Expose it in `MainViewModel` as an `ICommand` bound to the UI.

## Testing

There are currently no test projects in the solution. Core services like `WslService` and `ResourceMonitorService` are good candidates for unit testing since they live in `WSLManager.Core` and have no WPF dependencies.
