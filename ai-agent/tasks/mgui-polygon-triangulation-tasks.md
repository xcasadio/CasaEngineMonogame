# Plan agent IA — Remplir un polygone sans `DrawSolidPolygon` (triangulation sans allocation)

Plan d'exécution du point ouvert O16 du chantier archivé
[mgui-nonalloc-suite-tasks.md](archive/mgui-nonalloc-suite-tasks.md) : `PrimitiveDrawing.DrawSolidPolygon` de
MonoGame.Extended alloue à chaque polygone rempli, dans les deux backends.
L'auteur a demandé le 2026-11-17 : « Est-ce un gros chantier de retirer `DrawSolidPolygon` ? », puis, après
l'estimation de l'agent (trois tâches : triangulation propre, branchement des deux backends, vérification) :
« Oui, écris le plan ». Les arbitrages de l'agent sont les points P1 → P6, à valider à l'approbation du plan.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Remplir un polygone (`FillPolygon`) et un rectangle dessiné en mode primitives (`FillRectangle`) n'alloue plus rien,
dans le backend de MGUI (`DrawTransaction`) comme dans celui du moteur (`CasaLegacyShapeRenderer2D`), avec les mêmes
pixels qu'aujourd'hui. Les deux backends cessent d'appeler `PrimitiveDrawing.DrawSolidPolygon` pour ces remplissages :
ils triangulent eux-mêmes, dans des tampons gardés, puis envoient les triangles à `PrimitiveBatch.AddVertex` comme
le faisait `DrawSolidPolygon`. MonoGame.Extended reste une dépendance des deux dépôts.

## État vérifié du dépôt (2026-11-17)

