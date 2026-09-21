# Plan agent IA — Ressources comptées, libération différée et polices bitmap

Plan d'exécution de l'[ADR-0036](../../docs/decisions/0036-reference-counted-asset-handles-and-bitmap-fonts.md).
Les décisions D1 → D10 ci-dessous ont été arbitrées avec l'auteur le 2026-09-21 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

**Le défaut à l'origine.** Le portage d'Alundra (dépôt parent) a trouvé en jeu que les textes de son
inventaire perdent leur police bitmap `font3` après un changement de carte. Le moteur reconstruit toute
l'interface à chaque monde, et une police enregistrée sur un moteur de texte ne passe pas au suivant.

**Ce que le chantier donne au moteur :**
- **Ressources comptées :** `AssetContentManager.Acquire<T>(id)` rend un handle, avec une seule instance par
  id pour tout le jeu.
- **Libération différée :** une ressource rendue attend en mémoire. Le moteur la libère au tout début du
  changement de monde suivant, ou sur appel explicite de `CollectUnreferenced()`.
- **Polices bitmap :** les fichiers `.fnt` deviennent des ressources du moteur.
- **Registre de polices :** un registre au niveau du jeu donne à chaque moteur de texte les polices tenues,
  par référence, sans rien recharger. MGUI gagne pour cela `RemoveStaticFont`.

**Consommateur.** La tranche DLL, où l'écran d'inventaire prend `font3` à sa construction et la rend à sa
destruction, vit dans le plan du dépôt parent : `docs/plan-e13d-inventaire.md`, tranche D5.f. Sa recette
en jeu est la preuve visible de ce chantier (D9).

## État vérifié du dépôt (2026-09-21)

**Git**
- La branche `chantier/asset-handles` a été créée depuis `main` (`7413f54d`). Elle porte l'ADR-0036
  (`578a31f7`).
- Modifications préexistantes de l'auteur, **jamais indexées** : `CasaEngine.Launcher/Program.cs` (à ne
  jamais ouvrir), `Projects/SampleProject/.casaeditor/viewport.editor.json`, et la référence `MGUI`. Ce
  dépôt enregistre `b8765bc`, le checkout de MGUI est sur `develop` à `fbd6280` (`git -C MGUI log -1`).

**Ressources**
- `AssetContentManager` met en cache par catégorie (`CasaEngine/Framework/Assets/AssetContentManager.cs:16`,
  `Load<T>` `:72-120`), avec un seul cache par id et par catégorie.
- `Entity` est toujours rechargée (`:84`).
- `Unload(category)` libère tout `IDisposable` de la catégorie (`:195-211`). Aucun appel dans le moteur.
- Pas de compteur, pas de handle, pas de chargement asynchrone. L'accès n'est pas protégé pour plusieurs
  threads (champs `Dictionary` simples).
- `AssetDictionary` (`:255-299`) indexe par id et par nom.
- `OnDeviceReset` parcourt toutes les catégories (`:220-231`).
- Le dispatch des chargeurs se fait par `typeof(T)` (`:89-94`) ; les chargeurs sont enregistrés dans
  `AssetLoaderRegistry.cs:25-57`. Aucun chargeur `.fnt`.
- Résolution des ressources :
  - `ResolveAssetInfo(Guid)` passe par `RuntimeContext` ou `AssetCatalog` (`:165-173`) ;
  - `EngineRuntimeContext.ResolveAssetInfoByFileName` existe, avec `AssetCatalog.GetByFileName` par défaut
    (`CasaEngine/Framework/Application/EngineRuntimeContext.cs:22,47`) ;
  - le catalogue indexe par nom de fichier exact (`AssetCatalog.cs:11,44-48`).

**Changement de monde**
- `GameManager.UpdateWorld` (`CasaEngine/Framework/Application/GameManager.cs:73-129`) enchaîne dans cet
  ordre :
  1. `Clear()` de l'ancien monde (`:77-80`), qui appelle `GameplayProxy.OnEndPlay` (`World.cs:110-113`) ;
  2. `Load<World>(…, cache: false)` (`:95`) ;
  3. `ViewManager.Clear()` (`:105`), qui détruit le runtime d'interface de chaque vue
     (`CasaEngineGame.cs:653-657`) ;
  4. `LoadContent`, `BootstrapViews` : nouvelles vues, donc nouveaux `UIRoot` (`CasaEngineGame.cs:643-648`) ;
  5. `BeginPlay`, puis `WorldLoaded` (`:120`).
