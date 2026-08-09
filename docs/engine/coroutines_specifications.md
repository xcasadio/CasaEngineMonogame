# Spécification — Système de coroutines pour CasaEngine

## Objectif

Ajouter à CasaEngine un système de **coroutines runtime** proche de Unity, adapté à un moteur C# / MonoGame.

Le but est de permettre d’écrire des comportements séquentiels dans le temps sans devoir gérer manuellement des états, timers et booléens dans `Update`.

Exemples d’usage :

- attendre plusieurs frames ;
- attendre un délai en secondes ;
- déplacer progressivement une entité ;
- enchaîner une séquence scriptée ;
- attendre la fin d’une animation ;
- attendre la fermeture d’un dialogue ;
- désactiver puis réactiver les inputs joueur ;
- faire des transitions de scène ;
- exécuter des scripts de gameplay simples ;
- charger progressivement des ressources sans bloquer une frame complète.

---

## Principe général

Une coroutine est une méthode C# retournant `IEnumerator` ou un type équivalent.

Elle s’exécute jusqu’à rencontrer un `yield return`, puis elle est suspendue. Le moteur reprend son exécution plus tard selon l’instruction retournée.

Exemple conceptuel :

```csharp
IEnumerator IntroSequence()
{
    playerInput.Enabled = false;

    yield return MoveEntityTo(npc, targetPosition, 1.5f);
    yield return new WaitForSeconds(0.5f);

    dialogueBox.Show("Bienvenue !");
    yield return new WaitUntil(() => dialogueBox.IsClosed);

    playerInput.Enabled = true;
}
```

Le code exprime une séquence temporelle lisible :

```text
désactiver les inputs
déplacer le PNJ
attendre 0.5 seconde
ouvrir le dialogue
attendre sa fermeture
réactiver les inputs
```

---

# 1. Fonctionnalités V1

La V1 doit rester simple, robuste et facile à intégrer dans la boucle `Update` de CasaEngine.

## 1.1 Démarrage d’une coroutine

Le moteur doit permettre de démarrer une coroutine depuis un objet de jeu, un composant, un système ou un service global.

API proposée :

```csharp
CoroutineHandle StartCoroutine(IEnumerator routine);
CoroutineHandle StartCoroutine(IEnumerator routine, object? owner);
```

Exemple :

```csharp
_coroutineManager.StartCoroutine(Blink());
```

ou depuis un composant :

```csharp
StartCoroutine(Blink());
```

---

## 1.2 Identifiant de coroutine

Chaque coroutine démarrée doit retourner un handle.

```csharp
public readonly struct CoroutineHandle
{
    public int Id { get; }
    public bool IsValid { get; }
}
```

Le handle permet de :

- arrêter une coroutine précise ;
- vérifier si elle est encore active ;
- l’utiliser dans les outils de debug ;
- l’associer à un propriétaire.

---

## 1.3 Arrêt d’une coroutine

API proposée :

```csharp
void StopCoroutine(CoroutineHandle handle);
void StopAllCoroutines();
void StopAllCoroutines(object owner);
```

Cas d’usage :

```csharp
CoroutineHandle blinkRoutine;

blinkRoutine = StartCoroutine(Blink());

StopCoroutine(blinkRoutine);
```

Quand un objet est détruit, désactivé ou retiré de la scène, le moteur doit pouvoir arrêter automatiquement les coroutines associées à cet objet.

---

## 1.4 Attente d’une frame

Instruction de base :

```csharp
yield return null;
```

Comportement :

- la coroutine est suspendue ;
- elle reprend à la prochaine frame ;
- elle reprend dans la phase de mise à jour choisie par le scheduler.

Exemple :

```csharp
IEnumerator WaitOneFrame()
{
    yield return null;
    Console.WriteLine("Frame suivante");
}
```

---

## 1.5 Attente en secondes

Instruction proposée :

```csharp
yield return new WaitForSeconds(1.5f);
```

Comportement :

- utilise le temps de jeu ;
- est affecté par le `TimeScale` ;
- reprend à la première frame disponible après expiration du délai.

Classe proposée :

```csharp
public sealed class WaitForSeconds : ICoroutineInstruction
{
    public float Duration { get; }
}
```

---

## 1.6 Attente en temps réel

Instruction proposée :

```csharp
yield return new WaitForSecondsRealtime(1.5f);
```

Comportement :

