# Plan agent IA — Migrer vers les handles comptés et supprimer Load, AddAsset, GetAsset et les catégories

Plan d'exécution de l'[ADR-0037](../../docs/decisions/0037-counted-handles-replace-load-and-categories.md),
suite de l'[ADR-0036](../../docs/decisions/0036-reference-counted-asset-handles-and-bitmap-fonts.md).
Les décisions D1 → D9 ci-dessous ont été arbitrées avec l'auteur le 2026-09-21 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Chaque ressource partagée est tenue par un handle compté (`Acquire<T>`). Son détenteur la rend quand il
disparaît : un composant dans `Detach`, le monde dans `Clear`, un service à sa propre fin, un panneau de
l'éditeur dans `Dispose`. Une ressource que plus personne ne tient est libérée au changement de monde
suivant.

Trois méthodes nouvelles couvrent les autres usages :
- **`LoadCopy<T>(id)`** donne une copie neuve pour les modèles (entités, mondes, cinématiques, matériaux en
  édition, relectures du rechargement à chaud).
- **`Register<T>(id, objet)`** range un objet fabriqué en cours de route.
- **`Replace<T>(id, objet)`** remplace l'instance partagée d'un id (rechargement à chaud, sauvegarde de
  l'éditeur).

Une fois que plus rien ne les appelle, `Load<T>`, `AddAsset`, `GetAsset`, `GetAssets`, `Unload`,
`UnloadAll`, `LoadDirectly` et les catégories sont supprimés, sans étape `[Obsolete]`.

Le chantier migre tous les appelants : le moteur, l'éditeur, les démos, les tests, et la DLL Alundra du dépôt
parent (plan parent `docs/plan-migration-handles.md`).

## État vérifié du dépôt (2026-09-21)

**Git**
- La branche **`chantier/asset-handles-migration`** a été créée depuis `chantier/asset-handles`
  (`dceec487`), **non encore mergée dans `main`** : c'est une branche empilée. Elle porte l'ADR-0037
  (`8c78ab07`).
- Modifications préexistantes de l'auteur, jamais indexées : `CasaEngine.Launcher/Program.cs` (à ne jamais
  ouvrir) et `Projects/SampleProject/.casaeditor/viewport.editor.json`.

**Relevé des appels** (en lecture seule, 2026-09-21, puis comparaison mécanique à `rg`)
- `CasaEngine/` : environ 40 appels au gestionnaire de ressources.
- `CasaEngine.Editor/` : 12, et `CasaEngine.EditorServices/` : 1.
- `CasaEngine.Demos/` : 7 `Load<T>`, plus des `LoadFromFile` qui restent.
- DLL Alundra : 7.
- `CasaEngine.Tests` : 33 correspondances dans 14 fichiers, dont une partie ne concerne que des homonymes
  (`ScriptAssemblyHost.Unload`, `ElementFactory`).
- Les `Game.Content.Load<Effect>` et `Content.Load<Texture2D>` des composants de rendu, de l'éditeur et du
  backend UI passent par le `ContentManager` de MonoGame : **hors périmètre**.

**Détenteurs et crochets**
- Les composants chargent dans `InitializeWithWorld` et libèrent dans `Detach`, par exemple :
  - `TileMapComponent.cs:236` et `:1074-1084` (données de carte, tilesets, planches), `Detach` `:1028` ;
  - `AnimatedSpriteComponent.cs:169,412`, `Detach` `:698` ;
  - `SoundEmitterComponent.cs:217`, `Detach` `:172`.
- Quand le monde se vide, `World.ClearEntities` → `DetachComponentsOfDiscardedEntity` (`World.cs:195-212`)
  ne détache que `entity.AttachedComponents`, c'est-à-dire la liste `_components` (`Entity.cs:630`).
  **Trois choses lui échappent :**
  - **le composant racine**, rangé à part dans `_rootComponent` (`Entity.cs:66-86`) ;
  - **l'arbre de composants de scène sous la racine**, alors que `SceneComponent.Detach` cascade sur ses
    enfants (`SceneComponent.cs:318-327`) ;
  - **les entités enfants** (`Entity.cs:264-269`, initialisées avec le monde en `World.cs:1027-1031`).
- Dans Alundra, la carte de tuiles est justement le composant racine d'une entité sans autre composant
  (`alundra-casaengine-project-converter/Writers/WorldWriter.cs:390-400`). Son `Detach` ne court donc
  jamais.
- Le test de démontage existant n'observe qu'un composant non racine
  (`CasaEngine.Tests/Application/WorldEntityTeardownTests.cs:38-69`).
- Une entité retirée en cours de partie (`Entity.Destroy` → `ToBeRemoved`, `World.cs:480-512`) quitte le
  monde **sans aucun détachement**.
- **`IAssetable` déclare son propre `Dispose()` mais n'hérite pas d'`IDisposable`** (`IAssetable.cs:5-9`).
  Or la collecte ne libère que les `IDisposable` (`AssetContentManager.cs:201`). `Texture` et `RiggedModel`
  sont des `IAssetable` (`Texture.cs:8`, `RiggedModel.cs:50`) ; `StaticModel` et `SkinnedMesh` ne sont ni
  l'un ni l'autre (`StaticModel.cs:16`, `SkinnedMesh.cs:8`).
- **Les environnements sont résolus pendant le rendu** : `RenderPipeline.cs:166` passe par
  `EnvironmentResolver`, qui s'appuie sur `ResolvedEnvironmentCache` (`EnvironmentResolver.cs:18-58`,
  `ResolvedEnvironmentCache.cs:3-48`). Ni ce cache ni `RenderView` n'ont de point de libération, et
  `ViewManager.Clear` ne libère rien de l'environnement (`ViewManager.cs:224-236`).
- Les générateurs d'environnement reconstruisent une cubemap **sous le même id** quand la précédente
  `IsDisposed` (`PanoramaEnvironmentGenerator.cs:39-63`, et les deux autres).

**Modèles copiés**
- `World.SpawnEntity` (`World.cs:144,151`), `EntityReference` (`:54,71`) et `Load<Entity>(JObject)`
  (`EntityReference.cs:50`, seul appelant de `Load<T>(JObject)`).
- `GameManager.UpdateWorld` (`Load<World>`, `cache: false`).
- Les lectures `cache: false` des cinématiques (démos), des matériaux (`MaterialAuthoringAssetCache.cs:29`,
  `MaterialCompiler.cs:109`, `MaterialRuntimeResolver.cs:31`, `StaticModelMaterialOverrideResolver.cs:83`)
  et du rechargement à chaud (`CasaEngineGame.cs:923,934`).

**Objets rangés par `AddAsset`**
- La texture par défaut : `CasaEngineGame.cs:462-467`, sous le nom `Texture.DefaultTextureName`, relue par
  nom dans `ArrowComponent.cs:78-79`.
- Les cubemaps générées : `PhysicalAtmosphere`, `Panorama` et `ProceduralSkyEnvironmentGenerator`,
  `GetAsset` puis `AddAsset`.
- Le rechargement à chaud : `CasaEngineGame.cs:945-966`.
- L'éditeur : `SpriteAssetInspectorPanel.cs:525,529` et `SpriteSceneThumbnailRenderer.cs:289,293`.
- `SpriteLoader` (`Assets/Sprites/SpriteLoader.cs`), **qui n'a aucun appelant**.

**Recherches par nom**
- `StaticSpriteComponent.LoadSpriteData(string)` (`:148-150`), exposée par `TryLoadSpriteData(string)`
  (`:223-230`).
- `ArrowComponent` (texture par défaut).

**Dépendances**
- `Sprite.Create` partage la `Texture` en cache (`Load<Texture>`, `Sprite.cs:16-21`) puis appelle
  `Texture.Load`, qui lit la `Texture2D` (`Texture.cs:62-67`).
- `Texture.Dispose` libère la `Texture2D` elle-même, pourtant partagée (`Texture.cs:69-80`).
- `AnimationClipLoader.cs:26` et `RetargetProfileLoader.cs:31-32` chargent un `SkeletonDefinition`.

**Catégories et tests**
- Aucun appelant n'utilise une autre catégorie que `default`, et `Unload`/`UnloadAll` n'ont aucun appelant
  hors du gestionnaire.
- Référence de la branche : `CasaEngine.Tests` 1738/1739, le seul échec étant
  `EditorThemeAsset_Disables_Docking_Accent_Bars`, préexistant et hors périmètre ; `MGUI.Tests` 2989/2989.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Une seule instance par ressource, tenue par handle ; les catégories disparaissent (ADR-0036 D1, ADR-0037). |
| D2 | `LoadCopy<T>(id)` pour les modèles : objet neuf lu depuis le fichier, hors cache et hors compteurs. |
| D3 | `Register<T>(id, objet)` → handle pour les objets fabriqués ; `Replace<T>(id, objet)` pour le rechargement à chaud et l'éditeur. |
| D4 | Le monde détache aussi les composants d'une entité retirée en cours de partie. Détacher une entité jetée veut dire détacher tout son arbre : composant racine (cascade sur les composants de scène enfants), autres composants, entités enfants. C'est vrai qu'elle soit jetée par `Clear`/`ClearEntities` ou par le retrait en cours de partie (précision apportée à la relecture du plan, voir T1.2). |
| D5 | Suppression de `Load<T>`, `AddAsset`, `GetAsset`, `GetAssets`, `Unload`, `UnloadAll`, `LoadDirectly` et des catégories, **sans `[Obsolete]`**. |
| D6 | Un seul chantier, tranché par zone ; la suppression vient en dernier, quand plus rien n'appelle ces membres. |
| D7 | `LoadFromFile<T>` reste (fichiers hors catalogue, outils et démos). |
| D8 | La libération différée de l'ADR-0036 est inchangée : collecte au début de chaque changement de monde, plus l'appel explicite. |
| D9 | Pas de démo moteur dédiée. Les démos touchées sont relancées, et la recette visible reste le portage Alundra. |

