# CasaEngine Folder Hierarchy Audit Report

Audit ciblé sur `CasaEngine/Core`, `CasaEngine/Engine`, `CasaEngine/Framework` et tous leurs sous-dossiers.

## 1. Résumé exécutif

Verdict global : **partiellement bon, mais pas encore assez lisible ni assez strict pour ressembler à une hiérarchie moderne de moteur de jeu**.

La structure actuelle reflète une intention saine :

- `Core` contient surtout les primitives réutilisables.
- `Engine` contient surtout les briques bas niveau.
- `Framework` contient surtout l'orchestration runtime, la scène, le rendu et le gameplay.

Mais cette hiérarchie n'est **pas suffisamment auto-explicative** pour trois raisons :

1. plusieurs dossiers ont des noms trop larges, trop historiques, ou trop proches les uns des autres ;
2. plusieurs classes sont rangées dans des dossiers qui ne reflètent pas leur responsabilité réelle ;
3. le découpage en couches n'est pas entièrement respecté dans le code, donc l'arborescence ne représente pas encore une frontière architecturale fiable.

## 2. Cartographie actuelle

### 2.1 `Core`

Sous-dossiers observés :

- `Collections`
- `Design`
- `Helpers`
- `Log`
- `Maths`
- `MultiThreading`
- `Packing`
- `Parser`
- `Serialization`

Lecture globale :

- Le rôle de `Core` est compréhensible.
- Les dossiers sont majoritairement cohérents.
- Les principaux problèmes viennent surtout de `Helpers`, `Maths`, `Parser`, `Log` et `Packing`.

### 2.2 `Engine`

Sous-dossiers observés :

- `Animations`
- `Input`
- `Physics`
- `Plugins`
- `Primitives2D`
- `Primitives3D`

Lecture globale :

- `Input` et `Physics` sont bien placés.
- `Primitives3D` reste cohérent comme brique technique.
- `Primitives2D` pose un vrai problème de couche.
- `Plugins` est utile mais son nom est trop large par rapport à ce qu'il contient réellement.

### 2.3 `Framework`

Sous-dossiers observés :

- `AI`
- `Assets`
- `Audio`
- `Debugger`
- `Entities`
- `Game`
- `GameFramework`
- `Graphics`
- `Graphics2D`
- `GUI`
- `Input`
- `Materials`
- `Physics`
- `Project`
- `Rendering`
- `Scripting`
- `SpacePartitioning`
- `Transform`
- `World`

Fichiers à la racine du dossier :

- `Constants.cs`
- `ObjectBase.cs`

Lecture globale :

- `Framework` concentre presque tout le runtime haut niveau.
- C'est la zone la plus riche, mais aussi la plus hétérogène.
- Les ambiguïtés principales sont `Game` vs `GameFramework`, `Graphics` vs `Rendering`, `GUI`, `Debugger`, `Graphics2D`, `AI`, ainsi que les fichiers posés à la racine.

## 3. Évaluation des noms de dossiers

### 3.1 Dossiers globalement bien nommés

- `Core/Collections`
- `Core/Serialization`
- `Engine/Input`
- `Engine/Physics`
- `Framework/Assets`
- `Framework/Materials`
- `Framework/Rendering`
- `Framework/Entities`
- `Framework/Audio`

Ces dossiers décrivent correctement leur rôle, même si certains méritent ensuite une sous-structuration plus claire.

### 3.2 Dossiers corrects mais perfectibles

#### `Core/Maths`

Le contenu est cohérent, mais `Maths` est un nom peu idiomatique dans un codebase .NET moderne. `Math`, `Mathematics` ou `Math` avec sous-dossiers spécialisés est plus lisible.

#### `Core/Parser`

Le rôle est clair, mais `Parsing` serait plus explicite et plus homogène avec le reste du projet.

#### `Core/Log`

Le dossier est utile, mais `Logging` ou `Diagnostics/Logging` serait plus standard.

#### `Framework/Project`

Le dossier est utile mais trop petit et trop transverse. Son contenu relève plutôt de la configuration runtime / projet que d'un module autonome.

#### `Engine/Plugins`

Le dossier est utile, mais le contenu actuel correspond surtout au chargement d'assembly gameplay. `Extensions`, `RuntimeExtensions`, `Assemblies` ou `PluginLoading` décrirait mieux la responsabilité réelle.

### 3.3 Dossiers mal nommés ou trop ambigus

#### `Core/Helpers`

Le dossier regroupe des responsabilités trop différentes :

