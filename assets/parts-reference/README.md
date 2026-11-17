# 新しいキャラクターのパーツを作る（参考シートと生成プロンプト）

別のキャラクターでこのアプリ用のパーツ一式を作るための資料です。

**作るのはキー画像 1 枚と、透過 PNG 5 枚だけです（手順 1 と 3）**。この 5 枚で、アプリの動きとしぐさ（まばたき、口パク、笑顔、居眠り、あくび など）がすべて動きます。
できた画像は、このリポジトリを開いたコーディング AI に渡して組み立ててもらいます（AI 側の手順は `../../AGENTS.md`）。
手順 2・4・5 は、将来さらに表現を増やしたいときのための任意の差分で、今のアプリでは使いません。

| ファイル | 用途 |
| --- | --- |
| `parts-sheet.png` | パーツ分けの参考シート（3200×1422）。完成図 1 枚と、その 5 つのレイヤーを同じ縮尺で並べ、重ね順を添えたもの |
| `panel-1-composite.png` 〜 `panel-6-face-expression.png` | シートの各コマの原寸（1469×1071）。1 コマだけ渡したいときに使う |

水色の部分は「透明」を表します（生成 AI は透過の参考画像をうまく読めないことがあるため、単色で塗っています）。
作り直すときは `.venv/Scripts/python tools/parts-sheet/make_sheet.py .` を実行します。

## 今のキャラで苦労した点と、生成の段階での対策

今のキャラは「あとからコードで直す」ことになった箇所がいくつかありました。新しいキャラでは、絵を作る段階で最初から避けます。
参考シートのコマも、今の素材そのままではなく、この**直したあとの分け方**で作ってあります（首を体側に付けています。それ以外は今の素材と同じ分け方です）。

| 苦労した点 | 原因 | 生成の段階での対策 |
| --- | --- | --- |
| 頭を動かすと首が襟から浮く・襟にかぶる。頭を上に動かせない | 首が頭のレイヤーに付いていた。コードで首だけ切り抜いて体側に固定した | **首は体（base）に付ける**。あごの裏に隠れる高さまで長めに描く。頭は首なしで、あごの輪郭を閉じる（3-1、3-4） |
| キーボード側の手を回すと、袖口との境目が崩れる | 腕は 1 枚絵で、手と袖口の境目をコードで切り分けている。境目の線があいまいだと切りにくい | **腕は 1 枚のまま生成する**（分けて生成すると崩れるため）。袖口の縁を、途切れない 1 本のはっきりした線で描かせる（手順 1、3-2） |
| マウス側の肩の輪郭が途切れる | 袖の肩先に、輪郭のない淡い重ねしろがはみ出していた | 外側の輪郭を肩の上まで黒線のまま保ち、描き足す肩先は胴体の輪郭の内側に収める（3-3） |
| キーボード側の腕が体の内側から生えて見える | 袖の付け根が胴体の手前に描かれていた。胴体を重ね直す範囲を手でなぞった | 袖は付け根を胴体の裏まで延ばして描く（3-2） |
| 半目など「止めた表情」で目のまわりが四角く切り替わり、前髪が途切れる | 表情差分で前髪や肌の色まで描き変わっていた | 前髪を瞳にかけない絵にする（手順 1）。差分は 1 か所だけ変え、ほかは画素単位で同じにさせる（手順 4） |
| 口をすぼめる演出の逃げ場が少ない | 口のまわりの肌が狭い | 口のまわりと目の下に、無地の肌を広めに残す（手順 1） |
| 袖を体側へ寄せると潰れる | 袖が短く、曲がりが少ない | ひじの曲がりが分かる、ゆとりのある長さの袖にする（手順 1） |

## 全体の流れ

