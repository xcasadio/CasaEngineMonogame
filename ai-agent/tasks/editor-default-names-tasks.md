# Plan agent IA — Noms par défaut des éléments d'écran et des composants créés dans l'éditeur

Plan d'exécution de la partie moteur de la demande de l'auteur du 2026-10-09 : « Il faut nommer correctement chaque élément dans tout le projet […] chaque élément de l'UI peut être nommé. Il faut faire pareil pour chaque asset et élément contenu dans un asset (par exemple un world). » La partie contenu (RacingGameCasaEngine) est le plan `ai-agent/tasks/rgce-element-naming-tasks.md` du dépôt RacingGame, qui finit par la mise à jour de son pointeur vers ce travail.
Les décisions D1 → D3 ci-dessous ont été arbitrées avec l'auteur le 2026-10-09 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

L'éditeur donne un nom unique à tout ce qu'il crée :

- **éléments d'écran** : un élément ajouté depuis la palette, le conteneur qui enveloppe la racine, un élément dupliqué ou collé (avec ses descendants). Aujourd'hui, l'élément ajouté n'a pas de nom, et la copie garde le nom de l'original, ce qui donne deux noms identiques dans la même fenêtre ;
- **composants** : un composant ajouté par le dialogue « Add component », ou créé en déposant un modèle dans le viewport. Aujourd'hui, il garde le nom `Object <guid>` donné par le moteur.

Un composant cloné avec son entité garde son nom (P8). Aujourd'hui, il le perd : la duplication d'une entité dans l'éditeur rend `Object <guid>` à tous ses composants.

La convention de nommage des éléments d'écran est complétée pour tous les types de la palette. Le chantier livre aussi une ADR.

## État vérifié du dépôt (2026-10-09)

Branche `chantier/rgce-editor-project`, tête `7a5320b6a`, arbre propre (`git status --short` vide).

- **Éléments d'écran, création** :
  - `UIScreenNodeFactory.Create` (`CasaEngine.EditorServices/ScreenEditor/Factory/UIScreenNodeFactory.cs:29-50`) crée `new UIScreenNode(entry.ControlType)` avec les seules propriétés par défaut, sans `Name`. `WrapRootAndInsert` (`:101-111`) crée un `StackPanel` sans nom.
  - `AddNodeCommand.Execute` (`Commands/AddNodeCommand.cs:39-53`) appelle la fabrique à chaque exécution, y compris au rétablissement (redo).
  - Chemin de la palette : `UIScreenToolboxPanel.cs:122` → `GameEditor.cs:1952-1981` (`OnToolboxControlRequested`) → `AddNodeCommand`.
- **Duplication et collage** :
  - `DuplicateNodeCommand.Execute` (`Commands/DuplicateNodeCommand.cs:29-51`) insère `source.DeepClone()`. `UIScreenNode.DeepClone` (`DocumentModel/UIScreenNode.cs:163-178`) copie `Name`, donc la copie porte le nom de l'original. Une racine dupliquée est enveloppée dans un `MGStackPanel` sans nom (`:46`).
  - Collage : `GameEditor.cs:2073-2093` relit le XAML du presse-papiers, en prend un `DeepClone` et le passe à `PasteNodeCommand` (`Commands/PasteNodeCommand.cs:31-53`), qui l'insère tel quel. Une racine est enveloppée dans un `MGStackPanel` sans nom (`:44`).
- **Conséquence au runtime d'un nom en double** :
  - MGUI indexe les noms par fenêtre : `MGWindow.IndexElementName` (`MGUI/MGUI.Core/UI/MGWindow.cs:2115-2125`) lève `MGDuplicateElementNameException` sur un nom déjà présent. Le dictionnaire `ElementsByName` est créé par `new()` (`:1705`), donc comparé en ordinal.
  - Le chargeur d'écrans le rapporte en diagnostic `DuplicateElementName` (`CasaEngine.Tests/UI/UIScreenLoaderTests.cs:148`).
