# Plan agent IA - Visualisation editeur des LightComponent

## Contexte

Objectif : dans le viewport editeur, toute entite qui contient un `LightComponent` doit afficher une icone billboard. L'icone depend du type de lumiere :

- `LightType.Spot` -> `CasaEngine.Editor\Content\icons\png-white\cone.png`
- `LightType.Point` -> `CasaEngine.Editor\Content\icons\png-white\lightbulb.png`
- `LightType.Directional` -> `CasaEngine.Editor\Content\icons\png-white\sun.png`

Quand l'entite est selectionnee, ou quand le `LightComponent` lui-meme est selectionne, l'editeur doit afficher une aide visuelle supplementaire :

- `Point` : icone ampoule + sphere filaire de `Range`
- `Spot` : icone cone + cone filaire oriente avec `OuterConeAngleDegrees` et `Range`
- `Directional` : icone soleil + 3 fleches paralleles orientees selon la direction du composant

Cette feature est strictement editeur. Elle ne doit pas modifier le rendu runtime, la serialisation des worlds, ni le comportement d'eclairage deja branche dans `LightingContext`.

## Regles obligatoires pour l'agent

1. Traiter une seule tache a la fois.
2. Avant de commencer une tache, remplacer son statut `⏳ Todo` par `🚧 In progress`.
3. Quand la tache est terminee, validee et committee, remplacer `🚧 In progress` par `✅ Done`.
4. Si le code est termine mais qu'une verification manque encore, utiliser `🧪 Needs testing` et noter la verification manquante.
5. Si une tache est bloquee, utiliser `⚠️ Blocked` et ajouter une note courte sous la tache.
6. Le statut doit rester en face du nom de la tache, dans le titre de la tache.
7. Faire exactement un commit compilable par tache atomique.
8. Mettre a jour ce fichier dans le meme commit que le code de la tache.
9. La tache ne peut passer en `✅ Done` que si le commit realise est renseigne.
10. Ne pas regrouper plusieurs taches de ce plan dans un meme commit.
11. Langue du document : francais. Langue du code : anglais.
12. Aucun nouveau code WPF.
13. Ne pas ajouter de dependance lourde.
14. Ne pas ajouter de LINQ, closures ou allocations evitables dans `Update`, `Draw` ou les passes d'overlay.
15. Tout rendu `SpriteBatch`, line-list ou changement d'etat GPU doit restaurer le `GraphicsDevice` avec `GraphicsStateGuard` ou un guard equivalent.
16. Ne jamais remplacer une action d'overlay existante sans la chainer explicitement si une autre feature l'utilise deja.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Validation minimale par tache

