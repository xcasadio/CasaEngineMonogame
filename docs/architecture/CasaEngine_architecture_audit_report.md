# CasaEngine Architecture Audit Report

Audit réalisé à partir de `ai-agent/CasaEngine_architecture_audit_tasks.md`.

## 1. Résumé exécutif

CasaEngine est **partiellement conforme** à l'architecture UI cible.

Le moteur possède déjà les briques structurantes attendues pour une intégration moderne de type MGUI / XAML : une notion explicite de vue (`RenderView`), une abstraction de surface (`IRenderSurface`), un pipeline de composition par vue (`IViewRenderPipeline` + `IUICompositionService`), un runtime UI abstrait par vue (`IUIViewRuntime`), un routage input multi-vues (`InputRouter`), ainsi qu'un hébergement multi-panneaux côté éditeur (`EngineHost` + `ViewportControl`).

Les écarts ne portent donc plus sur l'absence de fondations, mais sur des zones de câblage encore incomplètes ou trop concrètes : le runtime UI par défaut reste branché directement sur MGUI dans le composition root, l'affectation joueur/vue/UI n'est pas raccordée de bout en bout, l'intégration input éditeur/runtime reste hybride, et le world-space UI est encore explicitement marqué comme stub.

Verdict global : **Partiellement conforme**.

## 2. Architecture actuelle observée

### Boucle principale et composition de frame

La boucle principale est portée par `CasaEngine/Framework/Game/CasaEngineGame.cs`.

- `Initialize()` instancie le contexte runtime, `GameManager`, `RenderPipeline`, `InputRouter`, `UIViewRuntimeFactory` et le service de composition UI par défaut.
- `Update()` déclenche `PreviewUpdate`, met à jour les `UIView` des vues actives, puis exécute `GameManager.UpdateWorld()` et les composants MonoGame.
- `Draw()` sépare le rendu en pré-pipeline, pipeline multi-vues, puis post-pipeline.

Le point d'entrée principal de composition est `CasaEngine/Framework/Rendering/RenderPipeline.cs`.

Ordre de frame observé :

1. `InputComponent.Update()` collecte l'input brut et délègue à `InputRouter.TryDispatch()`.
2. `CasaEngineGame.Update()` met à jour les `UIView` par vue.
3. `GameManager.UpdateWorld()` charge le monde si nécessaire, bootstrappe les vues runtime et met à jour le gameplay.
4. `RenderPipeline.Render()` exécute une pré-passe `World.DrawWorldUIToTextures()`.
5. Pour chaque `RenderView` présentée : application de la surface, clear, création du `RenderFrame`, rendu 3D, composition UI, overlays, presenter.

### Vues, surfaces et hébergement

La séparation View / Surface existe déjà dans le code réel.

- `CasaEngine/Framework/Rendering/RenderView.cs` encapsule monde, caméra, surface, pipeline, presenter, host et `UIView`.
- `CasaEngine/Framework/Rendering/IRenderSurface.cs` abstrait la cible de rendu.
- `CasaEngine/Framework/Rendering/BackBufferSurface.cs` couvre le backbuffer et le split-screen.
- `CasaEngine/Framework/Rendering/RenderTargetSurface.cs` couvre les surfaces offscreen et les panneaux éditeur.
- `CasaEngine/Framework/Rendering/ViewManager.cs` gère le registre de vues, la vue active, la capture d'input, la synchronisation avec les hosts et l'auto-layout split-screen.
- `CasaEngine/Framework/Rendering/IViewHost.cs` formalise le lien entre une vue et un host externe.

### Runtime UI existant

L'intégration UI est déjà partiellement abstraite.

