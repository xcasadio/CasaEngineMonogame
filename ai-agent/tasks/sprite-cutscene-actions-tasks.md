# Plan agent IA — Actions de cutscene pour des sprites

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-09. Une cutscene doit pouvoir jouer une séquence de sprites 2D sans code de jeu : afficher un sprite, le déplacer à vitesse constante, le teinter sur une durée et le cacher. Elle doit aussi boucler, et sortir vers une suite donnée à l'appui d'une touche parmi une liste.

Les décisions D1 → D8 ont été arbitrées avec l'auteur le 2026-11-09. Les choix C1 → C10 sont proposés par ce plan et verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Relu par trois plan-verifiers neufs, qui ont tous rendu REVISE :
1. ordre de dessin d'un sprite mélangé, fin de la veille, touche tenue à l'armement ;
2. format de `color` dans les actions ;
3. chargement d'un sprite nommé en JSON, champs malformés, type de `count`.

Chaque point est corrigé. Aucune relecture n'a rendu READY. L'auteur approuve le plan le 2026-11-09 (« J'approuve le plan »). Le lancement attend son go.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- **Sprites pilotables par données.** Une entité du monde dont le composant racine est un `StaticSpriteComponent` peut être cachée au chargement, teintée et dessinée en mélange additif ou soustractif.
- **Quatre actions de sprite, comptées en temps.** `ShowSprite`, `HideSprite`, `MoveSprite` et `TintSprite` visent une entité par son nom. Déplacement et teinte démarrent puis laissent la séquence continuer, et avancent avec le temps écoulé.
- **Une action `Repeat`** qui rejoue ses enfants sans fin ou N fois.
- **Une action `ExitOnInput`** qui arme une veille pour le reste de la cutscene. À l'appui d'une des entrées nommées d'un mapping, elle arrête la cutscene et lance une cutscene de suite.
- **Livrables associés** : ADR-0090 et un sample `Projects/SpriteCutsceneSample` avec des images synthétiques.

Le chantier ne touche pas aux actions existantes et n'ajoute ni animation d'images, ni rotation, ni échelle (voir « Hors périmètre »).

## État vérifié du dépôt (2026-11-09)

Lu sur `main` à `da5c75af`, égal au pointeur du projet consommateur. Lecture seule, par une découverte en cinq parties dont les faits décisifs ont été recontrôlés.

- **Cutscenes.**
  - Types d'action : `CutsceneActionTypes.cs:5-21`. Il n'y a ni branchement, ni boucle, ni attente de touche, et aucune action ne vise un sprite.
  - Une action touche six endroits :
    - le type, dans `CutsceneActionTypes` ;
    - la classe `*CutsceneActionData` ;
    - le sérialiseur (`Serialization/CutsceneAssetJsonSerializer.cs:48-174` à l'écriture, `:187-248` à la lecture). Un type inconnu devient `UnknownCutsceneActionData` ;
    - le validateur (`CutsceneValidator.cs:20-169`). Les enfants y sont parcourus par `ValidateActionList` ;
    - la fabrique (`CutsceneActionCoroutineFactory.cs:30-241`) ;
    - le builder en lecture seule de l'éditeur (`CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs`).
  - `Sequence` et `Parallel` gardent leurs enfants dans une `List<CutsceneActionData>`. Ils sont sérialisés par `SaveActions` et `LoadActions` (`CutsceneAssetJsonSerializer.cs:176-185`, `:380-394`).
  - Les champs requis récents passent par `FormatErrors` (`:273-305`).
  - `FindEntityByName` existe déjà dans la fabrique (`CutsceneActionCoroutineFactory.cs:428-440`).
- **Directeur.**
  - Il appartient au monde : `World.CutsceneDirector` (`World/World.cs:105`), créé par `WorldRuntimeSystems.cs:12,17`.
  - `Play` arrête d'abord la lecture en cours (`CutsceneDirector.cs:53-62`). Un directeur ne joue qu'une cutscene à la fois.
  - `Stop` arrête toutes les coroutines dont il est le propriétaire (`:147-152`), enfants de `Parallel` compris. `Ended` est levé une fois par lecture : `Completed`, `Failed` ou `Stopped` (`:154-183`).
  - La cutscene de démarrage d'un monde se charge par Guid (`World.cs:545-579`). Aucune action ne lance une autre cutscene.
- **Temps.**
  - `Wait` décompte le temps de jeu mis à l'échelle (`WaitForSeconds.cs:18-22`), comme `FadeScreen` et `FadeMusic` (`CutsceneActionCoroutineFactory.cs:470`, `:520`).
  - Dans `World.Update`, les systèmes, coroutines comprises, passent avant les entités (`World.cs:618-636`, `WorldRuntimeSystems.cs:19-24`). Les entités reçoivent `frameTime.DeltaTime`, le temps mis à l'échelle.
  - `StartCoroutine` ne fait qu'inscrire la coroutine : son premier pas a lieu à la mise à jour suivante (`CoroutineManager.cs:36-48`).
- **Sprite d'entité.**
  - `StaticSpriteComponent` (`Scene/Entities/Components/StaticSpriteComponent.cs`) dessine toujours avec `Color.White` et sans mélange (`:133`, `:137`).
  - Sa propriété `Color` n'est pas utilisée. Elle vaut par défaut `default(Color)`, un noir transparent, et le constructeur de copie ne la reprend pas (`:36`, `:45-54`).
  - `Load` ne lit que `spriteDataName` (`:154-164`). `TryLoadSpriteData(string)` est public (`:261`).
  - **Un sprite nommé en JSON ne se charge pas aujourd'hui.**
    - `Load` appelle `LoadSpriteData`, qui lit `Owner.World.Game.AssetContentManager` (`:154-169`). Or le composant n'est attaché à son entité qu'après `ElementFactory.Load`, dans le setter de `RootComponent` (`Entity.cs:81-86`, `:619-623`). Tout `spriteDataName` réel lève donc une `NullReferenceException`.
    - Le constructeur de copie ne reprend ni `_sprite` ni `Color` (`:50-54`). `InitializeWithWorld` ne reconstruit le sprite que si `SpriteAssetId` est posé et `_spriteData` nul (`:69-76`). Un clone d'entité asset ne se dessine donc pas.
  - Aucun `.world` de `Projects/` n'utilise ce composant.
- **Rendu 2D.**
  - `SpriteRendererComponent.DrawSprite` a une surcharge avec `SpriteBlendMode` (`Application/Components/SpriteRendererComponent.cs:641`). `SpriteBlendMode` vaut `Opaque`, `AlphaBlend`, `Additive` ou `Subtractive`. Le mode est appliqué par suite contiguë d'entrées (`:252-273`, `:356-365`).
  - La semi-transparence PSX par texel d'ADR-0051 est un autre chemin, piloté par `SpriteData.PsxSemiTransparency` (`:656`, `:662`).
- **Entités.**
  - `Entity.IsVisible` existe et coupe `Draw` (`Scene/Entities/Entity.cs:130`, `:546`). Il n'est lu d'aucun JSON.
  - Le type d'un composant se résout par son nom simple (`Assets/ElementFactory.cs:24-33`). Un sprite se déclare par un asset `.sprite` (`Configuration/Constants.cs:16`), chargé en `SpriteData` (`Assets/AssetLoaderRegistry.cs:45`).
- **Entrée.**
  - Un `.buttonsMapping` contient des entrées nommées. Chacune a une touche et une touche de rechange, qui portent clavier, manette et souris (`Projects/VideoSample/ui-input.buttonsMapping`).
  - `InputMappingManager` (`Input/InputMappingManager.cs`) offre `AddInputMapping`, `TryGet(name)`, `Update(KeyboardManager, MouseManager, GamePadManager)` et `GetButtonState(name)`. `GetButtonState` lève sur un nom inconnu (`:73`), et l'appui de l'image est `Pressed && !PressedPreviousFrame` (`:76-87`).
  - `InputComponent` met à jour son gestionnaire une fois par image (`Input/InputComponent.cs:104`).
  - `GameManager.FindButtonsMappingAsset(idOrName)` trouve un mapping catalogué (`Application/GameManager.cs:394-400`).
  - `UINavigationInput` lit les mêmes appareils de son côté (`UI/UINavigationInput.cs:260-266`).
- **Tests.**
  - `CasaEngine.Tests/Cutscenes/` (`FrameCutsceneActionsTests`, `ScreenAndWorldCutsceneActionsTests`, `PlayVideoCutsceneActionTests`, `CutsceneAssetJsonSerializerTests`, `CutsceneDirectorTests`), `Rendering/SpriteRendererComponentBlendModeTests.cs`, `Input/InputMappingManagerContainsTests.cs` et `UI/VideoSampleDataTests.cs`.
  - `AllocationWindow.Start()` mesure les allocations.
- **ADR.** Prochain numéro libre : 0090.
- **Arbre de travail.** Branche `chantier/sprite-cutscene-actions` créée depuis `main` `da5c75af`. Aucun changement préexistant.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-09.**

| Réf | Décision |
|---|---|
| D1 | **Contenu demandé.** <ul><li>Des actions de cutscene qui affichent un sprite (image, position, mélange additif ou soustractif), le déplacent à vitesse constante, le teintent sur une durée et le cachent.</li><li>Une veille sur une liste de touches, qui sort de la cutscene vers une suite donnée.</li><li>Une boucle.</li><li>Une ADR, et un sample où une boucle de sprites bouge, se teinte et sort sur une touche.</li></ul> |
| D2 | **Les sprites sont des entités du monde.** Le `.world` déclare des entités à composant sprite. Les actions les visent par leur nom |
| D3 | **Temps, comme dans un moteur moderne.** Les actions de sprite se comptent en secondes et en pixels par seconde, et avancent avec le temps écoulé, sans pas par image. Le consommateur convertit ses durées comptées en images. Mots de l'auteur : « il faut convertir le temps mesuré par nombre de frames de la PSX (avec un temps fixe) par un calcul avec le temps (comme dans les moteurs modernes) » |
| D4 | **Sortie par une veille sur des entrées nommées.** Une action arme une veille pour le reste de la cutscene. À l'appui d'une des entrées nommées d'un mapping d'entrée du projet, donc clavier et manette en données, la cutscene s'arrête et une cutscene de suite démarre |
| D5 | **Boucle par une action `Repeat`.** Elle rejoue ses enfants sans fin ou N fois, dans la même cutscene, sans recharger le monde |
| D6 | **Format et API additifs.** Un projet, un monde ou une cutscene existants se chargent et se comportent à l'identique. ADR-0090 enregistre les décisions |
| D7 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur, un jeu ou une décompilation. Les données sont synthétiques |
| D8 | Simple et optimisé : pas d'allocation par image dans les mises à jour de sprite ni dans la veille |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **`StaticSpriteComponent`, champs additifs.** <ul><li>`Color` vaut blanc par défaut, est repris par le constructeur de copie et est passé au rendu. Un sprite existant reste dessiné en blanc, donc à l'identique.</li><li>`BlendMode` (`SpriteBlendMode`), par défaut le chemin actuel : les appels de `Draw` d'aujourd'hui (`StaticSpriteComponent.cs:128-137`) restent inchangés. Le mode s'applique à tout le sprite, sans la semi-transparence par texel d'ADR-0051 (O1).</li><li>**Ordre de dessin d'un sprite mélangé.** La seule surcharge `Sprite` qui prend un mélange impose une clé de tri (`SpriteRendererComponent.cs:641-644`), et le comparateur compare les clés dès qu'une des deux entrées en a une (`:553-569`). Règle : <ul><li>avec un `DepthSortable2DComponent`, le sprite mélangé passe par cette surcharge et sa clé, comme le dessin non mélangé d'aujourd'hui ;</li><li>sans ce composant, il passe par une nouvelle surcharge publique additive, `DrawSprite(Sprite, Vector2, float, Vector2, Color, float zOrder, SpriteBlendMode)`, qui met l'entrée en file sans clé (`hasSortKey: false`), par le chemin privé qui prend déjà un mélange (`:1002-1004`). Le sprite garde donc son ordre par Z, comme un sprite opaque.</li></ul></li><li>JSON facultatif : `color` et `blend_mode`. `color` suit le format objet des entités, `{"r", "g", "b", "a"}`, quatre clés de 0 à 255, lu par `JsonHelper.GetColor` (`Core/Serialization/JsonHelper.cs:124-132`) et écrit par `EditorJsonSaveHelper.Save(Color)`. Il vaut blanc s'il est absent. `blend_mode` est le nom exact de l'énumération.</li><li>`Entity.IsVisible` se lit du JSON par une clé facultative `is_visible` (vrai si absente) et s'écrit par le chemin de sauvegarde des entités, aller-retour testé.</li><li>**Champ malformé** : un `color` sans l'une des quatre clés ou avec une valeur hors de 0..255, un `blend_mode` inconnu, un `is_visible` qui n'est pas un booléen. Le chargement échoue alors avec une `InvalidDataException` qui nomme le composant, le champ et la valeur. Aucune valeur par défaut silencieuse.</li><li>**Chargement du sprite nommé** (correctif lié, voir « État vérifié »). `Load` ne fait que retenir `spriteDataName`. Un nom absent vaut `"null"` : pas de sprite. Le nom est résolu dans `InitializeWithWorld`, une fois le monde connu, par le catalogue et un handle comme aujourd'hui. Un nom introuvable y lève une exception qui nomme le sprite.</li><li>**Copie.** Le constructeur de copie reprend `Color`, `BlendMode` et le nom ou l'id du sprite, et un clone reconstruit son sprite dans `InitializeWithWorld`. Une entité déclarée en ligne dans un `.world`, comme une entité asset clonée (`LoadCopy<Entity>(id).Clone()`), se dessine avec son sprite, sa couleur et son mode.</li></ul> |
| C2 | **État de mouvement et de teinte sur le composant.** <ul><li>`StaticSpriteComponent` gagne un déplacement en cours (cible, vitesse) et une teinte en cours (départ, cible, durée, temps écoulé), avancés dans son `Update` par le temps reçu.</li><li>Le déplacement suit la droite vers la cible à vitesse constante. La position est `départ + direction × min(vitesse × t, distance)`, et la cible est posée exactement à l'arrivée. Seuls X et Y bougent, Z reste.</li><li>La teinte est linéaire entre la couleur de départ et la cible, arrondie à l'entier le plus proche par composante, et posée exactement à la fin. Une durée 0 pose la cible tout de suite.</li><li>Un nouveau déplacement ou une nouvelle teinte remplace celui en cours. Afficher ou cacher le sprite arrête les deux.</li><li>Une action lancée à l'image U avance dès l'`Update` des entités de U, qui suit les coroutines. Les tests fixent cette règle.</li><li>Aucune allocation dans `Update`.</li></ul> |
| C3 | **Actions de sprite**, toutes instantanées : la séquence continue à la même image. Champs en snake_case. <ul><li>`ShowSprite` : `entity` (requis), puis, facultatifs, `sprite` (nom d'un asset `.sprite`), `x` et `y` (ensemble), `color` et `blend_mode`. Un champ absent garde la valeur actuelle. L'action rend l'entité visible et arrête mouvement et teinte.</li><li>`HideSprite` : `entity`. Rend l'entité invisible, arrête mouvement et teinte.</li><li>`MoveSprite` : `entity`, `x`, `y` (cible) et `speed` (pixels par seconde, strictement positif).</li><li>`TintSprite` : `entity`, `color` (cible) et `duration` (secondes, au moins 0).</li><li>**Format de `color` dans une action** (`ShowSprite`, `TintSprite`) : la convention des cutscenes, celle de `FadeScreenFrames`. C'est un tableau `[r, g, b]` (alpha 255) ou `[r, g, b, a]`, d'entiers de 0 à 255, lu par `ReadColor` et écrit en quatre entrées par `SaveColor` (`Serialization/CutsceneAssetJsonSerializer.cs:307-334`, `:361-364`).</li><li>Unités : `x` et `y` sont la position de l'entité dans le monde. T1.1 lit et documente la convention 2D (origine, sens de Y, unité de la caméra 2D), et T3.1 la mesure.</li></ul> |
| C4 | **`Repeat`** : `count`, requis, un entier JSON d'au moins 0 (0 pour sans fin, un nombre décimal comme 2.5 est refusé), et `actions` (liste, comme `Sequence`). <ul><li>Chaque tour joue les enfants en séquence.</li><li>Un tour sans fin qui se termine sans qu'une image passe fait échouer la lecture (`Failed`), avec un message qui nomme l'action. Cela évite de geler le jeu.</li><li>Une liste vide donne un avertissement, comme `Sequence`.</li></ul> |
| C5 | **`ExitOnInput`** : `mapping` (nom ou id d'un `.buttonsMapping` catalogué), `inputs` (liste de noms d'entrées, au moins un) et `next_cutscene` (nom ou id d'une cutscene cataloguée). <ul><li>L'action est instantanée : elle arme une veille, coroutine dont le directeur est propriétaire. Une nouvelle `ExitOnInput` remplace la veille en cours.</li><li>**Durée de vie.** Le directeur garde le handle de la veille et l'arrête avant de lever `Ended`, à chaque fin de lecture : `Completed` (aujourd'hui, cette branche n'arrête rien, `CutsceneDirector.cs:133-144`), `Failed`, `Stopped`, et le remplacement par un nouveau `Play`. Aucune veille ne survit à sa lecture.</li><li>La veille garde son propre `InputMappingManager`, limité aux entrées listées. Elle n'entre donc pas en collision avec les mappings du jeu. Elle le met à jour à chaque image avec les appareils de l'`InputComponent`, et réagit à l'appui de l'image (`Pressed && !PressedPreviousFrame`), pas au maintien.</li><li>**Règle d'armement.** La première mise à jour après l'armement ne fait qu'enregistrer l'état des entrées et ne peut pas déclencher, puisque `InputMapping.Update` copie d'abord `Pressed` dans `PressedPreviousFrame` (`InputMapping.cs:27-34`). Une touche tenue à l'armement ne déclenche qu'après un relâchement puis un nouvel appui. Ainsi, la touche qui a quitté une cutscene ne fait pas sauter la suivante.</li><li>À l'appui, la veille demande au directeur de jouer la cutscene de suite. `Play` arrête d'abord la lecture en cours (`Ended` à `Stopped`), veille comprise, puis lance la suite. Un test prouve que cet arrêt depuis la coroutine de la veille est sûr. Le moment exact, entre l'appui et le premier pas de la suite, est mesuré par un test et écrit dans la doc (O3).</li><li>Le même appui peut aussi atteindre la navigation de l'UI (par exemple le saut d'une vidéo) : documenté (O4).</li></ul> |
| C6 | **Échecs.** <ul><li>Types JSON : `x`, `y`, `speed` et `duration` sont des nombres, entiers ou décimaux, lus en `float`. `count` est un entier. `entity`, `sprite`, `mapping`, `next_cutscene` et chaque élément d'`inputs` sont des chaînes non vides.</li><li>Le validateur refuse un champ requis absent ou mal formé (type ci-dessus), une vitesse non strictement positive, une durée ou un `count` négatifs, un `color` d'action qui n'est pas un tableau de 3 ou 4 entiers de 0 à 255 (message de `ReadColor`), une liste `inputs` vide, `x` sans `y` ou l'inverse, et un `blend_mode` inconnu.</li><li>À l'exécution, une entité inconnue, une entité sans `StaticSpriteComponent` racine, un sprite, un mapping, une entrée ou une cutscene introuvables font échouer la lecture (`Failed`). Le message nomme le champ et la valeur, comme `OpenScreen`.</li></ul> |
| C7 | **Éditeur.** Le builder en lecture seule montre les nouveaux types et leurs champs. L'édition de ces actions dans l'éditeur n'est pas faite |
| C8 | **Sample `Projects/SpriteCutsceneSample`.** <ul><li>Un projet sans DLL : un monde à caméra 2D, quatre entités sprite cachées au chargement, sans `DepthSortable2DComponent` (ordre par Z, C1), un mapping `sample-input` avec une entrée `exit` (touche du clavier et bouton de manette), une cutscene de démarrage et une cutscene de suite.</li><li>La cutscene de démarrage arme `ExitOnInput`, puis lance un `Repeat` sans fin : des sprites additifs et soustractifs entrent par un bord avec `MoveSprite`, s'allument puis s'éteignent avec `TintSprite`, et se cachent, avec des `Wait` entre les deux.</li><li>La suite fait un `FadeScreen` vers le noir, puis un `ChangeWorld` vers un monde de fin qui revient au premier après une attente.</li><li>Les images sont synthétiques, générées par un code de test. Un test de données vérifie leurs pixels décodés, les références des mondes et des cutscenes, et la validation des cutscenes.</li></ul> |
| C9 | **Tests sans GPU** pour la logique : <ul><li>la sérialisation aller-retour et la validation de chaque action ;</li><li>les calculs de mouvement et de teinte avec des pas de temps choisis (arrivée exacte, remplacement, durée 0, `TimeScale`) ;</li><li>`Repeat` (N tours, tour vide sans fin en échec, arrêt pendant un tour) ;</li><li>`ExitOnInput` : appui, maintien sans effet, remplacement de la veille, suite jouée et `Ended` ;</li><li>`ExitOnInput` avec une touche tenue avant et pendant l'armement, sans arrêt ni suite. Après un relâchement puis un nouvel appui, la lecture s'arrête et la suite fait son premier pas à l'image mesurée ;</li><li>`ExitOnInput` dans une cutscene qui se termine (`Completed`) : un appui à une image suivante ne lance aucune suite, et `GetDebugSnapshot` ne montre plus aucune coroutine du directeur ;</li><li>l'absence d'allocation dans `Update` et dans la veille.</li></ul> Le rendu est testé sans GPU sur le modèle de `SpriteRendererComponentBlendModeTests`. Couleur et mode doivent arriver au renderer. Pour un sprite mélangé sans `DepthSortable2DComponent`, l'entrée en file porte le mode, n'a pas de clé, et se range par Z face à un sprite opaque |
| C10 | **Docs.** ADR-0090. La doc des cutscenes (`docs/engine/cutscene_commandes_sequentielles_async_coroutine.md`) gagne les cinq actions, leurs champs, la règle du temps et les échecs. La doc du composant sprite reçoit les nouveaux champs |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/sprite-cutscene-actions`**, créée depuis `main`. Ne jamais committer sur `main`.
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
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds : `StaticSpriteComponent.Update` et le pas de la veille ;
  - restaurer tout état GPU modifié ;
  - le runtime ne dépend pas de l'éditeur ;
  - sérialisation additive.

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
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/SpriteCutsceneSample/SpriteCutsceneSample.json`.

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0090

- Objectif : committer ce plan et sa ligne d'index, et écrire ADR-0090, qui reprend D1 à D8 et C1 à C10.
- Fichiers : `ai-agent/tasks/sprite-cutscene-actions-tasks.md`, `ai-agent/README.md`, `docs/decisions/0090-cutscene-actions-drive-named-sprite-entities-over-time.md`, `docs/decisions/README.md`.
- Validation : ADR relue contre D1 à D8 et C1 à C10 ; contrôle D7 (aucun nom de projet consommateur, de jeu ou de décompilation).
- Commit : `docs(cutscenes): plan sprite cutscene actions and record ADR-0090`
- Note : plan approuvé par l'auteur le 2026-11-09 (« J'approuve le plan »), lancé le même jour (« Oui, lance-le »). ADR-0090 relue contre D1 à D8 et C1 à C10. Contrôle D7 fait par `rg` sur le plan, l'index et l'ADR : aucun nom.

---

## Phase 1 — Composant sprite

### ✅ T1.1 — Couleur, mélange et visibilité au chargement

- Objectif : C1.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs`, `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs` (surcharge de C1), `CasaEngine/Framework/Scene/Entities/Entity.cs`, le chemin de sauvegarde des entités (à localiser), et des tests dans `CasaEngine.Tests`.
- Étapes :
  1. Lire et noter ici la convention 2D (origine, sens de Y, unité de la caméra 2D) avec ses `fichier:ligne`, pour C3.
  2. Couleur blanche par défaut, copie de la couleur, couleur et mode passés au rendu ; le chemin actuel est gardé pour le mode par défaut.
  3. Lecture des champs JSON `color`, `blend_mode` et `is_visible`, et écriture par le chemin de sauvegarde existant.
- Validation :
  - tests de chargement : des champs absents donnent l'état actuel ;
  - aller-retour de sauvegarde ;
  - couleur et mode reçus par le renderer ;
  - un test sans GPU sur un sprite mélangé sans `DepthSortable2DComponent` : l'entrée en file porte le mode, n'a pas de clé, et se range par Z face à un sprite opaque ;
  - un test sur un sprite mélangé avec ce composant : l'entrée porte le mode et la clé ;
  - une entité dont le `StaticSpriteComponent` a un vrai `spriteDataName`, un `color` et un `blend_mode`, chargée en ligne puis comme entité asset clonée, et ajoutée à un monde : sans exception, le renderer reçoit le sprite, la couleur et le mode ;
  - refus avec contexte d'un `color` incomplet ou hors bornes, d'un `blend_mode` inconnu et d'un `is_visible` non booléen ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(sprites): let a sprite component carry a colour, a blend mode and a hidden start`
- Note :
  - **Convention 2D (étape 1)** : l'unité du monde est le pixel et Y monte (`Camera2dComponent.cs:11`). La caméra orthographique est centrée sur `Target` (`:84-87` projection, `:106` `CreateLookAt(..., Vector3.Up)`) et son volume vaut la taille du viewport divisée par le zoom. `position` d'un sprite est le point d'origine du sprite : le centre du quad est à `position.X - origin.X*scale + (w/2)*scale` et `position.Y + origin.Y*scale - (h/2)*scale` (`SpriteRendererComponent.cs:1066-1067`), donc le haut du sprite est à `position.Y + origin.Y` et Y croît vers le haut. L'ordre par Z : le Z le plus grand est dessiné en premier (`CompareSpriteDisplayData`, `:553`, le retour de la comparaison de Z en `:568`). T3.1 doit mesurer cette convention sur capture.
  - **Fait** : `StaticSpriteComponent` gagne `Color` (blanc par défaut, copié, passé au rendu) et `BlendMode` (`Opaque` par défaut = chemins actuels). Nouvelle surcharge publique `SpriteRendererComponent.DrawSprite(Sprite, Vector2, float, Vector2, Color, float, SpriteBlendMode)` sans clé de tri (plus une jumelle `internal` qui prend le scissor, pour les tests sans GPU). Avec `DepthSortable2DComponent`, le sprite mélangé passe par la surcharge à clé. `Draw` délègue à `Submit(Rectangle)` interne.
  - **Chargement du sprite nommé** : `Load` retient `spriteDataName` (absent ou `"null"` : pas de sprite), le nom est résolu dans `InitializeWithWorld` ; un nom introuvable lève une `InvalidDataException` qui nomme le sprite (aussi pour `TryLoadSpriteData`, qui levait avant une `NullReferenceException`). Le constructeur de copie reprend nom, id, couleur et mode ; `InitializeWithWorld` reconstruit le sprite tant que `_sprite` est nul, donc un clone se dessine.
  - **JSON** : `color` (objet r,g,b,a de 0 à 255), `blend_mode` (nom exact) et `is_visible` (booléen) sont lus ; un champ malformé lève une `InvalidDataException` avec composant, champ et valeur. Écriture dans `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (`SaveEntity`, `SaveStaticSpriteComponent`) : seulement si la valeur diffère du défaut, pour qu'un fichier existant reste identique. Le nom de sprite encore non résolu est aussi réécrit.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5268 réussis, 0 échec (25 nouveaux tests dans `CasaEngine.Tests/Scene/StaticSpriteComponentColorBlendTests.cs`, aucun test existant modifié).
  - **Écart** : aucun par rapport au plan. Le test « entité asset clonée » utilise `Entity.Clone()` sur une entité chargée du JSON (le chemin de `LoadCopy<Entity>(id).Clone()` sans le disque).

### ✅ T1.2 — Mouvement et teinte avancés par le temps

- Objectif : C2.
- Fichiers : `StaticSpriteComponent.cs` et des tests.
- Validation : tests de C9 sur le mouvement et la teinte, et absence d'allocation dans `Update` ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(sprites): move and tint a sprite component over time`
- Note :
  - **Fait** : `StaticSpriteComponent` gagne `StartMove(Vector2 cible, float vitesse)`, `StartTint(Color cible, float durée)`, `StopMove()`, `StopTint()`, `StopMotionAndTint()`, `IsMoving` et `IsTinting`. `Update` avance d'abord le mouvement puis la teinte avec le temps reçu (donc mis à l'échelle), avant le reste de l'`Update`. Le mouvement part de la position courante, suit la droite à vitesse constante (`départ + direction × min(vitesse × t, distance)`), laisse Z inchangé et pose la cible exactement à l'arrivée ; une cible égale à la position est posée tout de suite. La teinte est linéaire, arrondie à l'entier le plus proche par composante, posée exactement à la fin ; une durée 0 pose la cible tout de suite. Un nouvel appel remplace l'état en cours.
  - **Pour T2.1** : `ShowSprite` et `HideSprite` appellent `StopMotionAndTint()` (le composant ne connaît pas `Entity.IsVisible`, que l'action pose). Une vitesse non strictement positive ou une durée négative ou non finie lève `ArgumentOutOfRangeException` (erreur de programmation) : le validateur de T2.x les refuse en amont.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5293 réussis, 0 échec (25 nouveaux tests dans `CasaEngine.Tests/Scene/StaticSpriteComponentMotionTintTests.cs`, dont l'absence d'allocation du pas de mouvement et de teinte de `Update` (composant détaché) mesurée avec `AllocationWindow` ; aucun test existant modifié).
  - **Écart** : aucun. La règle « une action lancée à l'image U avance dès l'`Update` des entités de U » relève du monde et sera fixée par les tests de T2.2.

---

## Phase 2 — Actions de cutscene

### ✅ T2.1 — `ShowSprite` et `HideSprite`

- Objectif : C3 pour ces deux actions, et C6 et C7 pour elles.
- Fichiers : les six endroits d'une action (« État vérifié »), et des tests.
- Validation :
  - sérialisation, validation, exécution et échecs testés ;
  - aller-retour de `color` en `[r, g, b]` (relu avec alpha 255) et en `[r, g, b, a]` ;
  - refus d'un objet `{"r", …}`, d'un tableau de 2 ou 5 entrées et d'une composante hors de 0..255 ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(cutscenes): show and hide sprite entities from a cutscene`
- Note :
  - **Fait** : types `ShowSprite` et `HideSprite`, classes `ShowSpriteCutsceneActionData` (`EntityName`, `Sprite`, `X`, `Y`, `Color`, `BlendMode`, `FormatErrors`) et `HideSpriteCutsceneActionData`, lecture et écriture JSON (`entity`, `sprite`, `x`, `y`, `color` en tableau via `ReadColor` et `SaveColor`, `blend_mode`), validateur (entité requise et non vide, champs malformés par `FormatErrors`, `x` sans `y` refusé), fabrique et builder en lecture seule de l'éditeur.
  - **Exécution** : `ShowSprite` vérifie tout avant de modifier (entité, composant racine, sprite au catalogue), puis charge le sprite, pose X et Y (Z gardé), couleur et mode, arrête mouvement et teinte (`StopMotionAndTint`) et rend l'entité visible. `HideSprite` rend l'entité invisible et arrête mouvement et teinte. Entité inconnue, racine qui n'est pas un `StaticSpriteComponent` ou sprite hors catalogue : `Failed`, message avec le champ et la valeur. Les deux actions sont instantanées.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5322 réussis, 0 échec (29 nouveaux tests dans `CasaEngine.Tests/Cutscenes/ShowHideSpriteCutsceneActionsTests.cs` : aller-retour, `color` en 3 et 4 entrées, refus d'un objet, de 2 ou 5 entrées, de 256, de -1 et d'un décimal, exécution, échecs, builder ; aucun test existant modifié).
  - **Écart** : aucun. Le chemin « sprite nommé chargé » à l'exécution n'est pas testé de bout en bout (il faut un jeu et un catalogue réels) ; seul le refus d'un sprite hors catalogue l'est.

### ✅ T2.2 — `MoveSprite` et `TintSprite`

- Objectif : C3 pour ces deux actions, et C6 et C7 pour elles. Les actions démarrent l'état de T1.2 et rendent la main à la même image.
- Validation : comme T2.1, avec les mêmes tests d'aller-retour et de refus de `color` pour `TintSprite`, plus un test qui joue une cutscene `MoveSprite`, puis `Wait`, puis une assertion de position, avec des pas de temps choisis.
- Commit : `feat(cutscenes): move and tint sprite entities from a cutscene`
- Note :
  - **Fait** : types `MoveSprite` et `TintSprite`, classes `MoveSpriteCutsceneActionData` (`EntityName`, `X`, `Y`, `Speed`, `FormatErrors`) et `TintSpriteCutsceneActionData` (`EntityName`, `Color`, `Duration`, `FormatErrors`), lecture et écriture JSON (`color` en tableau via `ReadColor` et `SaveColor`, nombres entiers ou décimaux lus en `float`), validateur (champs requis absents ou mal formés, vitesse non strictement positive, durée négative), fabrique et builder en lecture seule de l'éditeur.
  - **Exécution** : les deux actions vérifient l'entité et son composant racine, puis appellent `StartMove` et `StartTint` de T1.2 et laissent la séquence continuer à la même image. Entité inconnue ou racine qui n'est pas un `StaticSpriteComponent` : `Failed`, message avec l'action, le champ et la valeur.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5355 réussis, 0 échec (33 nouveaux tests dans `CasaEngine.Tests/Cutscenes/MoveTintSpriteCutsceneActionsTests.cs` : aller-retour, `color` en 3 et 4 entrées, refus d'un objet, de 2 ou 5 entrées, de 256, de -1 et d'un décimal, champs requis, vitesse et durée, échecs, builder ; cutscene `MoveSprite` puis `Wait` avec pas de 0,5 s : X vaut 5, 10 puis 15 aux images 1, 2 et 3, la cutscene se termine à l'image 3 ; teinte à mi-durée et à l'arrivée ; remplacement d'un déplacement ; aucun test existant modifié).
  - **Écart** : aucun. Les tests rejouent l'ordre d'une image (coroutines puis `Update` du composant) à la main plutôt que par `World.Update`.

### ✅ T2.3 — `Repeat`

- Objectif : C4, C6 et C7.
- Validation : tests de C9 sur `Repeat`, plus le refus d'un `count` absent et d'un `count` décimal ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(cutscenes): repeat a list of cutscene actions`
- Note :
  - **Fait** : type `Repeat`, classe `RepeatCutsceneActionData` (`Count`, `Actions`, `FormatErrors`), lecture et écriture JSON (`count` entier, `actions`), validateur (`count` absent, décimal, chaîne ou booléen refusé ; `count` négatif refusé ; liste vide en avertissement comme `Sequence`), fabrique et builder en lecture seule de l'éditeur. La lecture de `frames` passe maintenant par un `ReadInteger` commun, au message identique.
  - **Exécution** : chaque tour joue les enfants en séquence. `Repeat` pilote lui-même la pile des énumérateurs de chaque enfant : un énumérateur imbriqué est suivi, tout autre `yield` (image, instruction, handle) compte comme une image qui passe. Un tour sans fin (`count` 0) qui se termine sans image fait échouer la lecture (`Failed`, message « Repeat: … without a frame passing »), y compris une liste vide sans fin. Un `count` N avec une liste vide se termine tout de suite. Un enfant en échec ou une lecture remplacée termine le `Repeat`. L'arrêt pendant un tour libère les énumérateurs (`finally`) et plus rien ne s'exécute.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5378 réussis, 0 échec (23 nouveaux tests dans `CasaEngine.Tests/Cutscenes/RepeatCutsceneActionTests.cs` : aller-retour, refus de `count`, avertissement, N tours égaux à la séquence écrite N fois, ordre des enfants, tours instantanés, tour sans fin avec image imbriquée, tour sans image en échec, enfant en échec, arrêt pendant un tour, builder ; aucun test existant modifié).
  - **Écart** : aucun. Une pile et les énumérateurs sont alloués par enfant et par tour, hors des chemins chauds listés (pas dans `Update` du sprite ni dans la veille).

### ✅ T2.4 — `ExitOnInput`

- Objectif : C5, C6 et C7.
- Fichiers : les six endroits d'une action (« État vérifié »), `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs` (handle de la veille et son arrêt à chaque fin), et des tests.
- Étapes :
  1. Lire l'ordre d'une image entre l'`InputComponent`, les coroutines et l'UI, et le noter ici avec ses `fichier:ligne`.
  2. Implémenter la veille, puis mesurer par un test l'image de l'appui, celle de l'arrêt et celle du premier pas de la suite.
- Validation : tests de C9 sur la veille, et absence d'allocation dans son pas ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(cutscenes): leave a cutscene for another one on a named input`
- Note :
  - **Ordre d'une image (étape 1)** : dans `CasaEngineGame.Update`, la mise à jour de l'UI (`view.UIView?.Update`, `CasaEngineGame.cs:595`, dont `UINavigationInput`) passe d'abord, puis `world.UpdateWorldUI` (`:616`), puis `GameManager.UpdateWorld` (`:619`). Celui-ci appelle `World.Update` (`GameManager.cs:168`), qui met à jour les systèmes, coroutines comprises (`World.cs:618`, `WorldRuntimeSystems.cs:21`), puis les entités (`World.cs:635`). Les composants du jeu viennent ensuite par `base.Update` (`CasaEngineGame.cs:636`), dont l'`InputComponent` (`UpdateOrder` `Input`, `InputComponent.cs:40`, mise à jour des gestionnaires `:99-104`). La veille lit donc l'état des appareils de l'image précédente.
  - **Images mesurées (O3)**, par `ExitOnInputCutsceneActionTests` : appui lu par l'`InputComponent` à l'image P, arrêt de la cutscene (`Ended` à `Stopped`) à P+1, premier pas de la suite à P+2. L'armement a lieu à l'image U (action), le premier pas de la veille à U+1 (il n'enregistre que l'état).
  - **Fait** : type `ExitOnInput`, classe `ExitOnInputCutsceneActionData` (`Mapping`, `Inputs`, `NextCutscene`, `FormatErrors`), JSON (`mapping`, `inputs`, `next_cutscene`), validateur (champs requis, chaînes non vides, `inputs` liste de chaînes non vides, au moins une), builder en lecture seule. `CutsceneInputWatch` (coroutine du directeur) garde son propre `InputMappingManager` limité aux entrées listées, sans allocation par pas, et réagit à `Pressed && !PressedPreviousFrame`, la première mise à jour ne faisant qu'enregistrer. À l'exécution, la fabrique lit les appareils de l'`InputComponent`, copie le mapping (`LoadCopy<ButtonsMapping>`) et la cutscene de suite (`GameManager.FindCutsceneAsset`, nouveau, sur le modèle de `FindButtonsMappingAsset`) ; absence de jeu, mapping, entrée ou cutscene introuvables : `Failed` avec le champ et la valeur.
  - **Directeur** : `ArmInputWatch` remplace la veille en cours et ne fait rien sur une lecture remplacée ou en échec ; `RaiseEnded` arrête le handle de la veille avant de lever `Ended`, donc à chaque fin (`Completed`, `Failed`, `Stopped`, remplacement par `Play`). `Play` appelé depuis la veille est sûr (testé avec `ThrowCoroutineExceptionsInDebug`). Le point d'entrée interne `ExitOnInputResolver` du directeur permet aux tests sans jeu de fournir les appareils.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5406 réussis, 0 échec (28 nouveaux tests dans `CasaEngine.Tests/Cutscenes/ExitOnInputCutsceneActionTests.cs` : aller-retour, validation, builder, échecs, appui et images mesurées, maintien sans effet, touche tenue avant et pendant l'armement, appui sur l'image d'armement, remplacement de la veille, cutscene terminée puis appui, arrêt de la veille avant `Ended` pour les quatre fins, `Play` depuis la veille, absence d'allocation du pas ; aucun test existant modifié).
  - **Écart** : aucun. Le chemin réel (mapping et cutscene lus du catalogue d'un jeu) n'est pas testé de bout en bout, faute de jeu en test ; seul le refus sans jeu l'est. Il sera exercé par le sample (T3.1).

### ✅ T2.5 — Corrections des relectures

- Objectif : traiter les constats non bloquants laissés par les relectures de T1.1 à T2.4.
- Validation : les tests ajoutés passent ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert (aucun test présent à `e7a26748` n'est modifié).
- Commit : `fix(cutscenes): address the review findings of the chantier`
- Note :
  - **Relecture 1** : l'attribut `[MethodImpl(AggressiveInlining)]` est remis sur la surcharge `DrawSprite(..., SpriteEffects, Rectangle)` et la doc XML de la nouvelle surcharge est rattachée à celle-ci. Référence de ligne de T1.1 corrigée.
  - **Relecture 2** : `StartMove` lève `ArgumentOutOfRangeException` pour une cible non finie (4 cas de test). Libellé de la mesure d'allocation de T1.2 corrigé.
  - **Relecture 3, ShowSprite** : l'action vérifie que l'entrée de catalogue est un asset `sprite` (échec « is not a sprite asset »), et tout échec de chargement devient un `Failed` avec le champ et la valeur. `StaticSpriteComponent.LoadSpriteData` crée désormais le nouveau sprite avant de rendre l'ancien : un sprite qui ne se charge pas laisse l'ancien en place (vérifié par identité du sprite et de sa prise sur la feuille). Tests : asset catalogué qui n'est pas un sprite, fichier de sprite qui échoue au chargement, remplacement par un autre sprite.
  - **Relectures 3 et 4, nombres non finis** : `ReadNumber` refuse un nombre non fini (NaN, infini, littéral hors du `float` comme `1e39`) par une erreur de format « must be a finite number » qui nomme la clé ; le validateur la rapporte. Tests pour `x`, `y` de `ShowSprite`, `x`, `y`, `speed` de `MoveSprite` et `duration` de `TintSprite`, avec cinq littéraux chacun.
  - **Relecture 3, couleur** : le test à trois entrées sauve, relit, et vérifie quatre entrées et l'alpha 255.
  - **Relecture 5, ordre de `Repeat`** : le test enregistre l'état vu après chaque mise à jour (position et visibilité) et prouve l'ordre des enfants à chaque tour.
  - **Relecture 6, `ExitOnInput`** : chemin réel testé sans GPU par un faux jeu (objet non initialisé portant un `AssetContentManager` et un `InputComponent` non initialisé) et un catalogue de test : mapping inconnu, asset qui n'est pas un mapping, entrée absente du mapping, `next_cutscene` inconnue. Le chemin de succès (veille construite avec les gestionnaires d'un vrai `InputComponent`) reste exercé par le sample (T3.1).
  - **DEFER** : un `Repeat` fini avec un `count` énorme et des enfants seulement instantanés joue tous ses tours dans une seule mise à jour (seuls les tours sans fin sont gardés, comme l'exige C4). À documenter en T3.2.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur ; `dotnet test CasaEngine.Tests` : 5424 réussis, 0 échec (18 nouveaux tests, dont `CutsceneCataloguedAssetsTests` et `NonFiniteNumbersCutsceneActionTests`, et l'ordre de `Repeat` réécrit).
  - **Écart** : aucun.

---

## Phase 3 — Sample et documentation

### 🧪 T3.1 — Sample `Projects/SpriteCutsceneSample`

- Objectif : C8.
- Fichiers : `Projects/SpriteCutsceneSample/` et un test de données sur le modèle de `VideoSampleDataTests`.
- Validation :
  - le test de données passe ;
  - lancement au Launcher, avec des captures prises par un outil hors dépôt : sprites qui entrent et se teintent, mélanges additif et soustractif visibles, boucle ;
  - la position d'un sprite mesurée sur une capture confirme la convention de T1.1 ;
  - la sortie par la touche est vérifiée, sinon notée pour la recette (T4.2) ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(samples): add a sample that drives sprites from a cutscene`
- Note (état au 2026-11-09, reprise en session principale après deux tours de correction du workflow) :
  - **Contenu.** Projet sans DLL `Projects/SpriteCutsceneSample/`, en 320x180 `IntegerFit` :
    - `SpriteWorld.world` : une caméra 2D, une couleur d'effacement claire (150, 170, 210) qui sert de fond, et quatre entités sprite cachées au chargement, sans `DepthSortable2DComponent`. `orb-left` et `ring-right` nomment leur sprite dans le monde ; `orb-right` et `ring-left` le reçoivent de `ShowSprite` ;
    - `EndWorld.world` ;
    - le mapping `sample-input`, avec une entrée `exit` : Espace au clavier, Start à la manette ;
    - `loop.cutscene` : `ExitOnInput`, puis un `Repeat` sans fin. Chaque tour fait `ShowSprite` hors écran à mi-teinte, `MoveSprite` à 120 px/s, `TintSprite` vers la teinte pleine puis vers le noir, `HideSprite`, avec des `Wait` entre les étapes ;
    - `exit.cutscene` : un `FadeScreen` soustractif blanc, puis `ChangeWorld` vers le monde de fin ;
    - `end.cutscene` : une attente de 2 s, l'effacement de l'overlay par un `FadeScreen` noir soustractif de durée 0, puis le retour au premier monde.

    L'image `shapes.png` est générée par `SpriteCutsceneSampleImages` et contrôlée au pixel par `SpriteCutsceneSampleDataTests`.
  - **Corrections des relectures.**
    - Le fond opaque est remplacé par la couleur d'effacement : dessiné après les sprites, il échouait au test de profondeur sous eux.
    - L'overlay de sortie est effacé avant le retour au premier monde.
    - En session principale : les sprites apparaissaient en noir, donc invisibles en additif comme en soustractif pendant leur entrée. Ils apparaissent maintenant à mi-teinte.
    - Les deux commits de correction de la relecture 2 sont fusionnés en un seul (`819a6367`), car le premier ne compilait pas seul.
  - **Launcher (session principale).** Deux lancements sans aucune entrée envoyée par l'agent. Captures hors dépôt toutes les 500 ms. Le log ne contient ni avertissement ni erreur.
    - Les sprites entrent depuis les bords en ligne droite, à y constant (centre à -158, puis -118, puis -60 au repos), et s'arrêtent exactement sur leur cible (±60, ±35).
    - La convention de T1.1 est confirmée. Le centre mesuré d'`orb-left` est (300, 165) dans la capture en 960x540 (facteur 3), soit (-60, +35) en pixels du monde : Y vers le haut, caméra centrée sur l'origine, hotspot au centre du sprite.
    - Les mélanges sont visibles : les orbes additifs éclaircissent le fond, les anneaux soustractifs l'assombrissent de la teinte complémentaire. Chaque tour passe de la mi-teinte à la teinte pleine, puis au noir, puis cache les sprites. La boucle tourne, un tour dure environ 5,5 s.
    - La sortie (fondu vers le noir, monde de fin, retour au premier monde) s'enchaîne sans erreur.
    - **Sortie spontanée, expliquée.** Dans chaque lancement, la sortie s'est déclenchée une fois (vers 24 s, puis vers 5 s) sans touche envoyée par l'agent, puis le sample est revenu au premier monde. L'auteur jouait au même moment à un autre jeu avec une manette (« je suis en train de jouer a un jeu avec une manette ») : le bouton Start de sa manette a atteint la veille, car la manette est lue même quand la fenêtre n'a pas le focus. Ce n'est pas un défaut de `ExitOnInput` ; c'est un comportement antérieur du moteur, noté en O5.
  - **Validation.** Les deux `.sln` sont à 0 erreur. Dernier passage complet de `CasaEngine.Tests` : 5440 réussis. Après le changement de `loop.cutscene` et du README, `SpriteCutsceneSampleDataTests` passe 16 sur 16.
  - **Statut.** La tâche reste 🧪 : la sortie par la touche Espace est à vérifier à la recette (T4.2).

### ✅ T3.2 — Documentation

- Objectif : C10.
- Validation : relecture contre le code livré ; deux `.sln` à 0 erreur.
- Commit : `docs(cutscenes): describe the sprite, repeat and exit-on-input actions`
- Note :
  - **Documenté.** `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` gagne la section « Sprite, repeat and exit-on-input actions (ADR-0090) » (en anglais, dans un document par ailleurs en français) : champs, types et défauts des six actions, `color` en tableau, règle du temps, `Repeat` et sa garde, `ExitOnInput` (armement, durée de vie, ordre d'image mesuré en T2.4), échecs (validation, `Failed`, règles de T2.5) et limites O1, O2, O4, O5 et le `Repeat` fini à grand `count`. Aucune doc ne décrivait `StaticSpriteComponent` : nouveau `docs/engine/static-sprite-component.md` (champs JSON, résolution du nom, ordre de dessin, convention 2D mesurée en T3.1, mouvement et teinte), lié depuis `docs/README.md`. Un nouveau fichier plutôt qu'une section, car les docs de rendu 2D existantes sont en français.
  - **Relecture contre le code** (`git diff e7a26748..HEAD`) : fabrique, directeur, veille, sérialiseur, validateur, composant, `Entity`, `World.Update`. Les liens vers le sample et ADR-0090 sont posés.
  - **ADR-0090** relue contre le code livré : aucune erreur factuelle, aucune correction.
  - **Validation** : `dotnet build` des deux `.sln` à 0 erreur.

---

## Phase 4 — Vérification et recette

### ✅ T4.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur la branche : C1 à C10, D2 à D8, la non-régression, D7, les builds et les tests.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of the sprite cutscene actions`
- Note : passe du 2026-11-09 sur `da5c75af..8b595728` : **CONFIRMED**, aucun P0 à P2.
  - Lancé par le vérificateur :
    - les deux `.sln` à 0 erreur ;
    - `CasaEngine.Tests` : 5440 réussis, 0 échec, 0 ignoré ;
    - tous les fichiers de test de la branche sont nouveaux, donc aucun test existant n'est modifié.
  - C1 à C10 sont revérifiés dans le code. En particulier, les anciens appels de dessin restent les mêmes avec le mode par défaut, la veille s'arrête dans `RaiseEnded` à chaque fin, et l'ordre mesuré des images (appui en P, arrêt en P+1, premier pas de la suite en P+2) est égal à la doc.
  - D6, D7 et D8 tiennent. La seule occurrence de « PSX » est le nom d'API moteur existant et la citation de l'auteur en D3.
  - Le Launcher n'a pas été lancé. L'écran repose sur la note de T3.1, et la sortie au clavier est laissée à la recette.
  - Avis, tous P4 et DEFER :
    - A1, introduit : un tour de `Repeat` sans fin alloue une pile et les énumérateurs de ses enfants. C'est par tour, pas par image, et hors des chemins de D8. Déjà noté en T2.3.
    - A2, antérieur à la branche : `RemoveCollisions` ne vide pas `_collisionObjects`. Un `ShowSprite` avec `sprite` y passe plus souvent qu'avant, ce qui est sans effet sur un sprite sans collision. À traiter dans un chantier suivant si un sprite à collisions est rechargé.
    - A3, introduit, confiance faible : `is_visible` à faux s'écrit dans le monde. L'éditeur ne met `IsVisible` à faux que sur ses entités d'aperçu et de caméra, qui ne semblent pas sauvegardées dans le monde. Tous les chemins de sauvegarde n'ont pas été suivis.

### 🧪 T4.2 — Recette de l'auteur

- Objectif : l'auteur lance le sample et l'accepte.
- Validation : accord de l'auteur, écrit ici. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the accepted check of the sprite cutscene sample`
- Note : en attente de l'auteur.
  - À voir : les sprites qui entrent depuis les bords, se teintent puis s'éteignent, les mélanges additif et soustractif, et la boucle.
  - À vérifier : Espace (ou Start à la manette) lance le fondu, le monde de fin, puis le retour. Sans appui, rien ne sort. Pendant la recette, aucune autre application ne doit utiliser une manette (O5).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | La semi-transparence par texel d'ADR-0051 (texels opaques et texels mélangés dans un même sprite) n'est pas reliée au mode choisi par une action : le mode s'applique à tout le sprite. Si le projet consommateur en a besoin, ce sera une décision et un chantier suivants | Après T4.2 |
| O2 | Un mouvement ou une teinte en cours continue sur l'entité après l'arrêt de la cutscene, puisque l'état est sur le composant. Si un consommateur veut les figer, ce sera une décision suivante | T1.2, T3.2 |
| O3 | Le délai entre un appui et l'arrêt dépend de l'ordre d'une image (`InputComponent`, coroutines). T2.4 le mesure et le documente, sans le changer | T2.4 |
| O4 | Le même appui peut atteindre la navigation de l'UI, par exemple le saut d'une vidéo. Documenté, sans changement | T2.4, T3.2 |
| O5 | La manette est lue même quand la fenêtre n'a pas le focus : un appui sur Start dans une autre application déclenche la veille d'`ExitOnInput` (constaté au Launcher en T3.1). Comportement antérieur du moteur, hors de ce chantier : ignorer l'entrée sans focus serait une décision de l'auteur | T3.1, T3.2 |

## Hors périmètre

- Animation d'images d'un sprite, rotation, échelle, sprites en plusieurs parties.
- Variantes comptées en images des nouvelles actions, et tout changement des actions existantes (`Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames`).
- Son, et toute attente liée à un lecteur de disque.
- L'édition des nouvelles actions dans l'éditeur (seul le builder en lecture seule est fait).
