# Plan agent IA — Cutscene CasaEngine

## Analyse critique

Statut : la V1 n'est plus seulement un plan. Le repo contient déjà un runtime cutscene fonctionnel avec `Wait`, `Sequence`, `Parallel`, un premier `MoveTo` direct au-dessus du `CharacterController`, un `CutsceneDirector` branché à `World`, des tests runtime et une vue éditeur lecture seule.

Conséquence : ce fichier doit désormais servir à la fois d'historique V1 livré et de feuille de route V2 incrémentale, avec des étapes assez petites pour être validées visuellement entre deux tranches de code.

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

- `World.CoroutineManager` existe dans `CasaEngine/Framework/Scene/World/World.cs` et pointe vers `World.RuntimeSystems.CoroutineManager`.
- `CoroutineManager`, `CoroutineHandle`, `WaitForSeconds` et `CoroutineUpdateContext` existent dans `CasaEngine/Framework/Scripting/Coroutines`.
- Les loaders sont enregistrés dans `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs`.
- Le contrat loader est `CasaEngine/Framework/Assets/IAssetLoader.cs`.
- Le chargement passe par `CasaEngine/Framework/Assets/AssetContentManager.cs`.
- Les extensions d'assets sont dans `CasaEngine/Framework/Configuration/Constants.cs`.
- `AssetInfo.AssetType` est inféré par extension dans `CasaEngine/Framework/Assets/AssetInfo.cs`.

Surfaces V2 déjà présentes dans le repo :

- `MoveToCutsceneActionData` et `CharacterControllerMoveToDriverComponent` existent déjà.
- `CutsceneDirector.Stop()` annule et retire désormais les drivers `MoveTo` runtime encore actifs, puis annule les agents navigation activés par la cutscene.
- `NavigationGrid2D`, `GridPathfinder2D`, `NavigationAgentComponent`, `CharacterControllerNavigationDriverComponent` et `CharacterControllerSteeringBridgeComponent` existent déjà côté runtime.
- l'éditeur sait déjà afficher un asset `.cutscene` en lecture seule.

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
- Les démos de validation visuelle doivent être validées par l'utilisateur ; l'agent peut les implémenter, les lancer et préparer le protocole de vérification, mais pas déclarer la validation visuelle à sa place.
- Pour la V2, avancer par petites tranches : runtime ciblé, tests ciblés, démo minimale, validation visuelle utilisateur, puis seulement la tranche suivante.

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

## Cadence de validation V2

Le but de la V2 n'est pas de brancher toute la navigation en une seule passe. L'ordre d'exécution attendu est le suivant :

1. consolider le direct `MoveTo` comme tranche de référence ;
2. ajouter une démo minimale visible et demander une validation visuelle utilisateur ;
3. seulement ensuite figer le contrat d'action `NavigateTo` ou `FollowEntity` ;
4. implémenter le bridge cutscene -> navigation en gardant la même discipline de test ciblé puis démo ciblée ;
5. ajouter une seconde validation visuelle utilisateur avant d'élargir le scope.

Une tâche de démo ne peut donc pas être considérée comme complètement fermée tant que l'utilisateur n'a pas validé le comportement en jeu.

## Tâches

### ✅ Tâche 1 — Analyse et plan

Objectif : valider les décisions utilisateur et créer le plan agent strict.

Actions :

- Mettre à jour `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` avec les décisions V1.
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

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Cutscenes` : vert, 15 tests.
- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` : vert ; warnings existants hors scope dans le repo.

### ✅ Tâche 10 — Hygiène runtime V2.0 pour `MoveTo` direct

Objectif : traiter le driver `MoveTo` comme un helper runtime système et non comme un composant d'authoring.

Actions :

- masquer `CharacterControllerMoveToDriverComponent` côté authoring avec `[Browsable(false)]` ;
- exclure ce composant du menu `Add Component` de l'éditeur ;
- exclure ce composant de la sérialisation d'authoring ;
- faire suivre `CutsceneDirector.Stop()` vers les drivers `MoveTo` actifs, les annuler puis les retirer de leurs entités ;
- conserver `World.Clear()` comme point d'arrêt global qui passe par `CutsceneDirector.Stop()`.

Validation :

- tests cutscene ciblés verts avec couverture du `Stop()` sur `MoveTo` ;
- build solution éditeur vert ;
- aucune exposition authoring résiduelle du driver runtime.

### 🧪 Tâche 11 — CutsceneDemo minimal `MoveTo` direct

