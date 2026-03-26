param(
    [Parameter(Mandatory = $true)]
    [string]$AutomatedExecutablePath,
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,
    [Parameter(Mandatory = $true)]
    [string]$ResultsPath,
    [int]$TimeoutMilliseconds = 240000
)

$ErrorActionPreference = "Stop"

if (Test-Path $ResultsPath)
{
    Remove-Item $ResultsPath -Force
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = "cmd.exe"
$startInfo.Arguments = "/c set `"WATSON_TEST_AUTOMATED_RESULTS_PATH=$ResultsPath`" && start `"`" /wait `"$AutomatedExecutablePath`""
$startInfo.WorkingDirectory = $WorkingDirectory
$startInfo.UseShellExecute = $true
$startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo

try
{
    if (-not $process.Start())
    {
        throw "Unable to start shared automated test process."
    }

    if (-not $process.WaitForExit($TimeoutMilliseconds))
    {
        try
        {
            $process.Kill($true)
        }
        catch
        {
        }

        throw "Shared automated test process exceeded timeout of $TimeoutMilliseconds ms."
    }

    if ($process.ExitCode -ne 0)
    {
        throw "Shared automated test process exited with code $($process.ExitCode)."
    }

    if (-not (Test-Path $ResultsPath))
    {
        throw "Shared automated results file was not created."
    }
}
finally
{
    $process.Dispose()
}
