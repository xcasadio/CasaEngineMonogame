# Plan agent IA — Découpe de l'interface MGUI dans l'espace de la vue

Plan d'exécution de la partie moteur de la tranche E19.s2 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2q.1,
dépôt `alundra-casaengine-project-converter`, ligne O-E19-60). **Approuvé : plan parent §1.2q.1 READY (relecture n°2) le 2026-10-05,
travail dans le moteur autorisé par l'auteur le 2026-10-03.** La décision ci-dessous vient du plan parent (D-E19-71, 2026-10-05) :
**ce plan l'applique, il ne la rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Dans une vue qui ne commence pas au coin de la fenêtre (les bandes de la résolution virtuelle d'ADR-0048, un écran partagé), les
rectangles de découpe de l'interface MGUI, calculés en pixels de la vue, sont écrits tels quels dans `GraphicsDevice.ScissorRectangle`,
que MonoGame applique en pixels absolus du tampon, alors que les sprites sont dessinés relativement à l'origine de la vue : chaque
élément est dessiné à `rect + origine` mais découpé à `rect`. De plus, la première découpe de l'interface s'intersecte avec un ciseau
périmé que le périphérique tient après un agrandissement de la fenêtre. Ce chantier décale les découpes par l'origine de la vue à
l'écriture et les ramène à la relecture, et rafraîchit le ciseau du périphérique à chaque redimensionnement de la fenêtre.

## Hors périmètre

- Le monde (les sprites se dessinent sans découpe) ; le rendu hors écran de l'interface (la stratégie Mask n'est jamais choisie, le
  point est consigné au plan parent comme latent, non exercé).
- MGUI (sous-module) : aucun changement ; l'hôte de MGUI et celui de l'éditeur rafraîchissent déjà le ciseau.
- Le jeu et le convertisseur du dépôt parent.

## État vérifié du dépôt (2026-10-05)

- Branche `chantier/e19s2-ui-clip-view-space` créée depuis `chantier/e19g2c-backdrop-stp` `3b05301f`. Modification locale de
  l'auteur dans `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer.
- `CasaDrawTransaction.SetClipTarget` (`CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`) intersecte, compare et écrit
  les rectangles bruts ; `CurrentClipBounds` relit `GraphicsDevice.ScissorRectangle` tel quel ; `PushRectangleClip(null)` rétablit
  `Renderer.GetViewport(0)`.
