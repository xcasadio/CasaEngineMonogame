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

### ✅ T3.2 — Mesure sur le renderer de CasaEngine (D4)

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
- **✅ depuis T3.4 et T3.6 (2026-11-15)** : le runtime possède sa transaction et l'image réelle est à 0 octet. État
  au moment de T3.2 : la preuve tient avec une transaction créée une fois ; le chemin réel
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

### Suite O7 — Transaction de dessin gardée par le runtime (plan du 2026-11-15)

Demande de l'auteur le 2026-11-15 : « écris le plan pour O7 et enchaîne toutes les tâches », en mode AUTO.

Relectures : première REVISE (retrait des vues, exceptions), corrigée ; seconde REVISE, T3.3 et T3.4 sans blocage,
trois précisions sur T3.5 (test par réflexion, plan historique dans la recherche, retour arrière), corrigées ; une
relecture de clôture de T3.5 avant son exécution.

**Décision D8** (révisée le 2026-11-15 après la consigne de l'auteur : « un design moderne avec de la performance. Si
il faut casser l'API on la casse et on ne garde pas une fonctionnalité si elle est obsolète »). `CasaDesktopRuntime`
possède une seule `CasaDrawTransaction` pour toute sa vie. `CreateDrawTransaction` l'ouvre (réarmée : elle se
comporte comme une transaction neuve) ; `Dispose` la ferme. **Pas de chemin de repli** : demander une transaction
pendant que celle du runtime est ouverte lève `InvalidOperationException` (usage invalide, échec immédiat), au lieu de
créer en silence un second objet. C'est une rupture de comportement de `CreateDrawTransaction`. Enregistrée en
ADR-0097 du moteur par T3.4 (0095 et 0096 sont pris par `main`).

**Faits lus dans le code (2026-11-15).**

- Sites de création : `CasaDesktopRuntime.cs:127` (`CreateDrawTransaction`, appelé à chaque image par
  `MGDesktop.Draw`, `MGUI/MGUI.Core/UI/MGDesktop.cs:1878`) et `:151` (`DrawViews`). Aucun autre `new
  CasaDrawTransaction` hors des tests.
- État fixé par le constructeur (`CasaDrawTransaction.cs:99-113`) : `PrimitiveProjectionMatrix` (calculée depuis le
  viewport courant, propriété en lecture seule, recopiée dans `CasaDrawStateController`) ; `CasaDrawStateController`
  (`CurrentSettings` = réglages initiaux, `PreviousSettings` = `DrawSettings.Default`, `CurrentContext` = `None`) ;
  `CasaRenderTargetService` (sans état) ; renderer de formes, créé par `Options.CreateShapeRenderer` (option publique)
  ou `CasaLegacyShapeRenderer2D` ; `CasaClipManager` (compteurs de `ClipDiagnostics`, `_stencilDepth` et
  `MaxObservedDepth` de l'exécuteur de stencil, compteurs et état de l'exécuteur de masque, emplacements de clip
  réutilisables) ; `PrimitiveDrawing`. Puis `BeginDraw(DefaultContext)` si `DeferBegin` est faux.
- `Dispose` (`:876`) ne fait que `EndDraw(CurrentContext)`.
- Deux renderers de formes dans le dépôt : `CasaLegacyShapeRenderer2D` et `CasaAposShapeRenderer2D`
  (`CasaEngine.AposShapes`). Leur état propre n'a pas été lu : T3.3 commence par là.
- `ClipDiagnostics` n'est lu que par `CasaDrawTransaction` (texte de debug) et par `DesktopFrameGpuAllocationTests`.
- Des tests gardent une transaction rendue par `CreateDrawTransaction` sur plusieurs images sans la libérer
  (`DesktopFrameGpuAllocationTests.cs:112`, `TextSpanGpuTests.cs:63`).
- Appelants du dessin dans le code produit : `UIRoot.cs:193`, `WorldUIComponent.cs:112`,
  `DefaultUICompositionService.cs:15`, `CasaEngineGame.cs:731`, `GameEditor.cs:9047` et `:9099`,
  `GameEditor.Remote.cs:47`. Qu'aucun ne dessine un bureau pendant qu'un autre du même runtime est ouvert n'a pas été
  vérifié : T3.4 commence par là.
- `DrawViews` (`CasaDesktopRuntime.cs:149`) n'a aucun appelant trouvé par `rg` dans `CasaEngine`, `CasaEngine.Editor`,
  `CasaEngine.EditorServices` et `CasaEngine.Demos`.
