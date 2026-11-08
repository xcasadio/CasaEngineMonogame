# Plan agent IA — Cutscenes pour des écrans minutés

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-08. Un projet sans DLL enchaîne des écrans minutés par ses seules données : un monde lance une cutscene à son chargement, et cette cutscene ouvre un écran, attend un nombre d'images, fait un fondu compté en images, puis change de monde.

Les décisions D1 → D7 ont été arbitrées avec l'auteur le 2026-11-08. Les choix C1 → C9 sont proposés par ce plan : ils sont verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- Un `.world` peut nommer une cutscene, lancée quand le monde commence à jouer.
- Quatre actions de cutscene s'ajoutent :
  - `WaitFrames`, une attente comptée en images ;
  - `FadeScreenFrames`, un fondu d'écran compté en images, à côté de `FadeScreen`, compté en secondes ;
  - `OpenScreen`, qui ouvre un écran de données ;
  - `ChangeWorld`, qui change de monde.
- `ScreenEffectService` gagne une rampe comptée en images, à côté de sa rampe en secondes, qui ne change pas.
- `CutsceneDirector` lève un événement `Ended` à chaque fin d'une lecture.
- ADR-0087 et un nouveau sample, `Projects/TimedScreenSample`.

Le chantier ne livre pas de `fade_screen` d'écran compté en images, ni de ligne de l'inspecteur du monde pour le nouveau champ (voir « Hors périmètre »).

## État vérifié du dépôt (2026-11-08)

Lu sur `main` à `147c3d64`, égal au pointeur du projet consommateur, en lecture seule. La découverte s'est faite en trois parties, et chaque affirmation a été recontrôlée. Les points qui décident ont été relus en session principale.

- **Cutscenes.** Asset JSON `CutsceneAsset`, version 1 (`CasaEngine/Framework/Cutscenes/CutsceneAsset.cs:9,16-18` ; `Serialization/CutsceneAssetJsonSerializer.cs:10-45`). Types d'actions : `Wait`, `MoveTo`, `NavigateTo`, `Sequence`, `Parallel`, `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` et `FadeScreen` (`CutsceneActionTypes.cs:5-15`). Un type inconnu se charge en `UnknownCutsceneActionData`, que le validateur refuse (`CutsceneValidator.cs:136-137`). `Play` met alors l'état `Invalid` et ne démarre rien (`CutsceneDirector.cs:39-47`).
- **Ajouter une action touche six endroits, sans registre** :
  - la classe de données ;
  - une constante de `CutsceneActionTypes` ;
  - `SaveAction` et `LoadAction` du sérialiseur (`CutsceneAssetJsonSerializer.cs:47-113`, `:126-182`) ;
  - `CutsceneValidator.ValidateAction` ;
  - le `switch` de `CutsceneActionCoroutineFactory.ExecuteAction` (`CutsceneActionCoroutineFactory.cs:27-155`), dont le défaut lève (`:152-153`) ;
  - `CutsceneReadOnlyDocumentBuilder.BuildAction` de l'éditeur (`CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs:32-68`), dont le défaut n'affiche que `runtime_type`.
- **Temps des coroutines.**
  - `Wait` compte en secondes (`CutsceneActionCoroutineFactory.cs:31-37`).
  - `WaitForFrames(N)` existe, mais aucune cutscene ne s'en sert (`Scripting/Coroutines/WaitForFrames.cs:3-20`).
  - Une instruction cédée à la mise à jour U est testée pour la première fois à U+1. `WaitForFrames(N)` se termine donc à U+N, et la coroutine reprend dans cette même mise à jour (`CoroutineManager.cs:186-266`). `WaitForFrames(0)` prend une mise à jour.
  - `StartCoroutine` ne fait que mettre en attente. La coroutine tourne à la mise à jour suivante du `CoroutineManager` (`CoroutineManager.cs:36-48`, `:131-134`).