| 手順 | 作るもの | 回数 | 渡す画像 |
| --- | --- | --- | --- |
| 1 | キー画像（新キャラを同じ構図で描いた 1 枚） | 1 | キャラの参考画像 ＋ `parts-sheet.png` |
| 2 | （任意）差分の計画（どの差分が必要かの一覧。画像ではなく文章） | 1 | キー画像 ＋ `parts-sheet.png` |
| 3 | 必須レイヤー 5 枚 | 5 | キー画像 ＋ `parts-sheet.png` |
| 4 | （任意）表情の差分（目・口） | 必要な数 | 手順 3 の `head.png` |
| 5 | （任意）動きを広げる差分（胴体と机・揺れるパーツ） | 必要な数 | キー画像または手順 3 のレイヤー |
| 6 | 確認とやり直し | — | — |

**品質のための決まりごと**

- **パーツは必要なぶんだけ分ける**。分けるほど生成で崩れる箇所が増えます。必須は 5 枚で、手と袖のように 1 枚の絵からコードで切り分けられるものは分けて生成しません。
- **1 回の生成で 1 枚、変えるのは 1 か所**。6 コマのシートごと出させたり、目と口を同時に変えたりすると、位置がずれたり関係ない所まで描き変わったりします。
- **すべて同じ 1 枚（キー画像）から作る**。レイヤーはキー画像から、表情は `head.png` から。別々に描かせた絵は重なりません。
- **各回 2〜3 枚出して、いちばん良いものを選ぶ**。選ぶ基準は「位置がずれていない」「元の絵が描き変わっていない」「描き足した所が自然」の順。
- 生成の設定は、品質は最高、背景は透過、入力画像への忠実度は高、大きさはキー画像と同じ縦横比（約 1470×1070）にします。
- 手順 3 が終わったら、差分へ進む前に必ず手順 6 の確認をします。土台がずれたまま差分を作ると全部やり直しになります。

`[ ]` の中は書き換えてください。プロンプトは英語のほうが指示が通りやすいので英語にしています。

---

## 手順 1: キー画像

渡す画像: **画像 A = 作りたいキャラの参考画像**、**画像 B = `parts-sheet.png`**

```
Use case: character redraw into a fixed animation layout.

Image A is the CHARACTER reference: take the identity from it — hair style and colour, face, eye shape and colour, outfit, accessories, colour palette.
Image B is a PARTS REFERENCE SHEET. Use ONLY its panel 1 ("COMPOSITE") as the layout to reproduce. Panels 2-6 show how this picture will later be cut into layers; read them to understand which areas must stay clean and separable, but do NOT draw a sheet.

CHIBI (required): redraw the character as a CHIBI / super-deformed mascot with the SAME body proportions as the character in panel 1 — about 2 to 2.5 heads tall, a very large head, a small simple body, short arms, small mitten-like hands with simple fingers, big eyes, a tiny nose-less face. If Image A shows the character with normal or tall proportions, convert it: keep the hair, face features, outfit and colours recognisable, but shrink and simplify the body to the chibi proportions of panel 1. Never keep realistic or tall proportions.

TASK: Draw the chibi character from Image A as ONE single illustration in exactly the composition of panel 1: a chibi character sitting at a wooden desk, seen from the front-left at a 3/4 angle, upper body only. The head is in the upper-left of the canvas, turned 3/4 toward the right. The near arm (viewer's lower-left) rests its hand on a computer mouse on the desk. The far arm reaches to the right and rests its hand on a keyboard that lies diagonally on the desk. The desk runs diagonally from the lower-left to the right edge.

LAYOUT LOCK — match panel 1 as if tracing over it:
- Same canvas aspect ratio (landscape, about 1470 x 1070), same camera, same scale. Do not recenter, zoom or crop.
- Same head size and head position; eyes and mouth at the same places on the canvas.
- Same shoulder positions, same path of both sleeves, same hand positions, same mouse position, same keyboard and desk position and perspective.
- Only the character's identity changes. [Keep the desk, keyboard and mouse as in panel 1. / Restyle the desk, keyboard and mouse to: ...]

RIG-FRIENDLY DRAWING RULES (this picture will be cut into head / torso / two arms and animated):
- Neutral face: both eyes fully open, mouth small and closed. The eyes are large and clearly drawn, each with a visible iris, pupil and highlight, and are not covered by more than thin bang strands. Leave clear plain skin around the mouth and under the eyes.
- The head and hair must form one compact shape. Hair may NOT fall in front of the shoulders, arms, hands or chest; long hair goes behind the back and stays above the collar line. No hair strand may cross an arm.
- A short neck is clearly visible between the chin and the collar, and the chin has its own closed outline. The collar or neckline is open enough to show where the neck enters it. No scarf, choker, hood or hair hides the neck.
- Bangs end above the eyes or touch only their top edge; no strand crosses an iris.
- Both arms wear long, roomy sleeves with a visible bend at the elbow, ending in a clearly drawn cuff at the wrist; the edge of each cuff next to the hand is one clean, unbroken dark line, and the hand is clearly a separate shape from the cuff. Both hands are simple, fully visible, with all fingers resting on top of the mouse / keys (no fingers hidden under objects, no interlocking).
- The near sleeve overlaps the torso; its outer outline runs as one continuous dark line up to the shoulder under the hair.
- Nothing connects separate parts: no cables, straps, scarves, bag, cup or other props crossing from head to body or from arm to body. Accessories sit entirely on the head or entirely on the torso.
- Parts meant to sway later (an ahoge, a bun, twin tails, ribbons, animal ears, a tail) are drawn as clear separate shapes with their own closed outline, attached at one visible root, not merged into the rest of the hair.
- Style: thick, even, dark-brown pencil-textured outlines; flat warm colours with minimal shading; no cast shadows on the desk, no gradients, no glow, no motion lines.

OUTPUT: one clean illustration on a transparent background (or one flat solid colour if transparency is unavailable). No panels, no grid, no labels, no text, no border, no blue-grey filler.
```

