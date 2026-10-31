# Plan agent IA — Navigation pilotée par les données (E3)

Plan d'exécution de E3, suite de [data-driven-screen-flow-tasks.md](archive/data-driven-screen-flow-tasks.md) (E1) et de [pixel-ui-screens-tasks.md](pixel-ui-screens-tasks.md) (E2), sans analyse dédiée : l'étude préalable vient d'un projet externe consommateur du moteur et chaque point y a été vérifié dans le code avant d'entrer ici.
Les décisions D1 → D9 ci-dessous ont été déléguées puis arbitrées par l'auteur le 2026-10-31 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un projet sans DLL obtient, par ses seules données : le bouclage du focus aux bords, une répétition clavier et manette réglable, des touches confirmer et annuler configurables, un son au déplacement du focus. MGUI ne change pas.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-31)

- Branche `chantier/data-driven-navigation` créée depuis `chantier/pixel-ui-screens` à `e60fb10a`, arbre propre. `MGUI` à `3db3c769`, `develop`, inchangé.
- Dernier ADR : 0079 (`docs/decisions/`).
- `TryHandleInputAction` est public, mappe l'action, la dispatche et applique le focus en file (`MGDesktop.cs:640-651`). `UseRawNavigationInput` à faux coupe le clavier et la manette bruts (`MGDesktop.cs:715,1439,1580`).
- `InputAction` et `IsUIAction` existent (`MGUI.Shared/Input/Semantic/InputAction.cs:5-46`). `TryHandleInputAction` lit `Source` et `Key` du contexte, pas `GamePadButton` (`MGDesktop.cs:647-648`).
- Le service protège les touches réservées d'une zone de texte quand `Key` est renseigné (`UIFocusNavigationService.cs:232-244`), mais une touche quelconque liée par une mapping n'en fait pas partie : `Keys.X` tapé dans une zone de texte serait aussi un Cancel (voir C5).
- `Cancel` n'a pas de repli : `MGButton` ne traite que Submit (`MGButton.cs:374-375`), `TryPerformFallbackNavigation` retourne false (`UIFocusNavigationService.cs:351-361`). Le retour false signale donc un Cancel que rien n'a consommé.
- `NavigateTo` essaie d'abord `NavigationNeighbors` si le voisin est une cible valide (`UIFocusNavigationService.cs:176-180`), sinon la géométrie, et retourne false au bord (`:185-194`). `NavigationNeighbors` est public (`MGElement.cs:2999`).
- `GetFocusableElements` a pour racine la portée de focus active, sinon la fenêtre du focus (`UIFocusNavigationService.cs:95-96,419-427`), triée par `TabIndex`, `Top`, `Left` (`:449-455`).
- `Focus` met en file (`MGElement.cs:2822-2828`) : la demande s'applique dans le `Desktop.Update` suivant.
- `IsRepeatDue` est public et statique (`KeyboardTracker.cs:335-343`). `GamePadTracker` lit `PlayerIndex.One` par défaut, `CurrentState` est public (`GamePadTracker.cs:42,50`).
- `GamePadState.IsButtonDown(0)` : `KeyButton` avec `Buttons.None` serait toujours vrai, d'où le contrôle de C3. Déduit de la sémantique du masque, source MonoGame non lue.
- `ButtonsMapping.Load` lève sur un champ manquant, `AssetLoader` l'avale en log (`AssetLoader.cs:18-23`), `LoadNew` lève alors « IAssetLoader can't load » (`AssetContentManager.cs:233`).
- `InferAssetType` donne `buttonsmapping` pour un `.buttonsMapping` (`AssetInfo.cs:98-106`) et le catalogue l'applique à une entrée sans `asset_type` (`AssetCatalog.cs:248-250`).
- **Ordre mapping et vues, point à vérifier demandé** : `ViewAdded` est abonné à `CasaEngineGame.cs:440`, les chargeurs enregistrés à `:490`, et une vue naît de `DefaultRuntimeViewBootstrapper` (`:67`), appelé par `GameManager.UpdateWorld` (`GameManager.cs:143`), ou d'une démo (`viewManager.Add`, relevé par `rg "viewManager\.(CreateView|Add)\b"` sur `CasaEngine`, `CasaEngine.Demos` et `CasaEngine.Launcher`). Rien entre `:440` et `:506` n'ajoute de vue. La mapping se charge donc juste après le mixer (`:493`), avant `base.Initialize()` (`:506`), et avant toute création d'un `UIRoot` par `ViewAdded`.
- `new DataDrivenScreen` n'existe que dans `GameManager` et les tests : l'éditeur n'en construit aucun.
- `MGListBox` ne lit pas les flèches dans son `KeyboardHandler` (`MGListBox.cs:1932-1957`) : pas de double déplacement sur le chemin sémantique.
- `HeadlessUiTestHarness` donne un `MGDesktop` sans device ; la manette n'y est pas injectable (`GamePadTracker.cs:100`), d'où D1.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Nouvelle classe `CasaEngine/Framework/UI/UINavigationInput.cs`, qui prend un `MGDesktop` et une `ButtonsMapping`, testable avec `HeadlessUiTestHarness`. Elle pose `UseRawNavigationInput` à faux et appelle `TryHandleInputAction` sur les fronts d'entrée. Sa logique reçoit des états de boutons injectés. |
| D2 | Nouveau champ projet optionnel `UIInputMapping`, id ou nom d'un `.buttonsMapping`. Absent, rien ne change. Sous `UseExternalViewManagement`, sans effet. `UIRoot` crée et appelle la classe entre `Desktop.Update` et la mise à jour de la pile. |
| D3 | Les noms d'entrée sont ceux de l'enum `InputAction`. Les autres sont ignorés, une mapping peut servir aussi au gameplay. Une entrée analogique sur une action UI est refusée au chargement. Submit, Cancel et les quatre directions sont obligatoires. |
| D4 | Les touches que la mapping ne lie pas, Tab, Home, End, PageUp, PageDown, retombent sur le mapper existant. |
| D5 | Deux champs optionnels dans `.buttonsMapping`, `repeat_initial_delay_ms` et `repeat_interval_ms`, défauts 350 et 120, réécrits seulement s'ils sont posés. Unité : millisecondes. Les directions répètent, avec déplacement dès l'appui ; Submit et Cancel partent sur le front seul. Une seule minuterie, remise à zéro dès que l'ensemble des entrées tenues change. |
| D6 | `.uiscreen` : `focus_wrap`, valeurs `vertical` ou `horizontal`, appliqué une fois par `NavigationNeighbors` sur `Desktop.GetFocusableElements` à la première image où le focus est dans la fenêtre. |
| D7 | `.uiscreen` : `cancel_command` nomme une entrée de `ui_commands`, passe par `Remember` (même garde de pile, exécution dans `Update`), refusé au chargement si le projet n'a pas de `UIInputMapping`. |
| D8 | `.uiscreen` : `move_command` nomme une entrée de `ui_commands`, lancé quand le focus se déplace entre éléments de cette fenêtre hors mode pointeur, pas quand un focus par défaut s'applique. |
| D9 | Aucun changement de `MGListBox` ni de la répétition de Submit du chemin brut : limites documentées, comme la perte du bouclage en grille et la liste figée au chargement. |

