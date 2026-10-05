# Plan agent IA — Semi-transparence PSX par texel des couches de fond (couches de défilement et cellulaires)

Plan d'exécution de la partie moteur de la tranche E19.g G2c du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2o.3,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2o.3 READY le 2026-10-05, travail dans le moteur autorisé
par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent (D-E19-68, règle G2c-R2, tâche G2c-1) : **ce plan les
applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`ScrollingLayerDefinition` et `CellularLayerDefinition` portent un mode de semi-transparence PSX (`SpritePsxSemiTransparency`, défaut
`None`). Quand il n'est pas `None`, les composants de couches soumettent chaque quad ou cellule en deux entrées de même clé de tri sur
deux fenêtres d'alpha brut disjointes (texels opaques, puis texels STP à l'état de mélange du mode), comme les sprites d'entités de G2a
(ADR-0051) ; le `Blend` de la couche est alors ignoré.

## Hors périmètre

- La surcouche de teinte (`ScrollingLayerService.Tint`), le calcul des clés de tri et la clé par cellule d'E19.m3 : inchangés.
- Le convertisseur et la DLL du parent (G2c-2, G2c-3, G2c-4).
- La surcharge `DrawSprite(Sprite, ...)` n'est pas redirigée sur la nouvelle surcharge (elle garde son `drawDebug`).

## État vérifié du dépôt (2026-10-05)

- Branche `chantier/e19g2c-backdrop-stp` créée depuis `chantier/e19m3-cellular-order` (`a885f226`), qui porte G2a (ADR-0051,
  `chantier/e19g2a-psx-semi`). Modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais
  la modifier.
- `SpriteRendererComponent` : le cœur privé de `DrawSprite` prend déjà `alphaMin`/`alphaMax` ; la surcharge `Sprite` à mode PSX pose les
  deux entrées ; les surcharges publiques `Texture2D` à clé de tri n'ont pas de mode PSX.
- `ScrollingLayerComponent.Submit` et `CellularLayerComponent.Submit` appellent la surcharge `Texture2D` + ciseaux +
  `SpriteBlendMode` avec `definition.Blend` (une entrée, fenêtre neutre).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| G2c-R2 | Champ public `SpritePsxSemiTransparency PsxSemiTransparency` (défaut `None`) sur `ScrollingLayerDefinition` et `CellularLayerDefinition` ; nouvelle surcharge **interne** de `DrawSprite` (`Texture2D`, rectangle source, origine, position, rotation, échelle, couleur, z, clé de tri, effets, ciseaux, mode PSX) : `None` → une entrée identique à celle de la surcharge existante à `SpriteBlendMode.Opaque` ; un mode → les deux entrées de G2a (opaque `(0,75 ; 1]` état opaque ; STP `(0,25 ; 0,75]` état du mode, couleur (64, 64, 64) pour `Mode3`), même clé et même z. `Submit` des deux composants l'appelle quand le champ n'est pas `None` (`Blend` ignoré), l'appel existant sinon. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19g2c-backdrop-stp`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté (valeurs lues),
  puis vert ; le rouge se lit sur le code d'avant avec la surface d'API minimale ajoutée sans comportement.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- Une valeur écrite d'avance que la mesure contredit est un ARRÊT ; un test existant qui change ou rougit aussi.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Démo : lancée depuis `CasaEngine.Demos/` ; texture chargée depuis un PNG par le chargeur du moteur ; capture du back-buffer en
  processus (`GetBackBufferData`), jamais de capture du bureau.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Démo : captures du back-buffer égales aux valeurs ci-dessous à ±1 par canal (RVB).

---

## Phase 1 — Moteur

### ✅ E1 — Champ des définitions, surcharge interne, soumission des couches (tests d'abord)

- Objectif : G2c-R2.
- Fichiers : `CasaEngine/Framework/Rendering/ScrollingLayers/ScrollingLayerDefinition.cs`,
  `CasaEngine/Framework/Rendering/CellularLayers/CellularLayerDefinition.cs`,
  `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`, `ScrollingLayerComponent.cs`, `CellularLayerComponent.cs`,
  tests dans `CasaEngine.Tests/Rendering/ScrollingLayers/` et `CasaEngine.Tests/Rendering/CellularLayers/`.
