# Plan agent IA — Caméra de viewport style Unreal (éditeur et démos)

Plan d'exécution du chantier « caméra de viewport comme Unreal », discuté avec l'auteur le 2026-11-14.
Les décisions D1 → D10 ci-dessous ont été arbitrées avec l'auteur le 2026-11-14 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Donner au viewport 3D (perspective) de l'éditeur et à la caméra des démos runtime (`ScriptArcBallCamera`) les contrôles souris et clavier du viewport perspective d'Unreal Engine, documentés par Epic ([Viewport Controls, UE 5.9](https://dev.epicgames.com/documentation/unreal-engine/viewport-controls-in-unreal-engine), lue le 2026-11-14). La logique de navigation est écrite une seule fois, dans le runtime, et partagée par l'éditeur et les démos. Dans l'éditeur, la sélection passe au clic relâché pour libérer le glisser gauche au profit de la caméra.

Problème d'origine : dans les démos, la molette déplace la cible de l'orbite (`ScriptArcBallCamera.cs:111`), donc le point de rotation bouge après un coup de molette.

## État vérifié du dépôt (2026-11-14)

- Démos : `Demo.CreateCamera` crée un `ArcBallCameraComponent` piloté par `ScriptArcBallCamera` (`CasaEngine.Demos/Demo.cs:24-35`). `Projects/SandBoxGame/SandBoxGame.cs:53` utilise aussi `ScriptArcBallCamera`.
- `ScriptArcBallCamera` (`CasaEngine/Framework/Scripting/ScriptArcBallCamera.cs`) : clic droit glissé = orbite autour de `Target` (l. 79-101) ; molette = dolly, `Target += Direction * step` (l. 103-113) ; flèches et Page Up/Down déplacent `Target` sans bouton (l. 120-153) ; arbitrage avec l'UI par `ResolveUiOwnership` (l. 156-176). Propriétés publiques `InputTurnRate`, `InputDistanceRate`, `InputDisplacementRate`, copiées par `Clone` (l. 198-206).
- Éditeur : `EditorViewportCameraController` (`CasaEngine.Editor/Runtime/EditorViewportCameraController.cs`), `internal`, état propre `Yaw`/`Pitch`/`Distance`/`Target` recopié dans un `ArcBallCameraComponent` par `ApplyTo` (l. 58-64). Clic milieu glissé = orbite, Shift + clic milieu = pan (l. 188-209) ; clic droit glissé = orbite (l. 211-220) ; molette = `Distance` ×/÷ 1.1, bornée [0.5, 1000] (l. 222-232) ; flèches et Page Up/Down déplacent `Target`, WASD/QE aussi pendant un clic droit ou quand la vue a le focus clavier, sauf Ctrl tenu (l. 234-311).
- Appelé depuis `WorldViewportPanel.Update` (`CasaEngine.Editor/Controls/WorldViewportPanel.cs:552`), seulement quand la vue n'utilise ni la caméra 2D ni la vue de face 2D (`UsesCamera2d`, `UseFront2dCamera`, l. 534-563). Autres usages : `CaptureState`/`RestoreState` (mode Play, l. 362, 438), `SetState` (vue de face 2D, l. 698, 816), `Focus` dans `FocusBounds` (l. 776).
- Cadrage : `WorldViewportPanel.FocusEntity` → `FocusBounds` (l. 740-779), déclenché par double-clic dans le panneau des entités (`GameEditor.cs:3922`) et par les éditeurs d'asset (`EntityAssetEditorPanel.cs:191`, `Animation2dAssetInspectorPanel.cs:279`). Aucune touche F : `rg -n "Keys\.F\b" CasaEngine.Editor` ne renvoie rien.
- Sélection : `EditorViewportGizmoController.Update` sélectionne **à l'appui** du bouton gauche (`CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs:321-336`), Ctrl = ajouter, Alt = retirer. `Gizmo.SelectEntities` ne sélectionne que si `ActiveAxis == GizmoAxis.None` (`GizmoTool/Gizmo.cs:339-358`). `ActiveAxis` est mis à jour au survol quand les boutons gauche et droit sont relâchés (`Gizmo.cs:618-622`). La sélection se fait par rayon contre les boîtes englobantes (`PickObject`, `Gizmo.cs:1009-1045`).
- Raccourcis gizmo actuels : 1/2/3/4 = mode, Shift = précision, O, I, P, Échap (`EditorViewportGizmoController.cs:271-311`).
- Composant : `ArcBallCameraComponent` expose `Target`, `Distance`, `Yaw`, `Pitch` (l. 15-120) et place l'œil en `Target - Direction * Distance` (`ComputeViewMatrix`, l. 153-158). `Camera3dComponent.FieldOfView` existe (l. 11, borné [0.1, π − 0.1]) et est **recalculé depuis le rapport d'aspect** par `InitializeWithWorld` et `OnScreenResized` (`Camera3dComponent.cs:29-57`).
- Des démos écrivent l'état initial directement dans le composant : `WorldSpaceUIDemo.InitializeCamera` (`CasaEngine.Demos/Demos/WorldSpaceUIDemo.cs:87-95`, `Target`, `Distance`, `Yaw`, `Pitch`).
- Ordre par image dans l'éditeur : `_cameraController.Update` (`WorldViewportPanel.cs:552`) passe **avant** `UpdateGizmoInput` (l. 566). `Gizmo.Update` manipule dès que le bouton gauche est tenu et `ActiveAxis != None` (`Gizmo.cs:415`). Le composant est sérialisé (`Load`, l. 279-287) ; la caméra de l'éditeur n'est pas sauvegardée.
- Souris runtime : `MouseManager` (`CasaEngine/Engine/Input/MouseManager.cs`) fournit `DeltaX`, `DeltaY`, `WheelDelta`, l'état des trois boutons, appuyé et relâché.
- Tests existants : `CasaEngine.Tests/Editor/EditorViewportCameraControllerTests.cs` (3 tests, clavier et capture), `CasaEngine.Tests/Scene/ArcBallCameraComponentTests.cs`.
- Doc : pas de page sur les contrôles du viewport 3D dans `docs/editor/` (`docs/README.md:91-95`). Dernière ADR : ADR-0088.
- Arbre de travail : modifications préexistantes de l'auteur (`.serena/project.yml`, `MGUI`, `.codegraph/`, `.serena/memories/`, `Projects/SampleProject/.casaeditor/viewport.editor.json`) : ne jamais les indexer.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Schéma complet du viewport perspective d'Unreal, dans l'éditeur **et** dans les démos (réponse de l'auteur : « A, je veux le comportement complet d'Unreal », « schéma complet »). |
| D2 | Glisser gauche (sans Alt, hors poignée de gizmo) = caméra : avance/recule et tourne à gauche/droite. La sélection passe au **clic relâché sans mouvement**. |
| D3 | Molette = avance/recule de la caméra par crans. Quand la distance au pivot atteint le minimum, le pivot est **poussé** devant la caméra. |
| D4 | Les raccourcis caméra actuels de l'éditeur (clic milieu = orbite, Shift + clic milieu = pan, clic droit = orbite, flèches et Page Up/Down sans bouton) sont **remplacés**, pas conservés. |
| D5 | Pas de nouveau composant caméra : `ArcBallCameraComponent` garde son nom, son API et son format. Seuls les contrôles changent. |
| D6 | La logique de navigation est une classe pure du runtime (`CasaEngine`), partagée par l'éditeur et `ScriptArcBallCamera`. Décision enregistrée en ADR-0089. |
| D7 | Table de contrôles (source : doc Epic citée dans l'objectif), en perspective : glisser gauche = avance/recule + lacet ; glisser droit = regarder autour de l'œil ; gauche + droit, ou glisser milieu = pan haut/bas/gauche/droite ; molette = avance/recule par crans ; clic droit tenu + molette = vitesse de déplacement ; clic droit tenu + W/S/A/D (et flèches, pavé 8/2/4/6) = avant/arrière/gauche/droite ; + E/Q (pavé 9/7, Page Up/Down) = haut/bas en espace monde ; + R/F = haut/bas en espace local ; + Z/C (pavé 1/3) = champ de vision plus large/plus étroit ; Alt + glisser gauche = orbite autour du pivot ; Alt + glisser droit = dolly vers le pivot ; Alt + glisser milieu = pan (track) ; F = cadrer la sélection. |
| D8 | Sélection dans l'éditeur (source : même doc) : clic gauche = remplace la sélection ; Ctrl ou Shift + clic gauche = ajoute à la sélection. Alt ne sert plus à retirer de la sélection. |
| D10 | Modes du gizmo comme Unreal : W = déplacer, E = tourner, R = échelle, à la place de 1/2/3, seulement quand le bouton droit n'est pas tenu (sinon c'est le vol de D7) et sans Ctrl. 4 reste l'échelle uniforme, qui n'a pas de touche dans Unreal. P1, P3 à P8 validés par l'auteur le 2026-11-14 (« le reste est OK »). |
| D9 | Les vues orthographiques (caméra 2D `EditorViewport2dCameraController`, vue de face 2D) ne changent pas, sélection comprise (à l'appui, Ctrl ajoute, Alt retire). |

## Points à valider (proposés par l'agent)

| Réf | Proposition | Raison |
|---|---|---|
| P1 | Seuil de glisser : 4 pixels de distance cumulée depuis l'appui. En dessous, c'est un clic. | La doc d'Epic ne donne pas de valeur ; il faut une constante. Réglable en une ligne. |
| P2 | **Remplacé par D10** (réponse de l'auteur du 2026-11-14 : « P2 : comme Unreal W E R »). | — |
| P3 | Démos : F cadre l'origine du monde (pivot remis à `Vector3.Zero`, distance inchangée), puisqu'il n'y a pas de sélection. Le `Demo` peut fournir un autre point via une propriété. | D1 demande le schéma complet, mais les démos n'ont pas de sélection. |
| P4 | Valeurs par défaut reprises de l'existant : sensibilité de rotation 0.005 rad/pixel, vitesse de déplacement 10 unités/s, distance minimale 0.5, maximale 1000, cran de molette = 10 % de la distance au pivot (au moins 0.5 unité), pan = 0.01 × distance par pixel. Vitesse au clic droit + molette : ×1.25 ou ÷1.25 par cran, bornée [0.1, 1000]. | Pas de valeur dans la doc d'Epic ; ces chiffres sont ceux du code actuel (`EditorViewportCameraController.cs:30-36`, `ScriptArcBallCamera.cs:49-51`). |
| P6 | **Propriété de l'état.** Pivot, distance, lacet, tangage : dans les démos, le composant est la source de vérité (`ScriptArcBallCamera` lit l'état du composant au début de chaque `Update` et le réécrit à la fin, donc les écritures d'`InitializeCamera` sont gardées) ; dans l'éditeur, `EditorViewportCameraController` reste la source (comme aujourd'hui, pour `Focus`, `SetState`, `CaptureState`/`RestoreState`) et écrit le composant par `ApplyTo`. **Champ de vision** : jamais dans l'état de navigation ; Z/C modifient directement `Camera3dComponent.FieldOfView`, que `ApplyTo` ne touche pas. Un redimensionnement ou une réinitialisation le recalcule depuis l'aspect (règle existante) et efface donc le réglage Z/C ; le mode Play ne le sauvegarde pas. Limites acceptées et documentées. | Constat du relecteur : sans règle, `ApplyTo` écraserait soit le champ de vision calculé, soit l'état initial des démos. |
| P7 | **F et vol.** F cadre seulement si le bouton droit n'est pas tenu ; bouton droit tenu, F descend en espace local (D7). La navigation renvoie une demande de cadrage, l'appelant l'exécute. | D7 donne deux sens à F. |
| P8 | **Ordre par image dans l'éditeur** : (1) `WorldViewportPanel` lit `Gizmo.ActiveAxis` (survol de l'image précédente) ; (2) il appelle la caméra avec le drapeau « poignée » ; (3) la caméra renvoie un résultat (`ViewportNavigationResult` : clic gauche relâché, demande de cadrage) ; (4) ce résultat est passé à `EditorViewportGizmoController.Update`, qui sélectionne au clic relâché. Le survol vieux d'une image est une limite acceptée : `ActiveAxis` est recalculé à chaque image boutons relâchés, l'écart est d'un déplacement de souris d'une image. | Constat du relecteur sur l'ordre caméra → gizmo. |
| P5 | `ScriptArcBallCamera` garde ses trois propriétés publiques ; `InputDistanceRate` devient la part de distance d'un cran de molette, `InputDisplacementRate` la vitesse de vol. Doc XML mise à jour. | API publique stable (`AGENTS.md` §9.8). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/unreal-viewport-camera`**, créée depuis `main` (`778f6aa8` au 2026-11-14). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` ou `dotnet build CasaEngine.Editor.MonoGame.sln` selon le périmètre) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin). La navigation tourne dans `Update` : entrée par `struct`, aucune allocation par image.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous les tests passent, dont les nouveaux tests de navigation.
- Smoke démos : lancer `CasaEngine.Demos`, ouvrir une démo 3D (par exemple `MaterialDemo`), donner plusieurs coups de molette puis Alt + glisser gauche : l'orbite tourne autour du pivot cadré, pas d'un point déplacé ; vérifier chaque ligne de D7.
- Smoke éditeur : ouvrir `Projects/SampleProject`, vérifier chaque ligne de D7 et D8 dans le viewport perspective ; un clic sur une entité la sélectionne au relâchement ; un glisser gauche ne change pas la sélection ; un glisser sur une poignée de gizmo déplace l'entité sans bouger la caméra ; la vue 2D et la vue de face 2D se comportent comme avant (D9) ; le mode Play restaure la caméra en sortie.

---

## Phase 0 — Décision et cœur de navigation

### ✅ T0.1 — ADR-0089 : navigation de viewport partagée style Unreal

- Objectif : enregistrer D5, D6, D7, D8 et P1 à P8 retenus.
- Fichiers : `docs/decisions/0089-viewport-camera-navigation-follows-unreal.md` (nouveau), `docs/decisions/README.md`.
- Étapes :
  1. Rédiger l'ADR selon `docs/decisions/template.md` (skill `adr`), en citant la doc d'Epic et ce plan.
  2. Ajouter la ligne à l'index.
- Validation : relecture ; liens valides.
- Commit : `docs(adr): record ADR-0089, Unreal-style viewport camera navigation`
- Note de validation (2026-11-14) : ADR-0089 écrite et ajoutée à l'index `docs/decisions/README.md` ; pas de code touché.

### ✅ T0.2 — Classe pure `ViewportCameraNavigation` et ses tests

- Objectif : une logique de navigation unique, sans dépendance à l'éditeur ni à MonoGame `Game`, testable sans GPU.
- Fichiers : `CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs` (nouveau : `ViewportCameraNavigation`, qui ne garde que l'état du geste et la vitesse de vol ; `ViewportNavigationInput`, `ViewportCameraState` (pivot, distance, lacet, tangage, **sans** champ de vision, P6) et `ViewportNavigationResult` en `struct`) ; `CasaEngine.Tests/Scene/ViewportCameraNavigationTests.cs` (nouveau).
- Étapes :
  1. Entrée par image : boutons gauche/droit/milieu (tenus, appuyés, relâchés), Alt/Ctrl/Shift, deltas souris, cran de molette, touches de D7, temps écoulé, et un drapeau « l'appui gauche a commencé sur une poignée de gizmo » qui interdit le geste caméra.
  2. Machine à états du geste en cours (aucun, glisser gauche, regarder, pan, orbite Alt, dolly Alt, track Alt), choisie à l'appui ; seuil P1 avant de déclarer un glisser gauche ; la méthode indique si le relâchement gauche est un **clic** (pour la sélection de l'éditeur).
  3. Regarder autour de l'œil : garder la position de l'œil, changer lacet/tangage, recalculer le pivot à `œil + Direction × distance`.
  4. Molette (D3) : réduire la distance ; au minimum, pousser le pivot vers l'avant. Clic droit tenu + molette = vitesse (P4).
  5. Vol clavier (clic droit tenu) : axes de D7, E/Q en monde, R/F en local. Z/C renvoient un delta de champ de vision dans le résultat ; l'appelant l'applique à `Camera3dComponent.FieldOfView` (P6).
  6. Signature : `Update(ref ViewportCameraState state, in ViewportNavigationInput input) → ViewportNavigationResult` (clic gauche relâché, demande de cadrage P7, delta de champ de vision). Aides `ViewportCameraState.From(ArcBallCameraComponent)` et `ApplyTo(ArcBallCameraComponent)` (pivot, distance, lacet, tangage seulement).
  7. Tests : un test par ligne de D7 ; le pivot ne bouge pas à la molette tant que la distance reste au-dessus du minimum ; poussée au minimum ; l'œil ne bouge pas en regardant ; clic contre glisser autour du seuil ; poignée de gizmo = aucun mouvement ; tangage borné ; F seul = demande de cadrage, bouton droit + F = descente locale sans demande de cadrage (P7) ; `ApplyTo` ne touche pas `FieldOfView` (P6).
- Validation : `dotnet build CasaEngine.MonoGame.sln` ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` vert.
- Commit : `feat(camera): add a shared Unreal-style viewport camera navigation`
- Note de validation (2026-11-14) : `ViewportCameraNavigation` et 29 tests (`CasaEngine.Tests/Scene/ViewportCameraNavigationTests.cs`), dont la position de l'œil identique à celle d'`ArcBallCameraComponent`. `dotnet build CasaEngine.MonoGame.sln` réussi ; `CasaEngine.Tests` 5107/5107. Constante non donnée par P4 : vitesse de Z/C = π/4 rad/s (`FieldOfViewRate`). Alt + glisser droit : le zoom suit `dx - dy`.

---

## Phase 1 — Éditeur

### ✅ T1.1 — `EditorViewportCameraController` délègue à la navigation partagée

- Objectif : D4 et D7 dans le viewport perspective de l'éditeur ; touche F.
- Fichiers : `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs`, `CasaEngine.Editor/Controls/WorldViewportPanel.cs`, `CasaEngine.Tests/Editor/EditorViewportCameraControllerTests.cs`.
- Étapes :
  1. Le contrôleur garde la capture (activer la vue, relâcher l'input), `CaptureState`/`RestoreState`/`SetState`/`Focus`/`CreateCameraComponent`, et confie les calculs à `ViewportCameraNavigation`. Supprimer les anciens gestes (D4).
  2. Capturer la vue au début de tout geste gauche, droit ou milieu, comme aujourd'hui pour le droit et le milieu.
  3. Garder la règle « Ctrl tenu = pas de vol au clavier » (raccourcis Ctrl+S, Ctrl+D) : avec D7 les touches ne volent qu'au clic droit, la règle reste vraie.
  4. Sur demande de cadrage du résultat (F, bouton droit non tenu, P7 ; vue avec le focus clavier, pas de Ctrl) = `FocusBounds` de la sélection courante du gizmo ; sans sélection, rien. Delta Z/C appliqué à `_camera.FieldOfView` (P6).
  4 bis. Propriété de l'état selon P6 : le contrôleur garde pivot, distance, lacet, tangage ; `CaptureState`/`RestoreState` inchangés (sans champ de vision).
  5. Adapter les trois tests existants au nouveau schéma (le vol au clavier exige le clic droit).
- Validation : `dotnet build CasaEngine.Editor.MonoGame.sln` ; tests verts.
- Commit : `feat(editor): drive the 3D viewport camera with the Unreal-style navigation`
- Note de validation (2026-11-14) : le contrôleur délègue à `ViewportCameraNavigation`, capture la vue sur tout bouton, renvoie `null` sans mouvement libre ; F appelle `FocusEntity` sur l'entité sélectionnée. Ajout : pas de delta souris au tout premier appel (plus de saut depuis (0, 0)). Tests du contrôleur réécrits et complétés (10 cas). `dotnet build CasaEngine.Editor.MonoGame.sln` réussi ; tests caméra 40/40. Smoke dans l'éditeur lancé : à faire en T3.2.

### ✅ T1.2 — Sélection au clic relâché, poignées de gizmo prioritaires

- Objectif : D2 et D8.
- Fichiers : `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`, `CasaEngine.Editor/Controls/WorldViewportPanel.cs`, éventuellement `GizmoTool/Gizmo.cs` (exposer, sans changer, ce qu'il faut pour savoir si l'appui est sur une poignée : `ActiveAxis` est déjà public).
- Étapes :
  1. Appliquer l'ordre par image de P8 dans `WorldViewportPanel.Update` : lire `ActiveAxis` avant la caméra, passer le drapeau « poignée », transmettre le résultat à `EditorViewportGizmoController.Update` par un nouveau paramètre `ViewportNavigationResult?`.
  2. **Règle par mode de vue (D9)** :
     - Perspective (ni `UsesCamera2d` ni `UseFront2dCamera`) : le contrôleur renvoie un résultat. À l'appui gauche : manipulation de gizmo si `ActiveAxis != None` (inchangé), plus aucune sélection à l'appui. Au relâchement : si le résultat signale un clic (P1), `SelectEntities` avec ajout = Ctrl ou Shift, retrait = faux.
     - Caméra 2D (`UsesCamera2d`, qui n'appelle pas `_cameraController`) et vue de face 2D (`UseFront2dCamera`, appelée avec `allowFreeCameraMovement = false`) : le paramètre vaut `null`. La navigation ne tourne pas (le contrôleur sort tôt quand `allowFreeCameraMovement` est faux et renvoie `null`), F n'y fait rien de nouveau, et la sélection reste **exactement l'actuelle** : à l'appui, Ctrl ajoute, Alt retire (`EditorViewportGizmoController.cs:321-336`).
  3. En perspective seulement, Alt + clic ne retire plus de la sélection (D8).
  4. Vérifier que Shift (mode précision du gizmo, l. 291) n'interfère pas avec Shift + clic.
  5. Test unitaire si la décision clic/glisser est isolable sans GPU ; sinon le dire dans la note de validation.
- Validation : build éditeur ; tests verts ; smoke éditeur : (a) glisser gauche commencé sur une poignée = l'entité bouge, `Target` et `Yaw` de la caméra inchangés ; (b) clic dans le vide avec une sélection = sélection vidée **au relâchement**, pas à l'appui ; (c) Ctrl + clic et Shift + clic ajoutent ; (d) glisser gauche ne change pas la sélection ; (e) en vue 2D et en vue de face 2D, un clic sélectionne à l'appui comme avant (Ctrl ajoute, Alt retire) et un glisser gauche ne bouge pas la caméra. Test unitaire du contrôleur : `allowFreeCameraMovement = false` renvoie `null` et ne change ni pivot, ni distance, ni angles.
- Commit : `feat(editor): select on click release and keep the left drag for the camera`
- Note de validation (2026-11-14) : ordre P8 en place dans `WorldViewportPanel.Update` (`IsHoveringHandle` lu avant la caméra, résultat passé au gizmo) ; perspective : sélection au clic relâché, Ctrl ou Shift ajoute ; vues 2D : résultat `null`, sélection à l'appui inchangée. La décision clic/glisser et la poignée sont couvertes par `ViewportCameraNavigationTests` ; le contrôleur du gizmo demande un `GraphicsDevice`, pas de test unitaire ajouté. `dotnet build CasaEngine.Editor.MonoGame.sln` réussi ; `CasaEngine.Tests` 5111/5111. **Manque** : smoke (a) à (e) dans l'éditeur lancé, reporté à la recette T3.2.
- Recette de l'auteur acceptée le 2026-11-14 (« recette ok »).

### ✅ T1.3 — Modes du gizmo sur W, E, R

- Objectif : D10.
- Fichiers : `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`, `CasaEngine.Editor/Controls/WorldViewportPanel.cs` si l'état du bouton droit doit être transmis, docs qui citent 1/2/3 (`rg -n "\b[123]\b.*(Translate|Rotate|Scale)" docs/editor`).
- Étapes :
  1. Remplacer `Keys.D1`/`D2`/`D3` par `Keys.W`/`E`/`R` (l. 271-284), garder `Keys.D4`.
  2. Ignorer W/E/R quand le bouton droit est tenu ou Ctrl tenu.
  3. Vérifier qu'aucun autre raccourci de l'éditeur n'utilise W, E ou R seuls (`rg -n "Keys\.(W|E|R)\b" CasaEngine.Editor` au 2026-11-14 : seulement le vol clavier de `EditorViewportCameraController`).
- Validation : build éditeur ; tests verts ; smoke : W/E/R changent le mode, clic droit + W avance sans changer de mode.
- Commit : `feat(editor): switch gizmo modes with W, E and R like Unreal`
- Note de validation (2026-11-14) : W/E/R remplacent 1/2/3, 4 inchangé ; ignorés bouton droit tenu ou Ctrl tenu. Aucune doc de `docs/editor` ne citait 1/2/3. `dotnet build CasaEngine.Editor.MonoGame.sln` réussi. **Manque** : smoke dans l'éditeur lancé, reporté à la recette T3.2.
- Recette de l'auteur acceptée le 2026-11-14 (« recette ok »).

---

## Phase 2 — Démos runtime

### ✅ T2.1 — `ScriptArcBallCamera` utilise la navigation partagée

- Objectif : D1 et D7 dans les démos et `SandBoxGame`, ce qui corrige le pivot qui bouge à la molette.
- Fichiers : `CasaEngine/Framework/Scripting/ScriptArcBallCamera.cs`, tests associés (`CasaEngine.Tests/Scripting/EngineOwnedScriptObjectsTests.cs` si `Clone` change).
- Étapes :
  1. Construire `ViewportNavigationInput` depuis `MouseManager` et `KeyboardManager`, en gardant `ResolveUiOwnership` : souris ignorée si l'UI la tient, au début du geste seulement (un geste commencé dans la vue continue au-dessus de l'UI, comme le clic droit aujourd'hui).
  2. Propriété de l'état selon P6 : `ViewportCameraState.From(component)` au début de chaque `Update`, `ApplyTo(component)` à la fin ; delta Z/C appliqué à `FieldOfView`. Demande de cadrage (P7) = P3.
  3. P5 pour les propriétés publiques ; doc XML de la classe réécrite.
  4. Vérifier les démos qui lisent les mêmes touches sans clic droit (`AnimationIkDemo` flèches et Page Up/Down, `AnimationBlendDemo` WASD) : avec D7, la caméra ne les lit plus qu'au clic droit ; noter tout conflit restant.
- Validation : `dotnet build CasaEngine.MonoGame.sln` ; tests verts ; smoke démos, dont `WorldSpaceUIDemo` qui s'ouvre sur sa caméra initiale (`InitialCameraTarget`, `InitialCameraDistance`, `InitialCameraYaw`, `InitialCameraPitch`).
- Commit : `feat(scripting): give ScriptArcBallCamera the Unreal-style navigation`
- Note de validation (2026-11-14) : `ScriptArcBallCamera` lit l'état du composant à chaque `Update` et le réécrit (P6) ; un bouton ne rejoint le geste que s'il est appuyé hors UI ; propriétés publiques gardées et reliées à la navigation (P5) ; nouvelle propriété `FocusPoint` (P3), copiée par `Clone`. Conflit restant : `AudioDemo` utilise F (boucle sonore) avec la caméra par défaut, donc F recentre aussi la caméra (O3). Les touches des autres démos (flèches, Page Up/Down, WASD, R, Q) ne bougent plus la caméra sans clic droit. Pas de test unitaire du script : son `Update` lit l'`InputComponent` du jeu. `dotnet build CasaEngine.MonoGame.sln` réussi ; `CasaEngine.Tests` 5111/5111. **Manque** : smoke dans `CasaEngine.Demos` (dont `WorldSpaceUIDemo` sur sa caméra initiale), reporté à la recette T3.2.
- Recette de l'auteur acceptée le 2026-11-14 (« recette ok »).

---

## Phase 3 — Documentation et recette

### ✅ T3.1 — Doc des contrôles de caméra

- Objectif : documenter D7, D8, P1 à P5 pour l'éditeur et les démos.
- Fichiers : `docs/editor/viewport-camera-controls.md` (nouveau), `docs/README.md`, README de `CasaEngine.Demos` s'il liste des contrôles.
- Étapes : tableau des contrôles, différences avec Unreal (pas de sélection rectangle, pas de déplacement temporaire du pivot du gizmo, vues 2D inchangées), limites.
- Validation : relecture ; liens valides.
- Commit : `docs(editor): document the Unreal-style viewport camera controls`
- Note de validation (2026-11-14) : `docs/editor/viewport-camera-controls.md` écrit (souris, clavier, F, W/E/R, sélection, vues 2D, limites) et ajouté à `docs/README.md`. `CasaEngine.Demos` n'a pas de README général qui liste les contrôles : rien à mettre à jour.

### ✅ T3.2 — Vérification indépendante et recette de l'auteur

- Objectif : vérifier le chantier entier avant de le déclarer fini.
- Étapes :
  1. Validation globale ci-dessus.
  2. Vérificateur neuf (`verifier`) sur la branche : contrôles D7 et D8 contre le code et les tests, D9 inchangé, pas d'allocation par image dans la navigation.
  3. Recette de l'auteur dans l'éditeur et les démos.
- Validation : verdict CONFIRMED et accord de l'auteur ; sinon 🧪 avec ce qui manque.
- Commit : `docs(ai-agent): record the check of the Unreal-style viewport camera`
- Note de validation (2026-11-14) : vérificateur neuf **CONFIRMED** sur `main..HEAD` (les deux `.sln` 0 erreur, `CasaEngine.Tests` 5111/5111, sonde hors dépôt : 1,3 million d'appels de `Update` sans allocation par image). Avis non bloquants, reportés : A1 (P3) `IsHoveringHandle` lit `CurrentSelection`, qui alloue une liste par LINQ à chaque image où la souris survole une poignée (éditeur seulement ; **corrigé le 2026-11-14** à la demande de l'auteur : `_gizmo.Gizmo.Selection.Count`, build éditeur réussi, `CasaEngine.Tests` 5111/5111) ; A2 (P4) relâcher le droit et appuyer le gauche dans la même image donne une image de pan ; A3 (P4) vue avec focus clavier sans input pointeur : un clic théorique, ignoré par le gizmo ; A4 (P4) saut possible à la sortie du mode Play si un bouton est tenu, comportement antérieur ; A5 (P4) sélection et W/E/R sans test unitaire (le contrôleur du gizmo demande un `GraphicsDevice`). **Manque** : recette de l'auteur dans l'éditeur et les démos (T1.2, T1.3, T2.1), et réponse à O3.
- Recette de l'auteur acceptée le 2026-11-14 (« recette ok »).

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Résolu le 2026-11-14 : P1, P3 à P8 validés, P2 remplacé par D10. | T0.1 |
| O3 | Résolu le 2026-11-14 (« change la touche de AudioDemo ») : le fondu de la boucle sonore d'`AudioDemo` passe de F à U (code, aide à l'écran, `docs/engine/audio-system.md`). | T2.1 |
| O2 | Le glisser gauche bouge-t-il la caméra quand l'appui tombe sur une entité déjà sélectionnée mais hors poignée ? Proposition : oui, comme Unreal en perspective (seule la poignée prend le glisser). | T1.2 |

## Hors périmètre

- Vues orthographiques : caméra 2D de l'éditeur et vue de face 2D (D9), dont la sélection rectangle d'Unreal en orthographique.
- Sélection rectangle en perspective, magnétisme aux sommets (V), déplacement temporaire du pivot au clic milieu, mode jeu (G), plein écran (F11), Magic Mouse et trackpad.
- Espace (bascule de mode) et Q (outil de sélection) d'Unreal.
- Réglages utilisateur de la souris et de la caméra : **chantier suivant demandé par l'auteur le 2026-11-14**, un panneau de configuration dans l'éditeur, enregistré dans un fichier JSON de paramètres de l'éditeur. Il fera l'objet de son propre plan une fois celui-ci terminé.
- Tout changement d'`ArcBallCameraComponent` (nom, API, format), D5.