- utilise le temps réel ;
- ignore le `TimeScale` ;
- utile pour les menus, pauses, UI, transitions système.

Classe proposée :

```csharp
public sealed class WaitForSecondsRealtime : ICoroutineInstruction
{
    public float Duration { get; }
}
```

---

## 1.7 Attente d’un nombre de frames

Instruction proposée :

```csharp
yield return new WaitForFrames(3);
```

Comportement :

- attend exactement N frames ;
- pratique pour différer une action après le layout UI, après un changement de scène, ou après une frame de rendu.

Classe proposée :

```csharp
public sealed class WaitForFrames : ICoroutineInstruction
{
    public int FrameCount { get; }
}
```

---

## 1.8 Attente d’une condition

Instructions proposées :

```csharp
yield return new WaitUntil(() => condition);
yield return new WaitWhile(() => condition);
```

Exemple :

```csharp
yield return new WaitUntil(() => dialogueBox.IsClosed);
```

Comportement :

- `WaitUntil` reprend lorsque la condition devient vraie ;
- `WaitWhile` reprend lorsque la condition devient fausse ;
- la condition est testée à chaque update du scheduler.

Classes proposées :

```csharp
public sealed class WaitUntil : ICoroutineInstruction
{
    public Func<bool> Predicate { get; }
}

public sealed class WaitWhile : ICoroutineInstruction
{
    public Func<bool> Predicate { get; }
}
```

---

## 1.9 Coroutines imbriquées

Le système doit permettre d’attendre la fin d’une autre coroutine.

Exemple :

```csharp
IEnumerator Sequence()
{
    yield return MoveEntityTo(player, target, 1.0f);
    yield return PlayAnimation(player, "Idle");
}
```

Cela implique que le scheduler sache gérer :

```csharp
yield return IEnumerator;
```

Comportement attendu :

- la coroutine parente est suspendue ;
- la coroutine enfant est exécutée ;
- la coroutine parente reprend quand l’enfant est terminé.

---

## 1.10 Fin naturelle d’une coroutine

Une coroutine est terminée quand son `IEnumerator.MoveNext()` retourne `false`.

Le moteur doit alors :

- la retirer de la liste active ;
- libérer ses références ;
- marquer son handle comme terminé ;
- déclencher éventuellement un callback de fin ;
- reprendre les coroutines parentes qui attendaient sa fin.

---

# 2. API C# proposée

## 2.1 Interface principale

```csharp
public interface ICoroutineManager
{
    CoroutineHandle StartCoroutine(IEnumerator routine);
    CoroutineHandle StartCoroutine(IEnumerator routine, object? owner);

    void StopCoroutine(CoroutineHandle handle);
    void StopAllCoroutines();
    void StopAllCoroutines(object owner);

    bool IsRunning(CoroutineHandle handle);

    void Update(CoroutineUpdateContext context);
}
```

---

## 2.2 Contexte d’update

```csharp
public readonly struct CoroutineUpdateContext
{
    public float DeltaTime { get; }
    public float UnscaledDeltaTime { get; }
    public float TimeScale { get; }
    public long FrameIndex { get; }
}
```

Ce contexte permet aux instructions de savoir comment avancer.

---

## 2.3 Instruction de coroutine

```csharp
public interface ICoroutineInstruction
{
    bool IsCompleted(CoroutineUpdateContext context);
}
```

Une instruction est terminée quand `IsCompleted` retourne `true`.

Exemple simplifié :

```csharp
public sealed class WaitForSeconds : ICoroutineInstruction
{
    private float _remainingTime;

    public WaitForSeconds(float seconds)
    {
        _remainingTime = seconds;
    }

    public bool IsCompleted(CoroutineUpdateContext context)
    {
        _remainingTime -= context.DeltaTime;
        return _remainingTime <= 0f;
    }
}
```

---

## 2.4 Classe interne de coroutine

```csharp
internal sealed class CoroutineInstance
{
    public CoroutineHandle Handle;
    public object? Owner;
    public Stack<IEnumerator> Stack;
    public object? CurrentYield;
    public bool IsStopped;
    public bool IsCompleted;
}
```

Le `Stack<IEnumerator>` permet de gérer les coroutines imbriquées.

---

# 3. Intégration dans CasaEngine

## 3.1 Où placer le système

Structure proposée :