- État par transaction omis par la première version de cette liste (relecture du 2026-11-15) :
  `CasaDrawTransaction.cs:771-773` (`_FreeScissorClipSlots`, `_RasterizerVariantA`, `_RasterizerVariantB` : une
  variante gardée après un réarmement peut changer l'identité et l'égalité de `CurrentSettings` et de
  `PreviousSettings` par rapport à une transaction neuve, à cause du cache privé d'inverse noté en T3.1) ; `:517-518`
  (`TexturedVertexBuffer`, `TexturedIndexBuffer`) ; `CasaDrawStateController.cs:11` (`_primitiveProjectionMatrix` en
  lecture seule) ; `Clipping/CasaClipManager.cs:13-16` (`_FreeNoClipSlots`, trois compteurs) ;
  `Clipping/CasaMaskClipExecutor.cs:18-19` (compteurs ; un clip à masque laissé ouvert garde sa cible de rendu louée) ;
  `CasaAposShapeRenderer2D.cs:29-31` (`_fallback`, `_batchDepth` remis dans un `finally`).
- Chaque `UIRoot` et l'éditeur construisent leur propre runtime (`UIRoot.cs:83-86`, `GameEditor.cs:389`) ; les
  dessins de capture et d'export de l'éditeur (`GameEditor.cs:9099`, `GameEditor.Remote.cs:47`) suivent le dessin
  normal, ils ne sont pas imbriqués, et sont dans un `try`/`catch` : l'éditeur continue après une exception de dessin.
- `IUIDesktopRuntime` (`MGUI/MGUI.Shared/Rendering/IUIDesktopRuntime.cs:21-22`) ne déclare que
  `CreateDrawTransaction` et `RegisterView`. `DrawViews`, `UpdateViews` et `UnregisterView` de `CasaDesktopRuntime`
  n'ont pas d'appelant dans le code produit. Le test `CasaMguiBackendOwnershipTests.cs:155` cherche dans
  `docs/engine/casaengine-mgui-backend.md` la ligne « RegisterView / UnregisterView / Views / UpdateViews / DrawViews ».

**Hors périmètre.** `MGUI/` (dont son propre `MainRenderer`, qui crée aussi une transaction par image) ; le clip à
stencil, `SetDrawSettingsTemporary`, `SetTransformTemporary` (mesures de T3.2) ; rendre la transaction du `MainRenderer`
de MGUI réutilisable (T3.5 ne fait que le renommer).

**Retour arrière.** Chaque tâche est un commit du moteur : `git revert` du commit de T3.4 rend la création par image ;
celui de T3.3 retire la méthode de réarmement. T3.5 fait deux commits : `git revert` du commit du moteur d'abord
(il rend l'ancienne référence du sous-module, donc `IUIDesktopRuntime.CreateDrawTransaction`), puis, si on veut aussi
le retirer de la branche de MGUI, `git revert` du commit de MGUI dans le sous-module.

### ✅ T3.3 — Une transaction réarmable

- Résultat : `CasaDrawTransaction` a une méthode `internal` de réarmement (réglages initiaux, `DeferBegin`, contexte
  par défaut) après laquelle elle est dans l'état d'une transaction neuve construite avec les mêmes arguments au même
  moment. Le constructeur passe par le même code. Le runtime n'est pas modifié : aucun changement de comportement.
- Fichiers (runtime) : `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`,
  `CasaDrawStateController.cs`, `Clipping/CasaClipManager.cs`, `Clipping/CasaStencilClipExecutor.cs`,
  `Clipping/CasaMaskClipExecutor.cs`, `Primitives/CasaLegacyShapeRenderer2D.cs` ;
  `CasaEngine.AposShapes/Framework/UI/Backend/MonoGame/Primitives/CasaAposShapeRenderer2D.cs` en lecture, modifié
  seulement s'il garde un état par transaction ; test nouveau
  `CasaEngine.Tests/UI/Backend/DrawTransactionRearmGpuTests.cs`.
- Étapes : (1) lister chaque champ d'instance de la transaction et de ses cinq objets, avec sa valeur à la
  construction et ce qui le modifie, liste écrite sous la tâche ; (2) écrire le réarmement, qui remet chaque champ de
  la liste ; (3) tests.
- Fin, prouvée sur GPU réel, une transaction réarmée contre une transaction neuve, même scénario : mêmes
  `CurrentSettings`, `PreviousSettings`, `CurrentClipBounds`, `ClipDiagnostics`, même état du `GraphicsDevice`
  (rasterizer, blend, depth-stencil, scissor, viewport, render target) à chaque étape, mêmes pixels, dans ces cas :
  usage précédent terminé proprement ; usage précédent laissé avec des réglages changés, un clip rectangulaire non
  libéré, un clip à stencil non libéré, un clip à masque non libéré (sa cible de rendu louée est rendue ou le fait
  est écrit ici), une variante de rasterizer en cache ; taille de viewport différente entre les deux usages (la projection des
  primitives suit) ; `DeferBegin` vrai puis faux ; réglages initiaux différents. Réarmer puis dessiner l'image de
  T3.2 : 0 octet, réarmement compris.
- Renderer de formes : il est gardé avec la transaction. Si l'un des deux renderers du dépôt garde un état par
  transaction qui ne peut pas être remis, ou si garder le renderer d'un projet externe (`CreateShapeRenderer`) change
  ce qu'il dessine, la tâche s'arrête (⚠️) avec le fait écrit ici.
- Validation : les deux solutions 0 erreur, `CasaEngine.Tests` vert sans test sauté.
- Commit (moteur) : `refactor(ui): let a draw transaction be re-armed`
- **Fait le 2026-11-15.** `CasaDrawTransaction.Rearm(DrawSettings, bool, DrawContext)` (`internal`). Aucun membre
  public ajouté, changé ou retiré ; le runtime n'est pas modifié. Validation : les deux solutions 0 erreur,
  `CasaEngine.Tests` 5540 verts (5521 et 19 nouveaux), aucun sauté.
- Champs remis par le réarmement : matrice de projection (transaction et `CasaDrawStateController`), `CurrentContext`
  (`None`), `CurrentSettings`, `PreviousSettings`, les trois compteurs de `CasaClipManager`, `_stencilDepth` et
  `MaxObservedDepth`, les deux compteurs de l'exécuteur de masque. Gardés : les piles d'emplacements de clip libres et
  les tampons de sommets (réserves sans effet observable), `PrimitiveDrawing`, `CasaRenderTargetService` et les deux
  renderers de formes du dépôt (sans état par transaction ; `_batchDepth` d'Apos vaut 0 hors appel).
- Variantes de rasterizer et `PreviousSettings` par défaut : gardés d'un usage à l'autre seulement tant qu'ils sont
  `Equals` à un jumeau jamais exposé, sinon recréés. Raison : l'égalité générée de `DrawSettings` (MGUI) compare son
  cache privé d'inverse ; les remettre à chaque réarmement ferait allouer à chaque image. Une transaction jamais
  réarmée ne crée aucun jumeau.
- Écart au plan : le constructeur ne passe pas entièrement par le code du réarmement (l'ordre de construction est
  gardé pour que `CreateShapeRenderer` voie `CurrentSettings` rempli) ; le début du dessin, la projection et le
  `PreviousSettings` par défaut sont partagés.
- Précondition du réarmement : le `Dispose` de l'usage précédent a terminé le lot ; `CurrentContext` est remis à
  `None` sans condition, ce qui rend utilisable une transaction dont le `Dispose` précédent a levé.
- Clip à masque laissé ouvert : sa cible de rendu louée reste tenue par le scope, le réarmement ne la rend pas au
  pool (comme aujourd'hui pour une transaction abandonnée). Un scope non libéré par l'usage précédent ne doit pas être
  libéré après le réarmement.
- Tests (`DrawTransactionRearmGpuTests`, 19, GPU réel) : même scénario (sprites, primitives, triangles texturés, clips
  rectangle, nul, stencil, masque, réglages temporaires) sur une transaction neuve et sur une transaction réarmée, deux
  runtimes au même historique ; réglages, clip, diagnostics et état du device comparés à chaque étape, puis pixels.
  Tous les cas de la tâche. Zéro octet, réarmement compris, total exact sur 500 images : image de T3.2 et image avec
  un clip qui coupe le scissor. Contre-épreuves : chaque champ non remis fait échouer au moins un test (projection : 2
  tests ; `PreviousSettings` : 8 ; `CurrentSettings` : 5 ; compteurs : 5 et 6 ; profondeur de stencil : 1 ;
  `CurrentContext` : 1).
- Non vérifié : les champs de `PrimitiveDrawing` (classe de MonoGame.Extended, non lue ; ses pixels sont identiques
  entre neuf et réarmé) ; un renderer de formes d'un projet externe.

### ✅ T3.4 — Le runtime possède sa transaction

- Prérequis : T3.3 ✅.
- Résultat : `CasaDesktopRuntime` crée sa transaction une fois ; `CreateDrawTransaction` l'ouvre, réarmée ; `Dispose` la ferme ; une demande pendant qu'elle est ouverte lève `InvalidOperationException` avec un
  message qui dit quoi faire. Le constructeur public de `CasaDrawTransaction` devient `internal` si aucun projet du
  dépôt ne l'appelle hors de `CasaEngine` et de ses tests (rupture d'API acceptée par D8, notée dans l'ADR).
- Étape préalable, en lecture : pour chaque appelant du dessin listé plus haut, vérifier qu'il ne peut pas demander
  une transaction au runtime pendant qu'une autre du même runtime est ouverte (UI en espace monde dessinée dans une
  cible de rendu, éditeur, capture à distance). Si un appelant du code produit le fait, la tâche s'arrête (⚠️) avec
  le fait écrit ici : le choix entre une pile de transactions et un changement de cet appelant revient à l'auteur.
- Vues : `DrawViews`, `UpdateViews` et `UnregisterView` sont retirés de `CasaDesktopRuntime` (aucun appelant,
  hors interface : fonctionnalité obsolète, D8), noté dans l'ADR. `RegisterView` reste (membre de
  `IUIDesktopRuntime`) ; `Views` et `MutableViews` restent s'ils ont un lecteur dans les deux solutions, sinon ils
  sont retirés avec le reste et `RegisterView` garde le seul comportement que l'interface exige (fait écrit ici).
  La ligne du tableau de `docs/engine/casaengine-mgui-backend.md` et l'affirmation de
  `CasaMguiBackendOwnershipTests.cs:155` sont remplacées ensemble par la ligne des membres qui existent encore :
  remplacement prévu, pas un affaiblissement au sens de la règle d'arrêt.
- Exceptions : l'état « ouverte » est remis à « fermée » dans un `finally` de `Dispose`, que `EndDraw` réussisse ou
  lève ; un réarmement ou un `BeginDraw` qui lève dans `CreateDrawTransaction` laisse la transaction fermée. Après
  une exception de dessin, l'image suivante se dessine normalement (c'est le comportement d'aujourd'hui, où chaque
  image a une transaction neuve).
- Fichiers (runtime) : `CasaDesktopRuntime.cs`, `CasaDrawTransaction.cs` ; tests
  `CasaEngine.Tests/UI/Backend/DesktopFrameGpuAllocationTests.cs`, `DrawTransactionRearmGpuTests.cs`, `CasaEngine.Tests/UI/CasaMguiBackendOwnershipTests.cs` (la ligne des vues), et les tests
  existants qui ouvrent deux transactions du même runtime à la fois, adaptés pour fermer la première (liste écrite
  ici ; une affirmation existante n'est ni retirée ni affaiblie) ; `docs/decisions/0097-…md` et son index ;
  `docs/engine/casaengine-mgui-backend.md`.
- Fin : sur GPU réel, `MGDesktop.Update()` puis `MGDesktop.Draw()` (le chemin réel, qui demande sa transaction au
  runtime) de la fixture de T3.2 : 0 octet par image au repos et en glissement, total exact sur la fenêtre mesurée
  (pas de division) ; contre-éprouvé sur le code d'avant (2304 attendus) ; pixels égaux aux valeurs figées de T3.2 ;
  une seconde demande pendant que la transaction est ouverte lève, et la première reste utilisable (état du device et
  pixels inchangés) ; après `Dispose`, la demande suivante rend le même objet, réarmé ; changement de taille du
  viewport entre deux images : pixels égaux à ceux d'un runtime neuf.
- Fin, exceptions (GPU réel) : (1) un dessin lève dans la transaction du runtime ; après `Dispose`, et aussi après un
  `Dispose` qui lève lui-même (par exemple une texture libérée restée dans le lot de sprites), la demande suivante
  réussit, rend le même objet réarmé, et l'image suivante a les pixels de référence de T3.2 ; (2) une demande avec
  `deferBegin: false` dont le début lève laisse la demande suivante réussir.
- Fin, vues : `BackendParityDocument_Lists_MainRendererResponsibilities` passe, la ligne étant la même dans le
  document et dans le test et correspondant aux membres présents ; aucune autre affirmation retirée.
- Contrat écrit dans l'ADR et la doc XML : une transaction par runtime, une seule ouverte à la fois, à ne plus
  utiliser après son `Dispose` ; le renderer de formes de `CreateShapeRenderer` est créé une fois par runtime et non
  plus une fois par image.
- Arrêt : si une affirmation d'un test existant doit être retirée ou affaiblie, ou si un appelant du dépôt utilise une
  transaction après son `Dispose`, la tâche s'arrête (⚠️) avec le fait écrit ici.
- Validation : les deux solutions 0 erreur, `CasaEngine.Tests` vert sans test sauté ; `verifier` frais sur T3.3 à
  T3.5 après T3.5 ; T3.2 passe alors en ✅ et O7 est fermé.
- Commit (moteur) : `perf(ui): let the desktop runtime own its draw transaction`
- **Fait le 2026-11-15** (ADR-0097). Validation : les deux solutions 0 erreur, `CasaEngine.Tests` 5549 verts (5540
  et 9 nouveaux), aucun sauté.
- Appelants vérifiés, aucun n'imbrique deux dessins sur un même runtime : chaque `UIRoot` a son runtime
  (`UIRoot.cs:83-86`, dont `_windowUI` de `CasaEngineGame.cs:731`) ; `WorldUIComponent.cs:112` et
  `DefaultUICompositionService.cs:15` dessinent en séquence ; `GameEditor.cs:9047` une fois par image ; l'export PNG
  (`:9099`) et la capture (`GameEditor.Remote.cs:47`) s'exécutent pendant `Update` ou après le dessin. Dans MGUI, seul
  `MGDesktop.cs:1878` demande une transaction.
- Retiré : `DrawViews`, `UpdateViews`, `UnregisterView`, `Views`, `MutableViews` (aucun appelant, aucun lecteur) ;
  `RegisterView` (interface) ne fait plus que le contrôle d'argument : l'ancienne liste n'avait pas de lecteur et
  gardait les vues en vie. Le constructeur de `CasaDrawTransaction` est `internal`. La ligne du document et celle de
  `CasaMguiBackendOwnershipTests.cs:155` sont remplacées ensemble par `| RegisterView |`.
- Réalisation : transaction créée à la première demande, réarmée ensuite ; `Dispose` remet l'état « fermée » dans un
  `finally` ; une demande pendant qu'elle est ouverte lève `InvalidOperationException` (« already open … dispose the
  transaction returned by the previous CreateDrawTransaction call »).
- Mesures, GPU réel, chemin réel (`MGDesktop.Draw(1.0f, réglages gardés)`), total exact sur 500 images : 0 octet en
  `Update` et en `Draw`, au repos et en glissement. Contre-épreuve sur le code d'avant : 2152 octets par image (et non
  les 2304 annoncés par T3.2, mesurés avec `Desktop.Draw()` sans argument : 2344). Pixels égaux aux valeurs figées de
  T3.2.