**Points à valider par l'auteur** (arbitrages proposés par l'agent) :

| Réf | Proposition |
|---|---|
| P1 | Un `Texture` du moteur chargé par `Texture.Load` tient sa `Texture2D` par handle, et `Dispose` rend ce handle au lieu de libérer la texture GPU partagée (remarque A4 de la clôture de l'ADR-0036). Un `Texture` construit sur une `Texture2D` brute (la texture par défaut) la possède toujours et la libère toujours. Un `Sprite` tient sa `Texture` : `Sprite` devient `IDisposable`. |
| P10 | La collecte libère aussi les `IAssetable` (`Texture`, `RiggedModel`), qui ont leur `Dispose()` sans être `IDisposable`. `StaticModel` et `SkinnedMesh`, ressources partagées qui prendront des handles sur leurs dépendances, deviennent `IDisposable` et les rendent dans `Dispose` ; la collecte les appelle quand elle les libère. |
| P11 | Les ressources d'environnement (`EnvironmentAssetLookup`) sont prises au premier défaut du cache de résolution, jamais par image, et tenues pour toute la partie par un détenteur de niveau jeu : un dictionnaire par id dans `EnvironmentAssetLookup`, attaché au gestionnaire. Les cubemaps générées sont tenues pour toute la partie par leur générateur, et une reconstruction sous le même id passe par `Replace`. La libération par carte des environnements est une suite. |
| P2 | Les clips audio restent tenus par le fournisseur de clips du service audio pendant toute la partie, comme aujourd'hui. La libération par carte de l'audio est une suite. |
| P3 | Les textures et cubemaps d'un matériau compilé sont tenues par l'entrée du cache de matériaux, et rendues quand l'entrée est remplacée ou le cache vidé. |
| P4 | `EntityReference` désérialise une entité en ligne directement (`new Entity()` puis `Load(noeud)`), sans passer par le gestionnaire. `Load<T>(JObject)` disparaît. |
| P5 | `SpriteLoader`, sans appelant, est supprimé. |
| P6 | Une recherche par nom devient une recherche dans le catalogue, puis un `Acquire` : `TryLoadSpriteData(string)` garde sa signature. La texture par défaut devient `CasaEngineGame.DefaultTexture` (`Register`, tenue par le jeu). |
| P7 | `GameManager` journalise, au niveau Info, le nombre de ressources libérées à chaque changement de monde. C'est une trace utile, et c'est la preuve de la recette. |
| P8 | Les générateurs d'environnement gardent leurs cubemaps générées comme handles `Register` dans leur propre dictionnaire, au lieu de `GetAsset`. Une cubemap reconstruite sous le même id est remise par `Replace`, en gardant la prise du générateur (P11). |
| P9 | `AssetDictionary` perd son index par nom ; le cache ne connaît plus que les ids. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/asset-handles-migration`**, empilée sur `chantier/asset-handles`. Elle se
  merge après elle. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin,
  lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de
  validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier. La
  tâche T7.1 se commite dans le dépôt parent, selon son plan.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le
  message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une
  réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire
  la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché :
  `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`.
- **Tests** : `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build`, toujours au premier plan. Un échec autre
  que l'échec préexistant est une régression.
- **Au-delà du build, pour chaque site migré :**
  - le détenteur doit rendre ce qu'il prend ;
  - un test couvre ce rendu quand le détenteur est constructible sans périphérique graphique ;
  - sinon, la raison est écrite sous la tâche.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et
  noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais
  `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur :
  - `Acquire`, `LoadCopy`, `Register` et la collecte ne sont appelés qu'au chargement et au changement de
    monde, jamais par image ;
  - aucun handle ne se prend dans un chemin chaud ;
  - le runtime ne dépend pas de l'éditeur.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- **Builds :** les deux solutions, ainsi que `Alundra/Alundra.csproj` du dépôt parent, compilent sans erreur.
- **Tests :** `CasaEngine.Tests` et `MGUI.Tests` au moins à la référence, plus les tests ajoutés ;
  `Alundra.Tests` du dépôt parent vert.
- **Suppression :** après T8.1,
  `rg "\.Load<|AddAsset\(|GetAsset<|GetAssets<|DefaultCategory|UnloadAll\(" CasaEngine CasaEngine.Editor CasaEngine.EditorServices CasaEngine.Demos CasaEngine.Tests`
  ne rend plus aucun appel au gestionnaire de ressources. Les `Content.Load` de MonoGame restent.
- **Démos :** les démos touchées sont relancées au moins une fois, avec `CasaEngine.Demos/` comme dossier
  courant (AGENTS.md §6).
- **Recette visible** (tranche du plan parent) : parcours 389 → 390 → 389 avec le harnais Alundra.
  - L'inventaire est en `font3` et les icônes du HUD sont affichées sur chaque carte.
  - `font3` et les sprites tenus par les écrans ne sont chargés qu'une fois.
  - Une planche de tuiles propre à la 389 (son `.texture` et son `.png`) est libérée au changement
    390 → 389 (trace P7 : nombre libéré > 0), puis rechargée au retour sur la 389. Cela prouve à la fois
    le détachement de la carte de tuiles, qui est un composant racine (T1.2), et la libération des
    `Texture` (P10).
- **Éditeur :** vérification manuelle par l'auteur des panneaux touchés (tuiles, inspecteurs d'animation 2D
  et de sprite, aperçu des clips, vignettes). Sans elle, les tâches de l'éditeur restent 🧪.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0037

- Objectif : consigner les décisions de l'auteur et marquer la clause de compatibilité de l'ADR-0036 comme
  remplacée.
- Fichiers : `docs/decisions/0037-counted-handles-replace-load-and-categories.md`,
  `docs/decisions/0036-…` (statut), `docs/decisions/README.md`.
- Validation : relu contre les réponses de l'auteur.
- Commit : `docs(adr): counted handles replace Load and asset categories` (`8c78ab07`).

---

## Phase 1 — API et crochet de destruction

### ✅ T1.1 — `LoadCopy<T>`, `Register<T>`, `Replace<T>`

- Objectif : les trois entrées nouvelles de l'ADR-0037, ajoutées à côté de l'existant.
- Fichiers : `CasaEngine/Framework/Assets/AssetContentManager.cs`,
  `CasaEngine.Tests/Assets/AssetContentManagerHandleTests.cs`.
- Définitions de la tâche :
  - **Présent :** un id est « présent » quand il est dans la catégorie par défaut, avec ou sans bail.
  - **Épinglé :** une entrée sans bail, rangée par `Load<T>` ou `AddAsset`, est épinglée, comme depuis
    l'ADR-0036.
- Étapes :
  1. **`LoadCopy<T>(Guid id)`** : chemin de lecture de `LoadNew`, sans cache ni bail.
  2. **`Register<T>(Guid id, T asset)`** : lève `InvalidOperationException` si l'id est présent (un appelant
     qui veut remplacer une instance utilise `Replace`). Sinon, il range l'objet avec un bail non épinglé et
     rend un handle (un détenteur).
  3. **`Replace<T>(Guid id, T asset)`** :
     - **id présent :** remplace l'instance partagée. Le bail éventuel est gardé tel quel (détenteurs et
       épinglage), et une entrée sans bail reste sans bail, donc épinglée. L'ancienne instance n'est pas
       libérée par le gestionnaire : ses détenteurs la gardent jusqu'à leur propre rafraîchissement, comme
       le fait déjà le code de rechargement à chaud.
     - **id absent :** range l'objet avec un bail non épinglé, sans détenteur, donc en attente.
  4. **Règle de nom**, pour les deux méthodes :
     - le nom rangé dans l'index par nom est le `Name` de l'`AssetInfo` du catalogue quand l'id s'y
       résout (`ResolveAssetInfo`) ;
     - sinon, aucune entrée par nom n'est créée, et le nom du bail reste nul. `AssetDictionary` gagne un
       ajout sans nom, et `RemoveById` gère déjà un nom nul ;
     - pour `Replace` sur un id présent, l'entrée par nom qui désignait l'ancienne instance désigne
       désormais la nouvelle. L'index par nom disparaît à T8.1 (P9).
  5. **P10 :** `CollectUnreferenced` libère aussi une ressource qui est `IAssetable` sans être
     `IDisposable`. Une ressource qui est les deux n'est libérée qu'une fois.
  6. Doc XML.
