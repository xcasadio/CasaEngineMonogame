# Plan d'implementation - Renderer MGUI CasaEngine

## Objectif

MGUI ne doit plus etre consomme comme si le backend MonoGame concret faisait partie du coeur UI.
CasaEngine doit donc posseder son propre backend de rendu MGUI, branche sur les contrats publics MGUI,
sans dependance runtime concrete a `MGUI.MonoGame` ni au type `MainRenderer` amont.

Le but n'est pas de reouvrir `MGUI.Core`, ni de reecrire les controles UI.
Le but est de fournir, cote CasaEngine, le backend concret necessaire pour faire vivre `MGDesktop`
dans le runtime jeu, le world-space UI et le shell editeur.

La reference comportementale obligatoire est `MGUI/MGUI.Shared/Rendering/MainRenderer.cs`.
Le backend CasaEngine ne doit pas "inventer" un renderer alternatif au sens produit.
Il doit reproduire le meme resultat observable que `MainRenderer`, puis exprimer cette implementation
avec les conventions et services CasaEngine.

## Philosophie de migration

- Le code source de verite est `MainRenderer.cs` et, par extension immediate, les usages que `DrawTransaction.cs` fait de ce renderer.
- Toute responsabilite visible dans `MainRenderer` doit etre soit:
	- portee telle quelle dans le backend CasaEngine,
	- adaptee explicitement avec une justification CasaEngine,
	- ou laissee de cote de maniere documentee si elle est prouvee inutilisee dans les chemins CasaEngine.
- Aucune tache ne doit reposer sur une supposition du type "CasaEngine n'a probablement pas besoin de ce comportement" sans audit factuel.
- La cible n'est pas une API qui ressemble vaguement a `MainRenderer`; la cible est une parite de comportement sur les demos et ecrans CasaEngine.
- Les adaptations CasaEngine autorisees sont structurelles, pas fonctionnelles: usage de `EngineRuntimeContext`, `RenderTargetPool`, `FontSystem`, bootstrap per-view et regles de rendu du moteur.

## Contraintes pour l'agent IA

- Faire un commit atomique apres chaque tache terminee.
- Mettre a jour l'icone de statut dans ce fichier apres chaque tache.
- Si une tache codee compile mais n'a pas encore sa validation ciblee, la laisser en `🧪` et ne pas la marquer `✅`.
- Ne pas reintroduire de dependance directe de CasaEngine vers `MGUI.MonoGame` dans l'etat final.
- Ne pas modifier `MGUI.Core` ou `MGUI.Shared` sauf blocage strictement necessaire a la compatibilite.
- Preferer porter/adapter le backend amont par petits morceaux plutot que repartir de zero.
- Utiliser `MainRenderer.cs` comme checklist concrete des responsabilites a couvrir: host, raw input, surface, batches, content, fonts, assets, text engine, update args, views, textures utilitaires.
- Verifier explicitement les comportements caches de `MainRenderer`: `ScrollMarker`, textures de couleur unie, cache de cercles, `RegisterView`, `UpdateViews`, `DrawViews`, gestion `PreviewUpdate` / `EndUpdate`.
- Reutiliser quand c'est pertinent les services existants du moteur: `EngineRuntimeContext.WindowInputSource`, `RenderTargetPool`, `FontSystem`, `DefaultUICompositionService`, `ScreenStack`.
- Toute tache touchant le draw doit restaurer proprement les etats GPU et eviter les allocations evitables dans les hot paths.
- Validation bornee uniquement: builds cibles, tests filtres, et demos UI proches du perimetre.
- Quand un comportement amont est adapte a la philosophie CasaEngine, noter noir sur blanc l'equivalence entre le membre amont et son homologue CasaEngine.

## Legende statut

- `✅` Done
- `🚧` In progress
- `⏳` Todo
- `🧪` Needs testing
- `⚠️` Blocked

## References

