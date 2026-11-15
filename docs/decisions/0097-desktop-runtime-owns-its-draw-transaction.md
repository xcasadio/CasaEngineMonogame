# ADR-0097: The desktop runtime owns its draw transaction

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/mgui-desktop-frame-nonalloc-tasks.md`, suite O7, decision D8, requested by the author on 2026-11-15 ("a modern design with performance; if the API has to be broken, break it, and do not keep a feature that is obsolete")

## Context

`MGDesktop.Draw` asks its runtime for a draw transaction at every frame (`IUIDesktopRuntime.BeginDrawTransaction`). `CasaDesktopRuntime` answered with `new CasaDrawTransaction(...)`: the transaction object and what it owns (draw state, clip manager, render-target service, shape renderer, primitive drawing) were created and dropped at every frame, 2152 bytes per frame measured on a real GPU with the rest of the frame at zero. The transaction can be put back in the state of a new one (`CasaDrawTransaction.Rearm`, T3.3 of the plan), so the allocation is not needed.

Read in the code: each `UIRoot` and the editor build their own runtime; the draw callers in product code (`UIRoot.Draw`, `WorldUIComponent.DrawToTexture`, `DefaultUICompositionService.Compose`, `CasaEngineGame.Draw` for the window UI, `GameEditor.Draw`, the editor's PNG export and remote capture) draw one desktop at a time and none draws a desktop of a runtime while another draw of the same runtime is open. `CasaDesktopRuntime.DrawViews`, `UpdateViews`, `UnregisterView` and `Views` had no caller and no reader in `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.EditorServices`, `CasaEngine.Demos` or the tests; `IUIDesktopRuntime` only requires `BeginDrawTransaction` and `RegisterView`.

## Decision

- `CasaDesktopRuntime` owns one `CasaDrawTransaction` for its whole life. `BeginDrawTransaction` creates it on the first call and puts the same object back in the state of a new transaction on every later call; the `Dispose` of the transaction ends its batch and closes it.
- There is no fallback. A request while the transaction is open throws `InvalidOperationException` (a programming error, an immediate failure) whose message says to dispose the transaction first; the runtime never creates a second object in silence. This changes the behaviour of `BeginDrawTransaction`, which used to return a new, independent transaction at each call.
- The contract of the transaction: one per runtime, one open at a time, and not to be used after its `Dispose`: the next request hands the same object to the next draw.
- The shape renderer returned by `CasaMonoGameBackendOptions.CreateShapeRenderer` is created once per runtime (with the transaction), no longer once per frame. A renderer that keeps state per transaction must be usable across frames.
- Failures leave the transaction usable: the open state is cleared in a `finally` of `Dispose`, whether the end of the batch succeeds or throws; a re-arm or a begin of the batch that throws inside `BeginDrawTransaction` leaves the transaction closed. After a draw that throws, the next frame draws normally, as it did when each frame had a new transaction.
- Removed from `CasaDesktopRuntime`, public members without caller: `DrawViews`, `UpdateViews`, `UnregisterView`, `Views`. `RegisterView` stays because `IUIDesktopRuntime` requires it, and only checks its argument: the runtime no longer keeps a list of views (the list had no reader and kept every registered view alive).
- The constructor of `CasaDrawTransaction` is `internal`: the runtime creates its transaction, and no project of the repository called the constructor outside `CasaEngine` and `CasaEngine.Tests` (reached through `InternalsVisibleTo`).

## Consequences

- Public API breaks, accepted by the author: the four removed members, the constructor visibility, and `BeginDrawTransaction` throwing while a transaction is open. Code that held two transactions of one runtime at once must close the first before asking for the second. The existing tests that did so were adapted (`DesktopFrameGpuAllocationTests.WhatTheFrameStillAllocates` closes its kept transaction before it draws a desktop).
- A real GPU frame (`MGDesktop.Update()` then `MGDesktop.Draw(1.0f, settings)`) allocates 0 bytes at rest and during a drag of the slider, exact total over 500 frames, with the pixels of the code that created a transaction per frame.
- `MGDesktop.Draw()` with no settings still allocates 192 bytes per frame: MGUI's `DrawSettings.Default` returns a new object at each read. It is in MGUI (a submodule) and is outside this decision; a caller that passes its own `DrawSettings` avoids it.
- The scope of a mask clip left open keeps its rented render target and a `ClipScope` not disposed by a failed draw is abandoned (T3.3): a draw that throws in the middle of a clip leaves those, as it did before.
- A shape renderer of a project outside the repository, given through `CreateShapeRenderer`, is now kept across frames: it was not checked (the two renderers of the repository keep no state per transaction).
- The method was renamed from `CreateDrawTransaction` to `BeginDrawTransaction` in MGUI (MGUI ADR-0028) and in the engine, because it opens a transaction that `Dispose` closes and a runtime may hand back the same object at each call. Public API break without alias, requested by the author on 2026-11-15.
- Tests: `DesktopFrameGpuAllocationTests` (real path at zero, same pixels, second request throws, same object after `Dispose`, viewport change, draw that throws, `Dispose` that throws, begin that throws) and `DrawTransactionRearmGpuTests` (the re-arm).
