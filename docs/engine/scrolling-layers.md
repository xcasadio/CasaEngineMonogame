# Couches défilantes CasaEngine — V1 (mécanisme)

Le mécanisme moteur derrière les fonds défilants d'Alundra (plan `plan-e9b-backdrops-moteur.md`) :
parallaxe caméra, auto-défilement et cadence de V-animation, par tick logique entier, pour un jeu de
couches et une teinte plein écran optionnelle. Patron d'architecture identique aux effets d'écran
([screen-effects.md](screen-effects.md)) et à l'audio : service sans type GPU + composant mince + doc
+ tests.

**Moteur = mécanisme, DLL = politique** : ce service ne sait pas ce qu'est un compagnon `.backdrop.json`,
un mode `Tiles`/`Cellular`, ni comment traduire `Ground`/`BlendMode` en passe/fusion/teinte — cette
traduction reste dans la DLL consommatrice (`AlundraBackdropStage.BuildDefinitions`), exactement comme
le fondu E10 sépare le mécanisme moteur de la fidélité PSX.

---

## 1. Vue d'ensemble

```text
ScrollingLayerService          définitions de couches/teinte, état par tick, contrat de poussée
        ↑
ScrollingLayerComponent        GameComponent : résout les textures, avance, soumet les quads
```

Point d'entrée depuis le jeu : `game.ScrollingLayerComponent.Service`.

## 2. Le contrat de poussée et la consommation des ticks

Chaque frame, l'appelant (la DLL) pousse l'état de défilement de la frame :

```csharp
service.SetFrame(scrollX, scrollY, ticks, cameraTarget);
```

`SetFrame` **arme** une frame en attente (`HasPendingFrame`, `PendingTicks`) sans rien avancer ; une
seconde poussée avant l'`Advance()` suivant **écrase** la précédente (jamais de cumul — la dernière
poussée fait foi). `Advance()` **consomme** la frame en attente : pour chacun des `PendingTicks`
ticks, pour chaque couche, dans l'ordre de l'original — cadence V (`++AnimFrameTimer > AnimTimer` puis
`++AnimFrameCounter`) puis auto-défilement accumulé (`OffsetX += Speed`, `+= Dir` tous les `|Period|`
ticks) — puis remet `PendingTicks` à 0. Le décalage final par couche (`LayerOffsetX/Y`, lu par
`TryGetLayerState`) est recalculé après chaque `Advance()`, **même à zéro tick** : une nouvelle
poussée doit se refléter immédiatement, exactement comme l'original recalcule à chaque frame quel que
soit le nombre de ticks.

Une seule horloge, entière, aucune forme close : `ticks = 0` n'avance rien, `ticks = 4` avance quatre
fois — fidèle à l'original sur une frame de rattrapage, identique bit à bit à une frame nominale.

## 3. Formule de placement et origines couvrantes

Vue et toile poussées en configuration (Alundra : toile 640×480, vue 320×240 — la taille du
framebuffer original, pas celle de la fenêtre). Coin haut-gauche de la vue en espace monde :
`cameraTarget + (−vue.Largeur/2, +vue.Hauteur/2)`. Les origines couvrantes sont calculées sans
allocation par la paire `CoveringOriginStart`/`CoveringOriginCount` (bornes entières, boucle sur
entiers, au plus 2×2 quads par couche pour une toile 640×480 contre une vue 320×240) — remplace la
`List<Point>` allouée de l'ancien mécanisme DLL.

## 4. Z policy (revised, plan-e9c-defauts-321.md, D-E9c-5)

A `RenderPass2D.Background` layer is submitted at `cameraTarget.Z - configuration.BackgroundDepth`
(`ScrollingLayerConfiguration.BackgroundDepth`, default 1 - Alundra's camera target Z is 0, so a
background layer lands at z = -1). Every other pass, and the tint quad, stay at `cameraTarget.Z` - and
so does the full-screen fade (`ScreenEffectComponent.SubmitOverlay`, `cameraPosition.Z`), the camera's
own depth being the one value both components read it from.

This corrects the previous policy - z = 0 for every quad, exactly like the retired
`BackdropRenderer` (`:418`, `:467`) - which the original E9.b measurement (map 321,
`scratchpad/port-321-before-a/b.png`) had judged correct because it only examined the *empty sky*
above the arena floor: clouds were visible there and never in front of the floor's lower half, so the
floor appeared to win the tie unconditionally. Slice E9.c measured further and found map 321's
topmost bone row (`Render_0`, z_offset 0.0, rows 13-19) is drawn through
`SpriteRendererComponent.DrawStaticBatch`'s IMMEDIATE static batch during `World.Draw`, i.e. BEFORE
the sprite queue (backdrop included) is flushed - at equal depth (`LessEqual` + depth write), the
*later*-drawn quad wins, and the backdrop is queued and flushed after the static batch. So a
Background layer at z = 0 overpainted that topmost bone row all along, cloud cutting cleanly at the
row where the bone's silhouette changes - invisible against an empty sky, visible only where a static
tile shares the same world Z.

