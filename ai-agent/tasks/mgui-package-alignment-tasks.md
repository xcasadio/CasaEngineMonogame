# Plan agent IA — Alignement sur la mise à jour du sous-module MGUI

Pas d'analyse séparée : la section « État vérifié du dépôt » en tient lieu.
Les décisions D1 → D7 ci-dessous ont été arbitrées avec l'auteur le 2026-09-13 : **ce plan les applique, il ne les rediscute pas**.
Révision 2 (2026-09-13) : l'auteur a écarté la proposition P1 de la première version (racine en FontStashSharp 1.6.0) au profit d'un épinglage en 1.5.7 dans MGUI (D5 à D7). L'épinglage passe donc en T1.2, avant le renommage des namespaces (T1.3).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Après la mise à jour du sous-module MGUI (commit `72394285`), CasaEngine doit de nouveau restaurer, compiler, passer ses tests et lancer l'éditeur. `MGUI/Directory.Packages.props` a monté ses packages. Le chantier :

- aligne les versions épinglées à la racine sur celles de MGUI ;
- épingle dans MGUI la version de FontStashSharp que CasaEngine utilise ;
- suit le renommage des namespaces de brushes de MGUI ;
- aligne les packages de test et les outils `dotnet-mgcb`.

CasaEngine reste sur la base MGUI `df22af7`. Le sous-module NvgSharp n'est pas modifié.

## État vérifié du dépôt (2026-09-13)

- **Mise à jour de MGUI** : `HEAD` = `72394285` « update MGUI » sur `main` ; le sous-module `MGUI` passe de `21187900` à `df22af7` (`git diff HEAD~1 HEAD -- MGUI`). Dans MGUI, le commit `61fc475` change `Directory.Packages.props` (`git -C MGUI diff 21187900 df22af7 -- Directory.Packages.props`) :
  - `MonoGame.Framework.DesktopGL` et `MonoGame.Content.Builder.Task` : 3.8.4.1 → 3.8.5.1 ;
  - `MonoGame.Extended` et `MonoGame.Extended.Content.Pipeline` : 3.8.* → 6.1.1 ;
  - `Microsoft.NET.Test.Sdk` : 17.11.1 → 18.10.0 ; `xunit.runner.visualstudio` : 2.8.2 → 4.0.0.

  `FontStashSharp.MonoGame` y reste flottant en `1.*` (`MGUI/Directory.Packages.props:13`).
- **Racine avant chantier** (`Directory.Packages.props`) : MonoGame 3.8.4.1, MonoGame.Extended 3.8.0, FontStashSharp.MonoGame 1.5.7, Microsoft.NET.Test.Sdk 17.12.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.1. Les projets de `MGUI/` utilisent `MGUI/Directory.Packages.props`, qui n'importe pas celui de la racine.
- **Conflit MonoGame** : `dotnet restore CasaEngine.Editor.MonoGame.sln` sur `HEAD` échoue en `NU1605`, passage de `MonoGame.Content.Builder.Task` de 3.8.5.1 à 3.8.4.1 dans `CasaEngine` et `CasaEngine.Editor` (chemin `CasaEngine -> MGUI.Core -> MonoGame.Content.Builder.Task (>= 3.8.5.1)`). `MonoGame.Framework.DesktopGL` ne remonte pas en transitif (`PrivateAssets` à `All`, `MGUI/MGUI.Core/MGUI.Core.csproj:26-28`), mais la version de son assembly passe de 3.8.4.1 à 3.8.5.1 (réflexion sur les DLL du cache NuGet).
- **Format des effets** : MonoGame 3.8.5.1 déclare `Effect+MGFXHeader.MGFXVersion` = 11 et `MGFXMinVersion` = 10 ; MonoGame 3.8.4.1 déclare `MGFXVersion` = 10 (réflexion). Les effets compilés par un mgcb plus ancien restent acceptés.
- **MonoGame.Extended** : aucun projet de la racine ne le référence directement ; il arrive par `MGUI.Core` et `MGUI.Shared` (`rg PackageReference --glob "*.csproj"`).
- **Après la modification de T1.1**, sur MGUI `df22af7` : restore des deux solutions sans `NU1605` ; `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.Launcher` et `CasaEngine.Demos` résolvent MonoGame 3.8.5.1 et MonoGame.Extended 6.1.1 (`obj/project.assets.json`) ; `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- **Renommage MGUI** : `dotnet build CasaEngine.MonoGame.sln` donne 8 erreurs `CS0234`, « `Fill_Brushes` n'existe pas dans `MGUI.Core.UI.Brushes` », et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` la même erreur, une fois. Hors `MGUI/`, `rg "Border_Brushes|Fill_Brushes"` trouve 9 lignes `using MGUI.Core.UI.Brushes.Fill_Brushes;`, et aucun contenu XAML ou JSON :
  - `CasaEngine.Demos/Demos/UIOverlay/PauseMenuScreen.cs:5`, `CasaEngine.Demos/Demos/UIOverlay/HudScreen.cs:5`, `CasaEngine.Demos/Demos/DemoUI/DemoInfoScreen.cs:5`, `CasaEngine.Demos/Demos/DemoUI/DemoHintOverlay.cs:4`, `CasaEngine.Demos/Demos/DemoUI/BlendingControlsScreen.cs:5` ;
  - `Projects/CasaEngine.RPGDemo/Scripts/Screens/TitleScreen.cs:5`, `Projects/CasaEngine.RPGDemo/Scripts/Screens/MainHUDScreen.cs:6`, `Projects/CasaEngine.RPGDemo/Scripts/Screens/GameOverScreen.cs:5` ;
  - `CasaEngine.Tests/UI/EditorControlTemplateAssetLoadingTests.cs:5`.