できた絵を確認してから次へ進みます。見るところ: ちびキャラの頭身（見本と同じ 2〜2.5 頭身）になっているか、頭・手・マウス・キーボードの位置が見本と重なるか、髪が肩や腕にかかっていないか、首が見えているか、袖口がはっきり描かれているか、前髪が瞳にかかっていないか。
**ここが全ての土台です。納得いくまで作り直してください。**

---

## 手順 2: 差分の計画（任意。文章で答えさせる）

必須の 5 枚だけを作るなら、この手順は飛ばして手順 3 へ進みます。

キャラによって必要な差分は変わります（アホ毛があるか、しっぽがあるか、口の形はどうか）。
画像を読める AI（ChatGPT など）にキー画像を見せて、作る差分の一覧と、手順 4・5 のプロンプトに差し込む文を作らせます。

渡す画像: **画像 A = キー画像**、**画像 B = `parts-sheet.png`**

```
You are planning the animation assets for a 2D desktop mascot. Do NOT generate an image; answer in text.

Image A is the key illustration of the character. Image B shows how such a picture is cut into layers (panel 1 = finished picture, panels 2-6 = base with neck, keyboard arm, mouse arm, head without neck, face expression).

The rig animates: typing with the far hand, moving and clicking the mouse with the near hand, head sway and tilt, blinking, lip-sync, and idle life. Decide which EXTRA difference images this particular character needs, beyond the five standard layers, to animate well. Look at Image A carefully and answer every item with YES or NO and one line of reasoning.

A. Expression donors (each is the head layer with ONE feature changed):
 A1 eyes half-closed (sleepy / focused)
 A2 eyes closed in a happy smile
 A3 eyes looking down at the desk
 A4 eyes glancing at the viewer
 A5 eyes wide (surprised)
 A6 mouth pressed shut
 A7 mouth smiling open
 For each YES, write the exact "Change ONLY:" sentence for THIS character: name its eye shape, iris colour, highlight shape, lash style and mouth shape as drawn in Image A, and say how far each element moves or closes. Say NO where the drawing style makes it impossible (for example eyes hidden by hair, or dot eyes that cannot look sideways).

B. Motion extensions:
 B1 torso and desk as two separate layers (needed for breathing; say NO if the torso is mostly hidden)
 B2 detachable swaying parts: list EVERY part of this character that should swing on its own (ahoge, bun, side locks, twin tails, ponytail, ribbons, hair clip charms, animal ears, tail, hood strings, earrings ...). For each one give: a short file name in kebab-case, which layer it belongs to (head or base), where its root is, and what lies underneath it that must be repainted when it is removed.

C. Risks: list anything in Image A that will make cutting the layers hard (hair over a shoulder, a hand hidden behind an object, an accessory crossing two layers, shadows on the desk) and how the key illustration should be changed to avoid it.

Finish with a numbered JOB LIST: one line per image to generate, in the order they should be made, each with its file name and the image it is generated from.
```

