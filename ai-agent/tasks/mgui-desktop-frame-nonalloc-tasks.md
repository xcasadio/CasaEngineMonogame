# Plan agent IA — Image complète du bureau MGUI sans allocation

Plan préparé par T3.2 de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : le 2026-10-06, la relecture
du plan du slider a resserré sa preuve au chemin du slider lui-même et renvoyé ici le reste d'une image du bureau.
**État : questions posées à l'auteur (section « Questions à l'auteur »), plan à compléter avec ses réponses, puis à
approuver. Aucune modification de code avant approbation.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Une image complète du bureau (`MGDesktop.Update` puis `MGDesktop.Draw`) qui contient un slider glissé n'alloue rien,
et une image au repos non plus. Le texte qui change vraiment relève du plan du texte
([mgui-text-nonalloc-tasks.md](mgui-text-nonalloc-tasks.md)).

## État vérifié du dépôt (2026-10-06)

Mesures sur MGUI `chantier/mgui-slider-nonalloc` (`1166a5a`, chemin du slider déjà traité), runtime de test
`GraphTestRuntime`, sonde temporaire non commitée (fenêtre avec un `MGStackPanel`, un `MGTextBlock` et un `MGSlider`,
moyenne par image après chauffe) :

- image au repos : environ 12 400 octets, dont `MGDesktop.Update` environ 1 400 et `MGDesktop.Draw` environ 11 000 ;
- image de glissement du slider : environ 14 500 octets.
- Réserve : `GraphTestRuntime.CreateDrawTransaction` crée une `GraphNoOpDrawTransaction` à chaque `Draw`
  (`MGUI/MGUI.Tests/Graph/GraphTestRuntime.cs:52-53`), qui enregistre chaque appel dans des listes ; une part de
  l'allocation du dessin mesurée ici vient donc du banc de test, pas de MGUI. T0.1 refait la mesure sans ce biais.

Sites déjà connus :

- `MGDesktop.Update` trie les fenêtres à chaque image : `Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).ToList()`
  (`MGUI/MGUI.Core/UI/MGDesktop.cs:1602`) ; `MGDesktop.Draw` (`:1729`) refait `foreach (var Window in Windows.OrderBy(x => x.IsTopmost))`
  (`:1739`).
- `ThicknessUtils.IsEmpty` (LINQ, 64 octets par bordure uniforme dessinée, inclus dans les mesures ci-dessus) : corrigé
  après ces mesures par le chantier du slider (D11, MGUI `c77e014`, ADR-0022 de MGUI) ; T0.1 refait la mesure sans lui.
- `MGDesktop.Update` est découpé en phases mesurables (`UIPerformanceProbe.BeginDesktopPhase`, `MGDesktop.cs:1517`
  à `:1591` : Animations, RootWindowEntries, ResponsiveMetrics, OverlayWindowBounds, RootWindowPlacement,
  InputModeAndFocus, HighPriorityInput, FloatingWindows, …), ce qui permet de ventiler la mesure.
- Non parcourus : `MGWindow.Update` et `MGWindow.Draw` (barre de titre, ombres, surcouches), layout des conteneurs
  (`MGStackPanel` et les autres), `DrawSelf` des autres contrôles.

## Questions à l'auteur

| Réf | Question | Options vues |
|---|---|---|
| Q1 | Périmètre : quels contrôles une « image du bureau sans allocation » doit-elle couvrir ? | (a) la fenêtre et les conteneurs qui hébergent un slider (`MGWindow`, `MGStackPanel`, `MGGrid`, `MGDockPanel`, et `MGDockHost` du docking de l'éditeur) ; (b) tous les contrôles de MGUI, contrôle par contrôle ; (c) d'abord (a), puis un plan par famille de contrôles. |
| Q2 | Ordre des fenêtres : le tri par `IsTopmost` est refait deux fois par image. Le garder en cache, invalidé à l'ajout, au retrait et au changement de `IsTopmost` ou d'ordre, est-il acceptable ? | (a) oui ; (b) tri sans allocation à chaque image (liste réutilisée, tri stable). |
| Q3 | Mesure de référence : runtime de test avec une transaction non enregistreuse, ou aussi le renderer réel de CasaEngine (démo ou éditeur instrumenté) ? | (a) runtime de test seulement ; (b) les deux. |
| Q4 | Layout : un layout complet du bureau (déclenché par exemple par un changement de texte) fait-il partie de ce chantier, ou seulement une image sans invalidation ? | (a) image sans invalidation seulement ; (b) layout compris. |

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | (resserrement du 2026-10-06, plan du slider) Le reste d'une image complète du bureau est traité dans ce chantier séparé. |

## Règles d'exécution pour l'agent

Celles de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : branches dédiées dans le moteur et dans
MGUI, une tâche et un commit à la fois, jamais de push ni de merge, tests d'allocation contre-éprouvés, arrêt sur toute
allocation imprévue. Langue : plan en français, code et commits en anglais.

## Validation globale

- Test de bout en bout : une image complète du bureau, au repos et pendant un glissement, n'alloue rien ;
  `MGUI.Tests` vert ; `MGUI.Samples` sans erreur ; les deux solutions du moteur sans erreur ; `CasaEngine.Tests` vert ;
  `verifier` frais.

## Tâches (provisoires, à figer après les réponses)

### ⏳ T0.1 — Banc de mesure par phase

- Objectif : runtime de test dont la transaction de dessin est réutilisée et n'enregistre rien (dans `MGUI.Tests`),
  puis mesure par phase de `MGDesktop.Update` (phases de `UIPerformanceProbe`) et par fenêtre au dessin ; inventaire des
  sites restants, classés, avant tout changement.

### ⏳ T1.x — Selon Q1 à Q4

Une tâche par site ou par famille (ordre des fenêtres, phases de `MGDesktop.Update`, `MGWindow`, conteneurs, contrôles),
chacune avec son test d'allocation contre-éprouvé et ses tests de comportement (ordre de dessin, z-order, hit-test).

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les chiffres ci-dessus incluent la transaction enregistreuse du banc de test ; ils servent d'ordre de grandeur seulement. | T0.1 |

## Hors périmètre

- Le pipeline de texte de `MGTextBlock` : [mgui-text-nonalloc-tasks.md](mgui-text-nonalloc-tasks.md).
