# ADR-0096: Keyboard bindings are key positions

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/demos-camera-help-keyboard-tasks.md`, decisions D2 to D7, approved by the author on 2026-11-15

## Context

MonoGame's `Keys` are the keys of the characters typed, so they depend on the keyboard layout. The demos, the camera navigation and the input mappings were written for a QWERTY keyboard: on AZERTY, W A S D is spread over the keyboard (W sits at the bottom left) and the help texts named keys the author did not find where expected. Unity's Input System and Godot (`physical_keycode`) can read keys by physical position.

## Decision

- A keyboard binding of an input mapping (`KeyButton` of a button or an axis, from a `.buttonsMapping` file or from code), of the UI navigation actions, and the letter keys of the camera navigation (`ViewportCameraNavigation`, game and editor) name the **position** of that key on a US QWERTY keyboard. They are read on the active layout: identical on QWERTY, W A S D becomes Z Q S D on AZERTY.
- Translated keys: letters, top-row digits and punctuation. Function keys, arrows, the numeric keypad, Escape, Enter, Space and the modifiers are kept.
- `KeyboardLayout.FromQwertyPosition` gives the key of the active layout; the table is built once from Windows (`GetKeyboardLayout`, `MapVirtualKeyEx`), identity on other systems or when the layout cannot be read. `KeyButton.KeyOnLayout()` is the key to read; `KeyButton.Key` is never changed, so a saved mapping keeps its keys and the file format does not change.
- Kept by letter: the editor's shortcuts (Ctrl + letter, gizmo W/E/R, timeline) and the scripts' direct reads `PlayerInput.IsKeyPressed`, `IsKeyJustPressed`, `IsKeyReleased`.
- Texts that name a mapped key are written from the bound keys on the active layout (demo sheets, help panel, the text overlays of the Audio, Animation blend and Animation IK demos).
- The RPG demo, which bound its moves to Z Q S D (AZERTY letters), binds W A S D.

## Consequences

- A project's mapping now works by position on every layout; a mapping written for AZERTY letters (as the RPG demo was) must be rewritten in QWERTY positions.
- A layout changed while the game runs (Alt + Shift) is not followed until the next start.
- `CasaEngine.Tests` reads keys on QWERTY whatever the machine (`TestKeyboardLayout`), and the tests that set another layout run in a collection that is never parallel.
- Tests: `KeyboardLayoutTests`, `KeyPositionBindingTests`, `UINavigationInputTests.OnAzerty_AMappedLetterIsReadAtItsQwertyPosition`.
