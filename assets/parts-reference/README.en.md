# Making the parts for a new character (reference sheets and prompts)

[Back to the top](../../README.en.md) · [日本語](README.md)

This guide is for making a full set of parts for a different character by yourself. To let the AI do all of it, use the [character creation prompt](../../docs/en/character-creation-prompt.md) instead.

**All you make is one key illustration and five transparent PNG files (steps 1 and 3).** With these five files every motion and gesture of the app works (blinks, lip sync, smiling, dozing, yawning and so on).
Give the finished images to a coding AI that has this repository open, and it assembles the character (the AI's procedure is in `../../AGENTS.md`).
Steps 2, 4 and 5 are optional extras for adding more expressions in the future; the current app does not use them.

Only use pictures you are allowed to use. The sheets here are a layout reference only: take the composition and the way the parts are split, not the look of the sample character ([asset license](../../ASSETS.md)).

| File | Purpose |
| --- | --- |
| `parts-sheet.png` | Reference sheet for splitting the parts (3200×1422): one finished picture and its five layers side by side at the same scale, with the stacking order |
| `panel-1-composite.png` – `panel-6-face-expression.png` | Each panel of the sheet at full size (1469×1071), for when you want to pass a single panel |

Light blue areas mean "transparent" (image generators sometimes misread transparent reference images, so they are painted a flat colour).
To rebuild the sheets, run `.venv/Scripts/python tools/parts-sheet/make_sheet.py .`.

## What was hard with the sample character, and how to avoid it at generation time

Several things in the sample character had to be fixed in code afterwards. For a new character, avoid them when drawing.
The sheet panels are not the sample assets as they were, but split in this **corrected way** (the neck is attached to the body; everything else is split as in the sample assets).

| Problem | Cause | What to do at generation time |
| --- | --- | --- |
| Moving the head lifts the neck off the collar or makes it overlap; the head cannot move up | The neck was part of the head layer, so the code cut it out and fixed it to the body | **Attach the neck to the body (base)**, drawn long enough to reach behind the chin. The head has no neck and its jaw outline is closed (3-1, 3-4) |
| Turning the keyboard-side hand breaks the border with the cuff | The arm is one picture and the code splits the hand from the cuff; a vague border is hard to split | **Generate the arm as one piece** (generating it in parts breaks it). Have the cuff edge drawn as one clean, unbroken line (step 1, 3-2) |
| The outline of the mouse-side shoulder breaks off | A pale, unoutlined overlap stuck out at the top of the sleeve | Keep the outer outline a dark line up over the shoulder, and keep any added shoulder inside the torso outline (3-3) |
| The keyboard-side arm seems to grow out of the inside of the body | The root of the sleeve was drawn in front of the torso, and the area where the torso is redrawn on top had to be traced by hand | Draw the sleeve extending behind the torso (3-2) |
| Holding an expression such as half-closed eyes shows a square seam around the eyes and cuts the bangs | The expression image also redrew the bangs and skin colour | Draw the bangs off the irises (step 1). Each difference image changes one place only and keeps everything else pixel-identical (step 4) |
| Little room for the pursed-lips effect | Too little skin around the mouth | Leave plenty of plain skin around the mouth and under the eyes (step 1) |
| The sleeve flattens when moved toward the body | The sleeve was short with little bend | Use roomy sleeves long enough to show the bend of the elbow (step 1) |

## Overview

| Step | What you make | Times | Images to pass |
| --- | --- | --- | --- |
| 1 | Key illustration (the new character drawn in the same composition) | 1 | Reference picture of the character + `parts-sheet.png` |
| 2 | (Optional) Plan of difference images (a list in text, not an image) | 1 | Key illustration + `parts-sheet.png` |
| 3 | The five required layers | 5 | Key illustration + `parts-sheet.png` |
| 4 | (Optional) Expression differences (eyes, mouth) | as needed | `head.png` from step 3 |
| 5 | (Optional) Differences for more motion (torso and desk, swaying parts) | as needed | Key illustration or a layer from step 3 |
| 6 | Checking and redoing | — | — |

**Rules for quality**

- **Split only as much as needed.** Every extra split is another place where generation can break. Five layers are required; things the code can cut from one picture, such as the hand and the sleeve, are not generated separately.
- **One image per generation, one change per image.** Asking for the whole six-panel sheet at once, or changing the eyes and the mouth together, shifts positions and redraws unrelated areas.
- **Make everything from the same single picture (the key illustration).** Layers come from the key illustration, expressions from `head.png`. Pictures drawn separately will not line up.
- **Generate 2–3 candidates each time and pick the best**, judging in this order: not shifted, original drawing not redrawn, added areas look natural.
- Generation settings: highest quality, transparent background, high fidelity to the input image, and the same aspect ratio as the key illustration (about 1470×1070).
- After step 3, always do the checks of step 6 before moving on to the differences. Differences made on a misaligned base all have to be redone.

Replace the parts in `[ ]`. The prompts are in English because instructions come across better that way.

---

## Step 1: Key illustration

Images to pass: **Image A = reference picture of your character**, **Image B = `parts-sheet.png`**

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

Check the result before moving on. Look at: chibi proportions (2–2.5 heads tall, like the sample); head, hands, mouse and keyboard lining up with the sample; no hair over the shoulders or arms; a visible neck; clearly drawn cuffs; bangs off the irises.
**Everything is built on this picture. Redo it until you are happy with it.**

---

## Step 2: Plan of difference images (optional; answered in text)

If you only make the five required files, skip this and go to step 3.

Which difference images a character needs depends on the character (an ahoge, a tail, the shape of the mouth).
Show the key illustration to an AI that can read images (ChatGPT, for example) and have it write the list of differences and the sentences to insert into the prompts of steps 4 and 5.

Images to pass: **Image A = key illustration**, **Image B = `parts-sheet.png`**

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

If answer C reports a problem, go back to step 1 and fix the key illustration. The JOB LIST is the list of images to make in steps 4 and 5.

---

## Step 3: The required layers (5 times)

Images to pass: **Image A = key illustration**, **Image B = `parts-sheet.png`** (and, if you can, the matching `panel-N-….png`)

Each time, put the "Common" text first and then the sentence for that layer.

### Common (put at the start every time)

```
Use case: precise layer extraction for 2D animation.

Image A is the approved KEY ILLUSTRATION of the character. Image B is a PARTS REFERENCE SHEET made from a different character: panel 1 is a finished picture and panels 2-6 are the layers it was cut into. Blue-grey in Image B only marks transparency.

Produce ONE layer of Image A, cut the same way as panel [N] of Image B is cut from panel 1. Image B shows WHAT belongs to the layer and what is removed; every pixel you keep must come from Image A's character, never from Image B's character.

REGISTRATION (critical): output the ORIGINAL FULL CANVAS of Image A — same size, same aspect ratio. Every kept part stays at exactly its position, scale and angle in Image A. Do not crop to the part, recenter, zoom, rotate or redraw it. Everything not in the layer becomes genuinely transparent.
Keep Image A's painted pixels as faithfully as possible: same outlines, same line weight, same colours, same texture. Where a kept part was hidden behind something that is removed, paint the hidden area in the same style, continuing the existing lines and patterns.
Edges: clean anti-aliased edges against transparency, with no halo, no fringe of the removed neighbour's colour, and no semi-transparent haze.
No sheet, no panel frame, no labels, no text, no blue-grey filler, no checkerboard, no shadow, no extra objects. Transparent PNG.
```

### 3-1. base (N = 2) → `base.png`

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

### 3-2. keyboard-arm (N = 3) → `keyboard-arm.png`

The sleeve and the hand are taken out as one piece (the app splits the hand from the sleeve).

```
LAYER: KEYBOARD ARM (panel 3). Keep ONLY the FAR arm (on the right of the picture, reaching to the keyboard): its sleeve, cuff and hand, together as ONE piece exactly as drawn in Image A.
REMOVE everything else: head, hair, torso, near arm, mouse, keyboard, desk. No key or desk pixels may remain around or between the fingers.
Do not separate, shift or redraw the hand and the sleeve; keep the cuff edge next to the hand as the same clean, unbroken dark line it is in Image A.
The hand must be complete: paint any part of the fingers or palm that the keyboard was covering.
SHOULDER END: the sleeve starts behind the torso. Extend it about 60 pixels past where it disappears, as a smooth tapering piece of sleeve in the same fabric, so it can be tucked behind the body. Do not close that hidden end with a thick outline.
Keep the sleeve's natural shape and its bend at the elbow; do not inflate it.
```

### 3-3. mouse-arm (N = 4) → `mouse-arm.png`

```
LAYER: MOUSE ARM (panel 4). Keep ONLY the NEAR arm (lower-left of the picture): its sleeve, cuff, hand, and the MOUSE under the hand, together as one piece.
REMOVE everything else: head, hair, torso, far arm, keyboard, desk. No wood pixels may remain around the mouse or between the fingers.
The mouse must be complete, including the part under the hand and the part that touched the desk; no desk shadow.
SHOULDER (important): the sleeve's outer outline must stay the same thick dark line all the way up to the top of the shoulder, exactly where Image A draws it — do not let it fade, thin out or turn light brown. Where the top of the sleeve was hidden under the hair, complete it as a small rounded shoulder that stays INSIDE the silhouette of the torso in Image A. It must not bulge outside the shoulder line, and it must not have a pale, unoutlined edge anywhere on the outer side.
The inner side of the sleeve (the side lying over the chest) may end in a soft edge without outline, because it sits on top of the torso.
```

### 3-4. head (N = 5) → `head.png`

```
LAYER: HEAD (panel 5). Keep ONLY the entire head: all hair with its complete silhouette and every loose strand, ears, face, and every accessory worn on the head.
REMOVE the neck, torso, collar, both arms, hands, desk, keyboard and mouse.
NO NECK (important): the head ends at the chin and jaw. Close the chin and jaw with the same dark outline as the rest of the face; no skin may hang below it. The neck is part of the body layer.
Complete the parts of the hair that the shoulders, neck or collar were covering — including the hair behind the neck — closing the hair outline with the same dark line.
Do not change the face: same expression, same eyes, same mouth, same angle.
```

### 3-5. face-expression (N = 6) → `face-expression.png`

This time only, **pass the `head.png` made in 3-4 as Image A** (not the key illustration). This is the required expression image.
Its eyes and mouth are used not only for blinking and lip sync but also for smiling (closed eyes + open mouth), sleeping (closed eyes) and yawning (closed eyes + a mouth stretched downward). The closed eyes may stay on screen for several seconds, so pick one with a calm, pleasant shape.

```
LAYER: FACE EXPRESSION (panel 6). Image A here is the finished HEAD layer. Output the same head layer with exactly TWO changes:
1. Both eyes gently CLOSED in a relaxed blink: one curved dark lash line per eye at the position of the original upper eyelid, no iris visible, skin colour where the eye was. Eyebrows unchanged.
2. The mouth OPEN in a small speaking shape: about the same width as the original mouth and roughly 35-40 pixels tall, dark warm interior with a tiny tongue, keeping the character of the original upper lip. Not a big smile.
EVERYTHING ELSE must be pixel-identical to Image A: head position, size and angle, hair outline and every bang strand over the eyes, accessories, cheeks, skin colour, chin outline, canvas size and all transparent margins.
This image is sampled only around the eyes and the mouth and laid over the neutral head, so alignment and unchanged colour outside those features are essential.
```

If either feature comes out badly, make two images instead, one for the eyes only and one for the mouth only (`eyes-closed.png` and `mouth-open.png`). Changing one place at a time breaks less, and the app can read the two files instead.

**Now do the checks of step 6. Once the five files are ready, you have every image needed for assembling.**

---

## Step 4: Expression differences (optional)

**The current app does not use these images.** Smiling, sleeping and yawning work with the required `face-expression.png`. This step is for adding expressions in the future that the five files cannot make, such as half-closed eyes or a change of gaze.

Image to pass: **Image A = `head.png` from step 3** (every time; never start from a difference made earlier)

Which ones you need is decided by the answers A1–A7 in step 2. What each one is for:

| Motion you want | Difference needed |
| --- | --- |
| Hold the eyes half-closed (drowsy, narrowing the eyes in focus) | `eyes-half.png` |
| A smile shaped differently from `face-expression.png` | `eyes-smile.png`, `mouth-smile.png` |
| Look down at the hands or keyboard | `eyes-down.png` |
| Glance at the viewer now and then | `eyes-front.png` |
| Be surprised, react to rapid typing | `eyes-wide.png` |
| Press the lips together while typing | `mouth-mugu.png` |

### Common (replace only the one `Change ONLY:` line each time)

```
Use case: precise-object-edit. Image A is a registered HEAD-ONLY animation layer. Create one facial expression donor layer from it.

Change ONLY: [one line from the list below, or the sentence written for this character in step 2]

Everything else must remain PIXEL-IDENTICAL to Image A: exact head position, scale and 3/4 angle, hair outline and every bang strand over the eyes, eyebrows unless named above, cheek blush shape and colour, skin colour, any mole or face mark, accessories, chin outline, canvas size and all transparent margins. Do not recenter, crop or zoom. Keep the full original landscape transparent canvas.
Draw the changed feature with the same line weight, the same pencil texture and the same flat colours as the rest of the face. Where the feature gets smaller or moves, fill the uncovered area with the exact neighbouring skin colour, flat, with no smudge, no shadow and no outline of the old shape left behind.
This image will be sampled ONLY around the changed feature and composited over the neutral head, so pixel alignment and unchanged colour outside that feature are essential.
No torso, arms, text, white background or checkerboard. Transparent PNG.
```

### Lines to insert

If step 2 produced sentences for this character, use those instead (they name the eye colour and shapes, so they are more precise).

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

## Step 5: Differences for more motion (optional)

**The current app does not use these images.** A part that sticks out against the background without touching other hair (such as an ahoge) can already be cut from `head.png` and swayed without any extra image. This step is for parts that cannot move unless what lies under them is painted in (buns, twin tails and so on), or for adding breathing of the torso in the future.

Which ones you need is decided by the answers B1 and B2 in step 2.

### 5-1. Split the torso and the desk → `torso.png` and `desk.png` (only for moving the torso with breathing)

Image to pass: **Image A = `base.png`**. Make them in two separate generations.

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

### 5-2. Swaying parts (ahoge, bun, twin tails, ribbons, ears, tail and so on)

Make **a pair of images** for each part listed in B2 of step 2. Pass the layer the part belongs to (`head.png` for parts of the head, `base.png` for parts of the body).

**(a) The part only** → `part-[name].png`

```
Use case: precise layer extraction. Image A is a registered animation layer.
Output ONLY this one part: [description of the part, e.g. the single curved ahoge on top of the head].
Remove everything else to transparency.
Its root is at [where the root is, e.g. the crown of the head]. Extend the part about 25 pixels past its root, tapering, in the same colour and without an outline across the hidden end, so the root stays covered when the part swings.
Keep the part's outline closed everywhere else, and keep every pixel of it exactly where it is in Image A. Same full canvas, no recentering, cropping or zoom. Transparent PNG.
```

**(b) The layer without the part** → `head-no-[name].png` (`base-no-[name].png` for a part of the body)

```
Use case: precise-object-edit. Image A is a registered animation layer.
Change ONLY: remove [description of the part] completely. Repaint what it was covering — [what lies underneath, e.g. the top of the hair and its outline] — continuing the surrounding lines, colour and texture so that nothing shows the part was ever there.
Everything else must remain PIXEL-IDENTICAL: position, scale, angle, face, all other hair and accessories, canvas size and transparent margins. Do not recenter, crop or zoom. Transparent PNG.
```

To remove several parts, you may chain (b) (use the result of one removal as the input of the next). But always use expression differences made from `head.png` before any removal (the eyes and mouth do not move).

---

## Step 6: Checking and redoing

### Automatic check

```
.venv/Scripts/python tools/parts-sheet/check_layers.py <key illustration> <folder with the layers> <output.png>
```

First it checks that the five files the app reads (`base.png`, `keyboard-arm.png`, `mouse-arm.png`, `head.png`, `face-expression.png`) are all there and have transparent backgrounds. If something is missing or not transparent, it lists it and stops with exit code 1 (fix it and run it again).

If everything is there, it writes an image with the key illustration, the stacked layers and their difference side by side, and prints the share of clearly different pixels.

- **A layer whose outline glows double in the difference image is shifted or scaled.** Make it again.
- The sample character's assets give about 3.4% (differences at the roots of the arms and in painted-in areas). Much more than that means something is shifted.
- Layers with a different aspect ratio get a warning.

### Visual check

| Where to look | What is wrong |
| --- | --- |
| Shoulder (mouse side) | The dark outline breaks off; a pale unoutlined bulge sticks out |
| Neck | The head layer still has a neck; the body's neck is too short to reach behind the chin; an outline is drawn across the top of the neck |
| Keyboard-side hand and sleeve | The cuff edge line is broken; the hand or sleeve is shifted from the key illustration |
| Hands | Desk or key colours remain between the fingers; part of a hand is missing |
| Mouse and keyboard | Blank areas under the hands; the rows of keys are broken |
| Torso | The area under the arms is not painted; the shoulder outline is missing |
| Edges | White or light-blue fringes, semi-transparent haze |
| Expression differences | Anything other than the eyes or mouth (bangs, cheeks, outline) differs from the original, or is shifted by a few pixels |

### Prompts for redoing

When the position or size is off (pass the shifted image and the original):

```
Image A is the reference. Image B is a layer that was cut from it but came out misaligned.
Redo the layer so that every kept pixel sits exactly where it is in Image A: same position, same scale, same angle. Change nothing else about what the layer contains. Same full canvas as Image A, transparent everywhere else. Transparent PNG.
```

When only one area needs fixing:

```
Image A is a registered animation layer. Fix ONLY this: [what to fix, e.g. the outline of the left shoulder is missing; draw it as the same thick dark line, continuing the torso outline].
Everything else must remain PIXEL-IDENTICAL. Do not recenter, crop or zoom. Transparent PNG.
```

### If it still does not work

- **The sample character's look leaks in**: pass only the one matching panel as Image B instead of `parts-sheet.png`.
- **The background is not transparent**: have it output a flat colour (pure magenta, for example) and remove it afterwards. Avoid light blue, which is easily confused with clothes or the desk.
- **An expression difference redraws the whole face**: describe the area to change more narrowly (for example "only the upper eyelid line of each eye"). If that fails, generate from an image cropped around the face and paste the result back in place.

## Assembling

A coding AI with this repository open assembles the character following `../../AGENTS.md`. Tell it the folder with the key illustration and the five files and ask it to assemble the character (with the [character creation prompt](../../docs/en/character-creation-prompt.md), you can leave the image generation to the AI as well). This is roughly what the AI does:

1. Compares the five files with `check_layers.py` and has any misaligned one remade.
2. Creates `characters/<folder name>/` and puts the five files and `character.txt` in it (a few pixels of difference in size is fine; they are scaled to 1469×1071 when drawn). The sample in `assets/` is left untouched.
3. Measures the positions of the eyes, mouth, neck, shoulders, cuffs, wrists, symbols and so on from the pixels and writes them in `character.txt`. New assets have the neck on the body, so `neck =` is left empty. The list of items is in "キャラクターごとに変える場所" (what to change for each character) in `../../AGENTS.md`.
4. Renders frames with the app's drawing code to check seams and parts sticking out, then runs the checks (`verify.ps1`), builds and checks that the app starts.

Motion that uses the differences of steps 4 and 5 (gaze, half-closed eyes, swaying parts that need painting in, breathing and so on) is not in the app yet.
