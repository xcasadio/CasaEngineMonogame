# Plan agent IA — Fin du zéro allocation du dessin MGUI (contours, géométries de clip, appelants)

Plan d'exécution des restes notés à la clôture des chantiers archivés
[mgui-nonalloc-suite-tasks.md](archive/mgui-nonalloc-suite-tasks.md) et
[mgui-polygon-triangulation-tasks.md](archive/mgui-polygon-triangulation-tasks.md) (point P6, « Restes »).
L'auteur a demandé le 2026-11-17, après la liste des restes proposée par l'agent : « oui » à un plan « fin du zéro
allocation du dessin ». Les arbitrages de l'agent sont les points P1 → P7, à valider à l'approbation du plan.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Dessiner à chaque image, sans allocation et avec le même rendu, ce qui alloue encore dans le dessin de MGUI après les
deux chantiers précédents : le contour d'un polygone dans les deux backends, la géométrie de clip triangulée des
formes (`MGEllipse`, `MGPolygon`, `MGPathLite`), le triangle de `MGRatingControl`, les animations de
`MGHighlightBorderBrush`, et le reste d'un glissement d'onglet qui change de groupe dans `MGDockHost`. Ce que le
chantier ne fait pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-11-17)

- Branches `chantier/mgui-draw-nonalloc-finish` créées dans le moteur depuis `main` `52781879` et dans MGUI depuis
  `develop` `98bf9edf`. Arbre de travail : seul `Projects/SampleProject/.casaeditor/viewport.editor.json` est non suivi
  (fichier de l'auteur, à ne jamais indexer). Références des suites : `MGUI.Tests` 4006, `CasaEngine.Tests` 6129,
  aucun sauté.
- **Contour d'un polygone.** `StrokeAndFillPolygon` remplit puis appelle `StrokePolygon` en contexte primitives
  (`MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs:703-720` ;
  `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs:236-251`). `StrokePolygon`
  (`DrawTransaction.cs:726-785`, `CasaLegacyShapeRenderer2D.cs:253-310`) teste `Vertices.Any()` ; arêtes centrées
  sur les sommets : `Vertices.SelectConsecutivePairs(true)` puis `StrokeLineSegment` par arête ; arêtes intérieures :
  `Triangulator.DetermineWindingOrder(Vertices.Append(Vertices[0]).ToArray())` quand le sens n'est pas donné, puis en
  contexte sprites `SpriteBatch.DrawPolygon(origine, Triangulator.EnsureWindingOrder(Vertices.ToArray(), ...))`, en
  contexte primitives deux `FillTrianglePrimitive` par arête issue de `SelectConsecutivePairs(true)`.
  `SelectConsecutivePairs(true)` (`MGUI/MGUI.Shared/Helpers/GeneralUtils.cs:104-114`) est fait de `Skip`, `Any`, `Zip`,
  `Append` et `First`. Les deux `IUIDrawContext.StrokeAndFillPolygon` explicites convertissent une énumération qui
  n'est pas une `IReadOnlyList` par `ToList()` (`DrawTransaction.cs:45-46`, `CasaDrawTransaction.cs:39-40`).
  Appelants à chaque image : `MGEllipse.cs:121`, `MGPolygon.cs:46`, `MGPathLite.cs:88`, `MGRatingControl.cs:672`,
  `:693`.
- **Géométrie de clip triangulée.** `MGVectorShapeHelper.CreateTriangulatedClipGeometry`
  (`MGUI/MGUI.Core/UI/Shapes/MGVectorShapeHelper.cs:246-268`) crée un tableau de sommets, appelle
  `Triangulator.EnsureWindingOrder` et `Triangulator.Triangulate` de MonoGame.Extended (tableaux neufs), puis
  `CreateGeometryClipDefinition` (`MGElement.cs:5290`) crée un `ClipDefinition` neuf (`ClipDefinition` est un
  `sealed record class`, `ClipGeometry` un `readonly record struct` de deux `IReadOnlyList`,
  `MGUI/MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs:42`, `:56`). Appelé à chaque dessin d'une forme remplie par
  un pinceau (`MGEllipse.cs:114`, `MGPolygon.cs:39`, `MGPathLite.cs:81`) et par les crochets de clip propre de ces
  formes (`MGEllipse.cs:170`, `MGPolygon.cs:94`, `MGPathLite.cs:154`). Une même forme construit donc plusieurs
  définitions par image : une de remplissage (`.Fill`) dans `DrawSelf` et une de clip propre (`.Self`), de noms et de
  translations différents ; `MGPathLite` construit une définition `.Fill` par figure fermée, dans une boucle
  (`MGPathLite.cs:71-84`), et son clip propre ne vaut que pour une figure seule (`:139-154`). Les tableaux de sommets
  sources sont remplacés quand ils changent (`MGEllipse.cs:182`, `MGPolyline.cs:79`, `MGPathLite.cs:221`). Le
  gestionnaire de clips ne garde une définition que de son `Push` à son `Pop`
  (`MGUI/MGUI.MonoGame.Integration/Rendering/Clipping/ClipManager.cs:82-130`).
- **Triangle de `MGRatingControl`.** `MGRatingControl.cs:738-752` : `ShapeVertices256[Triangle].Select(...).ToList()`
  et `SelectConsecutivePairs(true)` pour le contour, à chaque dessin ; la suite de la branche (remplissage partiel)
  est à lire dans la tâche.
- **`MGHighlightBorderBrush`.** Le contrat `IBorderBrush.Draw(ElementDrawArgs, MGElement, MGBoxShape, MGBoxGeometry)`
  (`MGUI/MGUI.Core/UI/Brushes/BorderBrushes/IBorderBrush.cs:32`) est implémenté à `MGHighlightBorderBrush.cs:922` ;
  une autre surcharge `Draw(..., Rectangle Bounds, Thickness BT)` existe à `:579`. La seconde crée à chaque dessin des
  listes et dictionnaires (`new List<Rectangle>` `:609`, `:812`, `:835`, `new Dictionary` `:709`, `:744`,
  `Distinct().ToList()` `:741`, `:797`, `AsEnumerable().Reverse()` `:753`). Quatre animations : `Pulse`, `Flash`,
  `Progress`, `Scan`. Pour `Progress` et `Scan`, la surcharge du contrat choisit entre deux chemins (`:933-952`) : sur
  une géométrie à maillage d'anneau (coins arrondis), `DrawProgressOnRing` et `DrawScanOnRing`, qui créent des
  tableaux à chaque dessin (`new int[n]`, `new double[n + 1]`, `:998-999`) ; sur une boîte à coins droits, la
  surcharge `:579`, appelée à `:940`. Les deux surcharges sont donc dans le périmètre.
- **Glissement d'onglet qui change de groupe.** Mesuré par le vérificateur de T5.9 du plan archivé (sonde hors dépôt) :
  0 à 32 octets par image après T5.7 (32 pour un glissement sur le bord haut de l'hôte à cheval sur deux groupes) ;
  l'exécutant de T5.7 avait vu 96 à 128 octets dans un autre montage, cause non cherchée (hypothèse : réaffichage des
  indicateurs de dépôt quand le groupe survolé change).
- **Frappe dans `MGTextBox`.** Reste de 356 octets par image (T5.8 du plan archivé) : géométrie gardée par le curseur,
  arguments de `TextChanged` et du changement de bornes de layout, `EscapeMarkdown`, chaîne de la touche.
- Outils de preuve existants : bancs de mesure `ControlBench` et `FrameAllocationMeter`, journaux de dessin figés
  (`MGUI/MGUI.Tests/Graph/OrderedRecordingDrawTransaction.cs`, `FrozenJournal`), sonde des sommets du lot
  `MGUI/MGUI.Tests/Rendering/PrimitiveBatchProbe.cs` (liée dans `CasaEngine.Tests`), corpus de polygones
  `PolygonCorpus.cs`, `PolygonTriangulator` (MGUI ADR-0038).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Demande de l'auteur du 2026-11-17 : un plan « fin du zéro allocation du dessin » sur la liste des restes proposée par l'agent (contour d'un polygone, géométrie de clip triangulée, triangle de `MGRatingControl`, `MGHighlightBorderBrush`, glissement d'onglet qui change de groupe, frappe dans `MGTextBox`). |
| D2 | Plan approuvé par l'auteur le 2026-11-17 (« Approuvé, lance-le »), P1 à P7 compris, après une relecture READY. La frappe dans `MGTextBox` reste hors de ce plan (P6). |

## Points à valider à l'approbation (arbitrages de l'agent)

| Réf | Proposition | Autre voie |
|---|---|---|
| P1 | Mesurer d'abord (T0.1). Un cas mesuré à 0 est fermé sans code ; une allocation dont la cause est hors des fichiers de sa tâche est mesurée, écrite, et la tâche s'arrête en ⚠️ (règle D8 du plan archivé). | Écrire le code sans mesure préalable. |
| P2 | Contour : même géométrie et même ordre d'appels qu'aujourd'hui, écrits sans itérateur ni tableau temporaire (boucle par index sur les arêtes, dernière arête vers le premier sommet seulement si plus de deux sommets, comme `SelectConsecutivePairs(true)`) ; sens d'enroulement calculé par la même somme, dans le même ordre, que `DetermineWindingOrder` sur le tableau fermé. Le contour en contexte sprites (`SpriteBatch.DrawPolygon` de MonoGame.Extended) garde cet appel : s'il alloue lui-même, c'est mesuré et écrit, sans remplacement. | Remplacer aussi `SpriteBatch.DrawPolygon` (rendu à reprouver). |
| P3 | Géométrie de clip : la forme garde une entrée (géométrie triangulée par `PolygonTriangulator`, et son `ClipDefinition`) **par usage** : une pour le remplissage (`.Fill`) et une pour le clip propre (`.Self`) de `MGEllipse` et de `MGPolygon` ; pour `MGPathLite`, une par figure fermée remplie, plus celle du clip propre. Chaque entrée est gardée tant que la référence du tableau de sommets source, la translation et les bornes cibles sont les mêmes, et refaite (définition neuve, jamais réécrite en place) dès que l'une change. Au repos et pendant une animation qui ne déplace pas la forme : 0 ; un déplacement ou un redimensionnement recrée la définition (opération ponctuelle, hors de la cible D7 du plan archivé). | Réécrire les tableaux en place dans une même définition (une définition gardée par le gestionnaire de clips, ou comparée par référence, verrait ses valeurs changer : à prouver sûr d'abord). |
| P4 | `MGHighlightBorderBrush` : même algorithme, collections gardées par l'instance, mêmes sommets et rectangles dans le même ordre (y compris l'ordre que donne `Distinct`, première occurrence gardée) ; preuve par journaux figés sur l'ancien code pour chaque animation, à plusieurs instants. Les deux surcharges de `Draw` sont traitées (`:579` est appelée par `:940`), et pour `Progress` et `Scan` les deux chemins : boîte à coins droits et anneau à coins arrondis. | Réécrire l'animation. |
| P5 | Glissement d'onglet qui change de groupe : tâche d'enquête. Mesure reproductible dans le dépôt, cause trouvée et nommée ; corrigée si elle est dans les fichiers du dock et si le comportement reste identique (rejeu du scénario figé de T5.7) ; sinon ⚠️ avec le fait. Quatre issues, chacune avec sa fin (T1.5) : non reproduit, cause dans le dock, cause ailleurs, non attribué dans le budget. | Laisser comme limite connue. |
| P6 | Frappe dans `MGTextBox` hors de ce plan : les parts qui restent demandent de réutiliser les arguments d'événements de MGUI (`TextChanged`, changement de bornes), un changement de contrat comme celui de l'entrée (ADR-0034), qui mérite sa propre décision ; la chaîne de la touche est le texte lui-même. | L'inclure, avec un ADR de contrat. |
| P7 | Mêmes preuves que les chantiers précédents : sommets du lot lus par la sonde pour les chemins de primitives des backends ; journaux de dessin figés sur l'ancien code pour les appelants de `MGUI.Core` ; pixels comparés dans le test, jamais d'empreinte figée en constante ; une mutation volontaire par preuve. | — |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/mgui-draw-nonalloc-finish`** dans le moteur (depuis `main`) et dans MGUI (depuis
  `develop`), déjà créées. Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche et par dépôt** : le commit de MGUI d'abord, puis celui du moteur, qui porte la référence du
  sous-module, le code moteur de la tâche et ce plan. Message en anglais `type(area): summary`. Chaque commit du
  moteur compile avec la référence de MGUI qu'il porte ; vérifier `git -C MGUI status` après chaque commit de MGUI.
- **Ne jamais pousser.** La fusion dans `main` et `develop` reste une décision de l'auteur.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Le comportement de MonoGame.Extended se lit dans la DLL 6.1.1 décompilée (`ilspycmd`). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build MGUI/MGUI.sln`,
  `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln`. **Tests** :
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans
  `--no-build`, tout vert, aucun test sauté (les tests GPU tournent sur cette machine).
