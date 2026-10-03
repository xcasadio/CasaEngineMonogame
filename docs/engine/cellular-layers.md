# Couches à cellules CasaEngine — V1 (mécanisme)

Le mécanisme moteur derrière le mode **cellulaire** des fonds d'Alundra (`docs/plan-e9d-mode-cellulaire.md`)
: le second des deux modes de rendu de fond de l'original (`Mode == 2`), jamais porté jusqu'ici. Contrairement
au mode défilant ([scrolling-layers.md](scrolling-layers.md)), une couche cellulaire n'est pas une texture
pleine tuilée mais jusqu'à 200 rectangles indépendants, chacun son propre sprite, chacun sa propre dérive,
sa propre période, sa propre parallaxe caméra. **Couple parallèle, pas une extension** : les deux mécanismes
partagent seulement les fonctions pures et la plomberie de tri/fusion existante
(`RenderSortKey2D`/`SpriteBlendMode`/`RenderPass2D`) — `ScrollingLayerService` n'est pas déformé
(plan-e9d-mode-cellulaire.md D3).

**Moteur = mécanisme, DLL = politique** : ce service ne sait pas ce qu'est un compagnon `.backdrop.json`
ni comment construire les couches — cette traduction reste dans la DLL consommatrice, exactement comme
pour le mécanisme frère.

---

## 1. Vue d'ensemble

```text
CellularLayerService          définitions de couches/cellules, WaveLut de carte, état par tick, contrat de poussée
        ↑
CellularLayerComponent        GameComponent : résout les planches, avance, soumet chaque cellule visible
```

Point d'entrée depuis le jeu : `game.CellularLayerComponent.Service`.

## 2. Quatre types de cellule

`CellularCellType` (Alundra `ScrollScreen.cs:369-375`) — volontairement non contiguë, pas de valeur 3 :

| Type | Comportement |
|---|---|
| `Normal` (0) | dérive (`DX`/`DY`) + pas de période, parallaxe caméra, enroulement sur les deux axes dans l'écran 320×240 |
| `ScriptTrack` (1) | `case` au corps vide dans l'original : **ne dessine rien, n'avance aucun état** (D5 — gelé, zéro occurrence sur 8360 cellules mesurées) |
| `FallRespawn` (2) | même dérive et même enroulement en X que `Normal`, mais **aucun enroulement en Y** — une cellule qui dépasse le bas de l'écran réapparaît en haut à une abscisse aléatoire |
| `WaveX` (4) | **sans état**, ignore totalement la parallaxe caméra, ne bouge jamais en Y — sa position X vient de deux échantillons d'une table `WaveLut` par carte, combinés par une formule fixe |

## 3. Le contrat de poussée et la consommation des ticks

Même contrat que le mécanisme frère :

```csharp
service.SetFrame(cameraX, cameraY, ticks, cameraTarget);
```

`SetFrame` **arme** une frame en attente sans rien avancer ; une seconde poussée avant l'`Advance()`
suivant **écrase** la précédente. `Advance(nextRandomUInt32)` **consomme** la frame : pour chacun des
`ticks`, pour chaque couche — cadence V (`AnimFrameTimer`/`AnimFrameCounter`), avance du `WaveTick`
(byte, enroule mod 256), puis chaque cellule dans l'ordre de définition. `nextRandomUInt32` n'est appelé
que si une cellule `FallRespawn` réapparaît réellement ce tick (voir §6).

Une seule horloge entière ; `ticks = 0` n'avance rien.

## 4. `Normal` — dérive, période, enroulement

```
posX += DX ; posY += DY
si PeriodX != 0 : stepX = ComputePeriodStepOr(DX, PeriodX) ; si tickX++ > |PeriodX| { posX += stepX ; tickX = 0 }
   (idem en Y avec PeriodY/DY/tickY)
baseX = CamXDen != 0 ? cameraX * CamXNum / CamXDen : 0   (idem baseY)
sx = posX - baseX ; drawX = sx ; minX = U0 - U1
   si sx < minX       : posX += 320 - minX
   sinon si sx > 319   : posX += -320 + minX
(idem en Y, 240 et minY = V0 - V1)
```

**Période.** Le pas s'applique quand `|P|` est inférieur au compteur d'**avant** son incrément, puis le
compteur repasse à 0 : un pas tous les `|P| + 2` ticks (`0x8005CC64`/`0x8005CCBC` pour le type 0,
`0x8005D218`/`0x8005D278` pour le type 2). Le défilement automatique de `ScrollingLayerService` garde son
propre rythme, `|P|` ticks (`0x8005C7E0`).

