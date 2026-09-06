# ADR-0030: AGENTS.md as the single source of AI agent rules and the tooling layout

- **Status**: Accepted
- **Date**: 2026-09-06
- **Source**: `ai-agent/tasks/archive/ai-guidelines-tasks.md:14-25,46,47,52,53,74,75,76` (line numbers as of the plan's approval; the plan was archived on completion)

## Context

Before this chantier, `AGENTS.md` was 33 lines and referenced by no other file, `CLAUDE.md` contained a single line pointing at `.github/copilot-instructions.md`, and `.github/copilot-instructions.md` was 535 lines in English. `.github/` held 7 agents (6 `agents/*.agent.md` plus `agents/engine-developer.md`), 6 path instructions (`instructions/*.instructions.md`) and 6 skills (`skills/*/SKILL.md`); `.claude/` held only `settings.local.json`. APM (Agent Package Manager, Microsoft) was installed on the machine (`apm --version`: 0.28.0) but not used (source: `ai-agent/tasks/ai-guidelines-tasks.md:14-25`).

## Decision

- D1: the target tools are Claude Code, GitHub Copilot (VS Code and the cloud agent), and any other tool reading `AGENTS.md` (Codex or equivalent) (source: `ai-agent/tasks/ai-guidelines-tasks.md:46`).
- D2: the single source of rules is `AGENTS.md`, hand-written, in French. `CLAUDE.md` becomes `@AGENTS.md` plus a short "Claude Code" section. `.github/copilot-instructions.md` is reduced to a pointer to `AGENTS.md`. No APM is used (source: `ai-agent/tasks/ai-guidelines-tasks.md:47`).
- D7: in `.github`, delete the files whose project is delivered (`agents/engine-developer.md`) and update all the others (`applyTo` globs, Bullet/Jolt, `EditorUI`/WPF, `DebugEditor`, frontmatters) (source: `ai-agent/tasks/ai-guidelines-tasks.md:52`).
- D8: for Claude Code, adopt everything that is documented: a committed `.claude/settings.json`, `.claude/rules/`, `.claude/agents/`, `.claude/skills/`, and hooks. No `.claude/commands/` (source: `ai-agent/tasks/ai-guidelines-tasks.md:53`).
- P9: `.github/copilot-instructions.md` is kept as a short pointer file to `AGENTS.md`, with no rule duplicated (source: `ai-agent/tasks/ai-guidelines-tasks.md:74`).
- P10: the 6 skills are moved to `.claude/skills/` and `.github/skills/` is removed, since `.claude/skills/` is read by both Copilot and Claude Code (source: `ai-agent/tasks/ai-guidelines-tasks.md:75`).
- P11: `.claude/agents/` mirrors the 6 Copilot domain agents, with their `tools` translated to the documented Claude tool names (source: `ai-agent/tasks/ai-guidelines-tasks.md:76`).

## Consequences

- Implemented and verified in the current repository: `AGENTS.md` is 143 lines, in French, and its header states it is the single source of rules for Claude Code, GitHub Copilot and any other `AGENTS.md`-reading tool (`AGENTS.md:1-3`).
- `CLAUDE.md` is 7 lines: first line `@AGENTS.md`, followed by a `## Claude Code` section pointing at `.claude/rules/`, `.claude/agents/`, `.claude/skills/` and `.claude/settings.json`.
- `.github/copilot-instructions.md` is 5 lines: a pointer to `AGENTS.md`, plus a mention of `.github/instructions/`, `.github/agents/` and the shared `.claude/skills/`.
- `.claude/` now contains `agents/` (6 files: `build-ci.md`, `editor-mgui.md`, `gameplay-samples.md`, `mgui-framework.md`, `physics-integration.md`, `rendering-pipeline.md`), `rules/` (6 files), `skills/` (8 subfolders including `adr`, `plan`, `feature-workflow` and domain skills), `settings.json`, and `hooks/block-commit-on-main.ps1`.
- `.github/skills/` no longer exists (verified: `fd . .github -d 2` lists only `agents/`, `copilot-instructions.md` and `instructions/`).
- No use of APM was found in the repository (no APM manifest or reference under version control); this is consistent with D2's "no APM" clause but is not, by itself, proof the tool was never invoked outside the repo — that remains unverified beyond the absence of tracked files.