- `CasaEngine/Framework/GUI/IUIViewRuntime.cs` définit le contrat runtime UI par vue.
- `CasaEngine/Framework/GUI/IUIViewRuntimeFactory.cs` permet de créer un runtime UI depuis le moteur.
- `CasaEngine/Framework/GUI/MGUI/MguiViewRuntimeFactory.cs` branche aujourd'hui MGUI comme implémentation par défaut.
- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs` implémente le runtime UI concret actuel.
- `CasaEngine/Framework/GUI/MGUI/ScreenStack.cs` et `CasaEngine/Framework/GUI/MGUI/IUIScreen.cs` portent la navigation et la modalité.
- `CasaEngine/Framework/Rendering/IUICompositionService.cs` et `CasaEngine/Framework/Rendering/DefaultUICompositionService.cs` rendent explicite la phase de composition UI d'une vue.

### Bootstrap runtime et support éditeur

Le bootstrap des vues runtime est déjà séparé du monde.

- `CasaEngine/Framework/Game/DefaultRuntimeViewBootstrapper.cs` crée une vue backbuffer plein écran par défaut si aucune vue n'existe.
- `CasaEngine/Framework/Game/GameManager.cs` appelle ce bootstrapper après le chargement du monde.
- `CasaEngine/Framework/World/World.cs` ne crée plus directement de vue de rendu dans le chemin runtime audité.

Le support éditeur est déjà multi-vues.

- `CasaEngine.EditorUI/Controls/EngineHost.cs` héberge un unique `CasaEngineGame` partagé.
- `CasaEngine.EditorUI/Controls/ViewportControl.cs` implémente `IViewHost` et affiche la texture d'une vue dans WPF.
- `EngineHost.RegisterEditorView()` permet d'enregistrer plusieurs vues d'éditeur avec surfaces dédiées, caméras dédiées et pipeline dédié.

## 3. Capacité d’intégration UI actuelle

### Hébergement d'un runtime UI

Le moteur sait déjà fournir la majorité des services attendus par un runtime UI moderne.

- timing : `CasaEngineGame.PreviewUpdate`, `CasaEngineGame.EndUpdate`
- input brut : `InputComponent`, `KeyboardManager`, `MouseManager`, `GamePadManager`
- viewport et taille logique : `IRenderSurface.ViewportRect`, `IViewHost`, `UIViewMetrics`
- surfaces offscreen : `RenderTargetSurface`, `RenderTargetPool`
- ordre de composition : `RenderPipeline`, `IViewRenderPipeline`, `IUICompositionService`
- intégration éditeur : `EngineHost`, `ViewportControl`, `EditorViewPipeline`

Conclusion : la capacité d'hébergement existe déjà et n'est pas purement théorique.

### Plusieurs vues UI indépendantes

Le moteur sait héberger plusieurs vues UI indépendantes.

Éléments de preuve :

- `CasaEngineGame` crée une `UIView` pour chaque vue enregistrée.
- `RenderView` porte sa propre `UIView` et ses propres métriques UI.
- `CasaEngine.Demos/Demos/SplitScreenDemo.cs` prouve que deux vues backbuffer actives peuvent coexister.
- `CasaEngine.EditorUI/Controls/EngineHost.cs` prouve qu'un même runtime peut piloter plusieurs vues RT dans l'éditeur.

### Distinction Screen / View / Surface

La séparation conceptuelle recherchée est déjà visible :

- `IUIScreen` / `ScreenStack` = logique d'écran et de navigation
- `RenderView` = instance de vue hébergeant monde, caméra, pipeline, runtime UI
- `IRenderSurface` = cible de rendu

Cette partie est l'une des forces de l'architecture actuelle.

### Pipeline de composition explicite

Cette capacité existe déjà.

- `DefaultViewPipeline` rend le monde, flush les renderers, puis délègue la phase UI à `IUICompositionService`.
- `EditorViewPipeline` compose ses overlays puis délègue lui aussi la phase UI au même contrat.
- `RenderPipeline.Render()` gère déjà l'ordre RT puis backbuffer, ainsi qu'une pré-passe `DrawWorldUIToTextures()`.

Conclusion : CasaEngine possède déjà un pipeline de composition explicite, extensible, et orienté vue.

### Routage input

Le routage input est plus avancé que dans beaucoup de moteurs MonoGame traditionnels.

- `InputComponent.Update()` délègue l'arbitrage à `InputRouter.TryDispatch()`.
- `InputRouter` connaît la vue cible, la vue pointeur, le focus clavier, la vue modale et l'affectation joueur→vue.
- `ViewManager` sait convertir screen-space vers view-local et gérer la capture d'input.
- `UIRoot` expose déjà `IsPointerOverUI`, `IsKeyboardCaptured` et `HasModalInput`.

Conclusion : la base est bonne, mais certains chemins restent incomplètement raccordés.

### Split-screen, multi-panel editor et world-space UI

- split-screen local : **oui**, prouvé par `SplitScreenDemo`
- multi-panel editor : **oui**, prouvé par `EngineHost` + `ViewportControl`
- world-space UI : **partiellement**, structure présente mais implémentation incomplète

## 4. Écarts par rapport à l’architecture cible

### Écart 1 — Le composition root runtime reste branché directement sur MGUI

Constat :

- `CasaEngineGame` instancie directement `new MguiViewRuntimeFactory()`.
- `OnViewAddedCreateUIRuntime()` crée automatiquement `view.UIView` depuis cette factory par défaut.

Impact :

- le moteur expose bien `IUIViewRuntime`, mais le runtime UI concret reste choisi en dur dans le cœur du jeu
- remplacer MGUI par un runtime XAML-like demanderait encore un passage dans `CasaEngineGame`

Classes/fichiers concernés :

- `CasaEngine/Framework/Game/CasaEngineGame.cs`
- `CasaEngine/Framework/GUI/MGUI/MguiViewRuntimeFactory.cs`

Classification : **important**.

### Écart 2 — L'affectation joueur → vue → UI n'est pas raccordée de bout en bout

Constat :

- `InputRouter` expose `AssignPlayer`, `GetViewForPlayer` et `GetRenderViewForPlayer`.
- `PlayerController` expose `AssignedViewId` et `UIView`.
- pendant l'audit, aucun câblage concret n'a été trouvé reliant automatiquement ces contrats.
- les exemples UI inspectés (`CasaEngine.Demos/Demos/UIOverlayDemo.cs`, `CasaEngine.Demos/DemosGame.cs`) récupèrent souvent la première `UIView` disponible au lieu d'une vue explicitement ciblée.

Impact :

- le moteur est prêt structurellement pour un HUD par vue, mais pas encore outillé proprement pour un HUD par joueur ou par panneau cible
- le split-screen UI peut dériver vers un comportement implicite “première vue disponible”

Classes/fichiers concernés :

- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/GameFramework/PlayerController.cs`
- `CasaEngine.Demos/Demos/UIOverlayDemo.cs`
- `CasaEngine.Demos/DemosGame.cs`

