# Plan agent IA — Éditeur de cutscenes : édition de la structure à la souris (tranche E)

Cinquième tranche du programme « éditeur de cutscenes visuel » (ADR-0096 ; plans [cutscene-editor-preview-tasks.md](cutscene-editor-preview-tasks.md), [cutscene-editor-outliner-details-tasks.md](cutscene-editor-outliner-details-tasks.md), [cutscene-editor-timeline-tasks.md](cutscene-editor-timeline-tasks.md), [cutscene-editor-transport-tasks.md](cutscene-editor-transport-tasks.md)).
Elle permet de **construire et réorganiser une cutscene à la souris** : déplacer une action par glisser-déposer dans l'arbre, la regrouper dans un conteneur, la couper, la copier et la coller, depuis un menu contextuel commun à l'arbre et à la timeline. Le format `.cutscene` ne change pas.

Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.
L'auteur a demandé le 2026-11-18 « Tranche E en mode AUTO » : ce plan est approuvé d'avance. Les points P1 → P16 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Quand un document `.cutscene` est actif :

- **glisser une action** dans l'arbre de Hierarchy la déplace : avant ou après une autre action, ou dans un conteneur (`Sequence`, `Parallel`, `Repeat`) ; un trait ou un cadre montre où elle tombera ;
- un **clic droit** sur une action, dans l'arbre comme dans la timeline, ouvre le même menu : ajouter (par catégorie), regrouper dans un conteneur, dégrouper, monter, descendre, couper, copier, coller, dupliquer, supprimer ;
- **Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+D** agissent sur l'action sélectionnée du document cutscene actif ; **Suppr** la supprime quand l'arbre a le focus ;
- chaque opération s'annule et se rétablit, et l'aperçu la rejoue comme toute autre modification.

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `5915b418`, 58 commits depuis `main` `c365b514`, non fusionnée.
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.
Référence des tests avant ce plan : 6206 tests verts.

Faits lus par l'agent principal :

- Les trois conteneurs tiennent leurs enfants dans une liste `Actions` ; `CutsceneLoadFidelity.GetChildren` la rend pour `Sequence`, `Parallel` et `Repeat`, et `null` pour les autres actions (`CasaEngine.EditorServices/Cutscenes/CutsceneLoadFidelity.cs:43-50`).
- `CutsceneAsset.RootAction` a un accesseur d'écriture public (`CasaEngine/Framework/Cutscenes/CutsceneAsset.cs:18`).
- Les pertes de chargement d'une session sont rattachées à l'**instance** de l'action, pas à son chemin (`CutsceneEditSession.cs:53-59`) ; `RemoveCutsceneActionCommand` retire celles du sous-arbre supprimé et les remet à l'annulation (`RemoveCutsceneActionCommand.cs:36-41`, `:69-80`).
- Arbre de Hierarchy (`CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`) : un `MGTreeViewItem` par action, avec deux dictionnaires élément ↔ action ; l'en-tête de chaque ligne est un `MGStackPanel` construit par le panneau (`BuildHeader`, `:430-458`) ; le clic droit sélectionne la ligne puis ouvre un `MGContextMenu` à deux entrées, `Duplicate` et `Delete`, sauf sur la racine (`OnTreeItemRightClicked`, `OnTreeRightClick`, `:353-376`). Le fichier ne traite aucune touche.
- `MGTreeViewItem` : le panneau de ligne (`HeaderPanel`) est privé ; il porte les clics gauche, double et droit de la ligne (`MGUI/MGUI.Core/UI/MGTreeViewItem.cs:209`, `:247-249`). Le contenu d'en-tête fourni par l'appelant est public (`HeaderContent`). Une ligne n'est sélectionnée que par un **clic** (`OnHeaderPanelClick`, `:615-634`), et un clic demande un relâchement à moins de 2 px de l'appui (`MGUI/MGUI.Shared/Input/Mouse/MouseTracker.cs:466`) : un appui suivi d'un glisser ne sélectionne rien.
- `CutsceneActionCatalog.Clone` ne copie ni les erreurs de format ni les types inconnus (`CutsceneActionCatalog.cs:108-123`). La duplication est refusée pour un sous-arbre qui porte un type inconnu, une erreur de format ou une perte de chargement (`InsertCutsceneActionCommand.GetDuplicateRefusal`, `InsertCutsceneActionCommand.cs:44-79`).
- Les limites d'un en-tête (`LayoutBounds`) sont dans l'espace de mise en page de la fenêtre et tiennent compte du défilement ; elles sont vides juste après une reconstruction de l'arbre, raison pour laquelle le panneau attend déjà `OnLayoutUpdated` (`CutsceneOutlinerPanel.cs:275-335`). `OnEndDraw` est levé après la restauration du découpage (`MGUI/MGUI.Core/UI/MGElement.cs:5379`, `:5400-5403`).
- D'après la relecture de ce plan : `ExecuteCommand` lève `HistoryChanged`, qui appelle `RaiseDocumentChanged` puis la reconstruction de l'arbre (`CutsceneAssetInspectorPanel.cs:321-325`, `:1259-1279` ; `CutsceneOutlinerPanel.cs:254-257`), **avant** que l'opération fixe la sélection. À la reconstruction, une ligne est dépliée si son action est dans `_expandedActions` (`CutsceneOutlinerPanel.cs:413-416`) ; l'arrivée d'une sélection ne déplie que les parents de la ligne sélectionnée (`:474-482`). Une ligne de statut est ancrée sous l'arbre (`:111-114`).
- `InsertCutsceneActionCommand.HasUnknownOrFormatError` est privé (`InsertCutsceneActionCommand.cs:101`). `CutsceneLoadLoss.Path` est une chaîne enregistrée au chargement (`CutsceneLoadLoss.cs:13`), montrée par `CutsceneSaveGuard.cs:49` : elle n'est déjà plus à jour après une insertion ou une suppression.
- Suppr sur un arbre a un précédent dans l'éditeur (`CasaEngine.Editor/Controls/ContentBrowserPanel.cs:1298-1311`). L'éditeur n'a pas d'Échap global, hors du gizmo du viewport quand celui-ci a le focus (`EditorViewportGizmoController.cs:308`).
- Raccourcis globaux (`CasaEngine.Editor/GameEditor.cs:6472-6526`) : Ctrl+Z, Ctrl+Y, Ctrl+D, Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+S, lus dans l'état brut du clavier. Ils sont suspendus seulement quand `MGDesktop.ShouldCaptureGameplayInput()` est vrai : fenêtre modale, menu contextuel ouvert, ou champ de saisie de texte au focus (`MGUI/MGUI.Core/UI/MGDesktop.cs:635-638`). **Un arbre qui a le focus ne les suspend pas.** Suppr n'est pas un raccourci global.
- `ExecuteDuplicate`, `ExecuteCopy`, `ExecuteCut` et `ExecutePaste` n'agissent que sur le document d'écran d'interface (`_activeScreenPreviewPanel`), sans regarder le type du document actif (`GameEditor.cs:2076-2157`). Le document cutscene actif se reconnaît par `_editorContext.ActiveDocument?.Kind == EditorDocumentKind.Cutscene` avec `_activeCutsceneInspectorPanel` (`GameEditor.cs:3838-3846`). Le type de document `EditorDocumentKind.Cutscene` existe (`CasaEngine.Editor/EditorDocumentKind.cs:14`).
- Timeline générique : le menu contextuel vient de `ContextMenuProvider`, de type `ITimelineContextMenuProvider`, interne à `CasaEngine.Editor` (`Controls/Timeline/Menu/ITimelineContextMenuProvider.cs:7-12`) ; sans fournisseur, le menu par défaut ne contient que les entrées des éléments et pistes `IsEditable`, donc rien pour la timeline des cutscenes.

