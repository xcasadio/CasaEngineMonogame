## Context

The repository contains code paths for both:
- **Editor mode**
- **In-game / runtime mode**

The current implementation uses editor-specific compilation directives such as `#if EDITOR` in some places.

The goal of this audit is **not** to refactor immediately.  
The goal is to **inspect the codebase and produce a concrete follow-up action file** that will later be used by another AI agent to perform the refactor safely.

This audit must determine whether the current architecture has a clean separation between:
- runtime/game responsibilities
- editor/tooling responsibilities

It must also identify where the separation is blurred:
- editor code leaking into runtime
- runtime types depending on editor-only concepts
- asset loading/saving responsibilities mixed together
- build/configuration logic relying too much on preprocessor directives
- code that should be moved behind interfaces, services, modules, or separate projects

---

## Main objective

Produce a new markdown file in the repository with:
- a clear inventory of findings
- a classification of issues by severity
- a proposed action list that can be executed later by another AI agent
- enough precision so the next agent can implement the refactor in small steps

The output file should become the basis for a future implementation plan.

The report must be detailed enough that a future implementation agent can:
- locate the affected code without re-discovering it from scratch
- execute the refactor in small verified steps
- distinguish temporary acceptable boundaries from real architectural blockers

---

## Required final deliverable

Create a markdown file named:

`docs/audits/editor-runtime-separation-audit-report.md`

This file must contain:
1. Executive summary
2. Current architecture overview
3. Inventory of editor/runtime coupling points
4. Inventory of `#if EDITOR` usages
5. Findings grouped by category
6. Severity per finding
7. Recommended target architecture
8. Refactor action list for a future agent
9. Suggested commit strategy for the future refactor agent
10. Risks and validation points

The report should also include, where evidence exists:
- exact project-to-project dependency direction
- exact startup/composition root locations
- exact type and member names for each finding
- line references when practical and cheap to obtain

---

## Constraints

- Do **not** perform large refactors during this audit task.
- Do **not** change behavior unless strictly necessary to complete the audit.
- Small non-functional fixes are allowed only if they are required to improve the audit output.
- Prefer evidence-based findings with exact file paths, class names, and short explanations.
- Each finding must distinguish:
  - what exists now
  - why it is a separation problem
  - what kind of refactor would solve it
- Be pragmatic: identify real architectural problems, not theoretical perfectionism.

---

## Working rules

- You must inspect the actual repository structure and code.
- You must search for editor-specific directives, namespaces, references, services, and responsibilities.
- You must identify both explicit coupling and hidden coupling.
- You must produce a report that is directly usable by another implementation agent.
- You must not invent files/classes that do not exist.
- You must cite exact code locations in the report.
- You must prefer evidence from project files, entry points, and concrete call sites over folder-name assumptions.
- You must distinguish between:
  - acceptable boundary use
  - localized mixed responsibility
  - architectural coupling that blocks clean runtime/editor isolation

---

## Definition of "good separation"

Use the following principles during the audit:

### Runtime / in-game side
Should contain only what is needed to:
- load runtime assets
- run the world/game
- render the game
- update gameplay systems
- run runtime UI
- execute runtime serialization/loading when needed

### Editor side
Should contain:
- asset authoring
- asset saving/writing
- import/reimport pipeline
- inspectors
- gizmos
- viewport tools
- editor-only panels and services
- editor metadata
- undo/redo
- editing commands
- project/asset database authoring tools

### Architectural expectation
Prefer:
- separate projects / assemblies
- interfaces
- service abstraction
- null object services where appropriate
- explicit editor-only implementations

Avoid:
- editor logic embedded in runtime classes
- runtime logic controlled everywhere with `#if EDITOR`
- shared classes with mixed load/save/edit responsibilities
- runtime assemblies referencing editor assemblies
- editor-only dependencies leaking into game startup or runtime loop

---

## Tasks

### Task 1 — Inspect the repository structure
Review the solution and project structure.

Identify:
- all `.csproj` files
- all executable projects
- all shared libraries
- any projects clearly editor-only
- any projects clearly runtime/game-only
- any mixed-responsibility projects

