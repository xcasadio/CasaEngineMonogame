# Plan agent IA — Pont XAML runtime pour les écrans MGUI

Plan d'exécution d'un chantier né d'une règle posée par l'auteur le 2026-09-20 : **« pour tout ce qui est UI
et définition des contrôles il faut utiliser les fichiers XAML »**.

**Provenance des décisions.** Seules **D2, D3 et D4** ont été arbitrées par l'auteur, le 2026-09-20, en réponse
à trois questions posées. Les autres sont des choix d'ingénierie déduits du code et d'`AGENTS.md` : elles sont
révisables par l'auteur, et le plan le dit plutôt que de les lui attribuer.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Donner au runtime le moyen qui lui manque : charger un écran MGUI depuis un couple « enveloppe JSON + `.xaml` »,
puis retrouver ses contrôles par leur nom pour y pousser les données de jeu. Ensuite, migrer vers ce pont les
**dix** écrans de ce dépôt qui construisent aujourd'hui leur arbre de contrôles en C#.

Le XAML décrit ce qui est statique : l'arbre, les noms, la mise en page, les styles. Le code pousse ce qui
change : textes, valeurs, visibilité, positions dépendant de la résolution, abonnements aux événements. Ce
principe n'est pas inventé ici, il est déjà écrit dans
[screen-authoring-conventions.md](../../docs/editor/ui-screen-editor/screen-authoring-conventions.md) §6 :
*« At runtime, bind dynamic data explicitly in code rather than inline XAML binding syntax »*. Il n'avait
simplement jamais d'implémentation runtime.

Le onzième écran, `AlundraHudScreen`, vit dans le dépôt parent du portage et sera migré par un plan séparé,
une fois ce pont livré et le pointeur de sous-module avancé.

## État vérifié du dépôt (2026-09-20)

**Ce qui existe déjà et qu'il ne faut pas réécrire :**

- `XAMLParser.LoadRootWindow(MGDesktop, XamlDocumentSource, XamlLoaderMode, bool, bool)` existe
  (`MGUI/MGUI.Core/UI/XAML/XAMLParser.cs:491-496`). C'est l'appel exact dont le pont a besoin ; il n'y a pas
  d'analyseur à écrire. **Il prend un `MGDesktop`, pas un `UIRoot`** — ce détail décide de la testabilité.
- `XamlDocumentSource.FromFile(path)` existe et conserve `FilePath` et `DisplayName`
  (`MGUI/MGUI.Core/UI/XAML/XamlDocumentSource.cs`), donc le contexte d'erreur est déjà porté par la source.
- `XamlLoaderMode.Strict` valide les noms d'éléments inconnus et **refuse un `Name` déclaré deux fois**
  (`MGUI/MGUI.Core/UI/XAML/XamlLoaderDiagnostics.cs:111-175`, ADR-0013 de MGUI). En `Compatibility`,
  `XamlLoaderDiagnostics.Execute` ne fait rien du tout (`:84-104`).
- `MGWindow.GetElementByName<T>` et `TryGetElementByName<T>` existent (`MGUI/MGUI.Core/UI/MGWindow.cs:1956-1962`).
- `UIScreenAsset` porte `SourceXamlFile`, `ThemeName`, `PreviewResolution` et `ResourceFiles`
  (`CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`), et le runtime sait déjà le désérialiser :
  `AssetLoaderRegistry.cs:46` enregistre `new AssetLoader<UIScreenAsset>()`.
- `MGResources.GetThemeOrDefault(name, ...)` existe et retombe seul sur le thème par défaut
  (`MGUI/MGUI.Core/UI/MGResources.cs:415`).
- Le socle d'écran runtime existe : `IUIScreen`, `UIScreenBase`, `UIRoot`, `ScreenStack`, `GameScreenManager`
  dans `CasaEngine/Framework/UI/`.

**Ce qui manque, et qui est l'objet du chantier :**

- **Aucun chemin runtime ne transforme `SourceXamlFile` en `MGWindow`.** Le seul appelant de `XAMLParser`
  hors de MGUI est `CasaEngine.EditorServices/ScreenEditor/Preview/UIScreenPreviewBuilder.cs:34`, et
  `AGENTS.md` §9.9 interdit au runtime de dépendre de l'éditeur. `AssetLoader<UIScreenAsset>` ne fait que lire
  le JSON : il ne charge pas le XAML.
- Les dix écrans du dépôt construisent leur arbre en C# : `DialogueScreen` (252 lignes),
  `BlendingControlsScreen` (279), `DemoInfoScreen` (239), `MainHUDScreen` (94), `HudScreen` (92),
  `TitleScreen` (73), `ScreenEffectSmokeHudScreen` (67), `PauseMenuScreen` (64), `GameOverScreen` (64),
  `DemoHintOverlay` (55).

**Trois contraintes mesurées, qui décident de la forme du pont :**

1. `UIScreenPreviewBuilder.CreateTaggedMarkup` / `TagElement` (`:85` et `:118-146`) **réécrit le `Name` de chaque
   élément** en `_cse_<Guid>` pour gérer la sélection dans l'éditeur. Copier ce chemin détruirait exactement
   les noms dont nos écrans ont besoin. Le pont runtime ne réutilise rien de `UIScreenPreviewBuilder`.
2. Deux résolveurs de chemin divergent : `UIScreenEditorSession.cs:162-185` reste silencieux sur un chemin
   faux, `UIScreenPreviewPanel.cs:624-658` lève un `FileNotFoundException` tôt. Le runtime adopte le second.