- Cas prouvés : seconde demande pendant l'ouverture (lève, état du device inchangé, la première dessine les pixels de
  référence) ; après `Dispose`, même objet réarmé ; changement de taille de viewport, pixels égaux à ceux d'un runtime
  neuf ; un dessin qui lève (`DrawTextureTo` d'une texture nulle) ; un `Dispose` qui lève ; un début qui lève avec
  `deferBegin: false`, à la première demande et à une demande ultérieure. Six des neuf tests échouent sur le code
  d'avant ; les trois autres prouvent l'égalité de comportement.
- Test existant adapté : `WhatTheFrameStillAllocates` ferme la transaction du harnais avant de mesurer
  `Desktop.Draw()` ; aucune affirmation retirée.
- Écarts : une texture libérée restée dans le lot ne fait pas lever `Dispose` sur ce backend ; le test provoque
  l'échec par un `SpriteBatch.End()` externe. Le test du chemin réel passe des réglages gardés, voir T3.6.
- **Reste trouvé : `MGDesktop.Draw()` sans argument alloue 192 octets par image**, parce que `DrawSettings.Default`
  (`MGUI/MGUI.Shared/Rendering/DrawSettings.cs:104`) crée un objet à chaque lecture et que `MGDesktop.cs:1878` le lit
  à chaque image. C'est l'appel de `UIRoot.cs:193` et de l'éditeur : l'image réelle du moteur n'est donc pas encore à
  zéro. Traité par T3.6.

### ✅ T3.5 — `CreateDrawTransaction` renommé dans MGUI

Demande de l'auteur le 2026-11-15 : « renomme aussi CreateDrawTransaction dans MGUI ».

- Prérequis : T3.4 ✅.
- Résultat : `IUIDesktopRuntime.CreateDrawTransaction` s'appelle `BeginDrawTransaction` (nom choisi par l'agent :
  la méthode ouvre une transaction que `Dispose` ferme, elle n'en crée plus forcément une ; à changer si l'auteur en
  préfère un autre). Même signature, même comportement. Rupture d'API publique de MGUI et du moteur, sans
  alias `[Obsolete]` (D8).
