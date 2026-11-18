# 変更履歴 / Changelog

このプロジェクトの主な変更を記録します。
All notable changes to this project are recorded here.

## [0.1.0] - 2026-11-18

最初の公開版です。 / First public release.

### アプリ

- Windows 用のデスクトップマスコット（WPF / .NET Framework）。キー入力に合わせてキーボードを叩き、マウス操作に合わせて手元のマウスを動かします。
- まばたき、口パク（マイク・仮想入力・PC 再生の音量）、アホ毛の揺れ。
- しぐさ: あいさつ、居眠りと起床、やりきった、なでなで、考え中、必死の汗。
- 右クリックメニューと通知領域から、キャラクターの切り替え、表示サイズ、クリック透過、オーディオデバイス、マウスの移動範囲、約 50 項目の動きの調整。
- ネットワーク通信なし。設定はフォルダ内の `bin/` にだけ保存します。

### キャラクター作り

- 参考画像 1 枚とプロンプト 1 つで、ChatGPT デスクトップアプリの Codex または Codex CLI にキャラクターを作ってもらう依頼文（`docs/character-creation-prompt.md`）。
- パーツの参考シートと生成プロンプト、レイヤーの照合ツール、コマの書き出しツール。
- 同梱のキャラクター: 妹尾トマリ（見本）、ピンクちゃん、工事ちゃん。

### 検証

- 回帰検査 298 項目（`verify.ps1`）と、GitHub Actions での自動検査。

[0.1.0]: https://github.com/rotejin/sumikko-mascot/releases/tag/v0.1.0
