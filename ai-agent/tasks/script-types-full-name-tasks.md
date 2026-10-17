# Plan agent IA — Objets script par nom complet, depuis l'assembly de gameplay seulement

Plan d'exécution de la tranche R0 (partie moteur) du chantier du dépôt parent `docs/plan-restructuration-dll.md` (§4, révision 4).
Les décisions D1 → D8 ci-dessous ont été arbitrées avec l'auteur le 2026-10-16 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

Approuvé par l'auteur le 2026-10-16 (« fais toutes les tâches », parent D-R-9). La branche `chantier/script-types-full-name` a été créée depuis `main` du moteur `781d3527`, après la fusion d'E20 (ce qui ferme O1).

**Révision 5 (2026-10-17).** L'auteur a tranché O2 (parent D-R-10) : l'aperçu de l'éditeur reste tolérant (D9) et l'inspecteur affiche de
nouveau le script, avec une icône d'avertissement quand son nom ne se résout pas (D10). L'auteur précise que cet affichage existait : c'est
une régression (voir T0.1 et T3.2). T0.1 est close ; T3.1 lit la politique d'exécution ; T3.2 est nouvelle. **Révision 5b (même jour)**,
après une relecture REVISE à trois blocages : (1) une entité « omise » n'entre pas dans le monde et ne se sélectionne pas, donc son icône
serait inatteignable : l'auteur a tranché (parent D-R-11, D11 ci-dessous) : dans l'aperçu, l'entité **et le monde** dont le script ne se
résout pas sont **gardés sans script** ; (2) pas d'entrée vide dans la liste ; (3) l'élément d'avertissement que les tests interrogent est fixé. **Révision 5c (même jour)**,
après la seconde relecture REVISE (quatre blocages ; cap de deux relectures atteint ; dispositions de la session principale, toutes FIX ;
une relecture de clôture) : (1) la portée de tolérance s'ouvre dans le corps de `InternalAddEntities` (ses quatre appelants, dont
`Update`, chemin de l'aperçu d'un préfab et des ajouts de l'éditeur) ; (2) le « genre » d'un script est « type public de premier niveau
dérivé de `GameplayProxy` », le même pour une entité et pour un monde (aucune autre distinction dans le code) ; (3) un nom vide n'affiche
aucune sélection ; (4) les vérifications à l'œil de l'éditeur sont la recette de l'auteur.

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
| D9 | (2026-10-17, parent D-R-10, ferme O2 ; précisée par D11) La politique d'exécution porte `ThrowOnScriptTypeResolution` : vrai pour `Runtime` et `EditorSimulation`, faux pour `EditorPreview`. Quand il est faux, le monde ouvre une **portée de tolérance** sans allocation (par exemple un compteur `[ThreadStatic]`) autour de ses créations : **dans le corps de `InternalAddEntities`** (`World.cs:774-801`), ce qui couvre ses quatre appelants, `LoadContent` (`:304`, `:321`), `InitializePlayerControllers` (`:384`) et `Update` (`:528`) — donc les entités en ligne, les préfabs, le pion, l'aperçu d'un préfab de l'éditeur (`EntityAssetEditorPanel.cs:305-309` → `PreviewWorldDriver.Rebuild` → `RefreshNow` → `World.Update`) et toute entité ajoutée à un monde déjà chargé — ; autour de `CreateGameplayProxy` ; et autour de l'initialisation du proxy de monde (`Initialize`, `InitializeWithWorld`, `World.cs:313-317`), qui couvre les entités que ce proxy fait naître. Les sites de création (`Entity.InitializePrivate`, `World.CreateGameplayProxy`) la lisent : dans la portée, un nom qui ne se résout pas écrit un avertissement au journal (clé, valeur, fichier, indice, comme le message de D3) et l'objet reste **sans proxy**, son `GameplayProxyClassName` intact ; hors de la portée, `ScriptTypeResolutionException`. Le filtre de D7 reste `when (e is not ScriptTypeResolutionException)`. Le contrôleur et le mode ne sont pas créés dans l'aperçu (`InitializePlayerControllers` et `RunBeginPlay` faux) : inchangés. |
| D10 | (2026-10-17, parent D-R-10) L'inspecteur de l'éditeur (`EntityDetailsPanel`) affiche le script de l'entité ou du monde sélectionné, et une icône d'avertissement (`EditorIcons.TriAlert`) à côté quand un nom non vide ne se résout pas, avec le message de la résolution. L'auteur : « cela doit être affiché dans l'inspector obligatoirement ; c'est un bug car je l'avais codé ». |
| D11 | (2026-10-17, parent D-R-11) Dans l'aperçu de l'éditeur, une entité dont le script ne se résout pas **reste dans le monde sans script** (aucun comportement), visible dans la hiérarchie et sélectionnable, avec l'icône et le message sur sa ligne Script ; **le monde** dont le script ne se résout pas s'ouvre sans son script, avec l'icône sur sa ligne Script, au lieu de faire planter l'éditeur. En jeu et en session de jeu, l'exception sort. |

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

