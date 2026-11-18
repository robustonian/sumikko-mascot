# Sumikko Mascot（隅っこキャラ）

<p align="center"><strong>日本語</strong> | <a href="README.en.md">English</a></p>

<p align="center"><strong>ChatGPT に「作りたいキャラクターの画像 1 枚」と「用意されたプロンプト 1 つ」を渡すだけで、自分だけのデスクトップマスコットが作れます。</strong></p>
<p align="center"><strong>One character picture + one prompt = your own animated desktop mascot.</strong></p>

<p align="center">
  <a href="https://github.com/rotejin/sumikko-mascot/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/rotejin/sumikko-mascot/actions/workflows/ci.yml/badge.svg"></a>
  <img alt="License: MIT" src="https://img.shields.io/badge/License-MIT-blue">
  <img alt="Platform: Windows" src="https://img.shields.io/badge/Platform-Windows-0078D6">
  <img alt="Runs offline" src="https://img.shields.io/badge/Runs-offline-brightgreen">
  <a href="https://github.com/sponsors/rotejin"><img alt="Sponsor rotejin" src="https://img.shields.io/badge/Sponsor-rotejin-ea4aaa?logo=githubsponsors&logoColor=white"></a>
  <a href="https://x.com/rotejin"><img alt="X: @rotejin" src="https://img.shields.io/badge/X-%40rotejin-000000?logo=x&logoColor=white"></a>
</p>

<p align="center">
  <img src="docs/media/tomari-typing.gif" alt="キー入力に合わせてキーボードを叩く妹尾トマリ（トレイラーより）" width="720">
</p>

Windows のデスクトップの隅っこで、あなたのキー入力やマウス操作に合わせてキーボードを叩いたり、マウスを動かしたりするキャラクターです。Live2D や PSD は使わず、透過 PNG と描画コードだけで動きます。日本語名は「隅っこキャラ」で、アプリの中でもこの名前で表示されます。

同梱の「ピンクちゃん」と「工事ちゃん」は、どちらも参考画像 1 枚とこのリポジトリの依頼文から作ったキャラクターです。完成図、パーツ画像の生成、位置合わせ、組み込み、動作検査まで AI が行っています。

## 必要なもの

- **Windows**（動作確認は Windows 11 のみです。Windows 10 でも動く見込みですが、確認していません。macOS・Linux では動きません）
- .NET Framework 4.x（Windows に標準で入っています）

自分のキャラクターを作る場合は、さらに次のものが必要です。

- **ChatGPT デスクトップアプリの Codex** または **Codex CLI**（画像生成と、フォルダ内のファイル編集・コマンド実行ができる環境）。利用できるプランは OpenAI の案内を確認してください
- Python 3（AI が画像の照合ツールで使います。アプリの実行には不要です）

## 3 ステップで自分のマスコットを作る

1. **ダウンロードする**
   このページの **Code → Download ZIP** で取得して展開するか、`git clone` します。
2. **AI に画像とプロンプトを渡す**
   ChatGPT デスクトップアプリの Codex（または Codex CLI）で、このフォルダを開きます。作りたいキャラクターの画像を添付し、[キャラクター制作の依頼文](docs/character-creation-prompt.md) の文章を貼り付けて送ります。書き換えるのは【キャラクター名】だけです。
3. **完成図を 1 回だけ確認する**
   AI が「机でキーボードとマウスを操作するちびキャラ」の完成図を 1 枚見せて、確認を求めます。よければ OK と返事をします。あとは、パーツ画像の生成、位置合わせ、アプリへの組み込み、動作検査まで AI が進めます。

できあがったら `Start.cmd` で起動し、キャラを右クリック → **キャラクター** から選びます。アプリの起動中に作った場合は、一度終了してから起動し直してください。

- 制作の確認には、GPT-6 Astra（推論 medium）を使いました。
- 生成結果は毎回異なるため、仕上がりには差が出ます。AI が確認できるのは静止したコマだけなので、動きのテンポや印象は実際に動かして確かめてください。
- 渡す画像は、自分が使ってよい画像にしてください。

自分で画像を用意して組み立てる方法や、キャラクターの切り替えについては [キャラクターの追加と切り替え](docs/characters.md) を見てください。

## まず同梱のキャラで動かしてみる

`Start.cmd` をダブルクリックします。初回は Windows 標準のコンパイラでビルドしてから起動します。アプリの実行に Python や外部パッケージは要りません。

キャラを右クリックすると、キャラクターの切り替え、表示サイズ、音声、動きの設定を変えられます。終了もこのメニューから行います。詳しくは [使い方](docs/usage.md) を見てください。

## トレイラー

57 秒の紹介動画です（音が出ます）。

https://github.com/user-attachments/assets/f4ac66c1-2760-4643-8265-25f2bc6cb0da

## できること

- キーを打つと手がキーボードを叩き、マウス操作に手元が連動します。
- 居眠り、あいさつ、なでなで、打ち終えたときに喜ぶしぐさがあります。
- マイクや PC の再生音の音量に合わせて口パクします。
- キャラクターを何人でも追加して、メニューから切り替えられます。

## 同梱のキャラクター

| 妹尾トマリ（見本） | [ピンクちゃん](characters/pink-chan/README.md) | [工事ちゃん](characters/koji-chan/README.md) |
| --- | --- | --- |
| <img src="design/desktop-companion-concept-v3.png" alt="妹尾トマリ" width="260"> | <img src="characters/pink-chan/preview.png" alt="ピンクちゃん" width="260"> | <img src="characters/koji-chan/preview.png" alt="工事ちゃん" width="260"> |
| `assets/` | `characters/pink-chan/` | `characters/koji-chan/` |

