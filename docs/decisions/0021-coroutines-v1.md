# ADR-0021: Coroutines V1

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/coroutines_specifications.md:1250-1347`

## Context

The document records the validated V1 decisions for the coroutine system, following a list of stated goals (easy debugging, clean integration with object lifetimes, no direct serialization of `IEnumerator`, room to extend towards editable sequences) at `docs/engine/coroutines_specifications.md:1240-1243`.

## Decision

- The coroutine system V1 is attached to the `World`; each `World` owns its own `CoroutineManager`; gameplay coroutines are destroyed with their `World`; global/UI/editor coroutines are not included in V1 but may use a separate manager later (source: `docs/engine/coroutines_specifications.md:1248-1256`).
- The engine passes a `FrameTime` (containing `DeltaTime`, `UnscaledDeltaTime`, `TotalTime`, `UnscaledTotalTime`, `TimeScale`, `FrameIndex`) instead of a plain `float elapsedTime`; `WaitForSeconds` uses `DeltaTime`; `WaitForSecondsRealtime` uses `UnscaledDeltaTime` (source: `docs/engine/coroutines_specifications.md:1258-1272`).
- Pause is represented by `TimeScale = 0`: `WaitForSeconds` does not progress, `WaitForSecondsRealtime` keeps progressing, `WaitForFrames` keeps progressing as long as `World.Update` keeps being called (source: `docs/engine/coroutines_specifications.md:1274-1283`).
- A coroutine may have an owner (`Entity`, `Component`, or a system object); an entity's coroutines stop on `Entity.Destroy()` and, as a safety net, on effective removal from the `World`; a component's coroutines stop on `Detach()`; a plain `Enabled = false` does not stop coroutines automatically in V1 (source: `docs/engine/coroutines_specifications.md:1285-1298`).
- `CoroutineHandle` carries `ManagerId`, `Slot`, `Generation`; a stale handle must never point to a new coroutine (source: `docs/engine/coroutines_specifications.md:1300-1305`).
- `yield return CoroutineHandle` waits for the targeted coroutine; only handles from the same `CoroutineManager` are supported in V1; a finished or stopped handle resumes immediately; an invalid handle produces a warning or an exception depending on debug mode; a handle pointing to the current coroutine is an error (source: `docs/engine/coroutines_specifications.md:1307-1317`).
- By default, an exception in a coroutine is logged, stops only the faulty coroutine, and does not stop the whole scheduler; in strict debug mode the scheduler may rethrow after marking the coroutine faulty (source: `docs/engine/coroutines_specifications.md:1319-1326`).
- V1 implements only the `Update` phase; `LateUpdate`, `FixedUpdate`, and `EndOfFrame` are reserved for V2 (source: `docs/engine/coroutines_specifications.md:1328-1332`).
- V1 exposes a debug API, not necessarily a full editor window (source: `docs/engine/coroutines_specifications.md:1334-1335`).

## Consequences

- Global, UI, and editor coroutines are deferred past V1 and will need a separate manager design.
- `LateUpdate`, `FixedUpdate`, and `EndOfFrame` coroutine phases are deferred to V2; code relying on them cannot be V1-compliant.
- Implementation status observed in code: `CoroutineManager` (`CasaEngine/Framework/Scripting/Coroutines/CoroutineManager.cs`), `ICoroutineManager` (`CasaEngine/Framework/Scripting/Coroutines/ICoroutineManager.cs`), `CoroutineHandle` (`CasaEngine/Framework/Scripting/Coroutines/CoroutineHandle.cs`), and `FrameTime` (`CasaEngine/Core/Time/FrameTime.cs`) all exist, matching the decision.
