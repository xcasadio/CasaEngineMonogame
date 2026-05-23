# Système de séquences scriptées avec commandes séquentielles async/coroutine

## Objectif

Ce document décrit une architecture simple et robuste pour gérer des séquences scriptées dans un moteur de jeu comme CasaEngine.

Le but n’est pas de créer tout de suite un éditeur complet de type Unity Timeline ou Unreal Sequencer.  
La première version doit permettre d’écrire des cinématiques et événements de gameplay sous forme de scripts séquentiels non bloquants.

Exemples de séquences ciblées :

- déplacer un personnage vers un PNJ ;
- jouer une animation ;
- ouvrir un dialogue ;
- bloquer les inputs joueur ;
- déplacer la caméra ;
- jouer un son ;
- attendre quelques secondes ;
- déclencher un flag de quête ;
- restaurer l’état du gameplay à la fin.

L’approche recommandée pour une V1 est :

```text
CutsceneAsset CasaEngine
    ↓
CutsceneDirector dans World
    ↓
World.CoroutineManager
    ↓
Coroutine CasaEngine frame par frame
    ↓
Fin, Stop ou debug
```

---

## Décisions CasaEngine V1 validées

Ces décisions priment sur les exemples conceptuels plus bas dans le document.

Pour CasaEngine V1 :

- utiliser `World.CoroutineManager` ;
- ne pas créer de nouveau `CutsceneRunner` ;
- ajouter `CutsceneDirector` directement dans `World` ;
- garder `CutsceneDirector` comme façade sans `Update` séparé ;
- limiter la V1 à `Wait`, `Sequence`, `Parallel`, `Stop`, debug, validation, sérialisation asset CasaEngine et affichage éditeur lecture seule ;
- exclure les commandes gameplay de la V1 ;
- ne pas inventer `InputManager`, `DialogueSystem`, `CameraManager`, `QuestSystem`, `AudioSystem`, `CharacterController` ou `Animator` ;
- charger `CutsceneAsset` via le système d’assets CasaEngine ;
- utiliser des actions typées au lieu de `Dictionary<string, string>` ;
- ne pas implémenter `CompleteImmediately` en V1.

Les exemples de déplacement, animation, dialogue, caméra, input, quêtes et audio sont donc des pistes futures. Ils ne doivent pas être transformés en tâches V1 tant que les systèmes CasaEngine correspondants n’ont pas été identifiés.

---

## État implémenté au 2026-05-23

La frontière V1/V2 a déjà bougé dans le code : CasaEngine ne se limite plus à `Wait`, `Sequence` et `Parallel`. Un premier mouvement gameplay `MoveTo` est maintenant implémenté au-dessus du `CharacterController`.

Runtime :

- `CasaEngine.Framework.Cutscenes.CutsceneAsset` hérite de `ObjectBase` et expose `Version` et `RootAction` ;
- les actions typées actuellement supportées sont `WaitCutsceneActionData`, `MoveToCutsceneActionData`, `SequenceCutsceneActionData` et `ParallelCutsceneActionData` ;
- `MoveToCutsceneActionData` porte `entity`, `destination`, `stopping_distance` et `timeout_seconds` ;
- `UnknownCutsceneActionData` conserve les types non reconnus afin que la validation puisse les signaler proprement ;
- `CutsceneValidator` valide `RootAction`, `Wait.seconds >= 0`, `MoveTo.entity`, `MoveTo.stopping_distance`, `MoveTo.timeout_seconds`, les actions inconnues, et signale les `Sequence`/`Parallel` vides en warning ;
- `CutsceneAssetJsonSerializer` charge/sauve l’arbre d’actions typées avec discriminateur `type`, y compris les paramètres de `MoveTo` ;
- `Constants.FileNameExtensions.Cutscene` ajoute l’extension `.cutscene` ;
- `CutsceneAssetLoader` charge les assets `.cutscene` via `AssetContentManager` ;
- `AssetLoaderRegistry` enregistre le loader pour `CutsceneAsset` ;
- `World` possède `CutsceneDirector` et l’arrête dans `World.Clear()` ;
- `CutsceneDirector.Play(CutsceneAsset)` démarre une coroutine via `World.CoroutineManager` ;
- `CutsceneActionCoroutineFactory` exécute `MoveTo` en résolvant l’entité cible par nom, en ajoutant au besoin un `CharacterControllerMoveToDriverComponent`, puis en attendant la fin du déplacement ou le timeout ;
- `CharacterControllerMoveToDriverComponent` prend `CharacterControlMode.Cutscene`, convertit une destination monde XZ en `SetMoveIntent(Vector2)`, et restaure le mode précédent à l’arrivée, à l’annulation ou au timeout ;
- en parallèle du runtime cutscene, le repo contient déjà une base navigation réutilisable pour la V2 : `NavigationGrid2D`, `GridPathfinder2D`, `NavigationPath`, `NavigationQuery`, `NavigationAgentComponent`, `CharacterControllerNavigationDriverComponent` et `CharacterControllerSteeringBridgeComponent` ;
- `CutsceneDirector.Stop()` arrête les coroutines possédées par le director, annule les `CharacterControllerMoveToDriverComponent` encore actifs et retire ces composants runtime de leurs entités ;
- `CutsceneDebugSnapshot` expose l’état runtime, l’asset courant, le handle actif, les messages de validation et les coroutines actives liées à la cutscene.

Éditeur :

- `CutsceneReadOnlyDocumentBuilder` construit un modèle lecture seule à partir d’un `CutsceneAsset` et d’un snapshot runtime optionnel ;
- `CutsceneAssetInspectorPanel` affiche l’asset `.cutscene`, son arbre d’actions, ses paramètres `Wait` et `MoveTo`, ses erreurs/warnings de validation et l’état runtime disponible ;
- `GameEditor` ouvre les fichiers `.cutscene` comme documents d’éditeur lecture seule ;
- aucune édition, sauvegarde, timeline, preview avancée ou commande gameplay n’a été ajoutée.

Validation exécutée :

```text
dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Cutscenes
Résultat : 15 tests verts

dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
Résultat : build OK ; warnings existants hors scope sur le repo
```