- Fichiers : dans `MGUI/`, l'interface (`MGUI.Shared/Rendering/IUIDesktopRuntime.cs`), `MainRenderer` (ses trois
  surcharges, publique et interne), `MGUI.Core/UI/MGDesktop.cs:1878`, `MGUI.Samples`, les implémentations et appels de
  `MGUI.Tests`, la documentation de `MGUI/Docs/` qui cite le nom, un ADR de MGUI (numéro suivant) ; dans le moteur,
  `CasaDesktopRuntime.cs`, les implémentations et appels de `CasaEngine.Tests`, `docs/` qui cite le nom, l'ADR-0097
  complété. Tout autre projet des deux solutions qui implémente ou appelle la méthode.
- Fin : `rg "CreateDrawTransaction"` ne rend plus rien dans les deux dépôts, hors de ce plan, des ADR qui racontent
  le renommage, de `ai-agent/tasks/archive/`, de `ai-agent/audits/` (lecture seule) et de
  `ai-agent/tasks/casaengine-mgui-renderer-tasks.md` (plan historique, lignes 92 et 199, laissé tel quel) ;
  `MGUI.sln`, les deux solutions du moteur 0 erreur ; `MGUI.Tests` et `CasaEngine.Tests` verts, mêmes nombres de
  tests qu'avant la tâche, aucun sauté ; `MainRenderer_ImplementsSmallDesktopRuntimeContract` passe avec
  `{ "BeginDrawTransaction", "RegisterView" }`.
