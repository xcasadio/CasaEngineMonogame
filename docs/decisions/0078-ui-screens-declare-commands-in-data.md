# ADR-0078: UI screens declare their commands in data

- **Status**: Accepted
- **Date**: 2026-10-31
- **Source**: this chantier: `ai-agent/tasks/data-driven-screen-flow-tasks.md` (decisions D1 to D3, P1 to P4, open points O1 to O7 settled by the author on 2026-10-31), branch `chantier/data-driven-screen-flow`. Amends ADR-0035 and ADR-0038.

## Context

- `XamlUIScreenBase` is abstract (`CasaEngine/Framework/UI/XamlUIScreenBase.cs:23`) and every subclass is C# code, so opening a screen needs code in a gameplay DLL or in a host.
- `ProjectSettings.FirstScreenName` is read and written (`ProjectSettingsHelper.cs:23,123`) but nothing consumes it.
- A project with an empty `GameplayDllName` runs: the load is skipped (`ProjectSettingsHelper.cs:57-59`) and a world without script is accepted (`World.cs:357-360`). `SampleProject` was run that way before this chantier: it shows its world and logs no exception.
- `MGButton.CommandName` looks its command up in the resources of its scope, and a window owns a resource scope (`MGButton.cs:299`, `MGWindow.cs:1640`). A left click and Submit run the same path (`MGButton.cs:239-248,274-280`). `MGResources.AddCommand` is called by `MGUI.Samples`, by no engine code.
- ADR-0035 gives event subscriptions to the screen's code. ADR-0038 has the screen's code set a view model. Neither leaves a place for a screen with no code.
- ADR-0075 forbids naming engine types in data: they are created by code.

## Decision

- **The `.uiscreen` envelope gains three optional fields**: `is_modal` (boolean), `default_focus` (the name of an element of the XAML) and `ui_commands`. A field left out keeps today's behaviour and is not written back. The data replaces the policy a DLL would have held, for a project that has no DLL.
- **`ui_commands` is a list of commands, each with a `name` and an ordered list of `actions`.** A button points at a command with its existing XAML property `CommandName`, so the XAML format does not change.

  ```json
  "ui_commands": [
    {
      "name": "Play",
      "actions": [
        { "type": "play_sound", "target": "Confirm" },
        { "type": "change_world", "target": "Level1.world" }
      ]
    }
  ]
  ```

- **The actions are a closed list**: `open_screen` (target: a screen asset name or id), `close_screen` (the screen that runs the action, no target), `change_world` (target: a catalogued world file, as in `FirstWorldLoaded`), `play_sound` (target: a sound asset name or id), `quit`. A list has neither conditions nor waits. An action type the engine does not know is kept as `Unknown` with its JSON, so saving an envelope does not lose it, and it fails when the screen loads.
- **`DataDrivenScreen` is a sealed engine class, created by the engine.** No class name appears in the data (ADR-0075). It is the only class that reads these fields, so an existing screen subclass keeps its behaviour.
- **Invalid data throws at load, with context**: the envelope path, the command and the value. This covers an unknown action, a missing or unknown target, a duplicate or empty command name, a `default_focus` that names nothing, and a button whose `CommandName` resolves to no command. A button with no `CommandName` is legal. `AssetLoader` swallows exceptions from `Load` (`AssetLoader.cs:18-23`), so the checks run after the asset is acquired, not in `UIScreenAsset.Load`.
- **A command does not run inside MGUI's input handling.** A click or a Submit is remembered and `DataDrivenScreen.Update` runs it, because an action can change the screen stack. A click on a screen that is not at the top of its stack is ignored: a screen frozen under a modal one receives no `Update`, so a remembered click would otherwise run when the modal one closes.
- **`Hide` disposes a `DataDrivenScreen`.** No owner holds these screens, so a pop and the end of a world (`ViewManager.Clear`, then `UIRoot.Dispose`, then `ScreenStack.Clear`) must both give back the envelope and the bindings. A screen is not pushed twice: `open_screen` builds a new one.
- **`default_focus` is applied once, in `Update`**, at the first frame where the target's `IKeyboardHandlerHost.CanReceiveKeyboardInput()` is true. Asking earlier loses the request (`XamlCommandFocusTests` pins this). MGUI's auto-focus is not enough: it picks the first navigable control, and a hovered control stops it, in which case Enter does nothing because Submit has no fallback.
- **`FirstScreenName` opens a `DataDrivenScreen` once, after the first `WorldLoaded`**, on the active view. An empty name does nothing. Under `UseExternalViewManagement` (edit mode and Play-in-Editor) it does nothing either, with no error: the editor owns its views. In the runtime, a name with no active view throws.

## Consequences

- A project without a DLL can open a screen, wire buttons to actions, play a sound, change world and quit.
- **This differs from ADR-0042.** The Yarn runner exposes a registry that code fills, because its consumer has code. A registry would serve nobody here, since the target project has none: that is why the actions are a closed list, and a new action is an engine change with its own decision. A project with a DLL keeps every existing path, including its own `XamlUIScreenBase` subclass.
- The format is additive: existing envelopes load and save unchanged.
- A command stays within one window's scope, so two screens may use the same command name.
- Known limits, accepted:
  - The fade of a menu that ends on a wait is not expressible, since an action list cannot wait. `change_world` is the last step here, and a wait action belongs to a later change.
  - A world does not open a screen when it loads. `FirstScreenName` runs once, and a field on `.world` can be added later.
  - The editor does not open the first screen, and has no panel for the actions: they are edited by hand in the JSON.
  - A click on a non-modal screen that sits under another screen is ignored as well, the rule being the top of the stack.
  - `default_focus` is not restored when a screen on top closes.
