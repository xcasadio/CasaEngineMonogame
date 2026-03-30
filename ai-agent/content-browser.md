# Content Browser — Plan de tâches pour agent IA

## Contexte

Créer un **Content Browser** (navigateur de contenu) similaire à Unity ou Unreal Engine, en utilisant les contrôles existants de MGUI. Le Content Browser sera implémenté dans le moteur de jeu, pas dans MGUI lui-même.

### Contrôles MGUI disponibles et leur rôle

| Composant du Content Browser | Contrôle(s) MGUI | Notes |
|------------------------------|-------------------|-------|
| Arborescence de dossiers (panneau gauche) | `MGTreeView` / `MGTreeViewItem` | Supporte hiérarchie, expand/collapse, sélection, data binding, headers custom (`MGElement`) |
| Vue en grille/tuiles (thumbnails) | `MGUniformGrid` + `MGImage` + `MGTextBlock` | Grille à cellules fixes, idéal pour les miniatures |
| Vue en liste détaillée | `MGListView<T>` | Colonnes dynamiques, templates custom par cellule, sélection par ligne |
| Vue liste simple | `MGListBox<T>` | Multi-sélection (Single/Contiguous/Multiple), virtualisation intégrée, templates custom |
| Séparation redimensionnable gauche/droite | `MGGrid` + `MGGridSplitter` ou `MGDockSplitContainer` | Splitter draggable entre les deux panneaux |
| Barre de recherche/filtre | `MGTextBox` | Événement `TextChanged`, placeholder text |
| Menus contextuels (clic droit) | `MGContextMenu` / `MGContextMenuItem` | Icônes, sous-menus, séparateurs, items dynamiques via `ItemsFactory` |
| Infobulles de prévisualisation | `MGToolTip` | Peut contenir n'importe quel contenu (image + métadonnées) |
| Sélecteur de mode de vue | `MGComboBox<T>` ou `MGToggleButton` | Basculer entre grille/liste/détails |
| Barre d'outils | `MGMenuBar` ou `MGStackPanel` horizontal + `MGButton` | Boutons d'actions rapides |
| Barre de chemin (breadcrumb) | `MGStackPanel` horizontal + `MGButton` + `MGSeparator` | **À composer manuellement** — pas de contrôle breadcrumb natif |
| Layout principal | `MGDockPanel` | Dock.Top = toolbar/search, Dock.Left = tree, Fill = contenu |
| Panneau dockable | `MGDockHost` + système de docking complet | Optionnel : pour un éditeur avec panneaux flottants/ancrés |

### Prérequis MGUI

> **Important** : Avant de commencer ce plan, les tâches décrites dans [`update-mgui.md`](update-mgui.md) doivent être complétées.
> Elles ajoutent à MGUI : le **framework drag-and-drop**, la **navigation clavier** (TreeView/ListBox/ListView), et le **tri des colonnes** dans ListView.

### Ce qui reste à composer manuellement (dans le moteur de jeu)

1. **Breadcrumb bar** — composer avec `MGStackPanel` + `MGButton`
2. **Renommage en place** — overlay d'un `MGTextBox` sur l'item sélectionné
3. **WrapPanel** — inexistant dans MGUI, utiliser `MGUniformGrid` comme alternative pour la vue tuiles

---

## Tâches

> **Règle : l'agent doit commiter après chaque tâche complétée.**

**Légende** : ✅ terminé, 🟡 partiel, ⬜ non commencé

### ✅ Tâche 1 — Structure du projet et modèle de données

**Objectif** : Créer la structure de fichiers du Content Browser et le modèle de données représentant les assets.

