# CasaEngine Folder Hierarchy Refactor Tasks

Backlog de refactor dérivé de l'audit de hiérarchie.

## Règles de travail

- Une sous-tâche par commit.
- Ne pas casser l'API sans compatibilité.
- Toujours faire compiler la solution entre deux sous-phases.
- Commencer par les corrections de structure sans impact fonctionnel.

## Phase 1 — Corriger les violations structurelles franches

### HIER-001

Statut : ✅ Done

Objectif : supprimer la dépendance montante `Engine -> Framework` dans `Engine/Primitives2D/Primitive2D.cs`.

Travail attendu :

- soit déplacer `Primitive2D` vers une couche runtime/rendu plus adaptée
- soit descendre les constantes shader nécessaires dans une couche plus basse
- soit introduire une abstraction locale qui ne référence plus `Framework.Rendering.Shaders`

Critère d'acceptation :

- aucun fichier sous `CasaEngine/Engine` ne dépend de `CasaEngine.Framework.*`

Exécution :

- `Engine/Primitives2D/Primitive2D.cs` utilise maintenant des noms de paramètres d'effet locaux (`PrimitiveEffectParameterNames`) et ne référence plus `Framework.Rendering.Shaders`

### HIER-002

Statut : ✅ Done

Objectif : sortir les fichiers `*.Test.cs` de `CasaEngine/Core/Packing`.

Travail attendu :

- déplacer les tests vers `CasaEngine.Tests`
- supprimer les anciens namespaces `CasaEngineCommon.Packing`
- garder `Core/Packing` strictement runtime

Critère d'acceptation :

- aucun fichier de test ne reste dans `CasaEngine/Core`

Exécution :

- les tests `Packing` ont été recréés dans `CasaEngine.Tests/Packing`
- les anciens fichiers `*.Test.cs` ont été supprimés de `CasaEngine/Core/Packing`

### HIER-003

Statut : ✅ Done

Objectif : aligner dossiers et namespaces pour les zones déjà identifiées.

Travail attendu :

- clarifier `Game/Components/Editor` vs `Game.Components.DebugTools`
- clarifier `GUI/MGUI` vs `CasaEngine.Framework.GUI`
- supprimer les reliquats de namespaces historiques

Critère d'acceptation :

- les namespaces exposés correspondent au rangement physique ou disposent d'une compatibilité explicitement documentée

Exécution :

- les zones les plus incohérentes ont été réorganisées physiquement
- la stratégie de compatibilité des namespaces conservés est documentée dans `ai-agent/audits/CasaEngine_folder_hierarchy_namespace_compatibility.md`

## Phase 2 — Clarifier la nomenclature du runtime

### HIER-004

Statut : ✅ Done

Objectif : renommer les catégories trop ambiguës de `Framework`.

Travail attendu :

- `Framework/Game` -> `Runtime/Application` ou équivalent
- `Framework/GameFramework` -> `Runtime/Gameplay` ou `Gameplay/Framework`
- `Framework/GUI` -> `Runtime/UI`
- `Framework/Debugger` -> `Runtime/Debug` ou `Runtime/Diagnostics`

Critère d'acceptation :

- un nouveau contributeur peut distinguer sans contexte : boucle runtime, gameplay, UI, debug

Exécution :

- `Framework/Game` -> `Framework/Application`
- `Framework/GameFramework` -> `Framework/Gameplay`
- `Framework/GUI` -> `Framework/UI`
- `Framework/Debugger` -> `Framework/Debug`

### HIER-005

Statut : ✅ Done

Objectif : moderniser les noms dans `Core`.

Travail attendu :

- `Maths` -> `Math`
- `Parser` -> `Parsing`
- `Log` -> `Logging`
- éclater `Helpers` en catégories explicites

Critère d'acceptation :

- `Core` ne contient plus de dossier fourre-tout de type `Helpers`

Exécution :

- `Maths` -> `Math`
- `Parser` -> `Parsing`
- `Log` -> `Logging`
- `MultiThreading` -> `Threading`
- `Helpers` a été éclaté en catégories explicites puis supprimé

## Phase 3 — Réorganiser les sous-domaines trop plats ou trop larges

### HIER-006

Statut : ✅ Done

Objectif : segmenter `Framework/Materials` par responsabilité.

