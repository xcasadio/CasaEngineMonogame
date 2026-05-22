# Plan agent IA — Cutscene V1 CasaEngine

## Analyse critique

Statut : les décisions V1 sont suffisantes pour construire un plan agent exécutable, à condition de ne pas élargir le scope.

Décisions validées :

- utiliser `World.CoroutineManager` comme scheduler unique ;
- ne pas créer de `CutsceneRunner` ;
- ajouter `CutsceneDirector` dans `World` ;
- ne pas ajouter d'`Update` séparé au `CutsceneDirector` ;
- limiter la V1 à `Wait`, `Sequence`, `Parallel`, `Stop`, debug, validation, sérialisation asset CasaEngine et éditeur lecture seule ;
- ne pas ajouter de commandes gameplay en V1 ;
- ne pas inventer de services fictifs ;
- charger `CutsceneAsset` via `AssetContentManager` et `IAssetLoader` ;
- utiliser des actions typées, pas `Dictionary<string, string>` ;
- ne pas implémenter `CompleteImmediately` en V1.

Surfaces repo déjà identifiées :

- `World.CoroutineManager` existe dans `CasaEngine/Framework/Scene/World/World.cs`.
- `CoroutineManager`, `CoroutineHandle`, `WaitForSeconds` et `CoroutineUpdateContext` existent dans `CasaEngine/Framework/Scripting/Coroutines`.
- Les loaders sont enregistrés dans `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs`.
- Le contrat loader est `CasaEngine/Framework/Assets/IAssetLoader.cs`.
- Le chargement passe par `CasaEngine/Framework/Assets/AssetContentManager.cs`.
- Les extensions d'assets sont dans `CasaEngine/Framework/Configuration/Constants.cs`.
- `AssetInfo.AssetType` est inféré par extension dans `CasaEngine/Framework/Assets/AssetInfo.cs`.

Points à confirmer avant code substantiel :

- le meilleur namespace/runtime folder pour les types cutscene ;
- la façon la plus compatible de sérialiser des actions typées avec `ObjectBase`/`ISerializable` ;
- l'emplacement exact de la vue éditeur lecture seule ;
- la possibilité de tester `World.CutsceneDirector` sans initialiser un `CasaEngineGame` complet.

## Règles d'exécution de l'agent

- Après chaque tâche terminée, mettre à jour l'icône de statut dans ce fichier.
- Respecter les instructions repo applicables aux fichiers touchés.
- Ne pas créer de `CutsceneRunner`, `CutsceneApi` async/await, ni de scheduler parallèle.
- Ne pas créer `InputManager`, `DialogueSystem`, `CameraManager`, `QuestSystem`, `AudioSystem`, `CharacterController` ou `Animator` pour satisfaire la V1.
- Ne pas ajouter de commande gameplay en V1.
- Ne pas utiliser LINQ/closures dans les chemins exécutés frame par frame.
- Exécuter au minimum les tests ciblés après chaque tranche de code quand c'est possible.
- Exécuter un build local de solution avant de terminer, ou documenter clairement le blocage externe.
- Ne pas committer sans demande explicite dans la session ; si des commits sont demandés, faire un commit atomique par sous-tâche.

## Légende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Conditions d'arrêt

L'agent doit s'arrêter et documenter le blocage si :

- il ne trouve pas où enregistrer `CutsceneAssetLoader` ;
- `ObjectBase` ou la sérialisation existante bloque le modèle typé proposé ;
- `World` ne peut pas recevoir `CutsceneDirector` proprement ;
- les tests nécessitent un `CasaEngineGame` complet non disponible ;
- une commande gameplay est demandée sans système cible identifié ;
- il faut changer l'ordre de `World.Update` ;
- la sérialisation existante impose un format différent ;
- l'éditeur lecture seule impose une refonte MGUI/docking hors scope.

## Hors scope V1

