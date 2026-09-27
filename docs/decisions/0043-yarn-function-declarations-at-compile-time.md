# ADR-0043: Yarn scripts compile with function declarations supplied by the game

- **Status**: Accepted
- **Date**: 2026-09-27
- **Source**: this chantier: `ai-agent/tasks/yarn-extension-points-tasks.md`, task T1.3 (revised after
  open point O4), branch `chantier/yarn-extension-points`. Complements ADR-0042.

## Context

- ADR-0042 gave `YarnDialogueRunner` a `RegisterFunction(string, Delegate)` applied to the `Library` of
  every `Yarn.Dialogue` it creates, so a game's function is callable at run time. `YarnDialogueCompiler`
  (`CasaEngine.Compiler/Dialogue/YarnDialogueCompiler.cs`) compiled every script against a fresh, empty
  `Yarn.Library`, with no way to tell the compiler about a function the game will register later.
- Verified during T1.3 (O4): a function call inside a line's text (`{upper("hi")}`) fails to compile
  against an empty `Library`, with Yarn Spinner's own diagnostic ("Can't determine the type of the
  expression"); the same call inside an `<<if ...>>` condition compiles. So a game's registered
  function only works from a condition unless the compiler already knows its signature - this
  contradicted the plan's original assumption ("a function is not declared to the compiler still
  compiles"), which is why T1.3 was blocked.
- `Yarn.Compiler.CompilationJob.CreateFromString(string, string, Library, int)` and
  `CreateFromInputs(IEnumerable<CompilationJob.File>, Library, int)` already accept a `Yarn.Library`
  (checked against the shipped `YarnSpinner.Compiler.dll`, `YarnSpinner.Compiler` 3.2.1): a `Library`
  built for compilation only needs the function's name and delegate signature (types), not a working
  implementation, so the same declarations can be shared between compile time and the implementation
  registered on the runner at run time.

## Decision

- `YarnDialogueCompiler.CompileString` and `CompileFile` each gain an overload taking an optional
  `Yarn.Library` of function **declarations**, passed straight through to `CompilationJob`. Omitting it
  keeps today's behaviour (an empty `Library`, unchanged compiled output for scripts that call no such
  function).
- The engine ships no declarations of its own: the caller (the game, or its export tooling) builds this
  `Library` and is responsible for keeping it consistent with the implementations it later registers on
  `YarnDialogueRunner.RegisterFunction`.

## Consequences

- A game can call its registered functions from both line text (`{...}`) and conditions
  (`<<if ...>>`), as long as it declares the same name and delegate shape to the compiler.
- A function used in line text but never declared to the compiler still fails to compile, with Yarn
  Spinner's own diagnostic - unchanged, and now avoidable by declaring it.
- Deferred (O3, unchanged from ADR-0042): a function name colliding with a Yarn Spinner built-in only
  fails when the dialogue starts, not when it is declared or registered.