3. **`UIRoot` n'est pas constructible dans la suite de tests.** Il est `sealed`, son unique constructeur prend
   un `CasaEngineGame` et un `IRenderSurface` et monte un backend MonoGame (`UIRoot.cs:26` et `:75-83`). La
   suite contourne déjà ce mur avec un patron établi : `DialogueScreenLayoutTests` monte un `MGDesktop`
   sans écran via un `TestRuntime` et pilote `DialogueScreen.BuildWindow(desktop)` plutôt que `OnInitialize`
   (`CasaEngine.Tests/Dialogue/DialogueScreenLayoutTests.cs:17-38`). **Tout ce chantier reprend ce patron** :
   ce qui doit être testé prend un `MGDesktop`, jamais un `UIRoot`.

**Ce que le code ne tranchait pas, et que l'auteur a tranché le 2026-09-20 (voir D11) :** l'extension du couple
XAML. `Constants.FileNameExtensions.Screen = ".screen"` (`Constants.cs:20`), et l'éditeur route **`.screen`** vers
son ouvreur d'écran XAML (`GameEditor.cs:3914`), lequel reconnaît le format en cherchant `source_xaml_file`
dans le JSON (`GameEditor.cs:5053-5071`) — **il n'existe aucune route pour `.uiscreen`**. Mais les cinq écrans
livrés dans `Projects/SampleProject/Screens/` sont des `.uiscreen`, catalogués `"asset_type": "uiscreen"`
(`AssetInfos.json:6-7`). Et surtout : **`.uiscreen` est l'extension que les tests emploient partout**, y
compris pour piloter la session d'édition d'écran elle-même
(`CasaEngine.Tests/ScreenEditor/UIScreenEditorSessionTests.cs:28,42,76,93,125,137,156,165`,
`CasaEngine.Tests/Assets/AssetCatalogTests.cs:19,42,46`,
`CasaEngine.Tests/EditorServices/EditorAssetCatalogServiceTests.cs:57,74`). La chaîne n'apparaît donc dans
aucun `.cs` **de production**, mais elle est le format que les tests tiennent pour vrai. La frontière est
étroite et nette : la **route de document** de l'éditeur connaît `.screen`, la **session d'édition** est
testée avec `.uiscreen`. L'auteur a tranché pour `.uiscreen` et pour l'ajout de la route manquante : D11.

**Le format hérité `.screen` est une donnée morte, et c'est un fait mesuré, pas une opinion.** Les quatre
fichiers restants (`Projects/RPGDemo/Screens/{MainHUD/MainHUD,TitleScreen/TitleScreen,GameOver/GameOverScreen}.screen`
et `Projects/SampleProject/Screens/Screen_Test.screen`) portent une enveloppe d'entité plus une liste plate de
widgets absolus d'une ancienne boîte à outils — `ImageBox`, `Label`, `ProgressBar`, avec `movable`,
`resize_edge`, `outline_moving`, `*_screen_ratio`. Trois constats :

- **Aucun code ne les lit.** Les seules occurrences de `.screen` dans tout le C# sont la constante
  (`Constants.cs:20`) et la route de document (`GameEditor.cs:3914`) — laquelle les **rejette**, puisque
  `TryLoadUIScreenAsset` rend `false` quand `source_xaml_file` manque (`GameEditor.cs:5065`), ce qui est leur
  cas à tous les quatre.
- **Les écrans C# les ont déjà remplacés.** `MainHUDScreen.cs:14-17` le dit lui-même : *« Replaces the legacy
  Neoforce MainHUD.screen »*. Les trois écrans de RPGDemo sont des réécritures délibérées, avec une autre mise
  en page que le fichier hérité.
- **`Screen_Test.screen` a une liste `controls` vide** : il n'y a littéralement rien à convertir dedans.

Conséquence pour le chantier : les convertir reproduirait une mise en page que le code a sciemment abandonnée.
L'auteur a tranché le 2026-09-20 : **ils sont supprimés** (D12, T0.5). Ils restent lisibles dans l'historique
git, donc la contre-épreuve de la phase 3 ne perd rien.

**Modifications préexistantes de l'auteur dans l'arbre de travail :** à vérifier par `git status` avant la
première tâche. Rappel permanent : `CasaEngine.Launcher/Program.cs` ne doit jamais être touché ni indexé.

## Décisions verrouillées