Résultats (2026-10-17, après T5.1) :
- Les deux `.sln` : 0 erreur. `CasaEngine.Tests` construit explicitement : **4314/4314**, 0 échec.
- Smoke `CasaEngine.Demos` (depuis `CasaEngine.Demos/`) : démarre, vivant 14 s, journal sans exception. Parcours `CASAENGINE_DEMO_CYCLE=60` (24 démos, deux tours, passe par `Demo.CreateCamera` donc par `SetGameplayProxy`) : `result=PASS steps=97 failedChecks=0 errors=0 defaultCameraWarnings=0`, code 0. Un premier parcours lancé juste après l'arrêt forcé du smoke a donné un seul contrôle en échec au pas 2 (« this world was not asked for », démo Cutscene MoveTo) ; relancé tel quel il passe, sans changement de code entre les deux : alea de démarrage du parcours, non lié aux scripts.
- Smoke RPGDemo : lancé par un lanceur de scratch (hors dépôt, `CasaEngine.Launcher` compilé avec `args[0]`, car le `Program.cs` du dépôt est modifié par l'auteur et pointe le projet Alundra). `TitleScreenWorld` : le monde charge son `ScriptTitleScreenWorld` par nom complet, sans exception ni avertissement de script (l'avertissement « No camera found » est préexistant). Preuve par mutation réelle : avec le nom court dans `TitleScreenWorld.world`, le jeu s'arrête sur `ScriptTypeResolutionException` (fichier, assemblies cherchées `CasaEngine.RPGDemo`, « Did you mean 'CasaEngine.RPGDemo.Scripts.ScriptTitleScreenWorld' ? ») ; fichier restauré par `git checkout`, `git status` propre. `DefaultWorld` (copie de scratch du projet, premier monde changé) : les entités `character_link`, `character_octopus`, `weapon_sword`, `weapon_rock` et le `weapon_rock` lancé par `ThrowableWeapon` sont créés sans exception de script. Après environ 30 s de jeu sans entrée, une `NullReferenceException` sort de `Entity.Destroy` → `AnimatedSpriteComponent.OnEnabledValueChange` (détruire un rocher qui touche le joueur) ; ces fichiers ne sont pas dans le diff de la branche (`AnimatedSpriteComponent.cs` identique à `781d3527`) : constat préexistant, à signaler, non corrigé.
- Smoke SandBoxGame (`Projects/SandBoxGame/bin/Debug/net9.0-windows/SandBoxGame.exe`) : `LoadContent` passe (la caméra par `SetGameplayProxy`), puis `NullReferenceException` dans `SandBoxGame.DrawBoxWithEffect` (`effect.Parameters` sur un effet nul, ligne 174, code non touché par la branche) à la première image : constat préexistant, à signaler, non corrigé.
- Éditeur (Script de l'inspecteur, avertissement d'un nom faussé, aperçu d'un préfab) : recette de l'auteur de T3.2, non faite par l'agent, T3.2 reste 🧪.


- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 échec (compte de départ remesuré en T0.1).
- Smoke : RPGDemo et les démos (`CasaEngine.Demos`, depuis son dossier) démarrent et affichent leur scène ; l'éditeur ouvre RPGDemo, et son inspecteur montre la ligne Script, sans avertissement avec la DLL construite et avec l'avertissement pour un nom faussé (T3.2) — **recette de l'auteur** pour la partie éditeur (un agent lance les démos, pas l'éditeur à la main).

---

## Phase 0 — Recensement

### ✅ T0.1 — Recenser les filets et la frontière de l'éditeur

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
  **Clôture (2026-10-17, D9 et D10).** O2 est fermé par les décisions de l'auteur (D9 à D11). Règles par filet, en plus du tableau : F1 garde le filtre de D7 ; dans l'aperçu,
  l'exception n'est pas levée (portée de tolérance de D9, ouverte quand la politique le dit, lue comme les autres drapeaux,
  `World.cs:306`, `:313`, `:479`, `:581`) : l'entité est ajoutée sans proxy (`entity.Initialize()` ne lève plus, `_entities.Add` a lieu,
  `World.cs:788-792`), y compris par `Update` (aperçu d'un préfab, ajouts de l'éditeur) ; F2 : dans l'aperçu, le monde s'ouvre sans proxy ; F3, F4 : le contrôleur et le mode ne sont pas créés dans
  l'aperçu ; F7 inchangé (une session de jeu s'arrête) ; F9 : dans l'aperçu, l'ouverture d'un monde ne plante plus pour un script
  d'entité ou de monde introuvable, et l'inspecteur montre l'avertissement de l'objet sélectionné (D10, D11) ; F10 à F12 (DLL) : dans la
  portée, aucune exception n'arrive, l'entité naît sans proxy.
  **Régression trouvée (2026-10-17).** L'éditeur WPF avait, dans l'inspecteur d'entité, une section « Script » : une liste déroulante
  « Script class name » liée à `Entity.GameplayProxyClassName` (et pour le monde à `World.GameplayProxyClassName`), dont les éléments
  étaient les classes de gameplay enregistrées, plus « Game Mode » et « Default Pawn » pour le monde
  (`CasaEngine.EditorUI/Controls/EntityControls/EntityControl.xaml` et `EntityControl.xaml.cs`,
  `ComboBoxGameplayProxyClassName_OnSelectionChanged`). Elle a disparu le 2026-03-30 avec le projet WPF (`dc2f4d25`, « remove unused
  projects ») ; l'éditeur MGUI ne l'a jamais reprise : aujourd'hui aucun écran de `CasaEngine.Editor/` n'affiche ni ne modifie
  `GameplayProxyClassName` (seule lecture : une trace de smoke, `GameEditor.cs:6695`). « Game Mode » et « Default Pawn » manquent aussi :
  hors de cette tranche (O3).
