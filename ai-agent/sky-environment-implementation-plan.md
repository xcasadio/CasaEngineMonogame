# Sky / Environment System — Plan d'implementation IA

## Regles obligatoires pour l'agent

1. Traiter une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Quand la tache est terminee et validee, remplacer `🚧` par `✅`.
4. Si le code est fait mais qu'une validation manque encore, utiliser `🧪` et noter ce qui manque juste sous la tache.
5. Si la tache est bloquee, utiliser `⚠️` et ajouter une note courte expliquant le blocage.
6. Mettre a jour ce fichier dans le meme commit que le code de la tache.
7. Faire exactement un commit compilable par tache atomique.
8. Ne jamais regrouper plusieurs taches dans le meme commit.
9. Respecter l'ordre du plan, sauf blocage documente dans la tache en cours.
10. Si une tache modifie le pipeline de rendu, les shaders ou les structures runtime partagees, verifier explicitement qu'aucune fuite d'etat GPU n'est introduite.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Validation minimale par tache

- Tache runtime rendering / shaders: `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`
- Tache editor integration: `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Tache demos uniquement: `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- A la fin du plan: build runtime + build editor + un smoke test de demo ou de preview editor lie a l'environnement

## Cible architecturale

Objectif final:

- disposer d'un systeme d'environnement global porte par le world
- autoriser un override optionnel par vue pour les previews et l'editeur
- permettre a l'editeur de selectionner le world lui-meme et d'editer ses parametres d'environnement
- separer clairement le rendu du ciel du calcul des donnees de lighting
- commencer par un environnement HDR cubemap global avant les probes locales
- garder un fallback compatible avec le `ClearColor` et le lighting legacy existant

Principe de livraison:

- d'abord poser les contrats runtime et les points d'extension
- ensuite ajouter le fond visuel du ciel
- ensuite deplacer la source de lighting vers l'environnement
- ensuite exposer ces parametres proprement dans l'editeur au niveau world
- enfin ouvrir la voie aux probes, au procedural et a l'atmosphere physique

## Parametres du World a exposer dans l'editeur

Perimetre recommande pour la V1:

- mode de fond global: `ClearColor`, `Cubemap`, fallback legacy
- couleur de fallback
- asset d'environnement global ou reference de cubemap
- multiplicateur ambient global
- multiplicateur specular global
- action explicite de rebuild / refresh si le runtime l'exige

Perimetre a repousser en V2:

- rotation fine de l'environnement si elle doit rester coherente entre fond visuel et IBL
- exposition, tint et autres controles artistiques avances
- source panorama HDR avec conversion ou import dedie
- reflection probes locales et blending entre probes
- atmosphere physique, soleil, nuages et cycle jour / nuit
- reglages avances d'irradiance, spherical harmonics et prefilter specular

Regle de scope:

- la V1 doit permettre d'editer le minimum utile directement sur le World
- tout ce qui suppose un pipeline d'import, une capture locale ou une coherence multi-systemes plus complexe reste en V2

## Taches

### ✅ ENV-001 — Introduire les contrats runtime de l'environnement
Objectif:
creer les types de base qui representent un environnement de scene sans changer le comportement actuel.

Fichiers / classes concernes:
- nouveaux fichiers sous `CasaEngine/Framework/Rendering/Environment/`
- `EnvironmentType`
- `EnvironmentBackgroundMode`
- `WorldEnvironmentSettings`
- `ResolvedEnvironmentSettings`

Criteres d'acceptation:
- les nouveaux types compilent sans modifier le rendu existant
- aucun renderer existant n'est encore branche sur ces types
- les noms restent stables et exploitables par l'editeur plus tard

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`feat(rendering): add runtime environment contracts`

---

### ✅ ENV-002 — Attacher l'environnement au world
Objectif:
faire du world la source de verite du systeme d'environnement global.

Fichiers / classes concernes:
- `CasaEngine/Framework/Scene/World/World.cs`
- eventuels fichiers runtime lies au cycle de vie du world

Criteres d'acceptation:
- `World` expose une instance d'environnement globale
- le cycle de vie est clair: creation, reset, nettoyage
- aucun changement visible de rendu tant qu'aucun environnement explicite n'est configure

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`feat(rendering): attach environment settings to world`

---

