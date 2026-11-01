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
- `ScreenStack.Update` ne met à jour que le sommet bloquant et au-dessus (`ScreenStack.cs:137-149` à `fc4359b1`) : une liste suspendue sous un modal ne reprendrait pas.
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
| C9 | Ajouté par T3.1, remplace la partie « chemin brut » de D5, seul point de D1 → D10 revu : aucun écran ne tient l'arrêt. `ScreenStack.IsNavigationSuspended` le dit d'après le sommet de la pile, `UIRoot` le recalcule à chaque image et pose `Desktop.IsNavigationInputSuspended` (ADR-0025 de MGUI), qui laisse `UseRawNavigationInput` tranquille et oublie ce qui est tenu quand il retombe. |

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

### 🧪 T2.1 — Sample et course réelle

- Objectif : sample obligatoire, D7 et D9.
- Fichiers : `Projects/PixelUiSample/`, `CasaEngine.Tests/UI/UINavigationInputTests.cs`.
- Étapes : curseur et script qui le génère, bouton, monde noir, caméra 2D dans les deux mondes.
- Validation : Launcher, captures pendant et après le fondu, touches pendant l'attente, log sans exception.
- Commit : `feat(sample): fade the pixel sample to a black world`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, vert. Vrai Launcher, gestes injectés, captures relues et pixels lus dans les PNG, fenêtre 960x540 sauf mention :
  - curseur à l'échelle 3 : corps opaque (255,216,64), ombre (96,64,16) sur le fond (208,176,128), soit 112 retirés de chaque canal, exact ;
  - clic sur `FADE OUT`, fondu de 1,5 s : pendant, monde, texte, boutons et curseur s'assombrissent ensemble, le voile est bien au-dessus de l'UI ; après, image noire uniforme, seuls les coins arrondis de la fenêtre Windows diffèrent ; `BlackWorld` chargé une fois, aucune exception ni avertissement ;
  - clavier sur une copie à 6 s, projet à mapping : Bas, Bas, Z sur `FADE OUT`, puis Haut, Haut, Z, X pendant le fondu. Le bouton garde son reflet de focus (bleu 63 contre 56 aux autres sous le même voile), rien ne repart, `BlackWorld` une fois. La ligne de `UIRoot` et les délégués de `GameManager` sont donc prouvés ;
  - même parcours sur une copie sans mapping, chemin brut de MGUI : Haut, Haut, Entrée pendant le fondu, focus intact (104 contre 97), `BlackWorld` une fois ;
  - fenêtre 1000x600, k = 3, image centrée : les coins de la vue sont couverts, les bandes restent noires ;
  - copie sans caméra dans les mondes (D7) : ici le voile a couvert toute la vue, avec une `CameraLookAtComponent` par défaut et l'avertissement « No camera found ». C'est une seule mesure, à cette taille : ADR-0033 ne la garantit pas, D7 reste la règle.
- Défaut trouvé par la course, dans le sample : un bouton étroit à côté du curseur sortait de la navigation, Bas depuis `OPTIONS` allait sur `QUIT` par la géométrie, et Z quittait le jeu. Le curseur est maintenant dans le bouton, en pleine largeur.
- À vérifier par l'auteur, 🧪 : manette réelle pendant un fondu. `Suspended_TheGamepadReachesNothing_Either` le prouve sur des états injectés, pour le chemin à mapping ; le chemin brut ne l'est que par lecture (`MGDesktop.cs:1580`). Aucun son dans ce parcours.

### ✅ T2.2 — Documentation