- **Rampe de fondu.**
  - `ScreenEffectService` n'a qu'une rampe en secondes (`CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs:30-31`, `:118-161`, `:164-186`). Chaque canal est arrondi par `MathF.Round`, puis borné (`:188-192`).
  - `Layer` est une propriété persistante, `BelowUI` par défaut (`:67`), que `Clear` ne touche pas (`:98-102`).
  - `ScreenEffectComponent.Update` avance la rampe une fois par `Game.Update` (`Application/Components/ScreenEffectComponent.cs:63-65`).
  - Le service et son composant appartiennent au jeu, pas au monde. Un changement de monde ne les remet pas à zéro (`World.cs:125-145`).
- **Les deux fondus existants.**
  - L'action `FadeScreen` des cutscenes ne prend que `r,g,b` (opaque), `duration_seconds` et `blend_mode` (`FadeScreenCutsceneActionData.cs:11-29`). Elle ne règle jamais `Layer`. Elle lance la rampe au premier test de son instruction, donc à la mise à jour suivante (`CutsceneActionCoroutineFactory.cs:352-389`).
  - Le `fade_screen` des écrans règle `Layer`, puis lance la rampe RGBA (`Application/GameManager.cs:253-260`). Ses champs `from` et `to` valent `[r, g, b]` ou `[r, g, b, a]`, et ses énumérations s'écrivent par leur nom exact (`UI/MGUI/UIScreenAsset.cs:283-338`).
- **Ordre d'une image.** `CasaEngineGame.Update` met à jour les UI des vues, puis appelle `GameManager.UpdateWorld` (`CasaEngineGame.cs:616`), où tournent les coroutines du monde. Viennent ensuite les composants, dont `ScreenEffectComponent` (`:633`). Un fondu lancé par une cutscene à la mise à jour U avance donc d'un pas dès U.
- **Chargement d'un monde.** Dans `GameManager.UpdateWorld` (`GameManager.cs:95-168`), dans cet ordre :
  1. `Clear` de l'ancien monde, qui arrête son directeur de cutscene (`World.cs:134` → `WorldRuntimeSystems.cs:27-32`) ;
  2. le chargement ;
  3. `LoadContent` et les vues ;
  4. `BeginPlay` (`:153`) ;
  5. `WorldLoaded` et `OpenFirstScreen` (`:158`) ;
  6. `World.Update`, dans le même appel (`:167`).

  `World.BeginPlay` ne fait rien quand `ExecutionPolicy.RunBeginPlay` est faux (`World.cs:520-525`). Il est faux en aperçu d'éditeur et vrai en simulation (`GameplayExecutionPolicy.cs:47,61`). `BeginPlay` lance déjà l'asset nommé par `gameplay_mode_asset_id` (`World.cs:537-552`).
- **Aucun lancement de cutscene sans code.** Seules deux démos appellent `CutsceneDirector.Play` (`CasaEngine.Demos/Demos/CutsceneMoveToDemo.cs:113`, `CutsceneNavigateToDemo.cs:118`). Aucun champ du `.world` ne nomme une cutscene.
- **Écrans.**
  - `OpenFirstScreen` n'ouvre que `FirstScreenName`, une seule fois par partie, et rien sous gestion externe des vues (`GameManager.cs:178-190`). Les mondes suivants n'ont aucun moyen d'ouvrir un écran sans code, comme l'écrit le README de `Projects/BufferedResolutionSample`.
  - `PushNewScreen` et `CreateDataDrivenScreen` sont privés (`:205`, `:218`). `IsScreenAsset` et `IsWorldAsset` sont internes (`:269`, `:271`).
  - `SetWorldToLoad(string)` est public (`:333`). Un nom inconnu lève une exception à l'image suivante (`:116-127`). Les scripts s'en servent déjà, simulation de l'éditeur comprise (`Projects/CasaEngine.RPGDemo/Scripts/ScriptWorld.cs:39`).
- **Directeur.**
  - API : `Play`, `Stop`, `IsPlaying` et `GetDebugSnapshot`, sans aucun événement (`CutsceneDirector.cs:23-98`).
  - États : `Idle`, `Playing`, `Completed`, `Stopped`, `Invalid` et `Failed` (`CutsceneRuntimeState.cs:3-11`). `MarkRuntimeFailure` passe en `Failed` (`CutsceneDirector.cs:131-138`).
  - Une exception levée par une action est attrapée par `CoroutineManager.Update`, qui arrête la coroutine (`CoroutineManager.cs:148-158`). `MoveTo` en lève une sur entité absente ou délai dépassé. L'état reste alors `Playing`, alors que `IsPlaying` devient faux.
