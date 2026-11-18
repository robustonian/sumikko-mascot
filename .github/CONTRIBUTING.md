# 開発への参加 / Contributing

> English follows the Japanese.

不具合の報告や改善の提案を歓迎します。個人で作っているプロジェクトのため、返事や取り込みは できる範囲で行います。

## Issue

- 不具合・キャラクター作りの相談・要望は、[Issue のテンプレート](https://github.com/rotejin/sumikko-mascot/issues/new/choose) から書いてください（書いてほしいことは [SUPPORT.md](SUPPORT.md)）。
- 脆弱性は公開の Issue に書かず、[SECURITY.md](SECURITY.md) の方法で報告してください。

## Pull Request

大きな変更は、先に Issue で相談してください。小さな修正（誤字、文書の改善、明らかな不具合の修正）はそのまま送ってかまいません。

送る前に確かめてほしいこと:

- `powershell -NoProfile -ExecutionPolicy Bypass -File verify.ps1` が全項目合格する（検査を消して通すことはしない）。
- コンパイラは .NET Framework 付属の C# 5 まで（`?.`、文字列補間、`out var`、式形式メンバー、`nameof` は使えない）。外部のパッケージは使わない。
- ソースファイルを足したら、`build.ps1`、`verify.ps1`、`tools/motion-harness/build-harness.ps1` の一覧に加える。
- 動きや見た目を変えたら、`tools/motion-harness/` でコマを書き出して確かめる。
- 文書を変えたら、日本語版と英語版（`docs/en/`、`README.en.md` など）の両方をそろえる。英語が難しければ、日本語だけで送ってもかまいません。

細かい決まりと落とし穴は [AGENTS.md](../AGENTS.md) にあります。

## 画像について

- **同梱のキャラクター（妹尾トマリ・ピンクちゃん・工事ちゃん）の画像を変更・追加する Pull Request は受け付けていません。** 画像の利用条件は [ASSETS.md](../ASSETS.md) にあります。
- **新しいキャラクターの追加も受け付けていません。** 自分のキャラクターは、自分の手元で楽しんでください。
- 不具合の説明のためにスクリーンショットを添えるのはかまいません。他人のキャラクターや、使ってよいか分からない画像は含めないでください。

## ライセンス

送ってもらったコード・スクリプト・文書は、このリポジトリと同じ [MIT License](../LICENSE) で公開されます。

---

## English

Bug reports and suggestions are welcome. This is a personal project, so replies and merges are best-effort.

### Issues

- Use the [issue templates](https://github.com/rotejin/sumikko-mascot/issues/new/choose) for bugs, character-creation questions and feature requests ([SUPPORT.md](SUPPORT.md) lists what to include).
- Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md), not in public issues.

### Pull requests

Please open an issue first for larger changes. Small fixes (typos, documentation, obvious bugs) can be sent directly.

Before sending:

- `powershell -NoProfile -ExecutionPolicy Bypass -File verify.ps1` passes every check (never delete a check to make it pass).
- The compiler is the C# 5 compiler that comes with .NET Framework (no `?.`, string interpolation, `out var`, expression-bodied members or `nameof`). No external packages.
- New source files are added to the lists in `build.ps1`, `verify.ps1` and `tools/motion-harness/build-harness.ps1`.
- Changes to motion or appearance are checked by rendering frames with `tools/motion-harness/`.
- Documentation changes keep the Japanese and English versions (`docs/en/`, `README.en.md` and so on) in step. If you cannot write Japanese, English only is fine.

Detailed rules and pitfalls are in [AGENTS.md](../AGENTS.md) (Japanese).

### Images

- **Pull requests that change or add images of the bundled characters (Tomari, Pink-chan, Koji-chan) are not accepted.** See [ASSETS.md](../ASSETS.md) for the image terms.
- **New characters are not accepted either.** Enjoy your own characters locally.
- Screenshots that explain a bug are fine. Do not include other people's characters or images you may not share.

### License

Code, scripts and documents you contribute are published under the same [MIT License](../LICENSE) as this repository.