Faits relevés par exploration (lecture seule, 2026-11-18), **à relire par l'exécutant avant de s'y appuyer** :

- `CutsceneActionTree` (`CasaEngine.EditorServices/Cutscenes/CutsceneActionTree.cs`) : `Contains` (`:12`), `TryFindParent(root, action, out parent, out index)` (`:43`), `GetPath` (`:76`), `TryResolvePath` (`:88`), `CountActions` (`:182`), `TryGetInsertionTarget(root, selection, out parent, out index, out error)` (`:208`) : sans sélection, fin de la racine ; conteneur sélectionné, dernier enfant ; sinon, juste après la sélection.
- `InsertCutsceneActionCommand(session, parent, index, action)` et son `TryCreateDuplicate` ; `RemoveCutsceneActionCommand(session, action)` et son `GetRefusal`, qui refuse la racine ; `IEditorCommand` (`Description`, `Execute`, `Undo`) ; `EditorCompositeCommand(description, commands)` (`CasaEngine.EditorServices/History/`).
- `CutsceneActionCatalog` : `Kinds` (23 types en 7 catégories), `Create(type)`, `CanHaveChildren`, `Clone(action)` par sérialisation.
- Document (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`) : `SelectAction` (`:139`), `AddAction(type)` (`:226`), `DuplicateSelectedAction()` (`:248`), `RemoveSelectedAction()` (`:274`), `GetEditRefusal` (`:294`), `ExecuteCommand` (`:321`), `RaiseDocumentChanged` (`:328`) ; `AddAction`, `DuplicateSelectedAction` et `RemoveSelectedAction` rendent un message de refus ou `null` ; `SelectAction` rend un booléen.
- MGUI : `MouseHandler` offre `DragStart`, `Dragged` et `DragEnd`, avec un seuil de 3 px ; l'élément où le glisser a commencé continue de recevoir `Dragged` et `DragEnd` quand le pointeur le quitte (`MGUI/MGUI.Shared/Input/Mouse/MouseHandler.cs:387-407`, `:848`, `:860`). D'après la relecture de ce plan : l'élément qui veut le glisser doit marquer `DragStart` comme traité (`SetHandledBy`), sinon la fenêtre, mise à jour après ses enfants, le prend et les événements suivants ne lui arrivent plus (`MGWindow.cs:1806-1812`, `MGElement.cs:4125-4152`, `MouseHandler.cs:99-117`) ; l'arbre prend le focus clavier à l'appui (`MGElement.cs:2799-2805`). `MGElement` offre `OnBeginDraw` et `OnEndDraw` (`MGUI/MGUI.Core/UI/MGElement.cs:5395`, `:5403`) et `KeyboardHandler` avec l'événement `Pressed` ; `MGTreeView` est focalisable et gère lui-même les flèches, Début, Fin, Espace et Entrée (`MGTreeView.cs:342`, `:417-498`). `MGContextMenu` : `AddButton`, `AddSeparator`, ouverture par `GetDesktop().TryOpenContextMenu(menu, position)`. Le sous-menu d'un bouton de menu contextuel se crée par `new MGContextMenu(menuParent)` (`MGContextMenu.cs:689`) ; tout autre hôte lève une exception (`MGContextMenuItem.cs:272-275`). Un menu contextuel racine se ferme quand la souris s'éloigne de plus de 75 px ou sur un appui hors de lui (`MGContextMenu.cs:879-898`).
- Tests existants : `CutsceneActionStructureTests`, `CutsceneActionTreePathTests`, `CutsceneActionCatalogTests` (`CasaEngine.Tests/EditorServices/Cutscenes/`), `CutsceneDocumentOperationsTests`, `CutsceneOutlinerPanelTests` (`CasaEngine.Tests/Editor/`) ; harnais `ContentBrowserViewTestHarness` (`Create`, `AdvanceFrame(temps, position, bouton, touches, molette)`, `Click`), qui simule aussi un glisser (utilisé par `TimelineRulerDragTests`).
- Automatisation : `TryApplyAutomationCutsceneEdits` applique `--cutscene-select-action` puis `--cutscene-add-action`, une étape par frame (`GameEditor.cs:7916-8003`) ; `IsAutomationCutsceneEditRequested` et `IsAutomationCutsceneEditCompleted` (`:7888-7896`) commandent l'application des éditions, l'ordre avec le transport (`:8031-8033`) et le délai de capture (`:7287-7298`) ; les diagnostics écrivent l'action sélectionnée, le nombre d'actions et l'état modifié (`:8160-8182`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre d'actions reste le modèle enregistré ; la timeline en est une projection. Le format `.cutscene` ne change pas. |
| D2 | Cette tranche apporte l'édition de la structure à la souris : déplacer, regrouper, couper, copier, coller. Le placement libre sur la timeline reste pour la tranche F. |
| D3 | Le temps s'affiche en secondes et en images. |
| D4 | Mode autonome : plan approuvé d'avance, exécution enchaînée, aucun push ni fusion. |
| D5 | L'auteur valide à distance sur captures d'écran ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | **Déplacer** est une commande à part : l'instance de l'action change de place, sans copie. Elle garde donc sa sélection, son état déplié, ses pertes de chargement et ses durées mesurées jusqu'au changement du document. La destination s'exprime par un conteneur et un indice **parmi ses enfants avant le déplacement**. Sont refusés : la racine, un conteneur déposé dans lui-même ou dans un de ses descendants, une destination qui n'est pas un conteneur ou qui n'est pas dans l'arbre, et un déplacement qui ne change rien. L'opération du document **reçoit l'action à déplacer** : elle ne dépend pas de la sélection, et sélectionne l'action déplacée une fois le déplacement fait. |
| P2 | **Regrouper** remplace une action par un nouveau conteneur (`Sequence`, `Parallel` ou `Repeat` créé par le catalogue) qui la contient. La racine peut être regroupée : le conteneur devient la nouvelle racine. **Dégrouper** remplace un conteneur non racine par ses enfants, dans l'ordre, à sa place ; un conteneur vide est simplement retiré. Le nombre de tours d'un `Repeat` dégroupé est perdu, et revient à l'annulation. |
| P3 | **Presse-papiers** : un objet de l'éditeur, partagé par tous les documents cutscene, qui garde une copie de l'action copiée. Chaque collage en insère une nouvelle copie, à l'endroit que la règle d'ajout existante donne pour la sélection. Couper = copier puis supprimer ; la racine se copie mais ne se coupe pas. **Copier et couper sont refusés pour un sous-arbre qui porte un type inconnu, une erreur de format ou une perte de chargement**, par la même règle que la duplication : la copie les perdrait, et après un couper-coller l'enregistrement écrirait cette perte sans rien montrer. Ce presse-papiers n'est pas celui du système. |
| P4 | **Une seule action à la fois.** L'arbre de MGUI n'a qu'une sélection : la sélection multiple n'entre pas dans cette tranche. On regroupe plusieurs actions en en regroupant une, puis en glissant les autres dans le conteneur. |
| P5 | **Glisser-déposer dans l'arbre, sans modifier MGUI.** Le panneau écoute le glisser du bouton gauche, trouve la ligne sous le pointeur par les limites des en-têtes, et dessine lui-même le repère. La bande d'une ligne va de son haut au haut de la ligne visible suivante (pour la dernière ligne, jusqu'à son bas) : aucune position entre deux lignes n'est sans réponse. Zones d'une bande : quart haut = avant ; quart bas = après ; milieu = dans le conteneur (dernier enfant) si la ligne est un conteneur, sinon moitié haute = avant et moitié basse = après. « Après » un conteneur déplié qui a des enfants signifie premier enfant de ce conteneur. Il n'y a de destination que si le pointeur est **dans la zone visible du défilement de l'arbre**, en abscisse comme en ordonnée ; hors de cette zone : aucune destination, aucun repère. Sous la dernière ligne, dans cette zone : fin de la racine. Au-dessus de la première ligne : aucune destination. La ligne de la racine n'accepte que « dans » : ses quarts haut et bas donnent une destination refusée, sans repère. Le glisser ne change pas la sélection quand il commence ; après un dépôt accepté, l'action déplacée est sélectionnée, qu'elle l'ait été avant ou non. |
| P6 | Repère visuel : un trait horizontal à l'indentation de la destination pour avant et après ; un cadre autour de la ligne pour « dans » ; aucun repère quand la destination est refusée. Le déplacement n'a lieu qu'au relâchement sur une destination acceptée. Relâcher ailleurs, ou appuyer sur Échap, annule. Une reconstruction de l'arbre ou un changement de document pendant un glisser l'annule aussi. |
| P7 | Pendant un glisser, pas de défilement automatique près des bords ni de dépliage automatique d'un conteneur replié : déposer « dans » un conteneur replié ajoute à sa fin. Ce sont des limites acceptées de cette tranche. |
| P8 | **Un seul menu contextuel**, construit par une classe commune et ouvert depuis l'arbre et depuis la timeline, pour l'action sélectionnée ; sans sélection (clic droit dans le vide de l'arbre), il ne propose que `Add` et `Paste`, qui insèrent à la fin de la racine. Entrées : `Add` (sous-menu par catégorie, puis type), `Wrap in` (`Sequence`, `Parallel`, `Repeat`), `Ungroup`, `Move up`, `Move down`, `Cut`, `Copy`, `Paste`, `Duplicate`, `Delete`. Une entrée dont l'opération serait refusée n'est pas affichée. Si `MGContextMenu` ne permet pas un sous-menu à deux niveaux, `Add` liste les types à plat, préfixés par leur catégorie. |
| P9 | `Move up` et `Move down` échangent l'action avec sa voisine dans le même conteneur ; ils n'en sortent pas. Avec la règle d'indice de P1, pour une action d'indice `i` dans son conteneur `P` : monter = déplacer vers `(P, i − 1)`, descendre = déplacer vers `(P, i + 2)`. Monter le premier enfant et descendre le dernier sont refusés, donc absents du menu. |
| P10 | Ctrl+C, Ctrl+X, Ctrl+V et Ctrl+D passent par les raccourcis globaux existants : quand le document actif est une cutscene, ils agissent sur ce document **et rien d'autre** ; sinon ils gardent leur comportement actuel. Un champ de saisie au focus continue de recevoir ces touches, comme aujourd'hui. |
| P11 | Suppr supprime l'action sélectionnée seulement quand l'arbre des actions a le focus clavier. Ce n'est pas un raccourci global : il ne doit jamais supprimer une action pendant qu'on travaille ailleurs. Pas de déplacement au clavier dans cette tranche : les flèches appartiennent à la navigation de l'arbre. |
| P12 | Dans la timeline, le clic droit sur un élément le sélectionne puis ouvre le menu commun. Aucun élément ne se déplace à la souris dans la timeline : cela appartient au placement libre (tranche F). |
| P13 | Après une opération, la sélection suit : l'action déplacée reste sélectionnée ; un regroupement sélectionne le conteneur, qui s'affiche déplié (tout conteneur qui apparaît dans l'arbre s'affiche déplié, voir T2.2) ; un dégroupement sélectionne le premier enfant libéré, ou rien si le conteneur était vide ; un collage sélectionne la copie. |
| P14 | Automatisation : `--cutscene-edit-ops "<op>;<op>;…"`, appliquée une opération par frame après `--cutscene-select-action` et `--cutscene-add-action`. Opérations : `select:<chemin>`, `add:<Type>`, `move:<chemin du conteneur>:<indice>` (déplace l'action sélectionnée), `up`, `down`, `wrap:<Type>`, `ungroup`, `copy`, `cut`, `paste`, `duplicate`, `delete`, `undo`, `redo`, `shortcut:<copy|cut|paste|duplicate>` (passe par le raccourci global), `menu` (ouvre le menu contextuel de la sélection), `draghover:<chemin cible>:<before|after|into>` (met l'arbre dans l'état d'un glisser en cours de l'action sélectionnée vers cette cible, sans souris). Le menu ouvert par `menu` ne se ferme pas tout seul quand la souris réelle est loin (seuil de fermeture automatique désactivé pour ce chemin). Une opération refusée s'écrit dans les diagnostics et n'arrête pas la suite. |
| P15 | Les diagnostics écrivent le plan de l'arbre, une ligne par action (chemin, type, résumé), le résultat de chaque opération, le contenu du presse-papiers et l'état du repère de glisser. |
| P16 | Le bouton `Add` et la liste des types du haut de l'arbre restent. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.Editor.MonoGame.sln`, et `dotnet build CasaEngine.MonoGame.sln` pour les tâches de la phase 1 ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par les options d'automatisation, avec `--diagnostics-out` hors dépôt. Aucun lancement automatisé n'enregistre un asset.
- **Aucun fichier de `Projects/`, de `MGUI/` ni de `CasaEngine/` (runtime) n'est modifié.**
- **Aucun test existant n'est modifié**, à une exception possible : un test qui décrit le menu contextuel à deux entrées de l'arbre, que P8 remplace. Dans ce cas il est réécrit et la note de validation le nomme.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ, de closure ni de formatage de chaîne dans `Update`, `Draw` et le layout. Pendant un glisser, la recherche de la ligne sous le pointeur et le dessin du repère n'allouent rien : la structure des lignes visibles (élément, action, profondeur, dépliée ou non) est conservée et reconstruite seulement quand l'arbre change ; leur haut et leur hauteur sont relus sur place, sans allocation, à chaque usage.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent à 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts ; référence avant ce plan : 6206 tests.
- `git diff 5915b418 -- Projects/ MGUI/ CasaEngine/` : vide.
- Captures automatisées (T2.7) : déplacement, regroupement de quatre déplacements en parallèle, collage, menu contextuel ouvert, repère de glisser, annulations.
- Retour arrière : un commit par tâche ; annuler une tâche revient à annuler son commit.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0101

- Objectif : enregistrer P1 → P16.
- Fichiers : `docs/decisions/0101-<short-title>.md` (créé), `docs/decisions/README.md`, `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr`, statut `Accepted`, date 2026-11-18, source : ce plan, ADR-0096 et ADR-0097. Contenu : les trois opérations de structure et leurs règles (P1, P2) ; le presse-papiers de l'éditeur (P3) ; une seule action à la fois (P4) ; le glisser-déposer fait dans le panneau sans changer MGUI, ses zones et son repère (P5 → P7) ; le menu commun à l'arbre et à la timeline (P8, P9, P12) ; le clavier (P10, P11) ; ce qui est reporté (sélection multiple, glisser dans la timeline, défilement et dépliage automatiques, déplacement au clavier).
  2. Ligne d'index ; ligne de ce plan dans `ai-agent/README.md`.
- Validation : les liens de l'index pointent sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.
- Commit : `docs(decisions): record the structure editing of cutscenes by mouse`
- Note de validation (2026-11-18) : ADR-0101 écrite (statut `Accepted`, sources : ce plan, ADR-0096 et ADR-0097), ligne ajoutée à l'index des décisions et à `ai-agent/README.md`. Les faits du contexte ont été relus dans le code avant d'être écrits : `GetChildren`, `GetDuplicateRefusal`, `ITimelineContextMenuProvider`, menu à deux entrées de l'arbre (`OnTreeRightClick`), `ShouldCaptureGameplayInput`, sous-menu `new MGContextMenu(menuParent)`, Suppr du navigateur de contenu. Les liens de l'index vers 0099, 0100 et 0101 pointent sur des fichiers existants ; aucun nom de jeu, de décompilation ni de projet tiers dans les fichiers touchés (recherche `rg` vide). Aucun code touché : ni build ni test lancés pour cette tâche (référence des tests inchangée : 6206).

---

## Phase 1 — Services d'édition (`CasaEngine.EditorServices`)

### ✅ T1.1 — Déplacer une action

- Objectif : P1.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/MoveCutsceneActionCommand.cs` (créé), `CutsceneActionTree.cs`, tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. Une fonction de refus, utilisable sans créer la commande : `null` si le déplacement est permis, sinon un message. Elle applique P1, destination absente de l'arbre comprise.
  2. La commande : retire l'action de son conteneur et l'insère dans la destination ; quand le conteneur est le même et que l'indice de départ est avant l'indice de destination, l'indice effectif est diminué de un. `Undo` remet l'action à son indice de départ dans son conteneur de départ. Description `Move {type}`.
- Validation : build des deux solutions ; tests : déplacement vers l'avant et vers l'arrière dans le même conteneur, avec l'ordre exact des enfants attendu ; vers un autre conteneur ; vers la fin ; refus de la racine, d'un descendant de soi, de soi-même, d'une destination non conteneur, d'une destination absente de l'arbre, d'un indice hors bornes et des deux indices qui ne changent rien ; annuler puis rétablir redonne chaque fois l'ordre exact ; les pertes de chargement d'une action déplacée sont inchangées (avec `LoadWithLosses`) ; l'instance déplacée est la même.
- Commit : `feat(editor-services): move a cutscene action inside its tree`
- Note de validation (2026-11-18) : `MoveCutsceneActionCommand` (`GetRefusal` statique + commande) créée dans `CasaEngine.EditorServices/Cutscenes/`, sans toucher à `CutsceneActionTree.cs` (ses fonctions `Contains` et `TryFindParent` suffisent). Les faits (`GetChildren`, `Contains`, `TryFindParent`, `RemoveCutsceneActionCommand`, `LoadLosses`) ont été relus dans le code. 17 tests ajoutés dans `MoveCutsceneActionCommandTests` (déplacement avant, arrière, début, fin, autre conteneur, conteneur avec son sous-arbre, équivalents de monter et descendre de P9, refus de la racine, de soi-même, d'un descendant, d'une destination non conteneur ou absente de l'arbre, d'un indice hors bornes et des deux indices sans effet, pertes de chargement inchangées avec `LoadWithLosses`, instance identique, sauvegarde et rechargement identiques, annuler et rétablir à l'ordre exact). Builds `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet test` : 6223/6223 (6206 + 17).

### ✅ T1.2 — Regrouper et dégrouper

- Objectif : P2.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/WrapCutsceneActionCommand.cs`, `UngroupCutsceneActionCommand.cs` (créés), tests.
- Étapes :
  1. Regrouper : le type doit être un conteneur du catalogue ; le conteneur est créé par `CutsceneActionCatalog.Create` ; il prend la place de l'action, qui devient son seul enfant. Pour la racine, `Asset.RootAction` devient le conteneur. `Undo` remet l'action à sa place et retire le conteneur.
  2. Dégrouper : refusé pour une action qui n'est pas un conteneur et pour la racine ; les enfants prennent la place du conteneur, dans l'ordre ; les pertes de chargement du conteneur lui-même sont retirées et reviennent à l'annulation, celles des enfants ne bougent pas. `Undo` remet le même conteneur, avec les mêmes enfants dans le même ordre.
  3. Fonctions de refus utilisables sans créer les commandes.
- Validation : build ; tests : regroupement d'une feuille, d'un conteneur et de la racine, pour les trois types ; dégroupement d'un conteneur à plusieurs enfants, vide, et d'un `Repeat` dont le nombre de tours revient à l'annulation ; refus ; annuler et rétablir ; un type non conteneur est refusé.
- Commit : `feat(editor-services): wrap a cutscene action in a container and ungroup a container`
- Note de validation (2026-11-18) : `WrapCutsceneActionCommand` (`GetRefusal`, `IsContainerType`, commande) et `UngroupCutsceneActionCommand` (`GetRefusal`, commande) créés dans `CasaEngine.EditorServices/Cutscenes/`. Les faits (`GetChildren`, `Contains`, `TryFindParent`, `RemoveCutsceneActionCommand` pour le retrait et la remise des pertes, `CutsceneActionCatalog.Create`) ont été relus dans le code. Le conteneur du regroupement est créé une fois, à la construction : annuler puis rétablir remet la même instance. Le dégroupement ne touche pas à la liste `Actions` du conteneur retiré, qui garde donc ses enfants pour l'annulation. 21 tests ajoutés dans `WrapAndUngroupCutsceneActionCommandTests` (regroupement d'une feuille, d'un conteneur et de la racine pour les trois types, `Repeat` créé à un tour, enregistrement et rechargement identiques, refus d'un type non conteneur, inconnu ou vide et d'une action hors de l'arbre, pertes de chargement inchangées au regroupement, dégroupement à plusieurs enfants, vide, imbriqué, `Repeat` dont le nombre de tours revient à l'annulation, refus de la racine et d'une feuille, regrouper puis dégrouper, pertes du conteneur retirées et remises à leur indice, celles des enfants inchangées, annuler et rétablir). Builds `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet test` : 6244/6244 (6223 + 21).