Conséquence importante : une cutscene peut déjà être chargée, validée, jouée, arrêtée et inspectée, et elle peut maintenant déplacer visiblement un acteur si l’entité ciblée possède un `CharacterControllerComponent`. En revanche, le runtime cutscene n’utilise pas encore la navigation calculée (`NavigationAgentComponent` / `GridPathfinder2D`) et ne couvre ni caméra, ni dialogue, ni input, ni flags gameplay.

---

## Bindings d’entités actuels et limites V2

L’état actuel n’est plus "aucun binding". `MoveTo` résout déjà une entité cible par son `Entity.Name` exact dans `World.Entities`.

Ce qui existe déjà :

```text
binding par nom exact pour MoveTo
erreur runtime si l’entité est introuvable
erreur runtime si l’entité n’a pas de CharacterControllerComponent
validation syntaxique des champs de MoveTo dans l’asset
```

Ce qui reste insuffisant pour une V2 robuste :

```text
pas d’identifiant stable d’entité ou de binding d’asset
pas de gestion des doublons de noms
recherche linéaire simple dans World.Entities
pas de binding vers camera/dialogue/input/navigation map
pas de validation éditeur liée à une scène ou à un World réel
```

Pour une version future plus robuste, il faudra éviter de sérialiser des références directes vers des instances runtime. Les données devront utiliser des identifiants logiques, par exemple :

```text
Player
VillageChief
MainCamera
Door_A
```

À l’exécution, un resolver futur pourra traduire ces identifiants vers les objets réels du monde chargé. Cette partie reste hors scope V1 tant qu’aucun système cible CasaEngine n’est choisi pour le déplacement, l’animation, le dialogue, la caméra ou l’input.

---

## Éditeur lecture seule implémenté

L’éditeur affiche maintenant les cutscenes en lecture seule.

Ce qui est affiché :

```text
nom de l’asset
chemin source
état éditable = False
état runtime du CutsceneDirector
nombre de coroutines actives
arbre d’actions
paramètres runtime actuellement supportés
warnings/erreurs de validation
coroutines actives liées à la cutscene si un World est disponible
```

Exemple d’arbre affichable avec `MoveTo` :

```text
Cutscene: HeroMove

- MoveTo  root_action
    entity: Hero
    destination: 1, 0, 0
    stopping_distance: 0.05
    timeout_seconds: 3
```

Validation V1 affichée :

```text
RootAction obligatoire
Wait.seconds >= 0
MoveTo.entity requis
MoveTo.stopping_distance >= 0
MoveTo.timeout_seconds >= 0
Sequence vide = warning
Parallel vide = warning
action inconnue = erreur
```

Ce qui reste explicitement absent :

```text
ajout d’action
suppression d’action
réordonnancement
édition des paramètres
drag and drop
timeline
preview avancée
sauvegarde depuis l’UI
validation de bindings entity/dialogue/animation/camera/quest
```

---

## Analyse des prérequis V2 avec CharacterController et Navigation

La V2 n’est plus bloquée sur l’existence d’un premier déplacement gameplay. Le dépôt possède déjà un socle fonctionnel, mais la cutscene n’en consomme encore qu’une partie.

### Déjà implémenté et réutilisable

Validation ciblée de cette tranche :

```text
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter 'Cutscene|CharacterController|Navigation'
Résultat constaté : 79 tests verts, 0 échec
```

```text
action cutscene MoveTo sérialisée, validée et affichée en lecture seule
driver CharacterControllerMoveToDriverComponent dédié à l’autorité Cutscene
restauration du mode de contrôle précédent sur arrivée, timeout ou annulation du driver
destruction du CharacterControllerMoveToDriverComponent quand la cutscene est stoppée ou annulée
driver navigation CharacterControllerNavigationDriverComponent pour MoveTo, SetPath et FollowTarget
NavigationGrid2D, GridPathfinder2D, NavigationPath et NavigationQuery
NavigationAgentComponent pour calculer un chemin et l’envoyer au driver navigation
CharacterControllerSteeringBridgeComponent pour brancher SteeringAgentComponent sur SetMoveIntent(Vector2)
tests dédiés pour MoveTo cutscene, driver de contrôle, grille de navigation et bridge navigation/controller
```

### Ce que la cutscene V2 n’utilise pas encore

```text
le runtime cutscene ne parle pas encore a NavigationAgentComponent
MoveTo est un déplacement direct vers une destination monde, pas un déplacement calculé avec pathfinding
aucun FollowTarget cutscene n’existe encore
aucune politique de repath n’existe pour obstacle dynamique ou cible qui bouge
le snapshot debug cutscene ne décrit pas l’état du driver gameplay actif
```

### Pré-requis minimaux avant une V2 robuste

1. définir le contrat de déplacement V2 : garder `MoveTo` pour le direct et ajouter `NavigateTo`/`FollowEntity`, ou étendre `MoveTo` avec un mode explicite ;
2. appliquer la même politique de `Stop`/annulation aux futurs drivers gameplay V2 ; `CharacterControllerMoveToDriverComponent` est maintenant annulé puis retiré quand la cutscene est stoppée ;
3. définir comment une cutscene récupère la bonne carte de navigation ; actuellement `NavigationAgentComponent.NavigationMap` est injecté manuellement ;
4. stabiliser les bindings d’entités avec une stratégie plus robuste que `Entity.Name` exact ;
5. étendre la validation V2 : entité absente, controller absent, carte de navigation absente, destination inaccessible, timeout et raison d’échec clairement surfacés ;
6. décider la politique de blocage et de repath pendant une cutscene ;
7. étendre `CutsceneDebugSnapshot` et l’éditeur lecture seule pour exposer destination, waypoint courant, état du driver et raison d’arrêt ;
8. ajouter un sample de validation V2 basé soit sur `MoveTo` direct, soit sur une vraie navigation avec `NavigationGrid2D` branchée au monde.

### Ce qui peut rester hors de la première V2

```text
obstacles dynamiques complexes
path smoothing avance
off-mesh links
navmesh 3D
camera, dialogue, audio et quetes
```

---

## Sample de cutscene : faisabilité

Il est possible de créer un sample visible immédiatement, mais il faut être clair sur ce qu’il démontre.

Avec l’état actuel, un sample peut démontrer :

