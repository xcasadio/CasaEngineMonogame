# Copilot Instructions — CasaEngineMonogame

This repository is a C# / MonoGame game engine with both runtime and editor code.

The agent must act as a careful engine programmer:
- preserve existing architecture unless explicitly asked to change it;
- prefer small, safe, testable changes;
- avoid unnecessary abstractions;
- never refactor unrelated code;
- keep runtime performance in mind, especially in `Update`, `Draw`, layout, input, rendering and asset loading paths.

## Pilotfish / Agent Orchestration

For this repository, do not delegate by default.

Delegate only when at least one of these conditions applies:
- repository-wide exploration is required;
- more than five files require repetitive mechanical changes;
- the task can be specified without architectural ambiguity;
- an independent verifier pass provides meaningful value.

Keep architecture decisions, reverse-engineering conclusions, rendering decisions,
engine/editor architecture, and small localized changes in the main session.

For CasaEngine and MGUI:
- use scout only for broad code discovery;
- use mech-executor only for fully specified mechanical edits;
- use executor only for implementation tasks with clear scope and done criteria;
- use verifier for non-trivial completed changes before reporting them done.
---

# Shell tools available

The repository is developed on Windows.

Available tools:
- `rg` / ripgrep is installed and should be preferred for fast code search.
- `rtk` is installed and should be used when command output may be large or noisy.
- `fd` is installed and must be preferred for file discovery.
- `jq` is installed and must be used for JSON inspection.
- `yq` is installed and must be used for YAML/XML/INI/CSV inspection.
- `ast-grep` is installed and should be used for structural code search when plain text search would be too noisy.

## Shell usage rules

Prefer deterministic shell tools over guessing.

Use:
- `rg "pattern" .` for precise code search.
- `rtk rg "pattern" .` when the search may return a lot of output.
- `fd` for file discovery.
- `jq` for JSON files.
- `yq` for YAML/XML/INI/CSV files.
- `ast-grep` for structural C# code search when text search is too noisy.

Prefer:
- `rtk git status` instead of `git status`.
- `rtk git diff` instead of `git diff`.
- `rtk git log -n 20` instead of `git log -n 20`.
- `rtk test <command>` for verbose test commands.
- `rtk dotnet test` or `rtk test "dotnet test"` for .NET test output if supported.

If RTK is not available, fall back to normal commands.

At the start of a task, the agent may verify tools with:
- `rg --version`
- `rtk --version`
- `rtk gain`
- `fd --version`
- `jq --version`
- `yq --version`
- `ast-grep --version`

Never run broad recursive listing commands like:
- `dir /s`
- `tree /f`
- unfiltered `Get-ChildItem -Recurse`

Prefer filtered commands:
- `fd "Name" . -e cs`
- `fd . CasaEngine -e cs -d 4`
- `rg "pattern" CasaEngine --glob "*.cs"`
- `rg "pattern" . --glob "!bin/**" --glob "!obj/**"`

---

# General task workflow

Before coding:
1. Inspect the relevant files.
2. Search for existing patterns before creating new ones.
3. Check whether MonoGame, .NET or existing CasaEngine code already provides the needed feature.
4. Identify the minimal set of files required for the requested change.
5. Avoid broad refactors unless explicitly requested.

During coding:
1. Keep changes scoped to the task.
2. Preserve existing public APIs when possible.
3. Do not rename public types, members or serialized fields unless explicitly requested.
4. Do not silently change architecture.
5. Do not introduce new dependencies without strong justification.
6. Prefer clear code over clever code.

After coding:
1. Build if possible.
2. Run relevant tests if they exist.
3. Report changed files.
4. Report tests/build commands run.
5. Report risks, assumptions and follow-up tasks.

---

# Priorities

When making tradeoffs, use this order:

1. Correctness  
2. API stability  
3. Runtime performance  
4. Editor usability  
5. Readability  
6. Samples / demos  
7. Internal cleanup  

Do not sacrifice correctness or API stability for style cleanup.

---

# Scope control

Do only the requested task.

Allowed:
- small local cleanup required by the task;
- adding missing validation directly related to the task;
- adding tests or samples directly related to the task.

Not allowed unless explicitly requested:
- large refactors;
- changing unrelated systems;
- changing project structure;
- changing serialization format;
- replacing existing architecture;
- renaming public APIs;
- rewriting working systems from scratch.

