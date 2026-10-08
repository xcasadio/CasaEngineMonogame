# Plan agent IA — Boîte de ligne des deux moteurs de texte

Plan d'exécution né d'un défaut signalé par l'auteur le 2026-10-08 : dans le navigateur de démos, le texte est coupé verticalement (jambages de `g`, `j`, `p`, `q`, `y`). L'analyse en lecture seule a montré que la hauteur de ligne des deux moteurs de texte de MGUI ne contient pas l'encre qu'ils dessinent.
Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-10-08 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Les deux moteurs de texte de MGUI, `SpriteFontTextEngine` et `FontStashSharpTextEngine`, suivent un même contrat de **boîte de ligne** :

- le point de dessin d'un moteur est le haut de sa boîte de ligne ;
- la boîte va de l'encre la plus haute à l'encre la plus basse des caractères du **répertoire de la boîte de ligne**, U+0020 à U+024F hors blancs et caractères de contrôle, qui est aussi le répertoire des atlas SpriteFont de MGUI ;
- `ResolvedFont.LineHeight` est la hauteur de cette boîte et `ResolvedFont.DrawOrigin` vaut zéro pour les deux moteurs intégrés ; chaque moteur calcule sa boîte avec ses propres données, sans calibrage croisé de la hauteur ;
- le texte est dessiné à l'échelle avec laquelle il est mesuré (`ExactScale`) : `MGTheme.FontSettings.UseExactScale` disparaît, ainsi que le paramètre `Exact` des aides de texte de `DrawTransaction` (MGUI) et de `CasaDrawTransaction` (moteur).

Le contrat est enregistré dans l'ADR-0023 de MGUI. Les dessinateurs de MGUI, du moteur et de l'éditeur sont alignés.

## État vérifié du dépôt (2026-10-08)

Arbre et branches :