- `SetWorldToLoad(World)` (chemin du play-in-editor) pose `_isNewWorld` sans passer par `Clear`.
  `RestoreWorld` ne recharge rien.
- `GameManager` se teste construit avec `null` (`CasaEngine.Tests/Application/GameManagerWorldLoadTests.cs:19,31`).

**Interface**
- `UIRoot` (`CasaEngine/Framework/UI/UIRoot.cs:75-106`) crée un `MGDesktop` et un
  `FontStashSharpTextEngine`, à qui il donne le `FontSystem` TTF du jeu (`CasaEngineGame.cs:41`, créé
  `:360`).
- `Dispose` (`:157-166`) ne fait que vider la pile d'écrans.
- `IUIScreen` n'a aucun crochet de destruction. Un écran retiré reçoit seulement `Hide()`
  (`ScreenStack.cs:79-114`).

**MGUI**
- `FontStashSharpTextEngine.AddStaticFont` (`MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs:270`) n'a
  pas d'inverse.
- `ResolveFont` retombe en silence sur une police de repli pour une famille inconnue (`:535-556`), et
  `MGTextBlock.TrySetFont` rend alors `false` (`MGUI/MGUI.Core/UI/MGTextBlock.cs:109-122`). Le `FontFamily`
  du XAML passe par ce chemin (`MGUI/MGUI.Core/UI/XAML/Controls.cs:3268-3275`).
- `MGUI.Tests` construit des moteurs de texte avec `Fonts/arial.ttf`
  (`MGUI/MGUI.Tests/Text/FSSMeasureDrawConsistencyTests.cs:44-51`).

**Tests du moteur**
- `CasaEngine.Tests` enregistre des ressources de test dans le catalogue et crée des
  `new AssetContentManager()` (`CasaEngine.Tests/Input/ButtonsMappingAssetLoaderTests.cs:44-79`).
- Il obtient une `SpriteFontBase` sans périphérique graphique depuis un TTF
  (`CasaEngine.Tests/UI/FontStashSharpBitmapFontRegistrationTests.cs`).
- Aucun test ne crée de `GraphicsDevice`.
- Le `.sln` n'inclut pas `CasaEngine.Tests` : il faut le builder lui-même avant `dotnet test --no-build`.

**Côté Alundra (preuve de D9)**
- Le catalogue exporté contient `UI\font3.fnt` (id `d18a3985-004d-59cc-a080-ce51fa57da99`, type `fnt`,
  `face="font3"`, `page id=0 file="Textures/font3.png"`) et `UI\Textures\font3.png` (id
  `14cb6308-222d-51cc-a750-add7128db3e4`).
- Défaut reproduit en jeu le 2026-09-21 par un harnais hors dépôt : ouvert sur la 389, portail, rouvert sur
  la 390, `FontFamily = 'Arial'`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Une seule instance par id de ressource, pour tout le jeu. Les catégories deviennent à terme des portées propriétaires au-dessus de ce cache unique. |
| D2 | Le XAML désigne une police par son nom de famille (`FontFamily="font3"`), résolu par le registre du jeu. |
| D3 | Une interface qui survit aux changements de monde est un chantier séparé. |
| D4 | Pas de portée « Session ». Handles comptés et libération différée : les détenteurs sont les objets qui utilisent la ressource. |
| D5 | Libération des ressources à 0 au tout début de chaque changement de monde, avant le `Clear` de l'ancien, plus un appel explicite `CollectUnreferenced()`. |
| D6 | Le jeu charge la police par l'API du moteur, seulement quand un objet qui l'utilise existe : jamais au lancement ni à l'écran titre. |
| D7 | Les portées par carte sont un chantier séparé. |
| D8 | MGUI gagne `RemoveStaticFont`, sur une branche créée depuis `fbd6280` (`develop`). La référence enregistrée ici avance d'autant. |
| D9 | Pas de démo moteur : la preuve visible est la recette en jeu du portage Alundra (tranche D5.f du plan parent). |
| D10 | L'écran qui utilise une police la prend à sa construction et la rend à sa destruction. |

