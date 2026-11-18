# Plan agent IA — Éditeur de cutscenes : arbre des actions et détails typés (tranche B)

Deuxième tranche du programme « éditeur de cutscenes visuel » décrit dans [cutscene-editor-preview-tasks.md](cutscene-editor-preview-tasks.md) et ADR-0096.
Elle remplace la colonne de champs texte par un arbre des actions et un panneau de détails à champs typés, et permet d'ajouter, de supprimer et de dupliquer des actions.

Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-11-18 : **ce plan les applique, il ne les rediscute pas**.
L'auteur a choisi le 2026-11-18 le mode autonome : ce plan est approuvé d'avance et l'exécution s'enchaîne. Les points P1 → P12 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Quand un document `.cutscene` est actif :

- le panneau **Hierarchy** de l'éditeur montre l'arbre des actions, avec une ligne de résumé par action et une barre pour ajouter, dupliquer et supprimer ;
- le panneau **Inspector** montre les propriétés de l'action sélectionnée dans des champs typés : nombres bornés, case à cocher, liste déroulante, couleur, sélecteur d'entité, sélecteurs d'asset ;
- le document lui-même ne garde que la barre de transport, la ligne d'état et l'aperçu, qui occupe toute la zone ;
- toute modification, de valeur ou de structure, s'annule et se rétablit, marque le document modifié, et relance l'aperçu s'il jouait.

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `b1b93f42`, 16 commits depuis `main` `c365b514`, non fusionnée.
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.

Panneaux contextuels :

- Les panneaux Hierarchy et Inspector sont des `ContextualDockPanelHost` ; un type de document y enregistre son contenu par une `ContextualPanelDefinition` (`Role`, `DocumentKind`, `Title`, `ContentFactory`, `Refresh`) (`CasaEngine.Editor/GameEditor.cs:1442-1470`, `:1583-1600` ; `CasaEngine.Editor/Controls/ContextualPanels/ContextualPanelDefinition.cs:6-17`).
- Aucune définition n'existe pour `EditorDocumentKind.Cutscene` : les deux panneaux affichent « No hierarchy / No inspector is available for the active editor » (capture du 2026-11-18).
- `ActivateCutsceneDocument` rend le document actif et garde `_activeCutsceneInspectorPanel` (`GameEditor.cs:3722-3733`). Elle n'est appelée qu'à l'ouverture de l'asset et à la fermeture d'un autre onglet (`:5147`, `:3812`) : `OnDockHostActivePanelChanged` n'a pas de branche cutscene (`:2289-2337`). Revenir sur un onglet cutscene par un clic ne le rend donc pas actif, et Ctrl+Z vise alors l'historique du document précédent.
- `ContextualDockPanelHost` n'appelle `ContentFactory` que quand la définition active change ; il appelle `Refresh` à chaque changement de document actif ou de sélection (`CasaEngine.Editor/Controls/ContextualPanels/ContextualDockPanelHost.cs:36-37`, `:76-102`). Son hôte de contenu est un `MGStackPanel` vertical (`:66`).
- `EditorHistoryService.Execute(context, command)` empile dans le contexte donné ; `Undo` et `Redo` n'agissent que sur le contexte actif ; la pile ne fusionne pas les commandes consécutives (`CasaEngine.Editor/History/EditorHistoryService.cs:111-166`). `EditorCompositeCommand` regroupe plusieurs commandes en une entrée (`CasaEngine.EditorServices/History/EditorCompositeCommand.cs`).
- Modèles à imiter : `EntityAssetHierarchyPanel` et `TileMapHierarchyPanel`, reliés au document actif par `SetEditorPanel` (`GameEditor.cs:1546-1552`, `:1578-1584`).

