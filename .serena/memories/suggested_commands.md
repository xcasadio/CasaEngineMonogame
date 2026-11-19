# Commands (Windows dev box)

- Build: `dotnet build CasaEngine.MonoGame.sln` (engine, demos, launcher, sample games); `dotnet build CasaEngine.Editor.MonoGame.sln` (editor, editor services, compilers, shaders, GizmoTool).
- Tests: `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (filter: `--filter FullyQualifiedName~Physics`).
- Content tools: `dotnet tool restore`, then `dotnet mgcb` / `dotnet mgcb-editor`.
- Search: `rg`, `fd`, `jq`, `yq`, `ast-grep` installed (usage in AGENTS.md §7); never broad recursive listings.
- Agent shell is Git Bash: use `/d/...` or relative paths; the Claude commit hook cannot resolve `cd /d/... && git commit` — run git from the repo root.
- codegraph index: `codegraph init` at repo root if `.codegraph/` is missing.