If an improvement is useful but outside the task, document it instead of implementing it.

---

# CasaEngine architecture rules

CasaEngine is a game engine, not a generic application framework.

Prefer engine-style architecture:
- clear runtime/editor separation;
- deterministic update order;
- explicit ownership;
- low allocations in hot paths;
- stable asset serialization;
- explicit rendering states;
- predictable input and focus behavior.

Do not introduce service abstractions everywhere by default.

Use interfaces/adapters when there is a real backend boundary, for example:
- rendering backend;
- physics backend;
- audio backend;
- asset importer/exporter;
- editor/runtime boundary.

Avoid unnecessary abstractions for simple engine systems.

---

# Editor vs runtime separation

Preserve separation between editor-only and runtime code.

Rules:
- Runtime must not depend on editor UI.
- Editor features must not leak into game runtime.
- Save/export tools belong to editor/tooling code.
- Load/runtime execution belongs to runtime code.
- Avoid `#if EDITOR` unless the existing project pattern already uses it and it is the least invasive solution.
- Prefer clear project or namespace boundaries when available.

If a task touches both editor and runtime, explain which files belong to each side.

---

# Public API and compatibility

CasaEngine should remain compatible with existing code.

Rules:
- Do not break public APIs unless explicitly requested.
- Prefer additive changes.
- If a breaking change is unavoidable, clearly report it.
- Keep serialized asset fields stable.
- Do not rename serialized properties without migration support.
- Do not change asset formats silently.

For public APIs:
- add short XML documentation when useful;
- add a small usage snippet in docs or samples when the feature is significant.

---

# Performance rules

These rules are strict for hot paths such as:
- `Update`
- `Draw`
- layout calculation
- hit testing
- input processing
- rendering passes
- physics stepping
- animation update
- particle update
- asset streaming

## Allocation rules

Avoid in hot paths:
- LINQ;
- closures;
- lambdas capturing variables;
- `foreach` on types that may allocate;
- string interpolation / formatting;
- creating temporary `List<T>`, `Dictionary<TKey,TValue>`, arrays or delegates;
- boxing;
- reflection;
- per-frame event subscription/unsubscription.

Prefer:
- cached lists with `Clear()`;
- reusable buffers;
- object pools when justified;
- explicit `for` loops;
- precomputed strings;
- precomputed layout data;
- cached delegates if needed.

## Rendering performance

Rules:
- Minimize `SpriteBatch.Begin` / `End`.
- Avoid redundant state changes.
- Avoid texture switches where possible.
- Batch draw calls where possible.
- Do not allocate during rendering.
- Restore `GraphicsDevice` states after temporary changes.

---

# MGUI / UI rules

For MGUI controls and editor UI:

## Layout

Any property affecting size or position must invalidate layout properly.

Examples:
- width / height;
- margin / padding;
- visibility;
- font;
- text;
- children collection;
- docking / alignment;
- min/max size;
- content.

Layout must be deterministic.

Avoid:
- rebuilding the whole visual tree every frame;
- recalculating unchanged layout every frame;
- hidden side effects in `Draw`.

## Input

Input must be deterministic.

Rules:
- hit-test must respect z-order;
- hit-test must respect visibility;
- hit-test must respect enabled state;
- hit-test must respect clipping;
- mouse capture must be used for drag operations;
- keyboard focus must be unique;
- tab navigation should be supported when applicable;
- controls should not steal focus unexpectedly.

## Clipping

Rules:
- use Push/Pop semantics;
- always restore previous clipping state;
- always restore `GraphicsDevice` state;
- prefer scissor clipping for rectangular clips;
- use stencil/mask only when needed for complex or rounded clipping.

## Real-time editor constraints

Controls may be refreshed every frame.

Therefore:
- avoid allocations in layout/input/draw;
- avoid LINQ;
- cache measurements;
- do not create new commands/events every frame;
- do not recreate child controls every frame unless explicitly required.

---

# Rendering / shaders

Rendering code must separate:

1. Data  
   Examples: materials, meshes, textures, lights, cameras.

2. Pipeline  
   Examples: forward pass, shadow pass, GBuffer pass, lighting pass.

3. Backend  
   Examples: MonoGame `GraphicsDevice`, `Effect`, `RenderTarget2D`.

