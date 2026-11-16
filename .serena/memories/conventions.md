# Conventions

- Language: code, comments, commits, `docs/`, ADRs in English; AGENTS.md, rules, plans, replies to author in French.
- C#: file-scoped namespaces matching folders (`CasaEngine.Framework.Scene.Entities.Components`); `GlobalUsings.cs` aliases XNA `Color`/`Rectangle`/`Point`/`Keys` — don't re-import conflicting System.Drawing types.
- Each project has `GlobalUsings.NamespaceCleanup.cs` and `InternalsVisibleTo.*.cs` files; tests/editor services rely on `internal` access instead of widening APIs.
- Design decisions are referenced in code as `ADR-00xx` comments; new decisions go to `docs/decisions/`.
- Hot paths (Update/Draw/layout/input/render/physics step): no LINQ, no capturing lambdas, no per-frame allocations; `for` loops, cached lists. Full list: AGENTS.md §9.3.
- Serialized asset field names are stable API; changes need migration support.