```text
chargement d’un asset .cutscene via AssetContentManager
validation de l’asset
Play via World.CutsceneDirector
attente non bloquante via World.CoroutineManager
MoveTo via CharacterControllerMoveToDriverComponent
prise et restitution de CharacterControlMode.Cutscene
Sequence et Parallel
Stop
GetDebugSnapshot
affichage de l’état runtime et des coroutines actives
```

Ce sample peut déjà démontrer un déplacement visible simple si une entité de la scène possède un `CharacterControllerComponent`. En revanche, il ne démontre pas encore une navigation calculée complète, ni une caméra scriptée, ni un dialogue, ni un verrouillage d’input.

### Sample minimal possible sans nouvelle feature runtime

Un exemple simpliste peut être ajouté dans `CasaEngine.Demos` sans changer le runtime cutscene :

1. ajouter un fichier `Content/Cutscenes/HeroMove.cutscene` ;
2. ajouter l’entrée correspondante dans `Content/AssetInfos.json`, avec `asset_type = "cutscene"` ou une extension `.cutscene` suffisante pour l’inférence ;
3. ajouter une classe `CutsceneDemo : Demo` ;
4. dans `Initialize`, charger l’asset avec `game.AssetContentManager.Load<CutsceneAsset>(assetInfo.Id)` ;
5. créer ou charger une entité `Hero` avec `CharacterControllerComponent` ;
6. appeler `game.GameManager.CurrentWorld.CutsceneDirector.Play(asset)` ;
7. afficher dans une petite UI ou un overlay l’état de `GetDebugSnapshot()` : `Playing`, `Completed`, `Stopped`, messages de validation, coroutines actives ;
8. afficher la position du héros ou son mode de contrôle pour vérifier la prise/rendu d’autorité ;
9. ajouter au besoin une touche de restart et une touche de `Stop()` ;
10. enregistrer la démo dans `DemosGame`.

Exemple d’asset pour ce sample :

```json
{
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "HeroMove",
    "type": "CutsceneAsset",
    "version": 1,
    "schema_version": 1,
    "root_action": {
        "type": "MoveTo",
        "entity": "Hero",
        "destination": {
            "x": 2.0,
            "y": 0.0,
            "z": 0.0
        },
        "stopping_distance": 0.05,
        "timeout_seconds": 3.0
    }
}
```

### Ce qu’il faut pour un sample cinématique simpliste

Si le sample attendu doit montrer une vraie navigation calculée ou une séquence cinématique plus riche, l’état actuel ne suffit pas encore. Il faudra d’abord choisir et implémenter au moins les briques V2 suivantes :

```text
action cutscene navigation explicite ou extension de MoveTo
binding logique stable vers l’entité cible
accès à NavigationGrid2D/NavigationAgentComponent depuis le World ou l’entité
politique Stop/Cancel/Repath pour les drivers de navigation
validation d’accessibilité ou d’échec de chemin
snapshot debug et lecture seule éditeur pour l’état navigation
tests runtime du chemin calculé et des cas bloqués
```

La voie la plus petite pour une V2 navigation exploitable n’est donc plus d’ajouter un faux déplacement direct, mais de brancher proprement la cutscene sur la navigation déjà existante sans casser l’autorité du `CharacterController`.

---

## CutsceneDirector

Dans CasaEngine V1, il doit être une façade attachée au `World`.

API V1 attendue :

```csharp
public sealed class CutsceneDirector
{
    public bool IsPlaying { get; }

    public void Play(CutsceneAsset asset);
    public void Stop();
    public CutsceneDebugSnapshot GetDebugSnapshot();
}
```

Responsabilités V1 :

- démarrer une coroutine du `World.CoroutineManager` à partir du `CutsceneAsset` ;
- conserver le `CoroutineHandle` actif ;
- arrêter la cutscene courante avec `Stop` ;
- exposer un snapshot de debug ;
- déléguer l’avancement à `World.CoroutineManager`.

Responsabilités exclues de la V1 :

- déplacer des entités ;
- forcer des animations ;
- ouvrir des dialogues ;
- gérer les inputs joueur ;
- piloter une caméra ;
- appliquer des flags de quête.

---

## CutsceneContext

Cette section est une piste pour les versions futures.

En V1, il ne faut pas créer de `CutsceneContext` contenant des services fictifs. La V1 ne contient que `Wait`, `Sequence` et `Parallel`, donc elle n’a pas besoin d’accès à l’input, au dialogue, à la caméra, à l’audio, aux quêtes, au déplacement ou à l’animation.

Toute dépendance future doit être classée avant implémentation :

```text
Existe déjà
À identifier dans le repo
À créer plus tard
Hors scope V1
```

Les noms suivants ne doivent pas être introduits par le plan V1 s’ils ne sont pas d’abord reliés à des types CasaEngine réels :

```text
InputManager
DialogueSystem
CameraManager
QuestSystem
AudioSystem
CharacterController
Animator
```

---

## Autorité pendant une cutscene

Cette section concerne une version future avec commandes gameplay. Elle est hors scope V1.

Pendant le gameplay normal :

```text
PlayerController possède l’autorité sur le personnage.
```

Pendant une cutscene :

```text
CutsceneDirector possède temporairement l’autorité.
```

Mais il ne doit pas tout gérer directement.

Il devra utiliser les systèmes existants, une fois ceux-ci identifiés dans CasaEngine :

```text
Déplacement → CharacterController ou Navigation
Animation → Animator
Dialogue → DialogueSystem
Caméra → CameraManager
Input → InputManager
Audio → AudioSystem
Quêtes → QuestSystem
```

Le `CutsceneDirector` orchestre.  
Il ne remplace pas les systèmes de gameplay.

---

## Déplacement d’un personnage

Cette section est hors scope V1. Elle ne doit pas devenir une tâche tant que le système cible de déplacement n’est pas décidé.

Questions à trancher avant toute implémentation future :

```text
déplacement direct du Transform ?
via Pawn ?
via Controller ?
via Navigation ?
avec collision ?
sans collision ?
```

---

### Approche 1 : déplacement direct par la commande

La commande modifie directement la position de l’acteur.