- **Assets.** ADR-0037 : une cutscene se lit en copie par `LoadCopy<CutsceneAsset>` (`Assets/AssetContentManager.cs:98-101`), qui lève sur un id inconnu (`:224-229`).
- **Format `.world`.**
  - `World.Load` lit des clés en snake_case. Il lit les ids d'asset avec `ContainsKey` (`World.cs:1014-1027`). Le bloc d'ADR-0086 suit la règle « absent veut dire absent » (`:1034-1035`).
  - Le seul écrivain est `EditorEntityJsonSerializer.SaveWorld`, qui n'écrit le bloc d'ADR-0086 que s'il existe (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:42-57`).
- **Tests à suivre.**
  - Cutscenes : `CasaEngine.Tests/Cutscenes/CutsceneDirectorTests.cs`, `CutsceneAssetJsonSerializerTests.cs`, `EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilderTests.cs`.
  - Jeu construit sans initialisation : `Cutscenes/CutsceneActionCoroutineFactorySoundAssetHandleTests.cs` et `Rendering/ScreenEffects/ScreenEffectComponentViewSizeTests.cs`.
  - Fondu : `Rendering/ScreenEffects/ScreenEffectServiceTests.cs` et `FadeScreenCutsceneActionTests.cs`.
  - Écrans : `UI/FirstScreenTests.cs` (`GameManager(null)`, vue factice) et `UI/ScreenActionContextWiringTests.cs` (catalogue rempli par `AssetCatalog.AddInternal`).
  - Champ du `.world` : `Scene/WorldVirtualResolutionTests.cs` (aller-retour, clé absente, rechargement).
- **ADR.** Prochain numéro libre : 0087. ADR liées : 0020 (cutscenes V1), 0037 (assets en copie), 0078 (écrans de données, `FirstScreenName`), 0081 (fondus des écrans) et 0086 (bloc du `.world`).
- **Arbre de travail.** Branche `chantier/timed-screen-cutscenes`, créée depuis `main` `147c3d64`, égal au pointeur. Aucun changement préexistant.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-08.**

