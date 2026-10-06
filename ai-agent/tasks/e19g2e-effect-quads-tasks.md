# Plan agent IA — Service et composant des quads d'effets (E19.g G2e)

Plan d'exécution de la partie moteur de la tranche E19.g G2e du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, section « E19.g G2e —
Service des quads d'effets (moteur) », dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent relu READY sur deux angles ;
travail dans le moteur autorisé par l'auteur le 2026-10-03.** Les décisions viennent du plan parent (règles G2E-R1 à R5) : **ce plan les
applique, il ne les rediscute pas.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le jeu pousse à chaque image dessinée une liste de quads libres PS1 (planche, fenêtre brute, quatre coins du monde, mode PSX, clé de tri) et le
moteur les dessine par `SpriteRendererComponent.DrawPsxQuad` (ADR-0068) : `EffectQuad`, `EffectQuadService` (sans GPU), `EffectQuadComponent`
(ADR-0071). Consommateur : l'étape G3c de la DLL Alundra.

## Hors périmètre

- La DLL Alundra qui construit et pousse les quads (G3c), l'export, le convertisseur.
- `DrawPsxQuad` et `PsxQuad.fx` (ADR-0068) : inchangés.

## État vérifié du dépôt (2026-10-07)

- Branche `chantier/e19-suite2` (réutilisée, pas créée), pointe `dd1f8afa`. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la modifier.
- ADR : le plus grand numéro de toutes les branches locales est 0070 (`chantier/demo-browser`), donc **0071**.
- La prévision de la démo (G2e-0) est commitée dans le parent (`6072de4`) : `docs/plan-e19-g3c-annexe/g2e_predict.py`, `g2e_expected.json`,
  `g2e_compare.py`.

## Règles d'exécution pour l'agent

- Commits sur `chantier/e19-suite2` uniquement ; jamais sur `main`. Une seule tâche à la fois, statut mis à jour dans le même commit.
- Tests d'abord : rouge constaté, puis vert. **Ne jamais pousser, ne jamais merger.**
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement. Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Release` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build -c Release --blame-hang-timeout 300s` tout vert (aucun test existant ne bouge).
- La démo `Effect quads` (répertoire courant `CasaEngine.Demos/`) : quatre scènes × facteurs 1 et 3, chaque vidage comparé par
  `g2e_compare.py` (sortie 0).

## Tâches

### ✅ G2e-1 — Tests d'abord

- Fichiers : `CasaEngine.Tests/Rendering/EffectQuads/EffectQuadServiceTests.cs`, `EffectQuadComponentSubmissionTests.cs`, types vides
  (`EffectQuad`, `EffectQuadService`, `EffectQuadComponent`) dont les corps lèvent `NotImplementedException`.
- Validation faite : 22 tests rouges sur 22 (8 du service, 14 du composant).
- Commit : `test(rendering): effect quad service and component tests, red on empty types (E19.g G2e)` (`87bc8e6d`)

### ✅ G2e-2 — Code, démo, doc, ADR

- Fichiers : `EffectQuadService.cs`, `EffectQuadComponent.cs`, `ComponentOrder.cs` (`EffectQuads`), `CasaEngineGame.cs` (propriété et création),
  `CasaEngine.Demos/Demos/PsxSemiTransparency/EffectQuadDemo.cs` et `DemosGame.cs`, `docs/engine/effect-quads.md`, ADR-0071, index.
- Validation faite : 22 tests verts ; la mutation « `ResolveTextures` n'enregistre pas la version » fait échouer 3 tests ; suite complète
  `CasaEngine.Tests` en Release : 4148 verts (4126 + 22), aucun test existant ne bouge. Démo : 8 vidages (4 scènes × k = 1 et 3), `g2e_compare.py`
  sort 0 pour chacun.
- Commit : `feat(rendering): effect quad service and component draw the quads the game pushes (E19.g G2e)`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
