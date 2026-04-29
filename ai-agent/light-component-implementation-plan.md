# LightComponent - Plan d'implementation IA

## Regles obligatoires pour l'agent

1. Traiter une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Quand la tache est terminee et validee, remplacer `🚧` par `✅`.
4. Si le code est termine mais qu'une validation manque encore, utiliser `🧪` et noter juste dessous ce qui manque.
5. Si une tache est bloquee, utiliser `⚠️` et ajouter une note courte sur le blocage.
6. Mettre a jour ce fichier dans le meme commit que le code de la tache.
7. Faire exactement un commit compilable par tache atomique.
8. Ne jamais regrouper plusieurs taches dans le meme commit.
9. Respecter l'ordre du plan, sauf blocage documente dans la tache en cours.
10. Eviter toute allocation evitable dans les hot paths `Update`, `Draw`, collecte des lumieres et binding shader.
11. Toute modif shader / pipeline / renderer doit restaurer proprement l'etat GPU.
12. Le `RenderPipeline` ne doit jamais referencer `LightComponent` directement ; il ne consomme qu'une abstraction de rendu.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Validation minimale par tache

- Tache runtime / rendering: `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- Tache editor / editor services: `dotnet build .\\CasaEngine.Editor\\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- Tache demos / sample: `dotnet build .\\CasaEngine.Demos\\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`
- Fin de plan: build runtime + build editor + build demos + un smoke test manuel dans l'editeur ou une demo qui montre les 3 types de lumieres

## Etat de depart a garder en tete

- `LightingContext` ne supporte aujourd'hui que des directional lights + ambient.
- `RenderPipeline` remplit `view.Lighting` via `EnvironmentLightingResolver.Resolve(...)`.
- Les lumieres hardcodees vivent encore dans `EnvironmentLightingResolver`, `StaticMeshRendererComponent.DefaultLighting`, `SkinnedMeshRendererComponent.DefaultLighting` et du code de demo comme `MaterialDemo`.
- `EntityDetailsPanel` detecte automatiquement tout `EntityComponent` concret avec constructeur vide dans la boite `Add Component`.
- `GenericComponentEditor` sait deja editer `enum`, `bool`, `int`, `float`, `double`, `Vector3`, `Color`, `Guid` et `string`.
- La sauvegarde de world / entity passe par `CasaEngine.EditorServices.EditorEntityJsonSerializer` ; le chargement des composants passe deja par `ElementFactory.Load<T>(...)` et `Load(JObject)`.

## Cible architecturale

Objectif final:

- introduire un `LightComponent` derive de `SceneComponent` avec un parametre `LightType` expose en public: `Directional`, `Point`, `Spot`
- rendre ce composant editable dans l'editeur et serialisable dans les worlds / entities
- faire consommer les lumieres par le rendu via une structure runtime dediee (`LightingContext` et types de lumiere de rendu), pas via un couplage direct a `LightComponent`
- supprimer completement les lumieres hardcodees des renderers, de la resolution d'environnement et des demos / previews qui en dependent aujourd'hui

Principes de conception:

- `LightComponent` est une source d'auteuring ; le rendu manipule des donnees converties / extraites.
- Les lumieres directionnelles et spot utilisent l'orientation du `SceneComponent` ; les point et spot utilisent aussi la position.
- Garder un scope V1 minimal mais exploitable: couleur, intensite, portee, angles de spot si necessaire.
- Garder des caps fixes cote shader pour eviter les allocations et garder un binding deterministe.
- Si plus de lumieres sont presentes que le cap shader, appliquer une priorisation claire et documentee.

## Criteres d'acceptation globaux

- Un utilisateur peut ajouter un `LightComponent` depuis l'editeur, changer son type entre `Directional`, `Point`, `Spot`, sauvegarder puis recharger sans perte.
- Le `RenderPipeline` ne reference pas `LightComponent` ; il ne voit qu'un collecteur / provider et un `LightingContext` de rendu.
- Les materiaux lit forward utilisent les trois familles de lumieres au runtime.
- Aucune lumiere hardcodee ne subsiste dans le chemin runtime normal.
- Les previews / demos qui ont encore besoin d'un eclairage se donnent une vraie source explicite de lumiere.

## Risques a surveiller

- `LightingContext` n'a pas encore de support point / spot ; le travail touche aussi les shaders et leurs parametres.
- Les previews editor et certaines demos risquent de devenir noires des que les fallbacks hardcodes sont retires.
- La collecte des lumieres ne doit pas introduire de LINQ ni de nouvelles listes allouees par frame.
- Le binding shader doit remettre a zero les slots inactifs pour eviter les donnees stale.

## Taches