- Branches `chantier/mgui-polygon-triangulation` créées dans le moteur depuis `main` `4dda85ca` et dans MGUI depuis
  `develop` `492c5be2`. Arbre de travail : seul `Projects/SampleProject/.casaeditor/viewport.editor.json` est non suivi
  (fichier de l'auteur, à ne jamais indexer).
- Sites qui appellent `DrawSolidPolygon` (`rg "DrawSolidPolygon" --glob "*.cs"`) :
  `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs:805` (`FillPolygon`) et `:496` (`FillRectangle` en
  contexte primitives, qui crée un `Vector2[4]` à chaque appel, `:489-495`) ;
  `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs:334` (`FillPolygon`) et `:93`
  (`FillRectangle` en contexte primitives, même `Vector2[4]` neuf, `:86-92`). Le rendu Apos Shapes délègue
  `FillPolygon` au rendu historique (relevé par le vérificateur de T5.9, `CasaAposShapeRenderer2D.cs:38`, `:93`).
- `DrawSolidPolygon` (MonoGame.Extended 6.1.1, `lib/net8.0/MonoGame.Extended.dll`, décompilé par `ilspycmd`) : lève
  `InvalidOperationException` si le lot de primitives n'est pas prêt ; pour deux sommets, appelle `DrawPolygon`
  (contour) et s'arrête ; sinon `Triangulator.Triangulate(vertices, WindingOrder.CounterClockwise, out outputVertices,
  out indices)`, puis, pour chaque triangle, trois `PrimitiveBatch.AddVertex(outputVertices[indices[k]] + position,
  color, PrimitiveType.TriangleList)` dans l'ordre des indices ; contour si `outline` (les backends passent `false`).
- `Triangulator.Triangulate` (même DLL) : découpe d'oreilles. Si `DetermineWindingOrder` rend `Clockwise`, copie
  inversée par `ReverseWindingOrder` (tableau neuf, premier sommet gardé) ; listes statiques partagées
  (`IndexableCyclicalLinkedList<Vertex>` des sommets et des oreilles, `CyclicalList<Vertex>` des sommets convexes et
  réflexes, `List<Triangle>`) ; un nœud neuf par sommet ajouté ; `indices = new int[3 × triangles]`. Boucle
  `while (sommets > 3 && oreilles > 0) ClipNextEar` puis dernier triangle s'il reste trois sommets : un polygone sans
  oreille s'arrête en route et n'est rempli qu'en partie. `IsConvex` compare deux vecteurs normalisés ; `IsEar` teste
  les sommets réflexes par `Triangle.ContainsPoint` ; `ClipNextEar` prend la première oreille, rend le triangle
  (oreille, suivant, précédent) et revalide les deux voisins (`AddFirst` d'une nouvelle oreille). `Vertex` est égal à
  un autre par position et index.
- `PrimitiveBatch` (même DLL) : `AddVertex` écrit position (`Vector3(sommet, -0.1f)`) et couleur dans le tableau
  privé `_triangleVertices` à l'index `_triangleVertsCount`, puis incrémente ce compteur ; quand le tableau est plein,
  il le vide d'abord (`FlushTriangles` dessine et retire du compteur les triangles complets). N'alloue pas. Aucun
  membre public ne rend ces sommets (surface publique : `IsReady`, `Begin`, `AddVertex`, `End`, `SetProjection`,
  `Dispose`). Les deux backends créent leur lot avec 1024 sommets (`MGUI/MGUI.MonoGame.Integration/Rendering/MainRenderer.cs:208`,
  `CasaEngine/Framework/UI/Backend/MonoGame/CasaDesktopRuntime.cs:90`). Aucun test ne lit aujourd'hui ces champs privés.
- Mesures de T5.6 (GPU réel, octets par appel, identiques dans les deux backends) : `DrawSolidPolygon` seul, 376 pour
  un triangle, 424 pour un triangle de l'autre sens, 992 pour un concave de 8 sommets, 928 et 952 pour une flèche de
  7 sommets dans les deux sens. `FillPolygon` n'alloue plus rien d'autre (T5.6).
- Appelants de `FillPolygon` à chaque image : `UISymbolDrawing.cs:48` (flèches de `UISymbolElements`,
  `MGDockDropIndicators`, `MGTreeViewItem`, `MGComboBox`, `MGCheckBox`, `MGContextMenuItem`, `MGGraphControls`),
  `MGEllipse.cs:121`, `:125` (32 segments par défaut, `MGEllipse.cs:83`), `MGPolygon.cs:46`, `:50`,
  `MGPathLite.cs:88`, `:94`, `MGRatingControl.cs:672`, `:693`, `MGDockedBorderBrush.cs:162-181`,
  `MGHighlightBorderBrush.cs:807`.
- `MGUI.Shared` est référencé par `MGUI.MonoGame.Integration` (`MGUI.MonoGame.Integration.csproj:14`) et par
  `CasaEngine` (`CasaEngine.csproj:33`) ; il référence MonoGame.Extended (`MGUI.Shared.csproj:15`).
- Autres usages de `Triangulator`, hors de ce plan : `StrokePolygon` des deux backends (`DrawTransaction.cs:753`,
  `:758` ; `CasaLegacyShapeRenderer2D.cs:281`, `:286`), et `MGVectorShapeHelper.CreateTriangulatedClipGeometry`
  (`MGVectorShapeHelper.cs:259-260`), appelé à chaque dessin d'une forme à remplissage par pinceau
  (`MGEllipse.cs:114`, `MGPolygon.cs:39`, `MGPathLite.cs:81`).
- Tests existants : `MGUI/MGUI.Tests/Integration/FillPolygonGpuTests.cs` et
  `CasaEngine.Tests/UI/Backend/FillPolygonGpuTests.cs` (pixels comparés à `DrawSolidPolygon` dans le test,
  `FillPolygon` n'allouant pas plus que `DrawSolidPolygon` seul) ; `FillPolygonCallerJournalTests` (sommets envoyés
  par trois appelants). Références des suites : `MGUI.Tests` 3990, `CasaEngine.Tests` 6124, aucun sauté.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Demande de l'auteur du 2026-11-17 : écrire le plan qui retire `DrawSolidPolygon` des remplissages, sur l'estimation de l'agent (triangulation propre, deux backends, vérification). |
| D2 | Plan approuvé par l'auteur le 2026-11-17 (« plan approuvé »), P1 à P6 compris. Réponse donnée juste après la question de l'agent sur P1 (« On part sur P1 avec le chemin convexe sous preuve d'identité ? ») : un chemin rapide pour un polygone convexe (éventail) est ajouté à T1.1, **seulement s'il rend exactement les mêmes sommets ordonnés et indices** que `Triangulator.Triangulate` ; sinon il est abandonné et noté, sans rien changer d'autre. Interprétation de l'agent, signalée à l'auteur. |

## Points à valider à l'approbation (arbitrages de l'agent)

| Réf | Proposition | Autre voie |
|---|---|---|
| P1 | Porter **le même algorithme** que `Triangulator.Triangulate` : même détection du sens, même inversion, mêmes listes d'oreilles, de sommets convexes et réflexes avec le même ordre d'ajout et de retrait, mêmes calculs en `float`. Preuve : mêmes sommets ordonnés et mêmes indices que `Triangulator.Triangulate`, entrée par entrée, sur tout le corpus, polygones dégénérés compris (même remplissage partiel quand l'algorithme d'origine s'arrête en route). Les triangles envoyés au lot sont alors exactement ceux d'aujourd'hui. | Un autre algorithme, prouvé par les pixels seulement : un polygone dégénéré pourrait se remplir autrement. |
| P2 | Un type public `PolygonTriangulator` dans `MGUI.Shared/Rendering/` (classe scellée, instance qui garde ses tampons), un par transaction de MGUI et un par rendu du moteur, comme les tampons de T5.6. Pas d'état statique partagé. ADR de MGUI 0038 (type public ajouté, contrat de durée de validité des sommets et indices rendus). | Type interne et `InternalsVisibleTo` vers `CasaEngine` ; ou classe statique à tampons par thread. |
| P3 | `FillRectangle` en contexte primitives passe par le même triangulateur, sur un tableau de quatre sommets gardé : mêmes triangles qu'aujourd'hui, plus de `Vector2[4]` neuf. | Écrire les deux triangles à la main (il faudrait prouver qu'ils sont ceux du triangulateur). |
| P4 | Un polygone de deux sommets garde l'appel à `DrawSolidPolygon`, qui dessine alors un contour : comportement inchangé, cas sans intérêt pour le zéro. Un polygone vide ou nul lève comme aujourd'hui. | Reproduire le contour de `DrawPolygon`. |
| P5 | Les tampons grandissent jusqu'au plus grand polygone vu et ne rétrécissent pas, comme les tampons déjà gardés du moteur (D12 du plan archivé). | Plafond au-delà duquel un tableau neuf est créé (256 sommets en T5.6). |
| P6 | Hors de ce plan, notés : le contour d'un polygone (`StrokePolygon`, qui alloue par `Append`, `ToArray`, `EnsureWindingOrder` et `SelectConsecutivePairs`) et la géométrie de clip triangulée à chaque dessin d'une forme à remplissage par pinceau (`CreateTriangulatedClipGeometry`). | Les inclure : chacun demande sa propre preuve de rendu. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/mgui-polygon-triangulation`** dans le moteur (depuis `main`) et dans MGUI (depuis
  `develop`), déjà créées. Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche et par dépôt** : le commit de MGUI d'abord, puis celui du moteur, qui porte la référence du
  sous-module, le code moteur de la tâche et ce plan. Message en anglais `type(area): summary`. Chaque commit du
  moteur compile avec la référence de MGUI qu'il porte ; vérifier `git -C MGUI status` après chaque commit de MGUI.
