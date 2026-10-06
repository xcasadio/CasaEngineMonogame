# Plan agent IA — Quad libre PS1 du moteur (quatre coins libres, effet à la résolution de l'écran)

Plan d'exécution de la partie moteur de la tranche E19.g G2b-1 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2o.5,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2o.5 READY (relecture de clôture) et approuvé par l'auteur le
2026-10-06 ; travail dans le moteur autorisé par l'auteur le 2026-10-03.** L'auteur a tranché O-E19-71 (résolution de l'écran, D-E19-92). Les
décisions ci-dessous viennent du plan parent (règles G2b1-R1 à R6, tâches G2b1-0 à G2b1-4) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`SpriteRendererComponent.DrawPsxQuad` soumet un quad de la PS1 à quatre sommets libres (échelle, miroir, parallélogramme, quad quelconque),
dessiné avec la règle de la PS1 à la résolution de l'écran (ADR-0068) : une entrée par quad (deux pour un mode PSX, règle d'ADR-0051),
sommets écrits dans les cases TR, BR, BL, TL du lot (diagonale TR-BL de la PS1), séries sans élimination des faces, effet `PsxQuad.fx`
(couverture par le coin haut-gauche du pixel d'écran, texel choisi par le shader).

## Hors périmètre

- Le convertisseur, la piste de coins de `.anim2d`, la DLL (G2b-2, G2b-3, G2e).
- `SpriteBatch.fx` et son rechargement, les tuiles, les fonds, `DrawDirectly` : aucun chemin existant ne change.
- La scène dessinée dans une cible de 320 × 240 (option C d'O-E19-71, écartée par l'auteur).

## État vérifié du dépôt (2026-10-06)

- Branche `chantier/e19g2b-free-quads` créée par la règle R-BR (1) depuis `face4b1e`, le commit que le parent épingle (pointe de
  `chantier/e19g2d-adr-renumber`). Modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la
  modifier.
- ADR : le plus grand numéro trouvé (`main`, `chantier/audio-modern`, `chantier/e19*`) est 0067, donc **0068**.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19g2b-free-quads`**. Ne jamais committer sur `main`.
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
- Démo `PSX free quads` aux facteurs 1 et 3 : tampon d'image égal à la prévision `docs/plan-e19-g2b-annexe/g2b1_predictions.json` du parent
  (`g2b1_compare.py`), 0 différence.

---

## Phase 1 — Moteur

### ✅ E1 — Soumission, lot et effet (tests d'abord)

- Objectif : G2b1-R1 à R6, G2b1-1.
- Fichiers : `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`, `CasaEngine/Content/Shaders/PsxQuad.fx`,
  `CasaEngine/Content/Content.mgcb`, `CasaEngine.Tests/Rendering/SpriteRendererComponentPsxQuadTests.cs`.
- Valeurs de test (écrites d'avance), au niveau du lot : coins (10, 20), (30, 22), (12, 2), (34, 0) sur une texture de 16 × 16 et la fenêtre
  (2, 3, 8, 6) → cases TR, BR, BL, TL, décalages (8,5 ; 11), (12,5 ; −11), (−9,5 ; −9), (−11,5 ; 9) depuis le centre (21,5 ; 11) ;
  coordonnées de texture `(coin + 0,5 + 1/4096) / 16` ; deux entrées par mode de même clé, fenêtres 0,75-1 puis 0,25-0,75.
- Validation faite : rouge d'abord, `DrawPsxQuad` présent et vide : 11 tests rouges sur 12 (« Expected: 4, Actual: 0 » : file vide) ; vert
  après, `SpriteRendererComponent*` 36 sur 36, aucun test existant touché ; mutation « sans epsilon » : 2 tests rouges.
- Commit : `feat(rendering): DrawPsxQuad queues a free PS1 quad drawn by the PsxQuad effect at the screen resolution (E19.g G2b-1)`

### ✅ E2 — Démo sur GPU

- Objectif : G2b1-2. Démo `PSX free quads` (`CasaEngine.Demos/Demos/PsxSemiTransparency/PsxFreeQuadDemo.cs`), caméra à `Zoom` = k
  (`CASAENGINE_PSXQUAD_ZOOM`), tampon d'image écrit dans `CASAENGINE_PSXQUAD_DUMP_PATH` ; comparaison par `g2b1_compare.py` du parent.
- Validation faite : × 1 : 3160 pixels couverts égaux à B(1), 88 sondes, 0 différence ; × 3 : 28 488 pixels, 106 sondes, 0 différence ; lignes
  1:1 (a) = (b) aux deux facteurs. Première exécution : × 3 faux sur y (signe de `ddy` sous OpenGL) ; corrigé dans le shader (signe pris sur
  la rangée d'écran). Exécution rouge (`DrawPsxQuad` réduit au rectangle englobant) : 2728 puis 23 441 différences.
- Commits : `fix(rendering): the PsxQuad effect takes the screen-row derivative with the sign of the back end (ddy points up under OpenGL)`,
  `feat(demos): PSX free quads demo, dumps its back-buffer for the comparison with the prediction (E19.g G2b-1)`

### ✅ E3 — Documentation et ADR

- Objectif : G2b1-3. `docs/engine/sprite-psx-semi-transparency.md` (section « Free quads », la ligne « not covered » remplacée), ADR-0068
  (`Accepted`, en anglais) et sa ligne d'index, ce fichier et sa ligne dans `ai-agent/README.md`.
- Commit : `docs(adr): ADR-0068 free PSX quads draw at the screen resolution`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
