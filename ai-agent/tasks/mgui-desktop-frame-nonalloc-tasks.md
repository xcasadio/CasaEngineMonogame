# Plan agent IA — Image complète du bureau MGUI sans allocation

Plan préparé par T3.2 de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : le 2026-10-06, la relecture
du plan du slider a resserré sa preuve au chemin du slider lui-même et renvoyé ici le reste d'une image du bureau.
**État : questions tranchées par l'auteur le 2026-11-14 (D2 à D5, « Fait ça et fais l'implémentation de manière autonome »), tâches figées, exécution en mode AUTO. Branches `chantier/mgui-nonalloc` dans le moteur (depuis `main` `f6f8006a`) et dans MGUI (depuis `develop` `e9d6a95d`).**

**Relecture du 2026-11-14 : trois verdicts REVISE sur les tâches T1.1 à T1.4 ; elles ont été mises en pause sur O3, O4 et O5.**

**2026-11-15 : l'auteur a répondu « Oui, les deux » à O3 et O5 (D6, D7), puis « et execute tout en mode auto car je pars dormir ». Les tâches T1.0 à T4.1 sont réécrites ci-dessous à partir de l'inventaire de T0.1 ; exécution en mode AUTO, sans push ni merge.**


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
| D6 | (O3, 2026-11-15) Le périmètre D2 est étendu aux parties de la fenêtre elle-même : `MGBorder`, la barre de titre (`MGDockPanel`, `MGTextBlock`, `MGButton` au repos et survolé) et `MGResizeGrip`. |
| D7 | (O5, 2026-11-15) `ClipScope` peut devenir réutilisable dans ce chantier : changement additif de l'API publique de `MGUI.Shared`, enregistré par un ADR de MGUI (T3.1). |

## Règles d'exécution pour l'agent

Celles de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : branches dédiées dans le moteur et dans
MGUI, une tâche et un commit à la fois, jamais de push ni de merge, tests d'allocation contre-éprouvés, arrêt sur toute
allocation imprévue. Langue : plan en français, code et commits en anglais.

## Validation globale

- Test de bout en bout : une image complète du bureau, au repos et pendant un glissement, n'alloue rien ;
  `MGUI.Tests` vert ; `MGUI.Samples` sans erreur ; les deux solutions du moteur sans erreur ; `CasaEngine.Tests` vert ;
  `verifier` frais.

## Tâches

