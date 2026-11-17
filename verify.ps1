$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\WindowsBase.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
# bin is not tracked by git, so a fresh checkout does not have it yet.
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null

# The whole application must compile exactly as build.ps1 builds it. A separate output
# directory leaves a running bin\SumikkoCompanion.exe untouched.
& (Join-Path $root 'build.ps1') -OutputDirectory (Join-Path $bin 'verify')

# The checks include every application source, so the settings flow of the main window is tested too.
$testSources = @('CharacterView.cs','CharacterProfile.cs','CharacterActing.cs','MotionTuning.cs','TuningWindow.cs','MouseMotionRange.cs','MouseRangeCalibration.cs','CalibrationChecks.cs','NativeSignals.cs','AudioDeviceWindow.cs','AudioDeviceChecks.cs','MouseRangeWindow.cs','SettingsStore.cs','AppHost.cs','TuningChecks.cs','ActingChecks.cs','CharacterChecks.cs','RegressionTests.cs') | ForEach-Object { Join-Path $root ('app\' + $_) }
$testExe = Join-Path $bin 'RegressionTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /main:RegressionTests ('/out:'+$testExe) $references $testSources
if($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe $root
if($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
$previewExe = Join-Path $bin 'RenderPreview.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 ('/out:'+$previewExe) $references (Join-Path $root 'app\CharacterView.cs') (Join-Path $root 'app\CharacterProfile.cs') (Join-Path $root 'app\CharacterActing.cs') (Join-Path $root 'app\MotionTuning.cs') (Join-Path $root 'app\MouseMotionRange.cs') (Join-Path $root 'app\RenderPreview.cs')
if($LASTEXITCODE -ne 0) { throw 'Preview compilation failed.' }
& $previewExe $root
if($LASTEXITCODE -ne 0) { throw 'Preview render failed.' }
