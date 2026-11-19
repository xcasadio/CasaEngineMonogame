# Plan agent IA — Le pilotage à distance de l'éditeur édite les assets `.entity`

Plan d'exécution du chantier demandé par l'auteur le 2026-11-19 (choix « Étendre le moteur » sur le point ouvert O2 du plan du jeu `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` de RacingGame). Il n'y a pas d'audit dédié : les faits viennent d'une découverte en lecture seule (trois rapports, `artifacts/entity-asset-remote/discovery.json` du dépôt du jeu, ignoré par git), recoupée par relecture du code pour chaque fait de la section « État vérifié ».
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-11-19 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Les commandes du pilotage à distance (CLI `casa-editor`, serveur MCP, ADR-0092 et ADR-0093) savent éditer un asset `.entity` par le panneau que l'éditeur a déjà pour lui : l'ouvrir, le lire avec ses composants et leurs propriétés, modifier une propriété, ajouter et retirer un composant, annuler et rétablir, l'enregistrer, et en créer un nouveau par son nom. Chaque édition passe par le code que l'inspecteur du panneau emploie et par l'historique du panneau.

Le chantier se termine côté jeu : `Entities/PlayerCar.entity` de RacingGame est ouvert, modifié, rétabli et enregistré par l'éditeur lui-même, piloté par le CLI, et le fichier est comparé à celui de l'outil (levée de O2).

Ce qu'il ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-11-19)

Branche `chantier/entity-asset-remote`, créée depuis `main` local `1ed32d862` (3 commits après `origin/main` `da32906e2`), arbre propre (`git status --short` vide, `git rev-parse HEAD main origin/main`). Aucune modification préexistante de l'auteur. Aucun build ni test lancé pour écrire ce plan : la référence est prise en T0.1.

**Registre et hôte**

- Une commande est un `EditorRemoteCommand(name, description, inputSchemaJson, readOnly, destructive, handler)` (`CasaEngine.Editor/Remote/EditorRemoteCommandRegistry.cs:9-31`). Les familles s'enregistrent dans le constructeur d'`EditorRemoteControl` (`CasaEngine.Editor/Remote/EditorRemoteControl.cs:151-168`). L'hôte est `IEditorRemoteHost` (`EditorRemoteControl.cs:16-133`), implémenté par `GameEditorRemoteHost` (`CasaEngine.Editor/GameEditor.Remote.cs:188-705`) et simulé par `CasaEngine.Tests/Editor/Remote/RemoteTestHost.cs`.
- Le CLI et le serveur MCP lisent la liste des commandes à l'exécution (relevé d'éclaireur : `Tools/CasaEngine.Editor.Cli/CliApp.cs:46-50`, `Tools/CasaEngine.Editor.Mcp/EditorToolCatalog.cs:64-84`, non relu) : à confirmer en T2.2 par `casa-editor commands`, qui doit lister les nouvelles commandes sans changement dans `Tools/`.

**Référence, ouverture et création d'un asset**

- `EditorRemoteAssets.Resolve(projectDirectory, reference, expectedType)` (`CasaEngine.Editor/Remote/EditorRemoteAssets.cs:32-91`) : id de catalogue ou fichier relatif au projet ; l'argument puis le `FileName` rendu par le catalogue passent par `EditorRemoteValidation.TryResolveInsideProject` (dans le projet, sans `:`, sans lien sous la racine, `EditorRemoteValidation.cs:64-91`, `:104-136`) ; type = extension (`AssetInfo.InferAssetType`, `CasaEngine/Framework/Assets/AssetInfo.cs:98-106`) égale au type attendu ; fichier présent.
- Types ouvrables : `EditorRemoteAssetTypes.IsOpenable` (`EditorRemoteAssets.cs:17`) : `material`, `particle`, `uiscreen`, `sound`. Ce prédicat a **deux** usages : `Resolve` avec `expectedType` nul, donc `asset.open` (`EditorRemoteAssets.cs:78`, `EditorRemoteAssetCommands.cs:74-86`), et l'aiguillage de l'automatisation `TryOpenAutomationAsset` (`CasaEngine.Editor/GameEditor.cs:8196-8211`, appelée en `:8179` et `:8289`) : un type ouvrable passe par `asset.open`, tout autre garde la route de l'éditeur (`TryOpenEditorAsset`). Aujourd'hui `--open-asset X.entity` prend donc la route de l'éditeur, qui ouvre aussi un `.entity` absent du catalogue.
- Création : `EditorRemoteAssetCreation.Prepare` (`EditorRemoteAssetCreation.cs:21-80` : nom à la règle des mondes, dossier existant dans le projet et non lien, aucun nom ni fichier déjà au catalogue, fichier absent) puis `Reserve` ou `WriteNew` (`:86-101`, `FileMode.CreateNew`). Deux modèles existent : `material.create` sérialise dans la commande et écrit par `WriteNew` (`EditorRemoteMaterialCommands.cs:37-59`) ; `sound.create` et `particle.create` passent par l'hôte, qui réserve le fichier, ajoute au catalogue, écrit par `EditorAssetWriterService.SaveAsset`, enregistre le catalogue, et en cas d'échec retire l'entrée et supprime le fichier réservé (`GameEditor.Remote.cs:438-466`, `:511-537`).
- Le sérialiseur d'une entité, `EditorEntityJsonSerializer` et `EditorAssetJsonSerializer`, est `internal` à `CasaEngine.EditorServices` (`EditorEntityJsonSerializer.cs:16`, `EditorAssetJsonSerializer.cs:25`), dont les internes ne sont visibles que de `CasaEngine.Tests` (`InternalsVisibleTo.Tests.cs:3`) : `CasaEngine.Editor` n'atteint l'écriture d'une entité que par `EditorAssetWriterService.SaveAsset` (`EditorAssetWriterService.cs:47-70`, public) ou `EditorEntityWriter.SaveEntity` (public).
- **Aucun chemin de l'éditeur ne crée un asset `.entity`** : `rg "FileNameExtensions\.Entity"` ne rend que la route d'ouverture (`GameEditor.cs:4642`), le dépôt d'un asset dans un monde (`WorldViewportPanel.cs:1088`, `:1144`) et l'aperçu (`AssetPreviewClassifier.cs:18`).

**Éditions d'un monde (modèle des éditions)**