### ✅ T1.3 — Presse-papiers des actions

- Objectif : P3.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneClipboard.cs` (créé), tests.
- Étapes :
  1. Une classe qui garde une copie (`CutsceneActionCatalog.Clone`) de l'action copiée, dit si elle a un contenu et de quel type, et rend à chaque demande une **nouvelle** copie. Pas d'état statique : l'instance est créée par l'éditeur et passée aux documents.
  2. Une fonction de refus de copie pour une action d'une session : le même refus que `InsertCutsceneActionCommand.GetDuplicateRefusal` (type inconnu, erreur de format ou perte de chargement dans le sous-arbre), sans refuser la racine. `HasUnknownOrFormatError` passe de privé à interne pour être réutilisé ; il n'est pas recopié.
- Validation : build ; tests : la copie gardée ne change pas quand l'action d'origine est modifiée ensuite ; deux collages rendent deux instances distinctes, sans liste partagée ; un sous-arbre entier est copié ; presse-papiers vide ; la copie d'une action qui porte une perte de chargement (`LoadWithLosses`) ou un type inconnu est refusée et ne change pas le presse-papiers.
- Commit : `feat(editor-services): add a clipboard of cutscene actions`
- Note de validation (2026-11-18) : `CutsceneClipboard` créé dans `CasaEngine.EditorServices/Cutscenes/` : `HasContent`, `ContentType`, `GetCopyRefusal(session, action)` statique, `TryCopy(session, action)` (rend le refus ou `null`, et ne change pas le presse-papiers en cas de refus), `CreatePaste()` (nouvelle copie à chaque appel, `null` si vide) et `Clear()` ; aucun état statique. `GetCopyRefusal` refuse une action hors de l'arbre, un sous-arbre qui porte un type inconnu ou une erreur de format, ou une perte de chargement ; la racine est copiable. `InsertCutsceneActionCommand.HasUnknownOrFormatError` est passé de privé à interne et réutilisé, sans être recopié ; la boucle sur les pertes de chargement de `GetDuplicateRefusal` est en revanche répétée dans `GetCopyRefusal`, car celle-ci doit accepter la racine. Les faits (`Clone`, `GetDuplicateRefusal`, `LoadLosses`, `GetFormatErrors`) ont été relus dans le code. 13 tests ajoutés dans `CutsceneClipboardTests` (vide, type et instance neuve, copie insensible aux modifications ultérieures de l'original, deux collages sans liste ni instance partagée, sous-arbre entier, racine, remplacement du contenu, `Clear`, instances indépendantes, action hors de l'arbre, perte de chargement, erreur de format et type inconnu refusés sans changer le presse-papiers). Builds `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet test` : 6257/6257 (6244 + 13).

### ✅ T1.4 — Trouver la destination d'un dépôt

- Objectif : la règle de P5 comme calcul pur, testable sans interface.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneDropTargetResolver.cs` (créé, avec ses types), tests.
- Étapes :
  1. Entrée : la racine, l'action glissée, la liste ordonnée des lignes visibles (action, haut, hauteur, profondeur, ligne dépliée ou non) la position du pointeur (abscisse et ordonnée) et le rectangle de la zone visible de l'arbre, tous dans le même espace que les hauts des lignes. La bande d'une ligne est celle de P5. Un pointeur hors du rectangle ne donne aucune destination.
  2. Sortie : le conteneur et l'indice de destination au sens de T1.1, le genre (avant, après, dans), l'indice de la ligne de référence, la profondeur du repère, et si la destination est acceptée (par la fonction de refus de T1.1).
  3. Le calcul n'alloue rien : il lit la liste qu'on lui passe.
