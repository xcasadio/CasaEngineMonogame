# Base générique réutilisable pour les timelines CasaEngine

> ## Note de revue — 2026-06-13
>
> Ce document est une **vision de conception**. Il a été confronté au code réel du dépôt.
> Les corrections ci-dessous **font autorité** sur le reste du document quand il y a conflit.
> Le plan d'exécution dérivé se trouve dans
> [`ai-agent/tasks/archive/timeline-generic-agent-plan.md`](../../ai-agent/tasks/archive/timeline-generic-agent-plan.md).
>
> ### Décisions verrouillées
>
> 1. **Périmètre** : on construit une **base timeline générique** (`Track` / `Item` /
>    `Duration` / `Kind`), validée **uniquement** par l'éditeur Animation2D existant.
> 2. **Cutscene** : le modèle cutscene réel est **un arbre d'actions** `Sequence` /
>    `Parallel` ([`CutsceneAsset.RootAction`](../../CasaEngine/Framework/Cutscenes/CutsceneAsset.cs)),
>    joué par [`CutsceneDirector`](../../CasaEngine/Framework/Cutscenes/CutsceneDirector.cs).
>    Il **n'a pas** de `StartTime`/`Duration` absolus, **pas** de track par acteur,
>    et le director n'a **pas** de `Seek`/`Pause`/`Update(dt)`. L'éditeur cutscene reste
>    donc un **contrôle d'arbre**, pas une timeline plate. La timeline pourra plus tard
>    afficher une **projection de preview** (lecture seule) calculée depuis l'arbre, sans
>    jamais en devenir la source de vérité. → **Il n'y a pas de `CutsceneTimelineAdapter`**
>    qui modifierait un modèle cutscene plat. Toutes les sections « cutscene » ci-dessous
>    (5, 8, 9 partiel, 12, 16-étape 9, 18) sont **hors périmètre / aspirationnelles**.
> 3. **Visibilité** : le modèle générique passe de `internal sealed` à **`public sealed`**,
>    au **même emplacement** `CasaEngine.Editor.Controls.Timeline`.
> 4. **Renommage** : `Lane`/`Event` → `Track`/`Item` est propagé **jusqu'à l'API
>    Animation2D** (`Animation2dTimelineControl`, ses records de données, ses événements
>    publics et `Animation2dAssetInspectorPanel`).
> 5. **Approche** : **phasée**. Cœur d'abord (renommage, puis `Duration`/`Kind`), puis
>    les abstractions (adapter, policy, renderer, menu provider, playback).
>
> ### Réalité du code à connaître (ne rien supposer)
>
> - Les types réels sont `internal sealed` : `TimelineModel { DurationSeconds, Lanes,
>   Events }`, `TimelineLane { Id, Label, IsEditable }`, `TimelineEvent { Id, LaneId,
>   TimeSeconds, EventType, ValueText, ToolTipText, IsEditable }`. La description de
>   l'existant dans le document est correcte.
> - Le type d'animation réel est **`Animation2dData`** (pas `AnimationAsset`). **Aucun
>   asset audio n'existe** : la « Audio timeline » est purement hypothétique.
> - L'intégration Animation2D passe **déjà** par un sous-classement : `Animation2dTimelineControl`
>   *override* `CreateContextMenu` / `CreateTrackHeaderContextMenu`. Le passage à un
>   `ITimelineContextMenuProvider` (composition) est une **migration**, pas un ajout neutre.
> - Sont **déjà implémentés** et ne doivent pas régresser : rename inline de lane, copy /
>   paste / duplicate, drag d'event, zoom ancré souris, scroll horizontal, mapping
>   `Guid ↔ index` de l'adapter Animation2D, tests `TimelineViewTransformTests`.
> - Incohérences internes du document corrigées par le plan : `TimelineEditOperation`
>   (cité en section 13 mais jamais défini) est **abandonné** ; les « événements
>   recommandés » de la section 10 **renomment** les événements existants au lieu d'en
>   ajouter.

## Contexte

Le moteur possède déjà un contrôle de timeline utilisé pour les animations :

```text
CasaEngine.Editor/Controls/Timeline/TimelineControl.cs
```

Ce contrôle est une bonne base, car il est déjà découpé en plusieurs parties visuelles :

```text
TimelineControl
├── Corner header
├── Time ruler
├── Track/Lane header panel
├── Timeline viewport
└── Horizontal scrollbar
```

Le problème principal n'est donc pas la structure visuelle du contrôle, mais plutôt le modèle de données et les hypothèses métier actuelles.

Aujourd'hui, la timeline est surtout pensée pour des événements ponctuels d'animation :

```text
TimelineModel
├── DurationSeconds
├── Lanes
└── Events
```

Avec un événement qui ressemble à :

```csharp
TimelineEvent
{
    TimeSeconds
}
```

Ce modèle fonctionne pour des événements d'animation ponctuels, mais il devient limité pour d'autres usages comme :

- cutscenes ;
- commandes avec durée ;
- clips audio ;
- tracks de caméra ;
- séquences de gameplay ;
- keyframes ;
- markers ;
- actions synchronisées.

L'objectif est donc de transformer la timeline existante en une base générique capable de gérer plusieurs types de timelines sans dupliquer les contrôles.

---

# Objectif de la timeline générique

Le même contrôle doit pouvoir servir à plusieurs contextes.

## Animation timeline

Exemples d'éléments :

- événements ponctuels ;
- keyframes ;
- frames de sprite ;
- changements de partie de sprite ;
- déclenchement de son ;
- hitbox active/inactive ;
- markers d'animation.

