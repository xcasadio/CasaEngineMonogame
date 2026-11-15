# Plan agent IA — Démos : angle de la caméra, panneau d'aide F1, clavier par position

Demande de l'auteur du 2026-11-15, après un essai de la démo Hello world à 1024×768 sur un clavier AZERTY :

- le cube paraît très déformé quand il est au bord de l'écran ;
- F1 cache le panneau d'aide, mais un second F1 en réaffiche un tronqué (titre, « Camera », une ligne) ;
- les touches sont prévues pour un clavier QWERTY. Elles doivent le rester sur un clavier QWERTY et suivre la position des touches sur un clavier AZERTY.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

- La caméra 3D garde son angle vertical (`FieldOfView`) quelle que soit la forme de la fenêtre.
- Le panneau d'aide réapparaît entier après F1.
- Les liaisons clavier du mapping moteur et la navigation caméra désignent une **position** de touche, celle du clavier QWERTY US. Elles sont traduites selon la disposition du clavier (périmètre exact : D2, D5, D6). Sur QWERTY, rien ne change ; sur AZERTY, WASD devient ZQSD, et les textes d'aide affichent les touches réelles.

## État vérifié du dépôt (2026-11-15, `main` `a63f5bdd`)

- `Camera3dComponent.ComputeFieldOfView` (`CasaEngine/Framework/Scene/Entities/Components/Camera3dComponent.cs:53-57`) remplace `FieldOfView` à chaque `InitializeWithWorld` et à chaque `OnScreenResized` par `π/4 × 1,7778 / aspect`. En 16:9, cela fait 45° vertical ; à 1024×768 (4:3), 60° vertical, soit environ 75° horizontal. Le `fieldOfView` chargé (`Load`, par exemple `0.8377581` dans `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity:36`) est donc écrasé, tout comme la valeur saisie dans l'inspecteur (`CameraComponentEditor.cs:53`).
- Panneau d'aide : `DemoHelpOverlay` (`CasaEngine.Demos/Demos/DemoUI/DemoHelpOverlay.cs`) dimensionne sa fenêtre une seule fois, dans `OnWindowLoaded` (`ApplySizeToContent` puis `ApplyScreenPlacement`). `SetVisible` ne fait que basculer `Window.Visibility` (`Collapsed` / `Visible`). La cause du panneau tronqué n'est pas encore établie.
- Clavier :
  - les démos utilisent `MonoGame.Framework.DesktopGL` (SDL) ; dans MonoGame, `Keys` est la touche du caractère produit, donc dépendante de la disposition ;
  - la navigation caméra code en dur `W A S D Q E Z C R F` (`CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs:289-311`), et l'éditeur s'en sert aussi ;
  - les actions des jeux viennent de `ButtonsMapping` / `InputMapping` (`CasaEngine/Framework/Input/`) ;
  - le moteur a aussi une source d'input Win32 (`Win32WindowInputSource.cs`) ;
  - `SDL2.dll` est livré par le paquet DesktopGL 3.8.1.303.
