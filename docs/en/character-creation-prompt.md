# Character creation prompt

[Back to Adding and switching characters](characters.md) · [日本語](../character-creation-prompt.md)

Use the **Japanese prompt** on the [Japanese page](../character-creation-prompt.md) as it is, whatever language you speak. It is the version that has been tested from start to finish, and the AI understands it fully. You only copy and paste it.

How to use it:

1. Open this folder in Codex in the ChatGPT desktop app, or in Codex CLI.
2. On the [Japanese page](../character-creation-prompt.md), copy all the text in the box. It starts with 「添付画像のキャラクターを、このリポジトリの新しいデスクトップマスコットとして作ってください。」
3. In the second line, replace 【キャラクター名】 (including the 【 】 brackets) with your character's name, for example 名前は「Mochi」です。
4. To get the AI's messages and final report in English, add this line at the very end:

   ```text
   Please write all your messages and the final report in English instead of Japanese.
   ```

5. Attach a reference picture of the character you want and send it together with the text.
6. The AI shows you one key illustration and asks you to confirm it. Approve it if you like it. After that, the AI carries on until the character can be chosen in the app.
7. When it is ready, run it in the app and ask for follow-up fixes where something looks unnatural ([Polishing with follow-up requests](#polishing-with-follow-up-requests)). You can write those requests in English.

The recommended model is **GPT-6.2 Sol (reasoning effort: high)**. Making a character with it has been confirmed, and it uses fewer tokens. Every generation is different. Only attach a picture you are allowed to use. The bundled characters (Tomari, Pink-chan and Koji-chan) cannot be used as references ([asset license](../../ASSETS.md)).

## What the prompt asks for

So that you know what you are sending, the Japanese prompt asks the AI to:

- Read `AGENTS.md`, `README.md`, `assets/parts-reference/README.md` and your picture, and use the existing mechanism for adding characters. It explicitly allows the AI to generate and edit the images.
- Generate one key illustration of your character as a chibi at a desk with a keyboard and a mouse, show it to you, and wait for your approval. After you approve, carry on without asking again.
- Generate the transparent PNG parts, check them (including with `check_layers.py`), and fix or regenerate anything misaligned.
- Add the character under `characters/`, measuring the positions of the eyes, mouth, neck, shoulders, cuffs, wrists and so on from the pixels and writing them in `character.txt`.
- Render frames and check blinks, lip sync, typing, the mouse, gestures, seams and holes; build, run the regression checks and check that the app starts.
- Keep the assets, settings, prompts and results in the repository, without breaking existing characters or your settings, and without committing.
- Report at the end what was added, how to use it, what was checked and what is left (in plain Japanese, or in English if you added the line above).

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
