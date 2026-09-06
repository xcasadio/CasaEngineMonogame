# GameplayMode dans CasaEngine

## But

Remplacer l'actuelle classe `GameMode` par un concept plus adapté à CasaEngine : un `GameplayMode` responsable des règles de gameplay d'un `World`, sans recopier le modèle Unreal Engine.

Le `GameplayMode` doit permettre d'exprimer des règles comme :

- succès ou échec d'un niveau ;
- pause, reprise, arrêt et redémarrage ;
- timer, score, vies et état runtime simple ;
- objectifs simples dans une étape ultérieure ;
- intégration progressive avec les assets et l'éditeur.

Le `GameplayMode` ne doit pas devenir un `GameManager` global. Il ne doit pas prendre en charge le rendu, l'input brut, la physique, l'audio bas niveau, l'UI complète, les dialogues ou les cutscenes. Ces systèmes existent ou évolueront séparément ; le mode doit seulement recevoir un contexte limité.

## Constats vérifiés dans le dépôt

Ces points décrivent l'état actuel du code au moment de cette note.

- La classe actuelle est [CasaEngine/Framework/Gameplay/GameMode.cs](../../CasaEngine/Framework/Gameplay/GameMode.cs).
- Elle reprend une machine à états de match proche d'Unreal : `EnteringMap`, `WaitingToStart`, `InProgress`, `WaitingPostMatch`, `LeavingMap`, `Aborted`.
- Elle contient beaucoup de code Unreal commenté ou traduit, non relié au moteur CasaEngine.
- `ReadyToStartMatch()` retourne actuellement `false` par défaut, donc `StartPlay()` ne fait pas passer le match en `InProgress` sans appel manuel à `StartMatch()`.
- `World` charge un asset `GameMode` via `GameModeAssetId`, ou crée `new GameMode()` si aucun asset n'est défini.
- `World.BeginPlay()` appelle `GameMode.StartPlay()`.
- `World.Update(FrameTime)` appelle `GameMode?.Tick(elapsedTime)` avant `InternalAddEntities()`, `RuntimeSystems.Update(frameTime)` et l'update des entités.
- `World.InitializePlayerControllers()` utilise `GameMode.DefaultPawnAssetId` et `GameMode.PlayerControllerClass` pour créer le pawn et le controller par défaut.
- Les mondes sérialisent déjà `game_mode_asset_id`.
- Le seul asset `.gameMode` trouvé est `Projects/RPGDemo/RpgGameMode.gameMode`; il stocke `default_pawn_asset_id`, `player_controller_class` et `hud_classClass`.
- `AssetLoaderRegistry` enregistre `GameMode` avec `AssetLoader<GameMode>()`.
- `AssetLoader<T>` construit `new T()` puis appelle `Load(JObject)`. Un type abstrait ne peut donc pas être chargé directement par ce loader générique.
- Le moteur utilise `FrameTime` comme contexte de temps runtime, pas `GameTimeContext`.
- `WorldRuntimeSystems` expose déjà `CoroutineManager` et `CutsceneDirector`.
- Aucun type `Scene` n'a été trouvé dans les recherches ciblées ; le contrat existant à utiliser pour la V1 est donc `World`.
- `GameScreenManager` mentionne `GameStateChanged` dans sa documentation, mais aucun câblage runtime n'a été trouvé autour de cet événement.
- `Projects/CasaEngine.RPGDemo/GameModes/RPGActionGameMode.cs` hérite de `GameMode`, mais la recherche ciblée n'a pas montré de référence qui le charge comme mode actif.

## Critique de l'actuel `GameMode`

L'actuel `GameMode` mélange plusieurs responsabilités qui doivent être séparées.

Il gère une machine à états de match copiée d'Unreal, mais cette machine ne correspond pas encore aux besoins décrits pour CasaEngine : conditions de succès, échec, pause, reprise, restart, objectifs ou résultat explicite. Le vocabulaire `MatchState` est orienté match multijoueur Unreal, alors que le besoin CasaEngine est plus général : niveau, mission, tutoriel, boss fight, survie, puzzle ou mode de gameplay.

