param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

$testXunitProjectPath = Join-Path $PSScriptRoot "Test.XUnit.csproj"

dotnet build $testXunitProjectPath -c $Configuration

dotnet test $testXunitProjectPath --no-build -c $Configuration -f $Framework --logger "console;verbosity=minimal"
