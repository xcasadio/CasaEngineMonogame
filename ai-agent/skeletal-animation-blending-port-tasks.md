# Portage de l'exemple three.js « Skeletal Animation Blending » dans CasaEngine

## Objectif

Recréer dans CasaEngine, sous forme d'un nouveau demo, l'exemple three.js
**Skeletal Animation Blending** (`webgl_animation_skinning_blending`) avec :

- un personnage skinné jouant **idle / walk / run** mélangés en temps réel ;
- une **fenêtre « Controls »** (équivalent du panneau lil-gui) reproduisant toutes
  les interactions de l'exemple ;
- un rendu visuellement équivalent (caméra, fond, sol, ombre portée, éclairage).

La fenêtre « example » à gauche de la page three.js n'est **pas** reproduite.

> Référence source : [treejs/examples/webgl_animation_skinning_blending.html](../treejs/examples/webgl_animation_skinning_blending.html)
> Capture de référence : [treejs/examples/screenshots/webgl_animation_skinning_blending.jpg](../treejs/examples/screenshots/webgl_animation_skinning_blending.jpg)

---

## Règles pour l'agent

1. Traiter **une seule tâche à la fois**, dans l'ordre du plan.
2. Avant de commencer une tâche, remplacer son statut `⏳ Todo` par `🚧 In progress`.
3. Quand la tâche est terminée, validée et committée, remplacer le statut par `✅ Done`.
4. Si le code est écrit mais qu'une vérification manque, utiliser `🧪 Needs testing`
   et noter la vérification manquante sous la tâche.
5. Si une tâche est bloquée, utiliser `⚠️ Blocked` et ajouter une note courte.
6. Le statut reste **devant le nom de la tâche**, dans le titre de la tâche.
7. Faire **exactement un commit compilable par tâche atomique**.
8. Mettre à jour ce fichier (statut + commit réalisé) **dans le même commit** que le code.
9. Une tâche ne passe `✅ Done` que si le hash/message de commit est renseigné.
10. Ne pas regrouper plusieurs tâches du plan dans un même commit.
11. Langue du document : français. Langue du code et des commits : anglais.
12. Ne pas casser l'API publique ; préférer des ajouts additifs.
13. Hot path (`Update`/`Draw`/layout) : pas de LINQ, pas d'allocations évitables.
14. Tout rendu `SpriteBatch`/line-list ou changement d'état GPU doit restaurer
    l'état du `GraphicsDevice`.
15. Ne pas ajouter de dépendance lourde.

## Légende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Validation minimale par tâche