- **Mesure avant et après** : chaque tâche qui vise zéro écrit sous elle les octets mesurés avant et après (500
  images ou appels après 500 de chauffe ; un test de zéro arrondit la moyenne vers le haut et affirme sur le total).
- **Rendu inchangé** : selon P7.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais ; les fichiers
  d'architecture de `MGUI/Docs/*.md` en français sans accents, comme ils le sont.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build MGUI/MGUI.sln`, `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln` :
  0 erreur.
- `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout
  vert, aucun test sauté (références 4006 et 6129 avant ce plan).
- Smoke manuel, par l'auteur : `MGUI.Samples` et l'éditeur ; ellipses, polygones et chemins avec contour et avec
  remplissage par pinceau, contrôle de notation (triangle), bordures en surbrillance (les quatre animations),
  glissement d'onglet d'un groupe à l'autre : rien ne doit changer à l'écran.

---

## Phase 0 — Mesure

### ✅ T0.1 — Mesure de chaque reste

- Objectif : les octets par image (Update et Draw) de chaque cas du plan, sur le code d'avant, avec des fixtures qui
  restent dans la suite ; chaque tâche suivante part de ces chiffres (P1).
- Fichiers : `MGUI/MGUI.Tests/Architecture/ControlFamilyZeroAllocationTests.cs` (ou un fichier voisin selon le
  modèle existant) ; tests GPU des deux backends pour `StrokePolygon` seul (`MGUI/MGUI.Tests/Integration/`,
  `CasaEngine.Tests/UI/Backend/`).
- Étapes :
  1. Fixtures au repos et en animation : `MGEllipse`, `MGPolygon`, `MGPathLite` avec contour plein, avec remplissage
     par pinceau, avec clip propre, **avec remplissage par pinceau et clip propre sur la même forme**, et un
     `MGPathLite` d'au moins deux figures fermées remplies par un pinceau ; `MGRatingControl` en triangle (plein et
     partiel) ; `MGHighlightBorderBrush` sur chacune des quatre animations, à plusieurs instants, `Progress` et `Scan`
     étant mesurées sur une boîte à coins droits et sur une boîte à coins arrondis dont la géométrie a un maillage
     d'anneau (le test affirme lequel des deux chemins a tourné) ; glissement d'onglet qui change de groupe à chaque image,
     dans les montages de T5.7 et du vérificateur de T5.9 (bord haut de l'hôte à cheval sur deux groupes).
  2. Sur GPU réel, dans chaque backend : `StrokePolygon` d'un triangle, d'une flèche et d'une ellipse de 32 segments,
     arêtes centrées et intérieures, contextes primitives et sprites ; `StrokeAndFillPolygon`.
  3. Les tests de cette tâche affirment les chiffres mesurés (égalité au reste mesuré), pour que chaque tâche suivante
     les resserre ; un cas à 0 est noté et sa tâche fermée sans code.
- Validation : chiffres écrits sous la tâche ; trois solutions 0 erreur, deux suites vertes, aucun test sauté.
- Commit : `test(ui): measure what the drawing of MGUI still allocates` (MGUI), puis le commit du moteur.
- Validation (2026-11-17) : les trois solutions 0 erreur ; `MGUI.Tests` 4068 verts (62 tests ajoutés),
  `CasaEngine.Tests` 6130 verts (1 ajouté), aucun sauté. Écrit par un `executor` ; aucun code produit touché. Chaque
  chiffre est stable sur trois exécutions et affirmé par égalité ; chaque fixture affirme, sur un journal de dessin,
  que le chemin visé a tourné.
  - Obstacle d'environnement levé en session principale : le serveur MCP `casa-editor` de la session tourne depuis
    `Tools/CasaEngine.Editor.Mcp/bin/` et verrouillait trois fichiers que le build recopie (MSB3021). Ils ont été
    renommés en `*.locked-by-running-mcp` (dossier `bin/`, hors git), sans arrêter le serveur ; le build de
    l'éditeur et la suite du moteur ont ensuite été rejoués en entier.

  **T1.1, contour, GPU réel, octets par appel, identiques dans les deux backends sauf mention :**

  | Appel | Arêtes centrées | Arêtes intérieures, sens donné | Arêtes intérieures, sens calculé |
  |---|---|---|---|
  | `StrokePolygon`, primitives | 280 | 280 | 400 / 432 / 632 (triangle, flèche, ellipse) |
  | `StrokePolygon`, sprites | 280 | 48 / 80 / 280 | 168 / 232 / 632 |
  | `StrokeAndFillPolygon` | 280 | MGUI 400 / 432 / 632 ; moteur 280 | 400 / 432 / 632 |
  | `SpriteBatch.DrawPolygon` seul | — | 0 | — |

  Les 280 octets sont la chaîne de `SelectConsecutivePairs(true)`, quel que soit le nombre de sommets.
  `SpriteBatch.DrawPolygon` n'alloue pas : rien à noter pour P2. Écart entre backends à garder : le
  `StrokeAndFillPolygon` de MGUI passe toujours un sens nul à `StrokePolygon`, celui du moteur transmet le sens reçu.

  **T1.2, géométrie de clip, octets par image au dessin (Update à 0) :**

  | Cas | `MGEllipse` | `MGPolygon` | `MGPathLite` |
  |---|---|---|---|
  | Remplissage plein, contour, ou les deux | 0 | 0 | 0 |
  | Remplissage par pinceau | 4392 | 1152 | 1152 |
  | Clip propre | 4392 | 1152 | 1152 |
  | Remplissage par pinceau et clip propre | 8784 | 2304 | 2304 |
  | Deux figures fermées remplies par pinceau | — | — | 1136 |

  Une opacité animée à chaque image ne change pas ces chiffres. Constat à traiter en T1.2 : le nom du clip
  (`$"{ElementType}.Fill"`, `.Self`) est formaté à chaque image (48 octets), et la première image mesurée porte
  4872 octets de plus (cache des noms d'énumération vidé par la collecte qui ouvre la fenêtre de mesure) ; les
  totaux affirmés valent le coût par image fois 500, plus 4872.

  **T1.3, triangle de `MGRatingControl` (quatre éléments), octets par image au dessin :** plein sans contour 1792,
  plein avec contour 4096, partiel sans contour 1712, partiel avec contour 3728 ; horizontal et vertical identiques.

  **T1.4, `MGHighlightBorderBrush`, octets par image au dessin, à six instants :**

  | Géométrie | `Pulse` | `Flash` | `Progress` | `Scan` |
  |---|---|---|---|---|
  | Coins droits (surcharge `:579`, directe ou par le contrat) | 0 | 0 | 2248 à 2472 | 240 |
  | Anneau à coins arrondis | 0 | 0 | 488 | 448 |

  `Pulse` et `Flash` sont déjà à 0 : fermés sans code (P1).

  **T1.5, glissement qui change de groupe à chaque image, octets par image (Update / Draw) :** 0 / 0 sur six
  montages (centres de deux groupes, réordonnancement puis centre, zones gauche et haute de deux groupes, bande de
  proximité, bord bas de l'hôte à cheval sur deux groupes) ; **32 / 0 sur le bord haut de l'hôte à cheval sur deux
  groupes**. Les 96 à 128 octets vus en T5.7 du plan archivé ne sont reproduits par aucun montage.

  Non mesuré : une forme qui se déplace ou change de taille (hors cible) ; le contour dans les fixtures de
  `MGUI.Core` (la transaction du banc ne dessine pas les polygones : seuls les tests GPU le couvrent).

## Phase 1 — Restes

### ✅ T1.1 — Contour d'un polygone sans itérateur ni tableau temporaire

- Objectif : `StrokePolygon` et `StrokeAndFillPolygon` n'allouent plus dans les deux backends (P2), mêmes sommets
  envoyés au lot et mêmes pixels.
- Fichiers : `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs`,
  `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs`, et les deux
  `IUIDrawContext.StrokeAndFillPolygon` explicites (`DrawTransaction.cs:45-46`, `CasaDrawTransaction.cs:39-40`) ;
  tests GPU des deux backends.
- Étapes :
  1. Figer sur l'ancien code, par la sonde des sommets du lot, les sommets écrits par `StrokePolygon` et
     `StrokeAndFillPolygon` (arêtes centrées et intérieures, deux sens, sens donné et calculé) sur le corpus de
     polygones, avec une origine fractionnaire ; mêmes appels sur le nouveau code, comparaison au bit près.
  2. Réécrire selon P2. Un `IEnumerable` qui n'est pas une liste garde sa copie (cas rare, comme pour `FillPolygon`).
  3. Contexte sprites : mesurer `SpriteBatch.DrawPolygon` seul ; s'il alloue, écrire le chiffre (P2) ; pixels et
     journal inchangés.
  4. Mutations vérifiées puis annulées : oublier la dernière arête ; inverser le sens calculé.
- Validation : 0 octet (ou le reste de `SpriteBatch.DrawPolygon`, écrit) ; sommets du lot identiques ; pixels
  identiques ; journaux figés inchangés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(rendering): stroke a polygon without allocating` (MGUI), puis
  `perf(ui): stroke a polygon without allocating` (moteur).