- Arrêt : un test qui nomme la méthode est mis à jour dans le même commit, par remplacement du nom seulement
  (remplacement prévu) : chaîne cherchée dans une source ou un document (`MGUI.Tests/Architecture/
  HostRuntimeContractTests.cs:82`, `UIViewTests.cs:47`), nom comparé par réflexion (`HostRuntimeContractTests.cs:143`),
  `cref` de documentation (`DesktopFrameAllocationMeasurements.cs:19`), implémentation ou appel. Tout autre changement
  de test arrête la tâche.
- Commits : MGUI `refactor(rendering)!: rename CreateDrawTransaction to BeginDrawTransaction` ; moteur
  `refactor(ui)!: take the renamed BeginDrawTransaction of MGUI`, avec la référence du sous-module.
- **Fait le 2026-11-15** (MGUI `c0fca99d`, ADR-0028 de MGUI ; ADR-0097 du moteur complété). 28 fichiers dans MGUI
  (interface, `MainRenderer`, `MGDesktop.cs`, `MGUI.Samples`, 19 fichiers de test, deux documents, l'ADR et son
  index) et 12 dans le moteur (`CasaDesktopRuntime.cs` avec le texte de son exception, un `cref` de
  `CasaDrawTransaction.cs`, 8 fichiers de test, la documentation, l'ADR-0097). Aucun changement de test autre que le
  nom. `rg "CreateDrawTransaction"` hors `ai-agent/` ne rend plus que les phrases du renommage des deux ADR.
- Validation : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3649 verts, `CasaEngine.Tests` 5549
  verts, mêmes nombres qu'avant, aucun sauté.
- La commande groupée du sous-agent (`sed` sur 38 fichiers) a été refusée par le système de permissions ; la session
  principale ne l'a pas lancée à sa place, le renommage a été fait par éditions fichier par fichier.

### ✅ T3.6 — `MGDesktop.Draw()` sans argument sans allocation

Ajoutée le 2026-11-15 après la mesure de T3.4 ; dans le périmètre du chantier (D2, `MGDesktop.cs`), exécutée par la
session principale, vue par la vérification finale.

- Résultat : `MGDesktop.Draw(float, DrawSettings)` appelé sans réglages n'alloue plus de `DrawSettings` par image : le
  bureau garde ses réglages par défaut. `DrawSettings.Default` n'est pas modifié.
- Fichiers : `MGUI/MGUI.Core/UI/MGDesktop.cs` ; test MGUI dans `MGUI.Tests/Architecture/DesktopFrameZeroAllocationTests.cs` ;
  test moteur dans `CasaEngine.Tests/UI/Backend/DesktopFrameGpuAllocationTests.cs` (le chemin réel par
  `Desktop.Draw()` sans argument : 0 octet, total exact, pixels de référence).
- Fin : les deux tests à 0, contre-éprouvés (192 attendus côté moteur) ; `MGUI.Tests` et `CasaEngine.Tests` verts ;
  T3.2 passe en ✅ et O7 est fermé.
- Commits : MGUI `perf(desktop): keep the default draw settings of a desktop` ; moteur
  `perf(ui): draw a real desktop frame without allocating`, avec la référence du sous-module.
- **Fait le 2026-11-15** (MGUI `7a3d1c97`). `MGDesktop` garde un `DrawSettings` par défaut dans un champ privé ;
  `DrawSettings.Default` n'est pas modifié.
- Preuve, GPU réel : le harnais du moteur appelle maintenant `Desktop.Draw()` sans argument sur le chemin réel (il
  passait des réglages gardés) ; tous les tests du chemin réel de T3.4 (zéro au repos et en glissement, total exact
  sur 500 images, pixels de référence, exceptions) passent par cet appel. Contre-épreuve sur MGUI `c0fca99d` : 96 000
  octets sur 500 images, soit 192 par image, au repos et en glissement.
- Écart : pas de test côté MGUI. Le runtime de test de MGUI crée une transaction par appel, donc `Draw()` sans
  argument n'y est pas mesurable à zéro ; la preuve est le test GPU du moteur.
- Validation : `MGUI.Tests` 3649 verts ; les deux solutions du moteur 0 erreur ; `CasaEngine.Tests` 5549 verts ; aucun
  sauté.

### ✅ Vérification de la suite O7 (2026-11-15)

- **Vérification indépendante de T3.3 à T3.6 : CONFIRMED**, aucun constat P0 à P2. Commandes d'acceptation relancées :
  `MGUI.Tests` 3649 verts, `CasaEngine.Tests` 5549 verts, les trois solutions 0 erreur, aucun test sauté.
- Sondes du vérificateur, ancien code (`8517f76b`, MGUI `c363749b`) contre nouveau, sur GPU réel : 5 graines, 1900
  images aléatoires (dessins, clips rectangle, nul, stencil et masque, réglages, transformation et cible temporaires,
  UI en espace monde par un second runtime, changements de viewport, fins d'image normales, scopes abandonnés, dessin
  qui lève, `Dispose` qui lève, début qui lève) ; réglages, clips, diagnostics, état du device à chaque étape et
  pixels : journaux identiques à l'octet. Zéro remesuré image par image, `Update` puis `Draw` entrelacés : 0 octet en
  `Draw` au repos, souris sur le pouce, glissement, changement de texte du label, après un changement de taille ;
  l'ancien code donne 2304 par `Draw`. Fuites : 20 000 images, cibles louées et emplacements stables.
- Inventaire des champs par réflexion : seules différences entre un runtime neuf et un runtime réarmé, les réserves
  voulues. `PrimitiveDrawing` (MonoGame.Extended 6.1.1) n'a qu'un champ, le lot de primitives du runtime : le point
  « non vérifié » de T3.3 est levé.
- Recherche dans le code produit des deux solutions : aucune demande imbriquée sur un même runtime, aucune
  transaction gardée après son `Dispose`, aucune référence aux membres retirés ni au constructeur (code, réflexion,
  XAML, scripts), aucun appelant qui comptait sur `RegisterView`. `CreateShapeRenderer` n'est affecté par aucun code
  produit.
- Constats reportés, non corrigés (P4) : lire `CurrentSettings.InverseTransform` à chaque image pendant qu'une
  variante de rasterizer est courante ferait recréer la paire de variantes à chaque image (aucun code produit ne lit
  `InverseTransform` ; lu dans le code, non reproduit) ; libérer une seconde fois la transaction d'une image
  précédente ferme celle de l'image en cours (mauvais usage documenté par l'ADR-0097) ; le test de glissement mesure
  500 `Draw` d'affilée sans `Update` entre eux (la sonde entrelacée du vérificateur donne 0).