- `MGUI/Docs/monogame-host-integration-guide.md`
- `MGUI/Docs/rendering-backend-architecture.md`
- `MGUI/Docs/rendering-decoupling-tasks.md`
- `MGUI/MGUI.MonoGame/MGUI.MonoGame.csproj`
- `MGUI/MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI/MGUI.Shared/Rendering/DrawTransaction.cs`
- `CasaEngine/Framework/UI/UIRoot.cs`
- `CasaEngine/Framework/UI/ViewRenderHost.cs`
- `CasaEngine/Framework/UI/MguiViewRuntimeFactory.cs`
- `CasaEngine/Framework/Rendering/DefaultUICompositionService.cs`
- `CasaEngine.Editor/Game1.cs`
- `CasaEngine.Demos/Demos/UIOverlayDemo.cs`
- `CasaEngine.Demos/Demos/WorldSpaceUIDemo.cs`
- `CasaEngine.Tests/`
- `docs/casaengine-mgui-backend.md`

## Criteres de succes finaux

- `UIRoot` et `MguiViewRuntimeFactory` n'instancient plus `MainRenderer`.
- CasaEngine possede un backend concret qui implemente `IUIDesktopRuntime` et `IUIDrawTransaction`.
- Le runtime jeu, le world-space UI et le shell editeur utilisent ce backend CasaEngine.
- Le code CasaEngine ne depend plus d'un backend concret upstream `MGUI.MonoGame`.
- Les responsabilites visibles de `MainRenderer` utiles a CasaEngine sont toutes mappees explicitement vers des equivalents CasaEngine.
- Les resultats visuels et comportementaux observables sur les demos CasaEngine sont alignes avec ceux obtenus via `MainRenderer`.
- Les etats GPU sont restaures correctement apres draw UI et clipping.
- Les overlays UI et le world-space UI restent fonctionnels avec l'input route actuel.

## Couverture obligatoire de `MainRenderer`

Chaque point suivant doit etre audite puis marque comme `porte`, `adapte` ou `hors perimetre prouve` pendant la migration:

- constructeur et dependances: `IRenderHost`, `IRawInputSource`, `IUISurface`, `IUIAssetProvider`
- services runtime: `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `ContentManager`, `FontManager`, `AssetProvider`, `TextEngine`
- cycle de frame: `PreviewUpdate`, `EndUpdate`, `UpdateArgs`, `Input.Update`, `Mouse.UpdateHandlers`, `Keyboard.UpdateHandlers`
- surface desktop: `GetViewport(int margin)`
- gestion de vues: `RegisterView`, `UnregisterView`, `Views`, `UpdateViews`, `DrawViews`
- ressources utilitaires: `ScrollMarker`
- caches runtime: `GetOrCreateSolidColorTexture`, `GetOrCreateWhiteCircleTexture`, nettoyage des textures de cercle disposees
- contrat de draw: `CreateDrawTransaction(...)`

Si un point n'est pas repris a l'identique, la difference doit etre motivee par un besoin CasaEngine verifiable.

## Taches

### ✅ T01 - Auditer les seams backend a reprendre

**But :** figer exactement ce que CasaEngine doit implementer et ce qu'il peut simplement reutiliser.

**Travail attendu :**
- Cartographier dans CasaEngine tous les usages directs de `MainRenderer`, `DrawTransaction`, `IRenderHost`, `IUISurface`, `IUIDesktopRuntime`, `IUIRenderContext`, `IUIDrawTransaction`, `IUIImageResource` et `ITextMeasurementEngine`.
- Lister le sous-ensemble du backend amont a reprendre ou adapter depuis `MGUI.MonoGame.csproj`.
- Produire une matrice de parite `MainRenderer member -> equivalent CasaEngine / adaptation / hors perimetre`.
- Lire `MainRenderer.cs` en entier et ne valider l'audit qu'une fois toutes ses responsabilites repertoriees.
- Choisir un emplacement cible unique pour le backend CasaEngine, par exemple `CasaEngine/Framework/UI/Backend/MonoGame/`.
- Ajouter une courte note d'audit dans ce fichier si une hypothese initiale s'avere fausse.

**Livrable :**
- Frontiere de migration explicite.
- Liste de classes CasaEngine a creer ou a deplacer.
- Tableau de couverture des responsabilites de `MainRenderer`.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`

**Commit attendu :** `docs(ui): audit CasaEngine MGUI backend migration seams`

---

### ✅ T02 - Creer le squelette du backend CasaEngine

**But :** poser une frontiere de code claire avant de porter le comportement.

