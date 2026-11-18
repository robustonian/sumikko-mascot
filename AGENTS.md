# AI 向けの作業手引き

このリポジトリで作業するコーディング AI のための手引きです。人向けの使い方は `README.md` にあります。

このリポジトリの目的は 2 つです。

1. デスクトップ常駐キャラのアプリ（Windows / .NET Framework / WPF、および Linux / Python 3 / PyQt5、透過 PNG 5 枚＋描画コード）。キャラクターは複数置けて、メニューから切り替えられる。
2. **利用者が画像生成で作ったパーツ画像を AI が受け取り、そのキャラクターで動くアプリに組み立てること**。

利用者は絵の専門家でもプログラマーでもないかもしれません。報告は利用者が使っている言語で（日本語なら平易な日本語で）、何を確かめたか・何を確かめていないかをはっきり書いてください。

## 目次

- [環境と決まりごと](#環境と決まりごと)
- [キャラクターの置き場所と切り替え](#キャラクターの置き場所と切り替え)
- [新しいキャラクターを組み立てる](#新しいキャラクターを組み立てる)
- [キャラクターごとに変える場所](#キャラクターごとに変える場所)
- [見た目の確かめ方](#見た目の確かめ方)
- [落とし穴](#落とし穴)

## 環境と決まりごと

- **Linux版**: `./Start.sh` で起動、`./verify.sh` でLinux用の全回帰検査・同梱3キャラの起動検査・静止画の出力。詳細は `docs/linux.md`。Linux用のPythonソースは `linux/` に置き、WindowsのC#ソース一覧への追加は不要。PyQt5はLinux版だけの依存。以下のPowerShell・C# 5・外部パッケージ不要の決まりはWindows版に適用する。
- Linuxの個人設定 `bin/linux/settings.json` も消さない・勝手に書き換えない。検査では `--settings-dir` に一時フォルダを渡す。入力機器の権限やグループを自動変更しない。
- **ビルド**: `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`。アプリの起動中は失敗するので、先に `Get-Process -Name SumikkoCompanion -ErrorAction SilentlyContinue` で確かめ、起動中なら利用者に閉じてもらう（勝手に終了させない）。
- **検査**: `powershell -NoProfile -ExecutionPolicy Bypass -File verify.ps1`。本体のコンパイル、回帰検査、プレビュー静止画（`preview/frame-*.png`）の書き出しを行う。アプリの起動中でも実行できる。変更のたびに全項目の合格を確かめる。
- **起動の確認**: `bin/SumikkoCompanion.exe --smoke-test --demo` は 3 秒で終了する。すでに起動中だと二重起動防止で即終了するので、経過時間が 3 秒以上かを見る。
- **コンパイラは C# 5 まで**（.NET Framework 付属の csc）。`?.`、文字列補間、`out var`、式形式メンバー、`nameof` は使えない。外部パッケージは使わない。
- **Python** は Pillow が要る（`tools/` のスクリプト用）。仮想環境を作って実行する。
- ソースファイルを足したら、`build.ps1`、`verify.ps1`（2 か所）、`tools/motion-harness/build-harness.ps1` の一覧に加える（検査だけのファイルは `verify.ps1` の検査の一覧だけ）。
- 検査項目の数を変えたら、`docs/development.md` の「回帰検査 ○ 項目」も合わせる。
- 検査でメイン画面（`CompanionWindow`）を作るときは、必ず設定ファイルのパスを渡す 3 引数のコンストラクタに一時フォルダのファイルを渡す。2 引数版は実際の `bin/settings.txt` と `bin/tuning.txt` を読み書きする。
- コミットは利用者に頼まれてから行う。
- 利用者の PC の設定（`bin/settings.txt`、`bin/tuning.txt`、`bin/tuning-*.txt`）は消さない・書き換えない。

## キャラクターの置き場所と切り替え

- 見本のキャラクターは `assets/`。追加のキャラクターは `characters/<フォルダ名>/`。どちらも中身は透過 PNG 5 枚と `character.txt`。`face-expression.png` の代わりに `eyes-closed.png`（閉じ目）と `mouth-open.png`（開いた口）の 2 枚でもよい（あるほうを使う。`check_layers.py` も両方の形を受け付ける）。
- 実例として `characters/pink-chan/` と `characters/koji-chan/` がある（首が体側、目と口が別ファイル）。検査は `characters/` の全キャラクターについて、`character.txt` に問題がなく起動できることも確かめる。
- 書き出しツールの `pose`・`face` の切り出し範囲は見本向けの固定値。ほかの範囲を見たいときは `scene` に切り出し範囲を渡す。
- `character.txt` は、その絵に合わせた数値（目・口・腕・記号の位置など）を「名前 = 数値…」で書いたファイル。**キャラクターごとの値はここに書き、描画コードは書き換えない**。書かなかった項目は見本の値（`app/CharacterProfile.cs` の初期値）になる。項目の説明は `assets/character.txt` のコメントにある。
- `assets/character.txt` は `app/CharacterProfile.cs` の初期値と同じ内容でなければならない（検査で確かめている）。見本の値を変えるときは両方を変える。
- 項目を増やすときは、`CharacterProfile` の初期値、`assets/character.txt`、`CharacterView` の読み込み（コンストラクタ）の 3 か所に足す。
- メニューの「キャラクター」には、`assets/` と、5 枚がそろった `characters/` の各フォルダが出る。フォルダ名に `=` は使えない。表示名は `character.txt` の `name`。
- 選んだキャラクターは `bin/settings.txt` の `character=`、キャラクターごとのマウス範囲は `mouseRange@<フォルダ名>=`、動きの調整は `bin/tuning-<フォルダ名>.txt` に保存される。
- 読めない行は `CharacterProfile.Problems` に入り、その項目は見本の値のままになる。書いたあとは必ず問題が 0 件かを確かめる（書き出しツールが標準エラーに出す）。

## 新しいキャラクターを組み立てる

利用者から渡されるもの:

- **キー画像** 1 枚（見本と同じ構図で描かれた、そのキャラクターの完成図）
- **透過 PNG 5 枚**: `base.png`、`keyboard-arm.png`、`mouse-arm.png`、`head.png`、`face-expression.png`

画像の作り方（利用者の作業）は `assets/parts-reference/README.md` にあります。足りない画像や作り直しが要る画像があれば、そこに載っている指示文を示して利用者に頼みます。通常はAIが画像を描き直すことはしません。ただし、`docs/character-creation-prompt.md` などで利用者が画像生成・修正も依頼した場合は、その依頼に沿って制作します。素材の不足を不自然な描画でごまかしてはいけません。

同梱のキャラクター（`assets/`・`design/`・`characters/pink-chan/`・`characters/koji-chan/`）の画像を参考画像にして新しいキャラクターを作ることは、`ASSETS.md` で禁止されています。そうした依頼は行わず、利用者に理由を伝えます。参考シート（`assets/parts-reference/`）を構図の見本として使うのは手順どおりで問題ありません。

### 手順

1. **受け取った画像を見る**。5 枚とキー画像を実際に開いて、何が描かれているかを確かめる。寸法、透過、キャラクターの特徴（アホ毛や耳などの突き出た部分があるか、前髪が瞳にかかっているか、首が頭と体のどちらに付いているか）を記録する。
2. **機械的に照合する**。

   ```
   python tools/parts-sheet/check_layers.py <キー画像> <5 枚を置いたフォルダ> <出力.png>
   ```

   5 枚がそろっていない、背景が透過していない場合は一覧を出して終了コード 1 で止まる。そろっていれば「キー画像｜重ねた結果｜差」を並べた画像を書き出し、違う画素の割合を表示する。見本のキャラクターでは約 3.4%。輪郭が二重に光っているレイヤーは位置か大きさがずれているので、利用者に作り直しを頼む（やり直し用の指示文は `assets/parts-reference/README.md` の手順 6）。**ここを通らないまま先へ進まない**。
3. **キャラクターのフォルダを作る**。`characters/<フォルダ名>/` に 5 枚を置き、`assets/character.txt` をコピーする。`name` を書き換える。見本の `assets/` には手を付けない。
4. **位置を測って合わせる**。[キャラクターごとに変える場所](#キャラクターごとに変える場所) の一覧を上から順に進め、測った値をそのフォルダの `character.txt` に書く。座標は推測せず、画像の画素を測る（透過の境目、輪郭線の位置）。1 か所直すごとに、その場所のコマを書き出して見る（書き出しツールに `--character=<フォルダ名>` を付ける）。
5. **検査を通す**。`verify.ps1` が全項目合格することを確かめる。検査は見本のキャラクターで行うので、`character.txt` だけを足した場合は結果が変わらないはず。新しいキャラクターのために描画コードを変えた場合は、見本の見た目が変わっていないことをコマで確かめる。検査を消して通すことはしない。
6. **動きを見る**。[見た目の確かめ方](#見た目の確かめ方) の場面をひととおり書き出し、継ぎ目・はみ出し・穴・記号の位置を確かめる。
7. **ビルドして起動を確かめ、利用者に実機で見てもらう**。メニューの「キャラクター」から新しいキャラクターを選んでもらう。AI が見られるのは静止コマと数値だけなので、テンポや印象の最終判断は利用者に委ねる。
8. **報告する**。合わせた場所、測った値、確かめた場面、確かめていないこと、利用者に見てほしい点を書く。

### 判断の目安

- 見本と同じ位置に描けている絵なら、座標の調整は小さく済む。大きくずれている絵は、コードで合わせるより画像の作り直しを頼むほうが結果が良い。
- 絵の都合で成立しないしぐさは、無理に出さずにオフにする（そのキャラクターの `character.txt` に `tune.<名前> = 値` を書く。`app/MotionTuning.cs` の初期値は全キャラクター共通なので変えない）。たとえば突き出た髪がないキャラクターでは、`ahogeBase =` と `ahogeTip =` を空にし、`tune.hair.ahoge = 0` を書く。
- `character.txt` の項目では表せない違い（腕の切り分け方そのものが違う、など）が出たら、描画コードに項目を足して対応する。見本の値を初期値にして、見本の見た目が変わらないようにする。
- 必須の 5 枚以外の差分画像（半目、視線、分離した胴体など）は、今のアプリでは使わない。渡されても、使う動きを新しく作る依頼がない限り組み込まない。

## キャラクターごとに変える場所

座標はすべて原寸（1469×1071）。画像の寸法が数 px 違っても、描画時にこの大きさへ揃う。表示は幅 320〜560 に縮むので、**原寸 3px が画面の約 1px**。
値はキャラクターごとの `character.txt` に書く（見本の値は `assets/character.txt`）。下の表の「名前」はコードの中での呼び名で、`character.txt` での名前は表のあとの対応表にある。

### 頭と顔

| 名前 | 内容 | 測り方 |
| --- | --- | --- |
| `leftEye` / `rightEye` | 目の差し替え範囲（多角形）。`head.png` と `face-expression.png` を入れ替える領域 | 開いた目と閉じた目の両方が入り、前髪をなるべく含まない範囲。縁は 5px ぼかして合成される（`EyeFeather`） |
| `DrawBlinkEye` の中の数値 | まばたき途中のまぶたの線（開いた目の上端から閉じた位置までの曲線） | 開いた目の上まぶたの線と、閉じた目の線の位置から決める。途中のコマを書き出して確かめる |
| `mouthRegion`、口の拡大の中心 `(709, 462)` | 口パクで `face-expression.png` から切り出す範囲と、縦に伸ばす中心 | 開いた口が入るだ円。中心は上唇のあたり |
| `MouthCenter`、`pursedMouthRegion`、肌の色 | 口をすぼめる演出。元の口を肌色で覆い、縮めた口を重ねる | 口の中心と、口のまわりの無地の肌の色（スポイトで測る） |
| `YawnTearSpot` | あくびの涙の位置 | 手前の目の外側の目じりの少し下 |
| `HeadPlacement` の回転の中心 `(601, 552)` | 頭を回す中心 | 首の付け根（あごの下） |
| `neckRegion` / `headWithoutNeck` | 見本の `head.png` は首つきなので、首だけ切り抜いて体側に固定している | **新しい素材は首が `base.png` 側にあり、頭は首なし**。`neck =` を空にすると、頭をそのまま描く。そのうえで頭を上にも動かせるなら `headRise` に上がれる量を書く（コマで首との間を確かめる） |
| `AhogeRootA/B`、`AhogeTipLine`、`AhogeBasePiece`、`AhogeTipPiece` | 突き出た髪を揺らすための切り出し | 背景に対して突き出ていて、ほかの髪に接していない部分だけが動かせる。根元を横切る線と、すき間を通る輪郭を画素で測る。該当する髪がなければ `ahogeBase =`、`ahogeTip =` を空にして、`tune.hair.ahoge = 0` を書く |

### キーボード側の腕

| 名前 | 内容 |
| --- | --- |
| `KeyHinge`、`KeyCuffAxis`、`KeyReach`、`KeyForearm` | 袖が曲がる線（袖口と平行）と、そこから袖口までの長さ |
| `KeyPalm`、`KeyKnuckle` | 手を回す中心（袖口の中に入る縁の円の中心）と、手を拡大する基準点 |
| `KeyHandEdge` | 手と袖口の境目の線（輪郭の外側を上から下へ）。**ここの精度が、手を回した時の見た目を決める** |
| 腕を切り分ける多角形（`keyRootClip` などを作る数値） | 根元／袖／袖口の縁／手 の 4 つ |
| `collarForeground` | 袖の付け根を体の後ろへ隠すために、胴体を袖の上へ描き直す範囲 |
| `AlongMin` など、`KeySpot` | 手が叩く範囲（キーボードの絵の上）。行の向きと奥行きの向きを、キーボードの絵の傾きから決める |

### マウス側の腕

| 名前 | 内容 |
| --- | --- |
| `MouseShoulderY`、`MouseWristY` | 肩の線（これより上は動かさない）と手首の線（これより下は手とマウスが形を保って動く） |
| `MouseShoulderLine` と肩先の切り落とし | 見本の素材の肩先に輪郭のない部分があったための補修。新しい素材で肩の輪郭がつながっていれば、`mouseShoulderLine =`、`mouseShoulderCut =` を空にする |
| `ClickOrigin`、`ClickRays` | クリックの線が出る位置と向き（マウスの手前の、机の空いている側） |

### しぐさ・その他

| 場所 | 名前 | 内容 |
| --- | --- | --- |
| `app/CharacterActing.cs` | `HeadCenter`、`HeadRadiusX/Y` | なでなでの判定に使う頭のだ円 |
| `app/CharacterActing.cs` | `SymbolAnchor` | 記号（zzz・！・ハートなど）を出す基準点。頭の横の、絵の中で空いている場所。記号は絵の上端（y=0）より上へ出ると切れる |
| `app/AppHost.cs` | `TrayIconCrop` | 通知領域のアイコンに使う `head.png` の範囲（頭全体が入る正方形） |
| `app/MouseMotionRange.cs` | 既定の範囲 | 手元のマウスが動ける範囲（机からはみ出さない、体にめり込まない） |
| `app/CharacterView.cs` | `MakeOutlinePen` の色 | 絵の輪郭線の色。クリックの線・記号・まぶたの線に使う |
| `character.txt` | `tune.<名前> = 値` | そのキャラクターで成立しないしぐさをオフにする |

### `character.txt` での名前

| コードでの呼び名 | `character.txt` |
| --- | --- |
| `leftEye` / `rightEye` | `leftEye`、`rightEye` |
| `DrawBlinkEye` の中の数値 | `nearLid`（手前の目）、`farLid`（奥の目）、`lidWidth` |
| まぶたを曲線で描く方式（任意） | `nearLidCurve`・`nearLidLower`・`farLidCurve`・`farLidLower`。閉じたまつ毛の弧が開いた時と逆向きの絵で使う。開いた時・閉じた時のまつ毛の中心線を列ごとに測って 3 点の曲線に合わせる |
| 閉じ目・開いた口の画像の指定（任意） | `closedEyesImage`、`openMouthImage`（そのフォルダの中のファイル名） |
| `mouthRegion`、口の拡大の中心 | `mouthRegion`、`mouthPivot` |
| `MouthCenter`、`pursedMouthRegion`、肌の色 | `mouthCenter`、`pursedMouth`、`mouthCover`、`skinColor` |
| `YawnTearSpot` | `yawnTear` |
| `HeadPlacement` の回転の中心 | `headPivot`、`headRise` |
| `neckRegion` / `headWithoutNeck` | `neck` |
| `AhogeRootA/B`、`AhogeTipLine`、`AhogeBasePiece`、`AhogeTipPiece` | `ahogeBase`（最初の 2 点が根元の線）、`ahogeTip`（最初と最後の点が折れ曲がる縦線の上） |
| `KeyHinge`、`KeyCuffAxis`、`KeyReach`、`KeyForearm` | `keyHinge`、`keyCuffAxis`、`keyForearm`（`KeyReach` は `keyCuffAxis` に直角な向きとして自動で決まる） |
| `KeyPalm`、`KeyKnuckle`、`KeyHandEdge` | `keyPalm`、`keyKnuckle`、`keyHandEdge` |
| 腕を切り分ける多角形 | `keyArmBox`（腕全体が入る範囲）。4 つの部分は、これと上の線から自動で作る |
| `collarForeground` | `collar` |
| `AlongMin` など、`KeySpot` | `keyRows`、`keyDepth`、`keyReachX` |
| あいさつ・やりきったで手を上げる位置 | `waveHand`、`triumphLift` |
| `MouseShoulderY`、`MouseWristY` | `mouseShoulderY`、`mouseWristY` |
| `MouseShoulderLine` と肩先の切り落とし | `mouseShoulderLine`、`mouseShoulderCut` |
| `ClickOrigin`、`ClickRays` | `clickOrigin`、`clickRays` |
| `HeadCenter`、`HeadRadiusX/Y` | `headArea` |
| `SymbolAnchor` | `symbolAnchor` |
| `TrayIconCrop` | `trayIcon` |
| マウスの既定の範囲 | `mouseRange` |
| `MakeOutlinePen` の色 | `outlineColor` |

## 見た目の確かめ方

`tools/motion-harness/` は、アプリと同じ描画コードでコマを PNG に書き出すツールです（使い方は同フォルダの `README.md`）。

- 静止の姿勢: `pose`（手）、`face`（顔。`blink=1` で閉じ目、`mouthShown=1` で開いた口）。
- 素の動き: `sim`、`hold`、`idle`、`mouse`。しぐさを切った状態で動く。
- しぐさ: `scene`。たとえば 20 秒で眠る設定にして 36 秒で起こす:

  ```
  Harness.exe <プロジェクト> <出力先> scene "40;0.1:set:act.sleepAfter=20;36:k" 6 150,0,1100,640 .32
  ```

確かめる場面と見るところ:

| 場面 | 見るところ |
| --- | --- |
| 打鍵（単発・連打・Enter・長押し） | 手と袖口の境目、袖の付け根、手が机やキーボードからはみ出さないか |
| マウスの移動とクリック | 肩の輪郭のつながり、手首の継ぎ目、クリックの線の位置 |
| まばたき、閉じ目の保持 | 目のまわりの継ぎ目、前髪の段差 |
| 口パク、笑顔、あくび | 口のまわりの継ぎ目、口の位置 |
| 居眠りと起床 | 頭が沈んだ時に首・襟との間に穴が開かないか、記号の位置と大きさ |
| あいさつ | 手を高く上げた時の袖の伸び |
| 突き出た髪の揺れ | 根元の継ぎ目（2 倍に拡大して見る） |

コマは縮小して並べると全体の流れが、原寸や拡大で見ると継ぎ目が分かります。両方で見てください。

## 落とし穴

実際に踏んだものです。

- **描画に新しい値を使ったら `DrawnStateChanged()` の比較対象に足す**（`CharacterView.cs`）。足さないと、その値だけが変わったフレームが描かれない。速い動きなら `NeedsFullFrameRate` にも足す。
- **パーツを動かすときは、回転ではなく「線に固定したずらし」にする**。固定する線の上では動かないので、動かない部分との間にすき間が開かない（袖、マウス側の腕、アホ毛がこの方式）。回転は、手のように縁が円になっている所だけで使う。
- **切り分けた部分どうしは 1〜1.5px 重ねる**。重ねないと境目に細い線が出る。
- **絵全体を動かせるのは下方向だけ**。絵は下端で切れているので、上へ動かすと切り口が見える。
- **途中まで閉じた目は長く見せない**。下側がまっすぐ切れる。0.2 秒以内で通過させる。
- **まばたきは 0.1 刻みで全段階を書き出して見る**（`face "b:blink=0.3"` など）。`nearLid` 方式は、閉じ目の絵をまぶたの外側すべてに出すので、閉じたまつ毛の端が開いた目の外にある絵では、閉じる前からその端が黒いかけらとして見える。その場合は曲線の方式（`nearLidCurve` など）を使う。
- **開いた口の絵は、閉じた口と上唇の形がつながるものを選ぶ**。形が違う絵（閉じた口が w 形なのに、開いた口が小さな丸など）は、開き始めに 2 つの口が重なって見える。口も 0〜1 を数段階書き出して見る。
- **袖は、固定する線に平行な方向の幅しか保たれない**。手を上げて腕の向きがその線と平行に近づくほど、袖は細く見える。見えている袖が短い絵で起きやすい。線（`keyCuffAxis`）を立てる、振り上げ（`tune.key.height` など）・手を上げるしぐさ（`waveHand`、`triumphLift`）を低くする、手が胴体に寄る範囲（`keyRows`、`keyReachX`）を狭める、で抑える。線を真縦にすると袖口の上に角が出るので、コマで見比べて決める。
- **袖が曲がる線（`keyHinge`）は、袖が胴体の後ろから出てくる所に合わせる**。胴体の奥に置くと、手を大きく動かした時に、隠れていた付け根の縁が肩口から角のように出る。
- **`head.png` と `face-expression.png` は、輪郭全体が 1px ほど違うことがある**（別々に生成した絵のため）。頭まるごとの入れ替えは輪郭がちらつくので、目と口の範囲だけを使う。
- **記号や効果は、原寸で 100px 前後ないと画面で読めない**。
- **キーの位置に手を連動させない**。試したところ「同じ場所を叩いている」ように見えたため、毎回違う場所を叩く方式にしている。
- **動きは数値を見るだけで決めない**。コマを書き出して見る。それでも AI に見えるのは静止コマなので、最後は利用者に実機で見てもらう。
- Windows の Git Bash から csc を直接呼ぶと `/nologo` などの引数がパスに変換される。`.ps1` に書いて実行する。
- パスに日本語が入っていると PowerShell の出力が文字化けすることがあるが、処理は正常。
