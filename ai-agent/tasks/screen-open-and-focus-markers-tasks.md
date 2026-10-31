# Plan agent IA — Commande d'ouverture et marqueurs de focus

Plan d'exécution d'un petit chantier sur `DataDrivenScreen`. Les décisions ci-dessous ont été déléguées par l'auteur le 2026-11-01 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un projet sans DLL peut :

- lancer une liste d'actions à l'ouverture d'un écran, avant sa première image, donc un fondu d'entrée ;
- afficher un élément, un curseur par exemple, seulement quand un autre élément tient le focus.

Hors périmètre : un curseur qui glisse d'une ligne à l'autre, une animation de focus, tout changement de MGUI.

## État vérifié du dépôt (2026-11-01)

- `git log -1` : la branche part de `chantier/screen-fade-actions` `c5ffe054`.
- ADR-0081 liste comme limite « No fade runs when a screen opens » : rien ne lance une commande à l'ouverture, et un écran dessine une image avant son premier `Update`.
- `DataDrivenScreen.OnWindowLoaded` tourne avant l'entrée de l'écran sur la pile (`DataDrivenScreen.cs:116`).
- MGUI n'offre aucun lien de données vers le focus : `HasKeyboardFocus` est une implémentation explicite (`MGElement.cs:2918`) sans notification.
- `MGDesktop.FocusedKeyboardHandler` se lit sans allocation, et `DataDrivenScreen.WatchFocusMove` compare déjà sa valeur d'une image à l'autre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Champ `on_open_command` du `.uiscreen`, qui nomme une entrée de `ui_commands`. Elle démarre à la fin de `OnWindowLoaded`, pas dans `Update`, pour que la première image dessinée montre déjà le fondu. |
| D2 | Cette commande n'accepte que `play_sound`, `fade_screen` et `wait_fade`. Au chargement l'écran n'est pas sur la pile : changer la pile ou le monde depuis cet endroit n'a pas de sens défini. Le reste est refusé au chargement. |
| D3 | Champ `focus_markers` : liste d'objets `{ "element", "marker" }`, noms d'éléments XAML. Le marqueur est visible seulement quand l'élément tient le focus. Le lien est explicite, parce qu'un marqueur peut être un frère de l'élément sur un `Canvas`. |
| D4 | Un marqueur caché garde sa place (`Visibility.Hidden`) : le layout ne bouge pas. |
| D5 | La visibilité change seulement quand le focus change, dans `Update`, sans abonnement et sans allocation. Un écran gelé sous un modal garde son marqueur. |
| D6 | Une erreur de données (nom inconnu, élément qui ne peut pas prendre le focus) est refusée au chargement avec le chemin de l'enveloppe, comme les autres champs. |

## Règles d'exécution pour l'agent

- **Branche `chantier/screen-open-and-focus-markers`**, empilée sur `chantier/screen-fade-actions`. Jamais de commit sur `main`, jamais de push.
- **Un commit par tâche**, plan mis à jour dans le même commit, `git add` fichier par fichier.
- **Langue** : plan en français ; code, commits, `docs/` et ADR en anglais.
- Lancer `dotnet test` sans `--no-build` : les `.sln` ne reconstruisent pas `CasaEngine.Tests`.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe.
- Launcher sur `Projects/PixelUiSample` : fondu d'entrée, curseur visible seulement sur son bouton.

---

## Phase 1 — Format et écran

### ✅ T1.1 — ADR et plan

- Fichiers : ce plan, `ai-agent/README.md`, `docs/decisions/0082-screens-run-a-command-on-open-and-mark-the-focus.md`, `docs/decisions/README.md`.
- Commit : `docs(decisions): record the open command and the focus markers (ADR-0082)`.

### ✅ T1.2 — `on_open_command`

- Fichiers : `UIScreenAsset.cs`, `EditorAssetJsonSerializer.cs`, `DataDrivenScreen.cs`, `UIScreenAssetSerializationTests.cs`, `DataDrivenScreenTests.cs`.
- Validation : tests du format et de l'écran, dont une attente qui coupe les commandes dès l'ouverture.
- Commit : `feat(ui): run a command when a data-driven screen opens`.

### ✅ T1.3 — `focus_markers`

- Fichiers : mêmes fichiers.
- Validation : marqueur caché au chargement, visible avec le focus, refus des noms inconnus.
- Commit : `feat(ui): show a marker while an element holds the focus`.

## Phase 2 — Preuve

### ⏳ T2.1 — Sample et documentation

- Fichiers : `Projects/PixelUiSample`, `docs/engine/data-driven-screens.md`, ADR-0081 (limite levée), ce plan, `ai-agent/README.md`.
- Validation : Launcher, captures lues.
- Commit : `docs(ui): show the open command and the focus markers in the pixel sample`.

## Points ouverts

- Aucun.