- géométrie et math (`BoundingBoxHelper`, `MatrixExtensions`, `Vector3Helper`, `RayHelper`)
- chaînes et formatage (`StringExtension`, `StringBuilderExtensions`, `NumericFormatExtensions`)
- temps (`GameTimeHelper`)
- hasard (`RandomExtension`)

Le mot `Helpers` masque le vrai découpage. C'est le dossier le moins moderne et le moins auto-documenté de `Core`.

#### `Framework/Game`

Le dossier ne contient pas du gameplay au sens métier. Il contient surtout le **runtime host** : boucle de jeu, bootstrap, settings, contexte runtime, composants globaux. Le nom `Game` entre en collision conceptuelle avec `GameFramework`.

#### `Framework/GameFramework`

Le dossier contient des abstractions gameplay (`GameMode`, `Pawn`, `PlayerController`, `Controller`, `Player`). Le contenu est légitime, mais le nom est trop proche de `Game`, ce qui rend la lecture globale confuse.

#### `Framework/Graphics`

Le dossier ne contient pas le pipeline graphique. Il contient surtout :

- modèles runtime (`StaticModel`, `SkinnedMesh`, `RiggedModel`)
- structures de mesh (`SubMesh`, `StaticModelMesh`, `StaticModelNode`)
- formes sérialisables (`Shapes`)

Le mot `Graphics` est donc trop large. `Models`, `Geometry`, `SceneGeometry` ou `Meshes` serait plus précis.

#### `Framework/GUI`

Le terme `GUI` est daté et moins clair que `UI`. En plus, le dossier mélange :

- abstractions runtime UI
- types généraux de navigation d'écrans
- backend MGUI

Le lecteur ne sait pas immédiatement ce qui est contrat abstrait et ce qui est implémentation concrète.

#### `Framework/Debugger`

Le contenu correspond davantage à des outils de debug runtime (`DebugManager`, `FpsCounter`, `TimeRuler`, `OctreeVisualizer`) qu'à un débogueur au sens outil. `Debug`, `DebugTools` ou `Diagnostics` serait plus clair.

#### `Framework/Graphics2D`

Le dossier ne contient que `Line2d` et `Line2dRenderer`. Il est trop fin pour justifier une catégorie autonome sous ce nom. Il gagnerait à être absorbé dans un sous-dossier plus explicite de rendu 2D.

#### `Framework/AI/Reinforcement Learning`

Le dossier contient un espace dans son nom, alors que son namespace devient `Reinforcement_Learning`. Ce décalage rend la structure peu propre et peu moderne.

## 4. Dossiers utiles, redondants, ou trop larges

### 4.1 Dossiers utiles et pertinents

- `Engine/Input` vs `Framework/Input` : découpage utile entre état brut des périphériques et routage runtime.
- `Engine/Physics` vs `Framework/Physics` : découpage utile entre définitions/configuration et intégration runtime.
- `Framework/Rendering` : dossier structurant et cohérent.
- `Framework/Materials` : dossier central et utile, même s'il est trop plat.

### 4.2 Dossiers redondants ou trop proches conceptuellement

#### `Framework/Game` et `Framework/GameFramework`

Ces deux dossiers décrivent deux sens différents du mot "game" :

- le runtime host
- le gameplay framework

Le découpage de fond est valide, mais la nomenclature est redondante et confuse.

#### `Framework/Graphics`, `Framework/Graphics2D` et `Framework/Rendering`

La séparation de fond est défendable, mais les noms actuels ne rendent pas le découpage assez évident :

- `Rendering` = pipeline, vues, surfaces, passes, shaders
- `Graphics` = modèles, maillages, formes
- `Graphics2D` = mini utilitaires 2D

Dans un moteur moderne, ces concepts devraient être visibles directement dans les noms.

### 4.3 Dossiers trop plats ou trop fins

#### `Framework/World`

Le dossier contient seulement `World.cs`. La responsabilité est importante, donc le dossier n'est pas inutile, mais la catégorie est trop mince pour être vraiment structurante.

#### `Framework/Transform`

Même constat : le dossier n'héberge actuellement qu'une interface (`ITransformableObject.cs`). Il peut rester s'il est destiné à grandir, sinon il peut être absorbé dans `Entities` ou `Scene`.

#### `Framework/SpacePartitioning`

Le dossier n'est aujourd'hui qu'une enveloppe autour de `Octree`. Il est acceptable si d'autres structures spatiales sont prévues, sinon il est prématurément générique.

## 5. Placement des classes

### 5.1 Classes bien placées

