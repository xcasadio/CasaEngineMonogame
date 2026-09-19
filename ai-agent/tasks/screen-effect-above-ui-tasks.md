# Plan agent IA — Effet d'écran au-dessus de l'interface

Plan d'exécution d'un chantier né du portage Alundra (`docs/plan-e13-hud.md` du dépôt parent,
décision D-E13-11, §6 point 6), mais qui appartient au moteur : il sert tout projet dont un fondu
doit assombrir l'interface avec la scène.
Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur le 2026-09-19 : **ce plan les
applique, il ne les rediscute pas**. Révision 3 du même jour après deux relectures adverses, voir le
journal en fin de fichier. **Approuvé par l'auteur le 2026-09-19 : exécution en cours.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son
statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution
> directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne
> rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Donner à l'effet d'écran plein viewport (fondu, teinte) un mode qui se dessine **après la
composition de l'interface MGUI**, pour qu'une interface dessinée en MGUI s'assombrisse avec la
scène pendant un fondu au lieu de rester lisible au-dessus du noir. Le mode est optionnel, choisi
par le consommateur, et le comportement par défaut ne change pas.

Le besoin d'origine : dans le jeu PSX Alundra, au départ d'un warp, la dernière image est capturée
avec la jauge de vie et le fondu s'applique à cette capture ; la jauge s'assombrit donc avec le
décor. Le portage dessine sa jauge en MGUI et son fondu par `ScreenEffectComponent`, qui passe
structurellement sous l'interface : la jauge flotterait sur le noir. Ce chantier ne reproduit pas
la capture d'image de la PSX, seulement l'ordre de dessin qui donne le même résultat visible.

## État vérifié du dépôt (2026-09-19)

- **L'interface MGUI est composée à l'intérieur du pipeline de rendu, par vue.**
  `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:26-57`, `RenderView` : (1) `view.World.Draw`
  enfile ; (2) `foreach renderer in renderers : renderer.Flush(in frame, view.RenderStats)` (`:44`) ;
  (3) `(view.UICompositionService ?? DefaultUICompositionService.Instance).Compose(graphicsDevice,
  view, in frame)` (`:53-54`). Sa doc XML (`:7-19`) énonce cet ordre en trois points.
- **Trois implémentations de `IViewRenderPipeline`** (`IViewRenderPipeline.cs:31`) :
  `DefaultViewPipeline` (`sealed`, jeu et runtime) ; `OverlayViewPipeline`
  (`CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs:21`, `RenderView` **virtuel** `:57`,
  **son propre** vidage des renderers `:104` et **sa propre** composition `.Compose(gd, view, in
  frame)` `:148`, sans déléguer au pipeline par défaut) ; `SkyBackgroundViewPipeline`
  (`SkyBackgroundViewPipeline.cs:10`, délègue à un pipeline interne `:36`). Tout mécanisme posé dans
  le pipeline par défaut seul **manquerait l'éditeur et le Play-in-Editor**.
- `DefaultUICompositionService.Compose` (`DefaultUICompositionService.cs:12-14`) fait
  `view.UIView?.Draw()` → `UIRoot.Draw()` (`CasaEngine/Framework/UI/UIRoot.cs:132-140`) →
  `Desktop.Draw()`. Même cible de rendu et même viewport que la scène : `RenderPipeline.Render`
  (`RenderPipeline.cs:101-311`) applique la surface de la vue puis délègue (`:240`).
  `IUICompositionService` (`IUICompositionService.cs:8-11`) n'a qu'une méthode, `Compose`.
- **Un précédent de vidage hors pipeline existe** : `TileMapSurfaceComponent.cs:206` appelle
  `spriteRenderer?.Flush(in frame)` directement pour dessiner dans sa propre cible. C'est le patron
  que ce chantier réemploie : soumettre au renderer de sprites puis le vider **immédiatement**.
- `RenderPass2D.UI = 1000` **n'est consommé par aucune passe** : hors sa définition et
  `RenderPassDepthOffset.cs:26`, seul `RenderPassDepthOffsetTests.cs:25,76` le référence.
- **`RenderPassDepthOffset.DeriveDepthOffset` est un `switch` explicite**, pas une dérivation par
  rang : `RenderPassDepthOffset.cs:17-28`, huit bras de `Background => -3f * Step` à
  `UI => 4f * Step`, et `_ => 0f`. Ses tests (`RenderPassDepthOffsetTests.cs:16-26, 35-45, 47-60`)
  énumèrent les valeurs dans un tableau en dur et exigent la monotonie. **Ce chantier n'y touche
  pas** (D3).
- **L'effet d'écran est soumis au renderer de sprites dans `Update`**, pas dans `Draw` :
  `CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs:18-23` (commentaire),
  `:40-41` (`UpdateOrder = ComponentUpdateOrder.ScreenEffects`), soumission par image `:47-74`,
  `SubmitOverlay` `:130-163`, `renderer.DrawSprite(...)` avec `sortKey = new RenderSortKey2D((int)
  RenderPass2D.ScreenEffects, 0, 0, 0, 0, 0, 0)` à `:147`. Il est donc vidé au temps (2) et
  l'interface composée au temps (3).
- `SpriteRendererComponent` (`SpriteRendererComponent.cs:14`, `Flush` `:143`) : file préallouée à
  `NbSprites = 10000` (`:31-34`, tableau de sommets, liste et pile de réserve). `Draw` (`:155-206`)
  pose ses propres états de profondeur, de rastérisation, d'échantillonnage et de fusion, et ne
  restaure que le rectangle de ciseau.
