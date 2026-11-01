# ADR-0080: Data-driven navigation through a UI input mapping

- **Status**: Accepted
- **Date**: 2026-10-31
- **Source**: this chantier: `ai-agent/tasks/data-driven-navigation-tasks.md` (decisions D1 to D9, delegated to and settled by the author on 2026-10-31, complements C1 to C8), branch `chantier/data-driven-navigation`. Amends ADR-0078.

## Context

- A project with no DLL drives its screens with the keyboard and the gamepad through MGUI's built-in path: its tables are fixed `switch` statements (`MGDesktop.cs:212`, `UIFocusNavigationService.cs:562`), keys do not repeat, gamepad timings are only settable in code, a held A repeats Submit, and focus stops at the edges.
- MGUI has a public second path: `UseRawNavigationInput` set to false cuts the built-in reading, and `TryHandleInputAction` dispatches an `InputAction` and applies the focus change (`MGDesktop.cs:640-651`, `:715`). No engine code uses it.
- `.buttonsMapping` assets load (`ButtonsMapping.cs`, `AssetLoaderRegistry.cs:52`) and nothing reads them at run time.
- `NavigateTo` tries `NavigationNeighbors` first, so a focus wrap needs no MGUI change (`UIFocusNavigationService.cs:176-180`).
- Cancel has no fallback: `TryHandleInputAction` returns false when no control consumed it (`MGButton.cs:374-375`, `UIFocusNavigationService.cs:351-361`).

## Decision

- **A project names its UI mapping.** The optional project field `UIInputMapping` holds the id or name of a `.buttonsMapping`. Left out, nothing changes. Under `UseExternalViewManagement` it does nothing: the editor owns its input. `CasaEngineGame` reads it once after the asset loaders are registered, and an unknown name or an unreadable file stops the game there.
- **`UINavigationInput` is an engine class.** It takes an `MGDesktop` and a `ButtonsMapping`, sets `UseRawNavigationInput` to false and calls `TryHandleInputAction` on input edges. Its logic takes injected button states, so repeat is testable without a gamepad. `UIRoot` creates it and calls it between `Desktop.Update` and the screen stack, so a command runs on the frame of its input.
- **Entry names are the names of `InputAction`.** Any other name is ignored, and so is a gameplay action, so one file can also serve gameplay. `Submit`, `Cancel`, `NavigateUp`, `NavigateDown`, `NavigateLeft` and `NavigateRight` are required. `UINavigationInput` checks the content in its constructor, not when the file is read: a mapping is refused when `UIRoot` builds it, so when the first view is created, which in the runtime is during the `UpdateWorld` that loads the first world. It is refused when:
  - a required action is missing or binds no input;
  - a UI action is an analog input;
  - a UI action is declared twice, even when one of the entries binds no input: `InputMappingManager` would read only the first entry, and the UI and gameplay would disagree;
  - a UI action binds a mouse button, or a gamepad other than the one `GamePadTracker` reads: nothing would read that input.
- **Keys the mapping does not bind keep working.** Tab, Home, End, PageUp and PageDown fall back to `InputActionMapper`, Shift+Tab included.
- **Repeat is in the mapping, in milliseconds.** `.buttonsMapping` gains `repeat_initial_delay_ms` and `repeat_interval_ms`, 350 and 120 by default, written back only when set. A frame is not a unit of the engine: `IsFixedTimeStep` is false by default and nothing sets `TargetElapsedTime`. Directions move on the press, then repeat. Everything else fires on the edge only, so a held Submit does not run a command again. There is one timer, reset whenever the set of held inputs changes. A negative delay or an interval that is not positive is refused.
- **The `.uiscreen` envelope gains three optional fields.** A field left out keeps today's behaviour and is not written back.
  - `focus_wrap`, `vertical` or `horizontal`: applied once, through `NavigationNeighbors`, over `Desktop.GetFocusableElements`, at the first frame the focus is in the window.
  - `cancel_command` names an entry of `ui_commands`. It runs when Cancel reaches the top of the stack with nothing having consumed it, so an open combo box still closes first. It goes through the same path as a click: same stack guard, run in `Update`. It is refused at load when the project has no `UIInputMapping`, since nothing would report Cancel.
  - `move_command` names an entry of `ui_commands`. It runs when the focus moves between two elements of this window outside pointer mode, on either input path. Applying a default focus does not run it, and neither does a move made while a modal screen freezes the screen: `ScreenStack` keeps a frozen screen's view of the focus current, so its first `Update` after the modal closes sees nothing move. It is an ordinary command, so no sound field was added.
- **A text box keeps its keys.** While an editable text box has the focus, a key the existing mapper does not know stays text: a letter bound to Cancel does not cancel while the player types it. A key the focused text box reserves, through `ITextEntryHost.ShouldPreserveTextEntryKey`, stays its own too, editable or not: MGUI drops it before any control sees the action and answers false, as it does for a Cancel nothing consumed, so `UINavigationInput` asks the text box before it reports a Cancel. A space bound to Cancel is a space.
- **A key held when the UI is created is not an edge.** The first frame only reads the state, so the Enter that changed the world does not submit on the next one.

## Consequences

- A project without a DLL gets configurable confirm and cancel, repeat, wrap and a sound on focus moves. MGUI does not change.
- The format is additive: existing projects, mappings and envelopes load and save unchanged, and their input path is untouched.
- This path is opt-in, so the default path keeps its behaviour, including Submit repeating on a held A.
- Known limits, accepted:
  - A menu that is an `MGListBox` does not wrap: the control clamps its own index.
  - `focus_wrap` joins the first and the last element. It covers one row or one column, not a grid.
  - The list is taken once. An element added, shown or enabled later is not in it, and an end disabled later stops wrapping there.
  - Without `default_focus`, the first key gives the focus, so that key does not wrap yet.
  - Ctrl+Tab does not exist on this path: `InputActionContext` carries no modifier. A focused control that reserves Tab keeps it.
  - On this path a key is not marked as handled before the windows see it, so a focused control that reads the key itself sees it as well.
  - Gamepad buttons the mapping does not name, X and the shoulders for instance, do nothing here: only the five keys above fall back.
  - The Apps key does not fall back either.
  - The threshold of a stick direction read through `GamePadState.IsButtonDown` was not read in the MonoGame source, and may differ from MGUI's `ThumbstickThreshold`.
