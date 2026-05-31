# BenchmarkTool — Cross-Platform Desktop Process Benchmark

A modern desktop application for comparing the resource usage of two processes in real-time.  
Built with **C++20** (native metrics via Win32) and **C# / .NET 9 + Avalonia UI** (MVVM front-end).

---

## Architecture

```
BenchmarkTool/
│
├── Benchmark.Core/          C++20 DLL — Win32 metrics engine
│   ├── include/             Public headers (exported API + internals)
│   └── src/                 Implementation files
│
├── Benchmark.Models/        C# shared domain models (ProcessInfo, MetricSnapshot, …)
│
├── Benchmark.Interop/       C# P/Invoke layer (NativeMethods, NativeBenchmarkService)
│
└── Benchmark.UI/            C# Avalonia MVVM application
    ├── Services/            Application-layer service interfaces + implementations
    ├── ViewModels/          MVVM ViewModels (CommunityToolkit.Mvvm)
    ├── Views/               Avalonia AXAML views
    └── Assets/              Styles and resources
```

### Design Patterns

| Pattern | Where applied |
|---------|--------------|
| **RAII** | `HandleGuard` — owns Win32 HANDLEs |
| **Strategy** | `IMonitor` interface; `CpuMonitor`, `MemoryMonitor`, … are concrete strategies |
| **Facade** | `BenchmarkEngine` (C++) and `NativeBenchmarkService` (C#) |
| **Observer** | `IBenchmarkService.SnapshotsUpdated` event |
| **MVVM** | Full Avalonia MVVM with `CommunityToolkit.Mvvm` source generators |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` in `App.axaml.cs` |
| **Repository** | `BenchmarkSession` accumulates metric snapshots |

---

## Prerequisites

| Tool | Version |
|------|---------|
| Windows | 10 / 11 (x64) |
| .NET SDK | 9.0+ |
| CMake | 3.20+ |
| MSVC | Visual Studio 2022 (v143 toolset) or Build Tools |
| Git | any recent version |

---

## Build Instructions

### Step 1 — Build the C++ DLL

```powershell
# From the repo root
cd Benchmark.Core

cmake -B build -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

The output `Benchmark.Core.dll` is automatically copied to  
`Benchmark.UI/bin/Release/net9.0-windows/` by the CMake post-build step.

### Step 2 — Build and run the C# UI

```powershell
cd ..\Benchmark.UI
dotnet run -c Release
```

Or open `BenchmarkTool.sln` in Visual Studio 2022 and press F5.

> **Note:** Run the built `.exe` as administrator when you need metrics for protected  
> processes (e.g. system services). `dotnet run` works without elevation for normal development.

---

## Features

### MVP (Phase 1)
- Select two processes (running list or browse executable)
- Real-time metrics every 1 second:
  - CPU: current / average / peak
  - Memory: working set / private bytes / peak
  - Threads and handles
  - Disk I/O (cumulative)
  - Process lifetime / uptime
- Rolling 60-second charts (CPU, memory, threads) — LiveCharts2
- Side-by-side comparison dashboard
- Export to CSV or JSON

### Roadmap
- **Phase 2**: Browser startup benchmark, cold vs. warm start
- **Phase 3**: Multi-process sessions, historical reports
- **Phase 4**: Linux / macOS via platform abstraction layer

---

## Project Conventions

- **C++**: C++20, RAII everywhere, no raw owning pointers, `noexcept` on Win32 wrappers,  
  Doxygen comments on all public symbols.
- **C#**: nullable reference types enabled, `record` for immutable models,  
  `[ObservableProperty]` / `[RelayCommand]` source generators, no code-behind logic.
- **Naming**: `camelCase_` for private fields (C++ and C#), `PascalCase` for public members.

---

## License

MIT — see LICENSE file.