- Non vérifié : un renderer de formes d'un projet externe ; un autre GPU ou pilote ; le clip à masque par le
  résolveur normal (inatteignable avec les capacités par défaut, poussé par réflexion).
- Suite proposée à l'auteur, non faite : exclure le cache privé d'inverse de l'égalité de `DrawSettings` dans MGUI,
  ce qui supprimerait les jumeaux du réarmement et les constats P3 et P4 liés à cette égalité ; rendre aussi
  réutilisable la transaction du `MainRenderer` de MGUI.

### Suite O8 — `DrawSettings` sans cache d'inverse (plan du 2026-11-15)

Demandée par l'auteur le 2026-11-15 (« Fais la suite de manière autonome », en réponse à la suite proposée ci-dessus).

- **Décision D9.** `DrawSettings.InverseTransform` (et son champ privé `_InverseTransform`) et
  `DrawSettings.IsIdentityTransform` sont **retirés** de MGUI, au lieu d'être exclus de l'égalité. Rupture d'API
  publique, sans alias, selon la consigne de l'auteur du 2026-11-15 (design moderne, pas de fonctionnalité obsolète).
  ADR-0029 de MGUI ; ADR-0097 du moteur complété.
- Faits lus dans le code (2026-11-15) :
  - `rg "InverseTransform|IsIdentityTransform"` sur les deux dépôts, tous types de fichiers hors `ai-agent/` : seuls
    lecteurs, `DrawSettings.cs` lui-même et deux lignes de `DrawTransactionRearmGpuTests.cs:498`, `:503`, écrites pour
    prouver les jumeaux. Aucun code produit ne lit ces deux membres.
  - `DrawSettings` est un `record class` (`MGUI/MGUI.Shared/Rendering/DrawSettings.cs:84`) : son égalité générée
    compare le champ `_InverseTransform`, donc un objet dont l'inverse a été lu diffère d'un objet neuf. C'est la
    seule raison d'être des jumeaux du réarmement (`CasaDrawStateController.cs:12-15`, `CasaDrawTransaction.cs:131-140`,
    `:800-810`, `:827-829`).
  - Défaut latent du code actuel : `with { Transform = … }` (`CasaDrawTransaction.cs:714`,
    `MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs:1051`) copie le champ `_InverseTransform` et la valeur de
    `IsIdentityTransform` de l'original ; les deux seraient périmés sur la copie. Sans lecteur, sans effet aujourd'hui.
