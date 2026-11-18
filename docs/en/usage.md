# Usage

[Back to the top](../../README.en.md) · [日本語](../usage.md)

The app's menus are in Japanese. Each label below is shown as it appears in the app, followed by its meaning in parentheses.

## Starting

Double-click `Start.cmd`.

- On the first run, and after an update changes the source, it builds the app with the C# compiler that comes with Windows before starting it (a few seconds). It runs on Windows with .NET Framework 4 (tested on Windows 11 only; Windows 10 has not been tested).
- The mascot appears at the lower right of the screen and waves hello.
- It never starts twice. It does not add itself to Windows startup.
- To quit, right-click the mascot or its notification-area icon and choose **終了** (Quit).

## Menu

Right-clicking the mascot and right-clicking its face icon in the notification area open the same menu.
Even while clicks pass through the mascot or it is hidden, every setting can be changed from the notification area.

| Action | What it does |
| --- | --- |
| Drag the mascot with the left button | Move it |
| Move the pointer left and right over its head | Head pats (works even while clicks pass through) |
| **音声** (Audio) → **オフ** (Off) / **マイク・仮想入力** (Microphone / virtual input) / **PC 再生** (PC playback) | Choose what drives the lip sync (off at start-up) |
| **音声 → オーディオデバイス設定…** (Audio device settings…) | Choose the device, check the level, set the lip-sync sensitivity |
| **音声 → 口パクテスト（音声不要）** (Mouth test, no audio needed) | Open and close the mouth without any sound |
| **動き** (Motion) → **キーボードの動きの大きさ** (Keyboard motion size) | **控えめ** (Subtle) / **大きく（標準）** (Large, default) / **とても大きく** (Very large) |
| **動き → 動きを抑える** (Reduce motion) | Tone down all motion and stop the gestures |
| **動き → マウスの移動範囲…** (Mouse range…) | Set how far the mascot's mouse travels, in four directions |
| **動き → 動きの細かい調整…** (Fine motion tuning…) | Turn gestures on or off and adjust motion values with sliders |
| **動き → デモ** (Demo) | Demonstration with automatic typing and clicks |
| **表示** (Display) → **小 320** / **中 440** / **大 560** | Width 320 / 440 / 560 (small / medium / large) |
| **表示 → クリック透過** (Click-through) | Let clicks pass to the window underneath. Ctrl+Alt+C also toggles it |
| **表示 → 画面内に戻す** (Bring back on screen) | If you lose it, move it to the lower right of the screen with the pointer |
| **キャラクター** (Character) → (name) | Switch the character ([Switching characters](characters.md#switching-characters)) |
| **キャラクター → キャラクターのフォルダを開く** (Open the characters folder) | Open the `characters` folder where characters are placed |
| **一時的に隠す** (Hide for now) / **表示する** (Show) | Hide it during meetings or screen sharing. While hidden, drawing, input monitoring and audio capture all stop |
| Double-click the notification-area icon | Show it and turn click-through off |

If another app already uses Ctrl+Alt+C, the menu says so; turn click-through off by double-clicking the notification-area icon instead.

## Motion

- **Keyboard**: every key press sends the far hand to a different spot on the keyboard (it is not tied to the key you pressed). The hand grows as it rises, squashes sideways when it lands and bounces back. The wrist lags behind, and the head dips and tilts.
- **Enter**: an extra-big hit; the eyes squeeze shut and the whole picture sinks and bounces back.
- **Holding a key**: after about 0.4 seconds it hammers the same spot until you let go.
- **Mouse**: the near hand chases your mouse quickly, overshoots a little and settles. Fast sideways moves make the wrist tilt behind. Up/down and left/right are mirrored to match the mascot, which faces you.
- **Click**: the hand squashes and bounces back, and three short lines pop out of the mouse.
- **Idle**: the head sways slowly, leans a little toward the pointer and sometimes tilts. Blinks come at irregular intervals and speeds, sometimes two or three in a row.
- **Mouth**: it purses its lips while you type.
- **Ahoge** (the lock of hair that sticks up): it lags behind the head and swings back when the head stops.

Modifier keys alone (Shift, Ctrl, Alt and so on) do not make it type.

## Gestures

Gestures are things the mascot does on its own, apart from reacting to keys and clicks.

| Gesture | When | What happens |
| --- | --- | --- |
| Hello | At start-up, and when it comes back from being hidden | Waves with a smile; stops as soon as you type |
| Dozing | After a while with no activity (2 minutes by default) | A big yawn with a tear → three heavy blinks → sleep. It nods off, jerks awake and floats z's |
| Waking up | Doing something while it sleeps | A key, click, voice or quick mouse move: it jumps awake with a "!". A gentle mouse move: it quietly opens its eyes. The tiny jitter of a resting mouse does not wake it |
| Nailed it | Enter after 20 or more key presses in a row | Raises its hand high with a big smile, with sparkles (not more than once every 12 seconds) |
| Head pats | Move the pointer back and forth over its head at least twice | Narrows its eyes, opens its mouth, sways its head with your hand and shows a heart. Just passing over does nothing |
| Thinking | Typing stops for 2–3 seconds | Sometimes tilts its head with "…" and taps a finger |
| Sweating | Very fast typing (7 or more keys a second) continues | Sweat drops fly |

Each gesture can be turned on or off on the **しぐさ** (Gestures) tab of **動き → 動きの細かい調整…** (Fine motion tuning…).
With **動きを抑える** (Reduce motion) on, no gestures appear. They also pause while you adjust the mouse range.

## Lip sync (audio)

Choose the audio and device for the lip sync in **音声 → オーディオデバイス設定…** (Audio device settings…) and press **保存して適用** (Save and apply).

- **マイク・仮想入力** (Microphone / virtual input): microphones and virtual audio registered as recording devices. For a virtual cable, choose its recording side.
- **PC 再生** (PC playback): speakers, headphones, virtual outputs and other devices that are playing sound. It reacts to music and sound effects too.
- **Windows の既定** (Windows default): uses the default device. If you change the default, turn audio off once and choose it again.
- **試す** (Try) next to each device shows its level and how the mouth would open, on a meter (trying neither saves nor switches anything).
- You can adjust the sensitivity and the level below which sound is ignored. If the mouth does not react, raise the sensitivity; if it stays open, raise the ignore level.
- Devices are saved by ID, not by name. If the chosen device is unplugged, the choice is kept and the app never switches to another device on its own.
- Audio is always off at start-up. Choosing a source starts it with the saved device.

The mouth opens and closes with the volume (it does not recognise vowels). Sound played in exclusive mode may not be picked up.
**音声 → 口パクテスト（音声不要）** (Mouth test) shows small, medium and large mouth openings without any sound.

## Fine motion tuning

**動き → 動きの細かい調整…** (Fine motion tuning…) turns gestures on or off and changes motion values.

- It has five tabs: **しぐさ** (Gestures), **キーボードの手** (Keyboard hand), **マウスの手** (Mouse hand), **頭** (Head) and **まばたき・口** (Blinks and mouth), with about 50 items: how high the hand rises, how much it squashes on landing, how fast it chases the mouse, the length of the click lines, the ahoge sway, the blink interval, the time before dozing, the size of the symbols, how much the desk shakes on Enter, and more.
- There is no OK or Cancel. Changes apply to the mascot at once and are saved automatically shortly after. You can keep typing and clicking with the window open to try them.
- Turn on **デモを流す** (Run the demo) to tune while watching automatic typing and clicks.
- Items changed from their defaults are shown in bold. **戻す** (Reset) resets one item; **すべて初期値に戻す** (Reset all) resets everything.
- **変えた値をコピー** (Copy changed values) copies a list of the changed values to the clipboard.
- To hide the click lines, set **マウスの手 → クリックの線の長さ** (Mouse hand → click line length) to 0. To hide the symbols, set **しぐさ → 記号の大きさ** (Gestures → symbol size) to 0.

## Mouse range

**動き → マウスの移動範囲…** (Mouse range…) sets how far the mouse in the mascot's hand moves. It does not change how far your own pointer moves.

- Set up, down, left and right one at a time. The direction is the way you move your mouse.
- Click inside the picture and move your mouse the way the guide says; the mascot's hand follows. The farthest position you reach is remembered. Measuring stops when you leave the picture, press Space or switch to another window.
- Fine-tune the reach with **端の位置で止めて調整** (Stop at the edge and adjust), the slider, **－ 小さく** (Smaller) / **大きく ＋** (Larger), or the arrow keys.
- **この方向で決定** (Confirm this direction) moves to the next direction and **戻る** (Back) to the previous one. Click a direction name at the top to jump to it.
- **この方向を初期値に戻す** (Reset this direction) and **開いた時の値に戻す** (Restore the values from when the window opened) reset only the current direction.
- **保存して完了** (Save and finish) on the fourth direction saves the result. **キャンセル** (Cancel) or × returns to the values before you started.

## Saved settings and performance

- Position, size, motion strength, mouse range, the chosen audio device and the lip-sync sensitivity are saved in `bin/settings.txt`; values changed in fine motion tuning are saved in `bin/tuning.txt`. Both stay on this PC.
- When a character other than the sample is selected, which character it is and its mouse range are also saved in `bin/settings.txt`, and its fine motion tuning in `bin/tuning-<folder name>.txt` (separately for each character).
- Settings are written to a temporary file first and then swapped in, so a failed save never loses the previous settings.
- If saving fails, or the settings cannot be read at start-up, a notice and the top items of the menu tell you (save again / reload the settings / overwrite with the current settings). Settings that could not be read are never overwritten without asking.
- While nothing is happening the app redraws about 30 times a second and skips frames that look the same. On a key, a click, a fast mouse move or lip sync it switches back to the display's refresh rate from that frame. It also stays at about 30 times a second while sleeping.
- While hidden (**一時的に隠す**), drawing, input monitoring and audio capture all stop.

## Privacy

- For keys, the app only checks that a key was pressed, whether it was Enter and whether it is being held. **It does not record which key you pressed or what you typed.**
- Only the volume of the audio is used. Nothing is recorded or saved.
- It makes no network connections.
- The only files it saves are the settings files above.