- Commit : `docs(ai-agent): inventory the catches around a script-object creation`

## Phase 1 — La recherche par nom complet

### ✅ T1.1 — Tests de la fabrique (rouges)

- Objectif : les cas qui fixent la règle, rouges sur le code d'avant.
- Fichiers : `CasaEngine.Tests/Assets/ScriptTypeResolutionTests.cs` (nom à ajuster au dossier existant) ; un initialiseur de module qui enregistre l'assembly de test.
- Étapes : nom complet résolu dans une assembly enregistrée ; nom court refusé avec l'indice ; nom complet d'un type du moteur introuvable ; type d'une assembly non enregistrée introuvable ; deux assemblys au même nom complet : ambiguïté qui cite les deux ; mauvais genre ; casse ; type imbriqué refusé ; après désenregistrement, introuvable et contexte collecté ; aucune assembly : message explicite.
- Validation : rouges sur le code d'avant, nommés dans la note.
- Note de validation (2026-10-17) : `CasaEngine.Tests/Scripting/ScriptTypeResolutionTests.cs` (15 cas : nom complet dans une assembly enregistrée et dans l'assembly de test ; nom court refusé avec l'indice ; type du moteur introuvable ; assembly non enregistrée ; ambiguïté citant les deux assemblys ; mauvais genre ; casse ; type imbriqué ; type abstrait ; après désenregistrement, introuvable et contexte collecté ; aucune assembly ; `TryResolveScriptType` ; nom vide ; fichier complété une fois) et `ScriptTestAssemblyRegistration.cs` (initialiseur de module qui enregistre l'assembly de test). **Rouges sur le code d'avant** : exécutés contre une cale `CreateScriptObject`/`TryResolveScriptType` qui reprenait le comportement d'avant (`Create<T>`, recherche par nom court) : **15/15 en échec** (`ArgumentNullException` du `Activator`, ou aucune exception levée) ; verts avec T1.2 (15/15). Le test de collecte place la création dans une méthode `NoInlining`, comme `ElementFactoryReloadTests`.
- Commit : regroupé avec T1.2.

### ✅ T1.2 — `ElementFactory` : la recherche des objets script

- Objectif : `CreateScriptObject<T>` et `TryResolveScriptType` (ou équivalent), `ScriptTypeResolutionException` ; cache par `Type.FullName` construit à partir des seules assemblys de scripts, invalidé seulement à l'enregistrement et au désenregistrement, sans épingler un contexte collectible ; types publics de premier niveau, constructeur sans paramètre, bon genre.
- Fichiers : `CasaEngine/Framework/Assets/ElementFactory.cs`, le fichier de l'exception.
- Validation : T1.1 vert ; suite complète verte.
- Note de validation (2026-10-17) : `ElementFactory.CreateScriptObject<T>(typeName, key, source)` (nom complet exact, assemblys de scripts seulement ; index `Type.FullName` construit à partir des seules assemblys enregistrées, invalidé à l'enregistrement et au désenregistrement, pas à chaque `AssemblyLoad` ; types publics de premier niveau, concrets, non génériques, constructeur public sans paramètre ; lecture du cache sans allocation), `TryResolveScriptType` (rend le message, sans lever) et `GetScriptTypeNames(baseType)` (énumération pour T3.2), `ScriptTypeResolutionException` (nom vérifié contre les collisions par `check_names.py` : libre ; propriétés `Key`, `Value`, `Reason`, `SearchedAssemblies`, `Hint`, `FileName` complété une fois par `CompleteFileName`) et `ScriptTypeResolutionTolerance` (portée de D9, `[ThreadStatic]`, sans allocation, portant le fichier du monde pour l'avertissement ; lue par `CreateScriptObject` : dans la portée, avertissement et `null`). `Create<T>`/`Load<T>` (composants) inchangés. `ElementFactoryReloadTests` passent à la recherche par nom complet. Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : **4267/4267** (4252 + 15).
- Commit : `feat(assets): resolve script objects by full name from the script assemblies only`

## Phase 2 — L'enregistrement

### ✅ T2.1 — `AssemblyManager` enregistre avec le chargeur par défaut

- Objectif : D5.
- Fichiers : `CasaEngine/Engine/Plugins/AssemblyManager.cs` ; tests.
- Étapes : tests d'abord (le chargeur par défaut enregistre et remplace au rechargement ; un chargeur fourni n'enregistre rien ; **rechargement de l'éditeur** : une assembly de scripts compilée chargée deux fois par `AssemblyManager.Load` avec `AssemblyLoader = EditorScriptAssemblyService.LoadShadowCopy` — le premier contexte collectible est collecté, `Unload()` vrai, seule la seconde assembly est enregistrée) ; puis le code.
- Validation : tests rouges puis verts ; suite verte.
- Note de validation (2026-10-17) : `CasaEngine.Tests/Scripting/AssemblyManagerRegistrationTests.cs` (4 cas). **Rouges sur le code d'avant** : `DefaultLoader_RegistersTheGameplayAssembly` et `DefaultLoader_ReloadReplacesItsOwnRegistration` ; verts avant comme après, gardes de D5 : `ProvidedLoader_RegistersNothingAndKeepsNoReference` (la `ScriptAssemblyHost` se décharge, `Unload()` vrai) et `EditorShadowCopyReload_KeepsOnlyTheSecondAssemblyRegisteredAndCollectsTheFirst` (même nom complet dans les deux versions : une seule enregistrée, sinon ambiguïté ; le premier contexte collectible est collecté, prouvé par une `WeakReference`). **Constat hors tranche** : `EditorScriptAssemblyService.Unload()` rend `false` dans une construction Debug même quand tout est libéré, parce que le temporaire qui porte l'assembly vers `ElementFactory.UnregisterScriptAssembly(Host.LoadedAssembly!)` reste vivant pendant la vérification de décharge (vérifié : le même test passe avec cet appel placé dans une méthode `NoInlining`, modification retirée ensuite) ; le test n'assert donc pas ce retour (`EditorScriptAssemblyServiceTests` ne l'assert pas non plus) et la collecte est prouvée par la référence faible. Code : `AssemblyManager.Load` enregistre l'assembly seulement avec le chargeur par défaut (comparé à `Assembly.LoadFile`), désenregistre celle qu'il avait enregistrée au rechargement, ne garde rien avec un chargeur fourni. Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : **4271/4271**.
- Commit : `feat(plugins): register the gameplay assembly loaded by the default loader`