- **Modifications préexistantes de l'auteur**, non indexées :
  - 19 fichiers qui remplacent `MGUI.Core.UI.Brushes.Border_Brushes` et `Fill_Brushes` par `BorderBrushes` et `FillBrushes` : 18 sous `CasaEngine.Editor/`, plus `CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs` ;
  - la référence du sous-module `MGUI`, qui apparaît modifiée (`M MGUI`, voir « Pull de MGUI ») ;
  - fichiers non suivis : `.serena/` et `Projects/SampleProject/.casaeditor/viewport.editor.json`.
- **Plantage de l'éditeur** : `CasaEngine.Editor.exe --project Projects/SampleProject/SampleProject.json --play-smoke`, sur le build Debug obtenu après T1.1, plante à l'initialisation. Exception : `MissingMethodException: 'FontStashSharp.DynamicSpriteFont FontStashSharp.FontSystem.GetFont(Single)'` dans `DebugOverlay..ctor`, appelé par `CasaEngineGame.Initialize()` (`CasaEngine/Framework/Application/CasaEngineGame.cs:444`).
- **Cause du plantage** :
  - `FontStashSharp.MonoGame` est résolu en 1.5.7 (épinglé à la racine) par `CasaEngine`, `CasaEngine.EditorServices`, `CasaEngine.Launcher`, `CasaEngine.Demos`, `GizmoTool`, `NvgSharp.Text.MonoGame`, `CasaEngine.RPGDemo` et `SandBoxGame`.
  - Il est résolu en 1.6.0 (le `1.*` de MGUI ; dossier du cache NuGet daté du 2026-09-11) par `MGUI.FontStashSharp`, `CasaEngine.Editor` et `CasaEngine.Tests`. L'éditeur déploie `FontStashSharp.MonoGame.dll` 1.6.0.
  - `FontStashSharp.Base` et `FontStashSharp.Rasterizers.StbTrueTypeSharp` suivent (1.2.7 contre 1.2.9). Parmi les 18 projets des deux solutions, ce sont les seuls packages résolus en plusieurs versions (`obj/project.assets.json`).
  - Signature relevée par réflexion : `DynamicSpriteFont GetFont(Single)` en 1.5.7, `SpriteFontBase GetFont(Single)` en 1.6.0.
- **Résolution NuGet** : une contrainte `Version="1.5.7"` retient bien 1.5.7. `CasaEngine` résout `FontStashSharp.MonoGame` 1.5.7 alors que 1.5.8, 1.5.9 et 1.6.0 sont dans le cache (`obj/project.assets.json`, `~/.nuget/packages/fontstashsharp.monogame`).
- **FontStashSharp dans le code** :
  - `MGUI.FontStashSharp` range le résultat de `GetFont` dans un `SpriteFontBase` (`MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs:582` et `:589`) ;
  - côté CasaEngine, `CasaEngine/Framework/Rendering/DebugOverlay.cs:32` et `CasaEngine.Demos/Demos/AudioDemo.cs:55` le rangent dans un `DynamicSpriteFont`, que D5 évite de modifier.