Document:
- project name
- apparent responsibility
- notable references/dependencies
- first impression of separation quality

Add the results to the report.

---

### Task 2 — Map startup paths
Identify how the application starts in:
- editor mode
- game/runtime mode

Find:
- entry points
- bootstrap classes
- host/application initialization logic
- configuration differences
- service registration differences
- preprocessor-based startup branching

Document:
- whether the split happens at project level, startup level, or deep in shared code
- whether editor and game have separate composition roots

Add the results to the report.

---

### Task 3 — Inventory all `#if EDITOR` and related directives
Search the entire repository for:
- `#if EDITOR`
- `#elif EDITOR`
- `#endif`
- `EDITOR` constants in project files
- any editor/game compilation symbols
- any other equivalent conditional compilation used to separate modes

For every occurrence, record:
- file path
- enclosing class/type
- method/property/region
- short description of what is being conditionally compiled
- initial classification:
  - acceptable boundary use
  - suspicious mixed responsibility
  - likely architectural issue

Notes:
- Do not create noise by listing standalone `#endif` entries without meaningful surrounding context.
- Include `DefineConstants`, `PropertyGroup`, and other project-level symbol definitions when they influence editor/runtime behavior.
- Include other relevant conditional compilation symbols if they are serving the same separation purpose as `EDITOR`.

Add a dedicated section to the report with a table.

---

### Task 4 — Identify editor responsibilities inside runtime code
Audit shared/runtime projects for editor-only responsibilities mixed into runtime classes.

Look for:
- `Save`, `Export`, `Import`, `Reimport`, `SerializeForEditor`, `Thumbnail`, `Inspector`, `Gizmo`, `Tool`, `Command`, `Undo`, `Selection`, `EditorMetadata`, etc.
- asset classes that both load and save
- world/entity/component classes that expose editor-only APIs
- rendering code that contains editor overlays or gizmo behavior
- services that act differently only because of editor mode

For each finding, document:
- exact location
- why it belongs to editor side
- probable extraction target:
  - editor service
  - editor project
  - separate writer/importer class
  - interface-based split
  - composition root split

Add the results to the report.

---

### Task 5 — Identify runtime responsibilities polluted by editor concerns
Inspect the game/runtime loop and systems for hidden editor coupling.

Look for:
- editor checks during update/draw
- selection/debug/tool state influencing runtime systems
- runtime services aware of editor panels or editor workflow
- editor-only input handling inside generic input systems
- runtime asset pipeline coupled to authoring workflow

Document all findings and classify severity.

---

### Task 6 — Audit asset pipeline separation
Focus specifically on assets.

Determine whether the current design separates:
- runtime asset consumption
- editor asset authoring
- asset loading
- asset saving/writing
- importing/reimporting
- runtime-ready vs source asset format

Questions to answer:
- Are asset model classes mixed with editing behavior?
- Are `Load` and `Save` responsibilities colocated?
- Are writers/importers in runtime assemblies?
- Does the game depend on authoring-time asset APIs?
- Are there editor-only serialization paths in shared asset classes?

In the report, provide:
- current state
- concrete problematic examples
- recommended direction

---

### Task 7 — Audit project references and dependency direction
Inspect project-to-project dependencies.

Verify:
- whether runtime projects depend on editor projects
- whether shared/core projects contain editor-only dependencies
- whether editor-specific packages are referenced from common runtime assemblies
- whether the dependency direction is clean

Document:
- acceptable dependencies
- bad dependency directions
- ambiguous projects that should likely be split

Also provide a compact dependency matrix or bullet mapping such as:
- ProjectA -> ProjectB : reason / risk level
- ProjectC -> PackageX : editor-only or runtime-safe

Add a dependency analysis section to the report.

---

### Task 8 — Audit service boundaries and abstractions
Check whether editor-only behavior is abstracted behind interfaces/services or directly embedded.

Look for opportunities such as:
- `IAssetSaver`
- `IGizmoService`
- `ISelectionService`
- `IEditorOverlayService`
- `IAssetImporter`
- `IInspectorProvider`
- null object implementations for runtime