- Tache editor : `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`
- Tache tests : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter FullyQualifiedName~LightOverlay`
- Fin de plan : build editor + test cible si ajoute + smoke test manuel dans le viewport editeur avec les 3 types de lumieres

## Etat observe

- `LightComponent` existe deja avec `LightType.Directional`, `LightType.Point`, `LightType.Spot`, `Range`, `InnerConeAngleDegrees`, `OuterConeAngleDegrees`, `Direction`, `Position` et `Color`.
- `LightComponentEditor` existe deja et expose les proprietes utiles dans l'inspector.
- `EditorSelection.Current` expose deja `SelectedEntity` et `SelectedComponent`.
- `EntityDetailsPanel` emet deja `SelectedComponentChanged`.
- `WorldViewportPanel` route deja la selection d'entite vers le gizmo, mais ne route pas encore explicitement le composant selectionne vers un overlay de lumiere.
- `OverlayViewPipeline` a deja des etapes `RenderGizmosAction`, `RenderVectorOverlayAction`, `RenderSelectionOutlineAction` et `RenderUIOverlayAction`.
- `WorldViewportPanel.EnsureEditorOverlays` installe deja la grille et l'axe, mais ne branche pas encore `RenderVectorOverlayAction` ni `RenderUIOverlayAction` pour les lumieres.
- `EditorIcons` charge deja `Lightbulb`, mais n'expose pas encore `Cone` ni `Sun`.
- `cone.png`, `lightbulb.png` et `sun.png` existent dans `CasaEngine.Editor\Content\icons\png-white`.
- `Content.mgcb` reference deja `lightbulb.png`, mais pas encore `cone.png` ni `sun.png`.
- `GraphicsStateGuard` existe deja et doit etre utilise pour eviter les leaks d'etat GPU.

## Architecture cible

### Ressources

- Ajouter `EditorIcons.Cone` et `EditorIcons.Sun`.
- Ajouter les entrees Content Pipeline pour `icons/png-white/cone.png` et `icons/png-white/sun.png`.
- Garder `EditorIcons.Lightbulb` pour `Point`.
- Prevoir un fallback visuel discret si une texture manque, sans exception pendant le draw.

### Collecte

- Introduire une petite couche editor-only, par exemple sous `CasaEngine.Editor/Runtime/Overlays/`.
- Collecter les `LightComponent` du `World` actif dans une liste reutilisee.
- Ne pas exposer cette collecte au runtime normal.
- Associer chaque entree collectee a son `Owner`, son `LightComponent`, son `LightType`, sa position, sa direction, sa range, ses angles et son statut selectionne.

### Rendu billboard

- Dessiner une icone pour chaque `LightComponent` visible.
- Positionner l'icone via projection camera (`Viewport.Project` ou equivalent) depuis la position monde du composant.
- Garder une taille constante en pixels, par exemple 22 a 28 px, independante de la distance.
- Ignorer les lumieres derriere la camera ou hors viewport.
- Dessiner les icones apres le monde et les gizmos, mais avant la composition MGUI finale.
- Ne pas faire de hit-test sur les icones dans ce plan, sauf si une tache future le demande explicitement.

### Rendu selectionne

- Si une entite est selectionnee, afficher les helpers filaires de ses `LightComponent`.
- Si le composant selectionne est un `LightComponent`, afficher en priorite le helper de ce composant.
- Si une entite contient plusieurs `LightComponent`, ne jamais supposer qu'il n'y en a qu'un.
- `Point` : sphere filaire centree sur `LightComponent.Position`, rayon `Range`.
- `Spot` : cone filaire partant de `Position`, oriente selon `Direction`, longueur `Range`, rayon de base `Range * tan(OuterConeAngleRadians)`.
- `Directional` : 3 fleches paralleles, espacees perpendiculairement a `Direction`, longueur constante editeur, orientees dans le sens de la lumiere.

## Critères d'acceptation globaux

- Une entite avec `LightComponent` affiche toujours le billboard correct dans le viewport editeur.
- Changer `LightComponent.Type` dans l'inspector change l'icone sans redemarrer l'editeur.
- Selectionner l'entite affiche le helper filaire attendu pour chaque lumiere de cette entite.
- Selectionner directement un `LightComponent` affiche le helper de cette lumiere, meme si l'entite possede plusieurs composants.
- Les helpers suivent les changements de `Range`, d'angles de spot, de position et d'orientation.
- Les overlays ne cassent pas le gizmo transform, la grille, l'axe ni la composition MGUI.
- Aucune allocation evitable n'est ajoutee dans les passes de draw.
- L'etat GPU est restaure apres le rendu billboard et apres le rendu filaire.

## Taches

### ✅ Done - LIGHTVIS-001 - Charger les icones de lumieres editeur

Objectif : rendre les trois textures disponibles via `EditorIcons` et le Content Pipeline.

Fichiers probables :

- `CasaEngine.Editor/EditorIcons.cs`
- `CasaEngine.Editor/Content/Content.mgcb`
- eventuels fichiers generes par le Content Pipeline uniquement si le build les produit deja dans le workflow du repo

A faire :

1. Ajouter `EditorIcons.Cone` et `EditorIcons.Sun`.
2. Charger `Prefix + "cone"` et `Prefix + "sun"` dans `EditorIcons.Load`.
3. Verifier que `Lightbulb` reste charge via `Prefix + "lightbulb"`.
4. Ajouter les entrees `.mgcb` manquantes pour `icons/png-white/cone.png` et `icons/png-white/sun.png`.
5. Ne pas modifier les icones source PNG.

Critères d'acceptation :

- Les trois proprietes `EditorIcons.Lightbulb`, `EditorIcons.Cone` et `EditorIcons.Sun` existent.
- Le build editeur peut charger les trois textures par nom Content Pipeline.
- Aucun fallback runtime n'est necessaire pour ces trois icones dans un build normal.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`

Commit attendu :

- `feat(editor): load light viewport icons`

Commit realise :

- `02afbc11` `feat(editor): load light viewport icons`

---

### ✅ Done - LIGHTVIS-002 - Collecter les LightComponent visibles pour l'overlay

Objectif : fournir au viewport editeur une liste stable et reusable des lumieres a dessiner, sans encore dessiner les icones.

Fichiers probables :