Classification : **important**.

### Écart 3 — Le chemin input éditeur/runtime reste hybride

Constat :

- `InputRouter` est bien le dispatcher de la frame.
- en parallèle, `EngineHost` installe des providers globaux sur `InputComponent` puis enregistre aussi des providers raw par viewport via `RegisterViewInput()`.
- `ViewportControl` mélange activation de vue, focus WPF et providers Win32 raw.

Impact :

- l'architecture fonctionne déjà, mais le flux de vérité input est réparti entre plusieurs couches
- cela augmente le risque de comportements instables avec plusieurs panneaux, modales, gizmos et outils dockables

Classes/fichiers concernés :

- `CasaEngine/Framework/Input/InputComponent.cs`
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine.EditorUI/Controls/EngineHost.cs`
- `CasaEngine.EditorUI/Controls/ViewportControl.cs`

Classification : **important**.

### Écart 4 — Le support world-space UI est présent mais inachevé

Constat :

- `WorldUIComponent` possède une surface RT, une `UIView`, une texture de sortie et une méthode `DrawToTexture()`.
- le fichier le décrit explicitement comme `stub / not yet functional`.
- `World.DrawWorldUIToTextures()` existe, mais la consommation de la texture dans un rendu monde concret n'est pas finalisée dans la zone auditée.

Impact :

- l'architecture anticipe bien le besoin “UI dans texture puis dans le monde”, mais la chaîne complète n'est pas prête pour production

Classes/fichiers concernés :

- `CasaEngine/Framework/GUI/MGUI/WorldUIComponent.cs`
- `CasaEngine/Framework/World/World.cs`
- `CasaEngine/Framework/Rendering/RenderPipeline.cs`

Classification : **important**.

### Écart 5 — La navigation d'écrans existe mais reste optionnelle et peu centralisée

Constat :

- `IUIScreen`, `ScreenStack` et `GameScreenManager` existent déjà.
- `GameScreenManager` fournit une orchestration par état de jeu, mais aucune intégration systématique n'a été observée dans les chemins runtime inspectés.
- les démos et scripts poussent encore souvent les écrans manuellement.

Impact :

- la séparation Screen / View / Surface existe, mais l'usage de `Screen` reste encore dispersé entre démos, scripts et runtime UI concret

Classes/fichiers concernés :

- `CasaEngine/Framework/GUI/MGUI/GameScreenManager.cs`
- `CasaEngine/Framework/GUI/MGUI/ScreenStack.cs`
- `Projects/CasaEngine.RPGDemo/Scripts/ScriptWorld.cs`
- `Projects/CasaEngine.RPGDemo/Scripts/ScriptTitleScreenWorld.cs`

Classification : **amélioration souhaitable**.

### Écart 6 — Certains services nécessaires à l'UI restent accessibles via des globaux/statics

Constat :

- le runtime s'appuie encore sur `GameSettings`, `EngineEnvironment`, `AssetCatalog` et `RenderTargetPool.Shared`.

Impact :

- l'injection d'un runtime UI externe reste possible, mais moins propre qu'avec un contexte d'exécution explicitement passé aux factories et services de composition

Classes/fichiers concernés :

- `CasaEngine/Framework/Game/GameSettings.cs`
- `CasaEngine/Engine/EngineEnvironment.cs`
- `CasaEngine/Framework/Assets/AssetCatalog.cs`
- `CasaEngine/Framework/Rendering/RenderTargetPool.cs`

Classification : **amélioration souhaitable**.

## 5. Risques techniques

- Le remplacement de MGUI par un runtime plus externe reste possible, mais pas totalement localisé au composition root tant que `CasaEngineGame` décide lui-même de la factory concrète.
- Le support HUD/menu par joueur ou par vue cible peut devenir ambigu tant que le moteur n'expose pas un chemin standard pour récupérer la bonne `UIView`.
- Les interactions éditeur complexes peuvent produire des conflits de focus ou de capture tant que le flux input est partagé entre état global, host WPF et routing par vue.
- Le support world-space UI restera fragile tant qu'un cas complet texture → matériau/quad → interaction n'est pas validé.

## 6. Points forts

- La séparation `Screen` / `View` / `Surface` existe déjà dans le design réel.
- Le moteur sait déjà gérer plusieurs vues simultanées, en runtime comme dans l'éditeur.
- Le pipeline de frame est explicite et suffisamment moderne pour accueillir plusieurs passes UI.
- Le contrat `IUIViewRuntime` est déjà compatible avec une idée de runtime MGUI, XAML ou autre.
- `InputRouter` et `ViewManager` fournissent déjà un socle sérieux pour focus, modalité, capture et dispatch par vue.
- `DefaultRuntimeViewBootstrapper` montre que le bootstrap de vues est sorti du monde lui-même.

## 7. Priorités de refactor

### Priorité importante

- externaliser le choix du runtime UI concret hors de `CasaEngineGame`
- raccorder réellement joueur, vue, `UIView` et écrans ciblés
- unifier le chemin input multi-vues entre runtime et éditeur
- finaliser la chaîne world-space UI

### Priorité souhaitable

- centraliser davantage l'orchestration des `Screen`
- réduire la dépendance aux statics/globaux dans les services exposés au runtime UI

Le plan détaillé correspondant est fourni dans `docs/architecture/CasaEngine_architecture_refactor_tasks.md`.

## 8. Verdict final

**Verdict : Partiellement conforme**

CasaEngine est déjà très proche d'une architecture UI moderne viable. La base conceptuelle demandée par l'audit est bien présente dans le code : vues multiples, surfaces multiples, composition explicite, runtime UI par vue, input routing et hébergement éditeur multi-panneaux.

Le moteur n'a donc pas besoin d'une refonte fondationnelle avant d'accueillir MGUI ou un runtime XAML-like. En revanche, il reste plusieurs écarts d'intégration à traiter pour rendre cette architecture robuste, interchangeable et réellement exploitable à grande échelle, en particulier pour le split-screen piloté par joueur, les outils éditeur et le world-space UI.