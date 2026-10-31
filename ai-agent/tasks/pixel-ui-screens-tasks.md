# Plan agent IA — Écrans UI en pixels (E2)

Plan d'exécution de E2, suite de [data-driven-screen-flow-tasks.md](archive/data-driven-screen-flow-tasks.md) (E1), sans analyse dédiée : l'étude préalable vient d'un projet externe consommateur du moteur et chaque point y a été vérifié dans le code avant d'entrer ici.
Les décisions D1 → D10 ci-dessous ont été déléguées puis arbitrées par l'auteur le 2026-10-31 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un projet sans DLL déclare une résolution de conception et obtient ses écrans de données dessinés à une échelle entière qui suit la fenêtre, avec une police bitmap que l'écran déclare.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-31)

- `main` est à `c19ad673`, le commit de départ demandé, arbre propre. `MGUI` à `3db3c769`.
- Dernier ADR : 0078 (`docs/decisions/`).
- `VirtualResolutionLayout.Compute` est pur et donne `k` (`CasaEngine/Framework/Rendering/VirtualResolutionLayout.cs:38`). `CasaEngineGame.ActiveVirtualResolution` vaut null dans l'éditeur (`CasaEngineGame.cs:81-82`).
- `XamlUIScreenBase.OnScreenBoundsChanged` existe et `UIRoot.Update` l'alimente (`XamlUIScreenBase.cs:157`, `UIRoot.cs:135`), écrans gelés compris (`ScreenStack.cs:120`).
- `MGWindow.Scale` met à l'échelle depuis le coin haut-gauche de la fenêtre (`MGWindow.cs:484`) : `Left` et `Top` restent en coordonnées d'écran, d'où `Left*k` et `Top*k`. `Scale` recalcule les matrices avec le `Left` courant (`MGWindow.cs:454`), donc `Left` et `Top` s'écrivent avant lui.
- `ApplyScreenPlacement` calcule en pixels d'écran sans tenir compte de `Scale` (`MGWindow.cs:246-297`) : une fenêtre placée par alignement serait fausse sous échelle.
- `MGTextBlock.TrySetFont` refuse une famille non résolue et garde la précédente, sans message (`MGTextBlock.cs:119-122`). Une famille se résout pendant le chargement du XAML, donc la police doit être tenue avant `base.LoadWindow`.
- `UIFontRegistry.Acquire` n'a d'appelant que dans les tests (`rg "UIFonts" CasaEngine.Tests CasaEngine`). `UIRoot` attache son moteur de texte au registre (`UIRoot.cs:89-90`).
- `AssetInfo.InferAssetType` donne `fnt` pour un `.fnt` (`AssetInfo.cs:98-106`).
- Aucun projet du dépôt n'a de `VirtualResolution`, ni de `.fnt`. Le rendu d'un vrai `.fnt` n'a jamais été observé dans ce dépôt (`BitmapFontLoaderTests.cs:11-13` : aucun test n'a de device).
- Python n'est pas installé sur la machine : la génération de la police passe par PowerShell et `System.Drawing`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Nouvelle clé booléenne optionnelle `ScaleUI` dans `VirtualResolution` du projet. Lue, et écrite seulement si vraie (`VirtualResolutionSettings`, `ProjectSettingsHelper`). |
| D2 | `k` est l'échelle entière d'ADR-0048, dérivée de la fenêtre, jamais fixée en donnée. `UIScreenActionContext` gagne un membre optionnel, non `required`, qui donne la résolution de conception ou null, rempli par `GameManager` depuis `ActiveVirtualResolution`. |
| D3 | `DataDrivenScreen` pose `Window.Scale` depuis `VirtualResolutionLayout` à la fin de `OnWindowLoaded`, puis dans `OnScreenBoundsChanged`. Il garde `Left` et `Top` du XAML et écrit `Left*k`, `Top*k` au même endroit. Une fenêtre `HasScreenPlacement` sous cette échelle est refusée au chargement par `InvalidEnvelope`. |
| D4 | `UIRoot.UIScale` et `UIScaler` restent intacts. Aucune modification de MGUI. |
| D5 | Nouveau champ optionnel `fonts` du `.uiscreen` : liste de noms ou d'ids de catalogue d'assets `.fnt`, lue avec le contrôle de type existant dans `CommandFormatErrors`, réécrite seulement si non vide par `EditorAssetJsonSerializer`. |
| D6 | `UIScreenActionContext` gagne un `AcquireFont` optionnel, `Func<string, IDisposable>`. `GameManager` résout par le catalogue, vérifie le type `fnt`, appelle `_game.UIFonts.Acquire`. |
| D7 | `DataDrivenScreen` surcharge `LoadWindow` pour acquérir avant `base.LoadWindow`, garde les prises et les rend dans `Dispose`. Une police inconnue échoue avec le chemin de l'enveloppe. |
| D8 | Une faute de frappe sur `FontFamily` en XAML reste un repli silencieux de MGUI : limite documentée, pas de modification de MGUI. |
| D9 | Une seule `VirtualResolution` par projet : limite documentée. |
| D10 | Sample : nouveau projet `Projects/PixelUiSample`, `GameplayDllName` vide, `VirtualResolution` 320x180 avec `ScaleUI`, un écran de données, police bitmap en blocs dessinée pour le sample (PNG et `.fnt` catalogués). Exécution réelle à deux tailles de fenêtre avec captures. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/pixel-ui-screens`**, créée depuis `main` à `c19ad673`. Ne jamais committer sur `main`.
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
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : vert.
- Launcher sur `Projects/PixelUiSample/PixelUiSample.json` à deux tailles de fenêtre : texte de la police du sample en blocs de `k` pixels exacts, fenêtre à `Left*k`, `Top*k`, bandes noires hors de l'image, clic et clavier corrects, aucune exception dans `log.txt`.

---

## Phase 0 — Socle

### ✅ T0.1 — Plan et ADR-0079

- Objectif : enregistrer format, API et limites avant le code.
- Fichiers : `ai-agent/tasks/pixel-ui-screens-tasks.md`, `ai-agent/README.md`, `docs/decisions/0079-data-driven-screens-follow-the-design-resolution-and-hold-their-fonts.md`, `docs/decisions/0048-virtual-resolution-with-integer-fit-and-black-bands.md`, `docs/decisions/0078-ui-screens-declare-commands-in-data.md`, `docs/decisions/README.md`.
- Étapes : écrire le plan depuis le modèle ; ligne en tête du tableau d'`ai-agent/README.md` ; ADR-0079 selon `docs/decisions/template.md` ; statut d'ADR-0048 et d'ADR-0078 en `Accepted. Amended by ADR-0079 (...)`, corps inchangé, index mis à jour.
- Validation : relecture contre D1 à D10 et le code cité.
- Commit : `docs(adr): record pixel UI screens decisions`
- Validation faite le 2026-10-31 : relu contre D1 à D10 et les fichiers cités. Aucun code touché.

## Phase 1 — Runtime

### ✅ T1.1 — Clé `ScaleUI`

- Objectif : D1.
- Fichiers : `CasaEngine/Framework/Configuration/Project/VirtualResolutionSettings.cs`, `CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs`, `CasaEngine.Tests/Configuration/ProjectSettingsVirtualResolutionTests.cs`.
- Étapes : booléen `ScaleUI`, faux par défaut ; lecture stricte comme les dimensions (un booléen JSON, sinon `InvalidDataException` qui nomme la clé) ; écriture seulement si vrai.
- Validation : absente, faux et non réécrite ; vraie, aller-retour ; valeur non booléenne refusée.
- Commit : `feat(project): add the optional ScaleUI key to VirtualResolution`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` reconstruit, 4479/4479, dont 9 cas neufs dans `ProjectSettingsVirtualResolutionTests`. Un `null` JSON est refusé comme un autre type, à la manière de `Width`.