- Validation (2026-11-17, GPU réel) : les trois solutions 0 erreur ; `MGUI.Tests` 4073 verts, `CasaEngine.Tests` 6135
  verts (5 tests ajoutés de chaque côté), aucun sauté. Écrit par un `executor`, puis contesté par trois relecteurs
  indépendants en lecture seule (identité du comportement, solidité des preuves, chemin chaud et périmètre) : trois
  verdicts CONFIRMED, aucun constat P0 à P3.

  | Octets par appel | Avant | Après |
  |---|---|---|
  | `StrokePolygon`, arêtes centrées (deux contextes) | 280 | 0 |
  | `StrokePolygon`, arêtes intérieures, primitives, sens donné / calculé | 280 / 400 à 632 | 0 |
  | `StrokePolygon`, arêtes intérieures, sprites, sens donné / calculé | 48 à 280 / 168 à 632 | 0 |
  | `StrokeAndFillPolygon` | 280 à 632 | 0 |

  - 144 lignes de mesure par backend (six formes dans les deux sens, tableau et liste, arêtes centrées et
    intérieures, sens donné et calculé, primitives et sprites), toutes à 0. Une énumération qui n'est pas une liste
    garde sa copie.
  - Changement : arêtes parcourues par index (0 arête pour un sommet, 1 pour deux, autant que de sommets à partir de
    trois, arête de fermeture comprise, comme `SelectConsecutivePairs(true)`) ; sens d'enroulement calculé par la même
    somme dans le même ordre que `Triangulator.DetermineWindingOrder`, en deux variantes gardées pour rester au bit
    près : tableau fermé pour les arêtes intérieures en primitives, tableau ouvert pour le test que faisait
    `EnsureWindingOrder` en sprites. Le `StrokeAndFillPolygon` de MGUI passe toujours un sens nul, celui du moteur
    transmet le sens reçu, comme avant.
  - Écart avec le texte de la tâche, accepté : en contexte sprites, un polygone en sens horaire n'est pas copié dans
    un tampon mais lu par une vue en ordre inverse gardée par la transaction (`ReversedPolygonView`), parce que
    `SpriteBatch.DrawPolygon` lit `Count` et qu'un tampon de taille exacte se recréerait à chaque changement de
    taille. La vue rend les mêmes valeurs que `ReverseWindingOrder`, index par index ; un test GPU montre que
    `DrawPolygon` ne garde ni ne modifie sa liste.
  - Preuve des sommets : copies littérales des anciennes lignes dans les tests (`OldStrokePolygon`,
    `OldStrokeAndFillPolygon`), confrontées à l'ancien code produit sur 1 166 800 appels par backend (72 925
    polygones, huit variantes, tableau et liste, trois épaisseurs, origine fractionnaire) avant tout changement ; une
    empreinte du flux de sommets par variante figée sur l'ancien code ; rejeu sur le nouveau code égal au bit près.
  - Pixels comparés dans le test, primitives et sprites. Exceptions inchangées, testées.
  - Mutations vérifiées puis annulées dans les deux backends : oublier l'arête de fermeture, inverser le sens
    calculé ; chacune fait échouer la preuve des sommets et les pixels.
  - Avis P4 reportés : la source de la vue inversée n'est pas remise à zéro dans un `finally` (une exception de
    `SpriteBatch.Draw` garderait une référence jusqu'à l'appel suivant) ; le plancher du test de pixels est lâche ;
    le chemin sprites n'a qu'une preuve par pixels, et aucune mutation ne vise ses choix propres.
  - Reste noté, hors des fichiers de la tâche : `CasaAposShapeRenderer2D` a son propre `StrokePolygon`, non mesuré.