- Tests, sur l'API publique seule (`Acquire`, `CollectUnreferenced`, `Load`, les compteurs `Loads` et
  `DisposeCount` des ressources de test) :
  - une ressource de test `IAssetable`, non `IDisposable`, rendue puis collectée : `Dispose()` est appelé
    une fois ;
  - une ressource à la fois `IAssetable` et `IDisposable` n'est libérée qu'une fois ;
  - `LoadCopy` appelé deux fois rend deux objets distincts, le chargeur est appelé deux fois, et
    `CollectUnreferenced` rend 0 (rien en cache) ;
  - `Register` sur un id absent du catalogue ne lève pas ; le handle rendu puis collecté donne
    `CollectUnreferenced` = 1 et `Dispose` appelé une fois ;
  - `Register` sur un id présent lève, qu'il soit tenu par `Acquire` ou chargé par `Load` ;
  - **`Replace` sur un id tenu :** deux handles pris avant, `Replace`, puis un nouvel `Acquire` rend la
    nouvelle instance sans appeler le chargeur. On rend un des deux anciens handles puis le nouveau :
    `CollectUnreferenced` = 0. On rend le second : `CollectUnreferenced` = 1, et c'est la nouvelle instance
    qui est libérée ;
  - **`Replace` sur un id chargé par `Load` :** `Load`, `Replace`, puis `CollectUnreferenced` = 0, la
    nouvelle instance n'est pas libérée, et `Acquire` rend la nouvelle instance ;
  - **`Replace` sur un id absent :** un `Acquire` suivant rend l'instance remplacée, sans appel au chargeur.
    Sans `Acquire`, `CollectUnreferenced` = 1 et `Dispose` est appelé une fois.
- Validation : builds ; `CasaEngine.Tests` référence + nouveaux.
- Commit : `feat(assets): LoadCopy, Register and Replace for templates and objects made at run time`
- **Fait** :
  - 10 tests, dont les deux cas de libération des `IAssetable` et les trois cas de `Replace` ;
  - `CasaEngine.Tests` 1748/1749, avec le seul échec préexistant ; deux solutions sans erreur ;
  - `AssetDictionary` gagne `AddWithoutName` et `ReplaceInstance`, qui repointe aussi les noms désignant
    l'ancienne instance.

### ✅ T1.2 — Détacher tout l'arbre d'une entité jetée

- Objectif : D4. Le crochet de libération dont dépendent les phases 3 à 7 doit atteindre **tous** les
  composants d'une entité jetée.
- Fichiers : `CasaEngine/Framework/Scene/World/World.cs`,
  `CasaEngine.Tests/Application/WorldEntityTeardownTests.cs`.
- Étapes :
  1. `DetachComponentsOfDiscardedEntity` détache :
     - le composant racine, dont le `Detach` cascade déjà sur les composants de scène enfants ;
     - puis les autres composants (instantané de la liste, comme aujourd'hui) ;
     - puis, récursivement, les entités enfants.
     Chaque composant est détaché une seule fois.
  2. Deux chemins l'appellent :
     - `ClearEntities` (et donc `Clear`), comme aujourd'hui ;
     - le retrait des entités `ToBeRemoved` dans `World.Update` (`World.cs:506-512`), qui ne l'appelle pas
       aujourd'hui.
  3. Mettre à jour le commentaire de la méthode, qui ne parle que de deux appelants et des seuls composants
     attachés.
- Tests, sur le modèle de `WorldEntityTeardownTests` : un `SceneComponent` de test racine et un composant
  sur une entité enfant comptent chacun leurs `Detach`. Chacun en compte **exactement un** dans les trois
  cas suivants :
  - après `World.Clear()` ;
  - après `ClearEntities()` ;
  - après `Destroy()` suivi de `World.Update`.
  Le test existant du composant non racine reste vert.
- Validation : builds ; tests.
- Commit : `fix(world): detach the whole component tree of a discarded entity`
- **Fait** :
  - un composant n'est détaché que s'il a encore un propriétaire. Les composants de scène enfants ne vivent
    que sous la racine (`SceneComponent.AddChildComponent`), et les entités enfants ne sont pas dans la liste
    du monde (`InternalAddEntities`) : rien n'est donc détaché deux fois ;
  - au retrait en cours de partie, le détachement suit `NotifyEntityRemovedRecursive`, pour que les
    auditeurs voient encore l'entité intacte ;
  - trois tests : `Clear`, `ClearEntities`, et `Destroy` puis `Update` (qui passe sans périphérique) ;
  - contre-épreuve : sans le détachement de la racine et celui du retrait, les trois échouent ;
  - `CasaEngine.Tests` 1751/1752 ; deux solutions sans erreur.

---

## Phase 2 — Dépendances

### ✅ T2.1 — `Texture`, `Sprite`, chargeurs de squelette, `SpriteLoader`

- Objectif : P1, P5.
- Fichiers : `Assets/Textures/Texture.cs`, `Assets/Sprites/Sprite.cs`, `Assets/Sprites/SpriteLoader.cs`
  (supprimé), `Assets/Loaders/AnimationClipLoader.cs`, `Assets/Loaders/RetargetProfileLoader.cs`, tests.
- Étapes :
  1. **`Texture.Load`** prend un handle `Acquire<Texture2D>` s'il n'en a pas déjà, et `OnDeviceReset` le
     reprend si nécessaire. `Dispose` :
     - rend ce handle s'il existe, sans libérer la `Texture2D` partagée ;
     - libère sinon la `Texture2D` que le `Texture` possède, comme la texture par défaut construite sur une
       `Texture2D` brute (P1).
     La collecte appelle ce `Dispose` au titre d'`IAssetable` (P10, T1.1).
  2. **`Sprite.Create`** prend un handle `Acquire<Texture>`. `Sprite` devient `IDisposable` et le rend.
  3. **Chargeurs de squelette :** le `SkeletonDefinition` est pris par `Acquire`. Si l'objet produit le garde,
     il garde aussi le handle et devient `IDisposable` ; sinon, le handle est rendu après la conversion.
     Vérifier dans le code lequel des deux cas s'applique, et l'écrire.
  4. Supprimer `SpriteLoader`.
- Tests :
  - `Sprite.Dispose` rend sa texture (le chargeur de test rend une `Texture` sans GPU, si c'est
    constructible ; sinon, la raison est écrite) ;
  - le cas des chargeurs de squelette, avec les tests d'animation existants adaptés.
  - Une `Texture2D` ne se construit pas sans périphérique graphique : le test « collecter un `Texture` rendu
    rend sa `Texture2D` dans le même appel » n'est pas écrivable ici, et la raison est écrite sous la tâche.
    La cascade elle-même est prouvée à deux endroits :
    - par les tests de dépendances de T1.1 et de l'ADR-0036 ;
    - par la recette M2 du plan parent : le `.texture` et le `.png` d'une planche propre à la 389 sont
      rechargés au retour.
- Validation : builds ; tests.
- Commit : `refactor(assets): textures and sprites hold what they use through handles`
- **Fait** :
  - **`Texture.Load`** prend sa `Texture2D` une seule fois, même rappelé : chaque sprite d'une planche le
    rappelle sur la même `Texture` partagée. `Dispose` rend ce handle, ou libère une `Texture2D` possédée.
  - **Perte du périphérique graphique :** la `Texture2D` libérée est relue par `LoadCopy` puis `Replace`, au
    lieu d'être reprise telle quelle dans le cache.
  - **`Sprite`** tient sa `Texture` et est `IDisposable`. Tous ses créateurs gardent leurs sprites en cache
    (vérifié : aucun appel par image). Le remplacement de sprite d'`AnimatedSpriteComponent` (ligne 281,
    rechargement à chaud) ne libère pas l'ancien : c'est à traiter en T3.1.
  - **Chargeurs de squelette :** `AnimationClip` et `RetargetProfile` **gardent** leurs squelettes
    (`AnimationClip.Skeleton`, `RetargetJointMapping`). Ils les tiennent donc par handle et sont `IDisposable`.
  - **`SpriteLoader`** est supprimé.
  - **Tests :** les tests des deux chargeurs vérifient qu'un clip libéré laisse son squelette à qui le tient
    encore, et qu'un profil libéré entraîne ses deux squelettes dans la même collecte (3). Le test de
    `Sprite` n'est pas écrivable : `Sprite.Create` résout la `Texture2D`, qui exige un périphérique
    graphique. Il est couvert par la recette M2.
  - `CasaEngine.Tests` 1751/1752 ; deux solutions et la DLL Alundra compilent.

---

## Phase 3 — Composants et monde

### 🧪 T3.1 — Composants 2D et son

- Objectif : les composants prennent leurs ressources par `Acquire` et les rendent dans `Detach`.
- Fichiers :
  - `TileMapComponent` : modèle `TileMapData` → handle gardé tant que le composant vit, puis
    `CreateWorldWorkingCopy` ; tilesets ; planches ;
  - `StaticSpriteComponent`, y compris `TryLoadSpriteData(string)` par le catalogue (P6), et le `Sprite`
    qu'il crée ;
  - `AnimatedSpriteComponent`, `ParticleSystemComponent`, `SoundEmitterComponent`. Un `Sprite` remplacé
    (rechargement à chaud, `AnimatedSpriteComponent.cs:281`) ou retiré est `Dispose`é ; tous les sprites du
    composant le sont dans `Detach` ;
  - leurs tests.
- Validation : builds ; tests (un composant de test rendu au `Detach` rend ses handles) ; la carte 389
  d'Alundra ne change pas d'aspect, vérifié dans la recette du plan parent.