- Hors périmètre : la transaction du `MainRenderer` de MGUI (reste une suite à planifier) ; `DrawSettings.Default`
  (crée toujours un objet par lecture).

### ✅ T3.7 — Retirer le cache d'inverse de `DrawSettings` (MGUI)

- Résultat : `DrawSettings` ne porte plus que ses paramètres et `UsesScissorTest` ; deux réglages de mêmes valeurs
  sont égaux quel que soit leur passé.
- Fichiers : `MGUI/MGUI.Shared/Rendering/DrawSettings.cs` ; test `MGUI/MGUI.Tests/Rendering/DrawSettingsEqualityTests.cs`
  (nouveau) ; `MGUI/Docs/decisions/0029-draw-settings-without-inverse-cache.md` et l'index.
- Fin : `MGUI.sln` 0 erreur ; `MGUI.Tests` vert ; le test prouve l'égalité de deux `Default`, d'une copie `with { }`
  et l'inégalité sur chaque paramètre.
- Commit (MGUI) : `refactor(rendering)!: drop the inverse transform cache of DrawSettings`.
- **Fait le 2026-11-15** (MGUI `c8781dae`, ADR-0029 de MGUI). `DrawSettings` n'a plus que ses dix paramètres et
  `UsesScissorTest`. Cinq tests nouveaux, dont un qui échoue si un champ d'instance autre qu'un paramètre revient
  dans le record. Validation : `MGUI.sln` 0 erreur ; `MGUI.Tests` 3654 verts (3649 et 5 nouveaux), aucun sauté.

### ✅ T3.8 — Retirer les jumeaux du réarmement (moteur)

- Résultat : le réarmement garde toujours la paire de variantes de rasterizer et les réglages précédents par défaut ;
  plus de copie témoin, plus de drapeau `_WasRearmed`. Le constat P4 « lire `InverseTransform` à chaque image » n'a
  plus d'objet.
