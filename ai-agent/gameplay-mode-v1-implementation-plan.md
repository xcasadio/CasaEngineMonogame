# Plan agent IA - GameplayMode V1 CasaEngine

## Objectif

Implementer la V1 runtime de `GameplayMode` decrite dans `docs/gameplay-mode.md`, sans supprimer l'ancien `GameMode` tant que sa configuration de pawn/controller est encore consommee par `World`.

## Regles d'execution de l'agent

- Mettre a jour l'icone de statut dans le titre de chaque tache avant le commit qui termine la tache.
- Faire un commit separe par tache terminee.
- Ne jamais inclure les changements non lies deja presents dans le workspace.
- Garder la V1 limitee au runtime : pas d'asset data-driven, pas d'objectifs, pas de bus d'evenements.
- Lancer un test cible apres chaque tranche de code quand un test existe.
- Lancer un build ou test global raisonnable avant la fin.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Taches

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

### ⏳ Tache 2 - Contrats runtime GameplayMode

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

### ⏳ Tache 3 - Runner GameplayMode

Objectif : ajouter le cycle de vie runtime du mode actif.

Actions :

- Ajouter `GameplayModeRunner`.
- Implementer `Start`, `Update`, `Pause`, `Resume`, `Stop`, `Restart`, `Abort` si necessaire pour couvrir l'API V1.
- Mettre a jour `GameplayState.Phase`, `GameplayState.Result` et `GameplayState.ElapsedTime`.
- Eviter les allocations dans `Update`.

Validation :

- Test cible du runner si deja present, sinon build cible.
- Committer uniquement le runner et le statut de cette tache.

### ⏳ Tache 4 - Integration World minimale

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

### ⏳ Tache 5 - Tests unitaires V1

Objectif : couvrir le comportement minimal du runner et de l'integration monde.

Actions :

- Ajouter des tests `GameplayModeRunner`.
- Verifier `Start`, `Update`, `Success`, `Failure`, `Pause`, `Resume`, `Stop`.
- Ajouter un test d'integration leger sur `World.SetGameplayMode` et `World.Update(FrameTime)`.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter GameplayMode`.
- Committer uniquement les tests et le statut de cette tache.

### ⏳ Tache 6 - Validation finale V1

Objectif : verifier que la V1 compile avec le workspace courant.

Actions :

- Lancer un build ou test global raisonnable.
- Noter les echecs non lies s'il y en a.
- Mettre tous les statuts V1 a jour.

Validation :

- Commande de validation finale documentee dans le message de fin.
- Committer uniquement le statut final du plan si necessaire.