param([string]$root,[string]$scratch)
$ErrorActionPreference='Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\WindowsBase.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $scratch 'Harness.exe')) $references (Join-Path $root 'app\CharacterView.cs') (Join-Path $root 'app\CharacterProfile.cs') (Join-Path $root 'app\CharacterActing.cs') (Join-Path $root 'app\MotionTuning.cs') (Join-Path $root 'app\MouseMotionRange.cs') (Join-Path $scratch 'Harness.cs')
if($LASTEXITCODE -ne 0){ throw 'compile failed' }