## Cutscene timeline

Exemples d'éléments :

- commandes avec durée ;
- commandes instantanées ;
- navigation d'entité ;
- attente ;
- orientation vers une cible ;
- lancement d'animation ;
- commande de caméra ;
- commande de dialogue.

## Audio timeline

Exemples d'éléments :

- clips audio ;
- fade in / fade out ;
- markers ;
- événements sonores ponctuels.

## Gameplay timeline

Exemples d'éléments :

- triggers ;
- actions gameplay ;
- activation/désactivation d'entités ;
- conditions ;
- scripts.

La base générique doit donc gérer deux concepts fondamentaux :

```text
Track = ligne horizontale de la timeline
Item  = élément placé sur une track à un moment donné
```

Elle ne doit pas être pensée uniquement autour de :

```text
Lane  = ligne d'animation
Event = événement ponctuel
```

---

# 1. Renommer les concepts

Les noms actuels sont trop spécialisés :

```csharp
TimelineLane
TimelineEvent
```

Ils devraient être remplacés par des concepts plus génériques :

```csharp
TimelineTrack
TimelineItem
```

## Pourquoi `Track` ?

Le mot `Track` est utilisé dans beaucoup d'éditeurs modernes :

- timeline d'animation ;
- timeline audio ;
- sequencer ;
- éditeur vidéo ;
- cutscene editor.

Une track peut représenter :

- une entité ;
- une partie de sprite ;
- une propriété ;
- une piste audio ;
- une caméra ;
- une piste d'événements ;
- une piste de markers.

## Pourquoi `Item` ?

Le mot `Event` est trop restrictif.

Une cutscene ne contient pas seulement des événements. Elle contient des commandes, par exemple :

```text
NavigateTo
Wait
LookAt
PlayAnimation
```

Une timeline audio contient des clips.

Une timeline d'animation contient des keyframes ou des événements.

Le mot `Item` permet de couvrir tous ces cas.

---

# 2. Nouveau modèle générique

## `TimelineModel`

```csharp
public sealed class TimelineModel
{
    public float Duration { get; set; }
    public TimelineTimeUnit TimeUnit { get; set; } = TimelineTimeUnit.Seconds;

    public List<TimelineTrack> Tracks { get; } = new();
    public List<TimelineItem> Items { get; } = new();
}
```

Le modèle ne doit pas savoir s'il représente une animation, une cutscene ou de l'audio.

Il décrit seulement une structure temporelle générique.

---

## `TimelineTimeUnit`

```csharp
public enum TimelineTimeUnit
{
    Seconds,
    Frames
}
```

Même si le stockage interne peut rester en secondes, cette information permet à l'interface de savoir comment afficher la règle temporelle.

Exemples :

```text
Cutscene  => secondes
Animation => frames ou secondes
Audio     => secondes
```

---

## `TimelineTrack`

```csharp
public sealed class TimelineTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;

    public string TrackType { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }

    public object? Source { get; set; }
}
```

### Rôle des propriétés

| Propriété | Rôle |
|---|---|
| `Id` | Identifiant stable de la track |
| `Label` | Nom affiché dans l'éditeur |
| `TrackType` | Type logique de track |
| `IsEditable` | La track peut être modifiée |
| `IsVisible` | La track est affichée |
| `IsLocked` | La track est verrouillée |
| `Source` | Objet métier associé |

Exemples de `TrackType` :

```text
Actor
SpritePart
AnimationEvent
Audio
Camera
Marker
Property
```

Exemple pour une cutscene :

```csharp
new TimelineTrack
{
    Label = "Guard01",
    TrackType = "Actor",
    Source = actorBinding
};
```

Exemple pour une animation 2D multi-part :

```csharp
new TimelineTrack
{
    Label = "Head",
    TrackType = "SpritePart",
    Source = spritePart
};
```

---

## `TimelineItem`

```csharp
public sealed class TimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrackId { get; set; }

    public float StartTime { get; set; }
    public float Duration { get; set; }

    public TimelineItemKind Kind { get; set; }

    public string ItemType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public string ToolTipText { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanResizeStart { get; set; }
    public bool CanResizeEnd { get; set; }

    public object? Source { get; set; }
}
```

### Rôle des propriétés

| Propriété | Rôle |
|---|---|
| `Id` | Identifiant stable de l'item |
| `TrackId` | Track propriétaire |
| `StartTime` | Temps de début |
| `Duration` | Durée |
| `Kind` | Type visuel et temporel |
| `ItemType` | Type métier simplifié |
| `DisplayName` | Texte affiché dans la timeline |
| `ValueText` | Texte secondaire éventuel |
| `ToolTipText` | Tooltip |
| `IsEditable` | Peut être édité |
| `CanMove` | Peut être déplacé |
| `CanResizeStart` | Peut être redimensionné à gauche |
| `CanResizeEnd` | Peut être redimensionné à droite |
| `Source` | Objet métier associé |

---

## `TimelineItemKind`

```csharp
public enum TimelineItemKind
{
    Instant,
    Duration,
    Range,
    Marker
}
```

### `Instant`

Un item ponctuel sans durée.

Exemples :

```text
Animation event
Footstep event
Hit event
Trigger
Keyframe ponctuelle
```

Affichage possible :

```text
Track |      ◆        ◆       ◆
```

---

### `Duration`

Un item avec une durée.

Exemples :

```text
NavigateTo
Wait
PlayAnimation
AudioClip
CameraMove
```

Affichage possible :

