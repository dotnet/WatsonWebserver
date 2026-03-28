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

`Test.XUnit` is the CI-oriented xUnit runner. It shares reusable logic through `Test.Shared` and does not invoke `Test.Automated`.

The stable execution flow is:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

That script performs these steps:

1. Builds `src/Test.XUnit/Test.XUnit.csproj`
2. Executes `dotnet test --no-build`

If you want console output that shows each xUnit test with its pass/fail result and runtime, run:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj --no-build -c Debug -f net10.0 --logger "console;verbosity=detailed"
```

Notes:

- `verbosity=detailed` is what causes per-test output to appear
- `--no-build` keeps the console output focused on test execution if the project is already built
- omit `--no-build` if you want `dotnet test` to build first

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

## Recommended Usage

For direct coverage validation during development:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

For CI-style xUnit validation in this repository:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

For local xUnit runs where you want to see each test result and elapsed time:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj --no-build -c Debug -f net10.0 --logger "console;verbosity=detailed"
```

For local performance validation:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1,http2,http3 --scenarios hello,json,echo,json-echo
```