| Réf | Provenance | Décision |
|---|---|---|
| D1 | Déduite | Un écran XAML est un couple « enveloppe JSON portant `source_xaml_file` » + « fichier `.xaml` ». Le pont lui-même ne dépend pas de l'extension : il reçoit un `UIScreenAsset` déjà désérialisé. |
| D2 | **Auteur** | Le XAML du portage vit **dans le dépôt**, versionné et éditable à la main ou dans l'éditeur d'écrans ; c'est le convertisseur qui le recopie vers le projet généré et l'enregistre au catalogue à chaque export. Cette moitié-là appartient au dépôt parent et à son propre plan ; elle n'est pas faite ici. |
| D3 | **Auteur** | Périmètre : les **onze** écrans, migrés par tranches. Ce plan couvre les **dix** de ce dépôt. Le pont d'abord, puis un écran de preuve, puis les lots. |
| D4 | **Auteur** | Le runtime analyse en `XamlLoaderMode.Strict`, et rapporte une erreur d'asset nommée portant le fichier et la position (`AGENTS.md` §9.10). Un `Name` dupliqué casserait silencieusement la recherche par nom : il doit échouer au chargement. |
| D5 | Déduite | Le pont ne réutilise aucune partie de `CasaEngine.EditorServices` : ni `UIScreenPreviewBuilder`, ni le marquage des noms, ni les données de maquette, ni `UIScreenValidator`. Frontière `AGENTS.md` §9.9. |
| D6 | Déduite | `ThemeName` devient actif au chargement, via `MGResources.GetThemeOrDefault`. `PreviewResolution` reste une donnée d'éditeur. **`ResourceFiles` n'est pas touché par ce chantier** : aucun consommateur n'existe dans le dépôt, sa sémantique n'est écrite nulle part, et l'inventer serait contraire à `AGENTS.md` §2 et §9.7. Voir O4. |
| D7 | Déduite | Le XAML déclare l'arbre statique, les noms et la mise en page. Le code pousse les données, les positions dépendant de la résolution et les abonnements aux événements. Pas de syntaxe de binding en ligne. Repris du §6 du document de conventions, qui l'énonce déjà. |
| D8 | Déduite | Deux portes d'entrée, une seule mécanique : une qui prend un `XamlDocumentSource` (pour un écran sans projet, comme les démos), une qui prend un `UIScreenAsset` et le chemin de son enveloppe (pour un projet catalogué). La seconde appelle la première. |
| D9 | Déduite | Résolution de `SourceXamlFile`, dans cet ordre, comme le documente déjà `screen-authoring-conventions.md` §1 : relatif à l'enveloppe, puis relatif à `EngineEnvironment.ProjectPath`. Échec explicite si aucun des deux n'existe. |
| D12 | **Auteur** | Les quatre `.screen` hérités sont **supprimés**, avec leurs entrées de catalogue (T0.5). Ils ne sont pas convertis : la mesure a montré qu'ils sont morts et déjà remplacés. L'historique git les conserve, ce qui rend la suppression réversible et garde la contre-épreuve de la phase 3 possible. La constante `FileNameExtensions.Screen` et sa route ne sont **pas** retirées par ce chantier : c'est une API publique, voir O6. |
| D11 | **Auteur** | L'extension de l'enveloppe est **`.uiscreen`**, type de catalogue `uiscreen` — ce que disent déjà la session d'édition, le catalogue, les tests et le projet d'échantillon. **La route de document manquante est ajoutée à l'éditeur** (T0.4). `.screen` cesse alors de désigner deux choses : il ne reste que le format hérité, qui est supprimé en T0.5 (D12). |
| D10 | Déduite | **Tout ce qui doit être testé prend un `MGDesktop`.** Le socle d'écran expose une couture au niveau `MGDesktop` que `OnInitialize(UIRoot)` se contente d'appeler, exactement comme `DialogueScreen.BuildWindow(MGDesktop)`. Sans cela, rien de ce chantier n'est vérifiable dans la suite (contrainte 3 ci-dessus). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/xaml-runtime-bridge`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer, remplacer `⏳` par `🚧`. À la fin, lancer la validation
  indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis
  **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt ou vient d'une réponse de l'auteur. Sinon :
  tâche en ⚠️ Blocked, question écrite dans « Points ouverts », et **arrêt**.
- **Build obligatoire** avant tout ✅ : `dotnet build CasaEngine.MonoGame.sln`. **Tests** :
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`. Rappel : le `.sln` n'inclut pas `CasaEngine.Tests`,
  donc builder ce projet explicitement avant tout `--no-build`.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds. Le chargement d'un écran
  n'est pas un chemin chaud, l'`Update` d'un écran l'est.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : succès, zéro erreur. **Mesuré sur cette branche le 2026-09-20 :
  0 erreur, 144 avertissements préexistants.** Tout avertissement supplémentaire est à justifier.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` — **ligne de base mesurée sur cette branche le
  2026-09-20 : 1633 tests, 1632 verts, 1 échec préexistant**
  (`EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Disables_Docking_Accent_Bars`, attend `Collapsed`
  et obtient `Hidden`). Tout autre échec est une régression de ce chantier. Le compte diffère de celui du
  chantier `effet-ecran-alpha` (1642) parce que cette branche part de `main`, qui n'a pas encore ses tests.
  Rappel : le `.sln` n'inclut pas `CasaEngine.Tests`, donc builder ce projet avant tout `--no-build`.
- Smoke manuel : chaque écran migré doit être lancé et comparé à son rendu d'avant. Les démos se lancent
  **depuis `CasaEngine.Demos/`** et non depuis `bin/`, sinon la racine de contenu est incomplète.

---

## Phase 0 — Le pont

### ✅ T0.1 — Le chargeur d'écran XAML runtime

