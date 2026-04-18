# Systeme d'animation moderne complet - Plan d'implementation

## Objectif

Faire evoluer CasaEngine d'un systeme de skinned mesh base sur `RiggedModel` + lecture d'un clip unique vers un systeme d'animation 3D moderne, modulaire, extensible et compatible editor/runtime.

Decision de cadrage du 2026-04-17 :

- pas de retrocompatibilite recherchee avec l'ancien runtime d'animation
- les systemes legacy peuvent etre remplaces directement si le build, les tests et la demo de validation restent sains
- `RiggedModel` peut etre conserve temporairement comme conteneur de geometrie/import, mais il ne doit plus dicter l'architecture runtime cible

La cible finale doit couvrir :

- import separe des donnees `skeleton`, `skin`, `clip`, `graph` et profils de retargeting
- sampling robuste des clips avec interpolation stable et cache de poses
- blend moderne : crossfade, blend tree, blend space 1D/2D, couches, masques osseux, additive
- root motion, animation events et synchronisation gameplay
- skinning GPU propre avec support `Linear Blend Skinning` et `Dual Quaternion Skinning`
- support des morph targets / blend shapes et autres deformers quand la source les fournit
- contraintes runtime : IK simple, constraints de bones et look-at
- bascule franche du runtime legacy vers la nouvelle pile d'animation

## Etat de depart verifie

L'existant dans le depot est le suivant :

- import runtime via Assimp vers `RiggedModel`
- `SkinnedMesh` est un wrapper JSON pointant vers un asset de modele rigge
- animation actuelle = un seul clip actif a la fois, joue par index
- interpolation actuelle = `Quaternion.Slerp` pour la rotation et `Vector3.Lerp` pour translation/scale
- skinning actuel = matrices d'os envoyees a `skinEffect.fx`
- 4 influences max par vertex
- pas de controller d'animation, pas de crossfade, pas de layers, pas d'additive, pas de root motion, pas de retargeting, pas d'IK, pas de dual quaternion
- les mesh animations / morph channels sont detectes au chargement mais pas joues
- le rendu skinned utilise encore des tableaux CPU avec `DrawUserIndexedPrimitives`

## Principes d'architecture

1. Separer strictement authoring, runtime CPU et runtime GPU.
2. Eviter toute logique de blend ou d'IK directement dans `RiggedModel`.
3. Introduire des representations de pose explicites, reutilisables et testables.
4. Remplacer le runtime legacy des que le nouveau chemin est valide, sans conserver de branches de compatibilite inutiles.
5. Ne pas introduire d'allocations evitables dans les hot paths `Update` et `Draw`.
6. Rendre chaque phase demonstrable par une demo ou un sample.

## Strategie de reference externe issue de l'analyse comparee

Source d'arbitrage : [animation-example-analysis-report.md](animation-example-analysis-report.md)

- Base runtime prioritaire : `GameAnimationProgramming` pour `Transform`, `Pose`, `Skeleton`, `Clip`, `Track`, `TransformTrack`, sampling, blend, additive, crossfade simple, `DualQuaternion`, `CCDSolver` et `FABRIKSolver`.
- Base architecture prioritaire : `DigitalRune` pour `AnimationManager`, `IAnimationService`, `AnimationController`, transitions, `TimelineGroup`, `BlendGroup`, `SkeletonMapper`, IK multiple et compression.
- Ne pas porter tel quel : la couche OpenGL / windowing / shaders GLSL de Gabor, ni le content pipeline XNA/MonoGame, ni le systeme de services globaux, ni les types de scene trop couples a `DigitalRune`.
- Regle de choix : coeur runtime et maths d'abord depuis `GameAnimationProgramming`, couches moteur et cas avances ensuite depuis `DigitalRune`.
- Regle de licence : code `GameAnimationProgramming` sous MIT, code `DigitalRune` sous BSD-3-Clause. Les assets `DigitalRune` ont des licences heterogenes et doivent etre verifies asset par asset. Les assets `GameAnimationProgramming` restent des assets de validation interne tant que leur provenance detaillee n'a pas ete revalidee.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin de chaque tache, remplacer l'icone par `✅`, `🧪` ou `⚠️`.
4. Chaque tache doit laisser le repo dans un etat compilable.
5. Commiter le code et la mise a jour de ce plan dans le meme commit.
6. Si une hypothese de ce document s'avere fausse apres relecture du code, documenter la conclusion et ajuster la tache au lieu de forcer un refactor inadapté.
7. Toute nouveaute visible doit avoir une demo ou une validation manuelle cible.
8. Toute logique pure introduite pour les poses, clips, blend, root motion, masks ou retargeting doit avoir des tests dans `CasaEngine.Tests`.
9. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Validation minimale par tache