Il contient aussi de la configuration de démarrage joueur : `DefaultPawnAssetId`, `PlayerControllerClass`, `AIControllerClass`, `HUDClass`. Ces champs sont utiles au moteur actuel, car `World.InitializePlayerControllers()` les consomme réellement. Ils ne sont cependant pas des règles de gameplay. Les déplacer ou les conserver provisoirement doit être traité comme une migration séparée, sinon le remplacement de `GameMode` cassera le spawn du joueur dans les projets existants.

La classe est chargée comme asset concret par le loader générique. Cela empêche de remplacer directement `GameMode` par une classe abstraite `GameplayMode` chargée depuis `.gameMode`. Si `GameplayMode` devient une classe runtime abstraite, il faut soit un asset de configuration concret, soit un loader/factory capable de créer le bon mode. Pour la V1, il faut éviter de promettre un système data-driven complet avant d'avoir le contrat de chargement correspondant.

La documentation initiale utilisait `Scene` et `GameTimeContext`, mais ces types ne sont pas les contrats observés dans le dépôt. La V1 doit donc parler de `World` et de `FrameTime`. Une intégration à une future abstraction de scène peut rester une étape ultérieure, pas une dépendance de départ.

Enfin, le système actuel n'a pas d'état runtime explicite comparable à `GameplayState` et n'a pas de résultat unique comparable à `GameplayResult`. Les méthodes `IsSuccess()` et `IsFailure()` sont acceptables pour une esquisse, mais elles peuvent produire des états contradictoires. Pour CasaEngine, il vaut mieux introduire directement un résultat explicite, même en V1.

## Architecture cible V1

La V1 doit rester petite et testable. Elle remplace la machine `GameMode` Unreal par trois éléments runtime :

- `GameplayMode` : logique de règles de gameplay ;
- `GameplayModeRunner` : cycle de vie du mode actif ;
- `GameplayState` : état runtime minimal inspectable.

Elle ajoute aussi deux enums :

- `GameplayResult` : résultat courant du mode ;
- `GameplayPhase` : phase runtime du mode.

La V1 ne doit pas encore inclure :

- système complet d'objectifs ;
- bus d'événements gameplay ;
- assets data-driven polymorphes ;
- panneau éditeur avancé ;
- intégration dialogue/cutscene/checkpoint ;
- transitions de scène complexes.

Ces sujets restent valides, mais ils dépendent de contrats supplémentaires et doivent être faits après la base runtime.

## Contrats proposés pour la V1

Les signatures utilisent les types existants du dépôt.

```csharp
public enum GameplayResult
{
    Running,
    Success,
    Failure,
    Cancelled
}
```

```csharp
public enum GameplayPhase
{
    NotStarted,
    Playing,
    Paused,
    Success,
    Failure,
    Stopped
}
```

```csharp
public sealed class GameplayState
{
    public GameplayPhase Phase { get; set; } = GameplayPhase.NotStarted;
    public GameplayResult Result { get; set; } = GameplayResult.Running;
    public float ElapsedTime { get; set; }
    public int Score { get; set; }
    public int Lives { get; set; }
}
```

```csharp
public sealed class GameplayContext
{
    public GameplayContext(World world)
    {
        World = world;
    }

    public World World { get; }
    public CoroutineManager CoroutineManager => World.RuntimeSystems.CoroutineManager;
}
```

`GameplayContext` ne doit contenir que des contrats réellement disponibles. En V1, `World` suffit pour atteindre les services déjà présents, dont `RuntimeSystems.CoroutineManager`. Il ne faut pas ajouter `Scene`, `Audio`, `Input`, `UI`, `Dialogue` ou `Cutscenes` tant que le contrat exact à exposer n'est pas choisi.