### ✅ ENV-003 — Ajouter un override optionnel d'environnement par vue
Objectif:
preparer les cas preview, material inspector et viewport editor sans casser le world par defaut.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/RenderView.cs`
- `CasaEngine/Framework/Rendering/ViewDefinition.cs`

Criteres d'acceptation:
- une vue peut referencer un override d'environnement
- l'absence d'override conserve automatiquement l'environnement du world
- aucun changement visible tant que le nouveau champ n'est pas utilise

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`feat(rendering): add per-view environment override`

---

### ✅ ENV-004 — Resoudre l'environnement effectif d'une vue
Objectif:
creer un point unique qui decide quel environnement est actif pour une vue donnee et qui gere le fallback legacy.

Fichiers / classes concernes:
- nouveaux fichiers sous `CasaEngine/Framework/Rendering/Environment/`
- `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs`
- eventuellement `CasaEngine/Framework/Rendering/RenderContext.cs`

Criteres d'acceptation:
- la resolution `view override -> world environment -> fallback legacy` est centralisee
- le fallback vers `ClearColor` et le lighting legacy reste intact
- la logique n'est pas dupliquee dans plusieurs renderers

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`refactor(rendering): resolve effective environment per view`

---

### ✅ ENV-005 — Ajouter une vraie SkyPass au pipeline forward
Objectif:
introduire un emplacement officiel pour le rendu du fond 3D avant l'opaque.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/Draw/RenderPass.cs`
- `CasaEngine/Framework/Rendering/ForwardRenderPipeline.cs`
- nouveau `CasaEngine/Framework/Rendering/Draw/SkyPass.cs`

Criteres d'acceptation:
- le pipeline connait un `SkyPass`
- la pass peut etre inseree ou retiree proprement
- en l'absence de sky configure, le comportement actuel reste identique

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`feat(rendering): add sky pass to forward pipeline`

---

### ✅ ENV-006 — Creer le renderer et le shader de sky cubemap
Objectif:
poser les ressources GPU minimales pour dessiner un ciel cubemap sans melanger cela avec les materials de scene.

Fichiers / classes concernes:
- nouveau shader sous `CasaEngine/Content/Shaders/`
- nouveaux fichiers sous `CasaEngine/Framework/Rendering/Environment/`

Criteres d'acceptation:
- le rendu du ciel utilise son propre shader et ses propres etats GPU
- les etats GPU sont restaures correctement apres la pass
- aucune dependance n'est ajoutee aux materials runtime existants

Validation:
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Commit conseille:
`feat(rendering): add cubemap sky renderer`

---

### ⏳ ENV-007 — Supporter un environnement cubemap comme fond visuel
Objectif:
faire fonctionner le premier vrai mode de ciel moderne: cubemap / HDRI deja converti en cubemap.

Fichiers / classes concernes:
- fichiers d'environnement runtime
- `CasaEngine/Framework/Rendering/Draw/SkyPass.cs`
- chargeurs ou helpers d'assets si necessaire

Criteres d'acceptation:
- un world peut afficher un fond cubemap
- si aucun cubemap n'est assigne, le moteur garde le fallback `ClearColor`
- le ciel suit la camera sans introduire de parallaxe parasite

Commit conseille:
`feat(rendering): render cubemap environment background`

---

### ⏳ ENV-008 — Introduire un asset d'environnement global
Objectif:
sortir la donnee d'environnement du simple champ texture et preparer les futures donnees de lighting.

Fichiers / classes concernes:
- nouveaux fichiers sous `CasaEngine/Framework/Assets/` et/ou `CasaEngine/Framework/Rendering/Environment/`
- eventuels chargeurs d'assets

Criteres d'acceptation:
- l'environnement global peut porter au minimum une cubemap de fond et des metadonnees de lighting
- le systeme ne depend pas d'un `TextureCube` directement expose partout
- la forme choisie laisse de la place a une version prefiltrée plus tard

Commit conseille:
`feat(rendering): add global environment asset model`

---

### ⏳ ENV-009 — Centraliser la source de lighting au niveau environnement
Objectif:
faire en sorte que le lighting global ne soit plus implicitement pilote par les valeurs hardcodees des renderers.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/LightingContext.cs`
- nouveaux fichiers d'environnement / lighting runtime
- `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs`

Criteres d'acceptation:
- le `LightingContext` d'une vue peut etre derive de l'environnement effectif
- le fallback legacy reste disponible tant que l'environnement ne fournit rien
- la construction du lighting global n'est plus dispersee dans chaque renderer

Commit conseille:
`refactor(rendering): derive view lighting from environment`

---

### ⏳ ENV-010 — Brancher le renderer de meshes statiques sur le lighting resolu
Objectif:
remplacer la dependance implicite a `DefaultLighting` par la source de lighting centralisee.

Fichiers / classes concernes:
- `CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs`

Criteres d'acceptation:
- le renderer statique consomme le lighting resolu par la vue
- le fallback legacy conserve le rendu existant si aucune donnee d'environnement n'est prete
- aucune allocation inutile n'est introduite sur le hot path

Commit conseille:
`refactor(rendering): route static mesh lighting through environment`

---

### ⏳ ENV-011 — Brancher le renderer de meshes skinnes sur le lighting resolu
Objectif:
aligner le chemin skinned sur le meme modele de lighting que le renderer statique.

Fichiers / classes concernes:
- `CasaEngine/Framework/Application/Components/SkinnedMeshRendererComponent.cs`

Criteres d'acceptation:
- le renderer skinned consomme lui aussi le lighting resolu
- le comportement reste compatible avec les assets actuels
- le lighting d'une vue n'est plus duplique entre renderer statique et skinned

Commit conseille:
`refactor(rendering): route skinned lighting through environment`

---

### ⏳ ENV-012 — Ajouter les bindings shader pour l'environnement global
Objectif:
preparer les shaders de materiaux a consommer des donnees d'environnement globales en plus des lights directes.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`
- `CasaEngine/Content/Shaders/Lighting.fxh`
- `CasaEngine/Content/Shaders/LitForward.fx`
- classes de binding shader global si necessaire