**Position dessinée.** La cellule est dessinée à la position calculée **avant** les enroulements
(`0x8005CCFC`/`0x8005CD00` écrites en `0x8005CDF0`/`0x8005CDF4`) : les enroulements ne déplacent que la
position rangée, donc au tick d'un enroulement la cellule est hors de l'écran et n'apparaît à sa nouvelle
place qu'au tick suivant (ADR-0050).

**Le pas de période est un OU de signes, recalculé à chaque tick** —
`ComputePeriodStepOr(delta, period) = (delta < 0 || period < 0) ? -1 : 1` — **pas** le OU EXCLUSIF
calculé une fois au chargement que `ScrollingLayerService` utilise pour son propre auto-défilement. Les
deux ne divergent que lorsque `delta` et `period` sont **tous les deux négatifs** (OU donne -1, OU
EXCLUSIF donnerait +1) — c'est le seul cas qui les distingue, et c'est le cas épinglé par
`CellularLayerServiceTests.Advance_NormalCell_BothDxAndPeriodXNegative_UsesOrOfSigns_NotTheSiblingsXor`.

## 5. `FallRespawn` — même dérive, pas d'enroulement en Y

Même dérive et même enroulement en X que `Normal`. En Y, à la place de l'enroulement :

```
drawY = sy
si sy > 239 {
    posX = (int)nextRandomUInt32() / 102        (division signée, tronquée vers 0)
    posY += -240 + (V0 - V1)
}
```

La position dessinée (`drawX`, `drawY`) est celle d'avant l'enroulement en X et d'avant la réapparition
(`0x8005D2C0`/`0x8005D2C4` écrites en `0x8005D3AC`/`0x8005D3B0`), comme pour `Normal`.

**Tire le `rand()` de la bibliothèque C de l'original** (ADR-0050 ; `0x80081E6C`,
`s = s * 0x41C64E6D + 0x3039`, rend `(s >> 16) & 0x7FFF`) : `CellularLayerService` ne possède aucun
générateur à lui — `Advance` reçoit la prochaine valeur de ce `rand()` (0 à 0x7FFF) via un délégué
injecté, appelé **une fois** par réapparition et **seulement** quand une cellule réapparaît réellement ce
tick. L'abscisse tirée va de 0 à 321 (`0x8005D324`..`0x8005D340`) ; 320 et 321 sont hors écran, la cellule
s'enroule au tick suivant. `CellularLayerComponent.RandomSource` est le point où la DLL doit brancher son
exemplaire du générateur (propriété réglable, avertit une fois et rend 0 si jamais appelée sans avoir été
câblée).

## 6. `WaveX` — sans état, formule fixe

```
idxA1 = (Y0 * AWaveY) & 0xFF
idxA2 = (WaveTick * AWavePhase) & 0xFF
aW    = WaveLut[idxA1] * WaveLut[idxA2] * AWaveAmp ;  si aW < 0 : aW += 0x7F
idxB  = (Y0 * BWaveY + WaveTick * BWavePhase) & 0xFF
bW    = WaveLut[idxB] * BWaveWeight
tSum  = (aW >> 7) + bW ;                              si tSum < 0 : tSum += 0x7F
x     = X0 + (tSum >> 7) - 8   ;   y = Y0
```

Les deux `+= 0x7F` sont le correctif d'arrondi de la division signée du PSX (un décalage arithmétique
d'un négatif arrondit vers moins l'infini ; le correctif ramène à une troncature vers zéro) — **pas du
bruit**, ne pas les simplifier. `Y0` ne bouge jamais. `CellularLayerService.ComputeWaveDraw` est la
fonction pure qui porte cette formule, testée avec des valeurs calculées à la main pour les deux branches
de correctif (`CellularLayerServicePureFunctionsTests`).

**Si le `WaveLut` de la carte est vide, la cellule `WaveX` est sautée — elle seule.** La garde de
l'original est un `break` **dans un `case` de `switch`** (`GraphicManager.cs:1190-1193`) : il sort du
`switch`, pas de la boucle. Une couche qui mêle des cellules `WaveX` à des `Normal` ou des
`FallRespawn` continue donc d'avancer et de dessiner ces dernières
(`Advance_WaveXCell_WithEmptyWaveLut_SkipsOnlyItself_AndLaterCellsStillAdvance`).

## 7. La fenêtre source est animée

