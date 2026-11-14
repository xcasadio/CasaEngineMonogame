# Plan agent IA — Image complète du bureau MGUI sans allocation

Plan préparé par T3.2 de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : le 2026-10-06, la relecture
du plan du slider a resserré sa preuve au chemin du slider lui-même et renvoyé ici le reste d'une image du bureau.
**État : questions tranchées par l'auteur le 2026-11-14 (D2 à D5, « Fait ça et fais l'implémentation de manière autonome »), tâches figées, exécution en mode AUTO. Branches `chantier/mgui-nonalloc` dans le moteur (depuis `main` `f6f8006a`) et dans MGUI (depuis `develop` `e9d6a95d`).**

**Relecture du 2026-11-14 : trois verdicts REVISE sur les tâches T1.1 à T1.4 de ce plan (voir O3) ; T0.1 jugée exécutable
les trois fois. T0.1 s'exécute comme mesure ; T1.1 à T2.1 sont en pause jusqu'à la réponse de l'auteur à O3 et à une
réécriture de ces tâches à partir de l'inventaire de T0.1.**


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
  (`MGUI/MGUI.Core/UI/MGDesktop.cs`, vers `:1623`) ; `MGDesktop.Draw` (vers `:1750`) refait `foreach (var Window in Windows.OrderBy(x => x.IsTopmost))`
  (vers `:1760`).
- `ThicknessUtils.IsEmpty` (LINQ, 64 octets par bordure uniforme dessinée, inclus dans les mesures ci-dessus) : corrigé
  après ces mesures par le chantier du slider (D11, MGUI `c77e014`, ADR-0022 de MGUI) ; T0.1 refait la mesure sans lui.
- `MGDesktop.Update` est découpé en phases (`UIPerformanceProbe.BeginDesktopPhase`), mais la sonde ne compte que du
  temps et ne s'active que par variable d'environnement : elle ne sert pas à mesurer les allocations (relecture du
  2026-11-14). La phase `WindowUpdates` (vers `:1627`) appelle `Window.Update` pour chaque fenêtre.
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
| D2 | (Q1, 2026-11-14) Périmètre (c) : la fenêtre et les conteneurs qui hébergent un slider (`MGWindow`, `MGStackPanel`, `MGGrid`, `MGDockPanel`, `MGDockHost`) dans ce chantier ; les autres contrôles dans un plan par famille, à écrire ensuite (T4.1). |
| D3 | (Q2) Ordre des fenêtres (b) : tri stable sans allocation à chaque image, dans une liste réutilisée ; pas de cache à invalider. |
| D4 | (Q3) Mesure (b) : runtime de test de MGUI et renderer réel de CasaEngine. |
| D5 | (Q4) Layout compris (b), en seconde phase : l'image sans invalidation d'abord (T1.x), puis le layout complet du bureau (T2.1). Le layout du parent d'un `MGTextBlock` dont le texte change est ici, pas dans le plan du texte (sa D4). |

## Règles d'exécution pour l'agent

Celles de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : branches dédiées dans le moteur et dans
MGUI, une tâche et un commit à la fois, jamais de push ni de merge, tests d'allocation contre-éprouvés, arrêt sur toute
allocation imprévue. Langue : plan en français, code et commits en anglais.

## Validation globale

- Test de bout en bout : une image complète du bureau, au repos et pendant un glissement, n'alloue rien ;
  `MGUI.Tests` vert ; `MGUI.Samples` sans erreur ; les deux solutions du moteur sans erreur ; `CasaEngine.Tests` vert ;
  `verifier` frais.

## Tâches

