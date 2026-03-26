# Testing

This repository has two automated test entry points:

- `src/Test.Automated`
- `src/Test.XUnit`

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