### ✅ T1.2 — Géométrie de clip triangulée gardée par la forme

- Objectif : `MGEllipse`, `MGPolygon` et `MGPathLite` ne recréent plus leur géométrie de clip ni leur définition à
  chaque dessin (P3).
- Fichiers : `MGUI/MGUI.Core/UI/Shapes/MGVectorShapeHelper.cs`, `MGEllipse.cs`, `MGPolygon.cs`, `MGPathLite.cs`,
  `MGElement.cs` si `CreateGeometryClipDefinition` doit suivre ; tests de `MGUI.Tests` ;
  `MGUI/Docs/rendering-architecture.md`.
- Étapes :
  1. Lire qui garde un `ClipDefinition` ou une `ClipGeometry` au-delà du dessin (gestionnaires de clips des deux
     backends, caches par nom ou par référence) et l'écrire dans la note ; si une définition gardée ne peut pas être
     réutilisée sans changer le rendu, ⚠️ avec le site.
  2. Figer sur l'ancien code les journaux de dessin des trois formes (remplissage par pinceau, clip propre), et les
     sommets et indices de leur géométrie de clip.
  3. Triangulation par `PolygonTriangulator` (mêmes indices que `Triangulator`, ADR-0038 de MGUI) ; entrées gardées
     selon P3 : une par usage, une par figure remplie pour `MGPathLite`, chacune comparée sur la référence du tableau
     source, la translation et les bornes cibles.
  4. Tests d'invalidation : après un déplacement, après un changement de `Points` d'un `MGPolygon`, de `Width`, de
     `StrokeThickness` et de `SegmentCount` d'un `MGEllipse`, et des données d'un `MGPathLite`, la géométrie de clip
     est égale à celle qu'un `CreateTriangulatedClipGeometry` neuf rend (sommets et indices).
  5. Mutations vérifiées puis annulées : garder l'entrée après un déplacement ; ignorer le changement du tableau de
     sommets ; n'avoir qu'une entrée pour le remplissage et le clip propre. Chacune fait échouer un test.
