# ADR-0031: AI agent working rules

- **Status**: Accepted
- **Date**: 2026-09-06
- **Source**: `ai-agent/tasks/ai-guidelines-tasks.md:4,48,49,50,58,69`; `AGENTS.md` §3, §4, §8

## Context

The decisions D1 → D13 of the `ai-guidelines` plan were arbitrated with the author on 2026-09-06 and are applied by the plan without being re-discussed (`ai-agent/tasks/ai-guidelines-tasks.md:4`). D3, D4, D5, D13 and P4 govern how AI agents commit, ask questions, plan, and delegate to sub-agents in this repository, and are now reflected in `AGENTS.md`.

## Decision

- D3 — Commit: the agent commits alone, without asking, after each completed task; the commit is atomic and buildable. A dedicated branch per project is mandatory, created from `main`; never commit on `main`. Never push without the author's explicit request (source: `ai-agent/tasks/ai-guidelines-tasks.md:48`).
- D4 — Questions: grouped at plan time, then autonomous execution. On a block (missing information, contradiction, decision needed): stop and ask (source: `ai-agent/tasks/ai-guidelines-tasks.md:49`).
- D5 — Plan threshold: a plan is mandatory as soon as the work requires more than one commit; below that threshold, direct execution with the end-of-task report. The plan lives under `ai-agent/tasks/`, follows `ai-agent/plan-template.md`, and its update is included in the same commit as the task (source: `ai-agent/tasks/ai-guidelines-tasks.md:50`).
- D13 — No invention: every rule written in `AGENTS.md` or a file derived from it comes from an existing file in the repository, an answer from the author, or a cited official document. Otherwise the task is marked ⚠️ Blocked and the question is asked (source: `ai-agent/tasks/ai-guidelines-tasks.md:58`).
- P4 — Sub-agent model policy: keep the repository's delegation conditions, reworded with the global role vocabulary (`scout`, `mech-executor`, `executor`, `verifier`, `plan-verifier`), and "verifier for every non-trivial finished change". An agent running on Fable never launches a sub-agent that inherits Fable: every sub-agent is launched with a model explicitly matched to its task, following the author's global definitions — reading/search → haiku; execution/editing → sonnet; review/verification → opus. Project agents under `.claude/agents/*` carry `model: sonnet` (never omitted, never `inherit`). Safety net: `env.CLAUDE_CODE_SUBAGENT_MODEL = "sonnet"` in `.claude/settings.json`, so that no invocation without an explicit `model` falls back to the session's model (source: `ai-agent/tasks/ai-guidelines-tasks.md:69`).

## Consequences

- D3, D4, D5 and D13 are implemented in the current `AGENTS.md`: §3 "Workflow d'une tâche" states grouped questions, the plan-above-one-commit threshold, task-by-task execution with status icons, stop-on-block, and the end-of-task report (`AGENTS.md:19-25`); §4 "Git et commits" states the dedicated branch, the never-commit-on-`main` rule, one commit per completed task made by the agent without asking, atomic and buildable, English `type(area): summary` messages, and never pushing without explicit request (`AGENTS.md:27-33`).
- P4 is implemented in `AGENTS.md` §8 "Délégation à des sous-agents": default no delegation, the repository's delegation conditions, the `scout`/`mech-executor`/`executor`/`verifier` roles, and the explicit sub-agent model rule with the haiku/sonnet/opus mapping (`AGENTS.md:54-59`).
- The "never commit on `main`" rule is additionally enforced by a hook, not only documented: `.claude/hooks/block-commit-on-main.ps1` exists in the repository, referenced from `CLAUDE.md`'s "Claude Code" section.
- The sub-agent model safety net is implemented and verified: `.claude/settings.json` sets `CLAUDE_CODE_SUBAGENT_MODEL` to `"sonnet"` (`.claude/settings.json:27`), and all 6 files under `.claude/agents/` declare `model: sonnet` explicitly (verified by `rg -n "^model:" .claude/agents/*.md`).
- This ADR does not cover P1, P2, P3, P6, P7, P8, P12 or D6–D12, which are recorded separately or are process points local to this chantier rather than durable agent working rules.
