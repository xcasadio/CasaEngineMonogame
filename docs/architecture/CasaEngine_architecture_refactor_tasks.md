# CasaEngine Architecture Refactor Tasks

Ce fichier est généré à partir de l'audit architectural demandé dans `ai-agent/CasaEngine_architecture_audit_tasks.md`.

## Légende des statuts

- 🔴 Bloquant
- 🟠 Important
- 🟡 Amélioration souhaitable

## Tâches

### ✅ CASA-ARCH-001 — Externaliser le choix du runtime UI concret hors de `CasaEngineGame`
**Contexte**  
Le moteur expose déjà `IUIViewRuntime` et `IUIViewRuntimeFactory`, mais `CasaEngineGame` instancie encore directement `MguiViewRuntimeFactory` comme choix par défaut.

**Objectif**  
Faire du runtime UI concret une dépendance configurable du composition root, et non une décision codée en dur dans le cœur moteur.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Game/CasaEngineGame.cs`
- `CasaEngine/Framework/GUI/IUIViewRuntimeFactory.cs`
- `CasaEngine/Framework/GUI/MGUI/MguiViewRuntimeFactory.cs`

**Modification attendue**  
Permettre d'injecter la factory UI depuis un bootstrap runtime/éditeur ou un profil de configuration, sans instanciation directe dans `CasaEngineGame`.

**Critères d’acceptation**  
- `CasaEngineGame` n'instancie plus directement `MguiViewRuntimeFactory`
- le runtime UI par défaut reste MGUI sans régression fonctionnelle
- remplacer la factory concrète ne demande plus de modifier le cœur du moteur

**Dépendances éventuelles**  
Aucune

**Statut**  
Terminé

---

### ✅ CASA-ARCH-002 — Raccorder réellement l'affectation joueur → vue → UIView
**Contexte**  
`InputRouter` sait affecter un joueur à une vue et `PlayerController` possède déjà `AssignedViewId` et `UIView`, mais aucun raccord complet n'a été observé pendant l'audit.

**Objectif**  
Permettre à chaque joueur ou contexte gameplay de cibler explicitement sa vue et son runtime UI associé.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/GameFramework/PlayerController.cs`
- `CasaEngine/Framework/Game/GameManager.cs`
- `CasaEngine/Framework/Rendering/ViewManager.cs`

**Modification attendue**  
Ajouter un chemin standard qui synchronise l'affectation de vue dans `PlayerController`, expose la `UIView` correspondante et fournit une API de récupération explicite au gameplay.

**Critères d’acceptation**  
- un `PlayerController` affecté à une vue reçoit son `AssignedViewId`
- la `UIView` de cette vue peut être récupérée sans chercher la “première vue disponible”
- les HUD et menus peuvent cibler explicitement une vue ou un joueur donné

**Dépendances éventuelles**  
Aucune

**Statut**  
Terminé

---

### ✅ CASA-ARCH-003 — Unifier le flux input multi-vues entre runtime et éditeur
**Contexte**  
`InputComponent` délègue déjà à `InputRouter.TryDispatch()`, mais le chemin réel reste hybride entre providers globaux, providers raw par viewport, activation de vue et focus WPF.

**Objectif**  
Établir un chemin de vérité unique pour le dispatch clavier/souris par vue, utilisable de la même façon en runtime et dans l'éditeur.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/InputComponent.cs`
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine.EditorUI/Controls/EngineHost.cs`
- `CasaEngine.EditorUI/Controls/ViewportControl.cs`

**Modification attendue**  
Clarifier la frontière entre collecte brute, enregistrement des sources par vue, focus, capture et dispatch final pour éviter les doubles sources de vérité.

**Critères d’acceptation**  
- une vue cible est déterminée de manière univoque pour clavier et souris
- l'éditeur n'a plus de logique concurrente de sélection de providers en dehors du routage officiel
- les comportements de hover, click, drag gizmo et modal restent cohérents avec plusieurs panneaux visibles

**Dépendances éventuelles**  
Aucune

**Statut**  
Terminé

---

### ✅ CASA-ARCH-004 — Formaliser le contrat moteur de focus, capture et modalité
**Contexte**  
Le moteur expose déjà `KeyboardFocusViewId`, `ModalViewId`, `InputCaptureView` et les signaux UI `IsPointerOverUI`, `IsKeyboardCaptured`, `HasModalInput`, mais leur orchestration reste répartie.

**Objectif**  
Donner un contrat moteur unique pour savoir quelle vue a le focus, quelle vue capture l'input et quelle modalité bloque les consommateurs sous-jacents.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/Rendering/ViewManager.cs`
- `CasaEngine/Framework/GUI/IUIViewRuntime.cs`
- `CasaEngine/Framework/GUI/MGUI/ScreenStack.cs`