**Actions** :
1. Créer un dossier `ContentBrowser/` dans le projet du moteur de jeu
2. Créer les fichiers suivants :
   - `ContentBrowser/Models/ContentItem.cs` — Classe de base représentant un élément (fichier ou dossier)
     - Propriétés : `Name`, `FullPath`, `Extension`, `IsDirectory`, `Icon` (Texture2D), `Thumbnail` (Texture2D), `Size` (long), `LastModified` (DateTime), `Parent` (ContentItem), `Children` (ObservableCollection<ContentItem>)
     - Implémenter `INotifyPropertyChanged`
   - `ContentBrowser/Models/ContentItemType.cs` — Enum des types d'assets reconnus (Texture, Model, Sound, Script, Scene, Shader, Font, Material, Prefab, Unknown)
     - Propriété `ContentItemType Type` dans `ContentItem` déduite de l'extension
   - `ContentBrowser/Services/FileSystemScanner.cs` — Service qui scanne un répertoire et construit l'arbre de `ContentItem`
     - Méthode `ContentItem ScanDirectory(string rootPath)`
     - Méthode `void Refresh(ContentItem directory)` pour mise à jour incrémentale
3. Écrire des tests unitaires pour `ContentItem` et `FileSystemScanner`

**Commit** : `feat(content-browser): add data models and file system scanner`

---

### ✅ Tâche 2 — Layout principal et panneau d'arborescence

**Objectif** : Construire le layout de base avec le panneau d'arborescence de dossiers à gauche.