- **Stockage** : `UIScreenNode.Name` (`DocumentModel/UIScreenNode.cs:32`). Le sérialiseur n'écrit l'attribut `Name` que s'il n'est pas vide (`Xaml/UIScreenXamlSerializer.cs:62-64`, `:466-468`). `RenameNodeCommand` (`Commands/RenameNodeCommand.cs:16-28`) rogne le nom, et un nom vide devient `null`. `UIScreenDocument` ne trouve un nœud que par id (`DocumentModel/UIScreenDocument.cs:89`) : il n'y a pas d'énumération de tous les nœuds, on parcourt `Root` et `Children`.
- **Palette** : `UIControlRegistry.CreateDefault` (`ScreenEditor/Toolbox/UIControlRegistry.cs:43-100`), 17 types :
  - Layout : StackPanel, DockPanel, Grid, UniformGrid, ScrollViewer, Border, Expander ;
  - Controls : Button, TextBlock, TextBox, CheckBox, RadioButton, ComboBox, ListBox, Image ;
  - Input : Slider, ProgressBar.
  Aucune table type → préfixe n'existe.
- **Convention** (`docs/editor/ui-screen-editor/screen-authoring-conventions.md:64-72`) :
  - six préfixes : `btn`, `lbl` (TextBlock), `inp`, `lst`, `pnl`, `img` ;
  - le texte dit « two-letter type hint » alors que les exemples ont trois lettres ;
  - la ligne 72 dit qu'un contrôle non référencé dans le code n'a pas besoin de `Name`.
- **Composants** :
  - Nom par défaut : `ObjectBase` donne `Name = "Object " + Id` (`CasaEngine/Framework/Common/ObjectBase.cs:19-23`, `:31-35`). Le constructeur de copie copie `Name` (`:37-41`).
  - Dialogue « Add component » : `Activator.CreateInstance` (`CasaEngine.Editor/Controls/EntityDetailsPanel.cs:1077`), puis `AddComponent` (`:1101-1126`), sans nom.
  - Dépôt d'un modèle : `WorldViewportPanel.CreateStaticModelEntity` (`CasaEngine.Editor/Controls/WorldViewportPanel.cs:1072-1088`) nomme l'entité d'après le fichier ; le `StaticModelComponent` n'a pas de nom.
  - Le composant de particules reçoit déjà le nom « Particle System » (`CasaEngine.EditorServices/Particles/EditorParticleSystemComponentService.cs:15`).
