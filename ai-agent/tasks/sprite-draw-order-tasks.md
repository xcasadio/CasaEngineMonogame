# Plan agent IA — Ordre de dessin des sprites à Z égal

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-10. Des sprites de même Z doivent se dessiner dans un ordre choisi par les données, toujours le même, et non dans un ordre laissé au tri.

Les décisions D1 → D6 ont été arbitrées avec l'auteur le 2026-11-10. Les choix C1 → C7 sont proposés par ce plan et verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Relu par trois plan-verifiers neufs, qui ont tous rendu REVISE :
1. affectation à chaque entrée réutilisée, test de 64 entrées, surcharges exactes ;
2. test du pool sur les deux chemins ;
3. rang de soumission de `QueuePsxQuad` prouvé.

Chaque point est corrigé. Aucune relecture n'a rendu READY. L'auteur approuve le plan le 2026-11-10 (« J'approuve le plan »). Le lancement attend son go.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- **Un champ `draw_order`**, entier facultatif à 0 par défaut, sur `StaticSpriteComponent` et `VideoSpriteComponent`. À Z égal et sans clé de tri, le sprite de plus petit `draw_order` est dessiné d'abord, et celui dessiné après passe par-dessus.
- **Un ordre déterministe.** À Z et `draw_order` égaux, le départage se fait par l'ordre de soumission de l'image, au lieu d'être laissé à un tri instable.
- **Livrables associés** : ADR-0092 et un sample `Projects/SpriteDrawOrderSample`.

## État vérifié du dépôt (2026-11-10)

Lu sur `main` à `e93a4049`, égal au pointeur du projet consommateur.

- **Tri de la file.** `SpriteRendererComponent.FillVertices` trie la file par `_spriteDatas.Sort(SpriteDisplayDataComparison)` (`CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs:527`). `List<T>.Sort` n'est pas stable.
  - Le comparateur `CompareSpriteDisplayData` compare les clés quand l'une des deux entrées en a une. Sinon, le plus grand Z passe d'abord, et deux Z égaux rendent 0 (`:553-569`).
  - Une entrée est une structure `SpriteDisplayData` (`:16`) mise en file par deux chemins (`:817`, `:1097`).
- **Ordre de soumission.** `World.Draw` interroge l'octree du monde sur le frustum (`SpatialServices.WorldIndex.Query`, `CasaEngine/Framework/Scene/World/World.cs:925-950`, `OctreeWorldSpatialIndex`), puis dessine les entités visibles dans l'ordre de la requête. Cet ordre dépend des cellules et change quand un sprite bouge.
- **Profondeur.** Les sprites triés testent et écrivent la profondeur en `LessEqual` (`:137-141`). Deux sprites de même Z se mélangent donc l'un sur l'autre, dans l'ordre du tri (O3 d'ADR-0091).
- **Appels des composants.**
  - `StaticSpriteComponent.Submit` passe par les surcharges à clé, s'il a un `DepthSortable2DComponent`. Sinon, il passe par les surcharges sans clé : le chemin par défaut, ou la surcharge sans clé avec mélange d'ADR-0090 (`Scene/Entities/Components/StaticSpriteComponent.cs:322-341`).
  - `VideoSpriteComponent` passe par la surcharge sans clé avec `ignoresDepth` d'ADR-0091 (`Scene/Entities/Components/VideoSpriteComponent.cs:212`).