- Objectif : une seule classe qui, depuis un `UIScreenAsset` ou un `XamlDocumentSource`, rend un `MGWindow`
  prêt à l'emploi, avec des erreurs qui nomment le fichier fautif.
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenLoader.cs` (créé),
  `CasaEngine.Tests/UI/UIScreenLoaderTests.cs` (créé).
- Étapes :
  1. Écrire la résolution de chemin de D9, calquée sur `UIScreenPreviewPanel.ResolveSourceXamlPath`
     (`:624-658`) : relatif à l'enveloppe, puis relatif à `EngineEnvironment.ProjectPath`. `SourceXamlFile`
     vide ou fichier introuvable : exception nommant l'asset et les chemins essayés.
  2. Résoudre `ThemeName` par `MGResources.GetThemeOrDefault` et le poser sur la fenêtre obtenue (D6). Un
     `ThemeName` inconnu ne fait pas échouer le chargement : `GetThemeOrDefault` retombe seul sur le thème
     par défaut. **Ne pas toucher à `ResourceFiles`** (D6, O4).
  3. Appeler `XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(path), XamlLoaderMode.Strict, ...)`
     (D4). **Ne pas** passer par `UIScreenPreviewBuilder` (D5).
  4. Exposer les deux portes de D8 : une prenant un `XamlDocumentSource`, une prenant
     `(UIScreenAsset asset, string assetFilePath)` qui délègue à la première. **Les deux prennent un
     `MGDesktop`** (D10).
- Validation : tests montés sur un `MGDesktop` sans écran, selon le patron de
  `DialogueScreenLayoutTests.NewHeadlessDesktop` (`:32-38`). Couvrir au minimum : un XAML valide chargé depuis
  un fichier temporaire ; un `SourceXamlFile` relatif à l'enveloppe ; un `SourceXamlFile` introuvable qui lève
  avec le chemin dans le message ; un `Name` dupliqué que `Strict` refuse ; un `ThemeName` inconnu qui ne fait
  pas échouer le chargement. Build + suite complète verts.
- Commit : `feat(ui): load MGUI screens from XAML at runtime`

**Validation exécutée le 2026-09-20.** `dotnet build CasaEngine.MonoGame.sln` : 0 erreur. Suite complète :
**1646 tests, 1645 verts**, seul échec l'échec préexistant de la ligne de base. **+13 tests, 0 régression.**

Trois choses que l'exécution a apprises, au-delà du plan :

1. **O1 est répondu, et la réponse impose la signature.** Rien ne renseigne de chemin sur un `UIScreenAsset`
   au chargement : les trois appelants existants (`UIScreenPreviewPanel.cs:610-621`,
   `UIScreenEditorSession.cs:153-160`, `GameEditor.cs:5053-5080`) le construisent à la main et posent
   `FileName` eux-mêmes, et **aucun** ne passe par `AssetContentManager`, bien que le type y soit enregistré.
   `assetFilePath` est donc un paramètre, pas une lecture de l'asset.
2. **Le `catch` du chargeur couvre une vraie faille, et ce n'est pas une précaution théorique.** Construire
   l'arbre et l'attacher sont deux étapes ; `ParseWindowDefinition` est enveloppé par
   `XamlLoaderDiagnostics.Execute`, mais `Parsed.ToElement(Desktop)` ne l'est pas
   (`XAMLParser.cs:491-496`). Vérifié par une sonde jetable : pour un nom qu'un `ItemTemplate` clone,
   `LoadRootWindow` lève un `MGDuplicateElementNameException` **nu**, sans fichier ni position. Le chargeur le
   redécrit en `XamlLoaderException` portant le diagnostic. Un test dédié le prouve, et il échouerait si le
   `catch` disparaissait.
3. **Le harnais headless est partagé, pas recopié.** Le dépôt avait déjà **quatre** copies privées du même
   bloc de stubs (`DialogueScreenLayoutTests`, `ContentBrowserViewTestHarness`, `GridViewVirtualizationTests`,
   `EditorControlTemplateAssetLoadingTests`). Ce chantier migre dix écrans : plutôt qu'une cinquième copie
   privée, `CasaEngine.Tests/UI/HeadlessUiTestHarness.cs` est créé en **fichier neuf**, sans toucher aux
   quatre existants. Les regrouper serait un refactor hors périmètre, noté en O7.

### ✅ T0.2 — Le socle d'écran XAML

- Objectif : rendre l'écriture d'un écran XAML aussi courte que le sous-classement actuel, sans que chaque
  écran réécrive le chargement et la recherche par nom — et **testable sans carte graphique** (D10).
- Fichiers : `CasaEngine/Framework/UI/XamlUIScreenBase.cs` (créé),
  `CasaEngine.Tests/UI/XamlUIScreenBaseTests.cs` (créé).
- Étapes :
  1. Une classe abstraite dérivant de `UIScreenBase`, bâtie autour d'une **couture prenant un `MGDesktop`**
     (D10) : c'est elle qui appelle `UIScreenLoader`, garde le `MGWindow` obtenu et appelle
     `OnWindowLoaded(MGWindow)`. `OnInitialize(UIRoot root)` se réduit à appeler cette couture avec
     `root.Desktop`. C'est le patron déjà employé par `DialogueScreen.BuildWindow(MGDesktop)`.
  2. Implémenter `GetWindows()` une fois pour toutes.
  3. Fournir une recherche par nom qui **échoue avec le nom manquant et le fichier source** quand un contrôle
     attendu est absent, au lieu du `null` silencieux de `TryGetElementByName` (`AGENTS.md` §9.10). Un écran
     dont le XAML a perdu un nom doit le dire au chargement, pas planter plus tard.
  4. La source du XAML est déclarée par la sous-classe : soit un `UIScreenAsset`, soit un chemin de fichier.
- Validation : tests pilotant la couture `MGDesktop` sur un écran factice, **sans jamais construire de
  `UIRoot`** — contrôles retrouvés par nom, `GetWindows` rendant la fenêtre, nom absent produisant une erreur
  qui cite le nom et le fichier. Build + suite verts.
- Commit : `feat(ui): add a XAML-backed screen base class`

**Validation exécutée le 2026-09-20.** Build 0 erreur ; suite **1654 tests, 1653 verts**, seul échec celui de
la ligne de base. **+8 tests, 0 régression.**

Un écart au plan, dans le sens du mieux : `TryGetElementByName<T>` rend `false` pour **deux** fautes
distinctes — un nom absent, et un nom qui désigne un contrôle d'un autre type
(`MGWindow.cs:1959-1968`, il fait `Result as T` et teste le null). Le plan ne demandait que de signaler le
nom manquant ; `FindControl<T>` distingue les deux cas et le dit, parce qu'un écran qui cherche un bouton
là où le XAML a mis un texte n'a pas le même problème qu'un écran dont le nom a disparu. Deux tests
séparés le prouvent.

### ✅ T0.3 — ADR et documentation

- Objectif : laisser une trace de décision sur le pont lui-même, sans trancher ce qui ne l'est pas.
- Fichiers : `docs/decisions/0035-xaml-authored-ui-screens.md` (créé), `docs/decisions/README.md`,
  `docs/editor/ui-screen-editor/screen-authoring-conventions.md`, `docs/README.md` si l'index le demande.
- Étapes :
  1. ADR-0035 : les écrans d'interface sont décrits en XAML, le code pousse les données ; mode `Strict` au
     runtime ; frontière avec l'éditeur (D5) ; couture `MGDesktop` et pourquoi elle existe (D10).
  2. Ajouter au document de conventions une courte section « charger un écran au runtime », avec l'extrait
     d'usage de `XamlUIScreenBase`.
  3. **Extensions (D11) :** corriger le §1 et le §2 du document de conventions — l'enveloppe est `.uiscreen`,
     le type de catalogue `uiscreen`. Nommer `.screen` pour ce qu'il est : un format hérité d'une autre boîte
     à outils, que rien ne lit (supprimé en T0.5). Ne pas réécrire le reste du document, dont le §6 reste juste.
- Validation : relecture ; l'index des ADR cite bien 0035 ; plus aucune occurrence décrivant `.screen` comme
  le format XAML (`rg` dans `docs/`).
- Commit : `docs(ui): record ADR-0035 for XAML-authored screens`

**Validation exécutée le 2026-09-20.** `rg "\.screen" docs/` ne rend plus que des occurrences qui le nomment
comme format hérité. L'index cite ADR-0035.

Deux écarts au plan, tous deux des corrections :

1. **Le numéro d'ADR n'est pas le suivant libre sur cette branche.** L'index s'arrête à 0033 ici, parce que
   **ADR-0034 vit sur `chantier/effet-ecran-alpha`, non mergée**. Prendre 0034 aurait créé une collision au
   merge : ce chantier prend **0035** et laisse le trou, ce que l'ADR dit en tête.
2. **La citation du principe était fausse.** Le « bind dynamic data explicitly in code » est au **§6** du
   document de conventions, pas au §5 qui traite des ressources. Corrigé partout : ce plan, l'ADR, et les
   renvois.

### 🧪 T0.4 — La route de document manquante

- Objectif : rendre un `.uiscreen` ouvrable dans l'éditeur, ce qui est aujourd'hui impossible faute de route
  (D11). C'est la seule tâche de ce chantier qui touche l'éditeur, et elle est purement additive.
- Fichiers : `CasaEngine/Framework/Configuration/Constants.cs`, `CasaEngine.Editor/GameEditor.cs`,
  et un test dans `CasaEngine.Tests/` si la table de routes est atteignable depuis la suite.
- Étapes :
  1. Ajouter une constante d'extension pour `.uiscreen` **à côté** de `Screen = ".screen"`, sans toucher à
     cette dernière : le format hérité garde son nom (`AGENTS.md` §9.7, §9.8).
  2. Enregistrer la route correspondante vers `TryOpenUIScreenAsset` dans `GetAssetDocumentRoutes`
     (`GameEditor.cs:3914`), à côté de celle de `.screen`. `TryLoadUIScreenAsset` exige déjà
     `source_xaml_file` (`:5065`), donc un `.screen` hérité continue d'être refusé, ce qui est correct.
  3. Vérifier que la solution éditeur compile : cette tâche sort du périmètre de `CasaEngine.MonoGame.sln`.
- Validation : `dotnet build CasaEngine.Editor.MonoGame.sln` vert ; l'un des cinq `.uiscreen` du projet
  d'échantillon s'ouvre dans l'éditeur d'écrans par double-clic, ce qui ne marchait pas avant.
- Commit : `feat(editor): route uiscreen documents to the screen editor`

**Validation exécutée le 2026-09-20.** `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
`CasaEngine.MonoGame.sln` : 0 erreur. Suite **1654 / 1653 verts**, inchangée.