| Réf | Décision |
|---|---|
| D1 | Contenu demandé : <ul><li>un champ du `.world` qui lance une cutscene au chargement ;</li><li>les actions `WaitFrames`, `OpenScreen` et `ChangeWorld` ;</li><li>un fondu dont la durée est un nombre d'images, à côté de `FadeScreen` ;</li><li>un événement de fin ;</li><li>une ADR ;</li><li>un test qui montre qu'un fondu de N images atteint sa valeur finale exactement à l'image N, et qu'une attente de N images dure N images ;</li><li>un sample où un monde ouvre un écran, attend N images, fait un fondu de N images, puis change de monde, sans code C# de jeu.</li></ul> |
| D2 | Le fondu en images prend `from` et `to` (`[r, g, b]` ou `[r, g, b, a]`), `frames`, `layer` et `blend_mode`, comme le `fade_screen` des écrans (ADR-0081). Son résultat ne dépend pas de l'état laissé par un fondu précédent, et il peut passer au-dessus de l'UI |
| D3 | `Ended` est levé à chaque fin d'une lecture démarrée, avec l'état final : `Completed`, `Stopped` ou `Failed`. Une exception levée par une action reste hors chantier et est écrite comme limite |
| D4 | Le sample est neuf : `Projects/TimedScreenSample`. `Projects/BufferedResolutionSample` ne change pas |
| D5 | Le format change de façon additive : un ancien `.world` ou `.cutscene` se charge et se joue à l'identique. ADR-0087 enregistre la décision |
| D6 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur. Les données sont synthétiques |
| D7 | Les changements restent simples et optimisés, comme dans un moteur moderne : pas d'allocation par image, aucun système parallèle aux cutscenes |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **Une image est une mise à jour du jeu** (`Game.Update`), qui fait tourner le monde, puis les composants. Les actions en images ignorent le temps écoulé et `TimeScale` |
| C2 | **Contrat de temps**, prouvé par les tests : <ul><li>`WaitFrames` de N images, atteinte à la mise à jour U : l'action suivante tourne à U+N. Avec N = 0, elle tourne à U.</li><li>`FadeScreenFrames` de N images, atteinte à U : elle règle `layer` et lance la rampe dès U, pas à U+1 comme `FadeScreen`. Pour k de 1 à N, la valeur dessinée à U+k-1 est `Lerp(from, to, k/N)`, arrondie comme la rampe en secondes. `from` est l'image 0 et n'est jamais dessiné. La cible est dessinée exactement à partir de U+N-1. L'action suivante tourne à U+N. Avec N = 0, la cible est posée tout de suite et l'action suivante tourne à U.</li><li>La cutscene de démarrage d'un monde joue sa première action dans la mise à jour qui charge ce monde, après `OpenFirstScreen` (`GameManager.cs:158`, `:167`).</li></ul> |
| C3 | **Service.** `ScreenEffectService.StartFadeFrames(fromR, fromG, fromB, fromA, toR, toG, toB, toA, int frames, SpriteBlendMode blend)`. Chaque appel d'`Update` avance d'un pas, quel que soit le temps écoulé. `StartFade`, `SetOverlay` et `Clear` annulent une rampe en images comme une rampe en secondes. Les surcharges existantes ne changent pas. Aucune allocation |
| C4 | **Champ du `.world` : `startup_cutscene_asset_id`**, l'id d'un asset de cutscene, exposé par `World.StartupCutsceneAssetId`. <ul><li>Sans la clé, la valeur est `Guid.Empty`, même au rechargement : absent veut dire absent, comme pour ADR-0086. La clé n'est écrite que si un id est posé.</li><li>La cutscene est lue par `LoadCopy<CutsceneAsset>` (ADR-0037), puis lancée à la fin de `World.BeginPlay`, donc seulement si `RunBeginPlay` est vrai : en jeu et en simulation d'éditeur, pas en aperçu.</li><li>Un id inconnu lève une `InvalidOperationException` qui nomme le monde et l'id, comme un monde absent du catalogue.</li><li>Une cutscene invalide est signalée par une erreur loguée une fois, avec le monde et les messages du validateur. Le monde tourne sans elle.</li></ul> |
| C5 | **`OpenScreen`** : `{"type": "OpenScreen", "target": "<id ou nom d'écran>"}`. <ul><li>L'action pousse l'écran de données sur la vue UI active, comme `FirstScreenName` (ADR-0078). Elle passe par une méthode interne de `GameManager`, testable comme `OpenFirstScreen`, avec la politique et la fabrique en paramètres. Elle ne touche pas à l'ouverture unique de `FirstScreenName`.</li><li>Sous gestion externe des vues, donc dans l'éditeur, elle n'ouvre rien, le logue en information, et se termine.</li><li>Un écran absent du catalogue, l'absence de vue UI active ou l'absence de jeu font échouer la lecture : `MarkRuntimeFailure`, état `Failed`, raison loguée une fois.</li></ul> |
| C6 | **`ChangeWorld`** : `{"type": "ChangeWorld", "target": "<nom de fichier du monde>"}`. <ul><li>L'action vérifie le monde par `IsWorldAsset`, appelle `SetWorldToLoad(string)`, puis se termine aussitôt.</li><li>Le monde change à la mise à jour suivante. Si la cutscene joue encore à ce moment, `World.Clear` l'arrête et `Ended` est levé avec `Stopped`.</li><li>Un monde absent du catalogue ou l'absence de jeu font échouer la lecture.</li><li>Elle n'a pas de garde de politique : les scripts changent déjà de monde par nom en simulation.</li></ul> |
| C7 | **Champs requis.** `frames`, `from`, `to`, `layer`, `blend_mode` et `target` sont requis. Un champ absent ou mal formé est une erreur du validateur, qui nomme l'action et le champ. Un `frames` négatif ou non entier est une erreur. Les classes de données gardent les champs requis en types nullables, comme `UIScreenActionData` |
| C8 | **`Ended`** : `public event EventHandler<CutsceneEndedEventArgs> Ended`, dont les arguments portent l'état final et l'asset. <ul><li>Il est levé une fois par lecture démarrée : à la fin de `RunAsset`, avec `Completed` ou `Failed`, et par un `Stop` qui arrête une lecture en cours, avec `Stopped`. Ce dernier cas comprend `World.Clear` et un `Play` qui remplace une lecture en cours.</li><li>Un `Play` d'asset invalide ne démarre rien et ne lève rien.</li><li>Un indicateur par lecture garantit une seule levée.</li><li>Une exception levée par un abonné remonte comme toute exception de coroutine : `CoroutineManager.Update` la logue.</li></ul> |
| C9 | **Éditeur.** `CutsceneReadOnlyDocumentBuilder` affiche les champs des quatre actions, et `SaveWorld` écrit le nouveau champ. L'inspecteur du monde ne gagne pas de ligne |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/timed-screen-cutscenes`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin :
  1. lancer la validation indiquée ;
  2. remplacer l'icône par `✅`, `🧪` ou `⚠️` ;
  3. ajouter une courte note de validation sous la tâche ;
  4. **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur.