The original itself places `Ground = false` layers at `-0x10000000 + order`
(`GraphicManager.cs:825-826`) - behind every floor, wall and entity, never at equal depth. In this
engine a smaller world Z is FARTHER from the camera, so receding a Background layer to
`cameraTarget.Z - BackgroundDepth` makes its depth write lose against both the static tile batch
(world Z 0, drawn first) and the sorted overlay (also world Z 0, drawn after within the same flush) -
while still being correctly REJECTED nowhere it shouldn't, since nothing else writes a farther depth.
The full-screen fade (`RenderPass2D.ScreenEffects`, z = `cameraPosition.Z`, the camera depth) still
covers everything, as it did before.

## 5. Ciseaux

`Submit` ne lit jamais le `GraphicsDevice` : le rectangle de ciseaux est un **paramètre**, résolu une
fois par frame par `ScrollingLayerComponent.Update` (`GraphicsDevice.ScissorRectangle` sous garde,
repli `(0, 0, ScreenSizeWidth, ScreenSizeHeight)` en pixels si aucun périphérique) puis passé à la
surcharge `DrawSprite` à ciseaux explicite. Ce qui rend `Submit` exerçable headless en passant un
rectangle de test.

## 6. Fusion et résolution des textures

Chaque couche porte son propre `SpriteBlendMode`/teinte (politique DLL) ; la teinte plein écran est
toujours soumise en `SpriteBlendMode.AlphaBlend`. Les textures sont résolues par
`ScrollingLayerComponent.ResolveTextures(loader)`, appelé par `Update` seulement quand
`Service.LayersVersion` change (jamais par frame) — un id nul (`Guid.Empty`) donne une trame nulle
sans appeler le délégué ; trame 0 nulle → couche ignorée ; trame `f ≥ 1` nulle → repli sur `[frame0]`
seule, jamais un tableau partiel (même règle que l'ancien `BackdropRenderer.LoadLayerFrames`).

## 7. Note — cadence RAW vs résolue (déviation bénigne, non un bug)

`ScrollingLayerService.AdvanceLayerOneTick` fait boucler `AnimFrameCounter` modulo la longueur **RAW**
de `ScrollingLayerDefinition.FrameTextureAssetIds` (le tableau d'ids poussé par la DLL, avant tout
repli de chargement). L'ancien `BackdropRenderer.AdvanceAnimation` bouclait, lui, modulo la longueur
**résolue** de `Frames` (après le repli D-E9-9). C'est délibérément différent du service, et
observationnellement équivalent seulement parce que la longueur résolue vaut toujours 0 (couche
ignorée — le modulo ne s'applique à rien), 1 (repli : `ScrollingLayerComponent.Submit` clampe l'index
de trame à `frames.Length - 1`, donc la trame 0 est toujours dessinée quel que soit le compteur — même
résultat visuel qu'un modulo 1) ou N (identique au cas normal, pas de repli).

**Le compteur brut lui-même diffère en cas de repli** : avec 4 ids et un repli en trame 1, le service
compte 0,1,2,3,0,... (modulo 4, RAW) alors que l'ancien code aurait compté 0,0,0,... (modulo 1,
résolu). Un harnais d'équivalence tick à tick (S1) qui comparerait la **valeur** de `AnimFrameCounter`
entre les deux implémentations verrait donc une divergence sur une couche dégradée, alors que le rendu
est identique. Un tel harnais doit comparer la **texture soumise** (ou l'index de trame après clamp),
jamais la valeur brute du compteur, dès qu'un chargement de couche est dégradé.

## 8. Limites connues (V1)

- **Pas d'éditeur, pas de sérialisation** : le format `.backdrop.json` reste lu par la DLL: zéro
  changement convertisseur.
- **Aucune vue autre que celle qui pousse `SetFrame`** : une seule caméra active à la fois.
- **`Submit` ne soumet rien sans poussée reçue** (`FramesPushed == 0`) : l'aperçu éditeur, qui ne fait
  jamais tourner la DLL (`UpdateGameplayScripts = false`), ne dessine donc aucun fond — comportement
  inchangé, même limite que [screen-effects.md](screen-effects.md).
- Les comportements jamais portés par la DLL (activation par couche à l'exécution, décalage scripté,
  marcheur de teinte, mode cellulaire) restent hors de ce mécanisme.