**Aucun test ajouté, et voici pourquoi** (`AGENTS.md` §6) : la table de routes est construite par
`GameEditor.GetAssetDocumentRoutes`, une méthode **privée** d'un type d'éditeur qui ne s'instancie pas sans
fenêtre ni périphérique graphique. La rendre atteignable serait un refactor hors périmètre. La route se
vérifie donc **à la main** : double-clic sur `Projects/SampleProject/Screens/main-menu.uiscreen` dans
l'éditeur. 🧪 **Reste à faire par l'auteur.**

Un choix que le plan ne prévoyait pas : la route `.screen` est **conservée** à côté de la nouvelle. Elle ne
coûte rien et ne peut pas ouvrir un fichier hérité — `TryLoadUIScreenAsset` exige `source_xaml_file`
(`GameEditor.cs:5065`) — mais elle laisse s'ouvrir une enveloppe qu'un projet aurait enregistrée sous
l'ancien nom. Retirer une route est une rupture ; en ajouter une ne l'est pas.

### 🧪 T0.5 — Retrait des quatre écrans hérités

- Objectif : ne plus laisser traîner un second format d'écran que rien ne lit (D12).
- Fichiers supprimés : `Projects/RPGDemo/Screens/MainHUD/MainHUD.screen`,
  `Projects/RPGDemo/Screens/TitleScreen/TitleScreen.screen`,
  `Projects/RPGDemo/Screens/GameOver/GameOverScreen.screen`,
  `Projects/SampleProject/Screens/Screen_Test.screen`.
  Fichiers modifiés : `Projects/RPGDemo/AssetInfos.json`, `Projects/SampleProject/AssetInfos.json`.
- Étapes :
  1. **Avant de supprimer, relire les quatre fichiers et consigner dans ce plan** ce que chacun contenait :
     types de contrôles, positions, textes. C'est la contre-épreuve de la phase 3, faite une fois pour toutes
     pendant que les fichiers sont encore sous les yeux.
  2. Supprimer les quatre fichiers avec `git rm`, un chemin à la fois.
  3. Retirer leurs entrées du catalogue (`"asset_type": "screen"`), en ne touchant qu'à ces entrées.
  4. Vérifier qu'aucune référence ne subsiste : `rg "\.screen" --glob "!*.md"` ne doit plus rendre que la
     constante et la route (O6). Vérifier aussi que `MainHUD.png` et `MainHUD.texture`, qui vivent dans le
     même dossier et **servent toujours** à `MainHUDScreen`, ne sont pas emportés.
- Validation : build + suite verts ; RPGDemo et le projet d'échantillon se chargent sans erreur de catalogue ;
  le HUD de RPGDemo s'affiche toujours, ce qui prouve que sa texture a survécu.
