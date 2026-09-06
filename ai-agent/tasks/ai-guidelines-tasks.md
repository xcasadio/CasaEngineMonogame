# Plan agent IA — Fichiers de guidage des agents IA (AGENTS.md, CLAUDE.md, .github, .claude, ADR)

Plan d'exécution du chantier décrit dans [analysis-ai-agent-files.md](../audits/analysis-ai-agent-files.md) (audit du 2026-09-06 : 128 constats confirmés par réfutation croisée, identifiés `[rac-NN]`, `[gh-NN]`, `[plans-NN]`, `[dec-NN]`).
Les décisions D1 → D13 ci-dessous ont été arbitrées avec l'auteur le 2026-09-06 : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P12 restent à valider à l'approbation du plan.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Faire d'`AGENTS.md` la source unique des règles pour tous les agents IA du dépôt (Claude Code, GitHub Copilot, outils lisant `AGENTS.md`), y intégrer les deux nouvelles règles de l'auteur (« ne rien inventer, ne rien supposer, demander » ; « plan détaillé avec statuts à icônes et un commit par tâche »), supprimer les contradictions et doublons relevés par l'audit, mettre à jour ou supprimer les fichiers `.github` périmés, créer les mécanismes Claude Code documentés (`.claude/settings.json`, `rules/`, `agents/`, `skills/`, hooks), introduire les Architecture Decision Records dans `docs/decisions/` avec rétro-remplissage, et corriger les index et docs périmées listés par l'audit.

Aucun fichier C# n'est modifié par ce chantier.

## État vérifié du dépôt (2026-09-06)

Faits du dépôt (vérifiés par `fd`, `rg`, `wc`) :