- `MoveActorTo`
- `PlayAnimation`
- `ShowDialogue`
- `CameraFocus`
- `DisablePlayerControl`
- `EnablePlayerControl`
- `SetQuestFlag`
- `CompleteImmediately`
- resource locks actor/camera/input/dialogue/animation
- timeline
- édition MGUI avancée
- preview avancée
- sauvegarde depuis l'UI

## Tâches

### ✅ Tâche 1 — Analyse et plan

Objectif : valider les décisions utilisateur et créer le plan agent strict.

Actions :

- Mettre à jour `docs/cutscene_commandes_sequentielles_async_coroutine.md` avec les décisions V1.
- Créer ce plan.
- Vérifier que les anciennes sections `CutsceneRunner`, async/await et commandes gameplay sont marquées comme alternatives futures ou hors scope V1.

Validation :

- Grep ciblé du document.
- Aucun diagnostic markdown bloquant.

### ✅ Tâche 2 — Ancrage runtime et sérialisation

Objectif : confirmer les surfaces exactes avant création de types.

Actions :

- Lire `ObjectBase`, `ISerializable`, `AssetSaver` et deux assets JSON existants proches.
- Confirmer si `CutsceneAsset` doit hériter de `ObjectBase`.
- Confirmer comment charger/sauver un arbre d'actions typées avec discriminateur `type`.
- Choisir le namespace et le dossier runtime, probablement `CasaEngine.Framework.Cutscenes`.
- Documenter tout écart dans ce plan avant code.

Validation :

- Modèle retenu : `CutsceneAsset : ObjectBase` dans `CasaEngine.Framework.Cutscenes`, sérialisation JSON dédiée avec `root_action.type`.
- Aucun blocage `ObjectBase`/sérialisation rencontré.

### ✅ Tâche 3 — Modèle d'actions V1

Objectif : ajouter le modèle runtime minimal.

Actions :

- Ajouter `CutsceneAsset`.
- Ajouter `CutsceneActionData` typé.
- Ajouter `WaitCutsceneActionData` avec `Seconds`.
- Ajouter `SequenceCutsceneActionData` avec liste d'actions.
- Ajouter `ParallelCutsceneActionData` avec liste d'actions.
- Ajouter `CutsceneValidationResult`, sévérités et messages.
- Ajouter validation V1 : `RootAction` obligatoire, type connu, `Wait.Seconds >= 0`, `Sequence` vide warning, `Parallel` vide warning, action inconnue erreur.

Validation :

- Tests unitaires ajoutés dans `CasaEngine.Tests/Cutscenes`.
- Pas de validation entity/dialogue/animation/camera/quest en V1.

### ✅ Tâche 4 — Loader asset CasaEngine

Objectif : charger les cutscenes par le système d'assets CasaEngine.

Actions :

- Ajouter `Constants.FileNameExtensions.Cutscene = ".cutscene"`.
- Ajouter `CutsceneAssetLoader : IAssetLoader`.
- Enregistrer le loader dans `AssetLoaderRegistry.RegisterLoaders`.
- S'assurer que `asset_type = "cutscene"` ou l'inférence par `.cutscene` fonctionne avec `AssetInfo`.
- Charger via `AssetContentManager.Load<CutsceneAsset>(id)`.
- Ne pas utiliser `File.ReadAllText` depuis le gameplay/director ; seul le loader peut lire le fichier.

Validation :

- Test de chargement d'un asset `.cutscene` minimal via `AssetContentManager` ajouté.
- Test d'erreur sur type inconnu couvert par la validation `UnknownCutsceneActionData`.

### ✅ Tâche 5 — Exécution coroutine des actions

Objectif : transformer les actions V1 en coroutines CasaEngine.

Actions :

- Ajouter un exécuteur interne qui produit un `IEnumerator` à partir de `CutsceneActionData`.
- `Wait` doit utiliser `WaitForSeconds` existant.
- `Sequence` doit exécuter les actions dans l'ordre.
- `Parallel` doit démarrer les actions enfants dans le même `World.CoroutineManager` et attendre tous les `CoroutineHandle`.
- Implémenter `Parallel` sans verrous de ressources en V1.
- Éviter LINQ et allocations répétées dans les boucles frame par frame.