```csharp
public sealed class MoveActorToCommand : ICutsceneCommand
{
    private readonly Entity _actor;
    private readonly Vector2 _target;
    private readonly float _speed;

    public bool IsFinished { get; private set; }

    public MoveActorToCommand(Entity actor, Vector2 target, float speed = 80f)
    {
        _actor = actor;
        _target = target;
        _speed = speed;
    }

    public void Start(CutsceneContext context)
    {
        IsFinished = false;
        _actor.Get<Animator>().Play("Walk");
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        var transform = _actor.Get<TransformComponent>();

        Vector2 direction = _target - transform.Position;

        if (direction.LengthSquared() < 1f)
        {
            transform.Position = _target;
            _actor.Get<Animator>().Play("Idle");
            IsFinished = true;
            return;
        }

        direction.Normalize();
        transform.Position += direction * _speed * deltaTime;
    }

    public void Cancel(CutsceneContext context)
    {
        _actor.Get<Animator>().Play("Idle");
        IsFinished = true;
    }
}
```

Avantages :

- très simple ;
- suffisant pour une version gameplay future simple ;
- facile à déboguer ;
- comportement déterministe.

Inconvénients :

- ne respecte pas forcément la navigation ;
- peut contourner la physique ;
- peut traverser des obstacles si aucune collision n’est vérifiée.

---

### Approche 2 : commande gameplay `MoveTo`

La commande demande au `CharacterController` ou au système de navigation de déplacer l’acteur.

```csharp
public sealed class MoveActorToCommand : ICutsceneCommand
{
    private readonly Entity _actor;
    private readonly Vector2 _target;
    private readonly float _speed;

    public bool IsFinished { get; private set; }

    public void Start(CutsceneContext context)
    {
        IsFinished = false;

        var controller = _actor.Get<CharacterController>();
        controller.MoveTo(_target, _speed);
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        var controller = _actor.Get<CharacterController>();

        if (controller.HasReachedDestination)
        {
            IsFinished = true;
        }
    }

    public void Cancel(CutsceneContext context)
    {
        var controller = _actor.Get<CharacterController>();
        controller.Stop();

        IsFinished = true;
    }
}
```

Avantages :

- respecte mieux la logique gameplay ;
- peut utiliser pathfinding, collisions, tilemap, sol ;
- plus cohérent avec le reste du moteur.

Inconvénients :

- nécessite un `CharacterController` suffisamment robuste ;
- le résultat peut dépendre du monde ;
- un obstacle dynamique peut bloquer la cutscene.

Pour une version future, la décision devra être reprise après inspection des systèmes CasaEngine réels :

```text
option simple : déplacement direct possible
option intégrée : passer par le système gameplay réel quand il est identifié
```

---

## Animation pendant une cutscene

Cette section est hors scope V1. Elle ne doit pas être implémentée tant que le système d’animation CasaEngine cible n’est pas identifié.

Pendant le gameplay normal :

```text
Input → CharacterController → AnimationStateMachine
```

Pendant une cutscene :

```text
CutsceneCommand → Animator
```

Une commande d’animation peut être très simple :

```csharp
public sealed class PlayAnimationCommand : ICutsceneCommand
{
    private readonly Entity _actor;
    private readonly string _animationName;
    private readonly bool _waitUntilFinished;

    public bool IsFinished { get; private set; }

    public PlayAnimationCommand(Entity actor, string animationName, bool waitUntilFinished = false)
    {
        _actor = actor;
        _animationName = animationName;
        _waitUntilFinished = waitUntilFinished;
    }

    public void Start(CutsceneContext context)
    {
        IsFinished = false;

        var animator = _actor.Get<Animator>();
        animator.Play(_animationName);

        if (!_waitUntilFinished)
        {
            IsFinished = true;
        }
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        if (!_waitUntilFinished)
            return;

        var animator = _actor.Get<Animator>();

        if (animator.IsCurrentAnimationFinished)
        {
            IsFinished = true;
        }
    }

    public void Cancel(CutsceneContext context)
    {
        IsFinished = true;
    }
}
```

Cas d’usage :

```csharp
sequence.Add(new PlayAnimationCommand(player, "IdleRight"));
sequence.Add(new PlayAnimationCommand(npc, "Talk", waitUntilFinished: false));
sequence.Add(new PlayAnimationCommand(door, "Open", waitUntilFinished: true));
```

Il faut prévoir :

- animation immédiate ;
- animation bloquante ;
- animation non bloquante ;
- boucle ou non ;
- restauration éventuelle de l’animation précédente ;
- blocage de l’animation gameplay pendant la cutscene.

---

## Dialogue

Cette section est hors scope V1. Elle ne doit pas être implémentée tant que le système de dialogue CasaEngine cible n’est pas identifié.

Un dialogue est une commande qui ouvre le système de dialogue et attend sa fermeture.

```csharp
public sealed class ShowDialogueCommand : ICutsceneCommand
{
    private readonly string _dialogueId;

    public bool IsFinished { get; private set; }

    public ShowDialogueCommand(string dialogueId)
    {
        _dialogueId = dialogueId;
    }

    public void Start(CutsceneContext context)
    {
        IsFinished = false;

        context.DialogueSystem.Open(_dialogueId, onClosed: () =>
        {
            IsFinished = true;
        });
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        // Le DialogueSystem gère ses propres inputs.
    }

    public void Cancel(CutsceneContext context)
    {
        context.DialogueSystem.Close();
        IsFinished = true;
    }
}
```

Deux comportements peuvent exister.

---

### Dialogue bloquant

La cutscene attend que le dialogue soit fermé.

```text
MoveTo PNJ
    ↓
OpenDialogue
    ↓
Pause logique de la séquence
    ↓
Joueur valide le texte
    ↓
Dialogue fermé
    ↓
Suite de la cutscene
```

C’est le comportement recommandé pour :

- RPG ;
- Zelda-like ;
- dialogues de PNJ ;
- événements de map ;
- scènes sans voix audio.

---

### Dialogue non bloquant

La cutscene continue pendant que le texte est affiché.

```text
Afficher sous-titre
Déplacer caméra
Jouer animation
Continuer la scène
```

Ce comportement est utile pour :

- sous-titres ;
- voix off ;
- cinématique non interactive.

Pour une version future minimale, il serait acceptable de ne gérer que le dialogue bloquant.

---

## Inputs joueur

Cette section est hors scope V1. Elle ne doit pas être implémentée tant que le système d’input CasaEngine cible n’est pas identifié.

