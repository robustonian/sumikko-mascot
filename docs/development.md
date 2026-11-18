# 開発と検証

[トップへ戻る](../README.md)

| 場所 | 内容 |
| --- | --- |
| `build.ps1` | `bin/SumikkoCompanion.exe` をビルド（実行中のアプリは終了してから） |
| `verify.ps1` | 本体のコンパイル、回帰検査 298 項目、プレビュー静止画の書き出し。アプリの起動中でも実行できる |
| `app/` | アプリ本体と検査のソース |
| `assets/` | 見本のキャラクターの透過 PNG 5 枚と、その絵に合わせた数値 `character.txt`（[assets/README.md](../assets/README.md)） |
| `characters/` | 追加したキャラクター。1 人につき 1 フォルダ（PNG 5 枚と `character.txt`） |
| `assets/parts-reference/` | 新しいキャラクター用の参考シートと画像生成の指示文 |
| `design/` | 見本のキャラクターのもとになった絵 |
| `tools/motion-harness/` | アプリと同じ描画コードで、ポーズやコマ送りを PNG に書き出す確認用ツール |
| `tools/parts-sheet/` | 参考シートを作る `make_sheet.py` と、生成したレイヤーをキー画像と照合する `check_layers.py`（Python + Pillow） |
| `AGENTS.md` | AI が作業するときの手順と注意（組み立て、ビルド、検査、落とし穴） |

`app/` の構成:

| ファイル | 内容 |
| --- | --- |
| `AppHost.cs` | 起動、透過ウィンドウ、メニュー、通知領域、設定の適用 |
| `CharacterView.cs` | キャラの描画と動き（両腕・頭・アホ毛・まばたき・口元・口パク） |
| `CharacterActing.cs` | しぐさと記号の描画（`CharacterView` の一部） |
| `CharacterProfile.cs` | キャラクターごとの数値（`character.txt`）の読み込みと、選べるキャラクターの一覧 |
| `MotionTuning.cs` / `TuningWindow.cs` | 動きの調整値と調整画面 |
| `NativeSignals.cs` | Windows の入力と音量の取得 |
| `SettingsStore.cs` | `bin/settings.txt` の読み書き |
| `MouseMotionRange.cs` / `MouseRangeCalibration.cs` / `MouseRangeWindow.cs` | マウスの移動範囲と調整画面 |
| `AudioDeviceWindow.cs` | オーディオデバイス設定画面 |
| `RegressionTests.cs` / `CalibrationChecks.cs` / `AudioDeviceChecks.cs` / `TuningChecks.cs` / `ActingChecks.cs` / `CharacterChecks.cs` | 回帰検査 |
| `RenderPreview.cs` | プレビュー静止画の書き出し |

コンパイラは .NET Framework 付属のもの（C# 5 まで）を使います。外部のパッケージには依存しません。

Windows API の資料: [WinMM 音声バッファ](https://learn.microsoft.com/en-us/windows/win32/multimedia/audio-data-blocks)、[再生ピークメーター](https://learn.microsoft.com/en-us/windows/win32/coreaudio/peak-meters)、[音声エンドポイント ID](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-id-strings)、[WinMM とエンドポイントの対応付け](https://learn.microsoft.com/en-us/windows/win32/coreaudio/device-roles-for-legacy-windows-multimedia-applications)。

## 検証の実行

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File verify.ps1
```

ビルドのみ行う場合は `build.ps1` を使います。アプリが起動中なら、利用者が終了してから実行してください。`verify.ps1` は別の出力先を使うため起動中でも実行できます。

GitHub では、`main` への push と Pull Request のたびに GitHub Actions（`.github/workflows/ci.yml`、Windows）が `verify.ps1`、`build.ps1`、起動の確認（`--smoke-test --demo`）を行います。書き出したプレビュー静止画は、実行結果の Artifacts から取得できます。

画像の検査・参考シート生成にはPythonとPillowが必要です。アプリの実行には不要です。

```powershell
python -m venv .venv
.venv/Scripts/python.exe -m pip install Pillow
```

`bin/`（実行ファイル・個人設定）、`preview/`（検証画像・ログ）、`.venv/`（Python環境）は手元専用で、Git では管理しません。