- `RenderPass2D.cs:12-17` documente `ScreenEffects = 750` comme « above every world/effects layer
  and below the UI ». `docs/engine/screen-effects.md:57-60` dit la même chose.
- `ScreenEffectService` (`CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs`,
  documenté `docs/engine/screen-effects.md:32-53`) : `SetOverlay(r, g, b, blend)`,
  `StartFade(fromR..toB, duration, blend)`, `Clear()`, `Update(elapsedSeconds)` ; état `R/G/B`,
  `Blend` (`SpriteBlendMode`), `Active`.
- Consommateurs dans le moteur : `CasaEngineGame.cs:54,357`, `ScreenEffectComponent.cs`,
  `Cutscenes/CutsceneActionCoroutineFactory.cs` (action `FadeScreen`), tests
  `CasaEngine.Tests/Rendering/ScreenEffects/*`. **Aucune démo** (`Projects/CasaEngine.RPGDemo`,
  `Projects/SandBoxGame`) ne l'utilise.
- **Le placement du quad de l'effet n'est valide qu'avec une caméra 2D.** `ScreenEffectComponent
  .Update` résout `ActiveView.Camera as Camera2dComponent` (`ScreenEffectComponent.cs:53-67`) ;
  avec une caméra 3D il retombe sur `cameraPosition = Vector3.Zero` et des pixels d'écran bruts, et
  la formule de `SubmitOverlay` est documentée comme valide seulement pour « this engine's 2D world
  (+Y up) », où elle « cancels the active camera's own view transform » (`:123-129`). Le quad est
  alors un quad monde à l'origine, testé en profondeur contre la scène 3D
  (`SpriteRendererComponent.cs:165`, `DepthBufferEnable = true`, `LessEqual`) : rien ne garantit
  qu'il couvre le viewport. **Conséquence pour le smoke** : `UIOverlayDemo` construit une scène 3D
  (`CasaEngine.Demos/Demos/UIOverlayDemo.cs:52-94`) et ne convient pas ; **seule `TileMapDemo`
  utilise une `Camera2dComponent`** (`CasaEngine.Demos/Demos/TileMapDemo.cs:98`). Le smoke de T2.2
  se fait donc dans `TileMapDemo`, en y poussant une interface MGUI non modale, sur le modèle de
  `CasaEngine.Demos/Demos/UIOverlay/HudScreen.cs:16-92` (fenêtre en haut à gauche, boutons `:68`
  et `:73`).
- **Aucun crochet générique « après l'interface »** n'existe : `AfterRenderPipeline`
  (`CasaEngineGame.cs:633-635`) est vide et s'exécute avant la composition, puisque celle-ci vit
  dans le pipeline. `GameEditor.CaptureAutomationScreenshot` (`CasaEngine.Editor/GameEditor.cs:
  5988-6020`) dessine un bureau MGUI séparé dans une cible dédiée, hors image normale.
- Tests existants qui figent l'ordre ou l'effet : `CasaEngine.Tests/Rendering/ScreenEffects/
  ScreenEffectComponentSubmissionTests.cs`, `ScreenEffectComponentViewSizeTests.cs`,
  `ScreenEffectServiceTests.cs` ; `CasaEngine.Tests/Rendering/RenderPassDepthOffsetTests.cs`.
- Suite moteur : 1618 verts au dernier chantier (2026-09-07). `CasaEngine.MonoGame.sln` n'inclut
  pas `CasaEngine.Tests` : le construire explicitement avant `dotnet test --no-build`.
- **Changement préexistant de l'auteur dans l'arbre de travail** :
  `CasaEngine.Launcher/Program.cs`, bascule locale volontaire du chemin de projet vers Alundra.
  **Ne jamais l'indexer.**

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Un réglage de couche sur le service**, `ScreenEffectLayer { BelowUI, AboveUI }`, propriété `Layer` de `ScreenEffectService`, **par défaut `BelowUI`**. Aucun consommateur existant ne change de comportement. |
| D2 | **Un crochet « après l'interface » dans la composition, pas dans les pipelines.** `DefaultUICompositionService.Compose` appelle, après `view.UIView?.Draw()`, le dessin des surcouches post-interface enregistrées sur la vue (`RenderView`, une nouvelle liste ou un emplacement `IPostUIOverlay` ; T1.1 tranche la forme en lisant comment `ScreenEffectComponent` cible ses vues aujourd'hui). Les **trois pipelines** passent par cette composition, l'éditeur et le Play-in-Editor compris ; T1.1 le **vérifie** sur `OverlayViewPipeline.cs:148` avant d'écrire, et s'arrête si ce n'est pas le même service. |
| D3 | **Pas de seconde file, pas de nouvelle passe, `RenderPassDepthOffset` intact.** En mode `AboveUI`, `ScreenEffectComponent` **ne soumet plus dans `Update`** ; depuis le crochet de D2 il soumet son quad au renderer de sprites, à la passe `ScreenEffects` inchangée, puis appelle **immédiatement** `Flush(in frame, stats)`, sur le modèle de `TileMapSurfaceComponent.cs:206`. Rien ne reste jamais en file d'une image à l'autre, quel que soit le pipeline. |
| D4 | **États GPU** : le vidage immédiat s'exécute après `Desktop.Draw()`, qui laisse l'appareil dans un état quelconque. `SpriteRendererComponent.Draw` pose déjà tous les états qu'il utilise ; T1.1 **restaure** en sortie ceux qu'il a modifiés, conformément à la règle du dépôt, et un test le fige. |
| D5 | **Une décision d'architecture, un ADR** (`docs/decisions/`, skill `adr` du dépôt), la mise à jour des textes qui disent « below the UI » (`RenderPass2D.cs:12-17`, `DefaultViewPipeline.cs:7-19`, `docs/engine/screen-effects.md:57-60`), et **un smoke visible dans une démo du moteur** (T2.2), parce que la règle §6 d'`AGENTS.md` exige un sample pour toute feature visible non triviale. |

Voies écartées, pour mémoire, avec la raison :
- Une seconde file de sprites vidée par un quatrième temps des pipelines, routée par une nouvelle
  passe `> UI`. Écartée en révision 2 : `OverlayViewPipeline` ne délègue pas au pipeline par défaut,
  et une file jamais vidée y aurait grossi sans borne jusqu'à dépasser le tampon de `NbSprites`
  sommets ; la nouvelle passe aurait aussi tombé dans le bras par défaut de
  `RenderPassDepthOffset` et cassé la monotonie de ses tests.
- Un `IUICompositionService` décorateur redessinant l'effet lui-même. Écartée : dupliquerait la
  soumission et les modes de fusion de `SubmitOverlay`. Le crochet de D2 passe par le renderer.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/effet-ecran-au-dessus-ui`**, créée depuis `main` le 2026-09-19. Ne
  jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À
  la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte
  note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce
  fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format
  `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient
  d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en
  ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché
  (`dotnet build CasaEngine.MonoGame.sln`, puis `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`
  explicitement ; `dotnet build CasaEngine.Editor.MonoGame.sln` dès que T1.1 touche la composition
  vue par l'éditeur) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build`
  dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la
  raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser
  `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par
  fichier, jamais `git add -A` ni `git add .`. En particulier jamais
  `CasaEngine.Launcher/Program.cs`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout
  état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans
  `AGENTS.md` et les règles par chemin). Le crochet de D2 est parcouru **sans allocation** par image.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln`, puis
  `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 erreur, aucun avertissement nouveau.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build` : 1618 verts de base plus les
  tests ajoutés, 0 échec.
