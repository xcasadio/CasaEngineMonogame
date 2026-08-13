# Play-in-Editor — plan d'exécution

Objectif : Play/Pause/Stop d'un niveau dans le viewport de l'éditeur, avec isolation du
document (on joue une copie sérialisée, jamais le monde d'édition) et pipeline de scripts
C# rechargeable (compilation `dotnet build`, chargement en `AssemblyLoadContext`
collectible, déchargement vérifié).

Basé sur l'analyse [analysis-play-in-editor.md](../audits/analysis-play-in-editor.md)
(constats vérifiés : `EditorSimulation` définie mais inutilisée, gates par frame vs gates
au chargement, `Assembly.LoadFile` verrouillant le DLL, proxies instanciés même en preview).

Légende : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

Règles d'exécution pour les agents :

- branche de travail : `play-in-editor` (créée depuis `main`) ;
- **un commit par tâche terminée**, message `play-in-editor: <résumé tâche>`, en mettant à
  jour l'icône de statut de la tâche dans ce fichier **dans le même commit** ;
- ne jamais committer `CasaEngine.Launcher/Program.cs` (modification locale utilisateur)
  ni `Projects/SampleProject/.casaeditor/` ;
- chaque tâche doit builder (`dotnet build CasaEngine.Editor.MonoGame.sln`) et les tests
  du périmètre touché doivent passer ; la suite complète a **18 échecs préexistants sur
  HEAD** (baseline à ne pas aggraver) ;
- pas de renommage d'API publique, pas de changement de format de sérialisation autre
  qu'additif ;
- toute validation visuelle impossible en headless est marquée 🧪 avec la procédure
  manuelle décrite.

## Décisions d'architecture (fixées par l'analyse, ne pas rediscuter en cours de tâche)

1. **Jouer une copie** : au Play, le monde d'édition est sérialisé en `JObject` en mémoire
   (`EditorEntityJsonSerializer.SaveWorld`), un monde de play est créé via
   `new World()` + `Load(JObject)` puis `GameManager.SetWorldToLoad(World)` (chemin
   `_isNewWorld` → `LoadContent` + `BeginPlay`). Le monde d'édition est mis de côté
   **sans `Clear()`**.
2. **Restauration** : `SetWorldToLoad(World)` repasserait par `LoadContent` et dupliquerait
   les entités du monde d'édition → nouvelle API additive `GameManager.RestoreWorld(World)`
   qui réinstalle le monde sans `LoadContent`/`BeginPlay` et notifie `WorldChanged`.
3. **Policy** : Play = `GameplayExecutionPolicies.EditorSimulation`, édition =
   `EditorPreview`. La bascule se fait avant le swap de monde (gates de chargement).
4. **Caméra** : en Play, la RenderView du viewport est pilotée par la première
   `CameraComponent` du monde joué (même règle que `DefaultRuntimeViewBootstrapper`),
   sinon `CreateDefaultCamera()`. Au Stop, la caméra d'édition est restaurée.
5. **Scripts** : les types moteur (`CasaEngine.*`, MonoGame) doivent toujours être résolus
   dans l'ALC par défaut ; seul le DLL de gameplay (et ses dépendances privées) vit dans
   l'ALC collectible. `ElementFactory` doit pouvoir désenregistrer une assembly.
6. **Échec de build scripts** = on reste en édition, erreurs dans les Logs, pas de Play.
7. **Exception de script en Play** = arrêt propre du Play + erreur loggée (fail-stop,
   pas de catch silencieux par frame).

## Phase 0 — Cadrage

- ✅ T0.1 — Créer la branche `play-in-editor`, committer l'analyse
  (`ai-agent/audits/analysis-play-in-editor.md`) et ce plan.
  _Commit : `play-in-editor: analysis + execution plan`_

## Phase 1 — Fondations services (testables sans UI)

- ✅ T1.1 — `EditorPlayModeService` (nouveau, `CasaEngine.EditorServices/PlayMode/`) :
  machine à états `Editing → Starting → Playing ⇄ Paused → Stopping → Editing`,
  événements `StateChanged`, garde-fous (Play impossible si déjà en play, Stop idempotent),
  callbacks injectés (`onStart`, `onStop`, `onPause`) pour découpler de l'éditeur.
  Tests unitaires des transitions et des refus.
  _Commit : `play-in-editor: play mode state machine`_

- ✅ T1.2 — Snapshot/copie de monde : `EditorWorldPlaySnapshot` (EditorServices) —
  `Capture(World) : JObject` (via `EditorEntityJsonSerializer.SaveWorld`) et
  `CreatePlayWorld(JObject) : World` (`new World()` + `Load`, copie `Name`/`FileName`).
  Tests : round-trip d'un monde avec entité inline (AssetId vide), `script_class_name`,
  `space_policy`, environnement ; le monde source n'est pas modifié.
  _Commit : `play-in-editor: world play snapshot service`_