- Build principal : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Tests cibles selon la zone :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Skinned --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Rigged --no-restore`
- Validation visuelle cible :
  - `SkinnedMeshDemo`
  - une nouvelle demo dediee `AnimationBlendDemo`
  - une nouvelle demo dediee `AnimationIkDemo`

## Jeu de validation recommande

Chaque tache visible doit, quand c'est pertinent, citer un asset ou sample de ce pack au lieu d'utiliser une validation ad hoc.

### Pack V1 - validation minimale de production

- Locomotion / sampling / blend de base : [animation-example/GameAnimationProgramming-master/AllChapters/Assets/Woman.gltf](animation-example/GameAnimationProgramming-master/AllChapters/Assets/Woman.gltf)
- IK / grounding simple : [animation-example/GameAnimationProgramming-master/AllChapters/Assets/IKCourse.gltf](animation-example/GameAnimationProgramming-master/AllChapters/Assets/IKCourse.gltf)
- Comparaison LBS vs DQ : [animation-example/GameAnimationProgramming-master/AllChapters/Assets/dq.gltf](animation-example/GameAnimationProgramming-master/AllChapters/Assets/dq.gltf)
- Smoke import skinned type MonoGame/XNA : [animation-example/DigitalRune-master/Samples/Content/Dude/Dude.fbx](animation-example/DigitalRune-master/Samples/Content/Dude/Dude.fbx)

### Pack V2 - validation fonctionnelle avancee

- Crossfade : [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/05-CharacterCrossFadeSample.cs](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/05-CharacterCrossFadeSample.cs)
- Mixing / masks : [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/08-MixingSample.cs](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/08-MixingSample.cs)
- Retargeting : [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/10-SkeletonMappingSample.cs](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/10-SkeletonMappingSample.cs)
- Compression : [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/07-CompressionSample.cs](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/07-CompressionSample.cs)

### Pack V3 - validation R&D / phase ulterieure

- IK riche : [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/IK Samples](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/IK%20Samples)
- Animation textures / foule : [animation-example/GameAnimationProgramming-master/AllChapters/Code/Chapter15Sample01.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Chapter15Sample01.h)
- Anim textures associees : [animation-example/GameAnimationProgramming-master/AllChapters/Assets/Idle.animTex](animation-example/GameAnimationProgramming-master/AllChapters/Assets/Idle.animTex), [animation-example/GameAnimationProgramming-master/AllChapters/Assets/Walking.animTex](animation-example/GameAnimationProgramming-master/AllChapters/Assets/Walking.animTex), [animation-example/GameAnimationProgramming-master/AllChapters/Assets/Running.animTex](animation-example/GameAnimationProgramming-master/AllChapters/Assets/Running.animTex)

## Risques principaux

- melanger les donnees importees Assimp avec les representations runtime definitives
- casser le chargement ou la demo skinned pendant la bascule du runtime
- introduire des allocations ou copies de poses inutiles a chaque frame
- coupler trop fort gameplay et animation graph
- complexifier trop vite le rendu en voulant faire DQ, morph et IK dans la meme etape
- ne pas definir clairement la source de verite entre clip, pose locale, pose modele et matrices GPU
- sous-estimer la complexite du retargeting et du root motion

## Criteres d'acceptation finaux

- le moteur possede une pile `Skeleton -> Clip -> Pose -> Controller -> Skinning` explicite
- un personnage peut jouer plusieurs clips avec crossfade propre
- un blend tree et un blend space 1D sont en production, 2D au minimum demonstrable
- les couches et masques osseux permettent de combiner locomotion + upper body action
- les animations additives sont supportees sur au moins un cas concret
- le root motion peut etre active ou desactive par clip et expose un delta stable par frame
- les events d'animation sont dispatches proprement au gameplay
- le skinning GPU supporte LBS et DQ avec une politique claire de fallback
- les morph targets sont supportes au minimum pour le sampling et l'application runtime
- une couche IK simple fonctionne sur un sample de personnage
- le systeme est teste, documente et demontre par des demos dediees

---

## Architecture cible

### Assets authoring

- `SkeletonAsset`
- `SkinAsset` ou `SkinnedMeshAsset`
- `AnimationClipAsset`
- `AnimationGraphAsset`
- `BoneMaskAsset`
- `AvatarAsset` ou `RetargetProfileAsset`
- `IkRigAsset` si necessaire

### Runtime CPU

- `AnimationService` ou `IAnimationService`
- `AnimationManager`
- `SkeletonDefinition`
- `SkeletonPoseLocal`
- `SkeletonPoseModel`
- `AnimationClipSampler`
- `AnimationController`
- `AnimationState`
- `AnimationTransition`
- `AnimationLayer`
- `BlendGroup`
- `BlendTreeNode`
- `BlendSpace1DNode`
- `BlendSpace2DNode`
- `AnimationEventTrack`
- `RootMotionDelta`
- `MorphPose`
- `RetargetProfile`
- `RetargetProcessor`
- `IkSolverTwoBone`
- `IkSolverLookAt`

### Runtime GPU / rendering

- `SkinningMode.LinearBlend`
- `SkinningMode.DualQuaternion`
- palette d'os GPU stable
- vertex/index buffers persistants pour les meshes skinnes
- chemin de fallback si la feature n'est pas supportee

### Editor / debug

- preview de clips
- debug skeleton
- debug weights
- debug masks
- debug root motion
- debug layer stack
- debug event timeline

---

## Ordre de livraison recommande

### Version V1 - production minimale moderne

Objectif : remplacer le playback mono-clip actuel par une pile propre et extensible.

- phases 1 a 5
- phase 6 sans dual quaternion au premier passage
- phase 9 avec une demo de blend

### Version V2 - production avancee

Objectif : ajouter les features attendues d'un moteur de jeu moderne sur personnage.

- dual quaternion
- morph targets runtime
- layers, masks et additive completes
- root motion complet
- IK simple en production

### Version V3 - R&D / haut de gamme

Objectif : ajouter les techniques plus couteuses ou plus specialistes.

- retargeting avance inter-squelettes
- inertialization
- motion matching
- animation warping
- GPU skinning avance ou compute si necessaire

---

## Phase 1 - Poser les fondations runtime

- ✅ **T01.01 - Introduire les types de pose explicites**
  Objectif :
  - Creer une representation claire des poses locales et modeles, separee de `RiggedModel`.
  - Definir le contrat minimum d'une pose : transforms locales, transforms modeles, dirty flags, reset bind pose.
  Livrables :
  - `SkeletonDefinition`
  - `SkeletonPoseLocal`
  - `SkeletonPoseModel`
  Validation :
  - Build solution.
  - Tests sur composition parent/enfant et conversion local -> modele.
  Resultat du 2026-04-17 :
  - Types ajoutes sous `CasaEngine.Framework.Animations` : `BoneTransform`, `SkeletonJointDefinition`, `SkeletonDefinition`, `SkeletonPoseLocal`, `SkeletonPoseModel`.
  - Tests ajoutes dans `CasaEngine.Tests/Animation/SkeletonPoseTests.cs`.
  - Validation executee : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` puis tests cibles OK.
  Commit conseille :
  - `feat(animation): add core skeleton and pose runtime types`

- ✅ **T01.02 - Introduire une representation de clip runtime propre**
  Objectif :
  - Separer un `AnimationClip` runtime de `RiggedModel.RiggedAnimation`.
  - Definir des tracks typées par os et par canal.
  Validation :
  - Build solution.
  - Tests sur lecture de keyframes et duree.
  Resultat du 2026-04-17 :
  - Types ajoutes : `AnimationKeyframe<T>`, `Vector3AnimationTrack`, `QuaternionAnimationTrack`, `JointAnimationTrack`, `AnimationClip`.
  - Validation couverte par `CasaEngine.Tests/Animation/AnimationClipSamplerTests.cs`.
  Commit conseille :
  - `feat(animation): add typed runtime animation clips`

- ✅ **T01.03 - Introduire un `AnimationClipSampler` testable**
  Objectif :
  - Sampler un clip dans une pose locale sans side effect sur les autres systemes.
  - Gérer loop, clamp, temps negatif, clip vide et os sans track.
  Validation :
  - Tests sur `Slerp`, `Lerp`, bornes de temps et fallback bind pose.
  Resultat du 2026-04-17 :
  - Type ajoute : `AnimationClipSampler`.
  - Tests verifies : interpolation, clamp, loop negatif, fallback bind pose.
  - Validation executee : 8 tests animation OK dans `CasaEngine.Tests`.
  Commit conseille :
  - `feat(animation): add deterministic clip sampler`

