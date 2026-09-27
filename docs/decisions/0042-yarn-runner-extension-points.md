# ADR-0042: Yarn dialogue runner extension points

- **Status**: Accepted
- **Date**: 2026-09-27
- **Source**: this chantier: `ai-agent/tasks/yarn-extension-points-tasks.md` (decisions D1-D4 and the
  technical choices approved with the plan), branch `chantier/yarn-extension-points`. Consumer: the
  Alundra port (E15, `docs/plan-e15-yarn.md`, tranche E15.a).

## Context

- `YarnDialogueRunner` (`CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs`) only exposed a
  constructor taking an `IDialoguePresenter`. Its variable storage was a hard-coded
  `Yarn.MemoryVariableStore` built fresh on every `Start`; `OnOptions` and `OnCommand` were empty, so
  a `<<command>>` line blocked the dialogue forever (Yarn Spinner waits for `Continue()` after every
  command); no function could be registered on the dialogue's `Library`; no markup was parsed, so a
  line's substitutions were applied with `string.Replace` and any `[...]` markup or `Name:` speaker
  prefix reached `DialogueScreen` unprocessed.
- A game that uses Yarn Spinner (the Unity integration is the reference: `DialogueRunner.AddCommandHandler`,
  a replaceable variable storage) needs these as generic points of extension, not as Alundra-specific
  code inside the engine.
- Yarn Spinner 3.2.1 facts relied on for this decision (checked against the shipped `YarnSpinner.dll` and
  its XML docs): `Dialogue(IVariableStorage)` assigns the storage's `Program` and `SmartVariableEvaluator`;
  `Dialogue.SetProgram` re-assigns `VariableStorage.Program`; an unhandled `Command` is silently dropped
  by the virtual machine and the dialogue still expects a `Continue()` afterwards; `Library.RegisterFunction(string, Delegate)`
  throws `ArgumentException` when the name is already registered; `Yarn.Markup.LineParser` and
  `Yarn.Markup.BuiltInMarkupReplacer` are public, `LineParser.RegisterMarkerProcessor(string, IAttributeMarkerProcessor)`
  throws `InvalidOperationException` on a duplicate registration, and `LineParser.ParseString(string input,
  string localeCode, bool addImplicitCharacterAttribute = true)` detects a `Name: text` prefix and adds a
  `character` attribute (name `LineParser.CharacterAttribute`) whose `name` property
  (`LineParser.CharacterAttributeNameProperty`) carries the speaker, removable from the text with
  `MarkupParseResult.DeleteRange`.

## Decision

- `YarnDialogueRunner` gains only additive public API: the existing constructor and methods keep their
  current behaviour.
- **Injectable variable storage**: a `VariableStorage` property (`Yarn.IVariableStorage?`); `null` keeps
  today's behaviour (a fresh `MemoryVariableStore` per `Start`). The storage is bound to a new
  `Yarn.Dialogue` only once the requested start node is confirmed to exist on the freshly parsed
  `Yarn.Program` (`Dialogue.NodeExists`) - a refused `Start` touches neither the dialogue already
  running nor the shared storage.
- **Command registry with fallback**: `AddCommandHandler(string, Action<IReadOnlyList<string>>)`,
  `RemoveCommandHandler(string)`, and an `UnhandledCommand` event. Command names compare ordinally,
  case-sensitively; an empty command text is treated as unknown. An unknown command no longer blocks the
  dialogue (a deliberate behaviour change: it used to hang waiting for `Continue()`) - it is logged once per
  name, raises `UnhandledCommand`, and the dialogue resumes. The runner only calls `Continue()` after a
  command when the `Yarn.Dialogue` that raised it is still the current, active dialogue, so a handler that
  calls `Stop()` or `Start(other)` is not overridden by an auto-resume. A handler exception is never
  swallowed: the runner stops the dialogue (closing the presenter) first, then lets the exception
  propagate.
- **Function registration**: `RegisterFunction(string, Delegate)` is kept by the runner and applied to the
  `Library` of every `Yarn.Dialogue` it creates afterwards.
- **Markup parsing**: one `Yarn.Markup.LineParser` per runner, created once, with `select`, `plural` and
  `ordinal` registered through `RegisterMarkerProcessor` using Yarn Spinner's own `BuiltInMarkupReplacer`.
  `YarnDialogueRunner.LocaleCode` (default `"en"`) is passed to `ParseString`. This is a visible behaviour
  change, accepted: a `Name: text` line now shows `text` with `Name` as `Speaker`, and any `[...]` in a
  line is read as Yarn markup and stripped from the text, so a game that wants literal brackets escapes
  them per Yarn's own rules (`\[`, `\]`). A line whose markup fails to parse is delivered raw, with a
  logged warning, never an exception. `DialogueLine` gains an additive constructor overload and an
  `Attributes` property (empty by default) to carry the parsed attributes to the presenter.
- **Reading a line outside a dialogue**: `DialogueAsset.TryGetLineText(string, out string)` plus a static
  analyzer that applies the same substitution and markup handling as the runner, for lines a game shows
  outside of a dialogue box (menus, inventory).
- Out of scope for this decision: Yarn options (`->`) and their routing, asynchronous commands, the
  `.yarn` editor importer, multi-locale localization, and Yarn Spinner 3.2.1's French pluralization
  default (documented, not fixed here).

## Consequences

- A game (Alundra first) can back Yarn variables with its own state, dispatch `<<command>>` lines to
  gameplay code without writing engine code, register custom `{function(...)}` calls, and receive
  structured markup attributes instead of raw bracketed text.
- Two behaviour changes ship with this decision, both assumed: an unhandled command used to block the
  dialogue and now resumes it; a line's `[...]` markup used to reach `DialogueScreen` unprocessed (where
  MGUI's own inline formatting could reinterpret it) and is now parsed and stripped by the runner before
  the presenter ever sees it.
- Deferred (P3): a function name that collides with a Yarn Spinner built-in function only fails at
  `Start`, not at `RegisterFunction` time.
- No dedicated demo: the extension points have no screen of their own; the proof is the test suite plus
  their real usage by the Alundra port.
