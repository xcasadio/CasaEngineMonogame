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
```

Sémantique de la rampe, calquée sur `AudioService.FadeVoice` : cible atteinte exactement à la fin de
la durée, aucun dépassement sur une frame longue, `durationSeconds` à 0 (ou `NaN`) applique la cible
immédiatement. Un second `StartFade` appelé pendant une rampe en cours redémarre proprement dès lors
que l'appelant passe la valeur courante comme nouveau `from` — le service ne fait aucune magie
supplémentaire, il ne fait que ramper entre les deux valeurs qu'on lui donne.

`Update(elapsedSeconds)` avance la rampe ; sans rampe en cours, c'est un no-op sans allocation.

---

## 3. `ScreenEffectComponent` — le cran de rendu et la formule de placement

`RenderPass2D.ScreenEffects = 750`, entre `Effects` (500) et `UI` (1000) : au-dessus de toute la
scène/des effets, sous l'UI.

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
la propre transformation caméra et fait toujours couvrir tout l'écran, où que soit la caméra.

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

---

## 6. Limites connues (V1)

- **Pas de surface éditeur**, hormis l'action de cutscene `FadeScreen` (pas d'inspecteur dédié).
- **Une seule couche** : le service ne gère qu'un overlay plein écran, pas une pile d'effets.
- **Aucune caméra autre que `ViewManager.ActiveView` n'est consultée.** Un jeu avec plusieurs vues
  actives simultanément ne recevrait qu'une seule position caméra pour l'overlay.
- Toute la fidélité PSX (16.16, échange de canaux, division tronquante, verrous de persistance,
  cadence par tick) est un problème de la DLL consommatrice, pas de ce mécanisme moteur — câblée
  côté jeu (chantier E10.b, hors de ce dépôt moteur).