Criteres d'acceptation:
- les nouveaux parametres shader sont centralises et nommes clairement
- le binding global reste compatible avec les shaders existants
- aucun material n'est casse quand l'environnement global est absent

Commit conseille:
`feat(rendering): add global environment shader bindings`

---

### ⏳ ENV-013 — Implementer une premiere specular IBL globale
Objectif:
remplacer la simple reflexion purement materiau par une source specular globale issue de l'environnement effectif.

Fichiers / classes concernes:
- `CasaEngine/Framework/Materials/Runtime/LitDiffuseMaterial.cs`
- `CasaEngine/Content/Shaders/LitForward.fx`
- fichiers runtime d'environnement et de lighting

Criteres d'acceptation:
- les materiaux lit peuvent utiliser une specular environment globale
- le systeme garde une compatibilite raisonnable avec `ReflectionCube` existant
- la premiere version peut etre simple mais doit etre clairement separable d'une future prefilter GGX

Commit conseille:
`feat(rendering): add initial global specular ibl`

---

### ⏳ ENV-014 — Implementer une premiere ambient diffuse issue de l'environnement
Objectif:
faire evoluer le simple `AmbientColor` vers une approximation pilotee par l'environnement global.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/LightingContext.cs`
- `CasaEngine/Content/Shaders/Lighting.fxh`
- `CasaEngine/Content/Shaders/LitForward.fx`

Criteres d'acceptation:
- si un environnement global est present, l'ambient diffuse n'est plus uniquement une constante arbitraire
- le fallback legacy conserve le rendu des scenes existantes
- la voie reste ouverte vers irradiance ou spherical harmonics plus tard

Commit conseille:
`feat(rendering): add initial diffuse environment lighting`

---

### ⏳ ENV-015 — Ajouter un systeme dirty / rebuild a la demande
Objectif:
eviter de recalculer les donnees d'environnement a chaque frame et preparer les changements runtime ou editor.

Fichiers / classes concernes:
- fichiers runtime d'environnement
- eventuels services d'assets ou pipelines de preview

Criteres d'acceptation:
- l'environnement expose un etat dirty ou une version de donnees
- le rebuild des donnees de lighting est demande explicitement
- aucun recalcul couteux n'est fait sans raison sur la boucle de rendu

Commit conseille:
`feat(rendering): add on-demand environment rebuild policy`

---

### ⏳ ENV-016 — Ajouter la selection du world dans l'editeur
Objectif:
permettre au document world de selectionner explicitement le world racine, et pas seulement une entite ou un composant.

Fichiers / classes concernes:
- `CasaEngine.Editor/EditorSelectionKind.cs`
- `CasaEngine.Editor/Controls/EntitiesPanel.cs`
- `CasaEngine.Editor/Game1.cs`

Criteres d'acceptation:
- l'editeur expose un mode de selection du world racine
- la hierarchie peut selectionner le world sans casser la selection d'entites existante
- l'inspector peut distinguer clairement `World`, `WorldEntity` et `WorldComponent`

Commit conseille:
`feat(editor): add world root selection to world document`

---

### ⏳ ENV-017 — Ajouter un inspecteur des parametres d'environnement du world
Objectif:
quand le world est selectionne, afficher et editer les parametres utiles de l'environnement V1.

Fichiers / classes concernes:
- nouveaux fichiers sous `CasaEngine.Editor/Controls/` pour l'inspecteur world
- `CasaEngine.Editor/Game1.cs`
- eventuels registres d'editeurs de proprietes

Parametres a exposer pour cette tache (V1):
- mode de fond (`ClearColor`, cubemap, fallback)
- couleur de fallback
- asset d'environnement global ou reference de cubemap
- multiplicateur ambient global
- multiplicateur specular global
- action explicite de rebuild / refresh si necessaire