Le plan du texte est terminé. Ordre : T1.0 à T1.5, T2.1, T3.1, T3.2, T4.1. Validation de chaque tâche MGUI : `dotnet build MGUI/MGUI.sln`, `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.

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
- Limite de la mesure : la transaction de test rend le même `ClipScope` à chaque `Push`. Un backend réel crée aussi une `ClipDefinition` (classe) par clip et un objet par changement temporaire de réglages ou de transformation (relecture du 2026-11-15). `ClipScope` est une classe qui
  prend un `Action` (`MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs:87-106`) : un backend réel qui restaure le clip
  en alloue un par `Push`. Ce coût n'est pas dans le tableau ; il relève de T3.1.

### Règles communes aux tâches T1.1 à T2.1 (réécriture du 2026-11-15)

- **Une affirmation de zéro par fixture, dans l'ordre.** Chaque tâche rend nulle la mesure d'une fixture du tableau de
  T0.1, et cette fixture ne contient que du code traité par la tâche ou par une tâche antérieure. Le test de mesure
  correspondant de `DesktopFrameAllocationMeasurements` devient une affirmation `Assert.Equal(0, …)`.
- **Localisation par mesure.** Chaque tâche commence par localiser ses sites (retrait ou court-circuit temporaire d'un
  appel, mesure, remise en place ; rien de temporaire n'est commité). La liste des sites (fichier, ligne, octets) est
  écrite sous la tâche avant la correction.
- **Contre-épreuve.** Chaque test de zéro est vu échouer sur le code d'avant la tâche (`git stash` des fichiers de
  production, ou correction retirée à la main), avec le nombre d'octets noté sous la tâche.
- **Comportement inchangé.** Avant de toucher au code de production, la tâche ajoute un test de caractérisation qui
  passe sur le code d'avant : le journal ordonné de tous les appels de la transaction de T1.0
  (`OrderedRecordingDrawTransaction`) pour sa fixture, image au repos, image de survol, image de glissement, comparé
  au même journal produit après la tâche (valeur attendue figée dans le test, écrite depuis le code d'avant).
  `GraphNoOpDrawTransaction` ne sert pas à cette preuve : elle ignore plusieurs appels et perd l'ordre entre les
  sortes d'appels. Pour `Update` : ordre des appels `Update` des fenêtres, élément survolé, élément pressé et focus
  après un scénario souris fixe.
- **Arrêt.** Si un site est hors des fichiers de la tâche (brosses, `MGUI.Shared`, MonoGame, une classe de contrôle
  hors de D2 et D6), ou si le corriger change une API publique ou un comportement observable, la tâche écrit la mesure
  du site ici, passe en ⚠️ et s'arrête ; les tâches suivantes qui en dépendent ne commencent pas.
- Aucune API publique de MGUI n'est modifiée ni retirée par T1.1 à T2.1. La seule tâche qui touche une API publique
  est T3.1 (D7).
- Validation de chaque tâche : `dotnet build MGUI.sln` 0 erreur, `dotnet test MGUI.Tests/MGUI.Tests.csproj` vert sans
  test sauté, depuis `MGUI/`.

### ✅ T1.0 — Transaction de caractérisation

- Fichiers (tests seulement) : `MGUI/MGUI.Tests/Graph/OrderedRecordingDrawTransaction.cs`,
  `MGUI/MGUI.Tests/Architecture/OrderedRecordingDrawTransactionTests.cs`. Aucun code de production.
- Travail : une transaction de test qui écrit, dans une seule suite ordonnée, une ligne par appel de chaque membre de
  `IUIDrawTransaction` avec ses arguments : tous les membres de dessin (dont `StrokeRectangle`, `FillPoint`,
  `FillPolygon`, `StrokeAndFillPolygon`, `FillCircle`, `StrokeCircle`, textes, textures, lignes, dégradés) ; chaque
  `Push` de clip avec ses bornes et chaque `Dispose` du scope rendu ; `SetDrawSettingsTemporary` et
  `SetTransformTemporary` avec leurs réglages et leur `Dispose` ; `SetRenderTargetTemporary` et son `Dispose`.
- Fin : un test par réflexion vérifie que chaque membre de `IUIDrawTransaction` (et des interfaces qu'elle étend)
  écrit une ligne ; sur la fixture `Full`, retirer un appel `StrokeRectangle` ou `FillRectangle` du journal attendu,
  ou échanger deux appels de sortes différentes, fait échouer la comparaison (tests) ; deux images au repos
  successives donnent le même journal.
- Commit (MGUI) : `test(desktop): record every draw transaction call in order`
- **Fait le 2026-11-15** (MGUI `7cb8f1dd`). Une ligne par appel, dans l'ordre, avec les arguments ; chaque `Push`
  écrit un identifiant de scope et ses bornes, chaque `Dispose` sa ligne. Le journal attendu d'un test est figé par
  son nombre de lignes et un préfixe SHA-256, le journal entier étant écrit en cas d'échec (`FrozenJournal`).
- Écart : la fixture `Full` ne dessine aucun `StrokeRectangle` ; le test de mutation retire une ligne `FillRectangle`,
  `StrokeLineSegment`, `DrawTextViaEngine` ou `FillPoint`, et échange deux lignes de sortes différentes ; chaque
  mutation fait échouer la comparaison.

### ✅ T1.1 — Ordre des fenêtres sans allocation (D3)

- Fichiers : `MGUI/MGUI.Core/UI/MGDesktop.cs` (phase `OrderedWindows` de `Update`, boucle des fenêtres de
  `Draw(IUIDrawTransaction, float)`), test `MGUI/MGUI.Tests/Architecture/DesktopWindowOrderTests.cs`.
- Travail : les deux expressions LINQ (`Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).ToList()` et
  `Windows.OrderBy(x => x.IsTopmost)`) sont remplacées par deux méthodes `internal static` qui remplissent une liste
  fournie par l'appelant, par un tri stable écrit à la main (deux passes : une valeur de `IsTopmost`, puis l'autre).
  `MGDesktop` garde une liste par site. Si `Update` ou `Draw` est rappelé pendant sa propre boucle (ré-entrance), le
  site prend une liste neuve pour cet appel : la liste gardée n'est jamais modifiée pendant qu'elle est parcourue.
  Les deux boucles continuent de parcourir une copie, comme aujourd'hui (une fenêtre ouverte ou fermée pendant la
  boucle ne change pas le parcours en cours).
- Fin :
  - ordre : pour 2000 listes aléatoires de 0 à 12 fenêtres (graine fixe, `IsTopmost` mêlés), chaque méthode rend
    exactement la séquence de l'expression LINQ qu'elle remplace ;
  - zéro : chaque méthode, appelée seule 500 fois après chauffe sur une liste de 8 fenêtres, alloue 0 octet ;
    contre-épreuve : le même test appliqué à l'expression LINQ d'origine alloue (nombre noté) ;
  - caractérisation : ordre des `Update` et des `Draw` de 4 fenêtres mêlées, identique avant et après ; une fenêtre
    fermée pendant son propre `Update` ne casse pas la boucle (test).
- Hors de cette tâche : `MGDesktop.Update` entier n'est pas encore à zéro (T1.2 et T1.3).
- Commit (MGUI) : `perf(desktop): order the windows without allocating`
- **Fait le 2026-11-15** (MGUI `1cb1442e`). `MGDesktop.FillUpdateOrder` et `FillDrawOrder` (`internal static`, deux
  passes) ; une liste gardée par site avec un indicateur d'usage, vidée dans un `finally` ; un appel imbriqué prend une
  liste neuve.
- Mesures : chaque méthode, 500 appels sur 8 fenêtres : 0 octet ; les expressions LINQ d'origine : 552 octets (ordre
  d'`Update`) et 384 octets (ordre de `Draw`) par appel. Sur `NoWindow`, `Update` passe de 1080 à 856 octets et `Draw`
  de 88 à 0.
- Caractérisation (écrite avant, verte avant et après) : ordre d'`Update` et de `Draw` de 4 fenêtres mêlées, fenêtre
  fermée ou ouverte pendant son propre `Update` ou `Draw`, `Update` ou `Draw` imbriqué appelé par une fenêtre.
- Validation : `MGUI.Tests` 3557 verts à `1cb1442e`, aucun sauté.

### ✅ T1.2 — Le bureau sans fenêtre de l'appelant

- Fixture : `AnIdleFrame(NoWindow)` (aujourd'hui `Update` 1080 octets, `Draw` 88). Ce bureau n'est pas vide : à
  chaque `Update`, `MGDesktop` insère sa fenêtre de surcouches privée (`OverlayWindow`, `MGDesktop.cs:1442-1448`,
  `:1623-1624`), une `MGWindow` de style `None` dont le contenu est un `MGOverlayHost` (`MGOverlay.cs`), et appelle
  son `Update`. Cette fenêtre fait partie de tout bureau : elle est dans l'objectif du plan (interprétation de la
  session principale, notée ici ; elle n'ajoute aucun contrôle au périmètre de D2 et D6).
- Fichiers : `MGUI/MGUI.Core/UI/MGDesktop.cs` et les classes de `MGUI.Core` que son `Update` et son `Draw` appellent
  hors d'une fenêtre (suivi d'entrée, navigation, info-bulles, animations) ; `MGUI/MGUI.Core/UI/MGOverlay.cs` ; et le
  chemin que la fenêtre de surcouches au repos parcourt dans `MGUI/MGUI.Core/UI/MGWindow.cs`, `MGElement.cs`
  (`Update`, `Draw`, dont les deux `ClipDefinition` construites par élément vers `:5323`), les hôtes à contenu unique
  dont `MGOverlayHost` dérive, `ElementDrawArgs.cs`, `ElementUpdateArgs.cs`. La liste exacte des sites est écrite sous
  la tâche par la localisation.
- Fin : `MGDesktop.Update()` et `MGDesktop.Draw(IUIDrawTransaction)` de cette fixture allouent 0 octet par image,
  souris immobile puis souris en mouvement sans bouton ; contre-éprouvé ; caractérisation identique.
- Commit (MGUI) : `perf(desktop): update and draw a desktop without windows without allocating`
- **Fait le 2026-11-15** (MGUI `fda5cd1f`), après la réponse de l'auteur à O6. Le travail avait été rangé sur la
  branche MGUI `chantier/mgui-nonalloc-t1.2-parked` (`1c31be75`), qui reste en place : sa suppression revient à l'auteur.
  `FocusArchitectureTests.cs:87` cherche maintenant `MGDesktop.FillUpdateOrder(_NestedWindows,`. Suite à `fda5cd1f` :
  3586 réussis, aucun échec, aucun sauté.
- Sites localisés (octets par image, somme 856 = la mesure de `NoWindow` après T1.1) :
  - `Navigation/UIFocusNavigationService.cs:273-283`, `TryDispatchGamePadNavigationActions` : énumérateur boxé d'un
    `IReadOnlyList` (32) et une closure par itération, 16 itérations (384) ;
  - `MGOverlay.cs:244` : `MGOverlayHost` s'abonne à son propre `OnBeginUpdate`, donc `RaiseUpdateEvent` crée un
    `ElementUpdateEventArgs` par image (136) ;
  - `MGWindow.cs:1709` : même cause pour chaque fenêtre (136) ;
  - `MGWindow.cs:1874` : `_NestedWindows.Reverse().OrderByDescending(…)` dans `OnBeginUpdateContents`, par fenêtre,
    même sans fenêtre imbriquée (136) ;
  - `MGOverlay.cs:370` : `GetVisualTreeChildren(true, false)` crée une liste par image (32).
- Corrections : boucle manette sur un tableau avec appel direct ; quatre méthodes `internal virtual` de
  `MGElement.Update` (`BeginUpdateInternal`, `BeginUpdateContentsInternal`, `EndUpdateContentsInternal`,
  `EndUpdateInternal`), appelées juste avant l'événement correspondant, que `MGWindow` et `MGOverlayHost` redéfinissent
  au lieu de s'abonner à leurs propres événements ; ordre des fenêtres imbriquées par `MGDesktop.FillUpdateOrder` ;
  `MGOverlayHost.GetVisualTreeChildren` rend la liste de base quand il n'y a pas de surcouche inactive.
- Mesures : `Update` 0 octet sur toutes les fixtures (dont `Full` et le glissement) ; `Draw` :
  `BareWindow` 1369, `DefaultWindow` 2441, `WindowWithStackPanel` 3041, `…AndTextBlock` 3289, `…AndSlider` 3489,
  `Full` 3697, glissement 3697.
- Sites vus pour la suite, non mesurés : `MGWindow.cs:2429` (`_NestedWindows.OrderBy` dans `Draw`) ;
  `MGResizeGrip.cs:98` (abonnement à `OnBeginUpdateContents` de la fenêtre hôte).

### ✅ T1.3 — Une fenêtre nue

- Fixture : `AnIdleFrame(BareWindow)` (fenêtre sans barre de titre ni contenu ; aujourd'hui `Update` 1400, `Draw`
  1673).
- Fichiers : ceux de T1.2 (ce qu'une fenêtre visible parcourt de plus dans `MGWindow.cs` et `MGElement.cs` : fond,
  bordure, ombre, états visuels), `MGUI/MGUI.Core/UI/MGBorder.cs` et `MGResizeGrip.cs` (parties de la fenêtre, D6).
- Fin : `MGDesktop.Update()` et `MGDesktop.Draw(IUIDrawTransaction)` de cette fixture allouent 0 octet par image, au
  repos (souris hors de la fenêtre) et souris immobile au-dessus de la fenêtre ; contre-éprouvé ; caractérisation des
  appels de dessin identique.
- La transaction de test rend le même `ClipScope` à chaque `Push` : le coût du clip d'un backend réel n'est pas dans
  cette preuve (T3.1).
- Commit (MGUI) : `perf(window): update and draw a bare window without allocating`
- **Fait le 2026-11-15** (MGUI `72aba460`). `Draw` : 1369 octets par image avant, 0 après, souris dehors et souris
  immobile sur la fenêtre ; `Update` était déjà à 0 (ces deux tests ne peuvent pas être vus échouer). Contre-épreuve
  des deux tests `Draw` sur `fda5cd1f` : 1369 octets chacun. Caractérisation sur 5 images : 130 lignes, préfixe
  `7465221FAD2D49C8`, figée sur le code d'avant, identique après. Suite : 3595 verts, aucun sauté.
- Sites (octets par image, estimés par type d'allocation échantillonné, somme 1369) : `MGElement.cs` ~5295 à 5311,
  quatre `ClipDefinition` par image (410), gardées par élément tant que bornes et nom sont inchangés ; noms de clip
  interpolés dans `MGElement.cs` et `MGBorder.cs:260` (214), construits une fois ; `MGResizeGrip.cs:257`, une
  `List<Vector2>` par image (332), gardée dans un champ ; `MGElement.cs:5322`, un `MGElementDrawEventArgs` par élément
  dessiné (178), créé seulement s'il y a un abonné ; `MGWindow.cs:2417`, `ParentWindows.Any` (57), boucle ;
  `MGWindow.cs:2429`, `_NestedWindows.OrderBy` (89), `MGDesktop.FillDrawOrder` ; `MGContentHost.cs:214` et `:355`,
  itérateur de `GetChildren` (89).
- **Écart au plan, décidé en session principale** : `Containers/MGContentHost.cs` (classe de base des conteneurs, D2)
  est ajouté aux fichiers de T1.3 et T1.4. La tâche s'était arrêtée sur ce site (règle d'arrêt). `MGSingleContentHost`
  dessine `Content` directement quand le type d'exécution ne redéfinit pas `GetChildren` ; sinon la boucle d'origine
  est gardée. La réponse est calculée une fois par type (réflexion, cache statique). Aucun membre public ou protégé
  ajouté.
- Écart de forme : le test `ResolvedPilotWriteSitesTests` fige la ligne 338 de `MGContentHost.cs` ; le code ajouté
  est donc placé en fin de fichier dans des déclarations `partial` des trois classes, sans déplacer les lignes
  existantes.
- Le commit du sous-agent a été refusé par le hook (« depot cible non resolu », forme de la commande) ; la session
  principale a relu le diff, relancé la suite et commité elle-même, par le même hook.

### ✅ T1.4 — La barre de titre (D6)

- Fixture : `AnIdleFrame(DefaultWindow)` (fenêtre par défaut sans contenu ; aujourd'hui `Draw` 2745).
- Fichiers : `MGUI/MGUI.Core/UI/MGWindow.cs` (barre de titre), `MGButton.cs`, `Containers/MGDockPanel.cs` ; pour
  `MGTextBlock`, seulement ce que le plan du texte n'a pas traité (un texte sans balise au repos est à zéro depuis sa
  T1.4 ; un écart mesuré ici est noté, pas corrigé dans `MGTextBlock.cs` sans le dire sous la tâche).
- Fin : `Update` et `Draw` du bureau avec cette fenêtre : 0 octet par image, au repos, souris au-dessus de la barre de
  titre, et souris au-dessus du bouton de fermeture (état survolé) ; contre-éprouvé ; caractérisation identique dans
  ces trois états. Un clic sur le bouton n'est pas dans la preuve (fermer une fenêtre alloue légitimement).
- Commit (MGUI) : `perf(window): draw the title bar without allocating`
- **Fait le 2026-11-15** (MGUI `89910c84`). `Draw` : 2441 octets par image avant T1.3, 40 après T1.3, 0 après cette
  tâche, dans les trois états ; `Update` déjà à 0. Contre-épreuve sur `72aba460` : les trois tests `Draw` échouent à
  40 octets. Le survol du bouton est établi par hit-test (`Harness.FindTitleBarPoints` vérifie `HoveredElement` et
  l'état `Hovered`). Caractérisation sur 7 images : 322 lignes, préfixe `CA21F30ACC8E2699` ; fixture `Full` sur 5
  images : 340 lignes, préfixe `4EA6EED126EC08FA` ; figées sur `fda5cd1f`. Suite : 3605 verts, aucun sauté.
- Seul site : l'énumérateur boxé de la collection d'enfants de `MGDockPanel` (`MGContentHost.cs:229`, boucle `:214`).
  `MGMultiContentHost` parcourt `_Children` par index quand le type ne redéfinit pas `GetChildren`. `MGButton.cs`,
  `MGDockPanel.cs` et `MGTextBlock.cs` ne sont pas modifiés.
- **Différence de comportement, cas d'erreur seulement** : un enfant qui ajoute ou retire des enfants de son parent
  pendant son propre `Draw` faisait lever `InvalidOperationException` par l'énumérateur ; la boucle par index ne lève
  plus et continue à l'indice suivant de la collection modifiée. Pas de test écrit pour ce cas.
- Mesuré à cet état, à prouver par T1.5 : `WindowWithStackPanel`, `…AndTextBlock`, `…AndSlider`, `Full` et le
  glissement sont à 0 octet en `Update` et en `Draw`.
- Validation moteur à `89910c84` : les deux solutions 0 erreur, `CasaEngine.Tests` 5480 verts, aucun sauté.

### ✅ T1.5 — Conteneurs, et preuve de bout en bout sans invalidation

- Fixtures : `AnIdleFrame(WindowWithStackPanel)`, `…AndTextBlock`, `…AndSlider`, `AnIdleFrame(Full)`,
  `ADragFrame_OnTheFullFixture` (aujourd'hui `Draw` de 3345 à 4241), plus trois fixtures nouvelles : la fixture
  complète posée dans un `MGGrid` (2 lignes, 2 colonnes), dans un `MGDockPanel`, et dans un `MGDockHost` à un seul
  groupe d'onglets.
- Fichiers : `Containers/MGStackPanel.cs`, `Containers/Grids/MGGrid.cs`, `Containers/MGDockPanel.cs`, les fichiers de
  `MGDockHost` et de ses parties (`Docking/Controls/`), ce qui reste dans `MGSlider.cs` au repos, et `MGTextBlock.cs`
  si son `DrawSelf` au repos alloue encore (T0.1 y mesurait 120 octets avant le plan du texte ; à remesurer en début
  de tâche).
- Fin : tous les tests de mesure de T0.1 sont des affirmations de zéro, `Update` et `Draw`, au repos et pendant un
  glissement du slider (texte du label inchangé) ; même preuve pour les trois fixtures nouvelles ; contre-éprouvés ;
  caractérisation identique. Si `MGDockHost` demande plus que ses propres fichiers (onglets, boutons d'onglet hors de
  D2 et D6), sa fixture est retirée de la preuve, sa mesure est notée ici et reportée dans T4.1 ; les autres fixtures
  restent exigées.
- Commit (MGUI) : `perf(layout): update and draw the slider's containers without allocating`
- **Fait le 2026-11-15** (MGUI `43c398b0`). Les fixtures existantes étaient déjà à 0 après T1.4 ; les trois nouvelles
  mesuraient, en octets par image (`Update` / `Draw`) : `FullInGrid` 0 / 56 au repos et 0 / 344 en glissement,
  `FullInDockPanel` 0 / 0, `FullInDockHost` 192 / 120. Après : 0 partout, au repos, souris sur la fenêtre, souris en
  mouvement et glissement. Toutes les mesures de T0.1 sont des affirmations de zéro. Suite : 3632 verts, aucun sauté.
- Sites : `MGGrid.cs`, abonnement du constructeur à son propre `OnEndingDraw` (56, un `MGElementDrawEventArgs` par
  `Draw`) et itérateur `GridSelection.GetCells` parcouru deux fois même pour une sélection de mode `None` (288) ;
  `MGDockTabGroup.cs`, `MGDockTabItem.cs`, `MGDockDropIndicators.cs`, itérateur de `GetChildren` dans `DrawContents`
  (40 chacun) ; `MGDockHost.UpdateSelf`, lecture de `IsControlDown` et `IsShiftDown` à chaque image (192, l'allocation
  est dans `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:142-143`, non modifié).
- Corrections : `MGElement.EndingDrawInternal` (`internal virtual`, appelée juste avant `OnEndingDraw`, même forme que
  les méthodes de T1.2) redéfinie par `MGGrid` ; la sélection n'est parcourue que si son mode n'est pas `None` ; les
  trois contrôles de docking dessinent leurs enfants dans l'ordre et sous les conditions de `GetChildren`, sans
  itérateur ; `MGDockHost` lit Tab d'abord, puis Ctrl et Maj seulement si Tab vient d'être pressé.
- Contre-épreuve (production d'avant) : `FullInGrid` `Draw` 56 et 344 ; `FullInDockHost` `Update` 192, `Draw` 120.
  `FullInDockPanel`, `Full` et les autres étaient déjà à 0 : leurs tests ne peuvent pas être vus échouer.
- Caractérisation, journaux figés sur le code d'avant : `Full` glissement 800 lignes `68E93B778DFA70A0` ; `FullInGrid`
  350 `E313EED065EDC31C` et 820 `FEB8EB37392C2D41` ; `FullInDockPanel` 350 `02355343A2F879CC` et 820
  `03BDAA5F77FFA1B3` ; `FullInDockHost` 575 `C2490D7D7541C9AD` et 1270 `52957F04D8CFA4E5`.
- Écarts : `MGElement.cs` touché (hors de la liste de T1.5, pour la méthode ci-dessus). `MGDockTabItem.cs` et
  `MGDockTabGroup.cs` gardent le texte `child?.Draw(DA);` dans une méthode privée `DrawChild`, parce que
  `ControlTemplateInfrastructureTests.cs:184` et `:208` cherchent ce texte dans la source ; ce test n'est pas modifié.
  À l'auteur de dire s'il préfère adapter le test. Commit fait par la session principale (hook, comme T1.3).
- Reste, hors de la preuve, pour T4.1 : une `MGGrid` avec une sélection `Row`, `Column` ou `Cell` alloue encore
  l'itérateur à chaque `Draw` (non mesuré) ; `MGDockSplitContainer`, `MGDockAutoHideStrip`, `MGDockAutoHideDrawer` et
  `MGDockPreviewOverlay` gardent `foreach (GetChildren())` (un `MGDockHost` avec séparations ou panneaux masqués n'est
  pas prouvé) ; `KeyboardTracker.IsControlDown` et `IsShiftDown` allouent quand Tab est pressé.

### ✅ T2.1 — Layout complet sans allocation (D5)

- Prérequis : T1.5 ✅ ; plan du texte terminé (c'est le cas).
- Fixture : la fixture complète, dont le `MGTextBlock` reçoit à chaque image un texte de longueur différente par
  `SetText(ReadOnlySpan<char>)` en `RelayoutParent` (le plan du texte y a mesuré environ 2850 octets par image).
- Fichiers : `MGElement.cs` (`UpdateMeasurement`, `UpdateLayout`, `InvokeLayoutChanged` et leurs caches),
  `MGWindow.cs`, `Containers/MGStackPanel.cs`, `Containers/Grids/MGGrid.cs`, `Containers/MGDockPanel.cs`.
- Fin : `Update` puis `Draw` du bureau, après chaque changement de texte, allouent 0 octet ; les `LayoutBounds` de
  chaque élément de la fixture sont égaux, image par image, à ceux du code d'avant (suite figée de 50 textes) ;
  contre-éprouvé. `MGGrid` et `MGDockPanel` sont prouvés par les fixtures de T1.5 avec le même changement de texte.
- Commit (MGUI) : `perf(layout): measure and arrange the slider's containers without allocating`
- **Fait le 2026-11-15** (MGUI `0af01881`). Jeu de 50 textes de 5 à 84 caractères avec retour à la ligne (la hauteur
  du `MGTextBlock` varie). Octets par image, `Update` / `Draw`, avant puis après : `Full` 0 / 92 puis 0 / 0 ;
  `FullInDockPanel` 0 / 92 puis 0 / 0 ; `FullInGrid` 7248 / 184 puis 0 / 0. Deux preuves par fixture : 500 images
  dans un autre ordre après réchauffe, et les 50 textes rejoués après une seule passe. Suite : 3649 verts, aucun sauté.
  Moteur à `0af01881` : les deux solutions 0 erreur, `CasaEngine.Tests` 5480 verts.
- Sites : `MGGrid.cs`, `ComputeDimensions` et `UpdateContentLayout` (7248 : dictionnaires de largeurs et de hauteurs
  par passe, tri LINQ des colonnes et lignes restantes, listes temporaires, `HasFlag` qui boxe, `foreach` sur
  interfaces, dictionnaire de `GetCellBounds`, `GeneralUtils.Min(a, b, c)` en `params`) ; `MGElement.cs`,
  `ReuseRectangleClipDefinition` (92 à 184 : une `ClipDefinition` recréée à chaque changement de bornes).
- Corrections : tableaux de dimensions gardés par la grille (une passe imbriquée prend des tableaux neufs) ; tri par
  insertion stable sur les mêmes clés ; masque au lieu de `HasFlag` ; `Math.Min` ; `_CellBounds` reconstruit seulement
  si une cellule a changé de bornes ; jusqu'à 8 `ClipDefinition` récentes gardées par élément, le tableau n'étant créé
  qu'au premier changement de bornes.
- Contre-épreuve (production d'avant) : `Full` et `FullInDockPanel`, `Draw` 4608 octets sur 50 images et 34 848 sur
  500 ; `FullInGrid`, `Update` 362 400 et 3 624 000, `Draw` 9216 et environ 69 400.
- Caractérisation figée sur le code d'avant (bornes de chaque élément, cellule, colonne et ligne, événements
  `OnLayoutUpdated` et `OnLayoutBoundsChanged` avec arguments et ordre ; puis journal de dessin) : `Full` 580 lignes
  `365EFA53674E1445` et 3421 `41F2D22E04C15451` ; `FullInGrid` 962 `844A5144A1858CF8` et 3568 `499E2F3D359CB059` ;
  `FullInDockPanel` 730 `8F19D06C4E62E78D` et 3521 `12367A53E24DBF44` ; `FullInDockHost` 1630 `845AF18D5646C839` et
  5771 `B5F7B15D0E65D0AB`. Test ajouté sur une grille riche (colonnes et lignes `Auto`, pixel, pondérées, min et max,
  spans, espacements, `SizeToContent`) : 2806 lignes `DD5522217256FEFA`.
- Écart : le site des `ClipDefinition` est dans le dessin de `MGElement.cs`, hors des méthodes citées.
- Différences de comportement : une grille dont les bornes de cellules sont inchangées garde la même instance de
  dictionnaire dans `CellBounds` (avant : un dictionnaire neuf de contenu égal) ; un élément retient jusqu'à 8
  `ClipDefinition` de plus. Non couvert par un test : des définitions de ligne ou de colonne modifiées pendant leur
  propre calcul.
- Reste, hors de la preuve, pour T4.1 : `FullInDockHost` après un changement de texte, `Update` 32 octets par image
  (`MGDockTabGroup.cs:343`, un `MGSolidFillBrush` par `SyncHeaderVisuals`) et `Draw` intermittent (jusqu'à environ 15
  octets, cause non localisée) ; fenêtre en `SizeToContent` redimensionnée à chaque texte, `Draw` 488 à 1422 octets
  (plus de 8 tailles de clip) ; grille dont les cellules changent de bornes (`_CellBounds` recréé).

### ✅ T3.1 — `ClipScope` réutilisable (D7)

- Unité à risque (API publique de `MGUI.Shared`, MGUI et moteur) : `verifier` frais après T3.2.
- Fichiers (MGUI) : `MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs`,
  `MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs` (`ClipScope` : `:1076`, `:1114` ; `ClipDefinition`, une
  classe, créée à chaque appel : `:1074`, `:1099`),
  `MGUI.MonoGame.Integration/Rendering/Clipping/ClipManager.cs` (`:38` pour la stratégie `None`, `:94`, `:121`,
  `:162`), ADR `Docs/decisions/0027-reusable-clip-scope.md` et son index.
- Les `ClipDefinition` de ces sites sont retirées sans toucher à un type public : une définition `None` gardée, et
  une définition de rectangle gardée par profondeur de pile ou une surcharge interne qui évite l'objet.
- Arrêt : si un site demande un changement public autre que celui de `ClipScope` (D7), par exemple sur
  `ClipDefinition` ou `ClipResolveResult`, ou un changement de comportement observable, la tâche écrit la mesure du
  site ici, passe en ⚠️ et s'arrête ; T3.2 ne commence pas.
- Contrat : changement **additif**. Le constructeur `ClipScope(ClipResolveResult, Action)` et `Dispose()` gardent
  leur signature et leur comportement ; tout code qui crée un `ClipScope` par `new` continue de compiler et de se
  comporter comme avant. Un `ClipScope` gagne le moyen d'être rendu à son propriétaire après `Dispose` et réarmé avec
  une nouvelle `Resolution` (forme exacte décidée dans la tâche et écrite dans l'ADR). `DrawTransaction` et
  `ClipManager` gardent leurs scopes par profondeur de pile, avec leur action de restauration en cache.
- Fin :
  - `PushRectangleClip` (avec des bornes, et avec `null`), `PushClipTemporary` (rectangle, et stratégie `None`) et
    leur `Dispose`, 500 paires après chauffe sur `DrawTransaction` (GPU réel, `[GpuFact]`, aucun test sauté sur cette
    machine) : 0 octet ; contre-éprouvé ;
  - `SetDrawSettingsTemporary` et `SetTransformTemporary` de `DrawTransaction` allouent un `TemporaryChange`
    (`:1044-1052`) : hors de cette tâche, mesure écrite ici ;
  - même rectangle de scissor et mêmes états du `GraphicsDevice` après chaque `Push` et chaque `Dispose` qu'avant,
    sur une suite de clips imbriqués à 4 niveaux (rectangle, sans clip, intersection) ; un `Dispose` appelé deux
    fois sur le même scope reste sans effet la seconde fois, tant que le scope n'a pas été réarmé ;
  - les clips à stencil et à masque (`:121`, `:162`, `PushMask`) gardent leur comportement ; s'ils allouent encore,
    leur mesure est écrite ici (ils ne sont pas dans l'image du bureau de D2).
- Commit (MGUI) : `feat(clip): let a backend reuse its clip scopes`
- **Fait le 2026-11-15** (MGUI `f9c2a408`, ADR-0027 de MGUI), exécutée avant T1.2 à T2.1 parce que ses fichiers sont
  disjoints et que T1.2 attend O6. API publique ajoutée, sur `ClipScope` seulement : `ClipScope(Action)` (scope non
  armé), `Reset(ClipResolveResult)` et `ResetRectangle(Rectangle, bool)` (réarment un scope libéré ; lèvent
  `InvalidOperationException` sur un scope encore armé) ; `Resolution` est construite à la lecture après
  `ResetRectangle`. Le constructeur d'origine et `Dispose` sont inchangés ; `ClipDefinition` et `ClipResolveResult`
  ne sont pas touchés.
- Réutilisation : `DrawTransaction` garde des emplacements (scope, rectangle et état de scissor précédents, action de
  restauration créée une fois) sur une pile d'emplacements libres ; `ClipManager` fait de même pour la stratégie
  `None`. Écart au plan : pile d'emplacements libres au lieu d'un emplacement par profondeur, pour qu'un `Dispose`
  hors d'ordre ne rende jamais un scope encore armé (noté dans l'ADR, avec le risque d'une référence périmée :
  libérer de nouveau un scope déjà réarmé libère le nouveau clip).
- Changement non prévu, nécessaire au zéro : `SetClipTarget` et la restauration créaient un `DrawSettings` par
  `CurrentSettings with {…}` ; une méthode privée `WithRasterizerType` garde la dernière paire de réglages qui ne
  diffèrent que par le type de rasterizer.
- Mesures (500 paires `Push` et `Dispose`, GPU réel) : `PushRectangleClip` avec bornes 136 000 octets avant, 0 après ;
  `PushRectangleClip(null)` 328 000, 0 ; `PushClipTemporary` rectangle 88 000, 0 ; stratégie `None` 32 000, 0 ;
  imbrication à 4 niveaux 0.
- Ce qui alloue encore, mesuré, hors de la tâche : clip à stencil 1112 octets par paire ;
  `SetDrawSettingsTemporary` 112 ; `SetTransformTemporary` 304. Le clip à masque n'est pas atteint par
  `DrawTransaction` (le résolveur choisit toujours le stencil).
- Validation : `MGUI.Tests` 3575 verts ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` 0 erreur ;
  `CasaEngine.Tests` 5479 verts ; aucun test sauté.
- **Vérification indépendante : CONFIRMED** (2026-11-15). Liste par réflexion des membres publics et protégés avant et
  après : trois membres ajoutés, rien d'autre ; 100 séquences aléatoires de clips (environ 145 000 lignes d'état du
  `GraphicsDevice`, des réglages, des diagnostics et des `Resolution`) identiques à l'octet entre l'ancien et le
  nouveau code ; zéro remesuré par le vérificateur ; tests de zéro contre-éprouvés.
- Constat P3 reporté, non corrigé : après `SetTransformTemporary`, un `PushRectangleClip(null)`, une lecture de
  `CurrentSettings.InverseTransform` puis le `Dispose` du clip, les réglages rendus par `WithRasterizerType` ne sont
  pas `Equals` à une copie neuve (l'égalité générée de `DrawSettings` compare aussi son cache privé d'inverse) :
  `PreviousSettings` et le moment où le lot est vidé diffèrent de l'ancien code. L'état du `GraphicsDevice`, les
  valeurs publiques des réglages et le rendu sont identiques ; aucun code de MGUI ni du moteur ne lit
  `InverseTransform` ni `PreviousSettings`.
- Pour T3.2 : le backend du moteur (`CasaDrawTransaction`, `CasaClipManager`, exécuteurs) doit garder ses propres
  emplacements, réarmer ses scopes par `Reset` ou `ResetRectangle`, et éviter le `with` de ses réglages à chaque
  bascule de scissor.

### 🧪 T3.2 — Mesure sur le renderer de CasaEngine (D4)

- Prérequis : T1.5 ✅ et T3.1 ✅.
- Fichiers (runtime) : `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs` (`ClipScope` : `:676`,
  `:706` ; `ClipDefinition` : `:674`, `:694` ; changements temporaires de réglages et de transformation),
  `Clipping/CasaStencilClipExecutor.cs`, `Clipping/CasaMaskClipExecutor.cs`, `Clipping/CasaClipManager.cs` (`:38`) ; test
  `CasaEngine.Tests/UI/Backend/DesktopFrameGpuAllocationTests.cs` (`GpuDeviceHost`, comme `TextSpanGpuTests`).
- Fin : la fixture complète de T1.5 (fenêtre par défaut, `MGStackPanel`, `MGTextBlock`, `MGSlider`), mise à jour et
  dessinée par `CasaDesktopRuntime` et `CasaDrawTransaction` sur GPU réel, moteur de texte FontStashSharp : 0 octet
  par image au repos et pendant un glissement ; contre-éprouvé ; pixels de l'image égaux avant et après la tâche
  (image de référence prise sur le code d'avant). Ce qui alloue dans le backend et ne peut pas être corrigé dans ces
  fichiers est listé ici avec sa mesure, et la tâche reste 🧪.
- Validation : les deux solutions du moteur 0 erreur, `CasaEngine.Tests` vert sans test sauté.
- Commit (moteur) : `perf(ui): draw a desktop frame through the engine backend without allocating`, avec la référence
  du sous-module.
- **Code fait le 2026-11-15, tâche en 🧪** : la preuve tient avec une transaction créée une fois ; le chemin réel
  du moteur crée une `CasaDrawTransaction` par image (`CasaDesktopRuntime.cs:127` et `:151`, hors des fichiers de la
  tâche), dont le constructeur alloue 2304 octets par image (5616 avant la tâche). Rendre cette transaction
  réutilisable est une décision de l'auteur (voir O7).
- Mesures (GPU réel, FontStashSharp, 500 images après 500 de chauffe, transaction créée une fois), octets par image :
  `Update` 0 avant et après ; `Draw` au repos 4752 puis 0 ; `Draw` en glissement 5472 puis 0. Contre-épreuve sur le
  code d'avant : 4752 et 5472.
- Sites : `CasaDrawTransaction.cs:376`, `GetFirstValidDrawContext` en `params` (48 par forme dessinée) ;
  `PushRectangleClip` avec bornes (288 par paire) et sans bornes (672) : `ClipDefinition`, résolution, `ClipScope`,
  closure, `with` des réglages ; `CasaClipManager.cs:38`, stratégie `None` (80 par paire).
- Corrections, sur le modèle de T3.1 : emplacements de scissor réutilisables sur une pile d'emplacements libres,
  réarmés par `ResetRectangle` ; résolution partagée pour le clip nul ; `WithRasterizerType` ; emplacement réutilisable
  pour la stratégie `None` ; surcharge à trois paramètres sans tableau. `CasaStencilClipExecutor.cs` et
  `CasaMaskClipExecutor.cs` ne sont pas modifiés.
- Pixels : cible 640x360, SHA-256 des octets RGBA figé sur le code d'avant, repos `124E0E0F…438C9F36`, glissement
  `A627D0D3…94D7BFA9` ; identiques sur 5 exécutions avant et 3 après. Ces valeurs sont propres à cette machine : un
  autre GPU ou pilote peut en donner d'autres.
- Tests ajoutés (6, `DesktopFrameGpuAllocationTests`) : deux zéros, un test de pixels, deux tests de clips imbriqués
  à 4 niveaux (rectangle de scissor, `UsesScissorTest`, `CurrentClipBounds`, `Resolution`, réglages rendus à l'état
  de départ ; passent aussi sur le code d'avant), une mesure sans affirmation.
- Validation : les deux solutions 0 erreur ; `CasaEngine.Tests` 5486 verts, aucun sauté.
- Ce qui alloue encore dans le backend, mesuré : clip à stencil 1112 octets par paire (non atteint par la fixture) ;
  `SetDrawSettingsTemporary` 112 et `SetTransformTemporary` 304 par paire (`CasaDrawStateController.cs:115-116`).
  Lu dans le code, non mesuré : `CasaLegacyShapeRenderer2D.cs:86-93` crée un `Vector2[4]` par `FillRectangle` en
  contexte `Primitives`. Le constat P3 de T3.1 sur `WithRasterizerType` vaut aussi ici.

### ⏳ T4.1 — Suite par famille de contrôles (D2), documentation et clôture

- Fin : la liste des familles de contrôles non couvertes, avec leur mesure quand une tâche l'a donnée, est dans
  `ai-agent/README.md` comme suite à planifier ; `MGUI/Docs/layout-architecture.md` et
  `docs/engine/casaengine-mgui-backend.md` disent ce qui est à zéro et ce qui ne l'est pas ; `verifier` frais sur le
  chantier (T1.1 à T3.2) ; `MGUI.Tests`, les deux solutions du moteur et `CasaEngine.Tests` verts.
- Commit (moteur) : `docs(ai-agent): close the allocation-free desktop frame and list what is left`

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les chiffres de « État vérifié » incluent la transaction enregistreuse du banc de test ; ceux de T0.1 non. | T0.1 |
| O2 | L'arbre de l'auteur a déplacé la référence du sous-module MGUI de `a3c42cb0` à `e9d6a95d` (`develop`) sans la committer. La branche MGUI part de `develop` ; le commit de référence du moteur embarque donc aussi ces deux commits de l'auteur. À confirmer par l'auteur avant tout merge. | T3.2 |
| O3 | Résolu le 2026-11-15 : D6. | — |
| O4 | Résolu le 2026-11-15 : tâches T1.0 à T1.5 réécrites par fixture, dans l'ordre de l'inventaire de T0.1. | — |
| O5 | Résolu le 2026-11-15 : D7. | — |
| O6 | T1.2 retire de `MGWindow.cs` la ligne `foreach (var Nested in _NestedWindows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))`. Le test `MGUI.Tests/Focus/FocusArchitectureTests.cs:87` (`Window_Update_Source_Prioritizes_Modal_Window_Before_NonModal_Nested_Windows`) cherche cette ligne dans le texte du fichier et échoue. Ce fichier de test était hors du périmètre confié au sous-agent ; sa modification lui a été refusée par le système de permissions, et la session principale ne l'a pas faite à sa place. Question à l'auteur : autoriser le remplacement de la chaîne cherchée par `MGDesktop.FillUpdateOrder(_NestedWindows,` (le comportement, fenêtre modale avant les fenêtres imbriquées, est aussi couvert par un test de caractérisation de T1.2) ? T1.3 à T2.1 et T3.2 attendent cette réponse. **Réponse de l'auteur le 2026-11-15 : oui.** Fait dans `fda5cd1f`. | T1.2 |
| O7 | T3.2 : `CasaDesktopRuntime` crée une `CasaDrawTransaction` par image (`CasaDesktopRuntime.cs:127`, `:151`) ; son constructeur alloue 2304 octets par image. Le dessin lui-même est à 0. Question à l'auteur : autoriser une tâche qui garde et réutilise la transaction dans `CasaDesktopRuntime` (changement de durée de vie d'un objet du backend, hors des fichiers de T3.2) ? Sans cela, T3.2 reste 🧪. | T3.2 |

## Hors périmètre

- Le pipeline de texte de `MGTextBlock` : [mgui-text-nonalloc-tasks.md](mgui-text-nonalloc-tasks.md).
