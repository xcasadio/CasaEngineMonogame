Les trois projets compilent sans erreur. J'ai maintenant tout ce qu'il faut pour le verdict.

# Analyse de la qualité des démos (CasaEngine.Demos et Projects)

Verdict global : `CasaEngine.Demos` est **bien construit** — c'est un vrai harnais de démonstration avec une infrastructure soignée, même si les démos les plus anciennes n'ont pas été mises au niveau des récentes. Le dossier `Projects` est en revanche **très hétérogène** : deux templates de données corrects, un projet RPG légataire truffé de code mort, et un bac à sable (`SandBoxGame`) qui compile mais planterait au lancement.

## CasaEngine.Demos — solide, avec un écart ancien/récent

Ce qui est bien construit :

- **Vraie infrastructure de démos** : la classe abstraite [Demo.cs](CasaEngine.Demos/Demo.cs) définit un cycle de vie complet (`Initialize` / `ConfigureSceneLighting` / `CreateCamera` / `InitializeCamera` / `Update` / `PostDraw` / `OnScreenResized` / `Clean`), avec des défauts sensés (caméra arc-ball, rig d'éclairage 3 points factorisé dans [DemoSceneLightRig.cs](CasaEngine.Demos/DemoSceneLightRig.cs)). 19 démos couvrent quasiment tout le moteur : physique 2D/3D, matériaux, particules, environnement, skinning, IK, blend d'animations, split-screen, render-to-texture, UI overlay et world-space, cutscenes. C'est conforme à la règle d'AGENTS.md (« un sample minimal par feature non triviale »).
- **Support d'automatisation remarquable** : [DemosGame.cs](CasaEngine.Demos/DemosGame.cs) accepte `CASAENGINE_START_DEMO` (index, titre exact ou partiel), `CASAENGINE_CAPTURE_SCREENSHOT_PATH`/`_DELAY_MS` (capture puis exit code 0/1) et `CASAENGINE_SHOW_DEBUG_OVERLAY`. Les démos sont donc testables visuellement en CI sans interaction — c'est rare et précieux.
- **Navigation intégrée** : panneau d'info des démos (F1), titres et descriptions systématiques, changement de démo à chaud avec re-création des vues et de l'UI.
- **Les démos récentes sont exemplaires** : [SkinnedMeshDemo.cs](CasaEngine.Demos/Demos/SkinnedMeshDemo.cs) et [MaterialDemo.cs](CasaEngine.Demos/Demos/MaterialDemo.cs) restaurent l'état global dans `Clean()` (environnement du monde, pipeline de vue, cubemap), [RenderToTextureDemo](CasaEngine.Demos/Demos/RenderToTextureDemo.cs:154) et [ViewManagerSandbox](CasaEngine.Demos/Demos/ViewManagerSandbox.cs:338) disposent leurs surfaces RT et nettoient le ViewManager, [AnimationBlendDemo](CasaEngine.Demos/Demos/AnimationBlendDemo.cs:257) se désabonne de ses événements. Elles sont aussi pensées pour *valider* le moteur (colonnes cast/receive shadows, vues asymétriques pour vérifier les stats par vue).
- **Compilation : 0 erreur** (vérifié).

Ce qui l'est moins :

| Problème | Où | Impact |
|---|---|---|
| `Clean()` vides malgré des ressources GPU créées | [Collision3dBasicDemo](CasaEngine.Demos/Demos/Collision3dBasicDemo.cs:95), Collision2d, StaticModel, TileMap | Les `Texture2D.FromFile` et checker-textures ne sont jamais disposées ; `World.ClearEntities()` ne libère rien → fuite GPU à chaque changement de démo |
| Démo désactivée en silence | `//_demos.Add(new TileMapDemo())` ([DemosGame.cs:77](CasaEngine.Demos/DemosGame.cs:77)) | Les assets existent pourtant dans `Content/Maps` ; démo probablement cassée, sans commentaire expliquant pourquoi |
| Resize non géré en split-screen | [SplitScreenDemo](CasaEngine.Demos/Demos/SplitScreenDemo.cs) n'override pas `OnScreenResized` | Le hook a été créé précisément pour ça ; les rects des deux vues restent figés après redimensionnement |
| 90 warnings | tout le projet | Annotations `?` sans `<Nullable>enable</Nullable>` (CS8632) et usage massif de l'API obsolète `LoadDirectly` (CS0618, marquée « Used only for neoforce controls ») |
| Duplication | `CreateOrientationFromForward` copié dans `DemoSceneLightRig` et `SkinnedMeshDemo` | Devrait être un helper du framework (convertir une direction en orientation de lumière est un besoin générique) |
| Style | indentation cassée ([DemosGame.cs:70](CasaEngine.Demos/DemosGame.cs:70), [SkinnedMeshDemo.cs:192](CasaEngine.Demos/Demos/SkinnedMeshDemo.cs:192)), `TileMapDemo.LoadSprites` charge *tous* les sprites du catalogue dans une variable inutilisée | Cosmétique |

## Projects — un template propre, deux projets légataires

- **`Projects/CasaEngine.RPGDemo`** (plugin gameplay dans la solution) : l'architecture est saine sur le papier — contrôleurs avec machines à états (`PlayerState`, `EnemyState`, `CpuPlayerState`), armes, scripts liés aux entités, et un post-build qui copie la DLL dans le projet de données `RPGDemo`. Mais le code est visiblement une migration inachevée d'une ancienne version : blocs entiers commentés partout ([EnemyHuntState.cs](Projects/CasaEngine.RPGDemo/Controllers/EnemyState/EnemyHuntState.cs) est à moitié mort, [Character.cs](Projects/CasaEngine.RPGDemo/Controllers/Character.cs) contient des méthodes vides `Load`/`SetPosition`), `Plugin.Initialize()` vide, champ `CpuPlayerController.world` jamais assigné (warning CS0649 = toujours null). Et **la DLL + le PDB compilés sont committés dans git** ([Projects/RPGDemo/CasaEngine.RPGDemo.dll](Projects/RPGDemo/CasaEngine.RPGDemo.dll)) — un artefact de build versionné qui se désynchronisera des sources.
- **`Projects/RPGDemo`** : projet de *données* (464 fichiers : maps, tilesets, entités, écrans, skins) destiné à l'éditeur/launcher. Cohérent dans son rôle, mais il embarque des shaders `.fx` locaux (`simple.fx`, `spritebatch.fx`) qui datent de l'ancien pipeline et ne correspondent plus aux shaders intégrés actuels.
- **`Projects/EmptyProject` et `Projects/SampleProject`** : templates de données pour l'éditeur (monde par défaut, `AssetInfos.json`, et pour SampleProject des écrans XAML, particules, layouts `.casaeditor`). Rien à redire pour leur usage.
- **`Projects/SandBoxGame`** : c'est le point noir. Il est dans la solution principale et compile, mais [SandBoxGame.cs](Projects/SandBoxGame/SandBoxGame.cs) planterait dès la première frame : `_effectColor`/`_effectTexture` ne sont jamais assignés (toute l'initialisation est en commentaire) et `Draw` les déréférence via `DrawBoxWithEffect(...)` → `NullReferenceException` garantie à la ligne 176. Il alloue aussi un `SamplerState` **par frame** dans `Draw` — en contradiction directe avec la règle « hot path : zéro alloc » d'AGENTS.md. Les deux tiers du fichier sont du code mort. À réparer en sample minimal honnête, ou à retirer de la solution.

## Recommandations par priorité

1. Corriger ou retirer `SandBoxGame` (crash au lancement, dans la solution principale — quiconque le lance pour découvrir le moteur tombe sur une exception).
2. Donner un vrai `Clean()` aux quatre vieilles démos (disposer les textures créées) et réactiver ou supprimer `TileMapDemo` avec un commentaire expliquant son état.
3. Sortir `CasaEngine.RPGDemo.dll`/`.pdb` de git (le post-build les régénère) et purger le code commenté du RPGDemo.
4. Activer `<Nullable>enable</Nullable>` sur `CasaEngine.Demos` (le code utilise déjà les annotations) et remplacer `LoadDirectly` obsolète par le chemin de chargement recommandé.

Si vous voulez, je peux appliquer les points 1 et 2 directement.