Comme le mécanisme frère (§1.5 bis du plan), la ligne échantillonnée dans la planche 256×256 n'est pas
`V0` seul mais `(V0 + phase) & 0xFF`, avec `phase = (AnimFrameCounter << 8) / AnimNum` — calculée par
couche, par tick, avant la boucle des cellules, et lue par `CellularLayerComponent.Submit` via
`CellularLayerService.ComputeSourceV`.

## 8. `ScriptTrack` — gelé (D5)

`case` au corps vide dans l'original : ne dessine rien, n'avance aucun état. Zéro occurrence sur les
8360 cellules mesurées du corpus. Transcrit comme un `case` explicite qui ne fait rien, avec le
commentaire disant pourquoi — même politique que `ScrollingLayerService`'s D-E9b-14 pour son propre
opcode inaccessible.

## 9. Profondeur (D6)

`CellularLayerComponent.Submit` dérive la passe de rendu depuis `CellularLayerDefinition.Ground`,
exactement la même politique déjà exercée par 86 des 132 couches du mode défilant
(`AlundraBackdropStage.BuildDefinitions`) : `Ground = true` → `RenderPass2D.Effects` (avant-plan, sous
l'UI) ; `Ground = false` → `RenderPass2D.Background`, avec le même recul en Z par
`CellularLayerConfiguration.BackgroundDepth` que `ScrollingLayerConfiguration.BackgroundDepth`
(D-E9c-5). **Les 92 couches cellulaires livrées mesurent toutes `Ground = true`** : la branche
`Background`/recul existe pour la parité d'architecture, mais n'est exercée aujourd'hui par aucune
donnée du corpus.

## 10. Cuisson des planches (D2, convertisseur)

Aucun pixel ne sortait du convertisseur pour le mode cellulaire avant ce chantier. `CellularLayerDefinition.SheetTextureAssetIds`
porte désormais un id de texture par `PalDex` réellement employé par les cellules de la couche (jusqu'à
8 par carte) — la planche 256×256 décodée entière, sans remappage d'UV (`BackdropImageBuilder`,
convertisseur). `CellularLayerComponent.ResolveTextures` les résout dans le même style que le mécanisme
frère résout ses trames de V-animation, sauf qu'il n'y a pas de repli en chaîne : chaque `PalDex` est
indépendant, un id nul ou une texture qui échoue à charger désactive seulement les cellules qui la
référencent, jamais la couche entière.

## 11. Ciseaux et fusion

Même règle que le mécanisme frère : `Submit` ne lit jamais `GraphicsDevice`, le rectangle de ciseaux est
un paramètre résolu une fois par frame par `Update`. Chaque couche porte son propre `SpriteBlendMode`/
teinte (politique DLL), lus directement depuis `CellularLayerDefinition.Blend`/`Tint` — aucune
résolution supplémentaire ici, elle appartient à la DLL.

## 12. Limites connues (V1)

- **Pas d'éditeur, pas de sérialisation** : le format `.backdrop.json` reste lu par la DLL.
- **`CellularLayerComponent.RandomSource` doit être câblé par la DLL** avant qu'une couche `FallRespawn`
  n'atteigne `Advance()` — sans quoi le délégué par défaut avertit une fois et rend 0 (les cellules
  réapparaissent à l'abscisse 0). Une carte sans cellule `FallRespawn` n'atteint jamais ce chemin.
- **Une seule caméra active à la fois**, comme le mécanisme frère.
- **`Submit` ne soumet rien sans poussée reçue** (`FramesPushed == 0`).
- Les 8 champs `Cellular`/16 champs `Cell` sortis par le convertisseur sont copiés 1:1 ; les deux octets
  morts de l'original (`Unused0`/`Unused1`) ne sont pas représentés.

## 13. Masque d'une couche (E19.k2, ADR-0049)

`SetLayerActive(layerId, active)` éteint ou rallume toute couche dont `LayerId` vaut `layerId` (l'identifiant donné
par le jeu, pas la position dans le tableau ; un identifiant absent est sans effet). Une couche éteinte est
**figée** : `Advance` ne touche ni sa cadence, ni son `WaveTick`, ni les positions de ses cellules, et ne tire
aucune valeur du flux aléatoire pour elle ; `CellularLayerComponent.Submit` n'en soumet aucune cellule. `SetLayers`
et `Clear` remettent toutes les couches actives. `IsLayerActive(index)` lit l'état d'une couche par sa position.

Decisions: see [ADR-0049](../decisions/0049-background-layers-can-be-switched-off-by-identifier.md) and [ADR-0050](../decisions/0050-cellular-layers-follow-the-originals-period-drawn-position-and-c-rand.md).
