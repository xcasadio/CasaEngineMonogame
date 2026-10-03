# Plan agent IA — Résolution virtuelle à facteur entier et bandes noires

Plan d'exécution de la partie moteur de la tranche E19.s du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2q,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2q READY le 2026-10-03, travail dans le moteur
autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous ont été arbitrées avec l'auteur le 2026-10-03 (D-E19-47,
D-E19-60 du plan parent) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un projet peut déclarer une **résolution virtuelle** (largeur, hauteur) à mode d'échelle `IntegerFit`. Le moteur calcule alors,
hors éditeur, le plus grand facteur entier `k` qui tient dans la fenêtre, centre l'image `largeur·k × hauteur·k`, y place la vue
unique et sa caméra 2D (zoom `k`, viewport de la taille du rectangle), efface le reste de la fenêtre en noir, et recalcule tout
quand la fenêtre change de taille, en temps réel. Les écrans XAML sont prévenus quand les bornes de leur bureau changent.

## Hors périmètre

- Le mode d'échelle exact (non entier) et un mode `Fill`/`Stretch` : seul `IntegerFit` est livré.
- `BackBufferPresenter` / `PresentMode` : non branchés, non touchés.
- L'éditeur, les démos et les projets sans le réglage : comportement inchangé.
- Toute règle propre à Alundra (elle vit dans le jeu et le convertisseur).

## État vérifié du dépôt (2026-10-03)

- Branche `chantier/e19s-virtual-resolution` créée depuis `main` (`22228ffd`). Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer.
- `CasaEngineGame.OnScreenResized` (`CasaEngine/Framework/Application/CasaEngineGame.cs`) n'est atteint que par `DeviceReset` et
  `ApplyDisplaySettings` ; en DesktopGL un redimensionnement ne lève que `Window.ClientSizeChanged` (déduit du code de
  MonoGame 3.8.5.1 décompilé, non lancé). La vue unique prend `ViewportRect = (0, 0, w, h)` et sa caméra `OnScreenResized(w, h)`.
- `DefaultRuntimeViewBootstrapper` crée la vue plein écran à la taille de la fenêtre ; la caméra 2D projette
  `viewport / Zoom` (`Camera2dComponent.ComputeProjectionMatrix`) ; `CameraComponent.OnScreenResized` pose le viewport de la caméra.
- `XamlUIScreenBase` n'a que `OnWindowLoaded` ; `ScreenStack.Update` gèle les écrans sous un modal ; `MGDesktop.ValidScreenBounds`
  est local à la vue (`(0, 0, ViewportRect.W, H)`).
- Aucune ADR moteur ne couvre les viewports ni les bandes ; la dernière est ADR-0047, la suivante ADR-0048.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| S-R2 | Réglage de projet `VirtualResolution` (largeur, hauteur, mode `IntegerFit`, bandes noires). Absent : comportement d'aujourd'hui, inchangé. |
| S-R3 | `k = max(1, floor(min(L / largeur, H / hauteur)))` ; rectangle `largeur·k × hauteur·k` centré (décalages `floor((L − largeur·k) / 2)` et `floor((H − hauteur·k) / 2)`), rogné par la fenêtre. |
| S-R4 | Hors `UseExternalViewManagement` : abonnement à `Window.ClientSizeChanged` ; la vue unique prend le rectangle, sa caméra 2D `Zoom = k` et un viewport de la taille du rectangle rogné, posé après `World.OnScreenResized` ; même calcul à la création de la vue ; le back-buffer entier est effacé en noir avant les vues quand le rectangle ne couvre pas la fenêtre. |
| S-R5 | `XamlUIScreenBase` gagne un rappel de changement des bornes de l'écran, appelé quand les bornes valides du bureau de la vue changent. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19s-virtual-resolution`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté, puis vert.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Recette en jeu : par le plan parent (E19.s, recette S6).

---

## Phase 1 — Moteur

### ✅ T0.1 — Ce plan

- Objectif : le plan, la ligne de l'index.
- Commit : `docs(plan): plan the virtual resolution with integer fit and black bands (E19.s)`

### ✅ T1.1 — Fonction de mise en page

- Objectif : `VirtualResolutionLayout.Compute(L, H, largeur, hauteur)` pure, rend le facteur `k` et le rectangle rogné.
- Fichiers : `CasaEngine/Framework/Rendering/VirtualResolutionLayout.cs`, `CasaEngine.Tests/Rendering/VirtualResolutionLayoutTests.cs`.
- Validation : la table de S-R3 telle quelle (neuf lignes, 320 × 240) plus une autre résolution virtuelle.
- Validation faite : rouge d'abord (talon qui rend 0 : 19 tests sur 22 rouges, par exemple 1920 × 1080 → échelle 0 au lieu de 4), puis vert, 22 sur 22. Une résolution virtuelle non positive lève `ArgumentOutOfRangeException` ; une fenêtre vide rend l'échelle 1 et un rectangle vide.
- Commit : `feat(rendering): pure integer-fit layout for a virtual resolution (E19.s)`

### ⏳ T1.2 — Réglage de projet

- Objectif : `ProjectSettings.VirtualResolution` (nul = absent), lu et écrit par `ProjectSettingsHelper`.
- Fichiers : `ProjectSettings.cs`, `VirtualResolutionSettings.cs`, `ProjectSettingsHelper.cs`, tests.
- Validation : lecture, écriture, aller-retour, absent = nul (même après un projet qui l'avait), valeurs invalides rejetées.
- Commit : `feat(project): virtual resolution project setting (E19.s)`

### ⏳ T1.3 — Application à la vue, au redimensionnement et aux bandes

- Objectif : création de la vue, `OnScreenResized`, abonnement à `ClientSizeChanged`, effacement des bandes.
- Fichiers : `VirtualResolutionLayout.cs` (application à une surface et une caméra), `DefaultRuntimeViewBootstrapper.cs`,
  `CasaEngineGame.cs`, tests.
- Validation : fenêtre 1920 × 1080, `k` 4 → rectangle (320, 60, 1280, 960), viewport de la caméra 1280 × 960, aire visible
  320 × 240, à la création comme après un redimensionnement ; 400 × 200 → viewport 320 × 200 ; réglage absent = inerte ; prédicat des bandes.
- Commit : `feat(runtime): apply the virtual resolution to the view, its camera and the bands (E19.s)`

### ⏳ T1.4 — Rappel des bornes pour les écrans XAML

- Objectif : `XamlUIScreenBase.OnScreenBoundsChanged`, appelé par `UIRoot` via `ScreenStack`, y compris sous un modal.
- Fichiers : `XamlUIScreenBase.cs`, `ScreenStack.cs`, `UIRoot.cs`, tests.
- Validation : appelé quand les bornes changent, jamais quand elles sont identiques, jamais avant la construction de la fenêtre.
- Commit : `feat(ui): screen bounds changed callback on XAML screens (E19.s)`

### ⏳ T2.1 — ADR et documentation

- Objectif : ADR-0048 (`Accepted`), `docs/engine/rendering-2d-3d-spaces.md`, index des ADR.
- Commit : `docs(adr): ADR-0048 virtual resolution with integer fit and black bands`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