- Commit : `chore(assets): remove the four dead legacy screen files`

#### Relevé des quatre fichiers avant suppression (2026-09-20)

C'est la contre-épreuve de la phase 3, faite une fois pour toutes. Tout est en coordonnées absolues, dans une
fenêtre de référence de 800×480 pour l'écran-titre et 684×237 pour le HUD.

| Fichier | Contrôles |
|---|---|
| `MainHUD.screen` | `ImageBox` « ImageBox » en (17,345), 52×49, `sprite_id=b039042c-4b9d-4157-8be2-eed2d3c4f3bd` · `ProgressBar` « ProgressBar » en (74,346), 83×11 |
| `TitleScreen.screen` | `Label` « Rpg Demo » en (288,28), 137×35 · `Button` **ButtonStartGame** « Start Game » en (236,389), 225×36 · `Button` **ButtonExit** « Exit » en (236,431), 227×37 |
| `GameOverScreen.screen` | `Label` « GAME OVER » en (311,181), 64×63 |
| `Screen_Test.screen` | **aucun contrôle** |

Ce que la contre-épreuve apprend, et qui servira en phase 3 :

- **Rien n'a été perdu à la réécriture C#, et une chose a été gagnée.** Le HUD hérité n'avait qu'une image et
  une barre ; `MainHUDScreen` a le portrait, la barre de vie et un cadre. L'écran-titre hérité a deux boutons
  nommés, `ButtonStartGame` et `ButtonExit` : **ce sont les seuls noms utiles de tout le lot**, et la phase 3
  devrait les reprendre tels quels dans le XAML, pour que l'intention d'origine reste lisible.
- **Toutes les couleurs de texte sont à alpha 0** — `textColor=255,255,255,0` partout. Dans l'ancienne boîte à
  outils, ces libellés étaient donc invisibles tels quels : le format n'a jamais été rendu dans ce projet.
  Une raison de plus de ne pas transposer ces valeurs.
- Le `sprite_id` du HUD n'est pas emporté : supprimer l'enveloppe ne touche pas le sprite qu'elle citait.
  Vérifié — `b039042c-4b9d-4157-8be2-eed2d3c4f3bd` **est** `Screens\MainHUD\link_hud_portrait.sprite`, qui
  reste au catalogue.

**Validation exécutée le 2026-09-20.** Quatre fichiers supprimés par `git rm`, un chemin à la fois. Les six
entrées de catalogue retirées : 18 lignes de suppression pure, rien d'autre touché dans les deux
`AssetInfos.json`, et les deux fichiers restent du JSON valide (`jq -e type`). Plus aucun `.screen` dans
`Projects/`. **Aucune référence pendante** : les quatre identifiants supprimés ne sont cités nulle part dans
le dépôt (`.json`, `.world`, `.entity`, `.gameMode`, `.cs`). `Screens/MainHUD/` garde bien ses trois assets
vivants ; `Screens/TitleScreen/` et `Screens/GameOver/` disparaissent, leur `.screen` étant leur seul fichier.
Build 0 erreur, suite **1654 / 1653 verts**, inchangée.

🧪 **Reste à faire par l'auteur** : lancer RPGDemo et le projet d'échantillon une fois, pour confirmer qu'aucun
avertissement de catalogue n'apparaît et que le HUD s'affiche toujours.

---

## Phase 1 — La preuve

### ⏳ T1.1 — `PauseMenuScreen` en XAML

- Objectif : prouver le pont sur un écran interactif, avant d'y engager neuf autres migrations.
- Pourquoi celui-là : il tient en 64 lignes mais exerce tout ce qui compte — une `MGWindow` racine avec titre
  et taille, un pinceau de fond, un `MGStackPanel`, deux `MGTextBlock`, un `MGButton` avec un gestionnaire de
  clic, et une **position calculée à l'exécution** depuis `root.Desktop.ValidScreenBounds`.
- Fichiers : `CasaEngine.Demos/Demos/UIOverlay/PauseMenuScreen.cs`, son enveloppe et son `pause-menu.xaml`
  (créés), `CasaEngine.Demos/CasaEngine.Demos.csproj` (copie vers la sortie).
