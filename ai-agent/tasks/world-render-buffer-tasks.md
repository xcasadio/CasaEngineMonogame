# Plan agent IA — Résolution de conception par monde et rendu dans un buffer

> **Renumérotation (2026-11-08).** À l'intégration d'`origin/main`, dont l'ADR-0086 enregistre l'image figée, l'ADR de ce plan est devenue ADR-0087 (avant ADR-0086). Les références de ce plan sont à jour ; les notes qui parlent d'un numéro libre gardent celui de leur date.

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-08 : chaque écran est rendu dans un buffer à sa taille de conception, puis affiché dans la fenêtre, qui ne change pas de taille.
Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-11-08 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- Un monde peut déclarer, dans son `.world`, sa propre résolution de conception et son mode d'agrandissement. Sinon, la `VirtualResolution` du projet s'applique, comme aujourd'hui.
- Deux nouveaux modes rendent le monde et ses écrans MGUI dans une cible de rendu à la taille de conception, puis la présentent dans la fenêtre :
  - au facteur entier, échantillonnée au plus proche, avec des bandes noires ;
  - agrandie librement en gardant les proportions, lissée, avec des bandes noires.
- Le mode actuel `IntegerFit` (dessin direct, ADR-0048) ne change pas : un projet qui ne déclare rien garde exactement son rendu.
- La fenêtre ne change jamais de taille au changement de monde.

Le chantier ne livre pas de mode Fill ou Stretch, de couleur de bandes réglable, ni d'édition du nouveau champ dans l'inspecteur de l'éditeur (voir « Hors périmètre »).

## État vérifié du dépôt (2026-11-08)

Lu sur `main` à `7ac0133e`, en lecture seule, par une découverte en quatre parties dont chaque affirmation a été recontrôlée.

- **Une seule résolution par projet.** `ProjectSettings.VirtualResolution` (`CasaEngine/Framework/Configuration/Project/ProjectSettings.cs:94`). `VirtualResolutionMode` n'a qu'un membre, `IntegerFit` (`VirtualResolutionSettings.cs:4-8`). `ProjectSettingsHelper.ReadVirtualResolution` refuse tout autre mode (`ProjectSettingsHelper.cs:72-96`, message `:91-92`) et écrit le bloc seulement s'il existe (`:184-199`).
- **Dessin direct.** `VirtualResolutionLayout.Compute` calcule un facteur entier k (`VirtualResolutionLayout.cs:38-56`). `Apply` met le `ViewportRect` d'un `BackBufferSurface` sur l'image, redimensionne la caméra et fixe `Camera2dComponent.Zoom = k` (`:64-85`). `BackBufferSurface.Apply` ne change que le viewport (`BackBufferSurface.cs:19-39`). `CasaEngineGame.ActiveVirtualResolution` vaut la valeur du projet, ou null sous gestion externe des vues, c'est-à-dire dans l'éditeur (`CasaEngineGame.cs:86-91`).
- **Bandes.** `VirtualResolutionRuntime.ShouldClearBands` n'est vrai que pour une vue unique de type `BackBufferSurface` (`VirtualResolutionRuntime.cs:57-64`). Le tampon arrière est alors vidé en noir à chaque image (`CasaEngineGame.cs:747-760`).
- **Pipeline.** `RenderPipeline.Render` traite les vues sur cible de rendu avant celles sur tampon arrière, puis appelle `view.Presenter` après chaque vue (`RenderPipeline.cs:101-311`, `:286-288`). `ApplyResolutionScale` redimensionne une `RenderTargetSurface` qui a un `Host` (`:350-375`). Le détour `_sceneRt` des ombres ne sert qu'aux vues sur tampon arrière (`:139-153`).
- **Briques existantes, inutilisées.**
  - `RenderTargetSurface` : cible de rendu, `ViewportRect` à (0,0,largeur,hauteur) (`RenderTargetSurface.cs:22-120`, `:38-40`).
  - `BackBufferPresenter` : jamais construit. Il échantillonne en `LinearClamp`, fige son rectangle à la construction, laisse sa branche de bandes vide et n'a pas de facteur entier (`BackBufferPresenter.cs:9-101`).