- **JSON des composants.** Les champs `color`, `blend_mode` et `video` sont lus dans `Load`. Un champ malformé lève une `InvalidDataException`, qui nomme le composant, le champ et la valeur. La sauvegarde de l'éditeur n'écrit un champ que s'il n'a pas sa valeur par défaut (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`).
- **Tests.** Les tests de la file sans GPU passent par `FillVertices` (par exemple `CasaEngine.Tests/Scene/StaticSpriteComponentColorBlendTests.cs`).
- **ADR.** Prochain numéro libre : 0092.
- **Arbre de travail.** Branche `chantier/sprite-draw-order` créée depuis `main` `e93a4049`. Aucun changement préexistant.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-10.**

| Réf | Décision |
|---|---|
| D1 | **Contenu.** Un ordre de dessin choisi par les données pour des sprites de même Z |
| D2 | **Champ d'ordre** : un entier facultatif `draw_order` sur les composants sprite et vidéo, 0 par défaut. À Z égal, le plus petit est dessiné d'abord, comme l'« ordre dans le calque » des moteurs modernes. L'ordre d'ajout seul ne suffit pas, car le monde soumet ses entités dans l'ordre de son octree |
| D3 | **Format et API additifs.** Un projet ou un monde existant se charge et se dessine comme avant, à l'ordre près entre sprites de même Z, qui n'était pas défini. ADR-0092 enregistre la décision |
| D4 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur, un jeu ou une décompilation. Les données sont synthétiques |
| D5 | Pas d'allocation ni de coût notable ajoutés au tri ou à la mise en file |
| D6 | Un sample montre l'ordre à Z égal |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **File du renderer.** <ul><li>`SpriteDisplayData` gagne deux entiers : `DrawOrder`, donné par l'appel, et `Submission`, le rang de l'entrée dans la file de l'image.</li><li>Les deux chemins de mise en file, le `DrawSprite` privé du cœur (`:1069-1097`) et `QueuePsxQuad` (`:789-817`), affectent explicitement **les deux champs à chaque entrée**. `DrawOrder` vaut 0 quand l'appel n'en porte pas. Les entrées viennent d'un pool réutilisé sans remise à zéro (`:1106-1119`) : une valeur d'une image précédente ne doit jamais survivre.</li><li>Le comparateur, pour deux entrées sans clé, garde le plus grand Z d'abord. À Z égal, il passe le plus petit `DrawOrder` d'abord, puis le plus petit `Submission`.</li><li>Les entrées à clé gardent leur comparaison actuelle. Le `DrawOrder` d'un sprite à clé est sans effet, et la doc le dit.</li></ul> |
| C2 | **Surcharges.** Aucune signature existante ne change. Le `DrawSprite` privé du cœur gagne un paramètre facultatif `int drawOrder = 0`, qui n'est pas une API publique. Ajouts exacts : <ol><li>public `DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, bool drawDebug, SpriteEffects effects, Rectangle scissorRectangle, int drawOrder)`, le chemin par défaut d'un sprite sans clé, jumeau de la surcharge publique existante sans `drawOrder` (`:627`) ;</li><li>public `DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteBlendMode blendMode, int drawOrder)`, jumeau de la surcharge publique existante avec mélange (`:601`) ;</li><li>**internal** `DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteBlendMode blendMode, Rectangle scissorRectangle, int drawOrder)`, jumeau de la surcharge interne avec découpe (`:606`), qui reste interne ;</li><li>public `DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation, Vector2 scale, Color color, float z, SpriteEffects effects, bool ignoresDepth, int drawOrder)`, jumeau de la surcharge du film d'ADR-0091 (`:926-927`).</li></ol>Les autres appels mettent `DrawOrder = 0` (C1). Après T1.1, la liste des membres publics de `SpriteRendererComponent` ne gagne que les ajouts 1, 2 et 4 |
| C3 | **Composants.** <ul><li>`StaticSpriteComponent` et `VideoSpriteComponent` gagnent une propriété `DrawOrder` (entier, 0 par défaut), copiée par le constructeur de copie.</li><li>JSON : `draw_order`, facultatif, un entier JSON. Il est absent ou à 0 par défaut. Toute autre forme (décimal, chaîne, booléen) fait échouer le chargement avec une `InvalidDataException` qui nomme le composant, le champ et la valeur.</li><li>La sauvegarde de l'éditeur l'écrit seulement s'il n'est pas 0.</li><li>Avec `DrawOrder = 0`, un composant fait les mêmes appels qu'aujourd'hui. Avec un autre ordre, il passe par les surcharges de C2.</li></ul> |
| C4 | **Tests sans GPU**, par `FillVertices`. <ul><li>À Z égal, des `draw_order` différents donnent l'ordre croissant, quel que soit l'ordre de soumission.</li><li>À `draw_order` égal, l'ordre de soumission est gardé sur **64 entrées** de même Z et de même `draw_order`. `List<T>.Sort` trie par insertion, de façon stable, sous 16 entrées : un test plus petit passerait sans le départage.<ul><li>Le test tourne **sur les deux chemins** : 64 entrées par un appel de sprite (le `DrawSprite` du cœur), puis 64 entrées par `DrawPsxQuad` (`QueuePsxQuad`).</li><li>La note de T1.1 consigne trois exécutions : sans la comparaison de `Submission`, les deux variantes échouent ; sans l'affectation de `Submission` dans `QueuePsxQuad`, la variante PSX échoue ; avec tout en place, les deux passent.</li></ul></li><li>**Réutilisation du pool.** Le pool est une pile (`Stack<SpriteDisplayData>`, `:1106-1119`), et chaque chemin prend la première entrée libre.<ul><li>Le test met en file **deux** entrées à `draw_order` non nul, puis vide la file. Les deux entrées périmées sont ainsi dans le pool.</li><li>Il met ensuite en file une entrée par `QueuePsxQuad` et une par un appel sans `draw_order` : chacune reprend une entrée périmée. Les deux ont `DrawOrder == 0`.</li><li>La note de T1.1 consigne trois exécutions : l'une échoue quand l'affectation de `DrawOrder` est retirée de `QueuePsxQuad`, une autre quand elle est retirée du `DrawSprite` du cœur, et la dernière passe avec les deux affectations.</li></ul></li><li>À Z différents, l'ordre est inchangé.</li><li>Les entrées à clé ne changent pas.</li><li>Côté JSON : chargement, sauvegarde et refus d'un `draw_order` malformé, pour les deux composants.</li><li>Aucune allocation ajoutée par la mise en file.</li></ul> |
| C5 | **Docs.** ADR-0092. `docs/engine/static-sprite-component.md` (ordre de dessin) et `docs/engine/video.md` §9 gagnent le champ et la règle de départage |
| C6 | **Sample `Projects/SpriteDrawOrderSample`.** <ul><li>Un projet sans DLL, avec un monde à caméra 2D et trois sprites en mélange `AlphaBlend` qui se recouvrent, au même Z.</li><li>Leurs `draw_order` sont donnés dans l'ordre inverse de leur ordre dans le `.world`. L'empilement visible suit donc `draw_order`, et non l'ordre des données.</li><li>Les images sont synthétiques, générées par un code de test. Un test de données vérifie les pixels, les références et le champ.</li></ul> |
| C7 | **Launcher.** Toute exécution du Launcher par l'agent est annoncée à l'auteur avant de commencer (C9 d'ADR-0091) |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/sprite-draw-order`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin :
  1. lancer la validation indiquée ;
  2. remplacer l'icône par `✅`, `🧪` ou `⚠️` ;
  3. ajouter une courte note de validation sous la tâche ;
  4. **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur.