- **Le test d'ordre de T1.1** : la composition enregistre `[UIView.Draw, surcouche post-interface]`
  dans cet ordre, et la surcouche n'est jamais appelée quand `Layer = BelowUI`.
- **Le smoke visible de T2.2** dans `CasaEngine.Demos`, démo `TileMapDemo` (caméra 2D, seule
  démo où le placement du quad est sur son chemin supporté) : **précondition**, le fondu `BelowUI`
  couvre tout le viewport ; puis un fondu `AboveUI` assombrit l'interface MGUI poussée dans la démo
  avec la scène, et un fondu `BelowUI` la laisse lisible. Observation par l'auteur, et capture en
  processus si l'automation des démos le permet. **C'est le seul contrôle dont l'échec révélerait
  un quad émis dans le bon ordre mais invisible ou mal fusionné** ; le test d'ordre ne le peut pas.
  **Arrêt** : si la précondition échoue, le quad ne couvre pas le viewport dans cette démo et le
  smoke ne dit rien de l'ordre ; T2.2 passe ⚠️ Blocked avec la question dans « Points ouverts »
  (O5), et ne doit pas être lu comme un échec d'ordre.
- Le smoke du portage Alundra, tranche C4.dll de `docs/plan-e13-hud.md` du dépôt parent, après bump
  du pointeur de sous-module, reste la recette de bout en bout.
- Verifier frais à la clôture (T3.1), puis archivage dans `tasks/archive/` et ligne du tableau de
  `ai-agent/README.md`.

---

## Phase 0 — Le socle

### ✅ T0.1 — Le réglage de couche du service

- Objectif : `ScreenEffectService` porte une couche, `BelowUI` par défaut, sans effet sur le rendu
  tant que rien ne la lit.