- **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, jamais avec `--no-build`.
- Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds : la rampe en images et les instructions des actions tournent à chaque image ;
  - le runtime ne dépend pas de l'éditeur ;
  - sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression.**
  - Les tests existants des cutscenes, de `FadeScreen`, du `fade_screen` des écrans, de `FirstScreenName` et des `.world` restent verts sans être modifiés.
  - `Projects/BufferedResolutionSample` et `Projects/PixelUiSample` ne sont pas touchés.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/TimedScreenSample/TimedScreenSample.json`.

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0087

- Objectif : committer ce plan et sa ligne d'index. Écrire ADR-0087, qui reprend D1 à D7 et C1 à C9, ainsi que la limite de D3.
- Fichiers :
  - `ai-agent/tasks/timed-screen-cutscenes-tasks.md` ;
  - `ai-agent/README.md` ;
  - `docs/decisions/0087-a-world-starts-a-cutscene-and-cutscenes-count-frames.md` ;
  - `docs/decisions/README.md`.
- Étapes :
  1. ADR selon `docs/decisions/template.md`, statut Accepted à la date de l'approbation, avec la source de chaque fait.
  2. Ligne d'index de l'ADR.
- Validation : relecture de l'ADR contre ce plan. Contrôle D6 par une recherche des noms interdits, liste tenue hors du dépôt.
- Commit : `docs(cutscenes): plan timed screen cutscenes and record ADR-0087`
- Note : plan relu READY par un plan-verifier neuf, approuvé par l’auteur le 2026-11-08 (« Oui, lance-le »). ADR-0087 relue contre D1 à D7 et C1 à C9. Contrôle D6 fait.

---

## Phase 1 — Runtime

### ✅ T1.1 — Rampe de fondu comptée en images

- Objectif : `ScreenEffectService.StartFadeFrames` selon C3.
- Fichiers :
  - `CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs` ;
  - `CasaEngine.Tests/Rendering/ScreenEffects/ScreenEffectServiceTests.cs`.
- Étapes :
  1. Champs du nombre de pas et du pas courant, et une branche dans `Update`. Le pas k donne `Lerp(from, to, k/N)` avec le `Lerp` existant, et le pas N donne la cible exacte.
  2. `IsFading` reste vrai jusqu'au pas N, inclus.
  3. Avec N ≤ 0, la cible est posée tout de suite, comme une durée nulle en secondes.
  4. `StartFade`, `SetOverlay` et `Clear` annulent la rampe en images.
  5. Doc XML.
- Validation : tests, avec les valeurs attendues calculées dans le test par la formule de C2.
  - Pour N = 4, de `(0,0,0,255)` à `(0,0,0,0)` : la valeur après chaque `Update` et la cible exacte au 4e. `IsFading` est vrai après les pas 1 à 3 et faux après le 4e.
  - Les mêmes valeurs avec un temps écoulé de 0 et de 10 s.
  - N = 1 et N = 0.
  - `StartFade` en secondes, `SetOverlay` et `Clear` annulent une rampe en images.
  - Les tests existants du service passent sans être modifiés.
  - Build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(rendering): ramp a screen fade over a number of frames`
