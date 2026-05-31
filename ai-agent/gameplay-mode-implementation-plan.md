# Plan agent IA - GameplayMode CasaEngine

## Objectif

Implementer `GameplayMode` dans CasaEngine en deux temps :

- V1 : runtime minimal deja en place.
- V2 : migration complete hors de l'ancien `GameMode`, objectifs, evenements gameplay, asset de configuration et tests.

La classe legacy `GameMode` doit etre supprimee seulement apres migration de sa vraie responsabilite restante : la configuration de demarrage joueur consommee par `World`.

## Regles d'execution de l'agent

- Mettre a jour l'icone de statut dans le titre de chaque tache avant le commit qui termine la tache.
- Faire un commit separe par tache terminee.
- Ne jamais inclure les changements non lies deja presents dans le workspace.
- Garder les changements par tache petits et commitables.
- Lancer un test cible apres chaque tranche de code quand un test existe.
- Lancer un build ou test global raisonnable avant la fin.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Taches V1

### ✅ Tache 0 - Document d'orientation commite

Objectif : conserver la critique et la direction architecture dans l'historique Git avant de toucher au code.

Actions :

- Committer `docs/gameplay-mode.md` seul.
- Laisser les changements non lies hors commit.

Validation :

- Commit `docs: define gameplay mode direction` cree.

### ✅ Tache 1 - Plan agent V1

Objectif : creer ce plan executable par un agent IA.

Actions :

- Creer `ai-agent/gameplay-mode-v1-implementation-plan.md`.
- Decouper la V1 en taches petites et commitables.
- Documenter la regle de mise a jour des statuts.

Validation :

- Verification du diff cible sur ce fichier.
- Committer uniquement ce plan.

### ✅ Tache 2 - Contrats runtime GameplayMode

Objectif : ajouter les types runtime purs de la V1.

Actions :

- Ajouter `GameplayResult`.
- Ajouter `GameplayPhase`.
- Ajouter `GameplayState`.
- Ajouter `GameplayContext` base sur `World` et `CoroutineManager`.
- Ajouter `GameplayMode` base sur `FrameTime`.

Validation :

- Build cible du projet `CasaEngine` ou test compile cible.
- Committer uniquement les contrats runtime et le statut de cette tache.

### ✅ Tache 3 - Runner GameplayMode

Objectif : ajouter le cycle de vie runtime du mode actif.

Actions :

- Ajouter `GameplayModeRunner`.
- Implementer `Start`, `Update`, `Pause`, `Resume`, `Stop`, `Restart`, `Abort` si necessaire pour couvrir l'API V1.
- Mettre a jour `GameplayState.Phase`, `GameplayState.Result` et `GameplayState.ElapsedTime`.
- Eviter les allocations dans `Update`.

Validation :

- Test cible du runner si deja present, sinon build cible.
- Committer uniquement le runner et le statut de cette tache.

### ✅ Tache 4 - Integration World minimale

Objectif : brancher le runner dans `World` sans casser l'ancien `GameMode`.

Actions :

- Exposer un `GameplayModeRunner` sur `World`.
- Ajouter `SetGameplayMode(GameplayMode mode)`.
- Appeler `GameplayModeRunner.Update(frameTime)` a l'emplacement actuel de `GameMode.Tick()`.
- Appeler `GameplayModeRunner.Stop()` dans `World.Clear()`.
- Garder `GameMode`, `GameModeAssetId`, `DefaultPawnAssetId` et `PlayerControllerClass` compatibles.

Validation :

- Build cible du projet `CasaEngine`.
- Committer uniquement l'integration `World` et le statut de cette tache.

### ✅ Tache 5 - Tests unitaires V1

Objectif : couvrir le comportement minimal du runner et de l'integration monde.

Actions :

- Ajouter des tests `GameplayModeRunner`.
- Verifier `Start`, `Update`, `Success`, `Failure`, `Pause`, `Resume`, `Stop`.
- Ajouter un test d'integration leger sur `World.SetGameplayMode` et `World.Update(FrameTime)`.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter GameplayMode`.
- La commande cible peut etre bloquee par des erreurs de compilation existantes dans d'autres tests ; verifier que les nouveaux fichiers GameplayMode n'apparaissent pas dans les erreurs.
- Committer uniquement les tests et le statut de cette tache.

### ⚠️ Tache 6 - Validation finale V1

Objectif : verifier que la V1 compile avec le workspace courant.

Actions :

- Lancer un build ou test global raisonnable.
- Noter les echecs non lies s'il y en a.
- Mettre tous les statuts V1 a jour.

Validation :