**Modification attendue**  
Stabiliser les règles de priorité entre focus WPF, vue active, capture d'outil, modalité UI et consommation gameplay.

**Critères d’acceptation**  
- les règles de priorité sont explicites et documentées dans le code
- une UI modale bloque correctement les vues ou couches inférieures prévues
- les outils éditeur et le gameplay lisent le même état de focus/capture

**Dépendances éventuelles**  
- CASA-ARCH-003

**Statut**  
Terminé

---

### 🟠 CASA-ARCH-005 — Finaliser la chaîne world-space UI de bout en bout
**Contexte**  
`WorldUIComponent` possède déjà la plupart des briques nécessaires mais est explicitement marqué comme `stub / not yet functional`.

**Objectif**  
Permettre une UI rendue offscreen dans une texture puis réellement utilisée dans le monde 3D.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/GUI/MGUI/WorldUIComponent.cs`
- `CasaEngine/Framework/World/World.cs`
- `CasaEngine/Framework/Rendering/RenderPipeline.cs`
- classes de rendu monde consommant la texture finale

**Modification attendue**  
Compléter la chaîne de rendu, la consommation de texture côté monde et la restauration d'état GPU, puis fournir au moins un cas d'usage validé.

**Critères d’acceptation**  
- une `UIView` peut être rendue dans une `RenderTarget2D`
- la texture produite est effectivement affichée dans le monde
- la passe n'introduit pas de corruption d'état GPU sur les vues suivantes

**Dépendances éventuelles**  
- CASA-ARCH-001

---

### 🟡 CASA-ARCH-006 — Standardiser l'accès gameplay aux UIView ciblées
**Contexte**  
Les chemins inspectés montrent encore des usages de type “première `UIView` disponible”, notamment dans certaines démos.

**Objectif**  
Éviter les recherches implicites et fournir un point d'accès standard pour récupérer la `UIView` d'une vue, d'un joueur ou d'un panneau précis.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Rendering/ViewManager.cs`
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine.Demos/DemosGame.cs`
- `CasaEngine.Demos/Demos/UIOverlayDemo.cs`

**Modification attendue**  
Créer des helpers ou services d'accès ciblé, puis migrer les exemples qui s'appuient sur la première vue trouvée.

**Critères d’acceptation**  
- les exemples principaux n'utilisent plus `FirstOrDefault(v => v.UIView != null)` pour cibler l'UI
- un appelant peut demander explicitement la `UIView` d'une vue donnée
- le split-screen ne dépend plus d'une convention implicite de première vue

**Dépendances éventuelles**  
- CASA-ARCH-002

---

### 🟡 CASA-ARCH-007 — Intégrer un service de navigation d'écrans au niveau runtime
**Contexte**  
`GameScreenManager` existe déjà, mais son usage n'a pas été observé comme point de passage standard du runtime ou des projets.

**Objectif**  
Faire de la navigation `Screen` un service cohérent et réutilisable, au lieu d'un ensemble d'appels manuels dispersés.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/GUI/MGUI/GameScreenManager.cs`
- `CasaEngine/Framework/Game/GameManager.cs`
- `Projects/CasaEngine.RPGDemo/Scripts/ScriptWorld.cs`
- `Projects/CasaEngine.RPGDemo/Scripts/ScriptTitleScreenWorld.cs`

**Modification attendue**  
Définir à quel niveau le runtime orchestre les transitions d'écrans, et brancher au moins un cas de jeu réel sur ce chemin commun.

**Critères d’acceptation**  
- un projet peut piloter ses écrans par un service commun plutôt que par des appels dispersés
- les transitions d'état gameplay peuvent cibler une ou plusieurs vues de façon explicite
- la séparation `Screen` / `View` / `Surface` devient visible dans le code d'usage

**Dépendances éventuelles**  
- CASA-ARCH-002

---

### 🟡 CASA-ARCH-008 — Réduire l'exposition des services UI aux dépendances globales
**Contexte**  
Plusieurs services nécessaires au runtime UI restent fournis par des statics ou globaux moteur.

**Objectif**  
Rendre les dépendances UI plus injectables et mieux isolées du contexte global du moteur.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Game/GameSettings.cs`
- `CasaEngine/Engine/EngineEnvironment.cs`
- `CasaEngine/Framework/Assets/AssetCatalog.cs`
- `CasaEngine/Framework/Rendering/RenderTargetPool.cs`

**Modification attendue**  
Introduire progressivement un contexte de services explicitement passé aux factories et services UI critiques.

**Critères d’acceptation**  
- une factory UI peut être initialisée à partir d'un contexte explicite
- les chemins UI critiques diminuent leur dépendance directe aux statics moteur
- le comportement existant reste compatible pendant la transition

**Dépendances éventuelles**  
- CASA-ARCH-001