```text
Track | [ MoveTo Door ][ Wait ][ LookAt Player ]
```

---

### `Range`

Un intervalle temporel spécial.

Exemples :

```text
Loop range
Selection range
Active hitbox range
Blend range
```

---

### `Marker`

Un marqueur vertical ou un repère temporel.

Exemples :

```text
StartCombat
OpenDoor
CameraCut
```

---

# 3. Conversion depuis l'ancien modèle

Un ancien `TimelineEvent` devient un `TimelineItem` ponctuel.

Ancien modèle :

```csharp
TimelineEvent oldEvent;
```

Nouveau modèle :

```csharp
TimelineItem item = new()
{
    Id = oldEvent.Id,
    TrackId = oldEvent.LaneId,
    StartTime = oldEvent.TimeSeconds,
    Duration = 0f,
    Kind = TimelineItemKind.Instant,
    DisplayName = oldEvent.Name,
    Source = oldEvent
};
```

Correspondance :

```text
TimelineLane  -> TimelineTrack
TimelineEvent -> TimelineItem
TimeSeconds   -> StartTime
```

Pour garder le comportement actuel des animations, tous les événements existants doivent être convertis avec :

```csharp
Kind = TimelineItemKind.Instant;
Duration = 0f;
```

---

# 4. Ne pas stocker les objets métier directement dans le contrôle

Le contrôle de timeline générique ne doit pas connaître les types métier suivants :

```csharp
AnimationEvent
AnimationKeyFrame
CutsceneCommand
MoveEntityToCommand
SpritePartTrack
AudioClip
```

Il doit manipuler uniquement :

```csharp
TimelineModel
TimelineTrack
TimelineItem
```

Par contre, chaque `TimelineTrack` ou `TimelineItem` peut référencer l'objet métier d'origine grâce à :

```csharp
object? Source
```

Cela permet au contrôle d'être générique tout en conservant un lien vers l'asset réel.

---

## Exemple pour une cutscene

Objet métier :

```csharp
MoveEntityToCommand command;
```

Item timeline :

```csharp
TimelineItem item = new()
{
    Id = command.Id,
    TrackId = actorTrack.Id,
    StartTime = command.StartTime,
    Duration = command.Duration,
    Kind = command.Duration > 0f
        ? TimelineItemKind.Duration
        : TimelineItemKind.Instant,
    ItemType = command.GetType().Name,
    DisplayName = "Move To",
    ToolTipText = "Move entity to target position",
    Source = command
};
```

---

## Exemple pour une animation

Objet métier :

```csharp
AnimationEvent animationEvent;
```

Item timeline :

```csharp
TimelineItem item = new()
{
    Id = animationEvent.Id,
    TrackId = eventTrack.Id,
    StartTime = animationEvent.TimeSeconds,
    Duration = 0f,
    Kind = TimelineItemKind.Instant,
    ItemType = animationEvent.EventType,
    DisplayName = animationEvent.EventType,
    Source = animationEvent
};
```

---

# 5. Ajouter un adapter par domaine

Le `TimelineControl` ne doit pas modifier directement un asset d'animation ou de cutscene.

Il doit seulement remonter des intentions d'édition :

```text
Item déplacé
Item supprimé
Item dupliqué
Item redimensionné
Item ajouté
Track renommée
Temps courant modifié
Sélection modifiée
```

Ensuite, un adapter transforme ces intentions en modifications métier.

---

## Interface `ITimelineAdapter`

```csharp
public interface ITimelineAdapter
{
    TimelineModel BuildModel();

    void MoveItem(Guid itemId, Guid targetTrackId, float newStartTime);
    void ResizeItem(Guid itemId, float newStartTime, float newDuration);
    void DeleteItem(Guid itemId);
    void DuplicateItem(Guid itemId, Guid targetTrackId, float newStartTime);
    void InsertItem(Guid trackId, float time);
    void RenameTrack(Guid trackId, string newName);

    void OnSelectionChanged(Guid? itemId, Guid? trackId);
    void OnCurrentTimeChanged(float time);
}
```

---

## Adapter pour animation

```csharp
public sealed class AnimationTimelineAdapter : ITimelineAdapter
{
    private readonly AnimationAsset _animation;

    public AnimationTimelineAdapter(AnimationAsset animation)
    {
        _animation = animation;
    }

    public TimelineModel BuildModel()
    {
        TimelineModel model = new();

        // Convertir AnimationAsset -> TimelineModel
        // Tracks = pistes d'animation, parties de sprite, events, etc.
        // Items = events, keyframes, clips, etc.

        return model;
    }

    public void MoveItem(Guid itemId, Guid targetTrackId, float newStartTime)
    {
        // Modifier l'event ou la keyframe dans l'asset animation.
    }

    public void ResizeItem(Guid itemId, float newStartTime, float newDuration)
    {
        // Selon le type d'item, modifier sa durée ou refuser.
    }

    public void DeleteItem(Guid itemId)
    {
        // Supprimer l'élément métier correspondant.
    }

    public void DuplicateItem(Guid itemId, Guid targetTrackId, float newStartTime)
    {
        // Dupliquer l'élément métier correspondant.
    }

    public void InsertItem(Guid trackId, float time)
    {
        // Créer un nouvel event ou une nouvelle keyframe.
    }

    public void RenameTrack(Guid trackId, string newName)
    {
        // Renommer la piste si l'asset le permet.
    }

    public void OnSelectionChanged(Guid? itemId, Guid? trackId)
    {
        // Synchroniser avec l'inspector.
    }

    public void OnCurrentTimeChanged(float time)
    {
        // Mettre à jour la preview animation.
    }
}
```

