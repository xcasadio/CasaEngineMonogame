# Architecture du contrôle Timeline V1

## Objectif

Le contrôle `TimelineControl` permet d'afficher, de naviguer et de scrubber une timeline basée sur le temps, exprimé en secondes, pour gérer des événements ponctuels dans une animation.

Ce document décrit une architecture cible de refactor pour la V1. Le but n'est pas d'introduire un second contrôle parallèle, mais de faire évoluer le contrôle timeline actuel vers une structure plus nette et plus extensible.

La V1 cible volontairement un périmètre simple :

- une seule track ;
- des événements ponctuels ;
- une règle temporelle affichant les secondes ;
- un zoom horizontal ;
- un scroll horizontal ;
- un playhead ;
- le scrub temporel ;
- la sélection d'un événement ;
- l'affichage des propriétés de l'événement sélectionné dans une surface d'inspection extérieure au contrôle.

Le contrôle doit être conçu de manière suffisamment propre pour évoluer ensuite vers plusieurs tracks, des événements avec durée, du drag and drop, du snap, un curseur de lecture et une intégration plus avancée avec l'éditeur d'animation.

## Vue générale

La timeline peut être représentée visuellement par une grille avec une colonne de track et une zone temporelle.

```text
+----------------+-------------------------------------------+
| Tracks         | Règle temporelle                          |
+----------------+-------------------------------------------+
| Track 01       |   *       *          *        *            |
+----------------+-------------------------------------------+
|                | Scroll horizontal                         |
+----------------+-------------------------------------------+
```

Même si la V1 ne contient qu'une seule track, il est préférable de garder une structure qui prépare naturellement l'évolution vers plusieurs tracks. Cette préparation concerne surtout le layout et le découpage visuel. Le modèle V1, lui, reste une liste directe d'événements.

## Découpage recommandé

Le contrôle principal doit être composé de plusieurs sous-contrôles spécialisés.

```text
TimelineControl
 ├─ CornerHeader
 ├─ TimelineRuler
 ├─ TrackHeaderPanel
 ├─ TimelineViewport
 └─ HorizontalScrollBar
```

### `TimelineControl`

`TimelineControl` est le contrôle racine.

Il possède :

- le modèle de données de la timeline ;
- l'état visuel de la timeline ;
- les sous-contrôles ;
- les événements publics comme `SelectedEventChanged` ;
- la coordination entre ruler, viewport et scrollbar.

Il ne doit pas dessiner directement tous les éléments. Son rôle est principalement de composer les autres contrôles et de partager le modèle et l'état de vue.

### `CornerHeader`

Zone située en haut à gauche.

Elle peut simplement afficher le texte `Tracks`.

```text
+---------+
| Tracks  |
+---------+
```

Cette zone est simple en V1, mais elle pourra plus tard accueillir des boutons globaux comme :

- ajouter une track ;
- masquer toutes les tracks ;
- verrouiller toutes les tracks ;
- ouvrir un menu de configuration.

### `TimelineRuler`

`TimelineRuler` affiche la règle temporelle en haut de la timeline.

Son rôle :

- dessiner les graduations majeures ;
- dessiner les graduations mineures ;
- afficher les labels de temps : `0s`, `1s`, `2s`, etc. ;
- respecter le zoom horizontal ;
- respecter le scroll horizontal.

Le ruler ne doit pas posséder sa propre logique de conversion temps/position. Il doit utiliser le même `TimelineViewTransform` que le viewport.

### `TrackHeaderPanel`

`TrackHeaderPanel` affiche la colonne de gauche contenant le nom de la track.

En V1 :

```text
Track 01
```

Plus tard, ce contrôle pourra afficher :

- le nom de chaque track ;
- l'icône du type de track ;
- un bouton mute ;
- un bouton lock ;
- un bouton visibility ;
- un menu contextuel.

Même avec une seule track, il est préférable de garder ce contrôle séparé pour éviter de tout mélanger dans le viewport.

### `TimelineViewport`

`TimelineViewport` est la zone principale d'affichage et d'interaction.

Il dessine :

- le fond de la timeline ;
- les lignes verticales de grille temporelle ;
- la ligne horizontale de la track ;
- les événements ponctuels ;
- le playhead ;
- l'état sélectionné d'un événement ;
- plus tard, les guides de drag.

C'est aussi lui qui gère le hit testing des événements.

