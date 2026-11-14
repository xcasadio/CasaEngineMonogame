# ADR-0090: Demos carry a sheet and follow a learning path

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/archive/demos-onboarding-tasks.md` (decisions D2 to D5, D8, D9, points P1 to P3)

## Context

`CasaEngine.Demos` is the first thing a new user runs. On 2026-11-14 its code showed that:

- a demo exposed only `Title` and `Description` (`CasaEngine.Demos/Demo.cs`), and the main screen sheet showed
  nothing else (ADR-0072);
- during a demo the only shared text was "Esc / Select: back to the menu"; the keys of five demos were listed in the
  main screen description only, so they were invisible once the demo ran;
- no demo read the gamepad, although the main screen did;
- the demos were registered in historical order, the first one being a cutscene validation scene;
- several descriptions were written for the engine maintainer ("Validates ...", internal task identifiers).

## Decision

- **A demo carries a sheet.** `Demo` gains, additively, a list of controls (input, effect), a list of things to
  observe and the path of its documentation page. The defaults are empty, so an existing demo compiles unchanged.
- **The sheet shows in two places.** The main screen displays it for the selected demo. During a demo, one shared
  MGUI panel displays the title, the controls (back to the menu and default camera included) and the things to
  observe. The panel shows when the demo starts, F1 or the gamepad Start button collapses it, and it stays hidden
  while the back buffer is captured or probed.
- **The demos follow a learning path.** A "Getting started" theme comes first and holds minimal, commented demos;
  an "Engine validation" theme comes last and holds the scenes whose purpose is to check the engine. The
  registration order is the order of the main screen.
- **Demo indices are not stable.** `CASAENGINE_START_DEMO` keeps accepting an index, but documentation and scripts
  name a demo by its title. Titles are stable.
- **Demo actions go through the engine input configuration.** Each action has a keyboard key and a gamepad button,
  and the sheet lists both.
  Amended on 2026-11-14 (author's answer "Oui, option (a)", plan `demos-onboarding`, O4): an input mapping holds
  one key and one alternative, with no key combination, and three demos (Audio, Animation blend, Animation IK) have more actions than a gamepad has
  buttons. Every demo still reads its actions from the configuration (`Content/demos-input.buttonsMapping`), but in
  those demos only the main actions get a button; the others stay on the keyboard and their sheet shows a key alone.
- **A demo may write a file only beside the executable.** The save-game demo writes under
  `AppContext.BaseDirectory`, never in the per-user folder, shows the full path and offers an action that deletes
  the file.

## Consequences

- Adding a demo means filling its sheet; a demo with keys and an empty controls list is a defect.
- The text of a demo's controls lives in one place, read by the main screen and by the in-demo panel.
- Any documentation that started a demo by index had to be rewritten to use a title.
- The in-demo panel takes screen space: demos with their own HUD must not be covered by it.
- The chantier changes `CasaEngine.Demos`, its tests and the documentation only. Nothing in `CasaEngine/` depends
  on the sheet.
- Supersedes nothing: ADR-0072 (main screen, one world per demo) stays in force.