- **Clone d'un composant** :
  - `EntityComponent(EntityComponent other)` (`CasaEngine/Framework/Scene/Entities/Components/EntityComponent.cs:31-34`) n'appelle pas `ObjectBase(ObjectBase other)`, le seul constructeur qui copie `Name` (`ObjectBase.cs:37-41`). Il passe par `ObjectBase()`, qui donne `Object <guid>`. `SceneComponent(SceneComponent other) : base(other)` (`SceneComponent.cs:297`) passe par lui ;
  - d'autres `Clone()` construisent un objet neuf sans le copier : 34 surcharges dans le moteur, sous l'une ou l'autre forme ;
  - un composant n'est cloné qu'à trois endroits (`rg "\.Clone\(\)"` dans le moteur, l'éditeur, les services et RacingGame) : `Entity(Entity)` pour la racine (`Entity.cs:171`) et pour `Components` (`:178`), et `SceneComponent(SceneComponent)` pour les enfants (`SceneComponent.cs:303`). `Entity(Entity) : base(entity)` garde le nom de l'entité.
  - Ces clones servent à la duplication d'une entité dans l'éditeur (`EntitiesPanel.cs:780-791`, `entity.Clone()`) et aux entités instanciées depuis un asset (`World.cs:156`, `:163`).
- **Libellé d'un composant** : `GetComponentLabel` (`EntityDetailsPanel.cs:1267-1280`) affiche le nom d'affichage du type (`DisplayNameAttribute`, sinon le nom du type, `:1261-1265`). Le nom d'instance est ajouté entre crochets, sauf s'il est vide, égal au nom d'affichage, ou commence par `Object `.
- **API des entités** : `Entity.Components` (`CasaEngine/Framework/Scene/Entities/Entity.cs:42`), `Entity.RootComponent` (`:66`), `SceneComponent.Children` (`Components/SceneComponent.cs:30`).
- **Modèle de nom unique** : `EntitiesPanel.GenerateUniqueEntityName` (`CasaEngine.Editor/Controls/EntitiesPanel.cs:943-965`) garde le nom s'il est libre, sinon ajoute ` (n)` avec n de 2 à 9999, sans tenir compte de la casse.
- **Règle par chemin `editor-mgui`** : la logique d'édition va dans `CasaEngine.EditorServices`, l'UI dans `CasaEngine.Editor`.
- **Tests existants** :
  - `CasaEngine.Tests/ScreenEditor/UIScreenDocumentModelTests.cs` (création de nœud) ;
  - `CasaEngine.Tests/Editor/AddComponentListTests.cs` ;
  - aucun test des commandes d'ajout, de duplication, de collage ou de renommage de nœud.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Convention de nommage des éléments d'écran : celle de CasaEngine (`btn`, `lbl` pour les TextBlock, `img`, `pnl`, `inp`, `lst`). Elle s'applique aussi au contenu de RacingGameCasaEngine (plan RacingGame). |
| D2 | L'éditeur donne un nom unique par défaut, à sa création, à un élément d'écran (exemple de l'auteur : `img1`) et à un composant (exemple : `StaticModelComponent`), au lieu de rien ou de `Object <guid>`. |
| D3 | Pas d'avertissement du validateur pour les éléments sans nom ou en double (option non retenue par l'auteur). |

## Propositions à valider avec le plan

| Réf | Proposition |
|---|---|
| P1 | **Table des préfixes** (complète la convention, D1) :<br>- `btn` Button ; `lbl` TextBlock ; `img` Image ; `inp` TextBox ; `lst` ListBox ;<br>- `cmb` ComboBox ; `chk` CheckBox ; `rdo` RadioButton ; `sld` Slider ; `prg` ProgressBar ; `wnd` Window ;<br>- `pnl` pour les panneaux et conteneurs : StackPanel, DockPanel, Grid, UniformGrid, ScrollViewer, Border, Expander, OverlayPanel.<br>Le type est lu avec ou sans le préfixe `MG` (`MGStackPanel` comme `StackPanel`). Un type absent de la table prend son nom sans `MG`, première lettre en minuscule (`contextMenu`). |
| P2 | **Nom d'un élément neuf** : préfixe suivi du plus petit entier n ≥ 1 libre dans le document (`img1`, `img2`, `btn1`). La comparaison est ordinale, comme l'index de `MGWindow`. Le conteneur qui enveloppe la racine (ajout, duplication, collage) reçoit aussi un nom `pnl<n>`. |
| P3 | **Duplication et collage** : chaque nœud du sous-arbre inséré est traité dans l'ordre du document.<br>- S'il a un nom libre dans le document (en comptant ceux déjà donnés dans le sous-arbre), il le garde.<br>- Sinon, on retire ses chiffres de fin et on ajoute le plus petit n ≥ 2 libre : `btnPlay` → `btnPlay2`, `lblStat0` → `lblStat6` quand `lblStat0` à `lblStat5` existent.<br>- Un nœud sans nom reçoit un nom selon P2. |
| P4 | **Nom d'un composant neuf** : le nom de son type (exemple de D2), rendu unique dans l'entité comme `GenerateUniqueEntityName` : ` (n)` avec n ≥ 2, sans tenir compte de la casse. Les noms comptés sont ceux de l'arbre de `RootComponent` (`Children`, récursivement) et de `Components`. On l'applique au dialogue « Add component » et au dépôt d'un modèle. Le nom « Particle System » des particules ne change pas. |
| P5 | **Libellé** : `GetComponentLabel` masque aussi un nom d'instance égal au nom du type. Un composant nommé par défaut s'affiche donc comme aujourd'hui, par exemple « Race Road Generator » et non « Race Road Generator [RaceRoadGeneratorComponent] ». |
| P6 | **Emplacement** : deux classes statiques pures dans `CasaEngine.EditorServices` (règle `editor-mgui`), couvertes par des tests.<br>- `ScreenEditor/Naming/UIScreenNodeNames.cs` : préfixe d'un type, nom neuf (P2), noms d'un sous-arbre inséré (P3).<br>- `EditorComponentNames.cs` : nom unique d'un composant dans une entité (P4).<br>`GetComponentLabel` passe en `internal static` pour son test (`CasaEngine.Tests` voit déjà les membres internes de l'éditeur : `EditorCameraFactoryTests`). |
| P7 | **Branche** : `chantier/rgce-editor-project`, la branche que suit le pointeur de RacingGame, comme le chantier de la distance de vue infinie (`ai-agent/tasks/archive/editor-infinite-view-distance-tasks.md`). |
| P8 | **Un composant cloné avec son entité garde son nom.** C'est la condition pour que les noms de D2 survivent à une duplication. Méthode `internal EntityComponent CloneWithName()` dans `EntityComponent` : elle appelle `Clone()` puis copie `Name`. Les trois sites de clonage (`Entity.cs:171`, `:178`, `SceneComponent.cs:303`) l'appellent. Les 34 `Clone()` des composants ne changent pas. Le constructeur de copie d'`EntityComponent` ne change pas non plus. L'id du clone reste neuf. |