Il est préférable que le viewport dessine lui-même ses éléments plutôt que de créer un contrôle enfant pour chaque événement. Cela réduit les allocations, simplifie le clipping et prépare mieux les cas où la timeline contiendra beaucoup d'events.

## Layout MGUI conseillé

La structure peut être un `Grid` avec deux colonnes et trois lignes.

```text
Columns:
  120px   colonne des tracks
  *       zone timeline

Rows:
  24px    ruler
  *       contenu timeline
  16px    scrollbar horizontale
```

Représentation :

```text
+----------------+-------------------------------------------+
| CornerHeader   | TimelineRuler                             |
+----------------+-------------------------------------------+
| TrackHeader    | TimelineViewport                          |
+----------------+-------------------------------------------+
|                | HorizontalScrollBar                       |
+----------------+-------------------------------------------+
```

La scrollbar horizontale doit agir uniquement sur la colonne de droite, c'est-à-dire la zone temporelle. La colonne des tracks ne doit pas scroller horizontalement.

## Modèle de données

Le modèle de données doit représenter la timeline indépendamment de l'interface.

```csharp
public sealed class TimelineModel
{
    public float DurationSeconds { get; set; } = 10f;

    public List<TimelineEvent> Events { get; } = new();
}
```

Un événement ponctuel peut être représenté ainsi :

```csharp
public sealed class TimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public float TimeSeconds { get; set; }

    public string EventType { get; set; } = string.Empty;
}
```

Le modèle V1 reste volontairement minimal :

- `TimelineModel` expose `DurationSeconds` et `Events` ;
- `TimelineEvent` expose `Id`, `TimeSeconds` et `EventType` ;
- la V1 n'introduit pas de `TimelineTrack` explicite ;
- la V1 n'introduit pas de payload générique `Data`.

Le support multi-track est une préoccupation de V2. En V1, la colonne de track et les sous-contrôles séparés préparent cette évolution sans l'imposer au modèle.

## État de vue

Le modèle ne doit pas contenir les informations de zoom, scroll ou sélection. Ces informations appartiennent à l'état de vue.

```csharp
public sealed class TimelineViewState
{
    public float PixelsPerSecond { get; set; } = 100f;

    public float ScrollX { get; set; }

    public Guid? SelectedEventId { get; set; }
}
```

`PixelsPerSecond` représente le niveau de zoom.

Exemple :

- `50 px/s` : timeline éloignée ;
- `100 px/s` : zoom moyen ;
- `300 px/s` : zoom proche.

`ScrollX` représente le décalage horizontal en pixels.

La sélection est stockée avec l'identifiant de l'événement sélectionné, pas avec une référence directe au contrôle visuel.

## Transformation temps / écran

Le point le plus important de l'architecture est la conversion entre le temps et les coordonnées écran.

Il faut centraliser cette logique dans une classe dédiée.

```csharp
public sealed class TimelineViewTransform
{
    public float PixelsPerSecond { get; set; }

    public float ScrollX { get; set; }

    public float TimeToX(float timeSeconds)
    {
        return timeSeconds * PixelsPerSecond - ScrollX;
    }

    public float XToTime(float x)
    {
        return (x + ScrollX) / PixelsPerSecond;
    }
}
```

`TimelineRuler` et `TimelineViewport` doivent utiliser exactement le même transform.

Cela garantit que :

- les labels de la ruler sont alignés avec les events ;
- les lignes de grille sont alignées avec les graduations ;
- le hit testing correspond à ce qui est dessiné ;
- le zoom et le scroll restent cohérents.

## Dessin de la règle temporelle

Le ruler doit adapter ses graduations au zoom.

Exemple :

```text
Zoom éloigné : 0s, 5s, 10s, 15s
Zoom moyen   : 0s, 1s, 2s, 3s
Zoom proche  : 0.0s, 0.1s, 0.2s, 0.3s
```

Une fonction simple peut suffire pour la V1.

```csharp
private float GetMajorTickStep(float pixelsPerSecond)
{
    if (pixelsPerSecond < 20f)
        return 5f;

    if (pixelsPerSecond < 60f)
        return 1f;

    if (pixelsPerSecond < 150f)
        return 0.5f;

    return 0.1f;
}
```

Le ruler peut aussi dessiner des graduations mineures entre deux graduations majeures.

## Dessin des événements ponctuels

Un événement ponctuel n'a pas de durée. Il doit donc être affiché avec une largeur visuelle constante.

Formes possibles :