---

## Adapter pour cutscene

```csharp
public sealed class CutsceneTimelineAdapter : ITimelineAdapter
{
    private readonly CutsceneAsset _cutscene;

    public CutsceneTimelineAdapter(CutsceneAsset cutscene)
    {
        _cutscene = cutscene;
    }

    public TimelineModel BuildModel()
    {
        TimelineModel model = new();

        // Convertir CutsceneAsset -> TimelineModel
        // Track = acteur, caméra, dialogue, etc.
        // Item = commande de cutscene.

        return model;
    }

    public void MoveItem(Guid itemId, Guid targetTrackId, float newStartTime)
    {
        // Modifier StartTime de la CutsceneCommand.
    }

    public void ResizeItem(Guid itemId, float newStartTime, float newDuration)
    {
        // Modifier StartTime et Duration de la commande.
    }

    public void DeleteItem(Guid itemId)
    {
        // Supprimer la commande de la cutscene.
    }

    public void DuplicateItem(Guid itemId, Guid targetTrackId, float newStartTime)
    {
        // Dupliquer la commande.
    }

    public void InsertItem(Guid trackId, float time)
    {
        // Ouvrir une palette de commandes ou créer une commande par défaut.
    }

    public void RenameTrack(Guid trackId, string newName)
    {
        // Renommer l'acteur ou la piste si nécessaire.
    }

    public void OnSelectionChanged(Guid? itemId, Guid? trackId)
    {
        // Synchroniser avec l'inspector de cutscene.
    }

    public void OnCurrentTimeChanged(float time)
    {
        // Scrub de la cutscene / preview.
    }
}
```

---

# 6. Ajouter la notion de durée

C'est le changement le plus important.

Aujourd'hui, la timeline d'animation dessine surtout des événements ponctuels.

Pour une cutscene, il faut gérer des blocs avec durée :

```text
Guard | [ NavigateTo Door ][ Wait ][ NavigateTo Exit ]
```

Le rendu doit donc gérer deux modes principaux.

---

## Item instantané

Exemple :

```text
Animation Events |      ◆        ◆       ◆
```

Un item instantané :

```csharp
new TimelineItem
{
    Kind = TimelineItemKind.Instant,
    StartTime = 1.25f,
    Duration = 0f
};
```

---

## Item avec durée

Exemple :

```text
Guard | [ MoveTo Door      ][ Wait ][ LookAt Player ]
```

Un item avec durée :

```csharp
new TimelineItem
{
    Kind = TimelineItemKind.Duration,
    StartTime = 0.5f,
    Duration = 1.75f
};
```

---

## Modification dans le viewport

Au lieu d'avoir une méthode spécialisée du type :

```csharp
DrawEvents(...)
```

Il faut passer à :

```csharp
DrawItems(...)
```

Exemple :

```csharp
private void DrawItems(...)
{
    foreach (TimelineItem item in _owner.Model.Items)
    {
        if (item.Kind == TimelineItemKind.Instant)
        {
            DrawInstantItem(...);
        }
        else if (item.Kind == TimelineItemKind.Duration)
        {
            DrawDurationItem(...);
        }
        else if (item.Kind == TimelineItemKind.Marker)
        {
            DrawMarkerItem(...);
        }
        else if (item.Kind == TimelineItemKind.Range)
        {
            DrawRangeItem(...);
        }
    }
}
```

---

# 7. Extraire le rendu des items

Il ne faut pas mettre toute la logique de rendu directement dans `TimelineViewport`.

Sinon, à chaque nouveau type d'item, le viewport va grossir.

Il faut extraire le rendu dans une interface.

---

## Interface `ITimelineItemRenderer`

```csharp
public interface ITimelineItemRenderer
{
    void DrawItem(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        TimelineItemVisualState state);

    bool HitTest(
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        Point mousePosition);
}
```

---

## `TimelineRenderContext`

```csharp
public sealed class TimelineRenderContext
{
    public TimelineModel Model { get; init; }
    public TimelineViewState ViewState { get; init; }
    public TimelineViewTransform Transform { get; init; }

    public float TrackHeight { get; init; }
    public float HeaderWidth { get; init; }
    public float CurrentTime { get; init; }
}
```

---

## `TimelineItemVisualState`

```csharp
[Flags]
public enum TimelineItemVisualState
{
    None = 0,
    Selected = 1 << 0,
    Hovered = 1 << 1,
    Dragging = 1 << 2,
    Invalid = 1 << 3,
    Disabled = 1 << 4
}
```

---

## Renderer par défaut

```csharp
public sealed class DefaultTimelineItemRenderer : ITimelineItemRenderer
{
    public void DrawItem(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        TimelineItemVisualState state)
    {
        switch (item.Kind)
        {
            case TimelineItemKind.Instant:
                DrawDiamond(drawArgs, item, bounds, state);
                break;

            case TimelineItemKind.Duration:
                DrawBlock(drawArgs, item, bounds, state);
                break;

            case TimelineItemKind.Marker:
                DrawMarker(drawArgs, item, bounds, state);
                break;

            case TimelineItemKind.Range:
                DrawRange(drawArgs, item, bounds, state);
                break;
        }
    }

    public bool HitTest(
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        Point mousePosition)
    {
        return bounds.Contains(mousePosition.X, mousePosition.Y);
    }

    private void DrawDiamond(...)
    {
    }

    private void DrawBlock(...)
    {
    }

    private void DrawMarker(...)
    {
    }

    private void DrawRange(...)
    {
    }
}
```