Pendant une cutscene, il ne faut généralement pas désactiver tous les inputs.

Il faut plutôt changer de contexte d’input.

```text
GameplayInputContext
    Move
    Jump
    Attack
    Interact

CutsceneInputContext
    ConfirmDialogue
    SkipCutscene
    Pause
```

Au début de la cutscene :

```csharp
public sealed class DisablePlayerControlCommand : ICutsceneCommand
{
    public bool IsFinished { get; private set; }

    public void Start(CutsceneContext context)
    {
        context.InputManager.PushContext("Cutscene");
        context.World.PlayerController.Enabled = false;

        IsFinished = true;
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
    }

    public void Cancel(CutsceneContext context)
    {
    }
}
```

À la fin :

```csharp
public sealed class EnablePlayerControlCommand : ICutsceneCommand
{
    public bool IsFinished { get; private set; }

    public void Start(CutsceneContext context)
    {
        context.World.PlayerController.Enabled = true;
        context.InputManager.PopContext("Cutscene");

        IsFinished = true;
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
    }

    public void Cancel(CutsceneContext context)
    {
    }
}
```

Pendant la cutscene, le joueur peut encore :

```text
valider le dialogue
passer une ligne de texte
skip la cutscene
ouvrir pause éventuellement
```

Mais il ne peut plus :

```text
se déplacer
attaquer
sauter
interagir librement
changer l’état gameplay du personnage
```

---

## Caméra

Cette section est hors scope V1. Elle ne doit pas être implémentée tant que le système de caméra CasaEngine cible n’est pas identifié.

Une cutscene ne devrait pas modifier directement la caméra gameplay de manière définitive.

Dans un système générique, il serait préférable d’utiliser un gestionnaire de caméra. Le nom `CameraManager` ci-dessous est un exemple conceptuel, pas une décision CasaEngine V1.

```text
GameplayCamera
    ↓ blend
CinematicCamera
    ↓ cutscene
GameplayCamera
```

Exemple d’API :

```csharp
CameraManager.PushCamera(cinematicCamera, blendTime: 0.5f);
CameraManager.PopCamera(blendTime: 0.5f);
```

Commandes possibles :

```text
CameraFocusCommand
CameraMoveToCommand
CameraShakeCommand
CameraZoomCommand
RestoreCameraCommand
```

Exemple simple :

```csharp
public sealed class CameraFocusCommand : ICutsceneCommand
{
    private readonly Entity _target;
    private readonly float _duration;
    private float _elapsed;

    public bool IsFinished { get; private set; }

    public void Start(CutsceneContext context)
    {
        _elapsed = 0f;
        IsFinished = false;

        context.CameraManager.FocusOn(_target, _duration);
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        _elapsed += deltaTime;

        if (_elapsed >= _duration)
        {
            IsFinished = true;
        }
    }

    public void Cancel(CutsceneContext context)
    {
        IsFinished = true;
    }
}
```

---

## Wait

Une commande `WaitSecondsCommand` permet d’attendre sans bloquer le moteur.

```csharp
public sealed class WaitSecondsCommand : ICutsceneCommand
{
    private readonly float _duration;
    private float _elapsed;

    public bool IsFinished { get; private set; }

    public WaitSecondsCommand(float duration)
    {
        _duration = duration;
    }

    public void Start(CutsceneContext context)
    {
        _elapsed = 0f;
        IsFinished = false;
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        _elapsed += deltaTime;

        if (_elapsed >= _duration)
        {
            IsFinished = true;
        }
    }

    public void Cancel(CutsceneContext context)
    {
        IsFinished = true;
    }
}
```

Il faut éviter :

```csharp
await Task.Delay(1000);
```

pour les séquences de gameplay, car `Task.Delay` n’est pas contrôlé par le temps du moteur.

Il vaut mieux utiliser :

```csharp
await cutscene.WaitSeconds(1.0f);
```

ou :

```csharp
sequence.Add(new WaitSecondsCommand(1.0f));
```

afin que l’attente respecte :

- pause ;
- time scale ;
- debug frame step ;
- accélération ou ralentissement du jeu ;
- interruption de cutscene.

---

## Async / await

Cette approche n’est pas retenue pour CasaEngine V1.

Elle reste documentée comme piste future, mais le plan V1 doit utiliser les coroutines existantes du `World.CoroutineManager`.

La version à base de commandes est simple, mais elle devient vite verbeuse.

On peut ajouter une API `async/await` par-dessus.

Objectif :

```csharp
public async Task VillageChiefIntro(CutsceneApi cutscene)
{
    var player = cutscene.FindEntity("Player");
    var chief = cutscene.FindEntity("VillageChief");

    await cutscene.Begin();

    await cutscene.DisablePlayerControl();

    await cutscene.MoveTo(player, chief.Position + new Vector2(-32, 0));
    await cutscene.PlayAnimation(player, "IdleRight");

    await cutscene.PlayAnimation(chief, "Talk");
    await cutscene.ShowDialogue("village_chief_intro");

    await cutscene.SetQuestFlag("TalkedToVillageChief", true);

    await cutscene.RestoreCamera();
    await cutscene.EnablePlayerControl();

    await cutscene.End();
}
```

Le code semble linéaire, mais chaque `await` attend une commande qui est mise à jour frame par frame.

---

## CutsceneApi

La `CutsceneApi` expose des méthodes simples.

```csharp
public sealed class CutsceneApi
{
    private readonly CutsceneRunner _runner;

    public Task DisablePlayerControl()
    {
        return _runner.RunCommand(new DisablePlayerControlCommand());
    }

    public Task EnablePlayerControl()
    {
        return _runner.RunCommand(new EnablePlayerControlCommand());
    }

    public Task MoveTo(Entity actor, Vector2 target, float speed = 80f)
    {
        return _runner.RunCommand(new MoveActorToCommand(actor, target, speed));
    }

    public Task PlayAnimation(Entity actor, string animationName, bool waitUntilFinished = false)
    {
        return _runner.RunCommand(new PlayAnimationCommand(actor, animationName, waitUntilFinished));
    }

    public Task ShowDialogue(string dialogueId)
    {
        return _runner.RunCommand(new ShowDialogueCommand(dialogueId));
    }

    public Task WaitSeconds(float seconds)
    {
        return _runner.RunCommand(new WaitSecondsCommand(seconds));
    }
}
```