- losange ;
- petit rectangle ;
- marqueur vertical ;
- icône.

Pour une V1, le losange ou le petit rectangle est le plus simple.

Exemple :

```csharp
float x = transform.TimeToX(timelineEvent.TimeSeconds);
float y = trackCenterY;

var eventBounds = new RectangleF(
    x - 5f,
    y - 5f,
    10f,
    10f);
```

Même si l'événement est ponctuel, sa zone cliquable doit être plus grande que son point exact.

Exemple :

```csharp
var hitBounds = new RectangleF(
    x - 8f,
    y - 8f,
    16f,
    16f);
```

Cela rend la sélection plus confortable.

## Hit testing et sélection

Le hit testing doit être fait dans `TimelineViewport`.

Pseudo-code :

```csharp
private TimelineEvent? HitTestEvent(Vector2 localMousePosition)
{
    for (int i = Model.Events.Count - 1; i >= 0; i--)
    {
        TimelineEvent timelineEvent = Model.Events[i];

        float x = _transform.TimeToX(timelineEvent.TimeSeconds);
        float y = _trackCenterY;

        var hitBounds = new RectangleF(x - 8f, y - 8f, 16f, 16f);

        if (hitBounds.Contains(localMousePosition.X, localMousePosition.Y))
            return timelineEvent;
    }

    return null;
}
```

Il est préférable de parcourir les événements en ordre inverse si les derniers events sont dessinés au-dessus des précédents.

Lorsqu'un événement est sélectionné :

```csharp
ViewState.SelectedEventId = selectedEvent.Id;
SelectedEventChanged?.Invoke(this, selectedEvent);
```

Si aucun événement n'est touché :

```csharp
ViewState.SelectedEventId = null;
SelectedEventChanged?.Invoke(this, null);
```

## Intégration avec la surface d'inspection

Le `TimelineControl` ne doit pas connaître les détails internes de la surface d'inspection.

Il doit seulement exposer :

```csharp
public TimelineEvent? SelectedEvent { get; }

public event EventHandler<TimelineEvent?>? SelectedEventChanged;
```

L'éditeur ou l'écran parent connecte ensuite la timeline à sa propre logique d'inspection.

```csharp
timelineControl.SelectedEventChanged += (_, selectedEvent) =>
{
    // La vue parente met à jour sa propre surface d'inspection.
};
```

Cette architecture permet de garder le contrôle timeline indépendant.

Le contrôle timeline :

- affiche la timeline ;
- gère le zoom ;
- gère le scroll ;
- gère la sélection.

La surface d'inspection externe :

- inspecte l'objet sélectionné ;
- affiche ses propriétés ;
- permet l'édition des propriétés ;
- notifie éventuellement les changements.

La timeline ne doit pas contenir de logique spécifique à l'affichage des propriétés.

## Scroll horizontal

La largeur totale du contenu dépend de la durée et du zoom.

```csharp
float contentWidth = Model.DurationSeconds * ViewState.PixelsPerSecond;
```

Le scroll maximal dépend de la largeur visible.

```csharp
float maxScrollX = MathF.Max(0f, contentWidth - viewportWidth);
```

Lorsqu'on modifie le scroll :

```csharp
ViewState.ScrollX = Math.Clamp(newScrollX, 0f, maxScrollX);
```

La scrollbar horizontale doit mettre à jour `ViewState.ScrollX`, puis invalider le layout ou le rendu du ruler et du viewport.

## Zoom horizontal

Le zoom doit modifier `PixelsPerSecond`.

Il est préférable de centrer le zoom sur la position de la souris.

```csharp
float mouseX = localMousePosition.X;

float timeUnderMouseBeforeZoom =
    (mouseX + ViewState.ScrollX) / ViewState.PixelsPerSecond;

ViewState.PixelsPerSecond *= zoomFactor;
ViewState.PixelsPerSecond = Math.Clamp(ViewState.PixelsPerSecond, 20f, 1000f);

ViewState.ScrollX =
    timeUnderMouseBeforeZoom * ViewState.PixelsPerSecond - mouseX;
```

Ensuite, il faut borner le scroll.

```csharp
ViewState.ScrollX = Math.Clamp(ViewState.ScrollX, 0f, maxScrollX);
```

Ce comportement est beaucoup plus naturel qu'un zoom centré sur le début de la timeline.

## Overlay ou canvas transparent