---

## Renderers spécialisés possibles

Plus tard, on pourra ajouter :

```csharp
AnimationTimelineItemRenderer
CutsceneTimelineItemRenderer
AudioTimelineItemRenderer
```

Exemples :

- les animations peuvent afficher des losanges avec icônes ;
- les cutscenes peuvent afficher des blocs avec nom de commande ;
- l'audio peut afficher une forme d'onde ;
- les markers peuvent afficher une ligne verticale.

---

# 8. Ajouter une policy d'édition

Tous les types de timelines n'ont pas les mêmes règles.

## Exemples de règles différentes

| Timeline | Règle possible |
|---|---|
| Animation events | Plusieurs events peuvent exister au même temps |
| Sprite part animation | Une frame peut remplacer la précédente |
| Cutscene actor track | Deux commandes ne doivent pas se chevaucher |
| Audio track | Les clips peuvent ou non se superposer |
| Marker track | Les items n'ont pas de durée |
| Camera track | Certains blocs doivent être continus |

Il ne faut donc pas coder ces règles directement dans `TimelineControl`.

Il faut utiliser une policy.

---

## Interface `ITimelineEditPolicy`

```csharp
public interface ITimelineEditPolicy
{
    float SnapTime(float time, TimelineSnapContext context);

    bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime);
    bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration);
    bool CanInsertItem(TimelineTrack track, float time);

    TimelineValidationResult ValidateMove(
        TimelineModel model,
        TimelineItem item,
        TimelineTrack targetTrack,
        float newStartTime,
        float newDuration);
}
```

---

## `TimelineSnapContext`

```csharp
public sealed class TimelineSnapContext
{
    public TimelineModel Model { get; init; }
    public TimelineTrack? Track { get; init; }
    public TimelineItem? Item { get; init; }
    public TimelineSnapSettings SnapSettings { get; init; }
}
```

---

## `TimelineValidationResult`

```csharp
public sealed class TimelineValidationResult
{
    public bool IsValid { get; init; }
    public string? Message { get; init; }

    public static TimelineValidationResult Valid { get; } = new()
    {
        IsValid = true
    };

    public static TimelineValidationResult Error(string message)
    {
        return new TimelineValidationResult
        {
            IsValid = false,
            Message = message
        };
    }
}
```

---

## Policy pour animation

```csharp
public sealed class AnimationTimelineEditPolicy : ITimelineEditPolicy
{
    public float SnapTime(float time, TimelineSnapContext context)
    {
        // Snap sur les frames de l'animation.
        float frameRate = context.SnapSettings.FrameRate;
        float frame = MathF.Round(time * frameRate);
        return frame / frameRate;
    }

    public bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime)
    {
        return item.CanMove && !targetTrack.IsLocked;
    }

    public bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration)
    {
        return item.Kind == TimelineItemKind.Duration && item.CanResizeEnd;
    }

    public bool CanInsertItem(TimelineTrack track, float time)
    {
        return !track.IsLocked && track.IsEditable;
    }

    public TimelineValidationResult ValidateMove(
        TimelineModel model,
        TimelineItem item,
        TimelineTrack targetTrack,
        float newStartTime,
        float newDuration)
    {
        return TimelineValidationResult.Valid;
    }
}
```

---

## Policy pour cutscene

```csharp
public sealed class CutsceneTimelineEditPolicy : ITimelineEditPolicy
{
    public float SnapTime(float time, TimelineSnapContext context)
    {
        float step = context.SnapSettings.Step;
        return MathF.Round(time / step) * step;
    }

    public bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime)
    {
        return item.CanMove && !targetTrack.IsLocked;
    }

    public bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration)
    {
        return item.Kind == TimelineItemKind.Duration
            && !float.IsNaN(newDuration)
            && newDuration >= 0f;
    }

    public bool CanInsertItem(TimelineTrack track, float time)
    {
        return !track.IsLocked && track.IsEditable;
    }

    public TimelineValidationResult ValidateMove(
        TimelineModel model,
        TimelineItem item,
        TimelineTrack targetTrack,
        float newStartTime,
        float newDuration)
    {
        float newEndTime = newStartTime + newDuration;

        foreach (TimelineItem other in model.Items)
        {
            if (other.Id == item.Id)
            {
                continue;
            }

            if (other.TrackId != targetTrack.Id)
            {
                continue;
            }

            if (other.Kind != TimelineItemKind.Duration)
            {
                continue;
            }

            float otherStart = other.StartTime;
            float otherEnd = other.StartTime + other.Duration;

            bool overlaps = newStartTime < otherEnd && newEndTime > otherStart;

            if (overlaps)
            {
                return TimelineValidationResult.Error(
                    "Two commands cannot overlap on the same actor track.");
            }
        }

        return TimelineValidationResult.Valid;
    }
}
```

---

# 9. Ajouter le snap générique

Les animations et les cutscenes n'ont pas les mêmes besoins de snapping.

## Animation

Pour une animation, on veut généralement snapper aux frames :

```text
Frame 0
Frame 1
Frame 2
Frame 3
```

Exemple :

```csharp
SnapMode = TimelineSnapMode.Frame;
FrameRate = animation.FrameRate;
```

---

## Cutscene

Pour une cutscene, on veut souvent snapper à des pas temporels :

```text
0.1s
0.25s
0.5s
1.0s
```

Exemple :

```csharp
SnapMode = TimelineSnapMode.Step;
Step = 0.1f;
```