- Note : builds des deux `.sln` à 0 erreur ; `CasaEngine.Tests` 4915 verts, 0 échec. Les 8 nouvelles méthodes de test (10 cas) prouvent : N = 4 de (0,0,0,255) à alpha 0, valeur attendue (formule de C2) après chaque `Update` et cible exacte au 4e, avec un temps écoulé de 0 et de 10 s ; les quatre canaux ; N = 1 ; N = 0 et négatif ; `StartFade`, `SetOverlay` et `Clear` annulent la rampe en images ; aucune allocation par `Update`.

### ⏳ T1.2 — Actions `WaitFrames` et `FadeScreenFrames`

- Objectif : les deux actions, selon C2 et C7, sur les six endroits listés dans l'état vérifié.
- Fichiers :
  - `CasaEngine/Framework/Cutscenes/WaitFramesCutsceneActionData.cs` (nouveau) ;
  - `CasaEngine/Framework/Cutscenes/FadeScreenFramesCutsceneActionData.cs` (nouveau) ;
  - `CutsceneActionTypes.cs`, `Serialization/CutsceneAssetJsonSerializer.cs`, `CutsceneValidator.cs` et `CutsceneActionCoroutineFactory.cs` ;
  - `CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs` ;
  - `CasaEngine.Tests/Cutscenes/FrameCutsceneActionsTests.cs` (nouveau) ;
  - `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilderTests.cs`.
- Étapes :
  1. JSON :
     - `{"type": "WaitFrames", "frames": N}` ;
     - `{"type": "FadeScreenFrames", "from": [r,g,b,a], "to": [r,g,b,a], "frames": N, "layer": "AboveUI", "blend_mode": "AlphaBlend"}`.

     Les couleurs et les énumérations se lisent comme dans le `fade_screen` des écrans.
  2. Comportement de `WaitFrames` : avec N > 0, l'action cède `new WaitForFrames(N)`. Avec N = 0, elle se termine aussitôt.
  3. Comportement de `FadeScreenFrames` :
     - sans `ScreenEffectService`, l'action se termine aussitôt, comme `FadeScreen` ;
     - sinon, elle règle `Layer`, appelle `StartFadeFrames`, puis cède `WaitForFrames(N)` si N > 0.
  4. Cas de l'éditeur dans le builder.
- Validation :
  - aller-retour JSON des deux actions ;
  - une erreur du validateur pour chaque champ absent, couleur mal formée, nom d'énumération inconnu, `frames` négatif ou non entier ;
  - test de temps selon C2. Il fait tourner, à chaque image, la mise à jour des coroutines du monde, puis celle du service, avec un jeu construit sans initialisation, comme dans les tests de l'état vérifié. La séquence `FadeScreenFrames` (N = 4), `WaitFrames` (3), puis une action témoin, donne :
    - la valeur dessinée à chaque mise à jour ;
    - la cible à la mise à jour 4, où la mise à jour 1 est celle de la première action ;
    - l'attente de la mise à jour 5 à la 7 ;
    - l'action témoin à la mise à jour 8.
  - `WaitFrames` (0) suivie d'une action témoin : les deux tournent dans la même mise à jour ;
  - `layer` est posé avant la première valeur dessinée ;
  - le builder montre les champs ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(cutscenes): wait and fade the screen over a number of frames`

### ⏳ T1.3 — Événement `Ended`

- Objectif : `CutsceneDirector.Ended` selon C8.
- Fichiers :
  - `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs` ;
  - `CasaEngine/Framework/Cutscenes/CutsceneEndedEventArgs.cs` (nouveau) ;
  - `CasaEngine.Tests/Cutscenes/CutsceneDirectorTests.cs`.
- Validation : tests.
  - Fin normale : un seul `Ended`, avec `Completed`.
  - `Stop` : un seul `Ended`, avec `Stopped`.
  - `RuntimeSystems.Clear`, comme dans `World.Clear` : `Stopped`.
  - `NavigateTo` vers une entité absente, chemin d'échec existant : un seul `Ended`, avec `Failed`.
  - `Play` pendant une lecture : `Stopped` pour la première, puis `Completed` pour la seconde.
  - Asset invalide : aucun `Ended`.
  - `Stop` après la fin : aucun `Ended`.
  - Dans l'abonné, `GetDebugSnapshot().State` vaut déjà l'état des arguments.
  - Build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(cutscenes): raise an event when a cutscene ends`