```text
CasaEngine.Framework/
  Scripting/
    Coroutines/
      CoroutineManager.cs
      CoroutineHandle.cs
      CoroutineInstance.cs
      CoroutineUpdateContext.cs
      ICoroutineInstruction.cs
      WaitForSeconds.cs
      WaitForSecondsRealtime.cs
      WaitForFrames.cs
      WaitUntil.cs
      WaitWhile.cs
```

Le système peut être dans un namespace du type :

```csharp
namespace CasaEngine.Framework.Scripting.Coroutines;
```

---

## 3.2 Intégration dans la boucle moteur

Le `CoroutineManager` doit être mis à jour une fois par frame.

Exemple :

```csharp
public void Update(GameTime gameTime)
{
    Time.Update(gameTime);

    _coroutineManager.Update(new CoroutineUpdateContext(
        deltaTime: Time.DeltaTime,
        unscaledDeltaTime: Time.UnscaledDeltaTime,
        timeScale: Time.TimeScale,
        frameIndex: Time.FrameIndex));
}
```

---

## 3.3 Phases d’exécution

Pour la V1, une seule phase suffit :

```text
Update
```

Pour une V2, le système peut gérer plusieurs phases :

```csharp
public enum CoroutineUpdatePhase
{
    Update,
    LateUpdate,
    FixedUpdate,
    EndOfFrame
}
```

Instructions futures :

```csharp
yield return new WaitForFixedUpdate();
yield return new WaitForLateUpdate();
yield return new WaitForEndOfFrame();
```

---

## 3.4 Coroutines liées aux composants

Si CasaEngine possède un système `GameObject / Component`, chaque composant peut exposer :

```csharp
protected CoroutineHandle StartCoroutine(IEnumerator routine);
protected void StopCoroutine(CoroutineHandle handle);
protected void StopAllCoroutines();
```

Ces méthodes appellent le `CoroutineManager` global en utilisant le composant comme propriétaire.

Exemple :

```csharp
public class DoorComponent : Component
{
    private CoroutineHandle _openRoutine;

    public void Open()
    {
        _openRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        // ouvrir la porte
    }
}
```

---

## 3.5 Cycle de vie des objets

Le moteur doit définir clairement ce qui arrive aux coroutines quand leur propriétaire change d’état.

Recommandation V1 :

| Événement propriétaire | Comportement recommandé |
|---|---|
| Composant désactivé | Option configurable |
| GameObject désactivé | Stop automatique par défaut |
| Scène déchargée | Stop automatique |
| Objet détruit | Stop automatique |
| Changement de monde | Stop automatique sauf coroutines globales |

Option possible :

```csharp
public enum CoroutineOwnerPolicy
{
    StopWhenOwnerDisabled,
    ContinueWhenOwnerDisabled,
    StopWhenOwnerDestroyed
}
```

Pour la V1, on peut commencer avec :

```text
StopWhenOwnerDestroyed
```

et ajouter les autres politiques ensuite.

---

# 4. Types d’attente à prévoir

## 4.1 V1 indispensable

```text
yield return null
WaitForSeconds
WaitForSecondsRealtime
WaitForFrames
WaitUntil
WaitWhile
IEnumerator imbriqué
CoroutineHandle imbriqué
```

---

## 4.2 V2 utile

```text
WaitForAnimation
WaitForTween
WaitForDialogueClosed
WaitForSceneLoaded
WaitForAssetLoaded
WaitForInput
WaitForEvent
WaitForEndOfFrame
WaitForFixedUpdate
WaitForLateUpdate
```

---

## 4.3 V3 avancée

```text
WaitForTask
WaitForJob
WaitForSignal
WaitForTimelineMarker
WaitForNetworkResponse
WaitForPhysicsStep
WaitForRenderFence
```

---

# 5. Exemples de coroutines utiles dans un moteur de jeu

## 5.1 Déplacement progressif

```csharp
IEnumerator MoveEntityTo(Entity entity, Vector3 targetPosition, float duration)
{
    Vector3 startPosition = entity.Transform.Position;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.DeltaTime;

        float t = elapsed / duration;
        entity.Transform.Position = Vector3.Lerp(startPosition, targetPosition, t);

        yield return null;
    }

    entity.Transform.Position = targetPosition;
}
```

---

## 5.2 Clignotement

```csharp
IEnumerator Blink(Renderer renderer, int count, float interval)
{
    for (int i = 0; i < count; i++)
    {
        renderer.Enabled = false;
        yield return new WaitForSeconds(interval);

        renderer.Enabled = true;
        yield return new WaitForSeconds(interval);
    }
}
```