L'idée d'utiliser un overlay ou un canvas transparent est bonne, mais il vaut mieux éviter un overlay global posé au-dessus de tout le contrôle.

La meilleure approche pour la V1 est :

```text
TimelineViewport
 ├─ dessine le fond
 ├─ dessine la grille temporelle
 ├─ dessine les events
 ├─ dessine la sélection
 └─ dessine les overlays internes
```

Autrement dit, le viewport agit comme un canvas spécialisé.

Il peut dessiner les éléments d'overlay suivants :

- sélection d'un event ;
- hover d'un event ;
- ligne verticale sous la souris ;
- playhead ;
- guides de drag.

Il vaut mieux éviter un overlay global séparé, car cela complique :

- le clipping ;
- le calcul des coordonnées locales ;
- le scroll horizontal ;
- la synchronisation avec le ruler ;
- le hit testing ;
- les interactions futures.

## Responsabilités par classe

### `TimelineControl`

Responsabilités :

- contient le `TimelineModel` ;
- contient le `TimelineViewState` ;
- crée et dispose les sous-contrôles ;
- synchronise ruler, viewport et scrollbar ;
- expose l'événement `SelectedEventChanged` ;
- expose l'événement `ViewChanged` si nécessaire.

### `TimelineRuler`

Responsabilités :

- dessine les graduations ;
- dessine les labels de temps ;
- respecte le zoom ;
- respecte le scroll ;
- utilise `TimelineViewTransform`.

### `TimelineViewport`

Responsabilités :

- dessine le fond ;
- dessine la grille temporelle ;
- dessine la track ;
- dessine les events ;
- dessine la sélection ;
- gère le clic souris ;
- effectue le hit testing ;
- notifie la sélection au `TimelineControl`.

### `TrackHeaderPanel`

Responsabilités :

- affiche le nom de la track ;
- garde l'alignement vertical avec le viewport ;
- prépare le support futur de plusieurs tracks.

### `TimelineViewState`

Responsabilités :

- stocke le zoom ;
- stocke le scroll ;
- stocke l'event sélectionné ;
- ne contient aucune donnée métier de l'animation.

### `TimelineViewTransform`

Responsabilités :

- convertir un temps en coordonnée X ;
- convertir une coordonnée X en temps ;
- garantir la cohérence entre ruler, viewport, dessin et hit testing.

## Gestion de la performance

Même pour une V1 simple, il faut éviter que le contrôle devienne coûteux.

Recommandations :

- ne pas créer un contrôle enfant par event ;
- dessiner les events directement dans `TimelineViewport` ;
- ne pas allouer de listes temporaires à chaque frame ;
- éviter LINQ dans les méthodes de dessin ;
- ne dessiner que les events visibles ;
- centraliser les conversions temps/écran ;
- utiliser des dirty flags si le layout devient plus complexe.

Pour savoir si un event est visible :

```csharp
float x = transform.TimeToX(timelineEvent.TimeSeconds);

if (x < -eventVisualSize || x > viewportWidth + eventVisualSize)
    continue;
```

## Évolution vers la V2

Cette architecture prépare les évolutions suivantes :

- plusieurs tracks ;
- events avec durée ;
- drag and drop des events ;
- redimensionnement des events avec durée ;
- snap temporel ;
- preview de l'animation ;
- markers ;
- zoom vertical ;
- sélection multiple ;
- copier/coller ;
- undo/redo ;
- tracks typées : audio, hitbox, particle, gameplay.

Le point important est que la V1 ne doit pas être codée comme un contrôle jetable. Elle doit être simple, mais déjà structurée comme un vrai contrôle d'éditeur.

## Recommandation finale

Pour la V1, l'architecture conseillée est :

```text
TimelineControl
 ├─ Grid
 │   ├─ CornerHeader        row 0, column 0
 │   ├─ TimelineRuler       row 0, column 1
 │   ├─ TrackHeaderPanel    row 1, column 0
 │   ├─ TimelineViewport    row 1, column 1
 │   └─ HorizontalScrollBar row 2, column 1
```

Le dessin de la timeline doit être concentré dans `TimelineRuler` et `TimelineViewport`.

Le modèle V1 reste `TimelineModel.DurationSeconds + Events`, complété par `TimelineViewState` et `TimelineViewTransform`.

La surface d'inspection reste extérieure au contrôle timeline. Elle se connecte simplement à l'événement `SelectedEventChanged`.

Cette séparation garde le contrôle timeline réutilisable, performant et extensible.