---

## CutsceneRunner avec Task

Cette section décrit une alternative générique. Elle ne doit pas être implémentée en V1 CasaEngine.

CasaEngine possède déjà un `CoroutineManager` attaché au `World`. Créer un `CutsceneRunner` avec sa propre liste de commandes et sa propre logique d’update dupliquerait le scheduler existant.

Le `CutsceneRunner` transforme une commande en `Task`.

```csharp
public sealed class CutsceneRunner
{
    private readonly List<RunningCommand> _commands = new();
    private readonly CutsceneContext _context;

    public Task RunCommand(ICutsceneCommand command)
    {
        var completion = new TaskCompletionSource();

        _commands.Add(new RunningCommand(command, completion));

        command.Start(_context);

        return completion.Task;
    }

    public void Update(float deltaTime)
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            var running = _commands[i];

            running.Command.Update(_context, deltaTime);

            if (running.Command.IsFinished)
            {
                running.Completion.SetResult();
                _commands.RemoveAt(i);
            }
        }
    }

    public void CancelAll()
    {
        foreach (var running in _commands)
        {
            running.Command.Cancel(_context);
            running.Completion.TrySetCanceled();
        }

        _commands.Clear();
    }

    private sealed class RunningCommand
    {
        public ICutsceneCommand Command { get; }
        public TaskCompletionSource Completion { get; }

        public RunningCommand(ICutsceneCommand command, TaskCompletionSource completion)
        {
            Command = command;
            Completion = completion;
        }
    }
}
```

Donc :

```csharp
await cutscene.MoveTo(player, target);
```

signifie :

```text
crée une commande MoveTo
la met à jour chaque frame
quand elle est terminée, continue le script
```

---

## Exécution parallèle

Certaines actions doivent se produire en même temps.

Exemple :

```text
le joueur marche
la caméra se déplace
la musique commence
```

Avec `async/await` :

```csharp
await Task.WhenAll(
    cutscene.MoveTo(player, target),
    cutscene.CameraFocus(npc, duration: 1.0f),
    cutscene.FadeMusic("village_theme", duration: 2.0f)
);
```

Puis :

```csharp
await cutscene.ShowDialogue("intro");
```

Ce qui donne :

```text
MoveTo + CameraFocus + FadeMusic en parallèle
puis Dialogue
```

Il faut cependant faire attention à l’autorité.

Deux commandes ne doivent pas contrôler la même chose en même temps.

Exemples à éviter :

```text
MoveTo(player)
MoveTo(player) en parallèle

PlayAnimation(player, "Walk")
PlayAnimation(player, "Attack") en parallèle

CameraFocus(npc)
CameraMoveTo(position) en parallèle
```

Pour éviter les conflits, chaque commande peut déclarer les ressources qu’elle verrouille :

```text
Actor:Player.Transform
Actor:Player.Animation
Camera:Main
Input:Gameplay
Dialogue:System
```

Ce n’est pas obligatoire en V1, mais c’est utile à prévoir.

---

## Coroutine

Une coroutine est une autre manière d’écrire une séquence sans utiliser `async/await`.

Style Unity-like :

```csharp
IEnumerator IntroCutscene()
{
    yield return DisablePlayerControl();

    yield return MoveTo(player, npc.Position);
    yield return PlayAnimation(player, "IdleRight");
    yield return ShowDialogue("npc_intro_001");

    yield return EnablePlayerControl();
}
```

Le `yield return` signifie :

```text
lance cette action
arrête temporairement cette fonction
reprends ici quand l’action est terminée
```

Le moteur appelle la coroutine à chaque frame.

Avantages :

- très naturel dans un moteur de jeu ;
- facile à intégrer dans une boucle `Update`;
- très contrôlable par le moteur ;
- facile à mettre en pause ou à annuler.

Inconvénients :

- moins idiomatique C# moderne ;
- moins confortable pour les exceptions ;
- nécessite un runner de coroutine maison.

---

## Async/await vs coroutine

Les deux approches sont valides.

### Coroutine

```csharp
IEnumerator Cutscene()
{
    yield return MoveTo(player, npc.Position);
    yield return WaitSeconds(1.0f);
    yield return ShowDialogue("intro");
}
```

Avantages :

- proche du modèle Unity ;
- très adapté aux jeux ;
- facile à contrôler avec le temps moteur.

Inconvénients :

- moins typé ;
- plus manuel ;
- composition parallèle moins naturelle.

### Async/await

```csharp
async Task Cutscene()
{
    await cutscene.MoveTo(player, npc.Position);
    await cutscene.WaitSeconds(1.0f);
    await cutscene.ShowDialogue("intro");
}
```

Avantages :

- très lisible ;
- idiomatique C# ;
- bonne gestion des exceptions ;
- composition facile avec `Task.WhenAll`.

Inconvénients :

- attention à ne pas utiliser `Task.Delay` ;
- attention au thread principal ;
- il faut intégrer correctement les `Task` dans la boucle du moteur.

Pour CasaEngine, le choix recommandé est :

```text
Base V1 : World.CoroutineManager existant
API V1 : CutsceneDirector.Play(CutsceneAsset)
Actions V1 : Wait, Sequence, Parallel
Stop V1 : arrêt simple via CoroutineHandle
Option future : async/await seulement si un besoin réel apparaît
```

---

## Skip et annulation

Un vrai système de cutscene devra gérer le skip.

En V1, la décision est plus stricte : implémenter seulement `Stop` simple. `CompleteImmediately` est hors scope, car `Wait`, `Sequence` et `Parallel` n’ont pas d’état gameplay final à appliquer.

Quand le joueur appuie sur une touche de skip :

```text
annuler les commandes actives
appliquer ou non l’état final
fermer les dialogues
restaurer les inputs
restaurer la caméra
signaler la fin
```

Il faut distinguer deux comportements.

---

### Cancel

`Cancel` arrête la commande sans forcément appliquer l’état final.

Exemple :

```text
le personnage s’arrête là où il est
la caméra arrête son mouvement
le dialogue se ferme
```

---

### CompleteImmediately

Cette section est hors scope V1.

`CompleteImmediately` applique directement l’état final.