---

## `TimelineSnapSettings`

```csharp
public sealed class TimelineSnapSettings
{
    public bool IsEnabled { get; set; } = true;
    public TimelineSnapMode Mode { get; set; } = TimelineSnapMode.Step;
    public float Step { get; set; } = 0.1f;
    public float FrameRate { get; set; } = 60f;
}
```

---

## `TimelineSnapMode`

```csharp
public enum TimelineSnapMode
{
    None,
    Step,
    Frame,
    Markers,
    Items
}
```

---

# 10. Généraliser les événements publics

Le contrôle actuel expose des événements liés aux notions d'event et de lane.

Pour rendre la timeline générique, ces événements doivent utiliser les notions d'item et de track.

---

## Événements recommandés

```csharp
public event Action<TimelineItem?>? SelectedItemChanged;
public event Action<TimelineTrack?>? SelectedTrackChanged;
public event Action<float>? TimeScrubbed;

public event Action<TimelineItem, TimelineItemEdit>? ItemEditCommitted;
public event Action<TimelineTrack, float>? InsertRequested;
public event Action<TimelineItem, TimelineTrack, float>? DuplicateRequested;
public event Action<TimelineItem>? DeleteRequested;
public event Action<TimelineItem>? CopyRequested;
public event Action<TimelineTrack, float>? PasteRequested;
public event Action<TimelineTrack, string>? TrackRenameRequested;
```

---

## `TimelineItemEdit`

```csharp
public sealed class TimelineItemEdit
{
    public Guid ItemId { get; set; }
    public Guid TrackId { get; set; }

    public float StartTime { get; set; }
    public float Duration { get; set; }

    public TimelineEditKind EditKind { get; set; }
}
```

---

## `TimelineEditKind`

```csharp
public enum TimelineEditKind
{
    Move,
    ResizeStart,
    ResizeEnd,
    MoveToTrack,
    MoveAndResize
}
```

---

# 11. Séparer la timeline générique des menus métier

Le menu contextuel d'une cutscene n'est pas le même que celui d'une animation.

## Exemple pour animation

```text
Add Animation Event
Add Keyframe
Delete Event
Duplicate Event
```

## Exemple pour cutscene

```text
Add Command
├── Navigate To
├── Wait
├── Look At
└── Play Animation
```

Ces menus ne doivent pas être codés directement dans `TimelineControl`.

Il faut ajouter un provider.

---

## Interface `ITimelineContextMenuProvider`

```csharp
public interface ITimelineContextMenuProvider
{
    MGContextMenu? CreateContextMenu(
        TimelineControl timeline,
        TimelineTrack? track,
        TimelineItem? item,
        float cursorTime);
}
```

---

## Providers possibles

```csharp
AnimationTimelineContextMenuProvider
CutsceneTimelineContextMenuProvider
AudioTimelineContextMenuProvider
```

Le `TimelineControl` garde uniquement la mécanique :

```csharp
ContextMenuProvider?.CreateContextMenu(
    this,
    hoveredTrack,
    hoveredItem,
    cursorTime);
```

---

# 12. Ajouter un contrôleur de playback séparé

La timeline affiche un playhead et permet de scrubber le temps.

Mais elle ne doit pas savoir comment jouer une animation ou une cutscene.

Une animation lit des frames.

Une cutscene pilote des entités.

Un audio joue des sons.

Le playback doit donc être séparé.

---

## Interface `ITimelinePlaybackController`

```csharp
public interface ITimelinePlaybackController
{
    bool IsPlaying { get; }
    float CurrentTime { get; }

    void Play();
    void Pause();
    void Stop();
    void Seek(float time);
    void Update(float deltaTime);
}
```

---

## Contrôleur pour animation

```csharp
public sealed class AnimationTimelinePlaybackController : ITimelinePlaybackController
{
    public bool IsPlaying { get; private set; }
    public float CurrentTime { get; private set; }

    public void Play()
    {
        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Stop()
    {
        IsPlaying = false;
        Seek(0f);
    }

    public void Seek(float time)
    {
        CurrentTime = time;
        // Mettre à jour la preview animation.
    }

    public void Update(float deltaTime)
    {
        if (!IsPlaying)
        {
            return;
        }

        Seek(CurrentTime + deltaTime);
    }
}
```

---

## Contrôleur pour cutscene

```csharp
public sealed class CutsceneTimelinePlaybackController : ITimelinePlaybackController
{
    private readonly CutsceneRunner _runner;

    public bool IsPlaying { get; private set; }
    public float CurrentTime { get; private set; }

    public CutsceneTimelinePlaybackController(CutsceneRunner runner)
    {
        _runner = runner;
    }

    public void Play()
    {
        IsPlaying = true;
        _runner.Play();
    }

    public void Pause()
    {
        IsPlaying = false;
        _runner.Pause();
    }

    public void Stop()
    {
        IsPlaying = false;
        _runner.Stop();
        Seek(0f);
    }

    public void Seek(float time)
    {
        CurrentTime = time;
        _runner.Seek(time);
    }

    public void Update(float deltaTime)
    {
        if (!IsPlaying)
        {
            return;
        }

        CurrentTime += deltaTime;
        _runner.Update(deltaTime);
    }
}
```

---

# 13. Architecture cible

L'architecture recommandée pour la timeline générique est la suivante :

