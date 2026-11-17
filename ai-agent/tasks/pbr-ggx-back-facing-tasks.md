# Plan agent IA — GGX sur une normale qui tourne le dos au demi-vecteur

Plan d'une correction du moteur trouvée par la vérification indépendante de la tranche S1f du chantier RacingGame (`ai-agent/tasks/rgce-pbr-handover-tasks.md` dans le dépôt du jeu, Phase 10). Mode AUTO accordé par l'auteur le 2026-11-17 pour le programme.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`PbrDistributionGgx` redonne la valeur de GGX quand la normale de surface tourne le dos au demi-vecteur (`dot(N, H) < 0`). Aujourd'hui elle peut y exploser et dessiner des traînées blanches saturées sur une surface rugueuse à normal map.

## État vérifié du dépôt (2026-11-17)

- `CasaEngine/Content/Shaders/Pbr.fxh`, `PbrDistributionGgx` : `d = dot(cross(N, H), cross(N, H)) + noH * noH * alpha2`, avec `noH = saturate(dot(N, H))` passé par l'appelant (`AccumulatePbrLight`, et la couche de vernis).
- Le terme croisé vaut `1 - dot(N, H)^2` (identité de Lagrange), donc `1 - noH^2` seulement quand `dot(N, H) >= 0`. Quand `dot(N, H) < 0`, `noH` vaut 0 mais le terme croisé vaut `1 - dot(N, H)^2`, qui tend vers 0 quand `dot(N, H)` tend vers -1 : le dénominateur tend vers 0 et la distribution explose. La formule d'avant (`noH * noH * (alpha2 - 1) + 1`) donnait 1, soit `alpha2 / pi`.
- Introduit par `6ead47308` (« compute the GGX denominator without cancellation »), qui visait la précision près du reflet d'une surface lisse.
- Constaté dans le jeu : terrain `lit-pbr` de rugosité 1 avec normal map, traînées blanches sur les crêtes raides (3145 pixels blancs dans la vue `Beginner-start`, contre 50 avant le passage au PBR) ; elles disparaissent sans la normal map. Sorties : `artifacts/pbr-handover/s1f/verify/exp/` du dépôt du jeu.
- Test de couverture du source : `CasaEngine.Tests/Rendering/PbrShaderCoverageTests.cs` (cite la ligne de `d`). Doc : `docs/engine/pbr-rendering.md`.
- Branche `chantier/pbr-ggx-back-facing`, créée depuis `chantier/texture-asset-mipmaps`, sur laquelle le jeu pointe déjà.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le terme croisé n'est utilisé que pour `dot(N, H) > 0` ; sinon le dénominateur prend `1 - noH^2 = 1`, comme la formule d'avant `6ead47308`. La précision près du reflet est gardée. |
| D2 | Pas de nouvel ADR : correction d'un défaut, sans changement de décision ni de format. |

## Règles d'exécution pour l'agent

- Branche dédiée, jamais de commit sur `main`, jamais de push ; `git add` fichier par fichier ; aucune commande git dans `MGUI` ni `NvgSharp`.
- Builds `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; tests `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- Les deux solutions à 0 erreur ; tests verts hors des 8 tests GPU déjà en échec sur ce PC, total inchangé (6391).
- Empreintes de la démo PBR sur ce PC (`pbrdemo.sh`, tâche T4.1 du plan du jeu : `2d029da5659f2a7e`, `580dca11e3f9c87e` en `Legacy`, `5d5850be6d2181e4` avec vernis) : relancées et notées ; un changement est noté avec l'image avant et après, sans être un échec en soi.
- Dans le jeu (dépôt parent), après avancée du pointeur : rejeu des vues de l'original ; les pixels blancs (trois canaux ≥ 250) de `Beginner-start` et `Expert-start` reviennent au niveau d'avant le passage au PBR (`artifacts/pbr-handover/s1m/cand-a` : 50 et 246), à 10 % près ou en dessous.

Retour arrière : `git switch chantier/texture-asset-mipmaps` puis `git branch -D chantier/pbr-ggx-back-facing`, sur demande de l'auteur.

---

### ⏳ T1 — Dénominateur de GGX côté dos

- Fichiers : `CasaEngine/Content/Shaders/Pbr.fxh` (fonction et commentaire), `CasaEngine.Tests/Rendering/PbrShaderCoverageTests.cs` (ligne attendue, et une assertion sur le test de signe), `ClearCoatShaderCoverageTests.cs` si la ligne de la couche de vernis change, `docs/engine/pbr-rendering.md` si elle décrit le dénominateur, ce plan.
- Validation : validation globale.
- Commit : `fix(shaders): keep GGX finite when the normal faces away from the half vector`

## Points ouverts

- O1 : remontée dans `main` et push, avec les deux branches sur lesquelles celle-ci repose : décision de l'auteur.