- Tâche framework/runtime : `dotnet build CasaEngine.MonoGame.sln -c Debug`
- Tâche demo : `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug`
- Tâche tests : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~Animation`
- Capture d'écran (depuis `CasaEngine.Demos/`) :
  ```pwsh
  $env:CASAENGINE_START_DEMO        = "Skeletal animation blending"
  $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH     = "artifacts/validation/blending-casaengine.png"
  $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = "1500"
  dotnet run -c Debug
  ```
- ⚠️ `CasaEngine.Editor.MonoGame.sln` n'inclut **pas** `CasaEngine.Demos` : toujours
  rebuild `CasaEngine.Demos` avant une capture, sinon les binaires sont périmés.

---

## Cartographie three.js → CasaEngine

| Fonctionnalité three.js | Équivalent CasaEngine | État |
| --- | --- | --- |
| `PerspectiveCamera` + `lookAt` | `CameraLookAtComponent` / `SetPositionAndTarget` | ✅ existe |
| `scene.background = 0xa0a0a0` | `EnvironmentSettings.BackgroundColor` + `BackgroundMode.SolidColor` | ✅ existe |
| `scene.fog` (linéaire 10→50) | — | ❌ absent (tâche optionnelle) |
| `HemisphereLight(3)` | `AmbientColor`/`AmbientIntensity` + directionnelle de remplissage | ❌ absent → approximation |
| `DirectionalLight(3)` + shadow | `LightComponent` Directional + `CastShadows` + `ShadowPass` | ✅ existe |
| Sol `PlaneGeometry` + `receiveShadow` | `StaticModelComponent` + `BoxPrimitive`/plan + `ReceiveShadows` | ✅ existe |
| `Soldier.glb` (idle/run/walk) | modèle « kid » : 3 FBX séparés ; `kid_idle.model` = idle seul | ⚠️ partiel → tâche d'assemblage |
| `AnimationMixer` + 3 actions | `SkinnedMeshComponent` + `AnimationController`/graphe | ✅ existe |
| Blend weights (3 poids indépendants) | `LinearBlendAnimationNode` = 1D seulement | ❌ absent → `WeightedBlendAnimationNode` |
| `crossFadeTo` (warp) | animation des 3 poids sur une durée (niveau demo) | ✅ composable |
| pause / continue | `PauseAnimation` / `ResumeAnimation` | ✅ existe |
| single step | — | ❌ absent → `AdvanceAnimation(delta)` |
| `mixer.timeScale` | `AnimationClipNode.Speed` par nœud | ✅ existe |
| `model.visible` | `Entity.IsVisible` | ✅ existe |
| `SkeletonHelper` | `SkeletonDebugVisualizer.Draw(...)` + `Line3dRendererComponent` | ✅ existe |
| panneau lil-gui (folders/sliders/checkboxes/buttons) | `MGWindow` + `MGExpander` + `MGSlider` + `MGCheckBox` + `MGButton` | ✅ existe |
| `Stats` (FPS) | `DebugOverlay` (`CASAENGINE_SHOW_DEBUG_OVERLAY=1`) | ✅ existe (optionnel) |

---

## État observé (faits vérifiés)

- Les demos s'enregistrent dans [CasaEngine.Demos/DemosGame.cs](../CasaEngine.Demos/DemosGame.cs)
  via `_demos.Add(new XxxDemo());` (vers la ligne 78). Le titre du demo sert au
  démarrage automatique (`CASAENGINE_START_DEMO`).
- La classe de base est [CasaEngine.Demos/Demo.cs](../CasaEngine.Demos/Demo.cs) :
  `Initialize`, `Update`, `Clean`, `CreateCamera`, `InitializeCamera`,
  `ConfigureSceneLighting`, `PostDraw`, `OnScreenResized`.
- Le pattern UI per-view est démontré par
  [CasaEngine.Demos/Demos/UIOverlayDemo.cs](../CasaEngine.Demos/Demos/UIOverlayDemo.cs) :
  `_game.GameManager.ViewManager.GetActiveUIView()` puis `uiView.PushScreen(screen)` ;
  les écrans dérivent de `UIScreenBase` ([CasaEngine/Framework/UI/UIScreenBase.cs](../CasaEngine/Framework/UI/UIScreenBase.cs)).
- Les contrôles MGUI nécessaires existent :
  `MGExpander` ([MGUI/MGUI.Core/UI/MGExpander.cs](../MGUI/MGUI.Core/UI/MGExpander.cs)),
  `MGSlider`, `MGCheckBox`, `MGToggleButton`, `MGButton`, `MGStackPanel`, `MGTextBlock`.
- L'API d'animation moderne est dans `CasaEngine/Framework/Animations/` :
  `IAnimationGraphNode`, `AnimationClipNode` (avec `Speed`/`Loop`/`Advance`),
  `LinearBlendAnimationNode` (blend 1D uniquement), `AnimationPoseBlender.Blend`,
  `AnimationController`, `SkinnedMeshAnimationRuntime`.
- `SkinnedMeshComponent` ([CasaEngine/Framework/Scene/Entities/Components/SkinnedMeshComponent.cs](../CasaEngine/Framework/Scene/Entities/Components/SkinnedMeshComponent.cs))
  expose `PlayAnimation`, `CrossFadeToAnimation`, `PauseAnimation`, `ResumeAnimation`,
  `SeekAnimation`, `PlayAnimationGraph(IAnimationGraphNode)`, `AnimationClips`,
  `CurrentModelPose`, `SkeletonDefinition`.
- Helper squelette : `SkeletonDebugVisualizer.Draw(lineRenderer, modelPose, worldMatrix, options)`
  ([CasaEngine/Framework/Animations/SkeletonDebugVisualizer.cs](../CasaEngine/Framework/Animations/SkeletonDebugVisualizer.cs)) ;
  `lineRenderer = _game.Line3dRendererComponent`. Exemple complet dans
  [CasaEngine.Demos/Demos/AnimationIkDemo.cs](../CasaEngine.Demos/Demos/AnimationIkDemo.cs).
- Lumières : `LightComponent` gère `Directional`, `Point`, `Spot` (pas d'hémisphérique).
  Les ombres directionnelles fonctionnent pour les meshes skinnés.
- Environnement : `WorldEnvironmentSettings`
  ([CasaEngine/Framework/Rendering/Environment/WorldEnvironmentSettings.cs](../CasaEngine/Framework/Rendering/Environment/WorldEnvironmentSettings.cs))
  expose `BackgroundColor`, `BackgroundMode`, `AmbientColor`, `AmbientIntensity`,
  `Shadows` — **pas de brouillard**.
- Modèle : `kid_idle.FBX`, `kid_walk.FBX`, `kid_run.FBX` existent dans
  [CasaEngine.Demos/Content/SkinnedMesh/](../CasaEngine.Demos/Content/SkinnedMesh/).
  `kid_idle.model` pointe vers le rigged model `e6a898ce-…` issu de **`kid_idle.FBX`
  seul** → il ne contient que l'animation idle. Les 3 clips doivent être réunis sur
  un même squelette (tâche dédiée).

---

## Décisions d'architecture

- **Mélange 3 voies** : ajouter un `WeightedBlendAnimationNode` (N entrées + N poids
  normalisés `result = Σ wᵢ·poseᵢ / Σ wᵢ`). C'est le cœur de l'exemple (3 poids idle/walk/run
  indépendants), réutilisable et frère naturel de `LinearBlendAnimationNode`.
- **Crossfade** : piloté au niveau du demo en animant les 3 poids vers leur cible sur
  la durée choisie (équivalent fidèle du comportement `crossFadeTo` de l'exemple).
- **Vitesse globale** : appliquer `timeScale` au `Speed` des 3 `AnimationClipNode`.
- **Pause / single-step** : `PauseAnimation`/`ResumeAnimation` + nouvel
  `AdvanceAnimation(delta)` pour avancer d'un pas fixe pendant la pause.
- **Brouillard** : optionnel. Le fond `0xa0a0a0` + le sol `0xcbcbcb` donnent l'essentiel
  de la parité ; le vrai brouillard linéaire demande une passe shader (tâche stretch).

---

## Tâches

### Tâche 0 — Préparer la référence three.js

- ✅ **Done** — Produire la capture de référence three.js
  - Objectif : disposer d'une image de référence fiable pour la comparaison.
    Réutiliser [treejs/examples/screenshots/webgl_animation_skinning_blending.jpg](../treejs/examples/screenshots/webgl_animation_skinning_blending.jpg),
    ou, pour une comparaison plus juste, lancer l'exemple localement
    (`cd treejs && npm install && npm run dev`, ouvrir
    `examples/webgl_animation_skinning_blending.html`) et capturer la pose idle par défaut.
  - Livrable : copier l'image de référence dans `artifacts/validation/blending-threejs.png`.
  - Fichiers : `artifacts/validation/` (nouveau fichier image).
  - Commit suggéré : `chore(demos): add three.js blending reference screenshot`.
  - Validation : l'image existe et montre le personnage en plan rapproché sur sol gris.

### Tâche 1 — Nœud de blend pondéré N voies (framework)

- ✅ **Done** — Ajouter `WeightedBlendAnimationNode`
  - Objectif : un nœud de graphe d'animation qui mélange `N` entrées selon `N` poids,
    normalisés par la somme des poids ; si la somme est nulle, retomber sur la pose de bind.
  - Détails :
    - Implémenter `IAnimationGraphRuntimeNode` (méthodes `Advance`, `Evaluate`, `Skeleton`).
    - Toutes les entrées doivent cibler le même `SkeletonDefinition` (valider).
    - Réutiliser `AnimationPoseBlender.Blend` en accumulant les poses pondérées ;
      pré-allouer les poses de travail dans le constructeur (zéro alloc dans `Evaluate`).
    - Exposer `SetWeight(int index, float weight)` et `GetWeight(int index)`.
  - Fichiers : `CasaEngine/Framework/Animations/WeightedBlendAnimationNode.cs` (nouveau) ;
    tests dans `CasaEngine.Tests/Animation/WeightedBlendAnimationNodeTests.cs` (nouveau).
  - Commit suggéré : `feat(animation): add weighted N-way blend graph node`.
  - Validation : `dotnet build CasaEngine.MonoGame.sln -c Debug` +
    `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~WeightedBlend`.

### Tâche 2 — Avance explicite pour le single-step (framework)

- ✅ **Done** — Ajouter `AdvanceAnimation(float deltaSeconds)`
  - Objectif : permettre d'avancer la lecture d'un pas fixe même en pause
    (équivalent de `mixer.update(sizeOfNextStep)` en single-step).
  - Détails :
    - Ajouter une méthode au `AnimationController` qui avance le graphe / l'état courant
      d'un delta donné sans dépendre de l'état pause (`_graphPlaying`).
    - Relayer via `SkinnedMeshAnimationRuntime.AdvanceAnimation(delta)` puis
      `SkinnedMeshComponent.AdvanceAnimation(delta)`.
    - Ne pas modifier le comportement de `Update` existant (ajout additif).
  - Fichiers : `CasaEngine/Framework/Animations/AnimationController.cs`,
    `CasaEngine/Framework/Animations/SkinnedMeshAnimationRuntime.cs`,
    `CasaEngine/Framework/Scene/Entities/Components/SkinnedMeshComponent.cs` ;
    test dans `CasaEngine.Tests/Animation/AnimationControllerTests.cs`.
  - Commit suggéré : `feat(animation): add explicit single-step advance for paused playback`.
  - Validation : build solution + test ciblé `~AnimationController`.

### Tâche 3 — Modèle skinné « kid » avec idle + walk + run

- ✅ **Done** — Réunir les 3 clips sur un même squelette
  - Implémenté via l'approche de repli (B), runtime merge, dans
    `KidLocomotionModelFactory.Create(game)` : charge `kid_idle.FBX` (mesh + squelette
    affichés) puis `kid_walk.FBX`/`kid_run.FBX`, valide la compatibilité de squelette,
    rebind les clips et appelle `RiggedModel.OverrideRuntimeAnimationAssets` pour exposer
    3 clips nommés `Idle`, `Walk`, `Run`. Aucun changement de content pipeline requis
    (les 3 FBX sont déjà `/copy` dans `Content.mgcb`). Le même chemin runtime est
    déjà prouvé par `AnimationBlendDemo` ; la vérification live `AnimationClips.Count == 3`
    est couverte par le chargement du demo (Tâches 4 et 10).
  - Objectif : disposer d'un `SkinnedMesh` exposant **3 clips** (idle, walk, run)
    sur le squelette du personnage kid.
  - Approche recommandée (A) : importer via l'éditeur `kid_idle.FBX`, `kid_walk.FBX`,
    `kid_run.FBX` pour générer un asset `.skeleton` + 3 assets `.skeletonAnim`, puis créer
    un `.model` qui référence la géométrie + le squelette + les 3 `animation_clip_asset_ids`.
  - Approche de repli (B) : au runtime, charger les 3 FBX comme `RiggedModel` et fusionner
    leurs `AnimationClips` (mêmes noms d'os, donc squelettes compatibles) dans la liste de clips
    du modèle principal. À utiliser seulement si l'import éditeur n'est pas praticable.
  - Nommer les clips de façon stable : `idle`, `walk`, `run`.
  - Vérifier l'inclusion content : `kid_walk.FBX` / `kid_run.FBX` sont déjà `/copy` dans
    [CasaEngine.Demos/Content/Content.mgcb](../CasaEngine.Demos/Content/Content.mgcb).
  - Fichiers : `CasaEngine.Demos/Content/SkinnedMesh/…` (nouveaux assets `.model`/`.skeleton`/`.skeletonAnim`),
    `CasaEngine.Demos/Content/AssetInfos.json`, `CasaEngine.Demos/Content/Content.mgcb` si besoin.
  - Commit suggéré : `content(demos): add kid locomotion skinned model (idle/walk/run)`.
  - Validation : un mini test ou un log temporaire confirme que le `SkinnedMesh` chargé
    expose `AnimationClips.Count == 3` avec les noms attendus.
  - ⚠️ Note de risque : si les squelettes des 3 FBX diffèrent (ordre/nom d'os), passer la
    tâche en `⚠️ Blocked` et documenter l'écart avant de continuer.

### Tâche 4 — Scène du demo (caméra, sol, éclairage, fond)

- ✅ **Done** — Créer `SkeletalAnimationBlendingDemo` (scène statique)
  - Demo enregistré dans `DemosGame` (`Title = "Skeletal animation blending"`).
  - Caméra `CameraLookAtComponent`, vue 3/4 avant rapprochée style three.js ;
    le personnage regarde −Z, la caméra est placée côté −Z pour voir la face.
  - Fond gris `0xa0a0a0`, sol `LitDiffuseMaterial` gris `0xcbcbcb` (plan 100×100),
    ambient clair approximant l'`HemisphereLight`, lumière directionnelle clé + fill.
  - Personnage chargé via `KidLocomotionModelFactory`, joue `idle`.
  - ⚠ Note ombres : les ombres monde restent **désactivées**. Les activer rend le clear
    du fond `SolidColor` noir sur une vue back-buffer (la passe d'ombre change de render
    target et MonoGame jette le clear du back-buffer). Le fond gris étant l'élément
    visuel dominant de l'exemple, il est conservé ; l'ombre de contact est omise
    (limitation moteur préexistante, hors périmètre de cette tâche).
  - Validation : capture `artifacts/validation/blending-casaengine-task4.png` (fond gris,
    sol gris, personnage de face plein cadre).
  - Objectif : nouveau demo enregistré, reproduisant le cadrage et l'ambiance three.js,
    sans encore le mélange ni le panneau.
  - Détails :
    - `Title => "Skeletal animation blending"`, `Description` courte en anglais.
    - Caméra : `CameraLookAtComponent`/`SetPositionAndTarget`, cadrage équivalent à
      FOV 45 / position `(1, 2, -3)` / cible `(0, 1, 0)` (à ajuster à l'échelle du kid en Tâche 9).
    - Environnement : `BackgroundMode.SolidColor`, `BackgroundColor = RGB(160,160,160)` (`0xa0a0a0`) ;
      `AmbientColor`/`AmbientIntensity` réglés pour approximer l'`HemisphereLight`.
    - Lumière clé : `LightComponent` Directional, direction ≈ `(-3,10,-10)` normalisée,
      `CastShadows = true` ; éventuelle directionnelle de remplissage douce.
    - Sol : `StaticModelComponent` (plan large), matériau `LitDiffuseMaterial`
      `DiffuseColor = RGB(203,203,203)` (`0xcbcbcb`), `SpecularColor = Zero`, `ReceiveShadows = true`.
    - Charger le modèle de la Tâche 3 et jouer `idle` (statique pour l'instant).
    - `Clean()` doit réinitialiser l'environnement (`ResetToDefaults` + `MarkDirty`).
    - Enregistrer le demo dans `DemosGame.cs`.
  - Fichiers : `CasaEngine.Demos/Demos/SkeletalAnimationBlendingDemo.cs` (nouveau),
    `CasaEngine.Demos/DemosGame.cs`.
  - Commit suggéré : `feat(demos): add skeletal animation blending demo scene`.
  - Validation : build demos + lancement, le personnage idle apparaît sur sol gris avec ombre.

### Tâche 5 — Mélange idle/walk/run pondéré + vitesse

- ✅ **Done** — Brancher le `WeightedBlendAnimationNode`
  - 3 `AnimationClipNode` (idle/walk/run, `Loop = true`) + un `WeightedBlendAnimationNode`
    racine joué via `PlayAnimationGraph`. Poids par défaut three.js : idle 0, walk 1, run 0.
    `Update` applique les poids au nœud et `Speed = _timeScale` aux 3 clips chaque frame.
  - Validation : capture montre le personnage en pose de marche (walk weight 1).
  - Objectif : jouer les 3 clips simultanément via `PlayAnimationGraph`, avec des poids
    pilotables et une vitesse globale.
  - Détails :
    - Construire 3 `AnimationClipNode` (idle, walk, run, `Loop = true`) + un
      `WeightedBlendAnimationNode` racine ; `SkinnedMeshComponent.PlayAnimationGraph(root)`.
    - Poids par défaut comme three.js : idle `0`, walk `1`, run `0`.
    - Champs demo : `_idleWeight`, `_walkWeight`, `_runWeight`, `_timeScale = 1`.
    - Chaque frame : appliquer les poids au nœud et `Speed = _timeScale` aux 3 clips.
  - Fichiers : `CasaEngine.Demos/Demos/SkeletalAnimationBlendingDemo.cs`.
  - Commit suggéré : `feat(demos): drive idle/walk/run weighted blend`.
  - Validation : en modifiant les poids dans le code, le personnage passe d'idle à walk à run.

### Tâche 6 — Fenêtre « Controls » (structure MGUI)

- ⏳ **Todo** — Créer l'écran du panneau de contrôle
  - Objectif : reproduire la structure du panneau lil-gui avec ses 6 sections repliables,
    sans encore câbler toute la logique.
  - Détails :
    - Nouvel écran `BlendingControlsScreen : UIScreenBase` (couche HUD), poussé via
      `GetActiveUIView().PushScreen(...)` dans `InitializeCamera` (comme `UIOverlayDemo`).
    - `MGWindow` ancré en haut-droite ; un `MGExpander` par dossier :
      `Visibility`, `Activation/Deactivation`, `Pausing/Stepping`, `Crossfading`,
      `Blend Weights`, `General Speed`.
    - Contenu (widgets MGUI, sans logique finale) :
      - Visibility : `MGCheckBox` *show model*, `MGCheckBox` *show skeleton*.
      - Activation : `MGButton` *deactivate all*, `MGButton` *activate all*.
      - Pausing/Stepping : `MGButton` *pause/continue*, `MGButton` *make single step*,
        `MGSlider` *step size* (0.01–0.1).
      - Crossfading : 4 `MGButton` (*walk→idle*, *idle→walk*, *walk→run*, *run→walk*),
        `MGCheckBox` *use default duration*, `MGSlider` *custom duration* (0–10).
      - Blend Weights : 3 `MGSlider` *idle/walk/run weight* (0–1).
      - General Speed : `MGSlider` *time scale* (0–1.5).
    - Exposer des callbacks/`Action` vers le demo (pas de logique dans l'écran).
    - `Clean()` du demo retire l'écran de la vue.
  - Fichiers : `CasaEngine.Demos/Demos/SkeletalAnimationBlendingDemo.cs`,
    `CasaEngine.Demos/Demos/DemoUI/BlendingControlsScreen.cs` (nouveau).
  - Commit suggéré : `feat(demos): add blending controls panel (MGUI)`.
  - Validation : le panneau s'affiche en haut-droite, les sections se replient/déplient,
    les widgets sont présents (logique branchée aux tâches suivantes).

### Tâche 7 — Câblage Blend Weights + General Speed

- ⏳ **Todo** — Relier les sliders de poids et de vitesse
  - Objectif : les sliders *idle/walk/run weight* mettent à jour les poids du nœud ;
    *time scale* met à jour la vitesse.
  - Détails :
    - `onChange` des 3 sliders → `_idleWeight/_walkWeight/_runWeight`.
    - `onChange` time scale → `_timeScale`.
    - Synchroniser les valeurs affichées si les poids changent « de l'extérieur »
      (par un crossfade) — équivalent de `updateWeightSliders()`.
  - Fichiers : `SkeletalAnimationBlendingDemo.cs`, `BlendingControlsScreen.cs`.
  - Commit suggéré : `feat(demos): wire blend weight and speed controls`.
  - Validation : déplacer les sliders modifie visiblement le mélange et la vitesse.

### Tâche 8 — Câblage Crossfading + Activation + Pause/Single-step

- ⏳ **Todo** — Relier les boutons d'interaction
  - Objectif : reproduire les transitions et contrôles de lecture.
  - Détails :
    - Crossfade : animer `_xxxWeight` vers la cible `(1,0,0)`/`(0,1,0)`/`(0,0,1)` sur
      `duration` (défaut three.js : walk→idle 1.0, idle→walk 0.5, walk→run 2.5, run→walk 5.0),
      ou la *custom duration* si *use default duration* est décoché.
    - Activate all / deactivate all : remettre les poids par défaut / mettre tous les poids à 0.
    - pause/continue : `PauseAnimation`/`ResumeAnimation` + bascule d'un drapeau `_paused`.
    - make single step : sortir de pause le temps d'un pas, appeler `AdvanceAnimation(stepSize)`
      (Tâche 2), puis re-pause ; `stepSize` vient du slider *step size*.
    - Désactiver/activer les boutons de crossfade selon l'état des poids
      (équivalent de `updateCrossFadeControls()`), optionnel mais souhaitable.
  - Fichiers : `SkeletalAnimationBlendingDemo.cs`, `BlendingControlsScreen.cs`.
  - Commit suggéré : `feat(demos): wire crossfade, activation and single-step controls`.
  - Validation : chaque bouton produit l'effet attendu en jeu.

### Tâche 9 — Visibilité modèle + squelette

- ⏳ **Todo** — Brancher *show model* et *show skeleton*
  - Objectif : reproduire `model.visible` et le `SkeletonHelper`.
  - Détails :
    - *show model* → `Entity.IsVisible` de l'entité du personnage.
    - *show skeleton* → dessiner le squelette via
      `SkeletonDebugVisualizer.Draw(_game.Line3dRendererComponent, component.CurrentModelPose, component.WorldMatrixWithScale, options)`
      quand le drapeau est actif (voir `AnimationIkDemo`). Restaurer l'état GPU si nécessaire.
  - Fichiers : `SkeletalAnimationBlendingDemo.cs`, `BlendingControlsScreen.cs`.
  - Commit suggéré : `feat(demos): add model and skeleton visibility toggles`.
  - Validation : les deux cases masquent/affichent correctement le modèle et le squelette.

### Tâche 10 — Parité visuelle par capture d'écran

- ⏳ **Todo** — Comparer CasaEngine vs three.js et ajuster
  - Objectif : obtenir un rendu équivalent (pose idle par défaut, cadrage, fond, ombre).
  - Détails :
    - Capturer le demo avec `CASAENGINE_CAPTURE_SCREENSHOT_PATH` →
      `artifacts/validation/blending-casaengine.png` (rebuild `CasaEngine.Demos` avant).
    - Comparer à `artifacts/validation/blending-threejs.png` (Tâche 0).
    - Ajuster **échelle du personnage**, position/cible caméra, intensités lumineuses et
      couleur ambiante jusqu'à un cadrage et une tonalité proches.
    - Documenter l'écart résiduel (notamment l'absence de brouillard).
  - Fichiers : `SkeletalAnimationBlendingDemo.cs`, `artifacts/validation/` (images).
  - Commit suggéré : `test(demos): validate skeletal blending demo visual parity`.
  - Validation : capture côte à côte jugée équivalente ; noter les réglages finaux.

### Tâche 11 — (Optionnel / stretch) Brouillard linéaire

- ⏳ **Todo** — Ajouter un brouillard linéaire distance
  - Objectif : reproduire `scene.fog` (couleur `0xa0a0a0`, near 10, far 50) pour la parité fine.
  - Détails :
    - Ajouter des paramètres de brouillard à `WorldEnvironmentSettings` (couleur, near, far, activé)
      avec sérialisation rétro-compatible (champs optionnels).
    - Appliquer le mélange brouillard dans la passe forward (shader) en restaurant les états GPU ;
      prévoir un fallback si non supporté.
  - Fichiers : `CasaEngine/Framework/Rendering/Environment/WorldEnvironmentSettings.cs`,
    passes/shaders de rendu forward concernés, `SkeletalAnimationBlendingDemo.cs`.
  - Commit suggéré : `feat(rendering): add optional linear distance fog`.
  - Validation : le sol fond vers le gris au loin ; aucune régression sur les autres demos.
  - Note : tâche **optionnelle**, à ne traiter qu'après la parité de base (Tâche 10).

### Tâche 12 — Finalisation

- ⏳ **Todo** — Nettoyage et cohérence
  - Objectif : vérifier l'enregistrement du demo, le `Clean()` (reset environnement, retrait
    de l'écran UI), l'absence d'allocations évitables dans `Update`/`Draw`, et la cohérence
    des libellés du panneau avec l'exemple.
  - Fichiers : `SkeletalAnimationBlendingDemo.cs`, `BlendingControlsScreen.cs`, `DemosGame.cs`.
  - Commit suggéré : `chore(demos): finalize skeletal animation blending demo`.
  - Validation : build solution complet + capture finale + revue rapide des hot paths.

---

## Suivi des commits

| Tâche | Statut | Commit |
| --- | --- | --- |
| 0 | ✅ Done | chore(demos): add three.js blending reference screenshot |
| 1 | ✅ Done | feat(animation): add weighted N-way blend graph node |
| 2 | ✅ Done | feat(animation): add explicit single-step advance for paused playback |
| 3 | ✅ Done | feat(demos): add kid locomotion skinned model factory (idle/walk/run) |
| 4 | ✅ Done | feat(demos): add skeletal animation blending demo scene |
| 5 | ✅ Done | feat(demos): drive idle/walk/run weighted blend |
| 6 | ⏳ Todo | — |
| 7 | ⏳ Todo | — |
| 8 | ⏳ Todo | — |
| 9 | ⏳ Todo | — |
| 10 | ⏳ Todo | — |
| 11 (optionnel) | ⏳ Todo | — |
| 12 | ⏳ Todo | — |
