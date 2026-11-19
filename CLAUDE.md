@AGENTS.md

## Claude Code

- Règles par chemin : `.claude/rules/` (jumelles de `.github/instructions/`, chargées quand un fichier concerné est lu).
- Sous-agents de projet : `.claude/agents/` (chacun déclare son `model`). Skills partagés avec Copilot : `.claude/skills/` (`plan`, `adr`, et les skills de domaine).
- Réglages partagés : `.claude/settings.json` (permissions et serveurs MCP activés). Il ne déclare aucun hook : rien n'empêche techniquement un commit sur `main` ni un `git push`, les règles de `AGENTS.md` §4 s'appliquent quand même. Pas de `CLAUDE_CODE_SUBAGENT_MODEL` : cette variable écraserait le `model` déclaré par chaque agent.
- Rôles génériques (`scout`, `Explore`, `plan-verifier`, `security-reviewer`, `mech-executor`, `executor`, `verifier`, `security-executor`) : fournis par pilotfish, installé globalement dans `~/.claude/agents/` avec sa politique d'orchestration dans `~/.claude/CLAUDE.md` ; ils ne sont pas versionnés dans ce dépôt.