- ✅ T1.3 — `GameManager.RestoreWorld(World)` (runtime, additif) : réinstalle un monde
  déjà chargé comme `CurrentWorld` sans repasser par `LoadContent`/`BeginPlay`, annule un
  `_worldToLoad`/`_isNewWorld` en attente, notifie `WorldChanged`. Test unitaire (le monde
  restauré ne subit ni `Clear` ni re-`LoadContent` — vérifiable par compteur d'entités et
  absence de doublons).
  _Commit : `play-in-editor: GameManager.RestoreWorld`_

## Phase 2 — Intégration éditeur

- ✅ T2.1 — Orchestration Play/Stop dans `GameEditor` : instancie
  `EditorPlayModeService` ; Start = snapshot → policy `EditorSimulation` →
  `SetWorldToLoad(playWorld)` ; Stop = `playWorld.Clear()` → policy `EditorPreview` →
  `RestoreWorld(editWorld)` ; exceptions pendant `UpdateHost` en Play → Stop propre +
  `Logs.WriteError`. Raccourcis F5 (Play/Stop) sans UI dédiée à ce stade.
  _Commit : `play-in-editor: editor play/stop orchestration`_

- 🧪 T2.2 — Caméra + input de jeu dans `WorldViewportPanel` : mode Play (flag posé par
  `GameEditor`) — bind de la caméra du monde joué sur `_renderView.Camera`
  (première `CameraComponent`, sinon défaut), bypass du `SynchronizeCamera`/gizmo/
  contrôleur caméra éditeur pendant le Play, restauration au Stop ; assignation
  `InputRouter.AssignPlayer(PlayerIndex.One, viewportViewId)` + focus clavier sur la vue
  au démarrage du Play. 🧪 validation visuelle manuelle (procédure en fin de fichier).
  _Commit : `play-in-editor: viewport game camera and input`_

- 🧪 T2.3 — Toolbar Play/Pause/Stop : boutons dans la zone du viewport monde
  (états selon `EditorPlayModeService.State`), Pause via `GameManager.TimeScale = 0`
  (restauré au Resume/Stop), liseré coloré du viewport pendant le Play.
  🧪 validation visuelle manuelle.
  _Commit : `play-in-editor: play toolbar and viewport tint`_

- ✅ T2.4 — Verrous d'édition pendant le Play : sauvegarde projet/monde refusée avec
  message clair, enregistrement d'historique undo/redo suspendu, fermeture/changement de
  projet bloqués ; panneaux entités/inspecteur restent consultables (lecture de la
  hiérarchie runtime, comportement standard des moteurs modernes).
  _Commit : `play-in-editor: edit locks during play`_

## Phase 3 — Pipeline scripts (DLL à la volée)