- ✅ **T01.04 - Basculer `RiggedModel` sur le nouveau runtime de pose et de clips**
  Objectif :
  - Faire produire a `RiggedModel` un `SkeletonDefinition`, des `AnimationClip` runtime et un `AnimationController`.
  - Remplacer le playback legacy mono-clip par le nouveau runtime sans branche de compatibilite dediee.
  Validation :
  - Build solution.
  - Tests animation.
  - Smoke test `SkinnedMeshDemo`.
  Resultat du 2026-04-17 :
  - `RiggedModelLoader` capture maintenant une bind pose locale immuable par noeud et initialise le runtime moderne apres import.
  - `RiggedModel` construit `SkeletonDefinition`, `AnimationClip`, `SkeletonPoseLocal`, `SkeletonPoseModel` et `AnimationController` a partir des donnees importees.
  - Le playback legacy base sur `OriginalAnimations.Interpolate()` n'est plus le chemin runtime principal.
  Commit conseille :
  - `refactor(animation): switch rigged model playback to modern runtime animation`

---

## Phase 2 - Refondre l'import et les assets d'animation

- ✅ **T02.01 - Definir les nouveaux assets editoriaux**
  Objectif :
  - Introduire `SkeletonAsset`, `AnimationClipAsset`, `SkinnedMeshAsset` et leurs serialisations.
  - Supprimer la dependance de l'authoring aux classes runtime legacy.
  Fait :
  - Ajout des assets d'authoring `SkeletonAsset`, `AnimationClipAsset` et `SkinnedMeshAsset` avec serialisation JSON explicite.
  - `SkinnedMeshAsset` reference des ids de `skeleton`, `geometry` et `clips`, avec fallback de lecture de `rigged_model_asset_id` pour la transition.
  - Tests JSON cibles ajoutes dans `CasaEngine.Tests`.
  Validation :
  - Build solution.
  - Tests de serialisation JSON.
  Commit conseille :
  - `feat(animation): add authoring assets for skeletons clips and skinned meshes`

- ✅ **T02.02 - Faire sortir Assimp vers des donnees separees**
  Objectif :
  - Faire produire par le loader des assets distincts : skeleton, skin mesh, clips, morphs si presents.
  - Eviter que `RiggedModel` reste la seule sortie possible de l'import.
  Fait :
  - `EditorAssetImportService` detecte maintenant les fichiers skinnes/animes et produit un wrapper `.model` moderne, un `SkeletonAsset` `.skeleton` et un ou plusieurs `AnimationClipAsset` `.skeletonAnim` dans le dossier `_Imported/Animation`.
  - Le wrapper `.model` reference explicitement l'asset source de geometrie brute, le squelette importe, le clip par defaut et la liste des clips disponibles.
  - Le smoke test d'import est couvert par `EditorAssetImportServiceTests.ImportFile_SkinnedModelAuthorsSeparatedAnimationAssets` sur `Projects/SampleProject/Skinned/kid_idle.FBX`.
  - Les morph channels restent hors export authoring a cette etape faute de type d'asset dedie; ils restent scopes pour la phase 7 au lieu de bloquer la separation skeleton/clip/model.
  Validation :
  - Build solution.
  - Import smoke test sur `kid_idle.FBX`.
  Commit conseille :
  - `feat(animation): split assimp import into skeleton skin and clip outputs`

- ✅ **T02.03 - Brancher les loaders runtime associes**
  Objectif :
  - Enregistrer les nouveaux loaders dans `AssetLoaderRegistry`.
  - Pouvoir charger un clip sans charger un modele complet.
  Fait :
  - `AssetLoaderRegistry` enregistre maintenant `SkeletonDefinitionLoader` et `AnimationClipLoader`.
  - Un convertisseur partage `SkeletonAsset <-> SkeletonDefinition` et `AnimationClipAsset <-> AnimationClip` pour garder l'authoring et le runtime alignes.
  - `AnimationClipLoader` charge son `SkeletonDefinition` par id d'asset, ce qui permet de charger un clip runtime sans passer par `RiggedModel`.
  - La couverture de validation est ajoutee dans `CasaEngine.Tests/Animation/AnimationAssetLoaderTests.cs`.
  Validation :
  - Build solution.
  - Tests de chargement asset cible.
  Commit conseille :
  - `feat(animation): register runtime loaders for modern animation assets`

- ✅ **T02.04 - Supprimer les branches legacy restantes**
  Objectif :
  - Retirer les derniers points d'entree qui supposent l'ancien runtime mono-clip.
  - Documenter les points encore temporaires autour de `RiggedModel` tant que les nouveaux assets editoriaux ne sont pas en place.
  Fait :
  - `SkinnedMesh` lit maintenant le schema moderne du wrapper `.model` (`geometry_asset_id`, `skeleton_asset_id`, `default_animation_clip_asset_id`, `animation_clip_asset_ids`) et ne depend plus du chemin mono-clip historique pour les assets nouvellement authorises.
  - A l'initialisation, `SkinnedMesh` peut rehydrater le runtime `RiggedModel` avec le `SkeletonDefinition` et les `AnimationClip` charges separement.
  - `RiggedModel` expose un point d'injection explicite pour remplacer ses donnees runtime d'animation sans rebuilder toute la geometrie.
  - Point temporaire encore assume : la geometrie skinned brute reste chargee via `RiggedModel` comme backend de rendu/import tant que la phase 6 n'a pas sorti un conteneur de rendu skinned plus propre.
  Validation :
  - Build solution.
  - Note de nettoyage courte.
  Commit conseille :
  - `refactor(animation): remove remaining legacy animation runtime branches`

---

## Phase 3 - Construire le controller d'animation moderne

- ✅ **T03.01 - Introduire `AnimationController` et `AnimationState`**
  Objectif :
  - Remplacer la selection de clip par index par un controller runtime explicite.
  - Gerer lecture, pause, reprise, seek, speed multiplier et selection de clip par nom/id.
  Validation :
  - Build solution.
  - Tests sur changement d'etat et avance temporelle.
  Resultat du 2026-04-17 :
  - Types ajoutes : `AnimationState` et `AnimationController`.
  - `RiggedModel` expose maintenant `BeginAnimation(string)`, `PauseAnimation()`, `ResumeAnimation()` et `SeekAnimation()`.
  - `SkinnedMeshComponent` expose les memes commandes pour le gameplay/runtime scene.
  - Validation couverte par `CasaEngine.Tests/Animation/AnimationControllerTests.cs`.
  Commit conseille :
  - `feat(animation): add core animation controller runtime`