---

## 5.3 Dialogue

```csharp
IEnumerator ShowDialogueSequence()
{
    InputManager.PlayerInputEnabled = false;

    DialogueBox.Show("Bonjour !");
    yield return new WaitUntil(() => DialogueBox.IsClosed);

    InputManager.PlayerInputEnabled = true;
}
```

---

## 5.4 Cinématique simple

```csharp
IEnumerator Cutscene()
{
    PlayerInput.Enabled = false;

    yield return CameraFade.Out(0.5f);
    yield return SceneLoader.LoadSceneAsync("Village");
    yield return CameraFade.In(0.5f);

    Dialogue.Show("Nous sommes arrivés.");
    yield return new WaitUntil(() => Dialogue.IsClosed);

    PlayerInput.Enabled = true;
}
```

---

# 6. Gestion des erreurs

## 6.1 Exception dans une coroutine

Si une coroutine lance une exception :

```csharp
IEnumerator BrokenRoutine()
{
    throw new Exception("Erreur");
}
```

Le moteur doit :

- capturer l’exception ;
- logger le nom de la coroutine ;
- logger le propriétaire si disponible ;
- arrêter proprement cette coroutine ;
- ne pas faire planter tout le scheduler.

Comportement recommandé :

```text
Exception dans une coroutine = arrêt de cette coroutine seulement.
```

Option debug :

```csharp
public bool ThrowCoroutineExceptionsInDebug { get; set; }
```

---

## 6.2 Coroutine infinie

Une coroutine infinie est valide si elle contient des `yield`.

Exemple valide :

```csharp
IEnumerator Pulse()
{
    while (true)
    {
        ScaleUp();
        yield return new WaitForSeconds(0.5f);

        ScaleDown();
        yield return new WaitForSeconds(0.5f);
    }
}
```

Exemple dangereux :

```csharp
IEnumerator Bad()
{
    while (true)
    {
    }
}
```

Le moteur ne peut pas empêcher directement ce cas, car le blocage arrive dans `MoveNext()`.

Recommandation :

- documenter clairement ce piège ;
- ajouter des tests ;
- éviter les coroutines utilisateur sans `yield` dans les boucles ;
- éventuellement ajouter un mode debug qui mesure le temps passé dans `MoveNext()`.

---

# 7. Debug et outils développeur

## 7.1 Liste des coroutines actives

Le moteur doit pouvoir exposer une liste de debug :

```csharp
IReadOnlyList<CoroutineDebugInfo> GetActiveCoroutines();
```

Structure proposée :

```csharp
public sealed class CoroutineDebugInfo
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? OwnerName { get; init; }
    public string? CurrentInstruction { get; init; }
    public bool IsPaused { get; init; }
    public float? RemainingTime { get; init; }
}
```

---

## 7.2 Nommer une coroutine

Option utile :

```csharp
StartCoroutine(MyRoutine(), owner, name: "IntroSequence");
```

ou :

```csharp
CoroutineHandle handle = StartCoroutine(MyRoutine());
SetCoroutineName(handle, "IntroSequence");
```

Cela aide énormément dans un éditeur ou un profiler.

---

## 7.3 Affichage dans l’éditeur

Dans l’éditeur CasaEngine, prévoir une fenêtre :

```text
Debug > Coroutines
```

Informations à afficher :

| Colonne | Description |
|---|---|
| Id | identifiant interne |
| Name | nom de la coroutine |
| Owner | objet ou composant propriétaire |
| Current Yield | instruction en attente |
| Remaining Time | temps restant si connu |
| State | Running / Waiting / Stopped / Completed |
| Started At | frame ou temps de démarrage |

Actions utiles :

- arrêter une coroutine ;
- arrêter toutes les coroutines d’un objet ;
- filtrer par scène ;
- filtrer par propriétaire ;
- afficher les coroutines globales ;
- afficher les coroutines bloquées depuis trop longtemps.

---

# 8. Sérialisation

## 8.1 Recommandation V1

Ne pas sérialiser l’état interne brut des coroutines.

Raison :

- un `IEnumerator` C# contient un état compilé difficile à sérialiser proprement ;
- les closures peuvent capturer des références non sérialisables ;
- les versions de code changent ;
- la reprise après sauvegarde devient fragile.