- Commit : `refactor(components): 2D and sound components hold their assets through handles`
- **Fait** :
  - **`TileMapComponent`** : `_tileMapDataHandle` (le modèle, tenu pour toute la vie du composant même
    si seule sa copie de travail — `CreateWorldWorkingCopy()` — est utilisée, comme demandé), et un
    `AssetHandle<TileSetData>`/`AssetHandle<Texture>` par tileset (`_tileSetHandles`,
    `_tileSetTextureHandles`). `ReleaseTileMapAssetHandles()` les rend, appelée en tête d'
    `InitializeWithWorld` (ré-entrance sans `Detach`, comme le fait déjà `DisposeChunkGraphicsResources`)
    et dans `Detach`.
  - **`StaticSpriteComponent`** : `_spriteDataHandle` (le `SpriteData`, par id direct ou via
    `TryLoadSpriteData(string)` → `AssetCatalog.Get(name)` puis `Acquire`, P6) ; le `Sprite` créé est
    `Dispose`é avant tout remplacement (`LoadSpriteData`, `ReloadSpriteAsset`) et dans un nouveau `Detach`.
    `ReloadSpriteAsset` reçoit son `SpriteData` déjà résolu par l'appelant (rechargement à chaud) : le
    handle propre au composant est rendu, pas celui de l'appelant.
  - **`AnimatedSpriteComponent`** : un `AssetHandle<Animation2dData>` par id d'animation
    (`_animationDataHandles`), et un `AssetHandle<SpriteData>` par sprite résolu (`_spriteDataHandleById`),
    à côté du dictionnaire de `Sprite` existant. `ReleaseSpriteHandles`/`ReleaseAnimationDataHandles`
    rendent tout, appelées en tête d'`InitializeWithWorld` (couvre le cas déjà testé d'une
    ré-initialisation sans `Detach`) et dans `Detach`. `ReloadSpriteAsset` (rechargement à chaud) dispose
    l'ancien `Sprite` et rend l'ancien handle de `SpriteData` avant d'utiliser l'instance fournie par
    l'appelant — le défaut relevé à la clôture de T2.1.
  - **`ParticleSystemComponent`** : `_particleEffectAssetHandle`, rendu par `ReleaseParticleEffectAssetHandle`
    dans `Detach`, avant tout rechargement par id, et quand l'appelant fournit directement un
    `ParticleEffectAsset` (`SetParticleEffectAsset`, `ClearParticleEffectAsset`) — cette instance n'est
    alors plus tenue par un handle du composant.
  - **`SoundEmitterComponent`** : `_soundAssetHandle`, rendu par `ReleaseSoundAssetHandle` avant tout
    rechargement et dans `Detach`.
  - **Tests** : 9 nouveaux, tous vérifient le rendu du handle via `AssetContentManager.CollectUnreferenced()`
    d'un gestionnaire construit localement (chargeurs factices, aucune dépendance globale) :
    - `AnimatedSpriteWorldInitializationTests` (+3) : acquisition par id (pas `Load<T>`), tenue jusqu'à
      `Detach`, remise à zéro sur une seconde `InitializeWithWorld`, et rendu des sprites/handles au
      `Detach` ;
    - `StaticSpriteComponentAssetHandleTests` (+1, nouveau fichier) ;
    - `ParticleSystemComponentAssetHandleTests` (+3, nouveau fichier) ;
    - `SoundEmitterComponentAssetHandleTests` (+2, nouveau fichier) ;
    - `TileMapWorldWorkingCopyTests.TheTileMapComponentTakesItsOwnWorkingCopy` (garde de source existante)
      adapté : il vérifie désormais `Acquire<TileMapData>(TileMapDataAssetId)` au lieu de l'ancien `Load<T>`.
  - **Non testé, raison écrite ici (comme pour `Sprite` à la clôture de T2.1)** : le round-trip complet
    `StaticSpriteComponent`/`AnimatedSpriteComponent` → `Sprite.Create` → `Texture.Load` → `Texture2D`, et
    la chaîne tileset → texture de `TileMapComponent.LoadTileSets`, exigent un `GraphicsDevice`
    indisponible en headless. Le rendu du `Sprite` lui-même (son handle de texture) est couvert
    indirectement : `StaticSpriteComponentAssetHandleTests` et le nouveau test `Detach` d'
    `AnimatedSpriteWorldInitializationTests` construisent un `Sprite` réel par le même contournement sans
    périphérique que `SpriteRendererComponentBlendModeTests` (`RuntimeHelpers.GetUninitializedObject` +
    écriture directe de `_textureHold`), avec un vrai handle `AssetContentManager.Acquire<Texture>`, et
    vérifient que `Detach` le libère. La chaîne complète reste couverte par la recette M2 du plan parent
    (planche `.texture`/`.png` de la 389 libérée puis rechargée).
  - `CasaEngine.Tests` 1760/1761, seul échec l'échec préexistant du docking ; les deux solutions et
    `Alundra/Alundra.csproj` compilent sans erreur.
  - **🧪 Needs testing** : vérification manuelle en jeu par l'auteur restant (parcours 389 → 390 → 389,
    voir « Validation globale » du plan) — aucune démo ni panneau d'éditeur n'a été relancé pour cette
    tâche.

### 🧪 T3.2 — Composants 3D

- Fichiers et détenteurs :
  - **`StaticModelComponent`** tient son `StaticModel` par handle, rendu dans `Detach`. Le `StaticModel`,
    devenu `IDisposable` (P10), tient les handles de texture de ses `StaticModelMesh` et les rend dans son
    `Dispose`, que la collecte appelle quand plus personne ne le tient.
  - **`SkinnedMeshComponent`** tient son `SkinnedMesh` par handle, rendu dans `Detach`. Le `SkinnedMesh`,
    devenu `IDisposable` (P10), tient ses handles sur le `RiggedModel`, le `SkeletonDefinition` et les
    `AnimationClip`, et les rend dans son `Dispose`. Le `RiggedModel` est libéré par la collecte au titre
    d'`IAssetable`.
  - `ArrowComponent` est déplacé en T4.1 : il a besoin de `CasaEngineGame.DefaultTexture`, que T4.1 crée
    (ajustement d'ordre fait à l'exécution).
  - Tests.