- ✅ **T03.02 - Ajouter le crossfade entre deux clips**
  Objectif :
  - Implementer une transition temporelle stable entre source et destination.
  - Definir la politique sur temps normalise, reset du temps et synchronisation eventuelle.
  Validation :
  - Build solution.
  - Demo simple avec idle -> walk -> idle.
  Resultat du 2026-04-17 :
  - `AnimationController.CrossFade()` et `RiggedModel.CrossFadeToAnimation()` implementes.
  - Tests de blend et de promotion d'etat cibles ajoutes dans `CasaEngine.Tests/Animation/AnimationControllerTests.cs`.
  - `SkinnedMeshDemo` fait maintenant cycler les clips avec crossfade quand le modele charge au moins deux animations.
  Commit conseille :
  - `feat(animation): add clip crossfade transitions`

- ✅ **T03.03 - Introduire les couches et masques osseux**
  Objectif :
  - Permettre locomotion sur tout le corps + action upper body.
  - Ajouter `BoneMask` et `AnimationLayer` avec poids de couche.
  Validation :
  - Build solution.
  - Demo sur personnage avec locomotion + tir ou attaque bras seul.
  Resultat du 2026-04-17 :
  - Types ajoutes : `BoneMask`, `AnimationLayer`, `AnimationLayerBlendMode`.
  - `AnimationController` supporte maintenant des couches multiples, des masques par os et la composition override sur la pose finale.
  - Tests unitaires ajoutes dans `CasaEngine.Tests/Animation/AnimationControllerTests.cs` pour verifier qu'une couche masquee n'affecte que les os cibles.
  Resultat du 2026-04-18 :
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` couvre maintenant un mode `Upper-body override layer` qui combine locomotion full-body et action upper-body masquee sur le rig `kid_*`.
  - La validation visuelle est couverte par le run automatise de la demo et l'overlay runtime expose le poids de couche et le trigger d'action.
  Commit conseille :
  - `feat(animation): add layered animation with bone masks`

- ✅ **T03.04 - Introduire le blend additive**
  Objectif :
  - Supporter une pose de reference et un delta additive applique sur une base.
  - Demonstrer recoil, breathing ou aim offset simple.
  Validation :
  - Build solution.
  - Tests sur composition additive.
  Resultat du 2026-04-17 :
  - Le runtime supporte maintenant des couches additives basees sur la bind pose comme reference.
  - `AnimationController` compose les deltas de translation, rotation et echelle des couches additives au-dessus de la pose de base.
  - Validation couverte par les tests `AnimationControllerTests` et build/tests animation OK.
  Commit conseille :
  - `feat(animation): add additive pose blending`

---

## Phase 4 - Blend tree et blend spaces

- ✅ **T04.01 - Introduire les noeuds de graph d'animation**
  Objectif :
  - Definir une interface runtime pour les noeuds : clip, blend, blend space, layer, additive, output.
  - Garder le graph evaluable sans allocations par frame.
  Validation :
  - Build solution.
  - Tests sur evaluation d'un petit graph.
  Resultat du 2026-04-17 :
  - Infrastructure minimale ajoutee : `IAnimationGraphNode`, `AnimationClipNode`, `LinearBlendAnimationNode` et `AnimationPoseBlender`.
  - Le runtime peut deja evaluer un petit graph purement CPU sans allocations par frame dans les noeuds de base.
  - Tests ajoutes dans `CasaEngine.Tests/Animation/AnimationGraphNodeTests.cs`.
  - Reste a faire : introduire les noeuds specialises `layer`, `additive`, `blend space` et un vrai noeud de sortie/graph asset.
  Resultat du 2026-04-18 :
  - Ajout de `IAnimationGraphRuntimeNode` pour les noeuds capables d'avancer leur temps runtime sans allocations par frame.
  - `AnimationController` sait maintenant piloter un root node de graph via `PlayGraph(...)` et evaluer ce graph directement dans `OutputPose`.
  - `RiggedModel` propage cet usage via `PlayAnimationGraph(...)`, avec mise a jour coherente de l'etat runtime (`AnimationRunning`, `CurrentAnimationFrameTime`, root motion).
  - Validation executee : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`, puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~AnimationGraphNodeTests --no-restore`.
  Commit conseille :
  - `feat(animation): add animation graph node runtime`

- ✅ **T04.02 - Implementer un blend tree lineaire**
  Objectif :
  - Supporter `Blend` entre deux ou plusieurs entrees pilote par un float.
  - Definir la stabilite numerique des poids et leur normalisation.
  Validation :
  - Build solution.
  - Demo de blend idle/walk/run.
  Resultat du 2026-04-17 :
  - `LinearBlendAnimationNode` implemente un blend lineaire a deux entrees avec poids clampes entre `0` et `1`.
  - Validation unitaire ajoutee sur composition d'un blend tree minimal.
  Resultat du 2026-04-18 :
  - `LinearBlendAnimationNode` implemente maintenant aussi `IAnimationGraphRuntimeNode`, ce qui permet a `AnimationController.PlayGraph(...)` de faire avancer ses entrees runtime automatiquement.
  - `LinearBlendAnimationNode` supporte maintenant un nombre arbitraire d'entrees ordonnees uniforme, avec clamp aux bornes, tout en preservant l'API binaire historique `source/target/weight`.
  - `CasaEngine.Tests/Animation/AnimationGraphNodeTests.cs` couvre aussi le cas multi-entrees et le clamp hors bornes.
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` expose un mode `LinearBlendTree` dedie a la locomotion idle/walk/run, selectionnable pour les validations automatisees.
  - Validation executee : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`, `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~AnimationGraphNodeTests --no-restore`, `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`, puis run automatise avec `CASAENGINE_START_DEMO=Animation blend demo`, `CASAENGINE_ANIMATION_BLEND_MODE=LinearBlendTree` et capture `artifacts/validation/animation-blend-demo-linear-blend-tree.png`.
  Commit conseille :
  - `feat(animation): add linear blend tree nodes`

- ✅ **T04.03 - Implementer un blend space 1D**
  Objectif :
  - Piloter la locomotion par vitesse scalaire.
  - Supporter echantillons non uniformes et clamp aux bornes.
  Resultat du 2026-04-18 :
  - Type ajoute : `BlendSpace1DNode` avec `BlendSpace1DSample`.
  - Le runtime supporte les echantillons non uniformes, le clamp hors bornes et l'evaluation sans allocation par frame via des poses temporaires preallouees.
  - `BlendSpace1DNode` implemente `IAnimationGraphRuntimeNode` et peut donc etre joue directement par `AnimationController.PlayGraph(...)` ou `RiggedModel.PlayAnimationGraph(...)`.
  - Validation unitaire ajoutee dans `CasaEngine.Tests/Animation/AnimationGraphNodeTests.cs`.
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` fournit maintenant une demo locomotion 1D dediee idle/walk/run pilotee par clavier et validee en run automatise avec capture.
  Validation :
  - Build solution.
  - Demo locomotion 1D.
  Commit conseille :
  - `feat(animation): add 1d blend space support`