## Phase 3 — Les créations

### ✅ T3.1 — Les cinq créations et les filets

- Objectif : `Entity.InitializePrivate` (`:210-214`, la comparaison `:212` en nom complet), `World.CreateGameplayProxy`, `World.cs:440` (vide = `PlayerController` du moteur), `GameplayModeAsset.CreateMode`, `UIScreenDesignTimeDataLoader` (`Fail` avec le message) passent par la recherche de T1.2 ; le fichier du message selon D6 ; les filets de T0.1 selon D7 ; la tolérance de D9 et D11 (`GameplayExecutionPolicy.ThrowOnScriptTypeResolution`, vrai pour `Runtime` et `EditorSimulation`, faux pour `EditorPreview` ; la portée ouverte par le monde ; l'objet gardé sans proxy, son nom intact).
- Fichiers : `Entity.cs`, `World.cs`, `PlayerStartupSettings.cs`, `GameplayModeAsset.cs`, `UIScreenDesignTimeDataLoader.cs`, `CasaEngine/Framework/Application/GameplayExecutionPolicy.cs` ; tests.
- Étapes : tests d'abord, rouges : (i) une entité `LoadCopy<Entity>(id).Clone()` initialisée au mauvais `script_class_name` — le message contient le chemin relatif de l'asset ; (ii) le chargement réel d'un monde dont une entité en ligne est fautive lève, avec le fichier du monde ; (ii-b) un monde qui référence un préfab fautif lève, avec le `.entity` du préfab ; (ii-c) une autre faute d'entité est journalisée, l'entité omise, le monde chargé ; (iii) un monde fautif — son fichier ; (iv) un contrôleur fautif — le `.gameMode` ; un proxy cloné gardé (tue le mutant « `:212` en nom court ») ; un `Load` sans assembly ne lève pas ; (v) sous `EditorPreview`, le
  même monde que (ii) se charge sans exception : l'entité en ligne fautive, l'entité d'un préfab fautif et une entité au nom fautif que
  le proxy de monde fait naître dans son `InitializeWithWorld` sont dans `World.Entities`, sans proxy, leur `GameplayProxyClassName`
  intact, un avertissement par entité au journal ; un monde au script fautif s'ouvre sans proxy ; sous `EditorSimulation`, chacun lève
  (rouges si la politique est ignorée) ; sous `EditorPreview`, un monde **déjà chargé** reçoit `AddEntity` d'une entité au script
  fautif puis `Update(0f)` (le chemin de l'aperçu d'un préfab) : l'entité est dans `World.Entities`, sans proxy, son nom intact ; sous
  `EditorSimulation`, le même cas lève ; la portée est refermée après une exception (un test qui lève dans la portée puis vérifie qu'une
  création hors portée lève) ; puis le code.
- Validation : tests rouges puis verts ; suite verte.
- Note de validation (2026-10-17) : `CasaEngine.Tests/Scripting/ScriptCreationByFullNameTests.cs` (23 cas, harnais : `CasaEngineGame` non initialisé, vrai `AssetContentManager` à chargeurs factices, monde chargé par `World.Load` d'un document JSON puis `LoadContent` ; couvre (i), le proxy cloné gardé, le nom court refusé, `Load` sans assembly (D4), (ii) et (ii-b) avec le fichier du monde puis celui du `.entity` du préfab, (ii-c) une autre faute journalisée/omise/monde chargé, (iii), (iv) contrôleur fautif/vide/nom complet et mode fautif/vide, (v) les politiques `Runtime`/`EditorSimulation`/`EditorPreview`, l'aperçu qui garde l'entité en ligne, l'entité du préfab et l'entité née du script de monde (initialisée tout de suite, comme une fabrique de spawn) sans proxy, nom intact, un avertissement par entité (3), le monde au script fautif ouvert sans proxy, la simulation qui lève pour chacun, le monde déjà chargé + `AddEntity` + `Update(0f)` (aperçu : gardée sans proxy ; simulation : lève), la portée refermée après une exception, un monde sans jeu : strict). **Rouges sur le code d'avant** : 20 des 23 en échec (`ArgumentNullException` de l'`Activator`, ou aucune exception levée parce que le filet d'avant avalait tout) ; les 3 verts avant comme après sont des gardes (ordinaire : politique, portée, mode vide). Mutations réelles (script de scratch, fichier restauré par copie, `git status` propre ensuite) : `GetType().FullName`→`.Name` dans `Entity` (1 échec : le proxy cloné), ouverture de la portée retirée de `InternalAddEntities` (2 échecs : aperçu complet et monde déjà chargé), `CompleteFileName` du monde retiré (1 échec), `Dispose` de la portée sans `_depth--` (6 échecs), portée retirée autour de l'initialisation du proxy de monde (1 échec), portée retirée de `CreateGameplayProxy` (1 échec), `EditorSimulation.ThrowOnScriptTypeResolution=false` (3 échecs) ; le filtre `when` de D7 est redondant avec le `catch` de complétion qui le précède (mutation sans effet, gardé comme écrit par D7). Code : `GameplayExecutionPolicy.ThrowOnScriptTypeResolution` ; `Entity.InitializePrivate` (comparaison `FullName`, `Ordinal`) et `World.CreateGameplayProxy` par `CreateScriptObject` avec le fichier de l'objet ; `World.InternalAddEntities` ouvre la portée de tolérance (corps de la méthode : ses quatre appelants) et complète le fichier du monde d'une entité en ligne ; `LoadContent` ouvre la même portée autour de l'initialisation du proxy de monde ; contrôleur : nom vide = `new PlayerController()` (`PlayerControllerClass` vaut `""` par défaut), sinon nom complet avec le fichier des réglages ; `GameplayModeAsset.CreateMode` ; `UIScreenDesignTimeDataLoader` (le message de l'exception dédiée passe tel quel dans `Fail`, complété du fichier de données ; la branche « instance nulle » devenue inatteignable est retirée). Tests existants réécrits : `GameplayProxyStateSerializationTests` (noms complets), `GameplayModeRunnerTests` et `UIScreenPreviewBuilderDesignTimeDataTests` (types sortis au premier niveau) + un cas de message du view model (nom court : clé, fichier, indice). Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : **4295/4295**.
- Commit : `feat(scene): create gameplay proxies, controllers, modes and design-time view models by full name`

### 🧪 T3.2 — L'inspecteur affiche et choisit le script, avec l'avertissement (D10)

- Objectif : rétablir dans l'éditeur MGUI la section « Script » perdue avec l'éditeur WPF (T0.1, régression), avec le nom complet de
  D1 et l'avertissement de D10. Pour l'entité ou le monde sélectionné dans `EntityDetailsPanel` : une ligne « Script » avec une liste
  déroulante (`CreateStringCombo`, `EntityDetailsPanel.cs:988`) dont les éléments sont les noms complets des classes script des assemblys de
  scripts enregistrées — types publics de premier niveau dérivés de `GameplayProxy`, **la même liste pour une entité et pour un monde**
  (les proxys de monde et d'entité dérivent tous de `GameplayProxy`, `GameplayProxy.cs:7`, `ScriptWorld.cs:19`, `ScriptPlayer.cs:16`, et
  sont créés par `ElementFactory.Create<GameplayProxy>`, `Entity.cs:214`, `World.cs:332` ; la liste WPF montrait de même « les classes
  de gameplay enregistrées ») —, plus la valeur courante si elle
  n'y est pas ; pas d'entrée vide (le gestionnaire WPF d'origine ignorait déjà une sélection vide,
  `ComboBoxGameplayProxyClassName_OnSelectionChanged`, et `CreateStringCombo` filtre les valeurs vides, `:1011-1017`) ; pour un nom vide
  (la plupart des entités), la ligne **n'affiche aucune sélection** : elle ne prend pas le repli de `CreateStringCombo` sur le premier
  élément (`combo.SelectedItem = selectedItem ?? itemList.FirstOrDefault()`, `:1008`), par une variante locale à la ligne Script, sans
  changer les autres listes du panneau ; le choix change `GameplayProxyClassName` par une commande d'historique annulable
  (`ExecuteHistoryCommand`, comme « Rename Entity », `:356`) et marque l'asset modifié comme le fait le renommage ; quand un nom non vide ne se résout pas (introuvable,
  nom court, ambigu, type qui ne dérive pas de `GameplayProxy`), un **élément d'avertissement** à côté de la liste, présent même sans texture : l'image
  `EditorIcons.TriAlert` (`EditorIcons.cs:59`, aujourd'hui sans usage) si elle est chargée, sinon un texte « ! » (le motif de repli de
  `FilePlus`, `EntityDetailsPanel.cs:281-298`), et **le message de la résolution de T1.2 en texte sous la ligne** (un seul support) ;
  les deux sont exposés aux tests par des accesseurs internes (`CasaEngine.Editor/InternalsVisibleTo.Tests.cs` existe) ; l'état est recalculé à l'affichage et après un rechargement des scripts (`EditorScriptReloadCoordinator`), sans état
  gardé ailleurs.
- La résolution utilisée est celle de T1.2, par une méthode sans exception (`TryResolveScriptType` ou équivalent, qui rend le message) et
  une énumération des types script d'un genre (pas d'allocation dans le chemin de création ; l'éditeur peut allouer).
- Fichiers : `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` ; `CasaEngine/Framework/Assets/ElementFactory.cs` (l'énumération et le
  message, si T1.2 ne les a pas) ; tests. Le panneau est construit en code comme tout `CasaEngine.Editor/` (aucun XAML dans l'éditeur) :
  la ligne suit ce motif.
- Étapes : tests d'abord, rouges : (i) l'énumération rend les noms complets des types publics de premier niveau dérivés de
  `GameplayProxy` des assemblys enregistrées, et seulement eux (pas un type d'une assembly non enregistrée, pas un type qui n'en dérive
  pas, pas un type imbriqué), et rien après désenregistrement ; (ii) l'état d'affichage d'un nom (résolu ; introuvable ; nom court avec l'indice ; ambigu ; type qui ne dérive pas
  de `GameplayProxy` ; vide = aucun avertissement) ; (iii) **le parcours réel** : sous `EditorPreview`, un monde chargé dont une entité a un script introuvable (D11, par
  T3.1), puis l'inspecteur construit sur un `MGDesktop` et un `MGWindow` de test (motif de `CasaEngine.Tests/Editor/AudioMixerPanelTests.cs`
  et `ScreenEditor/UIScreenImageSourcePickerTests.cs`), **sans** `EditorIcons.Load` : pour cette entité, la ligne Script montre la valeur,
  l'élément d'avertissement existe et le texte porte le message ; pour une entité au script résolu et pour une entité sans script, aucun
  élément d'avertissement, et pour l'entité sans script **la valeur affichée est vide** et choisir la première classe de la liste change
  `GameplayProxyClassName` (rouge avec `CreateStringCombo` inchangé) ; même chose pour le monde au script introuvable ; (iv) choisir un autre script change
  `GameplayProxyClassName`, l'annulation le rend, et les autres listes du panneau (`CreateStringCombo`) ne changent pas ; puis le code.
- Validation : tests rouges puis verts ; suite verte ; les deux `.sln`. La preuve de l'agent est le test (iii) (parcours réel, accesseurs
  internes). **Recette de l'auteur** (un agent ne manipule pas l'éditeur lancé) : (a) RPGDemo, DLL construite : la ligne Script de
  l'entité et du monde, sans avertissement ; (b) cas positif : RPGDemo ouvert avec un nom de script faussé dans un de ses fichiers de
  données (l'agent prépare la copie de travail et la rétablit après, preuve par `git status`) : le monde s'ouvre, l'entité est dans la
  hiérarchie, l'avertissement et le message sont visibles dans l'inspecteur ; (c) l'aperçu d'un préfab au script faussé s'ouvre avec
  l'avertissement. Sans cette recette, T3.2 reste 🧪.
- Note de validation (2026-10-17) : `CasaEngine.Tests/Editor/EntityDetailsScriptRowTests.cs` (14 cas ; l'inspecteur est construit sur un `MGDesktop`/`MGWindow` de `HeadlessUiTestHarness`, **sans** `EditorIcons.Load` ; les mondes sont ceux de `ScriptCreationByFullNameTests`, rendus `internal`, chargés sous `EditorPreview`). Couvre (i) l'énumération `GetScriptTypeNames(typeof(GameplayProxy))` : noms complets des types publics de premier niveau concrets d'une assembly enregistrée, ni type imbriqué, ni abstrait, ni type qui ne dérive pas de `GameplayProxy`, ni assembly non enregistrée, rien après désenregistrement ; la liste de la ligne Script (`GetScriptChoices`) ajoute la valeur courante absente et n'a jamais d'entrée vide ; (ii) `TryGetScriptProblem` : aucun problème pour un nom résolu ou vide, un problème pour un nom introuvable, un nom court (l'indice cite le nom complet), un type qui ne dérive pas de `GameplayProxy` et un nom ambigu ; (iii) le parcours réel : monde chargé sous `EditorPreview` dont l'entité en ligne a un script introuvable (gardée par T3.1, sans proxy) : la ligne montre la valeur, l'élément d'avertissement existe (texte « ! » sans texture) et le texte porte le message ; aucun avertissement pour une entité au script résolu ni pour une entité sans script, dont la valeur affichée est vide (`SelectedIndex` -1) et dont le choix de la première classe change `GameplayProxyClassName` ; même chose pour le monde (script introuvable : valeur, avertissement, message ; sans script : valeur vide, même liste que l'entité, choix de la première classe) ; (iv) choisir un autre script change `GameplayProxyClassName`, une commande d'historique « Change Entity Script » / « Change World Script » l'annule (le panneau se recalcule dans les deux sens), les listes de politiques du panneau gardent leur sélection (`CreateStringCombo` inchangé) ; `RefreshScriptRow` recalcule l'état après l'enregistrement d'une assembly. **Mutations réelles** (script de scratch, fichier sauvegardé puis restauré, comparé à la sauvegarde) : ligne Script retirée (= code d'avant : 8 des 14 cas en échec, les 6 autres sont des cas de l'énumération et de `TryGetScriptProblem`, qui n'existaient pas), repli sur le premier élément de `CreateStringCombo` (3 échecs), `TryGetScriptProblem` toujours faux (6), choix sans commande d'historique (2), valeur courante absente non ajoutée (4). Code : `EntityDetailsPanel` (`AddScriptRow` pour l'entité et pour le monde, variante locale `CreateScriptCombo` sans repli, élément d'avertissement `EditorIcons.TriAlert` ou texte « ! », message sous la ligne, accesseurs internes `ScriptCombo`, `ScriptWarningElement`, `ScriptMessageText`, `RefreshScriptRow`) ; `GameEditor.RebuildScriptsNow` appelle `RefreshScriptRow` des deux inspecteurs après un rechargement des scripts réussi. Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : **4309/4309** (4295 + 14). Reste 🧪 : la recette de l'auteur dans l'éditeur lancé, ci-dessus (a), (b), (c).
- Commit : `fix(editor): show and pick the gameplay script in the inspector again, with a warning when it does not resolve`