- **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, jamais avec `--no-build`.
- Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (le tri, la mise en file, le dessin des composants) ; sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression** : les tests existants restent verts sans être modifiés. Les samples existants ne sont pas touchés.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/SpriteDrawOrderSample/SpriteDrawOrderSample.json`.

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0092

- Objectif : committer ce plan et sa ligne d'index, et écrire ADR-0092, qui reprend D1 à D6 et C1 à C7.
- Validation : ADR relue contre D1 à D6 et C1 à C7 ; contrôle D4.
- Commit : `docs(sprites): plan a draw order for sprites of equal depth, and record ADR-0092`
- Note : plan approuvé par l'auteur le 2026-11-10 (« J'approuve le plan »), lancé le même jour (« Oui, lance-le »). ADR-0092 (`docs/decisions/0092-sprites-of-equal-depth-follow-a-draw-order.md`) relue contre D1 à D6 et C1 à C7. Contrôle D4 fait par `rg` sur le plan, l'index et l'ADR : aucun nom.

---

## Phase 1 — Ordre de dessin

### ✅ T1.1 — File du renderer et surcharges

- Objectif : C1 et C2.
- Fichiers : `SpriteRendererComponent.cs` et des tests.
- Validation :
  - tests de C4 sur la file, dont le test de 64 entrées sur les deux chemins, avec ses exécutions consignées ici (échec sans la comparaison de `Submission`, échec de la variante PSX sans l'affectation de `Submission` dans `QueuePsxQuad`, succès avec tout en place) ;
  - le test de réutilisation du pool, avec ses trois exécutions consignées ici (échec sans l'affectation de `QueuePsxQuad`, échec sans celle du cœur, succès avec les deux) ;
  - la liste des membres publics de `SpriteRendererComponent` avant et après, qui ne diffère que par les ajouts 1, 2 et 4 de C2 ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(sprites): order sprites of equal depth by a draw order, then by submission`
