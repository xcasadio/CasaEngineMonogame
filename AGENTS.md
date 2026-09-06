# AGENTS.md — CasaEngineMonogame

Source unique des règles pour tous les agents IA qui travaillent dans ce dépôt : Claude Code (via `CLAUDE.md`, qui importe ce fichier), GitHub Copilot (qui lit `AGENTS.md` nativement) et tout autre outil lisant `AGENTS.md`. Les règles propres à un chemin sont dans `.github/instructions/` (Copilot) et `.claude/rules/` (Claude Code), en double exemplaire volontaire ; les skills partagés sont dans `.claude/skills/`.

## 1. Objet et périmètre

- CasaEngine est un moteur de jeu C# / MonoGame avec un runtime et un éditeur. Ce n'est pas un framework applicatif générique.
- Domaines de travail : l'éditeur (UI MGUI), le framework UI MGUI, le rendu (3D, skinned mesh, shaders, passes), l'intégration physique (bepuphysics2), les samples de gameplay.
- `MGUI/` et `NvgSharp/` sont des sous-modules git, donc des dépôts séparés avec leurs propres fichiers de règles : une modification y suit leurs règles, se commite dans le sous-module, puis la référence est mise à jour ici. Une opération de masse sur les fichiers de ce dépôt ne les touche pas.

## 2. Principes

- **Ne rien inventer, ne rien supposer, demander.** Toute API, tout fichier, tout comportement affirmé a été vu dans le code, ou vient d'une réponse de l'auteur, ou d'une documentation officielle citée. Ne jamais affirmer qu'une chose fonctionne sans l'avoir buildée, testée ou déduite du code lu. Séparer les faits des hypothèses. Quand une information manque ou que deux sources se contredisent : poser la question, ne pas trancher seul.
- Agir en programmeur moteur prudent : préserver l'architecture existante sauf demande explicite ; petites modifications sûres et testables ; pas d'abstraction inutile ; jamais de refactor hors tâche ; performance en tête dans `Update`, `Draw`, layout, input, rendu et chargement d'assets.
- Faire uniquement la tâche demandée. Autorisé sans demande : nettoyage local requis par la tâche, validation manquante directement liée, tests ou samples directement liés. Interdit sans demande explicite : refactor large, changement d'un système non concerné, de la structure du projet, du format de sérialisation ou de l'architecture, renommage d'API publique, réécriture d'un système qui fonctionne. Une amélioration utile hors tâche se documente, elle ne s'implémente pas.
- Avant de coder : inspecter les fichiers concernés, chercher les patterns existants, vérifier si MonoGame, .NET ou CasaEngine fournit déjà la fonctionnalité, identifier le minimum de fichiers à toucher. Préférer les outils shell déterministes (§7) à la supposition.
- Pas de nouvelle dépendance sans justification forte : expliquer pourquoi, périmètre minimal, solution de repli si possible. Éviter les dépendances lourdes.

## 3. Workflow d'une tâche

1. **Questions groupées.** Avant tout plan ou toute modification, poser en une seule fois toutes les questions dont la réponse change le travail. Ensuite, exécuter en autonomie.
2. **Plan obligatoire dès que le travail demande plus d'un commit.** Le plan est écrit dans `ai-agent/tasks/<sujet>-tasks.md` selon `ai-agent/plan-template.md` (skill `plan`), ajouté au tableau de `ai-agent/README.md`, puis soumis à l'auteur. Aucune modification de code avant son approbation. En dessous du seuil : exécution directe, puis rapport de fin (§12).
3. **Exécution tâche par tâche.** Chaque tâche du plan porte une icône de statut devant son nom : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked. Une seule tâche à la fois : passer en 🚧 avant de commencer, en ✅ (ou 🧪 si une validation manuelle manque) à la fin, et mettre à jour le plan **dans le même commit** que la tâche. Ne jamais laisser une tâche en 🚧 en fin de session.
4. **Blocage.** Information manquante, contradiction, décision à prendre : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts » du plan, et **s'arrêter**. Ne pas contourner, ne pas deviner.
5. **Rapport de fin** (§12) pour toute tâche, avec ou sans plan.