**Travail attendu :**
- Creer le dossier et le namespace cibles du backend CasaEngine.
- Ajouter les types de composition minimums, par exemple `CasaMonoGameBackendBootstrap`, `CasaMonoGameBackendSession`, `CasaDesktopRuntime`, `CasaDrawTransaction`, `CasaUIImageResource`, `CasaUIAssetProvider`.
- Garder les APIs publiques orientees contrats MGUI, pas types concrets amont.
- Faire apparaitre dans le code la correspondance voulue avec `MainRenderer` pour eviter une derive architecturale des le scaffold.
- Ajuster les references projet/package uniquement si necessaire pour compiler contre les contrats MGUI split.

**Livrable :**
- Backend CasaEngine identifiable dans l'arborescence.
- Scaffolding compile sans brancher encore le runtime reel.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`

**Commit attendu :** `feat(ui): scaffold CasaEngine MGUI backend`

---

### ✅ T03 - Porter les hosts de rendu et le bridge d'input brut

**But :** fournir au backend CasaEngine des adapters de boucle de vie et d'input compatibles avec le moteur.

**Travail attendu :**
- Porter ou adapter l'equivalent de `GameRenderHost<TObservableGame>` pour le shell editeur.
- Porter ou adapter l'equivalent de `ViewRenderHost` pour les vues runtime par surface.
- Faire consommer `EngineRuntimeContext.WindowInputSource` ou son override editor au lieu de relire l'input natif hors du chemin officiel.
- Preserver la conversion souris ecran -> viewport local et les signaux `PreviewUpdate` / `EndUpdate`.
- Reprendre aussi la logique utile de resize / scissor si CasaEngine depend du meme comportement observable.

**Livrable :**
- Hosts CasaEngine utilisables par le runtime jeu et l'editeur.
- Source d'input brute coherente avec le routage existant.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`

**Commit attendu :** `feat(ui): add CasaEngine MGUI render hosts and raw input bridge`

---

### ✅ T04 - Porter les services de base du runtime `MainRenderer`

**But :** reconstruire les services de base que `MainRenderer` initialise des sa construction.

**Travail attendu :**
- Implementer l'equivalent CasaEngine de `BackBufferSurface` ou de la surface logique necessaire a `IUISurface`.
- Creer et brancher `SpriteBatch`, `PrimitiveBatch`, `ContentManager`, `FontManager`, provider d'assets et `TextEngine` par defaut comme le fait `MainRenderer`.
- Reprendre la contrainte de compatibilite du moteur texte attendue par le draw backend.
- Charger les ressources utilitaires de construction, en particulier `ScrollMarker`.
- Implementer le wrapper image UI requis par MGUI a partir de `Texture2D`.

**Livrable :**
- Services de base du runtime alignes sur ceux de `MainRenderer`.
- Surface, images UI et provider d'assets concrets localises cote CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`

**Commit attendu :** `feat(ui): port CasaEngine MGUI runtime services and assets`

---

### ✅ T05 - Porter le cycle de frame et la gestion de vues de `MainRenderer`

**But :** reproduire les comportements runtime qui rendent `MainRenderer` vivant sur une frame complete.

**Travail attendu :**
- Implementer `IUIDesktopRuntime` dans `CasaDesktopRuntime`.
- Reprendre la logique `PreviewUpdate` -> `UpdateArgs` -> `Input.Update(...)` -> `PreviousUpdateTimeSpan`.
- Reprendre la logique `EndUpdate` -> `Mouse.UpdateHandlers()` / `Keyboard.UpdateHandlers()`.
- Implementer `RegisterView`, `UnregisterView`, `Views`, `UpdateViews` et `DrawViews` avec les memes garanties de base.
- Exposer `CreateDrawTransaction(...)` sans fuite de type concret amont.
- Garder le hot path sobre en allocations et coherent avec les frames moteur.