- Dossier principal : une autre session y travaille, sur `chantier/mgui-nonalloc`. Ce chantier travaille dans le worktree `D:\development\repo\CasaEngineMonogame-demos2` et ne touche pas au sous-module `MGUI`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Angle vertical fixe (« Hor+ »)**, comme Unity (`Camera.fieldOfView` vertical, 60° par défaut) et Godot (`keep_aspect = KEEP_HEIGHT` par défaut, 75°) : `FieldOfView` est l'angle vertical, la largeur de la fenêtre ne change que le champ horizontal. Valeur par défaut : π/4, la valeur actuelle en 16:9. Unreal fait l'inverse (champ horizontal fixe, 90° par défaut, réglable). *À confirmer par l'approbation du plan.* |
| D2 | Clavier : **tout le mapping moteur** est lu par position (réponse de l'auteur du 2026-11-15). Sont traduites : les liaisons clavier des `InputMapping` (`KeyButton` de bouton et d'axe, qu'elles viennent d'un fichier `.buttonsMapping` ou du code), celles des actions de navigation UI, et les touches de navigation de la caméra (`ViewportCameraNavigation`, démos et éditeur). Chacune désigne la position de la touche sur un clavier QWERTY US. La traduction se fait à la lecture : `KeyButton.Key` n'est jamais modifié, et un mapping sauvé garde ses touches d'origine. Le format des fichiers ne change pas. |
| D5 | Raccourcis de l'éditeur (Ctrl+Z/Y/C/V/X/S/D, gizmo W/E/R, timeline) : **par lettre, inchangés** (réponse de l'auteur du 2026-11-15). |
| D6 | API des scripts `PlayerInput.IsKeyPressed(Keys)` / `IsKeyJustPressed(Keys)` / `IsKeyReleased(Keys)` : **inchangée, par lettre** (réponse de l'auteur du 2026-11-15). Un script qui veut une position passe par un mapping. |
| D7 | RPGDemo (`Projects/CasaEngine.RPGDemo/Scripts/ScriptSign.cs`) : les déplacements Z/Q/S/D (lettres AZERTY) **passent en W/A/S/D**. Après traduction, cela donne ZQSD sur AZERTY et WASD sur QWERTY (réponse de l'auteur du 2026-11-15). |
| D8 | Les deux caméras enregistrées avec `fieldOfView` 0.8377581 (48°), `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity` et `Projects/SampleProject/Entities/camera.entity`, **reviennent à π/4 (0.7853982)**, la valeur affichée aujourd'hui en 16:9. Changement de données noté dans l'ADR (réponse de l'auteur du 2026-11-15). |
| D3 | QWERTY : comportement identique à aujourd'hui (demande de l'auteur). |
| D4 | Seules les touches qui changent de place d'une disposition à l'autre sont traduites : lettres, chiffres de la rangée du haut et ponctuation. F1-F12, flèches, pavé numérique, Échap, Entrée, Espace et touches de modification restent tels quels. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/demos-camera-help-keyboard`**, créée depuis `main` dans le worktree `CasaEngineMonogame-demos2`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : sinon, passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build** des deux solutions et **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` avant tout ✅.
- Pas d'allocation dans les chemins chauds : la traduction des touches est calculée une fois, puis lue dans une table.
- **Langue** : ce plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe.
- Smoke : démo Hello world à 1024×768, cube au bord moins étiré, F1 deux fois et panneau entier. Sur AZERTY, ZQSD déplace la caméra et l'aide affiche Z Q S D. Ce dernier point est validé par l'auteur : la machine de l'agent n'a pas de clavier AZERTY.

---

## Phase 1 — Caméra

### ✅ T1.1 — La caméra 3D garde son angle vertical

- Objectif : `FieldOfView` est l'angle vertical. Il est conservé au chargement, à l'initialisation et au redimensionnement, et vaut π/4 quand il n'est pas renseigné (0).
- Fichiers : `Camera3dComponent.cs`, les deux `camera.entity` de D8, tests dans `CasaEngine.Tests/Scene/`, ADR `docs/decisions/0095-*.md` et son index.
- Étapes :
  1. Mesurer d'abord, par un test, la projection actuelle d'un point au bord de l'écran à 1024×768, pour confirmer que la déformation vient de l'angle.
  2. Supprimer le recalcul dans `InitializeWithWorld` et `OnScreenResized`. Appliquer la valeur par défaut π/4 si l'angle vaut 0.
  3. Passer les deux `camera.entity` à 0.7853982 (D8).
  4. Vérifier les appelants qui comptaient sur le recalcul :
     - démos aux cadrages vérifiés en `PI/4` (`TileMap3dDemo`, `TileMapSurfaceScreenDemo`) ;
     - éditeur (`CameraComponentEditor`, `EditorEntityJsonSerializer`) ;
     - `CameraTargeted2dComponent` (lit `FieldOfView` comme un angle vertical) ;
     - `Camera2dComponentTests` (reproduit l'ancienne formule ; son commentaire cite `ComputeFieldOfView`).
- Validation :
  - tests de projection, avec une caméra créée sans angle et une caméra chargée depuis un `camera.entity` : π/4 vertical à 16:9 et à 4:3 ;
  - builds et tests ;
  - captures de Hello world et d'`EditorWorldDemo`.
- Commit : `fix(camera): keep the vertical field of view of the 3d camera`
- Validation faite (2026-11-15) :
  - Mesure : à 1024×768, l'ancienne formule donnait 60° vertical (`π/4 × 1,7778 / 1,3333`) ; c'est désormais 45°.
  - Tests : `Camera3dFieldOfViewTests` (16:9, 4:3, portrait, conservation au redimensionnement, deux `camera.entity` chargés) ; 143 tests caméra, puis `CasaEngine.Tests` 5485/5485. Builds des deux solutions : 0 erreur.
  - Captures avant/après à 1024×768 : le cube de Hello world est un peu plus grand au centre ; dans Tile map 3d, le bord gauche du mur sort de l'écran à 4:3 (conséquence attendue de D1, notée dans l'ADR-0095).
  - Appelants : `CameraTargeted2dComponent` calcule sa distance à partir de `FieldOfView`, il reste exact au pixel quel que soit l'angle ; l'éditeur écrit et lit `FieldOfView`, la valeur saisie s'applique maintenant.
  - Trouvé en route, corrigé dans un commit à part (`f2f7f2b6`) : la démo « World made in the editor » plantait au démarrage, faute d'entrées `/copy` pour `Content/EditorWorld/` dans `Content.mgcb`. Un audit du catalogue face au `Content.mgcb` ne trouve pas d'autre fichier manquant. Après correction, la démo se charge et s'affiche.

## Phase 2 — Panneau d'aide

### ⏳ T2.1 — Le panneau d'aide réapparaît entier

- Objectif : après F1 puis F1, le panneau a la même taille qu'au lancement de la démo.
- Fichiers : `CasaEngine.Demos/Demos/DemoUI/DemoHelpOverlay.cs` (le sous-module `MGUI` n'est pas modifié), test si une fenêtre MGUI peut être chargée en test, sinon smoke.
- Étapes :
  1. Reproduire, puis trouver pourquoi la hauteur change quand la fenêtre redevient visible.
  2. Corriger dans la démo : par exemple, redimensionner et replacer la fenêtre quand elle redevient visible.
- Validation : démo lancée, panneau caché puis réaffiché par F1, capture comparée à celle du lancement.
- Commit : `fix(demos): show the whole help panel again after F1`

## Phase 3 — Clavier par position

### ⏳ T3.1 — Traduction des positions de touche selon la disposition

- Objectif : un service moteur qui traduit une touche QWERTY US en touche de la disposition active, par sa position physique. C'est l'identité sur QWERTY.
- Fichiers : `CasaEngine/Framework/Input/KeyboardLayout.cs` (nom à confirmer), tests.
- Étapes :
  1. Table position → code de balayage (scancode) pour les touches de D4.
  2. Lecture de la touche produite à chaque position, côté système. Le backend se choisit en T3.1 après essai, voir O1.
  3. Table calculée une fois, sans allocation à la lecture ; identité si la détection échoue, avec un message dans le journal.
- Validation : tests avec une disposition simulée (QWERTY = identité ; AZERTY : W→Z, A→Q, Q→A, Z→W, M→« , ») ; builds ; tests.
- Commit : `feat(input): translate key positions to the active keyboard layout`

### ⏳ T3.2 — Le mapping et la caméra lisent les touches par position

- Objectif : les touches des `ButtonsMapping` et de `ViewportCameraNavigation` passent par la traduction, et les textes d'aide (actions des démos, lignes « Camera ») affichent la touche réelle.
- Fichiers :
  - `CasaEngine/Engine/Input/KeyButton.cs` (`IsPressed`) et `CasaEngine/Engine/Input/Axis.cs` ;
  - `CasaEngine/Framework/UI/UINavigationInput.cs` (`IsHeldOnDevices`, qui lit `input.Key` directement) ;
  - `CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs` ;
  - `CasaEngine.Demos/DemoBindingText.cs` et la ligne « Camera » des démos (textes d'aide) ;
  - les HUD qui écrivent en dur les lettres de touches liées par le mapping :
    - `CasaEngine.Demos/Demos/AnimationBlendDemo.Hud.cs` (`[W] Walk`, `[W/A/D]`, `[Q]/[E]`, `[E]`, `[R]`, `[1] [2] [3]`) ;
    - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs:91` (`W A S D`) ;
    - `CasaEngine.Demos/Demos/AnimationIkDemo.cs:179,181` (`[I]`, `[V]`, `[R]`, `[O]/[P]`) ;
  - `Projects/CasaEngine.RPGDemo/Scripts/ScriptSign.cs` (D7) ;
  - ADR `0096-*.md` et doc d'input.
- Étapes :
  1. Traduire à la lecture dans `KeyButton.IsPressed`, `Axis` et `UINavigationInput.IsHeldOnDevices`, sans jamais modifier `KeyButton.Key`.
  2. Navigation caméra : WASD QE ZC RF passent par la traduction.
  3. Les textes d'aide et les HUD sont écrits à partir des touches traduites (`DemoInput.Describe` / `DemoBindingText`), plus aucune lettre d'action liée n'est écrite en dur. Ces textes sont formatés une fois, pas à chaque image.
  4. RPGDemo : Z/Q/S/D → W/A/S/D (D7).
  5. Ne pas toucher aux raccourcis de l'éditeur (D5) ni à `PlayerInput.IsKey*` (D6).
- Validation, sous une disposition AZERTY simulée :
  - un bouton `InputMapping` lié à W répond à Z ;
  - un `Axis` lié à A/D répond à Q/D ;
  - une action de navigation UI liée à une lettre suit la position ;
  - un mapping chargé puis sauvé garde ses touches d'origine ;
  - `PlayerInput.IsKeyPressed(Keys.W)` reste inchangé ;
  - les lignes HUD d'AnimationBlend et d'AnimationIk nomment Z pour marcher, A/E pour le poids de couche et Z/Q/D pour les directions (test sur le texte formaté) ;
  - une recherche de `"\[[A-Z0-9]\]` et de `W A S D` dans `CasaEngine.Demos` ne trouve plus aucune lettre d'action liée écrite en dur.
  
  Puis builds, tests, et démo lancée sur QWERTY sans changement. Sur AZERTY, la validation revient à l'auteur (🧪 jusque-là).
- Commit : `feat(input): read keyboard bindings by key position`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Backend de détection : Win32 `MapVirtualKeyEx` (Windows, fonctionne aussi dans l'éditeur) ou SDL `SDL_GetKeyFromScancode` (multiplateforme, à condition que SDL soit initialisé). Proposition : Win32 sous Windows, identité ailleurs. À trancher sur essai. | T3.1 |
| O2 | Changement de disposition pendant l'exécution (Alt+Maj) : la table n'est calculée qu'au démarrage, faute de demande contraire. | T3.1 |

## Hors périmètre

- Modifier le sous-module `MGUI`.
- Les raccourcis de l'éditeur, Ctrl+lettre, gizmo W/E/R et timeline (D5), et `PlayerInput.IsKey*(Keys)` (D6) : ils restent par lettre.
- Un menu de réaffectation des touches.
- Les dispositions dont la traduction n'est pas une simple permutation (Dvorak et autres fonctionnent quand même, par position).
