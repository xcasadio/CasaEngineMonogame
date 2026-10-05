# Plan agent IA — Surcouche des fonds au mode du binaire (teinte plein écran à mode PSX)

Plan d'exécution de la partie moteur de la tranche E19.g G2d du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2o.4,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2o.4 READY (relecture n°2) le 2026-10-05, travail dans le
moteur autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent (O-E19-58, règle G2d-R1, tâche G2d-1) :
**ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`ScrollingTintDefinition` (la teinte plein écran des couches de défilement) porte un mode de semi-transparence PSX
(`SpritePsxSemiTransparency`, défaut `None`) par un nouveau constructeur à trois arguments ; celui à deux arguments garde
l'entrée d'aujourd'hui. `ScrollingLayerComponent.Submit` soumet pour un mode autre que `None` **une** entrée de même clé, même z et
fenêtre neutre, à l'état de mélange du mode :

| Mode | `SpriteBlendMode` | Couleur de l'entrée |
|---|---|---|
| `Mode0` | `AlphaBlend` | (R, G, B, 128) |
| `Mode1` | `Additive` | (R, G, B, 255) |
| `Mode2` | `Subtractive` | (R, G, B, 255) |
| `Mode3` | `Additive` | chaque canal × 64/255 arrondi au plus proche (13 pour 50), alpha 255 (inutilisé dans les données, choix de la session, même facteur que `Mode3` de G2a) |
| `None` | `AlphaBlend` | la couleur telle quelle (aujourd'hui) |

Le chemin à deux entrées des couches (G2a, G2c) ne convient pas à la teinte : un pixel blanc d'alpha 255 ferait dessiner l'entrée
opaque en opaque.

## Hors périmètre

- Le convertisseur et la DLL du parent (G2d-2, G2d-3, G2d-4).
- La démo de G2c `Background layers PSX semi-transparency` : non modifiée (ses six contrôles inchangés).
- Les tests existants du moteur, qui utilisent le constructeur à deux arguments.

## État vérifié du dépôt (2026-10-06)

- Branche `chantier/e19g2d-overlay-blend` créée depuis `main` (`ebeb81c9`). Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la modifier.
- `ScrollingLayerComponent.Submit` soumettait la teinte en `AlphaBlend` avec la couleur de la définition, sans mode.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19g2d-overlay-blend`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté (valeurs lues),
  puis vert ; le rouge se lit sur le code d'avant avec la surface d'API minimale ajoutée sans comportement.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- Une valeur écrite d'avance que la mesure contredit est un ARRÊT ; un test existant qui change ou rougit aussi.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Démos : lancées depuis `CasaEngine.Demos/` ; capture du back-buffer en processus (`GetBackBufferData`), jamais de capture du bureau.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Démos : lectures du back-buffer égales aux valeurs ci-dessous à ±1 par canal (RVB).

---

## Phase 1 — Moteur

### ✅ E1 — Mode de la teinte, soumission (tests d'abord)

- Objectif : G2d-R1.
- Fichiers : `CasaEngine/Framework/Rendering/ScrollingLayers/ScrollingTintDefinition.cs`,
  `CasaEngine/Framework/Application/Components/ScrollingLayerComponent.cs`,
  `CasaEngine.Tests/Rendering/ScrollingLayers/ScrollingLayerPsxSemiTransparencyTests.cs`.
- Valeurs de test (écrites d'avance), au niveau de la file : teinte (50, 0, 0, 255) en `Mode1` → une entrée `Additive`, (50, 0, 0, 255) ;
  (40, 40, 40, 255) en `Mode0` → `AlphaBlend`, (40, 40, 40, 128) ; `Mode2` → `Subtractive` ; `Mode3` (50, 0, 0) → `Additive`,
  (13, 0, 0, 255) ; constructeur à deux arguments → l'entrée d'aujourd'hui (`AlphaBlend`, couleur telle quelle) ; la clé de tri est gardée.
- Validation faite : rouges d'abord sur le code d'avant avec le constructeur à trois arguments ajouté sans comportement : 4 tests
  rouges sur 6 ; valeurs lues : `Mode1` et `Mode3` `AlphaBlend` là où `Additive` est attendu, `Mode2` `AlphaBlend` là où `Subtractive` est
  attendu, `Mode0` couleur (40, 40, 40, 255) là où (40, 40, 40, 128) est attendu ; les cas constructeur à deux arguments et clé de tri
  étaient verts d'avance (gardes). Verts après : `ScrollingLayer*` 80/80, aucun test existant touché.
- Commit : `feat(rendering): the scrolling-layer tint carries a PSX semi-transparency mode`

### ✅ E2 — Démos du moteur (une par mode, sonde du back-buffer)

- Objectif : preuve sur périphérique de G2d-R1. Deux classes de démo sœurs de celle de G2c (`BackgroundTintPsxMode1Demo`,
  `BackgroundTintPsxMode0Demo`, base commune `BackgroundTintPsxDemoBase`), chacune avec la couche de fond unie de G2c
  (`Content/PsxBackdropLayers/background.png`, (100, 150, 200), passe `Background`) et une teinte.
- Valeurs attendues (±1 par canal, couche aux points (48, 48), (160, 120), (300, 220)) : teinte (50, 0, 0) en `Mode1` → (150, 150, 200) ;
  teinte (40, 40, 40) en `Mode0` → (70, 95, 120).
- Validation faite : démos `Background tint PSX mode 1` et `Background tint PSX mode 0`
  (`CasaEngine.Demos/Demos/PsxSemiTransparency/BackgroundTintPsx*.cs`), lancées depuis `CasaEngine.Demos/` (Debug, 1024 × 768),
  `CASAENGINE_START_DEMO` puis `CASAENGINE_DEMO_PIXELS_PATH`. Pixels lus égaux aux valeurs écrites d'avance, 3 contrôles sur 3 par démo :
  `Mode1` → (150, 150, 200) alpha 255 aux trois points ; `Mode0` → (70, 95, 120) alpha 191 aux trois points. Rouge d'abord, la
  soumission de la teinte rendue aveugle au mode (`switch (SpritePsxSemiTransparency.None)`, rétabli après) : `Mode1` lu (50, 0, 0),
  `Mode0` lu (40, 40, 40), 6 contrôles sur 6 rouges. La démo de G2c, non modifiée, passe encore ses 6 contrôles sur 6. Lectures et images
  dans `scratchpad/e19g2d-exec/demo-out/`.
- Commit : `feat(demos): background tint PSX mode demos`

### ✅ E3 — Documentation et ADR

- Objectif : `docs/engine/scrolling-layers.md` (§6), `docs/engine/sprite-psx-semi-transparency.md` (section « Background layers ») ;
  ADR 0056 du moteur (`Accepted`, étend ADR-0053 à la teinte) et sa ligne d'index.
- Validation faite : ADR-0056 (`Accepted`, en anglais) et sa ligne à l'index ; `scrolling-layers.md` §6 (en français) et
  `sprite-psx-semi-transparency.md` (en anglais) décrivent la teinte à mode ; commentaire de `ScrollingTintDefinition` mis à jour.
- Commit : `docs(adr): ADR-0056 the scrolling-layer tint draws with the PSX mode of the map`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