### ⏳ T1.2 — Échelle entière de `DataDrivenScreen`

- Objectif : D2, D3, D4.
- Fichiers : `CasaEngine/Framework/UI/UIScreenActionContext.cs`, `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `CasaEngine/Framework/Application/GameManager.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`, `CasaEngine.Tests/UI/FirstScreenTests.cs`.
- Étapes : membre optionnel `DesignResolution` (`Point?`) ; `GameManager` le remplit seulement si `ActiveVirtualResolution` a `ScaleUI` ; `OnWindowLoaded` retient `Left`, `Top`, refuse `HasScreenPlacement`, applique ; `OnScreenBoundsChanged` applique à nouveau.
- Validation : headless : sans résolution, rien ne change, `Scale` du XAML gardé ; `k` à la charge ; `k` après changement de bornes ; `Left*k`, `Top*k` ; clic au bon endroit sous échelle ; `HasScreenPlacement` refusé seulement sous échelle ; `Scale` du XAML écrasé sous échelle.
- Commit : `feat(ui): scale data-driven screens to the design resolution`

### ⏳ T1.3 — Champ `fonts` de l'enveloppe

- Objectif : D5.
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/UI/UIScreenAssetSerializationTests.cs`.
- Étapes : liste `Fonts` ; lecture avec rapport dans `CommandFormatErrors` ; écriture si non vide.
- Validation : absent, liste vide et clé non réécrite ; aller-retour dans l'ordre ; chaque valeur mal formée signalée avec sa place ; nouveau chargement sans le champ le vide.
- Commit : `feat(ui): add the fonts field to the UIScreen envelope`

