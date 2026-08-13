# Analyse — Play-in-Editor (tester un niveau depuis l'éditeur)

Date : 2026-08-13. Analyse en lecture seule, aucun code modifié.

## Objectif

Permettre, comme dans les moteurs modernes (Unity, Unreal, Godot), de lancer le niveau en cours
d'édition directement dans le viewport de l'éditeur : Play / Pause / Stop, avec :

- exécution réelle du gameplay (scripts, physique, animations, input joueur) ;
- retour à l'état d'édition exact après Stop (aucune modification runtime persistée) ;
- prise en compte des scripts C# du projet : compilation d'une DLL à la volée,
  chargement avant Play, déchargement/rechargement après modification des sources.

## Constats vérifiés dans le dépôt

### Hébergement du runtime dans l'éditeur

- L'éditeur (`GameEditor : Game`) héberge un vrai `CasaEngineGame` via
  [HostedEditorGameAdapter.cs](../../CasaEngine.Editor/Runtime/HostedEditorGameAdapter.cs)
  (partage du GraphicsDevice, `InitializeHost/UpdateHost/DrawHost`).
- Il est configuré en `GameplayExecutionPolicies.EditorPreview`
  ([GameEditor.cs:970-981](../../CasaEngine.Editor/GameEditor.cs)).
- Les panneaux d'édition (EntitiesPanel, inspecteurs, sauvegarde) travaillent **directement sur
  `GameManager.CurrentWorld` du runtime hébergé** — il n'y a qu'une seule instance de World,
  à la fois document d'édition et monde affiché.

### Politiques d'exécution : la base est déjà là

[GameplayExecutionPolicy.cs](../../CasaEngine/Framework/Application/GameplayExecutionPolicy.cs)
définit trois politiques :

- `Runtime` : jeu standalone complet ;
- `EditorPreview` : politique actuelle de l'éditeur — pas de BeginPlay, pas de scripts,
  pas de physique, pas de player controllers ;