```csharp
public abstract class GameplayMode
{
    protected GameplayContext Context { get; private set; } = null!;
    protected GameplayState State { get; private set; } = null!;

    public void Initialize(GameplayContext context, GameplayState state)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);

        Context = context;
        State = state;
        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    public virtual void Start() { }
    public virtual void Update(FrameTime frameTime) { }
    public virtual void Pause() { }
    public virtual void Resume() { }
    public virtual void Stop() { }
    public virtual void Restart() { }
    public virtual void Abort() { }

    public virtual GameplayResult EvaluateResult()
    {
        return GameplayResult.Running;
    }
}
```

`GameplayMode` est une classe runtime. Elle ne doit pas hériter de `ObjectBase` sauf si l'implémentation choisit explicitement de continuer à charger le mode lui-même comme asset concret. Le choix recommandé est de garder le runtime séparé de l'asset de configuration.

```csharp
public sealed class GameplayModeRunner
{
    public GameplayMode? CurrentMode { get; private set; }
    public GameplayState? CurrentState { get; private set; }

    public void Start(GameplayMode mode, GameplayContext context)
    {
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(context);

        Stop();

        CurrentMode = mode;
        CurrentState = new GameplayState();

        mode.Initialize(context, CurrentState);
        mode.Start();

        CurrentState.Phase = GameplayPhase.Playing;
    }

    public void Update(FrameTime frameTime)
    {
        if (CurrentMode == null || CurrentState == null)
        {
            return;
        }

        if (CurrentState.Phase != GameplayPhase.Playing)
        {
            return;
        }

        CurrentState.ElapsedTime += frameTime.DeltaTime;
        CurrentMode.Update(frameTime);

        GameplayResult result = CurrentMode.EvaluateResult();
        CurrentState.Result = result;

        if (result == GameplayResult.Success)
        {
            CurrentState.Phase = GameplayPhase.Success;
        }
        else if (result == GameplayResult.Failure)
        {
            CurrentState.Phase = GameplayPhase.Failure;
        }
        else if (result == GameplayResult.Cancelled)
        {
            CurrentState.Phase = GameplayPhase.Stopped;
        }
    }

    public void Pause()
    {
        if (CurrentMode == null || CurrentState == null || CurrentState.Phase != GameplayPhase.Playing)
        {
            return;
        }

        CurrentState.Phase = GameplayPhase.Paused;
        CurrentMode.Pause();
    }

    public void Resume()
    {
        if (CurrentMode == null || CurrentState == null || CurrentState.Phase != GameplayPhase.Paused)
        {
            return;
        }

        CurrentState.Phase = GameplayPhase.Playing;
        CurrentMode.Resume();
    }

    public void Stop()
    {
        CurrentMode?.Stop();
        CurrentMode = null;
        CurrentState = null;
    }
}
```

Le runner ne doit pas appeler le rendu, la physique, l'UI ou les scripts d'entité directement. Il orchestre seulement le mode actif.

## Intégration dans `World`

L'intégration minimale doit remplacer les appels directs à `GameMode` dans `World` sans changer l'ordre d'update plus que nécessaire.

À faire :

1. Ajouter un `GameplayModeRunner` au runtime du `World` ou directement au `World`.
2. Ajouter une méthode explicite pour démarrer un mode runtime, par exemple `SetGameplayMode(GameplayMode mode)`.
3. Construire le `GameplayContext` avec le `World` courant.
4. Remplacer l'appel `GameMode?.Tick(elapsedTime)` par l'update du runner, au même emplacement dans `World.Update(FrameTime)` pour préserver l'ordre runtime initial.
5. Arrêter le mode actif dans `World.Clear()` avant ou avec le nettoyage des systèmes runtime.
6. Exposer l'état courant pour debug/tests via le runner, pas via une machine à états string-based.

Le placement exact du runner doit respecter l'ordre actuel : aujourd'hui `GameMode.Tick()` est exécuté avant `InternalAddEntities()`, `RuntimeSystems.Update(frameTime)` et l'update des entités. Si le runner est déplacé dans `WorldRuntimeSystems.Update()`, cet ordre change. Ce déplacement doit donc être une décision explicite, pas un effet secondaire.

