# Effets d'écran CasaEngine — V1 (fondu / teinte plein écran)

Une teinte plein écran, avec fondu optionnel : le mécanisme générique dont le premier consommateur
est le fondu d'entrée d'Alundra (E10). Le patron d'architecture est celui du système audio V1 :
service sans MonoGame + composant mince + doc + tests.

---

## 1. Vue d'ensemble

```text
ScreenEffectService           état : actif, r/g/b (byte), SpriteBlendMode, rampe optionnelle
        ↑
ScreenEffectComponent          GameComponent : possède le pixel 1x1, soumet le quad chaque frame
```

Point d'entrée depuis le jeu : `game.ScreenEffectComponent.Service`.

`ScreenEffectService` ne contient **aucun type MonoGame** (pas de `Color`, pas de `Game`) : c'est ce
qui rend la rampe testable sans périphérique graphique, exactement comme `AudioService`.

**Moteur = mécanisme, DLL = politique** : toutes les bizarreries de fidélité PSX — 16.16, échange de
canaux, division tronquante, verrou de persistance, cadence par tick logique — restent dans la DLL du
jeu, qui calcule ses propres machines et pousse `(r, g, b, blend, actif)` au service chaque frame. Le
service lui-même ne sait rien de tout cela ; sa propre commodité `StartFade` est une rampe linéaire
générique, sans mémoire au-delà de la dernière couleur reçue.

---

## 2. `ScreenEffectService`

```csharp
var effects = game.ScreenEffectComponent.Service;

// Teinte immédiate, sans rampe.
effects.SetOverlay(r: 0, g: 0, b: 0, SpriteBlendMode.Subtractive);

// Rampe : "from" et "to" sont fournis par l'appelant (le service ne mémorise rien de sa propre
// machine de couleur - c'est au consommateur, ici la DLL, de suivre son état courant).
effects.StartFade(fromR: 255, fromG: 255, fromB: 255, toR: 0, toG: 0, toB: 0,
    durationSeconds: 0.32f, SpriteBlendMode.Subtractive);

// Désactive : plus rien n'est soumis tant que SetOverlay/StartFade n'est pas rappelé.
effects.Clear();

// Couche : par défaut BelowUI (comportement inchangé). AboveUI dessine le quad après la composition
// de l'interface MGUI, pour qu'un fondu assombrisse une jauge dessinée en MGUI avec la scène - voir
// ADR-0033. `Clear()` ne touche jamais à `Layer` : c'est un réglage de rendu, pas un état de fondu.
effects.Layer = ScreenEffectLayer.AboveUI;
```

Sémantique de la rampe, calquée sur `AudioService.FadeVoice` : cible atteinte exactement à la fin de
la durée, aucun dépassement sur une frame longue, `durationSeconds` à 0 (ou `NaN`) applique la cible
immédiatement. Un second `StartFade` appelé pendant une rampe en cours redémarre proprement dès lors
que l'appelant passe la valeur courante comme nouveau `from` — le service ne fait aucune magie
supplémentaire, il ne fait que ramper entre les deux valeurs qu'on lui donne.

`Update(elapsedSeconds)` avance la rampe ; sans rampe en cours, c'est un no-op sans allocation.

### 2.1 Alpha channel (`A`, byte)

`ScreenEffectService` also carries an alpha channel, `A` (byte, default `255` — fully opaque, the
behaviour every existing consumer already gets). Two additive overloads take it explicitly; the
existing R/G/B-only `SetOverlay`/`StartFade` signatures are unchanged and delegate to the new ones
with `a = 255`, so no current caller (the cutscene `FadeScreen` action in
`CasaEngine/Cutscenes/CutsceneActionCoroutineFactory.cs`, `CasaEngine.Demos/Demos/TileMapDemo.cs`,
and the Alundra port, which depends on `Subtractive`) sees any change in behaviour:

```csharp
effects.SetOverlay(r: 0, g: 0, b: 0, a: 128, SpriteBlendMode.AlphaBlend);

effects.StartFade(fromR: 0, fromG: 0, fromB: 0, fromA: 0,
    toR: 0, toG: 0, toB: 0, toA: 255,
    durationSeconds: 0.5f, SpriteBlendMode.AlphaBlend);
```

`Update` interpolates `A` exactly like `R`/`G`/`B`. `Clear()` leaves `A` untouched, exactly as it
already leaves `Layer`/`R`/`G`/`B` untouched: it is a rendering setting the overlay carries between
activations, not fade state.

There are now two ways to fade the screen:

- **Alpha fade** (`SpriteBlendMode.AlphaBlend`, i.e. `BlendState.NonPremultiplied`): a modern fade
  toward an arbitrary target colour, blending `lerp(scene, Color(R,G,B), A/255)`.
  `ScreenEffectComponent.SubmitOverlay` draws the overlay quad with `new Color(Service.R, Service.G,
  Service.B, Service.A)`
  (`CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs`) over an opaque white
  1×1 pixel (`GetOrCreatePixelTexture`), so the sprite
  shader's non-premultiplied output (`texel * Color`, see `SpriteBlendMode.AlphaBlend`'s doc comment,
  `CasaEngine/Framework/Rendering/Depth/SpriteBlendMode.cs:14-20`) is exactly `Color`, and
  `BlendState.NonPremultiplied` (`SourceAlpha`/`InverseSourceAlpha`) blends it against the
  destination with no premultiplication anywhere on this path.