**Livrable :**
- Runtime desktop concret CasaEngine raccordable a `MGDesktop` avec comportement de frame aligne sur `MainRenderer`.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`

**Commit attendu :** `feat(ui): port CasaEngine desktop runtime frame lifecycle`

---

### 🧪 T06 - Porter les textures utilitaires et caches runtime de `MainRenderer`

**But :** ne pas oublier les comportements moins visibles mais requis par `DrawTransaction` et certains controles.

**Travail attendu :**
- Porter `GetOrCreateSolidColorTexture(...)` avec cache et reutilisation.
- Porter `GetOrCreateWhiteCircleTexture(...)` avec la meme logique generale de rayons min/max et de reutilisation.
- Porter le nettoyage des textures de cercle disposees si ce comportement reste necessaire.
- Verifier explicitement les usages de `ScrollMarker`, `BlackPixel`, `WhitePixel` et des cercles dans les chemins CasaEngine.

**Livrable :**
- Caches runtime CasaEngine couvrant les memes primitives utilitaires que `MainRenderer`.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- verification manuelle ciblee sur scrollbars, symboles et formes UI

**Commit attendu :** `feat(ui): port CasaEngine runtime texture caches`

---

### 🧪 T07 - Implementer le coeur de `CasaDrawTransaction`

**But :** rendre possible le draw UI nominal sans dependre de `DrawTransaction` amont.

**Travail attendu :**
- Implementer `IUIDrawTransaction` et `IUIRenderContext` dans `CasaDrawTransaction`.
- Mapper explicitement chaque dependance de `DrawTransaction` vers son equivalent CasaEngine: `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `FontManager`, `TextEngine`, `TextRenderer`, caches de textures utilitaires.
- Porter la gestion des contextes sprites/primitives, des `DrawSettings`, des transforms temporaires et du switch render target.
- Reprendre les helpers indispensables au draw de base des controles MGUI deja utilises par CasaEngine.
- Restaurer correctement `BlendState`, `DepthStencilState`, `RasterizerState`, viewport et scissor rectangle.

**Livrable :**
- Transaction de draw CasaEngine capable de dessiner l'overlay UI nominal.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- verification manuelle ciblee sur `UIOverlayDemo`

**Commit attendu :** `feat(ui): implement CasaEngine draw transaction core`

---

### 🧪 T08 - Porter le pipeline de clipping et les render targets temporaires

**But :** recuperer les comportements UI reels qui ne passent pas par le draw le plus simple.

**Travail attendu :**
- Porter ou adapter l'equivalent de `ClipManager` dans le backend CasaEngine.
- Implementer les clips rectangulaires, l'intersection, les render targets temporaires et les chemins de masque encore necessaires.
- Verifier que le pipeline de clip ne fuit pas d'etat GPU entre vues ou entre passes.
- Utiliser le `RenderTargetPool` moteur si le contrat colle, sinon garder un pool local borne et explicite.

**Livrable :**
- Clipping MGUI fonctionnel dans le backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- verification manuelle ciblee sur scroll/clipping/menu contextuel dans l'editeur

**Commit attendu :** `feat(ui): port CasaEngine clipping and temporary render target path`

---

### 🧪 T09 - Raccorder le texte runtime et les fontes CasaEngine

**But :** conserver un rendu texte stable apres remplacement du backend concret.

**Travail attendu :**
- Rebrancher `FontStashSharpTextEngine` sur `CasaEngineGame.FontSystem` et sur le backend CasaEngine.
- Garder le calibrage `MatchSpriteFontSizing(...)` quand il reste pertinent.
- S'assurer que le backend de draw texte attendu par MGUI est bien fourni par la combinaison retenue.
- Eviter d'avoir un chemin texte runtime et un chemin texte editor qui divergent sans raison valable.

**Livrable :**
- Texte HUD, labels, tooltips et panneaux editeur rendus via le backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- verification manuelle ciblee sur `UIOverlayDemo` et sur le shell editor

**Commit attendu :** `feat(ui): reconnect CasaEngine text engine to custom MGUI backend`

---

### 🧪 T10 - Brancher le runtime jeu sur le backend CasaEngine

**But :** migrer le chemin runtime per-view sans casser `IUIViewRuntime` ni `ScreenStack`.

**Travail attendu :**
- Remplacer l'instanciation directe de `MainRenderer` dans `UIRoot`.
- Faire construire `MGDesktop` a partir du runtime CasaEngine via `IUIDesktopRuntime`.
- Garder `IUIViewRuntime`, `ScreenStack`, `DefaultUICompositionService` et le pipeline de vue stables autant que possible.
- Nettoyer les `using`, commentaires et proprietes qui fuient encore `MainRenderer` dans le chemin runtime.