## Migration de l'ancien asset `.gameMode`

L'ancien asset `.gameMode` ne contient pas des règles de gameplay. Il contient au moins la configuration de pawn/controller par défaut.

Avant de supprimer `GameMode`, il faut traiter ces champs :

- `default_pawn_asset_id` ;
- `player_controller_class` ;
- `hud_classClass` ;
- éventuellement `ai_controller_class`, actuellement commenté au chargement.

Options de migration possibles :

1. Conserver temporairement une classe de compatibilité chargée depuis `.gameMode` pour alimenter `World.InitializePlayerControllers()`.
2. Déplacer ces champs vers la sérialisation du `World`, puisque le `World` les consomme déjà directement pour initialiser les players.
3. Créer plus tard un asset séparé pour la configuration de démarrage joueur.

La V1 ne doit pas cacher ces champs dans `GameplayMode`, car ils ne décrivent pas le résultat, les objectifs, la pause ou les règles du mode. Les mélanger à nouveau recréerait le problème de l'actuel `GameMode`.

## Assets de gameplay

Le chargement data-driven doit être une étape séparée.

Le loader actuel `AssetLoader<T>` impose `where T : ISerializable, new()` et construit directement `new T()`. Il ne permet pas de charger une classe abstraite `GameplayMode` sans loader spécialisé ou asset concret.

Pour une étape asset propre, il faudra introduire un asset concret, par exemple un asset de configuration qui hérite de `ObjectBase` et expose une méthode de création de mode runtime. Le nom et le format exact doivent être choisis pendant cette étape, en tenant compte de la compatibilité avec `game_mode_asset_id` et les fichiers `.gameMode` existants.

La V1 runtime peut donc être livrée sans asset data-driven complet, à condition que le moteur permette de démarrer un mode par code. La migration asset vient ensuite.

## Exemple V1 : mode de survie

```csharp
public sealed class SurvivalGameplayMode : GameplayMode
{
    private readonly float _duration;
    private bool _playerDead;

    public SurvivalGameplayMode(float duration)
    {
        _duration = duration;
    }

    public void OnPlayerDead()
    {
        _playerDead = true;
    }

    public override GameplayResult EvaluateResult()
    {
        if (_playerDead)
        {
            return GameplayResult.Failure;
        }

        if (State.ElapsedTime >= _duration)
        {
            return GameplayResult.Success;
        }

        return GameplayResult.Running;
    }
}
```

Cet exemple ne scanne pas le `World` à chaque frame. L'événement `OnPlayerDead()` reste appelé par le code de gameplay existant ou futur.

## Exemple V1 : collecte simple

```csharp
public sealed class CollectItemsGameplayMode : GameplayMode
{
    private readonly int _requiredCount;
    private int _collectedCount;

    public CollectItemsGameplayMode(int requiredCount)
    {
        _requiredCount = requiredCount;
    }

    public void OnItemCollected()
    {
        _collectedCount++;
    }

    public override GameplayResult EvaluateResult()
    {
        return _collectedCount >= _requiredCount
            ? GameplayResult.Success
            : GameplayResult.Running;
    }
}
```

Cet exemple reste volontairement manuel. Le bus d'événements gameplay peut venir ensuite, mais il ne doit pas être requis pour valider la V1.

## Étapes d'implémentation recommandées

### Étape 0 - Préserver l'existant

- Lister les usages de `GameMode` avant suppression.
- Garder une compatibilité pour `default_pawn_asset_id` et `player_controller_class`.
- Vérifier le chargement de `Projects/RPGDemo/RpgGameMode.gameMode`.
- Ne pas supprimer `game_mode_asset_id` tant que les mondes existants l'utilisent.

### Étape 1 - Runtime V1