- Validation : 0 au repos et pendant une animation sans déplacement, y compris sur la forme à remplissage par
  pinceau et clip propre et sur le chemin à plusieurs figures remplies ; journaux identiques ; tests d'invalidation
  verts ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(shapes): keep the triangulated clip geometry of a shape` (MGUI), puis le commit du moteur.
- Validation (2026-11-17) : les trois solutions 0 erreur ; `MGUI.Tests` 4107 verts (34 tests ajoutés), `CasaEngine.Tests`
  6135 verts, aucun sauté. Écrit par un `executor`, contesté par trois relecteurs en lecture seule (deux CONFIRMED, un
  REFUTED), corrigé, puis recontrôlé par un `verifier` neuf : **CONFIRMED**, aucun constat P0 à P3. Aucun changement
  dans le moteur.

  | Octets par image au dessin (Update à 0) | Avant | Après |
  |---|---|---|
  | `MGEllipse`, remplissage par pinceau ou clip propre | 4392 | 0 |
  | `MGEllipse`, les deux (aussi avec opacité animée) | 8784 | 0 |
  | `MGPolygon` et `MGPathLite`, remplissage par pinceau ou clip propre | 1152 | 0 |
  | `MGPolygon` et `MGPathLite`, les deux (aussi avec opacité animée) | 2304 | 0 |
  | `MGPathLite`, deux figures remplies | 1136 | 0 |

  - Totaux exactement à 0 : les 4872 octets de la première image et les 48 octets du nom formaté ont disparu (noms
    construits une fois). Hors cible, mesuré : une forme déplacée à chaque image reconstruit ses deux définitions pour
    760 octets par image (ellipse de 32 segments, 8794 avant) ou 264 (7 sommets, 2314 avant).
  - Relevé de l'étape 1 : aucun cache ni comparaison d'un `ClipDefinition` par nom ou par référence ; les gestionnaires
    de clips des deux backends ne gardent la résolution que du `Push` au `Pop` (un clip à stencil redessine la
    géométrie à sa libération) ; `ElementClipRequest` est une valeur par dessin. Une définition se réutilise donc
    d'une image à l'autre tant qu'on en crée une neuve au moindre changement.
  - Changement : `MGTriangulatedClipEntry` (interne, `MGVectorShapeHelper.cs`) garde la référence du tableau source,
    la translation, les bornes cibles et la définition, et refait une définition neuve quand l'une change. Une entrée
    par usage : remplissage et clip propre pour `MGEllipse` et `MGPolygon` ; une par figure remplie plus le clip propre
    pour `MGPathLite`. `CreateTriangulatedClipGeometry` et `MGElement.CreateGeometryClipDefinition` restent, inchangés,
    sans appelant produit.
  - **Constat P2 du relecteur « identité du comportement », corrigé** : l'ancien code teste le sens d'enroulement deux
    fois (`EnsureWindingOrder`, puis `Triangulate` qui le reteste) ; la première version ne le testait qu'une fois.
    Pour un polygone dont la somme flottante est positive dans les deux sens (aire signée presque nulle), l'ancien
    code triangule l'ordre d'origine et la première version triangulait l'ordre inversé : autre géométrie, autre zone
    de clip. Reproduit sur le polygone du relecteur, puis corrigé : la reconstruction refait la séquence d'origine
    (`EnsureWindingOrder` sur une copie, puis triangulation). Test neuf sur 150 polygones de ce type (tous croisés),
    en échec sur les 150 avant le correctif. Le vérificateur de recontrôle a comparé 68 010 entrées à lui, dont 5 000
    de ce type : 0 écart ; sa sonde rejouée sur la première version donne exactement 5 000 écarts.
  - **Constat P3 du relecteur « solidité des preuves », corrigé** : le test d'invalidation tirait son attendu de la
    définition contrôlée ; il le calcule maintenant depuis l'état de la forme. Sous la mutation « garder la définition
    après un déplacement », il échoue sur 10 cas sur 10 (2 sur 10 avant).
  - Journaux de dessin et géométries figés sur l'ancien code (10 cas, suite d'états : déplacement, largeur, hauteur,
    segments, points, commandes, épaisseur de contour), rejoués à l'identique. Égalité avec
    `CreateTriangulatedClipGeometry` sur tout le corpus de polygones.
  - Mutations vérifiées puis annulées : garder l'entrée après un déplacement ; ignorer le changement du tableau de
    sommets ; une seule entrée pour le remplissage et le clip propre ; retirer le premier test de sens.
  - Avis P4 reportés : le cache suppose que le tableau de sommets est remplacé et jamais écrit en place (vrai de tout
    le code des deux dépôts ; une sous-classe externe de `MGPolygon` qui écrirait dans `NormalizedPoints` garderait un
    clip périmé) ; chaque entrée a son propre triangulateur, dont les tampons restent à leur taille maximale ; une
    forme dessinée deux fois par image à des décalages différents reconstruirait à chaque dessin.
  - Constat préexistant, non traité : `MGEllipse` ne recalcule pas ses sommets quand `StrokeThickness` change
    (`_geometryDirty` n'est levé que par `Width`, `Height` et `SegmentCount`) ; rendu inchangé par cette tâche.

### ✅ T1.3 — Triangle de `MGRatingControl`

- Objectif : le triangle de `MGRatingControl` (plein et partiel, contour compris) n'alloue plus.
- Fichiers : `MGUI/MGUI.Core/UI/MGRatingControl.cs` ; `MGUI/MGUI.Tests/Architecture/FillPolygonCallerJournalTests.cs`
  ou un test voisin.
- Étapes : figer sur l'ancien code le journal du triangle (horizontal et vertical, plein et partiel, avec et sans
  contour) ; tampon gardé comme pour l'étoile et le losange (`GetScaledVertices`, T5.6 du plan archivé), arêtes par
  index ; rejouer. Mutation vérifiée puis annulée.
- Validation : 0 ; journal identique ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(controls): draw the triangle of the rating control without allocating` (MGUI), puis le commit du
  moteur.
