# Plan agent IA — Intégrer `origin/main` dans la ligne locale du moteur (seconde fois) et pousser

Plan né du blocage O7 de [entity-asset-remote-tasks.md](entity-asset-remote-tasks.md) : le 2026-11-19, le push demandé par l'auteur a été refusé parce qu'`origin/main` avait reçu 197 commits d'ailleurs. Il n'y a pas d'audit dédié ; le modèle suivi est le plan de la première intégration, arrivé avec `origin/main` (`ai-agent/tasks/origin-main-integration-tasks.md`, lu par `git show origin/main:…`).
Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur le 2026-11-19 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

`origin/main` du moteur (`760734aea`) est fusionné dans la ligne locale (les trois commits de `offline-world-writer` et le chantier `entity-asset-remote`), les deux décisions locales dont le numéro est pris sont renumérotées, les commandes de création refusent un dossier écrit avec `..`, tout est revérifié (moteur, éditeur lancé, jeu), puis `main` du moteur et `remaster` du jeu sont avancés et **poussés**, et le plan `entity-asset-remote` est archivé.

## État vérifié du dépôt (2026-11-19)

Relevé en lecture seule (`git` sans changement de référence ni d'arbre ; aucun build, aucune fusion).

- Moteur : branche `chantier/integrate-origin-main-2` = `chantier/entity-asset-remote` = `ce3604758`, arbre propre ; `main` local = `be750e7db` (avancé le 2026-11-19 à la demande de l'auteur, un commit de documents derrière la branche) ; `origin/main` = `760734aea` après `git fetch` ; base commune `da32906e2` (`git merge-base origin/main HEAD`).
- `origin/main` porte 197 commits depuis la base (`git rev-list --count`), datés du 2026-11-19 pour les derniers : éditeur de cutscenes, question d'enregistrement pour tout document modifié (ADR-0143), première intégration d'`origin/main`, retrait du hook de garde (`.claude/hooks/block-commit-on-main.ps1` supprimé ; `AGENTS.md` §4 dit maintenant « Aucun hook ne l'empêche : la règle s'applique par elle-même »).
- **Un seul conflit de texte** à la fusion : `docs/decisions/README.md` (`git merge-tree --write-tree --name-only origin/main HEAD`). `CasaEngine.Editor/GameEditor.cs` (2517 lignes ajoutées, 972 retirées sur `origin/main`) fusionne sans conflit de texte ; aucun build de l'arbre fusionné n'a été fait.
- **Numéros d'ADR** : `origin/main` porte les ADR-0128 à 0143 (`git ls-tree origin/main docs/decisions/`). Les ADR locales 0128 (écriture d'un monde hors ligne) et 0129 (pilotage des assets `.entity`) sont citées dans 15 fichiers du moteur (`git grep -c -E "ADR-0128|ADR-0129|0128-a-world-file|0129-editor-remote" HEAD` : 5 fichiers de code, 3 de tests, 2 ADR, l'index, 1 document, 2 plans, `ai-agent/README.md`) et dans 5 fichiers du jeu (`HANDOVER.md`, `ai-agent/README.md`, le plan `rgce-worlds-as-assets`, l'ADR-0042 du jeu, `scripts/RgceAssetCooker/README.md`).
- **Sous-modules** : `origin/main` épingle MGUI à `2f9773d0` ; la ligne locale à `f6ed1fcd`, qui est le `develop` local de MGUI (`git submodule status`). Le `fetch` a rapporté `f6ed1fcd..2f9773d0 develop -> origin/develop` : avance rapide. NvgSharp : même commit des deux côtés (`9c0da031`).
- **Ce qu'`origin/main` ne touche pas** : `CasaEngine.Editor/Remote/`, `CasaEngine.Editor/GameEditor.Remote.cs`, `CasaEngine.Tests/Editor/Remote/`, `Tools/`, `CasaEngine.Editor/Controls/EntityAssetEditorPanel.cs` (`git diff --name-only da32906e2 origin/main -- <chemins>` vide).
- **Ce qu'`origin/main` change autour des documents** (lu dans `git show origin/main:CasaEngine.Editor/GameEditor.cs`) : une méthode d'enregistrement par sorte de document ; pour un asset d'entité, `TrySaveEntityDocument(panelId, panel)` (`:6511-6529` : rien si non modifié, sinon `TrySaveLoadedAsset`, historique marqué enregistré, titre, vrai seulement si le document ne se lit plus modifié) et `IsEntityDocumentModified(panelId, panel)` (`:6969-6971`) ; `SaveDirtyEntityAssetEditors` les appelle (`:6332-6338`) ; la fermeture d'un onglet par l'utilisateur pose la question d'enregistrement pour toute sorte modifiable (`OnDockHostPanelClosing`, `:3046-3061`). Les membres de `GameEditor` dont l'hôte du pilotage se sert existent encore sous leur nom (`TryOpenEntityAsset`, `_entityAssetEditorPanels`, `UpdateDockPanelTitle`, `GetEntityDocumentTitle`, `OnDockHostPanelRemoved`, `TryOpenAutomationAsset`). L'hôte local enregistre un asset d'entité par son propre code (`GameEditor.Remote.cs`, branche d'entité de `TrySaveAsset`), équivalent mais distinct.
- `origin/main` change 47 fichiers du runtime (`CasaEngine/`), 33 de `CasaEngine.EditorServices/`, 86 de `CasaEngine.Tests/`, 55 de `Projects/`.
- **Défaut à corriger (O6 du plan précédent)** : `EditorRemoteAssetCreation.Prepare` (`CasaEngine.Editor/Remote/EditorRemoteAssetCreation.cs:33-53`) garde tel quel un dossier écrit `Entities/../Entities` ; le fichier est créé dans le projet mais catalogué sous ce chemin, et l'asset ne s'ouvre plus par les commandes (constat du vérificateur, rejoué dans l'éditeur lancé). Vaut pour `material.create`, `particle.create`, `sound.create` et `entity_asset.create`.
- Jeu : branche `chantier/rgce-worlds-as-assets` = `remaster` local = `6fe9ee8` ; `origin/remaster` = `170f41c`, ancêtre du local ; pointeur `CasaEngine` = `be750e7db`. Arbre propre hors `.serena/` et `log.txt`, plus ` M CasaEngine` (le moteur est un commit de documents plus loin).
- Références gardées : `artifacts/entity-asset-remote/` du dépôt du jeu (`commands-before.json`, `smokes-after/`, scripts `editorctl.py`, `t22_run.py`, `m9_run.py`) ; `CasaEngine.Tests` 6620/6620 sur la ligne locale ; références de parité du jeu `artifacts/worlds-as-assets/m1/ref/`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Intégrer `origin/main` par un plan soumis à l'auteur (« Plan d'intégration », 2026-11-19). |
| D2 | `main` du moteur (`be750e7db`) et `remaster` du jeu (`6fe9ee8`), avancés en local le 2026-11-19, restent où ils sont (« Les laisser »). |
| D3 | Les commandes de création refusent un dossier écrit avec `..` (« Refuser « .. » »), et cette tâche fait partie de ce plan (« Dans le plan d'intégration »). |
| D4 | La fusion va jusqu'au push (« Avancer et pousser », 2026-11-19) : `main` du moteur puis `remaster` du jeu, en avance rapide seulement. |
| D5 | Le plan `entity-asset-remote` est archivé après ces suites (« Après les suites »). |

## Points à valider avec ce plan

Arbitrages proposés par l'agent : l'approbation du plan les valide. P1, P2, P4 et P5 reprennent les arbitrages de la première intégration.

| Réf | Proposition |
|---|---|
| P1 | La fusion se fait sur `chantier/integrate-origin-main-2`, créée depuis `chantier/entity-asset-remote` (`ce3604758`) ; `main` est ensuite avancé sur elle. Aucun commit sur `main`. Côté jeu, la suite se fait sur `chantier/rgce-worlds-as-assets`, puis `remaster` est avancé. |
| P2 | **Les numéros publiés gagnent.** Avant la fusion, l'ADR locale 0128 devient 0144 et la 0129 devient 0145, avec la mention de l'ancien numéro dans chaque fichier (`Renumbered from ADR-0128 on 2026-11-19 when origin/main was merged`) et dans l'index (`numbered ADR-0128 until 2026-11-19`). Toute mention de l'arbre du moteur suit (code, tests, documents, plans). Dans le jeu, les documents ne sont pas réécrits (une ADR du jeu les cite) : une table de correspondance est ajoutée au `HANDOVER.md` et au plan `rgce-worlds-as-assets`. |
| P3 | **MGUI** : le `develop` local du sous-module est avancé en avance rapide sur `origin/develop` (`2f9773d0`, le commit qu'`origin/main` épingle), par `git -C MGUI merge --ff-only origin/develop`, après contrôle que l'arbre de MGUI est propre et que `origin/develop` est bien ce commit. C'est la seule commande git faite dans MGUI ; aucune dans NvgSharp. Si l'avance rapide est impossible : ⚠️. |
| P4 | Conflits : là où les deux côtés ont ajouté des lignes au même endroit, on garde les deux (l'index des ADR). |
| P5 | Les erreurs de compilation et les tests rouges nés de la fusion sont réparés en adaptant le code local aux contrats d'`origin/main` et du nouveau MGUI ; le comportement d'`origin/main` n'est pas modifié ; un choix entre deux comportements passe la tâche en ⚠️. Le commit de fusion contient ces réparations : aucun commit qui ne compile pas. |
| P6 | **Alignement sur l'ADR-0143** (code de sécurité, `security-executor`) : dans l'hôte du pilotage, la branche d'entité de `TrySaveAsset` appelle `TrySaveEntityDocument`, et l'état « modifié » d'un panneau d'entité (`GetAssetPanelKind`, `IsEntityAssetModified`) lit `IsEntityDocumentModified` : une seule méthode d'enregistrement par sorte de document. Les règles du pilotage ne changent pas : `panel.close` refuse un panneau modifié et ne pose aucune question ; `entity_asset.save` rend `written` vrai seulement si le panneau était modifié ; le panneau doit tenir le fichier résolu. Les branches matériau, particule et son de l'hôte, qu'`origin/main` n'a pas touchées, ne sont pas modifiées. |
| P7 | **Refus de `..` (D3), avec ses deux voisins** : `EditorRemoteAssetCreation.Prepare` refuse (`EditorRemoteArgumentException`) un dossier dont un segment est `..`, `.` ou vide, avant tout accès au disque. `.` et un segment vide (`Entities//Cars`) produisent le même défaut que `..` (un chemin catalogué non normalisé) : je propose de les refuser avec lui ; la racine reste écrite `''`, `.` ou `/`. Changement de comportement des quatre commandes de création, documenté. |
| P8 | **Contrôle « aucun test perdu »**, comme à la première intégration : pour chaque fichier de `CasaEngine.Tests/`, le nombre de lignes `[Fact` et `[Theory` de la tête fusionnée égale `local + origin − base` (`git grep -c -E "\[(Fact|Theory)"` sur `da32906e2`, `ce3604758`, `760734aea` et la tête) ; tout écart est listé et expliqué. |
| P9 | **Smokes d'automatisation** : `origin/main` change le comportement de l'éditeur, la référence d'avant fusion ne vaut donc plus octet pour octet. Règle : les huit smokes sortent en 0 ; aucune ligne `[Automation]` ne rapporte un échec (`Unable`, `refused`, `Timed out`) que `smokes-after/` n'a pas ; toute autre différence avec `smokes-after/` est listée dans la note, sans être un arrêt. |
| P10 | **Jeu.** Le pointeur passe sur la tête intégrée ; `dotnet build RacingGame.slnx` à 0 erreur (une erreur de compilation née d'un contrat d'`origin/main` se répare en adaptant l'appel, sans changer de comportement ; sinon ⚠️) ; `RgceAssetCooker --worlds --verify` à 0 écart ; M9 rejouée (`m9_run.py`) : mêmes huit valeurs d'écart qu'avant, ni plus ni moins ; **parité** : la « Validation commune » du plan `rgce-worlds-as-assets` contre `artifacts/worlds-as-assets/m1/ref/`. Le runtime du moteur change (47 fichiers) : **une image, un export ou un rapport différent est un arrêt** (⚠️, différences listées, question à l'auteur) ; rien n'est poussé dans ce cas. |
| P11 | **Push** (D4) : après la vérification indépendante, `main` du moteur est avancé sur la tête de `chantier/integrate-origin-main-2` et `remaster` du jeu sur celle de `chantier/rgce-worlds-as-assets` (avance rapide contrôlée), puis `artifacts/push-main-remaster.ps1` : essai à blanc, push, contrôle par `git ls-remote`, moteur d'abord. L'approbation de ce plan vaut demande de push pour ces deux têtes, telles que le plan les produit. Si `origin/main` ou `origin/remaster` a encore bougé : ⚠️, rien n'est poussé, aucun push forcé. MGUI n'est pas poussé (son `develop` local égale alors `origin/develop`). |
| P12 | **Copies et clôture** : copies de projet dans `<scratchpad>/entity-asset-remote-2/` (dossier temporaire de la session), supprimées à la clôture après contrôle qu'aucune jonction n'y reste ; sorties gardées dans `artifacts/entity-asset-remote/integration/` du dépôt du jeu. À la clôture, `entity-asset-remote-tasks.md` passe dans `tasks/archive/` (D5) ; ce plan-ci reste dans `tasks/`, son archivage revient à l'auteur. Le résultat du push est donné dans le rapport de fin, pas dans un commit de plus. |

## Relecture de sécurité

> À remplir après la passe (`security-reviewer`, lecture seule) sur P6, P7 et la fusion du point de vue du pilotage à distance.

## Règles d'exécution pour l'agent

- **Branches** de P1. Jamais de commit sur `main`, `master` ni `remaster`. **Ne pas pousser avant T4.2**, et seulement comme P11 le dit.
- **Une seule tâche à la fois**, icône `⏳` → `🚧` → `✅` / `🧪` / `⚠️`, note de validation, **un commit par tâche** qui inclut ce fichier, message en anglais `type(area): summary`, par fichier (`git commit -F`), terminé par la ligne `Co-Authored-By` de la session ; `git add` fichier par fichier ; jamais `.serena/`, `.codegraph/`, `log.txt`.
- **Fusion** : `git merge --no-ff --no-commit origin/main`, puis résolution, build, tests, et seulement alors le commit de fusion. Jamais `reset --hard`, `checkout` d'une autre branche, `stash` ni `git submodule update` ; une fusion à abandonner se défait par `git merge --abort`.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt. Au plus 3 cycles de correction par tâche.
- **Build** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`, 0 erreur ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (jamais `--no-build`), tout passe.
- **Sécurité** : T1.3 et T1.4 sont écrites par `security-executor` sur le contrat de la tâche, chacune relue par un `plan-verifier` frais juste avant son exécution ; un `verifier` frais contrôle l'ensemble en T4.1.
- **Lancements** : un seul lancement de l'éditeur ou du jeu à la fois, jamais pendant un build ; rebuild avant de lancer. Les réglages de l'utilisateur sous `%LOCALAPPDATA%/CasaEngine/RacingGameCasaEngine/` ne sont jamais modifiés.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- **Retour arrière** : avant T4.2 rien n'est poussé et `main` ne bouge pas ; tout se défait en abandonnant la branche. Après le push, un défaut se corrige par un nouveau commit, jamais par un push forcé.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- Les deux solutions du moteur à 0 erreur ; `CasaEngine.Tests` tout vert ; aucun test perdu (P8).
- `git merge-base --is-ancestor origin/main HEAD` et `git merge-base --is-ancestor ce3604758 HEAD` rendent vrai sur la branche.
- Éditeur lancé : parcours et refus de `t22_run.py` (132 contrôles, plus ceux de T1.4) ; smokes selon P9 ; `casa-editor commands` : contrat des 54 commandes inchangé par rapport à `t22/commands-after.json` (descriptions à part).
- Jeu : build, outil, M9, parité (P10).
- Vérification indépendante **CONFIRMED** (T4.1), puis push contrôlé par `git ls-remote` (T4.2).

---

## Phase 0 — Numéros de décisions

### ⏳ T0.1 — Renuméroter les ADR locales 0128 et 0129

- Objectif : P2, avant la fusion, quand toute mention « ADR-0128 » ou « ADR-0129 » de l'arbre local désigne encore une décision locale.
- Fichiers : `docs/decisions/0128-a-world-file-can-be-written-from-entities-that-are-not-initialized.md` → `0144-…`, `docs/decisions/0129-editor-remote-control-edits-entity-assets.md` → `0145-…` (`git mv`), `docs/decisions/README.md`, et les 12 autres fichiers du relevé (code : commentaires seulement).
- Étapes : `git mv` ; mention de l'ancien numéro dans les deux ADR et l'index ; remplacement des mentions par un script qui échoue si un fichier du relevé n'en contient pas ; `git grep -E "ADR-0128|ADR-0129"` ne rend plus que les mentions « ancien numéro ».
- Validation : `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur (des commentaires de code changent) ; le relevé `git grep` ci-dessus ; liens de l'index contrôlés (chaque fichier nommé existe).
- Commit : `docs(decisions): renumber the local records 0128 and 0129 to 0144 and 0145 before merging origin/main`

## Phase 1 — Fusion et code

### ⏳ T1.1 — Avancer MGUI

- Objectif : P3.
- Étapes : dans `MGUI/` : arbre propre (`git -C MGUI status --short` vide), `git -C MGUI rev-parse origin/develop` = `2f9773d0…`, `f6ed1fcd` ancêtre de ce commit ; `git -C MGUI merge --ff-only origin/develop`. Aucun commit ici : le pointeur est pris par la fusion (T1.2).
- Validation : `git -C MGUI rev-parse HEAD` = `2f9773d0…` ; `git -C MGUI status --short` vide.
- Commit : aucun (note dans le commit de T1.2).

### ⏳ T1.2 — Fusionner `origin/main`, résoudre, compiler, passer les tests

- Objectif : la branche contient `origin/main` et la ligne locale, compile et passe les tests (P4, P5, P8).
- Étapes : `git merge --no-ff --no-commit origin/main` ; résoudre `docs/decisions/README.md` (les lignes des deux côtés, dans l'ordre des numéros) et `ai-agent/README.md` s'il y a lieu ; les deux builds ; réparer selon P5 ; tests ; contrôle P8 ; commit de fusion.
- Validation : les deux builds à 0 erreur ; `CasaEngine.Tests` tout vert, nombre noté ; table P8 sans écart inexpliqué ; `git ls-tree HEAD MGUI` = `2f9773d0…` ; les deux `merge-base --is-ancestor` de la validation globale.
- Commit : `Merge remote-tracking branch 'origin/main' into chantier/integrate-origin-main-2` (message complété par la liste des réparations).

### ⏳ T1.3 — L'hôte du pilotage enregistre un asset d'entité par la méthode de l'éditeur

- Objectif : P6.
- Fichiers : `CasaEngine.Editor/GameEditor.Remote.cs` seulement.
- Étapes : branche d'entité de `TrySaveAsset` → `_editor.TrySaveEntityDocument(panelId, panel)` (panneau ouvert avec son onglet seulement ; faux → `error` nommant le fichier) ; `GetAssetPanelKind` et `IsEntityAssetModified` → `_editor.IsEntityDocumentModified(panelId, panel)`.
- Validation : build 0 erreur ; tests tous verts, aucune attente changée ; le comportement se contrôle dans l'éditeur lancé en T2.1 (`written` vrai puis faux, empreinte et date du fichier ; `panel.close` d'un panneau modifié refusé).
- Commit : `refactor(editor): save an entity asset from the remote host through the editor's save method`

### ⏳ T1.4 — Les créations refusent un dossier écrit avec `..`, `.` ou un segment vide

- Objectif : P7.
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteAssetCreation.cs`, tests de `CasaEngine.Tests/Editor/Remote/` (création de matériau, de particule, de son, d'entité), `docs/editor/editor-remote-control.md` (règle de `*.create`).
- Étapes : dans `Prepare`, après le calcul du dossier et avant tout accès au disque, refus de tout dossier dont un segment (séparé par `/` ou `\`) est `..`, `.` ou vide ; message qui nomme la règle. Tests, pour les quatre commandes : `Entities/../Entities`, `Entities/./Cars`, `Entities//Cars` refusés (`InvalidParams`), hôte non appelé, aucun fichier ni entrée de catalogue ; la racine (`''`, `.`, `/`) et un dossier simple acceptés comme avant.
- Validation : build 0 erreur ; tests tous verts ; un test existant dont l'attente changerait (un refus déjà attendu, par une autre règle) est nommé dans la note.
- Commit : `fix(editor): refuse a creation folder written with dot segments`

## Phase 2 — Éditeur lancé

### ⏳ T2.1 — Parcours, refus et smokes sur la tête intégrée

- Objectif : les commandes d'entité tiennent sur l'éditeur d'`origin/main` ; l'automatisation ne casse pas (P9).
- Étapes : rebuild ; `t22_run.py` sur une copie neuve (`<scratchpad>/entity-asset-remote-2/`), complété de trois refus de création (`..`, `.`, segment vide) pour `entity_asset.create` et `material.create` ; `casa-editor commands` comparé à `t22/commands-after.json` ; `Tools/automation-smokes.sh` vers `artifacts/entity-asset-remote/integration/smokes/` (chemin absolu), comparé selon P9.
- Validation : tous les contrôles du script passent ; contrat des 54 commandes inchangé ; règle P9.
- Commit : `docs(ai-agent): record the launched-editor run of the integrated editor`

## Phase 3 — Jeu

### ⏳ T3.1 — Le jeu sur le moteur intégré

- Objectif : P10 et la table de correspondance de P2.
- Fichiers (dépôt du jeu, branche `chantier/rgce-worlds-as-assets`) : pointeur `CasaEngine`, `ai-agent/HANDOVER.md`, `ai-agent/tasks/rgce-worlds-as-assets-tasks.md`, `ai-agent/README.md` ; un fichier de code du jeu seulement si P10 le demande.
- Étapes : pointeur sur la tête de la branche du moteur ; `dotnet build RacingGame.slnx` ; `RgceAssetCooker --worlds --verify` ; `m9_run.py` sur une copie neuve ; « Validation commune (parité) » du plan du jeu, points 1 à 6, sorties sous `artifacts/worlds-as-assets/integration/`.
- Validation : celle de P10 ; les empreintes des réglages de l'utilisateur sont celles notées en M1 avant et après.
- Commits (jeu) : `chore(engine): point CasaEngine at the engine with origin/main integrated`, puis `docs(ai-agent): record the integration of the engine's origin/main`.

## Phase 4 — Vérification, clôture, push

### ⏳ T4.1 — Vérification indépendante

- Étapes : `verifier` frais sur la tête intégrée et les commits du jeu : builds, tests, P8, parcours rejoué, refus de T1.4 et de P13 du plan précédent essayés par lui-même, contrat des commandes, M9, parité (relue sur les sorties de T3.1, rejouée au moins pour un genre), état des dépôts. Traitement des constats (`FIX`, `DEFER`, `REJECT`) ; un correctif de code relance l'acceptation concernée et une vérification.
- Validation : **CONFIRMED**.
- Commit : `docs(ai-agent): record the check of the integration of origin/main`

### ⏳ T4.2 — Archivage, avance, push

- Étapes : `git mv ai-agent/tasks/entity-asset-remote-tasks.md ai-agent/tasks/archive/` et lignes de `ai-agent/README.md` ; commit du moteur (le dernier) ; pointeur du jeu sur cette tête (commit du jeu) ; copies de P12 supprimées ; `fetch` des deux dépôts ; avance rapide contrôlée de `main` et de `remaster` (`git update-ref` avec l'ancienne valeur) ; `artifacts/push-main-remaster.ps1 -DryRun`, puis sans `-DryRun` ; `git ls-remote origin main` et `origin remaster` égaux aux têtes locales.
- Validation : les deux `ls-remote` ; arbres propres ; rapport de fin (`AGENTS.md` §12).
- Commits : `docs(ai-agent): archive the entity asset remote plan` (moteur), `chore(engine): point CasaEngine at the pushed engine main` (jeu).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P12 ; en particulier P3 (une commande git dans MGUI), P7 (`.` et segment vide refusés avec `..`), P10 (une différence de parité arrête tout avant le push) et P11 (l'approbation vaut demande de push pour les têtes produites). | Approbation |
| O2 | La parité du jeu tient-elle sur le runtime d'`origin/main` ? Inconnu avant T3.1. | T3.1 |
| O3 | La fusion compile-t-elle sans réparation ? Inconnu avant T1.2 (un seul conflit de texte, aucun build fait). | T1.2 |
| R1 | Risque : conflit sémantique dans `GameEditor.cs` (fermeture et enregistrement des documents) ; atténuation : T1.3, T2.1, T4.1. | T1.2, T1.3 |
| R2 | Risque : `origin/main` bouge encore avant le push ; atténuation : `fetch` et contrôle d'ancêtre en T4.2, arrêt sinon. | T4.2 |

## Hors périmètre

- Modifier le comportement apporté par `origin/main` ; les branches matériau, particule et son de l'hôte du pilotage ; l'ADR-0143.
- Pousser MGUI ou NvgSharp ; toucher aux autres branches locales.
- Accepter les ADR `Proposed` (0144, 0145 après renumérotation, 0041 et 0042 du jeu).
- Les points O5 du plan précédent (le panneau enregistre l'état après initialisation) et les écarts de rendu du jeu connus.