- Validation : builds ; tests ; démos 3D relancées (T6.1).
- Commit : `refactor(components): 3D components hold their assets through handles`
- **Fait** :
  - **`StaticModelMesh`** (fichier hors de la liste ci-dessus mais nécessaire au détenteur décrit) :
    `LoadTexture` prend sa texture de repli (utilisée quand le mesh n'a pas de matériau) par
    `Acquire<Texture>` au lieu de `Load<Texture>`, garde le handle, et un nouveau `ReleaseTextureHandle()`
    le rend.
  - **`StaticModel`** devient `IDisposable` : `Dispose()` appelle `ReleaseTextureHandle()` sur chacun de ses
    `Meshes`.
  - **`StaticModelComponent`** : `_staticModelHandle`, acquis dans `InitializeWithWorld` seulement quand
    `StaticModel` est encore nul (comme avant, pour laisser un `StaticModel` assigné directement par code
    intact), avec libération de l'éventuel handle précédent avant une nouvelle acquisition et libération
    explicite quand l'id d'asset est vidé (cas de l'éditeur, qui rappelle `InitializeWithWorld` après avoir
    changé `StaticModelAssetId` sans passer par `Detach`). Nouveau `Detach()` qui rend le handle puis appelle
    `base.Detach()` (qui cascade déjà aux enfants générés).
  - **`SkinnedMesh`** devient `IDisposable` : `_riggedModelHandle`, `_skeletonHandle` et
    `_animationClipHandles` (un par clip séparé), acquis dans `Initialize` (`Acquire<RiggedModel>`,
    `Acquire<SkeletonDefinition>`, `Acquire<AnimationClip>` par clip, en gardant la déduplication
    existante) ; `Dispose()` les rend tous. `SetRiggedModel` (démos, tests) reste un chemin sans handle,
    inchangé.
  - **`SkinnedMeshComponent`** : `_skinnedMeshHandle`, avec libération systématique de l'éventuel handle
    précédent en tête d'`InitializeWithWorld` (celle-ci rappelle toujours `Acquire` quand l'id est non vide,
    sans garde `== null` contrairement à `StaticModelComponent`) puis acquisition si l'id d'asset est non
    vide. Nouveau `Detach()` qui rend le handle puis appelle `base.Detach()`.
  - **Tests** (2 nouveaux fichiers, 5 tests) : `StaticModelComponentAssetHandleTests` (acquisition tenue
    jusqu'à `Detach`, deuxième `InitializeWithWorld` sans fuite, vidage de l'id d'asset qui libère) et
    `SkinnedMeshComponentAssetHandleTests` (acquisition tenue jusqu'à `Detach`, deuxième
    `InitializeWithWorld` sans fuite). Les deux utilisent un chargeur factice renvoyant un objet sans
    `RootNode`/`Meshes` (StaticModel) ou sans `RiggedModelAssetId`/`SkeletonAssetId` (SkinnedMesh) : aucun
    `GraphicsDevice` n'est donc nécessaire, comme pour `ParticleSystemComponentAssetHandleTests`.
  - **Non testé, raison écrite ici (même limite que T2.1/T3.1)** : le round-trip complet
    `StaticModelMesh.LoadTexture` → `Texture.Load` → `Texture2D`, et la chaîne
    `SkinnedMesh.Initialize` → `RiggedModel.Initialize` (buffers GPU des sous-meshes), exigent un
    `GraphicsDevice` indisponible en headless. Le rendu du handle de texture lui-même est couvert par la
    boucle `StaticModel.Dispose()` → `mesh.ReleaseTextureHandle()`, exercée avec de vrais handles
    `AssetContentManager` dans les deux nouveaux tests de composant (modèle sans meshes, donc sans texture
    à charger — le mécanisme de libération de `StaticModel` lui-même n'a pas de test dédié séparé de la
    lecture du code, comme pour le round-trip `Sprite` de T2.1/T3.1). La recette visible du plan parent
    (parcours 389 → 390 → 389) ne couvre pas de composant 3D : aucune carte du portage Alundra actuel n'en
    utilise, donc pas de preuve en jeu disponible pour cette tâche.
  - `CasaEngine.Tests` 1765/1766, seul échec l'échec préexistant du docking ; les deux solutions et
    `Alundra/Alundra.csproj` compilent sans erreur.
  - **🧪 Needs testing** : aucune démo 3D relancée pour cette tâche (`T6.1` s'en charge). Vérifié par
    lecture : aucune démo n'utilise `StaticModelAssetId` ni `SkinnedMeshAssetId` (`rg` sans résultat dans
    `CasaEngine.Demos`) — toutes assignent `StaticModel`/`SkinnedMesh` directement en code
    (`StaticModelDemo`, `SkeletalAnimationBlendingDemo`, `AnimationBlendDemo`, `AnimationIkDemo`,
    `SkinnedMeshDemo`…), le chemin que les deux composants laissent inchangé (aucun handle acquis). Le
    chemin par id d'asset ajouté ici n'est donc exercé par aucune démo existante ; relancer une démo 3D
    (T6.1) reste utile pour confirmer l'absence de régression sur le chemin direct, pas pour couvrir
    l'acquisition par handle elle-même.

### ✅ T3.3 — Monde, entités, modes de jeu, cinématiques, audio

- Fichiers :
  - `World` : `SpawnEntity` → `LoadCopy<Entity>` ; `PlayerStartupSettings` tenu par le monde et rendu dans
    `Clear` ; `GameplayModeAsset` pris le temps de `CreateMode` ;
  - `EntityReference` : `LoadCopy<Entity>`, et la désérialisation en ligne directe (P4) ;
  - `GameManager` : `LoadCopy<World>`, plus la trace du nombre libéré (P7) ;
  - `CutsceneActionCoroutineFactory` : le `SoundAsset` est tenu le temps de l'action ;
  - `AssetContentManagerAudioClipProvider` : P2 ;
  - tests.
- Validation : builds ; tests (`GameManagerWorldLoadTests` et tests d'entités adaptés).
- Commit : `refactor(world): worlds, entities and cutscenes use copies and handles`
- **Fait** :
  - **`World`** : `SpawnEntity<T>(string)` et `SpawnEntity<T>(Guid)` lisent leur gabarit avec
    `LoadCopy<Entity>`/`LoadCopy<T>` (au lieu de `Load<...>(cache: ...)`) puis `.Clone()`, qui reste
    nécessaire : lui seul donne à l'instance générée un `Id` neuf (constructeur de copie
    `ObjectBase(ObjectBase)`), `LoadCopy` renvoyant un objet dont l'`Id` est celui de l'asset. Plus aucun
    gabarit d'entité n'est mis en cache ni épinglé dans le gestionnaire. `PlayerStartupSettings` est
    maintenant acquis par handle (`_playerStartupSettingsHandle`), gardé pour la vie du monde et rendu
    par un nouveau `ReleasePlayerStartupSettings()`, appelé depuis `Clear()` et en tête de
    `LoadPlayerStartupSettings()` (pour ne pas fuir le handle précédent si le contenu est rechargé sur le
    même monde). `StartGameplayModeAsset` prend le `GameplayModeAsset` dans un `using`, le temps du seul
    appel à `CreateMode()` : le handle est rendu avant que la méthode ne retourne, le `GameplayMode` créé
    ne dépendant plus de l'asset ensuite. `LoadPlayerStartupSettings` et `ReleasePlayerStartupSettings`
    sont passés en `internal`, comme `CreateGameplayProxy`, pour que les tests les pilotent sans jeu
    complet.
  - **`EntityReference`** : la branche en ligne (`AssetId == Guid.Empty`) désérialise directement
    (`new Entity()` puis `Load(noeud)`) au lieu de `assetContentManager.Load<Entity>(JObject)` (P4) ; la
    branche par référence et `CreateFromAssetInfo` lisent leur gabarit avec `LoadCopy<Entity>` avant
    `.Clone()`. `AssetContentManager.Load<T>(JObject)` n'a donc plus aucun appelant dans le moteur (attendu
    pour T8.1, « État vérifié » du plan).
  - **`GameManager.UpdateWorld`** : le monde à charger vient de `LoadCopy<World>` (au lieu de
    `Load<World>(cache: false)`, un alias exact). P7 : le nombre d'assets libérés par
    `CollectUnreferenced()` au début d'un changement de monde est tracé au niveau Info
    (`Logs.WriteInfo`), avant même la résolution du chemin du nouveau monde — la preuve visible demandée
    par la recette du plan parent (planche de tuiles libérée 390 → 389).
  - **`CutsceneActionCoroutineFactory`** : `LoadSoundAsset` devient `WithSoundAsset(world, id, useAsset)`,
    privée, qui acquiert le `SoundAsset` dans un `using` et n'appelle `useAsset` que pendant que le handle
    est tenu ; `PlaySound`/`PlayMusic` y passent leur logique en lambda. `AudioService.PlaySound` et
    `MusicPlayer.Play` ne lisent l'asset que de façon synchrone (volume, bus, `IsStreaming`,
    `CreateVoiceParameters`...) pour démarrer la lecture, sans le garder : le handle peut donc être rendu
    dès le retour de l'appel, avant que la coroutine ne continue.
  - **`AssetContentManagerAudioClipProvider`** (P2) : devient `IDisposable` ; `GetClip` acquiert chaque
    clip une seule fois par id et garde le handle dans `_clipHandles`, rendu par `Dispose()` — même
    politique qu'avant (tenu pour toute la partie), mais par handle compté au lieu du `Load<T>` épinglé.
    `AudioSystemComponent.Dispose(bool)` rend le fournisseur (`(Service.ClipProvider as
    IDisposable)?.Dispose()`) après avoir disposé le `AudioService`, qui est son propriétaire pour la
    durée du jeu. La libération par carte de l'audio reste une suite, non traitée ici (point verrouillé
    P2 du plan).
  - **Tests** (3 nouveaux fichiers, 9 tests ; 1 test ajouté à un fichier existant) :
    `CasaEngine.Tests/Application/WorldAssetHandleTests.cs` (`SpawnEntity` relit à chaque appel sans mise
    en cache ; `LoadPlayerStartupSettings`/`ReleasePlayerStartupSettings` tiennent puis rendent le handle ;
    un second `LoadPlayerStartupSettings` ne fuit pas le précédent ; `StartGameplayModeAsset` ne tient le
    handle que le temps de l'appel — ce dernier et les deux premiers pilotés par réflexion sur
    `World.Game`/méthodes internes, comme `StaticModelComponentAssetHandleTests`) ;
    `CasaEngine.Tests/Cutscenes/CutsceneActionCoroutineFactorySoundAssetHandleTests.cs`
    (`WithSoundAsset` tient le handle pendant le callback puis le rend ; un id vide n'appelle jamais le
    callback, piloté par réflexion sur la méthode privée) ;
    `CasaEngine.Tests/Audio/AssetContentManagerAudioClipProviderHandleTests.cs` (un même id n'est chargé
    qu'une fois et rend la même instance ; un id vide ne charge rien ; `Dispose()` rend et dispose chaque
    clip tenu). `CasaEngine.Tests/Scene/EntityReferenceLoadTests.cs` gagne
    `Load_InlineEntity_DeserializesDirectlyWithoutTheAssetManager` (la branche P4, avec un
    `AssetContentManager` sans loader enregistré : la preuve qu'elle ne le touche pas).
    `CasaEngine.Tests/Application/GameManagerWorldLoadTests.cs` gagne
    `UpdateWorld_WhenAWorldChangeStarts_TracesTheFreedAssetCountAtInfoLevel` (P7, capture du logger comme
    `ScrollingLayerComponentLoggingTests`, classe passée dans `ProjectEnvironmentCollection`). Tous
    buildables et exécutables sans `GraphicsDevice`.
  - **Non testé, raison écrite ici** : le passage complet de `GameManager.UpdateWorld` par un monde
    valide (`LoadCopy<World>` puis `CurrentWorld.LoadContent(_game)`) exige un `CasaEngineGame` avec un
    `PhysicsSystemComponent` réellement construit (`World.Clear` → `DisposePhysicsWorldContext` appelle
    `Game.PhysicsSystemComponent.ReleaseContext`), indisponible en headless comme dans les tâches
    précédentes pour le `GraphicsDevice` — c'est pourquoi les tests de `World` ci-dessus pilotent
    `LoadPlayerStartupSettings`/`ReleasePlayerStartupSettings`/`StartGameplayModeAsset` directement plutôt
    que par un `Clear()` complet. Le changement de `_currentWorld = Load<World>(cache:false)` en
    `LoadCopy<World>` est un remplacement exact (`cache:false` ne mettait déjà rien en cache ni ne prenait
    de bail) : aucune régression possible de ce côté, seule la lecture normale du monde par
    `LoadContent`/`BeginPlay` reste hors de portée d'un test headless.
  - `CasaEngine.Tests` 1776/1777, seul échec l'échec préexistant du docking (en cours de correction par
    ailleurs, tâche séparée `task_704ea6e0`) ; les deux solutions moteur et `Alundra/Alundra.csproj`
    compilent sans erreur.

---

## Phase 4 — Services du jeu, rendu et matériaux

### ✅ T4.1 — Services du jeu