Validation :

- Tests `Sequence`, `Parallel`, invalid asset et `Stop` ajoutés via `CutsceneDirectorTests`.

### ✅ Tâche 6 — CutsceneDirector dans World

Objectif : exposer le point d'entrée runtime sans boucle d'update dédiée.

Actions :

- Ajouter `CutsceneDirector`.
- Ajouter `World.CutsceneDirector { get; }`.
- Initialiser le director sans dépendre d'un `CasaEngineGame` complet si possible.
- `Play(CutsceneAsset asset)` valide l'asset puis démarre une coroutine via `World.CoroutineManager.StartCoroutine`.
- `Stop()` arrête le `CoroutineHandle` actif.
- `IsPlaying` reflète `CoroutineManager.IsRunning(handle)`.
- `GetDebugSnapshot()` expose asset courant, handle, état, erreurs de validation et coroutines actives liées à la cutscene.
- Ne pas ajouter `CutsceneDirector.Update`.
- Ne pas modifier l'ordre de `World.Update`.

Validation :

- Tests `Play`, fin de coroutine, `Stop`, `World.Clear` et snapshot runtime ajoutés.

### ✅ Tâche 7 — Debug runtime V1

Objectif : rendre l'état runtime lisible sans éditeur complet.

Actions :

- Ajouter `CutsceneDebugSnapshot`.
- Inclure nom/id asset si disponibles.
- Inclure `CoroutineHandle` actif.
- Inclure état `Idle`, `Playing`, `Completed`, `Stopped`, `Invalid` si ce découpage reste simple.
- Inclure les messages de validation.
- Inclure les coroutines actives liées à la cutscene via l'API debug existante du `CoroutineManager`.

Validation :

- Tests runtime du snapshot sans UI ajoutés.

### ✅ Tâche 8 — Éditeur lecture seule

Objectif : afficher les cutscenes sans permettre de les modifier.

Actions :

- Lire les instructions éditeur applicables avant toute modification.
- Identifier la surface existante pour ouvrir/afficher un asset dans l'éditeur.
- Ajouter une vue lecture seule si la surface existe clairement.
- Afficher l'arbre d'actions.
- Afficher les paramètres V1.
- Afficher les warnings/erreurs de validation.
- Afficher l'état runtime et les coroutines actives liées à la cutscene si un `World` runtime est disponible.
- Ne pas ajouter, supprimer, réordonner ou modifier des actions.
- Ne pas ajouter drag and drop, timeline, preview avancée ou sauvegarde depuis l'UI.

Validation :

- `CutsceneReadOnlyDocumentBuilderTests` couvre l'arbre, les paramètres, la validation et les snapshots runtime.
- Panneau MGUI lecture seule ajouté et branché dans `GameEditor` pour l'ouverture `.cutscene`.
- Aucune édition, sauvegarde, timeline, preview avancée ni drag/drop ajouté.

### ✅ Tâche 9 — Validation finale solution

Objectif : vérifier que la tranche V1 reste compilable et testée.

Actions :

- Exécuter les tests cutscene ciblés.
- Exécuter les tests coroutine ciblés si l'exécution parallèle touche leurs contrats.
- Exécuter un build local de solution.
- Mettre à jour ce plan avec les statuts finaux et les résultats.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Cutscenes` : vert, 11 tests.
- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` : vert, 0 warning, 0 erreur.

## Format `.cutscene` V1 attendu

Exemple conceptuel :

```json
{
  "name": "IntroWait",
  "root_action": {
    "type": "Sequence",
    "actions": [
      {
        "type": "Wait",
        "seconds": 0.5
      },
      {
        "type": "Parallel",
        "actions": [
          {
            "type": "Wait",
            "seconds": 1.0
          },
          {
            "type": "Wait",
            "seconds": 0.25
          }
        ]
      }
    ]
  }
}
```

Ce format reste à adapter exactement au contrat `ISerializable`/`ObjectBase` confirmé en tâche 2.