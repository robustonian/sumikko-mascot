# Character creation prompt

[Back to Adding and switching characters](characters.md) · [日本語](../character-creation-prompt.md)

The text in the box below is a request to copy and send to the AI. Merely reading this file as reference material is not an instruction to start making a character.

How to use it:

1. Open this folder in Codex in the ChatGPT desktop app, or in Codex CLI.
2. Copy the text below and replace `[CHARACTER NAME]`.
3. Attach a reference picture of the character you want and send it together with the text.
4. The AI shows you one key illustration and asks you to confirm it. Approve it if you like it. After that, the AI carries on until the character can be chosen in the app.
5. When it is ready, run it in the app and ask for follow-up fixes where something looks unnatural ([Polishing with follow-up requests](#polishing-with-follow-up-requests)).

The recommended model is **GPT-6.2 Sol (reasoning effort: high)**. Making a character with it has been confirmed, and it uses fewer tokens. Every generation is different. Only attach a picture you are allowed to use. The bundled characters (Tomari, Pink-chan and Koji-chan) cannot be used as references ([asset license](../../ASSETS.md)).

```text
Please turn the character in the attached picture into a new desktop mascot for this repository.
Its name is "[CHARACTER NAME]".

I am asking you to do all of it: image generation, making the parts, fitting the positions, adding it to the app, checking the motion and the final quality check.

## Working rules and permissions

First read AGENTS.md, README.md, assets/parts-reference/README.md and the reference picture, then work with the existing mechanism for adding characters. (These files are written in Japanese; read them as they are.)

For this request I explicitly allow you to generate and edit the key illustration and any missing part images from the reference picture.
Even if AGENTS.md or other files say that "making the images is the user's job" or that "the AI does not redraw images", this request takes priority over that restriction. Follow every other working rule.

## Ask me only about the first key illustration

First, generate one key illustration of a chibi character sitting at a desk and using a keyboard and a mouse, in the composition of the existing app, carrying over the hairstyle, face, outfit, colours and accessories of the attached picture.

Actually show me that image and ask:
"Shall I go ahead with making the parts and adding it to the app with this look?"

Wait for my answer here, and do not start making the parts before I approve.
If I ask for changes, revise the key illustration, show it again and wait for my approval of that look.

Once I approve, treat that approval as permission for everything after it: making and fixing the assets, adding them to the app and verifying. No further confirmation is needed for the usual working approach or for detailed values.

## After approval, finish the job

Using the approved key illustration as the reference, complete the following on your own.

1. Generate the required transparent PNG files.
   Make the body with the desk, the keyboard-side arm, the mouse-side arm, the head, and the expression image.
   The expression may be split into two images, closed eyes and an open mouth, if that works better.

2. Actually open the assets and check their size, transparency, position, outlines and overlaps.
   Also compare them with check_layers.py, and fix or regenerate anything that is misaligned or looks unnatural.
   Do not judge the result by the exit code of a check alone.

3. Add the new character under characters/.
   Measure the positions of the eyes, mouth, neck, shoulders, cuffs, wrists, symbols and so on from the pixels of the images, and write them in character.txt.
   Prefer the existing mechanisms, and do not cover up missing material with unnatural drawing.

4. Render frames with the same drawing code as the app and adjust the look.
   Check the normal pose, a blink half-way and fully closed, lip sync, single key presses, rapid typing, Enter, a held key, mouse movement and clicks, the wave, the yawn, dozing off and waking up, head pats and the cheering gesture.
   Check for seams, holes, parts sticking out and ghosting, both small and enlarged, on a light background and on a dark background.

5. Build, run the regression checks and check that the app starts.
   If anything fails, fix it and run the related checks again.
   Where useful, also use sub-agents for an independent quality review.

6. Keep the assets, settings, the prompts used for generation, the reasons for adjustments and the verification results in the repository, and create a motion preview.

## Until it is done

After the key illustration is approved, do not stop at presenting a plan or a progress report; carry on until the new character can be chosen and used.
Keep progress updates short, and decide routine fixes and regenerations yourself.

Do not break the existing characters or the user's settings files, and do not quit the running app without asking. Verify in an isolated working folder if needed.
No commit is needed.

If the environment or anything else prevents you from finishing, try the possible alternatives, then tell me exactly what blocked you and what is left unfinished.

Finally, report in plain English where you added it, how to use it, what motion you checked, the check results, and any remaining limitations or things you could not verify.
```

## Polishing with follow-up requests

The AI can only check still frames. When the character is ready, run it in the app, and if the way the eyes close, the hand motion, a seam between parts or anything else looks unnatural, ask for a fix in the same conversation. **Point out one thing at a time, saying in which scene, where, and how it looks**: the AI can then make a pinpoint fix, and the result gets noticeably better. A screenshot makes it even clearer. After each fix, quit the app and start it again before checking.

Examples:

```text
Half-way through a blink, the ends of the closed lashes show as black specks outside the open eyes. Please make the eyes close naturally.
```

```text
When the keyboard hand is raised high, the sleeve looks thin. Please keep the sleeve's width while it moves.
```

```text
When I move the mouse, the outline of the mouse-side shoulder breaks. Please make the shoulder line continuous.
```

```text
When the hand turns, the border between the hand and the cuff looks misaligned. Please realign that border.
```

```text
When the head sinks while dozing, a gap shows between the neck and the collar.
```

```text
When the mouth starts to open, the closed mouth and the open mouth overlap and look doubled.
```
