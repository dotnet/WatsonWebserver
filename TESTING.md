# Testing

This repository has two automated test entry points:

- `src/Test.Automated`
- `src/Test.XUnit`

It also includes a benchmark harness:

- `src/Test.Benchmark`

## Test.Automated

`Test.Automated` is the primary automated console suite. It contains the migrated legacy coverage plus the added optimization-safety coverage.

Run it with:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

Behavior:

- Prints one normalized pass/fail line per test
- Prints an overall pass/fail summary with total runtime
- Enumerates failed tests at the end when failures occur

## Test.XUnit

`Test.XUnit` mirrors the same coverage surface as `Test.Automated`, but it consumes a shared serialized results artifact instead of re-implementing the test logic independently.

The stable execution flow is:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

That script performs these steps:

1. Builds `src/Test.XUnit/Test.XUnit.csproj`
2. Generates `src/Test.XUnit/bin/Debug/net10.0/shared-automated-results.json` using the shared automated runner
3. Executes `dotnet test --no-build`

## Test.Benchmark

`Test.Benchmark` is the performance harness used to compare Watson 7 against Watson 6, WatsonLite6, and Kestrel across supported protocols and scenarios.

Run it with:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets all --protocols http1,http2,http3 --scenarios hello,json
```

Behavior:

- Prints one formatted line per benchmark combination during the live run
- Prints a summary table after completion
- Prints protocol comparison tables after the summary

Useful examples:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1 --scenarios hello,json --warmup-seconds 2 --duration-seconds 5 --concurrency 16
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets all --protocols http3 --scenarios echo,json-echo --warmup-seconds 2 --duration-seconds 5 --concurrency 16
```

Notes:

- `Watson6`, `WatsonLite6`, and `Kestrel` availability depends on the selected protocol and scenario
- some combinations are intentionally skipped when the harness does not support them
- HTTP/3 benchmarking depends on local QUIC support

## Supporting Scripts

### `src/Test.XUnit/Run-Test.XUnit.ps1`

Wrapper for the full stable xUnit flow.

### `src/Test.XUnit/RunSharedAutomatedResults.ps1`

Generates the shared `shared-automated-results.json` artifact consumed by `Test.XUnit`.

Example:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\RunSharedAutomatedResults.ps1 `
  -AutomatedExecutablePath "C:\Code\Dotnet\WatsonWebserver-7.0\src\Test.Automated\bin\Debug\net10.0\Test.Automated.exe" `
  -WorkingDirectory "C:\Code\Dotnet\WatsonWebserver-7.0\src\Test.Automated\bin\Debug\net10.0\." `
  -ResultsPath "C:\Code\Dotnet\WatsonWebserver-7.0\src\Test.XUnit\bin\Debug\net10.0\shared-automated-results.json"
```

## Recommended Usage

For direct coverage validation during development:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

For CI-style xUnit validation in this repository:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

For local performance validation:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1,http2,http3 --scenarios hello,json,echo,json-echo
```
