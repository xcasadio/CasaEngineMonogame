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
Script de cutscene
    ↓
Commandes séquentielles
    ↓
CutsceneRunner
    ↓
Update frame par frame
    ↓
Fin, skip ou annulation
```

---

## Problème à résoudre

Dans un jeu, une cinématique ne doit jamais bloquer la boucle principale.

Il ne faut pas faire :

```csharp
player.MoveTo(npc.Position);
Thread.Sleep(2000);
dialogue.Open("intro");
```

Parce que `Thread.Sleep` bloque tout :

```text
plus de rendu
plus d’input
plus d’animation
plus d’audio
plus de simulation
```

Un moteur de jeu fonctionne frame par frame :

```text
Update frame 1
Update frame 2
Update frame 3
Update frame 4
...
```

Une séquence scriptée doit donc pouvoir exprimer :

```text
déplace le joueur vers le PNJ
attends qu’il arrive
ouvre un dialogue
attends que le dialogue soit fermé
rend le contrôle au joueur
```

mais sans arrêter le moteur.

---

## Principe général

Une cutscene est une suite de commandes.

Chaque commande peut durer :

- une seule frame ;
- plusieurs frames ;
- un temps défini ;
- jusqu’à la fin d’une animation ;
- jusqu’à la fermeture d’un dialogue ;
- jusqu’à ce qu’un personnage atteigne une position ;
- jusqu’à ce qu’une condition soit vraie.

Exemples de commandes :

```text
DisablePlayerControlCommand
MoveActorToCommand
PlayAnimationCommand
ShowDialogueCommand
WaitSecondsCommand
CameraFocusCommand
FadeOutCommand
PlaySoundCommand
SetQuestFlagCommand
EnablePlayerControlCommand
```

Chaque commande est mise à jour par le moteur à chaque frame.

---

## Structure minimale d’une commande

Une commande de cutscene peut être représentée par une interface simple :

```csharp
public interface ICutsceneCommand
{
    void Start(CutsceneContext context);
    void Update(CutsceneContext context, float deltaTime);
    bool IsFinished { get; }
    void Cancel(CutsceneContext context);
}
```

### `Start`

Appelé une seule fois au début de la commande.

Exemples :

- lancer une animation ;
- ouvrir un dialogue ;
- initialiser un timer ;
- démarrer un déplacement ;
- sauvegarder une position de départ.

### `Update`

Appelé à chaque frame tant que la commande n’est pas terminée.

Exemples :

- avancer un personnage vers une cible ;
- interpoler une caméra ;
- mettre à jour un fade ;
- attendre un timer ;
- vérifier si une condition est vraie.

### `IsFinished`

Indique si la commande est terminée.

Quand `IsFinished == true`, le système passe à la commande suivante.

### `Cancel`

Permet d’arrêter proprement la commande.

Utile pour :

- skip de cutscene ;
- changement de scène ;
- interruption forcée ;
- fermeture d’un dialogue ;
- restauration de l’état joueur.

---

## CutsceneSequence simple

Une séquence peut être une file de commandes exécutées une par une.

```csharp
public sealed class CutsceneSequence
{
    private readonly Queue<ICutsceneCommand> _commands = new();
    private ICutsceneCommand? _currentCommand;

    public bool IsFinished => _currentCommand == null && _commands.Count == 0;

    public void Add(ICutsceneCommand command)
    {
        _commands.Enqueue(command);
    }

    public void Start(CutsceneContext context)
    {
        _currentCommand = null;
    }

    public void Update(CutsceneContext context, float deltaTime)
    {
        if (_currentCommand == null)
        {
            if (_commands.Count == 0)
                return;

            _currentCommand = _commands.Dequeue();
            _currentCommand.Start(context);
        }

        _currentCommand.Update(context, deltaTime);

        if (_currentCommand.IsFinished)
        {
            _currentCommand = null;
        }
    }

    public void Cancel(CutsceneContext context)
    {
        _currentCommand?.Cancel(context);
        _currentCommand = null;
        _commands.Clear();
    }
}
```

Exemple d’utilisation :

```csharp
var sequence = new CutsceneSequence();

sequence.Add(new DisablePlayerControlCommand());
sequence.Add(new MoveActorToCommand(player, npc.Position));
sequence.Add(new PlayAnimationCommand(player, "IdleRight"));
sequence.Add(new ShowDialogueCommand("npc_intro_001"));
sequence.Add(new EnablePlayerControlCommand());
```

Dans la boucle du moteur :

```csharp
cutsceneSequence.Update(context, deltaTime);
```

---

## CutsceneDirector

Le `CutsceneDirector` est le point d’entrée principal.

Il ne doit pas contenir toute la logique de gameplay.  
Son rôle est d’orchestrer les systèmes existants.

```text
CutsceneDirector
    ├── lance une cutscene
    ├── bloque ou limite les inputs
    ├── résout les entités utilisées
    ├── exécute les commandes
    ├── gère pause / skip / annulation
    ├── restaure l’état du jeu
    └── signale la fin de la cutscene