Document:
- where service abstraction already exists
- where it is missing
- where existing abstractions are too editor-aware
- where conditional compilation could be replaced by dependency injection or composition

Add concrete examples.

---

### Task 9 — Audit naming and conceptual boundaries
Identify conceptual confusion in naming and organization.

Look for:
- editor concepts inside generic namespaces
- runtime concepts inside editor namespaces
- “manager” classes with mixed responsibilities
- types that represent both data and editing document behavior
- “utility” or “helper” classes that hide coupling

Document naming or folder structure issues that make the separation unclear.

---

### Task 10 — Classify findings by severity
Every finding in the report must be classified into one of these severities:

- **Critical**  
  Runtime/game cannot be cleanly isolated, or dependency direction is wrong.
- **High**  
  Clear architectural leakage likely to make refactors risky or maintenance difficult.
- **Medium**  
  Mixed responsibility exists but is localized and fixable.
- **Low**  
  Minor cleanup opportunity or acceptable for now.

Also add a category label for each finding, for example:
- Build/configuration
- Project structure
- Asset pipeline
- Rendering
- UI
- Serialization
- Services/DI
- Input
- Tooling
- Naming/organization

Severity guidance:
- Critical: runtime isolation is structurally blocked, or dependency direction is wrong across projects/assemblies.
- High: editor concerns are embedded in shared/runtime code in a way that increases refactor risk or spreads conditionals.
- Medium: mixed responsibility is real but localized behind a few files/types.
- Low: cleanup, naming, or boundary hardening opportunity with low migration risk.

---

### Task 11 — Propose a target architecture
Based on the findings, propose a realistic target architecture for this repository.

This must include:
- recommended separation between runtime and editor
- which projects should stay shared
- which responsibilities should move to editor-only assemblies
- where service interfaces should be introduced
- where conditional compilation is still acceptable
- where separate composition roots should exist

Keep it practical and incremental.

Do not propose a rewrite from scratch.

---

### Task 12 — Produce a future action plan for another AI agent
In the report, include a section called:

`Refactor action list for implementation agent`

This section must be structured as small actionable tasks.

Each task must contain:
- title
- scope
- expected code area
- why it matters
- suggested validation
- suggested commit message

Each task should also state whether it is primarily:
- dependency cleanup
- composition root split
- service extraction
- asset pipeline separation
- conditional compilation reduction
- naming/organization cleanup

The tasks must be small enough that another AI agent can:
- execute one task at a time
- validate it
- commit after each task

Do not produce giant refactor tasks.

---

### Task 13 — Propose commit strategy for the future implementation agent
Add a section describing how the future refactor agent should work.

This section must specify:
- one commit per small task
- preferred order of execution
- validation after each change
- when to stop and reassess
- which risky changes should be isolated in dedicated commits

Example structure:
1. dependency cleanup
2. composition root separation
3. asset saver extraction
4. editor service extraction
5. removal of unnecessary `#if EDITOR`
6. validation and cleanup

---

### Task 14 — Validate audit completeness
Before finishing, verify that the report includes:
- repository/project inventory
- startup/composition analysis
- full directive inventory
- asset pipeline analysis
- dependency direction analysis
- concrete findings
- target architecture
- actionable next-step task list
- commit strategy

If any section is weak or vague, improve it before finishing.

Before finishing, explicitly verify that:
- every critical/high finding has at least one concrete follow-up action
- the report identifies what can remain shared versus what should move to editor-only code
- the proposed task sequence minimizes risk by cleaning dependency direction before deeper behavior moves

---

## Expected report structure

The produced file should follow this structure:

```md
# Editor / Runtime Separation Audit Report

## Executive summary

## Repository and project overview

## Startup and composition roots

## Inventory of conditional compilation usages

## Findings by category

### Project structure
### Dependency direction
### Asset pipeline
### Rendering and runtime loop
### UI and tooling
### Services and abstractions
### Naming and organization

## Severity summary

## Recommended target architecture

## Refactor action list for implementation agent

## Suggested commit strategy

## Risks and validation checklist

## Appendix: dependency map and directive index