- **Ne jamais pousser.** La fusion dans `main` et `develop` reste une décision de l'auteur.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Le comportement de MonoGame.Extended se lit dans la DLL décompilée (`ilspycmd`), version 6.1.1. Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build MGUI/MGUI.sln`,
  `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln`. **Tests** :
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans
  `--no-build`, tout vert, aucun test sauté (les tests GPU tournent sur cette machine).
- **Mesure avant et après** : chaque tâche qui vise zéro écrit sous elle les octets mesurés avant et après (500
  appels ou images après 500 de chauffe ; un test de zéro arrondit la moyenne vers le haut et affirme sur le total).
- **Rendu inchangé** : pixels comparés dans le test (jamais une empreinte de pixels figée en constante : elle dépend
  du GPU), journaux de dessin figés inchangés.
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
  vert, aucun test sauté (références 3990 et 6124 avant ce plan).
- Smoke manuel, par l'auteur : `MGUI.Samples` et l'éditeur ; flèches des listes déroulantes, des arbres et des menus,
  cases à cocher, ellipses et polygones, indicateurs de dépôt du dock : rien ne doit changer à l'écran.

---

## Phase 1 — Triangulation et remplissages

### ✅ T1.1 — Triangulateur sans allocation, identique à celui de MonoGame.Extended