- Objectif : format, limites, pas exact.
- Fichiers : `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, `docs/README.md`, `docs/decisions/0081-screen-fades-and-waits-in-data-driven-commands.md`.
- Étapes : actions, erreurs, limites D5 à D8.
- Validation : relecture contre le code et T2.1.
- Commit : `docs(ui): document screen fades in data-driven commands`
- Validation faite le 2026-11-01 : relu contre `DataDrivenScreen`, `UINavigationInput`, `CutsceneAssetJsonSerializer`, `CutsceneActionCoroutineFactory` et les mesures de T2.1. Les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, 4663/4663. Hors `docs/`, seul code de ce commit : `UIScreenAsset` passe par `GetByte` et `GetSingle`, les aides JSON du fichier. La limite sur un contrôle qui lit lui-même le clavier vient de la lecture du code : elle est écrite comme telle, et ADR-0081 est corrigé, il la disait propre au chemin brut.

### ✅ T2.3 — Clôture

- Objectif : validation globale, plan et tableau à jour.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes : builds, tests, relecture du diff complet, recherche de noms interdits dans le diff.
- Validation : tout vert. Le plan reste dans `tasks/` tant qu'un 🧪 attend l'auteur.
- Commit : `docs(plan): close screen fade actions`
- Validation faite le 2026-11-01 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, 4663/4663 ; `MGUI.Tests` 3214/3214, GPU compris, sur `7e1dd328`. Diff complet depuis `fc4359b1` relu, 37 fichiers, et diff MGUI depuis `3db3c769` : aucun nom interdit (recherche dans les diffs et les messages de commit). Pas de vérificateur neuf : aucun outil de délégation n'était disponible dans cette session. Plan laissé dans `tasks/` pour le 🧪 de T2.1. Le pointeur `MGUI` vise `chantier/image-blend-mode`, non fusionnée dans `develop` : à fusionner avant de porter cette branche sur `main`.

## Phase 3 — Corrections après vérificateur neuf

### ✅ T3.1 — L'arrêt de la navigation se déduit de la pile

- Objectif : traiter les deux constats P2 du vérificateur. Chacun a été revérifié dans le code : les deux sont réels, aucun n'est faux.
  - Constat 1 : sans mapping, une entrée manette tenue pendant une attente se déclenche quand elle finit, contrairement à la doc. `GamePadTracker.Update` répète un bouton tenu quel que soit le lecteur, `TryDispatchGamePadNavigationActions` lit `WasTriggered`, vrai sur une répétition, et A vaut Submit. Le clavier brut n'a pas ce défaut : il suit le front `Pressed`, `Repeated` est un autre événement.
  - Constat 2 : la limite « jusqu'à la fin de l'attente » est fausse sous un modal. `ScreenStack.Update` part du sommet bloquant, l'écran gelé ne reçoit plus d'`Update`, `RestoreNavigation` n'est jamais atteint, et un joueur à la manette seule reste bloqué sur le modal.
- Cause commune : l'écran tenait un état global du desktop à travers une transition qu'il ne peut pas toujours achever. Corriger chaque cas aurait ajouté une transition de plus : c'est la transition qui disparaît (C9).
- Fichiers MGUI, branche `chantier/navigation-input-suspension` depuis `7e1dd328` : `MGUI.Core/UI/MGDesktop.cs`, `MGUI.Shared/Input/GamePad/GamePadTracker.cs`, `MGUI.Shared/Properties/AssemblyInfo.cs`, `MGUI.Tests/Focus/NavigationInputSuspensionTests.cs`, `MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs`, `Docs/decisions/0025-pause-the-raw-navigation-path.md`, `Docs/decisions/README.md`, `Docs/input-architecture.md`.
- Fichiers moteur : pointeur `MGUI`, `CasaEngine/Framework/UI/ScreenStack.cs`, `CasaEngine/Framework/UI/UIRoot.cs`, `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `CasaEngine/Framework/UI/UINavigationInput.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`, `docs/decisions/0081-screen-fades-and-waits-in-data-driven-commands.md`, `docs/engine/data-driven-screens.md`, ce plan, `ai-agent/README.md`.
- Étapes : côté MGUI, `IsNavigationInputSuspended` et `ForgetHeldButtons`, plus `StateOverride`, interne, pour nourrir le tracker en test. Côté moteur, `IsNavigationSuspended` et une ligne en fin de `UIRoot.Update` ; `DataDrivenScreen` perd `_rawNavigationBeforeWait`, `SuspendNavigation` et `RestoreNavigation`. Les tests passent sur une vraie `ScreenStack`, qui gèle réellement l'écran, avec `PushBuilt` et `RemoveBuilt`, internes, car `Push` exige un `UIRoot`.
- Validation : mutations, build, tests, course réelle.
- Commit : `fix(ui): work out the navigation stop from the stack each frame`
- Validation faite le 2026-11-01 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, 4668/4668 ; `MGUI.Tests` 3225/3225, GPU compris, sur `62498f75`.
- Mutations, chacune avec un `dotnet test` simple qui reconstruit, puis retirée :
  - `MGUI.Tests`, pas de `ForgetHeldButtons` à la levée : 2 tests échouent ; `IsNavigationInputSuspended` ignoré par le chemin brut : 5 tests échouent ;
  - `CasaEngine.Tests`, pas de `ForgetHeldButtons` : `AButtonHeldThroughAWait_RunsNothingWhenItEnds_UntilItIsPressedAgain` et `AButtonHeldThroughAWait_DoesNotConfirmTheScreenItOpens` échouent, la liste repart seule et `quit` part sur l'écran ouvert, soit le scénario du constat 1 ;
  - `CasaEngine.Tests`, un écran en attente compte même gelé : `AModalPushedOverAWait_HasTheKeyboardAndTheGamepad_AndTheListResumesWhenItCloses` et `TheStopComesBack_WhenAScreenOverAWaitClosesBeforeTheFadeEnds` échouent, sur l'assertion du modal.
- Course réelle, vrai Launcher, fondu de 4 s jusqu'à 128 sur des copies du sample, pixels lus dans les captures, aucune exception dans les logs :
  - copie sans mapping, puis `open_screen` : Entrée tenue depuis le confirmer jusqu'après l'attente, Haut, Haut pendant le fondu. Le focus reste sur `FADE OUT` (0,72,161 contre 0,48,154 aux autres), le popup s'ouvre, reste ouvert touche tenue avec son focus sur `BACK` (0,4,93 contre 0,0,86), puis Droite va sur `ONE`, puis `TWO` : le clavier est rendu ;
  - copie sans mapping, liste qui laisse l'écran : Haut, Haut pendant le fondu sans effet, puis Haut fait passer le focus de `FADE OUT` à `OPTIONS`, et Entrée ouvre le popup ;
  - copie à mapping, même parcours avec Z tenu : mêmes valeurs ;
  - `Projects/PixelUiSample` tel quel : `FADE OUT` au clavier, écran noir, `BlackWorld` chargé une fois.
- Non prouvé par une course : le modal poussé pendant une attente exige du code, donc pas de projet sans DLL. Il est prouvé sur une vraie `ScreenStack`, sans `UIRoot`. La ligne de `UIRoot` elle-même est prouvée par les courses ci-dessus, car `UIRoot` exige un device. Manette réelle : toujours à vérifier par l'auteur, voir T2.1 ; le chemin brut est désormais prouvé sur états injectés, et non plus seulement par lecture.
- `ResolvedPilotWriteSitesTests` a échoué en cours de route, à raison : cette garde épingle les écritures par ligne, et `var MinHeight` est passé de 980 à 1001. Entrée mise à jour, avec sa raison, comme chaque décalage précédent ; la garde n'est pas assouplie. Le pointeur `MGUI` vise maintenant `chantier/navigation-input-suspension` `62498f75`, qui contient `chantier/image-blend-mode` : c'est elle qu'il faut fusionner dans `develop`.

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
