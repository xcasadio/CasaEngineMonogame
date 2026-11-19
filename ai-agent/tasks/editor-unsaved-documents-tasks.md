# Plan agent IA — La question de sauvegarde pour tous les documents modifiés

Plan d'exécution d'une demande de l'auteur faite le 2026-11-19, après sa recette de l'éditeur de cutscenes : « La question doit être posée pour tous les documents ouverts et édités. Je pensais que c'était un comportement général pour chaque visualisation ouverte. »
Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur le 2026-11-19 : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P14 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

L'auteur a choisi le mode autonome (D5) : ce plan est approuvé d'avance, relu par un agent indépendant, exécuté, puis vérifié, sans attendre. Première relecture le 2026-11-19 : trois défauts corrigés (fermeture par `ClosePanel` et non `RemovePanel` au changement de projet ; une seule condition « modifié », P14 ; preuve du lancement de contrôle ramenée à ce qui se lit dans un fichier). Deuxième relecture le même jour : un défaut corrigé (un document qui se lit modifié est toujours réellement écrit par `Save`, voir l'Animation2D dans l'état vérifié et P14).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Aujourd'hui, l'éditeur demande « sauvegarder ? » avant de perdre des modifications dans trois cas seulement : la fermeture d'un écran d'UI ou d'une cutscene modifiés, la sortie de l'éditeur avec des écrans d'UI modifiés, l'ouverture d'un autre monde quand le monde courant est modifié. Tout autre document modifié se perd sans question.

Ce plan généralise la question à **tout document ouvert et modifié**, sur trois chemins :

1. la fermeture d'un onglet par l'utilisateur ;
2. la sortie de l'éditeur (documents et monde) ;
3. l'ouverture ou la création d'un autre projet.

Il aligne aussi le document Son sur les autres : titre avec ` *` quand il est modifié, et enregistrement par « Save project ».

## État vérifié du dépôt (2026-11-19)

Les numéros de ligne valent pour la tête de branche `b5b4ae05`.

