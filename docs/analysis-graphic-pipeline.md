# Analyse du pipeline graphique de CasaEngine

CasaEngine implémente un **forward renderer multi-vues à trois étages**, construit au-dessus de MonoGame (D3D11). L'architecture est propre et bien commentée, clairement construite par phases incrémentales (les commentaires « Phase 4 », « Phase 7 », « Phase 9/10 » en témoignent). Voici le détail, suivi des points forts et de quelques anomalies détectées.

## Étage 1 — Orchestration multi-vues : `RenderPipeline`

[RenderPipeline.cs](CasaEngine/Framework/Rendering/RenderPipeline.cs) itère la liste des [RenderView](CasaEngine/Framework/Rendering/RenderView.cs) fournie par le `ViewManager`. Chaque vue combine un monde, une caméra et une surface de sortie (`BackBufferSurface` ou `RenderTargetSurface`), avec des fonctionnalités orientées éditeur :

- **Modes de rafraîchissement** : `RealTime`, `OnDemand` (dirty flag via `Invalidate()`), `Throttled` (fps cible) — pensés pour les viewports d'éditeur WPF.
- **`ResolutionScale`** (0.25–2.0) avec redimensionnement du render target et upscale par le presenter.
- **Isolation d'état GPU** par vue via `GraphicsStateGuard` (snapshot/restore), plus une validation optionnelle des états « sales » en debug.
- Le clear couleur du back buffer passe par un quad `SpriteBatch` (et non `GraphicsDevice.Clear`) car MonoGame ignore le viewport lors d'un Clear — indispensable au split-screen.
- **Contournement D3D11** ([RenderPipeline.cs:137](CasaEngine/Framework/Rendering/RenderPipeline.cs:137)) : quand les ombres sont actives sur une vue back buffer, la scène est rendue dans un `sceneRt` en `PreserveContents` puis blittée en une fois — sinon le passage shadow-map → back buffer provoque un discard du swap-chain (les 4 derniers commits du dépôt tournent autour de ce problème).