- ✅ **T04.04 - Implementer un blend space 2D**
  Objectif :
  - Supporter locomotion directionnelle et strafe.
  - Definir interpolation triangulaire ou bilineaire selon l'implementation retenue.
  Resultat du 2026-04-18 :
  - Type ajoute : `BlendSpace2DNode` avec `BlendSpace2DSample`.
  - L'implementation retenue utilise une interpolation triangulaire barycentrique a l'interieur de l'enveloppe des samples, puis un clamp vers le segment le plus proche hors de l'enveloppe.
  - `AnimationPoseBlender` sait maintenant faire un blend pondere multi-poses pour supporter les poids barycentriques sans allocations par frame.
  - `BlendSpace2DNode` implemente `IAnimationGraphRuntimeNode` et peut etre pilote directement par `AnimationController.PlayGraph(...)` ou `RiggedModel.PlayAnimationGraph(...)`.
  - Validation unitaire etendue dans `CasaEngine.Tests/Animation/AnimationGraphNodeTests.cs` avec un cas directionnel a 4 samples (`idle` centre + `strafe left` + `forward` + `strafe right`) pour verrouiller le comportement barycentrique dans une enveloppe non triangulaire.
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` remplace le triangle technique `idle/walk/run` par un set directionnel reel avec `idle` au centre, `walk` en avant et deux clips de strafe proceduraux dedies sur les axes lateraux.
  - La demo 2D accepte maintenant un parametre de demarrage automatise (`CASAENGINE_ANIMATION_BLEND_SPACE_2D_X` / `CASAENGINE_ANIMATION_BLEND_SPACE_2D_Y`) pour figer une pose de validation representative sans input manuel.
  Validation :
  - `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter AnimationGraphNodeTests --no-build -v minimal`
  - `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj`
  - `Push-Location .\CasaEngine.Demos; $env:CASAENGINE_START_DEMO='Animation blend demo'; $env:CASAENGINE_ANIMATION_BLEND_MODE='BlendSpace2D'; $env:CASAENGINE_ANIMATION_BLEND_SPACE_2D_X='1'; $env:CASAENGINE_ANIMATION_BLEND_SPACE_2D_Y='0'; $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH='D:\development\repo\CasaEngineMonogame\artifacts\validation\animation-blend-demo-blendspace2d-directional.png'; $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS='1500'; dotnet .\bin\Debug\net9.0-windows\CasaEngine.Demos.dll; Pop-Location`
  Commit conseille :
  - `feat(animation): add 2d blend space support`

---

## Phase 5 - Root motion, events et hooks gameplay

- ✅ **T05.01 - Extraire un `RootMotionDelta` stable par frame**
  Objectif :
  - Permettre aux clips de fournir translation et rotation racine.
  - Exposer un mode applique ou observe seulement.
  Validation :
  - Build solution.
  - Tests sur delta cumule et boucle.
  Resultat du 2026-04-17 :
  - `RootMotionDelta` ajoute dans le runtime.
  - `AnimationController` expose maintenant un delta de root motion par frame via `CurrentRootMotionDelta` et `ConsumeRootMotionDelta()`.
  - Tests unitaires ajoutes pour verifier l'extraction de translation frame a frame.
  Resultat du 2026-04-18 :
  - `RootMotionMode` ajoute avec `Observe` et `Apply` pour permettre soit l'observation pure du root motion, soit la consommation + suppression du root sur la pose finale.
  - `AnimationControllerTests` couvre maintenant explicitement le mode `Apply` pour verifier que la pose finale est nettoyee apres extraction du delta.
  - `AnimationBlendDemo` expose une page `Additive + root motion` avec burst procedural, trail debug, et toggle clavier observe/apply validee en run automatise.
  Commit conseille :
  - `feat(animation): add root motion extraction`

- ✅ **T05.02 - Ajouter les events d'animation**
  Objectif :
  - Supporter notifies temporels sur clips.
  - Eviter les doubles declenchements sur seek, loop et crossfade.
  Validation :
  - Build solution.
  - Tests sur dispatch d'events.
  Resultat du 2026-04-17 :
  - Types ajoutes : `AnimationEventKeyframe` et `AnimationEventTrack`.
  - `AnimationClip` peut maintenant porter une piste d'events temporels.
  - `AnimationController` dispatch les events sur lecture normale et en boucle, sans declenchement parasite sur `Seek()`.
  - Politique actuelle de crossfade : les events du clip cible ne sont pas emises tant que la transition n'a pas promu l'etat cible, afin d'eviter les doubles notifies pendant le recouvrement.
  - Validation couverte par `CasaEngine.Tests/Animation/AnimationControllerTests.cs` avec tests playback, seek et loop.
  Commit conseille :
  - `feat(animation): add animation event tracks`

- ✅ **T05.03 - Brancher gameplay et composants scene**
  Objectif :
  - Faire vivre le controller dans un composant runtime dedie.
  - Eviter que le gameplay manipule directement les internals de pose.
  Validation :
  - Build solution.
  - Sample de personnage pilotable.
  Resultat du 2026-04-17 :
  - `SkinnedMeshComponent` expose deja `PlayAnimation`, `CrossFadeToAnimation`, `PauseAnimation`, `ResumeAnimation`, `SeekAnimation` et `ConsumeRootMotionDelta()`.
  - Les events d'animation sont remontes via `RiggedModel` puis `SkinnedMeshComponent` pour un branchement gameplay sans acces direct aux poses internes.
  Resultat du 2026-04-18 :
  - `SkinnedMeshComponent` expose maintenant aussi `PlayAnimationGraph`, `SetAnimationLayer`, `ClearAnimationLayer`, `SetAnimationLayerWeight` et `RootMotionMode`.
  - L'assignation directe de `SkinnedMesh` rebinde correctement les events runtime, ce qui permet a la demo de remonter les notifies sans toucher aux poses internes.
  Resultat du 2026-04-18 (suite) :
  - Nouveau type runtime : `SkinnedMeshAnimationRuntime`, instancie par `SkinnedMeshComponent`, qui heberge maintenant `AnimationController`, `SkeletonPoseModel`, la palette de skinning GPU et les hooks `PosePostProcessing` / events par instance de scene.
  - `RiggedModel.OverrideRuntimeAnimationAssets(...)` ne cree plus de controller par defaut : le mesh conserve les donnees asset (`SkeletonDefinition`, clips), tandis que les appels legacy directs sur `RiggedModel` recreent seulement un runtime transitoire a la demande pour ne pas casser l'API existante.
  - `SkinnedMeshRendererComponent` consomme des palettes de skinning par instance poussees par le composant, ce qui evite de faire vivre l'etat d'animation dans l'asset partage et aligne le branchement gameplay sur `SkinnedMeshComponent`.
  - `AnimationIkDemo`, `SkinnedMeshDemo` et la branche de crossfade avance de `AnimationBlendDemo` ont ete migres vers l'API composant au lieu de manipuler `RiggedModel` directement.
  - `CasaEngine.Tests/Animation/SkinnedMeshAnimationRuntimeTests.cs` verrouille l'absence de controller par defaut dans `RiggedModel`, l'independance de deux runtimes partageant le meme asset et la compatibilite legacy via creation paresseuse.
  Validation executee : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "FullyQualifiedName~AnimationControllerTests|FullyQualifiedName~AnimationGraphNodeTests|FullyQualifiedName~SkinnedMeshAnimationRuntimeTests" -v minimal`, `dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal`, puis smoke run automatise de `Animation IK demo` et `Skinned mesh demo` avec captures `artifacts/validation/t05-03-animation-ik-demo.png` et `artifacts/validation/t05-03-skinned-mesh-demo.png`.
  Commit conseille :
  - `feat(animation): integrate controller with scene components`