Pour la V1 :

```text
Les coroutines runtime ne sont pas sauvegardées.
```

---

## 8.2 Alternative recommandée

Pour les séquences qui doivent être sauvegardées, utiliser un système de script explicite.

Exemple :

```json
{
  "actions": [
    { "type": "MoveEntity", "entity": "npc_01", "target": [10, 0, 4], "duration": 1.5 },
    { "type": "Wait", "duration": 0.5 },
    { "type": "Dialogue", "textId": "intro_hello" }
  ],
  "currentActionIndex": 1,
  "elapsedInCurrentAction": 0.3
}
```

La coroutine peut servir à exécuter ce script, mais l’état sauvegardable doit appartenir au système de script, pas à l’`IEnumerator`.

---

# 9. Coroutines vs système de scripts

Les coroutines sont excellentes pour :

- comportements temporaires ;
- scripts courts codés en C# ;
- effets visuels ;
- transitions ;
- enchaînements simples ;
- logique de gameplay locale à un composant.

Elles sont moins adaptées pour :

- scripts de niveau sauvegardables ;
- cinématiques éditables dans un outil ;
- logique complexe de quête ;
- dialogues branchés ;
- timelines avec beaucoup de données ;
- comportements modifiables par des designers.

Pour CasaEngine, il est conseillé d’avoir deux niveaux :

```text
CoroutineManager
  système bas niveau runtime

ScriptSequence / Timeline / CommandSystem
  système haut niveau éditable et sérialisable
```

Un système de séquence peut utiliser les coroutines en interne, mais il ne doit pas dépendre uniquement des `IEnumerator` pour représenter les données.

---

# 10. Extension vers un système de commandes séquentielles

À terme, les coroutines peuvent servir de base à un système plus haut niveau :

```csharp
public interface ISequenceCommand
{
    IEnumerator Execute(SequenceContext context);
}
```

Exemples de commandes :

```text
MoveEntityCommand
PlayAnimationCommand
WaitCommand
ShowDialogueCommand
SetCameraTargetCommand
FadeScreenCommand
EnableInputCommand
LoadSceneCommand
```

Exemple :

```csharp
public sealed class WaitCommand : ISequenceCommand
{
    public float Duration { get; set; }

    public IEnumerator Execute(SequenceContext context)
    {
        yield return new WaitForSeconds(Duration);
    }
}
```

Cela permettrait à CasaEngine d’avoir :

- un runtime simple ;
- une représentation éditable ;
- une sérialisation propre ;
- un affichage des actions dans l’éditeur ;
- une base pour les cinématiques et événements scriptés.

---

# 11. Performance

## 11.1 Points à surveiller

Les coroutines peuvent générer des allocations :

- création de l’`IEnumerator` ;
- création des instructions `new WaitForSeconds(...)` ;
- closures dans `WaitUntil(() => ...)` ;
- coroutines imbriquées ;
- lambdas capturant des variables.

Pour la V1, ce n’est pas critique si les coroutines servent aux séquences, transitions et comportements ponctuels.

Éviter cependant :

```csharp
void Update()
{
    StartCoroutine(MyRoutine());
}
```

Cela crée une nouvelle coroutine à chaque frame.

---

## 11.2 Optimisations possibles

Pour une V2/V3 :

- pooler certaines instructions ;
- éviter les lambdas dans les chemins critiques ;
- utiliser des structs pour certaines instructions ;
- créer des instructions réutilisables ;
- ajouter des compteurs de coroutines actives ;
- profiler les allocations ;
- limiter les coroutines par propriétaire si nécessaire.

---

# 12. Règles recommandées pour CasaEngine

## 12.1 DO

- Utiliser les coroutines pour des séquences lisibles.
- Toujours mettre un `yield` dans les boucles longues.
- Associer les coroutines à un propriétaire quand elles dépendent d’un objet.
- Arrêter automatiquement les coroutines des objets détruits.
- Logger les exceptions avec le propriétaire et le nom de la coroutine.
- Prévoir une vue debug dans l’éditeur.
- Garder la V1 simple.

---

## 12.2 DON'T

- Ne pas utiliser les coroutines comme système de threads.
- Ne pas lancer une coroutine chaque frame sans contrôle.
- Ne pas sérialiser directement les `IEnumerator`.
- Ne pas mettre de calcul lourd bloquant dans une coroutine.
- Ne pas mélanger logique critique sauvegardable et coroutine brute.
- Ne pas dépendre de `WaitForSeconds` pour du timing parfaitement exact.