- Validation : build ; tests par table sur un petit arbre à deux niveaux : chaque zone d'une feuille, d'un conteneur déplié avec enfants, d'un conteneur replié, d'un conteneur vide, de la racine ; au-dessus de la première ligne (aucune destination) ; une ordonnée située dans l'écart entre le bas d'un en-tête et le haut du suivant (elle appartient à la bande de la ligne du dessus) ; sous la dernière et dans la zone visible (fin de la racine) ; pointeur sous la zone visible, à sa droite, et au-dessus d'elle alors que des lignes défilées s'y trouvent (aucune destination dans les trois cas) ; quarts haut et bas de la ligne de la racine (destination refusée) ; destination refusée quand on vise un descendant de l'action glissée ; destination sans effet refusée.
- Commit : `feat(editor-services): resolve where a dragged cutscene action would drop`
- Note de validation (2026-11-18) : `CutsceneDropTargetResolver.Resolve(root, dragged, rows, x, y, visibleArea)` créé dans `CasaEngine.EditorServices/Cutscenes/`, avec ses types : `CutsceneDropRow` (action, haut, hauteur, profondeur, dépliée), `CutsceneDropArea` (bords gauche et haut inclus, droit et bas exclus), `CutsceneDropKind` (`None`, `Before`, `After`, `Into`) et `CutsceneDropTarget` (genre, indice de la ligne de référence, conteneur, indice au sens de T1.1, profondeur du repère, message de refus ; `Accepted` vrai seulement avec un conteneur et sans refus). Le calcul lit la liste reçue (`IReadOnlyList` de structures) et n'alloue rien ; les refus sont des chaînes constantes. Pour appeler la fonction de refus de T1.1 sans session, `MoveCutsceneActionCommand.GetRefusal` a reçu une surcharge qui prend la racine ; la version à session lui délègue, sans changement de comportement (les 17 tests de T1.1 restent verts, non modifiés). Précisions de lecture de P5 : la bande de la dernière ligne s'arrête à son bas, l'ordonnée égale à ce bas donne la fin de la racine ; pour le repère, la profondeur de « après » un conteneur déplié qui a des enfants est celle de ses enfants, et celle de la fin de la racine est la profondeur de la ligne de la racine plus un ; la racine glissée ou une ligne absente de l'arbre donnent une destination refusée. Les faits (`GetChildren`, `TryFindParent`, `Contains`, `GetRefusal`) ont été relus dans le code. 56 tests ajoutés dans `CutsceneDropTargetResolverTests` (table sur un arbre à deux niveaux : feuille, conteneur déplié avec enfants, replié, vide et racine, aux frontières des quarts ; au-dessus de la première ligne ; écart entre deux en-têtes ; sous la dernière ligne et son bas exact ; sous, à droite et à gauche de la zone ; au-dessus de la zone avec des lignes défilées ; liste vide ; descendant de l'action glissée, soi-même, racine glissée, déplacement sans effet ; accord avec le refus de la commande ; zéro octet alloué sur 100 passes). Builds `CasaEngine.Editor.MonoGame.sln` et `CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet test` : 6313/6313 (6257 + 56).

---

## Phase 2 — Éditeur (`CasaEngine.Editor`)

### ⏳ T2.1 — Opérations du document

- Objectif : le document offre déplacer, monter, descendre, regrouper, dégrouper, copier, couper et coller, avec les mêmes garanties que ses opérations existantes.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/GameEditor.cs` (création et passage du presse-papiers aux documents), `CasaEngine.Tests/Editor/CutsceneDocumentOperationsTests.cs` (tests ajoutés).
- Étapes :
  1. Une méthode par opération, sur le modèle de `DuplicateSelectedAction` : contrôle par `GetEditRefusal`, commande exécutée par `ExecuteCommand`, message de refus ou `null` en retour. Pour chacune, une requête « est-ce permis » qui rend le refus sans rien faire, pour le menu. Le déplacement est `MoveAction(action, conteneur, indice)` : il reçoit l'action, sélectionnée ou non ; monter et descendre agissent sur la sélection et passent par lui. Copier et couper appliquent le refus de T1.3.
  2. Sélection après l'opération selon P13, fixée **après** `ExecuteCommand`, comme le fait `AddAction` : l'arrivée de la sélection fait déplier les parents de l'action sélectionnée. Le dépliage du conteneur lui-même est l'affaire de l'arbre (T2.2 étape 4).
  3. Monter et descendre appellent `MoveAction` avec les indices de P9.
  4. Le presse-papiers est une propriété du document, donnée par `GameEditor` (une seule instance pour l'éditeur) ; sans elle, le document en crée un pour lui seul.
- Validation : build ; tests ajoutés : chaque opération, son refus, son annulation et son rétablissement par l'historique, la sélection qui suit, l'événement `DocumentChanged` levé une fois par opération ; copier dans un document puis coller dans un autre qui partage le presse-papiers ; déplacer une action qui n'est pas la sélection la déplace et la sélectionne ; copier et couper une action qui porte une perte de chargement ou un type inconnu rendent un refus et ne changent ni le presse-papiers ni l'arbre ; monter et descendre donnent l'ordre attendu et sont refusés aux deux bouts.
- Commit : `feat(editor): move, wrap, ungroup, cut, copy and paste actions in a cutscene document`

### ⏳ T2.2 — Menu contextuel commun

- Objectif : P8, P9, P16.
- Fichiers : `CasaEngine.Editor/Controls/Cutscenes/CutsceneActionMenu.cs` (créé), `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Une classe qui construit le `MGContextMenu` d'un document pour son action sélectionnée, d'après les requêtes de T2.1 : une entrée refusée n'est pas ajoutée ; les séparateurs ne se suivent jamais et ne terminent pas le menu.
  2. `Add` : sous-menu par catégorie puis par type, avec la règle d'insertion existante. Chaque sous-menu est créé par `new MGContextMenu(menuParent)`. Repli à plat selon P8 seulement si deux niveaux se révèlent impossibles malgré cela, à dire dans la note de validation.
  3. L'arbre ouvre ce menu au clic droit, racine comprise, et dans le vide selon P8.
  4. Conteneur qui apparaît : à chaque reconstruction de l'arbre, un conteneur qui n'était pas dans l'arbre à la reconstruction précédente est ajouté à `_expandedActions`, donc affiché déplié. C'est ce qui montre déplié le conteneur d'un regroupement, un conteneur collé, et un conteneur revenu par une annulation. La première construction d'un document garde sa règle actuelle. La sélection d'un conteneur déjà présent et replié ne le déplie pas.
- Validation : build ; tests : les entrées présentes pour une feuille, un conteneur, la racine, la première et la dernière action d'un conteneur, et un presse-papiers vide ou plein ; `Cut` et `Copy` absents pour une action qui porte une perte de chargement ; sans sélection, seulement `Add` et `Paste` ; déclencher une entrée fait l'opération. Dans `CutsceneOutlinerPanelTests` : après le regroupement d'une feuille non racine, la ligne du conteneur est dépliée (`IsExpanded`) ; sélectionner un conteneur déjà présent et replié le laisse replié. Le test existant du menu de l'arbre, s'il décrit les deux anciennes entrées, est réécrit (règle d'exception).
- Commit : `feat(editor): open one context menu of structure operations on a cutscene action`