---

## Phase 6 - Moderniser le skinning et le rendu

- ✅ **T06.01 - Decoupler skinning runtime et rendu legacy**
  Objectif :
  - Faire du renderer skinned un consommateur de poses finales, pas le proprietaire de la logique animation.
  - Definir l'interface `pose finale -> palette GPU`.
  Validation :
  - Build solution.
  - Smoke test `SkinnedMeshDemo`.
  Resultat du 2026-04-18 :
  - Contrat explicite ajoute : `ISkinnedMeshPoseProvider` avec palette GPU et transform de noeud de mesh.
  - `SkinnedMeshAnimationRuntime` implemente maintenant ce contrat et expose une pose finale directement consommable par le renderer.
  - `RiggedModelPoseProvider` fournit le fallback legacy cote composant sans laisser le renderer lire directement l'etat runtime de `RiggedModel`.
  - `SkinnedMeshRendererComponent` consomme des `ISkinnedMeshPoseProvider` et n'accede plus directement a `GlobalShaderMatrixs` ni a la logique d'evaluation de pose.
  - `SkinnedMeshComponent` choisit explicitement le provider runtime ou legacy avant d'enqueuer le mesh pour le rendu.
  - Validation executee : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`, `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`, puis smoke `Skinned mesh demo` avec capture `artifacts/validation/t06-01-skinned-mesh-demo.png`.
  Commit conseille :
  - `refactor(rendering): separate animation pose evaluation from skinned rendering`

- ⏳ **T06.02 - Passer les meshes skinnes sur VB/IB persistants**
  Objectif :
  - Arreter l'usage principal de `DrawUserIndexedPrimitives` pour les meshes skinnes.
  - Garder les buffers GPU persistants et reutilisables.
  Validation :
  - Build solution.
  - Capture visuelle et verification stats draw calls / binds.
  Commit conseille :
  - `feat(rendering): move skinned meshes to persistent gpu buffers`

- ⏳ **T06.03 - Introduire `SkinningMode.LinearBlend` explicite**
  Objectif :
  - Formaliser le mode LBS actuel au lieu d'un shader implicite unique.
  - Clarifier les contrats vertex et shader.
  Validation :
  - Build solution.
  - Tests si un resolver de mode est introduit.
  Commit conseille :
  - `refactor(rendering): formalize linear blend skinning mode`

- ⏳ **T06.04 - Ajouter le dual quaternion skinning**
  Objectif :
  - Supporter le skinning dual quaternion pour mieux conserver les volumes sur les twists.
  - Definir la politique de fallback si scale non uniforme ou shader non compatible.
  Validation :
  - Build solution.
  - Demo visuelle sur twist avant-bras / epaules / hanches.
  Commit conseille :
  - `feat(rendering): add dual quaternion skinning support`

- ⏳ **T06.05 - Exposer le choix LBS vs DQ**
  Objectif :
  - Permettre le choix par asset, material ou renderer selon la direction retenue.
  - Garder un comportement par defaut stable pour les assets existants.
  Validation :
  - Build solution.
  - Demo comparant les deux modes.
  Commit conseille :
  - `feat(animation): expose configurable skinning mode selection`

---

## Phase 7 - Morph targets et autres deformers

- ⏳ **T07.01 - Introduire les donnees runtime de morph targets**
  Objectif :
  - Faire exister des `MorphTarget` et `MorphClip` si la source en fournit.
  - Ne plus se limiter a logguer les canaux morph Assimp.
  Validation :
  - Build solution.
  - Import test sur asset avec blend shapes.
  Commit conseille :
  - `feat(animation): add morph target runtime data`

- ⏳ **T07.02 - Sampler et appliquer les morphs**
  Objectif :
  - Sampler des poids de morph et les combiner au skinning.
  - Definir l'ordre d'application LBS/DQ + morph.
  Validation :
  - Build solution.
  - Demo visage ou deformation simple.
  Commit conseille :
  - `feat(animation): add morph target sampling and application`

- ⏳ **T07.03 - Definir la politique sur les autres deformers**
  Objectif :
  - Decider ce qui est supporte, ignore ou converti a l'import.
  - Documenter les limites explicites.
  Validation :
  - Note versionnee.
  Commit conseille :
  - `docs(animation): document deformer support policy`

---

## Phase 8 - Retargeting et contraintes runtime

- ⏳ **T08.01 - Introduire un `RetargetProfile`**
  Objectif :
  - Mapper proprement un clip source vers un squelette cible.
  - Definir conventions de reference pose, axes et echelles.
  Validation :
  - Build solution.
  - Tests sur mapping de bones.
  Commit conseille :
  - `feat(animation): add retarget profile assets`

- ⏳ **T08.02 - Implementer un retargeting de base**
  Objectif :
  - Supporter au minimum le retargeting entre squelettes proches.
  - Garder la logique separee du sampling de clip brut.
  Validation :
  - Build solution.
  - Demo avec deux rigs proches si disponible.
  Commit conseille :
  - `feat(animation): add baseline clip retargeting`

- ✅ **T08.03 - Ajouter un solver IK Two Bone**
  Objectif :
  - Couvrir main/bras et pied/jambe.
  - Definir une passe runtime post-animation claire.
  Resultat du 2026-04-18 :
  - `TwoBoneIkConstraint` et `IkSolverTwoBone` ont ete ajoutes sous `CasaEngine.Framework.Animations`.
  - `RiggedModel` expose maintenant un hook de post-traitement de pose, et `SkinnedMeshComponent` peut enregistrer des contraintes IK two-bone appliquees apres l'evaluation animation.
  - Des tests unitaires ont ete ajoutes dans `CasaEngine.Tests/Animation/IkSolverTwoBoneTests.cs` pour les cas atteignable, hors-portee, pole vector et chaines invalides.
  - Validation executee : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~IkSolverTwoBoneTests --no-restore` puis `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`.
  - Validation visuelle completee via `AnimationIkDemo` et capture automatisee `artifacts/validation/animation-ik-demo.png`.
  Validation :
  - Build solution.
  - Demo de reach target ou foot placement simple.
  Commit conseille :
  - `feat(animation): add two bone ik solver`