- **Changement de monde.** `GameManager.UpdateWorld` vide les vues, charge le monde, puis `RuntimeViewBootstrapper.BootstrapViews` appelle `CreateDefaultView` (`GameManager.cs:134-153`, `DefaultRuntimeViewBootstrapper.cs:25-74`). C'est le point d'entrée naturel d'une taille par monde. `CasaEngineGame.OnScreenResized` ne règle qu'une vue unique de type `BackBufferSurface`. Sinon, il donne à la caméra active la taille de la fenêtre (`CasaEngineGame.cs:293-354`).
- **UI.** MGUI dessine dans la cible liée. Ses bornes viennent du `ViewportRect` de la surface de la vue (`ViewRenderHost.GetBounds`, `CasaRenderSurfaceAdapter.GetBounds`). Une vue sur cible de rendu de taille de conception lui donne donc ces bornes sans changer MGUI. `DataDrivenScreen.ApplyDesignScale` calcule k sur ces bornes (`DataDrivenScreen.cs:602-636`) : à la taille de conception, k vaut 1. `GameManager.DesignResolutionOf` lit le réglage du projet (`GameManager.cs:218-224`, `:241`, `:259-264`).
- **Entrée.** La souris de l'UI vient de `ViewRenderHost` et celle du gameplay d'`InputRouter.CreateLocalMouseState`. Les deux soustraient l'origine de la vue sans diviser par un facteur (`InputRouter.cs:246-265`). `ViewMouseViewport.IsInside` compare à `ViewportRect` (`ViewMouseViewport.cs:28-33`).
- **Effets d'écran.** `ScreenEffectComponent` dimensionne le quad du fondu d'après le viewport et le zoom de la caméra. À défaut, il prend la taille de la fenêtre, et il lit le ciseau du périphérique (`ScreenEffectComponent.cs:71-88`, `:152-158`, `:172-201`). `UiDeviceScissor.ResetToBackBuffer` règle le ciseau à la taille du tampon arrière à chaque `ClientSizeChanged`.
- **Format `.world`.**
  - `World.Load` lit des clés en snake_case, ignore les clés inconnues et lit les champs facultatifs avec `ContainsKey` ou `?.` (`World.cs:988-1025`).
  - Le seul écrivain est `EditorEntityJsonSerializer.SaveWorld` (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:21-48`), appelé par l'éditeur et par l'instantané du mode jeu. Ce dernier relit le monde avec `World.Load` (`PlayMode/EditorWorldPlaySnapshot.cs:14-35`).
- **Tests existants.**
  - Tests purs, sans GPU : `VirtualResolutionLayoutTests`, `VirtualResolutionRuntimeTests`, `ProjectSettingsVirtualResolutionTests` (le cas de mode invalide utilise « Stretch », `:242`), `FirstScreenTests`, `ScreenActionContextWiringTests`, `DataDrivenScreenTests` (échelle et clics sous échelle, `:1938-2060`), `ScreenEffectComponentViewSizeTests`, `Camera2dComponentTests`.
  - Tests GPU : `UiClipViewSpaceGpuTests` relit des pixels d'une cible avec `GpuDeviceHost` et `[GpuFact]` (`CasaEngine.Tests/UI/Backend/GpuDeviceHost.cs`).
- **Sample.** `Projects/PixelUiSample` : 320x180 `IntegerFit` avec `ScaleUI`, trois mondes, lancé par `dotnet run --project CasaEngine.Launcher -- Projects/PixelUiSample/PixelUiSample.json`. `ProjectScreenAssetTests` le lit et vérifie `Window.Scale == 3` à 960x540 (`:160-217`).
- **ADR.** ADR-0048 a choisi le dessin direct, sans cible intermédiaire ni `BackBufferPresenter` (`:44-45`). Elle a reporté le mode non entier et le branchement du présentateur (`:59-60`). ADR-0079 : une seule résolution de conception par projet (`:44`). ADR-0068 : la règle des quads libres est définie au pixel d'écran, au facteur k. ADR-0070 refuse une résolution virtuelle avec des marges de layout. Le prochain numéro libre est 0086.
- **Arbre de travail.** Branche `chantier/world-render-buffer` créée depuis `main` (`7ac0133e`), égal au pointeur du projet consommateur. Aucun changement préexistant.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Les deux chemins coexistent, comme les modes « canvas_items » et « viewport » de Godot. `IntegerFit` reste le dessin direct actuel, inchangé. Deux modes s'ajoutent, qui rendent dans une cible de rendu à la taille de conception : `BufferedIntegerFit` et `BufferedFit` |
| D2 | `BufferedIntegerFit` présente la cible au facteur entier k de `Compute`, échantillonnée au plus proche (`PointClamp`). `BufferedFit` la présente au plus grand facteur non entier qui tient dans la fenêtre en gardant les proportions, lissée (`LinearClamp`). Les deux remplissent les bandes en noir |
| D3 | Un monde déclare sa résolution dans un bloc facultatif `virtual_resolution` de son `.world` : `width`, `height` et `mode`. Sans bloc, la `VirtualResolution` du projet s'applique. Le `Mode` du projet accepte aussi les deux nouveaux modes |
| D4 | La résolution effective se fixe au chargement de chaque monde, quand les vues sont reconstruites. La fenêtre ne change pas de taille |
| D5 | Le format change de façon additive : un ancien `.world` ou projet se charge et se rend à l'identique. ADR-0087 enregistre la décision |
| D6 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/world-render-buffer`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur. **Tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, jamais avec `--no-build`. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds : la présentation et la conversion de la souris tournent à chaque image ;
  - restaurer tout état GPU modifié (cible, viewport, ciseau, échantillonneur, mélange) ;
  - le runtime ne dépend pas de l'éditeur ;
  - sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression** : un projet qui ne déclare aucun mode buffer se rend au pixel près comme avant. Le test GPU de T3.1 le prouve sur une scène de test, et `ProjectScreenAssetTests` reste vert sur `Projects/PixelUiSample`.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/PixelUiSample/PixelUiSample.json`, à 960x540 puis à une taille non multiple.
  - Le monde de départ garde son rendu.
  - Un monde en `BufferedIntegerFit` montre des pixels nets au facteur entier, avec des bandes noires.
  - Un monde en `BufferedFit` remplit la hauteur, lissé.
  - La fenêtre garde sa taille.
  - Un clic ou un focus clavier vise le bon bouton dans les trois mondes.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0087 : résolution de conception par monde et présentation d'un buffer

- Objectif : enregistrer D1 à D5 avant le code.
- Fichiers :
  - `docs/decisions/0087-a-world-declares-its-design-resolution-and-may-render-into-a-buffer.md` ;
  - `docs/decisions/README.md` ;
  - lignes de statut d'ADR-0048 et d'ADR-0079.
- Étapes :
  1. Revérifier que 0086 est libre dans `docs/decisions/` et dans l'index.
  2. Écrire l'ADR d'après `docs/decisions/template.md`.
     - Contexte : ADR-0048 et ADR-0079.
     - Décision : D1 à D5.
     - Conséquences :
       - ADR-0048 reste valable pour `IntegerFit`. Son report « mode non entier » et « branchement du présentateur » est levé pour les seuls modes buffer.
       - ADR-0079 : une résolution de conception par monde.
       - ADR-0068 : en mode buffer, les quads libres sont dessinés au pixel de conception, puis agrandis.
       - ADR-0070 : une vue avec marges de layout reste refusée avec une résolution virtuelle, buffer compris.
  3. Mettre à jour l'index et les lignes de statut d'ADR-0048 et d'ADR-0079 (« amended by ADR-0087 »).
- Validation : relecture. L'ADR ne nomme aucun projet consommateur (D6). Ses liens relatifs s'ouvrent.
- Commit : `docs(decisions): record per-world design resolution and buffered presentation`
- Note de validation (2026-11-08) : 0086 libre dans le dossier et l'index. ADR écrite d'après le modèle, index et statuts d'ADR-0048 et d'ADR-0079 mis à jour. Aucun nom de projet consommateur ni nombre tiré de lui (D6). Arrondi du mode libre fixé dans l'ADR : taille entière par le bas, centrage comme `Compute` (O3).

## Phase 1 — Réglages et format

### ✅ T1.1 — Deux nouveaux modes dans `VirtualResolutionMode`

- Objectif : `BufferedIntegerFit` et `BufferedFit` sont lus et écrits dans la `VirtualResolution` du projet.
- Fichiers :
  - `CasaEngine/Framework/Configuration/Project/VirtualResolutionSettings.cs` ;
  - `ProjectSettingsHelper.cs` ;
  - `CasaEngine.Tests/Configuration/ProjectSettingsVirtualResolutionTests.cs`.
- Étapes :
  1. Ajouter les deux membres à l'enum, après `IntegerFit`, pour garder sa valeur.
  2. `ReadVirtualResolution` les accepte. Le message d'erreur liste les trois modes.
  3. Tests :
     - aller-retour de chaque mode ;
     - « Stretch » toujours refusé ;
     - un ancien projet sans `Mode` lu en `IntegerFit`.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert.
- Commit : `feat(rendering): add buffered virtual resolution modes`
- Note de validation (2026-11-08) : enum à trois membres, `IntegerFit` garde sa valeur, extension `IsBuffered`. Message d'erreur qui liste les trois modes. 7 tests ajoutés (aller-retour de chaque mode, `IsBuffered`, mode inconnu). Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4842/4842.

### ✅ T1.2 — Bloc `virtual_resolution` du `.world`

- Objectif : un monde déclare, de façon facultative, sa largeur, sa hauteur et son mode. Le bloc est lu au runtime, écrit par l'éditeur et gardé par l'instantané du mode jeu.
- Fichiers :
  - `CasaEngine/Framework/Scene/World/World.cs` (propriété et `Load`) ;
  - `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (`SaveWorld`) ;
  - tests de monde et d'instantané (`EditorWorldPlaySnapshotTests`, nouveau `WorldVirtualResolutionTests`).
- Étapes :
  1. Propriété facultative du monde, de type `VirtualResolutionSettings`. `ScaleUI` reste un réglage du projet seulement et n'est pas lu dans le monde.
  2. `World.Load` lit `virtual_resolution` s'il est présent.
     - `width` et `height` doivent être des entiers positifs.
     - `mode` est l'un des trois modes, et vaut `IntegerFit` par défaut.
     - Une erreur nomme le fichier du monde et le champ fautif.
  3. `SaveWorld` écrit le bloc seulement s'il est déclaré.
  4. Tests :
     - aller-retour ;
     - absence : rien d'écrit, propriété nulle ;
     - valeurs invalides refusées avec contexte ;
     - instantané du mode jeu qui garde le bloc.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert.
- Commit : `feat(world): read and write an optional per-world virtual resolution`
- Note de validation (2026-11-08) : propriété `World.VirtualResolution`, lecture dans `WorldVirtualResolutionReader` (runtime), écriture dans `SaveWorld` (éditeur) seulement si déclarée. Le chargeur d'assets ajoute déjà le nom du fichier au journal (`AssetLoader.cs:18-21`) ; le message nomme le monde et la clé. 16 tests (`WorldVirtualResolutionTests`, instantané du mode jeu compris). Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4858/4858.

## Phase 2 — Runtime

### ✅ T2.1 — Résolution effective par monde

- Objectif : au chargement d'un monde, la résolution effective est celle du monde, sinon celle du projet. Elle sert à la vue, à l'UI et aux bandes.
- Fichiers :
  - `CasaEngine/Framework/Application/CasaEngineGame.cs` (`ActiveVirtualResolution`) ;
  - `GameManager.cs` (`UpdateWorld`, `DesignResolutionOf`, `ReadScreenGameServices`) ;
  - tests `VirtualResolutionRuntimeTests`, `FirstScreenTests`, `ScreenActionContextWiringTests`.
- Étapes :
  1. Fixer la résolution effective dans `UpdateWorld`, avant `BootstrapViews`. Sous gestion externe des vues (éditeur), elle reste nulle. Règle de composition :
     - **le monde n'a pas de bloc** : la `VirtualResolution` du projet, telle quelle, ou nulle ;
     - **le monde a un bloc** : la largeur, la hauteur et le mode du monde, avec le `ScaleUI` du projet, ou faux si le projet n'a pas de `VirtualResolution`.
  2. `DesignResolutionOf` et les services d'écran lisent la résolution effective. En mode buffer, un écran reçoit les bornes de la cible et son échelle vaut 1, que `ScaleUI` soit vrai ou faux (T2.3).
  3. Tests :
     - un monde sans bloc reprend le projet ;
     - un monde avec bloc l'emporte ;
     - un monde en `IntegerFit` avec bloc, dans un projet en `ScaleUI: true`, donne à `DesignResolutionOf` la taille du monde ;
     - un monde avec bloc, dans un projet sans `VirtualResolution`, a `ScaleUI` faux, et `DesignResolutionOf` vaut null ;
     - le passage d'un monde à l'autre change la résolution effective ;
     - l'éditeur reste à null.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert. Les tests existants des réglages restent verts sans changement de sens.
- Commit : `feat(rendering): resolve the virtual resolution per loaded world`
- Note de validation (2026-11-08) : règle de composition dans `WorldVirtualResolutionState`, testable sans GPU, calculée une fois par chargement de monde (aucune allocation par image). `CasaEngineGame.ActiveVirtualResolution` la lit, et `GameManager.UpdateWorld` l'alimente avant `ViewManager.Clear` et `BootstrapViews`. Un monde en `IntegerFit` avec bloc passe donc déjà par le dessin direct à sa propre taille. Les modes buffer arrivent en T2.2. 9 tests. Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4867/4867.

### ✅ T2.2 — Vue sur buffer et présentation

- Objectif : en mode buffer, la vue par défaut rend dans une cible de rendu à la taille de conception, puis un présentateur la dessine dans la fenêtre selon D2.
- Fichiers :
  - `CasaEngine/Framework/Rendering/VirtualResolutionLayout.cs` (calcul pur du rectangle présenté) ;
  - un présentateur, soit `BackBufferPresenter.cs` corrigé, soit un nouveau `VirtualResolutionPresenter.cs` (voir O2) ;
  - `DefaultRuntimeViewBootstrapper.cs`, `CasaEngineGame.cs` (`OnScreenResized`), `VirtualResolutionRuntime.cs` ;
  - `ViewManager.cs` ou l'abonné de `ViewRemoved` dans `GameManager.cs` (libération de la cible) ;
  - tests `VirtualResolutionLayoutTests`, `VirtualResolutionRuntimeTests`, et un test de changement de monde.
- Étapes :
  1. Calcul pur `ComputePresentation(window, design, mode)` :
     - entier : image de `k*w` par `k*h` avec le k de `Compute` ;
     - libre : facteur `min(W/w, H/h)`, en réel ;
     - dans les deux cas, image centrée, avec arrondi documenté.
  2. `CreateDefaultView`, en mode buffer :
     - une `RenderTargetSurface` de la taille de conception, sans `Host`, pour que `ApplyResolutionScale` ne la redimensionne pas ;
     - une caméra au viewport de conception, avec `Zoom = 1` ;
     - le présentateur de la vue.
  3. Présentateur :
     - il vide le tampon arrière en noir et dessine la cible dans le rectangle de l'étape 1, en `PointClamp` (entier) ou `LinearClamp` (libre), avec `BlendState.Opaque` ;
     - il restaure l'état GPU et n'alloue rien par image ;
     - le rectangle est recalculé à chaque `ClientSizeChanged`, pas à chaque image.
  4. **Redimensionnement.** `OnScreenResized` appelle d'abord `World.OnScreenResized`, qui met toutes les caméras à la taille de la fenêtre (`CasaEngineGame.cs:308`). Ensuite, en mode buffer :
     - la caméra de la vue buffer est remise au viewport de conception avec `Zoom = 1`, comme `VirtualResolutionLayout.Apply` le fait pour `IntegerFit` ;
     - la taille de la cible ne change pas ;
     - le présentateur recalcule son rectangle.

     `ShouldClearBands` reste faux pour la vue buffer, puisque le présentateur remplit les bandes.
  5. **Propriété de la cible.** La `RenderTargetSurface` que `CreateDefaultView` crée appartient à cette vue par défaut. Elle est libérée quand la vue est retirée : `ViewManager.Clear` au changement de monde (`GameManager.cs:136-147`), retrait de la vue, sortie du jeu. Aujourd'hui, `ViewManager.Clear` ne libère aucune surface (`ViewManager.cs:243-256`). La libération se limite donc aux surfaces que le bootstrap a créées et marquées comme lui appartenant : les vues existantes ne changent pas de comportement.
  6. Tests :
     - rectangles des deux modes, avec des tailles exactes et non multiples ;
     - vue buffer créée à la taille de conception, avec un zoom de 1 ;
     - après le chemin de redimensionnement complet, `World.OnScreenResized` compris : caméra au viewport de conception, `Zoom` à 1, taille de la cible inchangée ;
     - après un changement de monde, la cible de l'ancienne vue buffer est libérée et une seule cible reste vivante ;
     - `IntegerFit` identique à avant.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert.
- Commit : `feat(rendering): render buffered worlds into a design-size target and present it`
- Note de validation (2026-11-08) :
  - O2 tranché : présentateur dédié `VirtualResolutionPresenter`, qui possède la cible et son `SpriteBatch` (créé à la première image). `BackBufferPresenter` reste intact (API publique).
  - Calcul pur `VirtualResolutionLayout.ComputePresentation`, non rogné par la fenêtre.
  - La présentation n'alloue rien par image et remet viewport, mélange, profondeur, raster et échantillonneur 0.
  - `CreateDefaultView` crée la vue buffer quand le mode l'est. `OnScreenResized` passe par `TryResizeBufferedView` après `World.OnScreenResized`. `OnViewRemovedDisposeUIRuntime` libère la cible avec `ReleaseOwnedPresentation`.
  - 21 tests, dont 3 sur GPU, réellement lancés ici. Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4888/4888.
  - Limite connue : les composants de la phase 3 de `CasaEngineGame.Draw` (`Line3d`, `PhysicsDebug`, `Axis`) dessinent après le pipeline, en coordonnées de fenêtre, donc par-dessus l'image présentée et non dans le buffer.
  - O5 (ombres) reste ouvert : aucun monde avec ombres n'est testé ici.

### ✅ T2.3 — UI et fondu dans le buffer

- Objectif : les écrans MGUI et le fondu d'écran se dessinent dans la cible, à l'échelle 1, sans que la taille du tampon arrière n'intervienne.
- Fichiers :
  - `CasaEngine/Framework/UI/DataDrivenScreen.cs` (vérifié, changé seulement si nécessaire) ;
  - `CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs` ;
  - `CasaEngine/Framework/UI/Backend/MonoGame/UiDeviceScissor.cs` et son appel dans `CasaEngineGame.cs` ;
  - tests `DataDrivenScreenTests`, `ScreenEffectComponentViewSizeTests`.
- Étapes :
  1. Vérifier par un test qu'en mode buffer, `ApplyDesignScale` donne `Window.Scale = 1` et place `Left`/`Top` aux positions de conception.
  2. Le quad du fondu couvre la cible, sans repli sur la taille de la fenêtre. Le ciseau lu ou remis à zéro est celui de la cible, pas du tampon arrière.
  3. Tests : taille du quad en mode buffer, et échelle des écrans à 1.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert.
- Commit : `fix(ui): draw screens and screen fades at design scale inside the buffer`
- Note de validation (2026-11-08) :
  - Mesuré sur le GPU de test : lier une `RenderTargetSurface` met le ciseau du périphérique aux bornes de la cible, et la délier rend celui du tampon arrière. Le test `BufferedScissorProbeGpuTests` le garde. L'UI et le fondu au-dessus de l'UI, dessinés pendant la composition de la vue, sont donc bornés par la cible sans changement. `UiDeviceScissor` reste tel quel.
  - Seul le fondu sous l'UI lisait le ciseau pendant `Update` : `ScreenEffectComponent.ResolveOverlayScissor` prend désormais les bornes de la cible pour une vue sur cible.
  - `DataDrivenScreen` est inchangé : avec les bornes de la cible, son échelle vaut 1 (test ajouté).
  - 6 tests. Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4894/4894.

### ✅ T2.4 — Souris dans le buffer

- Objectif : un pointeur placé dans l'image présentée trouve la vue buffer et vise le bon pixel de conception, dans les deux modes buffer.
- Fichiers :
  - `CasaEngine/Framework/Rendering/ViewManager.cs` (`ScreenToView`, `GetScreenBounds`) ;
  - `CasaEngine/Framework/UI/ViewRenderHost.cs` (`GetMouseState`) ;
  - `CasaEngine/Framework/Input/InputRouter.cs` (`ResolvePointerView`, `CreateLocalMouseState`, `GetScreenBounds`) ;
  - `CasaEngine/Framework/UI/ViewMouseViewport.cs` ;
  - tests de clic sous échelle, dans `DataDrivenScreenTests` ou un nouveau fichier, et tests de `ViewManager`.
- Étapes :
  1. **Une seule source.** Le rectangle présenté, en pixels de fenêtre, est porté par la vue buffer : par son présentateur ou par une propriété de la vue, choix fait au code (O2). Le présentateur le met à jour à chaque redimensionnement. `ViewManager`, `InputRouter` et `ViewRenderHost` lisent tous cette même valeur. La vue n'a pas de `Host`, pour que `ApplyResolutionScale` ne redimensionne pas la cible (T2.2).
  2. **Choix de la vue.** `ViewManager.GetScreenBounds` renvoie le rectangle présenté pour une vue buffer, au lieu du `ViewportRect` de la cible (0,0,w,h). `ScreenToView` et `ResolvePointerView` trouvent donc la vue dans toute l'image présentée.
  3. **Position locale.** Le calcul pur `(fenêtre - origine) * conception / taille présentée` est testé seul. Il est appliqué à `ScreenToView`, à l'UI et au gameplay, sans allocation. `IntegerFit` garde sa conversion actuelle.
  4. Tests :
     - en `BufferedIntegerFit` à k=3, un point de la fenêtre dans l'image mais hors de (0,0,w,h) est attribué à la vue buffer par `ScreenToView` et `ResolvePointerView`, avec la bonne position de conception ;
     - un point dans une bande n'est attribué à aucune vue ;
     - un clic sur un bouton en `BufferedIntegerFit` à k=3 et en `BufferedFit` à 1,5 vise ce bouton ;
     - `IntegerFit` inchangé.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert.
- Commit : `feat(input): map the window mouse into the presented buffer`
- Note de validation (2026-11-08) :
  - Une seule source : `VirtualResolutionPresenter.PresentedRect`, avec `WindowToVirtual` (exacte) et `WindowToVirtualPixel` (arrondie vers le bas).
  - `ViewManager.GetScreenBounds` et `ScreenToView`, `InputRouter.GetScreenBounds` et `CreateLocalMouseState`, et `ViewRenderHost.GetMouseState` la lisent tous.
  - `ViewInputContext` garde la position de fenêtre réelle pour une vue buffer. `ViewMouseViewport.IsInside` est inchangé, puisque les positions locales sont en pixels de conception.
  - 9 tests : conversion, point dans l'image hors de (0,0,w,h), bande, routeur, clic réel sur un bouton à k=3 et à 1,5, `IntegerFit` inchangé. Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4903/4903.

## Phase 3 — Preuves, sample, documentation

### ✅ T3.1 — Test GPU de non-régression et de présentation

- Objectif : prouver au pixel près que `BufferedIntegerFit` à k=3 donne la même image que `IntegerFit` à k=3 sur une scène de test, et que `BufferedFit` remplit la hauteur.
- Fichiers : nouveau `CasaEngine.Tests/Rendering/BufferedPresentationGpuTests.cs` (harnais `GpuDeviceHost`, `[GpuFact]`).
- Étapes :
  1. Une scène de test synthétique : des rectangles de couleur et une image à texels entiers.
  2. Dessin direct au facteur 3, puis dessin dans une cible de conception présentée au facteur 3 en point. Les deux sont relus avec `GetData` et comparés pixel par pixel.
  3. Mode libre : vérifier le rectangle présenté et les bandes noires. Les pixels lissés ne sont pas comparés au pixel près.
- Validation : `CasaEngine.Tests` vert. Le test est ignoré, pas en échec, sans GPU.
- Commit : `test(rendering): prove buffered integer presentation matches direct drawing`
- Note de validation (2026-11-08) : le tampon arrière du jeu de test fait 256x192, donc la scène fait 64x48 et k vaut 4. Dessin direct au facteur 4 dans une cible de 256x192, contre dessin à l'échelle 1 dans la cible de 64x48 présentée par `VirtualResolutionPresenter` dans le tampon arrière (`GetBackBufferData`) : 0 pixel différent sur 49 152. L'image attendue contient bien le rouge, le jaune et le magenta de la scène. `BufferedFit` : 100x48 remplit la largeur, avec des bandes noires en haut et en bas. Tests réellement lancés ici, pas ignorés. Les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4905/4905.

### ✅ T3.2 — Sample `Projects/PixelUiSample`

- Objectif : montrer les trois modes dans un même projet.
- Fichiers :
  - `Projects/PixelUiSample/` : un monde en `BufferedIntegerFit` et un monde en `BufferedFit`, chacun avec son bloc `virtual_resolution` et un écran pour y aller et en revenir ;
  - `Projects/PixelUiSample/README.md` ;
  - `CasaEngine.Tests/UI/ProjectScreenAssetTests.cs`, si la forme du sample change.
- Étapes :
  1. Ajouter les deux mondes et les commandes d'écran qui y mènent, sans toucher au monde de départ.
  2. Lancer le Launcher à 960x540 puis à une taille non multiple, et noter ce qui est observé (voir « Validation globale »).
- Validation : `CasaEngine.Tests` vert, sample lancé. La recette visuelle de l'auteur reste à faire, donc 🧪 jusqu'à elle.
- Commit : `feat(samples): add a sample of per-world buffered resolutions`
- Note de validation (2026-11-08) :
  - **Écart au plan.** Un écran de données ne s'ouvre qu'avec le premier monde (`OpenFirstScreen`, une seule fois). Pour montrer l'UI et la souris dans un buffer, le premier monde doit donc être buffer. Faire cela dans `PixelUiSample` aurait changé son monde de départ et `ProjectScreenAssetTests`. Le sample est donc nouveau : `Projects/BufferedResolutionSample`, avec `TitleWorld` (`BufferedIntegerFit` 320x180 et écran titre), `FitWorld` (`BufferedFit` 200x150) et `DirectWorld` (sans bloc, donc l'`IntegerFit` du projet). `PixelUiSample` est inchangé.
  - Mesuré au Launcher, par capture de la zone client :
    - `TitleWorld` à 960x540 : image entière, et chacun de ses 960x540 pixels dans un bloc uniforme de 3x3 ;
    - à 1000x600 : image de 960x540 en (20, 30), bandes noires, blocs de 3x3 uniformes ;
    - `FitWorld` à 960x540, par une variante de projet hors dépôt : image de x=120 à 839, toute la hauteur (720x540), bandes noires.
  - Recette de l'auteur au Launcher, le 2026-11-08 : « tout est OK », après deux lancements du sample : clics, changement de monde, redimensionnement à la main. Les deux journaux sont vides et les deux sorties ont le code 0.

