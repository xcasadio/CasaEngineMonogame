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

Pointeur du jeu : la session principale l'avance dans le dépôt du jeu par un commit de la tranche S1f du plan du jeu (correction de la vérification T10.3), après le commit de T1 et seulement si la validation du jeu ci-dessus tient. Avant cela, le rejeu du jeu se fait sur l'arbre de travail, sans commit dans le jeu.

Retour arrière, sur demande de l'auteur : d'abord, dans le jeu, remettre le pointeur `CasaEngine` sur `2b64b0591` (commit de retour ou `git checkout` du sous-module puis commit) ; ensuite, dans le moteur, `git switch chantier/texture-asset-mipmaps` puis `git branch -D chantier/pbr-ggx-back-facing`.

Budget : une passe d'implémentation et au plus une passe de correction.

Arrêts (tâche en ⚠️ Blocked, question notée) :
- après D1, les pixels blancs du jeu restent au-dessus de la cible : aucun autre terme du BRDF (visibilité de Smith, Fresnel, vernis) n'est modifié ;
- le shader ne compile plus ;
- le nombre total de tests change, ou un test autre que les 8 connus échoue ;
- une modification hors des fichiers de T1 serait nécessaire.

---

### ⚠️ T1 — Dénominateur de GGX côté dos

> Bloquée le 2026-11-17, budget épuisé (une implémentation, une correction), aucun commit de code ; `Pbr.fxh` et le test remis dans leur état.
> - Passe 1, forme conditionnelle (`dot(N, H) > 0 ? terme croisé : 1`) : builds à 0 erreur ; tests 6391 lancés, 6377 verts, 14 en échec : les 8 connus et 6 nouveaux, tous des tests GPU de `lit-pbr` (`LitPbrTransparentGpuTests` : `SpecularSurface_KeepsItsSpecular_ItIsNotAttenuatedByTheAlpha`, `DiffuseSurface_IsAttenuatedByTheAlpha_OverTheBackground` ; `LitPbrDetailTextureGpuTests` : `ADarkDetailTexture_DarkensTheBaseColorByTwiceItsValue`, `WithoutADetailTexture_TheImageIsTheSameWhetherOrNotOneWasSetAndCleared` ; `LitPbrClearCoatGpuTests` : `ClearCoatZero_RendersLikeAMaterialWithoutACoat_WhateverTheCoatRoughness`, `FullClearCoat_OnAWhiteDiffuseSeenHeadOn_LowersTheDiffuseByAboutFourPercent`). Sorties : `artifacts/pbr-handover/s1f/ggx/` du dépôt du jeu.
> - Passe 2, forme sans conditionnelle (`step`) : les mêmes 6 échecs.
> - Sur le shader d'origine, les mêmes tests passent (151 sur 151 du groupe `lit-pbr`), avant comme après les deux passes.
> - Mesure qui contredit la logique attendue : dans `ClearCoatZero_…`, cas de face (N = H, `dot(N, H)` = 1), le spéculaire de base vaut 0 au lieu de 0,003183 ; les deux formes devraient y redonner exactement la valeur d'avant. Le défaut n'est donc pas dans le calcul lui-même.
> - Hypothèse non vérifiée : la fonction est appelée trois fois dans les techniques les plus chargées (base, transparente, vernis, détail) ; quelques instructions de plus pourraient dépasser une limite de la chaîne OpenGL (MojoShader) pour ces techniques, qui dessineraient alors du noir. Les 6 tests en échec sont justement ceux de ces techniques.
> - Piste pour la reprise, à faire valider : une forme qui coûte moins d'instructions que l'actuelle, `d = (1 - noH) * (1 + noH) + noH * noH * alpha2`, exacte côté dos (`noH` vaut 0) et sans la soustraction qui annulait les chiffres (`1 - noH` est exact près de 1) ; à mettre en regard de la raison de `6ead47308` (deux lancements différaient de quelques niveaux sur une surface très lisse). Vérifier d'abord l'hypothèse de la limite d'instructions.
> - Question à l'auteur : reprendre avec cette piste (nouvelle passe hors du budget de ce plan), ou laisser le défaut du terrain en l'état jusqu'à une décision ?

- Fichiers : `CasaEngine/Content/Shaders/Pbr.fxh` (fonction et commentaire), `CasaEngine.Tests/Rendering/PbrShaderCoverageTests.cs` (ligne attendue, et une assertion sur le test de signe), `ClearCoatShaderCoverageTests.cs` si la ligne de la couche de vernis change, `docs/engine/pbr-rendering.md` si elle décrit le dénominateur, ce plan.
- Validation : validation globale.
- Commit : `fix(shaders): keep GGX finite when the normal faces away from the half vector`