- `CasaEngineGame.OnWindowClientSizeChanged` n'a pas de rafraîchissement du ciseau ; `CasaGameRenderHost` (éditeur) et l'hôte de MGUI en ont un.
- `CasaEngine.Tests` n'a pas de harnais GPU ; `MGUI.Tests/Integration/GpuDeviceHost.cs` en a un (`[GpuFact]` saute sans GPU).
- ADR suivante libre : 0054.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| S2-R1 | `CasaDrawTransaction` décale chaque rectangle de découpe par l'origine de la vue courante (`GraphicsDevice.Viewport.X/Y`) quand il l'écrit dans le périphérique (avant l'intersection et la comparaison, pour le repli et pour `PushRectangleClip(null)`), et le ramène dans l'espace de la vue à la relecture (`CurrentClipBounds`) ; la sauvegarde et la remise des valeurs brutes du périphérique ne changent pas ; une vue à l'origine ne change pas. |
| S2-R2 | Un seul propriétaire : `CasaEngineGame.OnWindowClientSizeChanged`, dont la première instruction appelle `UiDeviceScissor.ResetToBackBuffer(GraphicsDevice)` (interne, statique, espace de noms du backend MonoGame de l'interface) ; elle pose sans condition le ciseau à `(0, 0, BackBufferWidth, BackBufferHeight)`, avant le retour anticipé sans résolution virtuelle. |
| S2-R3 | ADR du moteur (0054, amende la partie interface d'ADR-0048 par une note de statut) ; la doc de la résolution virtuelle dit que les découpes de l'interface sont locales à la vue. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19s2-ui-clip-view-space`**, créée depuis `chantier/e19g2c-backdrop-stp` `3b05301f`. Jamais de commit sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté, puis vert.
- **Un commit par tâche**, message en anglais `type(area): summary`, chaque commit vert. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Un test existant qui change ou rougit est un arrêt (liste fermée : aucun) ; une valeur écrite d'avance que la mesure contredit aussi.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de matériaux est connu pour être instable).
- Démo en écran partagé lancée depuis `CasaEngine.Demos/` : la sonde du back-buffer lit l'élément d'interface de la vue décalée.
- Recette en jeu : par le plan parent (E19.s2, S2-5).

---

## Phase 1 — Moteur

### ✅ T1.1 — Harnais GPU, tests T-S2-1 à T-S2-4 et code (S2-1, S2-2)

- Objectif : un harnais sur vrai GPU porté de `MGUI.Tests` ; quatre tests écrits d'avance, rouges sur `3b05301f`, puis le code de S2-R1 et S2-R2.
- Fichiers : `CasaEngine.Tests/UI/Backend/GpuDeviceHost.cs`, `CasaEngine.Tests/UI/Backend/UiClipViewSpaceGpuTests.cs`, `CasaEngine/Framework/UI/Backend/MonoGame/UiDeviceScissor.cs`, `CasaDrawTransaction.cs`, `CasaEngineGame.cs`.
- Étapes : voir les valeurs du plan parent ; T-S2-1 et T-S2-4 rouges sur le code d'avant ; T-S2-2 rouge avec S2-R1 seul et l'utilitaire sans effet, verte avec S2-R2 ; T-S2-3 verte avant et après.
- Validation faite (2026-10-05, les tests ont tourné sur le GPU de la machine, aucun n'a sauté) : rouges d'abord sur le code de `3b05301f` (l'utilitaire `UiDeviceScissor.ResetToBackBuffer` posé sans effet, pour que le projet compile) : T-S2-1 le pixel absolu (80, 48) lit (100, 149, 237) (la couleur de fond) au lieu de rouge ; T-S2-4 le ciseau du périphérique lit (40, 80, 840, 240) au lieu de (561, 126, 840, 240) ; T-S2-3 (témoin) vert. S2-R1 posé, l'utilitaire toujours sans effet : T-S2-1, T-S2-4 et le témoin verts ; T-S2-2 rouge, le pixel (100, 80) lit (100, 149, 237) comme prédit et le ciseau après l'appel lit (0, 0, 96, 64) au lieu de (0, 0, 256, 192). Utilitaire réel et appel en première instruction de `OnWindowClientSizeChanged` : 4 tests sur 4 verts. `CasaEngine.Tests` 2566 sur 2566 (Debug, aucun saut). Aucun test existant touché. Un seul commit pour les tâches S2-1 (tests) et S2-2 (code) du plan parent : chaque commit doit rester vert.
- Commit : `fix(ui): MGUI clip rectangles in view space and a fresh device scissor on resize (E19.s2)`

### ⏳ T1.2 — Démo en écran partagé avec une interface dans la vue décalée (S2-1)

- Objectif : la démo en écran partagé reçoit un élément d'interface (écran XAML) dans la vue de droite ; sonde du back-buffer en processus.
- Fichiers : `CasaEngine.Demos/Demos/SplitScreenDemo.cs`, `CasaEngine.Demos/Content/Screens/split-screen-offset-view.xaml`, `CasaEngine.Demos/CasaEngine.Demos.csproj` si besoin.
- Validation : démo lancée depuis `CasaEngine.Demos/`, sonde rouge sur le code d'avant (l'élément manque), verte après.
- Commit : `feat(demos): split-screen demo with a UI element in the offset view (E19.s2)`

### ⏳ T1.3 — ADR et documentation (S2-R3)

- Objectif : ADR-0054 (`Accepted`) et sa ligne à l'index, note de statut sur ADR-0048, doc de la résolution virtuelle.
- Fichiers : `docs/decisions/0054-*.md`, `docs/decisions/README.md`, `docs/decisions/0048-*.md`, `docs/engine/rendering-2d-3d-spaces.md`.
- Commit : `docs(adr): ADR-0054 MGUI clip rectangles are local to the view`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