- `dotnet build CasaEngine/CasaEngine.csproj --no-restore` : OK, avec avertissements existants.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter GameplayMode` : bloque pendant la compilation du projet de tests sur des erreurs existantes hors GameplayMode ; aucune erreur `GameplayMode` trouvee dans la sortie.
- `dotnet build CasaEngine.MonoGame.sln --no-restore` : bloque dans `CasaEngine.RPGDemo` sur `SceneComponent.Coordinates`, hors scope V1.
- Committer uniquement le statut final du plan.

## Taches V2

### ✅ Tache 7 - Plan V2 et ordre de suppression GameMode

Objectif : etendre ce plan avec les taches V2 et placer explicitement la suppression de `GameMode` apres la migration de la configuration joueur.

Actions :

- Ajouter les taches V2 dans ce fichier.
- Identifier que `GameMode` ne peut etre supprime qu'apres migration de `DefaultPawnAssetId` et `PlayerControllerClass`.
- Committer uniquement le plan renomme/mis a jour.

Validation :

- Verification du diff cible sur ce fichier.

### ✅ Tache 8 - Extraire la configuration de demarrage joueur

Objectif : sortir de `GameMode` les donnees de pawn/controller encore utilisees par `World`.

Actions :

- Creer un type de configuration de demarrage joueur.
- Charger cette configuration depuis l'ancien asset reference par `game_mode_asset_id` pour compatibilite.
- Ajouter une propriete `PlayerStartupSettingsAssetId` sur `World`.
- Lire `player_startup_settings_asset_id` et garder la lecture legacy `game_mode_asset_id`.
- Adapter `InitializePlayerControllers()` pour ne plus lire `GameMode`.

Validation :

- `dotnet build CasaEngine/CasaEngine.csproj --no-restore`.
- Committer uniquement cette migration et le statut de la tache.

### ✅ Tache 9 - Supprimer la classe legacy GameMode

Objectif : retirer l'ancien `GameMode` une fois qu'il n'est plus necessaire au spawn joueur.

Actions :

- Supprimer `CasaEngine/Framework/Gameplay/GameMode.cs`.
- Supprimer `World.GameMode`, `GameModeAssetId`, `StartPlay()`, `Tick()` et les checks `HasMatchEnded()`.
- Supprimer l'enregistrement `AssetLoader<GameMode>()`.
- Supprimer ou adapter `RPGActionGameMode`.
- Mettre a jour les references doc/commentaires runtime qui parlaient de l'ancien type.

Validation :

- Recherche `GameMode` dans les fichiers C# : aucune reference code runtime restante.
- `dotnet build CasaEngine/CasaEngine.csproj --no-restore`.
- Committer uniquement la suppression legacy et le statut de la tache.

### ✅ Tache 10 - Etat V2 et contexte gameplay

Objectif : completer l'etat runtime et le contexte expose au mode.

Actions :

- Ajouter les variables runtime dans `GameplayState`.
- Ajouter un bus d'evenements gameplay dans `GameplayContext` ou le runner.
- Exposer le bus par `World` ou le runner sans service global.
- Garder `FrameTime` comme contexte de temps.

Validation :

- Build cible du projet `CasaEngine`.
- Committer uniquement l'etat/contexte V2 et le statut de la tache.

### ⏳ Tache 11 - Objectifs gameplay V2

Objectif : ajouter les objectifs composables sans scans couteux du `World` dans `Update`.

Actions :

- Ajouter `GameplayObjective`.
- Ajouter `ObjectiveGameplayMode`.
- Ajouter `CollectItemsObjective`.
- Ajouter `SurviveTimerObjective`.
- Eviter LINQ dans `Update` et `EvaluateResult`.

Validation :

- Build cible du projet `CasaEngine`.
- Committer uniquement les objectifs et le statut de la tache.

### ⏳ Tache 12 - Evenements gameplay V2

Objectif : permettre aux objectifs et modes d'ecouter des faits gameplay sans couplage direct.

Actions :

- Ajouter `IGameplayEvent`.
- Ajouter `IGameplayEventListener`.
- Ajouter `GameplayEventBus`.
- Ajouter `ItemCollectedEvent`.
- Enregistrer/desenregistrer les objectifs listeners dans `ObjectiveGameplayMode`.

Validation :

- Build cible du projet `CasaEngine`.
- Committer uniquement les evenements et le statut de la tache.

### ⏳ Tache 13 - Asset de configuration GameplayMode

Objectif : fournir un asset concret capable de creer un mode runtime sans ressusciter l'ancien `GameMode`.

Actions :

- Ajouter `GameplayModeAsset` comme asset concret de configuration.
- Permettre la creation d'un mode par nom de type via `ElementFactory`.
- Enregistrer le loader de `GameplayModeAsset`.
- Ajouter une propriete `GameplayModeAssetId` sur `World` separee de la configuration joueur.
- Demarrer le mode asset au chargement/debut de jeu si un asset est reference.

Validation :

- Build cible du projet `CasaEngine`.
- Committer uniquement l'asset gameplay et le statut de la tache.

### ⏳ Tache 14 - Tests V2

Objectif : couvrir la migration legacy, objectifs, evenements et assets.

Actions :

- Ajouter des tests de configuration joueur legacy JSON.
- Ajouter des tests `ObjectiveGameplayMode`.
- Ajouter des tests `GameplayEventBus` et `CollectItemsObjective`.
- Ajouter un test `GameplayModeAsset` si possible sans dependance projet.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter GameplayMode` si le projet de tests compile.
- Sinon verifier que les nouveaux fichiers n'ont pas d'erreurs et documenter le blocage existant.
- Committer uniquement les tests et le statut de la tache.

### ⏳ Tache 15 - Validation finale V2

Objectif : verifier que la V2 compile au niveau moteur et que les blocages restants sont hors scope.

Actions :

- Lancer `dotnet build CasaEngine/CasaEngine.csproj --no-restore`.
- Tenter `dotnet build CasaEngine.MonoGame.sln --no-restore`.
- Mettre a jour ce plan avec les resultats.

Validation :

- Committer uniquement le statut final du plan.