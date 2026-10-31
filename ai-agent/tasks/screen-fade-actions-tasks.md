# Plan agent IA — Image soustractive et actions de fondu (E4)

Plan d'exécution de E4, suite de [data-driven-navigation-tasks.md](data-driven-navigation-tasks.md) (E3), sans analyse dédiée : l'étude préalable vient d'un projet externe consommateur du moteur et chaque point y a été vérifié dans le code avant d'entrer ici.
Les décisions D1 → D10 ci-dessous ont été déléguées puis arbitrées par l'auteur le 2026-10-31 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un projet sans DLL obtient, par ses seules données : une image qui soustrait sa couleur de ce qui est dessous (attribut `BlendType` de l'`Image` XAML), un fondu d'écran lancé par une commande (`fade_screen`), et une attente de la fin de ce fondu (`wait_fade`) qui suspend la liste d'actions et coupe clavier et manette sans effacer le focus.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-31)

- Branche `chantier/screen-fade-actions` créée depuis `chantier/data-driven-navigation` à `fc4359b1`, arbre propre hors pointeur `MGUI`. Dernier ADR : 0080.
- MGUI : branche `chantier/image-blend-mode` depuis `develop` à `3db3c769`, faite avant ce plan, selon les règles de MGUI : `18d14d03` (code, tests, sample) et `7e1dd328` (ADR-0024 de MGUI). Non fusionnée dans `develop`, non poussée. Sample regardé dans `MGUI.Samples` : les trois modes rendent sur fond clair. 3214/3214 tests de `MGUI.Tests`, dont les tests GPU, et la mutation `Subtract` à la place de `ReverseSubtract` fait échouer les 5 tests GPU.
- `CasaMonoGameRenderInterop.BlendMap` lève sur une valeur absente (`CasaMonoGameRenderInterop.cs:17-37,60`) : l'entrée `Subtractive` doit arriver avec le pointeur.
- Ordre d'une image : `view.UIView.Update` (`CasaEngineGame.cs:571-575`), donc `DataDrivenScreen.Update`, puis `GameManager.UpdateWorld` (`:598`), puis les `GameComponent`. `ScreenEffects` est après `Audio` (`ComponentOrder.cs:24-30`). Un `fade_screen` avance donc d'un `dt` dans l'image où il part, et la liste reprend dans l'image qui suit celle où `IsFading` est passé à faux.
- `ScreenEffectComponent` est créé sans condition (`CasaEngineGame.cs:416`). `IsFixedTimeStep` vient du projet (`:492`). `TargetElapsedTime` n'est posé nulle part dans le moteur.
- `UseRawNavigationInput` à faux coupe le clavier et la manette du chemin brut, seuls lieux où il est lu (`MGDesktop.cs:1439,1580`), sans toucher au focus.
- `UIRoot.Update` appelle `_navigationInput?.Update` entre `Desktop.Update` et la pile (`UIRoot.cs:148-157`).
- `ScreenStack.Update` ne met à jour que le sommet bloquant et au-dessus (`ScreenStack.cs:154-165`) : une liste suspendue sous un modal ne reprendrait pas.
- Un monde sans caméra reçoit une `CameraLookAtComponent` (`DefaultRuntimeViewBootstrapper.cs:30-35`, `World.cs:492-504`). ADR-0033 dit le quad fiable seulement derrière une `Camera2dComponent`.
- `new UIScreenActionContext` : `GameManager`, `DataDrivenScreenTests` (2), `ProjectScreenAssetTests` (2).
- `CutsceneAssetJsonSerializer.ParseBlendMode` retombe sur une valeur par défaut : à ne pas reprendre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Un curseur qui mêle texels opaques et soustractifs est deux sprites empilés en XAML, le second en `BlendType` `Subtractive`. Ni shader, ni nouveau format de sprite. |
| D2 | Nouvelles actions `fade_screen` et `wait_fade` en fin de `UIScreenActionType`, avec `Parse` et `ToName`. `fade_screen` porte `layer`, `blend_mode`, `from`, `to`, `duration_seconds`, tous obligatoires, contrôlés sans défaut silencieux, signalés par `CommandFormatErrors` et contrôlés dans `PrepareAction` ; `EditorAssetJsonSerializer` les écrit. `wait_fade` n'a pas de cible. |
| D3 | Le contexte gagne `StartFade` et un `Func<bool> IsFadeRunning` mis en cache ; `GameManager` les câble sur `_game.ScreenEffectComponent.Service`, en posant `Layer` puis `StartFade`. |
| D4 | `DataDrivenScreen` reprend sa liste : deux champs, la commande en cours et l'indice de la prochaine action, sans itérateur, closure ni liste. `Update` la reprend. `_isRunningCommand` reste limité à l'appel de reprise, donc un `Hide` pendant une attente dispose tout de suite. `Dispose` vide la commande en cours. `Remember` refuse pendant une commande, ce qui règle le double confirmer. |
| D5 | Pendant une liste suspendue, clavier et manette sont ignorés sans effacer le focus : arrêt de `UINavigationInput` pour cet écran, `Desktop.UseRawNavigationInput` à faux sur le chemin brut, valeur précédente rendue à la fin de la liste et dans `Dispose`. Ni `IsEnabled` ni `IsHitTestVisible` sur la fenêtre. Le survol souris continue, les commandes restent refusées : limite documentée. |
| D6 | Pas de pas fixe dans le service. Un projet qui veut un pas exact pose `IsFixedTimeStep` et une durée en images sur la cadence : documenté, avec la limite qu'un pas ne tombe juste que s'il divise 255. Tout décalage d'image par rapport à une cible est documenté. |
| D7 | Un monde utilisé avec un fondu au-dessus de l'UI déclare une entité avec une `Camera2dComponent` : donnée, documentée, sans code moteur, vérifiée en course réelle. |
| D8 | Pas de fondu à l'ouverture d'un écran, pas d'action qui retire l'overlay : limites documentées. |
| D9 | Sample : `Projects/PixelUiSample` gagne un curseur à deux sprites fait pour le sample, et un bouton qui lance `fade_screen`, `wait_fade`, `change_world` vers un monde noir. Course réelle, captures pendant et après le fondu. |
| D10 | MGUI d'abord : `BlendType.Subtractive`, `MGImage.BlendMode`, attribut XAML, seconde passe de `Brightness` sautée hors défaut. ADR-0024 de MGUI. Ici : pointeur et entrée de table. |