```text
CasaEngine.Editor.Controls.Timeline
│
├── TimelineControl
├── TimelineViewport
├── TimelineRuler
├── TimelineTrackHeaderPanel
├── TimelineHorizontalScrollBar
│
├── Model
│   ├── TimelineModel
│   ├── TimelineTrack
│   ├── TimelineItem
│   ├── TimelineItemKind
│   ├── TimelineTimeUnit
│   ├── TimelineViewState
│   └── TimelineViewTransform
│
├── Rendering
│   ├── ITimelineItemRenderer
│   ├── DefaultTimelineItemRenderer
│   ├── TimelineRenderContext
│   └── TimelineItemVisualState
│
├── Editing
│   ├── ITimelineEditPolicy
│   ├── TimelineEditOperation
│   ├── TimelineItemEdit
│   ├── TimelineEditKind
│   ├── TimelineSnapSettings
│   ├── TimelineSnapMode
│   └── TimelineValidationResult
│
├── Menu
│   ├── ITimelineContextMenuProvider
│   ├── AnimationTimelineContextMenuProvider
│   └── CutsceneTimelineContextMenuProvider
│
├── Playback
│   ├── ITimelinePlaybackController
│   ├── AnimationTimelinePlaybackController
│   └── CutsceneTimelinePlaybackController
│
└── Adapters
    ├── ITimelineAdapter
    ├── AnimationTimelineAdapter
    └── CutsceneTimelineAdapter
```

---

# 14. Où placer cette timeline ?

## Court terme

Je recommande de garder la timeline dans :

```text
CasaEngine.Editor.Controls.Timeline
```

Pourquoi ?

Parce qu'elle est encore fortement liée à l'éditeur CasaEngine :

- thème editor ;
- assets CasaEngine ;
- inspector ;
- sélection d'entité ;
- cutscene editor ;
- animation editor.

---

## Moyen terme

Quand le contrôle sera suffisamment stabilisé, il pourra être extrait vers :

```text
MGUI.Controls.Timeline
```

Mais uniquement si le contrôle ne dépend plus de types CasaEngine.

Pour être migrable vers MGUI, la timeline générique ne doit dépendre que de concepts UI génériques :

```text
Control
Panel
ScrollBar
DrawingContext
Mouse events
Keyboard events
Theme generic
```

Elle ne doit pas dépendre de :

```text
CutsceneAsset
AnimationAsset
World
Entity
EditorThemePalette spécifique CasaEngine
Inspector CasaEngine
```

---

# 15. Ce qu'il ne faut pas faire

## Ne pas créer une timeline générique avec trop de génériques C#

Éviter ceci :

```csharp
TimelineControl<TAsset, TTrack, TItem>
```

C'est tentant, mais pour un contrôle UI, cela devient vite lourd.

Il faudrait propager les génériques partout :

```text
TimelineControl<T>
TimelineViewport<T>
TimelineRuler<T>
TimelineHitTest<T>
TimelineDragState<T>
TimelineRenderer<T>
TimelineSelection<T>
```

Cela complexifie beaucoup le code pour peu de bénéfices.

---

## Préférer un modèle non générique

Préférer :

```csharp
TimelineControl
TimelineModel
TimelineTrack
TimelineItem
object? Source
ITimelineAdapter
```

Avantages :

- plus simple ;
- plus flexible ;
- plus adapté à un éditeur ;
- plus facile à brancher sur plusieurs assets ;
- moins de propagation de types génériques ;
- plus simple à sérialiser/debugger.

---

# 16. Migration conseillée

## Étape 1 — Renommer sans changer le comportement

Créer les nouveaux types :

```csharp
TimelineTrack
TimelineItem
```

Puis faire la correspondance :

```text
TimelineLane  -> TimelineTrack
TimelineEvent -> TimelineItem
TimeSeconds   -> StartTime
```

À cette étape, le contrôle doit encore fonctionner comme aujourd'hui.

Les événements d'animation restent ponctuels.

---

## Étape 2 — Ajouter `Duration`

Ajouter :

```csharp
public float Duration { get; set; }
public TimelineItemKind Kind { get; set; }
```

Pour les événements d'animation existants :

```csharp
Kind = TimelineItemKind.Instant;
Duration = 0f;
```

Aucun changement visuel obligatoire à cette étape.

---

## Étape 3 — Modifier le rendu

Dans `TimelineViewport`, remplacer :

```csharp
DrawEvents(...)
```

par :

```csharp
DrawItems(...)
```

Puis gérer :

```csharp
DrawInstantItem(...)
DrawDurationItem(...)
DrawMarkerItem(...)
DrawRangeItem(...)
```

Résultat :

```text
Animation event : ◆
Cutscene command: [ Command ]
```

---

## Étape 4 — Ajouter le hit-test générique

Créer une structure de résultat :

```csharp
public sealed class TimelineHitTestResult
{
    public TimelineTrack? Track { get; init; }
    public TimelineItem? Item { get; init; }
    public TimelineHitTestArea Area { get; init; }
    public float Time { get; init; }
}
```

Avec :

```csharp
public enum TimelineHitTestArea
{
    None,
    TrackHeader,
    TrackBody,
    ItemBody,
    ResizeStart,
    ResizeEnd,
    Ruler,
    Playhead
}
```

Pour les items instantanés :

```text
Area = ItemBody
```

Pour les items avec durée :

```text
Area = ItemBody
Area = ResizeStart
Area = ResizeEnd
```

---

## Étape 5 — Ajouter le resize

Pour les items avec durée :

```text
[ MoveTo Door         ]
^                    ^
resize start         resize end
```

Règles :