### ✅ T3.3 — Documentation

- Objectif : documenter le champ et les modes.
- Fichiers : la page moteur qui décrit la résolution virtuelle (sous `docs/engine/`, trouvée par recherche), `docs/README.md` si une page est ajoutée, `ai-agent/README.md` (état du plan).
- Étapes :
  1. Résumé, exemple de bloc `virtual_resolution`, choix entre les trois modes, limites (D1, D2) et ce qui reste reporté.
- Validation : relecture, liens relatifs ouverts, D6 respecté.
- Commit : `docs(rendering): document per-world virtual resolution and buffered modes`
- Note de validation (2026-11-08) : section « Résolution par monde et rendu dans un buffer (ADR-0087) » dans `docs/engine/rendering-2d-3d-spaces.md`, écrite en français comme la page : bloc, tableau des trois modes, présentation, souris, propriété de la cible, limites. La limite « une seule résolution par projet » de `docs/engine/data-driven-screens.md` est corrigée. Ligne d'index de `ai-agent/README.md` mise à jour. Contrôle D6 sur tout le diff de la branche : aucun nom de projet consommateur. Les tailles d'écran tirées de lui, qu'un premier jet des tests utilisait, sont remplacées par des tailles neutres dans un commit à part. Les commits antérieurs de la branche les contiennent encore dans leur historique.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Noms des modes : `BufferedIntegerFit` et `BufferedFit` sont proposés. L'auteur peut les changer à l'approbation | T0.1, T1.1 |
| O2 | Corriger `BackBufferPresenter` (échantillonneur, rectangle, bandes, facteur entier) ou écrire un présentateur dédié. Choix au moment du code, selon ce qui casse le moins l'API publique, et écrit dans la note de la tâche | T2.2 |
| O3 | Arrondi du rectangle en mode libre (taille entière et centrage) : documenté dans l'ADR et testé | T0.1, T2.2 |
| O4 | Le viewport de l'éditeur ignore la résolution par monde, comme aujourd'hui le réglage du projet, sous gestion externe des vues. À confirmer au code | T2.1 |
| O5 | Avec les ombres activées, une vue sur cible de rendu n'utilise pas le détour `_sceneRt`. À vérifier, puis à écrire dans l'ADR | T2.2 |
| O6 | Avis P3 du vérificateur (2026-11-08, CONFIRMED) : `TryResizeBufferedView` ne traite qu'une vue unique. Avec une seconde vue à côté de la vue buffer, la caméra buffer garderait la taille de la fenêtre au redimensionnement. Même limite que `IntegerFit` (`bbViews.Count == 1`). Non reproduit, reporté | — |
| O7 | Avis P4 : un bloc `virtual_resolution` écrit sans `mode` reçoit `"mode": "IntegerFit"` au prochain enregistrement par l'éditeur. Les mondes sans bloc ne changent pas. Reporté | — |
| O8 | Avis P4 : `Enum.TryParse` accepte aussi `"1"`, les espaces et les listes à virgules pour `mode`, dans le monde comme dans le projet. Reporté | — |
| O9 | Avis P4, faible confiance : `Present` laisse la cible liée en `Textures[0]` après `SpriteBatch.End`, comme le blit existant de `_sceneRt`. Aucun symptôme constaté. Reporté | — |

## Hors périmètre

- Modes Fill et Stretch, couleur de bandes réglable : toujours reportés (ADR-0048).
- Édition du bloc `virtual_resolution` dans l'inspecteur de l'éditeur : le champ est lu et écrit, pas encore exposé.
- Changement de taille de la fenêtre au changement de monde : la fenêtre ne change pas (D4).
- Marges de layout (ADR-0070) avec une résolution virtuelle : toujours refusées.
- Toute vérification dans un projet consommateur : elle relève de ce projet.