## Règles d'exécution pour l'agent

- **Branche `chantier/rgce-editor-project`** (P7). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, ou vient d'une réponse de l'auteur. Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** (`dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`) et **tests** (`dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`) avant de passer une tâche de code en ✅.
- L'éditeur ne se pilote pas au clavier ni à la souris simulés : un geste qui demande un clic reste 🧪 pour l'auteur, avec ce qu'il faut regarder.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent sans erreur.
- `CasaEngine.Tests` est entièrement vert, nouveaux tests compris.
- Le plan RacingGame (T3.1) ouvre les écrans et le monde de test de RacingGameCasaEngine dans l'éditeur après la mise à jour du pointeur.
- Gestes à vérifier par l'auteur, dans l'éditeur :
  - un ajout depuis la palette affiche « Image img1 » dans la hiérarchie ;
  - une duplication d'un bouton nommé `btnPlay` donne `btnPlay2` ;
  - le collage d'un sous-arbre déjà présent renomme ses éléments ;
  - « Add component » nomme le composant d'après son type ;
  - un modèle déposé dans le viewport a un composant `StaticModelComponent` ;
  - la duplication d'une entité garde les noms de ses composants.

---

## Phase 1 — Noms par défaut

### 🧪 T1.1 — Noms des éléments d'écran ajoutés, enveloppés, dupliqués et collés

- Objectif : P1 à P3, dans les services d'édition, avec les commandes qui créent des nœuds.
- Fichiers :
  - `CasaEngine.EditorServices/ScreenEditor/Naming/UIScreenNodeNames.cs` (nouveau) ;
  - `CasaEngine.EditorServices/ScreenEditor/Factory/UIScreenNodeFactory.cs` ;
  - `CasaEngine.EditorServices/ScreenEditor/Commands/DuplicateNodeCommand.cs` ;
  - `CasaEngine.EditorServices/ScreenEditor/Commands/PasteNodeCommand.cs` ;
  - `CasaEngine.Tests/ScreenEditor/UIScreenNodeNamesTests.cs` (nouveau) ;
  - `CasaEngine.Tests/ScreenEditor/UIScreenNodeCreationNamesTests.cs` (nouveau).