### ⏳ T1.4 — Actions `OpenScreen` et `ChangeWorld`

- Objectif : les deux actions selon C5, C6 et C7.
- Fichiers :
  - `CasaEngine/Framework/Cutscenes/OpenScreenCutsceneActionData.cs` (nouveau) ;
  - `CasaEngine/Framework/Cutscenes/ChangeWorldCutsceneActionData.cs` (nouveau) ;
  - `CutsceneActionTypes.cs`, `Serialization/CutsceneAssetJsonSerializer.cs`, `CutsceneValidator.cs` et `CutsceneActionCoroutineFactory.cs` ;
  - `CasaEngine/Framework/Application/GameManager.cs` ;
  - `CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs` ;
  - `CasaEngine.Tests/Cutscenes/ScreenAndWorldCutsceneActionsTests.cs` (nouveau) ;
  - `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilderTests.cs`.
- Étapes :
  1. Méthode interne de `GameManager` qui ouvre un écran sur la vue UI active. Elle prend la politique et la fabrique en paramètres, comme `OpenFirstScreen`. Elle rend la raison d'un échec, et un enrobage passe `_game.ExecutionPolicy` et `CreateDataDrivenScreen`.
  2. Cas de la fabrique d'actions, avec échec par `MarkRuntimeFailure` et log unique de la raison.
  3. Cas de l'éditeur dans le builder.
- Validation :
  - aller-retour JSON ;
  - erreur du validateur pour une `target` absente ou vide ;
  - méthode de `GameManager`, avec une vue factice et `AssetCatalog.AddInternal` :
    - en jeu, l'écran est poussé sur la vue UI active ;
    - sous la politique de l'éditeur, rien n'est ouvert et aucune erreur n'est rendue ;
    - sans vue UI active, la raison de l'échec est rendue ;
    - un écran absent du catalogue rend la raison de l'échec ;
    - après une ouverture par cutscene, `OpenFirstScreen` ouvre encore `FirstScreenName` ;
  - fabrique :
    - sans jeu, `OpenScreen` et `ChangeWorld` finissent en `Failed`, avec `Ended` ;
    - `ChangeWorld` vers un monde absent du catalogue finit en `Failed` ;
    - vers un monde du catalogue, le monde demandé est en attente dans `GameManager` et la cutscene continue dans la même mise à jour ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(cutscenes): open a screen and change the world from a cutscene`

### ⏳ T1.5 — Cutscene de démarrage d'un monde

- Objectif : le champ `startup_cutscene_asset_id` selon C4.
- Fichiers :
  - `CasaEngine/Framework/Scene/World/World.cs` ;
  - `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` ;
  - `CasaEngine.Tests/Scene/WorldStartupCutsceneTests.cs` (nouveau).
- Validation : tests.
  - Aller-retour `SaveWorld` puis `World.Load`.
  - Sans la clé, la valeur est `Guid.Empty`, et un rechargement sans la clé efface l'id précédent.
  - Un monde sans id s'écrit sans la clé.
  - `BeginPlay` sous `Runtime` joue l'asset : l'instantané est `Playing`, avec son id.
  - Sous `EditorPreview`, rien n'est joué.
  - Un id inconnu lève une exception qui nomme le monde et l'id.
  - Une cutscene invalide : pas d'exception, le directeur est `Invalid`, une erreur est loguée.
  - La première action tourne au premier `World.Update` après `BeginPlay`.
  - Build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(world): start a cutscene when a world begins play`

---

## Phase 2 — Sample et documentation

### ⏳ T2.1 — Sample `Projects/TimedScreenSample`