Avant le rendu, la vue résout son environnement (`EnvironmentResolver`, avec override par vue) et collecte les lumières du monde (`WorldLightCollector`), puis `RenderFrameFactory` fige tout dans un [RenderFrame](CasaEngine/Framework/Rendering/RenderFrame.cs) immuable (matrices, viewport, environnement, lumières, réglages d'ombres).

## Étage 2 — Rendu d'une vue : `DefaultViewPipeline`

[DefaultViewPipeline.cs](CasaEngine/Framework/Rendering/DefaultViewPipeline.cs) enchaîne trois phases chronométrées (les temps CPU par phase sont accumulés dans `RenderStats`) :

1. **`World.Draw(frame)`** ([World.cs:623](CasaEngine/Framework/Scene/World/World.cs:623)) : culling par `BoundingFrustum` contre l'index spatial (`SpatialServices.WorldIndex`), puis chaque entité visible enfile ses commandes dans les composants renderers.
2. **Flush des renderers** dans un ordre fixe défini dans [CasaEngineGame.cs:357](CasaEngine/Framework/Application/CasaEngineGame.cs:357) : meshes statiques → meshes skinnés → sprites → particules → lignes 3D → 2D.
3. **Composition UI** : le runtime MGUI propre à la vue est dessiné pendant que le render target de la vue est encore actif.

C'est un modèle « enqueue puis flush » : les composants de scène ne dessinent jamais directement, ils soumettent des données aux renderers qui font le travail GPU par vue.

## Étage 3 — Le pipeline 3D : `ForwardRenderPipeline`

Le vrai pipeline 3D vit dans le flush de [StaticMeshRendererComponent](CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs), qui :

1. Convertit chaque (mesh, submesh, matériau, transform) en [RenderItem](CasaEngine/Framework/Rendering/Draw/RenderItem.cs) — struct portant le matériau compilé, les flags d'ombres, la `WorldInverseTranspose` précalculée et les `ShaderFeature`.
2. Génère une **clé de tri 64 bits** ([SortKeyGenerator.cs](CasaEngine/Framework/Rendering/Draw/SortKeyGenerator.cs)) : queue (4 bits) → hash shader (16) → hash matériau (16) → hash mesh (16) → distance inversée (12 bits, transparents uniquement). Un seul `Sort` ordonne toute la frame en minimisant les changements d'état.
3. Extrait les groupes instanciables (`ShaderFeature.Instanced`, seuil ≥ 4) vers l'[InstanceBatcher](CasaEngine/Framework/Rendering/Draw/InstanceBatcher.cs) (second vertex stream, matrices monde par instance).
4. Délègue à [ForwardRenderPipeline](CasaEngine/Framework/Rendering/ForwardRenderPipeline.cs), liste ordonnée et extensible de passes (le diagramme ci-dessus) : `ShadowPass` → callback post-shadow (injection des casters skinnés dans l'atlas avant que le sol ne l'échantillonne) → `SkyPass` → `OpaquePass` → `TransparentPass`.

**Ombres** ([ShadowPass.cs](CasaEngine/Framework/Rendering/Draw/ShadowPass.cs)) : V1 volontairement simple — une seule lumière directionnelle, shadow map orthographique unique (`SurfaceFormat.Single`, fallback `Color`) centrée sur la caméra avec `MaxDistance`, biais depth/normal configurables, techniques `ShadowDepth_Solid`/`ShadowDepth_Textured` (alpha-test). Pas de cascades, pas de snapping texel (risque de shimmering quand la caméra bouge).

## Systèmes transverses

- **Shaders** : le HLSL vit dans [CasaEngine/Content/Shaders](CasaEngine/Content/Shaders). `LitForward.fx` est une extension du `BasicEffect` XNA : Blinn-Phong (non PBR), **8 lumières directionnelles + 8 ponctuelles + 8 spots** (voir [LightingContext.cs](CasaEngine/Framework/Rendering/LightingContext.cs), avec scoring de priorité), normal map, cubemaps de réflexion/environnement et probes locales, échantillonnage de la shadow map. ~24 techniques couvrent les combinaisons vertex/pixel lighting × texture × vertex color × normal map × réflexion.
- **Sélection de variante** : les flags [ShaderFeature](CasaEngine/Framework/Rendering/Shaders/ShaderFeature.cs) (texture, vertex color, alpha-test, skinning, instancing, normal map…) forment une `ShaderVariantKey` ; [RenderShaderSelector](CasaEngine/Framework/Rendering/Shaders/RenderShaderSelector.cs) résout dans l'ordre variantes → shaders enregistrés → `ShaderManager` (assets) → fallback. Les shaders intégrés sont **hot-reloadables** (`TryReloadBuiltInShader`).
- **Matériaux** : `MaterialBase` (+ `LitDiffuseMaterial`, `UnlitTextureMaterial`) avec un système de compilation ([MaterialCompiler](CasaEngine/Framework/Materials/Compilation/MaterialCompiler.cs) → `CompiledMaterial`) qui fige shader effectif, features, queue et render states ; `MaterialPropertyBlock` permet des overrides par instance sans dupliquer l'asset, et des overrides par slot de matériau existent au niveau du composant.
- **Caches anti-redondance** : [RenderStateCache](CasaEngine/Framework/Rendering/Draw/RenderStateCache.cs) (déduplication blend/depth/rasterizer/sampler par égalité de référence) et [ShaderBindCache](CasaEngine/Framework/Rendering/Shaders/ShaderBindCache.cs) (les globals — caméra, lumières, environnement — ne sont ré-uploadés que si le shader change). Un `RenderTargetPool` partagé recycle les RT.
- **Skinning** : `SkinnedMeshRendererComponent` avec deux modes GPU (`LinearBlend`, `DualQuaternion` via `skinEffect.fx`), pose fournie par `ISkinnedMeshPoseProvider`.
- **2D** : chemin séparé avec sa propre clé de tri lexicographique à 7 champs ([RenderSortKey2D](CasaEngine/Framework/Rendering/Depth/RenderSortKey2D.cs) : passe 2D, sorting layer, ordre, élévation, coordonnée Y-sort, offset, id stable) — conçu pour les tilemaps avec profondeur (cf. [docs/tilemaps-gestion-profondeur.md](docs/tilemaps-gestion-profondeur.md)).
- **Observabilité** : `RenderStats` par vue (draw calls, changements d'état, binds, temps CPU par phase) affichable via `DebugOverlay`.

## Points forts

Séparation nette des responsabilités (orchestration vues / pipeline vue / passes 3D), extensibilité réelle (passes insérables, `IViewRenderPipeline` et presenter par vue), bonne discipline d'état GPU (guards + caches + validation debug), et une instrumentation CPU inhabituellement soignée pour un moteur de cette taille. Les contournements des pièges D3D11/MonoGame (clear vs viewport, discard du back buffer) sont documentés dans le code.

## Points d'attention relevés

1. **Bug probable — ordre des transparents inversé** : `SortKeyGenerator` encode `0xFFF - distance` pour que les objets *lointains* arrivent en premier dans le tri croissant, mais [TransparentPass](CasaEngine/Framework/Rendering/Draw/OpaqueAndTransparentPasses.cs:69) itère la liste **à l'envers** (`for i = count-1 … 0`). Les deux inversions se cumulent : au sein d'un même groupe (shader, matériau, mesh), les transparents sont dessinés de l'avant vers l'arrière — l'inverse de ce qu'exige l'alpha blending. Une des deux inversions est de trop.
2. **La distance est le critère le plus faible pour les transparents** : dans la clé 64 bits, les hashes shader/matériau/mesh dominent les 12 bits de distance. Le back-to-front n'est donc garanti qu'entre objets partageant le même matériau ; deux matériaux transparents différents se dessinent groupés par état, pas par profondeur.
3. **Commentaire trompeur dans `OpaquePass`** : « items pre-sorted front-to-back » — en réalité la distance n'est pas encodée pour les opaques, le tri est purement par état (ce qui est un choix défendable, mais sans early-z bénéfique).
4. **Copier-coller dans `SkinnedMeshRendererComponent.LoadContent`** ([SkinnedMeshRendererComponent.cs:94-101](CasaEngine/Framework/Application/Components/SkinnedMeshRendererComponent.cs:94)) : `RegisterPostShadowCallback` est appelé deux fois avec la même lambda. Sans conséquence (c'est une affectation, pas un `+=`), mais c'est du code mort.
5. **Clé de groupe d'instancing fragile** ([StaticMeshRendererComponent.cs:351](CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs:351)) : `VertexBuffer.Tag as IntPtr?` vaut quasi certainement `null` pour tous, donc le regroupement repose uniquement sur la clé de tri dont les hashes 16 bits peuvent entrer en collision — deux meshes différents pourraient théoriquement finir dans le même batch instancié.
6. **Passes en O(2n)** : `OpaquePass` et `TransparentPass` parcourent chacune toute la liste en filtrant. Trivial à optimiser (la liste est triée par queue, une partition suffirait), mais négligeable aux tailles de scène actuelles.

Si vous voulez, je peux corriger le point 1 (l'inversion des transparents) et le doublon du point 4 — ce sont les deux plus concrets.