Ordre commun aux deux plans : T0.1 d'ici, puis le plan du texte jusqu'à sa T1.4 (sa T1.1 est le prérequis de T3.1
d'ici), puis T1.x, T2.1 et T3.1 d'ici, puis le reste du texte, puis T4.1 d'ici. Validation de chaque tâche MGUI : `dotnet build MGUI/MGUI.sln`, `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.

### ✅ T0.1 — Banc de mesure

- Fixture : une fenêtre dont le contenu est un `MGStackPanel` avec un `MGTextBlock` et un `MGSlider`. Deux scénarios :
  image au repos (souris hors de la fenêtre) et image de glissement du slider (texte du label inchangé).
- Mécanisme : `GC.GetAllocatedBytesForCurrentThread` entre `AllocationWindow.Start()` et la fin, après chauffe, dans
  `MGUI.Tests`. `UIPerformanceProbe` ne compte que du temps et ne sert pas ici. Sont mesurés, un test par nombre :
  `MGDesktop.Update` entier ; `MGDesktop.Draw(IUIDrawTransaction)` entier avec une transaction de test réutilisée qui
  n'enregistre rien ; `MGWindow.Draw` de la fenêtre ; puis les sites que l'on peut appeler seuls (`Update` et `DrawSelf`
  de chaque élément de la fixture).
- Les tests de mesure n'affirment pas zéro : ils écrivent leurs chiffres (sortie du test) et l'inventaire classé est
  recopié sous cette tâche. Les tests de zéro arrivent avec chaque correction.
- Fin : l'inventaire chiffré est dans ce plan, chaque nombre avec le nom du test qui le produit ; aucun code de
  production touché.
- Commit (MGUI) : `test(desktop): measure a desktop frame without the recording transaction`
- **Fait le 2026-11-14** (MGUI `chantier/mgui-nonalloc`). `MGUI.Tests` : 3236 verts. Tests :
  `MGUI.Tests/Architecture/DesktopFrameAllocationMeasurements.cs`, transaction
  `MGUI.Tests/Graph/NonRecordingDrawTransaction.cs`. Octets par image, après chauffe :

  | Test | Fixture | `Update` | `Draw` |
  |---|---|---|---|
  | `AnIdleFrame(NoWindow)` | bureau sans fenêtre | 1080 | 88 |
  | `AnIdleFrame(BareWindow)` | fenêtre sans barre de titre ni contenu | 1400 | 1673 |
  | `AnIdleFrame(DefaultWindow)` | fenêtre par défaut, sans contenu | 1400 | 2745 |
  | `AnIdleFrame(WindowWithStackPanel)` | + `MGStackPanel` vide | 1400 | 3345 |
  | `AnIdleFrame(WindowWithStackPanelAndTextBlock)` | + `MGTextBlock` | 1400 | 3713 |
  | `AnIdleFrame(WindowWithStackPanelAndSlider)` | + `MGSlider` avec label | 1400 | 3919 |
  | `AnIdleFrame(Full)` | panneau, texte et slider | 1400 | 4241 |
  | `ADragFrame_OnTheFullFixture` | la même, slider glissé | 1400 | 4241 |

  `DrawSelf_OfEachElementOfTheFullFixture` : `MGStackPanel.DrawSelf` 0, `MGSlider.DrawSelf` 0, `MGTextBlock.DrawSelf`
  120, `MGWindow.Draw` 3937.
- Lecture de ces chiffres (déductions, pas des mesures par site) :
  - `Update` : 1080 octets sont le travail propre du bureau, 320 viennent de la première fenêtre (tri compris) ; le
    contenu de la fenêtre n'ajoute rien, au repos comme en glissement.
  - `Draw` : le bureau seul 88 ; une fenêtre nue 1585 de plus ; la barre de titre 1072 ; chaque élément ajouté coûte de
    370 à 600 octets alors que son `DrawSelf` vaut 0 (120 pour le texte) : le coût est dans le chemin commun
    `MGElement.Draw`, donc partagé par tous les contrôles, pas dans les conteneurs.
- Limite de la mesure : la transaction de test rend le même `ClipScope` à chaque `Push`. `ClipScope` est une classe qui
  prend un `Action` (`MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs:87-106`) : un backend réel qui restaure le clip
  en alloue un par `Push`. Ce coût n'est pas dans le tableau ; il relève de T3.1.

### ⚠️ T1.1 — Ordre des fenêtres sans allocation (D3)

- Fichiers : `MGUI/MGUI.Core/UI/MGDesktop.cs` (tri de `Update` vers `:1623`, de `Draw` vers `:1760`).
- Fin : les deux tris passent par une liste réutilisée et un tri stable sans LINQ ; tests d'ordre d'update, de dessin
  et de hit-test (fenêtres `IsTopmost` et ordinaires mêlées) identiques avant et après ; test d'allocation nul sur ces
  deux sites, contre-éprouvé (il échoue sur le code d'avant).
- Commit (MGUI) : `perf(desktop): order the windows without allocating`

### ⚠️ T1.2 — Le travail propre de `MGDesktop.Update`

- Périmètre : ce que `MGDesktop.Update` fait lui-même, hors du sous-arbre de `Window.Update` (qui appartient à T1.3 et
  T1.4).
- Fin : `MGDesktop.Update` d'un bureau sans fenêtre alloue 0 octet, au repos, contre-éprouvé (le test échoue sur le
  code d'avant la tâche si T0.1 y a mesuré une allocation ; sinon la tâche se réduit à ce test et le dit).
- Commit (MGUI) : `perf(desktop): update an idle desktop without allocating`

### ⚠️ T1.3 — `MGWindow` au repos

- Fin : `MGDesktop.Update` et `MGDesktop.Draw` d'un bureau dont la seule fenêtre n'a pas de contenu (barre de titre,
  ombres, surcouches) allouent 0 octet pour une image sans invalidation ; tests d'allocation contre-éprouvés ; appels de dessin identiques (transaction enregistreuse).
- Commit (MGUI) : `perf(window): update and draw an idle window without allocating`

### ⚠️ T1.4 — Conteneurs, et preuve de bout en bout

- Fichiers : `MGStackPanel`, `MGGrid`, `MGDockPanel`, `MGDockHost`, et ce qui reste à zéro dans `MGTextBlock` et `MGSlider` au repos (update et dessin sans invalidation).
- Fin : `MGDesktop.Update` entier et `MGDesktop.Draw` entier (les tests de mesure de T0.1 deviennent des affirmations
  de zéro) : une image complète du bureau (fenêtre avec `MGStackPanel`, `MGGrid`, `MGDockPanel`, un `MGTextBlock` et un
  `MGSlider`) n'alloue rien, au repos et pendant un glissement du slider ; même preuve pour un `MGDockHost` ; tests
  contre-éprouvés.
- Commit (MGUI) : `perf(layout): draw the slider's containers without allocating`

### ⚠️ T2.1 — Layout complet sans allocation (D5)

- Fin : après une invalidation de layout (taille d'un enfant, texte d'un `MGTextBlock` changé par le chemin du plan du
  texte), la mesure et l'arrangement de la fenêtre et des conteneurs de D2 n'allouent rien ; tailles et positions
  identiques avant et après sur un jeu de fenêtres de test.
- Commit (MGUI) : `perf(layout): measure and arrange the slider's containers without allocating`

### ⏳ T3.1 — Mesure sur le renderer de CasaEngine (D4)

- Fichiers (runtime) : `CasaEngine/Framework/UI/Backend/MonoGame/` ; test dans `CasaEngine.Tests/UI/`
  (`HeadlessUiTestHarness`).
- Fin : la même image du bureau mesurée avec `CasaDesktopRuntime` et `CasaDrawTransaction` ; ce qui alloue dans le
  backend du moteur est corrigé ou listé avec sa mesure. Si le harnais sans GPU ne peut pas dessiner, la mesure se fait
  dans une démo instrumentée et la tâche reste 🧪 avec les chiffres.
- Validation : les deux solutions du moteur, `CasaEngine.Tests`.
- Commit (moteur) : `perf(ui): draw a desktop frame through the engine backend without allocating`, avec la référence
  du sous-module.

### ⏳ T4.1 — Suite par famille de contrôles (D2)

- Fin : la liste des familles de contrôles non couvertes, avec leur mesure quand T0.1 l'a donnée, est dans
  `ai-agent/README.md` comme suite à planifier. Aucun code.
- Commit (moteur) : `docs(ai-agent): list the control families left to make allocation-free`

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les chiffres ci-dessus incluent la transaction enregistreuse du banc de test ; ils servent d'ordre de grandeur seulement. | T0.1 |
| O2 | L'arbre de l'auteur a déplacé la référence du sous-module MGUI de `a3c42cb0` à `e9d6a95d` (`develop`) sans la committer. La branche MGUI part de `develop` ; le commit de référence du moteur embarquera donc aussi ces deux commits de l'auteur. À confirmer par l'auteur avant tout merge. | T3.1 |
| O3 | La barre de titre de `MGWindow` est visible par défaut et faite d'un `MGDockPanel`, d'un `MGTextBlock` et d'un `MGButton` (`MGWindow.cs:41-43`, `:1675-1680`). `MGButton` est hors du périmètre D2. Question à l'auteur : (a) étendre D2 aux parties de la fenêtre elle-même, `MGButton` au repos compris (recommandé : sans cela aucune fenêtre à barre de titre n'est à zéro) ; (b) garder D2 et prouver le zéro sur une fenêtre sans barre de titre. Les tâches T1.1 à T1.4 sont à réécrire ensuite pour que chaque affirmation de zéro ne couvre que le code de sa tâche ou d'une tâche antérieure (pour T1.1 : un tri appelable seul par le test). | T1.1 à T2.1 |
| O4 | T0.1 montre que le coût du dessin est dans `MGElement.Draw` (commun à tous les contrôles) et dans `MGWindow.Draw`, pas dans les conteneurs : le découpage T1.3 / T1.4 par conteneur ne correspond pas aux sites. Proposition pour la réécriture : T1.1 tri des fenêtres ; T1.2 travail propre de `MGDesktop.Update` (1080) et `Draw` (88) ; T1.3 `MGElement.Draw` commun ; T1.4 `MGWindow.Update` et `Draw` ; T1.5 barre de titre selon O3. | T1.1 à T1.4 |
| O5 | `ClipScope` (classe, `Action` par `Push`) force une allocation par clip dans tout backend réel. La rendre réutilisable change une API publique de `MGUI.Shared`. Question à l'auteur : l'autoriser dans ce chantier (recommandé, sinon le zéro n'existe que sur le banc de test) ? | T3.1 |

## Hors périmètre

- Le pipeline de texte de `MGTextBlock` : [mgui-text-nonalloc-tasks.md](mgui-text-nonalloc-tasks.md).