- Valeurs de test (écrites d'avance), au niveau de la file (comme G2a) :
  - couche de défilement d'un quad en `Mode0` : **2** entrées de même clé : [`Opaque`, fenêtre (0,75 ; 1], blanc] puis [`AlphaBlend`,
    (0,25 ; 0,75], blanc] (aujourd'hui 1 entrée, `definition.Blend`, fenêtre (−1 ; 2]) ; `Mode1` → STP additif ; `Mode2` → STP
    soustractif ; `Mode3` → STP additif avec (64, 64, 64) ; `None` inchangé (une entrée, `definition.Blend`, fenêtre neutre) ;
  - couche cellulaire de 3 cellules en `Mode0` : **6** entrées, les entrées 2c et 2c+1 avec `LocalSortOffset` −c (aujourd'hui 3) ;
    `None` inchangé (une entrée par cellule, `definition.Blend`) ;
  - le `Blend` de la couche est ignoré quand un mode est posé (couche à `Blend = Additive`, `Mode0` : entrée opaque `Opaque`, entrée
    STP `AlphaBlend`) ; la surcharge `Sprite` n'est pas redirigée (`SpriteRendererComponentPsxSemiTransparencyTests` reste à une entrée).
- Validation faite : rouges d'abord sur le code d'avant avec la surface d'API ajoutée sans comportement (champ sur les deux
  définitions, surcharge interne qui ignore le mode, `Submit` qui l'appelle) : 10 tests rouges sur 12 ; valeurs lues égales aux valeurs
  écrites d'avance : couche de défilement en `Mode0` à `Mode3` (4 cas) et `Mode0` à `Blend = Additive` : 1 entrée là où 2 sont
  attendues ; couche cellulaire de 3 cellules en `Mode0` à `Mode3` (4 cas) et `Mode0` à `Blend = Additive` : 3 entrées là où 6 sont
  attendues ; les cas `None` (une entrée, `definition.Blend`, fenêtre neutre) étaient verts d'avance (gardes). Verts après : 12 tests
  ajoutés (`ScrollingLayerPsxSemiTransparencyTests` 6, `CellularLayerPsxSemiTransparencyTests` 6, théories comptées par cas),
  `CasaEngine.Tests` 2562/2562, aucun test existant touché (`SpriteRendererComponentPsxSemiTransparencyTests` reste à une entrée pour
  la surcharge `Sprite` sans mode).
- Commit : `feat(rendering): background layers draw per-texel PSX semi-transparency as two disjoint passes`

### ✅ E2 — Démo du moteur (texture chargée depuis un PNG, sonde du back-buffer)

- Objectif : preuve sur périphérique, avec le chargeur de texture du moteur (hypothèse à prouver : un PNG à alpha 128 garde son alpha).
- Valeurs attendues (fond (100, 150, 200), ±1 par canal, pixels loin des bords) : texel (96, 96, 88, α255) d'une couche `Mode0` →
  (96, 96, 88) ; texel (24, 32, 24, α128) d'une couche `Mode0` → (62, 91, 112) ; texel (96, 96, 96, α128) d'une couche `Mode1` →
  (196, 246, 255) ; texel (120, 80, 40, α128) d'une couche `None` → (120, 80, 40).
- Validation faite : démo `Background layers PSX semi-transparency`
  (`CasaEngine.Demos/Demos/PsxSemiTransparency/BackdropLayersPsxSemiTransparencyDemo.cs`, feuilles PNG générées dans
  `CasaEngine.Demos/Content/PsxBackdropLayers/`, chargées par `Texture2DLoader.LoadAsset`, c'est-à-dire `Texture2D.FromStream` : l'alpha
  128 est gardé, sans prémultiplication), quatre couches de défilement (fond, `Mode0`, `Mode1`, sans mode) et une couche cellulaire
  `Mode0` à `Blend = Additive` ; sonde `BackBufferProbe` (`GetBackBufferData` en processus), lancée depuis `CasaEngine.Demos/` (Debug,
  1024 × 768). Pixels lus égaux aux valeurs écrites d'avance, 6 contrôles sur 6 : texel (96, 96, 88, α255) d'une couche `Mode0` →
  (96, 96, 88) ; texel (24, 32, 24, α128) d'une couche `Mode0` → (62, 91, 112) (alpha de back-buffer 191) ; texel (96, 96, 96, α128)
  d'une couche `Mode1` → (196, 246, 255) ; texel (120, 80, 40, α128) d'une couche sans mode → (120, 80, 40) ; la couche cellulaire
  `Mode0` → (62, 91, 112) ; fond voisin (100, 150, 200). Rouge d'abord, la soumission des couches rendue aveugle au mode
  (`if (false && ...)`, rétabli après) : texel `Mode0` STP lu (24, 32, 24), `Mode1` STP lu (96, 96, 96), cellulaire lu (124, 182, 224)
  (le `Blend` additif de la couche, qui est ignoré quand un mode est posé), 3 contrôles sur 6 rouges. Image
  `scratchpad/e19g2c-exec/demo-out/backdrop-layers.png`, lectures `backdrop-layers.txt` (vert) et `backdrop-layers-red.txt` (rouge).
- Commit : `feat(demos): per-texel PSX semi-transparency of background layers demo`

### ✅ E3 — Documentation et ADR

- Objectif : docs `scrolling-layers.md`, `cellular-layers.md`, `sprite-psx-semi-transparency.md`, commentaire de
  `CellularLayerDefinition` ; ADR du moteur (prochain numéro libre, `Accepted`, D-E19-68, étend ADR-0051 aux couches de fond) et sa
  ligne d'index.
- Validation faite : ADR-0053 (`Accepted`, en anglais) et sa ligne à l'index ; `docs/engine/sprite-psx-semi-transparency.md` (section
  « Background layers »), `scrolling-layers.md` §6 et `cellular-layers.md` §11 (en français, langue de ces fichiers) y renvoient ; commentaire de
  `CellularLayerDefinition`.
- Commit : `docs(adr): ADR-0053 background layers draw per-texel PSX semi-transparency`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
