# Play-in-Editor

Tester le niveau en cours d'édition directement dans le viewport de l'éditeur,
comme dans les moteurs modernes : Play / Pause / Stop, gameplay réel (scripts,
physique, controllers), et retour exact à l'état d'édition au Stop.

## Utilisation

- **Play** : bouton ▶ de la toolbar du viewport monde, ou `F5`.
- **Stop** : décocher le bouton ▶, ou `Maj+F5`.
- **Pause / Reprise** : bouton ⏸ de la toolbar (fige gameplay, animations et physique
  via `GameManager.TimeScale = 0`).
- Un liseré orange entoure le viewport pendant la session de play.
- Cliquer dans le viewport donne le focus clavier au gameplay ; les raccourcis de
  l'éditeur (undo/redo, etc.) sont suspendus pendant le Play.

## Fonctionnement

Au Play, l'éditeur **joue une copie** du monde :

1. le monde d'édition est sérialisé en mémoire (même format qu'un fichier `.world`) ;
2. un monde de play est créé depuis ce JSON et chargé par le runtime hébergé sous la
   politique `GameplayExecutionPolicies.EditorSimulation` (BeginPlay, scripts,
   physique, player controllers actifs) ;
3. la caméra de la RenderView du viewport bascule sur la première `CameraComponent`
   du monde joué (ou une caméra par défaut), et le joueur 1 est routé sur la vue du
   viewport ;
4. au Stop, le monde de play est détruit (`Clear`), la politique repasse en
   `EditorPreview` et le monde d'édition — jamais modifié — est réinstallé
   (`GameManager.RestoreWorld`), caméra éditeur et undo intacts.

Pendant la session : sauvegarde du projet refusée, historique undo/redo suspendu
(`EditorHistoryService.IsSuspended`), changement de projet bloqué. Une exception non
gérée d'un script arrête proprement la session (fail-stop) au lieu de tuer l'éditeur.

## Scripts C# : compilation et rechargement à la volée

Deux réglages projet pilotent les scripts :

- `GameplayDllName` (existant) : nom du DLL gameplay chargé à l'ouverture du projet ;
- `GameplayCsprojName` (nouveau, optionnel) : chemin — relatif au dossier projet —
  du csproj des scripts. S'il est renseigné, l'éditeur sait recompiler.

Comportement éditeur :

- le DLL gameplay est chargé via une **copie shadow** dans un
  `AssemblyLoadContext` collectible (`ScriptAssemblyHost`) : le fichier d'origine
  n'est jamais verrouillé et reste recompilable pendant que le projet est ouvert ;
- au Play (ou via **File → Build Scripts**), si des sources `*.cs` sont plus récentes
  que le DLL chargé : `dotnet build` hors-process vers
  `<projet>/.casaeditor/script-build/<timestamp>/`, erreurs MSBuild remontées dans le
  panneau Logs ; en cas d'échec, le Play est annulé et l'ancien DLL reste actif ;
- en cas de succès : le monde d'édition est resérialisé puis rechargé (pour lâcher
  les anciennes instances de `GameplayProxy`), l'historique undo du monde est vidé,
  l'ancienne assembly est désenregistrée d'`ElementFactory` et son contexte déchargé
  (vérification par `WeakReference` ; un avertissement signale toute fuite), puis le
  nouveau DLL est chargé et les proxies recréés à partir des `script_class_name`
  sérialisés.

Le runtime standalone (Launcher) garde le chargement direct (`Assembly.LoadFile`) :
pas de rechargement en jeu final.

## Validation automatisée

L'éditeur embarque un scénario d'automation qui déroule une session complète
(Play → Pause → Resume → Stop) et vérifie chaque invariant (swap de monde, policy,
caméra, TimeScale, restauration à l'instance près), avec captures d'écran :

```text
CasaEngine.Editor.exe --project <projet.json> --play-smoke --capture-delay 2 ^
  --diagnostics-out diag.txt --screenshot-out shot.png
```

Le rapport (`Play smoke:` dans le fichier de diagnostics) liste les PASS/FAIL et le
chemin de l'assembly de scripts active (preuve du rechargement). Validé sur
SampleProject et RPGDemo (rebuild, no-rebuild et échec de build).

## Limites connues

- La copie de play suit la **sémantique de sauvegarde** : les entités issues d'assets
  (`EntityReference` avec `AssetId`) rechargent le `.entity` depuis le disque — des
  modifications d'asset non sauvegardées ne sont pas visibles en Play.
- Les UI screens MGUI du jeu ne sont pas encore hébergés dans la vue du viewport ;
  le rendu direct des scripts (ex. barre de vie dessinée en `Draw`) fonctionne.
- Après un build réussi, le DLL fraîchement compilé est recopié sur le DLL canonique
  du projet (`GameplayDllName`) — jamais verrouillé grâce à la copie shadow.
- Pas de frame-step ni de caméra libre (« eject ») pendant le Play.
- Le rechargement de scripts vide l'historique undo du monde.
- `TimeScale` s'applique désormais aussi à la physique (pause cohérente) ; un jeu qui
  utilisait `TimeScale` en s'attendant à une physique non ralentie doit être adapté.
- Un seul monde de play à la fois (état moteur global).

## Points d'entrée dans le code

| Élément | Fichier |
| --- | --- |
| Machine à états Play | `CasaEngine.EditorServices/PlayMode/EditorPlayModeService.cs` |
| Snapshot/copie de monde | `CasaEngine.EditorServices/PlayMode/EditorWorldPlaySnapshot.cs` |
| Session éditeur (policy, swap, restore) | `CasaEngine.Editor/PlayMode/EditorPlaySessionController.cs` |
| Caméra/input/toolbar viewport | `CasaEngine.Editor/Controls/WorldViewportPanel.cs` |
| Restauration sans rechargement | `CasaEngine/Framework/Application/GameManager.RestoreWorld` |
| ALC collectible | `CasaEngine/Engine/Plugins/ScriptAssemblyHost.cs` |
| Shadow copy + registre | `CasaEngine.EditorServices/Scripting/EditorScriptAssemblyService.cs` |
| Build hors-process | `CasaEngine.EditorServices/Scripting/EditorScriptBuildService.cs` |
| Orchestration reload | `CasaEngine.EditorServices/Scripting/EditorScriptReloadCoordinator.cs` |

## Suites possibles

Frame-step, caméra eject, HUD dans le viewport, watcher de sources avec build
automatique, « play from here », scaffold du csproj gameplay dans `CreateProject`.
