# Plan de refactor - Backend MonoGame extensible pour Apos.Shapes et NvgSharp

## Objectif

Conserver l'idee d'un backend MGUI concret base sur MonoGame, mais refactorer l'architecture CasaEngine pour que :

- le backend reste pilote par CasaEngine et non par `MainRenderer` amont,
- les primitives 2D puissent etre rendues via un service interchangeable, avec une implementation `Apos.Shapes`,
- les outils visuels avances d'editeur puissent utiliser un canvas vectoriel dedie, avec une implementation `NvgSharp`,
- `MGUI.Shared` et `MGUI.Core` ne soient pas deformes pour absorber une API vectorielle qui n'est pas la leur.

La cible n'est pas de rendre MGUI "renderer agnostic" au sens absolu.
La cible est de rendre le backend CasaEngine assez modulaire pour brancher plusieurs sous-systemes de rendu sous un backend MonoGame unique.

## Decisions d'architecture non negociables

1. MGUI reste la couche widgets / layout / input / clipping logique.
2. Le backend CasaEngine reste un backend MonoGame concret.
3. `Apos.Shapes` est integre comme moteur de primitives 2D derriere un contrat CasaEngine, pas directement dans `MGUI.Shared`.
4. `NvgSharp` est integre comme canvas vectoriel d'overlay editeur derriere un contrat CasaEngine, pas comme remplacement de `IUIDrawContext`.
5. Les integrations `Apos.Shapes` et `NvgSharp` doivent avoir un fallback clair vers le comportement actuel tant que la migration n'est pas terminee.
6. Les contrats publics MGUI ne doivent etre modifies qu'en cas de blocage prouve et avec compatibilite explicite.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin de chaque tache, remplacer l'icone par `✅`, `🧪` ou `⚠️`.
4. Ne jamais laisser une tache en `🚧` a la fin d'une session.
5. Mettre a jour ce fichier dans le meme commit que le code de la tache.
6. Faire un commit atomique par tache.
7. Conserver un build compilable apres chaque tache.
8. Ne pas brancher `NvgSharp` directement dans `MGUI.Shared` ou `MGUI.Core`.
9. Ne pas brancher `Apos.Shapes` directement dans les controles MGUI ; l'integration doit passer par un service CasaEngine.
10. Toute tache qui modifie le draw path doit verifier la restauration des etats GPU.
11. Toute tache qui ajoute une dependance externe doit documenter :
   - pourquoi elle est necessaire,
   - dans quel projet elle vit,
   - quel est le fallback si l'implementation n'est pas encore activee.
12. Toute tache terminee doit ajouter ou mettre a jour au moins un des elements suivants :
   - test borne dans `CasaEngine.Tests`,
   - demo ciblee dans `CasaEngine.Demos`,
   - documentation technique dans `docs/` ou `ai-agent/rendering/`.

## Legende des statuts

- `⏳` Todo
- `🚧` In progress
- `✅` Done
- `🧪` Needs testing
- `⚠️` Blocked

## Validation minimale par tache