### ✅ LIGHT-001 - Poser les contrats runtime des lumieres de rendu
Objectif:
introduire des types de rendu explicites pour `Directional`, `Point` et `Spot`, ainsi qu'un `LightingContext` etendu sans brancher encore le pipeline sur `LightComponent`.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/LightingContext.cs`
- nouveaux types sous `CasaEngine/Framework/Rendering/` ou `CasaEngine/Framework/Rendering/Lighting/`
- `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`

Criteres d'acceptation:
- le runtime sait stocker un nombre borne de directional / point / spot lights
- les structures sont reutilisables sans allocation par frame
- les slots inactifs peuvent etre remis a zero explicitement au binding
- aucun renderer ne depend encore d'un composant d'auteuring

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`

Commit conseille:
`feat(rendering): add runtime light contracts`

---

### ✅ LIGHT-002 - Ajouter LightComponent et son enum d'auteuring
Objectif:
creer `LightComponent` comme `SceneComponent` avec son enum `LightType` et les parametres minimums necessaires pour piloter le rendu.

Fichiers / classes concernes:
- nouveau `CasaEngine/Framework/Scene/Entities/Components/LightComponent.cs`
- nouveau `LightType` ou enum integre au composant
- eventuels attributs `DisplayName` / `Browsable`

Perimetre minimum recommande:
- `LightType Type`
- `Color Color`
- `float Intensity`
- `float Range` pour `Point` et `Spot`
- `float InnerAngle` / `float OuterAngle` ou un angle unique pour `Spot`

Criteres d'acceptation:
- `LightComponent` se clone correctement
- `Load(JObject)` recharge ses proprietes sans casser l'heritage `SceneComponent`
- la direction utilise l'orientation du composant au lieu d'un champ redondant
- le composant reste exploitable meme si l'editeur generique est le seul panneau au debut

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`

Commit conseille:
`feat(scene): add light component authoring model`

---

### ✅ LIGHT-003 - Brancher la serialisation world / entity pour LightComponent
Objectif:
faire persister `LightComponent` dans les scenes via le chemin de sauvegarde deja utilise par l'editeur.

Fichiers / classes concernes:
- `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`
- `CasaEngine/Framework/Scene/Entities/Components/LightComponent.cs`
- eventuels tests de round-trip si une surface de tests est adaptee

Criteres d'acceptation:
- un world sauvegarde les champs du `LightComponent`
- le reload d'un world recree bien le composant avec son type et ses parametres
- les noms de champs JSON sont stables et explicites
- les coordonnees du `SceneComponent` restent prises en charge par le chemin existant

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- `dotnet build .\\CasaEngine.Editor\\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`

Commit conseille:
`feat(serialization): persist light component in world assets`

---

### ✅ LIGHT-004 - Rendre LightComponent editable dans l'editeur
Objectif:
garantir qu'un utilisateur peut ajouter et modifier `LightComponent` depuis l'editeur sans code WPF additionnel.

Fichiers / classes concernes:
- `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`
- `CasaEngine.Editor/Controls/ComponentEditors/ComponentEditorRegistry.cs`
- nouveau `LightComponentEditor` seulement si l'editeur generique ne suffit pas

Strategie recommandee:
- commencer par tirer parti de l'auto-decouverte de `EntityDetailsPanel`
- commencer par l'editeur generique, qui sait deja gerer `enum`, `Color`, `float` et `Vector3`
- n'ajouter un panneau specifique que s'il faut masquer / montrer les champs selon `LightType` ou rafraichir plus finement le viewport

Criteres d'acceptation:
- `LightComponent` apparait dans `Add Component`
- ses proprietes sont modifiables dans l'inspector
- les modifications passent par l'historique editor quand c'est possible
- changer `Type` entre `Directional`, `Point`, `Spot` ne casse pas l'inspector

Validation:
- `dotnet build .\\CasaEngine.Editor\\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`

Commit conseille:
`feat(editor): expose light component in inspector`

---

### ✅ LIGHT-005 - Introduire un collecteur de lumieres decouple du pipeline
Objectif:
creer la couche qui convertit les sources du world en donnees de rendu sans faire connaitre `LightComponent` au `RenderPipeline`.

Fichiers / classes concernes:
- nouveau collecteur sous `CasaEngine/Framework/Rendering/`
- `CasaEngine/Framework/Scene/World/World.cs` uniquement si un point d'entree de service est necessaire
- `CasaEngine/Framework/Rendering/RenderView.cs` si un cache / versioning de lumiere est requis

Direction recommandee:
- introduire une abstraction du style `IRenderLightSource`, `ILightRuntimeProvider` ou `IWorldLightCollector`
- laisser `LightComponent` s'adapter a cette abstraction
- permettre au collecteur de fusionner la lumiere de scene avec l'ambient issu de l'environnement

