# Plan agent IA — Objets script par nom complet, depuis l'assembly de gameplay seulement

Plan d'exécution de la tranche R0 (partie moteur) du chantier du dépôt parent `docs/plan-restructuration-dll.md` (§4, révision 4).
Les décisions D1 → D8 ci-dessous ont été arbitrées avec l'auteur le 2026-10-16 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

Approuvé par l'auteur le 2026-10-16 (« fais toutes les tâches », parent D-R-9). La branche `chantier/script-types-full-name` a été créée depuis `main` du moteur `781d3527`, après la fusion d'E20 (ce qui ferme O1).

## Objectif

Quand le moteur crée un objet « script » à partir d'une chaîne lue dans les données — le proxy d'une entité, le proxy d'un monde, le
contrôleur du joueur, le mode de jeu, le view model d'aperçu d'un écran —, il ne cherche le type que dans l'assembly de gameplay
enregistrée, par son nom complet exact. Un nom introuvable, ambigu ou du mauvais genre lève une exception dédiée qui dit tout (clé, valeur,
fichier, assembly, indice), et cette exception traverse les filets qui rattrapent aujourd'hui toute erreur de création. Les types du moteur
que des données nomment aujourd'hui par chaîne (`PlayerController`, `ScriptArcBallCamera`) sont créés par le code. Les composants du moteur
(`"type"` des JSON) restent sur le chemin actuel.

## État vérifié du dépôt (2026-10-16, sous-module au commit `781d3527`, branche `chantier/e20-heros`)

Relevés en lecture seule par `git show` et `git grep` (preuves : scratchpad du dépôt parent, `dll-structure/r0-engine/`).
- `CasaEngine/Framework/Assets/ElementFactory.cs:22-26` : `Create<T>(name)` = `FindTypeByName` puis `Activator.CreateInstance(type) as T` ;
  `:70-86` assemblys candidates = l'AppDomain sauf les collectibles, puis `_scriptAssemblies` (`:15`) ; `:88-92` cache par `Type.Name`, sans
  casse, premier trouvé ; `:19` invalidation à chaque `AppDomain.AssemblyLoad` et à chaque `Register/UnregisterScriptAssembly` (`:40-61`) ;
  nom absent : `ArgumentNullException` (`ElementFactoryReloadTests.cs:66-67`) ; mauvais genre : `null` en silence.
- Créations d'objets script depuis une chaîne : `Entity.cs:583` (`script_class_name`) puis `InitializePrivate` `:210-214` (la comparaison `:212`
  sur `GetType().Name`) ; `World.cs:938` puis `CreateGameplayProxy` `:325-338` ; `PlayerStartupSettings.cs:18` (`player_controller_class`)
  puis `World.cs:440` ; `GameplayModeAsset.cs:18,25` (`mode_class_name`, appelé par `World.cs:504`) ; `UIScreenDesignTimeDataLoader.cs:87-113`
  (`view_model_type`, éditeur, ne lève jamais : résultat `Fail`, `:47-49`, `:110-118`).
- Composants, hors du chantier : `Entity.cs:589,594`, `SceneComponent.cs:442` ; « Add Component » de l'éditeur `EntityDetailsPanel.cs:1233-1250`.
- Assembly de gameplay : runtime `CasaEngineGame.cs:387` → `ProjectSettingsHelper.cs:57-60` → `AssemblyManager.cs:13,24` (`Assembly.LoadFile`),
  jamais enregistrée ; éditeur `GameEditor.cs:326` remplace le chargeur par `EditorScriptAssemblyService.LoadShadowCopy` (contexte collectible,
  `ScriptAssemblyHost.cs:21-22,53-67`), qui enregistre (`EditorScriptAssemblyService.cs:48`) et désenregistre (`:66`) ; le rechargement passe
  par `AssemblyManager.Load` (`EditorScriptReloadCoordinator.cs:89`).
- Fichier d'origine d'un objet : `AssetContentManager.LoadNew` pose `AssetId` et `FileName` (`:237-239`) ; le clone garde `FileName`, reçoit un
  `Id` neuf et perd `AssetId` (`ObjectBase.cs:37-42`) ; les entités de monde à asset sont `LoadCopy<Entity>(AssetId).Clone()`
  (`EntityReference.cs:58,75`) ; `World.InternalAddEntities` appelle `entity.Initialize()` avant `InitializeWithWorld(this)` (`World.cs:788-789`)
  sous un `catch (Exception e) { Logs.WriteException(e); }` (`:784-801`), qui couvre les entités en ligne, les références de préfab
  (`:297-304`, `:471-475`) et le pion par défaut (`:380`, `:384`).