ピンクちゃんと工事ちゃんのフォルダには、制作に使った指示文（`generation-prompts.json`）と、調整・検証の記録（`README.md`）も入っています。

同梱のキャラクターは、どれも ろてじん のオリジナルキャラクターです。使える範囲は [ライセンス](#ライセンス) を見てください。

## ドキュメント

| 目的 | 読むもの |
| --- | --- |
| 参考画像から AI にキャラを作ってもらう | [キャラクター制作の依頼文](docs/character-creation-prompt.md) |
| 操作・音声・動きの調整 | [使い方](docs/usage.md) |
| 自分で用意した画像でキャラを追加する、切り替える | [キャラクターの追加と切り替え](docs/characters.md) |
| 画像生成用の参考シート・パーツの仕様 | [パーツ制作ガイド](assets/parts-reference/README.md) |
| ビルド・回帰検査・ソースの構成 | [開発と検証](docs/development.md) |
| コーディング AI 向けの作業の決まり | [AGENTS.md](AGENTS.md) |
| 変更の履歴 | [CHANGELOG.md](CHANGELOG.md) |
| 開発に参加する | [CONTRIBUTING.md](.github/CONTRIBUTING.md) |

## 困ったときは

### ZIP から起動すると Windows の警告が出る

インターネットから入手したファイルなので、「Windows によって PC が保護されました」などの警告が出ることがあります。ZIP を展開する前に、ZIP を右クリック → **プロパティ** → 下にある **許可する** にチェック → **OK** としてから展開すると、この警告は出なくなります。

### キャラが出てこない

- 通知領域（タスクバーの右端。**^** の中にあることもあります）にキャラの顔のアイコンがあれば、すでに起動しています（二重には起動しません）。アイコンを右クリック → **表示 → 画面内に戻す** で、ポインターのある画面の右下に戻ります。隠している場合は **表示する** を選びます。
- アイコンがない場合は、もう一度 `Start.cmd` を開いて、黒い画面にエラーが出ていないかを見てください。ビルドに失敗したときは、その画面が開いたまま止まります。

### ビルドに失敗する

- Windows に入っている .NET Framework 4 の C# コンパイラ（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`）を使います。このファイルがない場合は、Microsoft のサイトから .NET Framework 4.8 を入れてください。
- 解決しないときは、黒い画面に出たメッセージを添えて [不具合の報告](https://github.com/rotejin/sumikko-mascot/issues/new/choose) をしてください。

### 作ったキャラクターがメニューに出ない

- `characters/<フォルダ名>/` に、必要な PNG と `character.txt` がそろっているかを確かめてください（[キャラクターの追加と切り替え](docs/characters.md)）。
- アプリの起動中に作ったときは、一度終了してから起動し直してください。

### 口パクしない

起動したときの音声は、いつもオフです。キャラを右クリック → **音声** で入力元を選びます。反応が弱いときは、**音声 → オーディオデバイス設定…** で感度を上げます（[使い方](docs/usage.md#口パク音声)）。

## 新しい版にする

**git で入手した場合**: キャラを右クリック → **終了** → `git pull` → `Start.cmd` を開く。ソースが変わっていれば、`Start.cmd` が自動でビルドし直します。`bin\` にある設定はそのまま使われます。

**ZIP で入手した場合**: 新しい ZIP を別のフォルダに展開し、古いフォルダから次のものを同じ場所へコピーします。

- `bin\settings.txt` と `bin\tuning*.txt`（位置・サイズ・音声・動きの調整などの設定）
- 自分で作ったキャラクターのフォルダ（`characters\<フォルダ名>`）

## アンインストール

キャラを右クリック → **終了** してから、フォルダごと削除します。設定はフォルダの中の `bin\` にだけ保存していて、レジストリや Windows の自動起動には何も登録していません。

## 質問・不具合の報告

[Issue](https://github.com/rotejin/sumikko-mascot/issues/new/choose) から、テンプレートを選んで書いてください。書いてほしいことは [SUPPORT.md](.github/SUPPORT.md) にあります。脆弱性は公開の Issue に書かず、[SECURITY.md](.github/SECURITY.md) の方法で報告してください。

そのほかの作者への連絡は、X（[@rotejin](https://x.com/rotejin)）へどうぞ。

## プライバシー

入力はキャラの反応のためだけに使い、入力した文字や音声は記録しません。音声は音量だけを取得します。ネットワーク通信は行いません。位置・表示・音声デバイスなどの設定は、手元の `bin/` フォルダに保存します。

## 名前について

Sumikko（すみっこ）は日本語で「隅っこ」という意味です。画面の隅っこにいる相棒、という意味で名付けました。日本語名は「隅っこキャラ」です。サンエックスの「すみっコぐらし」とは関係ありません。また、OpenAI の公式ツールではありません。

## ライセンス

ソースコード・スクリプト・文書は [MIT License](LICENSE) です。

**キャラクター画像は MIT の対象外**です。同梱のキャラクター（妹尾トマリ・ピンクちゃん・工事ちゃん）は ろてじん のオリジナルキャラクターで、[画像の利用条件（ASSETS.md）](ASSETS.md) の範囲で使えます。

- **ピンクちゃん・工事ちゃん**: 自分の PC のマスコットとして使うこと、非営利の配信・動画・SNS 投稿に映すことができます。
- **妹尾トマリ**: アプリのお試しと、その様子の SNS 投稿（非営利）までです。配信や動画では使えません。
- どのキャラクターも、自分のキャラクターとして使うこと、画像を取り出してほかに使うこと、営利で使うことはできません。

自分の画像から作ったキャラクターは、あなたのものです（元の画像の権利と、画像生成サービスの規約に従ってください）。

## English

The full English guide is in [README.en.md](README.en.md).