---

# 13. Plan d’implémentation recommandé

## Étape 1 — Base runtime

- créer `CoroutineHandle` ;
- créer `CoroutineManager` ;
- supporter `StartCoroutine` ;
- supporter `StopCoroutine` ;
- supporter `yield return null` ;
- gérer la fin naturelle d’une coroutine.

---

## Étape 2 — Instructions V1

- ajouter `ICoroutineInstruction` ;
- ajouter `WaitForSeconds` ;
- ajouter `WaitForSecondsRealtime` ;
- ajouter `WaitForFrames` ;
- ajouter `WaitUntil` ;
- ajouter `WaitWhile`.

---

## Étape 3 — Coroutines imbriquées

- supporter `yield return IEnumerator` ;
- ajouter une pile d’`IEnumerator` par coroutine ;
- reprendre la coroutine parente à la fin de l’enfant.

---

## Étape 4 — Ownership

- démarrer une coroutine avec un propriétaire ;
- arrêter toutes les coroutines d’un propriétaire ;
- arrêter les coroutines à la destruction d’un objet ;
- exposer des helpers dans `Component`.

---

## Étape 5 — Debug

- créer `CoroutineDebugInfo` ;
- exposer la liste des coroutines actives ;
- logger les erreurs ;
- ajouter des noms de coroutines ;
- préparer l’affichage éditeur.

---

## Étape 6 — Tests

Tests à prévoir :

- une coroutine se termine correctement ;
- `yield return null` attend une frame ;
- `WaitForSeconds` respecte le temps de jeu ;
- `WaitForSecondsRealtime` ignore le `TimeScale` ;
- `WaitForFrames` attend le bon nombre de frames ;
- `WaitUntil` reprend quand la condition devient vraie ;
- `WaitWhile` reprend quand la condition devient fausse ;
- une coroutine imbriquée bloque son parent ;
- `StopCoroutine` arrête une coroutine ;
- `StopAllCoroutines(owner)` arrête uniquement les coroutines du propriétaire ;
- une exception dans une coroutine est loggée et ne casse pas le scheduler.

---

# 14. Architecture minimale recommandée

```text
CoroutineManager
 ├─ List<CoroutineInstance> _activeCoroutines
 ├─ Queue<CoroutineInstance> _pendingStart
 ├─ Queue<CoroutineHandle> _pendingStop
 ├─ StartCoroutine(...)
 ├─ StopCoroutine(...)
 ├─ StopAllCoroutines(...)
 └─ Update(...)

CoroutineInstance
 ├─ Handle
 ├─ Owner
 ├─ Stack<IEnumerator>
 ├─ CurrentInstruction
 ├─ State
 └─ DebugName

ICoroutineInstruction
 └─ IsCompleted(context)

WaitForSeconds
WaitForSecondsRealtime
WaitForFrames
WaitUntil
WaitWhile
```

---

# 15. Pseudo-code du scheduler

```csharp
public void Update(CoroutineUpdateContext context)
{
    AddPendingCoroutines();

    foreach (CoroutineInstance coroutine in _activeCoroutines)
    {
        if (coroutine.IsStopped)
            continue;

        try
        {
            UpdateCoroutine(coroutine, context);
        }
        catch (Exception ex)
        {
            LogCoroutineException(coroutine, ex);
            coroutine.IsStopped = true;
        }
    }

    RemoveCompletedAndStoppedCoroutines();
}
```

```csharp
private void UpdateCoroutine(CoroutineInstance coroutine, CoroutineUpdateContext context)
{
    if (coroutine.CurrentInstruction is ICoroutineInstruction instruction)
    {
        if (!instruction.IsCompleted(context))
            return;

        coroutine.CurrentInstruction = null;
    }

    while (coroutine.Stack.Count > 0)
    {
        IEnumerator current = coroutine.Stack.Peek();

        if (!current.MoveNext())
        {
            coroutine.Stack.Pop();
            continue;
        }

        object? yielded = current.Current;

        if (yielded == null)
        {
            coroutine.CurrentInstruction = new WaitForFrames(1);
            return;
        }

        if (yielded is IEnumerator nestedEnumerator)
        {
            coroutine.Stack.Push(nestedEnumerator);
            continue;
        }

        if (yielded is ICoroutineInstruction coroutineInstruction)
        {
            coroutine.CurrentInstruction = coroutineInstruction;
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported coroutine yield type: {yielded.GetType().FullName}");
    }

    coroutine.IsCompleted = true;
}
```