答えの C に問題が挙がったら、手順 1 に戻ってキー画像を直します。JOB LIST が手順 4・5 で作る画像の一覧になります。

---

## 手順 3: 必須レイヤー（5 回）

渡す画像: **画像 A = キー画像**、**画像 B = `parts-sheet.png`**（余裕があれば該当コマの `panel-N-….png` も）

毎回、次の「共通」に続けて、レイヤーごとの文を 1 つ入れます。

### 共通（毎回の先頭に入れる）

```
Use case: precise layer extraction for 2D animation.

Image A is the approved KEY ILLUSTRATION of the character. Image B is a PARTS REFERENCE SHEET made from a different character: panel 1 is a finished picture and panels 2-6 are the layers it was cut into. Blue-grey in Image B only marks transparency.

Produce ONE layer of Image A, cut the same way as panel [N] of Image B is cut from panel 1. Image B shows WHAT belongs to the layer and what is removed; every pixel you keep must come from Image A's character, never from Image B's character.

REGISTRATION (critical): output the ORIGINAL FULL CANVAS of Image A — same size, same aspect ratio. Every kept part stays at exactly its position, scale and angle in Image A. Do not crop to the part, recenter, zoom, rotate or redraw it. Everything not in the layer becomes genuinely transparent.
Keep Image A's painted pixels as faithfully as possible: same outlines, same line weight, same colours, same texture. Where a kept part was hidden behind something that is removed, paint the hidden area in the same style, continuing the existing lines and patterns.
Edges: clean anti-aliased edges against transparency, with no halo, no fringe of the removed neighbour's colour, and no semi-transparent haze.
No sheet, no panel frame, no labels, no text, no blue-grey filler, no checkerboard, no shadow, no extra objects. Transparent PNG.
```

### 3-1. base（N = 2）→ `base.png`

```
LAYER: BASE (panel 2). Keep ONLY the desk, the keyboard, and the character's torso with its collar, neckline, everything worn on the chest, and the NECK.
REMOVE the whole head and all hair, BOTH arms with sleeves and hands, and the mouse.
NECK (important): the neck belongs to this layer, not to the head. Draw it rising out of the collar as a plain skin-coloured column with its two side outlines, and continue it UP about 70 pixels beyond where the chin covered it in Image A, so that the head can tilt and move up and down without ever exposing its top. Leave the top end open, with no outline across it. The collar overlaps the bottom of the neck exactly as in Image A.
RECONSTRUCT what the removed parts were hiding:
- the torso under both sleeves, including BOTH complete shoulders and the full side outline of the torso down to the desk, closed with the same dark outline;
- the collar and neckline behind the removed head, with no trace of hair or chin;
- continuous wood grain and the desk edge where the near arm and the mouse were;
- complete, evenly spaced keys where the far hand was resting.
Do not move or reshape the torso, desk or keyboard. Do not invent pockets, patches or new props.
```

### 3-2. keyboard-arm（N = 3）→ `keyboard-arm.png`

袖と手は 1 枚のまま取り出します（手と袖の切り分けはアプリ側で行います）。