- Objectif : `PolygonTriangulator` (P2) rend, pour un tableau ou une liste de sommets, les sommets ordonnés et les
  indices que `Triangulator.Triangulate(..., WindingOrder.CounterClockwise, ...)` rendrait, sans allocation après
  chauffe.
- Fichiers : `MGUI/MGUI.Shared/Rendering/PolygonTriangulator.cs` (nouveau) ; tests
  `MGUI/MGUI.Tests/Rendering/PolygonTriangulatorTests.cs` (dossier existant) ;
  `MGUI/Docs/decisions/0038-*.md` et son index ; `MGUI/Docs/rendering-architecture.md`.
- Étapes :
  1. Décompiler et lire en entier `Triangulator`, `Triangle`, `Vertex`, `CyclicalList<T>` et
     `IndexableCyclicalLinkedList<T>` de MonoGame.Extended 6.1.1 ; écrire dans la note de la tâche les règles
     d'ordre qui décident du résultat (indexation cyclique, `Contains`, `Remove`, `AddFirst`, `AddLast`, `RemoveAt`,
     égalité de `Vertex`).
  2. Écrire d'abord le test différentiel contre `Triangulator.Triangulate` : mêmes sommets ordonnés et mêmes indices,
     entrée par entrée. Corpus : triangle et rectangle dans les deux sens, flèches de `UISymbolDrawing`, formes de
     `MGRatingControl` (étoile, losange), ellipses de `MGVectorShapeHelper.CreateEllipseVertices` (32 segments et
     d'autres), concaves, sommets alignés, sommets consécutifs confondus, polygone qui se croise, polygone sans
     oreille (remplissage partiel), et un tirage aléatoire à graine fixe de polygones simples et non simples.
     `Triangulator` garde ses listes dans des champs statiques partagés : le différentiel et tout test qui l'appelle
     (directement ou par `DrawSolidPolygon`) tournent dans une même collection xUnit non parallèle.
  3. Écrire le triangulateur, avec les seuls types de MonoGame `Vector2` et des entiers : un fichier de `MGUI.Shared`
     qui nomme `PrimitiveBatch` ou `GraphicsDevice` fait échouer le test d'architecture
     `MGUI/MGUI.Tests/Architecture/Phase4RenderingArchitectureTests.cs:28`, `:53-57`. Tampons gardés par instance (sommets ordonnés, liste circulaire par indices précédent
     et suivant, listes d'oreilles, de convexes et de réflexes reproduisant l'ordre d'origine, indices), qui
     grandissent sans rétrécir (P5) ; mêmes calculs en `float` que l'original.
  4. Chemin rapide convexe (D2) : si le polygone est convexe, rendre directement l'éventail que la découpe
     d'oreilles d'origine produit sur un convexe. Le différentiel de l'étape 2 doit passer à l'identique avec et sans
     ce chemin (convexes des deux sens, sommets alignés, sommets confondus compris) ; à la moindre différence, le chemin
     est retiré et le fait est écrit sous la tâche.
  5. Test de zéro : triangulation d'un triangle, d'une flèche, d'un concave, d'une ellipse de 32 segments, dans les
     deux sens, 0 octet sur 500 appels après 500 de chauffe. Mutations vérifiées puis annulées : prendre la dernière
     oreille au lieu de la première ; ne pas inverser un polygone en sens horaire. Chacune fait échouer le
     différentiel.
  6. ADR 0038 de MGUI (P1, P2, P5, D2, durée de validité de ce que rend le triangulateur : jusqu'à son appel suivant).
- Validation : différentiel sans écart sur tout le corpus ; zéro tenu ; mutations qui échouent ; trois solutions
  0 erreur, deux suites vertes, aucun test sauté.
- Commit : `feat(rendering): triangulate a polygon without allocating` (MGUI), puis
  `chore(ui): take the polygon triangulator of MGUI (MGUI ADR-0038)` (moteur, référence et ce plan).
- Validation (2026-11-17) : les trois solutions 0 erreur ; `MGUI.Tests` 4001 verts (11 tests ajoutés),
  `CasaEngine.Tests` 6124 verts, aucun sauté. Écrit par un `executor`, relu en session principale. ADR-0038 de MGUI.
  O2 ne s'est pas produit.
  - Différentiel contre `Triangulator.Triangulate`, sommets au bit près et indices entrée par entrée : 72 921
    polygones, 291 684 comparaisons (tableau et liste, instance gardée et instance neuve), 0 écart, avec et sans le
    chemin convexe. Corpus : triangles et rectangles, flèches de `UISymbolDrawing`, formes de `MGRatingControl`,
    400 ellipses, concaves, sommets alignés, sommets confondus, polygones qui se croisent, tous les polygones de 3, 4
    et 5 sommets sur une grille de 3 × 3, et 5 900 polygones tirés avec la graine 20261117. 12 180 polygones y sont
    remplis en partie par l'original, dont 2 626 sans aucun triangle.
  - Chemin convexe (D2) gardé : sans sommet réflexe, l'original rend l'éventail (k, k + 1, n - 1) ; le chemin est
    pris par 13 775 polygones du corpus, avec le même résultat. Des sommets confondus donnent une direction NaN,
    donc un sommet réflexe : ce cas passe par le chemin général.

  | Octets par appel (500 après 500 de chauffe) | `Triangulator.Triangulate` | `PolygonTriangulator` |
  |---|---|---|
  | Triangle (deux sens) | 376 / 424 | 0 |
  | Flèche de 7 sommets (deux sens) | 928 / 952 | 0 |
  | Concave de 8 sommets (deux sens) | 992 / 1080 | 0 |
  | Ellipse de 32 segments (deux sens) | 3968 / 4248 | 0 |

  - API : `PolygonTriangulator` (public, scellé, `MGUI.Shared.Rendering`), `Triangulate(Vector2[])` et
    `Triangulate(IReadOnlyList<Vector2>)` rendent le nombre de triangles ; `Vertices` et `Indices` en
    `ReadOnlySpan`, valides jusqu'à l'appel suivant sur la même instance ; aucun état statique. Deux membres
    internes servent aux tests (désactiver le chemin convexe, savoir s'il a été pris). Un tableau nul lève
    `ArgumentNullException`, un tableau vide `ArgumentException` (l'original lève autre chose : T1.2 garde le
    comportement des backends) ; moins de trois sommets rend 0 triangle. Coordonnées NaN non prises en charge,
    écrit dans l'ADR.
  - Les tests qui appellent `Triangulator` sont dans la collection non parallèle `GpuDeviceCollection`.
  - Mutations vérifiées puis annulées : dernière oreille au lieu de la première (197 292 comparaisons en écart),
    pas d'inversion d'un polygone en sens horaire (113 020), éventail depuis un autre sommet (55 100) : chacune fait
    échouer le différentiel.

### ✅ T1.2 — Les deux backends remplissent sans `DrawSolidPolygon`

- Objectif : `FillPolygon` et `FillRectangle` en contexte primitives n'allouent plus rien dans les deux backends, avec
  les mêmes pixels et les mêmes sommets envoyés au lot que `DrawSolidPolygon`.
- Fichiers : `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs`,
  `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs` ; tests
  `MGUI/MGUI.Tests/Integration/FillPolygonGpuTests.cs`, `CasaEngine.Tests/UI/Backend/FillPolygonGpuTests.cs` ;
  `MGUI/Docs/rendering-architecture.md` (limite connue de `FillPolygon` retirée ou réécrite).
- Étapes :
  1. Mesurer d'abord sur le code d'avant, sur GPU réel, dans chaque backend : `FillPolygon` d'un triangle, d'une
     flèche, d'un concave, d'une ellipse de 32 segments (tableau et liste), et `FillRectangle` en contexte
     primitives.
  2. Remplacer, dans chaque backend, l'appel à `DrawSolidPolygon` par : même garde que lui quand le lot n'est pas
     prêt, polygone de deux sommets délégué à `DrawSolidPolygon` (P4), sinon triangulation par l'instance gardée puis
     trois `PrimitiveBatch.AddVertex(sommet + position, couleur, PrimitiveType.TriangleList)` par triangle, dans
     l'ordre des indices. `FillRectangle` en primitives passe par un tableau de quatre sommets gardé (P3).
  3. Preuve que les sommets envoyés au lot sont ceux de `DrawSolidPolygon`, par un seul mécanisme : le test lit par
     réflexion les champs privés `_triangleVertices` et `_triangleVertsCount` du `PrimitiveBatch` du backend
     (MonoGame.Extended 6.1.1). Pour chaque polygone du corpus de T1.1 et pour le rectangle en primitives, il relève
     le compteur avant l'appel, appelle l'ancien chemin (`DrawSolidPolygon`) puis le nouveau dans deux lots
     identiques, et compare les sommets écrits entre le compteur d'avant et celui d'après : position (Z compris),
     couleur et ordre. Pour qu'aucun vidage n'ait lieu pendant un appel, chaque mesure part d'un lot neuf et le test
     affirme que le compteur d'après vaut celui d'avant plus trois fois le nombre de triangles (le plus grand
     polygone du corpus reste sous les 1023 sommets utiles du lot des deux backends, `bufferSize - bufferSize % 3`). Arrêt : si ces champs n'existent pas
     ou ne se lisent pas ainsi dans l'un des deux backends, la tâche passe en ⚠️ Blocked avec le fait ; les pixels ne
     remplacent pas cette preuve.
     Séparément, pixels identiques sur GPU réel, comparés dans le test, pour un triangle, un concave, une flèche, une
     ellipse, un rectangle en primitives et sept polygones dans un lot.
  4. Les tests « pas plus que `DrawSolidPolygon` » de T5.6 deviennent des tests de zéro ; le test qui montre que
     `DrawSolidPolygon` ne modifie ni ne garde le tableau reste (il couvre P4).
  5. Mutations vérifiées puis annulées, faites dans le seul code de remplissage neuf des deux backends, contre la
     lecture des sommets de l'étape 3 : envoyer les trois sommets d'un triangle dans un autre ordre ; oublier le
     décalage `+ position` d'un sommet. Chacune fait échouer la preuve des sommets. (Z n'est pas une mutation
     possible : `AddVertex` l'écrit lui-même à -0.1 ; la lecture le compare quand même.)
- Validation : 0 octet par `FillPolygon` (tableau et liste) et par `FillRectangle` en primitives dans les deux
  backends ; sommets lus dans le lot identiques (étape 3) et pixels identiques ; journaux figés inchangés ; trois solutions 0 erreur, deux suites vertes,
  aucun test sauté.
- Commit : `perf(rendering): fill a polygon without DrawSolidPolygon` (MGUI), puis
  `perf(ui): fill a polygon without DrawSolidPolygon` (moteur).
- Validation (2026-11-17, GPU réel) : les trois solutions 0 erreur ; `MGUI.Tests` 4006 verts, `CasaEngine.Tests` 6129
  verts (5 tests ajoutés de chaque côté), aucun sauté ; journaux figés inchangés. Écrit par un `executor`, diff du
  moteur relu en session principale. O3 ne s'est pas produit.

  | Octets par appel, deux backends identiques | Avant | Après |
  |---|---|---|
  | `FillPolygon`, triangle (deux sens) | 376 / 424 | 0 |
  | `FillPolygon`, flèche de 7 sommets (deux sens) | 928 / 952 | 0 |
  | `FillPolygon`, concave de 8 sommets | 992 | 0 |
  | `FillPolygon`, ellipse de 32 segments (deux sens) | 3968 / 4248 | 0 |
  | `FillRectangle` en primitives | 552 | 0 |

  - Tableau et liste donnent le même chiffre ; une autre énumération est encore copiée par `ToArray()`, comme avant.
  - Preuve des sommets : le test lit par réflexion `_triangleVertices` et `_triangleVertsCount` du lot du backend et
    compare, pour l'ancien chemin (`DrawSolidPolygon`) et le nouveau, position (X, Y, Z au bit près), couleur et
    ordre, avec une origine fractionnaire (5.3, 3.7) ; il affirme pour chaque appel que le compteur a avancé de trois
    fois le nombre de triangles de l'original (aucun vidage). 72 921 polygones du corpus de T1.1, en tableau et en
    liste, et 9 rectangles en primitives, par backend : 0 écart. Écart avec le texte de l'étape 3 : au lieu d'un lot
    neuf par mesure, le test remet le compteur du lot du backend à un décalage de 0, 3, 6 ou 9 sommets avant chaque
    appel (même mécanisme ; un lot neuf par polygone demandait 72 000 rendus). Le corpus est partagé entre les deux
    dépôts (`PolygonCorpus.cs` et `PrimitiveBatchProbe.cs` de `MGUI.Tests`, liés dans `CasaEngine.Tests.csproj`).
  - Pixels comparés dans le test : triangle, concave, flèche et ellipse dans les deux sens (tableau, liste, liste en
    lecture seule, énumération), six rectangles en primitives, sept polygones dans un lot, polygones de deux et d'un
    sommet : identiques dans les deux backends.
  - Exceptions inchangées : `ArgumentException` pour nul ou vide ; `InvalidOperationException` au message de
    `DrawSolidPolygon` quand le lot n'est pas prêt (test neuf, passé aussi sur l'ancien code). Polygone de deux
    sommets délégué à `DrawSolidPolygon` (P4), par un `Vector2[2]` gardé quand il n'arrive pas en tableau.
  - Tampons par longueur de T5.6 retirés : le triangulateur lit la liste directement.
  - Mutations vérifiées puis annulées : trois sommets d'un triangle dans un autre ordre (la preuve des sommets
    échoue, aucun test de pixels n'échoue : c'est ce que les pixels ne voient pas) ; décalage d'origine oublié sur
    un sommet sur trois (preuve des sommets et pixels échouent), dans chaque backend.
  - Doc : limite connue de `FillPolygon` réécrite dans `MGUI/Docs/rendering-architecture.md` ; `StrokePolygon` et
    `CreateTriangulatedClipGeometry` allouent encore (P6).

### ✅ T1.3 — Vérification indépendante

- Objectif : un `verifier` neuf (opus) confirme ou réfute : triangulation identique à `Triangulator.Triangulate` (y
  compris sur des polygones qu'il choisit lui-même), sommets et pixels identiques dans les deux backends, zéros
  annoncés, aucune régression des deux suites.
- Sources : diff de T1.1 et T1.2, tests, ADR 0038.
- Validation : verdict CONFIRMED, ou constats traités selon leur priorité et revérifiés une fois.
- Commit : `docs(ui): record the verification of the polygon triangulation` (moteur, ce plan).
- Validation (2026-11-17) : `verifier` neuf (opus), verdict **CONFIRMED**, aucun constat P0 à P3. Aucun changement
  de code.
  - Triangulateur : différentiel du vérificateur contre `Triangulator.Triangulate` sur ses propres polygones (étoiles
    de 3 à 270 sommets, échelles de 1e-38 à 3e38, polygones non simples de 1 à 90 sommets, grilles entières, presque
    alignés, sommets répétés, convexes avec sommets à 180°, alternance de grands et petits polygones sur une même
    instance, -0, infinis, dénormaux, polygones de 800 à 2900 sommets) : 716 478 comparaisons par graine, quatre
    graines, Release et Debug, 0 écart ; chemin convexe toujours égal au chemin général ; 0 octet sur 20 000 appels ;
    huit fils sur des instances distinctes sans écart. Lecture d'un `IReadOnlyList` par l'indexeur seul vérifiée.
  - Backends : code identique en substance dans les deux ; test GPU du vérificateur sur 6 801 polygones à lui par
    `DrawTransaction.FillPolygon` (tableau, liste, liste lue par l'indexeur, énumération, origine (7.1, -2.9)) contre
    `DrawSolidPolygon`, par la sonde de lecture du lot : passé.
  - Sonde des sommets : une mutation du chemin convexe qui garde les pixels mais change l'ordre des sommets fait
    échouer la sonde ; la remise du compteur à un décalage ne peut pas masquer un écart (sommets attendus copiés
    avant l'appel neuf, compteurs vérifiés, place vérifiée) ; chaque test a son propre lot.
  - Avis P4 reportés : (A1) `PrimitiveBatchProbe.Compare` ne remet pas le compteur dans un `finally` : un appel qui
    lève laisse des sommets périmés dans le lot de ce seul test, déjà en échec ; (A2) la sonde dépend des noms de
    champs privés de MonoGame.Extended 6.1.1, et échoue bruyamment à une montée de version ; (A3) polygones du
    vérificateur non passés par le backend du moteur (couvert par le corpus des auteurs et le code identique).
  - Suites rejouées : trois solutions 0 erreur ; `MGUI.Tests` 4006 verts, `CasaEngine.Tests` 6129 verts, aucun
    sauté, tests GPU compris.
  - Non vérifié : pixels des polygones du vérificateur (sommets seulement) ; NaN au-delà du nombre de triangles ;
    aucun contrôle à l'œil.

### ⏳ T1.4 — Clôture

- Objectif : tableau final des mesures, docs de MGUI à jour, ligne de `ai-agent/README.md`, `MGUI.Samples` lancé une
  fois, validation globale, rapport de fin (`AGENTS.md` §12). Le plan passe dans `tasks/archive/` après acceptation
  par l'auteur.
- Validation : section « Validation globale ».
- Commit : `docs(ai-agent): close the polygon triangulation chantier`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | P1 à P6 à valider par l'auteur à l'approbation du plan. | toutes |
| O2 | Si le portage ne peut pas reproduire `Triangulator.Triangulate` à l'identique sur un polygone du corpus (comportement de MonoGame.Extended qui dépend d'un état non reproductible), la tâche s'arrête en ⚠️ avec ce polygone et les deux résultats. | T1.1 |
| O3 | Si la lecture par réflexion des sommets du lot échoue dans un backend (champs absents ou autrement nommés), T1.2 s'arrête en ⚠️ : pas de repli sur les pixels. | T1.2 |

## Hors périmètre

- Retirer MonoGame.Extended des deux dépôts : `PrimitiveBatch`, `PrimitiveDrawing` (contours, ellipses) et les
  extensions de `SpriteBatch` restent utilisés.
- Le contour d'un polygone (`StrokePolygon`) et la géométrie de clip triangulée à chaque dessin d'une forme à
  remplissage par pinceau (P6).
- Les autres allocations des appelants (triangle de `MGRatingControl`, reste de la branche de progression de
  `MGHighlightBorderBrush`), notées à la clôture du plan archivé.