- `Framework/Rendering/RenderView.cs`
- `Framework/Rendering/ViewManager.cs`
- `Framework/Input/InputRouter.cs`
- `Framework/Graphics/StaticModel.cs`
- `Framework/Entities/Entity.cs`

Leur emplacement reflète correctement leur rôle.

### 5.2 Classes placées dans des dossiers discutables

#### `Engine/Primitives2D/Primitive2D.cs`

Cette classe dépend de `CasaEngine.Framework.Rendering.Shaders`, ce qui introduit une dépendance montante de `Engine` vers `Framework`.

Conséquence :

- soit `Primitive2D` n'appartient pas à `Engine`
- soit les éléments shader qu'elle consomme ne doivent pas vivre dans `Framework`

Dans l'état, le dossier dit "bas niveau", mais le code dépend du haut niveau.

#### `Framework/Game/Components/Editor/AxisComponent.cs`
#### `Framework/Game/Components/Editor/GridComponent.cs`

Le dossier s'appelle `Editor`, mais le namespace principal exposé est `CasaEngine.Framework.Game.Components.DebugTools`. Le dossier réel et l'API logique racontent deux histoires différentes.

#### `Framework/GUI/MGUI/UIRoot.cs`
#### `Framework/GUI/MGUI/ScreenStack.cs`
#### `Framework/GUI/MGUI/ViewRenderHost.cs`
#### `Framework/GUI/MGUI/WorldUIComponent.cs`

Ces fichiers sont physiquement rangés dans `GUI/MGUI`, mais une partie d'entre eux reste dans le namespace racine `CasaEngine.Framework.GUI`. Cela brouille la frontière entre API abstraite et backend concret.

#### `Framework/ObjectBase.cs`
#### `Framework/Constants.cs`

Ces classes sont directement à la racine de `Framework`. C'est un signal qu'il manque un dossier de type `Runtime`, `Common`, `Foundation`, `Configuration` ou `ContentModel`.

#### `Framework/Assets/Animations/RiggedModelLoader.cs`

Cette classe est rangée avec les assets d'animation, tandis que d'autres loaders vivent dans `Framework/Assets/Loaders`. Le projet mélange deux logiques :

- regroupement par domaine métier
- regroupement par type technique (`Loaders`)

Cette incohérence rend le dossier `Assets` moins prévisible.

#### `Core/Packing/*.Test.cs`

Les fichiers `ArevaloRectanglePacker.Test.cs`, `CygonRectanglePacker.Test.cs`, `OutOfSpaceException.Test.cs`, `RectanglePacker.Test.cs`, `SimpleRectanglePacker.Test.cs` sont dans le runtime, même s'ils sont protégés par `#if UNITTEST`.

En plus, ils utilisent encore le namespace `CasaEngineCommon.Packing`.

Cela donne trois signaux négatifs :

- test et runtime cohabitent dans le même dossier
- ancien nom de namespace encore présent
- héritage historique non nettoyé dans une couche supposée stable

## 6. Qualité du découpage en couches

## 6.1 Ce qui fonctionne

- `Core` ne dépend pas de `Engine` ni de `Framework`.
- `Framework` dépend logiquement de `Core` et `Engine`.
- plusieurs modules expriment déjà correctement leur niveau de responsabilité.

## 6.2 Ce qui affaiblit la séparation

### Dépendance montante dans `Engine`

`Engine/Primitives2D/Primitive2D.cs` référence `CasaEngine.Framework.Rendering.Shaders`.

C'est la preuve la plus claire que l'arborescence ne reflète pas encore une vraie frontière architecturale.

### Tout vit dans le même assembly

`Core`, `Engine` et `Framework` sont aujourd'hui organisés par dossiers dans `CasaEngine.csproj`, pas par assemblies séparés.

Conséquence :

- le découpage dépend surtout de la discipline humaine ;
- les violations de couche restent faciles ;
- la hiérarchie semble plus forte qu'elle ne l'est réellement.

### `Framework` mélange plusieurs axes de découpage

On y trouve en même temps :

- orchestration runtime
- scène et entités
- gameplay
- UI
- rendu
- matériaux
- debug runtime
- configuration projet
- IA générale

Le problème n'est pas que ces modules coexistent, mais qu'ils ne sont pas regroupés sous des familles plus explicites.

## 7. Évaluation dossier par dossier

### 7.1 `Core`

Verdict : **base saine, mais besoin d'un nettoyage sémantique**.

À conserver :

- `Collections`
- `Serialization`
- `MultiThreading`
- `Packing`

À renommer ou re-catégoriser :