- ⏳ **T08.04 - Ajouter look-at et contraintes simples**
  Objectif :
  - Supporter look-at tete/torse et contraintes de base.
  - Garder une interface de passes runtime simple.
  Validation :
  - Build solution.
  - Demo de suivi de cible.
  Commit conseille :
  - `feat(animation): add look-at and simple bone constraints`

---

## Phase 9 - Editor, debug et demos

- ✅ **T09.01 - Ajouter un visualiseur de skeleton et poses**
  Objectif :
  - Permettre le debug des bones, axes et transforms.
  - Faciliter la validation du retargeting et de l'IK.
  Resultat du 2026-04-18 :
  - `CasaEngine.Framework.Animations/SkeletonDebugVisualizer.cs` a ete ajoute pour dessiner la hierarchie du skeleton et les axes locaux de chaque joint via `Line3dRendererComponent`.
  - `AnimationIkDemo` integre maintenant ce visualiseur avec un toggle runtime `[V]`, active par defaut pour la validation de pose/IK.
  - La doc de la demo IK a ete mise a jour pour decrire le mode skeleton debug.
  - Validation executee : `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`, puis run automatise depuis `CasaEngine.Demos` avec `CASAENGINE_START_DEMO=Animation IK demo` et capture `artifacts/validation/animation-ik-demo.png`.
  Validation :
  - Build solution.
  - Smoke test editor ou demo.
  Commit conseille :
  - `feat(editor): add skeleton debug visualization`

- ✅ **T09.02 - Ajouter un previewer de clips et blend tree**
  Objectif :
  - Permettre de previsualiser un clip, ses events, son root motion et ses blends.
  - A terme, ouvrir la voie a un editeur de graph.
  Resultat du 2026-04-18 :
  - `CasaEngine.Editor/Controls/AnimationClipPreviewPanel.cs` a ete ajoute pour fournir un previewer de clips integre a l'editor.
  - L'integration editor a ete branchee via `Game1`, `EditorDocumentKind`, `EditorHistoryContextKind` et `EditorPanelIds` pour ouvrir ce nouveau panneau dans le workflow existant.
  - La tache livre le preview tooling et les points d'integration necessaires pour la suite du travail autour des blend trees, sans coupler cela au runtime.
  Validation :
  - Build solution.
  - Validation manuelle dans l'editor.
  Commit conseille :
  - `feat(editor): add animation clip previewer`

