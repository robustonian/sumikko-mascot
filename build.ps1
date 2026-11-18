param([string]$OutputDirectory = '', [switch]$IfOutdated)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
# verify.ps1 passes its own directory so a running bin\SumikkoCompanion.exe is left alone.
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'bin' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputFile = Join-Path $OutputDirectory 'SumikkoCompanion.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$sources = @(
    (Join-Path $projectRoot 'app\NativeSignals.cs'),
    (Join-Path $projectRoot 'app\CharacterView.cs'),
    (Join-Path $projectRoot 'app\CharacterProfile.cs'),
    (Join-Path $projectRoot 'app\CharacterActing.cs'),
    (Join-Path $projectRoot 'app\MotionTuning.cs'),
    (Join-Path $projectRoot 'app\TuningWindow.cs'),
    (Join-Path $projectRoot 'app\MouseMotionRange.cs'),
    (Join-Path $projectRoot 'app\MouseRangeWindow.cs'),
    (Join-Path $projectRoot 'app\MouseRangeCalibration.cs'),
    (Join-Path $projectRoot 'app\AudioDeviceWindow.cs'),
    (Join-Path $projectRoot 'app\SettingsStore.cs'),
    (Join-Path $projectRoot 'app\AppHost.cs')
)
foreach ($source in $sources) {
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
}
# Start.cmd passes -IfOutdated: build on the first run and again whenever a source file is
# newer than the program (for example after git pull), otherwise keep the existing program.
if ($IfOutdated -and (Test-Path -LiteralPath $outputFile)) {
    $built = (Get-Item -LiteralPath $outputFile).LastWriteTimeUtc.Ticks
    $inputs = @($sources) + @($MyInvocation.MyCommand.Path)
    $newest = ($inputs | ForEach-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc.Ticks } | Measure-Object -Maximum).Maximum
    if ($newest -le $built) { Write-Output $outputFile; return }
    # A running copy keeps the program file locked, so leave it and say how to update.
    $running = @(Get-Process -Name SumikkoCompanion -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $outputFile })
    if ($running.Count -gt 0) {
        Write-Warning 'The source has changed, but the app is running. Quit it from its menu and start it again to use the new version.'
        Write-Output $outputFile
        return
    }
}
$references = @(
    (Join-Path $framework 'WPF\PresentationCore.dll'),
    (Join-Path $framework 'WPF\PresentationFramework.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll'),
    (Join-Path $framework 'System.Drawing.dll')
)
$referenceArguments = @($references | ForEach-Object { '/reference:' + $_ })
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/out:' + $outputFile) $referenceArguments $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $outputFile
