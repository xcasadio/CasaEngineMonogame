@AGENTS.md

## Claude Code

- Règles par chemin : `.claude/rules/` (jumelles de `.github/instructions/`, chargées quand un fichier concerné est lu).
- Sous-agents de projet : `.claude/agents/` (chacun déclare son `model`). Skills partagés avec Copilot : `.claude/skills/` (`plan`, `adr`, et les skills de domaine).
- Réglages partagés : `.claude/settings.json` (permissions et hooks). Le hook `PreToolUse` interdit tout commit sur `main` et refuse `git push` ; la variable `CLAUDE_CODE_SUBAGENT_MODEL` évite qu'un sous-agent hérite du modèle de la session.