Objectif : ajouter une première démo visible permettant de valider tout le flux `asset -> world -> controller -> stop` avant d'ouvrir la tranche navigation.

Actions :

- ✅ créer un `CutsceneMoveToDemo` minimal centré sur une entité `CutsceneHero` dotée d'un `CharacterControllerComponent` ;
- ✅ brancher l'asset `Content/Cutscenes/move_to_direct.cutscene` avec une action `MoveTo` directe ;
- ✅ prévoir les commandes `Space` pour relancer, `S` pour stopper, `R` pour reset ;
- ✅ ajouter des marqueurs visuels depart/destination et une capture smoke automation ;
- ✅ documenter le protocole de validation visuelle attendu dans `CasaEngine.Demos/Demos/CutsceneMoveToDemo.md`.

Validation :

- ✅ `jq empty .\CasaEngine.Demos\Content\AssetInfos.json` et `jq empty .\CasaEngine.Demos\Content\Cutscenes\move_to_direct.cutscene` ;
- ✅ `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -c Debug --no-restore -v:minimal` ;
- ✅ smoke automatisé avec `CASAENGINE_START_DEMO="Cutscene MoveTo demo"` et capture `artifacts/cutscene-moveto-demo-smoke.png` ;
- 🧪 validation visuelle utilisateur obligatoire : lancement, déplacement visible, arrêt manuel, restauration du contrôle, relance sans état sale.

### ✅ Tâche 12 — Contrat d'action V2 navigation

Objectif : figer le contrat de la première action navigation avant tout branchement sur `NavigationAgentComponent`.

Actions :

- ✅ choisir `NavigateToCutsceneActionData` comme première action V2 navigation ;
- ✅ garder `MoveTo` direct inchangé pour préserver la compatibilité et la tranche de validation déjà acquise ;
- ✅ définir les champs minimums : acteur ciblé, destination, stopping distance, timeout, et raison d'échec observable ;
- ✅ documenter ce contrat dans `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` et dans ce plan avant l'implémentation runtime.

Validation :

- ✅ format de données typé documenté ;
- ✅ sérialisation/validation prévues sans ambiguïté ;
- ✅ aucun branchement runtime navigation dans cette tâche ;
- `FollowEntityCutsceneActionData` reste hors de la première tranche navigation parce qu'il implique cible mobile, repath et politique d'arrêt plus large.

Contrat retenu :

```text
NavigateTo
  entity: string obligatoire, résolu comme MoveTo par Entity.Name exact
  destination: Vector3 obligatoire
  stopping_distance: float >= 0, défaut 0.1
  timeout_seconds: float >= 0, 0 = pas de timeout cutscene
```

Règles runtime prévues :

- l'entité ciblée doit posséder un `NavigationAgentComponent` ;
- le `NavigationAgentComponent.NavigationMap` doit être renseigné par la scène ou la démo ;
- `NavigateTo` appelle `NavigationAgentComponent.MoveTo(destination)` puis attend `ReachedDestination` ;
- `NavigateTo` échoue explicitement si aucun chemin n'est trouvé ou si le timeout expire ;
- `CutsceneDirector.Stop()` devra appeler `NavigationAgentComponent.Cancel()` pour les agents activés par la cutscene.

### ✅ Tâche 13 — Bridge runtime cutscene -> navigation

Objectif : exécuter la première action navigation cutscene via les composants runtime déjà présents, sans élargir prématurément le scope.

Actions :

- ✅ étendre le modèle d'actions, la validation et la sérialisation JSON ;
- ✅ résoudre l'entité cible puis son `NavigationAgentComponent` ;
- ✅ déléguer le déplacement calculé à l'agent/navigation driver existant ;
- ✅ échouer explicitement si l'agent, la carte ou la cible requise sont absents ;
- ✅ ne pas ajouter de repath dynamique, de navmesh 3D ou d'obstacles avancés dans cette tranche.

Validation :

- ✅ tests unitaires ajoutés sur l'action navigation réussie et les erreurs attendues dans `CasaEngine.Tests/Cutscenes` ;
- ✅ `dotnet build .\CasaEngine\CasaEngine.csproj -c Debug --no-restore -v:minimal` vert ;
- ⚠️ `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter "Cutscene|Navigation"` bloqué par des erreurs existantes hors cutscene/navigation V2 : `Pool<>`, `DualQuaternion`, `LightComponent.Coordinates`, `PreviewEnvironmentFactory` ;
- ✅ aucune erreur nouvelle détectée dans les fichiers cutscene/editor services touchés.