- **Pull de MGUI pendant la rédaction du plan** :
  - `git -C MGUI reflog` montre un `pull --rebase` le 2026-09-13 à 21:24, qui avance `develop` de `df22af7` à `6f39e03` : 11 commits du jour (animation, styling, tests) ;
  - `git -C MGUI diff df22af7 6f39e03 -- Directory.Packages.props "*.csproj" "*.props"` est vide ;
  - l'arbre de travail de MGUI est propre (`## develop...origin/develop`) ;
  - sur cette base, la solution éditeur compile sans erreur et la solution moteur n'a que les 8 erreurs `Fill_Brushes`. D6 l'écarte néanmoins de ce chantier.
- **Règles et solution de MGUI** :
  - `MGUI/CLAUDE.md` importe `MGUI/.github/copilot-instructions.md`, qui ne contient que des consignes d'outils shell (`rg`, `rtk`, `fd`, `jq`, `yq`, `ast-grep`). N'y figure aucune règle de branche ni de commit : les règles utilisateur s'appliquent.
  - `MGUI/MGUI.sln` contient `MGUI.Core`, `MGUI.Samples`, `MGUI.Shared`, `MGUI.FontStashSharp`, `MGUI.Tests`, `MGUI.MiniGame`, `MGUI.Rendering.Abstractions`, `MGUI.MonoGame.Integration` et `MGUI.MonoGame.LegacyRenderer`.
- **Outils mgcb** :
  - manifestes de la racine (`jq .tools`) : `.config/dotnet-tools.json` en 3.8.2.1105 ; `CasaEngine/`, `CasaEngine.Demos/`, `CasaEngine.Launcher/`, `CasaEngine.Shaders/`, `Projects/SandBoxGame/` `.config/dotnet-tools.json` en 3.8.1.303 ; `CasaEngine.Editor/.config/dotnet-tools.json` en 3.8.4 ;
  - chacun liste `dotnet-mgcb`, `dotnet-mgcb-editor`, `dotnet-mgcb-editor-linux`, `dotnet-mgcb-editor-windows` et `dotnet-mgcb-editor-mac` ;
  - les 5 outils existent en 3.8.5.1 sur nuget.org (`dotnet package search <id> --exact-match`), mais pas encore dans le cache local ;
  - par réflexion sur `dotnet-mgcb` 3.8.4, `mgcb.dll` déclare les paramètres `quiet` (« Only output content build errors. ») et `rebuild` (« Forces a full rebuild of all content. »). `dotnet mgcb /help` ne liste rien quand la sortie est redirigée : il lève `IOException` sur `Console.BufferWidth`.
- **Contenus mgcb** :
  - `Content/Content.mgcb` existe dans `CasaEngine`, `CasaEngine.Demos`, `CasaEngine.Editor`, `CasaEngine.Launcher`, `CasaEngine.Shaders` et `Projects/SandBoxGame`, et `MonoGame.Content.Builder.Task.targets` (package 3.8.5.1, ligne 51) l'inclut par défaut ;
  - ce `.targets` restaure les outils quand un manifeste change (cible `_RestoreMGCBTool`, dont les entrées comprennent les `.config/dotnet-tools.json`) ;
  - sa cible `RunContentBuilder` exécute mgcb depuis le dossier du projet, avec `$(MonoGameMGCBAdditionalArguments)`, qui vaut `/quiet` par défaut.
- **Tests** :
  - `CasaEngine.Tests` cible `$(WindowsTargetFramework)` (net9.0-windows), et `xunit.runner.visualstudio` 4.0.0 comme `Microsoft.NET.Test.Sdk` 18.10.0 fournissent `build/net8.0` ;
  - `MGUI/MGUI.Tests/obj/project.assets.json` date du 2026-09-04 et résout encore 17.11.1 et 2.8.2 : les nouvelles versions de test n'ont pas encore tourné côté MGUI.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Aligner les packages MonoGame de `Directory.Packages.props` (racine) sur MGUI : `MonoGame.Framework.DesktopGL` et `MonoGame.Content.Builder.Task` en 3.8.5.1, `MonoGame.Extended` et `MonoGame.Extended.Content.Pipeline` en 6.1.1. Source : demande de l'auteur (« corriger les versions »). |
