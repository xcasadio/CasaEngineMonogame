# Task completion

1. Build the solution(s) owning touched code (see `mem:suggested_commands`); both slns for cross-cutting changes.
2. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` when touched code is tested; add/update tests per AGENTS.md §6.
3. Run the related sample/demo for visible features.
4. Inspect `git diff`, stage file by file, one commit per task on a non-main branch, `type(area): summary`; never push.
5. Update docs/ADR when public API, asset format or editor behavior changed; end with the AGENTS.md §12 report.