Rules:
- avoid state leaks;
- restore `RasterizerState`, `BlendState`, `DepthStencilState`, render targets and viewports after temporary changes;
- prefer explicit render passes;
- handle missing shaders or unsupported features gracefully;
- provide fallback paths when reasonable;
- do not hard-code material parameters if they should be asset/editor data.

For future rendering features, prefer extensible structures:
- `ForwardPass`
- `ShadowPass`
- `GBufferPass`
- `LightingPass`
- material parameter blocks
- light layers
- debug visualization

Do not implement advanced rendering architecture unless the task explicitly asks for it.

---

# Physics

Physics must have clear ownership and synchronization.

Rules:
- clarify whether transform is driven by physics or by gameplay;
- avoid two-way transform synchronization without a clear rule;
- keep physics backend behind stable interfaces when possible;
- do not leak backend-specific types into high-level gameplay APIs unless already established;
- add debug draw when implementing new collision/physics features;
- keep fixed-step behavior deterministic.

When modifying physics:
- inspect the existing backend first;
- preserve current behavior unless explicitly changing it;
- document any migration risk.

---

# Assets and serialization

Asset formats must remain stable.

Rules:
- do not change serialized field names silently;
- do not change asset structure without migration notes;
- prefer existing CasaEngine asset patterns;
- avoid generic JSON models if the engine already has an asset system;
- validate asset data on load;
- report missing or invalid fields clearly;
- keep runtime loading independent from editor-only data.

For importers/exporters:
- use deterministic parsing;
- add sample input/output when possible;
- keep generated data stable between runs;
- avoid nondeterministic ordering.

---

# Tests and validation

Add or update tests when:
- an existing test project exists for the touched system;
- the task changes logic;
- the task fixes a bug;
- the task changes serialization;
- the task adds an importer/exporter;
- the task changes layout, input or rendering behavior in a testable way.

Preferred validation:
- unit tests for pure logic;
- golden/sample files for import/export;
- small demos/screens for editor features;
- build verification for project-wide changes.

If tests cannot be added, explain why.

Do not claim something works unless it was built, tested, or reasoned from inspected code.

---

# Documentation

Update documentation when:
- a public API is added;
- a feature is added;
- editor behavior changes;
- asset format changes;
- a new workflow is introduced.

Documentation should be short and useful.

Prefer:
- feature summary;
- usage snippet;
- limitations;
- known risks;
- next steps.

---

# Git rules

Use RTK-wrapped Git commands when possible:
- `rtk git status`
- `rtk git diff`
- `rtk git log -n 20`

Commit rules:
- make one commit per coherent sub-task when commits are requested;
- keep each commit buildable;
- use explicit commit messages;
- never push unless explicitly requested;
- do not commit unrelated changes;
- inspect `rtk git diff` before committing.

If the working tree already has user changes:
- do not overwrite them;
- do not revert them unless explicitly requested;
- report them if they affect the task.

---

# Code style

Follow existing style in the touched files.

General C# rules:
- use clear names;
- prefer explicit types when it improves readability;
- avoid clever one-liners;
- keep methods focused;
- avoid unnecessary regions;
- avoid unnecessary comments;
- comment only non-obvious logic.

For engine code:
- prefer predictable control flow;
- avoid hidden allocations;
- avoid reflection in runtime paths;
- avoid global mutable state unless the existing architecture uses it;
- keep update order explicit.

---

# Error handling

Use clear error handling.

Rules:
- fail early for invalid developer usage;
- report asset/data errors with useful context;
- avoid swallowing exceptions silently;
- avoid throwing every frame in runtime paths;
- avoid logging spam in hot paths;
- prefer validation before runtime execution.

---

# Reverse engineering / porting rules

When working on code derived from reverse engineering, decompilation, binary formats or legacy game behavior:

Rules:
- preserve original logic unless explicitly asked to improve it;
- preserve execution order;
- do not optimize unless explicitly requested;
- do not rename unknown fields unless the meaning is justified;
- separate facts from assumptions;
- keep offset/address comments when available;
- prefer deterministic parsers and tests over manual interpretation.

If translating C/C++/MIPS-style code to C#:
- preserve side effects;
- preserve integer sizes and signedness carefully;
- preserve overflow behavior when relevant;
- avoid replacing pointer logic with higher-level behavior unless equivalent;
- document uncertain fields.

---

# Completion report

At the end of a coding task, report:

```text
Changed files:
- ...

Validation:
- ...

Assumptions:
- ...

Risks:
- ...

Next useful step:
- ...