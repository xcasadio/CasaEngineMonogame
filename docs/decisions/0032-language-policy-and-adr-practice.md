# ADR-0032: Language policy and ADR practice

- **Status**: Accepted
- **Date**: 2026-09-06
- **Source**: `ai-agent/tasks/archive/ai-guidelines-tasks.md:51,54,70,522,530` (line numbers as of the plan's approval; the plan was archived on completion); `AGENTS.md` §5, §10

## Context

The `ai-guidelines` plan sets a language split between agent-facing material (written by and for the author, in French) and durable engineering artifacts (code, ADRs, `docs/`, commit messages, in English), and formalizes how architecture decisions are recorded going forward and backfilled from existing sources.

## Decision

- D6 — ADR practice: Architecture Decision Records live under `docs/decisions/`, one file per decision, in the short format (`Status`, `Date`, `Source`, `Context`, `Decision`, `Consequences`), written in English. Existing decisions are backfilled from the existing decision tables and lists. Existing audits under `ai-agent/audits/` stay read-only: they are referenced, never modified (source: `ai-agent/tasks/ai-guidelines-tasks.md:51`).
- D9 — Language: French for `AGENTS.md`, the path rules, the agents, the skills and the plans; **English** for the code, the commit messages, the docs under `docs/`, and the ADRs. The 49 existing French docs will be translated in a separate, later plan (source: `ai-agent/tasks/ai-guidelines-tasks.md:54`).
- P5 — ADR volume and naming: one file per decision (D6); the estimate before the inventory was "several dozen", with the exact count produced by the inventory task (T4.3). Decisions on the same theme may be grouped into a single file. Naming and status values (decisions of this plan, with no external source): `NNNN-short-title.md`, an increasing four-digit numbering, an index in `docs/decisions/README.md`; `Status` values: `Proposed`, `Accepted`, `Superseded by ADR-xxxx`, `Deprecated` (source: `ai-agent/tasks/ai-guidelines-tasks.md:70`).
- O2 — Translating the 49 existing French docs under `docs/` is deferred to a separate, later plan (D9) and is explicitly out of scope for this chantier (source: `ai-agent/tasks/ai-guidelines-tasks.md:522,530`).

## Consequences

- D9's language split is implemented in `AGENTS.md` §5 "Langue": French for `AGENTS.md` itself, the path rules, the agents, the skills, the plans under `ai-agent/tasks/`, and the replies to the author; English for the code (types, members, comments), the commit messages, the documents under `docs/`, and the ADRs under `docs/decisions/` (`AGENTS.md:35-38`).
- D6 and P5 are implemented: `docs/decisions/README.md` states the same rules (one file per decision, decisions on the same theme may share a file, template `template.md`, backfilled decisions keep a `Source` pointing at the original document, audits stay read-only, a decision is never rewritten — a change supersedes it) and its index lists the backfilled ADRs from `ADR-0001` onwards, confirming the `NNNN-short-title.md` naming and the `Accepted` status value are in active use.
- `AGENTS.md` §10 "Documentation et ADR" implements the D6 rule at the agent-workflow level: every architecture, asset-format, public-API or backend decision taken during a plan or a discussion is recorded in `docs/decisions/`, referencing the `adr` skill and the same one-file-per-decision, read-only-audits rules (`AGENTS.md:114-117`).
- O2 remains open and unstarted as of this writing: no translation plan for the 49 French `docs/` files was found under `ai-agent/tasks/` at the time of this backfill; it stays explicitly out of scope for the `ai-guidelines` chantier.