### ⏳ T2.3 — Glisser-déposer dans l'arbre

- Objectif : P5, P6, P7.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Vérifier d'abord, par un test, quel élément reçoit `DragStart` quand l'appui se fait sur une ligne : le `MouseHandler` de l'arbre, ou seulement celui du contenu d'en-tête de la ligne (`HeaderContent`). S'abonner là où l'événement arrive ; dans le second cas, l'abonnement se fait à la construction de chaque en-tête. Le gestionnaire marque `DragStart` comme traité (`SetHandledBy`), faute de quoi la fenêtre prend le glisser. Le noter dans la note de validation.
  2. Au début du glisser : retenir l'action de la ligne sous le point d'appui, **sans changer la sélection** ; la racine ne se glisse pas ; le bouton droit ne glisse pas.
  3. Lignes visibles : la structure (élément, action, profondeur, dépliée ou non) est conservée et refaite quand l'arbre est reconstruit ou qu'une ligne est dépliée ou repliée. Le haut et la hauteur de chaque ligne sont **relus sur place**, sans allocation, depuis `HeaderContent.LayoutBounds` à chaque `Dragged`, à chaque `draghover` et à chaque dessin du repère : ces limites changent avec le défilement et la mise en page sans que l'arbre soit reconstruit. Le pointeur, donné en coordonnées d'écran, est converti dans l'espace de mise en page (`ConvertCoordinateSpace`) avant d'appeler le calcul de T1.4.
  4. Dessiner le repère par `OnEndDraw` de l'arbre, selon P6, sans allocation, au décalage du dessin (`DA.Offset`) et **borné à la zone visible du défilement** de l'arbre (`ScrollViewer.ContentViewport`) : `OnEndDraw` arrive après la restauration du découpage.
  5. Au relâchement : **recalculer** la destination depuis la position courante du pointeur, puis, si elle est acceptée, appeler `MoveAction` du document avec l'action retenue, qui devient la sélection ; sinon ne rien faire. Un clic sans glisser sélectionne comme avant.
  6. Annulations : Échap pendant le glisser, lu sur l'événement clavier de l'arbre (qui a pris le focus à l'appui), l'annule ; une reconstruction de l'arbre (`Rebuild`) et un changement de document (`SetEditorPanel`) annulent tout glisser en cours : l'action retenue, la destination et le repère sont effacés, et le `DragEnd` qui suit est ignoré. Un état `draghover` posé après la dernière reconstruction n'est pas concerné.
  7. Exposer en lecture seule l'état du glisser (action glissée, destination, genre, acceptée ou non), pour les tests, les diagnostics et l'opération `draghover` de T2.6.
- Validation : build ; tests avec le harnais de souris : glisser une action **qui n'est pas sélectionnée** sur le quart bas d'une autre la place après elle (même instance, ordre exact des enfants), la sélectionne dans le document et dans l'arbre, et une annulation la remet ; après avoir changé la position de l'arbre à l'écran sans reconstruire ses éléments (fenêtre du harnais redimensionnée, ou arbre défilé), un glisser donne encore la destination et le genre attendus ; glisser dans un conteneur ; glisser un conteneur sur un de ses enfants ne fait rien ; relâcher sous l'arbre (sur la ligne de statut) et à sa droite ne change pas l'ordre des enfants ; une annulation par l'historique du document au milieu d'un glisser, puis un relâchement sans bouger : aucun déplacement, état du glisser vide, aucun repère ; `SetEditorPanel` vers un autre document au milieu d'un glisser : de même ; un clic simple sélectionne toujours ; l'état du glisser pendant le survol correspond à la zone visée. Si le harnais ne permet pas de simuler ce glisser, la tâche reste 🧪 avec la raison : le menu contextuel et l'automatisation restent les moyens livrés.
- Commit : `feat(editor): drag a cutscene action in the outliner to move it`

### ⏳ T2.4 — Clavier

- Objectif : P10, P11.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Dans `ExecuteDuplicate`, `ExecuteCopy`, `ExecuteCut` et `ExecutePaste` : si le document actif est de type `Cutscene` (`_editorContext.ActiveDocument?.Kind` et `_activeCutsceneInspectorPanel`), appeler l'opération du document cutscene actif et **sortir**, sans toucher au document d'écran ; sinon le code actuel, inchangé.
  2. Dans l'arbre : sur l'événement clavier de l'arbre (`KeyboardHandler.Pressed`), Suppr supprime l'action sélectionnée et marque la touche comme traitée. Avec l'Échap du glisser (T2.3), ce sont les deux seules touches que le panneau traite ; les flèches restent à l'arbre.
- Validation : build ; tests : Suppr sur l'arbre au focus supprime la sélection, et ne fait rien quand l'arbre n'a pas le focus ; la partie `GameEditor` n'a pas de test unitaire (aucun test ne construit `GameEditor`) : elle est validée par les opérations `shortcut:…` de T2.6 et T2.7.
- Commit : `feat(editor): route the copy, cut, paste and duplicate shortcuts to the active cutscene`

### ⏳ T2.5 — Menu contextuel dans la timeline

- Objectif : P12.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`, tests dans `CasaEngine.Tests/Editor/CutsceneTimelinePanelTests.cs` (ajoutés).
- Étapes : le panneau fournit un `ITimelineContextMenuProvider` : pour un élément, il sélectionne son action dans le document (le clic droit de la timeline sélectionne déjà l'élément visé avant de demander le menu, `TimelineViewport.cs:361-367` ; le refaire est sans effet) puis rend le menu de T2.2 ; pour le vide et pour un en-tête de piste, il ne rend rien.
- Validation : build ; tests : le fournisseur rend un menu pour un élément et sélectionne son action ; il ne rend rien pour le vide ; la timeline d'Animation2D n'a pas de fournisseur et garde son menu par défaut.
- Commit : `feat(editor): open the structure menu on an item of the cutscene timeline`

### ⏳ T2.6 — Automatisation et diagnostics

- Objectif : P14, P15.
- Fichiers : `CasaEngine.Editor/EditorAutomationOptions.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`, `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs` (tests ajoutés).
- Étapes :
  1. `--cutscene-edit-ops` : analyse de la liste en opérations, séparées par `;` ; le nom d'une opération va jusqu'au premier `:` ; `move` et `draghover` coupent leur argument au **dernier** `:` (un chemin ne contient ni `:` ni `;`). Une opération inconnue ou mal formée est gardée comme telle et notée refusée à l'application, sans écarter les autres.
  2. Application : une opération par frame, après les étapes existantes, avec la même attente qu'elles vis-à-vis de la lecture et de `--cutscene-edit-after-transport`. `IsAutomationCutsceneEditRequested` et `IsAutomationCutsceneEditCompleted` tiennent compte des opérations : sans cela `--cutscene-edit-ops` seule ne s'appliquerait jamais, ou la capture partirait trop tôt. `shortcut:<nom>` appelle la méthode du raccourci global ; `menu` ouvre le menu de la sélection à la position de sa ligne, convertie de l'espace de mise en page à l'écran, avec la fermeture automatique à distance désactivée ; `draghover` met l'arbre dans l'état de survol demandé, qui reste en place pour la capture.
  3. Diagnostics : plan de l'arbre, résultat de chaque opération, type du contenu du presse-papiers, état du glisser.
- Validation : build ; tests d'analyse (liste, opération inconnue, arguments manquants, absence) ; un lancement `--cutscene-edit-ops "select:root_action.actions[1].actions[4];up"` sur `loop.cutscene` écrit un plan où le premier `MoveSprite` est passé avant le dernier `ShowSprite`.
- Commit : `feat(editor): script the structure operations of a cutscene from the automation`

### ⏳ T2.7 — Captures de validation

- Objectif : produire les captures de la tranche et vérifier, par les diagnostics, que l'arbre obtenu est le bon.
- Étapes, sur `Projects/SpriteCutsceneSample`, `loop.cutscene`, `--diagnostics-out` hors dépôt, fichiers préfixés `f`. Les chemins exacts se lisent dans le plan de l'arbre d'un premier lancement sans opération (`f0`).
  1. Déplacement : déplacer le premier `TintSprite` en tête de la boucle ; le plan de l'arbre montre le nouvel ordre, l'action déplacée est sélectionnée, le document est modifié, la timeline montre sa barre au nouveau début.
  2. Regroupement : regrouper le premier `MoveSprite` dans un `Parallel`, puis y déplacer les trois autres ; le plan montre un `Parallel` de quatre `MoveSprite` ; avec `--cutscene-preview-seek 0.5`, l'aperçu est en pause à 0,500 s, 30 images, sur cette cutscene modifiée. Attendu par l'analyse du code : les quatre sprites ont bougé, d'un pas de moins que sans modification (`orb-left` à x = -132 au lieu de -130), parce que les branches d'un `Parallel` démarrent une mise à jour plus tard. Une valeur mesurée différente est rapportée et expliquée par le code, sans ajuster l'attente en silence ; des sprites restés à leur position de départ sont un échec.
  3. Dégroupement : sur l'arbre du lancement 2, dégrouper le `Parallel` ; le plan retrouve l'ordre d'origine des quatre `MoveSprite`.
  4. Copier-coller : copier un `Wait`, sélectionner la dernière action, coller ; puis `shortcut:copy` et `shortcut:paste` sur une autre action : le nombre d'actions augmente d'un à chaque collage et le presse-papiers annonce le bon type.
  5. Annulation : après les opérations du lancement 2, autant de `undo` que d'entrées d'historique (un regroupement et trois déplacements : 4 ; `select` n'en crée pas) : le plan est identique à celui de `f0` et le document n'est plus modifié.
  6. Menu : `select:<une feuille>;menu` : la capture montre le menu ouvert avec ses entrées lisibles ; puis de même sur un conteneur et sur la racine.
  7. Repère de glisser, avec le premier `MoveSprite` sélectionné (`root_action.actions[1].actions[4]`, à confirmer sur `f0`) : `draghover:<chemin d'un TintSprite situé plus bas>:after` : la capture montre le trait sous cette ligne, à son indentation ; puis `draghover:root_action.actions[1]:into` (la boucle) : la capture montre le cadre autour de la ligne de la boucle. Le dernier enfant de la boucle ne conviendrait pas pour « dans » : ce serait un déplacement sans effet, donc refusé et sans repère.
  8. Refus : `select:<la boucle>;move:<chemin de la boucle elle-même>:0` (la boucle de `loop.cutscene` n'a pas d'enfant conteneur) et `delete` sur la racine : les diagnostics écrivent le refus, le plan ne change pas.
- Validation : chaque capture est regardée et décrite honnêtement ; les plans d'arbre sont comparés aux attentes ; menu coupé, repère invisible ou mal placé : défaut à corriger avant ✅, ou à noter en 🧪. `git status` ne montre aucun nouveau fichier sous `Projects/` ; aucun asset n'est enregistré.
- Limite d'essais : deux séries de corrections au plus dans cette tâche ; au-delà, 🧪 avec la liste exacte de ce qui reste.
- Commit : `docs(plan): record the validation screenshots of the cutscene structure editing`, ou `fix(editor): …` si une correction est nécessaire.

---

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit le glisser-déposer et ses zones, le menu contextuel, le presse-papiers, les raccourcis, l'option `--cutscene-edit-ops` et les limites (une action à la fois, pas de glisser dans la timeline, pas de défilement ni de dépliage automatiques pendant un glisser).
- Fichiers : ce document, ce plan, `ai-agent/README.md`.
- Validation : relecture contre le code livré ; aucun nom de jeu ni de projet tiers.
- Commit : `docs(editor): document the structure editing of cutscenes by mouse`

### ⏳ T3.2 — Recette de l'auteur

- Validation à distance sur les captures de T2.7, puis recette réelle à la fin (D5). Passe en 🧪 quand les captures sont envoyées.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le glisser réel à la souris ne peut pas être essayé dans l'éditeur lancé : il est validé par les tests avec le harnais et par l'état `draghover`, puis par la recette de l'auteur. | T2.3, T2.7 |
| O2 | Coller dans une autre cutscene une action qui nomme une entité absente de son monde d'aperçu : l'action est collée telle quelle et le validateur le signale. À confirmer à la recette. | T1.3, T2.1 |
| O3 | Le chemin enregistré dans une perte de chargement (`CutsceneLoadLoss.Path`) n'est pas remis à jour quand l'action change de place ; c'était déjà vrai après une insertion ou une suppression. Limite connue, à écrire dans la documentation. | T1.1, T1.2, T3.1 |
| O4 | Confirmation de P1 → P16 par l'auteur. | Toutes |

## Hors périmètre

- Sélection multiple, et opérations sur plusieurs actions à la fois.
- Déplacer ou étirer un élément dans la timeline ; placement libre : tranche F.
- Défilement automatique et dépliage automatique pendant un glisser ; déplacement au clavier.
- Glisser un type depuis une palette vers l'arbre ; glisser d'un document à un autre.
- Presse-papiers du système.
- Tout changement du format `.cutscene`, du runtime `CasaEngine/` ou de MGUI.