- Fichiers :
  - `CasaEngineGame` : texture par défaut → `Register` et `DefaultTexture` (P6) ; rechargement à chaud →
    `LoadCopy` puis `Replace` ;
  - `ArrowComponent` : la texture par défaut vient de `CasaEngineGame.DefaultTexture` au lieu de
    `GetAsset<Texture>(Texture.DefaultTextureName)` (venu de T3.2) ;
  - `ScrollingLayerComponent`, `CellularLayerComponent` et `ParticleRendererComponent` : leurs caches de
    textures tiennent des handles, rendus quand l'entrée change et dans `Dispose` ;
  - tests existants adaptés.
- Validation : builds ; tests ; `HotReload` couvert par les tests existants s'il y en a, sinon la raison
  est écrite.
- Commit : `refactor(game): game services hold their assets, hot reload replaces instances`
- **Fait** :
  - **`CasaEngineGame`** : `CreateDefaultTexture` range la texture par défaut avec
    `AssetContentManager.Register(texture.Id, texture)` au lieu d'`AddAsset` sous le nom
    `Texture.DefaultTextureName`, et garde le handle dans `_defaultTextureHandle`, exposé par la nouvelle
    propriété publique `DefaultTexture` (P6). Aucun crochet de libération explicite : le jeu la tient pour
    toute sa vie, comme `AssetContentManagerAudioClipProvider` (T3.3) et sans `Dispose` de `CasaEngineGame`
    lui-même (aucun n'existe dans ce fichier). Le rechargement à chaud des particules et des sprites
    (`ResolveParticleAssetForHotReload`/`ResolveSpriteAssetForHotReload`) lit désormais avec
    `LoadCopy<T>` au lieu de `Load<T>(cache: false)` (remplacement exact, comme en T3.3) ; la mise en cache
    (`CacheParticleAssetForHotReload`/`CacheSpriteAssetForHotReload`) appelle `AssetContentManager.Replace`
    au lieu d'`AddAsset`, en gardant le calcul de `AssetId`/`Name`/`FileName` depuis le catalogue inchangé :
    `Replace` garde les détenteurs déjà en cours de l'ancienne instance, alors qu'`AddAsset` réépinglait une
    entrée neuve à chaque rechargement.
  - **`ArrowComponent.InitializeWithWorld`** : `StaticModel.Meshes[0].Texture = world.Game.DefaultTexture`,
    à la place de `GetAsset<Texture>(Texture.DefaultTextureName)`. `Texture.DefaultTextureName` n'a donc
    plus aucun appelant (`rg` sans résultat hors sa propre déclaration) ; il est laissé en place, `Texture.cs`
    n'étant pas dans la liste de fichiers de cette tâche.
  - **`ScrollingLayerComponent`** et **`CellularLayerComponent`** (mécanisme jumeau, même changement) :
    `LoadTexture(id)` prend désormais la texture par `Acquire<Texture>` (au lieu de `Load<Texture>`),
    ajoute le handle à une nouvelle liste `_layerTextureHandles`, puis résout le `Texture2D` comme avant.
    `ResolveTextures` — appelée uniquement à un changement de `LayersVersion`, jamais par image (doc de la
    classe) — rend d'abord tous les handles du résolvage précédent (`ReleaseLayerTextureHandles`) avant de
    reconstruire `_layerFrames`/`_layerSheets` : un id encore utilisé est simplement rendu puis repris,
    sans fuite ; ce n'est pas un coût de chemin chaud puisque `ResolveTextures` ne tourne pas par image.
    `ScrollingLayerComponent.Dispose(bool)` rend aussi ces handles (elle avait déjà un `Dispose`, pour
    `_whiteTexture`) ; `CellularLayerComponent` gagne un `Dispose(bool)` qu'elle n'avait pas, sur le même
    modèle.
  - **`ParticleRendererComponent`** : `_textureCache` (`Dictionary<Guid, Texture2D>`) devient
    `_textureHandles` (`Dictionary<Guid, AssetHandle<Texture>>`) ; `TryLoadTexture` devient
    `TryAcquireTexture`, qui acquiert par `Acquire<Texture>` et rend le handle si `.Load` échoue (au lieu de
    laisser fuir une acquisition partielle). `ResolveTexture` lit `handle.Asset.Resource`. Cette table n'a
    pas de point d'invalidation par entrée (elle grossit pour la partie, comme avant, aucun changement de
    politique) : `Dispose(bool)`, déjà présent, rend maintenant tous les handles tenus en plus de disposer
    `_effect`/`_fallbackTexture`.
  - **Tests** : aucun test nouveau. Les tests existants de `ScrollingLayerComponentSubmissionTests`,
    `ScrollingLayerComponentLoggingTests` et `CellularLayerComponentSubmissionTests` appellent
    `ResolveTextures` avec un chargeur synthétique (`_ => CreateTexture()` etc.) qui ne passe jamais par
    `LoadTexture`/`AssetContentManager` : l'ajout de `ReleaseLayerTextureHandles()` en tête de
    `ResolveTextures` est donc sans effet observable pour eux (aucun handle n'est jamais pris par ce
    chemin) — vérifié en relisant chaque test, tous verts sans modification. Aucun test ne couvre
    `ParticleRendererComponent.ResolveTexture`/`TryAcquireTexture` (`ParticleFlipbookTests` ne teste que
    `GetFlipbookTextureCoordinates`, une fonction pure). Le round-trip réel des trois composants — comme en
    T2.1/T3.2 — passe par `Texture.Load` → `Acquire<Texture2D>` → un `Texture2D`, qui exige un
    `GraphicsDevice` indisponible en headless ; c'est la même limite déjà écrite à ces deux tâches, non
    contournée ici. Aucun test ne couvre non plus `CasaEngineGame.DefaultTexture`/le rechargement à chaud :
    `CreateDefaultTexture` construit un `Texture2D(GraphicsDevice, ...)` réel, et
    `ResolveParticleAssetForHotReload`/`CacheParticleAssetForHotReload` (idem sprite) ne sont pas testés
    aujourd'hui (aucun fichier de test ne les cite, vérifié par `rg`) — écrire un test headless demanderait
    soit un `GraphicsDevice`, soit une refonte du chargement au delà du périmètre de cette tâche.
  - `CasaEngine.Tests` 1776/1777, seul échec l'échec préexistant du docking (correction en cours ailleurs,
    `task_704ea6e0`) ; les deux solutions moteur et `Alundra/Alundra.csproj` compilent sans erreur.

### 🧪 T4.2 — Rendu

- Fichiers et détenteurs :
  - `ShaderManager` : les effets sont tenus et rendus dans `Clear` et `Dispose`.
  - **Les trois générateurs d'environnement :** la première construction d'une cubemap passe par
    `Register`, et le handle est gardé dans le dictionnaire du générateur pour toute la partie. Une
    cubemap reconstruite parce que la précédente `IsDisposed` est remise **par `Replace`** sous le même id :
    la prise du générateur est gardée, et rien ne lève (P8, P11).
  - **`EnvironmentAssetLookup` :** `Acquire` seulement au défaut du cache de résolution, jamais par image.
    Le handle est gardé dans un dictionnaire par id de `EnvironmentAssetLookup` (niveau jeu), pour toute la
    partie : une deuxième résolution du même id ne reprend pas de handle (P11).
  - Tests existants adaptés.
- Tests nouveaux :
  - une nouvelle résolution après un changement des réglages d'environnement laisse **exactement une** prise
    par id ;
  - reconstruire une cubemap générée libérée, sous le même id, ne lève pas et rend la nouvelle cubemap.