**Livrable :**
- Runtime jeu MGUI base sur le backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -nologo`
- verification manuelle ciblee sur `UIOverlayDemo`

**Commit attendu :** `refactor(ui): wire runtime UIRoot to CasaEngine MGUI backend`

---

### 🧪 T11 - Migrer le shell editeur sur le backend CasaEngine

**But :** supprimer le bootstrap direct sur `MainRenderer` dans l'editeur MonoGame.

**Travail attendu :**
- Migrer `CasaEngine.Editor/Game1.cs` vers le bootstrap backend CasaEngine.
- Preserver le wiring actuel: input fenetre cache, docking, fontes editor, chargement des ressources par defaut, layout shell.
- Verifier que les panneaux editor visibles au demarrage restent rendus et interactifs.
- Eviter un fork de logique editor si le meme backend peut servir au runtime et a l'editeur.

**Livrable :**
- Shell editor branche sur le backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- verification manuelle ciblee: lancement editor, affichage shell, clics UI de base

**Commit attendu :** `refactor(editor): migrate editor shell to CasaEngine MGUI backend`

---

### 🧪 T12 - Reconnecter le world-space UI et l'input projete

**But :** valider que le backend CasaEngine couvre aussi le chemin UI hors ecran classique.

**Travail attendu :**
- Verifier `WorldUIComponent` et ses surfaces offscreen avec le nouveau backend.
- Faire descendre l'input projete jusqu'au host/snapshot du backend CasaEngine sans fallback natif parasite.
- Confirmer que le draw world-space n'introduit pas de fuite d'etat GPU sur les vues suivantes.
- Corriger les adaptateurs si le path offscreen demande un traitement legerement different du path overlay.

**Livrable :**
- World-space UI fonctionnel avec le backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -nologo`
- verification manuelle ciblee sur `WorldSpaceUIDemo`

**Commit attendu :** `feat(ui): reconnect world-space UI to CasaEngine MGUI backend`

---

### ✅ T13 - Ajouter les garde-fous de parite et documenter le bootstrap final

**But :** laisser un etat robuste pour le prochain agent et empecher le retour des dependances concretes amont.

**Travail attendu :**
- Ajouter des tests ou garde-fous d'architecture dans `CasaEngine.Tests` pour epingler l'absence de dependance CasaEngine a `MainRenderer`, `DrawTransaction` et `MGUI.MonoGame` sur les chemins nominaux.
- Ajouter un garde-fou de couverture pour verifier que la matrice de parite `MainRenderer` est tenue a jour.
- Quand c'est possible a cout raisonnable, ajouter des validations ciblees sur les comportements clefs portes depuis `MainRenderer`.
- Documenter le bootstrap CasaEngine final et les limites restantes si certaines compatibilites legacy demeurent.
- Mettre a jour ce fichier avec les statuts finaux et les vraies validations executees.
- Nettoyer les references/commentaires/residus de migration devenus faux.

**Livrable :**
- Garde-fous automatiques et doc courte de consommation du backend CasaEngine.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -nologo`
- si des tests filtres existent, les executer avec un filtre borne et documenter lequel

**Commit attendu :** `test(ui): add guards for CasaEngine MGUI backend ownership and parity`

---

## Ordre d'execution recommande

1. `T01`
2. `T02`
3. `T03`
4. `T04`
5. `T05`
6. `T06`
7. `T07`
8. `T08`
9. `T09`
10. `T10`
11. `T11`
12. `T12`
13. `T13`

## Validations executees

- `dotnet build .\CasaEngine\CasaEngine.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Tests\CasaEngine.Tests.csproj -nologo`
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -nologo --no-build --filter CasaMguiBackendOwnershipTests`

## Validation manuelle restante

- `UIOverlayDemo`
- lancement du shell editeur
- `WorldSpaceUIDemo`

## Notes de mise en oeuvre

- Le chemin nominal ne doit plus dependre du pont legacy `MGDesktop.Renderer`.
- Tant que possible, conserver `IUIViewRuntime` et `IUIViewRuntimeFactory` stables pour limiter l'onde de choc dans le moteur.
- Si un port depuis le backend amont est repris quasi tel quel, documenter localement ce qui a ete adapte pour les services CasaEngine.
- Sur chaque zone du backend, partir du comportement observe dans `MainRenderer` avant de chercher une expression "plus CasaEngine".
- La philosophie CasaEngine ici n'est pas de changer le resultat du renderer, mais d'heberger ce resultat dans les abstractions, services et contraintes du moteur.
- Si une tache revele qu'un contrat MGUI manque encore pour ce scenario, isoler le blocage, marquer `⚠️`, documenter le manque exact, puis proposer le plus petit ajustement amont necessaire.