- Créer `GameplayResult`.
- Créer `GameplayPhase`.
- Créer `GameplayState`.
- Créer `GameplayContext` basé sur `World` et `FrameTime`.
- Créer `GameplayMode`.
- Créer `GameplayModeRunner`.
- Brancher le runner dans `World.Update(FrameTime)` à l'emplacement actuel de `GameMode.Tick()`.
- Arrêter le runner dans `World.Clear()`.

### Étape 2 - Remplacement progressif de `GameMode`

- Remplacer `World.GameMode` par le runner ou une propriété orientée gameplay.
- Remplacer `GameMode.StartPlay()` par `GameplayModeRunner.Start(...)` au moment approprié du `BeginPlay`.
- Remplacer `MatchState` string-based par `GameplayState.Phase` et `GameplayState.Result`.
- Mettre à jour ou retirer `RPGActionGameMode`.
- Mettre à jour la documentation de `GameScreenManager`, qui référence encore `GameMode.GameStateChanged`.

### Étape 3 - Migration spawn/player

- Sortir `DefaultPawnAssetId` et `PlayerControllerClass` de la responsabilité gameplay.
- Adapter `World.InitializePlayerControllers()` au nouvel emplacement de ces données.
- Garder une lecture compatible de l'ancien JSON tant que les projets existants ne sont pas migrés.
- Corriger ou migrer la clé `hud_classClass` si le HUD devient réellement utilisé.

### Étape 4 - Assets de configuration

- Ajouter un asset concret de configuration de gameplay seulement après la V1 runtime.
- Enregistrer son loader dans `AssetLoaderRegistry`.
- Définir comment `game_mode_asset_id` évolue : conservation, renommage ou migration.
- Ajouter un premier asset concret seulement si son format JSON est défini.

### Étape 5 - Objectifs et événements

- Ajouter `GameplayObjective` après validation du runner.
- Ajouter un bus d'événements gameplay seulement si plusieurs systèmes doivent publier des faits gameplay sans dépendance directe au mode.
- Éviter les scans coûteux du `World` dans `Update`.
- Ne pas utiliser LINQ dans les chemins `Update`.

### Étape 6 - Éditeur

- Afficher le mode actif et le `GameplayState` courant.
- Afficher `Phase`, `Result`, `ElapsedTime`, `Score` et `Lives`.
- Ajouter les objectifs uniquement après l'étape objectifs.
- Préparer l'édition d'assets seulement après stabilisation du format asset.

## Tests et validations minimales

Pour valider la V1 :

- test unitaire du `GameplayModeRunner.Start()` : initialise le mode, crée un état, passe en `Playing` ;
- test unitaire du `GameplayModeRunner.Update()` : incrémente `ElapsedTime` et propage `Success` ou `Failure` ;
- test unitaire de `Pause()` et `Resume()` : bloque puis reprend l'update ;
- test de compatibilité ou smoke test de chargement d'un `World` qui contient `game_mode_asset_id` ;
- build de la solution après migration des usages de `GameMode`.

## Décisions à ne pas prendre en V1

- Ajouter une classe `Scene` pour cette fonctionnalité.
- Introduire des services globaux dans `GameplayContext` sans contrat existant.
- Faire un système complet d'objectifs avant le runner.
- Faire dépendre `GameplayMode` de l'UI ou du rendu.
- Renommer les extensions de fichiers sans plan de migration.
- Supprimer les champs de spawn joueur sans compatibilité.

## Résultat attendu

Après la V1, CasaEngine doit avoir un système runtime clair :

```text
World = runtime et conteneur d'entités
GameplayModeRunner = cycle de vie du mode actif
GameplayMode = règles du mode
GameplayState = état runtime inspectable
GameplayResult = résultat unique du mode
GameplayPhase = phase courante du mode
```

L'ancien `GameMode` peut alors être retiré progressivement, mais seulement après migration de ce qu'il portait réellement : la configuration de pawn/controller par défaut et les références asset existantes.

Decisions: see [ADR-0024](../decisions/0024-gameplaymode-v1.md).
