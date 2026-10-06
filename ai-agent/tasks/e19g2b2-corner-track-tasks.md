# Plan agent IA — Piste de coins de `.anim2d` (une partie dessinée comme un quad libre)

Plan d'exécution de la partie moteur de la tranche E19.g G2b-2 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2o.5,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2o.5 G2b-2 READY (relecture de clôture) et approuvé par l'auteur le
2026-10-06 ; travail dans le moteur autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent (règles
G2b2-R1 à R4, tâches G2b2-1 à G2b2-3) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`.anim2d` porte une piste `Corners` (clés `corner_keyframes`) : une partie aux coins actifs est dessinée comme un quad libre par
`SpriteRendererComponent.DrawPsxQuad` (ADR-0068), les coins étant des décalages en pixels depuis la position de la partie, multipliés par
l'échelle de l'entité ; la rotation, les retournements de la partie et le `SpriteEffect` du composant sont ignorés pour elle (ADR-0069).
Un fichier sans la piste se relit et se réécrit à l'octet.

## Hors périmètre

- Le convertisseur qui écrit la piste, l'export, la DLL (G2b-3, G2e).
- La frise de l'éditeur (la piste n'y est pas montrée), `AnimationAssetDataConverter` et `AnimationClipAsset` (clips 3D).
- `DrawPsxQuad`, `PsxQuad.fx` (G2b-1, ADR-0068) : inchangés.

## État vérifié du dépôt (2026-10-06)

- Branche `chantier/e19-suite2` (demande de l'auteur après le merge du 2026-10-06 : elle tient lieu de `chantier/e19g2b2-corner-track`),
  partie de `19aa21fe`, le commit que le parent épingle. Modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` : ne jamais
  l'indexer, ne jamais la modifier.
- ADR : le plus grand numéro trouvé (toutes les branches locales) est 0068, donc **0069**.

## Règles d'exécution pour l'agent

- Commits sur `chantier/e19-suite2` uniquement ; jamais sur `main`. Une seule tâche à la fois, statut mis à jour dans le même commit.
- Tests d'abord : rouge constaté (API présente, comportement absent), puis vert. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement. Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (aucun test existant ne bouge).

## Tâches

### ✅ E1 — Tests d'abord, puis modèle, échantillonneur, dessin

- Objectif : G2b2-R1 à R4, G2b2-1, G2b2-2.
- Fichiers : `Animation2dTrackData.cs`, `Animation2dCompositionAdapter.cs`, `Animation2dCompositionSampler.cs`,
  `Animation2dPartRuntimeState.cs`, `Animation2dData.cs`, `Animation2dBoundsCalculator.cs`, `AnimatedSpriteComponent.cs`,
  `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs` ; tests
  `Animation/Animation2dCornerTrackTests.cs`, `EditorServices/Animation2dCornerTrackSerializationTests.cs`, fixture
  `Animation/Fixtures/bankalundra_0_anim1_down.anim2d` (copie à l'octet du fichier exporté du parent).
- Validation faite : rouge d'abord, membre `Corners`, type, liste et champs d'état ajoutés sans comportement : 17 tests rouges sur 21
  (« Animation2d track property 'Corners' is not supported », durée attendue 0,9 lue 0,5, « Expected: 2, Actual: 0 ») ; les 4 verts d'avance
  sont des gardes (réécriture à l'octet d'un fichier réel **vérifiée avant le changement**, listes vides non écrites, durée inchangée).
  Vert après : 21 sur 21 ; la réécriture à l'octet tient encore.
- Commit : `feat(animation): .anim2d corner track draws a part as a free PSX quad (E19.g G2b-2)`

### ✅ E2 — Documentation et ADR

- Objectif : docs (`docs/engine/animation2d-composed-format-v1.md`, `docs/editor/animation2d_editor_casaengine.md`), ADR-0069 (`Accepted`, en
  anglais) et sa ligne d'index, ce fichier et sa ligne dans `ai-agent/README.md`.
- Commit : `docs(adr): ADR-0069 the .anim2d corner track draws a part as a free PSX quad`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