Compléments fixés à la lecture du code, là où D1 → D9 ne disent rien. Chacun évite une panne silencieuse et peut être élargi plus tard sans casser un fichier valide :

| Réf | Complément |
|---|---|
| C1 | Une action UI déclarée deux fois est refusée : `InputMappingManager` ne lirait que la première entrée, l'UI et le gameplay divergeraient. |
| C2 | Une liaison souris sur une action UI est refusée, ainsi qu'une liaison manette d'un autre numéro que celui lu par `GamePadTracker` : le fichier promettrait une entrée que rien ne lit. |
| C3 | Une liaison sans entrée (`NoDevice`, touche `None`, bouton `None`) compte comme absente. Une action obligatoire sans aucune entrée est refusée. |
| C4 | Le front est celui de l'action, comme `InputMapping.Pressed` (l'une ou l'autre liaison). La minuterie regarde les entrées : ajouter une liaison tenue la remet à zéro sans déplacer le focus. |
| C5 | Quand une zone de texte modifiable a le focus, une touche que le mapper existant ne connaît pas (une lettre liée à Cancel, par exemple) reste du texte et ne devient pas une action. Le reste suit le service de MGUI. |
| C6 | La première image ne lit que l'état : une touche déjà tenue à la création d'un `UIRoot` (Entrée qui vient de changer de monde) ne vaut pas un front. |
| C7 | `repeat_initial_delay_ms` ≥ 0 et `repeat_interval_ms` > 0, refusés sinon. Une valeur non entière fait lever `Load`, chemin d'échec existant d'un `.buttonsMapping`. |
| C8 | La mapping est lue par `LoadCopy` : l'objet appartient au jeu et ne dépend pas du comptage des prises. Elle se charge une fois dans `CasaEngineGame.Initialize`, un nom inconnu ou un fichier illisible y arrête le jeu. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/data-driven-navigation`**, créée depuis `chantier/pixel-ui-screens` à `e60fb10a`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur, puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`. **Jamais `--no-build`** : les `.sln` ne reconstruisent pas le projet de test.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **Dépôt public** : aucun nom de jeu commercial ni de projet privé dans ce dépôt, ni donnée de jeu. Dire « un projet » ou « consommateur externe ». Alundra peut être nommé.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : vert.
- Smoke : Launcher sur `Projects/PixelUiSample/PixelUiSample.json` au clavier : bouclage aux deux bouts, répétition d'une touche tenue, confirmer et annuler aux touches de la mapping, aucune exception dans le log.

