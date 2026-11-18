# Development

[Back to the top](../../README.en.md) · [日本語](../development.md)

| Path | Contents |
| --- | --- |
| `build.ps1` | Builds `bin/SumikkoCompanion.exe` (quit the running app first) |
| `verify.ps1` | Compiles the app, runs 298 regression checks and renders preview frames. Works while the app is running |
| `app/` | Source of the app and of the checks |
| `assets/` | The sample character's five transparent PNG files and the values fitted to that picture, `character.txt` ([assets/README.md](../../assets/README.md), Japanese) |
| `characters/` | Added characters, one folder each (five PNG files and `character.txt`) |
| `assets/parts-reference/` | Reference sheets and image-generation prompts for new characters ([English guide](../../assets/parts-reference/README.en.md)) |
| `design/` | The picture the sample character was based on |
| `tools/motion-harness/` | Renders poses and frame sequences to PNG with the app's own drawing code |
| `tools/parts-sheet/` | `make_sheet.py` builds the reference sheet; `check_layers.py` compares generated layers with the key illustration (Python + Pillow) |
| `AGENTS.md` | Procedures and pitfalls for AI agents (assembling, building, checking), in Japanese |

The `app/` folder:

| File | Contents |
| --- | --- |
| `AppHost.cs` | Start-up, the transparent window, menu, notification area, applying settings |
| `CharacterView.cs` | Drawing and motion of the character (both arms, head, ahoge, blinks, mouth, lip sync) |
| `CharacterActing.cs` | Gestures and symbols (part of `CharacterView`) |
| `CharacterProfile.cs` | Reading each character's values (`character.txt`) and listing the available characters |
| `MotionTuning.cs` / `TuningWindow.cs` | Motion values and the tuning window |
| `NativeSignals.cs` | Reading Windows input and audio levels |
| `SettingsStore.cs` | Reading and writing `bin/settings.txt` |
| `MouseMotionRange.cs` / `MouseRangeCalibration.cs` / `MouseRangeWindow.cs` | The mouse range and its window |
| `AudioDeviceWindow.cs` | The audio device window |
| `RegressionTests.cs` / `CalibrationChecks.cs` / `AudioDeviceChecks.cs` / `TuningChecks.cs` / `ActingChecks.cs` / `CharacterChecks.cs` | Regression checks |
| `RenderPreview.cs` | Renders the preview frames |

The app is compiled with the compiler that comes with .NET Framework (C# 5 at most: no `?.`, string interpolation, `out var`, expression-bodied members or `nameof`). It depends on no external packages.

Windows API references: [WinMM audio data blocks](https://learn.microsoft.com/en-us/windows/win32/multimedia/audio-data-blocks), [peak meters](https://learn.microsoft.com/en-us/windows/win32/coreaudio/peak-meters), [endpoint ID strings](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-id-strings), [device roles for legacy Windows multimedia applications](https://learn.microsoft.com/en-us/windows/win32/coreaudio/device-roles-for-legacy-windows-multimedia-applications).

## Running the checks

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File verify.ps1
```

To only build, use `build.ps1`. `Start.cmd` calls `build.ps1 -IfOutdated`, which builds only when the program is missing or a source file in `app/` or `build.ps1` is newer (while the app is running it does not rebuild and tells you to quit and start it again). If the app is running, quit it before running `build.ps1` directly. `verify.ps1` builds into a separate folder, so it works while the app is running.

On GitHub, every push to `main` and every pull request runs GitHub Actions (`.github/workflows/ci.yml`, Windows): `verify.ps1`, `build.ps1` and a start-up check (`--smoke-test --demo`). The rendered preview frames can be downloaded from the run's artifacts.

The image checks and the reference sheet need Python and Pillow. The app itself does not.

```powershell
python -m venv .venv
.venv/Scripts/python.exe -m pip install Pillow
```

`bin/` (the program and personal settings), `preview/` (rendered frames and logs) and `.venv/` (the Python environment) are local only and not tracked by Git.