- nouveau `CasaEngine.Editor/Runtime/Overlays/EditorLightOverlayCollector.cs`
- nouveau `CasaEngine.Editor/Runtime/Overlays/EditorLightOverlayItem.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- eventuels tests sous `CasaEngine.Tests/Editor/` si la collecte reste testable sans `GraphicsDevice`

A faire :

1. Parcourir le `World` actif et ses entites pour trouver tous les `LightComponent` actifs.
2. Reutiliser une liste interne entre deux frames avec `Clear()`.
3. Eviter LINQ et allocations temporaires dans la collecte appelee par le draw.
4. Capturer les donnees necessaires au rendu : owner, composant, type, position, direction, range, angles, couleur.
5. Calculer le statut selectionne a partir de l'entite selectionnee et du composant selectionne.
6. Gerer plusieurs `LightComponent` sur la meme entite.
7. Ne pas faire dependre `CasaEngine` runtime de cette classe editor-only.

Critères d'acceptation :

- Le collecteur retourne une entree par `LightComponent`.
- Les entrees selectionnees sont correctes pour selection entite et selection composant.
- Une entite sans `LightComponent` ne produit aucune entree.
- La collecte ne cree pas de liste ou tableau par frame.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`
- Si test ajoute : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter FullyQualifiedName~LightOverlay`

Commit attendu :

- `feat(editor): collect light overlay items`

Commit realise :

- `b5da2bb4` `feat(editor): collect light overlay items`

---

### ✅ Done - LIGHTVIS-003 - Dessiner les billboards d'icones de lumieres

Objectif : afficher une icone billboard pour chaque `LightComponent` collecte.

Fichiers probables :

- nouveau `CasaEngine.Editor/Runtime/Overlays/EditorLightBillboardOverlayRenderer.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine.Editor/EditorIcons.cs`

A faire :

1. Creer un renderer editor-only qui possede ou recoit un `SpriteBatch` cree une seule fois.
2. Brancher le renderer dans `OverlayViewPipeline.RenderUIOverlayAction` ou une etape equivalente executee avant la composition MGUI.
3. Chainer toute action `RenderUIOverlayAction` deja presente au lieu de l'ecraser silencieusement.
4. Choisir l'icone via un mapping strict : `Spot` -> `Cone`, `Point` -> `Lightbulb`, `Directional` -> `Sun`.
5. Projeter la position monde vers le viewport courant.
6. Ignorer les points derriere la camera, hors viewport ou sans texture disponible.
7. Dessiner les icones centre-align, taille constante en pixels.
8. Encadrer le `SpriteBatch.Begin/End` avec restauration d'etat GPU.
9. Disposer proprement les ressources creees par le renderer dans `WorldViewportPanel.Dispose`.

Critères d'acceptation :

- Les lumieres point affichent une ampoule.
- Les lumieres spot affichent un cone.
- Les lumieres directionnelles affichent un soleil.
- Les icones restent a taille lisible quand la camera bouge ou zoome.
- Les icones ne masquent pas ou ne cassent pas les panels MGUI.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`
- Smoke manuel : ouvrir un world avec les 3 types de lumieres et verifier les 3 icones dans le viewport.

Commit attendu :

- `feat(editor): render light component billboards`

Commit realise :

- `849d2ded` `feat(editor): render light component billboards`

---

### ✅ Done - LIGHTVIS-004 - Dessiner les helpers filaires des lumieres selectionnees

Objectif : afficher les volumes et directions des lumieres quand l'entite ou le `LightComponent` est selectionne.

Fichiers probables :

- nouveau `CasaEngine.Editor/Runtime/Overlays/EditorLightWireOverlayRenderer.cs`
- eventuel helper `CasaEngine.Editor/Runtime/Overlays/EditorLineOverlayMesh.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine/Framework/Rendering/GraphicsStateGuard.cs` uniquement en usage, pas en modification attendue

A faire :

1. Creer un renderer line-list 3D base sur `DebugPrimitiveColor` ou l'effect debug deja utilise par le gizmo.
2. Precalculer les vertices unitaires reutilisables : sphere filaire, cercle de base du cone, segments de fleches.
3. Dessiner la sphere point-light avec rayon `Range`.
4. Dessiner le cone spot avec longueur `Range` et rayon de base `Range * tan(OuterConeAngleRadians)`.
5. Orienter le cone selon `LightComponent.Direction`.
6. Dessiner 3 fleches paralleles pour directional, orientees selon `LightComponent.Direction`.
7. Utiliser une couleur lisible, idealement derivee de `LightComponent.Color` avec une valeur minimale de luminosite.
8. Ignorer proprement `Range <= 0` pour point et spot.
9. Eviter les allocations dans le draw : buffers reutilises, pas de creation de tableau par lumiere.
10. Restaurer `BlendState`, `DepthStencilState`, `RasterizerState`, viewport et scissor apres dessin.

Critères d'acceptation :

- Selectionner une point light affiche une sphere filaire a la bonne echelle.
- Selectionner une spot light affiche un cone filaire oriente et dimensionne correctement.
- Selectionner une directional light affiche 3 fleches paralleles orientees.
- Les helpers suivent les modifications d'inspector sans redemarrage.
- Les helpers n'interferent pas avec le transform gizmo.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`
- Smoke manuel : modifier `Range`, `Outer Cone`, position et rotation, puis verifier que les helpers suivent.

Commit attendu :

