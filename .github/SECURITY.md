# セキュリティポリシー / Security Policy

## 対象のバージョン

最新のリリースと `main` ブランチを対象に修正します。

## 脆弱性の報告

**脆弱性の詳しい内容を、公開の Issue に書かないでください。**

このリポジトリの **Security** タブ → **Report a vulnerability** から、非公開で報告してください（GitHub の Private vulnerability reporting）。

この方法が使えない場合は、作者の X（[@rotejin](https://x.com/rotejin)）に DM で連絡してください。

書いてほしいこと:

- 影響のあるファイルや機能
- 再現の手順
- 期待した動作と、実際の動作
- Windows のバージョン
- 安全に共有できるなら、再現に使うファイル

## このアプリの仕組み（安全面）

- **ネットワーク通信をしません。**
- 実行ファイルは同梱していません。`Start.cmd` は `build.ps1` を実行し、初回と、ソースが更新されたときだけ、Windows に入っている .NET Framework の C# コンパイラでソースから `bin/SumikkoCompanion.exe` を作ります。`build.ps1` は実行ポリシーを一時的に無視する設定（`-ExecutionPolicy Bypass`）で起動します。
- キー入力は、キーの状態を一定の間隔で読む方式（`GetAsyncKeyState`）で調べます。ほかのアプリの入力を横取りする「フック」は使いません。使うのは「キーが押された」「Enter かどうか」「押しっぱなしかどうか」だけで、どのキーを押したか、何を入力したかは記録しません。
- マウスはポインターの位置とボタンの状態を読みます。
- `Ctrl+Alt+C`（クリック透過の切り替え）を、Windows のホットキーとして登録します。
- 口パクに音声を選んだときだけ、マイクの音声（`waveIn`）や再生中の音量（`IAudioMeterInformation`）から音量を計算します。録音・保存はしません。
- 保存するのは、フォルダの中の `bin/` にある設定ファイル（`settings.txt`、`tuning*.txt`）だけです。レジストリや Windows の自動起動には登録しません。
- キャラクターのフォルダ（`characters/<名前>/`）から読み込むのは、PNG 画像と数値の設定ファイル `character.txt` だけです。キャラクターのフォルダに含まれるプログラムを実行することはありません。
- `tools/` の Python スクリプトは、キャラクター作りのときに画像を照合するためのものです。アプリの実行には使いません。

## English

Please do not post vulnerability details in public issues. Use **Security → Report a vulnerability** (GitHub private vulnerability reporting) in this repository. If that is not possible, send a direct message to [@rotejin](https://x.com/rotejin) on X.

The app makes no network connections. It ships no executables: `Start.cmd` compiles the source with the C# compiler of the .NET Framework that comes with Windows. It polls the key state with `GetAsyncKeyState` (no global hooks) and uses only whether a key was pressed, whether it was Enter and whether it is held; typed text is never recorded. Audio is used only for the mouth movement, as a volume level, and is never recorded. Settings are saved only under `bin/` in the project folder; nothing is written to the registry or to Windows startup. Character folders contain only PNG images and a numeric `character.txt`; no code from them is executed.