| D2 | Suivre le renommage MGUI `Fill_Brushes` → `FillBrushes` (et `Border_Brushes` → `BorderBrushes`) dans les 9 fichiers restants, et committer ce renommage **avec** les 19 fichiers déjà modifiés par l'auteur, dans un commit distinct de celui des versions. |
| D3 | Aligner les packages de test sur MGUI : `Microsoft.NET.Test.Sdk` en 18.10.0, `xunit.runner.visualstudio` en 4.0.0. `xunit` reste en 2.9.3. |
| D4 | Passer les 5 outils `dotnet-mgcb*` des 7 manifestes de la racine en 3.8.5.1. Le téléchargement depuis nuget.org est accepté. |
| D5 | Épingler `FontStashSharp.MonoGame` en 1.5.7 dans `MGUI/Directory.Packages.props`, au lieu du flottant `1.*`. La racine reste en 1.5.7 et le code de CasaEngine n'est pas modifié. |
| D6 | CasaEngine reste sur la base MGUI `df22af7`. Les 11 commits de `develop` récupérés par le pull du 2026-09-13 (`6f39e03`) n'entrent pas dans ce chantier. |
| D7 | L'épinglage est commité sur `develop` de MGUI, au-dessus de `6f39e03`, puis recopié par cherry-pick sur une branche partant de `df22af7`. CasaEngine référence cette copie. Rien n'est poussé. |

## Règles d'exécution pour l'agent

- **Branche dédiée `fix/mgui-package-versions`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser**, ni CasaEngine ni MGUI. Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` ou `dotnet build CasaEngine.Editor.MonoGame.sln` selon le périmètre) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`. Deux exceptions seulement :
  - les 19 fichiers de D2, indexés en T1.3 ;
  - la référence du sous-module `MGUI`, indexée en T1.2 une fois que le sous-module est sur la copie d'épinglage.
- **Sous-modules** : dans `MGUI/`, seuls les deux commits de T1.2 (D7) sont autorisés, faits avec `git -C MGUI`. `NvgSharp/` n'est pas modifié. Toute autre modification de sous-module passe en ⚠️ Blocked.
- **Smokes de l'éditeur** : relever `git status --porcelain --ignored -- Projects` avant et après chaque `--play-smoke`, et remettre dans son état d'avant tout fichier suivi que le smoke a modifié.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur, aucun `NU1605`, `CS1705` ni `MSB3277`.
- `obj/project.assets.json` des projets des deux solutions : un seul numéro de version par package.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : mêmes tests réussis et en échec que la référence mesurée en T1.3.
- MGUI :
  - `develop` = `6f39e03` suivi du commit d'épinglage ;
  - `casaengine/pin-fontstashsharp` = `df22af7` suivi de sa copie ;
  - la référence du sous-module dans CasaEngine pointe cette copie ;
  - aucune branche n'est poussée.
- Smoke : `CasaEngine.Editor.exe --project <SampleProject.json ou RPGDemo.json> --play-smoke --capture-delay 2 --diagnostics-out <fichier> --screenshot-out <image>` : pas d'exception, rapport `Play smoke:` sans FAIL, capture qui montre le monde rendu.

---

## Phase 1 — Alignement sur MGUI

### ✅ T1.1 — Packages MonoGame alignés sur MGUI (D1)

- Objectif : la racine épingle les mêmes versions MonoGame que MGUI ; le restore ne signale plus de passage à une version antérieure.
- Fichiers : `Directory.Packages.props` ; ce plan et `ai-agent/README.md` (création du plan).
- Étapes :
  1. `MonoGame.Framework.DesktopGL` et `MonoGame.Content.Builder.Task` : 3.8.4.1 → 3.8.5.1 ; `MonoGame.Extended` et `MonoGame.Extended.Content.Pipeline` : 3.8.0 → 6.1.1.
  2. Restore des deux solutions, build de `CasaEngine.Editor.MonoGame.sln`.
- Validation : restore sans `NU1605` ; versions 3.8.5.1 et 6.1.1 dans les `project.assets.json` de `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.Launcher` et `CasaEngine.Demos` ; build de la solution éditeur à 0 erreur. Deux défauts restent hors de cette tâche, car ils existent déjà sur `HEAD` : l'éditeur plante sur FontStashSharp (T1.2), et la solution moteur comme les tests échouent sur `Fill_Brushes` (T1.3).
- Commit : `fix(build): align MonoGame package versions with MGUI`
- Note de validation (2026-09-13) : validation ci-dessus obtenue sur MGUI `df22af7`, avant le pull de MGUI. Les logs des deux builds ne contiennent ni `NU1605`, ni `CS1705`, ni `MSB3277`. Sur MGUI `6f39e03`, la solution éditeur compile aussi sans erreur. Commit fait après l'approbation de la révision 2 du plan.

