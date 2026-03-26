param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testXunitProjectPath = Join-Path $PSScriptRoot "Test.XUnit.csproj"
$testAutomatedExecutablePath = Join-Path $repositoryRoot "src\Test.Automated\bin\$Configuration\$Framework\Test.Automated.exe"
$testXunitOutputPath = Join-Path $repositoryRoot "src\Test.XUnit\bin\$Configuration\$Framework"
$resultsPath = Join-Path $testXunitOutputPath "shared-automated-results.json"
$generatorScriptPath = Join-Path $PSScriptRoot "RunSharedAutomatedResults.ps1"

dotnet build $testXunitProjectPath -c $Configuration

powershell -ExecutionPolicy Bypass -File $generatorScriptPath `
    -AutomatedExecutablePath $testAutomatedExecutablePath `
    -WorkingDirectory (Join-Path $repositoryRoot "src\Test.Automated\bin\$Configuration\$Framework\.") `
    -ResultsPath $resultsPath

dotnet test $testXunitProjectPath --no-build -c $Configuration --logger "console;verbosity=minimal"