### ✅ Tâche 14 — Propagation `Stop`/`Cancel` vers les drivers navigation actifs

Objectif : appliquer à la navigation la même discipline que pour `MoveTo` direct.

Actions :

- ✅ suivre les agents navigation activés par la cutscene ;
- ✅ propager `CutsceneDirector.Stop()` et `World.Clear()` vers ces agents ;
- ✅ annuler proprement la navigation active et restaurer le mode de contrôle via le driver runtime existant ;
- ✅ couvrir aussi le cas d'annulation en cours de déplacement calculé.

Validation :

- ✅ tests ciblés `Stop`, `Cancel` et `World.Clear()` sur navigation ajoutés ;
- ✅ build runtime vert ;
- ⚠️ exécution des tests bloquée par les erreurs existantes du projet de tests listées en tâche 13 ;
- ✅ compatibilité conservée avec la tranche `MoveTo` directe.

### ✅ Tâche 15 — Debug runtime et éditeur lecture seule V2

Objectif : rendre observable l'état de la navigation cutscene sans éditeur complet.

Actions :

- ✅ étendre `CutsceneDebugSnapshot` avec l'action active, la destination, l'état navigation et la raison d'arrêt ;
- ✅ exposer ces informations dans le document lecture seule et le panneau d'inspection ;
- ✅ garder l'UI en lecture seule ;
- ✅ ne pas ajouter de timeline ni d'édition interactive dans cette tranche.

Validation :

- ✅ tests du builder lecture seule et du snapshot runtime ajoutés ;
- ✅ `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore -v:minimal` vert ;
- ⚠️ exécution des tests bloquée par les erreurs existantes du projet de tests listées en tâche 13.

### 🧪 Tâche 16 — CutsceneDemo navigation minimale

Objectif : valider visuellement la première intégration navigation avant toute extension plus ambitieuse.

Actions :

- ✅ dupliquer la démo minimale avec une carte `NavigationGrid2D` réelle ;
- ✅ ajouter `Content/Cutscenes/navigate_to_grid.cutscene` qui utilise `NavigateTo` ;
- ✅ prévoir un cas simple de destination atteignable et un arrêt manuel via `S` ;
- ✅ documenter la procédure de validation visuelle dans `CasaEngine.Demos/Demos/CutsceneNavigateToDemo.md`.

Validation :

- ✅ `jq empty .\CasaEngine.Demos\Content\Cutscenes\navigate_to_grid.cutscene` et `jq empty .\CasaEngine.Demos\Content\AssetInfos.json` ;
- ✅ `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -c Debug --no-restore -v:minimal` vert ;
- ✅ smoke automatisé depuis `CasaEngine.Demos` avec `CASAENGINE_START_DEMO="Cutscene NavigateTo demo"` et capture `artifacts/validation/cutscene-navigate-to-demo.png` ;
- ⚠️ tests runtime navigation non exécutables tant que le projet `CasaEngine.Tests` ne compile pas ;
- 🧪 validation visuelle utilisateur obligatoire : navigation visible, arrêt correct, retour de contrôle, relecture de la démo sans état résiduel.

### ✅ Tâche 17 — Validation finale V2 incrémentale

Objectif : terminer la tranche V2 minimale sans perdre la discipline de validation par petites étapes.

Actions :

- ✅ relancer les tests `Cutscene`, `CharacterController` et `Navigation` ciblés quand possible ;
- ✅ relancer au moins un build de solution ;
- ✅ mettre à jour ce plan avec les statuts finaux et les résultats constatés ;
- ✅ consigner explicitement les validations visuelles encore en attente côté utilisateur.

Validation :

- ⚠️ `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter "Cutscene|Navigation"` bloque avant exécution par compilation globale du projet de tests : `Pool<>`, `DualQuaternion`, `LightComponent.Coordinates`, `PreviewEnvironmentFactory` ;
- ✅ `dotnet build .\CasaEngine\CasaEngine.csproj -c Debug --no-restore -v:minimal` vert ;
- ✅ `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore -v:minimal` vert ;
- ✅ `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -c Debug --no-restore -v:minimal` vert ;
- ✅ `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore -v:minimal` vert, avec warnings existants dans `CasaEngine.DotNetCompiler` ;
- 🧪 les tâches de démo restent `Needs testing` tant que l'utilisateur n'a pas donné son feu vert visuel.

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