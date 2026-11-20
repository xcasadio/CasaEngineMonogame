# Plan agent IA — Troisième intégration d'`origin/main` : caméras, aperçu vivant, puis push

Suite de [origin-main-integration-tasks.md](origin-main-integration-tasks.md) et de la seconde intégration, archivée sur `origin/main`.
Les décisions D1 → D3 ci-dessous ont été arbitrées avec l'auteur le 2026-11-20 : **ce plan les applique, il ne les rediscute pas**. Les arbitrages P1 → P9 sont ceux de l'agent.
Relecture neuve du 2026-11-20 avant exécution : REVISE (contrôle des noms sans méthode, cas d'un `main` qui bouge, absence de retour arrière, comptage des tests absent de la première fusion, renumérotation trop aveugle) ; chaque point est repris dans cette version. Deuxième relecture neuve le même jour : REVISE (références non contrôlées avant les fusions, texte de la ligne `Source` de l'ADR-0146, contrôle des noms qui n'arrêtait pas le push, contenu des fichiers binaires, attributs de test dérivés) ; chaque point est repris ici. Relecture de clôture le même jour : REVISE sur deux points (les lignes que la renumérotation écrit elle-même n'étaient dans aucune classe acceptée ; le chemin « `origin/main` a bougé après la fusion » n'était pas exécutable) ; les deux sont repris ici, le second en le supprimant. L'auteur a alors demandé une relecture de plus (« Une relecture de plus ») : une quatrième relecture neuve, le 2026-11-20, a rendu **READY** sur cette version. Exécution lancée ensuite, sur le mandat de D1 et D2.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Réunir en un seul `main` trois lignes de travail du moteur, puis le pousser :

- le `main` local, qui porte 24 commits des caméras jamais poussés ;
- la branche `chantier/cutscene-live-preview` (aperçu vivant de l'éditeur de cutscenes et clôture des plans de cutscenes), partie d'un `main` plus ancien ;
- `origin/main`, qui a reçu 45 commits que le `main` local n'a pas.

Deux ADR locales des caméras portent des numéros que `origin/main` a publiés pour d'autres décisions : elles sont renumérotées.

## État vérifié du dépôt (2026-11-20)

Relevé dans le worktree `D:\development\repo\CasaEngineMonogame-cutscene` après `git fetch origin`.

- `origin/main` = `01cdf483` ; `main` local = `851bfa93` ; `chantier/cutscene-live-preview` = `eca50d96`. Base commune de `main` et d'`origin/main` : `760734ae`.
- `git rev-list --count` : `main..origin/main` 45 ; `origin/main..main` 24 ; `main..chantier/cutscene-live-preview` 13. La branche de l'aperçu vivant part de `ae2a42be`, un ancêtre du `main` local.
- **ADR.** `origin/main` porte `0144-a-world-file-can-be-written-from-entities-that-are-not-initialized.md` et `0145-editor-remote-control-edits-entity-assets.md`. Le `main` local porte `0144-simple-cameras-and-behaviour-components.md` et `0145-camera-behaviours-are-components-that-pose-a-simple-camera.md`. La branche de l'aperçu vivant porte `0146-a-world-can-run-under-its-own-execution-policy.md`. La branche `chantier/camera-2d-pose`, qu'une autre session occupe et que ce plan ne touche pas, porte en plus `0146-the-old-camera-classes-are-removed-and-their-data-is-migrated-once.md` et `0147-the-2d-camera-is-posed-by-its-transform.md`.
- **Précédent.** La seconde intégration a renuméroté deux ADR locales avant de fusionner (`dcd91e10`) : fichiers renommés, ligne « Renumbered from ADR-00nn on <date> when origin/main was merged » dans l'ADR, note « (numbered ADR-00nn until <date>) » dans l'index, et chaque mention mise à jour dans le code, les tests, les plans et la documentation.
- **Mentions à renuméroter.** Dans l'arbre qui réunit la branche et le `main` local, `git grep -E "ADR-014[45]|014[45]-"` rend 31 lignes dans 25 fichiers ; toutes désignent les deux ADR des caméras : 14 commentaires de code et de tests, un README de sample, deux plans, cinq fichiers de `docs/`, les deux ADR, l'index, et une ligne de l'ADR-0146. Une recherche plus large (numéros sans préfixe) trouve en plus trois lignes du plan de l'aperçu vivant (`cutscene-editor-live-preview-tasks.md:74`, `:140`, `:313`). Cinq lignes parlent de l'histoire de la numérotation elle-même et non d'une décision : la ligne `Source` de l'ADR-0146 (« The number 0146 follows ADR-0145 of the branch `chantier/camera-behaviours`, which is not merged. ») et les lignes 48, 74, 140 et 313 du plan de l'aperçu vivant.
- **Attributs de test.** `CasaEngine.Tests` utilise `[Fact]` et `[Theory]`, et des attributs dérivés `[GpuFact]` et `[GpuTheory]` (242 emplois dans 39 fichiers).
- **Fichiers binaires.** Le sample des caméras, que `origin/main` n'a pas, porte des images (`Projects/PerspectiveCameraSample/Images/backdrop.png`, `box.png`) : `git log -p` n'en montre pas le contenu.
- **Conflits prévus** (`git merge-tree --write-tree`, sans rien modifier) : la branche avec le `main` local, `docs/decisions/README.md` seul ; le `main` local avec `origin/main`, `ai-agent/README.md` et `docs/decisions/README.md`. Les autres fichiers touchés des deux côtés se fusionnent seuls (`World.cs`, `EditorEntityJsonSerializer.cs`, `docs/README.md`).
- **Sous-modules.** `MGUI` (`2f9773d0`) et `NvgSharp` (`9c0da031`) sont au même commit des deux côtés.
- **Tests.** La branche de l'aperçu vivant passe 8094 tests sur 8094. Le nombre de tests du `main` local et celui d'`origin/main` ne sont pas relevés : ils le sont dans les tâches.
- **Noms.** Aucun message de commit et aucune ligne ajoutée du côté local (`origin/main..main` et `origin/main..chantier/cutscene-live-preview`) ne nomme un jeu ni un projet extérieur (recherche sans tenir compte de la casse : aucun résultat).
- **Arbre de travail.** L'arbre principal du moteur est sur `chantier/camera-2d-pose` (autre session). `main` n'est extrait dans aucun worktree.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'auteur demande de fusionner et de pousser (« Merge et pousse »). |
| D2 | Devant l'écart entre `main` et `origin/main`, l'auteur choisit de tout réunir puis de pousser : `main` local, aperçu vivant et `origin/main` sur une branche d'intégration ; build et tests ; `main` avancé et poussé (« Tout réunir puis pousser »). |
| D3 | Les ADR locales des caméras 0144 et 0145 deviennent 0147 et 0148 ; celle de l'aperçu vivant reste 0146. Les numéros publiés gagnent. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | Branche d'intégration `chantier/integrate-origin-main-3`, créée depuis `chantier/cutscene-live-preview`, dans le worktree de ce chantier. `main` n'est avancé qu'à la fin, sans commit fait sur `main`, par avance rapide. |
| P2 | Ordre : fusionner d'abord le `main` local dans la branche, puis renuméroter, puis fusionner `origin/main`. Ainsi, au moment de la renumérotation, toute mention de 0144 ou 0145 est une mention des caméras. |
| P3 | La renumérotation suit le précédent `dcd91e10`, commentaires de code et de tests compris. Une note de validation datée d'un plan garde son sens : le numéro y est mis à jour, avec « numérotée ADR-0144 (ou 0145) jusqu'au 2026-11-20 ». Une phrase qui parle de l'histoire de la numérotation n'est pas réécrite : elle garde l'ancien numéro et reçoit la précision « devenue ADR-0147 (ou 0148) le 2026-11-20 », pour ne rien dire de faux. Quand une ligne est à la fois une note datée et une phrase d'histoire, c'est cette seconde règle qui s'applique. La ligne `Source` de l'ADR-0146 est le seul cas où la précision ne suffit pas : son texte final est donné en T0.2. Les lignes de ce plan-ci gardent les anciens numéros : elles décrivent la renumérotation. |
| P4 | Règle donnée à la session des caméras, qui a encore deux ADR sur sa branche : ses ADR se lisent avec **+3** (0144 → 0147, 0145 → 0148, et sur sa branche 0146 → 0149, 0147 → 0150). Ce plan ne touche pas à cette branche ; la règle est écrite dans l'index des ADR et dans les notes du dépôt parent. |
| P5 | Aucun changement de code hors résolution des conflits. Si un build ou un test échoue après une fusion à cause de la rencontre de deux lignes de travail : ⚠️ Blocked, question à l'auteur, pas de correction improvisée dans le code d'un autre chantier. |
| P6 | « Aucun test perdu » se prouve par un comptage, **à chacune des deux fusions** : pour chaque fichier de `CasaEngine.Tests`, le nombre d'attributs de test de l'arbre réuni (tout attribut dont le nom finit par `Fact` ou `Theory`, motif `\[\w*(Fact|Theory)\b`, donc `[GpuFact]` et `[GpuTheory]` compris) vaut celui d'un côté plus celui de l'autre moins celui de leur base commune (`ae2a42be` pour la branche et le `main` local, `760734ae` pour la tête locale et `origin/main`). Tout écart est expliqué ou bloque. Deux faux écarts sont connus : un même test ajouté à l'identique des deux côtés, gardé une fois ; un fichier renommé d'un côté et modifié de l'autre, qui donne deux écarts de fichier qui s'annulent. Le `main` local n'est pas compilé à part : son nombre de tests à l'exécution n'est pas relevé, le comptage par fichier en tient lieu. |
| P7 | Le push envoie `main` seul, avec les 24 commits des caméras, les 13 de l'aperçu vivant et de la clôture, et ceux de ce plan. Aucune autre branche n'est poussée. Avant le push, le contrôle des noms se fait **commit par commit** sur tout ce qui part (`origin/main..<branche>`) : le message de chaque commit, les lignes que chaque commit ajoute (`git log -p --no-merges`), ce que chaque commit de fusion ajoute lui-même en résolvant (`git show --cc`), les chemins ajoutés ou renommés (`--name-status`), et le contenu brut de chaque fichier binaire ajouté ou modifié (`git log -p` et `git show --cc` lancés avec `--text`, ou recherche dans le blob), en sachant que des métadonnées compressées ne se lisent pas ainsi. Une ligne ajoutée puis retirée par un commit suivant compte : elle resterait dans l'historique public. **Un seul résultat, ou un nombre de commits contrôlés différent de celui qui part, arrête tout : ni avance de `main`, ni push, question à l'auteur.** Le nombre de commits contrôlés est égal à `git rev-list --count origin/main..<branche>`. Les mots cherchés ne sont pas écrits dans ce plan. |
| P8 | Les fusions portent sur les commits relevés dans l'état vérifié, pas sur des noms qui peuvent bouger : `851bfa93` pour le `main` local, `01cdf483` pour `origin/main`. Chaque tâche de fusion commence par comparer les références à ces valeurs. **Si le `main` local a bougé** avant T0.1 ou depuis : ⚠️ Blocked et question à l'auteur. L'autre session peut y avoir fusionné des ADR qui portent les numéros 0146 et 0147, et des commits que D2 ne couvre pas. **Si `origin/main` a bougé**, avant T1.1 ou à n'importe quel moment avant le push : ⚠️ Blocked et question à l'auteur, puisque l'état vérifié ne vaut plus ; aucune fusion n'est refaite de la propre initiative de l'agent. La tête poussée est celle que T3.1 a confirmée, plus les seuls commits de documentation de T3.1 et de T3.2, que le contrôle des noms couvre. |
| P9 | **Retour arrière.** Avant l'avance de `main` : supprimer la branche d'intégration suffit, rien d'autre n'a changé. Après l'avance et avant le push : `git branch -f main <valeur>` remet `main` où il était, avec la valeur de `main` relevée à l'étape 1 de T3.2, qui est `851bfa93` si rien n'a bougé (sinon P8 a déjà arrêté le travail). Après le push : aucun retour arrière, seulement une correction par un nouveau commit ; jamais de push forcé. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/integrate-origin-main-3`**, dans le worktree `D:\development\repo\CasaEngineMonogame-cutscene`. Ne jamais committer sur `main`. Ne jamais toucher à l'arbre principal du moteur ni à `chantier/camera-2d-pose`.
- **Une seule tâche à la fois**, statut mis à jour dans ce fichier, note de validation sous la tâche, un commit par tâche (le commit de fusion pour une tâche de fusion), message en anglais `type(area): summary`.
- **Le push n'a lieu qu'en T3.2**, après la vérification indépendante, et seulement de `main` vers `origin/main`.
- **Ne rien inventer** ; blocage : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`.
- **Lancements automatisés de l'éditeur** : options d'automatisation seulement, sorties hors du dépôt, jamais `--keep-open`, aucune entrée clavier ou souris, aucun asset enregistré ; après chaque lancement, supprimer le seul fichier `Projects/SpriteCutsceneSample/.casaeditor/viewport.editor.json` qu'il crée.
- `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, `docs/` et ADR en anglais.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions compilent à 0 erreur sur la tête réunie.
- `dotnet test` entier vert ; comptage de P6 sans écart inexpliqué.
- `git grep -E "ADR-014[45]|014[45]-"` sur la tête réunie ne rend que les deux ADR d'`origin/main`, leurs mentions venues d'`origin/main`, et les lignes des classes acceptées (a) à (e) de T0.2, listées sous cette tâche.
- Lancements de contrôle de l'éditeur conformes (T2.1).
- Vérification indépendante CONFIRMED avant le push.

---

## Phase 0 — Côté local

### ✅ T0.1 — Fusionner le `main` local dans la branche d'intégration

- Objectif : une branche qui porte les caméras (deux tranches) et l'aperçu vivant.
- Étapes :
  1. `git rev-parse main origin/main` doit rendre `851bfa93` et `01cdf483` ; sinon ⚠️ Blocked (P8). Créer `chantier/integrate-origin-main-3` depuis `chantier/cutscene-live-preview`.
  2. `git merge --no-ff 851bfa93`. Résoudre `docs/decisions/README.md` en gardant les lignes des deux côtés, dans l'ordre des numéros (0144, 0145, 0146).
  3. Build des deux solutions, `dotnet test` ; noter le nombre de tests.
  4. Comptage de P6 pour cette fusion (base `ae2a42be`, branche, `main` local, arbre réuni), avec un script gardé hors du dépôt ; noter les quatre totaux et chaque écart.
- Validation : aucun marqueur de conflit dans l'arbre (`git grep -n -E "^(<<<<<<<|>>>>>>>)"` vide) ; 0 erreur ; tests verts ; comptage sans écart inexpliqué.
- Commit : `merge: bring main (second camera slice) into the live preview branch`
- Note de validation (2026-11-20) : `main` et `origin/main` étaient à `851bfa93` et `01cdf483`. Fusion de `851bfa93` : un seul conflit, `docs/decisions/README.md`, résolu en gardant les deux lignes dans l'ordre (0145 puis 0146) ; `World.cs`, `ai-agent/README.md` et `docs/README.md` fusionnés seuls. Aucun marqueur de conflit. Build des deux solutions : 0 erreur. `dotnet test` : premier lancement 8147 réussites sur 8148, un échec, `OpenAlCallbackDeviceLossTests.After_the_device_is_lost_game_commands_beyond_the_ring_capacity_do_not_wait_and_the_backend_stays_available` ; ce test passe trois fois de suite lancé seul, aucun des deux côtés de la fusion ne touche `CasaEngine/Framework/Audio` ni `CasaEngine.Tests/Audio`, et le second lancement complet rend 8148 sur 8148. L'échec est donc intermittent et antérieur à ce plan ; il n'est pas corrigé ici (point O4). Comptage de P6 sur 630 fichiers : base `ae2a42be` 5969, branche 5997, `main` local 6005, arbre réuni 6033, attendu 6033, aucun écart.

### ✅ T0.2 — Renuméroter les ADR locales 0144 et 0145 en 0147 et 0148

- Objectif : libérer les numéros qu'`origin/main` a publiés (D3).
- Fichiers : les deux ADR (`git mv`), `docs/decisions/README.md`, et les 25 fichiers de l'état vérifié.
- Étapes :
  1. `git mv` des deux fichiers vers `0147-simple-cameras-and-behaviour-components.md` et `0148-camera-behaviours-are-components-that-pose-a-simple-camera.md` ; titre mis à jour ; ligne « Renumbered from ADR-0144 (0145) on 2026-11-20 when origin/main was merged (its ADR-0144 (0145) is another decision) » sous `Source`.
  2. Index : les deux lignes renumérotées, avec la note « (numbered ADR-0144 (0145) until 2026-11-20) », placées après la ligne 0146.
  3. Relever les mentions par deux recherches sur l'arbre réuni : `git grep -n -E "ADR-014[45]|014[45]-"`, puis la recherche large `git grep -n -E "\b014[45]\b|ADR[ -]?14[45]\b"`. Classer chaque ligne : renumérotée (commentaires de code et de tests, documentation, README de sample, index, titres et renvois des deux ADR) ; note datée d'un plan, renumérotée avec l'ancien numéro entre parenthèses ; phrase sur l'histoire de la numérotation, gardée avec la précision de P3 (les lignes 48, 74, 140 et 313 du plan de l'aperçu vivant) ; ligne de ce plan-ci, inchangée. Chaque ligne tombe dans une seule classe.
  3 bis. Après le changement, relancer les deux recherches. Chaque ligne qui porte encore un ancien numéro doit être dans une de ces **classes acceptées** : (a) la ligne « Renumbered from » des ADR 0147 et 0148 ; (b) les deux notes « (numbered ADR-0144 (0145) until 2026-11-20) » de l'index et la note de la règle « +3 » ; (c) une note datée d'un plan, avec « numérotée ADR-0144 (ou 0145) jusqu'au 2026-11-20 » ; (d) une phrase d'histoire, avec « devenue ADR-0147 (ou 0148) le 2026-11-20 », et la ligne `Source` de l'ADR-0146 ; (e) une ligne de ce plan-ci. Toute autre ligne est un oubli à corriger.
  4. Dans l'ADR-0146, la fin de la ligne `Source` devient exactement : « The number 0146 followed ADR-0145 of the camera chantier, which was not merged when this record was written; that record became ADR-0148 on 2026-11-20, when origin/main was merged. »
  5. Écrire la règle « +3 » de P4 dans une note sous l'index des ADR.
- Validation : chaque ligne rendue par les deux recherches, avant puis après le changement, est listée sous cette tâche avec sa classe ; après le changement, la ligne `Source` de l'ADR-0146 ne dit rien de faux ; build des deux solutions à 0 erreur ; `dotnet test` vert (seuls des commentaires changent dans le code).
- Commit : `docs(decisions): renumber the local camera records 0144 and 0145 to 0147 and 0148 before merging origin/main`
- Note de validation (2026-11-20) : fichiers renommés par `git mv` en `0147-simple-cameras-and-behaviour-components.md` et `0148-camera-behaviours-are-components-that-pose-a-simple-camera.md`. Avant le changement, la première recherche rendait 31 lignes dans 25 fichiers et la recherche large quatre lignes de plus (`ai-agent/README.md:122`, plan de l'aperçu vivant `:74`, `:140`, `:313`). Renumérotées : 14 commentaires de code et de tests (`Demo.cs`, `DemoCameraFraming.cs`, `EditorViewportCameraController.cs`, six fichiers de tests, `ViewportCameraNavigation.cs`, `Follow2dCameraComponent.cs`, `OrbitCameraComponent.cs`, `PerspectiveCameraComponent.cs`, `ScriptArcBallCamera.cs`), `Projects/PerspectiveCameraSample/README.md`, `docs/README.md` (2), `docs/editor/editor-2d-viewport.md`, `docs/engine/camera-behaviour-components.md`, `docs/engine/perspective-camera-component.md` (2), les titres et le renvoi interne des deux ADR, leurs deux lignes de l'index, déplacées après la ligne 0146. Après le changement, les lignes qui portent encore un ancien numéro, chacune dans une classe : (a) `0147-…md:6` et `0148-…md:6` ; (b) `docs/decisions/README.md:161`, `:162` et la note de la règle « +3 » `:164` ; (c) plan des caméras `:289` ; (d) plan de l'aperçu vivant `:48`, `:74`, `:140`, `:313`, plan des caméras `:151` (phrase sur la suite des numéros) et `:287` (chemin de fichier d'une tâche faite), `0146-…md:5` avec le texte exact prévu ; (e) les lignes de ce plan et sa ligne dans l'index des plans, `ai-agent/README.md:122`. Aucune autre ligne. Dans le code, seules des lignes de commentaire qui citent un numéro d'ADR changent (contrôlé par `git diff`).  Build des deux solutions : 0 erreur. `dotnet test` : 8148 réussites sur 8148.

---

## Phase 1 — Fusion d'`origin/main`

### ✅ T1.1 — Fusionner `origin/main`, résoudre, compiler, passer les tests

- Objectif : une tête qui contient tout.
- Étapes :
  1. `git fetch origin`, puis `git rev-parse origin/main` doit rendre `01cdf483` ; sinon ⚠️ Blocked (P8). `git merge --no-ff 01cdf483`.
  2. Résoudre `ai-agent/README.md` (les lignes des deux côtés sont gardées) et `docs/decisions/README.md` (lignes dans l'ordre des numéros : 0144 et 0145 d'`origin/main`, 0146, 0147, 0148). Tout autre conflit, s'il en vient un : le résoudre seulement s'il est dans un fichier de documentation ou d'index ; dans du code, ⚠️ Blocked (P5).
  3. Build des deux solutions ; `dotnet test`.
  4. Comptage de P6 pour cette fusion (base `760734ae`, tête locale de T0.2, `origin/main`, arbre réuni), avec le script de T0.1 ; noter les quatre totaux et chaque écart.
- Validation : aucun marqueur de conflit ; 0 erreur ; tests verts ; comptage sans écart inexpliqué ; les ADR 0144 à 0148 existent chacune une fois et l'index les liste toutes.
- Commit : `merge: integrate origin/main (entity asset remote commands, path segment rule)`
- Note de validation (2026-11-20) : après `git fetch origin`, `origin/main` était toujours `01cdf483` et `main` `851bfa93`. Fusion de `01cdf483` : deux conflits, tous deux dans des index, résolus en gardant les lignes des deux côtés : `ai-agent/README.md` (la ligne du plan des caméras, puis les trois lignes venues d'`origin/main`) et `docs/decisions/README.md` (0144 et 0145 d'`origin/main`, puis 0146, 0147, 0148 et la note de renumérotation). `EditorEntityJsonSerializer.cs` s'est fusionné seul ; aucun conflit dans du code. Aucun marqueur de conflit. Les ADR 0144 à 0148 existent chacune une fois et l'index les liste chacune une fois. Build des deux solutions : 0 erreur. `dotnet test` : 8431 réussites sur 8431. Comptage de P6 sur 635 fichiers : base `760734ae` 5941, tête locale `df972f46` 6033, `origin/main` 6018, arbre réuni 6110, attendu 6110, aucun écart.

---

## Phase 2 — Éditeur lancé

### ✅ T2.1 — Lancements de contrôle sur la tête réunie

- Objectif : voir que l'éditeur réuni démarre et que l'aperçu vivant donne les mêmes valeurs qu'avant la fusion.
- Étapes, sur `Projects/SpriteCutsceneSample`, éditeur recompilé sur la tête réunie :
  1. `--open-asset Cutscenes/live.cutscene --cutscene-preview-seek 1` : monde `LiveWorld`, `FallingBox` à y=45,147, animation à 0,3 s, 9 particules.
  2. `--open-asset Cutscenes/loop.cutscene --cutscene-preview-seek 2` : les lignes de l'aperçu sont celles de la référence du plan de l'aperçu vivant, hors ligne d'état.
  3. Regarder la capture de 1 et le journal des deux lancements (aucune erreur).
- Validation : codes de sortie 0, valeurs égales, `git status --short` propre après suppression du fichier que chaque lancement crée. Ce que ces lancements ne couvrent pas (le pilotage à distance venu d'`origin/main`, les caméras dans le viewport 3D) n'est couvert que par les tests : le dire dans la note.
- Commit : `docs(plan): record the launched-editor runs of the integrated head`
- Note de validation (2026-11-20) : éditeur compilé sur la tête réunie, deux lancements automatisés sur `Projects/SpriteCutsceneSample`, sans entrée clavier ni souris, codes de sortie 0. `Cutscenes/live.cutscene`, saut à 1 s : monde d'aperçu `LiveWorld`, état `Playing`, transport `Paused`, `FallingBox` à y=45,147, animation d'`AnimatedOrb` à 0,3 s, 9 particules pour `Sparks` : les valeurs d'avant la fusion. `Cutscenes/loop.cutscene`, saut à 2 s : les lignes de l'aperçu sont celles de la référence du plan de l'aperçu vivant, sauf la ligne d'état, qui porte le nouveau texte. Capture du premier lancement regardée : boîte en chute avec son volume de collision, sol, orbe animé, anneau bleu de `Marker`, particules, timeline de 3 s. Aucune ligne d'erreur ni d'avertissement dans les deux journaux. Le seul fichier créé par les lancements (`Projects/SpriteCutsceneSample/.casaeditor/viewport.editor.json`) a été supprimé ; `git status --short` propre. Non couvert par ces lancements : le pilotage à distance venu d'`origin/main` et les caméras dans le viewport 3D, qui ne sont couverts que par les tests.

---

## Phase 3 — Vérification, avance, push

### ⏳ T3.1 — Vérification indépendante

- Objectif : un regard neuf sur la tête réunie avant tout push.
- Étapes : un vérificateur neuf reçoit la revendication exacte (les trois lignes de travail sont dans la tête ; rien n'est perdu ; builds et tests ; renumérotation complète ; noms) et rend CONFIRMED, REFUTED ou INCONCLUSIVE.
- Validation : CONFIRMED. Sinon, corriger ce qui est dans le périmètre, ou ⚠️ Blocked.
- Commit : `docs(plan): record the check of the third integration of origin/main`

### ⏳ T3.2 — Notes, avance de `main`, push

- Objectif : `main` et `origin/main` égaux à la tête réunie.
- Étapes :
  1. Refaire `git fetch origin` ; `git rev-parse main origin/main` doit rendre `851bfa93` et `01cdf483` ; sinon ⚠️ Blocked (P8), sans avance ni push.
  2. Mettre à jour la ligne de ce plan et celle du plan de l'aperçu vivant dans `ai-agent/README.md`, et passer cette tâche en ✅ avec une note qui dit que l'avance et le push suivent ce commit : c'est le dernier commit du plan, aucun commit n'est fait après le push.
  3. Contrôle des noms selon P7, sur `origin/main..chantier/integrate-origin-main-3`, ce dernier commit compris. **Condition d'arrêt** : un seul résultat, ou un nombre de commits contrôlés différent de `git rev-list --count`, et la tâche passe en ⚠️ Blocked, sans avance de `main` ni push. La méthode, le nombre de commits et de fichiers binaires contrôlés et le résultat vont dans le rapport à l'auteur.
  4. Avancer `main` par `git fetch . chantier/integrate-origin-main-3:main`, qui refuse tout autre chose qu'une avance rapide ; puis `git push origin main`, sans option de forçage.
  5. Vérifier que `git rev-parse main origin/main` rend deux fois la tête de la branche. Ce résultat va dans le rapport à l'auteur et dans les notes du dépôt parent, pas dans un commit.
- Validation : le contrôle des noms a rendu zéro résultat sur un nombre de commits égal à celui de `git rev-list --count`, avant l'étape 4 ; les deux commandes de l'étape 4 ont réussi telles quelles ; `main`, `origin/main` et la branche sont au même commit.
- Retour arrière : P9.
- Commit : `docs(ai-agent): record the third integration of origin/main`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Confirmation de P1 → P9 par l'auteur. | Toutes |
| O2 | La session des caméras devra reprendre le `main` réuni et renuméroter ses deux ADR de branche (P4). | T0.2 |
| O3 | La recette de l'aperçu vivant (T3.2 de son plan) n'est pas faite : la fusion et le push ont lieu avant, à la demande de l'auteur. L'ADR-0146 reste `Proposed`. | — |
| O4 | Test intermittent, antérieur à ce plan : `OpenAlCallbackDeviceLossTests.After_the_device_is_lost_game_commands_beyond_the_ring_capacity_do_not_wait_and_the_backend_stays_available` échoue parfois dans un lancement complet et passe seul (voir T0.1). C'est sans doute l'échec non identifié noté en O9 du plan de l'aperçu vivant. Non corrigé ici : hors périmètre. | T0.1 |

## Hors périmètre

- Toute correction de code d'un des trois chantiers.
- La branche `chantier/camera-2d-pose` et ses ADR.
- Le dépôt parent : ni commit ni push, sauf demande.
- MGUI et NvgSharp : aucun changement.