- Fichiers : `CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectLayer.cs` (nouveau,
  énumération `BelowUI`, `AboveUI`) ;
  `CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs` (propriété `Layer`, défaut
  `BelowUI` ; **`Clear()` ne touche pas à la couche**, qui est un réglage de rendu et non un état
  d'effet — décision prise ici pour lever O2) ;
  `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectServiceTests.cs`.
- Étapes :
  1. Lire `ScreenEffectService.cs` en entier ; relever ce que `Clear()` remet à zéro, pour le citer.
  2. Ajouter l'énumération et la propriété, doc XML en anglais citant D1.
  3. Tests : défaut `BelowUI` ; aller-retour `AboveUI` ; `Clear()` remet les couleurs et `Active`
     comme avant et **laisse `Layer` intacte**.
- Validation : build des deux projets ; `dotnet test ... --no-build` vert, tests ajoutés comptés.
- Commit : `feat(screen-effects): add a layer setting to the screen effect service`
- Note de validation : `ScreenEffectService.cs` lu en entier avant modification — `Clear()`
  (`:62-66`) ne remet que `Active = false` et `_isFading = false` ; il ne touche déjà pas à
  `R`/`G`/`B` ni à `Blend`. Aucune doc XML de `Clear()` n'annonce une sémantique « tout remettre » :
  rien ne contredit le choix D1/O2 de laisser `Layer` intacte, pas de blocage. `ScreenEffectLayer`
  ajouté (`ScreenEffectLayer.cs`, nouveau), propriété `Layer` (auto-implémentée, défaut `BelowUI`)
  ajoutée à `ScreenEffectService.cs:46-53`, doc XML citant D1. Quatre tests ajoutés à
  `ScreenEffectServiceTests.cs` : défaut `BelowUI`, aller-retour `AboveUI`/`BelowUI`, et `Clear()`
  qui laisse `Layer = AboveUI` intacte tout en remettant `Active`/`IsFading` à `false`. Build
  `CasaEngine.MonoGame.sln` : 0 erreur (avertissements `CS8632` préexistants, sans rapport).
  `CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 erreur. `dotnet test --no-build` : 1622 tests
  (1618 de base + 4 ajoutés), 1621 verts, 1 échec — `EditorControlTemplateAssetLoadingTests
  .EditorThemeAsset_Disables_Docking_Accent_Bars` (`Collapsed` attendu, `Hidden` obtenu), confirmé
  **préexistant et sans rapport** : même échec reproduit après `git stash` de ce changement, sur
  l'arbre de départ. Rien ne lit encore `Layer` : aucun comportement de rendu ne change.

---

## Phase 1 — Le crochet après l'interface

### ✅ T1.1 — La surcouche post-interface dans la composition

- Objectif : une surcouche enregistrée sur une vue est dessinée par la composition **après**
  `UIView.Draw()`, dans tous les pipelines, sans rien laisser en file.
- Fichiers : `CasaEngine/Framework/Rendering/IPostUIOverlay.cs` (nouveau, une méthode
  `Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)`) ;
  `CasaEngine/Framework/Rendering/RenderView.cs` (l'enregistrement, forme tranchée à l'étape 1) ;
  `CasaEngine/Framework/Rendering/DefaultUICompositionService.cs` (l'appel après `UIView?.Draw()`,
  sans allocation) ; `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:7-19` (doc XML) ;
  nouveaux tests dans `CasaEngine.Tests/Rendering/`.
- Étapes :
  1. **Vérifier d'abord** `CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs:144-149` :
     la composition qu'il appelle est-elle `view.UICompositionService ?? DefaultUICompositionService
     .Instance` ? Si oui, le crochet couvre l'éditeur par construction. Si non, passer en ⚠️ et
     écrire O3 : le crochet devra aussi être posé là.
  2. Lire comment `ScreenEffectComponent` cible ses vues aujourd'hui (`:47-74`, et les tests
     `ScreenEffectComponentViewSizeTests`) ; choisir en conséquence la forme de l'enregistrement sur
     `RenderView` (un emplacement unique ou une petite liste préallouée), et la justifier dans la
     note de validation.
  3. `DefaultUICompositionService.Compose` : après `view.UIView?.Draw()`, dessiner les surcouches
     enregistrées. Aucune allocation, aucune closure.
  4. Tests : (a) **le test d'ordre**, avec un faux `IUIView` (ou le plus petit substitut que les
     tests existants de `UIRoot`/composition permettent — lire `CasaEngine.Tests` avant de choisir)
     et une fausse surcouche qui enregistrent leurs appels : `Compose` produit exactement
     `[UIView.Draw, Overlay.Draw]` ; sans surcouche enregistrée, `[UIView.Draw]` ; avec une vue sans
     `UIView`, `[Overlay.Draw]` seul ; (b) une surcouche enregistrée puis retirée n'est plus appelée.
- Validation : build des trois solutions ; suite verte ; les tests nommés dans la note.
- Commit : `feat(rendering): draw post-UI overlays after the UI composition`
- Note de validation : Étape 1 vérifiée — `OverlayViewPipeline.cs:144-149` (`RenderUIOverlay`) appelle
  `(view.UICompositionService ?? DefaultUICompositionService.Instance).Compose(gd, view, in frame)`,
  exactement la même expression que `DefaultViewPipeline.cs:53-54`. **O3 résolu : pas de blocage**,
  le crochet posé une seule fois dans `DefaultUICompositionService.Compose` couvre les trois
  pipelines par construction (l'éditeur et le Play-in-Editor passent tous deux par
  `OverlayViewPipeline`, qui délègue au même service). Étape 2 — lu `ScreenEffectComponent.cs:47-74`
  et `ScreenEffectComponentViewSizeTests.cs` : le composant ne cible aujourd'hui **aucune vue
  explicitement** — il soumet toujours au `SpriteRendererComponent` du jeu et ne lit
  `GameManager.ViewManager.ActiveView` que pour la caméra 2D, jamais pour choisir une vue de
  destination. **O4 : rien à inventer**, ce fait est simplement constaté ici pour T1.2 ; T1.1
  n'avait besoin de trancher que la forme du stockage sur `RenderView`. Forme choisie : une petite
  liste préallouée (`List<IPostUIOverlay>`, capacité 4, créée à la première inscription) plutôt
  qu'un emplacement unique, pour ne pas fermer la porte à plusieurs surcouches sur la même vue (la
  plage 90-93 du plan gardait les deux options ouvertes) ; l'accès en lecture
  (`RenderView.PostUIOverlays`) renvoie `Array.Empty<IPostUIOverlay>()` tant que rien n'est
  enregistré, donc composer une vue sans surcouche n'alloue jamais. `IPostUIOverlay` créé
  (`CasaEngine/Framework/Rendering/IPostUIOverlay.cs`, une méthode `Draw`), enregistrement/retrait
  ajoutés à `RenderView.cs` (`RegisterPostUIOverlay`/`UnregisterPostUIOverlay`/`PostUIOverlays`),
  appel ajouté dans `DefaultUICompositionService.Compose` juste après `view.UIView?.Draw()`, boucle
  `for` explicite (pas de `foreach`/LINQ/closure). Doc XML de `DefaultViewPipeline.cs:7-19` mise à
  jour pour citer les surcouches post-interface. Quatre tests ajoutés dans le nouveau fichier
  `CasaEngine.Tests/Rendering/DefaultUICompositionServiceTests.cs`, avec un faux `IUIViewRuntime` et
  un faux `IPostUIOverlay` qui enregistrent leurs appels dans une liste partagée, et un
  `RenderView` construit avec un `World` nu, une `ArcBallCameraComponent` et un stub
  `IRenderSurface` — montage repris de `InputRouterTests.CreateView`, sans démarrer de
  `GraphicsDevice` réel : `Compose_WithUIViewAndOverlay_DrawsUIViewThenOverlay` (ordre
  `[UIView.Draw, Overlay.Draw]`), `Compose_WithUIViewAndNoOverlay_DrawsOnlyUIView` (`[UIView.Draw]`
  seul), `Compose_WithNoUIViewAndAnOverlay_DrawsOnlyTheOverlay` (`[Overlay.Draw]` seul, vue sans
  `UIView`), `Compose_AfterOverlayIsUnregistered_NoLongerCallsIt` (retrait effectif). Build
  `CasaEngine.MonoGame.sln` : 0 erreur (mêmes avertissements `CS8632` préexistants, sans rapport).
  `CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 erreur. `CasaEngine.Editor.MonoGame.sln` : 0
  erreur, requis puisque cette tâche touche la composition empruntée par l'éditeur (`OverlayViewPipeline`
  délègue au même `DefaultUICompositionService`). `dotnet test --no-build` : 1626 tests (1622 de
  base après T0.1 + 4 ajoutés ici), 1625 verts, 1 échec — le même
  `EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Disables_Docking_Accent_Bars`
  préexistant et sans rapport, déjà relevé à T0.1.