Criteres d'acceptation:
- si le world est selectionne, l'inspector n'affiche plus l'etat vide de l'inspecteur d'entite
- les parametres d'environnement V1 sont editables sans passer par du code ou un fichier JSON
- les changements appliques sont visibles sur la vue du world ou sur une preview associee
- les parametres explicitement repousses en V2 ne sont pas exposes prematurement dans cette premiere UI

Commit conseille:
`feat(editor): add world environment inspector`

---

### ⏳ ENV-018 — Brancher undo/redo, dirty state et persistance des parametres world
Objectif:
faire des modifications d'environnement du world un vrai flux d'authoring editor, avec historique et sauvegarde.

Fichiers / classes concernes:
- inspecteur world
- services editor d'historique / dirty state / sauvegarde world
- eventuels writers editor du world

Criteres d'acceptation:
- chaque edition de parametre passe par l'historique editor
- le world est marque dirty apres modification
- les parametres d'environnement du world sont sauvegardes et recharges correctement

Commit conseille:
`feat(editor): persist world environment authoring changes`

---

### ⏳ ENV-019 — Integrer l'override d'environnement dans les previews et l'editeur
Objectif:
utiliser la capacite de surcharge par vue pour les cas material preview, asset preview et viewport editor.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/PreviewPipeline.cs`
- fichiers editor et preview relies aux render views
- eventuels fichiers de material preview

Criteres d'acceptation:
- une preview peut utiliser un environnement dedie sans modifier le world principal
- les vues editor et preview ne fuient pas leur environnement les unes vers les autres
- le comportement reste stable quand aucune surcharge n'est definie

Commit conseille:
`feat(editor): add per-view environment overrides to previews`

---

### ⏳ ENV-020 — Ajouter une demo ou etendre une demo existante
Objectif:
fournir un cas visible qui prouve la separation entre fond visuel et lighting d'environnement.

Fichiers / classes concernes:
- `CasaEngine.Demos/`
- potentiellement `CasaEngine.Demos/Demos/MaterialDemo.cs`

Criteres d'acceptation:
- la demo montre clairement au moins un environnement cubemap global
- la demo permet d'observer le fond visuel et son impact sur le lighting
- la validation de la demo est documentee dans ce fichier ou dans une note associee

Commit conseille:
`test(demos): add environment rendering validation demo`

---

### ⏳ ENV-021 — Documenter le systeme d'environnement et les limites de la V1
Objectif:
laisser une doc courte pour l'equipe et pour les futurs agents avant d'ouvrir les chantiers probes et atmosphere.

Fichiers / classes concernes:
- `README.md` ou `docs/`
- eventuellement ce fichier de plan pour la cloture

Criteres d'acceptation:
- la doc explique ou vit l'environnement global
- la doc explique le role de l'override par vue
- la doc liste clairement ce qui n'est pas encore traite en V1

Commit conseille:
`docs(rendering): document environment system v1`

## Taches de seconde vague apres la V1

### ⏳ ENV-022 — Ajouter une entree Panorama HDR
Objectif:
supporter un format source pratique pour les artistes sans forcer une cubemap authoring manuelle.

Commit conseille:
`feat(rendering): add panorama hdr environment source`

---

### ⏳ ENV-023 — Introduire le modele runtime des reflection probes
Objectif:
poser les types et la selection runtime des probes locales sans encore faire tout le baking.

Commit conseille:
`feat(rendering): add reflection probe runtime model`

---

### ⏳ ENV-024 — Ajouter les reflection probes statiques et leur blending local
Objectif:
ameliorer les reflexions interieures et les cas locaux apres la mise en place de l'environnement global.

Commit conseille:
`feat(rendering): add local reflection probe blending`

---

### ⏳ ENV-025 — Ajouter un producteur de ciel procedural simple
Objectif:
fournir un mode de ciel leger pour les scenes sans HDRI.

Commit conseille:
`feat(rendering): add procedural sky producer`

---

### ⏳ ENV-026 — Ajouter un producteur d'atmosphere physique
Objectif:
ouvrir la voie a un ciel physiquement plausible, au soleil, a l'horizon et a la transition sol / espace.

Commit conseille:
`feat(rendering): add physical atmosphere producer`

## Exemples CasaEngine.Demos a livrer

- `EnvironmentShowcaseDemo`: comparaison claire entre fallback `ClearColor`, fond cubemap global et impact sur l'eclairage de scene
- extension ou variante de `MaterialDemo`: lecture rapide du specular / ambient environment sur plusieurs materiaux et rugosites percussives
- si utile apres la V1 editor: une preview dediee montrant qu'un override par vue ne modifie pas l'environnement global du world