- Étapes :
  1. Transcrire l'arbre en XAML, en nommant les contrôles selon les conventions du §2 du document
     (`btnResume`, `lblTitle`, `lblDescription`). Les textes littéraux restent dans le XAML : ils sont statiques.
  2. Réduire le C# à : charger, retrouver `btnResume`, y brancher `_requestResume`, brancher `WindowClosed`,
     et **poser la position centrée** — elle dépend de la résolution, donc elle reste au code (D7).
  3. Charger par chemin (D8, première porte : les démos n'ont pas de projet catalogué), fichiers déclarés en
     `CopyToOutputDirectory=PreserveNewest`. Cette porte ne passe pas par le catalogue.
- Validation : build + suite verts ; démo UIOverlay lancée **depuis `CasaEngine.Demos/`**, menu de pause
  ouvert, « Resume » cliqué et reprise effective, fenêtre centrée comme avant.
- Commit : `refactor(demos): author the pause menu screen in XAML`

---

## Phase 2 — Les écrans des démos

### ⏳ T2.1 — `HudScreen` et `ScreenEffectSmokeHudScreen`

- Objectif : les deux HUD de démo, qui poussent des valeurs à chaque image.
- Fichiers : `CasaEngine.Demos/Demos/UIOverlay/HudScreen.cs`,
  `CasaEngine.Demos/Demos/TileMapDemo/ScreenEffectSmokeHudScreen.cs`, plus leurs fichiers XAML.
- Étapes : même découpe qu'en T1.1. Vérifier que les textes rafraîchis par image passent par un champ
  `MGTextBlock` retrouvé **une fois** au chargement, jamais par une recherche par nom dans `Update`
  (`AGENTS.md` §9.3).
- Validation : build + suite verts ; les deux démos lancées, valeurs affichées et rafraîchies comme avant.
- Commit : `refactor(demos): author the HUD screens in XAML`

### ⏳ T2.2 — `DemoInfoScreen`, `BlendingControlsScreen`, `DemoHintOverlay`

- Objectif : les trois écrans de l'outillage de démo, dont les deux plus gros du lot (239 et 279 lignes).
- Fichiers : les trois `.cs` de `CasaEngine.Demos/Demos/DemoUI/`, plus leurs fichiers XAML.
- Étapes : même découpe. `BlendingControlsScreen` construit peut-être ses contrôles depuis une liste de
  modes : si l'arbre est réellement variable (O3), **seule la coque part en XAML** et les éléments répétés
  restent construits au code, à l'intérieur d'un conteneur nommé. Le dire dans la note de validation.
- Validation : build + suite verts ; les démos concernées lancées, contrôles présents et agissants.
- Commit : `refactor(demos): author the demo tooling screens in XAML`

---

## Phase 3 — Les écrans de RPGDemo

Dégelée par D11 : l'extension est `.uiscreen`, le type de catalogue `uiscreen`. Les trois écrans de cette
phase avaient chacun un fichier hérité de même nom, supprimé en T0.5 (D12) après relecture. Le relevé consigné
par T0.5 sert de **contre-épreuve**, jamais de source à convertir.

### ⏳ T3.1 — `TitleScreen` et `GameOverScreen`

- Objectif : les deux écrans pleine page de RPGDemo, premiers à passer par la **seconde** porte de D8, celle
  qui prend un `UIScreenAsset` catalogué.
- Fichiers : `Projects/CasaEngine.RPGDemo/Scripts/Screens/TitleScreen.cs` et `GameOverScreen.cs`,
  `Projects/RPGDemo/Screens/...` pour les enveloppes et les `.xaml`, `Projects/RPGDemo/AssetInfos.json` pour
  les entrées de catalogue.
- Étapes :
  1. Créer les couples `.uiscreen` + `.xaml` sous `Projects/RPGDemo/Screens/` (D11).
  2. Ajouter les entrées au catalogue avec `"asset_type": "uiscreen"`, sur le modèle de
     `Projects/SampleProject/AssetInfos.json:6-7`.
  3. Charger par l'asset, pas par le chemin.
  4. **Contre-épreuve** : comparer au relevé consigné par T0.5 pour `TitleScreen.screen` et
     `GameOverScreen.screen`, et noter ce que la réécriture C# avait abandonné — un contrôle, un texte, une
     couleur. Le signaler dans la note de validation ; ne rien réintroduire sans accord de l'auteur, la
     réécriture était délibérée.
- Validation : build + suite verts ; RPGDemo lancé, écran-titre affiché et navigable, écran de fin atteint ;
  et **les deux écrans s'ouvrent dans l'éditeur d'écrans**, ce qui exerce la route ajoutée en T0.4.
- Commit : `refactor(rpgdemo): author the title and game over screens in XAML`

### ⏳ T3.2 — `MainHUDScreen`

- Objectif : le HUD de RPGDemo, qui lit une texture d'asset héritée (`MainHUD.texture`) en plus de son arbre.
- Fichiers : `Projects/CasaEngine.RPGDemo/Scripts/Screens/MainHUDScreen.cs`, son enveloppe et son `.xaml`,
  `Projects/RPGDemo/AssetInfos.json`.
- Étapes : comme T3.1. Si la texture ne peut pas être référencée depuis le XAML, elle est poussée au code
  sur un `MGImage` nommé — c'est précisément le partage prévu par D7, pas un contournement.
- Validation : build + suite verts ; RPGDemo lancé, HUD identique à son rendu d'avant.
- Commit : `refactor(rpgdemo): author the main HUD screen in XAML`

---

## Phase 4 — L'écran du framework

### ⏳ T4.1 — `DialogueScreen`

- Objectif : le seul écran qui appartient au moteur lui-même et non à un jeu. Placé en dernier parce qu'il
  pose une question que les neuf autres ne posent pas (O2).
- Fichiers : `CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs`, plus son XAML à l'emplacement que
  tranchera O2.
- Étapes :
  1. **Si O2 n'est pas tranché quand la tâche s'ouvre : passer la tâche en ⚠️ Blocked et s'arrêter.**
  2. Sinon, migrer comme les autres : arbre et noms en XAML, lignes de dialogue et choix poussés au code.
  3. `DialogueScreenLayoutTests` pilote `BuildWindow(MGDesktop)` : cette signature est le contrat de test de
     l'écran, elle doit survivre à la migration. Les tests existants doivent rester verts sans être réécrits
     autrement que pour la construction de l'arbre.
- Validation : build + suite verts, `DialogueScreenLayoutTests` compris ; la démo ou le test de dialogue
  existant rejoué, lignes et choix affichés.
- Commit : `refactor(dialogue): author the dialogue screen in XAML`

---

## Phase 5 — Clôture

### ⏳ T5.1 — Vérification et archivage

- Objectif : fermer le chantier avec une preuve, pas avec une impression.
- Étapes :
  1. Build des deux solutions, suite complète, et comparaison à la ligne de base (un seul échec préexistant
     attendu).
  2. Passe `verifier` en contexte neuf sur la revendication exacte : *les écrans migrés chargent leur arbre
     depuis un `.xaml`, aucun ne construit plus ses contrôles en C#, et le runtime ne dépend pas de
     l'éditeur*. Vérification que rien ne référence `UIScreenPreviewBuilder` hors de l'éditeur.
  3. Section de clôture dans ce fichier, déplacement dans `tasks/archive/`, ligne mise à jour dans
     `ai-agent/README.md`. Les points ouverts non tranchés y sont reportés explicitement.
- Validation : verdict `CONFIRMED` du verifier, ou dispositions écrites pour chaque réserve.
- Commit : `docs(ai-agent): close the XAML runtime bridge chantier`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O7 | **Faut-il regrouper les cinq harnais headless du projet de tests ?** `CasaEngine.Tests` porte désormais cinq blocs de stubs MGUI quasi identiques : les quatre préexistants (`DialogueScreenLayoutTests`, `ContentBrowserViewTestHarness`, `GridViewVirtualizationTests`, `EditorControlTemplateAssetLoadingTests`) plus `HeadlessUiTestHarness`, créé par T0.1 pour que ce chantier n'en ajoute pas dix. Faire converger les quatre premiers vers le nouveau serait un refactor de fichiers hors périmètre : à proposer à l'auteur, pas à décider. | T5.1 |
| O6 | **La constante `FileNameExtensions.Screen` et sa route survivent-elles à T0.5 ?** Après la suppression des quatre fichiers, la route `.screen` (`GameEditor.cs:3914`) ne correspond plus à rien : elle exige `source_xaml_file`, qu'aucun `.screen` n'a jamais porté. Les retirer serait une rupture d'API publique (`AGENTS.md` §9.8), donc ce chantier ne le fait pas. À signaler à l'auteur à la clôture, comme nettoyage possible. | T5.1 |
| ~~O5~~ | ~~**Que deviennent les quatre `.screen` hérités ?**~~ **Tranché le 2026-09-20 : supprimés, non convertis. Voir D12 et T0.5.** | closed |
| ~~O0~~ | ~~**Quelle extension porte l'enveloppe d'un écran XAML ?**~~ **Tranché le 2026-09-20 : `.uiscreen`, avec ajout de la route manquante. Voir D11 et T0.4.** Pour mémoire, la contradiction mesurée : La contradiction passe entre deux étages de l'éditeur, pas entre le code et les données. La **route de document** est clavetée sur `.screen` (`GameEditor.cs:3914` + `Constants.cs:20`) et reconnaît le format en cherchant `source_xaml_file` (`:5053-5071`), sans aucune route pour `.uiscreen`. Mais la **session d'édition** est testée exclusivement avec des `.uiscreen` (`CasaEngine.Tests/ScreenEditor/UIScreenEditorSessionTests.cs:28,42,76,93,125,137,156,165`), le catalogue aussi (`AssetCatalogTests.cs:19,42,46`, `EditorAssetCatalogServiceTests.cs:57,74`), et le projet d'échantillon livre cinq `.uiscreen` catalogués `"asset_type": "uiscreen"` (`AssetInfos.json:6-7`). Conséquence, que T0.4 corrige : les `.uiscreen` du projet d'échantillon n'étaient pas ouvrables par double-clic dans l'éditeur, faute de route, alors que tout le reste de la chaîne les accepte. | closed |
| O1 | L'asset chargé par `AssetLoader<UIScreenAsset>` expose-t-il le chemin de son propre fichier ? Si non, la seconde porte de D8 prend ce chemin en paramètre, ce qui est déjà la signature retenue. À confirmer au code en T0.1, sans supposer. | T0.1 |
| O2 | **Où vit le XAML d'un écran qui appartient au moteur et non à un jeu ?** `DialogueScreen` est utilisable par n'importe quel projet, donc son XAML ne peut pas être l'actif d'un projet particulier. Recommandation : ressource embarquée dans l'assemblage `CasaEngine` comme valeur par défaut, qu'un projet peut remplacer en déclarant sa propre enveloppe. C'est une décision d'architecture : elle appartient à l'auteur. | T4.1 |
| O3 | `BlendingControlsScreen` construit-il un arbre fixe ou une liste variable de contrôles ? S'il est variable, seule la coque part en XAML. À lire avant de migrer, pas à deviner. | T2.2 |
| O4 | **Que désigne `ResourceFiles` dans une enveloppe d'écran, et relatif à quoi ?** Le champ est désérialisé (`UIScreenAsset.cs:14,24-35`) et écrit par l'éditeur (`EditorAssetJsonSerializer.cs:481`), mais **aucun consommateur n'existe dans le dépôt** et toutes les enveloppes livrées le déclarent vide. Le chantier n'y touche pas (D6). Si l'auteur veut qu'il serve — thèmes ? gabarits de contrôles ? — c'est un ajout à chiffrer séparément. | hors périmètre actuel |

## Hors périmètre

- **Le convertisseur du portage.** D2 dit que le XAML d'Alundra vit dans le dépôt parent et que le
  convertisseur le recopie et le catalogue. Cette moitié appartient au dépôt parent et à son propre plan.
- **`AlundraHudScreen`.** Onzième écran, migré par le plan du dépôt parent une fois ce pont livré.
- **La conversion des quatre `.screen` hérités.** Mesurée et écartée : données mortes, déjà remplacées par les
  écrans C#, l'une vide. Elles sont relues puis **supprimées** en T0.5 (D12), pas transposées.
- **Le retrait de la constante `FileNameExtensions.Screen` et de sa route.** Rupture d'API publique, laissée à
  l'auteur (O6).
- **L'ancienne boîte à outils de widgets.** `ImageBox`, `Label`, `ProgressBar` et leurs propriétés
  (`movable`, `resize_edge`, `outline_moving`, `*_screen_ratio`) n'ont pas d'équivalent MGUI et ne sont pas
  transposés.
- **`ResourceFiles`.** Aucune sémantique inventée, aucun code écrit (D6, O4).
- **L'éditeur d'écrans.** Ce chantier ne change ni l'aperçu, ni la sélection, ni `UIScreenValidator`. Il se
  contente de ne pas en dépendre.
- **La syntaxe de binding en ligne dans le XAML.** Écartée par D7 et par le §6 du document de conventions.
- **`PreviewResolution`.** Reste une donnée d'éditeur (D6).