**Points à valider par l'auteur** (arbitrages proposés par l'agent) :

| Réf | Proposition |
|---|---|
| P1 | `Load<T>(id)` en catégorie par défaut avec `cache: true` partage l'instance d'`Acquire<T>` et l'**épingle** : jamais collectée. Aucun appelant existant ne change de comportement. Les autres catégories, `cache: false` et `LoadFromFile` restent tels quels. |
| P2 | `Unload(category)` et `UnloadAll()` ne libèrent jamais une ressource qui a encore des handles vivants. |
| P3 | `CollectUnreferenced()` boucle jusqu'à point fixe : une dépendance rendue par une ressource libérée est libérée dans le même appel. La méthode rend le nombre de ressources libérées. |
| P4 | La collecte du début de changement de monde couvre les deux chemins, `SetWorldToLoad(string)` et `SetWorldToLoad(World)`, et jamais `RestoreWorld`. |
| P5 | Le registre pousse dans un moteur de texte nouvellement attaché les seules polices **tenues**. Une police en attente reprise est repoussée dans les moteurs attachés ; une police collectée en est retirée. |
| P6 | Une page de `.fnt` se résout relativement au dossier du `.fnt`, avec les séparateurs du catalogue (`\`), par `RuntimeContext.ResolveAssetInfoByFileName`. Une page introuvable lève une exception qui nomme le `.fnt` et la page. |
| P7 | `RemoveStaticFont` empêche les résolutions futures, mais ne touche pas un `MGTextBlock` qui a déjà résolu la police. Le contrat est que qui affiche une police la tient. |
| P8 | Deux entrées au [rapport des manques](../audits/mgui-gaps-from-xaml-screens.md), selon la règle de l'auteur : G5, pas d'API pour retirer une police (fermé par T1.1), et G6, un `FontFamily` inconnu est ignoré sans message (consigné, non corrigé). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/asset-handles`**, créée depuis `main`, ici et dans MGUI (depuis `fbd6280`, D8).
  Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin,
  lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de
  validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier. Une tâche
  MGUI se commite dans MGUI ; ce fichier est mis à jour dans le commit de pointeur qui suit (T1.2).
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le
  message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une
  réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire
  la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché
  (`dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`, le changement
  étant transverse). **Tests** : `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build` ; pour MGUI,
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`. Tests toujours au premier plan. Si le build est
  impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et
  noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais
  `git add -A` ni `git add .`. Ne jamais ouvrir `CasaEngine.Launcher/Program.cs`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds (`Acquire` et la collecte n'en sont pas :
    ils sont appelés au chargement et au changement de monde) ;
  - le runtime ne dépend pas de l'éditeur ;
  - API publique additive seulement (AGENTS.md §9.8).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- **Référence :** mesurée le 2026-09-21 avant T1.1, sur cette branche.
  - `CasaEngine.Tests` : **1707/1708**. Le seul échec est préexistant et stable, hors périmètre :
    `EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Disables_Docking_Accent_Bars` attend
    `Collapsed` et obtient `Hidden` (`CasaEngine.Tests/UI/EditorControlTemplateAssetLoadingTests.cs:98`).
    Il est signalé pour une tâche séparée.
  - `MGUI.Tests` : **2984/2984**.
- **Builds :** `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` sans
  erreur.
- **Tests :** les deux suites au moins aussi vertes que la référence, plus les tests ajoutés. Tout échec est
  une régression.
- **Recette visible (D9) :** tranche D5.f du plan parent. Le scénario : inventaire ouvert sur la carte 389,
  portail, rouvert sur la 390, journal au niveau `LogVerbosity.Trace`.
  - Les quatre textes sont en `font3` sur les deux cartes, et la capture le montre.
  - Le journal compte **une seule** ligne `Load asset …\UI\font3.fnt` et **une seule**
    `Load asset …\UI\Textures\font3.png` sur tout le parcours (trace écrite par
    `AssetContentManager.cs:104`, que prend aussi `Acquire`). Les deux se trouvent **après** la ligne
    `AlundraWorldProxy: world 'Ship Klark (beginning)-389' object layers`, écrite sans condition pendant
    l'initialisation du monde (`AlundraWorldProxy.cs:683-687`), et **avant** la ligne
    `AlundraWorldProxy: inventory screen wired` qui la suit (`AlundraWorldProxy.cs:1211`, écrite après la
    construction de l'écran, `:1207`). Aucune ne se trouve après la ligne
    `AlundraWorldProxy: world 'Ship Klark (inner)-390' object layers`.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0036

- Objectif : consigner les décisions de l'auteur.
- Fichiers : `docs/decisions/0036-reference-counted-asset-handles-and-bitmap-fonts.md`, `docs/decisions/README.md`.
- Validation : relu contre les réponses de l'auteur du 2026-09-21 ; citations `fichier:ligne` revérifiées.
- Commit : `docs(adr): reference-counted asset handles with deferred release, and bitmap fonts as assets` (`578a31f7`).

---

## Phase 1 — MGUI

### ✅ T1.1 — `FontStashSharpTextEngine.RemoveStaticFont`

- Objectif : pouvoir retirer une police statique d'un moteur de texte (D8, G5).
- Fichiers : `MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs`,
  `MGUI/MGUI.Tests/Text/FontStashSharpStaticFontRemovalTests.cs` (nouveau).
- Étapes :
  1. Dans `MGUI`, créer `chantier/asset-handles` depuis `fbd6280` (vérifier que `HEAD` y est).
  2. Ajouter `public bool RemoveStaticFont(string family, CustomFontStyles style)`, symétrique
     d'`AddStaticFont`. La méthode retire l'entrée de `_staticFonts`, appelle `InvalidateCache()` si une
     entrée a été retirée, et rend `true` dans ce cas. Doc XML qui dit la limite P7.
  3. Tests, avec une `SpriteFontBase` issue de `Fonts/arial.ttf` comme dans
     `FSSMeasureDrawConsistencyTests` :
     - après retrait, `ResolveFont` retombe (`IsFallback`) ;
     - le retrait d'une famille absente rend `false` ;
     - le cache de résolution ne rend plus l'ancienne police.
- Validation : `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`, référence plus les nouveaux tests, verts.
- Commit (dans MGUI) : `feat(text): remove a static font from the FontStashSharp text engine`
- **Fait** (MGUI `3075d93`, branche `chantier/asset-handles` depuis `fbd6280`) :
  - `MGUI.Tests` 2989/2989 (2984 + 5) ;
  - contre-épreuve : sans l'`InvalidateCache()`, deux des cinq tests échouent.

### ✅ T1.2 — Référence MGUI

- Objectif : enregistrer ici la branche MGUI de T1.1 (D8).
- Fichiers : `MGUI` (référence de sous-module), ce plan.
- Étapes :
  1. `git add MGUI` seul, après `git diff MGUI` qui doit montrer `b8765bc` → le commit de T1.1 (lequel
     contient `fbd6280`).
  2. Builder les deux solutions.
- Validation : les deux builds sans erreur ; `CasaEngine.Tests` à la référence.
- Commit : `chore(mgui): point at the text engine that can remove a static font`
- **Fait** :
  - `git diff MGUI` : `b8765bc` → `3075d93` ;
  - `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ;
  - `CasaEngine.Tests` à la référence, 1707/1708 avec le même échec préexistant.

---

## Phase 2 — Ressources comptées

### ✅ T2.1 — `AssetHandle<T>`, `Acquire<T>`, `CollectUnreferenced`

- Objectif : une instance par id, des handles comptés, une libération différée (D1, D4, D5 ; P1, P2, P3).
- Fichiers :
  - `CasaEngine/Framework/Assets/AssetHandle.cs` (nouveau) ;
  - `CasaEngine/Framework/Assets/AssetContentManager.cs` ;
  - `CasaEngine.Tests/Assets/AssetContentManagerHandleTests.cs` (nouveau, dossier créé s'il manque).
- Étapes :
  1. `AssetHandle<T> : IDisposable` expose `Id` et `Asset`. `Dispose` est idempotent et décrémente le
     compteur.
  2. Le gestionnaire tient, par id de la catégorie par défaut, un compteur et un drapeau « épinglé ».
  3. `Acquire<T>(Guid id)` réutilise l'instance présente (tenue, en attente ou épinglée), sinon la charge par
     le même chemin que `Load<T>`.
  4. `Load<T>` en catégorie par défaut avec `cache: true` épingle l'instance (P1). Les autres chemins restent
     inchangés.
  5. `CollectUnreferenced()` libère les entrées non épinglées à 0 : `Dispose` si `IDisposable`, puis retrait
     du cache. La méthode boucle jusqu'à point fixe (P3) et rend le nombre de ressources libérées.
  6. `Unload`/`UnloadAll` sautent les entrées tenues (P2).
  7. Doc XML sur chaque membre public.
- Tests, avec un chargeur de test et un catalogue de test comme dans `ButtonsMappingAssetLoaderTests` :
  - deux `Acquire` : même instance, chargeur appelé une fois ;
  - rendue puis reprise avant collecte : même instance, chargeur toujours appelé une fois ;
  - rendue puis collectée : `Dispose` appelé, puis un `Acquire` recharge ;
  - `Load` puis `Acquire`, tout rendre, collecter : rien de libéré (épinglée) ;
  - une dépendance rendue par le `Dispose` d'un parent est libérée dans le même appel ;
  - `Unload` n'élimine pas une entrée tenue ;
  - `Dispose` deux fois ne décrémente qu'une fois.
- Validation : build des deux solutions ; `CasaEngine.Tests` = référence + nouveaux tests.
- Commit : `feat(assets): reference-counted asset handles with deferred release`
- **Fait** :
  - `CasaEngine.Tests` 1718/1719, soit la référence plus 11 tests, avec le seul échec préexistant ; les deux
    solutions compilent sans erreur.
  - Deux cas ajoutés en relisant le code :
    - une entrée épinglée sans bail, retirée par `Unload`, perd aussi toutes ses entrées de nom (plusieurs
      ressources partagent un nom, par exemple le `.png`, le `.texture` et le `.fnt` de `font3`) ;
    - une candidate reprise pendant la collecte n'est pas libérée.
  - `Acquire<Entity>` lève une exception : une entité s'instancie à chaque usage.

### ✅ T2.2 — Collecte au début de chaque changement de monde

- Objectif : D5, P4.
- Fichiers : `CasaEngine/Framework/Application/GameManager.cs`,
  `CasaEngine.Tests/Application/GameManagerWorldLoadTests.cs`.
- Étapes :
  1. En tête d'`UpdateWorld`, quand un changement est en attente (`_worldToLoad` non vide, ou `_isNewWorld`
     posé par `SetWorldToLoad(World)`), appeler `CollectUnreferenced()` sur le gestionnaire du jeu, **avant**
     le `Clear` de l'ancien monde. Rien pour `RestoreWorld`.
  2. Un jeu absent (tests construits avec `null`) ne collecte rien.
- Tests : si `GameManager` accepte un gestionnaire de ressources de test sans `CasaEngineGame`, vérifier
  qu'une ressource rendue avant la demande est libérée et qu'une ressource tenue par l'ancien monde ne l'est
  pas. Sinon, écrire pourquoi sous la tâche : la preuve de l'ordre passe alors par le journal de la recette
  D5.f.
- Validation : build des deux solutions ; `CasaEngine.Tests` à la référence plus les tests de T2.1–T2.2.
- Commit : `feat(world): free unreferenced assets when a world change starts`
- **Fait** :
  - la condition est `HasPendingWorldLoad`, qui existait déjà et couvre les deux chemins ;
  - un constructeur interne reçoit le gestionnaire de ressources (le public passe celui du jeu), ce qui rend
    `GameManager` testable sans `CasaEngineGame` ;
  - deux tests : une ressource rendue est libérée et une ressource tenue ne l'est pas, avant même la
    recherche du nouveau monde (chemin absent du catalogue, qui lève ensuite) ; rien n'est libéré sans
    changement en attente ;
  - `CasaEngine.Tests` 1720/1721 ; deux solutions sans erreur.

---

## Phase 3 — Polices bitmap

### ✅ T3.1 — Ressource `BitmapFont` et chargeur `.fnt`

- Objectif : un `.fnt` du catalogue devient une ressource du moteur (ADR-0036 ; P6).
- Fichiers :
  - `CasaEngine/Framework/Assets/Fonts/BitmapFont.cs` (nouveau) ;
  - `CasaEngine/Framework/Assets/Loaders/BitmapFontLoader.cs` (nouveau) ;
  - `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs` ;
  - `CasaEngine.Tests/Assets/BitmapFontLoaderTests.cs` (nouveau).
- Étapes :
  1. `BitmapFont : IDisposable` porte `Family` (le `face` du `.fnt`), `Font` (`SpriteFontBase`) et les
     handles de ses pages. `Dispose` rend ces handles et signale sa libération (événement lu par T3.2). Son
     constructeur accepte une `SpriteFontBase` déjà faite, pour les tests.
  2. Le chargeur lit le texte et en extrait `face` et les `page … file=`, par une analyse pure et testable.
  3. Il résout chaque page (P6), en prend un handle `Acquire<Texture2D>`, puis construit la police par
     `StaticSpriteFont.FromBMFont(texte, page => new TextureWithOffset(…))`.
  4. Enregistrer le chargeur pour `typeof(BitmapFont)`.
- Tests, sans périphérique : l'analyse (`face`, pages), la résolution d'une page relative et sa normalisation,
  et une page absente du catalogue (exception qui nomme le `.fnt` et la page). La création de la texture et
  de la police exige un `GraphicsDevice`, qu'aucun test du moteur ne crée : elle est prouvée par la recette
  D5.f, et cette raison est écrite sous la tâche.
- Validation : build des deux solutions ; `CasaEngine.Tests` verts.
- Commit : `feat(assets): load BMFont files as bitmap font assets`
- **Fait** :
  - l'analyse est `BitmapFontDescriptor` (interne) ;
  - la résolution d'une page passe par `AssetContentManager.ResolveAssetInfoNextTo` (interne), avec la même
    racine de projet que `ResolveAssetPath` ;
  - si une page échoue, les pages déjà prises sont rendues ;
  - 10 tests : analyse, ordre des pages, erreurs, trois résolutions relatives, page absente du catalogue
    (nom demandé `UI\Textures\font3.png`, message qui nomme le `.fnt` et la page), extension, cycle de vie
    de `BitmapFont` ;
  - `CasaEngine.Tests` 1730/1731 ; deux solutions sans erreur.
  - **Non testable ici :** la construction de la texture et de la police exige un `GraphicsDevice`, qu'aucun
    test du moteur ne crée. Elle est prouvée par la recette D5.f.

### ✅ T3.2 — Registre de polices du jeu

- Objectif : chaque moteur de texte trouve par référence les polices tenues (D2, D6, P5).
- Fichiers :
  - `CasaEngine/Framework/UI/UIFontRegistry.cs` (nouveau) ;
  - `CasaEngine/Framework/Application/CasaEngineGame.cs` (propriété `UIFonts`) ;
  - `CasaEngine/Framework/UI/UIRoot.cs` ;
  - `CasaEngine.Tests/UI/UIFontRegistryTests.cs` (nouveau).
- Étapes :
  1. `UIFontRegistry` est construit avec l'`AssetContentManager` du jeu.
     - `Acquire(Guid bitmapFontAssetId)` rend un `IDisposable` : il prend un `AssetHandle<BitmapFont>` et
       compte les détenteurs par police.
     - Au premier détenteur, il fait `AddStaticFont(Family, Normal, Font)` dans chaque moteur attaché.
     - Au dernier rendu, il rend le handle : la police passe en attente et reste dans les moteurs.
     - À la libération de la `BitmapFont` (son événement), il fait `RemoveStaticFont` dans chaque moteur
       attaché.
  2. `Attach(FontStashSharpTextEngine)` / `Detach(…)`. Un moteur attaché reçoit aussitôt les polices tenues
     (P5).
  3. `CasaEngineGame.UIFonts` suit le modèle de `FontSystem` : créé une fois, au même endroit.
  4. `UIRoot` attache son moteur de texte à sa création et le détache dans `Dispose`.
- Tests, avec des `BitmapFont` construites sur des polices TTF rasterisées par le processeur et un chargeur de
  test :
  - police tenue, puis moteur attaché : la famille est résolue ;
  - moteur attaché, puis police tenue : résolue ;
  - police rendue : toujours résolue (en attente) ;
  - après `CollectUnreferenced` : retirée, `IsFallback` ;
  - détachement : plus de poussée vers ce moteur ;
  - deux détenteurs : un seul `AddStaticFont` utile, la police reste tenue tant qu'un détenteur reste.
- Validation : build des deux solutions ; `CasaEngine.Tests` verts.
- Commit : `feat(ui): a game-level registry that gives held bitmap fonts to every UI text engine`
- **Fait** :
  - 8 tests, dont le scénario exact du changement de carte : l'ancien détenteur rend la police, un nouveau
    moteur de texte s'attache, le nouveau détenteur la reprend. Résultat : même instance, un seul
    chargement, résolue sur le nouveau moteur, et rien de libéré au changement suivant.
  - Un moteur de texte nouvellement attaché ne reçoit pas les polices en attente, comme le prévoit P5.
  - `CasaEngine.Tests` 1738/1739 ; deux solutions sans erreur.
  - **Non testé ici :** l'attachement par `UIRoot`, qui exige un runtime MonoGame. Il est prouvé par la
    recette D5.f.

---

## Phase 4 — Documentation

### ✅ T4.1 — Doc moteur et manques MGUI

- Objectif : documenter l'API ajoutée (AGENTS.md §10) et consigner G5 et G6 (P8).
- Fichiers :
  - `docs/engine/asset-handles-and-bitmap-fonts.md` (nouveau, en anglais) ;
  - `docs/README.md` (index) ;
  - `docs/engine/dialogue-choices-and-bitmap-fonts.md` (§2 : une ligne qui renvoie au registre) ;
  - `ai-agent/audits/mgui-gaps-from-xaml-screens.md` (G5, G6) ;
  - `ai-agent/README.md` (ligne du tableau).
- Étapes :
  1. Écrire la doc : résumé, extrait d'usage (`Acquire`/`Dispose`, `UIFonts.Acquire`, `FontFamily` en XAML),
     moment de collecte, limites (P1, P7, fil unique), suites (D3, D7).
  2. Ajouter G5 et G6 au format des entrées existantes.
- Validation : relecture des liens ; aucun code touché.
- Commit : `docs(engine): document counted asset handles and the UI font registry`
- **Fait** :
  - `docs/engine/asset-handles-and-bitmap-fonts.md`, en anglais ;
  - sa ligne dans l'index `docs/README.md`, sous une nouvelle section « Ressources » ;
  - le renvoi dans `dialogue-choices-and-bitmap-fonts.md` §2 ;
  - G5 (corrigé par T1.1) et G6 (consigné) au rapport des manques de MGUI.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~Les propositions P1 à P8 attendent la validation de l'auteur avec ce plan.~~ **Validées par l'auteur le 2026-09-21** avec le plan (réponse « AUTO »). | toutes |
| O2 | Le merge de `chantier/asset-handles`, ici et dans MGUI, reste une décision de l'auteur, comme le merge de la branche MGUI dans `develop`. | après la clôture |

## Hors périmètre

- Portées par carte et migration des appelants de `Load<T>` vers des handles (D7).
- Interface qui survit aux changements de monde (D3), et crochet de destruction dans `IUIScreen`.
- Chargement asynchrone et accès depuis plusieurs threads.
- Correction de G6 (un `FontFamily` inconnu ignoré sans message) : consigné seulement.
- Démo moteur (D9).