- **Additive / subtractive** (`SpriteBlendMode.Additive` / `.Subtractive`, §4): the PSX-accurate
  colour math where the fade intensity is carried by the colour itself, not by alpha — this only
  works because these blend states ignore the overlay's alpha for the colour output. The Alundra
  port depends on `Subtractive` to reproduce the original PlayStation fade and keeps using it
  unchanged.

---

## 3. `ScreenEffectComponent` — le cran de rendu et la formule de placement

`RenderPass2D.ScreenEffects = 750`, entre `Effects` (500) et `UI` (1000) : au-dessus de toute la
scène/des effets, sous l'UI **par défaut** (`ScreenEffectService.Layer == ScreenEffectLayer.BelowUI`,
la valeur par défaut). Quand `Layer == ScreenEffectLayer.AboveUI`, le composant ne soumet plus le
quad dans son `Update` mais s'enregistre comme surcouche post-interface (`IPostUIOverlay`) sur la vue
active : `DefaultUICompositionService.Compose` la dessine après `UIView.Draw()`, en soumettant au
renderer de sprites puis en le vidant immédiatement, sans jamais laisser de file d'une image à
l'autre (ADR-0033). Aucun pipeline de vue ni aucune passe n'est modifié par ce mode.

Le composant possède le pixel 1×1 (créé paresseusement contre le `GraphicsDevice` réel ; contourné
sans exception si aucun n'est disponible) et soumet le quad plein viewport dans son propre `Update` —
pas dans `Draw` : `CasaEngineGame.Update` exécute `GameManager.UpdateWorld` (où la DLL pousse l'état
de la frame au service) **avant** tous les `GameComponent.Update`, donc l'état lu ici est déjà celui
de la frame courante.

```csharp
public void SubmitOverlay(SpriteRendererComponent renderer, Vector3 cameraPosition,
    int viewportWidth, int viewportHeight, Texture2D overlayTexture = null,
    Rectangle? scissorRectangle = null)
```

Tous les intrants sont fournis par l'appelant — aucun `GraphicsDevice`, aucune lecture de
`ScreenSizeWidth/Height` ou d'`ActiveView` dans cette méthode — ce qui la rend exerçable sans
périphérique, avec une texture explicite en test. La formule de placement est **exactement** celle
du bloc teinte de `BackdropRenderer.Draw` (dépôt Alundra) : le quad est positionné à
`cameraPosition - demi-viewport` (avec le flip Y du monde +Y-haut vers l'écran +Y-bas), ce qui annule
la propre transformation caméra et fait toujours couvrir tout l'écran, où que soit la caméra. Le quad
est soumis à `z = cameraPosition.Z` (et non plus une profondeur littérale `0f`) : le fondu suit ainsi
la profondeur de la caméra exactement comme la teinte des couches défilantes
(`ScrollingLayerComponent.Submit`, `cameraTarget.Z` — voir [scrolling-layers.md](scrolling-layers.md)
§4), ce qui reste sans effet observable tant que la caméra garde `Target.Z == 0` (le cas d'Alundra)
mais évite qu'un fondu se retrouve rejeté en profondeur derrière une couche d'arrière-plan reculée si
la caméra s'éloignait un jour de z = 0.

`Update` résout la seule source caméra retenue par le plan — `ViewManager.ActiveView.Camera` (casté
en `Camera2dComponent`, dont `.Target` est la position monde). La taille de vue passée à
`SubmitOverlay` n'est **plus** `ScreenSizeWidth/Height` en pixels par défaut (P3 hérité, plan E9.b
D-E9b-12) : la couture pure `TryGetCameraViewSize(camera, out width, out height)` — testable sans
périphérique — calcule `(Viewport.Width / Zoom, Viewport.Height / Zoom)` en **unités monde** quand la
caméra et son viewport sont valides (320 × 236 côté Alundra), et `Update` ne replie sur
`ScreenSizeWidth/Height` (pixels) que si elle renvoie faux (caméra nulle, viewport vide) — l'overlay
ne cesse jamais d'être soumis pour autant. Cette taille n'est donc **plus** en parité avec le bloc
teinte plein écran des couches défilantes (voir [scrolling-layers.md](scrolling-layers.md), dont la
configuration porte sa propre taille de vue 320×240 poussée par la DLL) : les deux mécanismes
résolvent leur taille de vue indépendamment. Le rectangle de ciseaux est résolu par `Update` sous la
même garde que le pixel 1×1 (`GraphicsDevice.ScissorRectangle`, ou `(0, 0, viewportWidth,
viewportHeight)` à défaut) et passé en dernier paramètre optionnel de `SubmitOverlay`.

---

## 4. Fusion : `SpriteBlendMode.Additive` / `.Subtractive`

Formules exactes de la GPU PSX (par canal, saturées), en plus des états `Opaque`/`AlphaBlend`
existants :

| Mode | `ColorBlendFunction` | Facteurs couleur | Facteurs alpha |
|---|---|---|---|
| `Additive` | `Add` | `One` / `One` | `Add`, `Zero` / `One` |
| `Subtractive` | `ReverseSubtract` (**pas** `Subtract`, qui calculerait src − dst) | `One` / `One` | `Add`, `Zero` / `One` |

`GetBlendState` (dans `SpriteRendererComponent`) rend des instances **en cache**, jamais allouées par
appel/run — le même contrat anti-allocation que les états existants.

---

## 5. Cutscenes : l'action `FadeScreen`

Cinquième action du système, calquée exactement sur `FadeMusic` :

| Action | Bloquante ? | Champs |
|---|---|---|
| `FadeScreen` | **oui** | `r`, `g`, `b`, `duration_seconds`, `blend_mode` |

Bloquante parce que l'action suivante doit démarrer une fois l'écran arrivé à la couleur cible,
exactement pour la même raison que `FadeMusic` attend la fin de sa rampe. L'exécution passe par la
fabrique de coroutines, qui appelle la commodité `StartFade` du service au premier tick puis attend
`duration_seconds` avant de laisser passer la suite — la rampe elle-même est avancée par
`ScreenEffectService.Update`, pas par la coroutine.

Hors cette action de cutscene, la feature **n'a pas de surface éditeur** en V1 — choix explicite.

### 5.1 UI screens: the `fade_screen` action

A project with no DLL reaches the same service from a `.uiscreen` (ADR-0081, [data-driven-screens.md](data-driven-screens.md#fades)). `fade_screen` sets `Layer`, then calls `StartFade`, and `wait_fade` holds the rest of the list while `IsFading` is true.

| | Cutscene `FadeScreen` | UI `fade_screen` |
|---|---|---|
| Waiting | The action blocks | A separate `wait_fade` action |
| Layer | Not set | `layer`, required |
| Start color | The current color | `from`, required |
| Alpha | 255 | Fourth value of `from` and `to`, 255 when left out |
| A missing or invalid field | Default value | Refused when the screen opens |

### 5.2 Exact steps per frame

The service has no step of its own: it interpolates from the elapsed time and rounds. A project that wants an exact step per frame gets it from data:

- it sets `"IsFixedTimeStep": true` in its project file;
- it writes `duration_seconds` as a number of frames over the rate.

The engine does not set `TargetElapsedTime`, so the rate is MonoGame's default: 166667 ticks, read from `new Game().TargetElapsedTime`, about 60 updates a second. A subtractive fade from 255 to 0 by 5 per frame lasts 51 frames, so `duration_seconds` is 51 / 60, written as `0.85`. `ScreenEffectServiceTests.AFadeInFrames_StepsExactly_WhenTheStepDivides255` proves the values on that step, for steps of 5, 15, 17 and 51.

Limits:

- A step is only exact when it divides 255. 16 per frame over 16 frames gives values that are not all multiples of 16, ending on 255.
- A duration rounded up ends one frame late: 0.8501 instead of 0.85 finishes on frame 52, not 51.
- The first step shows on the frame of the action, not the next one, when the fade starts from the UI: the UI updates before `ScreenEffectComponent`. Against a target that draws its first step one frame after the input, this is one frame early.
- A fade started when a world starts shows `from` on its first frame and its first step on the second, because a world's time starts at its first frame (ADR-0082). It reaches `to` on frame N + 1 for N steps, one frame later than a fade started from the UI: 255, 250, then 5 by 5 down to 0 on frame 52 for the fade above. A screen opened by `open_screen` during a world counts like the UI. A `BelowUI` fade stacks two quads on that first frame, see [data-driven-screens.md](data-driven-screens.md#limits).
- The last value is `to`. A target whose last visible value is lower ends higher here.
- A late frame loses steps. Under a fixed step MonoGame replays the time as Updates with no `Draw`, up to `Game.MaxElapsedTime`, which reads 500 ms, so 30 Updates at this rate. The fade advances on each of them, so a frame 4 steps late draws 255, 250, then 225. Only the first two frames of a world are protected, by `LoadTimeDiscard` (ADR-0082).

---

## 6. Limites connues (V1)

- **No editor surface**, apart from the cutscene action `FadeScreen` (no dedicated inspector). The UI action `fade_screen` is written by hand in the JSON: the editor has no panel for actions (ADR-0078).
- **Une seule couche** : le service ne gère qu'un overlay plein écran, pas une pile d'effets.
- **Aucune caméra autre que `ViewManager.ActiveView` n'est consultée.** Un jeu avec plusieurs vues
  actives simultanément ne recevrait qu'une seule position caméra pour l'overlay.
- Toute la fidélité PSX (16.16, échange de canaux, division tronquante, verrous de persistance,
  cadence par tick) est un problème de la DLL consommatrice, pas de ce mécanisme moteur — câblée
  côté jeu (chantier E10.b, hors de ce dépôt moteur).
