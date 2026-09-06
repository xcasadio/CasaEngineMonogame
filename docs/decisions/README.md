# Architecture Decision Records

This folder records the architecture decisions of CasaEngine: engine and editor architecture, asset formats, public APIs, backends, and the rules that govern the AI agents working on the repository.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`; rule in `AGENTS.md` §10).
- Backfilled decisions keep a `Source` pointing at the original document. Audits under `ai-agent/audits/` are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