- Note : fait le 2026-11-10.
  - Code : `SpriteDisplayData` gagne `DrawOrder` et `Submission`, affectés sur chaque entrée par le `DrawSprite` privé du cœur (`drawOrder` facultatif, 0) et par `QueuePsxQuad` (`DrawOrder = 0`, `Submission` = rang dans la file). Comparateur sans clé : plus grand Z, puis plus petit `DrawOrder`, puis plus petit `Submission`. Les quatre surcharges de C2 sont ajoutées (trois publiques, une interne) ; les surcharges existantes à découpe déléguent aux nouvelles avec 0, sans changer de signature.
  - Tests : `CasaEngine.Tests/Rendering/SpriteRendererComponentDrawOrderTests.cs`, 14 tests (64 entrées sur les deux chemins et entrelacés, rang de soumission, ordre à Z égal, Z différents, surcharges, pool, entrées à clé, zéro allocation).
  - Exécutions consignées, 64 entrées : sans la comparaison de `Submission`, les trois tests de 64 entrées échouent (sprite, PSX, entrelacé) ; sans l'affectation de `Submission` dans `QueuePsxQuad`, la variante PSX et l'entrelacée échouent (et les tests de rang) ; avec tout en place, 14 sur 14 passent.
  - Exécutions consignées, pool : sans `DrawOrder = 0` dans `QueuePsxQuad`, `ReusedEntries_NeverKeepTheDrawOrderOfAPreviousFrame` échoue (1 échec) ; sans `DrawOrder = drawOrder` dans le cœur, ce test échoue aussi (avec trois autres tests d'ordre) ; avec les deux, il passe. Écart : la recette du plan (deux entrées périmées créées par des appels à `draw_order` non nul) ne suffit pas pour la variante du cœur, car sans l'affectation du cœur ces entrées n'ont jamais de valeur non nulle. Le test empoisonne donc le pool par réflexion (`DrawOrder` 77, `Submission` 99 sur chaque entrée libre) après le vidage de la file.
  - Membres publics de `SpriteRendererComponent`, listés dans le source avant et après : seules les surcharges 1, 2 et 4 de C2 apparaissent (la 3 est interne) ; les deux champs de la structure privée `SpriteDisplayData` apparaissent aussi dans la liste mais ne sont pas des membres de la classe.
  - Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5555 réussis sur 5555, aucun test existant modifié.

### ⏳ T1.2 — Champ `draw_order` des composants

- Objectif : C3.
- Fichiers : `StaticSpriteComponent.cs`, `VideoSpriteComponent.cs`, `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`, et des tests.
- Validation : tests de C4 sur le JSON et sur les appels des deux composants ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(sprites): let sprite and video components carry a draw order`

---

## Phase 2 — Sample et documentation

### ⏳ T2.1 — Sample `Projects/SpriteDrawOrderSample`

- Objectif : C6.
- Validation : le test de données passe. Au Launcher, après annonce à l'auteur (C7), une capture hors dépôt montre l'empilement attendu ; sinon, c'est noté pour la recette. Deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(samples): add a sample that orders sprites of equal depth`

### ⏳ T2.2 — Documentation

- Objectif : C5.
- Validation : relecture contre le code livré ; deux `.sln` à 0 erreur.
- Commit : `docs(sprites): describe the draw order of sprites of equal depth`

---

## Phase 3 — Vérification et recette

### ⏳ T3.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur la branche : C1 à C7, D2 à D6, la non-régression, D4, les builds et les tests.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of the sprite draw order`

### ⏳ T3.2 — Recette de l'auteur

- Objectif : l'auteur lance le sample et l'accepte.
- Validation : accord de l'auteur, écrit ici. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the accepted check of the sprite draw order sample`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | À Z et `draw_order` égaux, l'ordre de soumission vient de l'octree et peut changer quand un sprite bouge. Le départage par la soumission rend seulement chaque image déterministe. Les données qui veulent un ordre fixe donnent des `draw_order` différents | T2.2 |

## Hors périmètre

- Un ordre pour les sprites à clé (`DepthSortable2DComponent`).
- Une action de cutscene qui change le `draw_order`.
- Le changement de l'ordre de soumission du monde.