- `component.add` (`EditorRemoteEditCommands.cs:208-247`), `component.remove` (`:249-266`, `destructive: true`), `property.set` (`:268-291`), `history.undo` et `history.redo` par `StepHistory` (`:363-406`). Ils passent par `EditorWorldOperations` (`CasaEngine.Editor/Runtime/EditorWorldOperations.cs` : `TryCreateComponent` `:305-315` sur la liste `ComponentTypes` `:44`, `AddComponent` `:321-347`, `TryRemoveComponent` `:382-416`, `SetProperty` `:419-444`), tous paramétrés par un `EditorHistoryContext`, par `EditorPropertyRules.FindEditableProperty` (`Runtime/EditorPropertyRules.cs:83-95`) et par `EditorRemoteValidation.ConvertPropertyValue` (`EditorRemoteValidation.cs:249-341` : nombres finis, textes de 4096 caractères au plus, noms d'énumération, GUID). Lecture : `EditorRemoteWorldModel.EnumerateComponents`, `FindComponent`, `DescribeComponent` (`EditorRemoteWorldModel.cs:48-85`, `:120-146`), textes tronqués à 2000 caractères (`:180-181`).
- `StepHistory` n'atteint qu'un contexte rendu par `host.TryGetAssetHistoryContext`, donc celui d'un panneau ouvert, jamais créé (`:376-380`).
- L'inspecteur nomme le composant qu'il ajoute (`EditorComponentNames.CreateUniqueName`, `Controls/EntityDetailsPanel.cs:1596-1599`) ; `component.add` ne le fait pas.

**Panneau d'un asset `.entity`**

- Ouvert par `GameEditor.TryOpenEntityAsset` (`GameEditor.cs:5639-5692`) : `EntityAssetEditorPanel.TryLoadAsset` (`Controls/EntityAssetEditorPanel.cs:243-282` : JSON avec `root_component` ou `components`, `Entity.Load`, nom et id pris de l'entrée de catalogue du fichier), id de panneau `panel_entity_asset_<AssetId:N>` (`EditorPanelIds.EntityAssetDocumentPrefix`, `Workspaces/EditorPanelIds.cs:16`), panneau rangé dans `_entityAssetEditorPanels` (`GameEditor.cs:228`). Pour un fichier **absent du catalogue**, `TryLoadAsset` prend l'id écrit dans le fichier (`:271-274`) : une copie d'un `.entity` catalogué, ouverte depuis l'interface ou par `--open-asset`, reçoit donc **le même id de panneau** que l'asset catalogué. Le panneau est enregistré dans le dictionnaire avant la création de son onglet ; si l'onglet ne peut pas être créé, `TryOpenEntityAsset` rend `false` et le panneau reste enregistré sans onglet (`GameEditor.cs:5651-5656`, `:5674-5677`).
- Le panneau tient l'entité vivante (`LoadedEntity`, `:57`), le chemin chargé (`LoadedRelativePath`, `:63`), son contexte d'historique (`EditorHistoryContextKind.Entity`, id = l'id du panneau, `:65-67`) et `IsDirty` (`:69`). L'entité vit dans un monde d'aperçu mis à jour en continu (`PreviewWorldDriver`, `UpdateMode = Continuous`, `:50-54`, `:298-310`).
- `TrySaveLoadedAsset` (`:139-172`) **n'écrit rien si le panneau n'est pas modifié** (`:149-152`) ; sinon `EditorAssetWriterService.SaveAsset(_loadedRelativePath, …)`, qui écrit `Path.Combine(EngineEnvironment.ProjectPath, fileName)` par `File.CreateText` (`EditorAssetWriterService.cs:59-70`) : un `fileName` absolu remplacerait la racine, et `File.CreateText` suit les liens.
- « Modifié » = révision courante de l'historique différente de la révision enregistrée (`History/EditorDirtyStateService.cs:97-101`) ; la révision courante est celle de la dernière entrée de la pile d'annulation (`CasaEngine.EditorServices/History/EditorHistoryStack.cs:39`). Conséquence : modifier puis **annuler** rend le panneau non modifié (rien ne sera écrit) ; modifier puis **remettre la valeur par une seconde modification** le laisse modifié (le fichier sera écrit).
- L'inspecteur du panneau est un `EntityDetailsPanel` (`GameEditor.cs:1834-1845`) qui reçoit le contexte du panneau actif (`RefreshEntityAssetViews`, `:3973-3982`) et passe par les mêmes `EditorWorldOperations` (`EntityDetailsPanel.cs:1635-1642`). Une entrée exécutée dans un contexte `Entity` rafraîchit déjà les vues et le titre (`OnEditorHistoryChanged`, `GameEditor.cs:2203-2218`) ; après annulation ou rétablissement, `RefreshViewsAfterHistoryCommand` le fait (`:2174-2201`).
- `project.save` enregistre déjà les panneaux d'entité modifiés (`SaveDirtyEntityAssetEditors`, `GameEditor.cs:6053-6074`). Fermer le panneau libère son monde d'aperçu, son historique et son état modifié (`OnDockHostPanelRemoved`, `:3067-3078`).
- **Un fichier `.entity` contient une seule entité** : `EditorEntityJsonSerializer.SaveEntity` écrit l'entité, sa racine, ses composants et son script, pas d'entité enfant (`EditorEntityJsonSerializer.cs:74-110`), et `Entity.Load` n'en lit pas (`CasaEngine/Framework/Scene/Entities/Entity.cs:582-631`).

**Ce que l'hôte ne sait faire que pour quatre types**

- `OpenAsset` (`GameEditor.Remote.cs:342-369`), `TryGetAssetHistoryContext` (`:378-408`), `TrySaveAsset` (`:562-597`), `FindAssetPanelId` et `GetAssetPanelKind` (`:600-650`), dont dépendent `ListPanels` (`:279-304`) et `ClosePanel` (`:321-340`, qui refuse un panneau modifié). Un panneau d'entité ouvert est aujourd'hui de genre `other` pour `panel.list` et non fermable par `panel.close`.

**Tests**

- `CasaEngine.Tests/Editor/Remote/` : les tests créent `new EditorRemoteControl(host, dossier)` avec un `RemoteTestHost` et appellent le répartiteur (`EditorRemoteAssetCommandsTests.cs:20-65`) ; les tests d'édition travaillent sur de vraies entités et le vrai `EditorHistoryService.Current` (`EditorRemoteEditCommandsTests.cs:25-47`).
- Aucun test ne couvre `EntityAssetEditorPanel` : son constructeur demande une fenêtre MGUI, un `GraphicsDevice` et le runtime de l'éditeur (`EntityAssetEditorPanel.cs:40-55`). `TryLoadAsset` est statique et sans dépendance graphique (`:243-282`).

**Manques relevés, non corrigés par ce plan** (voir « Hors périmètre »)

- L'enregistrement d'un `.entity` ne rafraîchit pas les mondes ouverts qui le référencent : `OnEditorAssetSaved` n'a pas de branche `.entity` (`GameEditor.cs:727-760`).
- `EditorEntityWriter.SaveEntity` n'a aucun appelant dans le moteur (`rg EditorEntityWriter` : son seul fichier) ; l'outil du jeu s'en sert.
- `component.add` ne nomme pas le composant ajouté, contrairement à l'inspecteur.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Périmètre « Complet avec création »** : ouvrir un `.entity`, le lire avec composants et propriétés, modifier une propriété, ajouter et retirer un composant, annuler et rétablir, enregistrer, créer un nouvel asset `.entity` par son nom. |
| D2 | **Forme « Nouvelle famille »** : commandes `entity_asset.*`, et `asset.open` accepte un `.entity`. Les commandes existantes ne changent pas de contrat. |
| D3 | **Branche « Avancer main d'abord »** : fait le 2026-11-19, sans push (`main` du moteur à `1ed32d862`, `remaster` du jeu à `433a09e`). La branche du chantier part de `main`. |
| D4 | **Mode `AUTO`** une fois ce plan approuvé : commits sur les branches du chantier seulement ; ni push, ni avance de `main`, `master` ou `remaster`, ni suppression hors de ce que ce plan nomme. |

Autres réponses de l'auteur du même jour, qui bornent le travail : aucune ADR n'est acceptée pour l'instant (la nouvelle ADR reste `Proposed`) ; pour les mondes du jeu, le code (`RaceWorldTemplates`, `RgceAssetCooker --worlds`) fait foi.

## Points à valider avec ce plan

Arbitrages proposés par l'agent : l'approbation du plan les valide.

| Réf | Proposition |
|---|---|
| P1 | **Commandes**, dans un nouveau fichier `CasaEngine.Editor/Remote/EditorRemoteEntityAssetCommands.cs`. `asset` = id de catalogue ou fichier relatif au projet, résolu par `EditorRemoteAssets.Resolve` avec le type attendu `entity`. <br>• `entity_asset.create {name, folder}` → `{asset_id, file}` ;<br>• `entity_asset.get {asset}` (lecture seule ; ouvre le panneau, comme `material.get`) → `{asset_id, file, modified, entity: {id, name, components}}`, chaque composant dans la forme d'`entity.get` (id, type, placement, parent, propriétés éditables) ;<br>• `entity_asset.set_property {asset, component_id, property, value}` → `{changed}` ;<br>• `entity_asset.add_component {asset, type, parent_component_id?}` → `{component_id, placement}` ;<br>• `entity_asset.remove_component {asset, component_id}` → `{removed}`, marquée `destructive` comme `component.remove` ;<br>• `entity_asset.save {asset}` → `{saved, written}`. |
| P2 | **Une entité par asset, pas d'argument `entity_id`.** Le format n'enregistre qu'une entité (État vérifié) : les commandes agissent sur l'entité de l'asset, et un `component_id` qui n'est pas un de ses composants est refusé (`not_found`). |
| P3 | **Commandes existantes, ajouts sans changement de schéma** : `asset.open` accepte le type `entity` ; `panel.list` rend le genre `entity` pour un panneau d'entité (aujourd'hui `other`) ; `panel.close` ferme un panneau d'entité **non modifié** et refuse un panneau modifié (`unsaved_changes`), comme pour les quatre autres genres ; `history.undo` et `history.redo` acceptent en `context` l'id d'un asset `.entity` **dont le panneau est ouvert**, jamais d'un panneau non ouvert. Les descriptions et messages qui énumèrent « material, particle, UI screen, sound » sont complétés. |
| P4 | **Automatisation inchangée** : `--open-asset` d'un `.entity` garde la route de l'éditeur (il ouvre aussi un `.entity` absent du catalogue). L'aiguillage de `TryOpenAutomationAsset` prend un prédicat à lui, limité aux quatre types d'aujourd'hui, au lieu d'`IsOpenable`. Les smokes d'automatisation (`Tools/automation-smokes.sh`) ne changent pas de chemin. |
| P5 | **Éditions** : par `EditorWorldOperations` avec le contexte d'historique du panneau ; une entrée d'historique par édition (aucune quand la valeur ne change pas) ; refusées en mode Play (`play_mode_active`) ; mêmes filtres que pour un monde (types de `component.types`, propriétés de `EditorPropertyRules`, valeurs de `ConvertPropertyValue`) et mêmes règles de placement que `component.add` et `component.remove`. Deux différences avec `component.add`, parce que l'édition passe par le code du panneau (ADR-0093) : le composant ajouté reçoit le nom que l'inspecteur lui donnerait (`EditorComponentNames.CreateUniqueName`), et la sélection du panneau suit l'édition (composant ajouté sélectionné, composant retiré désélectionné), pour que l'inspecteur ne montre pas un composant détaché. |
| P6 | **Création** : un asset `.entity` **vide** (une entité au nom donné, sans composant ; le client ajoute la racine par `entity_asset.add_component`), au modèle de `sound.create` : `Prepare`, fichier réservé par `FileMode.CreateNew`, entrée de catalogue, écriture par `EditorAssetWriterService.SaveAsset`, catalogue enregistré ; sur **toute** exception après la réservation (S3-4), l'entrée de catalogue est retirée de la mémoire et le fichier réservé supprimé, chaque geste gardé séparément, puis la commande répond `failed`. Non annulable, comme les autres créations. L'éditeur n'a aujourd'hui aucun geste qui crée un `.entity` : cette forme vide est un choix de ce plan. |
| P7 | **Enregistrement** : `entity_asset.save` enregistre par le panneau (ouvert au besoin) et garde sa règle : `written` vaut `true` seulement si le panneau était modifié, sinon aucun fichier n'est touché et `written` vaut `false`. Après une écriture, le panneau n'est plus modifié et son titre est mis à jour, comme le fait `project.save`. `asset.open`, `entity_asset.get` et `entity_asset.save` ne rechargent jamais un panneau ouvert (règle existante d'`OpenAsset`). |
| P13 | **Le panneau tient bien le fichier résolu (S3-1, S3-2, S3-6).** Le panneau d'un asset est retrouvé par son id, et un autre fichier peut porter cet id (État vérifié). Règle unique, écrite une fois dans une fonction pure testée (`EditorRemoteEntityAssets.IsLoadedFile`) : le chemin chargé par le panneau n'est ni nul, ni absolu, ni porteur de `..`, et `Path.GetFullPath(Path.Combine(<projet>, <chemin chargé>))` égale le chemin complet résolu par `Resolve`, sans tenir compte de la casse. Elle s'applique : à **chaque** commande `entity_asset.*`, `get` compris, avant toute lecture ou édition (refus `failed`, aucune entrée d'historique) ; à `OpenAsset` de l'hôte pour un `.entity` (un panneau ouvert sous cet id pour un autre fichier fait échouer l'ouverture) ; à `TryGetAssetHistoryContext` pour un panneau d'entité, qui exige en plus que l'id soit celui d'une entrée `.entity` du catalogue (sinon `history.*` répond `not_found`). Un panneau d'entité sans onglet dans le dock n'est pas tenu pour ouvert. |
| P14 | **Play (S3-7).** Ouvrir un `.entity` charge ses composants et son script dans un monde d'aperçu mis à jour en continu : du code du projet tourne, comme avec `play.start` ou l'ouverture du même asset à la main. Pour ne pas créer ce second monde pendant une partie : **toutes** les commandes `entity_asset.*`, `get` compris, et `asset.open` d'un `.entity` sont refusées en mode Play (`play_mode_active`). `entity_asset.get` reste marquée lecture seule, comme les autres `*.get` qui ouvrent leur panneau : le drapeau dit que le projet n'est pas modifié ; l'ADR et la documentation disent que l'ouverture exécute du code du projet. |
| P8 | **Hôte** : `IEditorRemoteHost` gagne `GetEntityAsset(assetId)` (l'entité du panneau ouvert, sinon `null`), `GetEntityAssetLoadedFile(assetId)`, `IsEntityAssetModified(assetId)`, `SelectEntityAssetComponent(assetId, component)` et `CreateEntityAsset(file, out assetId, out error)` ; un nouveau fichier `CasaEngine.Editor/Remote/EditorRemoteEntityAssets.cs` porte la règle de P13 ; `OpenAsset`, `TryGetAssetHistoryContext`, `TrySaveAsset`, `FindAssetPanelId` et `GetAssetPanelKind` apprennent le panneau d'entité. `RemoteTestHost` est étendu de même. Aucun changement dans `Tools/`, ni dans `EntityAssetEditorPanel`, ni dans `EditorWorldOperations`. |
| P9 | **Tests** dans `CasaEngine.Tests/Editor/Remote/` (liste dans les tâches). Ce que les tests unitaires ne peuvent pas couvrir, le panneau réel (écriture ou non à l'enregistrement, rafraîchissement, fermeture), est prouvé dans l'éditeur lancé (T2.2), par l'empreinte et la date du fichier avant et après. |
| P10 | **Copies hors dépôt, créées par ce plan et supprimées à sa clôture** (l'approbation du plan couvre leur suppression) : `<scratchpad>/entity-asset-remote/sample/` (copie de `Projects/SampleProject`, T2.2), `<scratchpad>/entity-asset-remote/rgce/` (copie de `RacingGameCasaEngine/Content` du jeu, T3.1) et `<scratchpad>/entity-asset-remote/verif/` (copies neuves des deux, faites par le vérificateur, T4.1), `<scratchpad>` étant le dossier temporaire de la session d'agent (`%TEMP%/claude/…/scratchpad`). Scripts et sorties gardés : `artifacts/entity-asset-remote/` du dépôt du jeu (ignoré par git). Rien n'est écrit dans `Projects/` ni dans `%LOCALAPPDATA%/CasaEngine/RacingGameCasaEngine/`. **Jonctions d'essai (S3-3)** : une jonction créée pour essayer un refus a pour cible un dossier jetable créé dans la même copie, jamais le dépôt ni `Projects/` ; elle est retirée seule, d'abord (`cmd /c rmdir <jonction>`, sans `/s`), la cible est contrôlée inchangée, et seulement ensuite la copie est supprimée. |
| P11 | **Documents** : `docs/editor/editor-remote-control.md` (table des commandes, règles des assets, limites) ; ADR-0129 `Proposed`, qui étend l'ADR-0093 ; ligne de ce plan dans `ai-agent/README.md`. |
| P12 | **Tranche du jeu (M9)**, ajoutée au plan `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` de RacingGame, branche `chantier/rgce-worlds-as-assets` : le pointeur `CasaEngine` du jeu passe sur la tête de `chantier/entity-asset-remote` (commit sur la branche du chantier du jeu, non poussé) ; sur une copie du contenu, l'éditeur lancé avec `--remote` ouvre `Entities/PlayerCar.entity` par le CLI, modifie une propriété, la remet par une seconde modification, enregistre (`written: true`), et le fichier est comparé octet pour octet à celui de l'outil. Un écart est rapporté tel quel, avec ses octets ; il n'est ni masqué ni corrigé dans cette tranche. |

## Relecture de sécurité

Travail sensible : frontière de confiance du pilotage à distance (chemins, liens, types, écritures de fichiers). Une passe `security-reviewer` en lecture seule précède la relecture du plan ; ses constats et leur traitement sont écrits ici avant l'approbation.

Passe du 2026-11-19 (`security-reviewer`, lecture seule, sur le plan `1fe36a02e` et la tranche M9 du jeu) : **aucun P0 ni P1** ; S3-1 à S3-3 en P2, le reste en avis. Les faits qui portent S3-1, S3-4 et S3-6 ont été relus dans le code par la session principale (`EntityAssetEditorPanel.cs:271-274`, `GameEditor.cs:5648-5656` et `:5674-5677`, `EditorAssetWriterService.cs:62`, `GameEditor.Remote.cs:526`, `EditorAssetCatalogService.cs:45-55`).

| Réf | Priorité | Constat | Traitement |
|---|---|---|---|
| S3-1 | P2, introduit | Le contrôle « le panneau tient le fichier résolu » ne couvrait que `save`, et sa comparaison n'était pas écrite. Une copie non cataloguée d'un `.entity` (même id dans le fichier), ouverte à la main, prend l'id de panneau de l'asset catalogué : les éditions et la lecture distantes viseraient la copie, qu'un `project.save` écrirait. | **FIX** : P13, règle appliquée à toute la famille et à l'hôte ; tests en T1.1 et T1.2. Le même schéma existe pour `material.save`, `particle.save` et `sound.save` (hors périmètre) : résidu écrit dans l'ADR-0129. |
| S3-2 | P2, introduit | `history.undo` et `history.redo` atteignaient un panneau d'entité par son seul id, qui peut être celui d'un fichier non catalogué. | **FIX** : P13 (`TryGetAssetHistoryContext`) ; test en T1.1. |
| S3-3 | P2, introduit (procédure) | Jonction d'essai sans cible nommée, puis suppression récursive des copies ; copie du vérificateur non nommée. | **FIX** : P10. |
| S3-4 | P3, introduit (reprend le modèle de `CreateSoundAsset`) | Annulation de la création limitée à deux types d'exception ; une autre laisserait une entrée de catalogue sur un fichier vide. | **FIX** : P6, T1.3. Résidu écrit dans l'ADR : un arrêt de l'éditeur entre deux gestes peut laisser un fichier seul. Le modèle de `sound.create` et `particle.create` n'est pas modifié (hors périmètre). |
| S3-5 | P3, introduit | Refus absents des essais : `create` avec un dossier `..`, absolu ou lien ; `create` et `save` en Play ; `asset.open` d'un `.entity` hors catalogue ; `history.redo` sans panneau ; fichier `.entity` dont le contenu n'est pas une entité ; entrée de catalogue hors projet ou de type contraire à l'extension pour `entity_asset.*` ; empreinte de toute la copie avant et après la série de refus. | **FIX** : T1.2, T1.3, T2.2. |
| S3-6 | P4, code existant rendu atteignable | Un panneau d'entité enregistré sans onglet (échec de création de l'onglet) serait tenu pour ouvert, éditable et jamais fermable. | **FIX** : P13 (un panneau sans onglet n'est pas ouvert), T1.1. |
| S3-7 | P4, même classe de capacité | Ouvrir un `.entity` fait tourner du code du projet dans un monde d'aperçu ; `entity_asset.get` est lecture seule et n'était pas refusée en Play. | **FIX** : P14 ; phrase dans l'ADR et la documentation. |
| S3-8 | P4, résidus hérités | `entity_asset.save` et l'écriture après réservation suivent les liens (`File.CreateText`) ; liens physiques, course entre contrôle et usage, `project.save` sans ces contrôles. | **DEFER** (résidus acceptés par l'ADR-0093) : rappelés pour `.entity` dans l'ADR-0129. |
| S3-9 | P4, comme les autres panneaux | L'historique du panneau est partagé avec l'auteur (une annulation distante peut défaire son édition manuelle) ; fermer un panneau non modifié efface son historique. | **FIX** par la documentation (T2.1). |
| S3-10 | P4, M9 | Les empreintes de `%LOCALAPPDATA%/CasaEngine/` incluent `editor-remote/<pid>.json` (fichiers de découverte, avec jeton) : un éditeur tué par le délai y laisse un fichier, d'où un faux arrêt. | **FIX** dans M9 : dossier `editor-remote/` exclu de la comparaison, jamais copié. |

Contrôlé sans constat : résolution des références (contenance, liens, type, fichier absent, id inconnu) ; contenance de la création (nom, dossier, `CreateNew`) ; filtres et conversion des éditions, recherche du composant bornée à l'entité ; `panel.close` distant plus strict que la fermeture à la main (qui ne demande rien pour un panneau d'entité) et même nettoyage ; P4 sans régression (la règle de chemin A3 s'applique avant l'aiguillage) ; données rendues tronquées, chemin rendu = celui du catalogue ; le CLI n'affiche pas le jeton.

Non contrôlé par la passe, gardé comme risque (R3) : un éditeur de l'inspecteur qui modifierait l'entité sans entrée d'historique laisserait le panneau « non modifié », donc fermable par `panel.close` ; comportement de l'inspecteur existant, non relu.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/entity-asset-remote`**, créée depuis `main`. Ne jamais committer sur `main`. Dans le dépôt du jeu : `chantier/rgce-worlds-as-assets` seulement, jamais `master` ni `remaster`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`, écrit dans un fichier et passé par `git commit -F`, terminé par la ligne `Co-Authored-By` de la session ; commandes de commit commençant par `cd D:/development/repo/RacingGame/CasaEngine &&` (ou le dépôt du jeu pour T3.1).
- **Ne jamais pousser** (D4). Le merge sur `main` reste une décision de l'auteur.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**. Au plus 3 cycles de correction par tâche.
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build CasaEngine.Editor.MonoGame.sln`, 0 erreur ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (jamais `--no-build`), tout passe. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- **Sécurité** : le code des tâches T1.1 à T1.3 est écrit par le rôle `security-executor` sur le contrat de la tâche ; T1.2 et T1.3 sont relues chacune par un `plan-verifier` frais juste avant leur exécution, sur le code du moment (deux `REVISE` puis une relecture de clôture au plus, sinon ⚠️ et question à l'auteur) ; un `verifier` frais contrôle l'ensemble en T4.1.
- **Lancements** : un seul lancement de l'éditeur ou du jeu à la fois, jamais pendant un build ; rebuild avant de lancer ; l'éditeur et le CLI lancés du même côté (ils écrivent `%LOCALAPPDATA%` et `%TEMP%`).
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** `.serena/`, `.codegraph/`, `log.txt` : `git add` fichier par fichier, jamais `git add -A` ni `git add .`. Aucune commande git dans `MGUI/` ni `NvgSharp/` ; jamais `checkout`, `switch`, `reset` ni `git submodule update` dans le moteur vu comme sous-module du jeu.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : aucune exception d'une commande ne sort de `Update` ; rien d'alloué par image quand aucune commande n'arrive ; aucun changement de comportement des panneaux en usage manuel ; le runtime (`CasaEngine`) n'est pas touché.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe, nombre de tests = référence de T0.1 + les tests ajoutés, aucun sauté de plus.
- `Tools/automation-smokes.sh` : mêmes résultats avant (T0.1) et après (T1.3) pour S1 à S8 (P4).
- Parcours dans l'éditeur lancé avec `--remote` sur la copie de `Projects/SampleProject`, piloté par `casa-editor run` : créer, ouvrir, lire, ajouter un composant, modifier une propriété, annuler, rétablir, enregistrer, relire le fichier, fermer le panneau ; et les refus de T2.2, chacun avec sa réponse.
- Jeu : `PlayerCar.entity` enregistré par l'éditeur et comparé au fichier de l'outil (T3.1).
- Vérification indépendante : **CONFIRMED** (T4.1).

---

## Phase 0 — Décision et référence

### ⏳ T0.1 — ADR-0129 et référence avant modification

- Objectif : la décision est écrite, et l'état de départ est mesuré avant tout changement de code.
- Fichiers : `docs/decisions/0129-<titre>.md` (nouveau, statut `Proposed`, skill `adr`), `docs/decisions/README.md`, ce plan.
- Étapes :
  1. Écrire l'ADR-0129, qui étend l'ADR-0093 : la famille `entity_asset.*`, l'édition par le panneau et son historique, une entité par asset, la création d'un asset vide, la règle d'écriture à l'enregistrement, ce qui reste hors périmètre, et les constats de sécurité retenus : P13, P14, résidus de S3-1 (même schéma pour les matériaux, particules et sons), de S3-4 (arrêt entre deux gestes de la création) et de S3-8.
  2. Référence : `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, résultats notés (erreurs, nombre de tests) ; `Tools/automation-smokes.sh artifacts/entity-asset-remote/smokes-before` (sorties dans le dépôt du jeu, chemin absolu), codes de sortie et empreintes notés.
- Validation : build à 0 erreur ; tous les tests passent ; S1 à S8 rendent leur code de sortie ; l'ADR est dans l'index avec le statut `Proposed`.
- Commit : `docs(adr): record the entity asset remote commands`

## Phase 1 — Commandes

Les trois tâches sont écrites par `security-executor`, l'une après l'autre.

### ⏳ T1.1 — `asset.open`, panneaux et historique pour un panneau d'entité

- Objectif : un `.entity` s'ouvre par `asset.open` ; son panneau est listé, fermable s'il n'est pas modifié, et son historique est atteint par `history.undo` et `history.redo` (P3), sous les règles de P13 et P14 ; l'automatisation ne change pas (P4).
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteAssets.cs` (type `entity`, `IsOpenable`, message de refus), `CasaEngine.Editor/Remote/EditorRemoteEntityAssets.cs` (nouveau, P13), `CasaEngine.Editor/Remote/EditorRemoteAssetCommands.cs` (descriptions, messages, commentaires de `RemotePanelInfo` et `RemotePanelCloseResult`, refus du Play pour un `.entity`), `CasaEngine.Editor/Remote/EditorRemoteEditCommands.cs` (descriptions et message de `history.*`), `CasaEngine.Editor/GameEditor.Remote.cs` (`OpenAsset`, `TryGetAssetHistoryContext`, `TrySaveAsset`, `FindAssetPanelId`, `GetAssetPanelKind`), `CasaEngine.Editor/GameEditor.cs` (`TryOpenAutomationAsset` seulement, P4), `CasaEngine.Tests/Editor/Remote/RemoteTestHost.cs`, `CasaEngine.Tests/Editor/Remote/EditorRemoteAssetCommandsTests.cs`.
- Étapes :
  1. `EditorRemoteAssetTypes.Entity = "entity"` ; `IsOpenable` l'accepte ; le message de refus d'un type non ouvrable nomme les cinq types.
  2. `EditorRemoteEntityAssets.IsLoadedFile` (P13), fonction pure. Hôte : `OpenAsset` route `entity` vers `TryOpenEntityAsset`, retrouve un panneau d'entité déjà ouvert sans le recharger, et échoue si ce panneau tient un autre fichier ; `FindAssetPanelId` et `GetAssetPanelKind` connaissent `EditorPanelIds.EntityAssetDocumentPrefix` et `_entityAssetEditorPanels` (genre `entity`, modifié = `IsDirty` du panneau) et ne tiennent pour ouvert qu'un panneau qui a son onglet ; `TryGetAssetHistoryContext` rend le contexte `Entity` du panneau ouvert seulement si l'id est une entrée `.entity` du catalogue dont le fichier est celui que le panneau a chargé ; `asset.open` d'un `.entity` est refusé en Play (P14) ; `TrySaveAsset` enregistre par `TrySaveLoadedAsset`, puis marque l'historique enregistré et met le titre à jour comme `SaveDirtyEntityAssetEditors`.
  3. `TryOpenAutomationAsset` : prédicat propre à l'automatisation, limité à `material`, `particle`, `uiscreen`, `sound`.
  4. Tests : `asset.open` d'un `.entity` par id et par fichier (type `entity`, fichier résolu) ; refus inchangés (monde, autre type, chemin hors projet, entrée de catalogue hors projet, fichier absent, id inconnu) ; `Resolve` avec le type attendu `entity` refuse un `.material` et l'inverse ; `panel.close` d'un panneau `entity` non modifié réussit, modifié est refusé (`unsaved_changes`) ; `history.undo` et `history.redo` avec l'id d'un asset d'entité ouvert atteignent son contexte, avec un id sans panneau sont refusés (`not_found`) et ne créent aucune pile ; `IsLoadedFile` : chemin égal (casse différente comprise), autre fichier, chemin nul, absolu, avec `..` ; `asset.open` d'un `.entity` en Play refusé (`play_mode_active`), d'un `.material` en Play inchangé. Ce que seul l'hôte réel fait (panneau d'un autre fichier sous le même id, panneau sans onglet, id hors catalogue pour l'historique) est écrit dans `GameEditor.Remote.cs` sur la fonction testée, et essayé dans l'éditeur lancé en T2.2.
- Validation : build 0 erreur ; tests tous verts ; les refus de liens existants (`EditorRemotePathLinkTests`) passent sans changement.
- Commit : `feat(editor): open entity assets through the remote commands`

### ⏳ T1.2 — `entity_asset.get`, éditions et enregistrement

- Objectif : lire un asset `.entity`, modifier une propriété, ajouter et retirer un composant, enregistrer (P1, P2, P5, P7, P13, P14).
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteEntityAssetCommands.cs` (nouveau), `CasaEngine.Editor/Remote/EditorRemoteControl.cs` (membres d'hôte de P8 sauf la création, enregistrement de la famille), `CasaEngine.Editor/GameEditor.Remote.cs`, `CasaEngine.Tests/Editor/Remote/RemoteTestHost.cs`, `CasaEngine.Tests/Editor/Remote/EditorRemoteEntityAssetCommandsTests.cs` (nouveau).
- Étapes :
  1. Aide commune à toute la famille : refus du Play (P14) ; résoudre `asset` (type `entity`) ; ouvrir le panneau par `host.OpenAsset` ; contrôle de P13 sur `host.GetEntityAssetLoadedFile` ; prendre l'entité par `host.GetEntityAsset` et le contexte par `host.TryGetAssetHistoryContext` ; refus `failed` si l'un manque, avant toute lecture ou édition.
  2. `entity_asset.get` : forme de P1, par `EditorRemoteWorldModel.EnumerateComponents` et `DescribeComponent(includeProperties: true)`.
  3. `entity_asset.set_property`, `.add_component`, `.remove_component` : composant cherché par `EditorRemoteWorldModel.FindComponent` sur l'entité de l'asset ; mêmes contrôles et mêmes messages que `property.set`, `component.add`, `component.remove` ; `EditorWorldOperations` avec le contexte du panneau ; nom et sélection de P5.
  4. `entity_asset.save` : `written` = état modifié lu avant l'appel à `host.TrySaveAsset` ; échec de l'hôte → `failed`.
  5. Tests, sur une vraie `Entity` tenue par `RemoteTestHost` et le vrai `EditorHistoryService` : lecture (forme, propriétés éditables seulement) ; propriété modifiée, valeur inchangée (`changed: false`, aucune entrée), propriété non éditable, valeur hors bornes (NaN, texte trop long, énumération par nombre) ; composant ajouté en racine, en enfant, en liste, type hors de `component.types`, seconde racine refusée, nom unique donné ; composant retiré, composant à enfants refusé, composant d'une autre entité refusé (`not_found`) ; annulation et rétablissement de chaque édition par `history.undo` et `history.redo` avec `context` = l'id de l'asset ; chaque commande de la famille, `get` comprise, refusée en Play ; asset hors projet, sous un lien, d'un autre type, absent du catalogue, d'entrée de catalogue hors projet ou de type contraire à son extension : refusés avant tout appel à l'hôte ; P13, pour chaque commande de la famille : hôte dont le fichier chargé est un autre fichier, un chemin avec `..`, un chemin nul : refus `failed`, entité inchangée, aucune entrée d'historique ; `save` : `written` faux puis vrai selon l'état modifié, échec de l'hôte rapporté.
- Validation : build 0 erreur ; tests tous verts.
- Commit : `feat(editor): edit entity assets through the remote commands`

### ⏳ T1.3 — `entity_asset.create`

- Objectif : créer un asset `.entity` vide par son nom (P6).
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteEntityAssetCommands.cs`, `CasaEngine.Editor/Remote/EditorRemoteControl.cs` (`CreateEntityAsset`), `CasaEngine.Editor/GameEditor.Remote.cs`, `CasaEngine.Tests/Editor/Remote/RemoteTestHost.cs`, `CasaEngine.Tests/Editor/Remote/EditorRemoteEntityAssetCommandsTests.cs`.
- Étapes :
  1. Commande : refus du Play ; `EditorRemoteAssetCreation.Prepare(…, EditorRemoteAssetTypes.Entity)` ; `host.CreateEntityAsset` ; réponse `{asset_id, file}`.
  2. Hôte, au modèle de `CreateSoundAsset` : `Reserve`, entité au nom et au fichier donnés, entrée de catalogue, `EditorAssetWriterService.SaveAsset`, catalogue enregistré ; sur toute exception après la réservation (S3-4) : entrée retirée de la mémoire (`EditorAssetCatalogService.TryRemoveEntry`, qui ne touche ni fichier ni catalogue sur disque), fichier réservé supprimé, chaque geste gardé, exception journalisée, réponse `failed`.
  3. Tests : création acceptée (fichier `<dossier>/<nom>.entity` transmis à l'hôte) ; refus du nom invalide, du dossier absent, hors projet (`..`, chemin absolu) ou lien, du nom déjà au catalogue (tout type, sans casse), du fichier déjà présent ou déjà catalogué, du Play ; et, sans hôte : une entité vide écrite par `EditorAssetWriterService.SaveAsset` est acceptée par `EntityAssetEditorPanel.TryLoadAsset` (le fichier créé s'ouvrira).
  4. `Tools/automation-smokes.sh artifacts/entity-asset-remote/smokes-after` : comparé à la référence de T0.1.
- Validation : build 0 erreur ; tests tous verts ; S1 à S8 identiques à la référence (fichiers `sN-automation.txt`, `sN-hashes.txt`, `sN-exit.txt`).
- Commit : `feat(editor): create entity assets through the remote commands`

## Phase 2 — Documentation et éditeur lancé

### ⏳ T2.1 — Documentation

- Objectif : la documentation décrit les nouvelles commandes et leurs règles.
- Fichiers : `docs/editor/editor-remote-control.md` (introduction : ADR-0129 ; table des commandes : `asset.open`, `panel.close`, `history.*`, ligne `entity_asset.*` ; règles des assets : une entité par asset, historique du panneau, `written`, création vide ; limites : la phrase « Only materials, particles, UI screens and sounds are editable assets » ; ce que l'enregistrement ne fait pas : rafraîchir les mondes ouverts ; sécurité : ouvrir un `.entity` exécute du code du projet dans un monde d'aperçu, famille refusée en Play, historique partagé avec l'auteur, fermeture qui efface l'historique du panneau, résidus de S3-8), ce plan. L'ADR-0129 n'est pas réécrite ici : un point que l'exécution aurait changé est une question à l'auteur.
- Validation : chaque commande de `casa-editor commands` de la famille est dans la table ; aucune phrase ne dit plus que ce que les tests ou T2.2 prouvent.
- Commit : `docs(editor): document the entity asset remote commands`

### ⏳ T2.2 — Parcours dans l'éditeur lancé

- Objectif : le parcours complet et les refus tournent dans le vrai éditeur (règle `.claude/rules/editor-mgui.md`).
- Sources : `artifacts/entity-asset-remote/` du dépôt du jeu (scripts `*.json` du CLI, sorties, empreintes) ; copie `<scratchpad>/entity-asset-remote/sample/` (P10).
- Étapes :
  1. `dotnet build CasaEngine.Editor.MonoGame.sln`, puis copie neuve de `Projects/SampleProject` ; éditeur lancé depuis son dossier de build avec `--project <copie>/SampleProject.json --remote`, délai borné ; `casa-editor editors` puis `casa-editor commands` (les six commandes `entity_asset.*` listées).
  2. **Parcours**, par `casa-editor run <script>.json`, chaque réponse gardée : `entity_asset.create` (`RemoteProbe`, dossier `Entities`) ; `asset.open` ; `panel.list` (genre `entity`, non modifié) ; `entity_asset.get` (aucun composant) ; `entity_asset.add_component` (un `SceneComponent` en racine, puis un composant enfant) ; `entity_asset.set_property` ; `entity_asset.get` (valeur lue, `modified: true`) ; `history.undo` puis `entity_asset.get` (valeur d'avant) ; `history.redo` puis `entity_asset.get` (valeur posée) ; `entity_asset.save` (`written: true`) ; lecture du fichier sur disque (le composant et la valeur y sont) ; second `entity_asset.save` (`written: false`, empreinte et date du fichier inchangées) ; `panel.close` (`closed: true`) ; `asset.open` de nouveau puis `entity_asset.get` (mêmes composants et valeurs que le fichier).
  3. Sur un asset existant de la copie (`Entities/Box.entity`) : `entity_asset.get`, une propriété modifiée, `panel.close` refusé (`unsaved_changes`, empreinte du fichier inchangée), `history.undo`, `panel.close` accepté.
  4. **Refus**, chacun avec sa réponse : `entity_asset.get` d'un `.material` et `material.get` d'un `.entity` ; `entity_asset.get` de `../outside.entity` ; d'un `.entity` présent sur disque mais absent du catalogue ; d'un `.entity` sous une jonction créée dans la copie (ou la raison écrite si la jonction ne peut pas être créée) ; `entity_asset.set_property` d'une propriété non éditable et d'une valeur NaN ; `entity_asset.add_component` d'un type hors de `component.types` ; `entity_asset.remove_component` avec le composant d'une entité du monde ; `history.undo` avec l'id d'un `.entity` non ouvert ; `entity_asset.create` sous un nom déjà au catalogue et sur un fichier existant ; `entity_asset.create` avec un dossier `..`, un dossier absolu et un dossier sous la jonction ; `asset.open` d'un `.entity` absent du catalogue ; `history.redo` avec l'id d'un `.entity` non ouvert ; `entity_asset.get` d'un `.entity` catalogué dont le contenu est celui d'un monde (`failed`, et `panel.list` ne montre aucun panneau de plus) ; P13 : une copie de `Entities/Box.entity` sous un autre nom, non cataloguée, ouverte par l'option `--open-asset` d'un lancement à part, puis `entity_asset.get`, `entity_asset.set_property` et `history.undo` sur `Entities/Box.entity` (refusés, la copie et l'asset inchangés) ; pendant le Play (`play.start`, puis `entity_asset.get`, `.set_property`, `.create`, `.save` et `asset.open` d'un `.entity`, puis `play.stop`) si le projet de la copie démarre son Play, sinon la raison écrite. Empreinte de chaque fichier de la copie relevée avant et après la série de refus : identiques (aucune commande refusée n'a écrit). La jonction est créée et retirée selon P10.
  5. `logs.read` : aucun `[Error]` dû aux commandes ; capture `capture.window` du panneau ouvert, gardée ; éditeur arrêté sans `project.save` ; aucun fichier de `Projects/SampleProject` du dépôt modifié (`git status`).
- Validation : toutes les réponses attendues sont obtenues ; un écart est un constat écrit dans la note, corrigé dans la tâche qui l'a introduit (au plus 3 cycles) ou remonté en ⚠️.
- Commit : `docs(ai-agent): record the launched-editor run of the entity asset commands`

## Phase 3 — Jeu

### ⏳ T3.1 — Tranche M9 du jeu : `PlayerCar.entity` enregistré par l'éditeur (lève O2)

- Objectif : P12. Le détail vit dans la tranche M9 de `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` (dépôt RacingGame), relue par un `plan-verifier` frais juste avant son exécution, comme le veut ce plan-là.
- Fichiers (dépôt du jeu, branche `chantier/rgce-worlds-as-assets`) : pointeur `CasaEngine`, `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` ; ici, ce plan.
- Validation : celle de M9 ; résultat reporté ici en une note (identique octet pour octet, ou l'écart).
- Commit (moteur) : `docs(ai-agent): record the game slice of the entity asset remote commands` ; commits du jeu : ceux de M9.

## Phase 4 — Vérification et clôture

### ⏳ T4.1 — Vérification indépendante, clôture

- Objectif : un `verifier` frais contrôle l'ensemble ; les constats sont traités ; les copies sont supprimées ; le rapport de fin est rendu.
- Étapes :
  1. `verifier` frais, sur la différence `1ed32d862..HEAD` et ce plan : build et tests rejoués ; parcours de T2.2 rejoué sur une copie neuve ; refus de sécurité essayés par lui-même (chemins, liens, types, catalogue, panneau non ouvert, panneau modifié, Play) ; contrôle qu'aucun contrat de commande existante n'a changé (schémas de `commands.list` avant et après) et que les smokes d'automatisation sont identiques.
  2. Traitement de chaque constat (`FIX`, `DEFER`, `REJECT`), selon sa priorité ; un correctif requis relance l'acceptation et une vérification.
  3. Suppression des copies de P10 ; `ai-agent/README.md` mis à jour ; rapport de fin (`AGENTS.md` §12) : ce qui est fait, ce qui n'est pas vérifié, ce qui revient à l'auteur (ADR-0129 à valider, fusion dans `main`, push, archivage du plan).
- Validation : verdict **CONFIRMED** ; `git status` propre dans les deux dépôts hors `.serena/` et `log.txt`.
- Commit : `docs(ai-agent): record the check of the entity asset remote commands`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P14 avant l'exécution ; en particulier P3 (un panneau d'entité devient listé `entity` et fermable par `panel.close`), P5 (nom et sélection du composant ajouté), P6 (asset créé vide), P12 (pointeur du jeu sur une branche non fusionnée du moteur) et P14 (toute la famille, lecture comprise, refusée en Play). | Approbation |
| O2 | Un asset vide (sans racine) s'affiche-t-il sans défaut dans le panneau d'aperçu ? `WorldViewportPanel.FocusEntity` sort sans rien faire quand la racine manque (`WorldViewportPanel.cs:788-796`) ; le reste se constate en T2.2. Un défaut du panneau serait un constat à rapporter, pas un changement de périmètre décidé par l'agent. | T2.2 |
| O3 | Le fichier de `PlayerCar.entity` enregistré par l'éditeur est-il identique à celui de l'outil ? Inconnu avant T3.1 : le panneau initialise l'entité dans un monde d'aperçu avant de l'écrire, ce que l'outil ne fait pas. | T3.1 |
| R1 | Risque : `IsOpenable` sert aussi à l'automatisation ; atténuation : P4 et smokes rejoués avant et après. | T1.1, T1.3 |
| R2 | Risque : le panneau réel n'a pas de test unitaire ; atténuation : parcours de T2.2 et vérification de T4.1 sur l'éditeur lancé. | T2.2, T4.1 |
| R3 | Risque non contrôlé (relecture de sécurité) : une édition faite à la main dans l'inspecteur sans entrée d'historique ne marquerait pas le panneau modifié ; `panel.close` le fermerait. La fermeture à la main ne demande déjà rien. Rapporté, non traité. | — |

## Hors périmètre

- Entités enfants d'un asset (le format n'en enregistre pas), renommage de l'entité ou de l'asset, suppression d'un asset, script de l'entité (`script_class_name`) et politiques de l'entité.
- Rafraîchissement des mondes ouverts qui référencent l'asset après son enregistrement (`OnEditorAssetSaved` sans branche `.entity`) : manque relevé, à traiter à part.
- Nom du composant ajouté par `component.add` dans un monde : inchangé (D2).
- Les autres types d'assets (sprites, animations, TileMaps, mixeurs audio, modèles) ; le docking.
- Résidus acceptés de l'ADR-0093 (liens physiques, course entre contrôle et usage, écritures de `project.save`) : inchangés.
- Acceptation de l'ADR-0129, fusion dans `main`, push, archivage de ce plan : décisions de l'auteur.