- `EditorSimulation` : **définie mais jamais utilisée** (aucune référence dans le dépôt).
  Elle décrit exactement le mode Play : gameplay + physique + controllers actifs, mais
  `UseExternalViewManagement = true` (les vues restent gérées par l'éditeur).

Les gates sont de deux natures :

- **par frame** : `World.Update` (l.422), `Entity.Update` (l.485),
  `PhysicsBaseComponent.Update` (l.136), `AnimatedSpriteComponent` (l.174-179),
  `PhysicsSystemComponent.Update` (l.58) → basculer la policy à chaud suffit ;
- **au chargement** : `World.LoadContent` (l.230-248, `InitializePlayerControllers`,
  `InitializeGameplayOnLoad`) et `World.BeginPlay` (l.307, `RunBeginPlay`)
  → un simple changement de policy ne suffit PAS, il faut repasser par
  LoadContent + BeginPlay, donc par un (re)chargement de monde.

### Cycle de vie du monde

- `GameManager.SetWorldToLoad(string)` recharge depuis l'asset (cache:false) et fait
  `Clear()` de l'ancien monde ; `SetWorldToLoad(World)` **remplace le monde courant sans
  le détruire** ([GameManager.cs:119-128](../../CasaEngine/Framework/Application/GameManager.cs)) —
  c'est le point d'entrée naturel pour un swap monde-édition ↔ monde-play.
- Le chemin `_isNewWorld` fait `LoadContent + BeginPlay` et notifie `WorldLoaded`/`WorldChanged`
  (l.88-109) — les panneaux peuvent donc suivre le swap.
- `World.Clear()` notifie `OnEndPlay`, stoppe le GameplayModeRunner, libère UI/tilemaps/physique
  ([World.cs:101-129](../../CasaEngine/Framework/Scene/World/World.cs)).
- La physique est par-monde (`PhysicsSystemComponent.GetOrCreateContext(world)`), donc deux
  mondes peuvent coexister sans conflit.
- `GameManager.TimeScale` existe déjà (pause = 0, avec `FrameTime` scalé) — base pour
  Pause/ralentis ; le pas-à-pas (frame step) reste à ajouter.
- Sérialisation en mémoire disponible : `EditorEntityJsonSerializer.SaveWorld(world, jObject)`
  ([EditorWorldWriter.cs](../../CasaEngine.EditorServices/EditorWorldWriter.cs)) — un snapshot
  ne nécessite pas d'écrire sur disque.

### Caméra, vues, input, UI

- Le viewport ([WorldViewportPanel.cs](../../CasaEngine.Editor/Controls/WorldViewportPanel.cs))
  possède ses caméras d'édition (ArcBall / 2D) et impose `_renderView.Camera = ActiveCamera` ;
  il expose déjà `SetWorldOverride(World)` (utilisé par les previews d'assets).
- En standalone, la caméra de jeu est choisie par
  [DefaultRuntimeViewBootstrapper.cs](../../CasaEngine/Framework/Application/DefaultRuntimeViewBootstrapper.cs)
  (premier `CameraComponent` du monde) — ce bootstrapper ne tourne pas en mode
  `UseExternalViewManagement` : en Play, l'éditeur devra lui-même brancher la caméra du monde
  sur la RenderView du viewport.
- Le routage input est déjà unifié éditeur/jeu (`InputRouter`, `ViewInputContext`,
  `GameManager.SyncPlayerViewAssignments` qui assigne joueur → vue ;
  cf. [editor-input-routing-architecture.md](../../docs/editor/editor-input-routing-architecture.md)).
  MGUI expose `ShouldCaptureGameplayInput` (utilisé dans GameEditor l.5163).
- L'UI de jeu est par-vue : `view.UIView = UIViewRuntimeFactory.Create(...)`
  ([CasaEngineGame.cs:637](../../CasaEngine/Framework/Application/CasaEngineGame.cs)) et
  `EngineRuntimeContext` fournit `MguiViewRuntimeFactory` par défaut — le HUD en Play est
  donc atteignable, mais à valider (vue créée par l'éditeur, pas par le runtime).

### Pipeline de scripts actuel

- Les scripts sont référencés **par nom de classe** dans les données sérialisées
  (`script_class_name` → `GameplayProxyClassName`, World.cs l.779, Entity.cs l.563) et
  instanciés via `ElementFactory.Create<GameplayProxy>` — excellente propriété pour le
  reload : rien d'autre que des strings dans les assets.
- La création des proxies n'est **pas** conditionnée par la policy : même en EditorPreview,
  `Entity.InitializePrivate` (l.197-202) et `World.LoadContent` (l.235-238) instancient les
  types du DLL de gameplay. Le monde d'édition retient donc des instances des types scripts.
- Chargement actuel du DLL : `ProjectSettings.GameplayDllName` →
  `AssemblyManager.Load` → **`Assembly.LoadFile` dans l'ALC par défaut**
  ([AssemblyManager.cs](../../CasaEngine/Engine/Plugins/AssemblyManager.cs),
  [ProjectSettingsHelper.cs:37-40](../../CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs)).
  Conséquences : fichier verrouillé sur disque, aucun déchargement possible, un
  rechargement du même chemin renvoie l'ancienne assembly. **L'éditeur passe par ce même
  chemin à l'ouverture du projet** (`EditorProjectAuthoringService.LoadProject`) — en l'état,
  impossible de recompiler le DLL pendant que le projet est ouvert.
- `ElementFactory` ([ElementFactory.cs](../../CasaEngine/Framework/Assets/ElementFactory.cs))
  cache tous les types de l'AppDomain par nom simple, se reconstruit sur `AssemblyLoad`,
  mais n'a **aucune gestion du déchargement** : les `Type` retenus épingleraient un ALC
  collectible pour toujours.
- `CasaEngine.DotNetCompiler` (Roslyn) existe mais vise les scripts « corps de méthode »
  dynamiques (`Assembly.Load(bytes)` non déchargeable) — ce n'est pas le pipeline d'un DLL
  de gameplay projet (pas de csproj, pas de NuGet, pas de PDB émis).
- `CreateProject` ne génère aucun csproj de scripts ; seuls les projets exemples
  (ex. `Projects/CasaEngine.RPGDemo.csproj`, compilé dans la solution) fournissent un DLL.
- Aucune invocation de `dotnet build`/MSBuild dans l'éditeur aujourd'hui.

## Ce qu'il faut ajouter

### 1. Machine à états Play Mode (EditorServices + Editor)

Nouveau service `EditorPlayModeService` (côté `CasaEngine.EditorServices`, orchestration UI
dans `GameEditor`) :

- États : `Editing → Starting → Playing ⇄ Paused → Stopping → Editing` + événements.
- Toolbar Play/Pause/Stop/Step sur le document World (icônes + raccourcis, ex. F5 / Maj+F5),
  teinte visuelle du viewport en Play (repère « on est en runtime »).
- Verrouillages en Play : sauvegarde monde/projet désactivée, undo/redo suspendu
  (`EditorHistoryService`), édition inspecteur soit bloquée soit marquée « non persistée ».

### 2. Isolation de session : jouer une copie, jamais le document

Recommandation (modèle Unity « serialize → copy ») :

1. Au Play : sérialiser le monde d'édition en `JObject` en mémoire
   (`EditorEntityJsonSerializer.SaveWorld`), créer un `World` neuf depuis ce JSON
   (mêmes chemins de chargement que le runtime), garder le monde d'édition de côté
   **sans le Clear**.
2. Basculer `ExecutionPolicy = EditorSimulation` sur le runtime hébergé, puis
   `GameManager.SetWorldToLoad(playWorld)` → le chemin `_isNewWorld` exécute
   `LoadContent` (player controllers, proxies) + `BeginPlay` naturellement.
3. Au Stop : `playWorld.Clear()`, rebasculer en `EditorPreview`,
   `SetWorldToLoad(editWorld)` sans re-BeginPlay (gate `RunBeginPlay` déjà en place),
   restaurer caméra/sélection d'édition.

Avantages : restauration d'état garantie par construction (le document n'est jamais touché),
test réel du chemin de sérialisation (ce que verra le jeu), physique isolée par-monde.
Les panneaux suivent `WorldChanged` et affichent la hiérarchie runtime pendant le Play
(comportement standard des moteurs modernes).

Alternative écartée : jouer « en place » et restaurer par rechargement JSON — plus simple en
apparence mais toutes les références des panneaux/sélection/history deviennent invalides au
Stop, et un échec de restauration corrompt le document.

### 3. Caméra, input, HUD en Play

- **Caméra** : au Play, brancher sur la RenderView du viewport la caméra du monde joué
  (même règle que `DefaultRuntimeViewBootstrapper` : premier `CameraComponent`, sinon
  `CreateDefaultCamera`) ; au Stop, restaurer la caméra d'édition (le viewport sauvegarde
  déjà des états caméra pour les previews). Option phase 3 : bouton « eject » pour reprendre
  la caméra libre pendant le Play.
- **Input** : au Play, `SyncPlayerViewAssignments` assigne le joueur à la vue du viewport ;
  le clic dans le viewport doit donner le focus gameplay (mécanisme
  `ShouldCaptureGameplayInput` existant), Échap rend le focus à l'éditeur.
  Souris capturée/relative à traiter si un jeu FPS-like l'exige (hors V1).
- **HUD/UI de jeu** : la vue de l'éditeur doit recevoir un `UIView` créé par
  `UIViewRuntimeFactory` comme en standalone — à valider ; sinon les
  `GameScreenManager`/screens ne s'afficheront pas en Play. Acceptable en phase 2.

### 4. Pipeline scripts : compilation + chargement/déchargement (demande explicite)

C'est le plus gros morceau. Quatre briques :

**a. Projet de scripts.** Convention : un csproj de gameplay dans le dossier projet
(généré par `CreateProject` pour les nouveaux projets ; chemin configurable dans
`ProjectSettings` pour l'existant, à côté de `GameplayDllName`). Cible `net8`-window,
référence `CasaEngine.dll` de l'éditeur.

**b. Service de build.** `EditorScriptBuildService` : lance `dotnet build` hors-process
(fiable : sémantique csproj, NuGet, PDB, diagnostics MSBuild), sortie dans un dossier
horodaté `.casaeditor/script-build/{n}/` (jamais deux builds au même chemin → pas de
verrou), erreurs remontées dans le LogsPanel avec fichier:ligne. Roslyn in-proc
(`CasaEngine.DotNetCompiler`) reste une option future pour du « quick script » mono-fichier,
pas pour le DLL projet.

**c. Hôte d'assembly déchargeable.** Nouveau `ScriptAssemblyHost` (runtime, remplace
`AssemblyManager` côté éditeur) :

- `AssemblyLoadContext(isCollectible: true)` + `AssemblyDependencyResolver` sur le dossier
  de build ;
- **règle d'identité de types** : tout ce qui est `CasaEngine.*`/MonoGame doit être résolu
  vers l'ALC par défaut (retourner null dans `Load()`), sinon le `GameplayProxy` du script
  n'est pas assignable au `GameplayProxy` du moteur ;
- `Unload()` vérifié par `WeakReference` + GC (diagnostic si l'ALC ne meurt pas) ;
- le runtime standalone garde un chemin simple (pas besoin de décharger en jeu final),
  mais devrait migrer de `Assembly.LoadFile` vers ce même hôte pour unifier.

**d. Orchestration du reload.** Séquence sur Play (ou bouton « Compile ») quand les sources
ont changé :

1. build → si échec : rester en édition, afficher les erreurs, ne pas lancer le Play ;
2. détruire toute instance des anciens types : Clear du monde de play s'il existe, et
   **recharger le monde d'édition** (les proxies sont instanciés même en preview — cf.
   constats) ; purger les caches d'assets retenant des entités avec proxies ;
3. invalider `ElementFactory` (nouvelle API d'enregistrement/désenregistrement par ALC —
   le scan AppDomain actuel doit exclure les assemblies en cours de déchargement) ;
4. `Unload()` de l'ancien ALC, chargement du nouveau build, reconstruction des caches ;
5. recréation des proxies via les `script_class_name` (déjà le design : aucune donnée à
   migrer).

Un `FileSystemWatcher` sur les sources (indicateur « scripts obsolètes », build auto au
Play) est un confort de phase 2/3.

### 5. Garde-fous

- Exception dans un script en Play : aujourd'hui `Entity.Update → GameplayProxy.Update`
  remonterait jusqu'à la boucle de l'éditeur et le tuerait. En Play, encadrer l'update
  gameplay : stopper le Play proprement + afficher l'erreur (politique « fail-stop »,
  pas de catch silencieux par frame).