**Actions** :
1. Créer `ContentBrowser/ContentBrowserPanel.cs` — Classe principale du Content Browser
2. Construire le layout avec `MGDockPanel` :
   - **Top** : Réserver un espace pour la toolbar (vide pour l'instant)
   - **Left** : `MGTreeView` pour l'arborescence de dossiers
   - **Fill** : Panneau de contenu (vide pour l'instant, placeholder)
3. Séparer les panneaux gauche/droite avec `MGGrid` (2 colonnes) + `MGGridSplitter` pour le redimensionnement
4. Peupler le `MGTreeView` avec les données de `FileSystemScanner` :
   - Chaque `MGTreeViewItem` affiche un `MGDockPanel` (header) contenant une `MGImage` (icône dossier) + `MGTextBlock` (nom du dossier)
   - Filtrer pour n'afficher que les dossiers (pas les fichiers)
   - Utiliser `ItemsSource` et `ChildrenPropertyName` pour le binding hiérarchique
5. Gérer l'événement `SelectionChanged` du TreeView pour mettre à jour le panneau de contenu

**Commit** : `feat(content-browser): add main layout with folder tree panel`

---

### ✅ Tâche 3 — Vue en grille (thumbnails)

**Objectif** : Afficher les fichiers du dossier sélectionné sous forme de grille de miniatures.

**Actions** :
1. Créer `ContentBrowser/Views/GridView.cs` — Vue en grille utilisant `MGUniformGrid`
2. Pour chaque fichier du dossier sélectionné, créer une cellule contenant :
   - `MGBorder` comme conteneur avec padding
   - `MGStackPanel` vertical contenant :
     - `MGImage` (thumbnail ou icône par défaut selon le type) avec `Stretch.Uniform`
     - `MGTextBlock` (nom du fichier, tronqué si trop long) centré
3. Supporter la sélection :
   - Clic simple = sélectionner (highlight visuel via changement de background du `MGBorder`)
   - Ctrl+Clic = multi-sélection
   - Gérer l'état sélectionné manuellement (liste de `ContentItem` sélectionnés)
4. Connecter au TreeView : quand un dossier est sélectionné dans l'arbre, reconstruire la grille avec le contenu du dossier
5. Double-clic sur un dossier dans la grille = naviguer dedans (mettre à jour l'arbre + la grille)
6. Double-clic sur un fichier = événement `FileOpened` (à connecter par le moteur)

**Commit** : `feat(content-browser): add thumbnail grid view`

---

### ✅ Tâche 4 — Vue en liste détaillée

**Objectif** : Ajouter une vue alternative affichant les fichiers en colonnes détaillées.

**Actions** :
1. Créer `ContentBrowser/Views/DetailView.cs` — Vue détaillée utilisant `MGListView<ContentItem>`
2. Définir les colonnes avec `SortKeySelector` pour chaque colonne triable :
   - **Icône** : Petite icône du type de fichier (`MGImage` dans le template) — non triable
   - **Nom** : `MGTextBlock` — `SortKeySelector = item => item.Name`
   - **Type** : Extension ou type lisible — `SortKeySelector = item => item.Type.ToString()`
   - **Taille** : Taille formatée (Ko, Mo, Go) — `SortKeySelector = item => item.Size`
   - **Date de modification** : `LastModified.ToString("yyyy-MM-dd HH:mm")` — `SortKeySelector = item => item.LastModified`
3. Configurer `GridSelectionMode.Row` pour la sélection par ligne
4. Le tri au clic sur les headers est géré nativement par MGUI (voir `update-mgui.md` tâches 6-7)

**Commit** : `feat(content-browser): add detail list view`

---

### ✅ Tâche 5 — Basculement entre les vues et sélecteur de mode

**Objectif** : Permettre à l'utilisateur de basculer entre la vue grille et la vue liste.

**Actions** :
1. Créer `ContentBrowser/Views/IContentView.cs` — Interface commune :
   - `MGElement RootElement { get; }` — L'élément UI racine de la vue
   - `void SetItems(IReadOnlyList<ContentItem> items)` — Mettre à jour les items affichés
   - `IReadOnlyList<ContentItem> SelectedItems { get; }`
   - `event Action<ContentItem> FileDoubleClicked`
   - `event Action<ContentItem> DirectoryDoubleClicked`
   - `void ClearSelection()`
2. Faire implémenter `IContentView` par `GridView` et `DetailView`
3. Ajouter un `MGComboBox<string>` ou 2 `MGToggleButton` dans la toolbar pour basculer entre "Grille" et "Détails"
4. Quand le mode change :
   - Remplacer la vue dans le panneau de contenu
   - Conserver la sélection actuelle si possible
   - Conserver le dossier actuel

**Commit** : `feat(content-browser): add view mode switching (grid/detail)`

---

### ✅ Tâche 6 — Barre d'outils et barre de recherche

**Objectif** : Ajouter la barre d'outils en haut avec recherche, navigation et actions.

**Actions** :
1. Créer `ContentBrowser/Controls/Toolbar.cs`
2. Construire avec un `MGDockPanel` ou `MGStackPanel` horizontal :
   - **Boutons de navigation** : Précédent (←), Suivant (→), Dossier parent (↑) — `MGButton` avec icônes ou texte
   - **Barre de chemin (breadcrumb)** : `MGStackPanel` horizontal avec un `MGButton` par segment du chemin
     - Clic sur un segment = naviguer vers ce dossier
     - Séparer les segments avec `>` ou `/` (`MGTextBlock`)
   - **Barre de recherche** : `MGTextBox` avec placeholder "Rechercher..."
     - Sur `TextChanged`, filtrer les items affichés dans la vue courante
     - Recherche récursive optionnelle (checkbox)
   - **Sélecteur de vue** : Les toggles grille/détails (déjà créés en tâche 5)
   - **Slider de taille** (optionnel) : `MGSlider` pour ajuster la taille des thumbnails en vue grille
3. Implémenter l'historique de navigation :
   - Stack `_backHistory` et `_forwardHistory` de chemins
   - Mettre à jour les états enabled/disabled des boutons navigation

**Commit** : `feat(content-browser): add toolbar with search, navigation and breadcrumb`

---

### ✅ Tâche 7 — Menus contextuels

**Objectif** : Ajouter les menus contextuels pour les actions sur les fichiers et dossiers.

**Actions** :
1. Créer `ContentBrowser/Controls/ContentContextMenu.cs`
2. Menu contextuel sur un **fichier** (clic droit dans la vue contenu) :
   - "Ouvrir" — déclencher l'événement `FileOpened`
   - "Renommer" — activer le mode renommage (tâche 9)
   - "Supprimer" — demander confirmation + supprimer
   - "Dupliquer" — copier le fichier avec suffixe `_copy`
   - Séparateur
   - "Copier le chemin" — copier le chemin dans le presse-papier
   - "Afficher dans l'explorateur" — ouvrir le dossier parent dans l'explorateur système
   - Séparateur
   - "Propriétés" — afficher une fenêtre/tooltip avec les métadonnées
3. Menu contextuel sur un **dossier** :
   - "Ouvrir" — naviguer dans le dossier
   - "Nouveau dossier" — créer un sous-dossier
   - "Renommer"
   - "Supprimer"
   - Séparateur
   - "Copier le chemin"
4. Menu contextuel sur le **fond vide** de la vue contenu :
   - "Nouveau dossier"
   - "Importer un fichier..."
   - Séparateur
   - "Rafraîchir"
   - "Coller" (si un item a été copié/coupé)
5. Utiliser `MGContextMenu` avec `MGContextMenuButton`, `MGContextMenuSeparator`
6. Ajouter des icônes pertinentes via la propriété `Icon` des items de menu

**Commit** : `feat(content-browser): add context menus for files, folders and background`

---

### ✅ Tâche 8 — Tooltips et prévisualisation

**Objectif** : Afficher des informations détaillées au survol des fichiers.

**Actions** :
1. Pour chaque item dans la vue grille et la vue liste, configurer la propriété `ToolTip` de l'élément
2. Contenu du tooltip (`MGToolTip` avec contenu `MGStackPanel` vertical) :
   - **Image de prévisualisation** (si c'est une texture) : `MGImage` avec taille fixe (ex: 200x200)
   - **Nom complet** : `MGTextBlock` en gras
   - **Type** : `MGTextBlock` — ex: "Texture 2D (PNG)"
   - **Chemin** : `MGTextBlock` — le chemin relatif au projet
   - **Taille** : `MGTextBlock` — ex: "2.4 Mo"
   - **Dimensions** (si image) : `MGTextBlock` — ex: "1024 x 1024"
   - **Date de modification** : `MGTextBlock`
3. Charger les thumbnails de manière asynchrone (éviter de bloquer l'UI)
4. Mettre en cache les thumbnails déjà chargés (dictionnaire `string path → Texture2D`)

**Commit** : `feat(content-browser): add tooltips with file preview`

---

### ✅ Tâche 9 — Renommage en place

**Objectif** : Permettre le renommage d'un fichier/dossier directement dans le Content Browser.

**Actions** :
1. Créer `ContentBrowser/Controls/InlineRenameOverlay.cs`
2. Quand le renommage est déclenché (F2, menu contextuel, ou clic lent sur le nom) :
   - Masquer le `MGTextBlock` du nom
   - Afficher un `MGTextBox` à la même position avec le nom actuel en texte sélectionné
   - Focus le `MGTextBox`
3. Valider le renommage :
   - **Entrée** : Renommer le fichier/dossier sur le disque, mettre à jour le `ContentItem.Name`, masquer le `MGTextBox`
   - **Échap** : Annuler, masquer le `MGTextBox`, restaurer le `MGTextBlock`
   - **Clic en dehors** : Valider le renommage
4. Validation du nom :
   - Interdire les caractères invalides (`\ / : * ? " < > |`)
   - Interdire les noms vides
   - Vérifier qu'un fichier/dossier avec ce nom n'existe pas déjà
   - Afficher un feedback visuel (bordure rouge) si le nom est invalide

**Commit** : `feat(content-browser): add inline rename functionality`

---

### ✅ Tâche 10 — Drag-and-Drop

**Objectif** : Implémenter le glisser-déposer pour déplacer des fichiers/dossiers en utilisant le framework DnD de MGUI (voir `update-mgui.md` tâches 8-10).

**Actions** :
1. Créer `ContentBrowser/DragDrop/ContentDragHandler.cs`
2. **Initier le drag** (utiliser `MGElement.DoDragDrop()`) :
   - Sur `MouseHandler.DragStart` d'un item sélectionné, appeler :
     ```csharp
     element.DoDragDrop(
         data: selectedItems,
         format: "ContentItem",
         allowedEffects: DragDropEffect.MoveOrCopy,
         dragGhost: CreateDragGhost(selectedItems) // MGBorder semi-transparent avec count + icône
     );
     ```
3. **Configurer les cibles de drop** (`AllowDrop = true`) :
   - Sur chaque `MGTreeViewItem` (dossiers dans l'arbre) :
     ```csharp
     treeViewItem.AllowDrop = true;
     treeViewItem.DragEnter += (s, e) => { /* highlight du dossier */ };
     treeViewItem.DragLeave += (s, e) => { /* retirer highlight */ };
     treeViewItem.DragOver += (s, e) => {
         var target = GetFolderFromTreeItem(treeViewItem);
         e.ShowDropIndicator = IsValidDropTarget(e.Data, target);
         e.Effect = InputTracker.Keyboard.IsControlDown 
             ? DragDropEffect.Copy : DragDropEffect.Move;
     };
     treeViewItem.Drop += (s, e) => PerformFileOperation(e);
     ```
   - Idem pour les dossiers affichés dans la vue grille/liste
4. **Validation du drop** :
   - Interdire de drop un dossier dans lui-même ou dans un de ses sous-dossiers
   - Interdire de drop au même emplacement
5. Gérer la touche **Ctrl pendant le drag** : le `DragDropEffect` passe de `Move` à `Copy` (le ghost est mis à jour par le framework)
6. Mettre à jour le modèle de données et rafraîchir les vues après un drop

**Commit** : `feat(content-browser): add drag-and-drop for files and folders`

---

### ✅ Tâche 11 — Raccourcis clavier spécifiques au Content Browser

**Objectif** : Ajouter les raccourcis clavier spécifiques au Content Browser, au-dessus de la navigation clavier native de MGUI (Up/Down/Left/Right/Home/End déjà gérés par MGUI, voir `update-mgui.md` tâches 3-5).

**Actions** :
1. **Raccourcis sur le TreeView** (s'abonner au `KeyboardHandler.Pressed` du TreeView) :
   - `Entrée` : naviguer dans le dossier sélectionné (mettre à jour la vue contenu)
   - `Suppr` : supprimer le dossier sélectionné (avec confirmation)
   - `F2` : déclencher le renommage en place
2. **Raccourcis sur la vue grille/liste** (s'abonner au `KeyboardHandler.Pressed`) :
   - `Entrée` : ouvrir l'item sélectionné (dossier = naviguer, fichier = événement `FileOpened`)
   - `Suppr` : supprimer les items sélectionnés (avec confirmation)
   - `F2` : renommer l'item sélectionné
3. **Raccourcis globaux** (au niveau du `ContentBrowserPanel`) :
   - `Ctrl+F` : focus la barre de recherche
   - `Backspace` : remonter au dossier parent
   - `Alt+Gauche` : historique navigation précédent
   - `Alt+Droite` : historique navigation suivant
   - `F5` : rafraîchir le dossier courant

**Commit** : `feat(content-browser): add content browser keyboard shortcuts`

---

### ✅ Tâche 12 — Gestion du système de fichiers (opérations réelles)

**Objectif** : Connecter les actions UI aux opérations réelles sur le système de fichiers.

**Actions** :
1. Créer `ContentBrowser/Services/FileOperationService.cs`
2. Implémenter les opérations :
   - `void CreateDirectory(string path, string name)`
   - `void Delete(ContentItem item)` — suppression fichier ou dossier récursif
   - `void Rename(ContentItem item, string newName)`
   - `void Move(ContentItem item, ContentItem targetDirectory)`
   - `void Copy(ContentItem item, ContentItem targetDirectory)`
   - `void Import(string[] externalPaths, ContentItem targetDirectory)` — copier des fichiers externes dans le projet
3. Chaque opération doit :
   - Mettre à jour le système de fichiers réel (`System.IO`)
   - Mettre à jour le modèle de données (`ContentItem` tree)
   - Notifier les vues pour rafraîchissement (via `INotifyPropertyChanged` / `ObservableCollection`)
4. Gérer les erreurs (fichier verrouillé, permissions, noms invalides) avec des messages d'erreur visuels (fenêtre `MGWindow` modale)
5. Ajouter un `FileSystemWatcher` pour détecter les changements externes et rafraîchir automatiquement

**Commit** : `feat(content-browser): add file system operations service`

---

### ✅ Tâche 13 — Cache de thumbnails et performances

**Objectif** : Optimiser les performances pour les dossiers contenant beaucoup de fichiers.

**Actions** :
1. Créer `ContentBrowser/Services/ThumbnailCache.cs`
2. Système de cache de thumbnails :
   - Dictionnaire `Dictionary<string, Texture2D>` par chemin de fichier
   - Taille maximale du cache (ex: 500 entrées) avec politique LRU (Least Recently Used)
   - Chargement asynchrone des thumbnails (thread séparé + queue)
   - Thumbnail placeholder (icône générique) affiché pendant le chargement
3. Génération de thumbnails :
   - Textures (PNG, JPG, etc.) : redimensionner à taille fixe (ex: 128x128)
   - Modèles 3D : icône par défaut (ou render preview si disponible dans le moteur)
   - Sons : icône note de musique
   - Scripts : icône code
   - Scènes : icône scène
4. Utiliser `VirtualizingStackPanel` dans le `MGListBox` si la vue grille utilise un ListBox wrapper
5. Ne charger que les thumbnails visibles à l'écran (lazy loading basé sur la viewport du `MGScrollViewer`)
6. Invalider le cache quand un fichier est modifié (via `FileSystemWatcher`)

**Commit** : `feat(content-browser): add thumbnail caching and performance optimizations`

---

### ✅ Tâche 14 — Intégration et API publique

**Objectif** : Exposer une API propre pour que le moteur de jeu puisse intégrer et étendre le Content Browser.

**Actions** :
1. Créer `ContentBrowser/ContentBrowserConfig.cs` — Configuration :
   - `string RootDirectory` — dossier racine du projet
   - `string[] ExcludedExtensions` — extensions à masquer (ex: `.meta`, `.tmp`)
   - `string[] ExcludedDirectories` — dossiers à masquer (ex: `bin`, `obj`, `.git`)
   - `int ThumbnailSize` — taille par défaut des thumbnails
   - `ContentViewMode DefaultViewMode` — vue par défaut (Grid/Detail)
   - `bool ShowHiddenFiles`
2. Créer `ContentBrowser/ContentBrowserEvents.cs` — Événements publics :
   - `event Action<ContentItem> FileSelected` — fichier sélectionné (simple clic)
   - `event Action<ContentItem> FileOpened` — fichier ouvert (double-clic)
   - `event Action<ContentItem> FileDeleted`
   - `event Action<ContentItem, string> FileRenamed` — (item, ancien nom)
   - `event Action<ContentItem, ContentItem> FileMoved` — (item, ancien parent)
   - `event Action<IReadOnlyList<ContentItem>> SelectionChanged`
3. Documenter l'utilisation :
   ```csharp
   var config = new ContentBrowserConfig { RootDirectory = "Content/" };
   var browser = new ContentBrowserPanel(mgWindow, config);
   browser.FileOpened += item => LoadAsset(item.FullPath);
   // Ajouter browser.RootElement à votre layout MGUI
   ```
4. Permettre l'ajout d'items custom dans les menus contextuels via un système d'extension :
   - `browser.RegisterContextMenuExtension(ContentItemType.Texture, "Définir comme icône", item => SetIcon(item))`

**Commit** : `feat(content-browser): add public API and configuration`

---

### ✅ Tâche 15 — Style et thème visuel

**Objectif** : Appliquer un style cohérent et professionnel au Content Browser.

**Actions** :
1. Utiliser le `MGTheme` existant comme base
2. Définir les couleurs spécifiques du Content Browser :
   - Background du panneau arborescence (légèrement plus sombre)
   - Couleur de sélection (highlight bleu)
   - Couleur de survol (hover gris)
   - Couleur de drag-over (bordure pointillée ou highlight)
   - Couleur de l'item en cours de renommage
3. Icônes par type de fichier :
   - Créer ou intégrer un set d'icônes pour chaque `ContentItemType` (dossier, texture, modèle, son, script, scène, etc.)
   - Supporter les icônes personnalisées via `ContentBrowserConfig`
4. Animations subtiles (si supporté) :
   - Transition douce à l'expansion/réduction des dossiers du TreeView
   - Feedback visuel au survol des items
5. Respecter le thème sombre/clair du moteur de jeu si applicable

**Commit** : `feat(content-browser): apply visual styling and theme`

---

### ✅ Tâche 16 — Tests et polish

**Objectif** : Tests finaux, corrections de bugs et polish.

**Actions** :
1. Tester les scénarios suivants :
   - Dossier vide → message "Ce dossier est vide" dans le panneau contenu
   - Dossier avec 1000+ fichiers → vérifier les performances (virtualisation)
   - Renommage avec caractères spéciaux et unicode
   - Drag-and-drop d'un dossier dans son propre sous-dossier (doit être bloqué)
   - Suppression du dossier actuellement affiché (navigation automatique au parent)
   - Redimensionnement de la fenêtre → vérifier le layout responsive
   - Changement de vue avec sélection active → sélection conservée
2. Ajouter un message visuel pour les cas d'erreur :
   - Dossier inaccessible (permissions)
   - Fichier en cours d'utilisation
3. Écrire des tests unitaires pour :
   - `FileOperationService` (créer, renommer, déplacer, supprimer)
   - `ThumbnailCache` (ajout, éviction LRU, invalidation)
   - Historique de navigation (back/forward)
   - Filtrage de recherche
4. Corriger tous les bugs identifiés

**Commit** : `test(content-browser): add tests and final polish`

---

## Résumé de l'architecture

```
ContentBrowser/
├── ContentBrowserPanel.cs          # Classe principale, layout MGDockPanel
├── ContentBrowserConfig.cs         # Configuration
├── ContentBrowserEvents.cs         # Événements publics
├── Models/
│   ├── ContentItem.cs              # Modèle de données (INotifyPropertyChanged)
│   └── ContentItemType.cs          # Enum des types d'assets
├── Views/
│   ├── IContentView.cs             # Interface commune des vues
│   ├── GridView.cs                 # Vue grille (MGUniformGrid)
│   └── DetailView.cs               # Vue détaillée (MGListView)
├── Controls/
│   ├── Toolbar.cs                  # Barre d'outils (navigation, recherche, vue)
│   ├── ContentContextMenu.cs       # Menus contextuels
│   └── InlineRenameOverlay.cs      # Renommage en place
├── DragDrop/
│   └── DragDropManager.cs          # Gestion du drag-and-drop
└── Services/
    ├── FileSystemScanner.cs        # Scan et construction de l'arbre
    ├── FileOperationService.cs     # Opérations fichiers (CRUD)
    └── ThumbnailCache.cs           # Cache de thumbnails avec LRU
```

## Ordre des dépendances

```
Tâche 1 (modèles)
  └→ Tâche 2 (layout + tree)
       └→ Tâche 3 (vue grille)
       └→ Tâche 4 (vue détaillée)
            └→ Tâche 5 (basculement vues)
                 └→ Tâche 6 (toolbar + recherche)
                      └→ Tâche 7 (menus contextuels)
                           └→ Tâche 8 (tooltips)
                           └→ Tâche 9 (renommage)
                           └→ Tâche 10 (drag-and-drop)
                           └→ Tâche 11 (navigation clavier)
                                └→ Tâche 12 (opérations fichiers)
                                     └→ Tâche 13 (cache thumbnails)
                                          └→ Tâche 14 (API publique)
                                               └→ Tâche 15 (style)
                                                    └→ Tâche 16 (tests + polish)
```