```
LAYER: KEYBOARD ARM (panel 3). Keep ONLY the FAR arm (on the right of the picture, reaching to the keyboard): its sleeve, cuff and hand, together as ONE piece exactly as drawn in Image A.
REMOVE everything else: head, hair, torso, near arm, mouse, keyboard, desk. No key or desk pixels may remain around or between the fingers.
Do not separate, shift or redraw the hand and the sleeve; keep the cuff edge next to the hand as the same clean, unbroken dark line it is in Image A.
The hand must be complete: paint any part of the fingers or palm that the keyboard was covering.
SHOULDER END: the sleeve starts behind the torso. Extend it about 60 pixels past where it disappears, as a smooth tapering piece of sleeve in the same fabric, so it can be tucked behind the body. Do not close that hidden end with a thick outline.
Keep the sleeve's natural shape and its bend at the elbow; do not inflate it.
```

### 3-3. mouse-arm（N = 4）→ `mouse-arm.png`

```
LAYER: MOUSE ARM (panel 4). Keep ONLY the NEAR arm (lower-left of the picture): its sleeve, cuff, hand, and the MOUSE under the hand, together as one piece.
REMOVE everything else: head, hair, torso, far arm, keyboard, desk. No wood pixels may remain around the mouse or between the fingers.
The mouse must be complete, including the part under the hand and the part that touched the desk; no desk shadow.
SHOULDER (important): the sleeve's outer outline must stay the same thick dark line all the way up to the top of the shoulder, exactly where Image A draws it — do not let it fade, thin out or turn light brown. Where the top of the sleeve was hidden under the hair, complete it as a small rounded shoulder that stays INSIDE the silhouette of the torso in Image A. It must not bulge outside the shoulder line, and it must not have a pale, unoutlined edge anywhere on the outer side.
The inner side of the sleeve (the side lying over the chest) may end in a soft edge without outline, because it sits on top of the torso.
```

### 3-4. head（N = 5）→ `head.png`

```
LAYER: HEAD (panel 5). Keep ONLY the entire head: all hair with its complete silhouette and every loose strand, ears, face, and every accessory worn on the head.
REMOVE the neck, torso, collar, both arms, hands, desk, keyboard and mouse.
NO NECK (important): the head ends at the chin and jaw. Close the chin and jaw with the same dark outline as the rest of the face; no skin may hang below it. The neck is part of the body layer.
Complete the parts of the hair that the shoulders, neck or collar were covering — including the hair behind the neck — closing the hair outline with the same dark line.
Do not change the face: same expression, same eyes, same mouth, same angle.
```

### 3-5. face-expression（N = 6）→ `face-expression.png`

この回だけ、**画像 A には 3-4 で作った `head.png` を渡します**（キー画像ではありません）。必須の表情差分です。
まばたきと口パクだけでなく、笑顔（閉じた目＋開いた口）、寝顔（閉じた目）、あくび（閉じた目＋縦に伸ばした口）にも、この 1 枚の目と口を使います。閉じた目は数秒見せ続けることがあるので、穏やかで感じの良い形になっているものを選んでください。

```
LAYER: FACE EXPRESSION (panel 6). Image A here is the finished HEAD layer. Output the same head layer with exactly TWO changes:
1. Both eyes gently CLOSED in a relaxed blink: one curved dark lash line per eye at the position of the original upper eyelid, no iris visible, skin colour where the eye was. Eyebrows unchanged.
2. The mouth OPEN in a small speaking shape: about the same width as the original mouth and roughly 35-40 pixels tall, dark warm interior with a tiny tongue, keeping the character of the original upper lip. Not a big smile.
EVERYTHING ELSE must be pixel-identical to Image A: head position, size and angle, hair outline and every bang strand over the eyes, accessories, cheeks, skin colour, chin outline, canvas size and all transparent margins.
This image is sampled only around the eyes and the mouth and laid over the neutral head, so alignment and unchanged colour outside those features are essential.
```

どちらかの出来が悪いときは、目だけ・口だけの 2 枚に分けて作ってください（`eyes-closed.png` と `mouth-open.png`）。1 か所だけのほうが崩れにくく、組み込み時に 2 枚から読むようにできます。

