# Linuxで使う

[トップへ戻る](../README.md)

Linux版は Python 3 と PyQt5 で動きます。Windows版と同じ PNG・`character.txt` を読み込みます。Windows版のビルドや Wine は不要です。

X11 または Wayland のデスクトップセッションで起動します。Waylandで位置指定や最前面表示を使うには XWayland が必要です。

## 起動

以下は Ubuntu / Debian のインストール例です。ほかのディストリビューションでは、利用しているパッケージ管理ツールで Python 3 と PyQt5 を用意してください。

```sh
sudo apt install python3-pyqt5 pulseaudio-utils xwayland
```

リポジトリのフォルダで実行します。

```sh
./Start.sh
```

キャラを左ドラッグすると移動でき、右クリックすると設定メニューが開きます。キャラクターの切り替え、表示サイズ、動きの調整、マウスの移動範囲、口パク、一時停止、終了を選べます。通知領域がある環境では、そこにもメニューが出ます。

隠した場合やクリックを背面に通している場合は、もう一度 `./Start.sh` を実行すると、起動中のキャラを操作できる状態で表示します。画面の外へ移動してしまった場合は、通知領域の **画面内に戻す** も使えます。通知領域がない環境では、隠す・クリック透過の項目を表示しません。

```sh
./Start.sh --character=pink-chan  # 起動時に選ぶ（未起動のとき）
./Start.sh --character=koji-chan
./Start.sh --demo                 # 入力なしで動きを見る
./Start.sh --diagnose             # 入力機器の読み取り状況を見る
```

`Start.sh` はまず PATH 上の `python3` を使い、PyQt5 を読み込めない場合は `/usr/bin/python3` も試します。変更する場合は `SUMIKKO_PYTHON=/path/to/python ./Start.sh` とします。

## 描画方式

起動時に内蔵GPU・専用GPUでQt OpenGL描画が使えるか確認します。OpenGLの初期化に失敗した場合、透過描画が使えない場合、または llvmpipe など既知のソフトウェア描画を検出した場合は、QtのCPU描画を使います。選ばれた方式は起動時の端末と、右クリック → **入力・音声の状態…** で確認できます。

輪郭やまばたきの細部は描画方式によってわずかに異なります。従来の描画を使用したい場合は、アプリを終了してから `./Start.sh --renderer cpu` で起動してください。

## キー入力とマウス

Linux版は、読み取り可能な `/dev/input/event*` から操作を受け取ります。Waylandのアプリを操作している間も反応します。入力機器を占有せず、キーの内容をファイルに記録しません。機器の抜き差しも定期的に確認します。

入力機器の読み取り権限がない環境では、X11の入力取得で補完します。Wayland上でこの補完方法が見られるのはXWaylandのアプリのキーだけです。反応しないときは **入力・音声の状態…** または `--diagnose` で確かめてください。デモは読み取り権限なしでも使えます。

直接入力を有効にするには、管理者が入力機器へのアクセスを許可する必要があります。たとえば `input` グループが `/dev/input/event*` を読める環境では、`sudo usermod -aG input "$USER"` の後、ログアウト・ログインして反映します。このグループはほかのアプリにも全入力機器の読み取りを許可するため、必要な環境でだけ設定してください。アプリが権限やグループを自動で変更することはありません。

Waylandでは画面全体のポインター座標を取得できないため、マウスの移動量で手元を動かします。X11では画面上の位置で動かします。どちらもキャラクターの安全な移動範囲内に収めます。

## 透過表示とWayland

GNOME Waylandで、ウィンドウの位置指定・最前面表示・ドラッグ移動を使うため、`Start.sh` は `DISPLAY` がある場合に Qt の X11 バックエンド（`xcb`）を選びます。Waylandからログアウトする必要はありません。

XWaylandがない環境や、`QT_QPA_PLATFORM=wayland ./Start.sh` で直接Waylandを使う場合、位置指定・最前面表示などはウィンドウ管理側の制約を受けます。

通常は透明部分を含むウィンドウの範囲でマウス操作を受け取ります。背面の作業を優先したいときは、通知領域のメニューから **クリックを背面に通す** をオンにしてください。

## 音声・保存先

起動時の音声はオフです。**音声（口パク）** で既定のマイク、PCの再生音、または個別の音声入力を選びます。`parec` と `pactl` を使うため、PulseAudio、またはPulseAudio互換機能のあるPipeWireが必要です。音声は音量の計算だけに使い、保存しません。

Linuxの設定は `bin/linux/settings.json` に保存します。キャラクターごとの調整値もこのファイルに入ります。Windowsの `bin/settings.txt`・`bin/tuning*.txt` は読み書きしません。保存先を変える場合は `--settings-dir=/path/to/directory` を使います。

画像・座標はWindows版と共用ですが、アニメーション処理と設定画面はLinux用の実装です。しぐさのタイミングや記号の形はWindows版と完全には同じではありません。Windowsのマウス範囲の自動測定、グローバルショートカットはLinux版にはありません。Linuxでは範囲の数値設定と、再起動による表示復帰を使ってください。

## 開発・検証

```sh
./verify.sh
```

Linux用の回帰検査41項目と、同梱3キャラクターそれぞれの3秒以上の起動検査を行います。検査は一時フォルダの設定を使い、個人設定を変更しません。検証画像は `preview/linux/` に保存します。腕、顔、マウス、あいさつ、居眠り、口パク、まばたきの0.1刻みのコマを含みます。

`verify.sh` は画面のない環境でも Qt の `offscreen` で動きます。実際のデスクトップ上での確認は別に行ってください。

```sh
./Start.sh --smoke-test --demo   # 3秒以上表示して終了、設定保存なし
```

Linux版のソースは `linux/` です。`tuning.json` はWindowsの `MotionTuning.cs` の調整項目に対応し、回帰検査で名前・範囲・初期値の一致を確かめます。座標の初期値は `assets/character.txt` を読み込みます。Windowsの既存298項目の検査は、引き続きWindowsで `verify.ps1` を実行します。

実装時に参照した仕様: [Qtの透過ウィンドウ](https://doc.qt.io/archives/qt-5.15/qwidget.html#creating-translucent-windows)、[Linuxの入力イベント](https://www.kernel.org/doc/html/latest/input/event-codes.html)。