- Étapes :
  1. `UIScreenNodeNames` :
     - `GetPrefix(string controlType)` (P1) ;
     - `CreateDefaultName(UIScreenDocument document, string controlType, IReadOnlySet<string>? reserved = null)` (P2), où `reserved` contient des noms pas encore insérés ;
     - `AssignUniqueNames(UIScreenDocument document, UIScreenNode subtreeRoot)` (P3), appelée avant l'insertion du sous-arbre.
     Les noms du document se lisent en parcourant `Root` et `Children`.
  2. `UIScreenNodeFactory.Create` donne au nœud neuf un nom (P2) avant `InsertNode`. `WrapRootAndInsert` nomme le conteneur **avant** `document.ClearRoot()`, contre les noms du document, avec le nom du nœud neuf dans `reserved`.
  3. `DuplicateNodeCommand.Execute` appelle `AssignUniqueNames` sur la copie avant de l'insérer. `PasteNodeCommand.Execute` fait de même pour `_nodeToInsert`.
  4. **Nom de l'enveloppe** dans ces deux commandes (racine dupliquée, ou collage sans cible sur un document qui a une racine). Le `MGStackPanel` est nommé par `CreateDefaultName` :
     - **avant** `_document.SetRoot(wrapper)` (`DuplicateNodeCommand.cs:44-49`, `PasteNodeCommand.cs:40-47`), puisqu'après cet appel le document n'a plus de nœud nommé ;
     - avec dans `reserved` tous les noms que `AssignUniqueNames` vient de donner au sous-arbre inséré.
     Sans cela, dupliquer une racine `pnl1` donne une copie `pnl2` et une enveloppe `pnl2`.
  5. Annuler puis rétablir redonne les mêmes noms : `AddNodeCommand` et `DuplicateNodeCommand` recréent leur nœud dans le même état du document ; le nœud collé garde les noms déjà donnés, puisqu'ils sont de nouveau libres.
- Validation :
  - build des deux solutions ; `CasaEngine.Tests` vert ;
  - tests de `UIScreenNodeNames` :
    - préfixes de P1, avec et sans `MG`, et repli d'un type inconnu ;
    - premier nom `img1`, puis `img2` ; un trou est réutilisé (`img1` libre quand `img2` existe) ;
    - les noms réservés sont respectés ;
  - tests des commandes :
    - ajout d'une Image puis d'un Button dans un document : `img1`, `btn1` ;
    - ajout sur une racine feuille : le conteneur d'enveloppe a un nom `pnl<n>` ;
    - duplication de `btnPlay` avec un enfant `pnlPlayFace` : `btnPlay2` et `pnlPlayFace2` ;
    - duplication de `lblStat0` quand `lblStat0` à `lblStat5` existent : `lblStat6` ;
    - collage deux fois du même sous-arbre `pnlPlaySlot` > `btnPlay` après le nœud sélectionné : tous les noms du document restent distincts ;
    - **enveloppes** (étape 4), chacun vérifiant que tous les `Name` sous `Root` sont distincts, enveloppe comprise, après l'exécution puis après annuler et rétablir :
      - duplication d'une racine `pnl1` qui a un enfant `btn1` ;
      - collage, sans sélection, d'une copie du sous-arbre `pnl1` > `btn1` dans un document dont la racine est ce `pnl1` ;
      - ajout d'une Image sur une racine feuille `img1` ;
      sans la réservation de l'étape 4, les deux premiers échouent (copie et enveloppe toutes deux `pnl2`) ;
    - annuler puis rétablir chaque commande redonne les mêmes noms ;
  - un document dupliqué puis sérialisé par `UIScreenXamlSerializer` se charge sans diagnostic `DuplicateElementName`, par le même chemin de chargement que `UIScreenLoaderTests.cs:140-160`. Ce test échoue sans l'étape 3.
- Commit : `feat(editor): give new, duplicated and pasted screen elements unique names`

