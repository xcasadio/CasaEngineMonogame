# Plan agent IA — Intégrer `origin/main` dans le `main` local

Plan d'exécution d'une demande de l'auteur du 2026-11-19 : fusionner `origin/main` du moteur dans le `main` local, après la fusion de MGUI avec `origin/develop` demandée le même jour.
Les décisions D1 → D3 ci-dessous viennent de l'auteur : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P9 sont des arbitrages de l'agent, à confirmer par l'auteur.

L'auteur a demandé deux fois ce jour-là de continuer en mode autonome : ce plan est relu par un agent indépendant, exécuté, puis vérifié, sans attendre. Rien n'est poussé. Première relecture le 2026-11-19 : deux défauts corrigés (l'avance de `main` n'était dans aucune tâche : T3.2 ; le contrôle « aucun test perdu » n'était pas exécutable : comptage par fichier sur trois arbres).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le `main` local a 189 commits que `origin/main` n'a pas (cutscenes, vidéo, question de sauvegarde pour tout document). `origin/main` a 336 commits que le `main` local n'a pas (rendu PBR, pilotage de l'éditeur à distance, documents de texture et de modèle statique, interface sans allocation), et il épingle le MGUI d'`origin/develop`. Le `develop` local de MGUI contient déjà la fusion des deux côtés (`e24cc710`).

Ce plan livre une branche `chantier/integrate-origin-main` qui contient les deux histoires, compile, passe tous les tests, et garde les comportements des deux côtés ; puis le `main` local est avancé sur elle, sans commit de fusion sur `main`.

## État vérifié du dépôt (2026-11-19)