- Types du moteur nommés par chaîne : `PlayerStartupSettings.cs:9` (défaut `nameof(PlayerController)`), `Projects/RPGDemo/RpgGameMode.gameMode:5`
  (`"PlayerController"`), `CasaEngine.Demos/Demo.cs:29` et `Projects/SandBoxGame/SandBoxGame.cs:53` (`ScriptArcBallCamera`,
  `CasaEngine/Framework/Scripting/ScriptArcBallCamera.cs:25`) ; RPGDemo : 8 noms courts de ses scripts dans 6 fichiers de données et
  `RPGDemo/Weapons/ThrowableWeapon.cs:34`. Le setter de `Entity.GameplayProxy` est privé (`Entity.cs:89`).
- Tests touchés : `GameplayProxyStateSerializationTests` (9 sites), `GameplayModeRunnerTests` (type imbriqué), `LocalMultiplayerTests:149-181`,
  `UIScreenPreviewBuilderDesignTimeDataTests` (view model imbriqué), `ElementFactoryReloadTests` (`"TestScriptProxy"`) ; restent verts :
  `TransformComponentTests:27`, `MigratedCollisionAssetTests:40`. `CasaEngine.Tests/Scripting/EditorScriptAssemblyServiceTests.cs` existe.
- Docs du nom court : `docs/.../screen-authoring-conventions.md:229,237-242`, `gameplay-csproj-scaffolding.md:162-165`, `gameplay-mode.md:28,30,45`,
  `play-in-editor.md:55-58`. ADR liés : 0011, 0018, 0024, 0038. Prochain ADR libre : **0075**.
- Changement préexistant de l'auteur, à ne jamais indexer : ` M CasaEngine.Launcher/Program.cs` (chemin de projet en dur, lignes 17-18).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Un objet script se crée par le nom complet de sa classe (namespace compris), casse exacte, seulement dans l'assembly de gameplay enregistrée (parent D-R-2). |
| D2 | Les types du moteur aujourd'hui nommés par chaîne sont créés par le code : `player_controller_class` vide = le `PlayerController` du moteur créé directement ; `RpgGameMode.gameMode` passe à vide ; `ScriptArcBallCamera` posée par `Entity.SetGameplayProxy(...)` (parent D-R-3). |
| D3 | Nom introuvable, ambigu ou du mauvais genre : exception dédiée `ScriptTypeResolutionException` (nom à vérifier contre les collisions) avec la clé, la valeur, le fichier, l'assembly consultée et l'indice « vouliez-vous dire » (parent D-R-4). |
| D4 | L'exception part à la création, jamais au `Load` : un chargement sans assembly de gameplay (vérification du convertisseur, tests, éditeur avant la construction de la DLL) continue de marcher (parent R0, révision 1). |
| D5 | Un propriétaire de l'enregistrement par chemin : `AssemblyManager.Load` enregistre seulement avec le chargeur par défaut (et désenregistre celle qu'il avait enregistrée au rechargement) ; avec un chargeur fourni, il n'enregistre rien et ne garde aucune référence (parent R0, révision 1). |
| D6 | Le fichier du message : le `FileName` de l'objet, sinon `AssetCatalog.Get(AssetId)?.FileName`, sinon (entité en ligne) le monde complète le message, sinon « fichier inconnu » ; le contrôleur et le mode citent le `.gameMode` (parent R0, révision 2). |
| D7 | Les filets : le `catch` de `World.InternalAddEntities` prend `when (e is not ScriptTypeResolutionException)` ; l'entité en ligne est complétée du fichier du monde par un `catch` de ce seul type ; toute autre exception garde le journal et l'omission (parent R0, révision 3). |
| D8 | Avant tout code, un recensement de **tous** les filets qui rattrapent une création d'objet script ; un filet que la règle ne traverse pas sans changer un autre comportement est un arrêt (parent D-R-9). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/script-types-full-name`**, créée depuis `main` du moteur une fois la fusion d'E20 faite (parent D-R-8) ; voir O1. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** : construire `CasaEngine.Tests` explicitement puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- Chaque test nouveau est lancé rouge sur le code d'avant (nommé dans la note de validation), puis vert.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ni aucune autre modification préexistante de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans le chemin de création (il tourne au spawn) ; le runtime ne dépend pas de l'éditeur ; formats d'assets : la valeur `script_class_name` change de forme (nom complet), note de migration dans l'ADR.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 échec (compte de départ remesuré en T0.1).
- Smoke : RPGDemo et les démos (`CasaEngine.Demos`, depuis son dossier) démarrent et affichent leur scène ; l'éditeur ouvre RPGDemo.