---

## Phase 0 — Cadre

### ✅ T0.1 — Plan et ADR-0080

- Objectif : enregistrer format, classe et limites avant le code.
- Fichiers : `ai-agent/tasks/data-driven-navigation-tasks.md`, `ai-agent/README.md`, `docs/decisions/0080-data-driven-navigation-through-a-ui-input-mapping.md`, `docs/decisions/0078-ui-screens-declare-commands-in-data.md`, `docs/decisions/README.md`.
- Étapes : plan, ligne du tableau, ADR-0080, statut d'ADR-0078 et index.
- Validation : relecture contre D1 à D9 et le code cité.
- Commit : `docs(adr): record data-driven navigation`
- Validation faite le 2026-10-31 : relu contre D1 à D9, C1 à C8 et le code cité. ADR-0078 : seul le statut change, index mis à jour.

## Phase 1 — Format et runtime

### ✅ T1.1 — Répétition dans `.buttonsMapping`

- Objectif : lire et réécrire les deux champs de D5.
- Fichiers : `CasaEngine/Framework/Input/ButtonsMapping.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/Input/ButtonsMappingAssetLoaderTests.cs`.
- Étapes : deux `int?` ; lecture stricte (entier JSON) ; écriture seulement si posés.
- Validation : absent non réécrit, aller-retour, valeur non entière levée, `Projects/RPGDemo` inchangé.
- Commit : `feat(input): add optional repeat timings to buttons mappings`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4518/4518. Le fichier de `Projects/RPGDemo` charge sans les clés et ne les gagne pas à la sauvegarde. La plage des valeurs n'est pas contrôlée ici : c'est le lecteur qui les applique (C7, T1.2).

### ✅ T1.2 — `UINavigationInput`

- Objectif : D1, D3, D4, D5, C1 à C7.
- Fichiers : `CasaEngine/Framework/UI/UINavigationInput.cs`, `CasaEngine.Tests/UI/UINavigationInputTests.cs`.
- Étapes : compiler la mapping en table de liaisons ; `Update(TimeSpan)` public qui lit `InputTracker` par un délégué conservé, surcharge interne à état injecté ; masque des entrées, fronts, minuterie unique ; `CancelNotHandled`.
- Validation : headless : fronts, répétition à la milliseconde près, minuterie remise à zéro, Submit et Cancel sans répétition, repli Tab, Shift+Tab, chaque refus, zone de texte, touche tenue à la création.
- Commit : `feat(ui): add a mapping-driven UI navigation input`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4559/4559, dont `UINavigationInputTests` 41/41 en sans-affichage, sur un vrai `MGDesktop`. Le chemin réel du clavier passe par `InputTracker` dans deux tests, la manette reste injectée. Une image sans changement n'alloue rien (`AllocationWindow`). Mutations constatées, avec reconstruction : sans remise à zéro de la minuterie, 3 tests échouent ; sans le garde de zone de texte, 1 ; sans la lecture de la première image, 1 ; Submit répété sous une direction tenue, 1. Ce dernier cas n'était d'abord pas couvert : le premier test laissait le focus quitter le bouton, le mutant passait, le test a été corrigé.

### ✅ T1.3 — Champ projet et câblage