Document cutscene actuel (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`) :

- Avec aperçu : barre de transport et ligne d'état en haut, colonne de propriétés de 420 px à droite, `WorldViewportPanel` à gauche (`CreateDocumentWithPreview`).
- La colonne est reconstruite en entier par `RefreshInspector` ; chaque propriété est une ligne libellé + `MGTextBox` créée par `CreateFieldRow`, validée par Entrée ou perte du focus, puis `CommitField` exécute un `SetCutsceneActionPropertyCommand` par `EditorHistoryService`.
- Les lignes « Runtime state » viennent du directeur du monde de l'éditeur, pas de l'aperçu (`GameEditor.RefreshCutsceneRuntimeSnapshot`, `GameEditor.cs:3748-3752`).
- La session d'aperçu (`CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`) expose `World`, `State`, `IsPlaying`, `Requests` ; elle n'expose pas la liste des noms d'entités.
- Les 16 tests de `CasaEngine.Tests/Editor/CutsceneAssetInspectorPanelTests.cs` retrouvent les champs par leur libellé puis par l'unique `MGTextBox` de la ligne, et l'un d'eux compte tous les `MGTextBox` du document : ils dépendent de la structure actuelle.

Métadonnées des propriétés (`CasaEngine.EditorServices/Cutscenes/`) :

- `CutsceneActionProperty` porte `Key`, `Kind`, `IsNullable`, `ValueType`, et les accès typés `GetValue`, `SetValue`, `TryParse`, `FormatValue`, `AreEqual`. Rien n'y dit qu'une chaîne est un nom d'entité ou une référence d'asset, ni les bornes d'un nombre.
- Les bornes sont dans `CasaEngine/Framework/Cutscenes/CutsceneValidator.cs` : `>= 0` pour les durées, distances et compteurs, `[0, 1]` pour les volumes, `> 0` pour `MoveSprite.speed`.
- Formes de référence lues par le runtime (`CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs`, `CasaEngine/Framework/Application/GameManager.cs:20-25`, `:263-272`, `:352-419`) : `sound_asset_id` est un GUID ; `sprite` est un nom du catalogue, type `sprite` ; `OpenScreen.target` et `PlayVideo.target` sont un nom ou un GUID, type `uiscreen` ; `ChangeWorld.target` est un **nom de fichier**, type `world` ; `mapping` est un nom ou un GUID, type `buttonsmapping` ; `next_cutscene` est un nom ou un GUID, type `cutscene` ; `entity` est le nom d'une entité du monde, comparé en ordinal.
- `AssetInfo` porte `Id`, `Name`, `FileName`, `AssetType` ; `AssetCatalog.AssetInfos` les énumère (`CasaEngine/Framework/Assets/AssetCatalog.cs:15`).
- Énumérations : `SpriteBlendMode` (Opaque, AlphaBlend, Additive, Subtractive), `ScreenEffectLayer` (BelowUI, AboveUI).

Structure de l'arbre :

- `CutsceneActionData` est abstraite, avec la seule propriété `Type` (`CasaEngine/Framework/Cutscenes/CutsceneActionData.cs:3-6`). `Sequence`, `Parallel` et `Repeat` portent une liste `Actions`.
- `CutsceneAssetJsonSerializer.SaveAction` et `LoadAction` sont privées (`Serialization/CutsceneAssetJsonSerializer.cs:48`, `:314`) ; `Save` et `Load` de l'asset entier sont publiques (`:11`, `:28`).
- Toutes les classes d'action concrètes ont un constructeur public sans paramètre, sauf `UnknownCutsceneActionData`. Les listes `Actions` sont en lecture seule mais modifiables.
- `CutsceneSaveGuard` refuse une sauvegarde pour un type inconnu, une erreur de format ou une perte de chargement ; il ne consulte pas `CutsceneValidator` (`CasaEngine.EditorServices/Cutscenes/CutsceneSaveGuard.cs:17-97`).
- `CutsceneEditSession` ne porte que l'asset et les pertes de chargement, dont chacune référence son action.

Contrôles disponibles :

- `MGTreeView` et `MGTreeViewItem` : ajout manuel d'éléments, `SelectedItem`, `SelectItem`, `SelectionChanged`, `ItemRightClicked`, en-tête libre ; ni étiquette de donnée, ni glisser pour réordonner (`MGUI/MGUI.Core/UI/MGTreeView.cs`, `MGTreeViewItem.cs`). `EntitiesPanel` montre le modèle : reconstruction complète, dictionnaires élément ↔ donnée, ensemble des nœuds dépliés tenu par le panneau, sélection restaurée sous un drapeau de suppression (`CasaEngine.Editor/Controls/EntitiesPanel.cs:67-76`, `:129-176`, `:427-512`).
- `NumericField` lève `ValueChanged` à chaque frappe et ne sait pas représenter « pas de valeur » (`CasaEngine.Editor/Controls/NumericField.cs:33-37`, `:143-160`).
- `ColorEditor` ne lève `ValueChanged` qu'à la validation de sa fenêtre de couleur (`CasaEngine.Editor/Controls/ColorEditor.cs:17-50`).
- `MGComboBox<T>` : `SetItemsSource`, affichage par `ToString()` ou par gabarit, pas de groupes ; poser `SelectedItem` lève `SelectedItemChanged` (`MGUI/MGUI.Core/UI/MGComboBox.cs:262-285`, `:413`). `MGCheckBox` et `MGContextMenu` existent ; `EntitiesPanel` ouvre un menu par `GetDesktop().TryOpenContextMenu(menu, e.Position)` (`EntitiesPanel.cs:298-315`).
- La validation d'un champ texte sur perte du focus s'appuie sur `Desktop.FocusedKeyboardHandlerChanged`, et le changement de focus de MGUI est différé : un clic dans l'arbre change la sélection avant que le champ ait perdu le focus (`CutsceneAssetInspectorPanel.cs:1005-1052`).
- `MaterialAssetInspectorPanel` relie ces contrôles à l'historique sous un drapeau `_isUpdating` (`CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs:598-725`, `:1314-1459`).
- Ces contrôles ne demandent qu'un `MGWindow` : ils se construisent dans le banc de test sans carte graphique (`CasaEngine.Tests/ContentBrowser/ContentBrowserViewTestHarness.cs:14-72`).

Automatisation : `--open-asset`, `--cutscene-preview-play`, `--screenshot-out`, `--diagnostics-out`, `--capture-delay` (`CasaEngine.Editor/EditorAutomationOptions.cs`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre d'actions reste le modèle enregistré ; le format `.cutscene` ne change pas (D1 et D4 du plan de la tranche A). |
| D2 | L'arbre permet dès cette tranche d'ajouter, de supprimer et de dupliquer des actions. Le réordonnancement à la souris reste pour plus tard. |
| D3 | Mode autonome : plan approuvé d'avance, exécution enchaînée, aucun push ni fusion. |
| D4 | L'auteur valide à distance sur captures d'écran ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | L'arbre va dans le panneau Hierarchy et les détails dans le panneau Inspector, par le mécanisme des panneaux contextuels. Le document garde transport, état et aperçu. C'est la disposition des autres éditeurs d'asset du moteur, et l'aperçu y gagne toute la zone du document. |
| P2 | Règle d'insertion : si l'action sélectionnée est `Sequence`, `Parallel` ou `Repeat`, la nouvelle action devient son dernier enfant ; sinon elle est insérée juste après la sélection, dans le même parent. Sans sélection, elle est ajoutée à la fin de la racine quand la racine est un conteneur. |
| P3 | L'action racine ne se supprime pas et ne se duplique pas. Supprimer un conteneur supprime ses enfants. Dupliquer une action dont le sous-arbre porte une erreur de format, une perte de chargement ou un type inconnu est refusé avec un message, parce que le clone perdrait ces marques. |
| P4 | Une action créée reçoit les valeurs par défaut de sa classe, sauf `Repeat.count` = 1 et `WaitFrames.frames` = 1. Elle peut donc être invalide tant que ses champs obligatoires sont vides : l'Inspector affiche les messages du validateur. La sauvegarde reste permise, comme aujourd'hui pour toute cutscene que le validateur critique, puisque la garde ne consulte pas le validateur. Il en va de même d'un conteneur vidé de ses enfants. |
| P5 | Les références se choisissent dans une liste déroulante des assets du bon type. La valeur écrite est celle que lit le runtime : GUID pour un son, nom de fichier pour un monde, nom du catalogue pour le reste. Une valeur absente du catalogue est conservée et affichée « (missing) ». |
| P6 | L'entité se choisit dans la liste des noms d'entités du monde d'aperçu. Sans monde d'aperçu, le champ redevient une zone de texte. |
| P7 | Un champ numérique est une zone de saisie avec boutons − et +, bornée par les limites du validateur. La frappe se valide par Entrée ou perte du focus, en **une** entrée d'historique ; les boutons valident tout de suite. Vide, il vaut « pas de valeur » pour une propriété annulable. |
| P8 | `FadeScreen.r`, `g` et `b` s'affichent comme un seul champ couleur ; sa validation écrit les trois propriétés en une entrée d'historique. |
| P9 | Restent en zone de texte : `PlaySound.bus_name` et la liste `ExitOnInput.inputs`. |
| P10 | Les lignes « Runtime state » décrivent le monde de l'éditeur en mode Play. Elles passent dans l'Inspector, sous le titre « Editor play mode », affichées quand aucune action n'est sélectionnée, avec le chemin du fichier, la validation et les pertes de chargement. |
| P11 | Les tests de `CutsceneAssetInspectorPanelTests` qui dépendent de la structure en lignes de texte sont réécrits sur la nouvelle structure, en gardant l'intention de chacun. |
| P12 | Cliquer une entité dans l'aperçu pour remplir le champ est reporté. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.Editor.MonoGame.sln`, et `dotnet build CasaEngine.MonoGame.sln` si un fichier du runtime est touché ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par les options d'automatisation, avec `--diagnostics-out` vers un dossier hors dépôt. Aucun lancement automatisé n'enregistre un asset.
- **Aucun fichier de `Projects/` n'est modifié**, et aucun fichier du runtime `CasaEngine/` non plus : cette tranche ne touche que `CasaEngine.EditorServices`, `CasaEngine.Editor`, les tests et la documentation.
- **MGUI** (sous-module) n'est pas modifié.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans `Update`, `Draw` et le layout ; l'arbre et les détails ne se reconstruisent que sur un changement, jamais à chaque frame.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts. Référence avant ce plan : 5925 tests. Seul `CutsceneAssetInspectorPanelTests.cs` est réécrit (P11) ; aucun autre test existant n'est modifié.
- `git diff b1b93f42 -- CasaEngine/ Projects/ MGUI/` : vide.
- Captures automatisées (T2.6) : l'arbre dans Hierarchy, les détails typés dans Inspector, l'aperçu sur toute la zone du document.
- Retour arrière : un commit par tâche ; annuler une tâche revient à annuler son commit.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0097

- Objectif : enregistrer D2 et P1 → P10.
- Fichiers : `docs/decisions/0097-<short-title>.md` (créé), `docs/decisions/README.md`, `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr` (il reprend P3 et P4 tels qu'écrits ici, dont le fait que la sauvegarde d'une action incomplète reste permise), statut `Accepted`, date 2026-11-18, source : ce plan et les décisions de l'auteur. Contenu : arbre dans Hierarchy et détails dans Inspector (P1) ; métadonnées de sens et de bornes des propriétés ; forme écrite des références (P5, P6) ; édition de structure limitée à ajouter, supprimer, dupliquer, avec la règle d'insertion (D2, P2, P3) ; valeurs par défaut d'une action créée (P4) ; sémantique de validation des champs (P7, P8). L'ADR dit qu'il réalise le point « adding, removing actions » reporté par ADR-0095, et que le déplacement reste reporté.
  2. Ligne d'index ; ligne de ce plan dans `ai-agent/README.md`.
- Validation : les liens de l'index pointent sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.
- Commit : `docs(decisions): record the cutscene outliner, typed details and structure editing`
- Validation faite : ADR-0097 créée (statut Accepted, 2026-11-18), ligne d'index et ligne de ce plan dans `ai-agent/README.md` ; le lien de l'index pointe sur le fichier créé ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.

---

## Phase 1 — Services d'édition (`CasaEngine.EditorServices`)

### ✅ T1.1 — Sens et bornes des propriétés

- Objectif : chaque propriété dit ce qu'elle désigne et ses limites, pour que l'interface choisisse le bon champ.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneActionProperty.cs`, `CutsceneActionPropertyEditor.cs`, `CutsceneReferenceOptions.cs` (créé), tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. Ajouter à `CutsceneActionProperty`, de façon additive : `Semantic` (énumération : valeur simple, entité, son, sprite, écran, monde, cutscene, mapping de boutons, liste d'entrées), `Minimum` et `Maximum` (annulables), et `IsMinimumExclusive` pour `MoveSprite.speed`.
  2. Renseigner ces valeurs dans la table de `CutsceneActionPropertyEditor`, d'après le validateur et les formes de référence citées dans « État vérifié ».
  3. `CutsceneReferenceOptions` : fonction pure qui, pour un sens et une liste d'`AssetInfo`, rend les options triées (libellé affiché, valeur écrite selon P5), et qui sait retrouver l'option d'une valeur lue, y compris un GUID là où le runtime accepte nom ou GUID.
- Validation : test en table sur **toutes** les propriétés de tous les types d'action (sens et bornes attendus) ; un test vérifie que chaque borne déclarée est cohérente avec `CutsceneValidator`, en ne regardant que les messages qui portent sur la clé testée : une valeur juste hors borne produit un message, une valeur à la borne n'en produit pas, sauf pour la borne exclusive de `MoveSprite.speed` où la valeur à la borne en produit un ; tests de `CutsceneReferenceOptions` pour chaque sens, dont un asset absent et un GUID.
- Commit : `feat(editor-services): describe the meaning and limits of cutscene action properties`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 5961/5961 (5925 avant, 36 ajoutés dans `CutsceneActionPropertyMetadataTests` et `CutsceneReferenceOptionsTests`). Les bornes déclarées sont vérifiées contre `CutsceneValidator` en ne regardant que les messages préfixés `Type.clé ` ; les propriétés d'octet (`FadeScreen.r/g/b`) ne portent pas de borne déclarée, leur nature `Byte` fixe 0 à 255. Le runtime n'est pas touché.

### ✅ T1.2 — Catalogue des actions et résumé

- Objectif : savoir quelles actions créer, avec quelles valeurs, et résumer une action en une ligne.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneActionCatalog.cs`, `CutsceneActionSummary.cs` (créés), tests.
- Étapes :
  1. `CutsceneActionCatalog` : la liste des types créables avec une catégorie (Flow, Timing, Movement, Sprites, Audio, Screen, World and input), `Create(type)` selon P4, `CanHaveChildren(action)` et `GetChildren(action)`.
  2. `CutsceneActionSummary.Describe(action)` : texte court sans allocation superflue, par exemple `MoveSprite  orb-left → (-60, 35) @ 120`, `Wait  1.5 s`, `Repeat  ×3` ou `Repeat  forever`.
  3. Clonage d'une action et de son sous-arbre par un aller-retour de sérialisation d'un asset temporaire (`Save` puis `Load`), puisque `SaveAction` est privée.
- Validation : tests : tous les types de `CutsceneActionTypes` sont dans le catalogue ; chaque action créée se sérialise et se recharge avec le même type et sans erreur de format ; le clone est égal à la source par sa forme sérialisée et n'en partage aucune liste ; un résumé par type.
- Commit : `feat(editor-services): add the cutscene action catalogue, summaries and cloning`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 5996/5996 (5961 avant, 35 ajoutés dans `CutsceneActionCatalogTests`). Les 23 types de `CutsceneActionTypes` sont dans le catalogue, chacun créé se sérialise et se recharge sans erreur de format ; le clone passe par `Save` puis `Load` de `CutsceneAssetJsonSerializer` (un type inconnu ou une erreur de format n'est donc pas copié, la duplication de T1.3 les refuse).

### ✅ T1.3 — Commandes de structure

- Objectif : ajouter, supprimer et dupliquer une action, de façon annulable.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/InsertCutsceneActionCommand.cs`, `RemoveCutsceneActionCommand.cs`, `CutsceneActionTree.cs` (créés), tests.
- Étapes :
  1. `CutsceneActionTree` : recherche du parent et de l'indice d'une action, calcul de la cible d'insertion selon P2, chemin d'une action (`root_action.actions[1]`).
  2. `InsertCutsceneActionCommand(session, parent, index, action)` et `RemoveCutsceneActionCommand(session, action)`, qui implémentent `IEditorCommand`. La suppression retire aussi de la session les pertes de chargement du sous-arbre supprimé et les remet **à leur place d'origine dans la liste** à l'annulation. La racine est refusée (P3).
  3. La duplication est une insertion du clone juste après la source, refusée dans le cas décrit par P3.
- Validation : tests : exécuter, annuler, rétablir pour chaque commande, à la racine et dans des conteneurs imbriqués ; l'instance réinsérée à l'annulation est la même ; cibles d'insertion des trois cas de P2 ; refus sur la racine ; refus de dupliquer un sous-arbre marqué ; pertes de chargement retirées puis remises au même indice, de sorte que le message de `CutsceneSaveGuard` est le même avant la suppression et après son annulation ; après une suite de commandes, l'asset se sérialise et se recharge à l'identique.
- Commit : `feat(editor-services): add undoable commands that insert and remove cutscene actions`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 6011/6011 (5996 avant, 15 ajoutés dans `CutsceneActionStructureTests`, sur un `EditorHistoryStack`). Les pertes de chargement sont retirées avec le sous-arbre puis remises à leur indice d'origine (le message de `CutsceneSaveGuard` est identique avant et après l'annulation). Ajout interne : `CutsceneEditSession.IndexOfLoadLoss` et `InsertLoadLoss`. `InsertCutsceneActionCommand.TryCreateDuplicate` et `GetDuplicateRefusal` portent la règle de duplication, `RemoveCutsceneActionCommand.GetRefusal` celle de la racine.

---

## Phase 2 — Éditeur (`CasaEngine.Editor`)

### ✅ T2.1 — Sélection et opérations du document

- Objectif : le document devient le point d'entrée unique des modifications ; l'arbre et les détails ne feront que l'appeler.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`, tests.
- Étapes :
  1. Sélection : `SelectedAction`, `SelectAction(action)`, événement `SelectionChanged`. Une action ajoutée ou dupliquée devient la sélection. À **chaque** `DocumentChanged` (commande, annulation, rétablissement, rechargement), le document vérifie par `CutsceneActionTree` que l'action sélectionnée est encore dans l'arbre, et vide la sélection sinon : aucune modification ne peut viser une action détachée.
  2. Opérations, toutes par `EditorHistoryService` dans le contexte du document : `SetProperty(action, property, value)`, `SetProperties(action, description, changes)` qui écrit plusieurs propriétés en une seule entrée par `EditorCompositeCommand`, `AddAction(type)`, `DuplicateSelectedAction()`, `RemoveSelectedAction()`. Chacune rend un message d'erreur ou nul.
  3. Événement `DocumentChanged`, levé une fois après chaque commande, annulation ou rétablissement, rechargement et sauvegarde.
  4. Aperçu : si une lecture est en cours quand `DocumentChanged` est levé, elle est relancée sur l'asset modifié. La session expose les noms d'entités du monde d'aperçu, dans une liste réutilisée.
  5. Corrections : séparateur manquant de la ligne d'état ; quand aucun monde d'aperçu n'est chargé, la zone d'aperçu est masquée au profit d'un message au lieu de montrer le monde de l'éditeur.
  6. La colonne de propriétés actuelle reste en place dans cette tâche.
- Validation : build ; tests ajoutés sur le panneau sans aperçu : chaque opération crée une entrée d'historique, marque le document modifié, lève `DocumentChanged` une fois, et s'annule ; sélection après ajout, duplication et suppression ; annuler un ajout vide la sélection ; supprimer, annuler puis rétablir laisse la sélection vide ; `SetProperties` crée une seule entrée. Les 16 tests existants restent verts sans modification.
- Commit : `feat(editor): give the cutscene document a selection and edit operations`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 6026/6026 (6011 avant, 15 ajoutés dans `CutsceneDocumentOperationsTests`, les 16 tests de `CutsceneAssetInspectorPanelTests` inchangés et verts). `SetProperties` prend des `CutscenePropertyChange` (propriété, valeur), interne au panneau. `CommitField` passe maintenant par `SetProperty`. `DocumentChanged` est levé depuis `OnHistoryChanged` (une fois par commande, annulation, rétablissement), `LoadAsset` et la sauvegarde. Correction de la ligne d'état : séparateur `  |  ` devant le message, point manquant après la liste des requêtes ignorées de l'aperçu. Sans monde d'aperçu, le viewport est replié au profit d'un message. Non vérifié ici : le redémarrage de la lecture sur `DocumentChanged` et le message sans monde ne sont pas couverts par un test (le banc n'a pas de session d'aperçu), à voir sur les captures de T2.6.

### ✅ T2.2 — Arbre des actions dans Hierarchy

- Objectif : le panneau Hierarchy montre l'arbre du document cutscene actif.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs` (créé), `CasaEngine.Editor/GameEditor.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. `CutsceneOutlinerPanel(MGWindow)` avec `SetEditorPanel(document)`, sur le modèle de `EntityAssetHierarchyPanel`. Barre du haut : liste déroulante plate des types du catalogue, chacun préfixé par sa catégorie (`Sprites / MoveSprite`), bouton Add, boutons Duplicate et Delete. Dessous : `MGTreeView` dans son propre `MGScrollViewer`, un élément par action, en-tête = type et résumé. `SetEditorPanel(null)` détache le panneau de l'ancien document et vide l'arbre.
  2. Reconstruction complète sur `DocumentChanged` uniquement ; les nœuds dépliés et la sélection sont conservés par référence d'action, comme dans `EntitiesPanel`. Tout est déplié à la première ouverture.
  3. La sélection de l'arbre et celle du document se suivent dans les deux sens, sous un drapeau qui évite la boucle. Duplicate et Delete sont inactifs sur la racine.
  4. Clic droit sur un élément : menu Duplicate et Delete.
  5. `GameEditor` : enregistrer une `ContextualPanelDefinition` de rôle Hierarchy pour `EditorDocumentKind.Cutscene`. Le panneau est relié au document actif dans `ContentFactory` **et** dans `Refresh`, comme pour `EntityAsset` et `TileMap` (`GameEditor.cs:3869`, `:3924`), sinon passer d'une cutscene à une autre garderait l'ancien document. À la fermeture du dernier onglet cutscene, le panneau est détaché.
  6. `GameEditor.OnDockHostActivePanelChanged` : ajouter la branche cutscene qui appelle `ActivateCutsceneDocument`, pour qu'un clic sur un onglet cutscene en fasse le document actif et le contexte d'historique actif.
- Validation : build ; tests sans carte graphique : l'arbre reflète l'asset du sample `loop.cutscene` ; Add, Duplicate et Delete passent par le document et l'arbre se met à jour ; la sélection et l'état déplié survivent à une reconstruction et à une annulation ; la règle de P2 dans ses trois cas ; relier le panneau à un second document remplace l'arbre et ne laisse aucun abonnement au premier. L'activation par onglet est vérifiée en T2.5. Capture automatisée envoyée à l'auteur.
- Commit : `feat(editor): show the cutscene action tree in the hierarchy panel`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 6033/6033 (6026 avant, 7 ajoutés dans `CutsceneOutlinerPanelTests` : arbre de `loop.cutscene`, Add, Duplicate et Delete par le document, refus de la racine, règle P2 dans ses trois cas, sélection et état déplié conservés après reconstruction et annulation, second document sans abonnement résiduel). Capture `30-outliner` (loop.cutscene, délai 3 s) regardée : le panneau Hierarchy montre la barre (liste `Flow / Sequence`, Add, Duplicate et Delete grisés sans sélection), l'arbre déplié avec type puis résumé atténué (`MoveSprite orb-left → (-60, 35)`, `Repeat forever`), et `27 actions` en pied. Défaut restant : les résumés longs (ExitOnInput, TintSprite) sont coupés à droite dans la largeur de 350 px, une barre de défilement horizontale permet de les lire (O3). Écart : l'arbre est ajouté directement au panneau, sans `MGScrollViewer` supplémentaire, car `MGTreeView` porte déjà le sien. Le panneau Inspector affiche encore « No inspector is available » (T2.3) et le document garde sa colonne de texte (T2.4). Les tests ne couvrent pas l'activation par clic d'onglet (T2.5).

### ✅ T2.3 — Détails typés dans Inspector : valeurs

- Objectif : le panneau Inspector montre l'action sélectionnée avec des champs typés pour les valeurs simples.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneDetailsPanel.cs`, `CasaEngine.Editor/Controls/Cutscenes/CutsceneNumberField.cs` (créés), `CasaEngine.Editor/GameEditor.cs`, tests.
- Étapes :
  1. `CutsceneDetailsPanel(MGWindow)` avec `SetEditorPanel(document)`. En-tête : type de l'action et son chemin. Une ligne par propriété : libellé à largeur fixe, champ, et pour une propriété annulable un bouton qui remet « pas de valeur ».
  2. Champs par nature : nombre, entier et octet → `CutsceneNumberField` (P7) ; booléen → `MGCheckBox` ; énumération → `MGComboBox<string>` des noms, avec « (none) » si annulable ; couleur → `ColorEditor` ; vecteur → trois `CutsceneNumberField` qui valident le vecteur entier ; `FadeScreen.r/g/b` → un `ColorEditor` (P8) ; chaîne simple et liste → zone de texte validée par Entrée ou perte du focus (P9).
  3. Toute validation appelle `SetProperty` du document. Une valeur refusée remet le champ à sa valeur et affiche la raison sous le champ.
  4. Sous les champs : les messages du validateur, les erreurs de format et les pertes de chargement qui concernent l'action sélectionnée.
  5. Sans sélection : la vue « cutscene » de P10.
  6. Le panneau, dans son propre `MGScrollViewer`, se reconstruit sur `SelectionChanged` et `DocumentChanged`, jamais à chaque frame ; les valeurs sont posées sous un drapeau qui évite de créer une commande.
  7. **Avant** toute reconstruction, le panneau valide le texte en attente du champ qui a le focus, sur l'action pour laquelle il a été construit. Un texte tapé puis un clic dans l'arbre ne perd donc rien. Chaque champ se désabonne de `FocusedKeyboardHandlerChanged` quand il est retiré.
  8. `GameEditor` : `ContextualPanelDefinition` de rôle Inspector pour `EditorDocumentKind.Cutscene`, reliée dans `ContentFactory` et dans `Refresh`, détachée à la fermeture du dernier onglet cutscene.
- Validation : build ; tests sans carte graphique : type de champ attendu pour chaque nature ; taper `1.25` puis Entrée crée **une** entrée d'historique ; un bouton + crée une entrée et respecte la borne ; un texte non numérique est refusé avec un message ; vide donne « pas de valeur » pour un annulable et est refusé sinon ; l'annulation remet la valeur affichée ; `FadeScreen` : une validation de couleur crée une seule entrée et écrit les trois octets ; taper dans un champ sans appuyer sur Entrée puis appeler `SelectAction` sur une autre action crée exactement une entrée, et l'ancienne action porte la valeur tapée.
- Commit : `feat(editor): edit cutscene action values with typed fields in the inspector`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur, 0 avertissement ; `dotnet test CasaEngine.Tests` 6046/6046 (6033 avant, 13 ajoutés dans `CutsceneDetailsPanelTests`) : champ de chaque nature, 1.25 puis validation = une entrée et annulation qui remet le champ, boutons + et − dans les bornes (dont la borne exclusive de `MoveSprite.speed`), texte non numérique, négatif et vide refusés avec la raison sous le champ, vide = « pas de valeur » pour un annulable (et bouton de remise à zéro), liste d'énumération et case à cocher, couleur de `FadeScreen` en un champ et une entrée pour les trois octets, vecteur validé en entier, texte tapé puis `SelectAction` sur une autre action = exactement une entrée sur l'ancienne action, messages de l'action mis à jour, panneau détaché par `SetEditorPanel(null)`. Capture `31-inspector-cutscene-view` (loop.cutscene, délai 3 s) regardée : l'Inspector montre le titre `SpriteLoop`, le chemin, la consigne, `Validation` (« No validation warnings or errors. ») et `Editor play mode` (Runtime state, Active coroutines), texte à la ligne sans coupure ni barre de défilement horizontale ; la colonne de texte du document est encore là (T2.4). Les champs typés ne sont pas visibles sur une capture avant T2.5 (sélection par option), ils sont couverts par les tests. Écarts : `CutsceneNumberField` et `CutsceneTextCommitBinding` sont dans `CasaEngine.Editor/Controls/Cutscenes/` ; un changement du document avec la même action sélectionnée remplit les champs en place (sous drapeau) au lieu de reconstruire, pour que le champ en cours d'édition garde le focus ; le panneau écoute un nouvel événement interne `RuntimeDisplayChanged` du document pour les lignes « Editor play mode » ; l'entité, le son, le sprite, etc. restent en zone de texte jusqu'à T2.4 ; les boutons − et + du nombre vont de 1 en 1 (0,1 quand la borne haute est 1).

### ✅ T2.4 — Détails typés : références, puis retrait de la colonne de texte

- Objectif : entités et assets se choisissent dans des listes ; le document ne montre plus de champs texte.
- Fichiers : `CutsceneDetailsPanel.cs`, `CutsceneAssetInspectorPanel.cs`, `GameEditor.cs`, `CasaEngine.Tests/Editor/CutsceneAssetInspectorPanelTests.cs` (réécrit, P11), tests.
- Étapes :
  1. Entité → liste déroulante des noms du monde d'aperçu, plus la valeur courante marquée « (missing) » si elle n'y est pas ; zone de texte sans monde d'aperçu (P6).
  2. Son, sprite, écran, monde, cutscene, mapping → liste déroulante de `CutsceneReferenceOptions` (P5).
  3. Retirer du document la colonne de propriétés et son code (`RefreshInspector`, `AddActionNode`, `CreateFieldRow`, `CommitField`) : l'aperçu occupe toute la zone sous la barre de transport et la ligne d'état. Sans aperçu, le document montre son en-tête, son chemin et sa ligne d'état.
  4. La vue « cutscene » de l'Inspector reprend les lignes « Runtime state » sous le titre « Editor play mode » (P10).
  5. Réécrire `CutsceneAssetInspectorPanelTests` sur la nouvelle structure. Chaque intention est conservée : champ validé → entrée d'historique et document modifié ; annulation ; saisie invalide refusée ; valeur inchangée → rien ; sans contexte d'historique ; pendant une session Play ; sauvegarde, sauvegarde refusée par la garde, rechargement ; instantané runtime inactif et en lecture ; panneau fermé ; fichier illisible. La note de validation liste la correspondance ancien test → nouveau test.
- Validation : build ; tests verts ; aucun autre fichier de test existant modifié ; tests ajoutés : liste d'entités alimentée, valeur manquante conservée, valeur écrite pour chaque sens (GUID du son, nom de fichier du monde).
- Commit : `feat(editor): pick entities and assets from lists and drop the cutscene text column`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur, 0 avertissement ; `dotnet test CasaEngine.Tests` 6055/6055 (6046 avant ; `CutsceneAssetInspectorPanelTests` réécrit, 16 tests devenus 17, et 8 tests ajoutés dans `CutsceneReferenceRowsTests`). Aucun autre fichier de test existant modifié. Le document ne contient plus aucun champ texte : en-tête, chemin, ligne d'état (validation et état modifié) ; avec aperçu, barre de transport, ligne d'état, ligne d'état de l'aperçu, puis l'aperçu sur toute la zone restante. La colonne de propriétés, `RefreshInspector` (devenu `RefreshHeader`), `AddActionNode`, `CreateFieldRow` et `CommitField` sont supprimés. `CutsceneDetailsPanel.ChoiceRow` : liste « (none) », choix, puis la valeur stockée marquée « (missing) » si elle n'y est pas (conservée, jamais réécrite) ; entité = noms du monde d'aperçu, assets = `CutsceneReferenceOptions` (GUID du son, nom de fichier du monde, nom du catalogue sinon, un écran stocké en GUID est retrouvé). Écart : quand il n'y a rien à choisir (aucun asset du type dans le catalogue, ou aucune entité nommée dans le monde d'aperçu), la référence reste une zone de texte, car une liste réduite à « (none) » ne servirait à rien ; cela garde aussi vertes, sans modification, les assertions de `CutsceneDetailsPanelTests` qui attendent une zone de texte pour `entity`, `sprite` et `sound_asset_id` dans un catalogue vide. Les lignes « Editor play mode » étaient déjà dans l'Inspector (T2.3). Capture `32-document` (loop.cutscene, `--cutscene-preview-play`, délai 1 s) regardée : le document montre la barre de transport (monde, Play, Stop, Restart), `Validation OK. Not modified.`, la ligne d'aperçu (`Preview: Playing. Skipped: ExitOnInput -> SpriteExit...`) puis l'aperçu sur toute la zone, sans colonne de texte ; Hierarchy montre l'arbre et sa barre, Inspector la vue cutscene (titre, chemin, consigne, Validation, Editor play mode) sans coupure. Défauts mineurs : les résumés longs de l'arbre sont encore coupés à droite (O3) ; dans « Editor play mode » le libellé « Active coroutines » passe sur deux lignes. Les listes d'entités et d'assets ne sont pas visibles sur une capture avant T2.5 (sélection par option) : elles sont couvertes par les tests ; la liste d'entités alimentée par un vrai monde d'aperçu n'est pas couverte par un test (le banc n'a pas de session d'aperçu), seulement par construction directe de la ligne.
- Correspondance ancien test -> nouveau test de `CutsceneAssetInspectorPanelTests` : `ThePanel_ShowsOneFieldPerPropertyOfEveryAction_WithTheTextOfTheValue` -> `TheDetailsPanel_ShowsOneTypedFieldPerPropertyOfTheSelectedAction_WithTheTextOfTheValue` (et nouveau `TheDocument_HoldsNoTextField_AndShowsItsHeaderItsPathAndItsStatus`) ; `ACommittedField_WritesTheAssetThroughTheHistory_AndMakesThePanelDirty` -> même nom, champ de l'Inspector ; `AnUndo_RestoresTheValueTheFieldAndTheCleanState` -> même nom ; `AnInvalidText_RestoresTheField_ShowsTheMessage_AndCreatesNoCommand` -> `AnInvalidText_RestoresTheField_ShowsTheReasonUnderIt_AndCreatesNoCommand` ; `ATextEqualToTheCurrentValue_CreatesNoCommand` -> même nom ; `EnterOnAFieldWithAFormatError_CommitsItsValue_AndClearsTheError` -> même nom ; `WithoutAHistoryContext_AChangeIsRefused` -> même nom (la raison est vérifiée sous le champ) ; `DuringAPlaySession_AChangeIsRefused` -> même nom (idem) ; `TheSave_WritesAFileThatReloadsWithTheModifiedValue_AndMarksThePanelSaved`, `ASaveOfAnUnmodifiedDocument_WritesNothing`, `TheSave_IsRefusedWithTheReasonOfTheGuard_WhenTheTreeHoldsAnUnknownAction`, `AnUnreadableFile_IsNotLoaded`, `AClosedPanel_NoLongerFollowsTheHistory` -> mêmes noms, champ saisi dans l'Inspector ; `TheRuntimeSnapshotOfAnIdleCutscene_KeepsTheFieldsAndTheTextBeingTyped` -> même nom, sur le champ de l'Inspector ; `TheRuntimeSnapshotOfARunningCutscene_ShowsItsState` -> `TheRuntimeSnapshotOfARunningCutscene_ShowsItsStateInTheCutsceneViewOfTheDetails_AndKeepsTheFields` ; `ReloadingTheDocument_StartsAgainACleanHistory` -> même nom (sélection vidée, valeur relue après sélection). Tests ajoutés (`CutsceneReferenceRowsTests`) : son écrit en GUID, monde en nom de fichier et sprite en nom du catalogue, écran stocké en GUID retrouvé, valeur absente conservée et marquée « (missing) », « (none) » qui vide, texte quand le catalogue est vide, liste d'entités avec nom manquant conservé, texte sans monde d'aperçu.

### ✅ T2.5 — Automatisation des captures

- Objectif : pouvoir capturer l'éditeur avec une action sélectionnée et après un ajout, sans entrée utilisateur.
- Fichiers : `CasaEngine.Editor/EditorAutomationOptions.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs`.
- Étapes :
  1. `--cutscene-select-action <chemin>` : après l'ouverture de la cutscene, sélectionner l'action de ce chemin (`root_action.actions[1].actions[2]`).
  2. `--cutscene-add-action <Type>` : après la sélection, ajouter une action de ce type par l'opération du document. Rien n'est enregistré.
  3. `--cutscene-reactivate` : après l'ouverture, activer le panneau du viewport du monde, puis réactiver le panneau de la cutscene par le même chemin qu'un clic d'onglet (celui qui déclenche `OnDockHostActivePanelChanged`).
  4. Les trois options comptent dans `HasAutomation`. Les diagnostics écrivent le chemin et le type de l'action sélectionnée, le nombre d'actions de l'arbre, l'état modifié, le type et l'identifiant du document actif, le contexte d'historique actif, et le document auquel l'arbre et les détails sont reliés.
  5. Tests d'analyse des options.
- Validation : build ; tests verts ; un lancement avec `--cutscene-reactivate` donne dans les diagnostics un document actif de type `Cutscene`, un contexte d'historique `Cutscene/<identifiant du panneau>`, et l'arbre relié à ce document.
- Commit : `feat(editor): add automation switches that select, add and reactivate in a cutscene document`
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 6068/6068 (6055 avant, 13 ajoutés : 3 dans `EditorAutomationOptionsTests`, 10 dans `CutsceneActionTreePathTests`). Options `--cutscene-select-action`, `--cutscene-add-action`, `--cutscene-reactivate` (comptent dans `HasAutomation`) ; elles s'enchaînent dans cet ordre, une étape par image : réactivation (viewport du monde puis onglet cutscene par `ActivateDockPanel`, donc par `ActivePanelChanged`), sélection, ajout ; rien n'est enregistré. Ajouts à `CutsceneActionTree` : `TryResolvePath` (réciproque de `GetPath`) et `CountActions`. Lancement avec `--cutscene-reactivate` (loop.cutscene) : diagnostics `Active document kind: Cutscene`, id `panel_cutscene_asset_18086f73c97d5066a58f34a8253ead66`, contexte d'historique `Cutscene/panel_cutscene_asset_18086f73c97d5066a58f34a8253ead66`, arbre de 27 actions, `Outliner linked to the active document: True`, `Details linked to the active document: True`. Le journal montre le viewport du monde actif puis la réactivation de l'onglet cutscene.

### ✅ T2.6 — Captures de validation

- Objectif : produire les captures de la tranche et vérifier qu'elles montrent une interface utilisable.
- Étapes, sur `Projects/SpriteCutsceneSample`, avec `--diagnostics-out` hors dépôt :
  1. `loop.cutscene`, action `MoveSprite` de `orb-left` sélectionnée, aperçu en lecture à 0,5 s : arbre dans Hierarchy, champs dans Inspector (entité en liste, x, y et vitesse en champs numériques), aperçu sur toute la zone du document.
  2. `loop.cutscene`, une action `TintSprite` sélectionnée : champ couleur et durée.
  3. `exit.cutscene`, action `FadeScreen` sélectionnée : couleur unique (P8), durée, mode de fusion en liste.
  4. `exit.cutscene`, action `ChangeWorld` sélectionnée : monde en liste déroulante.
  5. `loop.cutscene` avec `--cutscene-add-action Wait` : la nouvelle action apparaît dans l'arbre, sélectionnée, et le titre de l'onglet porte l'astérisque.
- Validation : chaque capture est regardée et décrite honnêtement dans la note ; texte coupé, champ illisible ou zone vide est un défaut à corriger avant ✅, ou à noter en 🧪. `git status` ne montre aucun nouveau fichier sous `Projects/`, et `git diff -- Projects/` est vide.
- Commit : `fix(editor): scroll the new selection into view in the outliner and give typed fields more room` (correctifs relevés sur les captures, avec la mise à jour du plan)
- Validation faite : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests` 6068/6068 ; `git status` ne montre aucun nouveau fichier sous `Projects/` (seuls les deux dossiers `.casaeditor` préexistants), `git diff -- Projects/` vide. Captures dans le dossier scratchpad de la session (`40-movesprite`, `41-tintsprite`, `42-fadescreen`, `43-changeworld`, `44-add-wait`, suffixe `-final.png`), regardées après correction :
  - `40-movesprite` (loop, `actions[1].actions[4]`, lecture, délai 0,5 s) : Hierarchy montre l'arbre avec la ligne `MoveSprite orb-left -> (-60, 35)` sélectionnée et visible ; Inspector montre le titre, le chemin, puis Entity (liste `orb-left`), X `-60`, Y `35`, Speed `120` en champs avec boutons - + x, libellés alignés dans une colonne fixe, champs alignés à droite, message de validation lisible ; l'aperçu en lecture occupe toute la zone du document (deux orbes et deux anneaux).
  - `41-tintsprite` : Color (pastille et `255, 200, 70`, entièrement lisible) et Duration `0.8`, boutons x alignés.
  - `42-fadescreen` (exit) : Color unique `255, 255, 255`, Duration seconds, Blend mode en liste (`Subtractive`).
  - `43-changeworld` (exit) : Target en liste déroulante (`EndWorld`).
  - `44-add-wait` : arbre à 28 actions, la nouvelle `Wait 0 s` est sélectionnée et visible en bas de l'arbre, le titre de l'onglet porte l'astérisque, la ligne d'état dit `Modified. | Add Wait`, l'Inspector montre `Wait`, `root_action.actions[2]`, Seconds `0`.
  - Défauts trouvés sur la première série et corrigés : (1) le texte de la couleur de TintSprite était coupé (`255, 200,`) : `LabelWidth` 140 -> 124 et boutons x plus étroits ; (2) après un ajout, la nouvelle ligne sélectionnée n'était pas visible (l'arbre reconstruit n'a pas encore de géométrie) : `CutsceneOutlinerPanel` fait défiler la sélection dans la vue au premier layout suivant (`OnLayoutUpdated`).
  - Défauts restants, non corrigés : les résumés longs de l'arbre (MoveSprite de `ring-left`, TintSprite) sont coupés à droite dans la largeur du panneau, une barre de défilement horizontale permet de les lire (O3) ; le libellé `Duration seconds` passe sur deux lignes ; les boutons x des propriétés annulables n'ont pas d'infobulle (leur sens est à confirmer à la recette).

---

## Phase 3 — Documentation et recette

### ✅ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit l'arbre, les détails typés, l'ajout, la duplication et la suppression, les formes de référence et les limites ; la section sur les formats de texte est mise à jour.
- Fichiers : `docs/editor/cutscene-editor.md`, ce plan, `ai-agent/README.md`.
- Validation : relecture contre le code livré.
- Commit : `docs(editor): document the cutscene outliner and typed details`
- Validation faite : `docs/editor/cutscene-editor.md` réécrit d'après le code livré (panneaux Hierarchy et Inspector, champs typés, formes de référence, règles d'ajout, de duplication et de suppression, options d'automatisation, limites, section des formats de texte) ; pas de build nécessaire, aucun fichier de code touché. Aucun nom de jeu ni de projet tiers.

### 🧪 T3.2 — Recette de l'auteur

- Validation à distance sur les captures de T2.6, puis recette réelle à la fin (D4). Reste 🧪 d'ici là.
- Note : l'auteur valide sur les captures de T2.6 et fera la recette réelle à la fin ; la tâche reste 🧪 jusque-là.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | `MGComboBox<T>` ne sait pas grouper : la liste des types est plate, préfixée par catégorie. Un vrai menu par catégorie est à voir avec l'édition de structure de la tranche E. | T2.2 |
| O2 | `ExitOnInput.inputs` reste une liste en texte : la proposer depuis les boutons du mapping demande de charger le mapping. | T2.3 |
| O3 | Les panneaux Hierarchy et Inspector font environ 350 et 320 px de large à 1600 px : lisibilité des libellés longs à juger sur les captures. | T2.6 |
| O4 | Confirmation de P1 → P12 par l'auteur. | Toutes |

## Hors périmètre

- Timeline, tête de lecture, pause : tranches C et D.
- Réordonner, déplacer ou envelopper des actions ; glisser-déposer ; copier-coller ; sélection multiple : tranche E.
- Cliquer une entité dans l'aperçu (P12) ; gizmo de destination.
- Vérifier qu'une entité nommée existe dans tous les mondes.
- Question « modifications non enregistrées » à la fermeture d'un onglet.
- Tout changement du runtime, du format `.cutscene` ou de MGUI.