**ここで手順 6 の確認をします。5 枚がそろえば、組み立てに必要な画像はこれで全部です。**

---

## 手順 4: 表情の差分（任意）

**今のアプリはこの差分を使いません**。笑顔・寝顔・あくびは必須の `face-expression.png` で出せます。ここは、半目や視線など、5 枚では作れない表情を将来足したいときのための手順です。

渡す画像: **画像 A = 手順 3 の `head.png`**（毎回これ。前に作った差分を元にしない）

どれが要るかは手順 2 の答え（A1〜A7）で決めます。欲しい動きとの対応:

| 欲しい動き | 必要な差分 |
| --- | --- |
| 目を半分閉じたまま止める（うとうと、集中して目を細める） | `eyes-half.png` |
| `face-expression.png` とは別の形の笑顔 | `eyes-smile.png`、`mouth-smile.png` |
| 手元やキーボードを見る | `eyes-down.png` |
| ときどきこちらを見る | `eyes-front.png` |
| 驚く、連打に反応する | `eyes-wide.png` |
| 打鍵中に口をむっと結ぶ | `mouth-mugu.png` |

### 共通（毎回、`Change ONLY:` の 1 行だけ差し替える）

```
Use case: precise-object-edit. Image A is a registered HEAD-ONLY animation layer. Create one facial expression donor layer from it.

Change ONLY: [下の表から 1 行、または手順 2 でこのキャラ用に作った文]

Everything else must remain PIXEL-IDENTICAL to Image A: exact head position, scale and 3/4 angle, hair outline and every bang strand over the eyes, eyebrows unless named above, cheek blush shape and colour, skin colour, any mole or face mark, accessories, chin outline, canvas size and all transparent margins. Do not recenter, crop or zoom. Keep the full original landscape transparent canvas.
Draw the changed feature with the same line weight, the same pencil texture and the same flat colours as the rest of the face. Where the feature gets smaller or moves, fill the uncovered area with the exact neighbouring skin colour, flat, with no smudge, no shadow and no outline of the old shape left behind.
This image will be sampled ONLY around the changed feature and composited over the neutral head, so pixel alignment and unchanged colour outside that feature are essential.
No torso, arms, text, white background or checkerboard. Transparent PNG.
```

### 差し替える 1 行

手順 2 でキャラに合わせた文ができていれば、そちらを優先してください（目の色や形の名前が入るぶん正確になります）。

```
[eyes-half.png]  Both eyes HALF-CLOSED and relaxed: the upper eyelid line lowered to cover the top 40% of each iris; the lower half of the iris, the highlight if still visible, and the lower eye outline unchanged. Mouth unchanged.
[eyes-smile.png] Both eyes closed in a HAPPY smile: each eye a single thick upward-curving arc (like "^"), no iris visible, drawn at the original eye positions and as wide as the original eyes. Mouth unchanged.
[eyes-down.png]  Both irises, pupils and highlights shifted DOWNWARD inside the unchanged eye outlines, as if looking down at the desk; the white of the eye shows above the iris. Eyelids and eye shapes unchanged. Mouth unchanged.
[eyes-front.png] Both irises, pupils and highlights shifted toward the VIEWER'S LEFT inside the unchanged eye outlines, as if glancing at the camera. Eyelids and eye shapes unchanged. Mouth unchanged.
[eyes-wide.png]  Both eyes opened WIDER in surprise: the upper eyelid line raised by about 10% of the eye height, irises slightly smaller with white visible all around them, highlights kept. Eye positions unchanged. Mouth unchanged.
[mouth-mugu.png] The mouth PRESSED SHUT in a small tight wavy line, about 70% of the original width, at the original mouth position. Eyes unchanged.
[mouth-smile.png] The mouth a small open SMILE keeping the shape of the original upper lip, warm dark interior, slightly wider than the original. Eyes unchanged.
```

---

## 手順 5: 動きを広げる差分（任意）