- Build moteur cible : `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`
- Build editeur cible : `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- Build demos cible : `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- Tests bornes : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter <FiltreCible> --no-restore`

Validation manuelle ciblee selon la tache :

- `UIOverlayDemo`
- `WorldSpaceUIDemo`
- shell editeur CasaEngine
- un overlay d'editeur exploitant le nouveau canvas vectoriel

## Criteres d'acceptation finaux

- `CasaDrawTransaction` n'est plus un monolithe qui concentre a lui seul sprites, primitives, texte, clipping et render targets.
- Le backend CasaEngine dispose d'un point d'extension explicite pour le rendu de primitives 2D.
- Une implementation `Apos.Shapes` peut etre activee sans modifier `MGUI.Core`.
- Le backend CasaEngine dispose d'un point d'extension explicite pour un canvas vectoriel d'editeur.
- Une implementation `NvgSharp` peut etre activee sans detourner `IUIDrawContext`.
- Le rendu UI nominal continue de fonctionner sans `Apos.Shapes` ni `NvgSharp`.
- Les etats GPU sont restaures correctement apres draw UI, clipping et overlays avances.
- Les contrats partages MGUI restent propres : pas de fuite `Apos.Shapes` / `NvgSharp` dans `MGUI.Shared` et `MGUI.Core`.

## References a auditer pendant la migration

- `CasaEngine/Framework/UI/Backend/MonoGame/CasaDesktopRuntime.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/CasaMonoGameRenderInterop.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/Clipping/CasaClipManager.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/CasaBackBufferSurface.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/CasaRenderSurfaceAdapter.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/CasaRenderTargetPool.cs`
- `CasaEngine/Framework/UI/Backend/MonoGame/Assets/CasaMonoGameImageResource.cs`
- `MGUI/MGUI.Shared/Rendering/IUIDrawContext.cs`
- `MGUI/MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI/MGUI.Shared/Rendering/DrawSettings.cs`
- `MGUI/MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs`
- `MGUI/MGUI.MonoGame/Rendering/IMonoGameBackendContracts.cs`
- `MGUI/MGUI.Core/UI/MGDesktop.cs`
- `MGUI/MGUI.Core/UI/MGTextBlock.cs`

---

## ✅ Phase 1 - Cadrer l'architecture cible et poser les garde-fous

- ✅ **T01.01 - Formaliser l'architecture cible par couches**
  Objectif :
  - Decrire noir sur blanc les couches suivantes : orchestration runtime, services de draw, renderer de primitives, canvas vectoriel d'editeur, clipping, surfaces, assets.
  - Figurer explicitement ou vivent `Apos.Shapes` et `NvgSharp`.
  Livrable :
  - note d'architecture courte dans `docs/` ou `ai-agent/rendering/`.
  Validation :
  - build moteur borne.
  Commit conseille :
  - `docs(ui): define extensible MonoGame backend target architecture`

- ✅ **T01.02 - Ajouter des tests d'architecture de frontiere**
  Objectif :
  - Verifier que `MGUI.Shared` et `MGUI.Core` n'introduisent pas de references a `Apos.Shapes` ou `NvgSharp`.
  - Verifier que l'editeur seul peut referencer le canvas vectoriel Nvg quand il sera ajoute.
  Validation :
  - tests cibles d'architecture.
  Commit conseille :
  - `test(ui): add architecture guards for backend extensibility boundaries`

- ✅ **T01.03 - Corriger le modele cible de surface et render target**
  Objectif :
  - Definir le remplacement du `null!` implicite de `CasaBackBufferSurface`.
  - Choisir un modele explicite pour backbuffer vs render target.
  Validation :
  - build moteur borne.
  Commit conseille :
  - `design(ui): define explicit surface target model for MonoGame backend`

- ✅ **T01.04 - Figurer la matrice de validation finale**
  Objectif :
  - Lister les demos, panneaux editeur et tests necessaires pour valider la migration.
  - Figurer quels scenarii couvrent UI nominale, world-space UI, primitives et overlay vectoriel.
  Validation :
  - document mis a jour.
  Commit conseille :
  - `docs(ui): define validation matrix for extensible backend refactor`

---

## ✅ Phase 2 - Eclater le backend monolithique actuel

- ✅ **T02.01 - Extraire la pile d'etat de draw hors de `CasaDrawTransaction`**
  Objectif :
  - Isoler la gestion des `DrawSettings`, transforms, effets, switches de contexte et restoration d'etats GPU.
  - Laisser `CasaDrawTransaction` orchestrer plutot qu'encoder toute la mecanique lui-meme.
  Validation :
  - build moteur + demos.
  - tests cibles sur l'etat de draw.
  Commit conseille :
  - `refactor(ui): extract draw state stack from CasaDrawTransaction`

- ✅ **T02.02 - Extraire les services render target et surfaces**
  Objectif :
  - Sortir de `CasaDrawTransaction` la logique de `SetRenderTarget`, cibles temporaires et restauration.
  - Unifier `CasaBackBufferSurface`, `CasaRenderSurfaceAdapter` et le pool autour d'un descripteur explicite.
  Validation :
  - build moteur + demos.
  Commit conseille :
  - `refactor(ui): extract render target and surface services`

- ✅ **T02.03 - Extraire les executants de clipping par strategie**
  Objectif :
  - Garder `ClipDefinition` / `ClipStrategy` comme contrat logique.
  - Scinder l'execution scissor / stencil / mask en services independants.
  Validation :
  - build moteur + demos.
  - test cible sur le routing des strategies.
  Commit conseille :
  - `refactor(ui): split clip execution by strategy`

- ✅ **T02.04 - Extraire caches et services runtime hors de `CasaDesktopRuntime`**
  Objectif :
  - Sortir les caches de textures utilitaires, services assets et services de texte du runtime principal.
  - Garder `CasaDesktopRuntime` concentre sur frame lifecycle, input, vues et composition root.
  Validation :
  - build moteur + demos.
  Commit conseille :
  - `refactor(ui): split runtime orchestration from backend services`

---

## ✅ Phase 3 - Introduire des points d'extension CasaEngine explicites

- ✅ **T03.01 - Introduire `IShapeRenderer2D` et son fallback actuel**
  Objectif :
  - Definir un contrat CasaEngine pour rectangles, lignes, cercles, polygones, triangles et anneaux.
  - Brancher une implementation fallback basee sur le comportement actuel `SpriteBatch` / `PrimitiveBatch`.
  Validation :
  - build moteur + demos.
  - tests cibles sur le contrat.
  Commit conseille :
  - `feat(ui): add extensible 2d shape renderer contract`

- ✅ **T03.02 - Introduire `IEditorVectorCanvas` / `IVectorCanvasSession`**
  Objectif :
  - Definir un contrat CasaEngine pour path, stroke, fill, save/restore, texte simple et clip vectoriel cote editeur.
  - Garder ce contrat hors de `MGUI.Shared`.
  Validation :
  - build moteur + editeur.
  Commit conseille :
  - `feat(editor-ui): add vector canvas contract for editor overlays`

- ✅ **T03.03 - Introduire un registre explicite d'adaptateurs backend**
  Objectif :
  - Remplacer progressivement la resolution reflexive de textures / render targets par des adaptateurs declares.
  - Rendre l'interop plus robuste pour plusieurs sous-systemes de rendu.
  Validation :
  - build moteur.
  - tests cibles sur les adaptateurs.
  Commit conseille :
  - `refactor(ui): replace reflective image bridging with explicit backend adapters`

- ✅ **T03.04 - Introduire des options de composition du backend**
  Objectif :
  - Permettre au bootstrap CasaEngine de choisir l'implementation des primitives, du texte, du canvas d'editeur et des services de surface.
  - Eviter les dependances rigides encodees dans les constructeurs.
  Validation :
  - build moteur + editeur.
  Commit conseille :
  - `feat(ui): add backend composition options for MonoGame runtime`

---

## 🧪 Phase 4 - Integrer `Apos.Shapes` comme moteur de primitives 2D

- ✅ **T04.01 - Ajouter la dependance et son isolation projet**
  Objectif :
  - Ajouter `Apos.Shapes` uniquement dans le ou les projets necessaires.
  - Documenter pourquoi la dependance vit la et quel fallback reste actif.
  Validation :
  - build moteur + demos.
  Commit conseille :
  - `build(ui): add Apos.Shapes dependency for backend primitives`

- ✅ **T04.02 - Implementer `AposShapeRenderer` avec parite minimale**
  Objectif :
  - Couvrir les primitives requises par MGUI nominal : fill/stroke rectangle, line, circle, triangle, polygon, rounded helpers si necessaire via geometry existante.
  - Garder un fallback pour les cas non encore portes.
  Validation :
  - build moteur + demos.
  - tests cibles de parite primitive.
  Commit conseille :
  - `feat(ui): implement Apos-based primitive renderer`

- ✅ **T04.03 - Router `CasaDrawTransaction` vers `IShapeRenderer2D`**
  Objectif :
  - Faire en sorte que `CasaDrawTransaction` n'ecrive plus directement toutes les primitives sur `PrimitiveBatch`.
  - Garder l'implementation fallback existante derriere le contrat.
  Validation :
  - build moteur + demos.
  Commit conseille :
  - `refactor(ui): route draw transaction primitives through shape renderer`

- 🧪 **T04.04 - Ajouter demos et tests de non-regression Apos**
  Objectif :
  - Verifier visuellement les controles MGUI dependant des primitives.
  - Ajouter des tests de frontiere ou snapshots logiques si possible.
  Validation :
  - build demos.
  - tests cibles.
  Commit conseille :
  - `test(demos): validate Apos primitive renderer integration`

---

## 🧪 Phase 5 - Integrer `NvgSharp` comme canvas vectoriel d'editeur

- ✅ **T05.01 - Ajouter la dependance NvgSharp cote editeur uniquement**
  Objectif :
  - Limiter la dependance `NvgSharp` aux projets editeur concernes.
  - Ne pas la faire remonter dans `CasaEngine` runtime sans justification.
  Validation :
  - build editeur.
  Commit conseille :
  - `build(editor-ui): add NvgSharp dependency for editor vector canvas`

- ✅ **T05.02 - Implementer `NvgSharpVectorCanvas` et le bridge d'etat GPU**
  Objectif :
  - Encapsuler save/restore, paints, paths, clips et texte simple dans une implementation CasaEngine.
  - Garantir la restauration des etats GPU en sortie de pass.
  Validation :
  - build editeur.
  - validation manuelle sur overlay simple.
  Commit conseille :
  - `feat(editor-ui): implement NvgSharp vector canvas backend`

- ✅ **T05.03 - Ajouter un pass d'overlay vectoriel cote shell editeur**
  Objectif :
  - Introduire un pass explicite d'overlay apres ou avant MGUI selon le besoin.
  - Eviter d'utiliser `IUIDrawContext` comme faux canvas vectoriel.
  Validation :
  - build editeur.
  - validation manuelle sur shell.
  Commit conseille :
  - `feat(editor-ui): add vector overlay pass to editor shell`

- 🧪 **T05.04 - Migrer un premier outil visuel d'editeur sur le canvas**
  Objectif :
  - Choisir un outil pilote : marquee, selection overlay, guides, ruler, debug gizmo 2D ou autre overlay simple.
  - Prouver que `NvgSharp` apporte une valeur sans casser MGUI.
  Validation :
  - build editeur.
  - verification manuelle ciblee.
  Commit conseille :
  - `feat(editor-ui): migrate first visual tool to vector canvas`

---

## 🧪 Phase 6 - Finaliser, durcir et documenter

- ✅ **T06.01 - Nettoyer les hypotheses `PrimitiveBatch` encore encodees en dur**
  Objectif :
  - Supprimer les branches qui supposent que toute primitive passe par `PrimitiveBatch`.
  - Conserver uniquement des chemins internes de fallback bien limites.
  Validation :
  - build moteur + editeur + demos.
  Commit conseille :
  - `refactor(ui): remove hard-coded PrimitiveBatch assumptions`

- ✅ **T06.02 - Durcir les tests de restauration d'etats GPU et render target**
  Objectif :
  - Ajouter des tests et smoke validations pour scissor, stencil, render target temporaire et overlays vectoriels.
  Validation :
  - tests cibles.
  - build moteur + editeur.
  Commit conseille :
  - `test(ui): harden gpu state restoration coverage`

- ✅ **T06.03 - Mettre a jour la documentation d'architecture et d'usage**
  Objectif :
  - Documenter le role de `IShapeRenderer2D`, du canvas vectoriel d'editeur et du fallback runtime.
  - Expliquer ou brancher une nouvelle implementation de primitives ou de canvas.
  Validation :
  - documentation presente et coherent build OK.
  Commit conseille :
  - `docs(ui): document extensible MonoGame backend architecture`

- 🧪 **T06.04 - Validation finale du backend extensible**
  Objectif :
  - Executer la matrice finale de validation.
  - Laisser les dernieres taches en `🧪` si une verification manuelle reste requise.
  Validation :
  - build moteur + editeur + demos.
  - tests bornes.
  - demos et shell valides manuellement.
  Commit conseille :
  - `chore(ui): validate extensible MonoGame backend refactor`