- `GameSettings`/`EngineEnvironment`/`AssetCatalog` sont des statiques globaux : un seul
  projet/monde de play à la fois — contrainte acceptable, à documenter.
- Interdire la fermeture du projet/du document World pendant le Play (ou forcer Stop).

## Phasage proposé

**Phase 1 — Play/Stop MVP (sans recompilation)**
Toolbar + `EditorPlayModeService` + snapshot/copie du monde + bascule
`EditorSimulation`/`EditorPreview` + caméra de jeu + input joueur dans le viewport +
garde-fous (save/undo/exceptions). Les scripts utilisent le DLL déjà chargé à l'ouverture
du projet (comportement actuel, non rechargeable). Valeur immédiate : tester la physique,
les controllers, le gameplay data-driven.

**Phase 2 — Scripts à la volée**
`ScriptAssemblyHost` (ALC collectible) + retrait du `Assembly.LoadFile` côté éditeur +
`EditorScriptBuildService` (dotnet build) + rework `ElementFactory` (désenregistrement) +
orchestration reload complète + erreurs de build dans le LogsPanel.

**Phase 3 — Confort**
Pause/TimeScale UI + frame step, caméra « eject », HUD/UI screens dans le viewport,
watcher de sources + build auto, édition d'inspecteur « runtime only » pendant le Play,
« play from here » (spawn du pawn à la position caméra).