- `feat(editor): draw selected light wire overlays`

Commit realise :

- `6e2a3ca3` `feat(editor): draw selected light wire overlays`

---

### ✅ Done - LIGHTVIS-005 - Router la selection composant vers l'overlay du viewport

Objectif : garantir que le viewport distingue selection d'entite et selection directe du `LightComponent`.

Fichiers probables :

- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine.Editor/EditorSelection.cs` en usage, modification seulement si necessaire
- `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` en usage, modification seulement si necessaire

A faire :

1. Ajouter au viewport une API explicite pour connaitre le composant selectionne, par exemple `SetSelectedComponent(EntityComponent? component)` ou une methode de selection overlay dediee.
2. Appeler cette API depuis les chemins existants de selection monde.
3. Garder le gizmo transform base sur l'entite ou le `SceneComponent` selectionne comme aujourd'hui.
4. Pour une selection d'entite, marquer tous les `LightComponent` de l'entite comme selectionnes pour les helpers.
5. Pour une selection directe de `LightComponent`, marquer ce composant en priorite, meme si l'entite possede plusieurs lumieres.
6. Verifier que selectionner un composant non-light ne casse pas le viewport et garde au minimum les billboards visibles.

Critères d'acceptation :

- Cliquer une entite dans la hierarchy affiche les helpers de ses lumieres.
- Cliquer un `LightComponent` dans l'inspector affiche le helper de ce composant.
- Une entite avec plusieurs lumieres ne selectionne pas arbitrairement la premiere lumiere.
- Le transform gizmo garde son comportement existant.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`
- Smoke manuel : selection entite, selection composant light, selection composant non-light.

Commit attendu :

- `feat(editor): route light overlay selection state`

Commit realise :

- `269841b0` `feat(editor): route light overlay selection state`

---

### ✅ Done - LIGHTVIS-006 - Ajouter tests et garde-fous de regression

Note validation : les tests automatises et le build editeur sont OK. Le smoke test visuel dans l'editeur a ete valide manuellement dans cette session.

Checklist manuelle consolidee : `ai-agent/audits/editor-final-smoke-checklist.md`, section `Validation 2 - Overlays LightComponent`.

Objectif : verrouiller les points fragiles sans dependre d'un rendu GPU complet dans les tests unitaires.

Fichiers probables :

- `CasaEngine.Tests/UI/CasaMguiBackendOwnershipTests.cs` ou nouveau fichier de tests editor statiques
- eventuels tests de collecte si le collecteur est decouple du `GraphicsDevice`
- `ai-agent/tasks/archive/light-component-editor-visualization-tasks.md`

A faire :

1. Ajouter un test statique qui verifie que `EditorIcons` expose et charge `Lightbulb`, `Cone` et `Sun`.
2. Ajouter un test statique qui verifie que `Content.mgcb` contient les entrees `icons/png-white/cone.png`, `icons/png-white/lightbulb.png` et `icons/png-white/sun.png`.
3. Ajouter un test statique ou un test pur collecteur qui couvre le mapping `LightType` -> icone attendue.
4. Si possible, tester la logique de selection : entite selectionnee, composant light selectionne, composant non-light selectionne.
5. Documenter le smoke test manuel final dans ce fichier avec le resultat observe.

Critères d'acceptation :

- Les tests ciblés passent.
- Le build editeur passe.
- Le smoke manuel confirme les 3 billboards et les 3 helpers selectionnes.
- Le fichier de plan contient les hashes de commit de toutes les taches terminees.

Validation :

- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter FullyQualifiedName~LightOverlay`
- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0`

Commit attendu :

- `test(editor): cover light viewport overlays`

Commit realise :

- `a renseigner apres commit final`

## Smoke test manuel final

A realiser apres `LIGHTVIS-006` :

1. Ouvrir l'editeur sur un projet de test.
2. Creer ou charger un world avec trois entites, chacune avec un `LightComponent` : `Point`, `Spot`, `Directional`.
3. Verifier que les billboards affichent respectivement ampoule, cone et soleil.
4. Selectionner l'entite point light et verifier la sphere filaire de range.
5. Selectionner le composant spot light et verifier le cone filaire oriente.
6. Selectionner l'entite directional light et verifier les 3 fleches paralleles orientees.
7. Modifier `Range`, `Outer Cone`, position et rotation, puis verifier que les overlays se mettent a jour.
8. Verifier que la grille, l'axe, le transform gizmo et les panels MGUI restent fonctionnels.

Resultat smoke test :

- Tests automatises : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter FullyQualifiedName~LightOverlay` OK, 5 tests passes.
- Build editeur : `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -p:WarningLevel=0` OK.
- Smoke visuel editeur : OK valide manuellement dans cette session sur les overlays `Point`, `Spot` et `Directional`.