**今のアプリはこの差分を使いません**。背景に対して突き出ていて、ほかの髪に接していないパーツ（アホ毛など）は、差分なしで `head.png` から切り出して揺らせます。ここは、お団子やツインテールのように下の絵を描き足さないと動かせないパーツや、胴体の呼吸を将来足したいときのための手順です。

どれが要るかは手順 2 の答え（B1・B2）で決めます。

### 5-1. 胴体と机を分ける → `torso.png` と `desk.png`（呼吸で胴体を動かしたいときだけ）

渡す画像: **画像 A = `base.png`**。2 回に分けて作ります。

```
Use case: precise layer extraction. Image A is a registered BASE animation layer (torso + desk + keyboard).
Output ONLY the TORSO with its collar, its neck and everything worn on it. Remove the desk and the keyboard to transparency.
Extend the torso about 60 pixels further DOWN below the desk edge that was hiding it, continuing the fabric, its pattern and both side outlines, left open at the bottom, so the body can rise and sink behind the desk.
Keep every torso pixel exactly where it is. Same full canvas, no recentering, cropping or zoom. Transparent PNG.
```

```
Use case: precise layer extraction. Image A is a registered BASE animation layer (torso + desk + keyboard).
Output ONLY the DESK and the KEYBOARD. Remove the torso to transparency.
Where the torso touched the far edge of the desk, complete the desk edge and its outline as one clean continuous line.
Keep every desk and keyboard pixel exactly where it is. Same full canvas, no recentering, cropping or zoom. Transparent PNG.
```

### 5-2. 揺れるパーツ（アホ毛・お団子・ツインテール・リボン・耳・しっぽ など）

手順 2 の B2 に挙がったパーツ 1 つにつき、**2 枚 1 組**で作ります。渡す画像は、そのパーツが載っているレイヤー（頭のものは `head.png`、体のものは `base.png`）です。

**(a) パーツだけ** → `part-[名前].png`

```
Use case: precise layer extraction. Image A is a registered animation layer.
Output ONLY this one part: [パーツの説明。例: the single curved ahoge on top of the head].
Remove everything else to transparency.
Its root is at [根元の場所。例: the crown of the head]. Extend the part about 25 pixels past its root, tapering, in the same colour and without an outline across the hidden end, so the root stays covered when the part swings.
Keep the part's outline closed everywhere else, and keep every pixel of it exactly where it is in Image A. Same full canvas, no recentering, cropping or zoom. Transparent PNG.
```

**(b) パーツを外した側** → `head-no-[名前].png`（体のパーツなら `base-no-[名前].png`）

```
Use case: precise-object-edit. Image A is a registered animation layer.
Change ONLY: remove [パーツの説明] completely. Repaint what it was covering — [下にあるもの。例: the top of the hair and its outline] — continuing the surrounding lines, colour and texture so that nothing shows the part was ever there.
Everything else must remain PIXEL-IDENTICAL: position, scale, angle, face, all other hair and accessories, canvas size and transparent margins. Do not recenter, crop or zoom. Transparent PNG.
```

パーツを複数外すときは、(b) を順番に重ねてかまいません（1 つ外した結果を次の入力にする）。ただし表情の差分は、必ず外す前の `head.png` から作ったものを使います（目と口の位置は変わらないため）。

---

## 手順 6: 確認とやり直し

### 機械的な確認

```
.venv/Scripts/python tools/parts-sheet/check_layers.py <キー画像> <レイヤーを置いたフォルダ> <出力.png>
```

最初に、アプリが読み込む 5 枚（`base.png`・`keyboard-arm.png`・`mouse-arm.png`・`head.png`・`face-expression.png`）がそろっているか、背景が透過しているかを確かめます。足りない・透過していないものがあれば一覧を出し、終了コード 1 で止まります（直してから再実行します）。

そろっていれば、キー画像・重ねた結果・その差を横に並べた画像を書き出し、はっきり違う画素の割合を表示します。