- Validation (2026-11-17) : les trois solutions 0 erreur ; `MGUI.Tests` 4855 verts (748 cas de journal ajoutés),
  `CasaEngine.Tests` 6135 verts, aucun sauté. Écrit par un `executor`, contesté par trois relecteurs en lecture seule :
  trois CONFIRMED, aucun constat P0 à P3. Seul `MGRatingControl.cs` change dans le code produit.

  | Octets par image au dessin (horizontal et vertical identiques) | Avant | Après |
  |---|---|---|
  | Plein, sans contour | 1792 | 0 |
  | Plein, avec contour | 4096 | 0 |
  | Partiel, sans contour | 1712 | 0 |
  | Partiel, avec contour | 3728 | 0 |

  - Changement : la branche du triangle passe par `GetScaledVertices` (mêmes flottants, même ordre que l'ancien
    `Select`), trace les trois arêtes par index dans l'ordre de `SelectConsecutivePairs(true)` (arête de fermeture en
    dernier), et calcule minimum et maximum en une boucle à comparaisons strictes, qui rend le résultat de
    `Enumerable.Min` et `Max` sur ces entrées.
  - Journal de dessin figé sur l'ancien code pour 374 scènes (horizontal et vertical ; valeurs 0, partielles dont une
    à clip de 0 pixel et une presque pleine, pleines ; épaisseurs de contour 0 à 3 et négatives, l'épaisseur étant un
    entier ; tailles 0, 1, impaires, 256, 257, négative ; 1 à 7 éléments ; échelles de fenêtre ; valeur de prévisualisation),
    rejoué à l'identique. Un second test compare les sommets et les arêtes aux anciennes expressions, sans passer par
    la table figée.
  - Mutations vérifiées puis annulées : arêtes dans un autre ordre (496 échecs), sommets échangés après la mise à
    l'échelle (716), boucle du minimum partant du mauvais index (119, dans le journal figé seulement).
  - Mesuré sans changement de code, non affirmé par un test : cercle, rectangle, étoile et losange du même contrôle sont
    à 0 octet par image sur le banc.
  - Avis P4 reportés : le test de journal garde son mode de capture (variable d'environnement
    `RATING_TRIANGLE_CAPTURE`, sous laquelle il n'affirme rien), comme `TextBoxCaretGeometryTests` ; seul le journal
    figé couvre le rectangle de clip du remplissage partiel.

### ✅ T1.4 — Animations de `MGHighlightBorderBrush`

- Objectif : les quatre animations de `MGHighlightBorderBrush` n'allouent plus à chaque dessin (P4).
- Fichiers : `MGUI/MGUI.Core/UI/Brushes/BorderBrushes/MGHighlightBorderBrush.cs` ; tests de `MGUI.Tests`
  (`HighlightBorderBrushRingAnimationTests` et voisins).
- Étapes :
  1. Lire les deux surcharges de `Draw` et les chemins d'anneau (`DrawProgressOnRing`, `DrawScanOnRing`) ; écrire
     dans la note quel chemin tourne pour quelle géométrie.
  2. Figer sur l'ancien code le journal de chaque animation à au moins six instants, dans les deux sens de
     progression et d'orientation de balayage, avec des bords d'épaisseurs différentes ; `Progress` et `Scan` sur une
     boîte à coins droits et sur une boîte à coins arrondis à maillage d'anneau.
  3. Collections gardées par l'instance, mêmes éléments dans le même ordre (P4) ; rejouer.
  4. Mutation vérifiée puis annulée : supprimer les doublons en gardant la dernière occurrence au lieu de la première.
- Validation : 0 sur chaque animation, et pour `Progress` et `Scan` sur les deux chemins (coins droits, anneau à
  coins arrondis) ; journaux identiques ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(brushes): animate the highlight border without allocating` (MGUI), puis le commit du moteur.
- Validation (2026-11-17) : les trois solutions 0 erreur ; `MGUI.Tests` 5284 verts, `CasaEngine.Tests` 6135 verts, aucun
  sauté. Écrit par un `executor`, contesté par trois relecteurs en lecture seule : trois CONFIRMED, aucun constat P0 à
  P2. Seul `MGHighlightBorderBrush.cs` change dans le code produit.

  | Octets par dessin, à six instants | Avant | Après |
  |---|---|---|
  | `Pulse`, `Flash` (toute géométrie) | 0 | 0 (aucun code) |
  | `Progress`, coins droits (surcharge directe ou par le contrat) | 2232 à 2472 | 0 |
  | `Scan`, coins droits | 240 | 0 |
  | `Progress`, anneau à coins arrondis | 488 | 0 |
  | `Scan`, anneau à coins arrondis | 448 | 0 |

  - 96 lignes de mesure à 0 (quatre boîtes, trois géométries, huit réglages d'animation).
  - Relevé de l'étape 1 : la surcharge du contrat envoie `Pulse` et `Flash` au dessin commun ; pour `Progress` et `Scan`
    elle prend la surcharge rectangle quand la boîte est à coins droits ou sans maillage d'anneau, sinon
    `DrawProgressOnRing` ou `DrawScanOnRing`. Comportement existant gardé : par le contrat sur coins droits, le
    pinceau de fond est dessiné deux fois (une fois par surcharge).
  - Changement : même algorithme et même ordre. `Distinct().ToList()` devient une compaction en place qui garde la
    première occurrence ; le dictionnaire des coins est gardé, vidé et rempli par quatre `Add` dans l'ancien ordre
    (même exception au même message pour deux coins égaux) ; le dictionnaire des coins extérieurs, aux clés 0 à 3,
    devient un aiguillage ; parcours inverse par index ; tableaux de l'anneau gardés, qui grandissent sans rétrécir ;
    une liste construite et jamais lue est retirée.
  - **Écart avec P4, accepté et signalé à l'auteur** : les tampons sont gardés par thread (`[ThreadStatic]`), pas par
    instance comme P4 l'écrivait. Raisons : c'est la convention du fichier et de `MGUI.Core` (`PolygonVerticesBuffer`,
    `MGDockedBorderBrush`, `MGRatingControl`, `UISymbolDrawing`) ; un pinceau figé partagé entre éléments et dessiné
    par deux threads ne peut pas se marcher dessus ; une copie ne reçoit aucun tampon par référence. Les relecteurs
    n'y trouvent aucune conséquence fonctionnelle : le pinceau de fond se dessine avant tout usage des tampons, et
    rien d'appelé pendant le dessin ne redessine avec ce pinceau.
  - Preuves : journal de dessin figé sur l'ancien code pour 154 scènes, chacune à quelques centaines d'instants (tête
    et queue sur chaque position entière du périmètre, donc exactement sur un coin et juste avant et après ; valeurs
    juste au-dessus de 0 et juste sous 1), deux sens, deux orientations, épaisseurs différentes, nulles, négatives,
    boîtes jusqu'à 2 × 2 et de périmètre nul, anneaux de rayons 2 à 30, avec et sans pinceau de fond ; copie littérale
    de l'ancien code dans le test, comparée appel par appel, exceptions comprises, sur ces scènes et sur des tirages
    aléatoires (6 000 dessins sur la surcharge rectangle, 500 boîtes par le contrat). Exceptions de l'ancien code
    reproduites : clé en double du dictionnaire des coins, division par zéro sur un périmètre nul, valeurs
    d'énumération inconnues. Un pinceau qui dessine deux boîtes en alternance, une copie et son original, et un
    pinceau figé dessiné par deux threads donnent à chaque boîte son journal.
  - Mutations vérifiées puis annulées : garder le dernier doublon (106 échecs), coins dans un autre ordre (21),
    parcours des sommets extérieurs à l'endroit (95), index de l'anneau décalé (21 et 31), dictionnaire non vidé (151).
  - Corrigé en session principale après la relecture (avis P3) : le test à deux threads attendait sans délai et
    pouvait bloquer la suite ou faire tomber l'hôte de test ; il échoue maintenant proprement (délais, exceptions
    recueillies, threads d'arrière-plan). Suites rejouées en entier après ce changement.
  - Avis P4 reportés : les tampons par thread supposent qu'aucun dessin de pinceau de surbrillance ne se réentre
    pendant un `Progress` ou un `Scan` (vrai aujourd'hui : le remplissage interne est un pinceau plein scellé) ; ils
    restent à leur taille maximale pour la vie du thread ; le test de zéro ne couvre ni le chemin résiduel (coins
    arrondis sans maillage) ni un pinceau avec fond, que seuls les journaux couvrent.

### ✅ T1.5 — Glissement d'onglet qui change de groupe

- Objectif : la cause des octets d'un glissement qui change de groupe est trouvée, et corrigée si P5 le permet.
- Fichiers : `MGUI/MGUI.Core/UI/Docking/` (selon la cause) ; tests `MGUI/MGUI.Tests/Docking/`.
- Étapes : mesure par les fixtures de T0.1, puis l'une de ces quatre issues, chacune avec sa fin :
  1. **Non reproduit** : aucun montage (ceux de T5.7, celui du vérificateur de T5.9 reconstruit dans le dépôt, et
     deux autres qui font changer de groupe à chaque image) ne donne un chiffre non nul. La tâche est fermée ✅ sans
     code, avec le 0 mesuré et les montages décrits.
  2. **Cause dans les fichiers du dock** : correction, rejeu du scénario figé de T5.7 (`DockDropTargetReplayTests`) à
     l'identique, test de zéro. ✅.
  3. **Cause hors des fichiers du dock** : ⚠️ avec la cause, le site et le chiffre, sans changement de code.
  4. **Reproduit mais non attribué** : le budget d'enquête est une attribution des octets par type (échantillonnage
     des allocations, comme en T5.8 du plan archivé) puis une mesure élément par élément du dock (hôte, indicateurs
     de dépôt, aperçu, groupes) ; si la cause n'est pas nommée après ces deux passes, ⚠️ avec le chiffre, le montage
     et ce qui est écarté, sans changement de code.
- Validation : l'issue est écrite sous la tâche avec ses chiffres ; pour l'issue 2, 0 et rejeu de T5.7 identique ;
  trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(docking): drag a tab across groups without allocating` (MGUI), puis le commit du moteur ; ou
  `docs(ai-agent): record what a drag across dock groups allocates` (moteur seul) si ⚠️.
- Validation (2026-11-17) : issue 2 du plan (cause dans les fichiers du dock, corrigée). Les trois solutions 0 erreur ;
  `MGUI.Tests` 5284 verts, `CasaEngine.Tests` 6135 verts, aucun sauté. Enquête et correctif faits en session principale
  (un seul défaut, une seule piste).

  | Glissement sur le bord haut de l'hôte, à cheval sur deux groupes, octets par image (Update / Draw) | Avant | Après |
  |---|---|---|
  | | 32 / 0 | 0 / 0 |

  - Attribution : une sonde temporaire (échantillonnage des allocations par type sur 600 000 images, non commitée)
    donne 182 échantillons sur 183 pour `MGSolidFillBrush`, et 32,00 octets par image.
  - Cause : sur le bord haut, la souris alterne entre les en-têtes d'onglets de deux groupes, donc le survol d'un
    onglet change à chaque image ; `MGDockTabItem.UpdateVisuals` recréait alors le pinceau du liseré d'accent
    (`accentColor.AsFillBrush()`, `MGDockTabItem.cs`). Ce n'est pas propre au glissement : tout changement de survol
    d'un onglet créait un pinceau. Le bord bas n'a pas d'en-tête sous la souris, d'où son 0.
  - Correctif : le pinceau est gardé avec la couleur pour laquelle il a été fait (`GetAccentBrush`), comme
    `MGDockSplitterBar` le fait déjà ; une autre couleur (onglet devenu actif, thème changé) le remplace. Conséquence
    observable, la même que pour la barre de séparation : le `MGRectangle` du liseré, élément interne de l'onglet, ne
    notifie plus `Fill` quand la couleur n'a pas changé.
  - Preuves : la sonde relit 0,00 octet par image ; le test de T0.1 affirme maintenant 0 sur ce montage (il affirmait
    16 000 octets sur 500 images, et échoue donc sur l'ancien code) ; les 442 tests du dock passent, dont le rejeu du
    scénario figé de T5.7 (`DockDropTargetReplayTests`) et les journaux figés.
  - Les 96 à 128 octets vus par l'exécutant de T5.7 du plan archivé ne sont reproduits par aucun des sept montages :
    non expliqués, non poursuivis.
  - Vu en passant, non traité : `MGDockTabGroup.SyncHeaderVisuals` fait le même `AsFillBrush()` pour le liseré du
    groupe ; aucun montage mesuré ne le montre allouer par image.

## Phase 2 — Vérification et clôture

### ✅ T2.1 — Vérification indépendante

- Objectif : un `verifier` neuf (opus) confirme ou réfute T1.1 à T1.5 : rendu identique (sommets du lot, journaux,
  pixels), zéros annoncés, aucune régression des deux suites. Une tâche restée en ⚠️ est exclue et nommée.
- Validation : verdict CONFIRMED, ou constats traités selon leur priorité et revérifiés une fois.
- Commit : `docs(ui): record the verification of the end of the MGUI drawing zero allocation` (moteur, ce plan).
- Validation (2026-11-17) : `verifier` neuf (opus), verdict **CONFIRMED** sur T1.1 à T1.5, aucun constat P0 à P3. Aucun
  changement de code.
  - Preuves figées rejouées sur l'ancien code (ancien arbre de MGUI `98bf9edf` extrait hors du dépôt, fichiers de test
    neufs posés dessus) : 1128 tests sur 1135 passent ; les 7 échecs sont exactement les tests de réutilisation de
    T1.2, attendus. Les journaux et empreintes figés de T1.2, T1.3 et T1.4 viennent donc bien de l'ancien code. Le
    test du dock de T1.5 n'échoue sur l'ancien arbre que pour le bord haut (16 000 octets sur 500 images).
  - Sondes différentielles du vérificateur, sur ses propres entrées, ancien arbre contre nouvel arbre, fichiers
    comparés à l'octet : T1.1, 121 056 appels (2 522 polygones dont 1 et 2 sommets, aires presque nulles, doublons,
    coordonnées jusqu'à ±3e38, NaN et infini ; sommets du lot de primitives et éléments du lot de sprites ; exceptions
    comprises), identiques, et 0 octet sur 612 configurations ; T1.2, 4 000 dessins et 5 242 clips à géométrie sur 400
    pas aléatoires par scène, identiques, aucune définition périmée ; T1.3, 60 bureaux, identiques ; T1.4, 20 000
    dessins aléatoires, dont 4 018 avec un pinceau de fond qui est lui-même un pinceau de surbrillance, un pinceau
    figé et sa copie en alternance, identiques, exceptions comprises.
  - Tampons par thread de T1.4 : jugés sûrs pour toutes les suites examinées (un pinceau pour deux éléments, copie et
    original, pinceau figé partagé, pinceau de fond de même type, réentrance) : le pinceau de fond est dessiné avant
    tout usage des tampons, et rien d'appelé pendant leur usage ne redessine avec ce type de pinceau.
  - T1.2 : aucun code des deux dépôts n'écrit en place dans un tableau de sommets source. T1.5 : personne ne dépend de
    la notification `Fill` du liseré.
  - Suites rejouées : trois solutions 0 erreur ; `MGUI.Tests` 5284 verts, `CasaEngine.Tests` 6135 verts, aucun sauté,
    tests GPU compris.
  - Avis P4 reportés : le pinceau gardé du liseré d'onglet est modifiable (son `Color` est public ; aucun code ne
    l'écrit) ; un `IUIDrawTransaction` tiers qui redessinerait un pinceau de surbrillance depuis `FillPolygon` ou
    `FillRectangle` lirait des tampons en cours d'usage (impossible avec les backends livrés) ; plus les avis déjà
    notés sous T1.1 et T1.2.
  - Non vérifié : le backend du moteur n'a pas été sondé avec les entrées du vérificateur pour T1.1 (code de même
    structure que celui de MGUI, tests des auteurs verts) ; les états de survol et de thème de T1.5 n'ont pas été
    pilotés (égalité des couleurs lue dans le code) ; aucun contrôle à l'œil.

### 🚧 T2.2 — Clôture

- Objectif : tableau final des mesures, docs de MGUI à jour (`rendering-architecture.md`, `controls-architecture.md`),
  ligne de `ai-agent/README.md`, `MGUI.Samples` lancé une fois, validation globale, rapport de fin (`AGENTS.md` §12).
  Le plan passe dans `tasks/archive/` après acceptation par l'auteur.
- Validation : section « Validation globale ».
- Commit : `docs(ai-agent): close the end of the MGUI drawing zero allocation`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Tranché le 2026-11-17 : P1 à P7 approuvés (D2). | toutes |

## Hors périmètre

- La frappe dans `MGTextBox` (P6).
- Retirer MonoGame.Extended, ou remplacer `SpriteBatch.DrawPolygon` (P2).
- Les opérations ponctuelles (déplacement ou redimensionnement d'une forme, changement de police, ouverture d'un
  menu), comme dans les chantiers précédents.
- Les contrôles et familles non mesurés par T0.1.