## Points ouverts

- O1 : remontée dans `main` et push, avec les deux branches sur lesquelles celle-ci repose : décision de l'auteur.

---

## Reprise demandée par l'auteur (2026-11-17)

Réponse à O12 du plan du jeu : « Reprendre la correction ». Nouvelle passe, hors du budget de T1.

### ⏳ T2 — Dénominateur sans produit vectoriel

- Décision D3 : `d = (1 - noH) * (1 + noH) + noH * noH * alpha2`, avec `noH = saturate(dot(N, H))` déjà calculé par l'appelant. Côté dos, `noH` vaut 0 et `d` vaut 1, comme avant `6ead47308`. Près du reflet, `1 - noH` est exact en flottant (lemme de Sterbenz pour `noH` entre 0,5 et 1), mais `noH` lui-même garde l'erreur d'arrondi de `dot(N, H)` (au moins un ulp près de 1) : `d` vaut environ `2 (1 - noH)` près du reflet, comme dans la forme d'avant `6ead47308`, et reste sensible à cette erreur sur une surface très lisse (de l'ordre de 6 % de D au plancher de rugosité 0,045). D3 supprime l'explosion côté dos ; son effet sur le déterminisme visé par `6ead47308` n'est pas établi par le calcul et se mesure (contrôle ci-dessous). La forme coûte moins d'instructions que celle d'aujourd'hui (plus de produit vectoriel ni de `dot` en plus), ce qui teste aussi l'hypothèse de la limite d'instructions de T1.
- Fichiers : ceux de T1 ; `PbrDistributionGgx` garde sa signature (les arguments `normal` et `halfVector` restent, inutilisés ou retirés avec leurs trois appels si le compilateur l'exige ; tout retrait est noté).
- Validation :
  - les deux solutions à 0 erreur ; tests 6391, seuls les 8 échecs connus ;
  - contrôle, avant la mesure de D3 : dans un arbre de travail du moteur non committé, la forme d'avant `6ead47308` (`noH * noH * (alpha2 - 1) + 1`) ; `pbrdemo.sh` lancé trois fois dans la variante par défaut ; empreintes notées. La scène de validation de la démo contient la sphère de verre très lisse visée par `6ead47308` (`PbrMaterialDemo`, « a glass sphere in front of a checkerboard », présente sauf si `CASAENGINE_PBR_DEMO_GLASS=0`) ;
  - déterminisme, la raison de `6ead47308` : `pbrdemo.sh` lancé trois fois dans chacune des trois variantes de T4.1 du plan du jeu (par défaut, `CASAENGINE_DEMO_COLOR_PIPELINE=Legacy`, `CASAENGINE_PBR_DEMO_CLEARCOAT=1`) donne trois empreintes égales par variante ; les nouvelles empreintes sont notées, et comparées à celles de T4.1 (`2d029da5659f2a7e`, `580dca11e3f9c87e`, `5d5850be6d2181e4`) ;
  - règle de décision : si le contrôle donne des empreintes différentes, trois empreintes égales de D3 valent preuve du déterminisme ; si le contrôle est déterministe sur ce PC, l'affirmation se limite à « non reproductible sur ce PC », et un nouveau contrôle sur l'autre PC (protocole de six captures de `6ead47308`) est ajouté aux points ouverts, à faire avant toute remontée dans `main` ou tout push ; si D3 donne des empreintes différentes sur ce PC, arrêt ;
  - le commentaire de `PbrDistributionGgx` (`Pbr.fxh`) et le texte de `docs/engine/pbr-rendering.md` disent ce qui précède : ni le produit vectoriel ni D3 ne sont présentés comme garantissant des captures identiques ;
  - jeu : validation globale de ce plan (pixels blancs de `Beginner-start` et `Expert-start` au niveau d'avant le PBR, à 10 % près ou en dessous), sur l'arbre de travail, puis pointeur avancé par un commit du jeu.
- Budget : une passe d'implémentation et une de correction.
- Arrêts : ceux de T1 (lire D3 à la place de D1) ; et des empreintes de la démo qui diffèrent d'un lancement à l'autre.
- Commit : `fix(shaders): keep GGX finite when the normal faces away from the half vector`