---

## Phase 0 — Recensement

### ⚠️ T0.1 — Recenser les filets et la frontière de l'éditeur

- Objectif : la liste complète des endroits où une création d'objet script est rattrapée (moteur, éditeur, et les chemins de la DLL Alundra que le plan parent nomme), avec, pour chacun, le comportement voulu (D3, D7) ; la frontière qui rattrape un échec de création quand l'éditeur ouvre un monde sans DLL construite.
- Sources : `git grep` des `catch` autour de `Initialize`, `CreateGameplayProxy`, `CreateLocalPlayerController`, `CreateMode`, `UIScreenDesignTimeDataLoader` ; les chemins de spawn de `World` ; l'ouverture d'un monde dans l'éditeur.
- Étapes :
  1. Compte de départ des tests remesuré.
  2. La liste, avec fichier:ligne et la règle de chacun, versée dans ce plan.
  3. Un filet que la règle ne traverse pas sans changer un autre comportement, ou l'absence de frontière dans l'éditeur : ⚠️ Blocked, question (D8).
- Validation : la liste versée ; aucun code touché.
- Note de validation (2026-10-16, ARRÊT D8 : pas de frontière dans l'éditeur hors session de jeu). Compte de départ : `CasaEngine.Tests` construit explicitement, **4252/4252 verts** (0 échec, 0 ignoré, 26 s), sous-module à `781d3527`.

  **Inventaire des filets autour d'une création d'objet script** (lignes relevées au commit moteur `781d3527` ; DLL au commit parent `be8f2f4`).

  Créations (rappel) : `Entity.cs:210-214` (proxy d'entité, appelé par `Entity.Initialize`), `World.cs:325-338` (proxy de monde), `World.cs:440` (contrôleur), `GameplayModeAsset.cs:11-18` (mode, appelé par `World.cs:504`), `UIScreenDesignTimeDataLoader.cs:113` (view model d'aperçu).

  | # | Filet | Ce qu'il enveloppe | Aujourd'hui (nom introuvable : `Activator.CreateInstance(null)` lève `ArgumentNullException`) | Règle (D3, D7) |
  |---|---|---|---|---|
  | F1 | `World.cs:797` `catch (Exception e) { Logs.WriteException(e); }` dans `InternalAddEntities` (`:774-801`) | `entity.Initialize()` (création du proxy d'entité) puis `InitializeWithWorld`, pour toute entité en attente ; appelé par `LoadContent` (`:304`, `:321`), `InitializePlayerControllers` (`:384`, pion par défaut `:380`) et `Update` (`:528`) ; couvre les entités en ligne, les références de préfab (`:297-304`, `:471-475`) et les ajouts de l'éditeur | journal et entité omise, monde chargé | D7 : filtre `when (e is not ScriptTypeResolutionException)` ; l'entité en ligne est complétée du fichier du monde par un `catch` de ce seul type ; toute autre exception : journal et omission inchangés |
  | F2 | aucun filet autour de `World.CreateGameplayProxy` (appel `:311`) | création du proxy de monde dans `LoadContent` | l'exception sort de `LoadContent` | traverse déjà ; message complété du fichier du monde (D6) |
  | F3 | aucun filet autour de `CreateLocalPlayerController` (`:381`, création `:440`) | création du contrôleur | sort de `LoadContent` | traverse déjà ; le message cite le `.gameMode` ou les réglages (D6) |
  | F4 | aucun filet autour de `StartGameplayModeAsset` (`:496-508`, appelé par `BeginPlay`) | création du mode | sort de `BeginPlay` | traverse déjà ; le message cite le `.gameMode` (D6) |
  | F5 | `UIScreenDesignTimeDataLoader.cs:115` `catch (Exception ex)` vers `Fail(...)` | `ElementFactory.Create<object>(typeName)` du view model d'aperçu (éditeur) | `Fail` avec le message de l'exception | D3 : même filet, le message de l'exception dédiée (clé, valeur, fichier, assembly, indice) passe tel quel dans `Fail` ; D4 : jamais d'exception hors de `Load` |
  | F6 | `UIScreenDesignTimeDataLoader.cs:67,71,82,131` | chemin du fichier, lecture JSON, `PopulateObject` | sans rapport avec la création | inchangés |
  | F7 | `GameEditor.cs:6369` `catch (Exception ex) when (_playModeService?.IsPlaySessionActive == true)` autour de `_editorRuntime?.UpdateHost(gameTime)` (`:6365-6374`) | toute la mise à jour du runtime hébergé, donc `GameManager.UpdateWorld` (`GameManager.cs:84`, `LoadContent` `:131`, `BeginPlay`) et `World.Update` | **session de jeu seulement** : arrêt de la session (`StopPlayMode`), l'éditeur survit | l'exception de D3 y est traitée comme toute exception de gameplay (arrêt de la session) : acceptable |
  | F8 | `GameEditor.cs:4581` `catch (Exception exception)` autour de `LoadCopy<World>` (`OpenWorldAsset`) | **lecture** du monde seule (`Load`) | refuse l'ouverture, éditeur intact | sans objet (D4 : la création n'a pas lieu au `Load`) |
  | F9 | `GameEditor.cs:4597` et `:1241` `gameManager.UpdateWorld(new GameTime())`, hors de tout `try` ; `CasaEngine.Editor/Program.cs:16` `catch` qui écrit un fichier de diagnostic puis `throw;` | matérialisation du monde ouvert ou rechargé | l'exception d'un proxy de monde, d'un contrôleur ou d'un mode sort et **tue l'éditeur** ; celle d'une entité est avalée par F1 | voir ci-dessous |
  | F10 | DLL `AlundraWorldProxy.cs:1093` (boucle de chargement de la carte, `try` `:1058`) | `AlundraEntitySpawnFactory.CreateEntityFromRecord` (`:1061`) qui appelle `entity.Initialize()` (`AlundraEntitySpawnFactory.cs:443`, `:514`, `:593`) | `catch (Exception)` : `AbortCreation`, journal, enregistrement omis | révision 4 : `catch` du seul type dédié, `AbortCreation` puis relance ; toute autre exception : inchangée |
  | F11 | DLL `AlundraWorldProxy.cs:3409` (`SpawnEntityByRecordId`, `try` `:3376`, opcodes 0x2D/0x8A/0x8B) | `CreateEntityFromRecord` (`:3379`) | `catch (Exception)` : `AbortCreation`, journal, `null` | idem F10 |
  | F12 | DLL `AlundraWorldProxy.cs:3539` (`SpawnEntityBySpriteIndex`, `try` `:3497`) | `CreateEntityFromSpriteIndex` (`:3508`) | `catch (Exception)` : `AbortCreation`, `Refuse(...)`, `null` | idem F10 |
  | F13 | DLL `AlundraEntitySpawnFactory.cs:379` | `prefabLoader(prefabAssetId)` = `LoadCopy<Entity>` : lecture seule, jamais la création (`CreateEntityFromPrefab` est appelé hors du `try`, `:387`) | repli sur une entité nue (`:401`) | sans objet (D4) ; la création du repli (`CreateBareEntityFromRecord`) a lieu dans le `try` du site appelant (F10 et F11) |

  Autres sites de la DLL à passer en nom complet (hors filet, partie parent) : `AlundraEntitySpawnFactory.cs:438`, `:509`, `:588` (`nameof(AlundraEntityScriptProxy)` posé dans `GameplayProxyClassName`). Les `catch` de `AlundraWorldProxy.cs:1458`, `:1736`, `:1770`, `:1924` ne touchent pas une création d'objet script. Aucun autre filet n'enveloppe `SpawnEntity`, `AddEntity` ou `LoadContent` dans le moteur, l'éditeur, les démos ou SandBoxGame ; les aperçus de l'éditeur (`PreviewWorldDriver.cs:51`, `WorldViewportPanel.cs:1680`, `EntityAssetEditorPanel.cs:109,308`) appellent `LoadContent`, `AddEntity` et `Update` sans `try`.

  **Frontière de l'éditeur : absente hors session de jeu (O2, ARRÊT D8).** `CasaEngine.Editor/Program.cs` relance toute exception ; `GameEditor.Update` ne rattrape que pendant une session de jeu (F7) ; `CasaEngineGame.Update` n'a qu'un `try/finally` ; `GameManager.UpdateWorld` n'a aucun `try`. Un projet ouvert dans l'éditeur sans DLL de gameplay construite (`AssemblyManager.Load` journalise « Gameplay assembly not found » et continue, `AssemblyManager.cs:20`) n'enregistre aucune assembly de scripts. Or `Entity.InitializePrivate` crée le proxy quelle que soit la politique (`:205-214`), et `EditorPreview` a `InitializeGameplayOnLoad = true` (`GameplayExecutionPolicy.cs:36`) : l'ouverture de `RPGDemo` (`ScriptWorld`, `ScriptPlayer`, `ScriptSign`...) ou l'aperçu d'un préfab à `script_class_name` appliquerait la règle D7 sans rien pour la rattraper. Le proxy de monde, le contrôleur et le mode plantent déjà l'éditeur aujourd'hui (F2 à F4, F9) ; **la règle D7 ferait passer les entités (F1), aujourd'hui journalisées et omises, au même sort** : l'éditeur planterait à l'ouverture d'un monde ou d'un préfab dont le script n'est pas construit, ce qui change un autre comportement. Le plan l'interdit (D8) : tâche en ⚠️, rien d'autre n'est fait, question en O2.
- Commit : `docs(ai-agent): inventory the catches around a script-object creation`

## Phase 1 — La recherche par nom complet

### ⏳ T1.1 — Tests de la fabrique (rouges)

- Objectif : les cas qui fixent la règle, rouges sur le code d'avant.
- Fichiers : `CasaEngine.Tests/Assets/ScriptTypeResolutionTests.cs` (nom à ajuster au dossier existant) ; un initialiseur de module qui enregistre l'assembly de test.
- Étapes : nom complet résolu dans une assembly enregistrée ; nom court refusé avec l'indice ; nom complet d'un type du moteur introuvable ; type d'une assembly non enregistrée introuvable ; deux assemblys au même nom complet : ambiguïté qui cite les deux ; mauvais genre ; casse ; type imbriqué refusé ; après désenregistrement, introuvable et contexte collecté ; aucune assembly : message explicite.
- Validation : rouges sur le code d'avant, nommés dans la note.
- Commit : regroupé avec T1.2.

### ⏳ T1.2 — `ElementFactory` : la recherche des objets script

- Objectif : `CreateScriptObject<T>` et `TryResolveScriptType` (ou équivalent), `ScriptTypeResolutionException` ; cache par `Type.FullName` construit à partir des seules assemblys de scripts, invalidé seulement à l'enregistrement et au désenregistrement, sans épingler un contexte collectible ; types publics de premier niveau, constructeur sans paramètre, bon genre.
- Fichiers : `CasaEngine/Framework/Assets/ElementFactory.cs`, le fichier de l'exception.
- Validation : T1.1 vert ; suite complète verte.
- Commit : `feat(assets): resolve script objects by full name from the script assemblies only`

## Phase 2 — L'enregistrement

### ⏳ T2.1 — `AssemblyManager` enregistre avec le chargeur par défaut

- Objectif : D5.
- Fichiers : `CasaEngine/Engine/Plugins/AssemblyManager.cs` ; tests.
- Étapes : tests d'abord (le chargeur par défaut enregistre et remplace au rechargement ; un chargeur fourni n'enregistre rien ; **rechargement de l'éditeur** : une assembly de scripts compilée chargée deux fois par `AssemblyManager.Load` avec `AssemblyLoader = EditorScriptAssemblyService.LoadShadowCopy` — le premier contexte collectible est collecté, `Unload()` vrai, seule la seconde assembly est enregistrée) ; puis le code.
- Validation : tests rouges puis verts ; suite verte.
- Commit : `feat(plugins): register the gameplay assembly loaded by the default loader`

## Phase 3 — Les créations

### ⏳ T3.1 — Les cinq créations et les filets

- Objectif : `Entity.InitializePrivate` (`:210-214`, la comparaison `:212` en nom complet), `World.CreateGameplayProxy`, `World.cs:440` (vide = `PlayerController` du moteur), `GameplayModeAsset.CreateMode`, `UIScreenDesignTimeDataLoader` (`Fail` avec le message) passent par la recherche de T1.2 ; le fichier du message selon D6 ; les filets de T0.1 selon D7.
- Fichiers : `Entity.cs`, `World.cs`, `PlayerStartupSettings.cs`, `GameplayModeAsset.cs`, `UIScreenDesignTimeDataLoader.cs` ; tests.
- Étapes : tests d'abord, rouges : (i) une entité `LoadCopy<Entity>(id).Clone()` initialisée au mauvais `script_class_name` — le message contient le chemin relatif de l'asset ; (ii) le chargement réel d'un monde dont une entité en ligne est fautive lève, avec le fichier du monde ; (ii-b) un monde qui référence un préfab fautif lève, avec le `.entity` du préfab ; (ii-c) une autre faute d'entité est journalisée, l'entité omise, le monde chargé ; (iii) un monde fautif — son fichier ; (iv) un contrôleur fautif — le `.gameMode` ; un proxy cloné gardé (tue le mutant « `:212` en nom court ») ; un `Load` sans assembly ne lève pas ; puis le code.
- Validation : tests rouges puis verts ; suite verte.
- Commit : `feat(scene): create gameplay proxies, controllers, modes and design-time view models by full name`

## Phase 4 — Les types du moteur et les autres utilisateurs

### ⏳ T4.1 — Types du moteur par le code ; démos, SandBoxGame, RPGDemo

- Objectif : D2 ; les noms des données et du code des projets d'exemple en noms complets ; les tests existants touchés réécrits (types imbriqués sortis au premier niveau).
- Fichiers : `PlayerStartupSettings.cs`, `Entity.cs` (`SetGameplayProxy`), `CasaEngine.Demos/Demo.cs`, `Projects/SandBoxGame/SandBoxGame.cs`, `Projects/RPGDemo/` (6 fichiers de données, `RpgGameMode.gameMode`, `Weapons/ThrowableWeapon.cs`), les tests listés.
- Validation : les deux `.sln` ; suite verte ; RPGDemo et les démos démarrent (smoke).
- Commit : `refactor(gameplay): create engine-owned script objects in code; full names in the samples`

## Phase 5 — Documentation

### ⏳ T5.1 — ADR-0075 et docs

- Objectif : ADR-0075 (règle, exception, enregistrement, migration des données vers le nom complet) ; les quatre docs du nom court ; le tableau d'`ai-agent/README.md`.
- Fichiers : `docs/decisions/0075-*.md`, `docs/decisions/README.md`, les quatre docs, `ai-agent/README.md`.
- Validation : relecture des liens ; validation globale.
- Commit : `docs(adr): ADR-0075 script objects by full name from the gameplay assembly`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | **Fermé le 2026-10-16** : branche créée depuis `main` du moteur `781d3527`, après la fusion d'E20 (D-R-8 du parent). | toutes |
| O2 | **Ouvert, bloque T0.1 (D8, 2026-10-16).** La frontière de l'éditeur à l'ouverture d'un monde sans DLL construite n'existe pas hors session de jeu (inventaire F7, F9). Choix de l'auteur, en termes concrets. (A) **recommandé** : un drapeau de politique, `GameplayExecutionPolicy.ThrowOnScriptTypeResolution` (vrai pour `Runtime` et `EditorSimulation`, faux pour `EditorPreview`) que le filtre de D7 lit : dans l'éditeur hors session de jeu, une entité dont le script n'est pas construit reste journalisée et omise comme aujourd'hui ; en jeu et en session de jeu l'exception sort. Coût : un champ de politique et un test ; le proxy de monde, le contrôleur et le mode continuent de planter l'éditeur comme avant (hors tranche). (B) un `catch` du type dédié dans l'éditeur aux points `UpdateHost` hors session, `OpenWorldAsset` (`UpdateWorld`) et les aperçus : journal et monde laissé à moitié chargé ; plus de code et plus de points à tenir. (C) l'éditeur refuse d'ouvrir un monde tant que la DLL n'est pas construite : change l'usage (on ne pourrait plus ouvrir un monde pour l'éditer avant de construire ses scripts). Sans réponse, rien d'autre n'est fait. | T0.1 |

## Hors périmètre

- Les composants (`"type"`), « Add Component » de l'éditeur, les thèmes MGUI, le compilateur dynamique, les champs morts `ai_controller_class` et `hud_class`.
- La partie parent de R0 (convertisseur, données, DLL Alundra, export) : plan du dépôt parent, §4.