- ✅ **T09.03 - Ajouter `AnimationBlendDemo`**
  Objectif :
  - Demonstrer crossfade, blend tree, blend space et additive.
  Resultat du 2026-04-18 :
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` a ete ajoute et enregistre dans `DemosGame`.
  - La demo charge `kid_idle`, `kid_walk` et `kid_run`, rebind les clips sur le skeleton idle rendu, puis expose une validation clavier des blend spaces 1D et 2D.
  - Le run automatise `CASAENGINE_START_DEMO=Animation blend demo` produit une capture dans `artifacts/validation/animation-blend-demo.png`.
  - La demo couvre maintenant cinq pages : blend space 1D, blend space 2D, cross-fade manuel, upper-body override masque, additive breathing + root motion.
  - Un second run automatise produit `artifacts/validation/animation-blend-demo-showcase.png` apres validation du chemin d'execution complet de la demo mise a jour.
  Validation :
  - Build solution.
  - Run de la demo.
  Commit conseille :
  - `feat(demos): add animation blend demo`

- ✅ **T09.04 - Ajouter `AnimationIkDemo`**
  Objectif :
  - Demonstrer IK, masks et root motion si possible.
  Resultat du 2026-04-18 :
  - `CasaEngine.Demos/Demos/AnimationIkDemo.cs` a ete ajoute et enregistre dans `DemosGame`.
  - La demo charge un vrai personnage skinned (`kid_idle.model`), selectionne une chaine two-bone pertinente depuis le skeleton, expose une cible IK pilotable et un poids runtime, puis reutilise le solver de `SkinnedMeshComponent`.
  - Une documentation courte a ete ajoutee dans `docs/animation-ik-demo.md`.
  - Validation executee : `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`, puis run automatise depuis `CasaEngine.Demos` avec `CASAENGINE_START_DEMO=Animation IK demo` et capture `artifacts/validation/animation-ik-demo.png`.
  Validation :
  - Build solution.
  - Run de la demo.
  Commit conseille :
  - `feat(demos): add animation ik demo`

---

## Phase 10 - Optimisations et techniques avancees

- ✅ **T10.01 - Ajouter compression de clips**
  Objectif :
  - Reduire memoire et bande passante CPU des clips.
  - Definir une politique de precision par canal.
  Validation :
  - Build solution.
  - Tests sur erreur max de reconstruction.
  Commit conseille :
  - `feat(animation): add clip compression pipeline`

- ✅ **T10.02 - Ajouter inertialization ou transitions avancees**
  Objectif :
  - Ameliorer la qualite des transitions rapides sans multiplier les clips de transition.
  Validation :
  - Build solution.
  - Demo visuelle avant/apres.
  Commit conseille :
  - `feat(animation): add inertialized transitions`

- ✅ **T10.03 - Evaluer motion matching**
  Objectif :
  - Encadrer le sujet comme une phase R&D, avec prerequis explicites sur base de clips et metadata.
  - Ne pas bloquer la pile production sur cette feature.
  Validation :
  - Note d'architecture versionnee.
  Commit conseille :
  - `docs(animation): define motion matching prerequisites and scope`

---

## Decoupage recommande des premieres sous-taches reellement faisables

Pour derisquer la migration, l'ordre concret recommande est :

1. `T01.01` a `T01.04`
2. `T03.01` et `T03.02`
3. `T05.01` et `T05.02`
4. `T04.01` a `T04.03`
5. `T06.01` a `T06.03`
6. `T03.03` et `T03.04`
7. `T06.04` et `T06.05`
8. `T07.*`, `T08.*`, `T10.*`

Cette sequence donne rapidement un systeme utilisable :

- pose runtime propre
- sampling de clips propre
- controller et crossfade
- blend space 1D de locomotion
- root motion et events
- rendu skinned modernise

Le dual quaternion, les morphs, le retargeting et le motion matching doivent rester des couches ulterieures, pas des prerequis a la mise a niveau initiale.

## References prioritaires par phase

- Phases 1 et 2 : partir des types coeur de `GameAnimationProgramming` et n'utiliser `GLTFLoader` que comme reference de forme des donnees, pas comme loader a porter tel quel.
- Phases 3 et 4 : combiner `CrossFadeController` de `GameAnimationProgramming` avec l'architecture `AnimationController`, transitions et `BlendGroup` de `DigitalRune`.
- Phase 5 : garder un contrat CasaEngine-specifique pour root motion et events. `TimelineGroup` de `DigitalRune` sert de reference conceptuelle, pas d'API a recopier.
- Phase 6 : prendre `DualQuaternion` et le shader de reference de `GameAnimationProgramming`, mais garder LBS comme fallback stable et par defaut pour la migration.
- Phase 7 : aucun des deux depots n'est une reference forte pour les morph targets. Garder un scope d'import-first, puis appliquer runtime seulement si les types sont solides.
- Phase 8 : prendre `SkeletonMapper` et les strategies de mapping de `DigitalRune`, mais commencer l'IK avec le cout d'integration le plus bas : `CCDSolver` / `FABRIKSolver` puis `TwoJoint` / `LookAt`.
- Phase 9 : caler les demos CasaEngine sur les packs V1/V2 au lieu d'inventer de nouveaux scenarios de validation.
- Phase 10 : s'appuyer d'abord sur `DigitalRune` pour compression et transitions avancees. Garder animation textures / foule de `GameAnimationProgramming` pour un chantier separe.

---

## Notes de conception importantes

- `RiggedModel` ne doit pas devenir le controller moderne. Il doit soit etre transforme en simple conteneur de donnees legacy, soit etre progressivement remplace comme source runtime principale.
- Le dual quaternion doit etre ajoute comme mode de skinning, pas comme remplacement brutal du LBS.
- Les morph targets doivent etre penses en meme temps que l'import, mais peuvent etre branches plus tard dans le runtime si les types sont poses proprement.
- Le retargeting doit etre scope minimal au debut : squelettes proches, reference pose explicite, pas de promesse trop large.
- Le motion matching doit rester hors chemin critique tant qu'il n'y a pas une pile propre de clips, features et locomotion de base.

## References externes a consulter pendant l'implementation

### Runtime coeur

- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Transform.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Transform.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Pose.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Pose.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Skeleton.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Skeleton.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Clip.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Clip.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Track.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Track.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/TransformTrack.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/TransformTrack.h)

### Controller, transitions et layering

- [animation-example/GameAnimationProgramming-master/AllChapters/Code/CrossFadeController.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/CrossFadeController.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Blending.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Blending.h)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/AnimationManager.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/AnimationManager.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/IAnimationService.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/IAnimationService.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/AnimationController.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/AnimationController.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Transitions/FadeInAndReplaceTransition.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Transitions/FadeInAndReplaceTransition.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Animations/NBlendAnimation/BlendGroup.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Animations/NBlendAnimation/BlendGroup.cs)

### Skinning LBS / DQ

- [animation-example/GameAnimationProgramming-master/AllChapters/Code/DualQuaternion.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/DualQuaternion.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Shaders/skinned.vert](animation-example/GameAnimationProgramming-master/AllChapters/Shaders/skinned.vert)
- [animation-example/GameAnimationProgramming-master/AllChapters/Shaders/dualquaternion.vert](animation-example/GameAnimationProgramming-master/AllChapters/Shaders/dualquaternion.vert)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Chapter14Sample01.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Chapter14Sample01.h)

### Retargeting et IK

- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton Mapping/SkeletonMapper.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton%20Mapping/SkeletonMapper.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton Mapping/DirectBoneMapper.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton%20Mapping/DirectBoneMapper.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton Mapping/ChainBoneMapper.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton%20Mapping/ChainBoneMapper.cs)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/CCDSolver.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/CCDSolver.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/FABRIKSolver.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/FABRIKSolver.h)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/IK/TwoJointIKSolver.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/IK/TwoJointIKSolver.cs)
- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/IK/LookAtIKSolver.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/IK/LookAtIKSolver.cs)

### Compression et phases avancees

- [animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton Animations/SkeletonKeyFrameAnimation_Compression.cs](animation-example/DigitalRune-master/Source/DigitalRune.Animation/Character/Skeleton%20Animations/SkeletonKeyFrameAnimation_Compression.cs)
- [animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/07-CompressionSample.cs](animation-example/DigitalRune-master/Samples/Samples/Samples/Animation/CharacterAnimation/07-CompressionSample.cs)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/AnimBaker.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/AnimBaker.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/AnimTexture.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/AnimTexture.h)
- [animation-example/GameAnimationProgramming-master/AllChapters/Code/Crowd.h](animation-example/GameAnimationProgramming-master/AllChapters/Code/Crowd.h)

### Regle pratique

- Si une tache touche au runtime coeur ou au DQ, consulter d'abord `GameAnimationProgramming`.
- Si une tache touche au controller, au layering, au retargeting, a l'IK riche ou a la compression, consulter d'abord `DigitalRune`.