### ⏳ T1.2 — FontStashSharp épinglé en 1.5.7 dans MGUI (D5, D6, D7)

- Objectif : un seul `FontStashSharp.MonoGame`, en 1.5.7, dans tous les projets des deux solutions ; l'éditeur démarre. `develop` de MGUI porte l'épinglage, et CasaEngine référence sa copie posée sur `df22af7`.
- Fichiers : `MGUI/Directory.Packages.props` (deux commits dans MGUI) ; référence du sous-module `MGUI` ; ce plan.
- Étapes :
  1. Vérifier que l'arbre de travail de MGUI est propre, et que `develop` et `origin/develop` sont toujours en `6f39e03`. Sinon, ⚠️ Blocked : le sous-module a encore bougé.
  2. Sur `develop`, remplacer `Version="1.*"` par `Version="1.5.7"` pour `FontStashSharp.MonoGame` (`MGUI/Directory.Packages.props:13`), soit la même forme de version qu'à la racine.
  3. `dotnet build MGUI/MGUI.sln` et `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.
     - En cas d'erreur ou d'échec, relancer sans l'épinglage pour savoir s'il en est la cause. Si oui, ou si `MGUI.FontStashSharp` ne compile pas en 1.5.7 : ⚠️ Blocked.
     - Si `MGUI.Tests` ne s'exécute pas même sans l'épinglage, le noter et s'en tenir au build.
  4. Commit dans MGUI, sur `develop` : `fix(build): pin FontStashSharp.MonoGame to 1.5.7`.
  5. `git -C MGUI switch -c casaengine/pin-fontstashsharp df22af7`, puis `git -C MGUI cherry-pick` du commit de l'étape 4. Refaire le build et les tests de l'étape 3 sur cette copie, avec la même règle.
  6. Dans CasaEngine, restore et build des deux solutions. Vérifier dans les `project.assets.json` que `FontStashSharp.MonoGame` est en 1.5.7, et que `FontStashSharp.Base` et `FontStashSharp.Rasterizers.StbTrueTypeSharp` n'ont plus qu'une version.
  7. Indexer la référence du sous-module (`git add MGUI`, qui pointe désormais la copie) et ce plan.
- Validation :
  - solution éditeur à 0 erreur ;
  - solution moteur sans autre erreur que les 8 `Fill_Brushes` de T1.3 ;
  - `--play-smoke` sur `Projects/SampleProject/SampleProject.json` : pas d'exception, pas de FAIL, capture qui montre le monde.
- Commit : `fix(build): pin FontStashSharp to 1.5.7 through MGUI`

### ⏳ T1.3 — Namespaces de brushes renommés par MGUI (D2)

- Objectif : plus aucune référence à `MGUI.Core.UI.Brushes.Fill_Brushes` ni `MGUI.Core.UI.Brushes.Border_Brushes` ; les deux solutions et le projet de tests compilent.
- Fichiers : les 9 fichiers listés dans l'état vérifié ; les 19 fichiers déjà modifiés par l'auteur, indexés tels quels.
- Étapes :
  1. Relire `git diff` des 19 fichiers de l'auteur : s'ils contiennent autre chose que ce renommage, ⚠️ Blocked.
  2. Remplacer `using MGUI.Core.UI.Brushes.Fill_Brushes;` par `using MGUI.Core.UI.Brushes.FillBrushes;` dans les 9 fichiers.
  3. `rg "Border_Brushes|Fill_Brushes" --glob "!MGUI/**" --glob "!**/bin/**" --glob "!**/obj/**" .` : aucun résultat.
- Validation : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` s'exécute, et son résultat devient la **référence** des tâches suivantes : nombres de tests réussis, en échec et ignorés, liste des échecs.
- Commit : `fix(mgui): use the renamed FillBrushes and BorderBrushes namespaces`

### ⏳ T1.4 — Packages de test alignés sur MGUI (D3)