## Phase 4 — Les types du moteur et les autres utilisateurs

### ✅ T4.1 — Types du moteur par le code ; démos, SandBoxGame, RPGDemo

- Objectif : D2 ; les noms des données et du code des projets d'exemple en noms complets ; les tests existants touchés réécrits (types imbriqués sortis au premier niveau).
- Fichiers : `PlayerStartupSettings.cs`, `Entity.cs` (`SetGameplayProxy`), `CasaEngine.Demos/Demo.cs`, `Projects/SandBoxGame/SandBoxGame.cs`, `Projects/RPGDemo/` (6 fichiers de données, `RpgGameMode.gameMode`, `Weapons/ThrowableWeapon.cs`), les tests listés.
- Validation : les deux `.sln` ; suite verte ; RPGDemo et les démos démarrent (smoke).
- Note de validation (2026-10-17) : `Entity.SetGameplayProxy(IGameplayProxy)` (nouvelle ; pose le proxy et `GameplayProxyClassName` = nom complet du type, donc `Initialize` garde l'instance, et initialise le proxy tout de suite si l'entité l'est déjà) ; `CasaEngine.Demos/Demo.cs` et `Projects/SandBoxGame/SandBoxGame.cs` posent `ScriptArcBallCamera` par `SetGameplayProxy(new ScriptArcBallCamera())` ; `RpgGameMode.gameMode` passe à `"player_controller_class": ""` (le contrôleur vide = `PlayerController` du moteur, fait en T3.1) ; RPGDemo : les 8 noms de scripts de 6 fichiers de données (`DefaultWorld.world` ×3, `TitleScreenWorld.world`, `character_link`, `character_octopus`, `weapon_rock`, `weapon_sword`) en `CasaEngine.RPGDemo.Scripts.<Nom>` ; `ThrowableWeapon` utilise `typeof(ScriptEnemyWeapon).FullName`. Tests : `CasaEngine.Tests/Scripting/EngineOwnedScriptObjectsTests.cs` (5 cas : `SetGameplayProxy` garde l'instance et note le nom complet, survit à `Initialize` sans assembly de script enregistrée, refuse null ; le vrai `RpgGameMode.gameMode` donne un contrôleur vide ; les 8 noms de scripts des données de RPGDemo sont des noms complets dont le fichier source existe et dont le namespace correspond). Rouge avant : les données ramenées à l'état d'avant (patch inversé puis réappliqué) font échouer les 2 cas de données ; les 3 cas de `SetGameplayProxy` ne compilent pas sans la méthode. Tests existants réécrits : `GameplayModeRunnerTests` (forme `player_controller_class` vide), `EditorWorldPlaySnapshotTests` (nom complet). Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : **4314/4314** (4309 + 5). Le smoke des démos est dans la validation globale ci-dessous.
- Commit : `refactor(gameplay): create engine-owned script objects in code; full names in the samples`