### 🧪 T1.2 — L'effet d'écran se dessine depuis le crochet en mode `AboveUI`

- Objectif : `Layer = AboveUI` déplace le dessin de l'effet après l'interface, `BelowUI` ne change
  rien, et l'appareil est rendu dans l'état où le crochet l'a trouvé.
- Fichiers : `CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs`
  (`Update` `:47-74` : en `AboveUI`, ne pas soumettre ; enregistrement comme `IPostUIOverlay` sur
  la ou les vues ciblées ; dans `Draw` du crochet, `SubmitOverlay` puis `renderer.Flush(in frame,
  view.RenderStats)` immédiatement, patron `TileMapSurfaceComponent.cs:206`) ;
  `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs` (**restauration** des
  états modifiés par `Draw` `:155-206` en sortie de `Flush`, D4 ; ne toucher à rien d'autre) ;
  `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectComponentSubmissionTests.cs` ;
  `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectComponentViewSizeTests.cs`.
- Étapes :
  1. Lire `SubmitOverlay` et les deux fichiers de tests pour réutiliser leur montage.
  2. `BelowUI` : chemin d'aujourd'hui, octet pour octet ; les tests existants restent verts tels
     quels.
  3. `AboveUI` : `Update` n'appelle plus `SubmitOverlay` ; le composant s'enregistre comme surcouche
     sur la vue ciblée quand la couche passe à `AboveUI` et se retire quand elle repasse à `BelowUI`
     ou quand l'effet n'est plus `Active` ; `Draw` de la surcouche soumet puis vide immédiatement.
  4. D4 : relever les états que `SpriteRendererComponent.Draw` pose (`:155-206`), les sauvegarder à
     l'entrée de `Flush` et les restaurer à la sortie. Un test le fige avec un `GraphicsDevice` de
     test si la suite en possède un ; sinon, le noter 🧪 avec la raison, et le smoke de T2.2 le
     couvre.
  5. Tests : `BelowUI` → soumission dans `Update`, aucune surcouche enregistrée ; `AboveUI` → aucune
     soumission dans `Update`, une surcouche enregistrée, et son `Draw` soumet une fois puis vide ;
     bascule `AboveUI → BelowUI` → surcouche retirée ; `Clear()` ou `Active = false` en `AboveUI` →
     surcouche retirée.
- Validation : build ; suite verte ; `ScreenEffectComponentViewSizeTests` inchangés et verts.
- Commit : `feat(screen-effects): draw the overlay above the UI when the layer says so`
- Note de validation : Étape 1 lue - `SubmitOverlay` (`ScreenEffectComponent.cs:130-163`) et les deux
  fichiers de tests existants construisent tous deux le composant et le renderer via un jeu headless
  (`RuntimeHelpers.GetUninitializedObject`), sans jamais appeler `Flush` avec des sprites en file
  (`_effect` y reste `null`, ce que confirme aussi `SpriteRendererComponentBlendModeTests` : « The
  device-facing draw loop itself cannot run headless »). Étape 2 : le bloc `BelowUI` de `Update`
  est resté identique, seule sa garde a gagné `Service.Layer == ScreenEffectLayer.BelowUI &&` en
  tête - le corps n'a pas bougé d'une ligne, donc `BelowUI` reste le chemin d'aujourd'hui octet pour
  octet ; `ScreenEffectComponentSubmissionTests` et `ScreenEffectComponentViewSizeTests` (huit tests)
  restent verts sans modification. Étape 3 : `ScreenEffectComponent` implémente maintenant
  `IPostUIOverlay` ; `UpdateAboveUIRegistration` (nouvelle méthode privée, appelée en tête d'`Update`)
  enregistre/désenregistre le composant sur `GameManager.ViewManager.ActiveView` - **O4 tranché ici** :
  c'est la seule vue que ce composant a jamais lue (déjà utilisée pour la caméra avant ce chantier),
  donc `AboveUI` s'y enregistre sans introduire de nouvelle dépendance. `Draw` (l'entrée
  `IPostUIOverlay`) résout la caméra 2D depuis la vue reçue en paramètre (pas `ActiveView` une
  deuxième fois - c'est la même vue), soumet via `SubmitOverlay` (inchangé, réutilisé tel quel), puis
  appelle immédiatement `renderer.Flush(in frame, view.RenderStats)`, patron exact de
  `TileMapSurfaceComponent.cs:206`. Étape 4 (D4) : `SpriteRendererComponent.Flush` lit désormais
  `DepthStencilState`/`RasterizerState`/`SamplerStates[0]`/`BlendState` avant `Draw` et les restaure
  après - quatre lectures de référence, aucune allocation ; la `ScissorRectangle` était déjà
  sauvegardée/restaurée par `Draw` lui-même (`:172,205`), donc non dupliquée. **Aucun
  `GraphicsDevice` de test n'existe dans la suite** (confirmé : aucun test du dépôt ne construit
  `new GraphicsDevice(...)`, et `SpriteRendererComponentBlendModeTests` documente explicitement cette
  limite) : la restauration ne peut pas être figée par un test unitaire ici, exactement le cas que le
  plan anticipait - notée 🧪, couverte par le smoke de T2.2. Étape 5 : cinq tests ajoutés dans le
  nouveau fichier `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectComponentAboveUIRegistrationTests.cs` :
  `Update_WhenLayerIsBelowUI_SubmitsOverlayAndRegistersNoOverlay`,
  `Update_WhenLayerIsAboveUI_DoesNotSubmitAndRegistersOnTheActiveView`,
  `Update_WhenLayerTogglesBackToBelowUI_UnregistersFromTheView`,
  `Update_WhenClearedWhileAboveUI_UnregistersFromTheView`,
  `Draw_WithNoSpriteRendererComponent_DoesNothingAndDoesNotThrow`. Montage : jeu headless réutilisé
  des tests existants, plus une `GameManager` réelle (`new GameManager(null)`, déjà utilisée telle
  quelle par `GameManagerWorldLoadTests`) avec une `RenderView` ajoutée (`ViewManager.Add` en fait
  l'`ActiveView`), câblés sur le jeu headless par réflexion sur les propriétés à setter privé
  `GameManager`/`SpriteRendererComponent` (même technique que le champ `_components` déjà utilisé par
  ces tests) ; la vue porte une `Camera2dComponent` dimensionnée (`OnScreenResized(320, 240)`), pas
  `ArcBallCameraComponent`, pour que `TryGetCameraViewSize` résolve et qu'`Update` ne retombe jamais
  sur `CasaEngineGame.ScreenSizeWidth/Height` (qui lit `Game.Window`, absent en tête-headless,
  indépendamment de ce chantier). Le champ privé `_pixelTexture` est préchargé par réflexion pour que
  le `SubmitOverlay` interne d'`Update` (qui n'a pas de texture explicite, contrairement aux tests de
  soumission existants) atteigne la file sans toucher un `GraphicsDevice`. **Ce que ces tests
  prouvent** : le routage `BelowUI`/`AboveUI` dans `Update`, la soumission (ou son absence), et le
  cycle de vie complet de l'enregistrement (bascule de couche, `Clear()`) - exactement le contenu
  vérifiable de l'étape 5 sans dispositif GPU. **Ce qu'ils ne prouvent pas** : que le `Draw` de la
  surcouche soumet réellement puis vide la file via un `Flush` GPU réel (le seul test qui invoque
  `Draw` le fait sans `SpriteRendererComponent` câblé, pour vérifier qu'il ne lève pas, pas qu'il
  vide) - même limite que D4 ci-dessus, notée 🧪, couverte par le smoke de T2.2. `dotnet build
  CasaEngine.MonoGame.sln` : 0 erreur (mêmes avertissements `CS8632` préexistants). `dotnet build
  CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 erreur. `dotnet build CasaEngine.Editor.MonoGame.sln`
  (lancé par prudence bien que cette tâche ne touche pas la composition) : 0 erreur. `dotnet test
  CasaEngine.Tests/CasaEngine.Tests.csproj --no-build` : 1631 tests (1626 de base après T1.1 + 5
  ajoutés ici), 1630 verts, 1 échec - le même `EditorControlTemplateAssetLoadingTests
  .EditorThemeAsset_Disables_Docking_Accent_Bars` préexistant et sans rapport, déjà relevé à T0.1 et
  T1.1. **Statut 🧪, pas ✅** : le code est écrit, buildé et testé jusqu'à la limite d'un dispositif
  GPU absent de cette suite ; la restauration d'état GPU (D4) et le `Draw`→`Flush` réel de la
  surcouche restent à observer par le smoke visible de T2.2 (hors périmètre de cette tâche).

---

## Phase 2 — La décision, les textes et le smoke

### ✅ T2.1 — ADR et documentation

- Objectif : la décision est enregistrée et les textes qui disaient « below the UI » disent
  désormais ce qui est vrai.
- Fichiers : `docs/decisions/<numéro suivant>-screen-effect-above-ui.md` (via le skill `adr` du
  dépôt, qui numérote et indexe) ; `CasaEngine/Framework/Rendering/Depth/RenderPass2D.cs:12-17` ;
  `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:7-19` ;
  `docs/engine/screen-effects.md:57-60` et sa section d'API (`:32-53`) pour `Layer`.
- Étapes :
  1. ADR : contexte (le besoin Alundra, cité), décision (D1 à D4), conséquences (crochet de
     composition, aucun pipeline modifié, vidage immédiat, restauration d'états), voies écartées
     (seconde file et nouvelle passe ; décorateur de composition), avec leurs raisons.
  2. Les textes : la passe `ScreenEffects` reste « below the UI » **par défaut**, et la couche
     `AboveUI` dessine après la composition ; `DefaultViewPipeline` décrit la composition comme
     « UI puis surcouches post-interface ».
- Validation : relecture ; `rg -n "below the UI" CasaEngine docs` ne rend plus que des mentions
  exactes du cas par défaut.
- Commit : `docs(rendering): document the above-UI screen effect layer`
- Note de validation : Skill `adr` suivi (`.claude/skills/adr/SKILL.md`) : `docs/decisions/README.md`
  et `template.md` lus, numéro suivant `0033` pris (dernier existant `0032`). ADR créé
  (`docs/decisions/0033-screen-effect-above-ui-layer.md`) : contexte citant le besoin Alundra
  (`docs/plan-e13-hud.md` du dépôt parent, D-E13-11) et les faits vérifiés du plan (pipelines,
  `TileMapSurfaceComponent.cs:206`, `RenderPassDepthOffset` switch explicite, limite caméra 2D) ;
  décision D1-D5 listée en puces ; les deux voies écartées avec leur raison (seconde file/nouvelle
  passe ; décorateur de composition) reprises telles que le plan les énonce (§« Voies écartées »,
  lignes 118-124) ; conséquences (contrat `IPostUIOverlay` réutilisable, boucle `for` sans
  allocation, pipelines/passes inchangés, `Clear()` laisse `Layer` intact, limite caméra 2D
  préexistante, restauration GPU non testable unitairement). Ligne ajoutée à
  `docs/decisions/README.md`. `RenderPass2D.cs:12-17` : le commentaire dit maintenant « above every
  world/effects layer and, by default, below the UI », avec un renvoi à `ScreenEffectService.Layer`/
  `ScreenEffectLayer.AboveUI` et à l'ADR-0033. `DefaultViewPipeline.cs:7-19` : **déjà mis à jour par
  T1.1** (note de validation de T1.1, « Doc XML de `DefaultViewPipeline.cs:7-19` mise à jour pour
  citer les surcouches post-interface ») — vérifié par relecture, le texte actuel dit déjà « the UI
  itself, then any post-UI overlays registered on the view », aucune modification supplémentaire
  nécessaire ici. `docs/engine/screen-effects.md` : `:57-60` (devenu `:57-63` après l'ajout) précise
  désormais que la couche `BelowUI` est la valeur **par défaut**, que `AboveUI` fait s'enregistrer le
  composant comme `IPostUIOverlay` sur la vue active, dessiné par `DefaultUICompositionService
  .Compose` après `UIView.Draw()` avec soumission puis vidage immédiat, et cite l'ADR-0033 ; section
  d'API (`:32-53`) complétée d'un exemple `effects.Layer = ScreenEffectLayer.AboveUI` et d'un rappel
  que `Clear()` ne touche jamais `Layer`. Vérification finale : `rg -n "below the UI" CasaEngine
  docs` ne rend plus que deux mentions, toutes deux le cas par défaut exact -
  `RenderPass2D.cs:15` (« by default, below the UI ») et le contexte historique de
  `0033-screen-effect-above-ui-layer.md:15` qui décrit le problème *avant* le chantier (« structurally
  below the UI composition step »), pas une affirmation du comportement actuel inconditionnel.
  Aucun code de production touché : seuls des commentaires XML, un ADR et deux documents `docs/` ont
  changé, donc pas de nouveau build/test requis par-delà celui déjà fait à T1.2 ; `dotnet build
  CasaEngine.MonoGame.sln` relancé par prudence : 0 erreur, mêmes avertissements `CS8632`
  préexistants, sans rapport.

### ⏳ T2.2 — Le smoke visible dans la démo `TileMapDemo`

- Objectif : quelqu'un peut voir, dans le moteur seul, une interface MGUI s'assombrir avec la
  scène, sur le seul chemin où le placement du quad de l'effet est supporté : une caméra 2D.
- Fichiers : `CasaEngine.Demos/Demos/TileMapDemo.cs` (caméra 2D à `:98`) ; un petit écran MGUI non
  modal poussé dans cette démo, sur le modèle de `CasaEngine.Demos/Demos/UIOverlay/HudScreen.cs:
  16-92` (à réutiliser tel quel s'il se pousse sans dépendance à la scène 3D, sinon un écran
  minimal dédié) ; le `README` de la démo s'il existe.
- Étapes :
  1. Lire `TileMapDemo` et la façon dont les démos exposent des touches ou des boutons.
  2. **Précondition, avant tout le reste** : déclencher un fondu `BelowUI` dans `TileMapDemo` et
     observer qu'il couvre **tout le viewport**. S'il ne le couvre pas, passer la tâche en
     ⚠️ Blocked, écrire O5, et s'arrêter : le smoke ne prouverait rien.
  3. Pousser l'écran MGUI, puis ajouter deux commandes : le même fondu vers le noir en `BelowUI`
     et en `AboveUI`, via `ScreenEffectService.StartFade` et `Layer`, durée courte, retour
     automatique.
  4. Chercher si l'automation des démos sait capturer le back-buffer en processus (`--screenshot-out`
     ou équivalent, voir `GameEditor.CaptureAutomationScreenshot` `:5988-6020` pour la technique) ;
     si oui, une capture à mi-fondu de chaque mode, avec un contrôle chiffré : le pixel d'un
     élément de l'écran MGUI est plus sombre en `AboveUI` qu'en `BelowUI`. Si non, le noter et
     s'en tenir à l'observation.
- Validation : précondition observée ; puis l'auteur observe les deux fondus dans la démo : en
  `AboveUI` l'interface s'assombrit avec la scène, en `BelowUI` elle reste lisible. Tant que cette
  observation n'est pas faite, la tâche reste 🧪. Capture chiffrée en plus si l'étape 4 le permet.
- Commit : `feat(demos): show the above-UI screen effect in the TileMap demo`

---

## Phase 3 — Clôture

### ⏳ T3.1 — Verifier, archivage, index

- Objectif : le chantier est vérifié par un tiers frais, archivé, indexé.
- Fichiers : ce plan (section de clôture) ; `ai-agent/tasks/archive/` ; `ai-agent/README.md`.
- Étapes :
  1. Verifier frais sur le claim de « Validation globale », avec le test d'ordre et le smoke de
     T2.2 comme acceptations principales, et la suite complète.
  2. Disposer chaque avis (FIX / DEFER / REJECT) dans la section de clôture.
  3. `git mv` du plan vers `tasks/archive/`, ligne du tableau de `README.md` sur le modèle de
     `tilemap-depth-settings-tasks.md`.
- Validation : verifier CONFIRMED ; suite verte ; ligne du README présente.
- Commit : `docs(ai-agent): archive the above-UI screen effect plan`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | **Tranché à T1.2** : `Flush(in RenderFrame frame, RenderStats stats = null)` n'utilise déjà `stats` nulle part dans son corps, pour tout appelant existant - le paramètre est accepté mais inerte. Le vidage immédiat de la surcouche lui passe `view.RenderStats` exactement comme tout autre appelant (`DefaultViewPipeline.cs:45`, `TileMapSurfaceComponent.cs:206`) : aucune cumulation ne se produit, pas de compteur propre à ajouter. | T1.2 |
| O2 | **Tranché dans ce plan** : `Clear()` ne remet pas la couche, qui est un réglage de rendu. Si la lecture de `ScreenEffectService.cs` à T0.1 montre que `Clear()` a une sémantique « tout remettre » documentée qui contredit ce choix, passer en ⚠️ et demander. | T0.1 |
| O3 | **Tranché à T1.1** : `OverlayViewPipeline.cs:144-149` appelle la même expression que `DefaultViewPipeline.cs:53-54` (`view.UICompositionService ?? DefaultUICompositionService.Instance`). Le crochet unique dans `DefaultUICompositionService.Compose` couvre les trois pipelines par construction. | T1.1 |
| O4 | **Tranché à T1.2** : `AboveUI` s'enregistre sur `GameManager.ViewManager.ActiveView`, la même vue que `Update` lisait déjà pour la caméra avant ce chantier - aucune nouvelle dépendance. | T1.1, T1.2 |
| O5 | Si le fondu `BelowUI` ne couvre pas tout le viewport dans `TileMapDemo`, le placement du quad a un défaut hors de ce chantier ; à remonter à l'auteur, ne pas corriger ici. | T2.2 |

## Hors périmètre

- Reproduire la capture d'image de la PSX pendant un warp : le portage continue de rendre la scène
  vivante sous le fondu ; seul l'ordre de dessin est traité.
- Le dessin du bureau MGUI lui-même, `UIRoot.Draw` : inchangé.
- Les trois pipelines de vue : inchangés ; le crochet vit dans la composition.
- `RenderPass2D`, `RenderPassDepthOffset` et leurs tests : inchangés.
- La tranche C4.dll du portage, qui armera `Layer = AboveUI` pour le fondu de warp : elle vit dans
  `docs/plan-e13-hud.md` du dépôt parent, après bump du pointeur de sous-module.

## Journal

| Date | Évènement |
|---|---|
| 2026-09-19 | Plan rédigé, révision 1 : seconde file de sprites et nouvelle passe `ScreenEffectsAboveUI`, quatrième temps dans `DefaultViewPipeline`. |
| 2026-09-19 | Relecture adverse : **REVISE**, trois P2, tous acceptés. (1) `RenderPassDepthOffset` est un `switch` explicite, pas une dérivation par rang : la nouvelle passe serait tombée à zéro. (2) `OverlayViewPipeline` ne délègue pas au pipeline par défaut : une seconde file y aurait grossi sans borne jusqu'au dépassement de tampon. (3) Aucun contrôle en dépôt ne pouvait révéler un quad émis dans le bon ordre mais invisible ; la règle §6 exige un sample. **Révision 2** : la conception change, crochet de composition et vidage immédiat sur le précédent de `TileMapSurfaceComponent.cs:206`, ce qui supprime la cause des deux premiers blocages ; T2.2 ajoute le smoke dans la démo `UIOverlay` ; D4 impose la restauration des états GPU. |
| 2026-09-19 | Relecture de clôture : **REVISE**, un P2, accepté. La démo `UIOverlay` a une caméra 3D, or le placement du quad de l'effet n'est valide qu'avec une `Camera2dComponent` (`ScreenEffectComponent.cs:53-67, :123-129`) ; une jauge qui ne s'assombrirait pas y serait indiscernable d'un mauvais ordre. **Révision 3** : le smoke passe dans `TileMapDemo`, seule démo à caméra 2D (`TileMapDemo.cs:98`), avec une précondition, le fondu `BelowUI` couvre le viewport, et un arrêt O5. **Deuxième REVISE consécutif, plafond atteint : disposition en session principale, pas de nouvelle soumission.** Le plan part à l'auteur pour approbation. |
