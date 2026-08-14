# BoursePilotAI — Project Memory

## Repository
- GitHub: https://github.com/majid10243/BoursePilotAI.git
- Two long-lived branches:
  - `main` — minimal WPF app (no market-data services yet).
  - `agent/market-data-dashboard` — `main` + market data dashboard/scanner (TsetmcService, CodalService, DataSyncService, LocalDataStore). The TSETMC/Codal code lives **only** on this branch.

## Tech stack / structure
- WPF .NET 8 (`net8.0-windows`, `UseWPF`), single project `Source_Code/BoursePilotAI.App`.
- No NuGet packages beyond the BCL; config is read with `System.Text.Json` (deliberately, to avoid adding `Microsoft.Extensions.Configuration` deps).
- `ImplicitUsings` + `Nullable` enabled. Namespaces: `BoursePilotAI` (App/MainWindow/StockItem), `BoursePilotAI.Models`, `BoursePilotAI.Services`.

## Build constraint (important)
- The .NET SDK is **not installed** in this Linux container, and the project is `net8.0-windows` + WPF, so it can only be built on **Windows**. Do not attempt `dotnet build` here; verify by inspection instead and ask the user to build on Windows.

## TSETMC configuration (fix landed on branch `fix/tsetmc-configuration`)
- Root problem was equivalent to "پیکربندی نقطه TSETMC ناقص است": TSETMC endpoints were hardcoded consts in `TsetmcService` with no config source, and `App` constructed no options.
- Fix introduced `Models/TsetmcOptions.cs` (BaseUrl/MarketWatchPath/HistoryPathTemplate/TimeoutSeconds/MaxRetries + `Validate()` that throws a clear Persian `InvalidOperationException` when incomplete).
- `appsettings.json` (copied to output via `<None Update="appsettings.json"><CopyToOutputDirectory>PreserveNewest`) holds the `Tsetmc` section.
- `App.xaml.cs` loads + validates `TsetmcOptions` at startup and exposes `static App.TsetmcOptions`.
- `TsetmcService` now takes `TsetmcOptions`, builds URIs from it, validates in ctor, and uses `RetryCount`.
- `MainWindow` passes `App.TsetmcOptions` into `new TsetmcService(...)` and sets `HttpClient.Timeout` from options.

## Notes on prior-work summaries
- The classes `MarketDataOptions`/`TsetmcMarketDataService`, the exact error string, and commit `2baae46` referenced in some task prompts do **not** exist in this repo. Map them to `TsetmcOptions`/`TsetmcService` respectively.