### ⏳ T1.4 — Tenir les polices d'un écran

- Objectif : D6, D7.
- Fichiers : `CasaEngine/Framework/UI/UIScreenActionContext.cs`, `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `CasaEngine/Framework/Application/GameManager.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`, `CasaEngine.Tests/UI/ScreenAssetLookupTests.cs`.
- Étapes : membre optionnel `AcquireFont` ; surcharge de `LoadWindow` ; rendu dans `Dispose` ; recherche et contrôle de type dans `GameManager`.
- Validation : headless : prises avant le chargement du XAML, dans l'ordre ; rendues à `Hide` ; rendues si le chargement échoue ; police inconnue : chemin de l'enveloppe et nom ; `fonts` sans `AcquireFont` : erreur claire ; autre type de catalogue refusé.
- Commit : `feat(ui): hold the bitmap fonts a data-driven screen declares`

## Phase 2 — Sample et documentation

### ⏳ T2.1 — `Projects/PixelUiSample`

- Objectif : sample obligatoire, D10, et preuve réelle.
- Fichiers : `Projects/PixelUiSample/` (projet, catalogue, deux mondes, écran, police PNG et `.fnt`, générateur, README), `CasaEngine.Tests/UI/ProjectScreenAssetTests.cs`.
- Étapes : dessiner la police ; générer PNG et `.fnt` ; écrire le projet ; lancer le Launcher à deux tailles, plus une taille sans multiple entier.
- Validation : captures relues et mesurées, clic et clavier réels, `log` sans exception ; 🧪 pour ce que seul un humain peut vérifier.
- Commit : `feat(sample): add a pixel UI sample with its own bitmap font`

### ⏳ T2.2 — Documentation

- Objectif : format, usage, limites.
- Fichiers : `docs/engine/data-driven-screens.md`, `docs/README.md`, `docs/engine/asset-handles-and-bitmap-fonts.md` si utile, ADR-0079 si la course réelle a précisé un point.
- Étapes : section « Pixel screens » ; limites D8 et D9 ; ce qu'ont montré les captures.
- Validation : relecture contre le code et les captures.
- Commit : `docs(ui): document pixel UI screens`

## Phase 3 — Clôture

### ⏳ T3.1 — Validation globale

- Objectif : rejouer la validation globale depuis un build complet, relire le diff entier, chercher les noms interdits.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes : builds, tests reconstruits, parcours du Launcher, `git diff main --stat`, recherche de termes interdits dans le diff.
- Validation : tout vert, arbre propre.
- Commit : `docs(plan): close pixel UI screens`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Internes de FontStashSharp 1.5.7 non lus (DLL) : placement vertical, arrondi des positions, comportement sans glyphe espace. À constater à la capture. | T2.1 |
| O2 | Infobulles et menus contextuels sous `Window.Scale` : non vérifiés. Le sample n'en a pas. | T2.2 |
| O3 | Aperçu de l'éditeur : `UIScreenPreviewBuilder` ne tient aucune police, une famille bitmap y retomberait sans doute. Non exécuté. | T2.2 |

## Hors périmètre

- Navigation, soustractif, fondu sur l'UI : chantiers suivants.
- Toute modification de MGUI, de `UIRoot.UIScale` ou d'`UIScaler`.
- Plusieurs résolutions de conception dans un même projet.
- Tout contenu propre à un jeu dans le moteur.
