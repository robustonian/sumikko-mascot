# Adding and switching characters

[Back to the top](../../README.en.md) · [日本語](../characters.md)

## Make your own character

To have the AI do everything from a reference picture, use the [character creation prompt](character-creation-prompt.md). After you approve one key illustration, the AI generates the parts, adds them to the app and checks them. You need an environment that can generate images and work inside a folder (Codex in the ChatGPT desktop app, Codex CLI, and so on).

To prepare the parts yourself, go through these three stages.

1. **Prepare one picture.** A picture of the character you want (any proportions and pose).
2. **Generate the parts.** Following [Making the parts](../../assets/parts-reference/README.en.md), give an image generator the reference sheet `parts-sheet.png` and the prepared prompts. You get one key illustration in the same composition as the sample, and five transparent PNG files.

   | File | Contents |
   | --- | --- |
   | `base.png` | Torso, neck, desk and keyboard |
   | `keyboard-arm.png` | The far arm (sleeve and hand) |
   | `mouse-arm.png` | The near arm and the mouse |
   | `head.png` | The head (without the neck) |
   | `face-expression.png` | The head with closed eyes and an open mouth (used for blinking, lip sync, smiling, sleeping and yawning) |

   These five are all that is required. Every motion and gesture in the [usage guide](usage.md) works with them.
3. **Have an AI assemble it.** Open this repository in a coding AI such as Codex (ChatGPT desktop app or Codex CLI) or Claude Code, tell it the folder with the key illustration and the five files, and ask it to assemble the character. Following [AGENTS.md](../../AGENTS.md), the AI checks the images, measures the positions, builds, runs the checks and reviews the motion frames. The finished character goes into `characters/<name>/` and can be chosen under **キャラクター** (Character) in the menu. The sample character stays as it is.

Good to know:

- The positions (eyes, mouth, wrists, shoulders and so on) are written in each character's settings file, `character.txt`. The AI measures the picture and fills it in. The list of items and their descriptions is in the sample [assets/character.txt](../../assets/character.txt) (comments in Japanese).
- [Pink-chan](../../characters/pink-chan/README.md) and [Koji-chan](../../characters/koji-chan/README.md) are worked examples of adding a character (records in Japanese). Depending on the generated pictures, fitting the positions or regenerating some images is needed.
- Only use pictures you are allowed to use. The bundled characters' pictures cannot be used as references ([ASSETS.md](../../ASSETS.md)).

## Switching characters

- The **キャラクター** (Character) menu lists the sample and the characters in the `characters` folder. Choosing one switches at once, and the same character appears the next time the app starts.
- One character is five transparent PNG files and a `character.txt` in `characters/<any folder name>/`. A folder without all five files is not listed. Instead of `face-expression.png`, you may provide two files: `eyes-closed.png` (head with closed eyes) and `mouth-open.png` (head with an open mouth).
- Bundled characters: the sample (`assets/`), [Pink-chan](../../characters/pink-chan/README.md) (`characters/pink-chan/`) and [Koji-chan](../../characters/koji-chan/README.md) (`characters/koji-chan/`).
- The name shown in the menu is `name = …` in `character.txt` (the folder name if it is not set).
- Items not written in `character.txt` use the sample's values. If a line cannot be read, the app tells you which item when you switch, and shows that item with the sample's value.
- The mouse range and the fine motion tuning are remembered separately for each character. You cannot switch while the mouse range or fine motion tuning window is open.
- The notification-area icon also becomes the chosen character's face.