- Validation : builds ; tests ; une démo d'environnement relancée (T6.1).
- Commit : `refactor(rendering): shaders and generated environments are held through handles`
- **Fait** :
  - **Deux petites classes internes génériques factorisent la tenue par handle, partagées par les quatre
    fichiers du plan** (`CasaEngine/Framework/Rendering/Environment/GeneratedAssetHandleCache.cs` et
    `AcquiredAssetHandleCache.cs`) : chacune tient un `Dictionary<Guid, AssetHandle<T>>` par
    `AssetContentManager`, via `ConditionalWeakTable`, pour que chaque manager — plusieurs coexistent dans
    les tests et dans l'éditeur — ait ses propres prises, jamais partagées ni fuitées d'un manager à
    l'autre. `GeneratedAssetHandleCache<T>.Acquire(manager, id, create, isStale)` couvre le cas D3/P8 des
    générateurs (`Register` au premier construit, `Replace` sous le même id quand `isStale` dit que
    l'instance tenue est périmée, la prise du générateur gardée) ; `AcquiredAssetHandleCache<T>.Acquire`
    couvre le cas P11 d'`EnvironmentAssetLookup` (un seul `Acquire` par id, tenu pour toute la partie).
    Chacune expose aussi un `TryGetCached` pour que l'appelant évite un travail inutile (résolution de
    chemin panorama, vérification du catalogue) quand l'entrée tenue est encore valide — comportement du
    code d'origine préservé à l'identique.
  - **`ShaderManager`** : `GetShader` acquiert l'`Effect` par `Acquire<Effect>` au lieu de `Load<Effect>`
    et garde le handle dans `_handles`, rendu dans `Invalidate`, `Clear` et `Dispose` (`Dispose` appelle
    `Clear`). `RegisterShader`, qui installe un `ShaderWrapper` déjà construit (effets intégrés chargés par
    le `ContentManager` de MonoGame, hors périmètre), rend aussi l'éventuel handle qu'il écrase, pour ne
    jamais laisser une prise orpheline si un id change de source. **Non couvert par un crochet de
    libération explicite ici** : aucun appelant (`StaticMeshRendererComponent`, `SkinnedMeshRendererComponent`,
    hors périmètre de T4.2) n'appelle `Dispose`/`Clear` sur son `ShaderManager` aujourd'hui ; comme avant la
    migration (`Load<Effect>` épinglait déjà pour toujours), l'effet reste tenu pour toute la vie du
    composant — aucune régression, mais la libération réelle attend que ces composants appellent
    `ShaderManager.Dispose` un jour, hors périmètre de cette tâche.
  - **Les trois générateurs** (`PanoramaEnvironmentGenerator`, `PhysicalAtmosphereEnvironmentGenerator`,
    `ProceduralSkyEnvironmentGenerator`) : `GetOrCreateCubemap` appelle d'abord
    `GeneratedAssetHandleCache<XnaTextureCube>.TryGetCached` (préserve l'ordre d'origine : pas de
    résolution du chemin panorama ni de coût de construction quand l'instance tenue est valide), puis
    `Acquire` avec une fabrique qui construit la cubemap et le prédicat `cubemap.IsDisposed` comme critère
    de péremption (comportement identique au `GetAsset`/`IsDisposed` d'origine). Effet de bord mineur,
    documenté ici plutôt que corrigé en douce (hors périmètre du plan) : l'ancien code indexait aussi la
    cubemap générée par son nom lisible (`AddAsset(id, name, cubemap)`) dans le dictionnaire de
    l'`AssetContentManager` ; `Register` ne l'indexe par nom que si l'id est dans le catalogue, ce qui n'est
    jamais le cas d'un id généré — l'index par nom disparaît donc pour ces cubemaps. Rien dans le dépôt ne
    les cherchait par nom (recherche `rg` vérifiée) ; `cubemap.Name` (le nom GPU/debug MonoGame) est
    toujours posé par la fabrique, inchangé.
  - **`EnvironmentAssetLookup`** : `TryLoadAsset<T>` vérifie d'abord `AcquiredAssetHandleCache<T>.TryGetCached`
    (ordre d'origine préservé : pas de vérification du catalogue quand déjà tenu), sinon vérifie le
    catalogue puis `Acquire`. Toujours interne, toujours appelé seulement depuis `EnvironmentResolver.Resolve`,
    lui-même déjà gardé par `ResolvedEnvironmentCache.TryGet` : aucun `Acquire` par image.
  - **Tests existants** : aucun test existant ne touchait `ShaderManager`, `EnvironmentAssetLookup` ou les
    trois générateurs au-delà des fonctions pures déjà couvertes par `PanoramaEnvironmentGeneratorTests`,
    `PhysicalAtmosphereEnvironmentGeneratorTests` et `ProceduralSkyEnvironmentGeneratorTests` (aucune ne
    touche l'`AssetContentManager` : rien à adapter, tous ces tests passent inchangés).
  - **Tests nouveaux** (2 fichiers, 8 tests) :
    `CasaEngine.Tests/Rendering/GeneratedAssetHandleCacheTests.cs` exerce
    `GeneratedAssetHandleCache<T>` avec un objet généré factice (pas une vraie cubemap : la construire
    demande un `GraphicsDevice`, indisponible en headless, comme pour les tests précédents de ce plan) —
    premier `Acquire` construit une fois et tient la prise (`CollectUnreferenced` ne la libère pas) ; un
    second `Acquire` non périmé ne reconstruit pas ; un `Acquire` sur une instance périmée reconstruit sous
    le même id, ne lève pas, rend la nouvelle instance et garde la prise (`CollectUnreferenced` toujours
    à 0) — la preuve demandée par le plan pour P8, à l'échelle de la logique de tenue qui est identique
    quel que soit `T`. `CasaEngine.Tests/Rendering/EnvironmentAssetLookupHandleTests.cs` exerce
    `EnvironmentAssetLookup.TryLoadEnvironmentAsset` avec un `EnvironmentAsset` factice (type de données pur,
    pas de `GraphicsDevice` nécessaire) : deux résolutions du même id ne chargent qu'une fois et la prise
    du manager (lue par réflexion sur le champ privé `_leases`, même route que les autres tests de ce plan)
    reste à 1 — la preuve demandée pour P11 ; plus un id absent du catalogue et un id vide. Le catalogue
    global (`AssetCatalog`) est vidé avant et après chaque test et la classe tourne dans
    `[Collection(ProjectEnvironmentCollection.Name)]`, comme les autres tests de ce plan qui le touchent.
  - `CasaEngine.Tests` 1784/1785, seul échec l'échec préexistant du docking
    (`EditorThemeAsset_Disables_Docking_Accent_Bars`, en cours de correction par ailleurs, tâche séparée
    `task_704ea6e0`) ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` compilent sans erreur ;
    `Alundra/Alundra.csproj` compile sans erreur.
  - **Manque encore, d'où 🧪** : la démo d'environnement relancée que la validation de T4.2 demande. Cet
    agent ne lance pas d'application graphique sans que la tâche le demande explicitement avec un délai
    borné ; T6.1 (Phase 6, non commencée) ne liste pas de démo d'environnement dans ses fichiers, donc
    aucune démo existante n'est identifiée comme couvrant ce point pour l'instant. **Vérification manuelle
    par l'auteur** : relancer la démo d'environnement (panorama / atmosphère physique / ciel procédural,
    selon celle qui existe) et confirmer que le fond et l'éclairage sont corrects, y compris après un
    changement de réglages qui force une reconstruction de cubemap.

### 🧪 T4.3 — Matériaux

- Fichiers :
  - `MaterialAuthoringAssetCache`, `MaterialCompiler`, `MaterialRuntimeResolver` et
    `StaticModelMaterialOverrideResolver` : lectures `cache: false` → `LoadCopy` ;
  - textures et cubemaps des matériaux compilés : P3 ;
  - tests (`MaterialCompilerTests`, `StaticModelMaterialOverrideResolverTests`…) adaptés.
- Validation : builds ; tests.
- Commit : `refactor(materials): material reads use copies, compiled materials hold their textures`

**Fait** : les quatre lectures `Load<MaterialAsset>(id, cache: false)` (`MaterialAuthoringAssetCache.GetOrLoad`,
`MaterialCompiler.BuildEffectiveValues.ResolveParent`, `MaterialRuntimeResolver.TryLoadRuntimeMaterial`,
`StaticModelMaterialOverrideResolver.ResolveMaterialAsset`) sont devenues `LoadCopy<MaterialAsset>(id)`, un
alias exact ici (ces quatre points ne relisaient déjà que l'entrée non encore mise en cache par les caches
d'auteurship au-dessus).

P3 (textures et cubemaps des matériaux compilés) : `MaterialCompiler.CompileBoth` (interne) résout maintenant
chaque texture par `Acquire<Assets.Textures.Texture>` (au lieu de `Load<Texture>(id)` épinglé) puis
`texture.Load(...)`, et chaque cubemap de réflexion par `Acquire<XnaTextureCube>` (au lieu de
`Load<XnaTextureCube>(id)` épinglé), et renvoie la liste des `AssetHandle` acquis (`TextureHolds`) au lieu de
les laisser pinnés pour toujours. `MaterialCache` (le seul appelant en production de `CompileBoth` via
`Recompile`/`RecompileRuntimeMaterial`) devient le propriétaire de cette liste par id de matériau : elle est
rendue quand l'entrée est remplacée (`Recompile`/`RecompileRuntimeMaterial`), retirée (`Invalidate`) ou que le
cache est vidé (`Clear`) — exactement la politique du point verrouillé P3 du plan. Le compilateur reste
appelable directement hors `MaterialCache` (éditeur `MaterialPreviewViewport`, `MaterialDemo`, tests) via les
méthodes publiques `Compile`/`CompileRuntimeMaterial`, qui continuent d'exister et de jeter les holds : ces
appelants gardent alors leurs textures chargées pour toute la durée du process, un comportement inchangé par
rapport à l'ancien `Load(cache: true)` (aucune régression), documenté dans le commentaire XML de `CompileBoth`.
La fabrique intégrée `lit-diffuse` reste enregistrée dans `RuntimeMaterialFactories` (restauration de
`RegisterRuntimeMaterialFactory` inchangée) ; `CreateRuntimeMaterial` la détecte par référence
(`ReferenceEquals`) pour lui passer directement la liste de holds sans toucher à la forme publique du délégué
`RuntimeMaterialFactory`, qu'une fabrique enregistrée par un appelant externe doit continuer de satisfaire.

Deux tests de `StaticModelMaterialOverrideResolverTests` construisaient l'`AssetContentManager` avec
`AddAsset` (dépôt direct dans le dictionnaire de la catégorie par défaut) : `LoadCopy` ne consulte jamais ce
dictionnaire (il appelle toujours le loader), contrairement à l'ancien `Load(id, cache: false)` qui renvoyait
l'entrée déjà présente sans recharger. Adaptés pour enregistrer un `IAssetLoader` de test et un catalogue via
`EngineRuntimeContext` (même technique que `StaticSpriteComponentAssetHandleTests`), ce qui a aussi révélé et
corrigé un bug d'ordre d'initialisation de champs statiques introduit pendant l'implémentation (le champ
`LitDiffuseFactory` était lu par l'initialiseur de `RuntimeMaterialFactories` avant sa propre initialisation,
d'où un `NotSupportedException` en test ; corrigé en le déclarant avant). Aucun nouveau test de fichier n'a
été ajouté : les tests existants de `MaterialCompilerTests`, `MaterialRuntimeResolverTests` (déjà sur
`TestProjectScope`, matériaux d'authoring parents/enfants réels) et les deux tests corrigés de
`StaticModelMaterialOverrideResolverTests` exercent déjà les quatre conversions `LoadCopy`.

**Non testé, raison écrite ici** : le chemin de libération réel des holds de texture/cubemap de P3
(`MaterialCache.Recompile`/`Invalidate`/`Clear` disposant `TextureHolds`) n'a pas de test dédié qui acquiert
une vraie image, car `Texture.Load` et le chargeur de `TextureCube` construisent un `Texture2D`/`TextureCube`
MonoGame réel, ce qui exige un `GraphicsDevice` indisponible dans ce projet de tests headless (même limite déjà
documentée pour `Sprite.Create` par T2.1). Les tests existants de `MaterialCacheTests` (matériaux sans texture,
id `Guid.Empty`) continuent de passer et couvrent la mécanique de remplacement/invalidation/vidage du
dictionnaire ; la couverture de la libération réelle d'une texture ou d'une cubemap de matériau reste une
**vérification manuelle par l'auteur** : ouvrir un modèle avec un matériau texturé (et si possible un matériau
réfléchissant) dans l'éditeur ou une démo, changer de matériau ou recharger à chaud, et confirmer à l'écran
qu'aucune texture ne reste visuellement bloquée sur l'ancienne image.

---

## Phase 5 — Éditeur

### 🧪 T5.1 — Panneaux et services de l'éditeur

- Fichiers :
  - `GameEditor` (`LoadCopy<World>`) ;
  - `TileMapEditorPanel`, `Animation2dAssetInspectorPanel` et `AnimationClipPreviewPanel` : handles rendus
    dans `Dispose` ;
  - `SpriteAssetInspectorPanel` et `SpriteSceneThumbnailRenderer` : `Replace` / `Register` ;
  - `EditorParticleSystemComponentService`.
- Validation : `CasaEngine.Editor.MonoGame.sln` compile ; tests ; **vérification manuelle par l'auteur**
  (voir Validation globale). D'où 🧪 à la fin de la tâche, tant qu'elle n'est pas faite.
- Commit : `refactor(editor): editor panels hold their assets through handles`

**Fait** :
- `GameEditor.OpenWorldAsset` : `Load<World>(id, cache: false)` → `LoadCopy<World>(id)`.
- `TileMapEditorPanel.LoadTileSets` : acquiert un `AssetHandle<TileSetData>` et un `AssetHandle<Assets.Textures.Texture>`
  par tileset, gardés dans deux nouvelles listes ; rendus par `ReleaseTileSets` (appelée avant chaque
  rechargement et dans `Dispose`).
- `Animation2dAssetInspectorPanel` : `ResolveSprite` acquiert désormais un `AssetHandle<SpriteData>` par sprite
  référencé (`_spriteDataHandleById`) ; `TryRefreshReferencedSpriteAsset` lit une copie fraîche non comptée
  (`LoadCopy<SpriteData>`) quand la donnée n'est pas fournie par le service de rechargement, et rend
  systématiquement le handle qu'elle détenait déjà pour cet id ; `ReleaseSpriteDataHandles` (nouvelle,
  appelée par `Dispose` et `RebuildPreviewState`) rend tout.
- `AnimationClipPreviewPanel` : le clip sélectionné et le clip de comparaison de blend deviennent des
  `AssetHandle<AnimationClip>` (`_selectedClipHandle`, `_blendReferenceClipHandle`, rendus par
  `ReleaseClipHandles`) ; le maillage riggé de prévisualisation devient un `AssetHandle<SkinnedMesh>`
  (`_previewSkinnedMeshHandle`, rendu par `ReleaseSkinnedMeshHandle`, appelée par `ClearPreviewMesh`) — le
  maillage est assigné directement sur `SkinnedMeshComponent.SkinnedMesh` sans passer par
  `SkinnedMeshAssetId`, donc le composant ne prend aucun handle lui-même dessus (commentaire du composant) ;
  ce panneau doit donc porter le sien. Les trois sont rendus dans `Dispose`, et les deux handles de clip au
  début de chaque `LoadAsset` (y compris sur exception).
- `SpriteAssetInspectorPanel.CacheLoadedSpriteAsset` et `SpriteSceneThumbnailRenderer.CacheSpriteAsset` :
  `AddAsset` → `Replace` (ces méthodes remettent l'instance en cours d'édition sous son id, potentiellement
  plusieurs fois pour le même id ; `Replace` est idempotent et garde les détenteurs existants, contrairement à
  `Register` qui lève si l'id est déjà présent).
- `EditorParticleSystemComponentService.ApplyParticleAsset` : `Load<ParticleEffectAsset>` → `LoadCopy` — le
  commentaire de `ParticleSystemComponent.SetParticleEffectAsset` précise qu'il ne prend pas de handle sur
  l'instance reçue (elle est supposée non partagée) ; le composant acquiert son propre handle partagé sur
  `ParticleEffectAssetId` à sa prochaine (ré)initialisation avec un monde.
- Détenteurs et libération : chaque site tient désormais son ou ses handles et les rend explicitement
  (`Dispose` des panneaux, ou avant chaque rechargement pour `TileMapEditorPanel`/`Animation2dAssetInspectorPanel`).
  Aucun test unitaire ajouté : les cinq classes touchées sont des panneaux MGUI ou un service qui construit un
  `HostedEditorGameAdapter`/`GraphicsDevice`, non instanciables dans `CasaEngine.Tests` sans périphérique
  graphique (comme les composants 3D de T3.2 et les services de T4.1) ; la preuve reste la recette manuelle de
  l'éditeur ci-dessous.
- Builds : `CasaEngine.MonoGame.sln` (0 erreur), `CasaEngine.Editor.MonoGame.sln` (0 erreur).
- Tests : `CasaEngine.Tests` 1784/1785 (le seul échec est
  `EditorThemeAsset_Disables_Docking_Accent_Bars`, préexistant et hors périmètre).
- `Alundra/Alundra.csproj` compile toujours (0 erreur).
- **Reste à faire avant ✅** : vérification manuelle par l'auteur dans l'éditeur lancé — ouvrir un `.tileMap`
  (tilesets affichés, changer de carte plusieurs fois), un `.animation2d` (aperçu des sprites, sauvegarder et
  rouvrir), un `.skeletonAnim` (aperçu du clip et du blend, changer de clip plusieurs fois), un `.sprite`
  (édition et vignette dans le Content Browser à jour), et ajouter/changer un effet de particules sur une
  entité dans l'éditeur.

---

## Phase 6 — Démos

### ⏳ T6.1 — Démos

- Fichiers :
  - `TopDownElevationDemo`, `TileMapSurfaceScreenDemo`, `TileMapDemo`, `TileMap3dDemo` ;
  - `CutsceneNavigateToDemo` et `CutsceneMoveToDemo` (`LoadCopy`) ;
  - `AudioDemo`.
- Validation : `CasaEngine.MonoGame.sln` compile ; chaque démo touchée est relancée une fois depuis
  `CasaEngine.Demos/`. Si la sélection d'une démo ne peut pas être automatisée, la tâche reste 🧪 avec la
  liste pour l'auteur.
- Commit : `refactor(demos): demos use handles and copies`

---

## Phase 7 — Consommateur Alundra (dépôt parent)

### ⏳ T7.1 — DLL Alundra

- Objectif : migrer les 7 appels de la DLL, selon le plan parent `docs/plan-migration-handles.md`, avant la
  suppression.
- Validation et commits : dans le dépôt parent.

---

## Phase 8 — Suppression

### ⏳ T8.1 — Supprimer l'ancienne API

- Objectif : D5.
- Fichiers : `AssetContentManager.cs` (tous les membres de D5, les catégories, l'épinglage, l'index par nom
  (P9)), et les tests qui ne compilent plus.
- Étapes :
  1. Supprimer.
  2. Builder les deux solutions et `Alundra/Alundra.csproj`.
  3. Corriger ce qui ne compile plus : il ne doit rester que des tests. Tout site de production qui ne
     compile plus est un site oublié par les phases 3 à 7 : le migrer ici, et le noter.
  4. Lancer la commande `rg` de la validation globale.
- Validation : builds ; toutes les suites ; `rg` vide.
- Commit : `refactor(assets)!: remove Load, AddAsset, GetAsset, Unload and asset categories`

---

## Phase 9 — Documentation et clôture

### ⏳ T9.1 — Documentation

- Fichiers :
  - `docs/engine/asset-handles-and-bitmap-fonts.md` : la section de compatibilité disparaît, et la doc
    décrit `LoadCopy`, `Register`, `Replace`, le détachement des entités détruites et la trace de collecte ;
  - `docs/README.md` ;
  - `ai-agent/README.md`.
- Validation : relecture des liens.
- Commit : `docs(engine): document the handle-only asset API`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | P1 à P11 attendent la validation de l'auteur avec ce plan. | toutes |
| O2 | Ordre des merges : `chantier/asset-handles`, puis `chantier/asset-handles-migration` ; dans le dépôt parent, `chantier/e13d-inventaire` puis `chantier/migration-handles`. Décision de l'auteur. | après la clôture |

## Hors périmètre

- Le `ContentManager` de MonoGame (`Game.Content.Load<Effect>`, icônes de l'éditeur, backend UI).
- La libération par carte de l'audio (P2) et des environnements (P11), et le chargement asynchrone.
- Une interface qui survit aux changements de monde (ADR-0036).
- L'échec préexistant du test de docking de l'éditeur.