- resize gauche modifie `StartTime` et `Duration` ;
- resize droite modifie seulement `Duration` ;
- durée minimale à respecter ;
- snap appliqué pendant le resize ;
- validation via `ITimelineEditPolicy`.

---

## Étape 6 — Ajouter `ITimelineEditPolicy`

Brancher une policy au contrôle :

```csharp
public ITimelineEditPolicy? EditPolicy { get; set; }
```

Avant de valider un move ou resize :

```csharp
TimelineValidationResult result = EditPolicy.ValidateMove(...);

if (!result.IsValid)
{
    // Afficher feedback visuel ou refuser l'opération.
    return;
}
```

---

## Étape 7 — Ajouter `ITimelineAdapter`

Le contrôle ne modifie plus directement l'asset.

Il notifie l'adapter :

```csharp
_adapter.MoveItem(item.Id, targetTrack.Id, newStartTime);
_model = _adapter.BuildModel();
```

Ou bien via événements si tu veux garder le contrôle complètement passif.

---

## Étape 8 — Brancher l'adapter animation

Créer :

```csharp
AnimationTimelineAdapter
```

But : conserver le comportement existant de la timeline d'animation avec le nouveau modèle.

C'est l'étape qui garantit que la migration ne casse pas l'éditeur d'animation.

---

## Étape 9 — Brancher l'adapter cutscene

Créer :

```csharp
CutsceneTimelineAdapter
```

Mapping recommandé :

```text
Cutscene Actor   -> TimelineTrack
Cutscene Command -> TimelineItem
```

Exemple :

```csharp
TimelineTrack track = new()
{
    Id = actor.Id,
    Label = actor.Name,
    TrackType = "Actor",
    Source = actor
};
```

```csharp
TimelineItem item = new()
{
    Id = command.Id,
    TrackId = actor.Id,
    StartTime = command.StartTime,
    Duration = command.Duration,
    Kind = command.Duration > 0f
        ? TimelineItemKind.Duration
        : TimelineItemKind.Instant,
    ItemType = command.GetType().Name,
    DisplayName = command.DisplayName,
    Source = command
};
```

---

# 17. Minimum nécessaire pour la V1 cutscene

Pour pouvoir réutiliser la timeline actuelle pour les cutscenes, le minimum est :

```text
1. TimelineItem.Duration
2. TimelineItemKind.Instant / Duration
3. DrawDurationItem
4. Hit-test des blocs
5. MoveItem
6. ResizeItem
7. CutsceneTimelineAdapter
8. CutsceneTimelineEditPolicy
```

Avec ça, tu peux afficher et éditer :

```text
Guard01 | [ NavigateTo Door ][ Wait ][ NavigateTo Exit ]
```

Tout en gardant les animations sous forme :

```text
Events  |     ◆        ◆       ◆
```

---

# 18. Exemple d'utilisation finale

## Animation editor

```csharp
AnimationTimelineAdapter adapter = new(animationAsset);

TimelineControl timeline = new();
timeline.SetModel(adapter.BuildModel());
timeline.EditPolicy = new AnimationTimelineEditPolicy();
timeline.ContextMenuProvider = new AnimationTimelineContextMenuProvider(adapter);
timeline.PlaybackController = new AnimationTimelinePlaybackController(animationAsset);
```

---

## Cutscene editor

```csharp
CutsceneTimelineAdapter adapter = new(cutsceneAsset);

TimelineControl timeline = new();
timeline.SetModel(adapter.BuildModel());
timeline.EditPolicy = new CutsceneTimelineEditPolicy();
timeline.ContextMenuProvider = new CutsceneTimelineContextMenuProvider(adapter);
timeline.PlaybackController = new CutsceneTimelinePlaybackController(cutsceneRunner);
```

---

# 19. Résumé des modifications concrètes

À faire sur le contrôle actuel :

```text
1. TimelineLane -> TimelineTrack
2. TimelineEvent -> TimelineItem
3. TimeSeconds -> StartTime
4. Ajouter Duration
5. Ajouter TimelineItemKind
6. Remplacer DrawEvents par DrawItems
7. Ajouter DrawInstantItem
8. Ajouter DrawDurationItem
9. Ajouter ITimelineItemRenderer
10. Ajouter ITimelineEditPolicy
11. Ajouter TimelineSnapSettings
12. Ajouter ITimelineAdapter
13. Généraliser les événements publics Event/Lane vers Item/Track
14. Ajouter ITimelineContextMenuProvider
15. Ajouter ITimelinePlaybackController
16. Créer AnimationTimelineAdapter
17. Créer CutsceneTimelineAdapter
18. Créer CutsceneTimelineEditPolicy
```

---

# 20. Recommandation principale

La bonne direction n'est pas de créer un nouveau contrôle de timeline pour chaque éditeur.

Il faut garder un seul contrôle générique :

```text
TimelineControl
```

avec un modèle générique :

```text
TimelineModel
TimelineTrack
TimelineItem
```

et brancher les comportements spécifiques via :

```text
ITimelineAdapter
ITimelineEditPolicy
ITimelineItemRenderer
ITimelineContextMenuProvider
ITimelinePlaybackController
```

Ainsi, le même contrôle pourra servir à :

- l'éditeur d'animation ;
- l'éditeur de cutscene ;
- l'éditeur audio ;
- l'éditeur de séquences gameplay ;
- éventuellement l'éditeur de particules ou de caméra plus tard.

La première étape importante est de rendre le modèle indépendant de la notion d'animation event.

La deuxième étape importante est d'ajouter la durée.

La troisième étape importante est de déplacer les règles métier dans des adapters et des policies.