```

Exemple :

```csharp
public sealed class CutsceneDirector
{
    private CutsceneSequence? _currentSequence;
    private readonly CutsceneContext _context;

    public bool IsPlaying => _currentSequence != null && !_currentSequence.IsFinished;

    public void Play(CutsceneSequence sequence)
    {
        _currentSequence = sequence;
        _currentSequence.Start(_context);
    }

    public void Update(float deltaTime)
    {
        if (_currentSequence == null)
            return;

        _currentSequence.Update(_context, deltaTime);

        if (_currentSequence.IsFinished)
        {
            _currentSequence = null;
        }
    }

    public void Cancel()
    {
        _currentSequence?.Cancel(_context);
        _currentSequence = null;
    }
}
```

---

## CutsceneContext

Le `CutsceneContext` donne accès aux systèmes nécessaires sans que les commandes dépendent directement du moteur entier.

```csharp
public sealed class CutsceneContext
{
    public InputManager InputManager { get; init; }
    public DialogueSystem DialogueSystem { get; init; }
    public CameraManager CameraManager { get; init; }
    public AudioSystem AudioSystem { get; init; }
    public EntityWorld World { get; init; }
    public QuestSystem QuestSystem { get; init; }
}
```

Le but est que les commandes soient simples et découplées.

```text
MoveActorToCommand utilise CharacterController / Transform
ShowDialogueCommand utilise DialogueSystem
CameraFocusCommand utilise CameraManager
PlaySoundCommand utilise AudioSystem
SetQuestFlagCommand utilise QuestSystem
```

---

## Autorité pendant une cutscene

Pendant le gameplay normal :

```text
PlayerController possède l’autorité sur le personnage.
```

Pendant une cutscene :

```text
CutsceneDirector possède temporairement l’autorité.
```

Mais il ne doit pas tout gérer directement.

Il doit utiliser les systèmes existants :

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

Il y a deux approches utiles pour une V1.

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
- suffisant pour une première version ;
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

Pour CasaEngine, l’approche recommandée est :

```text
V1 simple : déplacement direct possible
V1 propre : passer par CharacterController quand c’est possible
```

---

## Animation pendant une cutscene

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

Pour une V1, il est acceptable de ne gérer que le dialogue bloquant.

---

## Inputs joueur

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

Une cutscene ne devrait pas modifier directement la caméra gameplay de manière définitive.

Il est préférable d’utiliser un `CameraManager`.

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
Base interne : ICutsceneCommand + CutsceneRunner
API utilisateur : async/await
Option future : coroutine si besoin
```

---

## Skip et annulation

Un vrai système de cutscene doit gérer le skip.

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

`CompleteImmediately` applique directement l’état final.

Exemple :

```text
le personnage est téléporté à la position cible
la porte est ouverte
le flag de quête est activé
la caméra est restaurée
```

Pour une V1, on peut enrichir l’interface :

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

## Modèle sérialisable simple

Une cutscene peut être représentée par un asset.

```csharp
public sealed class CutsceneAsset
{
    public string Name { get; set; }
    public List<CutsceneActionData> Actions { get; set; } = new();
}
```

Chaque action contient un type et des paramètres.

```csharp
public sealed class CutsceneActionData
{
    public string Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}
```

Exemple JSON :

```json
{
  "name": "VillageChiefIntro",
  "actions": [
    {
      "type": "DisablePlayerControl",
      "parameters": {}
    },
    {
      "type": "MoveActorTo",
      "parameters": {
        "actor": "Player",
        "target": "VillageChief.LeftSide",
        "speed": "80"
      }
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
      "type": "EnablePlayerControl",
      "parameters": {}
    }
  ]
}
```

---

## Bindings d’entités

Il faut éviter de sérialiser des références directes vers des instances runtime.

À la place, on utilise des identifiants logiques.

```text
Player
VillageChief
MainCamera
Door_A
```

À l’exécution, le moteur résout ces identifiants.

```csharp
public sealed class CutsceneBindingResolver
{
    public Entity ResolveEntity(string bindingId)
    {
        // Exemple :
        // "Player" -> joueur actuel
        // "VillageChief" -> entité avec id ou tag correspondant
        // "Door_A" -> porte de la scène
    }
}
```

Avantages :

- la cutscene peut être rejouée dans une scène chargée ;
- les données sont sérialisables ;
- le fichier ne dépend pas d’une instance mémoire ;
- on peut afficher les actions dans l’éditeur.

---

## Factory de commandes