## 4. Git et commits

- **Branche dédiée par chantier**, créée depuis `main`. Jamais de commit sur `main` (un hook Claude Code déclaré dans `.claude/settings.json` l'interdit).
- **Un commit par tâche terminée**, fait par l'agent sans demander, atomique et buildable. Message en anglais, au format `type(area): summary` (`feat`, `fix`, `refactor`, `docs`, `chore`, `perf`, `test`).
- **Jamais de push**, jamais de merge sur `main`, sans demande explicite de l'auteur.
- Inspecter `git diff` avant de committer. Indexer fichier par fichier (`git add <chemin>`), jamais `git add -A` ni `git add .`. Ne jamais committer de changement sans rapport avec la tâche.
- Si l'arbre de travail contient déjà des modifications de l'auteur : ne pas les écraser, ne pas les annuler, ne pas les indexer, les signaler si elles touchent la tâche.

## 5. Langue

- Français : ce fichier, les règles par chemin, les agents, les skills, les plans de `ai-agent/tasks/`, les réponses à l'auteur.
- Anglais : le code (types, membres, commentaires), les messages de commit, les documents de `docs/` et les ADR de `docs/decisions/`.

## 6. Build, tests, samples

- Solutions : `CasaEngine.MonoGame.sln` (moteur, démos, launcher, jeux d'exemple) et `CasaEngine.Editor.MonoGame.sln` (éditeur, services d'édition, compilateurs, shaders, GizmoTool). Build obligatoire avant de marquer une tâche ✅ dès que du code est touché ; si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Tests : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Ajouter ou mettre à jour des tests quand un projet de test existe pour le système touché, quand la tâche change une logique, corrige un bug, change la sérialisation, ajoute un importeur ou un exporteur, ou change le layout, l'input ou le rendu de façon testable. Validation préférée : tests unitaires pour la logique pure, fichiers golden pour l'import et l'export, petite démo ou écran pour une feature éditeur, build complet pour un changement transverse. Si un test ne peut pas être ajouté, dire pourquoi.
- Samples : un sample ou une démo minimal est obligatoire pour toute feature visible non triviale. Si un sample existe pour la zone touchée, le lancer au moins une fois avant de rendre la tâche.

## 7. Outils shell

Le dépôt est développé sous Windows. Outils installés : `rg` (recherche de code), `fd` (découverte de fichiers), `jq` (JSON), `yq` (YAML, XML, INI, CSV), `ast-grep` (recherche structurelle C# quand la recherche textuelle est trop bruyante). Leur vérification en début de tâche (`<outil> --version`) est facultative.

- Recherche : `rg "pattern" CasaEngine --glob "*.cs"` ; `rg "pattern" . --glob "!bin/**" --glob "!obj/**"`.
- Fichiers : `fd "Name" . -e cs` ; `fd . CasaEngine -e cs -d 4`.
- Jamais de listage récursif large : `dir /s`, `tree /f`, `Get-ChildItem -Recurse` sans filtre.

## 8. Délégation à des sous-agents

- Par défaut, ne pas déléguer. Déléguer seulement si au moins une condition tient : exploration à l'échelle du dépôt ; modification mécanique répétitive sur plus de cinq fichiers, entièrement spécifiable ; tâche spécifiable sans ambiguïté d'architecture ; passe de vérification indépendante utile.
- Rester en session principale pour : décisions d'architecture, conclusions de reverse engineering, décisions de rendu, architecture moteur et éditeur, petites modifications localisées.
- Rôles : `scout` pour la découverte large en lecture seule ; `mech-executor` pour les éditions mécaniques entièrement spécifiées ; `executor` pour une implémentation à périmètre et critères de fin clairs ; `verifier` pour tout changement non trivial terminé, avant de le déclarer fait.
- **Modèle des sous-agents.** Un agent qui tourne sur Fable ne lance jamais un sous-agent qui hérite de Fable : chaque sous-agent reçoit un modèle explicite adapté à la tâche, jamais `inherit`, jamais omis. Barème : haiku pour la lecture et la recherche ; sonnet pour l'exécution et l'édition ; opus pour la revue et la vérification. Les agents de projet dans `.claude/agents/` déclarent leur `model`.

## 9. Règles moteur

### 9.1 Priorités en cas d'arbitrage

1. Correction · 2. Stabilité de l'API · 3. Performance runtime · 4. Ergonomie de l'éditeur · 5. Lisibilité · 6. Samples et démos · 7. Nettoyage interne. Ne jamais sacrifier la correction ou la stabilité de l'API à un nettoyage de style.

### 9.2 Architecture

- Architecture de moteur : séparation runtime/éditeur, ordre d'update déterministe et explicite, propriété explicite des objets, états de rendu explicites, input et focus prévisibles.
- Pas d'abstraction de service partout par défaut. Interfaces et adaptateurs seulement sur une vraie frontière de backend : rendu, physique, audio, import et export d'assets, frontière éditeur/runtime. Pas d'abstraction pour un système simple.
- Ne jamais changer l'architecture en silence ; pas d'état global mutable sauf si l'architecture existante l'utilise déjà.

### 9.3 Chemins chauds

Chemins chauds : `Update`, `Draw`, layout, hit-test, input, passes de rendu, pas de physique, update d'animation, update de particules, streaming d'assets. Interdits : LINQ, closures et lambdas capturantes, `foreach` sur un type qui alloue, interpolation ou formatage de chaîne, `List<T>`, `Dictionary<,>`, tableaux ou délégués temporaires, boxing, réflexion, abonnement ou désabonnement d'événement par frame. Préférer : listes conservées avec `Clear()`, buffers réutilisés, pools quand c'est justifié, boucles `for` explicites, chaînes et données de layout précalculées, délégués mis en cache.

### 9.4 Rendu et état GPU

- Toujours restaurer l'état du `GraphicsDevice` après un changement temporaire : `RasterizerState`, `BlendState`, `DepthStencilState`, scissor et clipping, render targets, viewports. Pas d'allocation pendant le rendu.
- Séparer données (materials, meshes, textures, lumières, caméras), pipeline (passes) et backend (`GraphicsDevice`, `Effect`, `RenderTarget2D`). Détail : règle par chemin `rendering`.

### 9.5 MGUI et UI d'éditeur

Toute propriété qui affecte la taille ou la position invalide le layout ; layout et input déterministes ; hit-test respectant z-order, visibilité, état activé et clipping ; capture souris pour le drag ; focus clavier unique ; clipping en Push/Pop avec restauration. Détail : règles par chemin `mgui-framework` et `editor-mgui`.

### 9.6 Physique

Backend : bepuphysics2, derrière `IPhysicsWorld`. Clarifier qui pilote le transform (physique ou gameplay) ; pas de synchronisation bidirectionnelle sans règle ; pas de type du backend dans les API gameplay sauf usage déjà établi ; debug draw pour toute nouvelle feature de collision ; pas fixe déterministe. Inspecter le backend existant avant de le modifier, préserver le comportement, documenter tout risque de migration. Détail : règle par chemin `physics`.

### 9.7 Assets et sérialisation

Les formats d'assets restent stables : pas de renommage de champ sérialisé sans support de migration, pas de changement de structure sans note de migration, pas de changement de format en silence. Préférer les patterns d'assets existants de CasaEngine au JSON générique. Valider les données au chargement et signaler les champs manquants ou invalides avec du contexte. Le chargement runtime ne dépend d'aucune donnée réservée à l'éditeur. Importeurs et exporteurs : parsing déterministe, ordre stable, données identiques d'une exécution à l'autre, exemple d'entrée et de sortie quand c'est possible.

### 9.8 API publique

Ne pas casser une API publique sans demande explicite ; préférer les changements additifs ; ne pas renommer un type, un membre ou un champ sérialisé public sans demande. Si une rupture est demandée ou inévitable : préférer une voie de compatibilité (`[Obsolete]`, surcharge) quand c'est raisonnable, et la signaler clairement dans le rapport. Ajouter une courte doc XML quand c'est utile.

### 9.9 Séparation runtime et éditeur

Le runtime ne dépend pas de l'UI de l'éditeur ; aucune feature éditeur ne fuit dans le runtime. Sauvegarde et export appartiennent à l'éditeur et à l'outillage (`CasaEngine.EditorServices`, `CasaEngine.Editor`) ; chargement et exécution au runtime (`CasaEngine`). Préférer des frontières de projet ou de namespace. Quand une tâche touche les deux côtés, dire quels fichiers appartiennent à chacun.

### 9.10 Erreurs

Échouer tôt sur un usage développeur invalide ; signaler les erreurs d'assets avec du contexte ; ne jamais avaler une exception en silence ; ne pas lever d'exception ni logguer à chaque frame dans les chemins runtime ; valider avant d'exécuter.

### 9.11 Style

Suivre le style des fichiers touchés. Code clair plutôt qu'astucieux : noms clairs, types explicites quand ils aident la lecture, pas de one-liner astucieux, méthodes focalisées, pas de région ni de commentaire inutile, commenter seulement la logique non évidente. Code moteur : flux de contrôle prévisible, pas d'allocation cachée.

### 9.12 Reverse engineering et portage

Sur du code issu de reverse engineering, décompilation, formats binaires ou comportement d'un jeu existant : préserver la logique et l'ordre d'exécution d'origine, ne pas optimiser ni renommer un champ inconnu sans justification, séparer faits et hypothèses, garder les commentaires d'offset et d'adresse, préférer des parseurs déterministes et des tests à l'interprétation manuelle. En traduisant du C, C++ ou MIPS vers C# : préserver les effets de bord, les tailles et signes des entiers, le comportement de dépassement quand il compte, ne pas remplacer la logique de pointeurs par un comportement de plus haut niveau sauf équivalence, documenter les champs incertains.

## 10. Documentation et ADR

- Mettre la documentation à jour quand une API publique ou une feature est ajoutée, quand le comportement de l'éditeur ou un format d'asset change, quand un nouveau workflow apparaît. Courte et utile : résumé, extrait d'usage, limites, risques, suites.
- **ADR.** Toute décision d'architecture, de format d'asset, d'API publique ou de backend, prise pendant un plan ou une discussion, est enregistrée dans `docs/decisions/` : un fichier par décision, regroupement possible par thématique, modèle `docs/decisions/template.md`, index `docs/decisions/README.md`, skill `adr`. Les audits de `ai-agent/audits/` sont en lecture seule : une décision qui y figure est recopiée en ADR, l'audit n'est pas modifié.

## 11. Organisation des documents

- `docs/` : documentation du moteur (`docs/engine/`) et de l'éditeur (`docs/editor/`), index `docs/README.md` ; décisions dans `docs/decisions/`.
- `ai-agent/` : travail des agents. `audits/` = analyses et checklists (lecture seule) ; `tasks/` = plans avec du travail restant ; `tasks/archive/` = plans terminés ; index et tableau des tâches restantes dans `ai-agent/README.md` ; modèle `ai-agent/plan-template.md`.
- Tout nouveau document va dans le bon dossier et dans l'index correspondant. Un plan terminé passe dans `tasks/archive/` et le tableau est mis à jour.
- Règles par chemin : chaque fichier de `.github/instructions/` a un jumeau dans `.claude/rules/` ; toute modification se fait dans les deux. Même règle entre `.github/agents/` et `.claude/agents/`.

## 12. Rapport de fin de tâche

```text
Changed files:
- ...

Validation:
- ... (build, tests, commandes lancées et résultats)

Assumptions:
- ... (aucune hypothèse non confirmée par l'auteur : sinon c'est une question à poser)

Risks:
- ...

Next useful step:
- ...
```