Exemple :

```text
le personnage est téléporté à la position cible
la porte est ouverte
le flag de quête est activé
la caméra est restaurée
```

Pour une version future avec commandes gameplay, on pourra enrichir l’interface :

```csharp
public interface ICutsceneCommand
{
    void Start(CutsceneContext context);
    void Update(CutsceneContext context, float deltaTime);
    bool IsFinished { get; }
    void Cancel(CutsceneContext context);
    void CompleteImmediately(CutsceneContext context);
}
```

Exemple pour `MoveActorToCommand` :

```csharp
public void CompleteImmediately(CutsceneContext context)
{
    var transform = _actor.Get<TransformComponent>();
    transform.Position = _target;

    _actor.Get<Animator>().Play("Idle");

    IsFinished = true;
}
```

Pour les cinématiques importantes, il vaut souvent mieux appliquer l’état final :

```text
quête mise à jour
personnages à leur bonne position
porte ouverte
caméra restaurée
inputs restaurés
dialogue fermé
```

---

## Restauration de l’état

Cette section est hors scope V1. Elle deviendra nécessaire avec les commandes gameplay qui modifient inputs, caméra, animations, dialogue, audio ou flags.

Le `CutsceneDirector` doit restaurer proprement le jeu à la fin.

États à restaurer :

```text
inputs
contrôle joueur
caméra active
animations forcées
dialogues ouverts
vitesse du jeu
UI bloquée
musique temporaire
état de pause
```

Une bonne approche est d’avoir un objet `CutsceneStateSnapshot`.

```csharp
public sealed class CutsceneStateSnapshot
{
    public bool WasPlayerControllerEnabled { get; set; }
    public string? PreviousInputContext { get; set; }
    public Camera? PreviousCamera { get; set; }
}
```

Au début :

```csharp
_snapshot = CaptureCurrentState();
```

À la fin :

```csharp
RestoreState(_snapshot);
```

Il faut appeler la restauration dans tous les cas :

```text
fin normale
skip
cancel
exception
changement de scène
```

---

## Sérialisation

Même si la V1 utilise des scripts C#, il est utile de prévoir une représentation sérialisable des actions.

Cela permet :

- d’afficher les actions dans l’éditeur ;
- de sauvegarder une séquence ;
- de rejouer une séquence depuis des données ;
- de faciliter le debug ;
- de préparer une future édition visuelle sans la développer maintenant.

---

## Modèle sérialisable implémenté

Une cutscene V1 doit être représentée par un asset CasaEngine.

Le fichier doit passer par `AssetInfo`, `AssetContentManager`, `IAssetLoader` et `AssetLoaderRegistry`. Il ne faut pas charger un JSON libre directement depuis le gameplay.

Format attendu :

```text
asset_type = "cutscene"
extension = .cutscene
loader = CutsceneAssetLoader
```

Le modèle doit être typé.

```csharp
public sealed class CutsceneAsset : ObjectBase
{
        public const int CurrentVersion = 1;

        public int Version { get; set; }
        public CutsceneActionData? RootAction { get; set; }
}

public abstract class CutsceneActionData
{
        public abstract string Type { get; }
}

public sealed class WaitCutsceneActionData : CutsceneActionData
{
        public float Seconds { get; set; }
}

public sealed class SequenceCutsceneActionData : CutsceneActionData
{
        public List<CutsceneActionData> Actions { get; } = new();
}

public sealed class ParallelCutsceneActionData : CutsceneActionData
{
        public List<CutsceneActionData> Actions { get; } = new();
}
```

Exemple de contenu `.cutscene` V1 :

```json
{
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "IntroWait",
    "type": "CutsceneAsset",
    "version": 1,
    "schema_version": 1,
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

L’entrée `AssetInfo` correspondante peut définir `asset_type = "cutscene"`. Si `asset_type` est absent, `AssetInfo.InferAssetType` peut l’inférer depuis l’extension `.cutscene`.

À éviter en V1 :

```csharp
Dictionary<string, string> Parameters
```

Le modèle typé a été validé avec `ObjectBase` et le serializer dédié. Il ne faut pas introduire un second format parallèle pour la V1.

---

## Bindings d’entités (futur)

La V1 implémentée n’a aucun binding d’entité. Les identifiants logiques et resolvers décrits dans les exemples de gameplay sont des pistes futures, pas des tâches V1.

Avant toute implémentation future, il faudra choisir les types CasaEngine réels à cibler pour les entités, la caméra, l’animation, le dialogue et l’input. Aucune commande ne doit introduire un service fictif pour contourner cette étape.

---

## Sérialisation depuis script C# (piste future)

Cette section décrit une idée future. Elle n’est pas implémentée dans la V1 et ne correspond pas au modèle actuel `CutsceneAsset.RootAction`.

Si la cutscene est écrite en C#, il peut être utile de produire aussi une description affichable.

Exemple :

```csharp
public sealed class CutsceneScriptBuilder
{
    private readonly List<CutsceneActionData> _actions = new();

    public CutsceneScriptBuilder DisablePlayerControl()
    {
        _actions.Add(new CutsceneActionData
        {
            Type = "DisablePlayerControl"
        });

        return this;
    }

    public CutsceneScriptBuilder MoveActorTo(string actor, string target, float speed)
    {
        _actions.Add(new CutsceneActionData
        {
            Type = "MoveActorTo",
            Parameters =
            {
                ["actor"] = actor,
                ["target"] = target,
                ["speed"] = speed.ToString()
            }
        });

        return this;
    }

