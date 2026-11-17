# Reproducible visual checks. Only preview/koji-chan is written; user settings are not read.
param([switch]$LightOnly)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$out = Join-Path $root 'preview/koji-chan'
$scratch = Join-Path $out 'harness'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$source = Get-Content -LiteralPath (Join-Path $root 'tools/motion-harness/Harness.cs') -Raw
# Adapt the diagnostic camera/background only; the production renderer is unchanged.
$source = $source.Replace('Color.FromRgb(58, 60, 72)', 'Environment.GetEnvironmentVariable("KOJI_LIGHT") == "1" ? Color.FromRgb(245, 245, 245) : Color.FromRgb(58, 60, 72)')
$source = $source.Replace('new Int32Rect(440, 260, 420, 300)', 'new Int32Rect(390, 285, 400, 270)')
Set-Content -LiteralPath (Join-Path $scratch 'Harness.cs') -Value $source -Encoding UTF8
& (Join-Path $root 'tools/motion-harness/build-harness.ps1') -root $root -scratch $scratch
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed' }
$harness = Join-Path $scratch 'Harness.exe'
$events = @('16', '0.1:g', '3:k', '4:e', '4.6:h:1')
for ($i = 0; $i -lt 24; $i++) {
    $time = (6 + $i * 0.1).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
    $events += $time + ':k'
}
$events += @('8.5:e', '10:p:-1:-1', '10.6:p:1:1', '10.9:c', '11.3:p:-1:1', '11.6:p:1:-1', '11.8:c',
    '12:a:350:240', '12.2:a:600:240', '12.4:a:350:240', '12.6:a:600:240', '12.8:a:350:240',
    '13:a:600:240', '13.2:a:350:240', '13.4:a:600:240', '13.6:a:350:240', '14:a:off')
$face = @()
for ($i = 0; $i -le 10; $i++) {
    $v = ($i / 10.0).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
    $face += ('b{0:D2}:blink={1},mouthShown=0' -f $i, $v)
}
foreach ($i in @(0, 2, 4, 6, 8, 10, 14)) {
    $v = ($i / 10.0).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
    $face += ('m{0:D2}:blink=0,mouthShown={1}' -f $i, $v)
}
$previousLight = $env:KOJI_LIGHT
try {
    $backgrounds = @('dark', 'light')
    if ($LightOnly) { $backgrounds = @('light') }
    foreach ($background in $backgrounds) {
        $env:KOJI_LIGHT = if ($background -eq 'light') { '1' } else { '0' }
        $folder = Join-Path $out $background
        & $harness $root (Join-Path $folder 'actions') scene ($events -join ';') 6 '0,0,1468,1070' .3 --character=koji-chan
        if ($LASTEXITCODE -ne 0) { throw 'Actions render failed' }
        & $harness $root (Join-Path $folder 'sleep') scene '40;0.1:set:act.sleepAfter=20;36:k' 30 '0,0,1468,1070' .3 --character=koji-chan
        if ($LASTEXITCODE -ne 0) { throw 'Sleep render failed' }
        & $harness $root (Join-Path $folder 'face') face ($face -join ';') 1 --character=koji-chan
        if ($LASTEXITCODE -ne 0) { throw 'Face render failed' }
        & $harness $root (Join-Path $folder 'pose') pose 'rest:0:0:0:0;up:0:0:75:0;left:-30:0:60:8;right:55:0:60:-8' 1 --character=koji-chan
        if ($LASTEXITCODE -ne 0) { throw 'Pose render failed' }
    }
} finally { $env:KOJI_LIGHT = $previousLight }
Write-Output $out