Travail attendu :

- séparer runtime, définition, sérialisation, authoring cache, compilation
- garder les API publiques compatibles via types de forwarding ou namespaces conservés si nécessaire

Critère d'acceptation :

- `Materials` n'est plus un dossier plat de plus de 30 fichiers hétérogènes

Exécution :

- `Materials` a été segmenté en `Authoring`, `Compilation`, `Definitions`, `Runtime`, `Serialization`

### HIER-007

Statut : ✅ Done

Objectif : clarifier `Framework/Graphics`, `Framework/Graphics2D` et `Framework/Rendering`.

Travail attendu :

- déplacer les types runtime de modèles/meshes vers une catégorie `Models` ou `Geometry`
- absorber `Graphics2D` dans un sous-dossier de rendu 2D plus explicite
- conserver `Rendering` pour pipeline, vues, surfaces, passes et shaders

Critère d'acceptation :

- le sens de chaque dossier de rendu est lisible sans lecture du code

Exécution :

- `Graphics` a été déplacé sous `Rendering/Models`
- `Graphics/Shapes` a été déplacé sous `Rendering/Geometry`
- `Graphics2D` a été déplacé sous `Rendering/Draw2D`

### HIER-008

Statut : ✅ Done

Objectif : ranger les fichiers racine de `Framework` dans des catégories explicites.

Travail attendu :

- déplacer `ObjectBase.cs`
- déplacer `Constants.cs`
- éviter les fichiers de domaine à la racine du runtime

Critère d'acceptation :

- la racine du futur runtime n'héberge plus que des marqueurs structurels légitimes

Exécution :

- `ObjectBase.cs` a été déplacé vers `Framework/Common`
- `Constants.cs` a été déplacé vers `Framework/Configuration`
- `Project` a été déplacé vers `Framework/Configuration/Project`

## Phase 4 — Rationaliser les modules spécialisés

### HIER-009

Statut : ✅ Done

Objectif : nettoyer `Framework/AI`.

Travail attendu :

- séparer les modules effectivement utilisés par le runtime (`Messaging`, `Navigation`, `Pathfinding`, `StateMachines`)
- regrouper les modules plus isolés dans `Experimental`, `Algorithms`, ou autre zone explicitement non centrale
- supprimer les espaces dans les noms de dossiers

Critère d'acceptation :

- l'arborescence AI distingue clairement gameplay AI et bibliothèques algorithmiques annexes

Exécution :

- les modules algorithmiques non centraux ont été regroupés sous `AI/Algorithms`
- le dossier `Reinforcement Learning` a été renommé physiquement en `ReinforcementLearning`

### HIER-010

Statut : ✅ Done

Objectif : décider du sort des dossiers fins.

Travail attendu :

- conserver `World`, `Transform`, `SpacePartitioning` seulement s'ils doivent grandir
- sinon les absorber dans des catégories plus explicites (`Scene`, `Entities`, `Spatial`)

Critère d'acceptation :

- les dossiers restants ont soit une vraie masse critique, soit une intention d'extension claire

Exécution :

- `World`, `Transform`, `SpacePartitioning` et `Entities` ont été regroupés sous `Framework/Scene`
- `Octree` vit désormais sous `Framework/Scene/Spatial/Octree`

## Phase 5 — Renforcer les couches par le projet

### HIER-011

Statut : ✅ Done

Objectif : évaluer un split par assembly une fois le nettoyage sémantique terminé.

Travail attendu :

- étudier `CasaEngine.Core`
- étudier `CasaEngine.Engine`
- étudier `CasaEngine.Runtime`
- conserver une compatibilité graduelle si le split est adopté

Critère d'acceptation :

- les couches logiques ne dépendent plus seulement des dossiers, mais aussi du graphe de projets

Exécution :

- l'évaluation du split par projets est documentée dans `ai-agent/audits/CasaEngine_layering_project_split_evaluation.md`
- le split n'est pas exécuté dans cette phase afin de séparer la réorganisation de hiérarchie d'un chantier MSBuild plus risqué

## Ordre conseillé

1. HIER-001
2. HIER-002
3. HIER-003
4. HIER-004
5. HIER-005
6. HIER-007
7. HIER-006
8. HIER-008
9. HIER-009
10. HIER-010
11. HIER-011