- Objectif : D2, C8.
- Fichiers : `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs`, `ProjectSettingsHelper.cs`, `CasaEngine/Framework/Application/CasaEngineGame.cs`, `GameManager.cs`, `CasaEngine/Framework/UI/UIRoot.cs`, tests de configuration.
- Étapes : champ optionnel, chargement une fois après le mixer, type de catalogue contrôlé, `UIRoot` crée et appelle. `CancelNotHandled` se branche en T1.5, avec la méthode de l'écran qu'il appelle.
- Validation : champ absent non réécrit, aller-retour, nom inconnu et autre type refusés. Le câblage réel est prouvé par T2.1, car `UIRoot` exige un device.
- Commit : `feat(project): load the UI input mapping a project names`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4566/4566. Testés : `ProjectSettingsUIInputMappingTests` et `FindButtonsMappingAsset`. `LoadUIInputMapping` et `UIRoot` ne sont pas testables sans device : à prouver par T2.1, aucune mapping n'étant encore nommée par un projet du dépôt.

### ✅ T1.4 — Champs de l'enveloppe

- Objectif : charger et sauver les champs de D6 à D8.
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/UI/UIScreenAssetSerializationTests.cs`.
- Étapes : enum `UIScreenFocusWrap`, deux chaînes ; erreurs dans `CommandFormatErrors` ; écriture hors défaut.
- Validation : absent non réécrit, aller-retour, valeur inconnue signalée.
- Commit : `feat(ui): add navigation fields to the UIScreen envelope`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4576/4576. Limite héritée de `UIScreenAsset` : une valeur mal formée n'est pas réécrite par une sauvegarde de l'enveloppe, et `DataDrivenScreen` refuse le fichier. `focus_wrap` est sensible à la casse, comme les types d'action.

### ⏳ T1.5 — `DataDrivenScreen`

- Objectif : appliquer D6, D7, D8.
- Fichiers : `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `UIScreenActionContext.cs`, `GameManager.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`.
- Étapes : résoudre les deux commandes au chargement ; bouclage une fois ; suivi du focus par comparaison, sans abonnement ; `HasUIInputMapping` optionnel dans le contexte.
- Validation : headless, avec `UINavigationInput` réel : bouclage aux deux bouts, Cancel garde de pile et ComboBox ouverte, son au déplacement, pas au focus par défaut, pas à la souris, un Submit prioritaire dans la même image.
- Commit : `feat(ui): wrap focus, cancel and react to focus moves from data`

## Phase 2 — Sample, doc

### ⏳ T2.1 — `PixelUiSample` et course réelle

- Objectif : sample obligatoire, gestes réels.
- Fichiers : `Projects/PixelUiSample/` (`.buttonsMapping`, projet, écrans, son copié de `Projects/SampleProject/Audio`, catalogue, `README.md`), `CasaEngine.Tests/UI/ProjectScreenAssetTests.cs`.
- Étapes : mapping aux touches non standard pour prouver qu'elle agit, `focus_wrap`, `cancel_command`, `move_command`.
- Validation : Launcher au clavier avec captures ; le script de pilotage tient une touche 200 ms, une tenue plus longue passe par une copie du script dans le scratchpad. 🧪 pour son et manette.
- Commit : `feat(sample): drive the pixel sample through a buttons mapping`

### ⏳ T2.2 — Documentation

- Objectif : format, limites.
- Fichiers : `docs/engine/data-driven-screens.md`, `docs/README.md`.
- Étapes : usage, table des actions, erreurs, limites.
- Validation : relecture contre le code et T2.1.
- Commit : `docs(ui): document data-driven navigation`

### ⏳ T2.3 — Clôture

- Objectif : validation globale, plan et tableau à jour.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes : builds, tests, relecture du diff complet, recherche de noms interdits dans le diff.
- Validation : tout vert. Le plan reste dans `tasks/` tant qu'un 🧪 attend l'auteur.
- Commit : `docs(plan): close data-driven navigation`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Aucun. | — |

## Hors périmètre

- Changement de MGUI : bouclage de `MGListBox`, répétition de Submit sur le chemin brut, modificateurs dans `InputActionContext`.
- Éditeur de mapping, inspecteur du `.uiscreen` : les champs s'éditent à la main dans le JSON.
- Fondu, attente, blocage d'entrée pendant un fondu : chantier suivant.
- Tout contenu propre à un jeu dans le moteur.