- 差の画像で**輪郭が二重に光っているレイヤーは、位置か大きさがずれています**。作り直します。
- 今のキャラの素材では約 3.4% です（腕の付け根や描き足し部分の差）。これを大きく超えるなら、どこかがずれています。
- 縦横比が違うレイヤーには警告が出ます。

### 目で見る確認

| 見るところ | だめな例 |
| --- | --- |
| 肩（マウス側） | 黒い輪郭が途中で途切れる、淡い縁のふくらみが外へはみ出す |
| 首 | 頭のレイヤーに首が残っている、体の首が短くてあごの裏まで届いていない、首の上端に輪郭線が引かれている |
| キーボード側の手と袖 | 袖口の縁の線が途切れている、手と袖の位置がキー画像からずれている |
| 手 | 指の間に机やキーの色が残る、手の一部が欠けている |
| マウス・キーボード | 手の下が空白のまま、キーの並びが崩れている |
| 胴体 | 腕の下が塗られていない、肩の輪郭がない |
| 縁 | 白や水色のふち、半透明のもや |
| 表情の差分 | 目や口以外（前髪・頬・輪郭）が元と違う、位置が数 px ずれている |

### やり直しの指示文

位置や大きさがずれたとき（ずれた画像と元画像を渡す）:

```
Image A is the reference. Image B is a layer that was cut from it but came out misaligned.
Redo the layer so that every kept pixel sits exactly where it is in Image A: same position, same scale, same angle. Change nothing else about what the layer contains. Same full canvas as Image A, transparent everywhere else. Transparent PNG.
```

一部分だけ直したいとき:

```
Image A is a registered animation layer. Fix ONLY this: [直したい点。例: the outline of the left shoulder is missing; draw it as the same thick dark line, continuing the torso outline].
Everything else must remain PIXEL-IDENTICAL. Do not recenter, crop or zoom. Transparent PNG.
```

### それでもうまくいかないとき

- **見本のキャラの見た目が混ざる**: 画像 B を `parts-sheet.png` ではなく該当コマ 1 枚だけにする。
- **透過にならない**: 背景を単色（例: 純粋なマゼンタ）で出させ、あとで抜く。水色は服や机の色と紛れやすいので避ける。
- **表情の差分で顔全体が描き変わる**: 変える場所をもっと狭く書く（例: "only the upper eyelid line of each eye"）。それでもだめなら、顔の周辺だけを切り出した画像で作らせ、元の位置へ貼り戻す。

## 組み込み

組み込みは、このリポジトリを開いたコーディング AI が `../../AGENTS.md` の手順で行います。キー画像と 5 枚を置いたフォルダを伝えて、組み立てを頼んでください。以下は、そのとき AI が行うことの概要です。

1. できた画像を上のファイル名で `assets/` に置く（寸法が数 px 違っても描画時に 1469×1071 へ揃う）。
2. **アプリ側の対応が必要**: 今のアプリは首つきの頭を前提に、首だけを切り抜いて体側に固定している。新しい素材は首が最初から体側にあるので、その切り抜きを外す（コードは今より単純になる）。ファイル名と枚数は今と同じ 5 枚。
3. **位置合わせ**: `app/CharacterView.cs` には今のキャラに合わせた座標が入っている。
   - 目と口の切り抜き範囲、口の中心、頭を回す中心
   - キーボード側の袖の蝶番の位置、手のひら中心、手と袖口の境目の線
   - 胴体を袖の手前に重ねる範囲
   - マウス側の肩・手首の高さ

   - 突き出た髪を揺らすための切り出し、なでなでの判定に使う頭の範囲、記号を出す位置

   手順 1 で見本と同じ位置に描けていれば小さな調整で済むが、ゼロにはならない。一覧は `../../AGENTS.md` の「キャラクターごとに変える場所」にある。
4. **手順 4・5 の差分を使う動きは、まだアプリにない**。視線・半目・描き足しの要る揺れるパーツ・呼吸などは、画像がそろってから実装する。