- `AGENTS.md` (33 lignes, français) n'est référencé par aucun autre fichier `[rac-09]` ; `CLAUDE.md` contient une seule ligne `@.github/copilot-instructions.md` ; `.github/copilot-instructions.md` fait 535 lignes (anglais, `rg -c '^'`).
- `.github/` contient 7 agents (6 `agents/*.agent.md` + `agents/engine-developer.md`), 6 instructions par chemin (`instructions/*.instructions.md`) et 6 skills (`skills/*/SKILL.md`). `.claude/` ne contient que `settings.local.json` et `worktrees/` ; `settings.local.json` n'est pas dans le `.gitignore` du dépôt, il est ignoré par le fichier d'exclusion global de l'auteur (`~/.config/git/ignore`, règle `**/.claude/settings.local.json`).
- Les sous-modules `MGUI/` et `NvgSharp/` ont leurs propres fichiers ; ils sont hors périmètre (D12).
- Solutions à la racine : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` `[rac-36]`.
- APM (Agent Package Manager, Microsoft, https://microsoft.github.io/apm/) est installé sur la machine (`apm --version` : 0.28.0) mais **n'est pas utilisé** (D2).
- `rtk` est désinstallé (confirmé par l'auteur le 2026-09-06 ; `command -v rtk` : absent). Les 15 mentions de `rtk` dans `.github/copilot-instructions.md` sont donc périmées ; celles de `MGUI/.github/copilot-instructions.md` sont hors périmètre (D12).
- Agents globaux de l'auteur (`~/.claude/agents/`) et leur champ `model` : `scout` et `Explore` = haiku ; `executor` et `mech-executor` = sonnet ; `verifier`, `plan-verifier`, `security-reviewer`, `security-executor` = opus.
- Arbre de travail : `CasaEngine.Launcher/Program.cs` modifié et `Projects/SampleProject/.casaeditor/viewport.editor.json` non suivi sont des changements **de l'auteur**, antérieurs au chantier : ne jamais les indexer ni les committer.

Faits de la documentation officielle (consultée le 2026-09-06 ; les URL sont la source de chaque règle technique de ce plan) :

- Claude Code, mémoire et règles — https://code.claude.com/docs/en/memory.md : `CLAUDE.md` est chargé à chaque session ; import `@chemin` (relatif au fichier, profondeur 4) ; la page documente explicitement `@AGENTS.md` pour un dépôt qui utilise `AGENTS.md` (« Claude Code reads CLAUDE.md, not AGENTS.md ») ; `.claude/rules/*.md` découverts récursivement, frontmatter `paths:` (liste YAML de globs, accolades supportées), un fichier sans `paths` est chargé au lancement ; les commentaires HTML de bloc sont retirés du contexte ; recommandation : moins de 200 lignes par fichier.
- Claude Code, settings — https://code.claude.com/docs/en/settings.md : `.claude/settings.json` est le fichier partagé (commité), `.claude/settings.local.json` le fichier personnel ; les hooks se déclarent dans `settings.json`.
- Claude Code, permissions — https://code.claude.com/docs/en/permissions.md : règles `Bash(<préfixe> *)` (le suffixe `:*` est équivalent) ; évaluation `deny`, puis `ask`, puis `allow` ; les règles `allow` d'un projet ne s'appliquent qu'après acceptation du dossier (workspace trust), les règles `deny` et `ask` s'appliquent immédiatement ; un hook bloquant (code 2 ou décision `deny`) prime sur les règles `allow`.
- Claude Code, hooks — https://code.claude.com/docs/en/hooks.md : événement `PreToolUse`, `matcher` (nom d'outil), handler `{ "type": "command", "if": "Bash(git commit *)", "command": …, "args": […], "timeout": … }` (le champ `if` est un filtre par règle de permission) ; entrée JSON sur stdin avec `tool_name`, `tool_input.command`, `cwd` ; blocage par sortie JSON `hookSpecificOutput.permissionDecision: "deny"` + `permissionDecisionReason`, ou code de sortie 2 avec le message sur stderr ; sous Windows, forme documentée `"command": "powershell.exe", "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/<script>.ps1"]`.
- Claude Code, sous-agents — https://code.claude.com/docs/en/sub-agents.md : `.claude/agents/<nom>.md`, frontmatter `name`, `description`, `tools`, `model`, etc. Valeurs de `model` : `sonnet`, `opus`, `haiku`, `fable`, un identifiant complet, ou `inherit` ; quand le champ est absent, l'ordre de résolution est : paramètre `model` de l'invocation, puis frontmatter, puis variable d'environnement `CLAUDE_CODE_SUBAGENT_MODEL`, puis **le modèle de la conversation principale** ; la doc cite le routage vers des modèles moins chers comme moyen de contrôler les coûts.
- Claude Code, skills — https://code.claude.com/docs/en/skills.md : `.claude/skills/<nom>/SKILL.md`, frontmatter `name`, `description`, etc. ; `.github/skills/` n'y est pas documenté comme emplacement de découverte.
- Claude Code, commandes — https://code.claude.com/docs/en/commands.md : les commandes personnalisées `.claude/commands/` sont documentées comme remplacées par les skills.
- Copilot, AGENTS.md — https://github.blog/changelog/2025-08-28-copilot-coding-agent-now-supports-agents-md-custom-instructions/ : l'agent cloud lit `AGENTS.md` (racine et imbriqués), en plus de `.github/copilot-instructions.md`, `.github/instructions/**.instructions.md`, `CLAUDE.md`.
- Copilot dans VS Code — https://code.visualstudio.com/docs/agent-customization/custom-instructions : `AGENTS.md` détecté automatiquement (setting `chat.useAgentsMdFile`), `CLAUDE.md` aussi (`chat.useClaudeMdFile`), `.github/copilot-instructions.md` aussi ; frontmatter des `*.instructions.md` : `name`, `description`, `applyTo` ; « If you have multiple instruction files in your project, VS Code combines and adds them to the chat context, no specific order is guaranteed ».
- Copilot, agents personnalisés — https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents et https://code.visualstudio.com/docs/agent-customization/custom-agents : `.github/agents/<nom>.agent.md` (suffixe obligatoire) ; frontmatter `name` (défaut : nom de fichier), `description` (obligatoire), `tools` (tableau YAML d'identifiants d'outils ou de jeux d'outils, ex. `'search'`, `'edit'`, `'web/fetch'`, `'search/codebase'`, `'search/usages'`, `'read/terminalLastCommand'`), `model`, `target` (`vscode` ou `github-copilot`), `mcp-servers`, `handoffs`. Les identifiants `workspace`, `terminal`, `code_search`, `git` utilisés aujourd'hui ne figurent pas dans cette documentation.
- Copilot, skills — https://docs.github.com/en/copilot/concepts/agents/about-agent-skills : emplacements projet `.github/skills`, `.claude/skills`, `.agents/skills` ; supportés par l'agent cloud, le code review, le CLI, l'app, et le mode agent de VS Code et JetBrains.
- Décisions d'architecture existantes recensées par l'audit (surface « décisions », `[dec-01]` → `[dec-10]`, doublons `[dec-13]` → `[dec-21]`), à rétro-remplir en ADR : `ai-agent/audits/analysis-audio-system.md` (D1 → D13 + D2-bis), `docs/engine/collision-2d-3d-architecture.md` (D1 → D6 + posture de compatibilité), `docs/editor/timeline-generic.md` (5), `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` (10), `docs/engine/coroutines_specifications.md` (« Décisions V1 validées »), `docs/editor/animation2d_editor_casaengine.md` (6), `docs/editor/ui-screen-editor/architecture.md` (retenues 4 / reportées 4), `docs/engine/shader-naming-convention.md` (4), `docs/engine/materials-sources-of-truth.md` (matrice), `ai-agent/tasks/gltf-import-migration-tasks.md`, `ai-agent/tasks/pbr-rendering-implementation-plan.md`, `ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md`, `ai-agent/tasks/play-in-editor-tasks.md`, `ai-agent/audits/CasaEngine_layering_project_split_evaluation.md`, `ai-agent/audits/CasaEngine_folder_hierarchy_namespace_compatibility.md`, `ai-agent/audits/structure-analyze-tasks.md`, `ai-agent/audits/analysis-possession-gameplay-framework.md`, `ai-agent/audits/analysis-bepuphysics2-migration.md`, `ai-agent/audits/analysis-tilemap-render-spaces.md`, `ai-agent/audits/analysis-play-in-editor.md`, `docs/engine/rendering-2d-3d-spaces.md`, `docs/engine/navigation-engine-features.md`, `docs/engine/gameplay-mode.md`, `docs/engine/character-controller-features.md`, `docs/engine/animation-motion-matching.md`, `docs/engine/animation-deformer-support-policy.md`, `docs/engine/yarn_spinner_integration.md`, `docs/engine/audio-system.md`, `docs/editor/play-in-editor.md`.

## Décisions verrouillées (arbitrées le 2026-09-06)

| Réf | Décision |
|---|---|
| D1 | Outils cibles : **Claude Code**, **GitHub Copilot** (VS Code et agent cloud), et tout outil lisant `AGENTS.md` (Codex ou équivalent). |
| D2 | **Source unique = `AGENTS.md`**, écrit à la main, en français. `CLAUDE.md` = `@AGENTS.md` plus une courte section « Claude Code ». `.github/copilot-instructions.md` est réduit à un pointeur vers `AGENTS.md` (voir P9). **Pas d'APM.** |
| D3 | **Commit** : l'agent committe seul, sans demander, après chaque tâche terminée. **Branche dédiée obligatoire par chantier, jamais de commit sur `main`.** Jamais de push sans demande explicite. |
| D4 | **Questions** : groupées au moment du plan ; exécution autonome ensuite. En cas de blocage ⚠️ : **s'arrêter et demander**. |
| D5 | **Plan obligatoire dès que le travail demande plus d'un commit** ; en dessous, exécution directe avec le rapport de fin de tâche. Le plan vit dans `ai-agent/tasks/`, suit `ai-agent/plan-template.md`, et **sa mise à jour est incluse dans le commit de la tâche**. |
| D6 | **ADR** dans `docs/decisions/`, **un fichier par décision**, format court (Status, Date, Context, Decision, Consequences, Source), en anglais. **Rétro-remplissage** depuis les tableaux et listes de décisions existants. **Les audits existants restent en lecture seule** : on les référence, on ne les modifie pas. |
| D7 | `.github` : **supprimer** les fichiers dont le chantier est livré (`agents/engine-developer.md`) et **mettre à jour** tous les autres (globs `applyTo`, Bullet/Jolt, `EditorUI`/WPF, `DebugEditor`, frontmatters). |
| D8 | Claude Code : **tout** ce qui est documenté : `.claude/settings.json` commité, `.claude/rules/`, `.claude/agents/`, `.claude/skills/`, hooks. Pas de `.claude/commands/`. |
| D9 | **Langue** : français pour `AGENTS.md`, règles, agents, skills et plans ; **anglais** pour le code, les messages de commit, les docs de `docs/` et les ADR. Les 49 docs existantes en français seront traduites dans un **plan séparé ultérieur**. |
| D10 | Dérives d'index et docs périmées listées par l'audit : **corrigées dans ce chantier** (`docs/`, `ai-agent/README.md`, plans actifs). Audits non modifiés (D6). |
| D11 | Branche **`ai-guidelines`**, créée depuis `main`. |
| D12 | Sous-modules `MGUI/` et `NvgSharp/` **hors périmètre**. |
| D13 | **Ne rien inventer** : toute règle écrite dans `AGENTS.md` ou un fichier dérivé provient d'un fichier existant du dépôt, d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon la tâche passe en ⚠️ et la question est posée. |

## Points à valider à l'approbation du plan

Arbitrages nécessaires révélés par l'audit. Chaque ligne donne la proposition retenue par défaut ; l'auteur peut la modifier avant approbation.

| Réf | Sujet | Proposition |
|---|---|---|
| P1 | Build : `AGENTS.md:27` impose « toujours » un build, `copilot-instructions.md:105` dit « if possible » `[rac-02]` ; la solution n'est pas nommée `[rac-36]`. | Build obligatoire avant de passer une tâche en ✅ ; si le build est impossible, la tâche reste 🧪 avec la raison écrite. Nommer les deux solutions (`CasaEngine.MonoGame.sln`, `CasaEngine.Editor.MonoGame.sln`). |
| P2 | Rupture d'API : « jamais sans compat » vs « sauf demande explicite » `[rac-03]`. | Pas de rupture sans demande explicite ; si demandée ou inévitable, préférer `[Obsolete]`/surcharge quand c'est raisonnable, et la signaler dans le rapport. |
| P3 | Samples : « obligatoire dès qu'une feature n'est pas triviale » (`AGENTS.md:18`) vs « allowed » et 6e priorité sur 7 `[rac-04]`. | Garder la règle d'`AGENTS.md` : sample ou démo minimal obligatoire pour toute feature visible non triviale. |
| P4 | Délégation : « do not delegate by default » + conditions du dépôt vs rôles nommés du `CLAUDE.md` global `[rac-10]` `[rac-11]` ; **coût des sous-agents** (amendement de l'auteur du 2026-09-06). | Garder les conditions du dépôt, reformulées avec le vocabulaire des rôles globaux (`scout`, `mech-executor`, `executor`, `verifier`, `plan-verifier`), et « verifier pour tout changement non trivial terminé ». **Règle de modèle** : un agent qui tourne sur Fable ne lance jamais un sous-agent qui hérite de Fable ; tout sous-agent est lancé avec un modèle explicitement adapté à la tâche, en suivant les définitions globales de l'auteur : lecture et recherche → haiku ; exécution et édition → sonnet ; revue et vérification → opus. Les agents de projet `.claude/agents/*` portent `model: sonnet` (jamais omis, jamais `inherit`). Filet de sécurité : `env.CLAUDE_CODE_SUBAGENT_MODEL = "sonnet"` dans `.claude/settings.json` (T3.1), pour qu'aucune invocation sans `model` ne retombe sur le modèle de la session. |
| P5 | Volume et nommage des ADR. | Un fichier par décision (D6). Estimation avant inventaire : plusieurs dizaines ; le compte exact est produit en T4.3. Les décisions d'une même thématique peuvent être regroupées dans un seul fichier (amendement de l'auteur du 2026-09-06). Nommage et statuts proposés (décisions de ce plan, sans source externe) : `NNNN-short-title.md`, numérotation croissante à quatre chiffres, index dans `docs/decisions/README.md` ; valeurs de `Status` : Proposed, Accepted, Superseded by ADR-xxxx, Deprecated. |
| P6 | `docs/engine/animation2d-composed-format-v1.md` est déclaré livré par deux plans mais n'existe pas `[plans-23]`. | L'écrire en anglais depuis le code (`Animation2dData`, `Animation2dPartData`, `Animation2dTrackData`, `Animation2dCompositionSampler`, présents sous `CasaEngine/Framework/Assets/Animations/`), tâche T5.4. |
| P7 | Plans actifs périmés : `ui-integration.md` `[plans-25]` (77 cases non cochées, mais `UIRoot`, `ScreenStack`, `ViewMouseViewport`, `InputRouter`, `WorldUIComponent` existent) et `static-model-import-tasks.md` `[plans-24]` (cite `CasaEngine.EditorUI`, `RiggedModelLoader`, `StaticModelImporter` disparus). | Réconciliation bornée dans ce chantier (T5.5) : cocher ce qui existe dans le code, archiver si livré, sinon lister ce qui reste. |
| P8 | Hook « commit sur `main` interdit » : script PowerShell sous `.claude/hooks/` (Windows), événement `PreToolUse`, matcher `Bash`, sans filtre `if` pour que le script reconnaisse aussi `git -C <chemin> commit`, les commandes chaînées et tout wrapper (doc hooks). Refus de `git push` par `permissions.deny` (doc permissions) et par le hook. `rtk` est désinstallé : aucune forme `rtk` n'est prévue. | Retenu. |
| P9 | `.github/copilot-instructions.md` : Copilot lit `AGENTS.md` nativement (changelog 2025-08-28, doc VS Code), mais la doc ne dit pas si tous les clients Copilot (ex. code review) le font. | Garder un fichier pointeur de quelques lignes vers `AGENTS.md`, sans règle dupliquée. |
| P10 | `.github/skills/` : `.claude/skills/` est lu par Copilot **et** Claude Code (doc skills des deux outils). | Déplacer les 6 skills vers `.claude/skills/` et supprimer `.github/skills/`. |
| P11 | `.claude/agents/` : miroir des 6 agents de domaine Copilot, avec frontmatter Claude. | Retenu ; les `tools` sont traduits vers les noms d'outils Claude documentés. |
| P12 | Format canonique des plans : l'audit constate qu'aucune forme unique n'existe pour le titre `[plans-01]`, les identifiants de tâche `[plans-16]`, le gabarit de tâche `[plans-12]`, ni les titres de sections `[plans-02]` ; le modèle de T1.1 est donc une **décision**, pas une observation. | Titre `# Plan agent IA — <sujet>` (forme de `pbr-rendering-implementation-plan.md:1`) ; identifiants `T<phase>.<numéro>` (forme de `play-in-editor-tasks.md:53`) ; tâche `### <icône> T<n>.<m> — <titre>` avec `Objectif`, `Fichiers` (ou `Sources` pour une tâche de rétro-remplissage), `Étapes`, `Validation`, `Commit` (gabarit de `pbr-rendering-implementation-plan.md:59-66` et `:74-75`) ; sections dans l'ordre listé en T1.1. |

## Règles d'exécution pour l'agent

- **Branche `ai-guidelines`** (D11). Ne jamais committer sur `main`. Ne jamais changer de branche dans l'arbre de travail principal pendant le chantier.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, remplacer par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier (D5).
- **Un commit par tâche**, message en anglais au format `type(area): summary` (`docs`, `chore`, `fix`, `refactor`…). Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser** (D3). Le merge sur `main` reste une décision humaine.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ni `Projects/SampleProject/.casaeditor/viewport.editor.json` (changements de l'auteur). Indexer fichier par fichier (`git add <chemin>`), jamais `git add -A` ni `git add .`.
- **Ne rien inventer** (D13). Si une information manque ou si deux sources se contredisent sans arbitrage dans D1 → D13 ou P1 → P12, passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter** (D4).
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Langue** (D9) : ce plan, `AGENTS.md`, les règles, agents et skills en français ; ADR et nouvelles docs en anglais ; messages de commit en anglais.
- Ne pas toucher `MGUI/` ni `NvgSharp/` (D12). Ne pas modifier les fichiers existants de `ai-agent/audits/` (D6) ; seul le nouvel audit `analysis-ai-agent-files.md` est ajouté en T0.1.
- Toute règle technique écrite dans un fichier livré cite sa source dans la note de validation de la tâche : constat `[xxx-NN]` de l'audit, décision D/P, ou URL de doc officielle (liste dans « État vérifié du dépôt »).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : fichiers écrits, validation manuelle incomplète ou en attente.
- ✅ Done : validé, commit effectué.
- ⚠️ Blocked : bloqué par une information manquante ou une décision à prendre.

## Validation globale

Ce chantier ne modifie aucun fichier C# : pas de build requis. Validation en fin de chantier (T6.1), chaque critère étant borné aux fichiers produits ou touchés par le chantier :

1. Chargement Claude Code : dans une session neuve, `/context` liste `CLAUDE.md` et `AGENTS.md` sous « Memory files » ; après lecture d'un fichier sous `CasaEngine/Framework/Rendering/`, la règle `.claude/rules/rendering.md` apparaît. Si la session courante ne peut pas être redémarrée, T6.1 reste 🧪 pour l'auteur.
2. Chargement Copilot : dans VS Code, `AGENTS.md` est chargé (setting `chat.useAgentsMdFile`) : vérification manuelle de l'auteur, 🧪.
3. Frontmatters : `yq --front-matter=extract` parse sans erreur chaque fichier de `.github/instructions/`, `.github/agents/`, `.claude/rules/`, `.claude/agents/`, `.claude/skills/*/SKILL.md`.
4. Liens : tous les liens markdown relatifs des fichiers créés ou modifiés par le chantier (liste tenue dans les notes de validation des tâches) résolvent vers un fichier existant (script jetable dans le dossier de travail de la session, résultat consigné sous T6.1).
5. Références périmées, périmètre = `AGENTS.md`, `CLAUDE.md`, `.github/`, `.claude/`, plus les fichiers de `docs/` modifiés par T5.2 et T5.3 : `rg -n "CasaEngine\.EditorUI|CasaEngine\.SimpleEditor|ThirdParties/|DebugEditor|Game1\.cs|#if EDITOR|Framework/Game/|CasaEngine\.AISamples" <périmètre>` renvoie vide ; `rg -n "BulletSharp|Jolt" AGENTS.md CLAUDE.md .github .claude` renvoie vide (les docs de `docs/` peuvent citer Bullet comme historique de migration : hors critère).
6. Index : `docs/README.md` liste tous les fichiers `docs/**/*.md` sauf lui-même et sauf `docs/decisions/**`, sous-arbre indexé par `docs/decisions/README.md` (lui-même listé dans `docs/README.md`) ; `docs/decisions/README.md` liste tous les `docs/decisions/NNNN-*.md` ; `ai-agent/README.md` liste tous les fichiers de `ai-agent/audits/*.md` et de `ai-agent/tasks/*.md` (l'archive reste décrite en prose, comme aujourd'hui). Contrôle par comparaison `fd` vs liens.
7. Couverture d'`AGENTS.md` : la table de correspondance produite en T1.2 couvre chaque puce normative de l'ancien `copilot-instructions.md` et de l'ancien `AGENTS.md`, et ne contient aucune ligne « supprimée » sans motif.

---

## Phase 0 — Ouverture du chantier

### ✅ T0.1 — Committer l'audit et ce plan sur la branche

- Objectif : ouvrir le chantier sur une branche isolée avec sa base de preuves.
- Fichiers : `ai-agent/audits/analysis-ai-agent-files.md` (rapport d'audit du 2026-09-06 avec identifiants de constats), `ai-agent/tasks/ai-guidelines-tasks.md` (ce fichier).
- Étapes :
  1. La branche `ai-guidelines` a été créée depuis `main` au moment de l'écriture du plan ; vérifier avec `git branch --show-current`. Si le résultat n'est pas `ai-guidelines`, ne rien indexer, passer la tâche en ⚠️ et demander.
  2. `git add` des deux fichiers ci-dessus uniquement.
- Validation : `git branch --show-current` retourne `ai-guidelines` ; `git status --short` ne montre plus ces deux fichiers comme non suivis ; `Program.cs` et `viewport.editor.json` restent non indexés.
- Commit : `docs(ai): add the AI-guidelines audit and the agent task plan`
- Note de validation (2026-09-06) : `git branch --show-current` = `ai-guidelines` ; `git worktree list` ne montre que l'arbre principal ; approbation du plan par l'auteur le 2026-09-06 avec les amendements P4, P5, P8 ; seuls les deux fichiers de la tâche sont indexés.

---

## Phase 1 — Socle des règles

### ✅ T1.1 — Écrire le modèle canonique de plan `ai-agent/plan-template.md`

- Objectif : figer le modèle que tout plan d'agent doit suivre (D5, P12), construit à partir des conventions observées dans les 25 plans actifs (audit, surface « plans ») et des choix de P12.
- Fichiers : `ai-agent/plan-template.md` (nouveau), `ai-agent/README.md` (une phrase de renvoi vers le modèle).
- Étapes :
  1. Sections, dans cet ordre, chacune attestée par l'audit : titre `# Plan agent IA — <sujet>` (P12) ; phrase de renvoi vers l'analyse source (`audio-system-tasks.md:3`) ; `## Objectif` ; `## État vérifié du dépôt (<date>)` `[plans-08]` ; `## Décisions verrouillées` en tableau `| Réf | Décision |` `[plans-07]` ; `## Règles d'exécution pour l'agent` `[plans-02]` ; `## Légende des statuts` avec la définition de chacune des cinq icônes `[plans-04]` (`pbr-rendering-implementation-plan.md:47-53`) ; `## Validation globale` `[plans-11]` ; phases `## Phase N — …` ; tâches selon P12 ; `## Points ouverts` en tableau `| Réf | Sujet | Tâche concernée |` (`audio-system-tasks.md:679`) `[plans-13]` ; `## Hors périmètre` `[plans-13]`.
  2. Bloc de règles d'exécution standard, formulation reprise de `audio-system-tasks.md:6-16` et `pbr-rendering-implementation-plan.md:34-45` `[plans-04]` `[plans-05]` `[plans-14]` `[plans-21]` : branche dédiée, une tâche à la fois, cycle ⏳ → 🚧 → ✅/🧪/⚠️, mise à jour du plan dans le commit de la tâche `[plans-18]` (D5), un commit par tâche avec message suggéré, jamais de push, jamais de 🚧 en fin de session, build/tests quand du code est touché (P1).
  3. Règle « ne rien inventer » et comportement en cas de blocage : une seule formulation, celle de D13 et D4 (l'audit relève trois comportements différents dans les plans existants `[plans-09]` `[plans-10]` `[plans-17]` ; D4 tranche : s'arrêter et demander).
  4. Note en tête du modèle : plan obligatoire dès que le travail demande plus d'un commit (D5) ; questions groupées au moment du plan (D4).
- Validation : le modèle contient toutes les sections listées ; ce plan lui-même respecte le modèle (chaque tâche a `Objectif`, `Fichiers` ou `Sources`, `Étapes`, `Validation`, `Commit`) ; chaque élément normatif du modèle renvoie à un constat `[plans-NN]`, une ligne d'un plan existant, ou une décision D/P.
- Commit : `docs(ai): add the canonical agent plan template`
- Note de validation (2026-09-06) : `rg` confirme la présence des 16 sections et rubriques du modèle (une occurrence chacune) et des 5 icônes de la légende ; `ai-agent/README.md` renvoie vers `plan-template.md` ; ce plan a 27 tâches portant chacune les cinq rubriques (`rg -c` : 27 `Objectif`, 27 `Étapes`, 27 `Validation`, 27 `Commit`). Sources des éléments du modèle : règles d'exécution `audio-system-tasks.md:6-16` et `pbr-rendering-implementation-plan.md:34-45` (`[plans-04]` `[plans-05]` `[plans-14]` `[plans-21]`), légende `pbr:47-53`, gabarit de tâche `pbr:59-66` et `:74-75` (P12), points ouverts `audio:679` (`[plans-13]`), build et tests P1, blocage D4, ne rien inventer D13, langue D9, seuil D5.

### ✅ T1.2 — Réécrire `AGENTS.md` comme source unique des règles

- Objectif : un seul fichier de règles, en français, sans doublon ni contradiction, intégrant les décisions D1 → D13 et les arbitrages P1 → P4, **sans perdre aucune règle** de l'ancien `copilot-instructions.md` autrement que volontairement.
- Fichiers : `AGENTS.md` ; table de correspondance consignée sous cette tâche dans ce plan.
- Étapes :
  1. **Table de correspondance** : une ligne par section de premier niveau de `.github/copilot-instructions.md` (535 lignes : préambule, Pilotfish / Agent Orchestration, Shell tools, General task workflow, Priorities, Scope control, CasaEngine architecture rules, Editor vs runtime separation, Public API and compatibility, Performance rules, MGUI / UI rules, Rendering / shaders, Physics, Assets and serialization, Tests and validation, Documentation, Git rules, Code style, Error handling, Reverse engineering / porting rules, Completion report) et par règle de l'actuel `AGENTS.md`. Pour toute section qui n'est pas migrée telle quelle, la table descend à la granularité de la **puce normative** (une ligne par puce). Chaque ligne donne sa destination : section d'`AGENTS.md`, fichier de `.github/instructions/` + `.claude/rules/`, ADR, ou « supprimée » avec motif (doublon : `[rac-05]` → `[rac-07]`, `[rac-13]` → `[rac-20]` ; périmée : `[rac-34]`, et tout ce qui concerne `rtk` : `[rac-12]`, `[rac-22]` → `[rac-25]`, outil désinstallé ; remplacée par une décision D/P). Cette table est écrite **avant** la réécriture et sert de validation.
  2. Résoudre les contradictions confirmées : commit (`[rac-01]` `[rac-30]` `[rac-31]` → D3), build (`[rac-02]` `[rac-36]` → P1), API (`[rac-03]` → P2), samples (`[rac-04]` → P3), délégation et verifier (`[rac-10]` `[rac-11]` → P4), `rtk` (`[rac-12]`, `[rac-22]` → `[rac-25]` → sections supprimées, outil désinstallé ; les outils restants sont `rg`, `fd`, `jq`, `yq`, `ast-grep`, vérification facultative en début de tâche), absence de toute étape de plan et d'approbation (`[rac-33]` → D4, D5).
  3. Retirer les références périmées : `#if EDITOR` `[rac-34]` ; Bullet/Jolt (`[gh-09]`, pour le rappel physique).
  4. Ajouter les règles nouvelles : « ne rien inventer, ne rien supposer, demander » avec son mécanisme (D4, D13 ; couverture partielle existante `[rac-26]` `[rac-27]` reprise) ; plan obligatoire et modèle (D5 ; statuts existants `[rac-29]`) ; commit par tâche et branche dédiée (D3) ; langue (D9 ; aucune règle de langue n'existe aujourd'hui `[rac-08]`) ; ADR (D6, T4.1) ; règle de jumelage « toute règle par chemin est écrite dans `.github/instructions/` **et** `.claude/rules/` » (D1, D8).
  5. Plan du fichier : 1 Objet et périmètre · 2 Principes (ne rien inventer, petites modifications sûres, pas de refactor hors tâche, architecture préservée) · 3 Workflow d'une tâche (questions groupées → plan → exécution → blocage → rapport) · 4 Git et commits · 5 Langue · 6 Build, tests, samples · 7 Outils shell · 8 Délégation, avec la règle de modèle des sous-agents (P4) · 9 Règles moteur (priorités, hot path, rendu et état GPU, MGUI, physique, assets et sérialisation, API, séparation runtime/éditeur, erreurs, style, reverse engineering) · 10 Documentation et ADR (`docs/decisions/` est créé en T4.1 ; le lien est vérifié au critère 4 en T6.1) · 11 Organisation `docs/` et `ai-agent/` · 12 Rapport de fin de tâche (un seul gabarit, celui de « Completion report », `[rac-19]`).
  6. Les règles propres à un chemin (rendu, MGUI, physique, éditeur, samples) ne gardent dans `AGENTS.md` qu'un rappel d'une ligne ; le détail va dans `.github/instructions/` et `.claude/rules/` (T2.1, T2.2), et la table de l'étape 1 le trace.
  7. Cible : 200 lignes (recommandation de la doc mémoire Claude Code) ; tolérance jusqu'à 250 lignes si les sections nouvelles (workflow, git, ADR) l'exigent, avec le compte final et le motif notés dans la validation ; le contenu par chemin part dans les règles.
- Validation : la table de correspondance est complète (chaque section source, et chaque puce normative des sections non migrées telles quelles, a une destination ; aucune ligne « supprimée » sans motif ; contrôle énumératif : chaque puce normative de `copilot-instructions.md` est retrouvée dans `AGENTS.md`, dans un fichier de `.github/instructions/` + `.claude/rules/`, ou dans une ligne « supprimée » motivée) ; `rg -c` de chaque règle dédoublonnée renvoie 1 ; aucune des chaînes périmées du critère 5 de la validation globale ; relecture des sections 3 et 4 contre D3 → D5.
- Commit : `docs(ai): make AGENTS.md the single source of agent rules`
- Note de validation (2026-09-06) : `wc -l AGENTS.md` = 143 (cible 200) ; `rg` des chaînes périmées du critère 5 et de `rtk` : 0 ; `LINQ` 1 occurrence, « Ne rien inventer » 1 ; vérificateur indépendant (contexte neuf, modèle opus) : **CONFIRMED**, 100 % des puces normatives des deux anciens fichiers retrouvées (≈ 200 reprises, ≈ 55 affectées aux règles par chemin à créer en T2.1/T2.2, ≈ 16 supprimées avec motif, 0 absente, 0 déformée), aucune règle sans source ; ses deux remarques textuelles intégrées (formulation des sous-modules en §1, « code clair plutôt qu'astucieux » en §9.11) et la ligne « Contenu nouveau » de la table complétée. Faits du dépôt cités : les deux solutions listent leurs projets (`rg 'Project\(' *.sln`), `CasaEngine.Tests` n'est dans aucune des deux, `IPhysicsWorld` existe.
- Table de correspondance (écrite avant la réécriture ; `§n` = section du nouvel `AGENTS.md`, `rules/<nom>` = `.github/instructions/<nom>.instructions.md` + `.claude/rules/<nom>.md`, remplis en T2.1 et T2.2) :

  | Source (`.github/copilot-instructions.md`) | Contenu | Destination |
  |---|---|---|
  | l.3 | nature du dépôt | §1 |
  | l.5-10 | programmeur moteur prudent, 5 puces | §2 puce 2 |
  | l.14-20 | pas de délégation par défaut, 4 conditions | §8 puce 1 |
  | l.22-23 | ce qui reste en session principale | §8 puce 2 |
  | l.25-29 | rôles scout, mech-executor, executor, verifier | §8 puce 3 (P4) |
  | l.34, 37, 39-42 | Windows, outils rg, fd, jq, yq, ast-grep | §7 |
  | l.38, 50, 56-63, 67-68 | tout ce qui concerne `rtk` | supprimée : outil désinstallé (`command -v rtk` absent ; `[rac-12]`, `[rac-22]` → `[rac-25]`) |
  | l.46 | outils déterministes plutôt que deviner | §2 puce 4 |
  | l.49, 51-54 | usage de rg, fd, jq, yq, ast-grep | §7 |
  | l.65-66, 69-72 | vérification des outils en début de tâche | §7 (facultative, `[rac-25]`) |
  | l.74-83 | listages interdits, commandes filtrées | §7 |
  | l.90-93 | avant de coder : inspecter, patterns, existant, minimum de fichiers | §2 puce 4 |
  | l.94 | pas de refactor large | §2 puce 3 (fusion `[rac-16]`) |
  | l.97 | changements bornés à la tâche | §2 puce 3 |
  | l.98-99 | préserver et ne pas renommer les API publiques | §9.8 (fusion `[rac-13]`) |
  | l.100 | pas de changement d'architecture en silence | §9.2 puce 3 (fusion `[rac-15]`) |
  | l.101 | pas de dépendance sans justification | §2 puce 5 (fusion avec `AGENTS.md:31-32`, `[rac-07]`) |
  | l.102 | code clair plutôt qu'astucieux | §9.11 |
  | l.105-106 | build et tests après codage | §6 (P1) |
  | l.107-109 | rapport : fichiers, commandes, risques et hypothèses | §12 (fusion `[rac-19]`) |
  | l.115-125 | priorités 1 → 7 | §9.1 |
  | l.131-147 | périmètre : autorisé, interdit, documenter l'amélioration hors tâche | §2 puce 3 (l.142 fusion `[rac-14]`, l.144 fusion `[rac-13]`) |
  | l.153 | moteur, pas framework générique | §1 puce 1 |
  | l.155-162 | architecture de moteur, 7 puces | §9.2 puce 1 (l.156 fusion §9.9, l.159 fusion §9.3 `[rac-17]`, l.160 fusion §9.7 `[rac-14]`) |
  | l.164-173 | pas d'abstraction de service, frontières de backend | §9.2 puce 2 |
  | l.179-185, 187, 189 | séparation éditeur/runtime | §9.9 |
  | l.186 | `#if EDITOR` | supprimée : périmée `[rac-34]` |
  | l.195-200 | API publique, rupture | §9.8 (P2) |
  | l.201-203 | champs sérialisés stables | §9.7 (fusion `[rac-14]`) |
  | l.205-206 | doc XML courte | §9.8 |
  | l.207 | extrait d'usage | §10 puce 1 (fusion `[rac-20]`) |
  | l.213-223 | liste des chemins chauds | §9.3 |
  | l.227-245 | allocations interdites et alternatives | §9.3 |
  | l.250-254 | perf de rendu : Begin/End, états, textures, batching, pas d'allocation | rules/rendering + rappel §9.4 |
  | l.255 | restaurer l'état du GraphicsDevice | §9.4 puce 1 (formulation canonique, fusion `[rac-06]`) |
  | l.261-283, 287-297, 302-306, 310-317 | MGUI : layout, input, clipping, temps réel | rules/mgui-framework + rappel §9.5 (l.304 fusion §9.4 `[rac-06]` ; l.313-314 fusion §9.3 `[rac-05]` `[rac-17]`) |
  | l.323-332 | données, pipeline, backend | §9.4 puce 2 + rules/rendering |
  | l.335-336 | pas de fuite d'état, restaurer les états | §9.4 puce 1 |
  | l.337-340, 342-351 | passes explicites, fallback, paramètres material, structures extensibles | rules/rendering |
  | l.357-370 | physique : propriété du transform, interfaces, debug draw, pas fixe, modification prudente | §9.6 (résumé) + rules/physics (détail) |
  | l.376-385, 388-391 | assets et sérialisation, importeurs | §9.7 |
  | l.397-411 | quand tester, validation préférée | §6 puce 2 |
  | l.413 | ne rien affirmer sans build, test ou lecture du code | §2 puce 1 (fusion D13, `[rac-26]`) |
  | l.419-433 | documentation : quand, forme | §10 puce 1 |
  | l.439-442 | git via rtk | supprimée : outil désinstallé |
  | l.445 | commit seulement sur demande | remplacée par D3 (§4 puce 2, `[rac-01]` `[rac-31]`) |
  | l.446-449 | commit buildable, message explicite, pas de push, pas de changement étranger | §4 |
  | l.450 | inspecter le diff (`rtk git diff`) | §4 puce 4 (`git diff`) |
  | l.452-455 | modifications préexistantes de l'auteur | §4 puce 5 |
  | l.461-470, 473-474 | style C#, code moteur | §9.11 |
  | l.475 | pas de réflexion | §9.3 (fusion `[rac-18]`) |
  | l.476 | pas d'état global mutable | §9.2 puce 3 |
  | l.477 | ordre d'update explicite | §9.2 puce 1 (fusion `[rac-18]`) |
  | l.483-491 | gestion des erreurs | §9.10 |
  | l.497-513 | reverse engineering et portage | §9.12 |
  | l.519-535 | rapport de fin | §12 |

  | Source (`AGENTS.md` ancien) | Contenu | Destination |
  |---|---|---|
  | l.3 | s'applique à tous les agents | §1 (en-tête) |
  | l.5-11 | domaines de travail | §1 puce 2 |
  | l.14 | commits fréquents par sous-tâche | §4 puce 2 (D3) |
  | l.15 | pas de rupture d'API sans compat | §9.8 (P2) |
  | l.16 | hot path sans allocation ni LINQ | §9.3 (fusion `[rac-05]`) |
  | l.17 | restaurer l'état du GraphicsDevice | §9.4 (fusion `[rac-06]`) |
  | l.18 | sample minimal obligatoire | §6 puce 3 (P3) |
  | l.19-24 | statuts de plan | §3 puce 3 + `ai-agent/plan-template.md` |
  | l.27 | build avant de terminer | §6 puce 1 (P1, `[rac-36]`) |
  | l.28 | lancer le sample de la zone | §6 puce 3 |
  | l.31-32 | dépendances | §2 puce 5 (`[rac-07]`) |

  Contenu nouveau, sans source dans les anciens fichiers : en-tête et §1 puce 3 (D2, D8, P10 ; sous-modules : fait git, et mémoire de session « ne pas toucher MGUI/ et NvgSharp/ lors d'opérations de masse »), §3 (D4, D5, D13), §4 puces 1, 3 et 4 (D3, D11, règle d'exécution du plan « indexer fichier par fichier »), §5 (D9), §6 commande de test (fait du dépôt : `CasaEngine.Tests/CasaEngine.Tests.csproj`, absent des deux solutions), §8 puce 4 (P4), §10 puce 2 (D6), §11 (`ai-agent/README.md` et `docs/README.md`), §11 puce 4 (D8).

### ✅ T1.3 — Brancher `CLAUDE.md` et `.github/copilot-instructions.md` sur `AGENTS.md`

- Objectif : les deux outils lisent `AGENTS.md` sans duplication de contenu (D2).
- Fichiers : `CLAUDE.md`, `.github/copilot-instructions.md`.
- Étapes :
  1. `CLAUDE.md` : première ligne `@AGENTS.md` (forme documentée, doc mémoire Claude Code), puis une section `## Claude Code` de quelques lignes : où sont `.claude/rules/`, `.claude/agents/`, `.claude/skills/`, `.claude/settings.json`, et le rappel que les hooks appliquent D3 (les chemins `.claude/` sont créés en phases 2 et 3 ; la section les annonce, T2.5 et T3.1 la complètent).
  2. `.github/copilot-instructions.md` : remplacer les 535 lignes par un pointeur (P9) : « Les règles de ce dépôt sont dans `AGENTS.md` à la racine, lu automatiquement ; les règles par chemin sont dans `.github/instructions/`. » (La phrase sur les skills est ajoutée en T2.5, quand `.claude/skills/` existe.)
- Validation : `rg -n "^@AGENTS.md" CLAUDE.md` renvoie la ligne 1 ; `wc -l .github/copilot-instructions.md` ≤ 10 ; contrôle du chargement reporté au critère 1 de la validation globale (🧪 si la session ne peut pas être redémarrée).
- Commit : `docs(ai): point CLAUDE.md and copilot-instructions at AGENTS.md`
- Note de validation (2026-09-06) : `CLAUDE.md` ligne 1 = `@AGENTS.md` (forme documentée, doc mémoire Claude Code) suivie d'une section « Claude Code » de 5 lignes ; `.github/copilot-instructions.md` réduit à 5 lignes (pointeur, P9), les 535 lignes d'origine ayant été tracées dans la table de T1.2. Le contrôle de chargement effectif (`/context` dans une session neuve) est reporté au critère 1 de la validation globale (T6.1).

---

## Phase 2 — Règles par chemin, agents et skills

### ✅ T2.1 — Corriger les instructions par chemin `.github/instructions/*.instructions.md`

- Objectif : des règles par chemin exactes, sans doublon avec `AGENTS.md`, aux globs vivants (D7).
- Fichiers : les 6 fichiers de `.github/instructions/`.
- Étapes :
  1. `applyTo` : retirer `CasaEngine.EditorUI/**`, `Editor/**` `[gh-07]`, `CasaEngine.SimpleEditor/**` `[gh-12]`, `ThirdParties/**`, `CasaEngine/**/Physic/**`, `CasaEngine.Demos/**/Physics/**` `[gh-08]`, `CasaEngine/**/Graphics/**`, `CasaEngine/**/Effects/**` `[gh-11]` ; ajouter les chemins réels vérifiés par `fd` (`CasaEngine/Engine/Physics/**`, `CasaEngine/Framework/Physics/**`, `CasaEngine/Framework/Application/Components/Physics/**`).
  2. Contenu : `physics.instructions.md` → backend bepuphysics2, retirer Bullet/Jolt `[gh-09]` et les interfaces inexistantes `IBody`/`IShape`/`IConstraint` `[gh-10]`, garder `IPhysicsWorld` ; `editor-mgui.instructions.md` → retirer `EditorUI`/WPF `[gh-13]` ; `rendering.instructions.md` → retirer « forward/deferred » `[gh-14]`, décrire les passes réelles (vérifiées par `rg` dans `CasaEngine/Framework/Rendering/`).
  3. Frontmatter uniforme : `name`, `description`, `applyTo` (doc VS Code).
  4. Retirer de chaque fichier les règles déjà dans `AGENTS.md` (`[gh-21]` `[gh-22]` `[gh-26]` `[gh-28]`) ; ne garder que ce qui est propre au chemin. Tracer chaque retrait dans la table de T1.2.
- Validation : chaque segment de chaque glob `applyTo` matche au moins un fichier (`fd`) ; `yq` parse les frontmatters ; critère 5 de la validation globale sur `.github/instructions/`.
- Commit : `docs(copilot): fix stale globs and backends in the path instructions`
- Note de validation (2026-09-06) : `yq --front-matter=extract` lit `name` et `applyTo` des 6 fichiers ; chaque segment de glob matche des fichiers (`fd` : Editor 142, EditorServices 72, GizmoTool 9, MGUI 482, Engine/Physics 13, Framework/Physics 14, Components/Physics 5, CasaEngine.Shaders 20, Content/Shaders 13, Framework/Rendering 118, Framework/Materials 31, Particles/Rendering 3, Demos 356, Projects 1269) ; `rg` des chaînes périmées, de `WPF`, `IBody`, `IShape`, `IConstraint`, `deferred` : aucune. Contenu migré depuis l'ancien `copilot-instructions.md` selon la table de T1.2 : l.250-254, 323-332, 337-351 → `rendering` ; l.261-317 → `mgui-framework` ; l.357-370 → `physics`. Passes de rendu citées vérifiées dans le code (`RenderPipeline`, `ForwardRenderPipeline`, `RenderPass`, `OpaquePass`, `TransparentPass`, `ShadowPass`, `SkyPass`) ; backend Bepu sous `CasaEngine/Framework/Physics/Bepu/`. Globs ajoutés : `CasaEngine/Content/Shaders/**`, `CasaEngine/Framework/Particles/Rendering/**` (dossiers réels). Doublons avec `AGENTS.md` retirés (`[gh-21]` `[gh-22]` `[gh-26]` `[gh-28]`).

### ✅ T2.2 — Créer les règles par chemin jumelles `.claude/rules/*.md`

- Objectif : les mêmes règles par chemin pour Claude Code (D8), au format documenté (`paths:` liste de globs, doc mémoire Claude Code).
- Fichiers : `.claude/rules/csharp-monogame.md`, `editor-mgui.md`, `mgui-framework.md`, `physics.md`, `rendering.md`, `samples.md` (nouveaux).
- Étapes :
  1. Un fichier par instruction Copilot, même corps, frontmatter `paths:` reprenant les globs de `applyTo` (syntaxe `**`, accolades supportées).
  2. En tête de chaque fichier, un commentaire HTML `<!-- Jumeau de .github/instructions/<nom>.instructions.md : modifier les deux. -->` (les commentaires HTML de bloc sont retirés du contexte, doc mémoire Claude Code).
  3. Aucun fichier de `.claude/rules/` sans `paths` : une règle sans `paths` serait chargée au lancement comme `CLAUDE.md` et ferait doublon avec `AGENTS.md`.
- Validation : `yq` parse ; chaque fichier a un `paths` non vide ; les corps sont identiques à leurs jumeaux au commentaire et au frontmatter près (`diff` après suppression des frontmatters).
- Commit : `docs(claude): add path-scoped rules mirroring the Copilot instructions`
- Note de validation (2026-09-06) : 6 fichiers générés par un script déterministe (dossier de travail de la session, `mirror_rules.py`) qui convertit `applyTo` en liste `paths:` et copie le corps tel quel ; `yq --front-matter=extract '.paths | length'` : 1, 3, 1, 3, 5, 2 (aucun fichier sans `paths`) ; comparaison des corps hors frontmatter et hors commentaire de jumelage : identiques pour les 6. Format `paths:` : doc mémoire Claude Code (liste YAML de globs).

### ✅ T2.3 — Nettoyer et harmoniser les agents Copilot `.github/agents/`

- Objectif : supprimer l'agent périmé, uniformiser les six autres (D7).
- Fichiers : suppression de `.github/agents/engine-developer.md` ; édition des 6 `*.agent.md`.
- Étapes :
  1. `git rm .github/agents/engine-developer.md` : son chantier est livré (`UIRoot`, `ScreenStack`, `ViewMouseViewport`, `InputRouter`, `WorldUIComponent` existent, `[gh-03]` `[gh-04]`), il contient un résidu de citation `[gh-05]` et une configuration `DebugEditor` inexistante `[gh-06]`.
  2. Frontmatter uniforme (`[gh-16]` `[gh-17]`) : `name` (= nom de fichier sans suffixe), `description` (obligatoire), `tools` en tableau YAML d'identifiants documentés par VS Code (`search`, `edit`, `web`, `agent`, `read/terminalLastCommand`, `web/fetch`, `search/codebase`, `search/usages` ; les valeurs actuelles `workspace`, `terminal`, `code_search`, `git` ne sont pas documentées). La liste exacte est relue sur https://code.visualstudio.com/docs/agent-customization/custom-agents au moment de l'exécution ; si un identifiant nécessaire n'y figure pas, la tâche passe en ⚠️.
  3. Contenu : `physics-integration.agent.md` → bepuphysics2, retirer Bullet/Jolt `[gh-09]` ; `rendering-pipeline.agent.md` → retirer « forward/deferred » `[gh-14]` ; titres de sections uniformes `## Mission`, `## Règles`, `## Workflow`, `## Done` `[gh-19]` ; français partout `[gh-18]` ; retirer les règles déjà dans `AGENTS.md` (`[gh-21]` → `[gh-27]`).
  4. Chaque agent renvoie au workflow d'`AGENTS.md` (plan, commit par tâche, ne rien inventer) au lieu de le reformuler (`[gh-01]` `[gh-02]` `[gh-20]`).
- Validation : `yq` parse ; `rg -l "Neoforce|Bullet|Jolt|DebugEditor|contentReference" .github/agents` vide ; `fd . .github/agents` renvoie 6 fichiers.
- Commit : `docs(copilot): remove the delivered engine-ui agent and harmonize the agent files`
- Note de validation (2026-09-06) : `engine-developer.md` supprimé (`git rm`) ; 6 fichiers `*.agent.md` restants, frontmatter `name` (= nom de fichier) + `description` lus par `yq` ; champ `tools` omis (voir O4 : la doc VS Code ne fournit pas la liste complète des identifiants, et la doc GitHub documente l'omission comme « tous les outils ») ; sections uniformes `## Mission`, `## Règles` ou `## Points d'attention`, `## Workflow`, `## Done` ; chaque workflow renvoie au workflow d'`AGENTS.md` (plan, commit par tâche, ne rien inventer) et chaque agent renvoie à son instruction par chemin ; `rg` : plus aucune mention de Neoforce, Bullet, Jolt, DebugEditor, contentReference, WPF, forward/deferred, ni des identifiants d'outils non documentés.

### ✅ T2.4 — Créer les sous-agents jumeaux `.claude/agents/*.md`

- Objectif : les six agents de domaine disponibles pour Claude Code (D8, P11).
- Fichiers : `.claude/agents/build-ci.md`, `editor-mgui.md`, `gameplay-samples.md`, `mgui-framework.md`, `physics-integration.md`, `rendering-pipeline.md` (nouveaux).
- Étapes :
  1. Même corps que le jumeau Copilot ; frontmatter documenté (doc sous-agents Claude Code) : `name`, `description`, `tools` (noms d'outils Claude : `Read`, `Glob`, `Grep`, `Edit`, `Write`, `Bash` ; liste relue sur https://code.claude.com/docs/en/sub-agents.md au moment de l'exécution, ⚠️ si un nom nécessaire n'y figure pas), avec `model: sonnet` explicite (P4 : jamais omis, jamais `inherit`, car un champ absent retombe sur le modèle de la session).
  2. Commentaire HTML de jumelage comme en T2.2.
  3. Le corps commun rappelle D13 (ne rien inventer, s'arrêter et demander) et le workflow d'`AGENTS.md`.
- Validation : `yq` parse ; les corps sont identiques aux jumeaux hors frontmatter et commentaire.
- Commit : `docs(claude): add project subagents mirroring the Copilot agents`
- Note de validation (2026-09-06) : 6 fichiers générés par script déterministe (`mirror_agents.py`, dossier de travail de la session) ; frontmatter `name`, `description`, `tools: Read, Glob, Grep, Edit, Write, Bash` (chaîne séparée par des virgules et noms d'outils relus sur https://code.claude.com/docs/en/sub-agents.md le 2026-09-06), `model: sonnet` explicite (P4) ; corps identiques aux jumeaux Copilot hors frontmatter et commentaire (comparaison Python) ; le corps contient déjà la règle « ne rien inventer » et le renvoi au workflow d'`AGENTS.md` (T2.3).

### ✅ T2.5 — Déplacer les skills vers `.claude/skills/` et les corriger

- Objectif : un seul emplacement de skills, lu par Copilot et Claude Code (P10).
- Fichiers : `git mv .github/skills/<nom>/SKILL.md .claude/skills/<nom>/SKILL.md` pour les 6 skills ; suppression de `.github/skills/` ; `.github/copilot-instructions.md` (ajout de la phrase sur `.claude/skills/`) ; `CLAUDE.md` (section Claude Code : ligne skills).
- Étapes :
  1. Ajouter à chaque `SKILL.md` le frontmatter documenté (`name`, `description`).
  2. Corriger le contenu : `physics-backend-adapter` → bepuphysics2 `[gh-09]` ; `render-pass-scaffold` et `shader-variant-workflow` → retirer « forward/deferred » `[gh-14]` ; `feature-workflow` → renvoyer au workflow d'`AGENTS.md` et au modèle de plan (`[gh-01]` `[gh-02]` `[gh-20]`) ; retirer les règles déjà dans `AGENTS.md` (`[gh-21]` → `[gh-28]`).
- Validation : `fd SKILL.md .claude/skills` renvoie 6 fichiers ; `.github/skills` n'existe plus ; `yq` parse ; le pointeur Copilot et `CLAUDE.md` mentionnent `.claude/skills/`.
- Commit : `docs(ai): move the skills to .claude/skills read by Copilot and Claude Code`
- Note de validation (2026-09-06) : 6 `SKILL.md` déplacés par `git mv` (renommages détectés par git), `.github/skills/` supprimé ; frontmatter `name` + `description` lus par `yq` ; contenus corrigés : `physics-backend-adapter` réécrit pour bepuphysics2 et `IPhysicsWorld` (plus de World/Body/Shape génériques), `render-pass-scaffold` sur les classes réelles `RenderPipeline`/`RenderPass`/`OpaquePass`/`TransparentPass`/`ShadowPass`/`SkyPass`, `feature-workflow` renvoie au workflow d'`AGENTS.md` et aux skills `plan` et `adr`, doublons avec `AGENTS.md` et les règles par chemin retirés ; `rg` : plus de Bullet, Jolt, forward/deferred ; `.github/copilot-instructions.md` et `CLAUDE.md` mentionnent `.claude/skills/`.

### ✅ T2.6 — Ajouter le skill `plan`

- Objectif : rendre exécutables par les deux outils les deux règles nouvelles de l'auteur : « ne rien inventer, ne rien supposer, demander » (D4, D13) et « plan avec statuts à icônes et un commit par tâche » (D3, D5).
- Fichiers : `.claude/skills/plan/SKILL.md` (nouveau).
- Étapes :
  1. Quand l'utiliser : dès que le travail demande plus d'un commit (D5).
  2. Procédure : poser les questions groupées **avant** d'écrire le plan (D4) ; lire `ai-agent/plan-template.md` ; écrire le plan dans `ai-agent/tasks/<sujet>-tasks.md` ; l'ajouter au tableau de `ai-agent/README.md` ; attendre l'approbation ; exécuter tâche par tâche avec le cycle des statuts et un commit par tâche incluant la mise à jour du plan (D3, D5).
  3. Règle explicite « ne rien inventer » : toute règle ou tout fait du plan provient d'un fichier du dépôt, d'une réponse de l'auteur ou d'une doc officielle citée ; sinon ⚠️ Blocked, question dans « Points ouverts », arrêt (D13, D4).
- Validation : `yq` parse ; `rg -n "inventer" .claude/skills/plan/SKILL.md` renvoie au moins une ligne ; chaque fichier cité par le skill existe (`ai-agent/plan-template.md`, `ai-agent/README.md`).
- Commit : `docs(ai): add the plan skill`
- Note de validation (2026-09-06) : `yq` lit `name` et `description` ; `rg -i inventer` renvoie la section « Règle absolue : ne rien inventer, ne rien supposer, demander » (D13, D4) ; fichiers cités existants : `ai-agent/plan-template.md`, `ai-agent/README.md`, `ai-agent/tasks/archive/`, `AGENTS.md` ; la procédure reprend D3 (commit par tâche, branche dédiée, jamais de push), D4 (questions groupées, arrêt sur ⚠️) et D5 (seuil, mise à jour du plan dans le commit).

---

## Phase 3 — Réglages et hooks Claude Code

### ✅ T3.1 — Créer `.claude/settings.json` partagé

- Objectif : permissions communes commitées (D8), refus de push (D3).
- Fichiers : `.claude/settings.json` (nouveau), `.gitignore` (ajout de `.claude/settings.local.json`, aujourd'hui ignoré seulement par le fichier d'exclusion global de l'auteur).
- Étapes :
  1. `permissions.allow`, syntaxe `Bash(<préfixe> *)` (doc permissions) : `rg`, `fd`, `jq`, `yq`, `ast-grep`, `git status`, `git diff`, `git log`, `git branch`, `git add`, `git commit`, `git mv`, `git rm`, `git worktree`, `git checkout -b`, `dotnet build`, `dotnet test`. Aucune entrée `rtk` (désinstallé). `git commit` est autorisé parce que le hook de T3.2 le rend sûr (D3 : l'agent committe seul). Remarque consignée : `fd -x` peut exécuter une commande arbitraire ; le hook de T3.2 inspecte toute la ligne de commande et refuse `git push` où qu'il apparaisse.
  2. `permissions.deny` : `Bash(git push *)` (D3). Les règles `deny` s'appliquent même avant l'acceptation du dossier (doc permissions).
  3. Ne rien recopier de `settings.local.json` (personnel).
  4. `env.CLAUDE_CODE_SUBAGENT_MODEL = "sonnet"` (P4 ; doc sous-agents : troisième niveau de l'ordre de résolution du modèle, avant le modèle de la session).
- Validation : `jq . .claude/settings.json` valide ; `git push --dry-run` lancée par l'agent est refusée par la règle `deny` ; `git check-ignore -v .claude/settings.local.json` cite désormais le `.gitignore` du dépôt.
- Commit : `chore(claude): add the shared project settings with permission rules`
- Note de validation (2026-09-06) : `jq` valide le fichier (17 règles `allow`, `deny` = `Bash(git push *)`, `env.CLAUDE_CODE_SUBAGENT_MODEL` = `sonnet`) ; `git push --dry-run origin ai-guidelines` lancée depuis l'outil Bash de la session a été **refusée** par la règle `deny`, prise en compte sans redémarrage ; `git check-ignore -v .claude/settings.local.json` cite `.gitignore:64` ; `.gitignore` conservé en CRLF (fins de ligne du fichier existant). Syntaxe des règles : doc permissions Claude Code (forme `Bash(<préfixe> *)`, évaluation deny → ask → allow).

### ⏳ T3.2 — Hook : interdire tout commit sur `main`

- Objectif : appliquer D3 par un mécanisme déterministe (la doc mémoire Claude Code précise que `CLAUDE.md` est du contexte, pas une contrainte ; les hooks en sont une).
- Fichiers : `.claude/hooks/block-commit-on-main.ps1` (nouveau), `.claude/settings.json` (section `hooks`).
- Étapes :
  1. Configuration (doc hooks) : `hooks.PreToolUse[0].matcher = "Bash"`, handler `{ "type": "command", "command": "powershell.exe", "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/block-commit-on-main.ps1"], "timeout": 10 }`, **sans** filtre `if` : le script reçoit toutes les commandes Bash et décide lui-même, afin de reconnaître aussi `git -C <chemin> commit`, les commandes chaînées (`cd <chemin> && git commit`) et les wrappers.
  2. Script : lit le JSON sur stdin et découpe `tool_input.command` en segments (`&&`, `||`, `;`, `|`) puis en tokens. Dans chaque segment, pour chaque token `git` (en tête, ou après `-x`/`--exec` d'un `fd`, ou n'importe où ailleurs, y compris après un wrapper), la **sous-commande** est le premier token suivant qui n'est ni une option globale ni la valeur d'une option globale (`-C <chemin>`, `-c <clé>=<valeur>`, `--git-dir=…`, `--work-tree=…`, `--no-pager`, …). Décision : sous-commande `push` → deny (D3, raison « Push interdit sans demande explicite (AGENTS.md, D3) ») ; sous-commande `commit` → déterminer le dépôt visé : `<chemin>` d'un `-C` s'il est présent, sinon le dernier `cd <chemin>` du même segment ou d'un segment précédent, sinon `cwd` ; si `--git-dir` ou `--work-tree` est présent, le dépôt est « non résolu » → deny (raison « dépôt cible non résolu ») ; sinon exécuter `git -C <chemin> branch --show-current` ; si le résultat est `main`, écrire `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Commit sur main interdit : crée une branche dédiée (AGENTS.md, D3)."}}` et sortir en 0 ; dans tous les autres cas, sortir en 0 sans sortie.
  3. Test unitaire du script sans changer de branche dans l'arbre principal : vérifier d'abord `git worktree list` (la branche `main` ne doit être extraite nulle part, sinon ⚠️), puis `git worktree add <dossier de travail de la session>/wt-main main` ; exécuter le script avec des JSON forgés : `git commit -m x` avec `cwd` = worktree (attendu : deny), `git -C <wt-main> commit -m x` avec `cwd` = dépôt principal (deny), `cd <wt-main> && git commit -m x` (deny), `git commit -m x` avec `cwd` = dépôt principal sur `ai-guidelines` (aucune sortie), `git status` (aucune sortie), `git -C <chemin> push` (deny), `fd . -x git push` (deny), `git --git-dir=<x> commit -m x` (deny, dépôt non résolu). Aucun commit n'est effectué.
  4. Test de bout en bout du câblage : après écriture de `settings.json`, lancer depuis l'outil Bash de la session `git -C <wt-main> commit --dry-run` (attendu : refus par le hook, visible comme une décision `deny` avec la raison), puis `git commit --dry-run` sur `ai-guidelines` (attendu : la commande s'exécute ; « nothing to commit » est acceptable). Si la session ne recharge pas `settings.json`, la tâche reste 🧪 avec cette procédure écrite pour l'auteur. Puis `git worktree remove <wt-main>`.
- Validation : les huit cas de l'étape 3 donnent le résultat attendu (sorties consignées) ; l'étape 4 montre un déclenchement réel du hook, ou la tâche est 🧪 avec la procédure ; `git branch --show-current` dans l'arbre principal reste `ai-guidelines` ; `git worktree list` ne montre plus le worktree de test.
- Commit : `chore(claude): block commits on main with a PreToolUse hook`

---

## Phase 4 — Architecture Decision Records

### ⏳ T4.1 — Créer `docs/decisions/` : index, modèle, règle

- Objectif : le socle ADR (D6).
- Fichiers : `docs/decisions/README.md`, `docs/decisions/template.md` (nouveaux), `docs/README.md` (section « Decisions »), `AGENTS.md` (section 10 : vérifier la cohérence avec le modèle).
- Étapes :
  1. Modèle en anglais : `# ADR-NNNN: <title>` ; `Status` (Proposed, Accepted, Superseded by ADR-xxxx, Deprecated) ; `Date` ; `Context` ; `Decision` ; `Consequences` ; `Source` (fichier et ligne d'origine pour un rétro-remplissage, ou « this chantier » sinon).
  2. Nommage : `NNNN-short-title.md`, numérotation croissante à quatre chiffres.
  3. `README.md` : tableau `| ADR | Title | Status | Date |`.
- Validation : liens depuis `docs/README.md` résolus ; modèle conforme à D6.
- Commit : `docs(adr): add the architecture decision records folder and template`

### ⏳ T4.2 — Ajouter le skill `adr`

- Objectif : rendre l'écriture d'une ADR exécutable par les deux outils (D6).
- Fichiers : `.claude/skills/adr/SKILL.md` (nouveau).
- Étapes : quand écrire une ADR (toute décision d'architecture, de format d'asset, d'API publique ou de backend, prise pendant un plan ou une discussion) ; lire `docs/decisions/template.md` ; numéroter d'après `docs/decisions/README.md` ; écrire en anglais ; ajouter la ligne d'index ; rappeler D13 (ne pas inventer de contexte : citer la source de la décision).
- Validation : `yq` parse ; chaque fichier cité par le skill existe (`docs/decisions/template.md`, `docs/decisions/README.md`).
- Commit : `docs(ai): add the adr skill`

### ⏳ T4.3 — Inventorier les décisions existantes à rétro-remplir

- Objectif : une liste exhaustive et sourcée avant d'écrire les ADR (P5).
- Fichiers : ce plan (tableau d'inventaire ajouté sous cette tâche).
- Étapes :
  1. Pour chaque source listée dans « État vérifié du dépôt », relever chaque décision : texte, fichier:ligne, date si connue, statut réel dans le code (vérifié par `rg`/`fd` : appliquée, partielle, abandonnée).
  2. Repérer les doublons (`[dec-13]` → `[dec-21]`) pour n'écrire qu'une ADR par décision.
  3. Noter le nombre total et proposer les regroupements éventuels (P5).
- Validation : tableau complet ; chaque ligne a une source vérifiable.
- Commit : `docs(adr): inventory the existing decisions to backfill`

### ⏳ T4.4 — Rétro-remplir les ADR « audio »

- Objectif : une ADR par décision du système audio.
- Sources : `ai-agent/audits/analysis-audio-system.md` D1 → D13 et D2-bis `[dec-01]` `[dec-18]` ; `docs/engine/audio-system.md`.
- Fichiers : `docs/decisions/NNNN-*.md` (nouveaux), `docs/decisions/README.md`, `docs/engine/audio-system.md` (une ligne « Decisions: see ADR-xxxx to ADR-yyyy »).
- Étapes : une ADR par décision (ou par groupe validé en P5), `Source` renseigné, statut `Accepted` si le code l'applique (vérifié en T4.3) ; l'audit n'est pas modifié (D6).
- Validation : index à jour ; chaque ADR cite sa source ; liens résolus.
- Commit : `docs(adr): backfill the audio system decisions`

### ⏳ T4.5 — Rétro-remplir les ADR « collision, physique, Bepu »

- Objectif : une ADR par décision de collision et de physique.
- Sources : `docs/engine/collision-2d-3d-architecture.md` (D1 → D6, posture de compatibilité `[dec-14]`), `ai-agent/audits/analysis-bepuphysics2-migration.md` `[dec-10]`.
- Fichiers : `docs/decisions/NNNN-*.md`, `docs/decisions/README.md`, `docs/engine/collision-2d-3d-architecture.md` (ligne de renvoi).
- Étapes : comme T4.4 ; la posture de compatibilité (« on remplace, on ne double pas ») devient une ADR unique référencée par les deux sources.
- Validation : comme T4.4.
- Commit : `docs(adr): backfill the collision and physics decisions`

### ⏳ T4.6 — Rétro-remplir les ADR « rendu, materials, shaders, tilemaps, PBR »

- Objectif : une ADR par décision de rendu.
- Sources : `docs/engine/shader-naming-convention.md`, `docs/engine/materials-sources-of-truth.md` `[dec-07]` `[dec-19]`, `docs/engine/rendering-2d-3d-spaces.md`, `ai-agent/audits/analysis-tilemap-render-spaces.md` `[dec-21]`, `ai-agent/tasks/pbr-rendering-implementation-plan.md` (« Decisions verrouillees »).
- Fichiers : `docs/decisions/NNNN-*.md`, `docs/decisions/README.md`, lignes de renvoi dans les docs sources de `docs/`.
- Étapes : comme T4.4.
- Validation : comme T4.4.
- Commit : `docs(adr): backfill the rendering and materials decisions`

### ⏳ T4.7 — Rétro-remplir les ADR « UI et éditeur »

- Objectif : une ADR par décision d'UI et d'éditeur.
- Sources : `docs/editor/timeline-generic.md` `[dec-02]`, `docs/editor/animation2d_editor_casaengine.md` `[dec-05]`, `docs/editor/ui-screen-editor/architecture.md` `[dec-06]`, `ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md`, `ai-agent/tasks/play-in-editor-tasks.md`, `ai-agent/audits/analysis-play-in-editor.md` `[dec-17]`, `docs/editor/play-in-editor.md`, `ai-agent/tasks/gltf-import-migration-tasks.md`.
- Fichiers : `docs/decisions/NNNN-*.md`, `docs/decisions/README.md`, lignes de renvoi dans les docs sources de `docs/`.
- Étapes : comme T4.4.
- Validation : comme T4.4.
- Commit : `docs(adr): backfill the UI and editor decisions`

### ⏳ T4.8 — Rétro-remplir les ADR « gameplay »

- Objectif : une ADR par décision de gameplay.
- Sources : `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` `[dec-03]` `[dec-15]`, `docs/engine/coroutines_specifications.md` `[dec-04]`, `docs/engine/yarn_spinner_integration.md` `[dec-16]`, `docs/engine/navigation-engine-features.md`, `docs/engine/gameplay-mode.md`, `docs/engine/character-controller-features.md`, `docs/engine/animation-motion-matching.md`, `docs/engine/animation-deformer-support-policy.md` `[dec-08]` `[dec-09]`, `ai-agent/audits/analysis-possession-gameplay-framework.md` `[dec-10]`.
- Fichiers : `docs/decisions/NNNN-*.md`, `docs/decisions/README.md`, lignes de renvoi dans les docs sources de `docs/`.
- Étapes : comme T4.4.
- Validation : comme T4.4.
- Commit : `docs(adr): backfill the gameplay decisions`

### ⏳ T4.9 — Rétro-remplir les ADR « architecture et organisation », dont celles de ce chantier

- Objectif : une ADR par décision d'architecture globale, plus les décisions D1 → D13 de ce chantier.
- Sources : `ai-agent/audits/CasaEngine_layering_project_split_evaluation.md`, `ai-agent/audits/CasaEngine_folder_hierarchy_namespace_compatibility.md`, `ai-agent/audits/structure-analyze-tasks.md` `[dec-10]` `[dec-20]` (règle de couches Core ← Engine ← Framework, découpage en assemblies différé) ; ce plan (D1 → D13 : source unique `AGENTS.md`, ADR, langue, commit par tâche, plan obligatoire).
- Fichiers : `docs/decisions/NNNN-*.md`, `docs/decisions/README.md`.
- Étapes : comme T4.4.
- Validation : comme T4.4.
- Commit : `docs(adr): backfill the architecture decisions and record this chantier's own`

---

## Phase 5 — Index et docs périmées

### ⏳ T5.1 — Remettre les index à jour

- Objectif : des index complets et datés (D10).
- Fichiers : `docs/README.md`, `ai-agent/README.md`.
- Étapes :
  1. `docs/README.md` : ajouter `engine/collision-2d-3d-architecture.md` et `engine/dialogue-choices-and-bitmap-fonts.md` `[dec-29]` ; section « Decisions » vers `decisions/README.md` (créée en T4.1).
  2. `ai-agent/README.md` : ajouter `audits/analysis-play-in-editor.md`, `audits/analysis-audio-system.md` `[plans-22]` `[dec-30]` et `audits/analysis-ai-agent-files.md` ; ajouter ce plan au tableau ; renvoi vers `plan-template.md` ; mettre à jour la date « État constaté le … » `[plans-26]` `[dec-31]` et les lignes du tableau dont le statut a changé (vérifié plan par plan) ; corriger l'affirmation « chaque fichier utilise la légende » `[plans-19]` ; marquer « historique » dans l'index les audits qui décrivent un état disparu (constat non conclu de l'audit : Bullet/ThirdParties, `CasaEngine.WithEditor.csproj`, `Editor/Editor.csproj`, `EditorUI`) sans modifier les audits eux-mêmes (D6) ; l'archive reste décrite en prose.
  3. Comparer `fd . ai-agent/tasks -d 1 -e md` et `fd . ai-agent/audits -e md` aux liens des deux index et ajouter tout fichier manquant, au-delà de ceux nommés ci-dessus (critère 6).
- Validation : critère 6 de la validation globale (comparaison `fd` vs liens) ; aucun lien mort.
- Commit : `docs: fix the documentation and ai-agent indexes`

### ⏳ T5.2 — Corriger les références périmées dans `docs/`

- Objectif : plus aucune référence à un fichier, chemin ou projet disparu dans les docs listées par l'audit (D10).
- Fichiers (`[dec-32]` → `[dec-37]`) : `docs/engine/material-hot-reload-flow.md`, `docs/engine/casaengine-mgui-backend.md`, `docs/engine/environment-system-v1.md`, `docs/editor/ui-screen-editor/architecture-entry-points.md`, `docs/engine/collision-2d-3d-architecture.md`, `docs/engine/character-controller-features.md`, `docs/engine/world-message-bus-migration-notes.md`, `docs/editor/graph_node_architecture_recommendation.md`, `docs/engine/dialogue-choices-and-bitmap-fonts.md`.
- Étapes :
  1. `Game1` / `CasaEngine.Editor/Game1.cs` → `CasaEngine.Editor/GameEditor.cs` `[dec-34]` (le fichier existe).
  2. Chemins d'avant la réorganisation (`Framework/Game/CasaEngineGame.cs`, `Framework/Entities/Components/…`) → chemins réels trouvés par `fd` `[dec-35]`.
  3. Liens vers `plan-e3-collisions.md` `[dec-33]`, `ai-agent/material-preview-smoke.txt`, `MGUI/docs/graph_node_system_agent_plan.md` `[dec-37]` : retirer ou pointer vers le fichier réel s'il existe sous un autre nom (`fd`).
  4. `CasaEngine.AISamples` dans les commandes de validation `[dec-36]` → projet réel ou retrait de la commande.
  5. Identifiant `D-E12-5` `[dec-32]` : remplacer par la référence de l'ADR correspondante créée en phase 4, ou retirer si aucune décision ne correspond (⚠️ si doute).
  6. Toute mention de Bullet, `ThirdParties/` ou `EditorUI` dans ces fichiers est soit une référence à corriger, soit un rappel historique qui doit être daté et au passé.
- Validation : liens résolus ; critère 5 de la validation globale sur les fichiers de cette tâche.
- Commit : `docs: fix stale file references in the engine and editor docs`

### ⏳ T5.3 — Résoudre les contradictions entre docs après vérification dans le code

- Objectif : une seule affirmation vraie par sujet (D10, D13).
- Fichiers (`[dec-22]` → `[dec-27]`) : `docs/engine/materials-workflow.md`, `docs/engine/materials-sources-of-truth.md`, `docs/engine/particle-system-v1-v2-migration.md`, `docs/engine/rendering-2d-3d-spaces.md`, `docs/engine/navigation-engine-features.md`, `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md`, `docs/engine/character-controller-features.md`, `docs/editor/animation2d_editor_casaengine.md`.
- Étapes : pour chaque contradiction, établir le fait dans le code (`rg` sur `CompiledMaterial` dans le draw path `[dec-22]` ; `BasicEffect` dans le renderer de particules `[dec-23]` ; existence de `Camera3dIn2dAxisComponent` `[dec-24]` (pour ce constat, `rendering-2d-3d-spaces.md` est déjà exacte et le fichier en désaccord est un audit en lecture seule : aucune correction, mention dans la note de validation) ; `NavigationAgentComponent` `[dec-25]` ; `CharacterControllerComponent` `[dec-26]` ; périmètre V1 de l'éditeur Animation2D `[dec-27]`), corriger la doc fautive, dater la correction. Si le code ne tranche pas, ⚠️ et question.
- Validation : chaque correction cite le fichier de code qui l'établit, dans la note de validation.
- Commit : `docs: resolve contradictions between engine docs after code verification`

### ⏳ T5.4 — Écrire `docs/engine/animation2d-composed-format-v1.md` (P6)

- Objectif : livrer la doc annoncée par deux plans `[plans-23]`.
- Fichiers : `docs/engine/animation2d-composed-format-v1.md` (nouveau, anglais), `docs/README.md`.
- Étapes : décrire le format composé V1 à partir des types `Animation2dData`, `Animation2dPartData`, `Animation2dTrackData`, `Animation2dCompositionSampler` et de la sérialisation `.anim2d` (fichiers trouvés par `fd` sous `CasaEngine/Framework/Assets/Animations/`) ; ne documenter que ce que le code fait.
- Validation : chaque champ documenté existe dans le code (note de validation : fichier:ligne par champ) ; lien depuis `docs/README.md`.
- Commit : `docs(animation): document the composed animation2d format v1`

### ⏳ T5.5 — Réconcilier les plans périmés `ui-integration.md` et `static-model-import-tasks.md` (P7)

- Objectif : des plans actifs qui décrivent l'état réel du code `[plans-24]` `[plans-25]`.
- Fichiers : `ai-agent/tasks/ui-integration.md`, `ai-agent/tasks/static-model-import-tasks.md`, `ai-agent/README.md`.
- Étapes : pour chaque case ou tâche, vérifier dans le code (types cités, `fd`/`rg`) et cocher ou marquer ✅ ce qui existe ; `[ARCHIVE]` et déplacement vers `tasks/archive/` si tout est livré, sinon liste courte de ce qui reste ; mettre à jour le tableau du README.
- Validation : aucune référence à un type ou dossier inexistant dans les deux plans (`rg` sur les noms cités) ; tableau du README cohérent.
- Commit : `docs(ai): reconcile the ui-integration and static-model-import plans with the code`

---

## Phase 6 — Clôture

### ⏳ T6.1 — Validation globale et clôture du plan

- Objectif : prouver les critères de la « Validation globale » et fermer le chantier.
- Fichiers : ce plan (résultats consignés), `ai-agent/README.md` (tableau).
- Étapes : dérouler les critères 1 → 7 ; consigner chaque résultat sous cette tâche (commande, sortie) ; passer en 🧪 les vérifications manuelles laissées à l'auteur (critères 1 et 2 si non exécutables) ; mettre à jour le tableau de `ai-agent/README.md` ; rédiger le rapport de fin (fichiers changés, validations, hypothèses, risques, suite).
- Validation : les critères 3 → 7 sont vérifiés par commande avec sortie consignée ; les critères 1 et 2 sont ✅ ou 🧪 avec la procédure écrite pour l'auteur.
- Commit : `docs(ai): close the AI-guidelines plan`

Le **merge sur `main` reste une décision humaine** : ne pas merger ni pousser sans demande explicite.

---

## Points ouverts

À trancher à l'approbation (P1 → P12 ci-dessus) ou remontés en ⚠️ pendant l'exécution.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le `CLAUDE.md` global de l'auteur (`~/.claude/CLAUDE.md`, bloc géré par pilotfish) cite un skill `baton-dispatch` introuvable `[rac-35]` ; hors dépôt, signalé seulement. | — |
| O2 | Traduction des 49 docs françaises de `docs/` : plan séparé ultérieur (D9). | — |
| O4 | Champ `tools` des agents Copilot : les pages VS Code « Custom agents » et « Tools » (consultées le 2026-09-06) ne donnent pas la liste complète des identifiants d'outils, et aucun identifiant documenté ne couvre l'exécution de commandes. Choix appliqué en T2.3 : omettre `tools`, ce que la doc GitHub documente comme « accès à tous les outils disponibles ». L'auteur peut restreindre plus tard avec des identifiants relevés dans VS Code (`#` dans le chat). | T2.3 |
| O3 | Le `CLAUDE.md` global de l'auteur borne les questions posées à l'utilisateur (forme `co_discover`, boucle de readiness) `[rac-28]` et ne définit aucun statut de plan `[rac-32]` ; `AGENTS.md` (dépôt) portera D4, D5 et D13 ; hors dépôt, signalé seulement. | — |

## Hors périmètre

- Sous-modules `MGUI/` et `NvgSharp/` (D12).
- Toute modification de code C#.
- Traduction des docs existantes (O2).
- Réécriture des audits existants (D6).