- `git rev-list --left-right --count main...origin/main` : 189 et 336. Base commune `778f6aa8`. `main` local à `e4114c97`, `origin/main` à `da32906e`.
- **Conflits de texte** (fusion d'essai du 2026-11-19, annulée) : 13 blocs dans 9 fichiers, plus le sous-module MGUI. `.claude/settings.json` (1), `CLAUDE.md` (1), `CasaEngine.Editor/GameEditor.cs` (3), `CasaEngine.Editor/History/EditorHistoryContextKind.cs` (1), `CasaEngine.EditorServices/EditorAssetWriterService.cs` (1), `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs` (1), `ai-agent/README.md` (1), `docs/README.md` (2), `docs/decisions/README.md` (2).
- **Numéros de décisions.** Le `main` local a 16 ADR numérotées 0089 à 0104 (`ls docs/decisions`). `origin/main` a ses propres ADR 0089 à 0127. Les numéros locaux sont cités dans 125 fichiers et 368 lignes (`rg -c "ADR-(0089|009[0-9]|010[0-4])"`, hors MGUI), dont des commentaires de code. Précédent sur `origin/main` : après une fusion, ce sont les décisions de la branche entrante qui sont renumérotées (commits `3c9ca70e`, `5d457865`) ; le fichier reçoit sous `Source` la ligne « Renumbered from ADR-00nn on <date> when origin/main was merged (its ADR-00nn is another decision) » et la ligne de l'index dit « (numbered ADR-00nn until <date>) ».
- **MGUI.** `develop` local à `e24cc710` : fusion d'`origin/develop` (`f6ed1fcd`, 70 commits) et des 4 commits locaux de l'élément Video ; build à 0 erreur, 5301 tests verts. Son index des décisions a deux lignes ADR-0026 (Video en local, texte par plage de caractères sur `origin`) ; l'ADR Video n'a jamais été poussée. Elle est citée dans 6 fichiers de MGUI et, côté moteur, dans `ai-agent/tasks/video-playback-tasks.md` et l'ADR locale 0089.
- **Le moteur local ne compile pas contre ce MGUI** : 19 erreurs dans `CasaEngine/Framework/UI/Backend/MonoGame/` (`ClipScope`, `DrawStateHandle`, `ClipHandle`, `BeginDrawTransaction`). `origin/main` contient l'adaptation de ce backend (il épingle `f6ed1fcd`).
- **Nouveaux documents sur `origin/main`** (ADR-0120 d'origin) : `_assetPreviewPanels` (aperçu en lecture seule), `_textureDocumentPanels` et `_staticModelDocumentPanels`, ces deux derniers modifiables, avec `GetTextureDocumentTitle`, `GetStaticModelDocumentTitle`, `SaveDirtyTextureDocuments`, `SaveDirtyStaticModelDocuments` et un retrait dans `OnDockHostPanelRemoved`. Sur `origin/main`, la question de sauvegarde ne couvre encore que les écrans.
- **Valeurs d'énumération.** `EditorAssetSaveSource` : `CutsceneEditorPanel = 9` en local ; `TextureEditorPanel = 9` et `StaticModelEditorPanel = 10` sur `origin/main`. La valeur n'est pas enregistrée dans un fichier : elle ne voyage que dans `EditorAssetSavedEventArgs` (`rg "EditorAssetSaveSource"`). Un test vérifie l'ordre d'ajout (`AudioMixerAssetEditorSerializationTests.EditorAssetSaveSource_AudioMixerPanelIsAppendedLast`). `EditorHistoryContextKind` : `Cutscene` en local, `Texture` et `StaticModel` sur `origin/main`, sans valeur explicite.
- **Tests.** `main` local : 6640 verts. Le nombre de tests d'`origin/main` n'a pas été mesuré.
- **Arbre de travail.** Sous-module `MGUI` modifié (pointe sur `e24cc710`). Deux dossiers non suivis, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Fusionner `origin/main` dans le `main` local du moteur (« Oui, fusionne », 2026-11-19). |
| D2 | MGUI reste sur son `develop` fusionné avec `origin/develop` (demande du même jour). |
| D3 | La question de sauvegarde vaut pour tout document ouvert et modifié (décision du 2026-11-19, ADR locale « Every modified document asks before its changes are lost ») : elle s'applique donc aussi aux documents modifiables apportés par `origin/main`. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | La fusion se fait sur la branche `chantier/integrate-origin-main`, créée depuis `main` ; `main` est ensuite avancé sur elle (avance rapide). Aucun commit n'est fait sur `main`. |
| P2 | Les numéros publiés gagnent. Les 16 ADR locales 0089 à 0104 deviennent 0128 à 0143 (même ordre, +39), avec la mention du précédent dans chaque fichier et dans l'index. La renumérotation est faite **avant** la fusion, quand toute mention « ADR-0089 » à « ADR-0104 » de l'arbre local désigne encore une décision locale. |
| P3 | Dans MGUI, l'ADR locale Video passe de 0026 à 0039 (les numéros 0026 à 0038 d'`origin/develop` sont publiés), avant que le moteur n'épingle MGUI. |
| P4 | Là où les deux côtés ont ajouté des lignes au même endroit, on garde les deux. Là où ils ont écrit différemment la même chose (`CLAUDE.md`, `.claude/settings.json`, lignes de `docs/README.md` et de l'ADR-0013 dans l'index), on prend le texte d'`origin/main`, en y ajoutant ce que le local disait de plus. |
| P5 | `EditorAssetSaveSource` : les valeurs publiées restent (`TextureEditorPanel = 9`, `StaticModelEditorPanel = 10`) ; `CutsceneEditorPanel` passe à 11. `EditorHistoryContextKind` : `Texture`, `StaticModel`, puis `Cutscene`. |
| P6 | Les erreurs de compilation et les tests rouges nés de la fusion sont réparés en adaptant le code local aux contrats d'`origin/main` et du nouveau MGUI. Le comportement d'`origin/main` n'est pas modifié ; si un choix entre deux comportements est nécessaire, la tâche passe en ⚠️ Blocked. |
| P7 | Le commit de fusion contient les réparations nécessaires pour compiler et passer les tests : il n'y a pas de commit qui ne compile pas. |
| P8 | Texture et modèle statique rejoignent les sortes qui posent la question de sauvegarde (D3) ; l'aperçu en lecture seule n'en fait pas partie. Les trois nouvelles tables sont vidées au changement de projet, comme les onze autres. |
| P9 | Les documents du dépôt parent ne sont pas réécrits : une table de correspondance des anciens et nouveaux numéros y est ajoutée. |

## Règles d'exécution pour l'agent

- **Branche `chantier/integrate-origin-main`** (P1). Ne jamais committer sur `main`. Ne jamais pousser, ni le moteur ni MGUI.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- Messages de commit en anglais, `type(area): summary`, terminés par `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`. Lancer `git commit` seul, sans `cd` enchaîné devant.
- **Ne rien inventer.** Un fait du plan qui se révèle faux, ou un choix entre deux comportements : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** : `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` à 0 erreur ; **tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` tous verts.
- Aucun test n'est supprimé ni affaibli pour passer. Un test qui échoue après la fusion est réparé dans le code, ou adapté seulement quand il vérifiait un détail que la fusion change légitimement (une valeur d'énumération de P5, un numéro d'ADR) ; chaque adaptation est notée.
- `git add` fichier par fichier ; jamais `Projects/PixelUiSample/.casaeditor/` ni `Projects/SpriteCutsceneSample/.casaeditor/`.
- Éditeur lancé : uniquement par ses options d'automatisation, sorties hors du dépôt, jamais `--keep-open`, aucune entrée envoyée, aucun asset enregistré.
- Aucun fichier, commentaire ou message de commit du moteur ou de MGUI ne nomme un jeu ou un projet extérieur.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions du moteur à 0 erreur ; `MGUI.sln` à 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` et `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` : tous verts.
- **Aucun test perdu dans la fusion**, contrôlé sur les sources, sans build : pour chaque fichier de `CasaEngine.Tests/`, compter les lignes `[Fact` et `[Theory` sur quatre arbres par `git grep -c -E "\[(Fact|Theory)" <rev> -- CasaEngine.Tests` : la base `778f6aa8`, le local `e4114c97`, `origin/main` `da32906e` et la tête fusionnée. Règle, fichier par fichier (un fichier absent compte 0) : `fusionné = local + origin − base`. Tout écart est listé avec le nom du fichier et expliqué (test ajouté ou retiré par une tâche de ce plan). Attendu avant T2.1 : aucun écart.
- `git merge-base --is-ancestor origin/main HEAD` et `git merge-base --is-ancestor e4114c97 HEAD` rendent vrai sur la branche ; après T3.2, `git rev-parse main` égale `git rev-parse chantier/integrate-origin-main` et `git merge-base --is-ancestor origin/main main` rend vrai.
- Lancement automatisé de l'éditeur sur `Projects/SpriteCutsceneSample` (ouverture d'une cutscene, capture) : le processus se termine seul, code 0, et la capture montre l'éditeur.