- **Sortes de documents.** Recensement du 2026-11-19 (quatre agents en lecture seule, faits décisifs relus par la session principale). Neuf sortes sont modifiables et savent dire si elles sont modifiées : écran d'UI (`UIScreenPreviewPanel`, état dans `_editorDirtyState`), cutscene (`CutsceneAssetInspectorPanel.IsDirty`), matériau (`MaterialAssetInspectorPanel.IsDirty`), sprite (`SpriteAssetInspectorPanel.IsDirty`), particules (`ParticleAssetInspectorPanel.IsDirty`), Animation2D (`Animation2dAssetInspectorPanel.IsDirty`), entité (`EntityAssetEditorPanel.IsDirty`), mixeur audio (`AudioMixerPanel.IsDirty`), son (`SoundAssetInspectorPanel.IsDirty`, `SoundAssetInspectorPanel.cs:87`). Deux sortes ne sont pas modifiables et n'ont ni état modifié ni sauvegarde : tile map (`TileMapEditorPanel`) et clip d'animation (`AnimationClipPreviewPanel`). Tables des panneaux ouverts : `GameEditor.cs:215-238`.
- **Fermeture d'un onglet.** `GameEditor.OnDockHostPanelClosing` (`GameEditor.cs:2943`) ne retient que les écrans d'UI et les cutscenes : `IsDocumentAskedBeforeClose` (`GameEditor.cs:2961-2962`). La logique est dans `ModifiedScreenCloseCoordinator` (`CasaEngine.Editor/History/`), pure et testée (`CasaEngine.Tests/Editor/ModifiedScreenCloseCoordinatorTests.cs`, 13 tests). `MGDockHost.PanelClosing` est levé pour toute fermeture par l'utilisateur (croix, `Close`, `Close Others`, `Close All`, onglet d'une fenêtre flottante, tiroir, fenêtre flottante entière) et jamais pour `RemovePanel`, `ClosePanel` ou `CloseFloatingWindow` appelés par le code (`MGDockHost.cs:105-145`).
- **Sortie.** `GameEditor.OnExiting` (`GameEditor.cs:3086-3095`) ne consulte que `_modifiedScreensExitCoordinator`, alimenté par `GetModifiedScreenTitles` (`GameEditor.cs:3097`), qui ne liste que les écrans d'UI. Ni les autres documents ni le monde ne sont demandés. `ModifiedScreensExitCoordinator` est pur et testé (10 tests).
- **Monde.** Le monde n'est demandé qu'à l'ouverture d'un autre monde : `OpenWorldAsset` (`GameEditor.cs:4843`), `AskSaveBeforeOpeningWorld` (`GameEditor.cs:4919`), `TrySaveProjectBeforeOpeningWorld` (`GameEditor.cs:4943`, qui appelle `SaveCurrentProject` puis rend « le monde n'est plus modifié »). Son onglet ne se ferme pas (`CanClose = false`, `GameEditor.cs:2371-2374`). `OpenWorldAsset` arrête une session de jeu avant de poser la question (`GameEditor.cs:4862-4866`).
- **« Save project ».** `SaveCurrentProject` (`GameEditor.cs:2581`) refuse pendant une session de jeu, puis enregistre les documents modifiés par des boucles `SaveDirty…` (`GameEditor.cs:5758-5940`), puis le projet et le monde. Aucune ligne pour le son. La sauvegarde d'un seul document n'existe en méthode que pour l'écran (`TrySaveScreenDocument`) et la cutscene (`TrySaveCutsceneDocumentBeforeClose`).
- **Son.** `TryOpenSoundAsset` (`GameEditor.cs:5093`) pose le titre une fois, sans ` *`, et ne s'abonne pas à `DirtyStateChanged`. Rouvrir un son déjà ouvert rappelle `LoadAsset` (`GameEditor.cs:5110`), qui remplace l'asset et appelle `SetDirty(false)` (`SoundAssetInspectorPanel.cs:180-192`) : les modifications non enregistrées sont perdues. Le son n'a pas de contexte d'historique.
- **Changement de projet.** Le lanceur appelle `QueueProjectOpen` ou `QueueProjectCreate` (`GameEditor.cs:2537`, `2553`), exécutés à la frame suivante par `ProcessPendingProjectLauncherAction`. `OpenProjectLauncher` refuse pendant une session de jeu (`GameEditor.cs:2510`). `OnProjectLoaded` (`GameEditor.cs:634`) appelle `_editorDirtyState.ClearAll()` et `_editorHistory.ClearAll()` sans rien demander. Les tables de documents ne sont vidées que par `OnDockHostPanelRemoved` (`rg "Panels\.(Clear|Remove)\(" CasaEngine.Editor/GameEditor.cs`) ; or le remplacement du layout ne lève pas `PanelRemoved` (`MGDockHost.ApplyLoadedLayoutModel`, `MGDockHost.cs:1286` ; `ResetDockLayoutToDefault`, `GameEditor.cs:2902`). Les documents du projet quitté restent donc dans les tables, sans onglet, avec leurs chemins relatifs à l'ancien projet.
- **Boîtes de message.** `EditorMessageBoxes.AskSave(title, message, answered)` (`EditorMessageBoxes.cs:61`), boutons `Save`, `Don't Save`, `Cancel`, une boîte à la fois. La décision est `ModifiedScreenCloseDecision.ApplyAnswer`.
- **Automatisation.** Sous automatisation (`_automationOptions.HasAutomation`), rien n'est demandé à la fermeture ni à la sortie ; la sortie écrit les écrans abandonnés dans le journal (`GameEditor.cs:3116-3122`). Le texte « modified screen(s) abandoned » n'est lu par aucun test (`rg "modified screen\(s\) abandoned"` : `GameEditor.cs`, un document et deux plans).
- **« Est modifié » n'a pas la même source partout** (relecture du 2026-11-19). Écran : `_editorDirtyState` seul. Cutscene et entité : `IsDirty` du panneau lit `EditorDirtyStateService`. Matériau, sprite, particules, Animation2D : drapeau `_isDirty` du panneau, mais le titre affiche ` *` pour « `_editorDirtyState.IsDirty(contexte)` OU `panel.IsDirty` » (`GameEditor.cs:6087-6175`). Mixeur audio : même OU, avec `IsDirty = IsDocumentDirty || _detailView.HasPendingEdit`. Son : `_isDirty` seul, sans contexte d'historique. Les deux sources divergent réellement pour le matériau et les particules : le drapeau du panneau compare un instantané (`MaterialAssetInspectorPanel.cs:805`, `ParticleAssetInspectorPanel.cs:1157`) alors que chaque modification avance la révision de l'historique ; changer une valeur puis la remettre par une seconde modification laisse le contexte modifié et le panneau propre. Dans ce cas la boucle `SaveDirty…` saute le document (`GameEditor.cs:5788-5791`, `5834-5837`) et ` *` reste après « Save project ». Pour l'Animation2D la divergence va dans l'autre sens, et elle est dangereuse : l'instantané d'historique retient le drapeau `_isDirty` (`Animation2dAssetInspectorPanel.cs:3629-3641`) et `ApplyHistorySnapshot` le restaure (`SetDirty(snapshot.IsDirty)`, même fichier, vers la ligne 3667). Modifier, enregistrer, puis annuler laisse donc le panneau « propre » avec un contenu différent du fichier, et le contexte d'historique modifié ; `TrySaveLoadedAsset` rend alors vrai sans écrire (`Animation2dAssetInspectorPanel.cs:389-393`). Aujourd'hui « Save project » n'enregistre pas ce document et ` *` reste. Le drapeau du sprite est mis à vrai par chaque modification (`SpriteAssetInspectorPanel.cs:543`) et à faux par le chargement et l'enregistrement ; son comportement à l'annulation n'a pas été examiné.
- **Ce que fait un enregistrement.** `TrySaveLoadedAsset` rend vrai sans écrire quand le panneau n'est pas modifié pour matériau, sprite, particules, Animation2D, entité et cutscene. Il écrit toujours pour le son (`SoundAssetInspectorPanel.cs:237-261`), le mixeur audio (`AudioMixerPanel.cs:295-320`) et l'écran (`UIScreenPreviewPanel.TrySaveDocument`, `UIScreenPreviewPanel.cs:259-286`) : l'appelant doit tester « modifié » d'abord, comme les boucles le font. Le marquage « enregistré » est fait par l'appelant pour écran, matériau, sprite, particules, Animation2D et entité, et par le panneau lui-même pour cutscene et mixeur audio.
- **Fermer un onglet par le code.** `MGDockHost.RemovePanel(string)` ne connaît que les panneaux inscrits par `RegisterPanel` (`MGDockHost.cs:1205-1215`) ; l'éditeur n'inscrit pas ses documents, il les ancre par `DockOperation.DockAsTab` : `RemovePanel` rend faux pour eux et ne lève rien. `MGDockHost.ClosePanel(DockPanelNode)` (`MGDockHost.cs:1120-1173`) ferme un panneau ancré, flottant ou en tiroir et lève `PanelRemoved` ; il rend faux, sans rien lever, quand le nœud n'est plus dans le modèle. C'est ce que `CloseScreenPanelAfterAnswer` utilise déjà (`GameEditor.cs:3027-3035`).
- **Libération d'un document.** `OnDockHostPanelRemoved` (`GameEditor.cs:3144-3313`) ne lit du nœud que son identifiant. Il finit par `SyncActiveEditorDocumentFromDockState()` et `RefreshActiveHistoryContext()`, et traite aussi le panneau `EditorPanelIds.AudioProfiler`. La tile map a un second retrait de sa table (`GameEditor.cs:5554`).
- **Le lanceur** se ferme lui-même avant d'appeler `QueueProjectOpen` ou `QueueProjectCreate` (`ProjectLauncherWindow.cs:167`, `241`). Un lancement automatisé appelle `QueueProjectOpen` au démarrage (`GameEditor.cs:492-495`), sans document ouvert.
- **Journal.** La ligne des documents abandonnés à la sortie n'est observable dans aucun fichier : l'éditeur ne journalise que dans son panneau et la sortie de débogage, et le fichier de diagnostics est écrit avant la sortie (`ai-agent/tasks/bound-screens-tasks.md:969-972`, `GameEditor.cs:7468-7476`).
- **Texte tapé et pas encore validé.** Cutscene : écrit avant la question (`CutsceneDetailsPanel.CommitPendingText`, commit `b5b4ae05`). Mixeur audio : compté par `IsDirty` (`AudioMixerPanel.cs:151`) et écrit par `TrySaveLoadedAsset`. Animation2D : les champs valident sur Entrée, à la perte du focus et au retrait du parent (`Animation2dAssetInspectorPanel.cs:1213-1280`) ; un texte en cours de frappe n'est pas compté. Matériau, sprite, particules, son : les champs appliquent à chaque frappe. Entité : champs de l'inspecteur non examinés.
- **Tests.** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 6624 verts à `b5b4ae05`. Aucun test ne construit `GameEditor`.
- **Arbre de travail.** Deux dossiers non suivis, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | La question de sauvegarde est posée pour tout document ouvert et modifié, pas seulement les écrans d'UI et les cutscenes. |
| D2 | En quittant l'éditeur, la question couvre les documents **et le monde**. |
| D3 | Avant d'ouvrir ou de créer un autre projet, la question est posée aussi. |
| D4 | Le document Son est aligné sur les autres : ` *` dans le titre quand il est modifié, et enregistrement par « Save project ». |
| D5 | Exécution en mode autonome, comme les tranches de l'éditeur de cutscenes : plan relu, exécuté et vérifié sans attendre ; recette de l'auteur à la fin ; ni push ni fusion. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | Le travail reste sur la branche `chantier/cutscene-property-editing` : la question des cutscenes, qu'il généralise, n'existe que sur cette branche. |
| P2 | Les classes `ModifiedScreenCloseCoordinator`, `ModifiedScreensExitCoordinator` et `ModifiedScreenCloseDecision` gardent leur nom ; leurs commentaires disent qu'elles servent tous les documents. Un renommage serait un chantier à part. |
| P3 | Les neuf sortes modifiables posent la question. Tile map et clip d'animation ne la posent pas : rien n'y est modifiable. |
| P4 | Textes. Fermeture : titre `Close <Kind>`, message `Save changes to '<title>' before closing?`. Sortie : titre `Quit`, message `Save changes to the modified documents before quitting?` suivi de la liste `- <Kind>: <title>`. Autre projet : titre `Open Project`, message `Save changes to the modified documents before opening another project?` suivi de la même liste. `<Kind>` vaut `Screen`, `Cutscene`, `Material`, `Sprite`, `Particle`, `Animation2D`, `Entity`, `Audio Mixer`, `Sound`, `World`. |
| P5 | À la sortie et au changement de projet, `Save` enregistre chaque document modifié, un par un, puis le monde par la sauvegarde du projet. L'action ne se poursuit que si plus rien de la liste n'est modifié ; sinon elle est annulée et la raison est dans le journal. |
| P6 | Si une session de jeu tourne quand `Save` est répondu à la sortie, elle est arrêtée d'abord, comme le fait l'ouverture d'un autre monde : la sauvegarde du projet est refusée pendant une session. La fermeture d'un onglet pendant une session enregistre le document directement, comme les écrans le font déjà. |
| P7 | Sous automatisation, rien n'est demandé sur les trois chemins. La sortie écrit dans le journal les documents abandonnés ; le texte passe de « modified screen(s) » à « modified document(s) ». |
| P8 | Changement de projet : la question est posée une fois le projet choisi dans le lanceur, avant le chargement. `Cancel` garde le projet courant et ne rouvre pas le lanceur. Quand le chargement est lancé, tous les documents ouverts du projet quitté sont d'abord fermés par le code, qu'ils aient encore un onglet ou non, et qu'une question ait été posée ou non : aucun ne doit rester dans les tables avec les chemins de l'ancien projet. |
| P9 | Rouvrir un son déjà ouvert et modifié garde ses modifications, comme le font le matériau, les particules et l'Animation2D (« Unsaved changes kept »). |
| P10 | Texte tapé et pas encore validé : rien de plus que l'existant (cutscene, mixeur audio). Les autres sortes sont notées en point ouvert. |
| P11 | Après `Reset Layout` ou `Load Layout`, les documents ouverts perdent leur onglet mais restent dans les tables (comportement existant, non modifié). Ils restent enregistrés par « Save project » et sont désormais listés à la sortie. |
| P12 | À la sortie, le monde est listé `World: <nom>` ; son `Save` est la sauvegarde du projet. |
| P13 | Les tâches des trois chemins finissent en 🧪 : aucune question ne s'affiche sous automatisation et aucune entrée n'est envoyée à l'éditeur. Le clic réel est la recette de l'auteur. |
| P14 | Un document est « modifié » quand son onglet affiche ` *` : la question, la liste et l'enregistrement lisent tous cette même condition. Quand elle est vraie, `Save` écrit réellement le fichier, même si le drapeau interne du panneau est propre ; un enregistrement réussi la remet toujours à faux. Jamais de marquage « enregistré » sans écriture. Conséquences voulues pour « Save project » : un matériau ou des particules dont une valeur a été changée puis remise sont réécrits (même contenu) et perdent leur ` *` ; une Animation2D enregistrée puis annulée est réellement enregistrée, alors qu'elle ne l'était pas. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`** (P1). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`, terminé par la ligne `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`. Lancer `git commit` seul, depuis le dossier de travail, sans `cd` enchaîné devant.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ ou 🧪 dès que du code est touché : `dotnet build CasaEngine.Editor.MonoGame.sln`, 0 erreur ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, tous verts.
- Les tests existants ne changent pas, sauf un test ajouté par ce plan.
- **Ne jamais indexer** `Projects/PixelUiSample/.casaeditor/` ni `Projects/SpriteCutsceneSample/.casaeditor/` : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Éditeur lancé** : uniquement par ses propres options d'automatisation, avec `--diagnostics-out` et `--screenshot-out` hors du dépôt, jamais `--keep-open`, jamais d'entrée clavier ou souris envoyée, aucun asset enregistré par un lancement. Si un `CasaEngine.Editor.exe` de l'auteur tourne déjà, ne pas le toucher : builder et tester vers un dossier de sortie hors du dépôt.
- **MGUI** est un sous-module en lecture seule : aucune modification.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Séparation : rien de ce plan n'entre dans le runtime `CasaEngine/`. Tout est dans `CasaEngine.Editor/` et ses tests.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, au moins 6624 plus les tests ajoutés.
- Lancement automatisé (T3.2) : avec un document modifié, l'éditeur se termine seul (code 0), sans question, et n'enregistre rien.
- Recette de l'auteur, dans l'éditeur lancé (section « Recette de l'auteur » en fin de plan).

---

## Phase 0 — Décision

### ✅ T0.1 — ADR : tout document modifié demande avant d'être perdu

- Objectif : enregistrer la décision D1 à D4 et les arbitrages qui la précisent.
- Fichiers : `docs/decisions/0104-every-modified-document-asks-before-its-changes-are-lost.md`, `docs/decisions/README.md`.
- Étapes :
  1. Suivre le skill `adr` et `docs/decisions/template.md`. Statut `Accepted`, date 2026-11-19, source : ce plan et les réponses de l'auteur.
  2. Contexte (faits seulement) : ADR-0041 décrit trois questions, dont deux ne couvrent que les écrans d'UI ; ADR-0095 reportait la question pour les cutscenes et les autres documents d'asset ; le commit `b5b4ae05` l'a ajoutée pour les cutscenes à la fermeture.
  3. Décision : D1 à D4, P3 à P9, P11, P12 et P14, en puces. Dire que cette ADR complète ADR-0041 et remplace le report écrit dans ADR-0095 (sans modifier ces deux fichiers).
  4. Conséquences : les trois chemins, ce qui reste hors périmètre (section « Hors périmètre » de ce plan).
  5. Ajouter la ligne à l'index.
- Validation : le fichier suit le modèle ; l'index a la ligne ; aucun autre fichier touché.
- Commit : `docs(adr): record that every modified document asks before its changes are lost`
- Validation faite (2026-11-19) : ADR-0104 écrite selon le modèle (statut Accepted, D1 à D5, P3 à P9, P11, P12, P14), ligne ajoutée à l'index ; ADR-0041 et ADR-0095 non modifiées ; aucun code touché, donc ni build ni tests.

---

## Phase 1 — Socle

### ✅ T1.1 — Modèle pur d'un document enregistrable, et porte « demander puis poursuivre »

- Objectif : donner aux trois chemins une vue commune d'un document, testable sans MGUI ni `GameEditor`.
- Fichiers : `CasaEngine.Editor/History/EditorSavableDocument.cs`, `CasaEngine.Editor/History/ModifiedDocuments.cs`, `CasaEngine.Editor/History/ModifiedDocumentsGate.cs` ; tests `CasaEngine.Tests/Editor/ModifiedDocumentsTests.cs`, `CasaEngine.Tests/Editor/ModifiedDocumentsGateTests.cs`.
- Étapes :
  1. `EditorSavableDocument` (interne, immuable) : identifiant du panneau, libellé de sorte (`Kind`, P4), titre, une fonction « est modifié » et une fonction « enregistrer » qui rend vrai quand le document est enregistré. Arguments nuls refusés tôt.
  2. `ModifiedDocuments` (statique, interne) : `GetModifiedTitles(documents)` rend, dans l'ordre reçu, `"<Kind>: <title>"` pour chaque document modifié ; `TrySaveAll(documents)` appelle « enregistrer » pour chaque document modifié, continue après un échec, et rend vrai seulement si plus aucun document n'est modifié après la passe.
  3. `ModifiedDocumentsGate` (interne) : construit avec « titres modifiés », « automatisation active », « demander » (titres, réponse) et « tout enregistrer ». `Request(proceed)` : sans titre modifié ou sous automatisation, appelle `proceed` tout de suite ; sinon, si aucune question n'attend, pose la question et retient `proceed` ; si une question attend déjà, ignore la demande. À la réponse : `ModifiedScreenCloseDecision.ApplyAnswer` ; si elle laisse passer, `proceed` est appelé une fois ; une seconde réponse à la même question est ignorée. `IsQuestionPending` en lecture.
  4. Tests. `ModifiedDocuments` : ordre et format des titres ; documents non modifiés ignorés ; `TrySaveAll` enregistre seulement les modifiés, continue après un échec et rend faux ; rend vrai quand tout est enregistré ; liste vide. Porte : rien de modifié → `proceed` immédiat sans question ; automatisation → `proceed` immédiat ; modifié → question, pas de `proceed` ; `Save` réussi → `proceed` une fois ; `Save` échoué → pas de `proceed`, la demande suivante repose la question ; `Don't Save` → `proceed` sans enregistrer ; `Cancel` → rien ; demande pendant une question → ignorée ; seconde réponse ignorée ; arguments nuls.
- Validation : build à 0 erreur ; tests verts, dont les nouveaux.
- Commit : `feat(editor): add the pure model of a savable document and the ask-then-proceed gate`
- Validation faite (2026-11-19) : trois classes internes pures ajoutées (`EditorSavableDocument`, `ModifiedDocuments`, `ModifiedDocumentsGate`), non branchées ; 16 tests ajoutés (6 + 10), calqués sur `ModifiedScreensExitCoordinatorTests`. Build de `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `CasaEngine.Tests` : 6640 verts (6624 + 16). Aucun test existant modifié.

### ✅ T1.2 — Aligner le document Son

- Objectif : D4 et P9.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`.
- Étapes :
  1. Ajouter `TryGetSoundAssetInspectorPanel(panelId, out panel)` sur le modèle des autres `TryGet…` (préfixe `EditorPanelIds.SoundAssetDocumentPrefix`, puis la table).
  2. Ajouter `GetSoundDocumentTitle(panelId)` : titre de `_soundInspectorPanelTitles` (défaut `Sound`), suivi de ` *` quand le panneau est modifié.
  3. Dans `TryOpenSoundAsset` : à la création du panneau, s'abonner à `DirtyStateChanged` par un gestionnaire nommé qui met à jour le titre de l'onglet (`UpdateDockPanelTitle`) ; utiliser `GetSoundDocumentTitle` pour le titre du nœud. Se désabonner dans la branche son de `OnDockHostPanelRemoved`.
  4. P9 : dans `TryOpenSoundAsset`, quand le panneau existe déjà et est modifié, ne pas rappeler `LoadAsset` : activer l'onglet, écrire dans le journal `Unsaved changes kept for <chemin>` et rendre vrai. Lire d'abord comment `MaterialAssetInspectorPanel.cs:232`, `ParticleAssetInspectorPanel.cs:172` et `Animation2dAssetInspectorPanel.cs:365` le font, et suivre le même comportement.
  5. Ajouter `SaveDirtySoundInspectors()` sur le modèle de `SaveDirtySpriteInspectors` (sans contexte d'historique : le son n'en a pas) et l'appeler dans `SaveCurrentProject` avec les autres boucles.
- Validation : build à 0 erreur ; tests verts. Lecture du diff : aucun autre document touché.
- Commit : `feat(editor): mark a modified sound in its tab and save it with the project`
- Validation faite (2026-11-19) : `TryGetSoundAssetInspectorPanel`, `GetSoundDocumentTitle`, `OnSoundInspectorDirtyStateChanged` (abonné à la création, désabonné dans `OnDockHostPanelRemoved`) et `SaveDirtySoundInspectors` (appelée par `SaveCurrentProject`) ajoutés dans `GameEditor.cs` seul. P9 : comme matériau, particules et Animation2D, un son déjà ouvert et modifié garde ses modifications (activation de l'onglet, journal `Unsaved changes kept for <chemin>`). Build de `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `CasaEngine.Tests` : 6640 verts. Aucun autre document touché.

### ✅ T1.3 — Une méthode d'enregistrement par document, et la liste des documents ouverts

- Objectif : que « Save project », la fermeture, la sortie et le changement de projet enregistrent un document par le même code ; exposer les documents ouverts sous la forme de T1.1.
- Fichiers : `CasaEngine.Editor/GameEditor.cs` ; `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`, `SpriteAssetInspectorPanel.cs`, `ParticleAssetInspectorPanel.cs`, `Animation2dAssetInspectorPanel.cs` (surcharge d'enregistrement forcé seulement) ; un test si l'un de ces panneaux se construit en test.
- Étapes :
  1. Pour chacune des neuf sortes, écrire une méthode `Is<Sorte>DocumentModified(panelId, panel)` qui rend exactement la condition que `Get…DocumentTitle` utilise pour afficher ` *` (P14), et faire lire cette méthode au titre : une seule source. Pour le son, c'est `panel.IsDirty` (T1.2).
  2. Pour chacune des neuf sortes, écrire `TrySave<Sorte>Document(panelId, panel)` (l'écran a déjà `TrySaveScreenDocument` : l'adapter ; la cutscene a `TrySaveCutsceneDocumentBeforeClose` : la renommer `TrySaveCutsceneDocument`). Contrat, le même pour toutes :
     - si le document n'est pas modifié (étape 1), rendre vrai sans rien écrire ni journaliser ; ce test est obligatoire, car le son, le mixeur audio et l'écran écrivent le fichier à chaque appel ;
     - sinon appeler l'enregistrement du panneau **en forçant l'écriture** ; s'il rend faux, journaliser son message en avertissement et rendre faux. Écran, mixeur audio et son écrivent toujours. Cutscene et entité lisent leur état dans le contexte d'historique : leur test interne « non modifié » ne peut pas être vrai ici. Pour matériau, sprite, particules et Animation2D, dont le drapeau interne peut être propre alors que le document se lit modifié, ajouter au panneau une surcharge additive `TrySaveLoadedAsset(out string errorMessage, bool force)` : avec `force` vrai, le test « Already saved » est sauté et le fichier est écrit ; la méthode existante appelle la nouvelle avec `false` et garde son comportement ;
     - s'il rend vrai : marquer le contexte d'historique enregistré quand la sorte en a un (`_editorDirtyState.MarkSaved`, sans effet de plus pour cutscene et mixeur audio, qui se marquent déjà), mettre le titre de l'onglet à jour, journaliser `… saved: <chemin>` comme la boucle actuelle, puis rendre « le document n'est plus modifié » (étape 1 relue). Un vrai garantit donc que le document ne se lit plus modifié.
     - cas voulus de P14 : un matériau ou des particules propres côté panneau mais modifiés côté historique sont réécrits (même contenu), le contexte est marqué enregistré et ` *` disparaît ; une Animation2D modifiée, enregistrée puis annulée est écrite dans son état affiché.
  3. Chaque boucle `SaveDirty…` devient : pour chaque panneau de la table, appeler `TrySave<Sorte>Document`. `SaveSelectedAnimation2dInspector` et `SaveSelectedCutsceneInspector` appellent la même méthode pour le document actif. « Save project » garde le même ordre d'appels.
  4. Ajouter `TryGetSavableDocument(panelId, out EditorSavableDocument document)` : une seule chaîne de tests sur les neuf sortes modifiables (P3), par les `TryGet…Panel` existants ; elle rend faux pour un panneau qui n'est plus dans sa table. Pour chacune : libellé de P4, titre de sa table de titres avec la même valeur par défaut que `Get…DocumentTitle` (sans le ` *`), « est modifié » = étape 1, « enregistrer » = étape 2. Cutscene : « est modifié » écrit d'abord le texte en attente quand le panneau de détails montre ce document (`CommitPendingText`, commit `b5b4ae05`), puis lit l'étape 1.
  5. Ajouter `CollectSavableDocuments(List<EditorSavableDocument> documents, bool includeWorld)` : vide la liste, parcourt les neuf tables dans un ordre fixe (écrans, cutscenes, matériaux, sprites, particules, Animation2D, entités, mixeurs audio, sons), puis ajoute le monde si demandé. Monde (P12) : libellé `World`, titre = `CurrentWorld.Name` du gestionnaire de jeu (comme `GameEditor.cs:8744` ; à défaut de nom, `World`), « est modifié » = `_editorDirtyState.IsDirty` du contexte du monde, « enregistrer » = `TrySaveProjectBeforeOpeningWorld`, renommé `TrySaveProjectAndWorld` puisqu'il sert maintenant deux chemins (garder son paramètre ou calculer le contexte dedans). Le monde est le dernier de la liste : `SaveCurrentProject` repasse alors sur des documents déjà enregistrés, que l'étape 2 ne réécrit pas.
  6. Ne brancher `TryGetSavableDocument` et `CollectSavableDocuments` sur aucun chemin dans cette tâche, sauf si le compilateur refuse une méthode privée inutilisée (alors les brancher sur la fermeture, étape 1 de T2.1, et le dire dans la note).
  7. Relever, en lecture seule, ce que fait la réouverture d'un document déjà ouvert et modifié pour chaque sorte (`TryOpen…Asset`) : garde-t-elle les modifications ? Écrire le résultat dans « Points ouverts », sans rien corriger.
- Validation : build à 0 erreur ; tests verts. Lecture du diff, sorte par sorte, à écrire dans la note : (a) le titre et « est modifié » lisent la même méthode ; (b) après un `TrySave<Sorte>Document` qui rend vrai, « est modifié » est faux ; (c) un document non modifié n'est jamais réécrit ; (d) hors les cas de P14, « Save project » a les mêmes effets qu'avant (marquage, titre, journal) ; (e) aucun chemin ne marque un contexte enregistré sans qu'un fichier ait été écrit dans le même appel ; (f) Animation2D : modifier, enregistrer, annuler, puis `TrySaveAnimation2dDocument` écrit le fichier et le document ne se lit plus modifié. Chercher un test existant qui construit l'un des quatre panneaux (`rg "new (Material|Sprite|Particle|Animation2d)AssetInspectorPanel" CasaEngine.Tests`) : s'il en existe un, ajouter un test de l'enregistrement forcé sur ce modèle ; sinon l'écrire dans la note, avec la raison.
- Commit : `refactor(editor): save one document through one method and list the open documents`
- Validation faite (2026-11-19) : dans `GameEditor.cs`, une méthode `Is<Sorte>DocumentModified` (écran, matériau, son, sprite, particules, mixeur audio, cutscene, Animation2D, entité) et une méthode `TrySave<Sorte>Document` par sorte (`TrySaveScreenDocument` adaptée, `TrySaveCutsceneDocumentBeforeClose` renommée `TrySaveCutsceneDocument`) ; les boucles `SaveDirty…`, `SaveSelectedAnimation2dInspector` et `SaveSelectedCutsceneInspector` appellent ces méthodes ; `TryGetSavableDocument`, `CollectSavableDocuments` (ordre : écrans, cutscenes, matériaux, sprites, particules, Animation2D, entités, mixeurs audio, sons, puis le monde) et `TrySaveProjectAndWorld` (ex-`TrySaveProjectBeforeOpeningWorld`, paramètre gardé) ajoutés. Surcharge additive `TrySaveLoadedAsset(out string errorMessage, bool force)` sur matériau, sprite, particules et Animation2D ; la méthode existante l'appelle avec `false`. `TryGetSavableDocument` et `CollectSavableDocuments` ne sont branchées sur aucun chemin : le compilateur accepte une méthode privée inutilisée (étape 6). Build de `CasaEngine.Editor.MonoGame.sln` : 0 erreur, aucun avertissement dans `GameEditor.cs` ; `CasaEngine.Tests` : 6640 verts, aucun test modifié.
  - (a) Le titre et « est modifié » lisent la même méthode, pour les neuf sortes : `Get<Sorte>DocumentTitle` appelle `Is<Sorte>DocumentModified`. Conditions : écran = contexte d'historique ; matériau, sprite, particules, mixeur audio, cutscene, Animation2D, entité = contexte d'historique OU drapeau du panneau (inchangé) ; son = drapeau du panneau seul (pas de contexte). Cutscene et entité : le panneau lit le même `EditorDirtyStateService.Current` que `_editorDirtyState`, avec l'identifiant de contexte posé par `SetHistoryContextId(panelId)` à la création (`GameEditor.cs`, un appel par sorte) : les deux sources sont identiques.
  - (b) Après un `TrySave<Sorte>Document` qui rend vrai, « est modifié » est faux : chaque méthode rend `!Is<Sorte>DocumentModified`. Lecture : écran, matériau, sprite, particules, Animation2D, entité = `MarkSaved` du contexte par l'appelant et drapeau du panneau remis à faux par l'écriture ; cutscene = `MarkSaved` dans `TrySaveLoadedAsset` ; mixeur audio = `MarkSaved` du panneau et `AudioMixerDocument.TrySave` remet son drapeau à faux (`RefreshState`) ; son = `SetDirty(false)`.
  - (c) Un document non modifié n'est jamais réécrit : la première ligne de chaque méthode rend vrai sans rien écrire ni journaliser quand `Is<Sorte>DocumentModified` est faux, ce qui couvre le son, le mixeur audio et l'écran, dont l'enregistrement du panneau écrit à chaque appel.
  - (d) Hors P14, « Save project » a les mêmes effets qu'avant (écriture, `MarkSaved`, titre, journal `… saved: <chemin>` aux mêmes textes) : les boucles testaient `panel.IsDirty` (cutscene, entité, mixeur : même source que le titre) ou le contexte d'historique (écran) ; elles testent maintenant la condition du titre, qui n'en diffère que dans les cas de P14. Différences voulues : `SaveSelectedCutsceneInspector` rafraîchit aussi le titre (sans effet visible, le panneau le fait déjà) ; la fermeture d'un écran par `Save` ne réécrit plus un écran qui ne serait pas modifié.
  - (e) Aucun `MarkSaved` sans écriture dans le même appel : les `MarkSaved` de l'appelant suivent un `TrySaveLoadedAsset` qui rend vrai ; pour matériau, sprite, particules et Animation2D l'écriture est forcée, donc réelle ; pour l'entité, le test interne « non modifié » du panneau ne peut pas être vrai quand le titre montre ` *`, puisqu'il lit le même contexte (a) ; pour la cutscene, le garde `CutsceneSaveGuard` peut refuser : le résultat est faux, rien n'est marqué.
  - (f) Animation2D, par lecture (aucun test ne construit ce panneau, voir plus bas) : modifier puis enregistrer (`SetDirty(false)`, contexte marqué), puis annuler : `ApplyHistorySnapshot` restaure le drapeau de l'instantané (`SetDirty(snapshot.IsDirty)`, `Animation2dAssetInspectorPanel.cs` vers la ligne 3673) et l'historique avance : le contexte se lit modifié, `IsAnimation2dDocumentModified` est vrai. `TrySaveAnimation2dDocument` appelle `TrySaveLoadedAsset(force: true)`, qui écrit `_animationData` (l'état affiché), remet le drapeau à faux, marque le contexte enregistré ; le document ne se lit plus modifié.
  - Test de l'enregistrement forcé : `rg "new (Material|Sprite|Particle|Animation2d)AssetInspectorPanel" CasaEngine.Tests` ne trouve rien : aucun test existant ne construit l'un des quatre panneaux. Matériau et particules se construisent avec une `MGWindow` seule, mais leur enregistrement écrit par `EditorAssetWriterService` sous le chemin du projet (état global) et le sprite et l'Animation2D demandent en plus un `GraphicsDevice` et l'hôte d'éditeur : un test fidèle demande un harnais qui n'existe pas pour ces quatre panneaux ; il n'est pas ajouté ici, la surcharge est vérifiée par lecture (le seul changement du corps est `!IsDirty` devenu `!force && !IsDirty`).
  - Décisions locales : le titre d'une entité dans la liste est celui de l'onglet sans le ` *` (nom de l'entité chargée, sinon titre de la table), via `GetEntityDocumentName` partagé avec le titre, pour que la question et l'onglet disent la même chose ; chaque sorte a une petite fabrique `Create<Sorte>SavableDocument`, appelée par `TryGetSavableDocument` et par `CollectSavableDocuments` (qui ne passe donc pas par le préfixe de l'identifiant) ; `Is<Sorte>DocumentModified` prend `(panelId, panel)` sauf l'écran (`panelId` seul) et le son (`panel` seul), qui n'ont pas besoin de l'autre argument.

---

## Phase 2 — Les trois chemins

### ⏳ T2.1 — Fermeture d'un onglet : toutes les sortes

- Objectif : D1 sur le chemin de fermeture.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/History/ModifiedScreenCloseCoordinator.cs` (commentaires seulement).
- Étapes :
  1. `IsDocumentAskedBeforeClose`, `IsDocumentModifiedBeforeClose`, `AskSaveBeforeClosingDocument` et `TrySaveDocumentBeforeClose` s'appuient sur `TryGetSavableDocument` : plus de branche par sorte dans ces quatre méthodes. Titre et message de P4.
  2. `CloseScreenPanelAfterAnswer` : renommer en `CloseDocumentPanelAfterAnswer` et dire « document » dans l'avertissement.
  3. Mettre à jour les commentaires de `OnDockHostPanelClosing`, du champ `_modifiedScreenCloseCoordinator` et de la classe `ModifiedScreenCloseCoordinator` (P2) : ils servent tout document modifiable.
  4. Vérifier par lecture que, pour chaque sorte, la réponse `Don't Save` suivie de la fermeture par le code passe bien par `OnDockHostPanelRemoved`, qui libère le panneau et son état modifié.
- Validation : build à 0 erreur ; tests verts (les 13 tests du coordinateur inchangés). Statut 🧪 (P13) : le clic réel n'est pas essayé.
- Commit : `feat(editor): ask to save any modified document before its tab closes`

### ⏳ T2.2 — Sortie de l'éditeur : tous les documents et le monde

- Objectif : D2.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/History/ModifiedScreensExitCoordinator.cs` (commentaires seulement).
- Étapes :
  1. `GetModifiedScreenTitles` devient `GetModifiedDocumentTitles` : `CollectSavableDocuments(…, includeWorld: true)` puis `ModifiedDocuments.GetModifiedTitles`.
  2. `TrySaveAllScreenDocumentsBeforeQuitting` devient `TrySaveAllModifiedDocuments` : si une session de jeu tourne, l'arrêter d'abord (P6, `StopPlayMode`, comme `OpenWorldAsset`) ; puis `CollectSavableDocuments` et `ModifiedDocuments.TrySaveAll` ; en cas d'échec, avertissement `Exit cancelled: one or more modified documents could not be saved.`
  3. `AskSaveBeforeQuitting` : message de P4. `LogScreensAbandonedByAutomatedExit` : renommer en `LogDocumentsAbandonedByAutomatedExit`, texte de P7.
  4. Mettre à jour les commentaires de `OnExiting`, du champ et de la classe `ModifiedScreensExitCoordinator` (P2).
- Validation : build à 0 erreur ; tests verts (les 10 tests du coordinateur inchangés). Statut 🧪 (P13).
- Commit : `feat(editor): ask to save every modified document and the world before quitting`

### ⏳ T2.3 — Autre projet : la question, puis la fermeture des documents du projet quitté

- Objectif : D3 et P8.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`.
- Étapes :
  1. Ajouter un champ `ModifiedDocumentsGate _projectSwitchGate`, construit à côté des deux coordinateurs : titres = `GetModifiedDocumentTitles`, automatisation = `_automationOptions.HasAutomation`, demander = `AskSaveBeforeOpeningProject` (P4), tout enregistrer = `TrySaveAllModifiedDocuments` sans son avertissement de sortie (donner à cette méthode le texte de l'avertissement en argument, ou séparer l'avertissement).
  2. `QueueProjectOpen` et `QueueProjectCreate` : l'affectation de `_pendingProjectLauncherAction` passe par `_projectSwitchGate.Request(…)`. `Cancel` ne met rien en attente (P8).
  3. Extraire de `OnDockHostPanelRemoved` la libération d'un document dans une méthode qui prend l'identifiant du panneau (`ReleaseDocumentPanel(string panelId)`), appelée par `OnDockHostPanelRemoved` comme avant. Elle garde tout le corps actuel, dans le même ordre, y compris la branche du panneau `EditorPanelIds.AudioProfiler` et les deux appels finaux `SyncActiveEditorDocumentFromDockState()` et `RefreshActiveHistoryContext()`.
  4. Ajouter `CloseAllDocumentsOfCurrentProject()` et l'appeler au début de l'action mise en attente, avant `LoadProject` ou `CreateProject`. Elle parcourt une copie des clés des onze tables (les neuf modifiables, tile map et clip d'animation), car la libération modifie les tables. Pour chaque identifiant :
     - chercher son nœud partout où `MGDockHost.ClosePanel` sait fermer : `_dockHost.LayoutModel.FindPanelById(panelId)`, puis les fenêtres flottantes et les tiroirs (lire `MGDockHost.ClosePanel`, `MGDockHost.cs:1120-1173`, et `_dockHost.FloatingWindows`, pour reprendre la même recherche) ;
     - si un nœud est trouvé, fermer par `_dockHost.ClosePanel(nœud)`, qui lève `PanelRemoved` et passe donc par l'étape 3. Ne pas utiliser `RemovePanel(string)` : il ne voit pas les documents de l'éditeur ;
     - si aucun nœud n'est trouvé (document resté sans onglet après `Reset Layout`, P11), ou si `ClosePanel` rend faux, appeler `ReleaseDocumentPanel(panelId)` directement ;
     - après le parcours, vérifier que les onze tables sont vides ; sinon écrire un avertissement qui nomme les identifiants restants (jamais d'exception).
  5. Vérifier par lecture que la fermeture d'un onglet se comporte comme avant (même ordre, mêmes effets), que `CloseAllDocumentsOfCurrentProject` ne touche ni le monde ni les panneaux d'outils, et qu'une question de fermeture restée en attente pour un document libéré est abandonnée à sa réponse (`ModifiedScreenCloseCoordinator.OnAnswered` relit « le document est ouvert », alimenté par `TryGetSavableDocument`).
- Validation : build à 0 erreur ; tests verts. Statut 🧪 (P13).
- Commit : `feat(editor): ask to save modified documents before another project opens`

---

## Phase 3 — Documentation et validation

### ⏳ T3.1 — Documentation

- Objectif : que la documentation dise le comportement général.
- Fichiers : `docs/editor/editor-message-boxes.md`, `docs/editor/cutscene-editor.md`, et tout autre document de `docs/editor/` qui dit qu'un document se perd sans question (`rg -n -i "without a prompt|before closing|unsaved" docs/editor`).
- Étapes :
  1. `editor-message-boxes.md` : la section des questions de sauvegarde décrit les quatre flux (fermeture de tout document modifiable, sortie avec documents et monde, autre projet, autre monde), la liste des sortes, les textes de P4, l'automatisation (P7), l'arrêt de la session de jeu (P6). Ajouter « Decisions: see ADR-0104 ».
  2. `cutscene-editor.md` : retirer la limite « Quitting the editor with a modified cutscene loses its changes » et renvoyer à `editor-message-boxes.md`.
  3. Ajouter une section « Limits » ou la compléter : les points P10 et P11, et les sortes non concernées (P3).
- Validation : relecture ; aucun fichier de code touché.
- Commit : `docs(editor): describe the save question for every modified document`

### ⏳ T3.2 — Lancement de contrôle et clôture

- Objectif : apporter la preuve atteignable sans entrée, et clore le plan.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes :
  1. Lancer l'éditeur sur `Projects/SpriteCutsceneSample` avec `--open-asset Cutscenes/stagger.cutscene`, `--cutscene-add-action Wait`, `--diagnostics-out` et `--screenshot-out` hors du dépôt, sans `--keep-open` (reprendre la forme exacte des lancements de `ai-agent/tasks/cutscene-editor-structure-tasks.md`). Trois observations, chacune lue dans un fichier ou un code de sortie : (a) le fichier de diagnostics dit que le document est modifié après l'ajout ; (b) le processus se termine seul, code de sortie 0 : la sortie automatisée avec un document modifié n'est pas retenue par la nouvelle question ; (c) `git status` ne montre aucun fichier de `Projects/` modifié : rien n'a été enregistré.
  2. La ligne `[Automation] Exiting with N modified document(s) abandoned` n'est observable dans aucun fichier (`ai-agent/tasks/bound-screens-tasks.md:969-972`) : la vérifier par lecture du code seulement, et l'écrire ainsi dans la note.
  3. Mettre à jour la ligne de ce plan dans `ai-agent/README.md` et noter la validation globale (builds des deux solutions, total des tests).
- Validation : les deux builds à 0 erreur ; tests verts ; les trois observations de l'étape 1 notées avec le fichier lu. Statut 🧪 tant que la recette de l'auteur n'est pas faite.
- Commit : `docs(plan): record the validation of the save question for every document`

---

## Recette de l'auteur

À faire dans l'éditeur lancé, sur un projet de test (un `Save` réécrit le fichier de l'asset).

- **Fermeture.** Pour chaque sorte (écran, cutscene, matériau, sprite, particules, Animation2D, entité, mixeur audio, son) : modifier, fermer l'onglet par la croix → la question s'affiche. `Cancel` garde l'onglet ; `Don't Save` ferme et la réouverture montre l'asset d'origine ; `Save` ferme et la réouverture montre la modification. `Close All` avec deux documents modifiés pose deux questions, l'une après l'autre.
- **Sortie.** Modifier une cutscene, un matériau et le monde, puis fermer la fenêtre de l'éditeur → une question liste les trois. `Cancel` reste ; `Save` enregistre tout puis quitte ; `Don't Save` quitte.
- **Sortie pendant le jeu.** Même essai avec une session de jeu en cours : `Save` arrête la session, enregistre, puis quitte.
- **Autre projet.** Modifier un document, `Open Project`, choisir un autre projet → la question s'affiche. `Cancel` reste sur le projet courant. Après `Don't Save`, quitter le nouveau projet ne pose aucune question sur l'ancien.
- **Son.** Modifier un son → ` *` dans le titre ; « Save project » l'enregistre et retire ` *` ; rouvrir le son modifié depuis le Content Browser garde la modification.
- **Tile map, clip d'animation.** Se ferment sans question.

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Aucun code ne ferme un onglet au clic du milieu (essai en test sur MGUI le 2026-11-19 : le clic ne ferme rien et ne lève pas `PanelClosing`). L'auteur a vu ce clic fermer un onglet : à réessayer à la recette. | T2.1 |
| O2 | Texte en cours de frappe non compté comme modification : champs validés d'Animation2D, champs de l'inspecteur d'entité non examinés (P10). | T2.1, T2.2 |
| O3 | `Reset Layout` et `Load Layout` retirent les onglets des documents sans les fermer (P11). | T2.2 |
| O4 | Supprimer ou renommer dans le Content Browser un asset dont le document est ouvert ne ferme pas ce document (recensement : aucun gestionnaire trouvé). | Aucune |
| O5 | Si le chargement de l'autre projet échoue après la question, les documents du projet courant sont déjà fermés (enregistrés ou abandonnés selon la réponse). | T2.3 |
| O6 | Réouverture d'un document déjà ouvert et modifié (relevé de T1.3, par lecture, rien corrigé). Garde les modifications : matériau, particules, cutscene, Animation2D, entité, mixeur audio (le panneau n'appelle `LoadAsset` qu'à sa création), son (P9, T1.2) et sprite (`LoadAsset` sauf si `IsDirty` ; un sprite dont le drapeau est propre mais dont l'historique est modifié est rechargé depuis le disque). Perd le contenu : écran (`TryOpenUIScreenAsset` appelle toujours `previewPanel.LoadAsset`, qui relit le XAML du disque et remplace `_currentDocument` ; le contexte d'historique n'est pas touché par cet appel, son effet sur l'historique n'a pas été examiné). Tile map et clip d'animation : sans modification possible. | Aucune (hors périmètre) |

## Hors périmètre

- Renommer les classes `ModifiedScreen…` (P2).
- Fermer les documents retirés par `Reset Layout` ou `Load Layout` (O3), et réagir à la suppression ou au renommage d'un asset ouvert (O4).
- Donner un historique d'annulation au document Son.
- Rendre modifiables la tile map et le clip d'animation.
- Toute modification de MGUI, dont un clic du milieu qui fermerait un onglet.
- Corriger la réouverture d'un document modifié pour une autre sorte que le son (relevée par T1.3, étape 5).
