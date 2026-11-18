# 変更履歴 / Changelog

このプロジェクトの主な変更を記録します。
All notable changes to this project are recorded here.

## [0.1.1] - 2026-11-18

文書だけの更新です。アプリの動作は v0.1.0 と同じです。 / Documentation-only update; the app itself is unchanged.

- おすすめのモデルを GPT-6.2 Sol（推論 high）にしました。
- 目の閉じ方・手の動き・パーツの切れ目などを、追加の指示でピンポイントに直す方法と、指示の例を足しました。
- 英語の案内は、動作を確かめた日本語の依頼文をそのまま使う形にしました（英語で返事がほしいときに足す一文つき）。
- 作者の連絡先（X: @rotejin）を、README・サポート・セキュリティポリシー・Issue の案内に足しました。
- README に、日本語名が「隅っこキャラ」であることを書き足しました。

### English

- Recommended model: GPT-6.2 Sol (reasoning effort: high).
- Added how to polish a character with pinpoint follow-up requests (how the eyes close, hand motion, seams between parts), with example requests.
- The English guide now uses the tested Japanese prompt as it is, with one line to add for replies in English.
- Added the author's contact (X: @rotejin) to the READMEs, the support and security policies and the issue chooser.
- The READMEs now explain that the Japanese name is 隅っこキャラ.

## [0.1.0] - 2026-11-18

最初の公開版です。 / First public release.

### アプリ

- Windows 用のデスクトップマスコット（WPF / .NET Framework）。キー入力に合わせてキーボードを叩き、マウス操作に合わせて手元のマウスを動かします。
- まばたき、口パク（マイク・仮想入力・PC 再生の音量）、アホ毛の揺れ。
- しぐさ: あいさつ、居眠りと起床、やりきった、なでなで、考え中、必死の汗。
- 右クリックメニューと通知領域から、キャラクターの切り替え、表示サイズ、クリック透過、オーディオデバイス、マウスの移動範囲、約 50 項目の動きの調整。
- ネットワーク通信なし。設定はフォルダ内の `bin/` にだけ保存します。
- `Start.cmd` は初回と、更新でソースが変わったときに自動でビルドします。ビルドに失敗したときは、画面を閉じずにエラーを表示します。

### キャラクター作り

- 参考画像 1 枚とプロンプト 1 つで、ChatGPT デスクトップアプリの Codex または Codex CLI にキャラクターを作ってもらう依頼文（日本語・英語）。
- パーツの参考シートと生成プロンプト、レイヤーの照合ツール、コマの書き出しツール。
- 同梱のキャラクター: 妹尾トマリ（見本）、ピンクちゃん、工事ちゃん。

### 文書と検証

- README・使い方・キャラクターの追加・開発・パーツ作りの英語版。
- 画像の利用条件（ASSETS.md）、セキュリティポリシー、サポート、開発への参加の案内、Issue と Pull Request のテンプレート。
- 回帰検査 298 項目（`verify.ps1`）と、GitHub Actions での自動検査。

### English

- A desktop mascot for Windows (WPF / .NET Framework) that types along with your keyboard and moves its mouse with yours, with blinks, volume-based lip sync, ahoge sway and gestures (hello, dozing and waking, cheering, head pats, thinking, sweating).
- Menu and notification-area settings: character switching, size, click-through, audio device, mouse range and about 50 motion tuning values. No network access; settings stay in `bin/`.
- `Start.cmd` builds on the first run and again after an update changes the source, and keeps its window open with the error if the build fails.
- A prompt (Japanese and English) that has Codex in the ChatGPT desktop app or Codex CLI make a character from one reference picture, plus reference sheets, generation prompts, a layer checker and a frame renderer.
- Bundled characters: Tomari (sample), Pink-chan and Koji-chan.
- English versions of the README and the guides; asset license, security policy, support and contributing guides, issue and pull request templates.
- 298 regression checks (`verify.ps1`) and CI on GitHub Actions.

[0.1.1]: https://github.com/rotejin/sumikko-mascot/releases/tag/v0.1.1
[0.1.0]: https://github.com/rotejin/sumikko-mascot/releases/tag/v0.1.0
