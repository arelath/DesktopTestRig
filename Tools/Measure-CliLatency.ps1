#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $Executable,
    [string[]] $CommandArguments = @('version'),
    [ValidateRange(3, 1000)] [int] $Iterations = 25,
    [int] $ExpectedExitCode = 0
)

$ErrorActionPreference = 'Stop'
foreach ($path in $Executable) {
    $resolvedPath = (Resolve-Path -LiteralPath $path).Path
    $samples = for ($index = 0; $index -lt $Iterations; $index++) {
        $startInfo = [Diagnostics.ProcessStartInfo]::new($resolvedPath)
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        foreach ($argument in $CommandArguments) { $startInfo.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        try {
            $timer = [Diagnostics.Stopwatch]::StartNew()
            [void] $process.Start()
            $outputTask = $process.StandardOutput.ReadToEndAsync()
            $errorTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(60000)) {
                $process.Kill($true)
                throw "CLI exceeded 60 seconds: $resolvedPath"
            }
            $timer.Stop()
            $outputText = $outputTask.GetAwaiter().GetResult()
            $errorText = $errorTask.GetAwaiter().GetResult()
            if ($process.ExitCode -ne $ExpectedExitCode) {
                throw "Unexpected exit code $($process.ExitCode): $outputText $errorText"
            }
            $timer.Elapsed.TotalMilliseconds
        }
        finally { $process.Dispose() }
    }
    $sorted = @($samples | Sort-Object)
    $middle = [int][Math]::Floor($Iterations / 2)
    $median = if ($Iterations % 2) { $sorted[$middle] } else { ($sorted[$middle - 1] + $sorted[$middle]) / 2 }
    [pscustomobject]@{
        Executable = $resolvedPath
        Arguments = $CommandArguments
        Iterations = $Iterations
        FirstMs = [Math]::Round($samples[0], 1)
        MedianMs = [Math]::Round($median, 1)
        P95Ms = [Math]::Round($sorted[[int][Math]::Ceiling($Iterations * 0.95) - 1], 1)
    }
}