---

# 16. Décisions importantes à valider

Avant l’implémentation définitive, décider :

1. Est-ce que les coroutines sont globales au moteur ou attachées à une scène ?
2. Est-ce que chaque `Scene` possède son propre `CoroutineManager` ?
3. Les coroutines continuent-elles quand le jeu est en pause ?
4. Les coroutines liées à l’UI utilisent-elles le temps réel par défaut ?
5. Les coroutines sont-elles arrêtées quand un composant est désactivé ?
6. Faut-il une phase `LateUpdate` dès la V1 ?
7. Faut-il un affichage éditeur dès la V1 ou seulement une API debug ?
8. Faut-il supporter `async/await` plus tard ?
9. Faut-il permettre d’attendre un événement moteur ?
10. Faut-il intégrer les coroutines au futur système de séquences scriptées ?

---

# 17. Recommandation finale

Pour CasaEngine, la meilleure approche est de commencer par un système simple :

```text
IEnumerator + CoroutineManager + quelques instructions d’attente
```

Puis de construire au-dessus un système plus haut niveau :

```text
ScriptSequence / Timeline / CommandSystem
```

Les coroutines doivent être vues comme un outil runtime pour écrire facilement des comportements séquentiels, pas comme le format de données principal des cinématiques ou des scripts sauvegardables.

La V1 doit donc viser :

- simplicité ;
- robustesse ;
- debug facile ;
- intégration propre au cycle de vie des objets ;
- aucune sérialisation directe des `IEnumerator` ;
- possibilité d’extension vers des séquences éditables.


# Décisions V1 validées

## Scope

Le système de coroutines V1 est attaché au `World`.

Chaque `World` possède son propre `CoroutineManager`.

Les coroutines gameplay sont détruites avec leur `World`.

Les coroutines globales, UI ou editor ne sont pas incluses dans la V1, mais pourront utiliser un manager séparé plus tard.

## Temps

Le moteur ne transmet plus seulement un `float elapsedTime`.

CasaEngine introduit un `FrameTime` contenant :

- `DeltaTime`
- `UnscaledDeltaTime`
- `TotalTime`
- `UnscaledTotalTime`
- `TimeScale`
- `FrameIndex`

`WaitForSeconds` utilise `DeltaTime`.

`WaitForSecondsRealtime` utilise `UnscaledDeltaTime`.

## Pause

La pause est représentée par :

`TimeScale = 0`

Dans ce cas :

- `WaitForSeconds` ne progresse pas ;
- `WaitForSecondsRealtime` continue ;
- `WaitForFrames` continue tant que `World.Update` continue d’être appelé.

## Ownership

Une coroutine peut avoir un owner.

L’owner peut être :

- une `Entity`
- un `Component`
- un objet système

Les coroutines d’une entité sont stoppées dès `Entity.Destroy()`.

Les coroutines sont aussi stoppées par sécurité lors du retrait effectif du `World`.

Les coroutines d’un composant sont stoppées lors du `Detach()` du composant.

Un simple `Enabled = false` ne stoppe pas automatiquement les coroutines en V1.

## CoroutineHandle

Un `CoroutineHandle` contient :

- `ManagerId`
- `Slot`
- `Generation`

Un handle obsolète ne doit jamais pointer vers une nouvelle coroutine.

## yield return CoroutineHandle

`yield return CoroutineHandle` attend la fin de la coroutine ciblée.

Seuls les handles du même `CoroutineManager` sont supportés en V1.

Un handle terminé ou stoppé reprend immédiatement.

Un handle invalide produit un warning ou une exception selon le mode debug.

Un handle pointant vers la coroutine courante est une erreur.

## Exceptions

Par défaut, une exception dans une coroutine :

- est loggée ;
- stoppe la coroutine fautive ;
- ne stoppe pas le scheduler complet.

En mode debug strict, le scheduler peut relancer l’exception après avoir marqué la coroutine comme fautive.

## Phases

La V1 n’implémente qu’une phase :

`Update`

`LateUpdate`, `FixedUpdate` et `EndOfFrame` sont réservés pour une V2.

## Debug

La V1 expose une API de debug, mais pas forcément une fenêtre éditeur complète.