- Objectif : `CasaEngine.Tests` tourne avec les versions de test de MGUI.
- Fichiers : `Directory.Packages.props`.
- Étapes :
  1. `Microsoft.NET.Test.Sdk` : 17.12.0 → 18.10.0 ; `xunit.runner.visualstudio` : 3.1.1 → 4.0.0 ; `xunit` inchangé (2.9.3).
  2. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- Validation : mêmes nombres de tests découverts, réussis, en échec et ignorés que la référence de T1.3, et même liste d'échecs. Si un écart vient des nouvelles versions : ⚠️ Blocked.
- Commit : `chore(tests): align test packages with MGUI`

### ⏳ T1.5 — Outils dotnet-mgcb en 3.8.5.1 (D4)

- Objectif : les contenus mgcb de la racine sont compilés par l'outil de même version que le runtime MonoGame.
- Fichiers : `.config/dotnet-tools.json`, `CasaEngine/.config/dotnet-tools.json`, `CasaEngine.Demos/.config/dotnet-tools.json`, `CasaEngine.Editor/.config/dotnet-tools.json`, `CasaEngine.Launcher/.config/dotnet-tools.json`, `CasaEngine.Shaders/.config/dotnet-tools.json`, `Projects/SandBoxGame/.config/dotnet-tools.json`.
- Étapes :
  1. Dans chaque manifeste, passer la `version` des 5 outils en 3.8.5.1, sans toucher aux autres champs.
  2. `dotnet tool restore` dans chaque dossier qui porte un manifeste. Relever ensuite par réflexion les attributs `CommandLineParameter` de `mgcb.dll` 3.8.5.1 : les paramètres `rebuild` et `quiet` doivent exister, sinon ⚠️ Blocked.
  3. Recompiler tous les contenus : build des deux solutions avec `-p:MonoGameMGCBAdditionalArguments="/quiet /rebuild"`, que la cible `RunContentBuilder` transmet à mgcb.
- Validation :
  - `jq` sur les 7 manifestes : 35 entrées en 3.8.5.1 ;
  - builds à 0 erreur, sans erreur mgcb ;
  - `--play-smoke` sur SampleProject sans exception ni FAIL, avec les effets et les polices recompilés chargés.
- Commit : `chore(build): update dotnet-mgcb tools to 3.8.5.1`

### ⏳ T1.6 — Clôture

- Objectif : validation globale, vérification indépendante, plan archivé.
- Fichiers : ce plan (déplacé dans `ai-agent/tasks/archive/`) et `ai-agent/README.md`.
- Étapes :
  1. Dérouler la « Validation globale », avec un smoke sur `Projects/SampleProject/SampleProject.json` et un sur `Projects/RPGDemo/RPGDemo.json`.
  2. Faire vérifier par le sous-agent `verifier` la revendication suivante :
     - les deux solutions compilent sans conflit de version, avec un seul numéro de version par package ;
     - les tests sont identiques à la référence de T1.3 ;
     - l'éditeur fonctionne en `--play-smoke` sur les deux projets ;
     - l'état de MGUI est conforme à D5 à D7.
  3. Archiver ce plan et mettre à jour sa ligne dans `ai-agent/README.md`.
- Validation : verdict `CONFIRMED` ; `git status` sans fichier suivi modifié hors des commits du chantier.
- Commit : `docs(ai-agent): close the MGUI package alignment plan`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun point ouvert à la rédaction du plan. | — |

## Hors périmètre

- Les 11 commits de `develop` postérieurs à `df22af7` (D6). Ils entreront dans CasaEngine à une prochaine mise à jour de MGUI, qui rendra la branche `casaengine/pin-fontstashsharp` inutile.
- Le push dans MGUI de `develop` et de `casaengine/pin-fontstashsharp`. Il doit précéder tout push de CasaEngine : sinon, un autre clone ne trouvera pas le commit que référence le sous-module.
- Les autres versions flottantes de `MGUI/Directory.Packages.props` (`8.*`, `0.*`, `0.26.*` et `17.*`) et les manifestes `dotnet-tools.json` de MGUI (3.8.1.303 et 3.8.4).
- Le sous-module NvgSharp : `NvgSharp/dotnet-tools.json` (3.8.4.1) et la propriété `FontStashSharpVersion` (1.2.8) de `NvgSharp/Directory.Build.props:11`.
- `xunit` (2.9.3 à la racine, 2.9.2 dans MGUI) et les autres packages de la racine.
- Les audits et plans qui citent MonoGame 3.8.4.1 comme contexte daté (`ai-agent/audits/analysis-audio-system.md`, `ai-agent/tasks/audio-system-tasks.md`) : ce sont des constats historiques, laissés tels quels.