- Objectif : montrer D1 sans code C# de jeu. Le second monde ouvre aussi son écran, ce que seul `FirstScreenName` permettait avant.
- Fichiers :
  - `Projects/TimedScreenSample/` : projet, `AssetInfos.json`, `FirstWorld.world`, `SecondWorld.world`, `Cutscenes/first.cutscene`, `Cutscenes/second.cutscene`, `Screens/` (un `.uiscreen` et un `.xaml` par monde), `Fonts/` (copie de la police de `Projects/BufferedResolutionSample`) et `README.md` ;
  - un test de données dans `CasaEngine.Tests`, sur le modèle de `ProjectScreenAssetTests`.
- Contenu :
  - Le projet n'a pas de DLL. `FirstWorldLoaded` vaut `FirstWorld.world`, et `FirstScreenName` est vide : les deux écrans viennent des cutscenes.
  - `FirstWorld` a un bloc `virtual_resolution` de 200x150 en `BufferedFit`. `SecondWorld` n'a pas de bloc : le 320x180 `IntegerFit` du projet s'applique. Le fondu au-dessus de l'UI est ainsi montré sur les deux chemins de présentation.
  - Cutscene de chaque monde, en séquence :
    1. `FadeScreenFrames` de 0 image vers le noir opaque, au-dessus de l'UI ;
    2. `OpenScreen` ;
    3. `FadeScreenFrames` de 30 images, du noir opaque au noir transparent ;
    4. `WaitFrames` de 90 images ;
    5. `FadeScreenFrames` de 30 images vers le noir opaque ;
    6. `ChangeWorld` vers l'autre monde, ce qui fait boucler le sample.
- Validation :
  - le test de données vérifie que chaque `.cutscene` se valide sans erreur, et que chaque `startup_cutscene_asset_id` et chaque `target` est au catalogue du sample ;
  - lancement au Launcher, avec des captures prises par un outil hors dépôt :
    - l'écran du premier monde ;
    - le noir entre les mondes ;
    - l'écran du second monde ;
    - le retour au premier ;
    - une fenêtre de taille inchangée ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(samples): add a sample whose worlds chain timed screens from data`

### ⏳ T2.2 — Documentation

- Objectif : décrire le champ, les quatre actions, le contrat de temps de C2, `Ended` et la limite de D3.
- Fichiers :
  - `docs/engine/data-driven-screens.md`, en anglais : section complète ;
  - `docs/engine/screen-effects.md` et `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` : court paragraphe dans la langue du fichier, avec un renvoi vers la section.
- Validation : relecture contre le code livré ; chaque nom de champ et de type est cité tel qu'il est dans le code.
- Commit : `docs(engine): describe timed screen cutscenes`

---

## Phase 3 — Vérification et recette

### ⏳ T3.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur la branche entière. L'affirmation à vérifier :
  - C2, prouvé par les tests et par le sample ;
  - C4 à C8 ;
  - D5 : un ancien `.world` ou `.cutscene` se charge à l'identique, et les tests existants restent verts sans être modifiés ;
  - D6.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of timed screen cutscenes`

### ⏳ T3.2 — Recette de l'auteur

- Objectif : l'auteur lance le sample au Launcher et l'accepte.
- Validation : accord de l'auteur, écrit ici, avec ses écarts éventuels. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the accepted check of the timed screen sample`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | `CasaEngineGame.Update` met à jour les UI des vues avant `GameManager.UpdateWorld` (`CasaEngineGame.cs:587-616`). Un écran poussé par une cutscene, comme celui de `FirstScreenName`, n'est donc mis à jour qu'à l'image suivante. T2.1 mesure s'il est dessiné vide pendant une image sous le noir. Le constat est écrit ici, sans correction dans ce plan | T2.1 |

## Hors périmètre

- Le `fade_screen` des écrans compté en images : les écrans gardent les secondes.
- Une ligne de l'inspecteur du monde pour `startup_cutscene_asset_id`.
- Une exception levée par une action : l'état reste `Playing` et `Ended` n'est pas levé. C'est le comportement existant, écrit comme limite dans l'ADR et la doc.
- L'action `FadeScreen` en secondes, inchangée : rampe lancée à U+1, calque non réglé.
- `TimeScale` pour les actions en images.
- Une garde de `ChangeWorld` en simulation d'éditeur.
- Les actions de sprite, l'attente d'une touche, la boucle et la vidéo, qui relèvent des chantiers suivants.