## Fichiers principaux impactés (estimation)

| Zone | Fichiers |
| --- | --- |
| Nouveau | `CasaEngine.EditorServices/PlayMode/EditorPlayModeService.cs`, `.../EditorScriptBuildService.cs`, `CasaEngine/Engine/Plugins/ScriptAssemblyHost.cs` |
| Éditeur | `GameEditor.cs` (toolbar, orchestration, verrous), `WorldViewportPanel.cs` (caméra de jeu, focus input, teinte Play), `LogsPanel` (erreurs build) |
| Runtime | `ElementFactory.cs` (invalidation par ALC), `ProjectSettingsHelper.cs` (ne plus LoadFile côté éditeur), `GameManager.cs` (frame step éventuel), `Entity.cs`/`World.cs` (fail-stop scripts en Play, si retenu) |
| Projet | `EditorProjectAuthoringService.CreateProject` (scaffold csproj scripts), `ProjectSettings` (chemin csproj) |

## Risques principaux

1. **Fuite d'ALC** (types épinglés par `ElementFactory`, caches d'assets, événements,
   singletons) → le reload « marche une fois puis fuit ». Mitigation : WeakReference +
   diagnostic systématique, inventaire des caches retenant des types scripts.
2. **Identité de types** entre ALC script et ALC défaut → erreurs de cast obscures.
   Mitigation : résolution stricte « moteur = ALC défaut » + test dédié.
3. **Restauration d'état** : le swap de monde doit remettre panneaux, sélection, gizmos,
   caméra et physique exactement comme avant — c'est l'essentiel de la valeur perçue.
4. **Sérialisation incomplète** : jouer une copie sérialisée révélera tout champ non
   sérialisé (c'est aussi un bénéfice : le Play teste le format de sauvegarde).
5. Statiques globaux (`GameSettings`, `AssetCatalog`) : pas bloquants pour un seul monde
   de play, mais interdisent le multi-instance (non demandé).