---

## Phase 0 — Numéros de décisions

### ✅ T0.1 — Renuméroter les 16 décisions locales du moteur

- Objectif : P2. Après cette tâche, aucun numéro d'ADR du `main` local ne chevauche ceux d'`origin/main`.
- Fichiers : `docs/decisions/0089-…md` à `0104-…md` (renommés), `docs/decisions/README.md`, et tout fichier du dépôt qui cite ces numéros (hors `MGUI/`, `NvgSharp/`, `bin/`, `obj/`).
- Étapes :
  1. Écrire un script (hors du dépôt) qui applique la table 0089→0128, 0090→0129, … 0104→0143 : renommer les 16 fichiers par `git mv` ; remplacer partout le jeton `ADR-00nn` (borné : pas suivi d'un chiffre) ; remplacer le préfixe de nom de fichier `00nn-<slug exact>` pour les 16 slugs. Remplacer en une passe, par table, pour qu'un numéro déjà remplacé ne soit pas remplacé une seconde fois.
  2. Dans chaque fichier renommé, ajouter sous la ligne `Source` : `- Renumbered from ADR-00nn on 2026-11-19 when origin/main was merged (its ADR-00nn is another decision)`. Dans l'index, ajouter au titre de chaque ligne `(numbered ADR-00nn until 2026-11-19)`.
  3. Chercher les numéros écrits sans le préfixe `ADR-` (par exemple « ADR-0096 et 0097 », « 0095 à 0103 ») : `rg -n --pcre2 "(?<![A-Za-z0-9-])(0089|009[0-9]|010[0-4])(?![0-9-])"`, et corriger à la main ceux qui désignent une décision.
  4. Contrôles : `rg "ADR-(0089|009[0-9]|010[0-4])\b"` ne renvoie plus que les mentions « Renumbered from » et « numbered … until » ; chaque lien de l'index pointe sur un fichier existant ; les 16 fichiers ont un titre `# ADR-01nn`.
- Validation : les contrôles de l'étape 4 ; `dotnet build CasaEngine.Editor.MonoGame.sln` n'est pas requis ici (le moteur ne compile pas avant la fusion, voir l'état vérifié) : vérifier seulement par `git diff --stat` que les fichiers `.cs` touchés ne changent que dans des commentaires.
- Commit : `docs(decisions): renumber the local records 0089-0104 to 0128-0143 before merging origin/main`
- Note de validation (2026-11-19) : script `renumber.py` hors dépôt (dossier temporaire de la session), un seul passage par table (jeton `ADR-00nn` borné, et préfixe `00nn-<slug exact>` des 16 fichiers). 16 fichiers renommés par `git mv` ; 469 remplacements dans 125 fichiers texte (75 `.cs`, 1 `.csproj`, 1 `.ini`, 48 `.md`), hors `MGUI/`, `NvgSharp/`, `bin/`, `obj/` ; aucun binaire touché (les `.png`, `.wav`, `.glb`, `.jpg` sont ignorés, et 10 fichiers `.cs` non UTF-8 de `CasaEngine/Framework/AI` sont intacts : aucun jeton). Fins de ligne et BOM conservés (lecture et écriture en octets) ; `git diff --stat` : 125 fichiers, 397 insertions, 381 suppressions. Les lignes `.cs`, `.csproj` et `.ini` changées ne contiennent que des commentaires (aucune ligne de code ni littéral).
  - Étape 2 : ligne `- Renumbered from ADR-00nn on 2026-11-19 …` ajoutée sous la ligne `Source` des 16 fichiers ; 16 lignes de l'index complétées par `(numbered ADR-00nn until 2026-11-19)`.
  - Étape 3 (numéros sans `ADR-`) : 7 mentions corrigées à la main, des numéros réservés par des plans (`Prochain numéro libre : 0090` devient 0129, 0091 devient 0130, 0092 devient 0131, 0093 devient 0132 ; `Dernier ADR : 0093. Ce plan prend 0094` devient 0132 et 0133 ; `le dernier ADR est le 0094 … libre est 0095` devient 0133 et 0134 ; `0089 pour le moteur` devient 0128). Non touchés car ce ne sont pas des décisions : `0.0096` (test Doppler), la chaîne binaire `"0100"` (test MDEC), `sprite_0100` à `sprite_0102` (document de l'éditeur d'animation).
  - Étape 4 : `rg "ADR-(0089|009[0-9]|010[0-4])"` ne renvoie que les 16 lignes « Renumbered from » et les 16 lignes de l'index « numbered … until » ; 104 liens de l'index, aucun fichier manquant ; aucun numéro en double dans l'index ; les 16 fichiers 0128 à 0143 ont le titre `# ADR-01nn`. Les mentions « ADR-0026 de MGUI » et « ADR-0097 du moteur » citées dans `MGUI/` ne sont pas touchées (MGUI est exclu ; T0.2 traite la première).

### ✅ T0.2 — Renuméroter la décision Video de MGUI

- Objectif : P3.
- Fichiers (dans `MGUI/`, branche `develop`) : `Docs/decisions/0026-a-video-element-plays-host-video-sources.md` (renommé en `0039-…`), `Docs/decisions/README.md`, et les fichiers de MGUI qui citent l'ADR Video : `MGUI.Shared/Assets/IUIVideo.cs`, `MGUI.Shared/Assets/IUIAssetProvider.cs`, `MGUI.Tests/Architecture/AssetProviderTests.cs`, `MGUI.Tests/Architecture/VideoElementTests.cs`, `MGUI.Core/UI/XAML/Controls.cs`, `MGUI.Core/UI/MGVideo.cs`. Côté moteur : les mentions « ADR-0026 de MGUI » ou « MGUI ADR-0026 » (`ai-agent/tasks/video-playback-tasks.md`, `ai-agent/README.md`, l'ADR renumérotée 0128).
- Étapes :
  1. Dans MGUI : `git mv` du fichier, titre `# ADR-0039`, ligne « Renumbered from ADR-0026 on 2026-11-19 when origin/develop was merged (its ADR-0026 is another decision) », ligne de l'index déplacée après ADR-0038 avec « (numbered ADR-0026 until 2026-11-19) ». Ne changer que les mentions qui désignent la Video : `Docs/text-architecture.md`, `MGUI.Tests/Architecture/HostRuntimeContractTests.cs` et `0026-text-as-a-span-of-characters.md` désignent l'autre ADR-0026 et ne changent pas.
  2. `dotnet build MGUI/MGUI.sln` à 0 erreur. Commit dans MGUI, sur `develop` : `docs(decisions): renumber the video element record to ADR-0039`.
  3. Dans le moteur : remplacer les mentions de l'ADR-0026 de MGUI qui désignent la Video par ADR-0039. Commit moteur : `docs(decisions): follow the new number of the video element record of MGUI` (le pointeur du sous-module n'est pas indexé ici : il l'est par T1.1).
- Validation : `rg "ADR-0026" MGUI --glob "!**/bin/**" --glob "!**/obj/**"` ne renvoie plus que l'ADR du texte et ses citations ; build de MGUI à 0 erreur.
- Note de validation (2026-11-19) : dans MGUI (`develop`, commit `2f9773d0`), fichier renommé par `git mv` en `0039-a-video-element-plays-host-video-sources.md`, titre `# ADR-0039`, ligne « Renumbered from ADR-0026 » sous `Source`, ligne de l'index déplacée après ADR-0038 avec « (numbered ADR-0026 until 2026-11-19) » ; 6 fichiers de code et de tests changés (commentaires seulement). `rg "ADR-0026" MGUI` ne renvoie plus que l'ADR du texte, ses citations (`text-architecture.md`, `HostRuntimeContractTests.cs`) et la ligne « Renumbered from ». `dotnet build MGUI/MGUI.sln` : 0 erreur. Côté moteur : ADR-0128, `ai-agent/README.md` et `video-playback-tasks.md` (7 mentions) suivent ADR-0039. Le pointeur MGUI n'est pas indexé (T1.1). Les tests de MGUI n'ont pas été relancés (commentaires seulement ; T3.1 le fait).

---

## Phase 1 — Fusion

### ✅ T1.1 — Fusionner `origin/main`, résoudre, compiler, passer les tests

- Objectif : D1, P4 à P7. Un seul commit de fusion, qui compile et dont les tests passent.
- Fichiers : les 9 fichiers en conflit, le sous-module `MGUI`, et le code local à adapter.
- Étapes :
  1. `git merge --no-ff --no-commit origin/main` sur `chantier/integrate-origin-main`.
  2. Résoudre, fichier par fichier :
     - `MGUI` (sous-module) : pointer sur la tête du `develop` local après T0.2 (`git add MGUI`).
     - `CasaEngine.Editor/GameEditor.cs`, bloc des `using` : garder les deux lignes.
     - `GameEditor.cs`, bloc du retrait d'un panneau : le côté local est dans `ReleaseDocumentPanel(string panelId)` ; prendre les trois blocs d'`origin/main` (texture, modèle statique, aperçu) en y remplaçant `panel.Id` par `panelId`, puis la ligne locale du mixeur audio.
     - `GameEditor.cs`, bloc des méthodes d'automatisation (734 lignes locales, 19 d'origin) : garder les deux, sans rien retirer.
     - `EditorHistoryContextKind.cs` et `EditorAssetWriterService.cs` : P5.
     - `EditorAutomationOptionsTests.cs` : garder les tests des deux côtés, en refermant chaque méthode.
     - `.claude/settings.json`, `CLAUDE.md`, `docs/README.md`, `docs/decisions/README.md`, `ai-agent/README.md` : P4. Dans l'index des décisions, les lignes d'`origin/main` (0089 à 0127) puis les lignes locales renumérotées (0128 à 0143) ; la ligne de l'ADR-0014 garde la mention locale de la décision remplacée, avec le nouveau numéro.
  3. Vérifier qu'il ne reste aucun marqueur de conflit (`rg -n "^(<<<<<<<|=======|>>>>>>>)( |$)"` sur les fichiers suivis touchés).
  4. Compiler les deux solutions. Réparer chaque erreur selon P6 : le code local (éditeur de cutscenes, vidéo, question de sauvegarde, tests) s'adapte aux contrats d'`origin/main` et du nouveau MGUI. Noter dans la tâche chaque fichier adapté et pourquoi.
  5. Passer les tests du moteur. Réparer selon les règles d'exécution. Noter chaque test adapté.
  6. Committer la fusion (message par défaut de Git, complété d'un paragraphe qui liste les conflits résolus et les adaptations).
- Validation : aucun marqueur ; les deux solutions à 0 erreur ; tests du moteur tous verts, total noté ; `git merge-base --is-ancestor origin/main HEAD` vrai.
- Commit : `Merge remote-tracking branch 'origin/main' into chantier/integrate-origin-main`
- Note de validation (2026-11-19) : `git merge --no-ff --no-commit origin/main` : 9 fichiers en conflit plus le sous-module, comme prévu (13 blocs). Résolutions :
  - `MGUI` : `git add MGUI` ; l'arbre de MGUI est resté sur la tête de `develop` (`2f9773d0`, T0.2), contrôlé par `git -C MGUI rev-parse HEAD` et `branch --show-current`.
  - `GameEditor.cs` : `using` des deux côtés ; `ReleaseDocumentPanel` : les trois blocs d'`origin/main` (texture, modèle statique, aperçu) avec `panelId` à la place de `panel.Id`, puis le mixeur audio local ; méthodes d'automatisation : les deux côtés gardés (méthodes locales de la cutscene, puis `TryOpenAutomationAsset` d'`origin/main`), aucune ligne retirée.
  - `EditorHistoryContextKind.cs` : `Texture`, `StaticModel`, `Cutscene` (P5). `EditorAssetWriterService.cs` : `TextureEditorPanel = 9`, `StaticModelEditorPanel = 10`, `CutsceneEditorPanel = 11` (P5).
  - `EditorAutomationOptionsTests.cs` : les tests des deux côtés, chaque méthode refermée.
  - `CLAUDE.md` : texte d'`origin/main` (P4). `docs/README.md` : première ligne d'`origin/main`, complétée de ce que le local disait de plus (vidéo, `AnyKey`, ADR-0130 et ADR-0128 dans la ligne de `data-driven-screens.md`) ; lignes de `cutscene-editor.md` et `asset-documents.md` gardées toutes deux. `ai-agent/README.md` et `docs/decisions/README.md` : lignes d'`origin/main` puis lignes locales (0128 à 0143) ; ADR-0013 d'`origin/main` (« amended by ADR-0103 »), ADR-0014 locale (« decision 2 superseded by ADR-0135 »).
  - `.claude/settings.json` : **écart au texte de P4, à confirmer par l'auteur.** Le local a retiré le hook qui interdit un commit sur `main` et un push, son script et la règle `deny` (commit `e32ff241`, le jour même) ; `origin/main` garde ce hook, inchangé depuis la base, et ajoute `mcp__codegraph__*` et `enabledMcpjsonServers`. Prendre le texte d'`origin/main` aurait rétabli un hook dont le script est supprimé (le script ne figurait pas dans les conflits : suppression locale gardée). Résultat retenu, celui de la fusion à trois points : permissions locales, plus `mcp__codegraph__*`, plus `enabledMcpjsonServers` ; ni hook ni `deny`. `CLAUDE.md` et `AGENTS.md` décrivent encore ce hook (déjà le cas avant la fusion, non touché).
  - Aucun marqueur de conflit restant (`rg` sur l'arbre hors `MGUI`, `bin`, `obj`).
- Compilation : `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` à 0 erreur du premier coup (aucun membre en double hors des blocs de conflit ; aucun fichier du code local à adapter aux contrats d'`origin/main` ou du nouveau MGUI : l'adaptation du backend MGUI est déjà dans `origin/main`). Contrôle des événements d'entrée de MGUI réutilisés (ADR-0034 de MGUI) dans le code local (`CutsceneOutlinerPanel`, `CutsceneOutlinerPanel.Drag`, `CutsceneTextCommitBinding`, `Timeline*`, `Animation2dAssetInspectorPanel`, `WorldViewportPanel`) : aucun champ, collection ni lambda ne garde l'objet après le gestionnaire ; les gestionnaires copient `Position` et `EndPosition` (ou lisent la valeur dans l'appel).
- Tests : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 8031 sur 8031 verts après une adaptation. Un seul échec avant : `CutsceneSaveTests.TheSaveSourceOfTheCutsceneEditorIsTheNextValue` attendait `CutsceneEditorPanel = 9` ; la valeur devient 11 (P5, valeur d'énumération), le test garde son nom et sa portée (la valeur de la cutscene reste la suivante après 9 et 10). Aucun autre test adapté, aucun supprimé ni affaibli. Aucun éditeur n'était lancé : sorties de build dans les `bin/` du dépôt.

---

## Phase 2 — La question de sauvegarde et les nouveaux documents

### 🧪 T2.1 — Texture et modèle statique posent la question

- Objectif : D3, P8.
- Fichiers : `CasaEngine.Editor/GameEditor.cs` ; `docs/editor/editor-message-boxes.md` ; le document d'`origin/main` sur ces documents (`docs/editor/asset-documents.md`) s'il parle de la fermeture.
- Étapes :
  1. Lire `TextureAssetDocumentPanel` et `StaticModelAssetDocumentPanel` : comment ils disent « modifié », comment `GetTextureDocumentTitle` et `GetStaticModelDocumentTitle` affichent ` *`, ce que font `SaveDirtyTextureDocuments` et `SaveDirtyStaticModelDocuments`, et si leur enregistrement écrit toujours ou saute un document non modifié.
  2. Leur donner les mêmes pièces que les neuf autres sortes (voir `ai-agent/tasks/editor-unsaved-documents-tasks.md`, T1.3) : `Is<Sorte>DocumentModified` lu par le titre, `TrySave<Sorte>Document` avec le même contrat (un document qui se lit modifié est réellement écrit ; un vrai garantit qu'il ne se lit plus modifié ; un document non modifié n'est jamais réécrit ; jamais de marquage « enregistré » sans écriture), la boucle `SaveDirty…` qui l'appelle, une entrée dans `TryGetSavableDocument` et dans `CollectSavableDocuments` (libellés `Texture` et `Static Model`).
  3. Ajouter les trois nouvelles tables à `CollectOpenDocumentPanelIds`, pour que le changement de projet les vide ; vérifier que `ReleaseDocumentPanel` les libère (résolution de T1.1).
  4. Mettre à jour `docs/editor/editor-message-boxes.md` : onze sortes posent la question ; l'aperçu en lecture seule, la tile map et le clip d'animation non.
- Validation : les deux solutions à 0 erreur ; tests verts. Lecture du diff, à écrire dans la note, pour les deux sortes : mêmes points (a) à (e) que T1.3 du plan cité. Statut 🧪 : le clic réel reste à faire par l'auteur.
- Commit : `feat(editor): ask to save a modified texture or static model document`
- Note de validation (2026-11-19) : dans `GameEditor.cs`, `IsTextureDocumentModified` et `IsStaticModelDocumentModified` (contexte d'historique OU drapeau du panneau, la condition que le titre affichait déjà), lues par `GetTextureDocumentTitle` et `GetStaticModelDocumentTitle` ; `TrySaveTextureDocument` et `TrySaveStaticModelDocument` ; `SaveDirtyTextureDocuments` et `SaveDirtyStaticModelDocuments` appellent ces méthodes ; `TryGetTextureDocumentPanel` et `TryGetStaticModelDocumentPanel` (préfixe d'identifiant puis table, comme le son) ; `CreateTextureSavableDocument` (libellé `Texture`) et `CreateStaticModelSavableDocument` (libellé `Static Model`), ajoutés à `TryGetSavableDocument` et à `CollectSavableDocuments` (après les sons, avant le monde). `CollectOpenDocumentPanelIds` reçoit les trois nouvelles tables (texture, modèle statique, aperçu en lecture seule) ; `ReleaseDocumentPanel` les libérait déjà (T1.1), contrôlé par lecture : texture et modèle statique par `CloseTextureDocumentPanel` / `CloseStaticModelDocumentPanel` (désabonnement, `Dispose`, table, titre, état modifié), aperçu par retrait de sa table. Documentation : `docs/editor/editor-message-boxes.md` (onze sortes ; aperçu en lecture seule, tile map et clip d'animation non) et `docs/editor/asset-documents.md`.
  - Lecture du panneau (étape 1) : texture et modèle statique ont un drapeau `IsDirty` (`SetDirty`, événement `DirtyStateChanged`) ; leur `TrySaveLoadedAsset` écrit **toujours**, sans test « déjà enregistré » : le test « modifié » vient donc en premier dans `TrySave<Sorte>Document`, et aucune surcharge d'écriture forcée n'est nécessaire (le panneau n'a pas de test interne à sauter). Aucun code ne marque le contexte d'historique Texture ou StaticModel modifié (seulement `MarkSaved`, `Remove`), donc le drapeau du panneau est l'état réel ; la condition OU garde la même forme que les autres sortes.
  - Lecture du diff, mêmes points que T1.3 : (a) le titre et « est modifié » lisent la même méthode, pour les deux sortes ; (b) après un `TrySave<Sorte>Document` qui rend vrai, « est modifié » est faux : `MarkSaved` du contexte, `SetDirty(false)` dans l'écriture du panneau, et le retour est `!Is<Sorte>DocumentModified` ; (c) un document non modifié n'est jamais réécrit : la première ligne rend vrai sans écrire ni journaliser ; (d) « Save project » garde ses effets (écriture, `MarkSaved`, titre, journal `Texture asset saved: <chemin>` et `Static model asset saved: <chemin>` aux mêmes textes) ; il n'écrit plus un document dont le contexte seul se lirait modifié sans que le drapeau le soit, cas qui n'existe pas ici (aucun marquage) ; en cas d'échec, l'avertissement est le même (`WarnSaveFailed`) ; (e) `MarkSaved` n'est appelé qu'après un `TrySaveLoadedAsset` qui rend vrai, dans le même appel. Les méthodes d'automatisation (`--set-texture-sampler`, `--set-static-model-material`) et la restauration après capture (`ReloadFromDisk` puis `MarkSaved`) ne sont pas touchées.
  - Build : `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` à 0 erreur, aucun avertissement dans `GameEditor.cs` ; `CasaEngine.Tests` : 8031 sur 8031 verts, aucun test modifié ni ajouté (aucun test ne construit `GameEditor` ni ces panneaux, qui demandent un `GraphicsDevice`). Aucun éditeur n'était lancé : sorties de build dans les `bin/` du dépôt. Statut 🧪 : le clic réel (fermer, quitter, changer de projet avec une texture ou un modèle statique modifié) reste à faire par l'auteur.

---

## Phase 3 — Clôture

### 🧪 T3.1 — Index, contrôle par lancement, clôture

- Objectif : apporter la preuve atteignable sans entrée et clore le plan.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes :
  1. Vérifier les index fusionnés : chaque lien de `docs/decisions/README.md`, `docs/README.md` et `ai-agent/README.md` pointe sur un fichier existant ; aucun numéro d'ADR n'apparaît deux fois dans l'index des décisions.
  2. Compiler `MGUI/MGUI.sln` et lancer `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.
  3. Lancer l'éditeur sur `Projects/SpriteCutsceneSample` avec `--open-asset Cutscenes/stagger.cutscene`, `--activate-panel cutscenetimeline`, `--diagnostics-out` et `--screenshot-out` hors du dépôt (forme des options dans `docs/editor/cutscene-editor.md`). Noter le code de sortie et le chemin de la capture ; `git status` ne doit montrer aucun fichier de `Projects/` modifié.
  4. Faire le contrôle « aucun test perdu » de la validation globale (comptage par fichier sur les quatre arbres) et noter dans la tâche les totaux des quatre arbres et la liste des écarts.
  5. Ajouter ce plan au tableau de `ai-agent/README.md` et noter la validation globale.
- Validation : les contrôles ci-dessus, notés avec le fichier lu.
- Commit : `docs(plan): record the validation of the integration of origin/main`
- Note de validation (2026-11-19) :
  - Index : script hors dépôt ; `docs/decisions/README.md` (144 liens), `docs/README.md` (85) et `ai-agent/README.md` (145) : aucun lien vers un fichier manquant ; 143 lignes d'ADR dans l'index des décisions, aucun numéro en double (O1 : les deux séries ne se mélangent pas).
  - MGUI : `dotnet build MGUI/MGUI.sln` 0 erreur ; `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` 5301 sur 5301 verts.
  - Lancement : aucun `CasaEngine.Editor.exe` en cours ; `CasaEngine.Editor.exe --project Projects/SpriteCutsceneSample/SpriteCutsceneSample.json --open-asset Cutscenes/stagger.cutscene --activate-panel cutscenetimeline --diagnostics-out … --screenshot-out …` : code de sortie 0, sorties dans `%TEMP%/t31out2` (`diag.txt`, `shot-final.png`, hors dépôt, le nom de la capture reçoit le suffixe `-final`). La capture montre l'éditeur avec la cutscene `SpriteStagger` ouverte et la timeline au premier plan ; le diagnostic donne « Activated dock panel 'panel_cutscene_timeline' » et la timeline attachée. `git status` : aucun fichier de `Projects/` modifié (seuls les deux dossiers `.casaeditor/` non suivis, jamais indexés).
  - Aucun test perdu : script hors dépôt (`git grep -c -E "\[(Fact|Theory)"` sur `CasaEngine.Tests`, règle `fusionné = local + origin − base` par fichier). Totaux de lignes `[Fact`/`[Theory` : base `778f6aa8` 3938 (445 fichiers), local `e4114c97` 4942 (515), `origin/main` `da32906e` 4695 (518), tête fusionnée 5699 (588). 4942 + 4695 − 3938 = 5699. **Écarts : aucun** (0 fichier sur 588), ce qui est le résultat attendu avant T2.1 et reste vrai après (T2.1 n'a ajouté ni retiré de test).
  - Ce plan est ajouté au tableau de `ai-agent/README.md`.
  - Validation globale : deux solutions du moteur à 0 erreur et 8031 tests verts (T1.1, T2.1), MGUI à 0 erreur et 5301 verts, ancêtres vérifiés en T1.1, lancement de contrôle fait. Reste : T3.2 (avance de `main`, après la vérification indépendante), le clic réel de T2.1 par l'auteur, la confirmation de l'écart à P4 sur `.claude/settings.json`, O2 et O3.

### ⏳ T3.2 — Avancer `main` sur la branche d'intégration

- Objectif : D1 et P1. À faire seulement après la vérification indépendante de la branche, si elle confirme.
- Fichiers : aucun fichier ; la référence `main` du moteur.
- Étapes :
  1. Préconditions : `git rev-parse main` rend encore `e4114c97` (sinon ⚠️ Blocked) ; l'arbre de travail est propre, hors les deux dossiers `.casaeditor/` non suivis ; la vérification indépendante a rendu CONFIRMED.
  2. Avancer `main` par avance rapide seulement, sans commit sur `main` et sans push : `git fetch . chantier/integrate-origin-main:main`, qui refuse tout ce qui n'est pas une avance rapide. Puis `git checkout main`.
  3. Contrôles, à noter : `git rev-parse main` égale `git rev-parse chantier/integrate-origin-main` ; `git merge-base --is-ancestor origin/main main` rend vrai ; `git merge-base --is-ancestor e4114c97 main` rend vrai.
  4. Retour arrière, si l'auteur le demande : `git branch -f main e4114c97` (depuis une autre branche), et dans MGUI `git branch -f develop e24cc710` pour défaire T0.2. Rien n'étant poussé, ces deux commandes suffisent.
- Validation : les trois contrôles de l'étape 3. Cette tâche ne fait pas de commit : sa note est écrite dans le plan par un commit sur la branche d'intégration avant l'étape 2 (`docs(plan): close the integration of origin/main`), la note disant que l'avance suit.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les plans et notes d'`origin/main` citent ses propres ADR 0089 à 0127 ; après la fusion, un même fichier ne mélange pas les deux séries, puisque les numéros locaux sont changés avant. À revérifier à la relecture de l'index. | T0.1, T3.1 |
| O2 | Trois anciens commits du `main` local contiennent, dans un plan, quatre lignes qui citent un motif de recherche de noms de projets extérieurs (tête corrigée par `30ddab40`). L'historique n'est pas réécrit par ce plan ; à décider par l'auteur avant tout push. | Aucune |
| O3 | Les fonctionnalités d'`origin/main` (rendu PBR, pilotage à distance, nouveaux documents) ne sont pas recettées par ce plan : il vérifie qu'elles compilent, que leurs tests passent et que l'éditeur démarre. | T3.1 |

## Hors périmètre

- Pousser quoi que ce soit, moteur ou MGUI.
- Réécrire l'historique (O2).
- Modifier le comportement d'une fonctionnalité d'`origin/main`, au-delà du raccord des deux nouvelles sortes de documents à la question de sauvegarde.
- Mettre à jour le pointeur du sous-module dans le dépôt parent et son `AGENTS.md` : sur demande de l'auteur.