> Validation (2026-10-09) :
> - `dotnet build CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
> - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 4398 réussis, 0 échec (dont les 41 nouveaux tests de `UIScreenNodeNamesTests` et `UIScreenNodeCreationNamesTests`).
> - Preuve par l'absurde, réservation de l'étape 4 retirée : exactement `Duplicate_OfTheRoot_NamesTheWrapper_AndKeepsEveryNameDistinct` et `Paste_WithoutTarget_OnAnExistingRoot_NamesTheWrapper_AndKeepsEveryNameDistinct` échouent (copie et enveloppe toutes deux `pnl2`).
> - Preuve par l'absurde, `AssignUniqueNames` retiré de `DuplicateNodeCommand` : `ADuplicatedDocument_Serialized_LoadsWithoutADuplicateElementName` échoue (`XamlLoaderException`, nom `btnPlay` en double), par `UIScreenLoader.Load` comme `UIScreenLoaderTests.cs:140-160`.
> - `AssignUniqueNames` renvoie l'ensemble des noms du sous-arbre, pour les réserver à l'enveloppe (étape 4). La ligne du plan dans `ai-agent/README.md` est incluse dans le commit.

### 🧪 T1.2 — Noms des composants ajoutés ou déposés

- Objectif : P4 et P5.
- Fichiers :
  - `CasaEngine.EditorServices/EditorComponentNames.cs` (nouveau) ;
  - `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` ;
  - `CasaEngine.Editor/Controls/WorldViewportPanel.cs` ;
  - `CasaEngine.Tests/Editor/EditorComponentNamesTests.cs` (nouveau).
- Étapes :
  1. `EditorComponentNames.CreateUniqueName(Entity entity, EntityComponent component)` (P4).
  2. Dialogue « Add component » : après `Activator.CreateInstance` (`EntityDetailsPanel.cs:1077`), le composant reçoit ce nom avant `AddComponent`. Annuler puis rétablir garde le même objet, donc le même nom.
  3. `WorldViewportPanel.CreateStaticModelEntity` nomme le `StaticModelComponent` de la même façon.
  4. `GetComponentLabel` masque un nom égal au nom du type (P5) et passe en `internal static`.
- Validation :
  - build des deux solutions ; `CasaEngine.Tests` vert ;
  - tests de `EditorComponentNames` :
    - entité vide : nom du type ;
    - deuxième composant du même type, en racine, en enfant de la racine ou dans `Components` : ` (2)`, puis ` (3)` ;
    - un nom existant écrit dans une autre casse compte comme pris ;
  - tests de `GetComponentLabel` :
    - nom égal au type sur un type avec `DisplayName` : nom d'affichage seul ;
    - nom ` (2)` : nom d'affichage puis le nom entre crochets ;
    - `Object <guid>` : nom d'affichage seul, comme aujourd'hui.
- Commit : `feat(editor): name the components the editor adds after their type`

> Validation (2026-10-09) :
> - `dotnet build CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
> - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 4406 réussis, 0 échec (dont les 8 nouveaux tests de `EditorComponentNamesTests` : cinq pour `CreateUniqueName`, trois pour `GetComponentLabel`).
> - Geste à vérifier par l'auteur (O1) : « Add component » nomme le composant d'après son type, et un modèle déposé dans le viewport a un composant `StaticModelComponent`.

### 🧪 T1.3 — Un composant cloné avec son entité garde son nom

- Objectif : P8, dans le runtime (`CasaEngine`).
- Fichiers :
  - `CasaEngine/Framework/Scene/Entities/Components/EntityComponent.cs` ;
  - `CasaEngine/Framework/Scene/Entities/Entity.cs` ;
  - `CasaEngine/Framework/Scene/Entities/Components/SceneComponent.cs` ;
  - `CasaEngine.Tests/Scene/ComponentCloneNameTests.cs` (nouveau).
- Étapes :
  1. `EntityComponent.CloneWithName()` (P8).
  2. Les trois sites l'appellent à la place de `Clone()`.
- Validation :
  - build des deux solutions ; `CasaEngine.Tests` vert ;
  - test : une entité avec une racine nommée, un enfant nommé de la racine et un composant nommé dans `Components`. Deux types de composant définis dans le test : un dont le `Clone()` passe par le constructeur de copie (comme `CameraLookAtComponent.cs:32-35`), et un dont le `Clone()` construit un objet neuf (`new X()`, comme `LegacyCarVisualComponent` de RacingGame).
    - Après `entity.Clone()`, les trois clones portent les mêmes noms et des ids neufs.
    - Sans l'étape 2, le test échoue (`Object <guid>`).
- Commit : `fix(scene): keep the component names when an entity is cloned`