- ✅ T3.1 — `ScriptAssemblyHost` (nouveau, `CasaEngine/Engine/Plugins/`) : ALC
  `isCollectible: true` + `AssemblyDependencyResolver`, règle de résolution « tout
  `CasaEngine.*`/MonoGame/déjà chargé dans l'ALC défaut → défaut », `Load(dllPath)`,
  `Unload()` avec vérification `WeakReference` + GC (diagnostic si l'ALC survit),
  état `IsLoaded`/`LoadedAssembly`. Tests : assembly de test compilée à la volée
  (Roslyn, déjà dépendance du repo) contenant un `GameplayProxy` dérivé ; vérifier
  l'identité de type (`typeof(GameplayProxy)` partagé), le chargement, le déchargement
  effectif, et le rechargement d'une v2.
  _Commit : `play-in-editor: collectible script assembly host`_

- ✅ T3.2 — `ElementFactory` rechargeable : API `RegisterAssembly`/`UnregisterAssembly`
  (ou invalidation par ALC), exclusion des assemblies collectibles déchargées du scan
  AppDomain, rebuild du cache après unload ; comportement existant inchangé pour le
  runtime standalone. Tests : un type d'une assembly déchargée n'est plus résolu ;
  un type de la v2 rechargée est résolu.
  _Commit : `play-in-editor: reloadable ElementFactory`_

- ✅ T3.3 — Chemin éditeur sans verrou de fichier : hook injectable pour le chargement du
  DLL gameplay (`ProjectSettingsHelper` n'appelle plus `Assembly.LoadFile` en dur) ;
  l'éditeur fournit un chargeur basé `ScriptAssemblyHost` avec shadow-copy du DLL ;
  le runtime standalone garde le comportement actuel par défaut. Test : ouverture d'un
  projet avec DLL via le hook → fichier d'origine non verrouillé (supprimable).
  _Commit : `play-in-editor: unlockable gameplay dll loading in editor`_

- ✅ T3.4 — `ProjectSettings.GameplayCsprojName` (additif) : lecture optionnelle
  (`rootElement["GameplayCsprojName"]?`), écriture seulement si non vide ; exposé dans
  les settings projet. Test de sérialisation additive (ancien JSON sans le champ charge
  sans erreur).
  _Commit : `play-in-editor: gameplay csproj project setting`_

- ✅ T3.5 — `EditorScriptBuildService` (EditorServices) : `dotnet build <csproj>
  -c Debug -o <projet>/.casaeditor/script-build/<timestamp>/` hors-process, capture
  stdout/stderr, parsing des erreurs MSBuild (`fichier(ligne,col): error CODE: message`)
  vers `Logs.WriteError`, résultat typé (succès + chemin DLL / échec + diagnostics).
  Test d'intégration : csproj fixture minimal compilé réellement (SDK présent sur la CI
  de dev), erreur de syntaxe → diagnostics remontés.
  _Commit : `play-in-editor: out-of-process script build service`_

- 🧪 T3.6 — Orchestration reload complète : coordinateur éditeur qui, au Play (ou via
  menu « Build Scripts ») quand `GameplayCsprojName` est configuré et que des sources
  `*.cs` sont plus récentes que le DLL chargé : build → si échec stop (erreurs logs) ;
  si succès : teardown (Clear du monde de play éventuel, monde d'édition rechargé depuis
  snapshot pour lâcher les anciens proxies), `ElementFactory.UnregisterAssembly`,
  `ScriptAssemblyHost.Unload()`, chargement du nouveau build, rebuild caches, rechargement
  du monde d'édition. 🧪 validation manuelle sur projet réel (RPGDemo).
  _Commit : `play-in-editor: script hot reload orchestration`_

## Phase 4 — Finition

- ✅ T4.1 — Documentation : `docs/editor/play-in-editor.md` (fonctionnement, limites,
  procédure scripts), index `docs/README.md`, table des tâches `ai-agent/README.md`,
  renvoi depuis l'audit.
  _Commit : `play-in-editor: documentation`_

- ✅ T4.2 — Vérification finale : build solution complet, suite de tests complète
  comparée à la baseline (18 échecs préexistants), correction des régressions
  éventuelles, passe de vérification indépendante, mise à jour des statuts de ce plan,
  rapport final (fichiers changés / validations / risques / suites).
  _Commit : `play-in-editor: final verification`_

## Résultat de la vérification finale (2026-08-13)

- Build `CasaEngine.Editor.MonoGame.sln` : 0 erreur (rebuild complet confirmé).
- Suite de tests : 5 exécutions consécutives stables à **18 échecs / 1005 réussites /
  1023 tests** — exactement la baseline préexistante de `main`, aucune régression ;
  les 37 nouveaux tests passent. Une instabilité initiale (vérification GC de
  l'unload sous exécution parallèle + fenêtre de NRE dans `ElementFactory.
  InvalidateCaches`) a été corrigée (commit `stabilize unload verification`).
- Passe de vérification indépendante : **CONFIRMED** (build, suite complète 2×,
  9 classes de tests en isolation, inspection des 6 garanties comportementales,
  compatibilité de sérialisation additive).
- Avis différés (non bloquants, hors périmètre du claim) :
  - **P3** : les dossiers shadow-copy `%TEMP%/casaeditor-scripts/<guid>/` ne sont
    jamais nettoyés (un par Play/reload) — follow-up à faire dans
    `EditorScriptAssemblyService.Unload()` + purge au démarrage ;
  - **P4** : `ElementFactory._scriptAssemblies` (List) non thread-safe si un
    `AssemblyLoad` concurrent survient pendant un rebuild de cache — moteur
    mono-thread par design, à durcir si un usage multi-thread apparaît.

## Hors périmètre (backlog, ne pas implémenter ici)

- Frame-step (avance image par image), caméra « eject » pendant le Play.
- HUD / `GameScreenManager` / UI screens du jeu rendus dans la vue du viewport.
- `FileSystemWatcher` sur les sources + indicateur « scripts obsolètes ».
- « Play from here » (spawn du pawn à la caméra), édition inspecteur « runtime only ».
- Scaffold csproj gameplay dans `CreateProject` (à faire avec un template projet dédié).
- Multi-instance / plusieurs mondes de play simultanés (statiques globaux non prêts).

## Limites connues (assumées, à documenter en T4.1)

- La copie de play reflète la **sémantique de sauvegarde** : les entités issues d'assets
  (`EntityReference.AssetId != vide`) rechargent le `.entity` depuis le disque (seul le
  transform initial est porté par le monde) — des modifications d'asset non sauvegardées
  ne sont pas visibles en Play. C'est cohérent avec le format de fichier actuel.
- Pendant le Play, les mondes de preview (particules, matériaux…) tournent sous la policy
  `EditorSimulation` du jeu hébergé ; leurs gates `SimulateInEditor` restent la protection
  principale.

## Procédures de validation manuelle (🧪)

1. Ouvrir `Projects/RPGDemo/RPGDemo.json` (ou SampleProject) dans l'éditeur.
2. F5 / bouton Play : le viewport passe en caméra de jeu, liseré actif, la physique et
   les scripts tournent ; le panneau entités montre la hiérarchie runtime.
3. Déplacer/détruire des entités par gameplay, puis Stop : le monde d'édition revient
   exactement à l'état d'avant Play (sélection, caméra éditeur, undo intacts).
4. Pause : gameplay figé (TimeScale 0), Resume reprend.
5. Scripts : modifier un `.cs` du projet gameplay, Play → build automatique, nouvelles
   valeurs visibles ; erreur de compilation → Play refusé, erreurs dans les Logs.
