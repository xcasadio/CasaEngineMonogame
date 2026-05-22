# Plan agent IA — Coroutines V1 CasaEngine

## Analyse critique

Statut : aucune decision critique bloquante restante apres l'ajout de `# Decisions V1 validees` dans `docs/coroutines_specifications.md`.

Les points precedemment ambigus sont maintenant tranches : scope par `World`, manager par monde, `FrameTime`, pause via `TimeScale`, ownership, structure robuste de `CoroutineHandle`, comportement de `yield return CoroutineHandle`, exceptions, phase unique `Update`, et debug API sans fenetre editeur obligatoire.

Points d'adaptation non bloquants au code existant :

- Le chemin reel est `CasaEngine/Framework/Scripting/Coroutines`, pas un projet `CasaEngine.Framework` separe.
- Les composants CasaEngine sont des `EntityComponent`, pas des `Component` Unity-like.
- L'API `World.Update(float)` doit rester compatible et deleguer vers une surcharge `World.Update(FrameTime)`.

## Regles d'execution de l'agent

- Apres chaque tache terminee, mettre a jour l'icone de statut dans ce fichier.
- Chaque tache terminee doit etre commitee separement avec un message explicite.
- Les changements non lies deja presents dans le workspace ne doivent pas etre inclus dans les commits.
- Executer au minimum un test cible apres chaque tranche de code quand c'est possible.
- Executer un build de solution avant de terminer.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Taches

### ✅ Tache 1 — Plan agent et analyse

Objectif : formaliser l'analyse critique et le plan executable.

Actions :

- Creer ce fichier de plan.
- Confirmer que la section `Decisions V1 validees` supprime les points critiques.
- Committer uniquement le plan.

Validation :

- Verification git des fichiers inclus dans le commit.

### ✅ Tache 2 — FrameTime runtime compatible

Objectif : introduire le temps frame complet sans casser les APIs existantes.

Actions :

- Ajouter `FrameTime` dans `CasaEngine.Core.Time`.
- Ajouter une factory depuis `GameTime` et une factory depuis `float elapsedTime`.
- Ajouter `TimeScale`, `DeltaTime`, `UnscaledDeltaTime`, `TotalTime`, `UnscaledTotalTime`, `FrameIndex`.
- Conserver les helpers existants de `GameTimeHelper`.

Validation :

- Tests unitaires `FrameTime`.
- Build du projet `CasaEngine` si necessaire.

### ✅ Tache 3 — Noyau CoroutineManager

Objectif : creer le scheduler sans integration monde.

Actions :

- Ajouter `CoroutineHandle` avec `ManagerId`, `Slot`, `Generation`, `IsValid`.
- Ajouter `CoroutineUpdateContext`.
- Ajouter `ICoroutineManager`.
- Ajouter `CoroutineManager` et `CoroutineInstance`.
- Supporter `StartCoroutine`, `StopCoroutine`, `StopAllCoroutines`, `IsRunning`, fin naturelle et `yield return null`.
- Gerer les handles obsoletes via generation.

Validation :

- Tests unitaires fin naturelle, handle obsolete, stop, `yield return null`.

### ✅ Tache 4 — Instructions V1 et coroutines imbriquees

Objectif : couvrir les instructions V1 de la specification.

Actions :

- Ajouter `ICoroutineInstruction`.
- Ajouter `WaitForSeconds`.
- Ajouter `WaitForSecondsRealtime`.
- Ajouter `WaitForFrames`.
- Ajouter `WaitUntil`.
- Ajouter `WaitWhile`.
- Supporter `yield return IEnumerator`.
- Supporter `yield return CoroutineHandle` pour le meme manager.

Validation :

- Tests unitaires timing scaled/unscaled, frames, conditions, IEnumerator imbrique, CoroutineHandle imbrique, handle invalide, self-wait.

### ✅ Tache 5 — Integration World et ownership

Objectif : attacher les coroutines V1 au `World` et au cycle de vie gameplay.

Actions :

- Ajouter `CoroutineManager` au `World`.
- Ajouter `World.Update(FrameTime)` et garder `World.Update(float)` compatible.
- Mettre a jour `GameManager.UpdateWorld(GameTime)` pour construire un `FrameTime`.
- Stopper les coroutines du monde dans `World.Clear()`.
- Stopper les coroutines d'une `Entity` dans `Entity.Destroy()`.
- Stopper les coroutines d'une `Entity` lors du retrait effectif du `World`.
- Stopper les coroutines d'un `EntityComponent` dans `Detach()`.

Validation :

- Tests unitaires/integration ownership entity/component/world.

### ⏳ Tache 6 — Helpers composants et debug API

Objectif : exposer l'API pratique et les donnees de debug V1.

Actions :

- Ajouter helpers protected/public adaptes sur `EntityComponent` pour demarrer/arreter avec owner composant.
- Ajouter helpers sur `Entity` si necessaire pour owner entite.
- Ajouter `CoroutineDebugInfo`.
- Ajouter `GetActiveCoroutines()`.
- Ajouter nommage via overload ou setter selon compatibilite minimale.
- Logger les exceptions avec nom et owner.
- Ajouter mode debug strict configurable.

Validation :

- Tests debug info, noms, owner name, exception stoppee, strict mode.

### ⏳ Tache 7 — Validation finale solution

Objectif : verifier que le repo reste compilable avec les changements.

Actions :

- Executer les tests coroutine cibles.
- Executer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Coroutines`.
- Executer un build local de solution.
- Committer la mise a jour finale du plan si necessaire.

Validation :

- Tests coroutine verts.
- Build solution vert ou probleme externe documente.