Criteres d'acceptation:
- le pipeline peut demander un `LightingContext` sans connaitre `LightComponent`
- la collecte est deterministe et borne le nombre de lumieres retenues
- aucun LINQ ni allocation evitable n'apparait dans la collecte par frame

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`

Commit conseille:
`refactor(rendering): add decoupled world light collector`

---

### ✅ LIGHT-006 - Brancher la collecte dans RenderPipeline et retirer le rig legacy
Objectif:
faire en sorte que `RenderPipeline` alimente `view.Lighting` via le collecteur et l'environnement, puis retirer les directional lights hardcodees d'`EnvironmentLightingResolver`.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/RenderPipeline.cs`
- `CasaEngine/Framework/Rendering/Environment/EnvironmentLightingResolver.cs`
- `CasaEngine/Framework/Rendering/RenderFrameFactory.cs` si la signature doit etre ajustee

Criteres d'acceptation:
- `RenderPipeline` ne parle qu'au collecteur / provider et a `LightingContext`
- `EnvironmentLightingResolver` ne recree plus de rig de directional lights hardcode
- l'ambient et les donnees d'environnement restent pris en compte
- le cache de lumiere reste coherent quand le world ou l'environnement changent

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`

Commit conseille:
`refactor(rendering): resolve view lighting from collected scene lights`

---

### ✅ LIGHT-007 - Etendre les shaders et materials au support Directional / Point / Spot
Objectif:
mettre a jour le binding et les shaders forward pour que les trois types de lumiere affectent les objets lit.

Fichiers / classes concernes:
- `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`
- shaders forward / lighting sous `CasaEngine.Shaders/` ou contenus equivalents charges par `Shaders\\LitForward`
- renderers / materials qui bindent `LightingContext`
- eventuels shaders skinned si le support doit etre coherent sur tous les mesh renderers lit

Criteres d'acceptation:
- les directional lights continuent de fonctionner
- les point lights et spot lights influencent les shaders lit
- les slots inactifs sont neutralises explicitement
- aucune fuite d'etat GPU n'est introduite

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`

Commit conseille:
`feat(rendering): support point and spot lights in forward shaders`

---

### ✅ LIGHT-008 - Supprimer les hardcodes restants dans les renderers et previews
Objectif:
retirer les derniers fallbacks lumineux caches dans les renderers et rebrancher les previews / demos sur des lumieres explicites.

Fichiers / classes concernes:
- `CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs`
- `CasaEngine/Framework/Application/Components/SkinnedMeshRendererComponent.cs`
- `CasaEngine.Demos/Demos/MaterialDemo.cs`
- mondes de preview editor ou helpers de preview qui dependaient d'un eclairage implicite

Criteres d'acceptation:
- `DefaultLighting` n'est plus une source hardcodee de lumiere de scene
- les previews material / editor qui doivent rester lisibles se donnent explicitement une lumiere ou un petit rig de preview
- aucune demo runtime ne depend encore d'un eclairage hardcode dans le renderer

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- `dotnet build .\\CasaEngine.Editor\\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- `dotnet build .\\CasaEngine.Demos\\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`

Commit conseille:
`refactor(rendering): remove hardcoded lighting fallbacks`

---

### ✅ LIGHT-009 - Ajouter une demo ou un scenario de validation visible
Objectif:
laisser une surface simple pour prouver que `Directional`, `Point` et `Spot` fonctionnent apres suppression des hardcodes.

Options recommandees:
- enrichir une demo existante avec un petit rig de 3 lumieres explicites
- ou ajouter un world / sample de validation qui permet d'editer puis sauvegarder un `LightComponent`

Criteres d'acceptation:
- il existe une surface visible qui montre les 3 types de lumieres
- cette surface ne depend plus d'un fallback hardcode du renderer
- la verification peut etre rejouee par un autre agent sans deduire de magie implicite

Validation:
- `dotnet build .\\CasaEngine.Demos\\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`

Commit conseille:
`feat(demos): add explicit light component validation scene`

---

### ✅ LIGHT-010 - Finaliser la doc courte et la validation de cloture
Objectif:
laisser le repo dans un etat testable avec une note courte sur l'usage de `LightComponent` et la validation finale.

Perimetre:
- doc courte dans `README.md`, `docs/` ou un fichier de note local si c'est la convention la moins intrusive
- rappeler comment ajouter `LightComponent`, quelles proprietes regler, et quelle demo / quel smoke utiliser

Criteres d'acceptation:
- documentation minimale disponible
- build runtime OK
- build editor OK
- build demos OK
- smoke test manuel execute sur la surface de validation choisie

Validation:
- `dotnet build .\\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- `dotnet build .\\CasaEngine.Editor\\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- `dotnet build .\\CasaEngine.Demos\\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`

Resultat de cloture:
- smoke automatise execute sur `Material system demo`
- capture validee dans `ai-agent/material-demo-lightcomponent-smoke.png`

Commit conseille:
`docs(lighting): document light component workflow`