> Validation (2026-10-09) :
> - `dotnet build CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
> - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 4407 réussis, 0 échec (dont le nouveau test `ComponentCloneNameTests`).
> - Étape 2 annulée temporairement : le test échoue (`Expected: "Root"`, `Actual: "Object 9fe23912-..."`) ; étape 2 rétablie, le test passe.

### ✅ T1.4 — Convention, ADR et index

- Objectif : la convention et la décision sont écrites.
- Fichiers :
  - `docs/editor/ui-screen-editor/screen-authoring-conventions.md` (section 2) ;
  - `docs/decisions/0122-the-editor-names-the-elements-and-components-it-creates.md` (nouveau, skill `adr`) ;
  - `docs/decisions/README.md` ;
  - `ai-agent/README.md` ;
  - ce plan, archivé dans `ai-agent/tasks/archive/` s'il ne reste que des 🧪.
- Étapes :
  1. Convention :
     - la table de P1 remplace la liste des six préfixes ; « three-letter type hint » remplace « two-letter » ;
     - la ligne 72 devient : chaque élément a un `Name` unique dans l'écran ; l'éditeur en donne un à la création, à la duplication et au collage (P2, P3) ;
     - « Decisions: see ADR-0122 ».
  2. ADR-0122 : D1 à D3, P1 à P5 et P8, avec le contexte (nom en double après une duplication, `Object <guid>`, nom perdu au clonage).
  3. Ligne du tableau de `ai-agent/README.md`.
- Validation : les liens de la convention, de l'ADR et des index pointent vers des fichiers existants (`rg`) ; relecture du diff.
- Commit : `docs(adr): record the default names the editor gives`

> Validation (2026-10-09) :
> - Docs seulement, pas de build. ADR-0122 écrite d'après `docs/decisions/template.md` (D1 à D3, P1 à P5, P8) ; index `docs/decisions/README.md`, section 2 de la convention et ligne du plan dans `ai-agent/README.md` mis à jour.
> - Liens vérifiés par `rg` : l'ADR-0122, son index et la convention pointent vers des fichiers existants.
> - Écart : le plan n'est pas archivé par cette tâche. Il reste dans `tasks/` tant que les gestes de l'auteur (O1) attendent, ce qui garde valides le lien de la source de l'ADR-0122 et celui de l'index.
>
> Statuts corrigés à la clôture : T1.1 à T1.3 en 🧪, car chacune attend un geste de l'auteur dans l'éditeur (O1) ; T1.4 en ✅, car la documentation est complète.

> **Vérification de clôture (2026-10-09)**, après T1.4. Workflow : trois relectures en lecture seule et un vérificateur qui lance les commandes.
> - Vérificateur : **CONFIRMED**.
>   - Les deux solutions buildent sans erreur. `CasaEngine.Tests` passe à 4407/4407, et encore après la restauration des mutations.
>   - Quatre mutations, chacune détectée :
>     - (a) `AssignUniqueNames` garde tous les noms : 9 tests échouent ;
>     - (b) sans la réservation du nom de l'enveloppe : les 2 tests d'enveloppe échouent ;
>     - (c) `EditorComponentNames` ignore les noms pris : 4 tests échouent ;
>     - (d) `Clone()` aux trois sites de clonage : le test de clonage échoue, et chaque site retiré seul est détecté.
>   - Les valeurs attendues du plan sont vérifiées dans les tests.
> - Relectures : aucun P0 à P2. Avis traités :
>   - les statuts des tâches, corrigés ;
>   - le lien de la source de l'ADR, qui reste valide puisque le plan reste dans `tasks/` ;
>   - O2, O3 et O4, reportés dans les points ouverts.
> - Commits : `3dbe7c0f6`, `cf5169c9a`, `0693ab321`, `1d7a46e45`.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Vérifications de l'auteur dans l'éditeur (gestes de la validation globale) : T1.1 ajout depuis la palette, duplication, collage ; T1.2 « Add component » et dépôt d'un modèle ; T1.3 duplication d'une entité. | T1.1, T1.2, T1.3 |
| O2 | ✅ **Corrigé le 2026-10-09** (commit `fix(editor): wrap a duplicated or pasted root in a StackPanel`). Préexistant, hors périmètre (avis P4 de la vérification) : la racine dupliquée, ou un collage sans cible sur une racine existante, était enveloppée dans un nœud de type `MGStackPanel` (`DuplicateNodeCommand.cs:49`, `PasteNodeCommand.cs:47`, depuis `fe27e3ae4`). Ce type est inconnu du XAML de MGUI, qui ne connaît que `StackPanel` (`MGUI/MGUI.Core/UI/XAML/Containers.cs:525`).<br>- Défaut confirmé avant le correctif : le document est sérialisé par `UIScreenXamlSerializer`, placé dans une `Window` comme le fait l'aperçu, puis chargé par `UIScreenLoader.Load` en mode strict, le chemin de `UIScreenLoaderTests.cs:140-160`. Le chargement lève `XamlLoaderException` : « Unknown XAML element 'MGStackPanel' ».<br>- Correctif : les deux commandes créent un `StackPanel`, comme la fabrique (`UIScreenNodeFactory.WrapRootAndInsert`).<br>- Tests de non-régression : `CasaEngine.Tests/ScreenEditor/UIScreenRootWrapperTests.cs`, un pour la duplication de la racine, un pour le collage sans cible. | — |
| O3 | Avis P3 de la vérification, reporté. `EditorComponentNames.CreateUniqueName` compte l'arbre de la racine et les éléments de `Components`, comme P4 le dit, mais pas les enfants d'un `SceneComponent` posé dans `Components`. Un composant ajouté comme enfant d'un tel composant peut donc reprendre un nom déjà pris dans l'entité. C'est cosmétique : libellé ambigu, nom en double enregistré. Correctif : parcourir aussi ces enfants, avec un test. | T1.2 |
| O4 | Avis P4 reporté : il manque une assertion annuler puis rétablir sur l'ajout simple (`AddNodeCommand` sans enveloppe). Le code est correct, car le nom est recalculé dans le même état du document. | T1.1 |
| O5 | Préexistant, hors périmètre, trouvé en traitant O2. Tous les écrans de RacingGameCasaEngine ont une racine `Window`, et `UIScreenLoader` n'accepte qu'une racine `Window` (`XAMLParser.LoadRootWindow`). Or, sur une racine `Window`, deux gestes mettent la `Window` dans un `StackPanel` qui devient la racine : dupliquer la racine (deux `Window` dans le `StackPanel`) et coller sans sélection. L'écran enregistré ne se charge plus (`XamlLoaderException` : « Unable to cast object of type 'MGUI.Core.UI.XAML.StackPanel' to type 'MGUI.Core.UI.XAML.Window' »). Vérifié le 2026-10-09 par un test jetable : sérialisation, puis `UIScreenLoader.Load`.<br>- **Duplication : 🧪 tranchée par l'auteur le 2026-10-09, « refuse la duplication d'une racine Window »**. Faite par le commit `fix(editor): refuse to duplicate a Window root`.<br>&nbsp;&nbsp;- `DuplicateNodeCommand.CanDuplicate` répond non pour une racine `Window`, et `Execute` ne change alors rien.<br>&nbsp;&nbsp;- Dans l'éditeur, l'entrée « Duplicate » du menu contextuel de la hiérarchie est désactivée sur cette racine. Ctrl+D et Edit > Duplicate ne font rien et n'ajoutent pas d'étape à l'historique.<br>&nbsp;&nbsp;- Test : `UIScreenRootWrapperTests.Duplicate_OfAWindowRoot_IsRefused_SoTheScreenStillLoads`.<br>&nbsp;&nbsp;- Geste à vérifier par l'auteur : clic droit sur la racine `Window` d'un écran, « Duplicate » grisé ; Ctrl+D sur cette racine sélectionnée, rien ne change.<br>- **Collage sans sélection sur une racine `Window` : reste à trancher.** Choix possible : coller dans le contenu de la `Window`. | — |

## Hors périmètre

- L'avertissement du validateur pour un élément sans nom ou en double (D3).
- Le nom par défaut `Object <guid>` du moteur (`ObjectBase`) : seul l'éditeur nomme ce qu'il crée (D2). Les composants créés par le code d'un jeu sont nommés par ce code (plan RacingGame).
- Les 34 surcharges de `Clone()` et le constructeur de copie d'`EntityComponent` : P8 garde le nom aux trois sites de clonage sans les toucher.
- Le renommage d'un composant dans l'inspecteur : il n'existe pas aujourd'hui (seule l'entité se renomme, `EntityDetailsPanel.cs:340-367`).
- Les noms des éléments et composants déjà enregistrés dans les écrans et mondes existants : le plan RacingGame traite ceux de RacingGameCasaEngine.