- Fichiers : `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`, `CasaDrawStateController.cs` ;
  `CasaEngine.Tests/UI/Backend/DrawTransactionRearmGpuTests.cs` (le test des variantes lues perd son objet et est
  retiré ; celui des variantes gardées vérifie qu'elles le sont dès le premier usage) ;
  `docs/decisions/0097-desktop-runtime-owns-its-draw-transaction.md` ; la référence du sous-module.
- Fin : les deux solutions du moteur 0 erreur ; `CasaEngine.Tests` vert, dont tous les tests de réarmement et le zéro
  du chemin réel ; vérification indépendante de T3.7 et T3.8.
- Retour arrière : `git revert` des deux commits (moteur puis MGUI).
- Commit (moteur) : `refactor(ui): drop the re-arm twins of the draw settings`, avec la référence du sous-module.
- **Fait le 2026-11-15** (moteur `fdd7b215`), vérification CONFIRMED. Retirés : les deux jumeaux des
  variantes, le jumeau des réglages précédents par défaut, le drapeau `_WasRearmed` ; `_defaultPreviousSettings` est
  `readonly`. Les variantes créées avant le premier réarmement sont maintenant gardées aussi.
- Tests : `Rearm_AfterRasterizerVariantsThatWereRead_IsLikeANewTransaction` devient
  `Rearm_AfterRasterizerVariantsWereCreated_IsLikeANewTransaction` (sans lecture d'inverse, le membre n'existe plus) ;
  `…ThatWereNotRead_KeepsThem…` devient `Rearm_KeepsTheRasterizerVariantsAndTheDefaultPreviousSettings` et part de
  l'usage du constructeur. Aucune autre affirmation retirée.
- Validation : les deux solutions du moteur 0 erreur ; `CasaEngine.Tests` 5549 verts, aucun sauté.

### ✅ Vérification de la suite O8 (2026-11-15)

- **Vérification indépendante de T3.7 et T3.8 : CONFIRMED**, aucun constat P0 à P2. Commandes relancées par le
  vérificateur : `MGUI.Tests` 3654 verts, `CasaEngine.Tests` 5549 verts, les trois solutions 0 erreur, aucun sauté ;
  les 19 tests de réarmement et les 15 tests du chemin réel sont parmi les verts.
- Confirmé par lecture : aucun lecteur des deux membres retirés (code, XAML, scripts, noms en chaîne) ; une variante
  gardée n'est rendue que si les réglages courants sont le même objet que l'autre variante de la paire, sinon une
  nouvelle est créée ; aucun appelant de `SetDrawSettings` ne comptait sur l'inégalité de deux réglages de mêmes
  valeurs ; aucune affirmation de test retirée.
- Constats reportés, non corrigés (P4) : `DrawSettings` n'est pas `sealed`, un record dérivé avec un état modifiable
  rendrait une variante gardée périmée (aucun type n'en dérive) ; la paire de variantes garde en vie deux objets de
  réglages et leur `BackendEffect` jusqu'au prochain changement de paire.

### ✅ T4.1 — Suite par famille de contrôles (D2), documentation et clôture

- Fin : la liste des familles de contrôles non couvertes, avec leur mesure quand une tâche l'a donnée, est dans
  `ai-agent/README.md` comme suite à planifier ; `MGUI/Docs/layout-architecture.md` et
  `docs/engine/casaengine-mgui-backend.md` disent ce qui est à zéro et ce qui ne l'est pas ; `verifier` frais sur le
  chantier (T1.1 à T3.2) ; `MGUI.Tests`, les deux solutions du moteur et `CasaEngine.Tests` verts.
- Commit (moteur) : `docs(ai-agent): close the allocation-free desktop frame and list what is left`
- **Fait le 2026-11-15.** Documentation : `MGUI/Docs/layout-architecture.md`, section « Image du bureau sans
  allocation » ; `docs/engine/casaengine-mgui-backend.md`, section « Allocation-free desktop frame ». Suite à
  planifier écrite dans `ai-agent/README.md`.
- **Vérification indépendante du chantier (T1.2 à T3.2) : CONFIRMED**, aucun constat P0 à P2. Sondes du vérificateur,
  ancien code contre nouveau, comparées à l'octet : 400 grilles aléatoires (315 712 lignes : dimensions, bornes des
  cellules, journaux de dessin, sélections, abonné à `OnEndingDraw`, lignes et colonnes ajoutées ou retirées), hôtes
  de contenu (redéfinitions de `GetChildren` à toute profondeur, masquage par `new`), cache de clips, fenêtres
  imbriquées, docking (Ctrl+Tab), scopes de clip du backend et pixels : identiques. Zéro remesuré image par image sur
  2000 images (`Full`, `FullInGrid`, `FullInDockPanel`, y compris `Draw` après changement de texte et souris réellement
  sur le slider) et sur GPU réel. Membres publics et protégés listés par réflexion : rien de retiré ni de changé.
- Constats reportés, non corrigés :
  - P4, force des tests : les tests de zéro divisent le total par 500 (jusqu'à 499 octets sur la fenêtre mesurée se
    liraient 0) ; les tests de changement de texte n'affirment que `Update` ; le point « souris sur le slider » des
    tests est en fait sur le `MGStackPanel` ou la grille. Les sondes du vérificateur couvrent ces trois trous et
    donnent 0.
  - P3, introduit par T3.2 : une référence de `ClipScope` libérée de nouveau après la réutilisation de son
    emplacement libère le clip plus récent (contrat documenté par l'ADR-0027 de MGUI ; aucun code des deux dépôts ne
    garde un scope après sa libération).
  - P4 : `MGSingleContentHost` et `MGMultiContentHost` déclarent maintenant `protected override DrawContents` (même
    signature qu'avant ; le `base.DrawContents` d'une sous-classe s'y lie).
  - P4 : `MGTextBox` et `MGGridColorPicker` passent un nom de clip neuf à chaque dessin : le cache de clips ne sert
    jamais pour eux (une définition par dessin comme avant, plus un court parcours).
  - Non vérifié : journaux de `MGDockDropIndicators` et `MGDockTabGroup` pendant un vrai glissement d'onglet (lus dans
    le code seulement) ; un autre GPU ou pilote.
- Validation finale : `MGUI.Tests` 3649 verts ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` 0
  erreur ; `CasaEngine.Tests` 5486 verts ; aucun test sauté.
- Le plan reste dans `tasks/` : T3.2 est 🧪 (O7) et l'auteur n'a pas encore accepté le chantier.

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les chiffres de « État vérifié » incluent la transaction enregistreuse du banc de test ; ceux de T0.1 non. | T0.1 |
| O2 | L'arbre de l'auteur a déplacé la référence du sous-module MGUI de `a3c42cb0` à `e9d6a95d` (`develop`) sans la committer. La branche MGUI part de `develop` ; le commit de référence du moteur embarque donc aussi ces deux commits de l'auteur. À confirmer par l'auteur avant tout merge. | T3.2 |
| O3 | Résolu le 2026-11-15 : D6. | — |
| O4 | Résolu le 2026-11-15 : tâches T1.0 à T1.5 réécrites par fixture, dans l'ordre de l'inventaire de T0.1. | — |
| O5 | Résolu le 2026-11-15 : D7. | — |
| O6 | T1.2 retire de `MGWindow.cs` la ligne `foreach (var Nested in _NestedWindows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))`. Le test `MGUI.Tests/Focus/FocusArchitectureTests.cs:87` (`Window_Update_Source_Prioritizes_Modal_Window_Before_NonModal_Nested_Windows`) cherche cette ligne dans le texte du fichier et échoue. Ce fichier de test était hors du périmètre confié au sous-agent ; sa modification lui a été refusée par le système de permissions, et la session principale ne l'a pas faite à sa place. Question à l'auteur : autoriser le remplacement de la chaîne cherchée par `MGDesktop.FillUpdateOrder(_NestedWindows,` (le comportement, fenêtre modale avant les fenêtres imbriquées, est aussi couvert par un test de caractérisation de T1.2) ? T1.3 à T2.1 et T3.2 attendent cette réponse. **Réponse de l'auteur le 2026-11-15 : oui.** Fait dans `fda5cd1f`. | T1.2 |
| O7 | T3.2 : `CasaDesktopRuntime` crée une `CasaDrawTransaction` par image (`CasaDesktopRuntime.cs:127`, `:151`) ; son constructeur alloue 2304 octets par image. Le dessin lui-même est à 0. Question à l'auteur : autoriser une tâche qui garde et réutilise la transaction dans `CasaDesktopRuntime` (changement de durée de vie d'un objet du backend, hors des fichiers de T3.2) ? Sans cela, T3.2 reste 🧪. **Résolu le 2026-11-15** : D8, T3.3 à T3.6, ADR-0097. | — |

## Hors périmètre

- Le pipeline de texte de `MGTextBlock` : [mgui-text-nonalloc-tasks.md](mgui-text-nonalloc-tasks.md).