Pour exécuter une cutscene sérialisée, il faut convertir les données en commandes runtime.

```csharp
public sealed class CutsceneCommandFactory
{
    private readonly CutsceneBindingResolver _resolver;

    public ICutsceneCommand CreateCommand(CutsceneActionData action)
    {
        switch (action.Type)
        {
            case "DisablePlayerControl":
                return new DisablePlayerControlCommand();

            case "EnablePlayerControl":
                return new EnablePlayerControlCommand();

            case "MoveActorTo":
            {
                var actorId = action.Parameters["actor"];
                var targetId = action.Parameters["target"];
                var speed = float.Parse(action.Parameters["speed"]);

                var actor = _resolver.ResolveEntity(actorId);
                var target = _resolver.ResolvePosition(targetId);

                return new MoveActorToCommand(actor, target, speed);
            }

            case "PlayAnimation":
            {
                var actorId = action.Parameters["actor"];
                var animation = action.Parameters["animation"];

                var actor = _resolver.ResolveEntity(actorId);

                return new PlayAnimationCommand(actor, animation);
            }

            case "ShowDialogue":
            {
                var dialogueId = action.Parameters["dialogueId"];
                return new ShowDialogueCommand(dialogueId);
            }

            default:
                throw new InvalidOperationException($"Unknown cutscene action type: {action.Type}");
        }
    }
}
```

---

## Actions parallèles sérialisées

La V1 peut rester strictement séquentielle.

Cependant, il est utile de prévoir une action spéciale `Parallel`.

Exemple :

```json
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
}
```

Comportement :

```text
lance toutes les sous-actions
attend qu’elles soient toutes terminées
puis passe à l’action suivante
```

Cela correspond à :

```csharp
await Task.WhenAll(
    cutscene.MoveTo(player, target),
    cutscene.CameraFocus(chief, 0.5f)
);
```

---

## Éditeur V1 : affichage uniquement

La partie éditeur ne doit pas être un éditeur complet de timeline.

Pour la V1, l’éditeur sert uniquement à :

```text
afficher les actions du script
afficher les paramètres
afficher l’ordre d’exécution
afficher les groupes parallèles
afficher les erreurs de binding
afficher l’état de sérialisation
```

Il ne permet pas encore :

```text
d’ajouter une action
de supprimer une action
de réordonner les actions
d’éditer une timeline
d’éditer les paramètres
de prévisualiser finement la scène
```

---

## Vue éditeur recommandée

L’éditeur peut afficher une liste hiérarchique.

Exemple :

```text
Cutscene: VillageChiefIntro

01. DisablePlayerControl
02. Parallel
    ├── MoveActorTo
    │     actor: Player
    │     target: VillageChief.LeftSide
    │     speed: 80
    └── CameraFocus
          target: VillageChief
          duration: 0.5
03. PlayAnimation
      actor: Player
      animation: IdleRight
04. PlayAnimation
      actor: VillageChief
      animation: Talk
05. ShowDialogue
      dialogueId: village_chief_intro
06. SetQuestFlag
      flag: TalkedToVillageChief
      value: true
07. EnablePlayerControl
```

Chaque action peut afficher :

```text
index
type
description courte
paramètres
validité
durée estimée si connue
binding résolu ou non
```

---

## Validation éditeur

Même sans édition, l’éditeur doit aider à détecter les problèmes.

Validation possible :

```text
action inconnue
paramètre manquant
paramètre invalide
entity binding introuvable
dialogueId introuvable
animation introuvable
quest flag inconnu
camera binding introuvable
groupe Parallel vide
commande non skippable
```

Exemple d’affichage :

```text
03. PlayAnimation
      actor: VillageChief
      animation: Talk
      status: OK

04. ShowDialogue
      dialogueId: village_chief_intro
      status: Missing dialogue asset
```

---

## Sérialisation depuis script C#

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
    ├── CutsceneRunner
    ├── CutsceneApi
    ├── CutsceneContext
    ├── ICutsceneCommand
    ├── CutsceneSequence
    ├── CutsceneAsset
    ├── CutsceneActionData
    ├── CutsceneCommandFactory
    └── CutsceneBindingResolver
```

Pour CasaEngine, la stratégie recommandée est :

```text
1. Construire un système de commandes séquentielles simple.
2. Ajouter une API async/await pour écrire les scripts lisiblement.
3. Gérer proprement inputs, dialogues, animations et caméra.
4. Ajouter skip, cancel et restauration d’état.
5. Sérialiser les actions.
6. Ajouter un éditeur en lecture seule qui affiche les actions et valide les données.
```

Cette approche permet d’obtenir rapidement un système utilisable pour des cinématiques, dialogues et événements de map, sans développer immédiatement un éditeur visuel complexe.