- Dépôt autonome `D:\development\repo\CasaEngineMonogame` ; worktree `.claude/worktrees/fss-line-height` sur `chantier/fss-line-height`, créé depuis `main` `2bcb1856`. Arbre propre.
- Sous-modules du worktree : `MGUI` détaché sur `18b2c14a` (= `develop`), `NvgSharp` sur `9c0da03`.
- Modifications préexistantes de l'auteur dans son checkout principal : `CasaEngine.Launcher/Program.cs`, `Projects/SampleProject/.casaeditor/viewport.editor.json` (non suivi). Le chantier ne les touche pas.
- Prochaine ADR libre de MGUI : 0023 (`MGUI/Docs/decisions/` s'arrête à 0022).

Mesures de référence (T0.1) :

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 4151/4151. `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` : 3174/3174.
- Sonde temporaire (non commitée) lancée dans `CasaEngine.Demos` avec les vraies polices :
  - Atlas SpriteFont Arial (`MGUI/MGUI.Core/Content/Fonts/Arial/*.spritefont`, régions U+0020 à U+024F) : des glyphes ont un `Cropping.Y` négatif. Les capitales accentuées montent au-dessus du haut de cellule : -1 pour `É` en 8 pt, -3 pour `ǻ` en 8 pt, jusqu'à -9 en 72 pt. Les blancs (`U+0020`, `U+00A0`) portent un faux pixel 1x1 en bas de cellule (`Cropping.Y` = 22 en 8 pt), qu'il faut exclure.
  - Hauteur actuelle du moteur SpriteFont : `FontSet.Heights` × `ExactScale`, calculée sur l'ASCII seul et décalée de `Origins` (`MGUI/MGUI.MonoGame.Integration/Text/FontSet.cs:110-121`, `198-208`). Exemple en 8 pt : 12 px pour une encre qui va de -3 à 14.
  - FontStashSharp (runtime : Tahoma enregistrée sous la famille `Arial`, `CasaEngine/Framework/UI/UIRoot.cs:93-114`, calibrée par `MatchSpriteFontSizing`) : la hauteur actuelle est celle du calibrage SpriteFont (11 pt : 16 px), alors que l'encre du répertoire va de -1 à 19 px pour une taille de 18,27 px.
  - Boîte retenue (D2), en px : SpriteFont 8 pt 17, 11 pt 20, 14 pt 26 ; FontStashSharp 8 pt 15, 11 pt 20, 14 pt 26.
- Dessin : `MGTextBlock` (`MGUI/MGUI.Core/UI/MGTextBlock.cs:1589-1645`) et `MGColorField` (`MGUI/MGUI.Core/UI/Color/MGColorField.cs:420-435`) dessinent à `SuggestedScale` sauf si `UseExactScale`, alors que la mesure est à `ExactScale`. `MGColorField` multiplie en plus `LineHeight` par l'échelle, qui y est déjà appliquée. `MGRotatedTextLabel` (`MGUI/MGUI.Core/UI/MGRotatedTextLabel.cs:101-108`) passe une origine en pixels d'écran à un moteur qui l'attend en unités natives. `MGDockAutoHideStrip.MeasureButtonSizePx` (`MGUI/MGUI.Core/UI/Docking/Controls/MGDockAutoHideStrip.cs:279-282`) corrige la largeur mesurée vers `SuggestedScale`.
- Les dessinateurs appliquent déjà une règle d'origine unique : position + `DrawOrigin` × échelle, origine = `DrawOrigin` (testée par `PropertyGrid_TextBox_Text_Draw_Compensates_For_DrawOrigin`, `MGUI/MGUI.Tests/Integration/PropertyGridTests.cs:128-164`).
- `UseExactScale` : `MGTheme.cs:332-340` et `977`, `XAML/Themes.cs:168`, `XAML/ThemeDefinitionBuilder.cs:266-267`, `Styling/UIThemeValueInvalidation.cs:119`, `MGUI.Tests/Architecture/ThemeValueInvalidationInventoryTests.cs:90`, `Docs/styling-theme-architecture.md:161`. Aucun fichier XAML ni aucun asset du dépôt ne l'utilise.
- Les aides `DrawText`, `DrawShadowedText` et `MeasureText(Family, …, bool Exact)` de `DrawTransaction` (`MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs:259-361`) et de `CasaDrawTransaction` (`CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs:272-374`) n'ont aucun appelant dans le dépôt.
- Éditeur : la timeline résout ses polices à `SuggestedScale` et multiplie `LineHeight` par l'échelle (`CasaEngine.Editor/Controls/Timeline/TimelineRuler.cs:36,110,141`, `TimelineViewport.cs:247`, `TimelineTrackHeaderPanel.cs:339`, `Rendering/DefaultTimelineItemRenderer.cs:117`).
- Tests sans carte graphique possibles : `MGUI.Tests` crée FontStashSharp depuis `Fonts/arial.ttf` (`MGUI/MGUI.Tests/Text/FSSMeasureDrawConsistencyTests.cs`) ; `FontSet` a un constructeur public à partir de `SpriteFont` (`FontSet.cs:150`) et `FontManager.AddFontSet` existe ; `MGUI.MonoGame.Integration` et `MGUI.Core` donnent `InternalsVisibleTo("MGUI.Tests")`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Contrat « boîte de ligne » pour les deux moteurs (« ok pour la boîte de ligne ») : le point de dessin est le haut de la boîte, `DrawOrigin` vaut zéro pour les moteurs intégrés, `LineHeight` contient l'encre, chaque moteur utilise ses propres données. Vraie correction des moteurs, pas de contournement dans les écrans (« Pas de workaround »). |
| D2 | La boîte va de l'encre la plus haute à l'encre la plus basse du répertoire U+0020 à U+024F (réponse « Encre du répertoire » du 2026-10-08), le même pour les deux moteurs. SpriteFont : glyphes de l'atlas de ce répertoire, tous styles de la taille précalculée. FontStashSharp : encre du même répertoire mesurée sur la police à la taille résolue. Les blancs et les caractères de contrôle sont exclus. |
| D3 | « supprime UseExactScale » : le texte est dessiné à `ExactScale`, l'échelle de mesure. Le paramètre `Exact` des aides de texte de `DrawTransaction` et `CasaDrawTransaction` porte le même choix : il est supprimé aussi. Rupture d'API publique assumée, signalée dans l'ADR et le rapport. |
| D4 | Exécution enchaînée en mode AUTO (« écris le plan et execute tout ensuite ») ; ni push ni merge. |
| D5 | Travail uniquement dans `D:\development\repo\CasaEngineMonogame` et ses sous-modules. |
| D6 | Les polices bitmap statiques du moteur FontStashSharp (BMFont, ADR-0036 du moteur) gardent la hauteur de ligne déclarée par leur fichier : hors périmètre, voir « Hors périmètre ». |

## Règles d'exécution pour l'agent

- **Branches** : `chantier/fss-line-height` dans le moteur (worktree `.claude/worktrees/fss-line-height`) ; `chantier/text-line-box` dans le sous-module `MGUI`, créée depuis `develop` `18b2c14a`. Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis committer.
- **Commits** : une tâche MGUI donne un commit dans `MGUI`, puis un commit du moteur qui avance le pointeur `MGUI` et met ce plan à jour. Chaque commit compile. Messages en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché ; **tests** `MGUI.Tests` pour les tâches MGUI, `CasaEngine.Tests` pour les tâches du moteur. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`. La sonde temporaire n'est jamais commitée.
- **Langue** : ce plan en français ; code, messages de commit, docs et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (le dessin et la mesure du texte en sont) ; les calculs de boîte se font à la résolution d'une police, qui est mise en cache.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout vert, nouveaux tests compris.
- Sonde relancée : hauteurs de ligne égales aux valeurs de D2 pour les deux moteurs.
- Captures du navigateur de démos ouvert (`CASAENGINE_DEMO_BROWSER=open`) : jambages et accents entiers dans l'arbre et la description.
- Vérificateur frais sur l'ensemble du chantier.

---

## Phase 0 — Cadrage

### ✅ T0.1 — Plan, mesures de référence

- Objectif : ce plan, les mesures de référence et l'arbitrage de la boîte (D2).
- Fichiers : `ai-agent/tasks/text-line-box-tasks.md`, `ai-agent/README.md`.
- Étapes :
  1. Builds et tests de référence.
  2. Sonde temporaire des atlas SpriteFont et de FontStashSharp ; question à l'auteur sur la boîte (D2).
  3. Plan et ligne du tableau de `ai-agent/README.md`.
- Validation : chiffres ci-dessus ; sonde retirée de l'arbre (`git status` propre hors plan).
- Commit : `docs(ai-agent): plan the text line box fix for both text engines`
- Note : builds 0 erreur, `CasaEngine.Tests` 4151/4151, `MGUI.Tests` 3174/3174. La sonde a contredit l'hypothèse d'une encre qui ne monte jamais au-dessus du haut de ligne ; l'auteur a tranché D2.

---

## Phase 1 — MGUI (sous-module, branche `chantier/text-line-box`)

### ✅ T1.1 — Moteur SpriteFont : boîte de ligne de l'atlas

- Objectif : `SpriteFontTextEngine` mesure et dessine avec la boîte de ligne de D2.
- Fichiers : `MGUI/MGUI.Shared/Text/LineBoxRepertoire.cs` (nouveau), `MGUI/MGUI.Shared/Text/ResolvedFont.cs` (doc), `MGUI/MGUI.MonoGame.Integration/Text/FontSet.cs`, `MGUI/MGUI.MonoGame.Integration/Text/Engines/SpriteFontTextEngine.cs`, `MGUI/MGUI.Tests/Text/SpriteFontLineBoxTests.cs` (nouveau).
- Étapes :
  1. `LineBoxRepertoire` (public, `MGUI.Shared.Text`) : bornes U+0020 et U+024F, `Contains(char)` qui exclut blancs et contrôles, et la chaîne des caractères du répertoire (construite une fois).
  2. `FontSet` : ajout public `LineBoxes` (taille précalculée → `FontLineBox(Top, Bottom)`, nouveau `record struct` à côté de `FontMetadata`), calculé dans les deux constructeurs sur tous les styles de la taille, glyphes du répertoire seulement, haut = min `Cropping.Y`, bas = max `Cropping.Y + BoundsInTexture.Height`. `Heights` et `Origins` restent inchangés.
  3. `SpriteFontTextEngine` : `LineHeight`, la hauteur de `MeasureText` et de `MeasureGlyph` = hauteur de la boîte × `ExactScale` ; `DrawOrigin` = zéro ; `DrawText` place le haut de la boîte au point de dessin (origine native décalée de `Top`).
  4. Doc de `ResolvedFont` : `LineHeight`, `DrawOrigin`, `ExactScale` décrivent le contrat (D1, D2).
  5. Tests sans carte graphique, polices `SpriteFont` synthétiques (texture nulle) : boîte d'un `FontSet` (accent négatif, jambage, blanc au faux pixel ignoré, glyphe hors répertoire ignoré, plusieurs styles) ; `ResolveFont` du moteur (hauteur × `ExactScale` pour une taille réduite, `DrawOrigin` nul, `MeasureText.Y` et `MeasureGlyph` égaux à `LineHeight`).
- Validation : build de `MGUI.sln` ou des projets touchés, `MGUI.Tests` vert ; build des deux solutions du moteur après l'avance du pointeur.
- Commits : MGUI `fix(text): size SpriteFont lines by the ink of the line box repertoire` ; moteur `chore(mgui): bump MGUI to the SpriteFont line box`.
- Note : MGUI `300cd8be`. `SpriteFontLineBoxTests` (5 tests : boîte sur deux styles avec accent négatif, blanc au faux pixel et glyphe hors répertoire ignorés ; repli sur l'interligne ; moteur à `ExactScale` 1 et 1/3, styles normal et gras). `MGUI.Tests` 3179/3179, les deux solutions du moteur sans erreur, `CasaEngine.Tests` 4151/4151 (un échec instable au premier passage après build, non reproduit sur quatre relances ; T1.1 ne touche pas le chemin du moteur, qui utilise FontStashSharp).

### ✅ T1.2 — Moteur FontStashSharp : boîte de ligne du répertoire

- Objectif : `FontStashSharpTextEngine` mesure et dessine avec la boîte de ligne de D2, mesurée sur sa propre police.
- Fichiers : `MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs`, `MGUI/MGUI.Tests/Text/FSSLineBoxTests.cs` (nouveau).
- Étapes :
  1. `ResolveFont` (police dynamique) : encre du répertoire mesurée par `TextBounds` sur la police à la taille résolue ; `LineHeight` = hauteur de cette encre (arrondie au pixel supérieur) ; `DrawOrigin` = zéro ; la poignée native garde le haut de l'encre.
  2. `DrawText` place le haut de la boîte au point de dessin (origine décalée du haut de l'encre).
  3. `MatchSpriteFontSizing` ne calcule plus de hauteur ni d'origine : suppression de `_calibratedLineHeight` et `_calibratedDrawOrigin` ; le calibrage des largeurs reste. La table de métriques de glyphes (non lue) prend la hauteur de la boîte SpriteFont de T1.1.
  4. Polices statiques inchangées (D6).
  5. Tests sans carte graphique (`Fonts/arial.ttf`) : pour une plage de tailles, `DrawOrigin` nul, `LineHeight` couvre l'encre de chaque caractère du répertoire et la dépasse de moins d'un pixel, `MeasureText.Y` = `LineHeight`, police de repli comprise.
- Validation : `MGUI.Tests` vert ; build des deux solutions du moteur après l'avance du pointeur.
- Commits : MGUI `fix(fss): size FontStashSharp lines by the ink of the line box repertoire` ; moteur `chore(mgui): bump MGUI to the FontStashSharp line box`.
- Note : MGUI `9fa782b9`. `FSSLineBoxTests` (15 tests : pour 12 tailles de 6 à 48, la hauteur est l'union de l'encre de chaque caractère du répertoire mesurée sur une police indépendante, son haut est au-dessus du point de dessin, l'origine est nulle ; hauteurs de `MeasureText`, `MeasureGlyph` et `GetLineHeight` ; police de repli). `MGUI.Tests` 3194/3194, les deux solutions du moteur sans erreur, `CasaEngine.Tests` 4151/4151.

### ✅ T1.3 — Une seule échelle de dessin

- Objectif : tout le texte de MGUI est dessiné à `ExactScale` (D3).
- Fichiers : `MGUI/MGUI.Core/UI/MGTheme.cs`, `MGUI/MGUI.Core/UI/XAML/Themes.cs`, `MGUI/MGUI.Core/UI/XAML/ThemeDefinitionBuilder.cs`, `MGUI/MGUI.Core/UI/Styling/UIThemeValueInvalidation.cs`, `MGUI/MGUI.Tests/Architecture/ThemeValueInvalidationInventoryTests.cs`, `MGUI/Docs/styling-theme-architecture.md`, `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs`, `MGUI/MGUI.Core/UI/MGTextBlock.cs`, `MGUI/MGUI.Core/UI/Color/MGColorField.cs`, `MGUI/MGUI.Core/UI/MGRotatedTextLabel.cs`, `MGUI/MGUI.Core/UI/Docking/Controls/MGDockAutoHideStrip.cs`, `MGUI/MGUI.Samples/Dialogs/SampleHUD.xaml.cs`, tests associés.
- Étapes :
  1. Suppression de `FontSettings.UseExactScale` (propriété, doc, copie de thème, définition XAML, builder, inventaire d'invalidation, test d'inventaire, doc de stylage).
  2. `DrawTransaction` : suppression du paramètre `Exact` de `DrawText`, `DrawShadowedText` et `MeasureText` ; dessin à `ExactScale` avec la règle d'origine unique ; retour = `MeasureText` du moteur.
  3. Dessinateurs à `ExactScale` : `MGTextBlock` ; `MGColorField` (et hauteur = `LineHeight`, sans seconde multiplication) ; `MGRotatedTextLabel` (origine native = `DrawOrigin` + taille mesurée / (2 × échelle)) ; `SampleHUD` (règle d'origine unique) ; `MGDockAutoHideStrip` mesure sans correction vers `SuggestedScale`.
  4. Tests : `MGTextBlock` dessine à `ExactScale` avec un moteur factice où `ExactScale` ≠ `SuggestedScale` ; centre de `MGRotatedTextLabel` ; centrage vertical de `MGColorField` si le harnais le permet.
- Validation : `MGUI.Tests` vert (test d'inventaire compris) ; `rg UseExactScale` ne rend plus que l'ADR ; build des deux solutions du moteur après l'avance du pointeur.
- Commits : MGUI `fix(text): draw text at the scale it is measured with and remove UseExactScale` ; moteur `chore(mgui): bump MGUI to the single text draw scale`.
- Note : MGUI `ecd09ba4`. `TextDrawScaleTests` (3 tests, moteur factice `ExactScale` 0,5 ≠ `SuggestedScale` 1, origine (0, 3)) : `MGTextBlock` dessine à 0,5 avec l'origine ; `MGRotatedTextLabel` tourne autour du centre de la boîte en unités natives ; `MGColorField` centre la boîte dans sa bande (une mutation qui remet la double mise à l'échelle fait échouer ce test). Le harnais partagé `GraphNoOpDrawTransaction` enregistre désormais les appels de texte. `MGUI.Tests` 3197/3197, `MGUI.sln` et les deux solutions du moteur sans erreur, `CasaEngine.Tests` 4151/4151. `rg UseExactScale` ne rend plus rien dans MGUI (l'ADR viendra en T1.4).

### ⏳ T1.4 — ADR-0023 de MGUI

- Objectif : enregistrer le contrat de boîte de ligne et l'échelle de dessin unique.
- Fichiers : `MGUI/Docs/decisions/0023-text-line-box-and-single-draw-scale.md`, `MGUI/Docs/decisions/README.md`.
- Validation : relecture ; index à jour.
- Commits : MGUI `docs(decisions): record the text line box contract (ADR-0023)` ; moteur `chore(mgui): bump MGUI to ADR-0023`.

---

## Phase 2 — Moteur et éditeur (branche `chantier/fss-line-height`)

### ⏳ T2.1 — Dessinateurs du moteur et de l'éditeur

- Objectif : le moteur et l'éditeur suivent D3.
- Fichiers : `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`, `CasaEngine.Editor/Controls/Timeline/TimelineRuler.cs`, `TimelineViewport.cs`, `TimelineTrackHeaderPanel.cs`, `Rendering/DefaultTimelineItemRenderer.cs`.
- Étapes :
  1. `CasaDrawTransaction` : même changement que `DrawTransaction` (T1.3, étape 2).
  2. Timeline : échelle = `ExactScale` ; hauteur du texte = `LineHeight`, déjà à l'échelle.
- Validation : build des deux solutions, `CasaEngine.Tests` vert.
- Commit : `fix(ui): draw engine and editor text at the measured scale`

### ⏳ T2.2 — Vérification visuelle

- Objectif : prouver la correction sur de vraies images.
- Étapes :
  1. Sonde temporaire relancée : hauteurs de D2 pour les deux moteurs.
  2. Captures de `CasaEngine.Demos` avec le navigateur ouvert, sur deux ou trois démos : jambages et accents entiers dans l'arbre et la description ; comparaison avec la capture d'avant.
  3. Capture de l'éditeur si l'automatisation le permet ; échantillons MGUI (moteur SpriteFont) à regarder par l'auteur.
- Validation : captures relues ; 🧪 pour ce qui demande l'œil de l'auteur.
- Commit : `docs(ai-agent): record the text line box visual checks`

### ⏳ T2.3 — Vérificateur frais et rapport

- Objectif : vérification indépendante du chantier, puis rapport de fin.
- Étapes : `verifier` frais sur le contrat (D1 à D3), les tests et les captures ; traitement des constats ; plan et `ai-agent/README.md` à jour.
- Commit : `docs(ai-agent): record the text line box verification`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Des consommateurs hors de ce dépôt (portage Alundra, autres projets) peuvent utiliser `UseExactScale` ou le paramètre `Exact` : à vérifier par l'auteur avant le merge. | T1.3, T2.1 |
| O2 | Un thème XAML externe qui écrit `UseExactScale` ne se chargera plus. | T1.3 |

## Hors périmètre

- Les polices bitmap statiques (BMFont) du moteur FontStashSharp gardent leur hauteur de ligne déclarée (D6).
- Les caractères hors du répertoire U+0020 à U+024F peuvent dépasser de la boîte.
- Le calibrage des largeurs de FontStashSharp sur SpriteFont reste tel quel.
- `ResolvedFont.SuggestedScale`, `FontSet.Heights` et `FontSet.Origins` restent publics et inchangés ; seuls leurs usages de dessin changent.
- Dans une ligne qui mêle des styles, la ligne de base de chaque atlas SpriteFont reste celle de son atlas (comportement existant).