Compléments fixés à la lecture du code, là où D1 → D10 ne disent rien. Chacun évite une panne silencieuse et peut être élargi plus tard sans casser un fichier valide :

| Réf | Complément |
|---|---|
| C1 | Seuls `play_sound` et `fade_screen` (et un autre `wait_fade`) peuvent précéder un `wait_fade` dans une liste : refus au chargement sinon. `open_screen` modal gèle l'écran, `close_screen` et `change_world` le sortent de la pile, `quit` termine : la liste mourrait sans signe. |
| C2 | `from` et `to` valent `[r, g, b]` ou `[r, g, b, a]`, entiers de 0 à 255. Sans le quatrième, l'alpha est 255, comme les surcharges RVB du service. Sans lui, un fondu `AlphaBlend` serait opaque dès la première image. |
| C3 | Valeur mal formée : `CommandFormatErrors`, chemin du fichier à l'appui. Champ absent : `PrepareAction`, comme une cible manquante. `duration_seconds` doit être fini et positif ou nul : 0 applique la cible tout de suite, comme le service. |
| C4 | `layer` et `blend_mode` s'écrivent avec le nom exact de l'enum : un nombre ou une autre casse sont refusés. |
| C5 | `fade_screen` refuse une `target`, comme `wait_fade`. |
| C6 | `StartFade` et `IsFadeRunning` sont `required` : un hôte qui n'offre pas de fondu n'existe pas à ce jour, et un délégué nul se verrait trop tard. |
| C7 | `UINavigationInput` continue de lire les entrées quand elle est suspendue, et traite tout ce qui est tenu comme une touche déjà tenue à sa création (C6 d'E3) : ni front ni répétition à la fin de la liste pour une touche tenue pendant l'attente. |
| C8 | `DataDrivenScreen.IsCommandSuspended`, public, dit qu'une liste attend. `UIRoot` le lit au sommet de la pile avant `UINavigationInput.Update`. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/screen-fade-actions`**, créée depuis `chantier/data-driven-navigation` à `fc4359b1`. Ne jamais committer sur `main`.
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
- Smoke : Launcher sur `Projects/PixelUiSample/PixelUiSample.json` : curseur à deux sprites visible, clic et touche sur le bouton de fondu, focus intact et touches sans effet pendant l'attente, monde noir ensuite, aucune exception dans le log.

---

## Phase 0 — Cadre

### ✅ T0.1 — Plan et ADR-0081

- Objectif : enregistrer les décisions avant le code.
- Fichiers : ce plan, `ai-agent/README.md`, `docs/decisions/0081-screen-fades-and-waits-in-data-driven-commands.md`, `docs/decisions/0078-ui-screens-declare-commands-in-data.md`, `docs/decisions/README.md`.
- Étapes : ADR-0081 amende ADR-0078 sur l'absence d'attente ; statut d'ADR-0078 et index mis à jour, corps inchangé.
- Validation : relecture contre D1 à D10 et le code cité.
- Commit : `docs(adr): record screen fades and waits in data-driven commands`
- Validation faite le 2026-10-31 : relu contre D1 à D10 et le code cité. Aucun code touché. Le pointeur `MGUI`, déjà déplacé dans l'arbre, n'entre pas dans ce commit : il va avec T0.2.

### ✅ T0.2 — Pointeur MGUI et table de blend

- Objectif : l'image soustractive marche sur le backend du moteur.
- Fichiers : `MGUI` (pointeur), `CasaEngine/Framework/UI/Backend/MonoGame/CasaMonoGameRenderInterop.cs`, `CasaEngine.Tests/UI/CasaMonoGameRenderInteropTests.cs`.
- Étapes : pointeur sur `7e1dd328` ; entrée `Subtractive` ; test des facteurs.
- Validation : les deux `.sln`, tests.
- Commit : `feat(ui): pick up the image blend mode of MGUI`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4601/4601, dont `CasaMonoGameRenderInteropTests` 2/2, qui couvre toutes les valeurs de `BlendType`.

## Phase 1 — Actions

### ✅ T1.1 — `fade_screen` et `wait_fade` dans l'enveloppe

- Objectif : charger et sauver les actions.
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenCommandData.cs`, `CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/UI/UIScreenAssetSerializationTests.cs`.
- Étapes : champs nullables, lecture avec `CommandFormatErrors`, écriture.
- Validation : aller-retour, valeurs mal formées, champs absents non inventés.
- Commit : `feat(ui): add fade actions to the UIScreen envelope`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4621/4621. Deux tests existants prenaient `wait_fade` comme exemple de type inconnu : ils passent à `shake_screen`, leur sens ne change pas. Jusqu'à T1.2, `DataDrivenScreen` refuse ces deux actions au chargement, sans rien lancer.

### ✅ T1.2 — Liste suspendue dans `DataDrivenScreen`

- Objectif : exécuter les actions, reprendre la liste, couper clavier et manette.
- Fichiers : `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `CasaEngine/Framework/UI/UIScreenActionContext.cs`, `CasaEngine/Framework/UI/UINavigationInput.cs`, `CasaEngine/Framework/UI/UIRoot.cs`, `CasaEngine/Framework/Application/GameManager.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`, `CasaEngine.Tests/UI/ProjectScreenAssetTests.cs`, `CasaEngine.Tests/UI/UINavigationInputTests.cs`, `CasaEngine.Tests/UI/ScreenFadeWiringTests.cs`, `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectServiceTests.cs`.
- Étapes : D3, D4, D5, C1, C3, C5, C7, C8.
- Validation : liste suspendue sur plusieurs images, `Hide` et `Dispose` pendant une attente, double confirmer, chemin brut et mapping, câblage du service, pas exact à cadence fixe.
- Commit : `feat(ui): run screen fades and suspend a command until they end`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, 4662/4662. Quatre mutations constatées, avec reconstruction : `Remember` sans le contrôle de la commande en cours (2 tests échouent), pas de `SuspendNavigation` (1), `Dispose` qui ne vide rien (2), `UINavigationInput` qui ignore `IsSuspended` (5). Mesuré par une sonde hors commit : `new Game().TargetElapsedTime.Ticks` vaut 166667, la valeur que prend `AFadeInFrames_StepsExactly_WhenTheStepDivides255`. La ligne de `UIRoot` et les délégués réels de `GameManager` ne sont prouvés que par la course de T2.1 : `UIRoot` exige un device.

## Phase 2 — Sample et documentation

### ⏳ T2.1 — Sample et course réelle

- Objectif : sample obligatoire, D7 et D9.
- Fichiers : `Projects/PixelUiSample/`.
- Étapes : curseur et script qui le génère, bouton, monde noir, caméra 2D dans les deux mondes.
- Validation : Launcher, captures pendant et après le fondu, touches pendant l'attente, log sans exception.
- Commit : `feat(sample): fade the pixel sample to a black world`

### ⏳ T2.2 — Documentation

- Objectif : format, limites, pas exact.
- Fichiers : `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, `docs/README.md`.
- Étapes : actions, erreurs, limites D5 à D8.
- Validation : relecture contre le code et T2.1.
- Commit : `docs(ui): document screen fades in data-driven commands`

### ⏳ T2.3 — Clôture

- Objectif : validation globale, plan et tableau à jour.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes : builds, tests, relecture du diff complet, recherche de noms interdits dans le diff.
- Validation : tout vert. Le plan reste dans `tasks/` tant qu'un 🧪 attend l'auteur.
- Commit : `docs(plan): close screen fade actions`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Aucun. | — |

## Hors périmètre

- Fondu à l'ouverture d'un écran, action qui retire l'overlay.
- Pas fixe dans le service, attente d'une durée ou d'autre chose qu'un fondu.
- Blocage de la souris pendant une attente.
- Curseur lié au focus.
- Fusion dans `develop` de MGUI, push.
- Tout contenu propre à un jeu dans le moteur.