## Phase 5 — Documentation

### ✅ T5.1 — ADR-0075 et docs

- Objectif : ADR-0075 (règle, exception, enregistrement, migration des données vers le nom complet) ; les quatre docs du nom court ; le tableau d'`ai-agent/README.md`.
- Fichiers : `docs/decisions/0075-*.md`, `docs/decisions/README.md`, les quatre docs, `ai-agent/README.md`.
- Validation : relecture des liens ; validation globale.
- Note de validation (2026-10-17) : `docs/decisions/0075-script-objects-by-full-name-from-the-gameplay-assembly.md` (numéro 0075 libre, vérifié dans l'index) enregistre D1 à D11, la régression de l'inspecteur (`dc2f4d25`), la migration des données vers le nom complet et la limite connue d'`EditorScriptAssemblyService.Unload()` en Debug ; index `docs/decisions/README.md` ; les quatre docs du nom court mises à jour (`screen-authoring-conventions.md`, `gameplay-csproj-scaffolding.md`, `gameplay-mode.md`, `play-in-editor.md`) ; ligne du tableau d'`ai-agent/README.md` (T0.1 à T5.1 ✅, T3.2 🧪). Aucun lien relatif cassé (un seul lien ajouté, le fichier de l'ADR dans l'index).
- Commit : `docs(adr): ADR-0075 script objects by full name from the gameplay assembly`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | **Fermé le 2026-10-16** : branche créée depuis `main` du moteur `781d3527`, après la fusion d'E20 (D-R-8 du parent). | toutes |
| O2 | **Fermé le 2026-10-17 par l'auteur (parent D-R-10 ; D9, D10).** Question d'origine : La frontière de l'éditeur à l'ouverture d'un monde sans DLL construite n'existe pas hors session de jeu (inventaire F7, F9). Choix de l'auteur, en termes concrets. (A) **recommandé** : un drapeau de politique, `GameplayExecutionPolicy.ThrowOnScriptTypeResolution` (vrai pour `Runtime` et `EditorSimulation`, faux pour `EditorPreview`) que le filtre de D7 lit : dans l'éditeur hors session de jeu, une entité dont le script n'est pas construit reste journalisée et omise comme aujourd'hui ; en jeu et en session de jeu l'exception sort. Coût : un champ de politique et un test ; le proxy de monde, le contrôleur et le mode continuent de planter l'éditeur comme avant (hors tranche). (B) un `catch` du type dédié dans l'éditeur aux points `UpdateHost` hors session, `OpenWorldAsset` (`UpdateWorld`) et les aperçus : journal et monde laissé à moitié chargé ; plus de code et plus de points à tenir. (C) l'éditeur refuse d'ouvrir un monde tant que la DLL n'est pas construite : change l'usage (on ne pourrait plus ouvrir un monde pour l'éditer avant de construire ses scripts). Sans réponse, rien d'autre n'est fait. | T0.1 |
| O3 | **Fermé le 2026-10-17** : « Game Mode » et « Default Pawn » du monde (plus « Gameplay Mode », sur la décision de l'auteur) sont rétablis dans l'inspecteur MGUI par le plan [world-game-mode-inspector-tasks.md](world-game-mode-inspector-tasks.md) (ADR-0076) ; le pion est celui du `.gameMode` partagé, écrit avec Ctrl+S. Question d'origine : « Game Mode » et « Default Pawn » du monde, présents dans l'inspecteur WPF, manquent aussi dans l'éditeur MGUI (même perte, `dc2f4d25`) : hors de cette tranche ; à rétablir à part, sur la décision de l'auteur. | — |

## Hors périmètre

- Les composants (`"type"`), « Add Component » de l'éditeur, les thèmes MGUI, le compilateur dynamique, les champs morts `ai_controller_class` et `hud_class`.
- La partie parent de R0 (convertisseur, données, DLL Alundra, export) : plan du dépôt parent, §4.