    public CutsceneAsset Build(string name)
    {
        return new CutsceneAsset
        {
            Name = name,
            Actions = _actions
        };
    }
}
```

Ce builder permet :

- de construire la séquence ;
- de la sérialiser ;
- de l’afficher dans l’éditeur ;
- de garder une structure simple.

---

## Exemple complet de cutscene

Séquence :

```text
le joueur perd le contrôle
la caméra focus le chef du village
le joueur marche vers lui
le joueur se tourne vers lui
le chef parle
un dialogue s’ouvre
un flag de quête est activé
la caméra est restaurée
le joueur reprend le contrôle
```

Version async :

```csharp
public async Task VillageChiefIntro(CutsceneApi cutscene)
{
    var player = cutscene.FindEntity("Player");
    var chief = cutscene.FindEntity("VillageChief");

    await cutscene.Begin();

    await cutscene.DisablePlayerControl();

    await Task.WhenAll(
        cutscene.MoveTo(player, chief.Position + new Vector2(-32, 0)),
        cutscene.CameraFocus(chief, duration: 0.5f)
    );

    await cutscene.PlayAnimation(player, "IdleRight");
    await cutscene.PlayAnimation(chief, "Talk");

    await cutscene.ShowDialogue("village_chief_intro");

    await cutscene.SetQuestFlag("TalkedToVillageChief", true);

    await cutscene.RestoreCamera();
    await cutscene.EnablePlayerControl();

    await cutscene.End();
}
```

Version séquentielle :

```csharp
var sequence = new CutsceneSequence();

sequence.Add(new DisablePlayerControlCommand());
sequence.Add(new ParallelCommand(
    new MoveActorToCommand(player, chief.Position + new Vector2(-32, 0)),
    new CameraFocusCommand(chief, duration: 0.5f)
));
sequence.Add(new PlayAnimationCommand(player, "IdleRight"));
sequence.Add(new PlayAnimationCommand(chief, "Talk"));
sequence.Add(new ShowDialogueCommand("village_chief_intro"));
sequence.Add(new SetQuestFlagCommand("TalkedToVillageChief", true));
sequence.Add(new RestoreCameraCommand());
sequence.Add(new EnablePlayerControlCommand());

director.Play(sequence);
```

Version sérialisée :

```json
{
  "name": "VillageChiefIntro",
  "actions": [
    {
      "type": "DisablePlayerControl"
    },
    {
      "type": "Parallel",
      "actions": [
        {
          "type": "MoveActorTo",
          "parameters": {
            "actor": "Player",
            "target": "VillageChief.LeftSide",
            "speed": "80"
          }
        },
        {
          "type": "CameraFocus",
          "parameters": {
            "target": "VillageChief",
            "duration": "0.5"
          }
        }
      ]
    },
    {
      "type": "PlayAnimation",
      "parameters": {
        "actor": "Player",
        "animation": "IdleRight"
      }
    },
    {
      "type": "PlayAnimation",
      "parameters": {
        "actor": "VillageChief",
        "animation": "Talk"
      }
    },
    {
      "type": "ShowDialogue",
      "parameters": {
        "dialogueId": "village_chief_intro"
      }
    },
    {
      "type": "SetQuestFlag",
      "parameters": {
        "flag": "TalkedToVillageChief",
        "value": "true"
      }
    },
    {
      "type": "RestoreCamera"
    },
    {
      "type": "EnablePlayerControl"
    }
  ]
}
```

---

## Commandes recommandées pour une V1

### Contrôle

```text
BeginCutscene
EndCutscene
DisablePlayerControl
EnablePlayerControl
WaitSeconds
WaitUntil
Parallel
Sequence
```

### Personnages

```text
MoveActorTo
MoveActorBy
FaceActor
FaceDirection
PlayAnimation
StopAnimation
SetActorVisible
SetActorEnabled
```

### Dialogue

```text
ShowDialogue
ShowDialogueAndWait
CloseDialogue
ShowSubtitle
HideSubtitle
```

### Caméra

```text
PushCamera
RestoreCamera
CameraFocus
CameraMoveTo
CameraZoom
CameraShake
```

### Audio

```text
PlaySound
PlayMusic
StopMusic
FadeMusic
```

### Écran

```text
FadeIn
FadeOut
FlashScreen
```

### Gameplay

```text
SetQuestFlag
SetGameFlag
SpawnEntity
DestroyEntity
EnableEntity
DisableEntity
OpenDoor
CloseDoor
LoadScene
```

---

## Ordre d’implémentation recommandé

### Étape 1 : base runtime

```text
ICutsceneCommand
CutsceneContext
CutsceneSequence
CutsceneDirector
Cancel simple
```

### Étape 2 : commandes essentielles

```text
WaitSecondsCommand
DisablePlayerControlCommand
EnablePlayerControlCommand
MoveActorToCommand
PlayAnimationCommand
ShowDialogueCommand
```

### Étape 3 : caméra et audio

```text
CameraFocusCommand
RestoreCameraCommand
PlaySoundCommand
FadeIn/FadeOut
```

### Étape 4 : async/await

```text
CutsceneRunner
CutsceneApi
RunCommand(command) -> Task
Task.WhenAll pour actions parallèles
```

### Étape 5 : skip et restauration

```text
CancelAll
CompleteImmediately
CutsceneStateSnapshot
restauration input/camera/dialogue
```

### Étape 6 : sérialisation

```text
CutsceneAsset
CutsceneActionData
CutsceneCommandFactory
CutsceneBindingResolver
JSON ou format asset CasaEngine
```

### Étape 7 : éditeur lecture seule

```text
affichage de la liste d’actions
affichage des paramètres
affichage des groupes parallèles
validation des bindings
validation des dialogues/animations
affichage des erreurs
```

---

## Résumé

Le système recommandé repose sur cette idée :

```text
Une cutscene est écrite comme une suite d’instructions,
mais chaque instruction est une commande non bloquante
mise à jour par le moteur à chaque frame.
```

Architecture minimale :

```text
CutsceneSystem
    ├── CutsceneDirector
    ├── CutsceneAsset
    ├── CutsceneActionData
    ├── WaitCutsceneActionData
    ├── SequenceCutsceneActionData
    ├── ParallelCutsceneActionData
    ├── CutsceneAssetLoader
    ├── CutsceneValidationResult
    └── CutsceneDebugSnapshot
```

Pour CasaEngine, la stratégie recommandée est :

```text
1. S’appuyer sur World.CoroutineManager.
2. Ne pas créer CutsceneRunner.
3. Ajouter CutsceneDirector dans World comme façade.
4. Implémenter uniquement Wait, Sequence, Parallel et Stop.
5. Charger CutsceneAsset via le système d’assets CasaEngine.
6. Ajouter validation, debug et éditeur lecture seule.
7. Reporter toutes les commandes gameplay à une version future.
```

Cette approche permet d’obtenir rapidement un socle de cutscene testable sans inventer de services gameplay ni dupliquer le scheduler de coroutines existant.