- `Helpers` -> à éclater
- `Maths` -> `Math`
- `Parser` -> `Parsing`
- `Log` -> `Logging`

### 7.2 `Engine`

Verdict : **petite couche bas niveau utile, mais pas encore architecturalement pure**.

À conserver :

- `Input`
- `Physics`

À requalifier :

- `Primitives2D`
- `Plugins`

À surveiller :

- `Primitives3D`, selon qu'il s'agit de géométrie de base ou d'objets plus proches du runtime rendu.

### 7.3 `Framework`

Verdict : **zone la plus fonctionnelle, mais aussi la plus difficile à lire**.

À conserver comme familles :

- `Assets`
- `Rendering`
- `Materials`
- `Entities`
- `Input`
- `Audio`
- `Physics`

À clarifier fortement :

- `Game`
- `GameFramework`
- `Graphics`
- `Graphics2D`
- `GUI`
- `Debugger`
- `AI`
- `Project`

## 8. Hiérarchie cible recommandée

La recommandation la plus compréhensible, sans changer immédiatement le comportement du moteur, est de tendre vers ceci :

```text
CasaEngine/
  Core/
    Collections/
    Design/
    Logging/
    Math/
      Curves/
      Geometry/
      Extensions/
    Packing/
    Parsing/
    Serialization/
    Text/
    Threading/

  Engine/
    Environment/
    Input/
      Devices/
      Providers/
      Sequences/
    Physics/
      Definitions/
    Plugins/
    Primitives/
      2D/
      3D/

  Runtime/
    Application/
      Bootstrap/
      Settings/
      Context/
    Content/
      Assets/
        Animations/
        Sprites/
        Textures/
        TileMap/
      Loaders/
      Registry/
      Project/
    Scene/
      World/
      Entities/
      Components/
      Transform/
    Gameplay/
      Framework/
      AI/
        Messaging/
        Navigation/
        Pathfinding/
        StateMachines/
        Experimental/
    Rendering/
      Views/
      Surfaces/
      Pipeline/
      Models/
      Geometry/
      Materials/
      Lighting/
      Shaders/
      Debug/
      UI/
        Abstractions/
        MGUI/
    Audio/
    Physics/
```

### Remarques importantes

- Le mot `Runtime` est ici plus clair que `Framework`.
- `GameFramework` devrait devenir `Gameplay` ou `Gameplay/Framework`.
- `GUI` devrait devenir `UI`.
- `Graphics` devrait être éclaté entre `Rendering/Models`, `Rendering/Geometry` et éventuellement `Rendering/Debug`.
- `Graphics2D` ne mérite pas une catégorie autonome sous son nom actuel.

## 9. Priorités de refactor

### Priorité 1

- supprimer les violations de couche
- déplacer les fichiers de test hors de `Core/Packing`
- nettoyer les anciens namespaces `CasaEngineCommon.*`

### Priorité 2

- clarifier les noms de dossiers les plus ambigus : `GUI`, `Debugger`, `Game`, `GameFramework`, `Maths`, `Parser`
- supprimer les incohérences dossier/namespace

### Priorité 3

- éclater les dossiers trop larges : `Core/Helpers`, `Framework/Materials`, `Framework/AI`
- ranger les fichiers racine de `Framework` dans des catégories explicites

### Priorité 4

- envisager un découpage par assembly (`CasaEngine.Core`, `CasaEngine.Engine`, `CasaEngine.Runtime`) seulement après nettoyage sémantique et correction des dépendances montantes

## 10. Verdict final

### La hiérarchie est-elle bonne ?

**Pas encore complètement.**

### Les dossiers portent-ils bien leurs noms ?

**Partiellement seulement.** Certains sont bons, d'autres sont trop génériques, trop datés, ou trop proches sémantiquement.

### Sont-ils utiles ou redondants ?

**La plupart sont utiles**, mais plusieurs sont soit trop fins, soit trop plats, soit redondants dans leur vocabulaire.

### Les classes sont-elles dans les bons dossiers ?

**Pas toujours.** Il existe plusieurs cas clairs de placement perfectible ou incohérent.

### Les dossiers représentent-ils bien les couches du moteur ?

**Partiellement.** L'intention de couche existe, mais elle n'est pas encore suffisamment stricte ni suffisamment lisible pour être considérée comme moderne et évidente.

### Conclusion

CasaEngine dispose déjà d'une bonne base de structuration, mais il lui manque un **nettoyage de nomenclature**, un **resserrage du découpage runtime**, et la **suppression de quelques héritages historiques** pour obtenir une hiérarchie réellement compréhensible, moderne et maintenable.