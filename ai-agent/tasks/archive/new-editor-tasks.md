# Plan d'implémentation — Nouvel éditeur CasaEngine.Editor (MGUI)

## Contexte

Remplacement de l'éditeur WPF (`CasaEngine.EditorUI`) par un nouvel éditeur MonoGame pur utilisant **MGUI** comme framework UI. Le projet cible est `CasaEngine.Editor`.

**Fonctionnalités à implémenter :**
- Ouverture / création de projet
- Content Browser (navigation des assets)
- Panneau de logs
- Affichage du monde (World viewport)
- Liste des entités du monde (hiérarchie)
- Détails d'une entité (liste des composants paramétrables)

---

## Conventions

- **Langue du code** : anglais (noms de classes, propriétés, commentaires XML)
- **Langue des échanges** : français
- **Commit** : un commit atomique après chaque tâche terminée
- **Suivi** : mettre à jour ce fichier après chaque tâche (icône ✅ ou ⬜)
- **Style** : factuel, concis, aucun storytelling

### Livrable après chaque tâche

```
## Résumé
- [quelques lignes max décrivant les modifications]

## Attendu
- [comportement attendu du code après cette tâche]

## À tester
- [ce que l'utilisateur doit vérifier manuellement]
```

---

## Fichiers de référence

| Concept | Fichier(s) |
|---|---|
| MGUI Desktop + Renderer | `MGUI.Samples/Game1.cs` |
| MGUI Docking system | `MGUI.Core/UI/Docking/Controls/MGDockHost.cs` |
| MGUI TreeView | `MGUI.Core/UI/MGTreeView.cs`, `MGTreeViewItem.cs` |
| MGUI ListBox | `MGUI.Core/UI/MGListBox.cs` |
| MGUI MenuBar | `MGUI.Core/UI/MGMenuBar.cs` |
| MGUI Window | `MGUI.Core/UI/MGWindow.cs` |
| MGUI TabControl | `MGUI.Core/UI/MGTabControl.cs` |
| MGUI TextBox | `MGUI.Core/UI/MGTextBox.cs` |
| MGUI ScrollViewer | `MGUI.Core/UI/MGScrollViewer.cs` |
| MGUI Expander | `MGUI.Core/UI/MGExpander.cs` |
| MGUI ComboBox | `MGUI.Core/UI/MGComboBox.cs` |
| MGUI Slider | `MGUI.Core/UI/MGSlider.cs` |
| MGUI CheckBox | `MGUI.Core/UI/MGCheckBox.cs` |
| MGUI ContextMenu | `MGUI.Core/UI/MGContextMenu.cs` |
| Layout Save/Load | `MGUI.Core/UI/Docking/DockLayout/DockLayoutSerializer.cs` |
| Éditeur WPF (référence) | `CasaEngine.EditorUI/MainWindow.xaml` |
| World Editor WPF | `CasaEngine.EditorUI/Controls/WorldControls/WorldEditorControl.xaml` |
| Entities Control WPF | `CasaEngine.EditorUI/Controls/EntityControls/EntitiesControl.xaml` |
| Entity Detail WPF | `CasaEngine.EditorUI/Controls/EntityControls/EntityControl.xaml` |
| Component Control WPF | `CasaEngine.EditorUI/Controls/EntityControls/EntityComponentControl.xaml` |
| Content Browser WPF | `CasaEngine.EditorUI/Controls/ContentBrowser/ContentBrowserControl.xaml` |
| Logs WPF | `CasaEngine.EditorUI/Controls/LogsControl.xaml` |
| Project Launcher WPF | `CasaEngine.EditorUI/ProjectLauncherWindow.xaml` |
| Engine World | `CasaEngine/Framework/World/World.cs` |
| Engine Entity | `CasaEngine/Framework/Entities/Entity.cs` |
| Engine Components | `CasaEngine/Framework/Entities/Components/` |
| Project Settings | `CasaEngine/Framework/Project/ProjectSettings.cs` |
| Project Loader | `CasaEngine/Framework/Project/ProjectSettingsHelper.cs` |
| Asset Catalog | `CasaEngine/Framework/Assets/AssetCatalog.cs` |
| Logger | `CasaEngine/Core/Log/Logs.cs`, `ILogger.cs` |

---

## Phase 1 — Audit MGUI : contrôles manquants

> Vérifier que MGUI fournit tous les contrôles nécessaires à l'éditeur.
> Si un contrôle manque, le créer dans MGUI ou dans CasaEngine.Editor.

### Tâche 1.1 — Audit des contrôles MGUI existants

✅ **Statut : Terminé**

**Résultats de l'audit :**

**Contrôles nécessaires vs disponibles :**

| Besoin éditeur | Contrôle MGUI | Statut |
|---|---|---|
| Barre de menu (File, Edit, Help) | `MGMenuBar` + `MGMenuBarItem` | ✅ Existe — sous-menus via `MGContextMenu`, séparateurs, items désactivables |
| Docking panels | `MGDockHost` | ✅ Existe — drag&drop, splitters, save/load JSON (`DockLayoutSerializer`) |
| TreeView (entités, dossiers) | `MGTreeView` + `MGTreeViewItem` | ✅ Existe — sélection, expand/collapse, `ItemsSource`, `ChildrenPropertyName` |
| ListBox (contenu dossier) | `MGListBox` | ✅ Existe — sélection simple/multiple, scrollable |
| TabControl | `MGTabControl` + `MGTabItem` | ✅ Existe |
| TextBlock / Label | `MGTextBlock` | ✅ Existe — rich text inline, wrap |
| TextBox (édition texte) | `MGTextBox` | ✅ Existe |
| Button | `MGButton` | ✅ Existe — dont `MGProgressButton` (répétition sur maintien) |
| CheckBox | `MGCheckBox` | ✅ Existe |
| ComboBox | `MGComboBox` | ✅ Existe |
| Slider (valeurs float) | `MGSlider` | ✅ Existe — `Minimum`, `Maximum`, `Value` (float), `IsDiscrete`, format string |
| Expander (sections dépliables) | `MGExpander` | ✅ Existe |
| ScrollViewer | `MGScrollViewer` | ✅ Existe |
| ContextMenu (clic droit) | `MGContextMenu` + `MGContextMenuItem` | ✅ Existe — normal, toggle, radio, séparateur, icônes |
| Image | `MGImage` | ✅ Existe |
| Grid layout | `MGGrid` | ✅ Existe — `MGGridSplitter` inclus |
| StackPanel | `MGStackPanel` | ✅ Existe |
| DockPanel | `MGDockPanel` | ✅ Existe |
| Splitter | `MGGridSplitter` | ✅ Existe |
| ProgressBar | `MGProgressBar` | ✅ Existe |
| GroupBox | `MGGroupBox` | ✅ Existe |
| Separator | `MGSeparator` | ✅ Existe |
| Color Picker | `MGGridColorPicker` | ✅ Existe |
| Window / Dialog | `MGWindow` | ✅ Existe — modal via `IsModal` |
| Toggle Button | `MGToggleButton` | ✅ Existe |
| Radio Button | `MGRadioButton` + `MGRadioButtonGroup` | ✅ Existe |
| OverlayPanel | `MGOverlayPanel` | ✅ Existe — utile pour popup/tooltip custom |
| **NumericUpDown (édition float/int)** | **Absent** | ❌ **Manquant — à créer dans `CasaEngine.Editor`** |
| **Vector3 editor (X, Y, Z)** | **Absent** | ❌ **Manquant — à créer dans `CasaEngine.Editor`** |
| **Asset selector (Guid → asset)** | **Absent** | ❌ **Manquant — à créer dans `CasaEngine.Editor`** |
| **ColorEditor (preview + picker)** | **Absent** | ❌ **Manquant — à créer (wrapper `MGGridColorPicker`)** |
| File/Folder dialog (OS natif) | Appel système | ⚠️ Via `System.Windows.Forms.OpenFileDialog` / `SaveFileDialog` |

**Résumé :**
- MGUI dispose de tous les contrôles UI standard nécessaires.
- 4 contrôles custom sont à créer dans `CasaEngine.Editor` (tâches 1.2 à 1.5) :
  - `NumericField` (TextBox + boutons ▲/▼ + molette)
  - `Vector3Editor` (3× NumericField)
  - `AssetSelector` (label + bouton Browse + fenêtre de sélection)
  - `ColorEditor` (preview couleur + popup `MGGridColorPicker`)
- Les dialogues fichiers OS utilisent `System.Windows.Forms` (déjà en dépendance via `UseWindowsForms`).

**Commit :** `docs(editor): audit MGUI controls for editor needs`

---

### Tâche 1.2 — Créer le contrôle NumericField dans CasaEngine.Editor

✅ **Statut : Terminé** — commit `b6ed3e55`

**Fichier à créer :** `CasaEngine.Editor/Controls/NumericField.cs`

**Actions :**
1. Créer une classe `NumericField` composée d'un `MGTextBox` + deux `MGButton` (▲/▼)
2. Propriétés : `float Value`, `float Min`, `float Max`, `float Step`, `string Label`
3. Événement `ValueChanged`
4. Valider l'entrée texte (parse float, clamp min/max)
5. Support du scroll molette pour incrémenter/décrémenter

**À tester :**
- Créer un `NumericField` avec Min=0, Max=100, Step=0.1
- Vérifier que les boutons ▲/▼ incrémentent/décrémentent
- Vérifier que le texte saisi est validé (rejeté si non-numérique)
- Vérifier le scroll molette
- Vérifier le clamp min/max

**Commit :** `feat(editor): add NumericField custom control`

---

### Tâche 1.3 — Créer le contrôle Vector3Editor dans CasaEngine.Editor

✅ **Statut : Terminé** — commit `1cd78e03`

**Fichier à créer :** `CasaEngine.Editor/Controls/Vector3Editor.cs`

**Actions :**
1. Créer une classe `Vector3Editor` composée de 3 `NumericField` (X, Y, Z) avec labels colorés (R, G, B ou X, Y, Z)
2. Propriété `Vector3 Value` (get/set)
3. Événement `ValueChanged`
4. Layout horizontal : `[X: ___] [Y: ___] [Z: ___]`

**À tester :**
- Afficher un `Vector3Editor` et modifier chaque composante
- Vérifier que `Value` retourne le bon `Vector3`
- Vérifier l'affichage labels X/Y/Z avec couleurs distinctes

**Commit :** `feat(editor): add Vector3Editor custom control`

---

### Tâche 1.4 — Créer le contrôle AssetSelector dans CasaEngine.Editor

✅ **Statut : Terminé** — commit `c26b10cd`

**Fichier à créer :** `CasaEngine.Editor/Controls/AssetSelector.cs`

**Actions :**
1. Créer un contrôle composé d'un `MGTextBlock` (nom de l'asset) + `MGButton` (browse)
2. Propriété `Guid AssetId` (get/set)
3. Afficher le nom de l'asset depuis `AssetCatalog` en fonction du `Guid`
4. Au clic sur Browse : ouvrir une fenêtre `MGWindow` listant les assets filtrables
5. Événement `AssetChanged`
6. Support d'un filtre par type d'asset (optionnel, propriété `Func<AssetInfo, bool> Filter`)

**À tester :**
- Afficher un `AssetSelector` et vérifier qu'il montre le nom de l'asset
- Cliquer sur Browse et vérifier que la fenêtre s'ouvre avec la liste des assets
- Sélectionner un asset et vérifier que `AssetId` est mis à jour
- Vérifier le filtre par type

**Commit :** `feat(editor): add AssetSelector custom control`

---

### Tâche 1.5 — Créer le contrôle ColorEditor dans CasaEngine.Editor

✅ **Statut : Terminé** — commit `837ee7d3`

**Fichier à créer :** `CasaEngine.Editor/Controls/ColorEditor.cs`

**Actions :**
1. Créer un contrôle composé d'un rectangle de preview couleur + `MGButton` pour ouvrir le `MGGridColorPicker`
2. Propriété `Color Value` (get/set) — `Microsoft.Xna.Framework.Color`
3. Événement `ValueChanged`
4. Au clic : ouvrir un popup/fenêtre avec `MGGridColorPicker`

**À tester :**
- Afficher le `ColorEditor`, vérifier le rectangle de preview
- Cliquer, vérifier que le color picker s'ouvre
- Choisir une couleur, vérifier que `Value` est mis à jour et le preview change

**Commit :** `feat(editor): add ColorEditor custom control`

---

## Phase 2 — Contrôles éditeur custom dans CasaEngine.Editor

> Créer les panneaux spécifiques de l'éditeur en utilisant MGUI.

### Tâche 2.1 — Structure de base de l'éditeur (Game1 + MGDesktop + MGDockHost)

✅ **Statut : Terminé** — commit `65c9e960`

**Fichiers à modifier :** `CasaEngine.Editor/Game1.cs`, `CasaEngine.Editor/Program.cs`

**Actions :**
1. Modifier `Game1` pour initialiser `MainRenderer` et `MGDesktop` (pattern de `MGUI.Samples/Game1.cs`)
2. Créer un `MGDockHost` comme layout principal avec des panels vides
3. Ajouter une `MGMenuBar` en haut avec les menus : File (New, Open, Save, Exit), Edit (Cut, Copy, Paste), Windows (Save Layout, Load Layout), Help (About)
4. Rendre le `MGDesktop` dans `Draw()`
5. Mettre à jour le `MGDesktop` dans `Update()`
6. Configurer la fenêtre : titre "CasaEngine Editor", taille 1600x900, redimensionnable

**À tester :**
- Lancer `CasaEngine.Editor`, vérifier que la fenêtre s'ouvre
- Vérifier que la barre de menu s'affiche avec les éléments File, Edit, Windows, Help
- Vérifier que le DockHost est visible (même vide)
- Vérifier que les menus s'ouvrent au clic

**Commit :** `feat(editor): setup Game1 with MGDesktop, MGDockHost, MGMenuBar`

---

### Tâche 2.2 — Project Launcher (ouverture de projet)

✅ **Statut : Terminé** — commit `20d27f29`

**Fichier à créer :** `CasaEngine.Editor/Controls/ProjectLauncherPanel.cs`

**Actions :**
1. Au démarrage de l'éditeur, afficher une `MGWindow` modale (Project Launcher)
2. Contenu :
   - Titre "CasaEngine Editor"
   - Bouton "Open Project" → ouvre un `OpenFileDialog` (System.Windows.Forms) pour sélectionner un fichier `.json` projet
   - Bouton "Create Project" → ouvre un `SaveFileDialog` pour créer un nouveau projet
   - Liste des projets récents (`mostRecentProjects.json`) avec double-clic pour ouvrir
   - Bouton "Launch" pour ouvrir le projet sélectionné
3. Appeler `ProjectSettingsHelper.Load(fileName)` quand un projet est sélectionné
4. Fermer la fenêtre modale après chargement réussi
5. Gérer la persistance des projets récents

**Dépendance :** `CasaEngine/Framework/Project/ProjectSettingsHelper.cs`

**À tester :**
- Lancer l'éditeur, vérifier que la fenêtre Project Launcher s'affiche
- Cliquer "Open Project", vérifier que le dialog fichier s'ouvre
- Sélectionner un fichier projet valide, vérifier que le projet se charge
- Vérifier que le projet apparaît dans les récents au prochain lancement
- Vérifier que "Create Project" crée un nouveau projet vide

**Commit :** `feat(editor): add ProjectLauncherPanel with open/create project`

---

### Tâche 2.3 — Panneau Content Browser

✅ **Statut : Terminé** — commit `db022630`

**Fichier à créer :** `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`

**Actions :**
1. Créer un panneau dockable `ContentBrowserPanel` pour le `MGDockHost`
2. Layout en deux colonnes avec splitter :
   - **Gauche** : `MGTreeView` affichant l'arborescence des dossiers
   - **Droite** : `MGListBox` affichant le contenu du dossier sélectionné (fichiers + sous-dossiers)
3. Alimenter depuis `AssetCatalog.AssetInfos` (même logique que `ContentBrowserViewModel`)
4. S'abonner aux événements `AssetCatalog.AssetAdded`, `AssetRemoved`, `AssetCleared`
5. S'abonner à `ProjectSettingsHelper.ProjectLoaded` pour reconstruire l'arbre
6. Barre d'outils en haut : bouton Save
7. Context menu (clic droit) sur le TreeView : New Folder, Rename, Delete
8. Context menu sur le ListBox : actions contextuelles selon le type d'asset
9. Afficher une icône dossier/fichier + nom + extension pour chaque item

**À tester :**
- Ouvrir un projet, vérifier que l'arborescence des dossiers s'affiche dans le TreeView
- Cliquer un dossier, vérifier que son contenu s'affiche dans le ListBox
- Vérifier les icônes (dossier vs fichier)
- Clic droit sur un dossier : vérifier le context menu (New Folder, Rename, Delete)
- Ajouter un asset dans le catalogue : vérifier qu'il apparaît dynamiquement
- Vérifier le splitter entre les deux colonnes

**Commit :** `feat(editor): add ContentBrowserPanel with folder tree and file list`

---

### Tâche 2.4 — Panneau Logs

✅ **Statut : Terminé** — commit `01e3ef07`

**Fichier à créer :** `CasaEngine.Editor/Controls/LogsPanel.cs`  
**Fichier à créer :** `CasaEngine.Editor/Log/LoggerEditor.cs`

**Actions :**
1. Créer `LoggerEditor : ILogger` — implémente `ILogger` et stocke les entrées de log dans une liste
2. Chaque entrée : `DateTime`, `LogVerbosity`, `string Message`
3. Créer un panneau dockable `LogsPanel`
4. Layout :
   - Barre de filtres en haut : `MGComboBox` pour filtrer par `LogVerbosity` (All, Trace, Debug, Info, Warning, Error)
   - Bouton "Clear" pour vider les logs
   - `MGListBox` scrollable affichant les entrées de log
5. Colorer les lignes selon la sévérité :
   - Trace → Gray
   - Debug → Green
   - Info → White
   - Warning → Orange/Yellow
   - Error → Red
6. Auto-scroll vers le bas quand un nouveau log arrive
7. Enregistrer le `LoggerEditor` dans `Logs` au démarrage de l'éditeur

**À tester :**
- Lancer l'éditeur, vérifier que le panneau Logs s'affiche
- Provoquer des logs (charger un projet) et vérifier qu'ils apparaissent
- Changer le filtre de verbosité et vérifier que les logs sont filtrés
- Cliquer "Clear" et vérifier que la liste se vide
- Vérifier les couleurs selon la sévérité
- Vérifier l'auto-scroll

**Commit :** `feat(editor): add LogsPanel with severity filtering and coloring`

---

### Tâche 2.5 — Panneau World Viewport (affichage 3D du monde)

✅ **Statut : Terminé** — commit `4ebdf1e8`

**Correctif de régression (2026-03-13) :**
- Le panneau utilisait un `RenderTarget2D` local et appelait `World.Draw()` directement, ce qui ne passait pas par le pipeline de rendu CasaEngine.
- Le nouvel éditeur héberge désormais un `CasaEngineGame` minimal partagé avec le `GraphicsDevice` MonoGame principal.
- Le viewport crée une vraie `RenderView` sur `RenderTargetSurface`, synchronisée avec le `World` chargé via `GameManager.SetWorldToLoad(...)`.
- Résultat attendu : le contenu du monde apparaît dans le panneau dès qu'un projet avec `FirstWorldLoaded` valide est ouvert.

**Fichier à créer :** `CasaEngine.Editor/Controls/WorldViewportPanel.cs`

**Actions :**
1. Créer un panneau dockable `WorldViewportPanel` qui occupe la zone centrale du DockHost
2. Réserver une zone de rendu dans le panel (un `RenderTarget2D`)
3. Rendre le `World` actuel dans ce `RenderTarget2D` pendant `Draw()`
4. Afficher le `RenderTarget2D` dans le panel via `MGImage` ou dessin direct
5. Gérer la caméra éditeur : rotation (clic milieu), pan (Shift + clic milieu), zoom (molette)
6. Intégrer le `GizmoTool` pour la manipulation des entités (translate, rotate, scale)
7. Gérer le picking : clic gauche sur une entité la sélectionne (raycast)
8. Synchroniser la sélection avec le panneau Entities

**Dépendances :** `CasaEngine/Framework/World/World.cs`, `GizmoTool/`

**À tester :**
- Charger un monde avec des entités, vérifier qu'il s'affiche dans le viewport
- Vérifier la rotation caméra (clic milieu + drag)
- Vérifier le pan (Shift + clic milieu)
- Vérifier le zoom (molette)
- Cliquer sur une entité dans le viewport, vérifier qu'elle est sélectionnée
- Vérifier que le gizmo apparaît sur l'entité sélectionnée
- Redimensionner le panel, vérifier que le viewport s'adapte

**Commit :** `feat(editor): add WorldViewportPanel with camera controls and picking`

---

### Tâche 2.6 — Panneau Entities (hiérarchie des entités du monde)

✅ **Statut : Terminé**

**Fichier à créer :** `CasaEngine.Editor/Controls/EntitiesPanel.cs`

**Actions :**
1. Créer un panneau dockable `EntitiesPanel` (position droite dans le DockHost)
2. Afficher un `MGTreeView` avec la hiérarchie des entités du `World` courant
3. Chaque item : icône entité + nom de l'entité
4. Support des entités enfants (`Entity.Children`) comme sous-nœuds
5. Sélection d'une entité → notifier les autres panneaux (WorldViewport pour highlight, EntityDetails pour affichage)
6. S'abonner aux événements du `World` : `EntityAdded`, `EntityRemoved`, etc.
7. Context menu (clic droit) : Add Entity, Delete Entity, Rename, Duplicate
8. Support du double-clic pour focus la caméra sur l'entité
9. Recherche/filtre par nom (optionnel, `MGTextBox` en haut)

**À tester :**
- Charger un monde, vérifier que les entités apparaissent dans le TreeView
- Vérifier la hiérarchie parent/enfant
- Sélectionner une entité, vérifier qu'elle est mise en surbrillance dans le viewport
- Clic droit → Add Entity : vérifier que l'entité est ajoutée
- Clic droit → Delete : vérifier la suppression
- Double-clic : vérifier que la caméra se déplace vers l'entité

**Commit :** `feat(editor): add EntitiesPanel with entity hierarchy tree`

## Résumé
- Ajout de `EntitiesPanel` avec `MGTreeView`, recherche par nom, menu contextuel Add/Delete/Rename/Duplicate et reconstruction selon le `World` courant.
- Intégration du panneau dans `Game1` à la place du placeholder "World Explorer".
- Synchronisation minimale avec `WorldViewportPanel` : sélection arbre → viewport/gizmo, double-clic arbre → focus caméra, sélection viewport/gizmo → arbre.

## Attendu
- Quand un monde est chargé, la hiérarchie complète des entités et sous-entités apparaît dans le panneau Entities.
- Les ajouts, suppressions, duplications et renommages effectués depuis le panneau sont reflétés immédiatement.
- Le viewport et l’arbre restent synchronisés sur la sélection courante.

## À tester
- Charger un projet avec un monde et vérifier l’affichage de la hiérarchie dans le panneau Entities.
- Sélectionner une entité dans l’arbre et vérifier que le gizmo/viewport suit la sélection.
- Sélectionner une entité depuis le viewport et vérifier que l’arbre suit la sélection.
- Utiliser le clic droit pour ajouter, renommer, dupliquer et supprimer une entité.
- Double-cliquer une entité et vérifier le focus caméra.

---

### Tâche 2.7 — Panneau Entity Details (détails et composants d'une entité)

✅ **Statut : Terminé**

**Fichier à créer :** `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`

**Actions :**
1. Créer un panneau dockable `EntityDetailsPanel` (sous le panneau Entities)
2. Quand une entité est sélectionnée, afficher :
   - **Nom de l'entité** : `MGTextBox` éditable
   - **Bouton "Add Component"** : ouvre un menu/popup pour choisir un type de composant
   - **TreeView des composants** : liste les `EntityComponent` de l'entité, chacun avec une icône
3. Sélection d'un composant → afficher ses propriétés éditables en dessous
4. S'abonner aux événements `Entity.ComponentAdded`, `Entity.ComponentRemoved`

**À tester :**
- Sélectionner une entité dans le panneau Entities
- Vérifier que son nom apparaît et est éditable
- Vérifier que la liste des composants s'affiche
- Cliquer "Add Component", vérifier que le menu s'ouvre avec les types disponibles
- Ajouter un composant, vérifier qu'il apparaît dans la liste
- Sélectionner un composant, vérifier que ses propriétés s'affichent

**Commit :** `feat(editor): add EntityDetailsPanel with component list`

## Résumé
- Ajout de `EntityDetailsPanel` avec nom d'entité éditable, popup d'ajout de composant, arbre des composants et zone de propriétés scrollable.
- Intégration du panneau dans `Game1` à la place du placeholder de propriétés, avec synchronisation sur la sélection d'entité venant du TreeView et du viewport.
- Ajout d'une édition générique minimale des propriétés simples (bool, string, numérique, `Vector3`, `Guid`, `Enum`, `Color`) et resynchronisation de `EntitiesPanel` sur les renommages d'entités.

## Attendu
- Quand une entité est sélectionnée, le panneau Details affiche son nom, la hiérarchie de ses composants et les propriétés éditables du composant sélectionné.
- Le bouton Add Component permet d'ajouter un composant top-level ou un enfant de `SceneComponent`, puis la liste se met à jour immédiatement.
- Un renommage depuis le panneau Details est propagé au panneau Entities sans refresh manuel.

## À tester
- Sélectionner une entité dans Entities puis vérifier que son nom et ses composants apparaissent dans Details.
- Renommer l'entité depuis Details et vérifier que le panneau Entities reflète le changement.
- Cliquer sur Add Component, ajouter un composant simple puis vérifier qu'il apparaît immédiatement dans l'arbre.
- Sélectionner un composant `SceneComponent`, vérifier les éditeurs Position/Scale, puis modifier une valeur et observer la mise à jour côté runtime.
- Sélectionner un composant avec propriétés simples (`bool`, `float`, `Guid` asset, `Enum`) et vérifier que les contrôles correspondants s'affichent.

---

### Tâche 2.8 — Property editors pour composants courants

✅ **Statut : Terminé**

**Fichiers à créer :** `CasaEngine.Editor/Controls/ComponentEditors/`

**Actions :**
1. Créer un dossier `ComponentEditors/` avec un éditeur de propriétés par type de composant
2. Créer une classe de base `ComponentEditorBase` qui :
   - Prend un `EntityComponent` en entrée
   - Génère un layout de propriétés éditables via `MGExpander` + `MGGrid`
   - Support des types : `float` → `NumericField`, `Vector3` → `Vector3Editor`, `bool` → `MGCheckBox`, `string` → `MGTextBox`, `Color` → `ColorEditor`, `Guid` (asset) → `AssetSelector`, `Enum` → `MGComboBox`
3. Implémenter les éditeurs pour les composants essentiels :
   - `TransformComponentEditor` : Position (Vector3), Rotation (Vector3), Scale (Vector3)
   - `StaticModelComponentEditor` : Model asset selector
   - `CameraComponentEditor` : FOV, near/far plane
   - `PhysicsComponentEditor` : mass, friction, collision shape
4. Utiliser un registry / factory pour résoudre quel éditeur afficher selon le type de composant
5. Fallback : éditeur générique par réflexion pour composants non explicitement supportés

**À tester :**
- Sélectionner une entité avec un composant Transform
- Vérifier que Position, Rotation, Scale s'affichent avec des `Vector3Editor`
- Modifier une valeur, vérifier que l'entité est mise à jour en temps réel dans le viewport
- Sélectionner un composant StaticModel, vérifier le sélecteur d'asset
- Tester le fallback générique sur un composant custom

**Commit :** `feat(editor): add component property editors with registry`

## Résumé
- Ajout d’une infrastructure `ComponentEditors/` avec un `ComponentEditorRegistry`, une base commune `ComponentEditorBase` et un fallback générique par réflexion.
- Implémentation d’éditeurs dédiés pour les composants `SceneComponent` (transform), `StaticModelComponent`, `CameraComponent` et `PhysicsBaseComponent` avec sections MGUI en `MGExpander` + `MGGrid`.
- `EntityDetailsPanel` délègue désormais l’affichage des propriétés au registry, et `CameraComponent` expose des propriétés near/far éditables pour la couche éditeur.

## Attendu
- La sélection d’un composant courant affiche un éditeur spécialisé plutôt qu’une simple liste générique de propriétés.
- Les composants de scène exposent Position, Rotation et Scale avec des `Vector3Editor`, les modèles statiques un sélecteur d’asset, les caméras FOV/near/far et les composants physiques leurs paramètres principaux ainsi que leur forme de collision quand elle est supportée.
- Les composants non enregistrés dans le registry continuent de fonctionner via un fallback générique par réflexion.

## À tester
- Sélectionner un `SceneComponent` et vérifier l’édition de Position, Rotation, Scale avec mise à jour immédiate dans le viewport.
- Sélectionner un `StaticModelComponent`, changer l’asset modèle et vérifier que la valeur du composant est mise à jour.
- Sélectionner un composant caméra et vérifier l’affichage de FOV, near plane et far plane.
- Sélectionner un composant physique (`BoxCollisionComponent`, `SphereCollisionComponent`, etc.) et vérifier l’édition de masse, friction et forme de collision.
- Sélectionner un composant non spécialisé et vérifier que le fallback générique continue d’afficher les propriétés simples éditables.

---

## Phase 3 — Vérifications côté moteur CasaEngine

> S'assurer que le moteur fournit tout ce que l'éditeur requiert.

### Tâche 3.1 — Vérifier les événements du World pour la synchronisation éditeur

✅ **Statut : Terminé**

**Fichier :** `CasaEngine/Framework/World/World.cs`

**Actions :**
1. Vérifier que `World` expose des événements `EntityAdded` et `EntityRemoved`
2. Si absents, les ajouter (pattern observer)
3. Vérifier que `ClearEntities()` déclenche un événement `EntitiesCleared`
4. Vérifier que l'ajout/suppression d'entités enfants est notifié

**À tester :**
- S'abonner à `World.EntityAdded`, ajouter une entité, vérifier que l'événement est levé
- S'abonner à `World.EntityRemoved`, supprimer une entité, vérifier l'événement
- Appeler `ClearEntities()`, vérifier que l'événement de nettoyage est levé

**Commit :** `feat(engine): ensure World exposes entity change events`

## Résumé
- Le `World` lève désormais ses notifications de manière cohérente lors des ajouts, suppressions et nettoyages, sans dépendre d’un symbole de compilation éditeur.
- Ajout d’un alias `EntitiesCleared` en complément de `EntitiesClear` pour clarifier l’intention côté éditeur.
- Le `World` relaie aussi les ajouts/suppressions d’entités enfants via les événements `EntityAdded` et `EntityRemoved`.

## Attendu
- Un chargement normal du monde, un ajout via l’éditeur, une suppression et un nettoyage complet déclenchent les notifications attendues.
- Les panneaux éditeur peuvent se resynchroniser sur les mutations de hiérarchie sans logique spécifique hors moteur.

## À tester
- S’abonner à `World.EntityAdded`, ajouter une entité top-level puis une entité enfant, et vérifier les deux notifications.
- S’abonner à `World.EntityRemoved`, supprimer une entité top-level puis une entité enfant, et vérifier les notifications.
- Appeler `ClearEntities()` et vérifier que `EntitiesClear` et `EntitiesCleared` sont levés.

---

### Tâche 3.2 — Vérifier les événements de l'Entity pour les composants

✅ **Statut : Terminé**

**Fichier :** `CasaEngine/Framework/Entities/Entity.cs`

**Actions :**
1. Vérifier que `Entity` expose `ComponentAdded` et `ComponentRemoved`
2. Vérifier que `Entity` expose `ChildAdded` et `ChildRemoved`
3. Vérifier que le renommage d'entité est notifié (`NameChanged` ou `PropertyChanged`)
4. Si des événements manquent, les ajouter

**À tester :**
- Ajouter un composant, vérifier l'événement `ComponentAdded`
- Supprimer un composant, vérifier `ComponentRemoved`
- Ajouter un enfant, vérifier `ChildAdded`
- Renommer l'entité, vérifier notification

**Commit :** `feat(engine): ensure Entity exposes component/child change events`

## Résumé
- Les événements `ComponentAdded`, `ComponentRemoved`, `ChildAdded` et `ChildRemoved` sont désormais exposés sans dépendre d’un symbole de compilation éditeur.
- Ajout d’un événement `NameChanged` sur `Entity` pour notifier explicitement les renommages.

## Attendu
- L’éditeur peut s’abonner aux changements de composants, d’enfants et de nom sur `Entity` dans la build courante.
- Un renommage via `entity.Name = ...` déclenche désormais une notification exploitable côté UI.

## À tester
- S’abonner à `ComponentAdded` puis ajouter un composant et vérifier l’événement.
- S’abonner à `ChildAdded` puis ajouter une entité enfant et vérifier l’événement.
- S’abonner à `NameChanged`, renommer une entité et vérifier les anciens/nouveaux noms.

---

### Tâche 3.3 — Vérifier le AssetCatalog pour le Content Browser

✅ **Statut : Terminé**

**Fichier :** `CasaEngine/Framework/Assets/AssetCatalog.cs`

**Actions :**
1. Vérifier que `AssetCatalog` expose `AssetAdded`, `AssetRemoved`, `AssetRenamed`, `AssetCleared`
2. Vérifier que `AssetInfo` contient le chemin relatif du fichier, le type d'asset, le Guid
3. Vérifier que l'on peut itérer `AssetCatalog.AssetInfos` depuis l'éditeur
4. Vérifier que le save/load du catalogue fonctionne indépendamment de WPF

**À tester :**
- Charger un projet, itérer les `AssetInfos`, vérifier les données
- Ajouter un asset, vérifier l'événement
- Sauvegarder et recharger le catalogue

**Commit :** `feat(engine): verify AssetCatalog events and data for editor`

## Résumé
- `AssetCatalog` expose désormais publiquement les événements `AssetAdded`, `AssetRemoved`, `AssetRenamed` et `AssetCleared`, avec un rechargement robuste via `Load()` qui nettoie d’abord l’état courant.
- `AssetInfo` porte maintenant explicitement le chemin relatif (`RelativeFileName`) et une catégorie d’asset (`AssetType`) dérivée du fichier si elle n’est pas fournie.
- Le save/load du catalogue préserve désormais `name`, `file_name` et `asset_type`, ce qui évite de perdre des métadonnées utiles pour l’éditeur entre deux chargements.

## Attendu
- Le moteur expose un catalogue d’assets directement consommable par l’éditeur sans dépendre d’un wrapper WPF.
- Un changement de projet ou un rechargement du catalogue repart d’un état propre, avec notifications cohérentes pour les panneaux éditeur.
- Chaque `AssetInfo` fournit un `Guid`, un chemin relatif exploitable et une catégorie d’asset disponible pour du filtrage côté UI.

## À tester
- Charger un projet et vérifier que `AssetCatalog.AssetInfos` contient des entrées avec `Id`, `RelativeFileName` et `AssetType` cohérents.
- S’abonner aux événements `AssetAdded`, `AssetRemoved`, `AssetRenamed` et `AssetCleared`, puis vérifier qu’ils se déclenchent lors des mutations du catalogue.
- Sauvegarder le catalogue, relancer le chargement du projet et vérifier que les noms et catégories d’assets sont conservés.

---

### Tâche 3.4 — Vérifier que le moteur de rendu fonctionne avec un RenderTarget éditeur

✅ **Statut : Terminé**

**Actions :**
1. Vérifier que le pipeline de rendu du `World` peut rendre dans un `RenderTarget2D` arbitraire
2. Si le rendu est lié au backbuffer, ajouter un paramètre `RenderTarget2D` au pipeline
3. Vérifier que la caméra peut être contrôlée indépendamment (caméra éditeur vs caméra jeu)
4. Vérifier que le `GizmoTool` fonctionne avec un viewport custom

**À tester :**
- Créer un `RenderTarget2D`, rendre le monde dedans, afficher le résultat
- Changer la taille du RenderTarget, vérifier que le rendu s'adapte
- Contrôler la caméra éditeur indépendamment

**Commit :** `docs(editor): verify custom RenderTarget rendering path`

## Résumé
- Vérification du pipeline multi-view : `RenderPipeline` applique bien une surface arbitraire par vue avant le rendu, puis restaure la cible initiale ensuite.
- Vérification de `RenderTargetSurface` : création de `RenderTarget2D`, resize via `EnsureSize/RequestResize`, exposition d’un `ViewportRect` cohérent pour la vue et la UI.
- Vérification du flux éditeur : `WorldViewportPanel` crée une vraie `RenderView` sur `RenderTargetSurface`, la caméra éditeur est indépendante du runtime jeu, et le gizmo consomme explicitement `ActiveCamera` + `ActiveSurface` pour un viewport custom.

## Attendu
- Le moteur peut rendre un `World` dans une cible hors backbuffer sans patch spécifique additionnel.
- Une vue éditeur peut garder sa propre caméra, sa propre taille de viewport et son propre cycle de rendu indépendamment d’une vue runtime classique.
- Le gizmo et le picking utilisent bien les dimensions du viewport custom au lieu de dépendre du backbuffer global.

## À tester
- Charger un monde dans l’éditeur et vérifier que le panneau viewport affiche bien le rendu du `World` dans sa texture dédiée.
- Redimensionner le panneau et vérifier que le rendu et le gizmo suivent la nouvelle taille.
- Manipuler la caméra éditeur puis vérifier qu’elle reste indépendante de toute caméra runtime du monde.
- Vérifier qu’un clic de picking et le gizmo continuent de fonctionner correctement après redimensionnement ou changement de focus.

---

## Phase 4 — Assemblage de l'éditeur

> Connecter tous les panneaux et finaliser l'éditeur.

### Tâche 4.1 — Assembler le layout principal de l'éditeur

✅ **Statut : Terminé**

**Fichier à modifier :** `CasaEngine.Editor/Game1.cs`

**Actions :**
1. Configurer le `MGDockHost` avec le layout par défaut :
   - **Centre** : `WorldViewportPanel`
   - **Droite haut** : `EntitiesPanel`
   - **Droite bas** : `EntityDetailsPanel`
   - **Bas** : `LogsPanel` + `ContentBrowserPanel` (onglets)
2. Connecter les menus File → Open/Save/New au `ProjectSettingsHelper`
3. Connecter Windows → Save Layout / Load Layout au `DockLayoutSerializer`
4. Implémenter le flux de sélection : Entities → EntityDetails + WorldViewport
5. Enregistrer le `LoggerEditor` dans `Logs`

**À tester :**
- Lancer l'éditeur, vérifier le layout par défaut (viewport centre, entities à droite, logs en bas)
- File → Open : vérifier que le monde se charge et s'affiche
- File → Save : vérifier que le projet est sauvegardé
- Sélectionner une entité dans Entities, vérifier les détails et le highlight viewport
- Windows → Save Layout, fermer, relancer, Load Layout : vérifier la restauration
- Redimensionner les panneaux avec les splitters

**Commit :** `feat(editor): assemble main editor layout with all panels`

## Résumé
- `Game1` assemble maintenant le layout principal conforme au plan: `WorldViewportPanel` au centre, `EntitiesPanel` en haut à droite, `EntityDetailsPanel` en bas à droite, et `ContentBrowser` / `Logs` en onglets en bas.
- Le menu `File` est connecté à l’ouverture/création de projet et au save du projet courant, tandis que le menu `Windows` expose désormais `Save Layout`, `Load Layout` et `Reset Layout` via le JSON du dock MGUI.
- Les content factories des panneaux sont centralisées pour réutiliser les mêmes instances lors d’un reset ou d’un rechargement de layout, et le flux de sélection Entities ↔ Viewport ↔ Details reste synchronisé.

## Attendu
- L’éditeur s’ouvre avec un agencement cohérent et exploitable sans placeholders restants dans le layout principal.
- Le projet courant peut être sauvegardé depuis le menu `File`, et le layout courant peut être exporté/importé depuis le menu `Windows`.
- Les panneaux principaux restent fonctionnels après un reset de layout ou un rechargement d’un layout JSON.

## À tester
- Lancer l’éditeur, ouvrir un projet et vérifier la disposition par défaut: viewport au centre, entities/détails à droite, logs/content browser en bas.
- Utiliser `File -> Save` et vérifier que le projet et le catalogue d’assets sont sauvegardés sans erreur.
- Utiliser `Windows -> Save Layout`, puis `Load Layout`, et vérifier que les panneaux sont bien restaurés avec leur contenu.
- Sélectionner une entité depuis `Entities`, puis depuis le viewport, et vérifier la synchronisation avec `Details`.

---

### Tâche 4.2 — Système de sélection centralisé

✅ **Statut : Terminé**

**Fichier à créer :** `CasaEngine.Editor/EditorSelection.cs`

**Actions :**
1. Créer une classe singleton `EditorSelection` qui gère la sélection courante
2. Propriétés : `Entity SelectedEntity`, `EntityComponent SelectedComponent`
3. Événements : `SelectionChanged`, `ComponentSelectionChanged`
4. Connecter : `EntitiesPanel` → `EditorSelection` → `EntityDetailsPanel`, `WorldViewportPanel`
5. Support de la sélection depuis le viewport (picking) vers `EditorSelection`

**À tester :**
- Sélectionner une entité dans le TreeView → vérifier que le viewport la met en surbrillance et que les détails s'affichent
- Cliquer une entité dans le viewport → vérifier que le TreeView la sélectionne et que les détails s'affichent
- Changer de sélection rapidement, vérifier la cohérence

**Commit :** `feat(editor): add centralized EditorSelection system`

## Résumé
- Ajout d’un singleton `EditorSelection` pour centraliser `SelectedEntity` et `SelectedComponent` avec événements dédiés.
- `Game1` relaie désormais la sélection via `EditorSelection` au lieu de synchroniser directement `EntitiesPanel`, `WorldViewportPanel` et `EntityDetailsPanel` entre eux.
- `EntityDetailsPanel` expose la sélection de composant pour garder l’état central cohérent avec l’arbre des composants.

## Attendu
- Une sélection d’entité depuis l’arbre ou depuis le viewport met à jour une source unique, puis resynchronise les trois panneaux sans couplage direct panneau-à-panneau.
- La sélection de composant est conservée dans l’état éditeur partagé et peut être consommée par d’autres panneaux sans logique ad hoc supplémentaire.
- Un changement de projet ou de monde repart d’un état de sélection propre.

## À tester
- Sélectionner une entité dans `Entities` puis vérifier la mise à jour du viewport et de `EntityDetails`.
- Sélectionner une entité depuis le viewport/gizmo puis vérifier que l’arbre `Entities` et `EntityDetails` suivent.
- Sélectionner un composant dans `EntityDetails`, changer d’entité, puis revenir et vérifier qu’aucune sélection obsolète ne persiste.

---

### Tâche 4.3 — Intégrer le Project Launcher au flux de démarrage

✅ **Statut : Terminé**

**Fichier à modifier :** `CasaEngine.Editor/Game1.cs`

**Actions :**
1. Au démarrage, afficher la fenêtre `ProjectLauncherPanel` (modale)
2. Bloquer l'affichage du DockHost tant qu'aucun projet n'est chargé
3. Après le chargement du projet :
   - Charger le `World` initial (`ProjectSettings.FirstWorldLoaded`)
   - Alimenter le Content Browser
   - Mettre à jour le titre de la fenêtre avec le nom du projet
4. Menu File → Open : permettre de changer de projet (réafficher le launcher)

**À tester :**
- Lancer l'éditeur : vérifier que le launcher s'affiche en premier
- Ouvrir un projet : vérifier que le monde se charge
- Vérifier le titre de la fenêtre
- File → Open : vérifier que le launcher se réaffiche

**Commit :** `feat(editor): integrate ProjectLauncher into editor startup flow`

## Résumé
- Le démarrage passe désormais par un affichage unique du `ProjectLauncherWindow`, utilisé aussi depuis le menu `File`.
- `Game1` n’initialise le `DockHost` et les panneaux principaux qu’après `ProjectLoaded`, puis rafraîchit explicitement le `ContentBrowser` et charge le monde initial.
- Le titre de fenêtre est mis à jour via une méthode dédiée pour refléter le projet courant ou revenir au titre par défaut.

## Attendu
- Au lancement, seul le menu principal et la fenêtre modale du launcher sont visibles tant qu’aucun projet n’est chargé.
- Après ouverture ou création d’un projet, l’éditeur initialise le layout principal, active le `Content Browser`, charge le monde de démarrage et affiche le nom du projet dans la barre de titre.
- `File -> Open Project` et `File -> New Project` réouvrent le launcher sans casser l’état du runtime hôte.

## À tester
- Lancer l’éditeur et vérifier que le launcher s’affiche avant tout panneau docké.
- Ouvrir ou créer un projet puis vérifier l’initialisation du viewport, du `Content Browser` et le titre de fenêtre.
- Utiliser `File -> Open Project` depuis un projet déjà chargé et vérifier que le launcher réapparaît correctement.

---

### Tâche 4.4 — StatusBar en bas de l'éditeur

✅ **Statut : Terminé**

**Actions :**
1. Ajouter un `MGDockPanel` ou `MGStackPanel` en bas de la fenêtre principale (hors DockHost)
2. Afficher des boutons pour ouvrir/fermer des panneaux : "Content Browser", "Logs"
3. Afficher des infos status : FPS, nom du projet, nombre d'entités

**À tester :**
- Vérifier que la status bar s'affiche en bas
- Cliquer "Content Browser" : vérifier que le panneau s'ouvre/ferme
- Vérifier l'affichage du FPS

**Commit :** `feat(editor): add status bar with panel toggles and info`

## Résumé
- Ajout d’une status bar en bas de la fenêtre principale, hors `MGDockHost`, avec boutons de toggle pour `Content Browser` et `Logs`.
- Les toggles s’appuient sur l’état réel du dock via `ShowDockable` / `RemovePanel` pour rouvrir ou fermer les panneaux proprement.
- La barre affiche maintenant le projet courant, le FPS échantillonné et le nombre d’entités du monde actif.

## Attendu
- Une barre de statut reste visible en bas de l’éditeur, indépendamment du layout docké.
- Les boutons `Content Browser` et `Logs` permettent d’ouvrir ou fermer ces panneaux sans passer par les tabs du dock.
- Les informations projet/FPS/nombre d’entités se mettent à jour pendant l’exécution.

## À tester
- Vérifier que la status bar est visible en bas dès qu’un projet est chargé.
- Cliquer sur `Content Browser` et `Logs` pour vérifier l’ouverture puis la fermeture des panneaux.
- Vérifier que le projet courant, le FPS et le nombre d’entités évoluent correctement.

---

### Tâche 4.5 — Sauvegarde et chargement du layout (persistance)

✅ **Statut : Terminé**

**Actions :**
1. Connecter le menu "Windows → Save Layout" au `DockLayoutSerializer.SaveLayoutToJson()`
2. Connecter "Windows → Load Layout" au `DockLayoutSerializer.LoadLayoutFromJson()`
3. Sauvegarder le layout dans le dossier du projet (ex: `.casaeditor/layout.json`)
4. Charger automatiquement le dernier layout au démarrage si le fichier existe
5. Gérer les panneaux manquants au restore (graceful fallback)

**À tester :**
- Réarranger les panneaux, sauver le layout
- Relancer l'éditeur, vérifier que le layout est restauré
- Supprimer le fichier layout, vérifier que le layout par défaut est utilisé
- Fermer un panneau, sauver, recharger : vérifier qu'il est bien fermé au restore

**Commit :** `feat(editor): add dock layout save/load persistence`

## Résumé
- Les menus `Windows -> Save Layout` et `Windows -> Load Layout` utilisent maintenant un chemin projet fixe: `.casaeditor/layout.json`.
- À l’ouverture d’un projet, `Game1` tente de restaurer automatiquement ce layout; en absence de fichier ou en cas d’erreur, l’éditeur retombe sur le layout par défaut.
- Les layouts qui référencent un panneau inconnu sont restaurés sans crash via un contenu de fallback explicite.

## Attendu
- Un layout sauvegardé pour un projet est réutilisé automatiquement lors du prochain chargement de ce même projet.
- Si aucun layout n’existe, ou si le JSON est invalide, l’éditeur repart sur son layout standard sans bloquer le démarrage.
- Un ancien layout qui contient un panneau non reconnu reste chargeable avec un fallback lisible au lieu d’un plantage.

## À tester
- Réarranger les panneaux, utiliser `Windows -> Save Layout`, relancer l’éditeur puis rouvrir le même projet et vérifier la restauration automatique.
- Supprimer `.casaeditor/layout.json` puis rouvrir le projet et vérifier le retour au layout par défaut.
- Fermer `Logs` ou `Content Browser`, sauvegarder le layout, rouvrir le projet et vérifier que l’état fermé est restauré.

---

## Résumé des tâches

| # | Phase | Tâche | Statut | Commit |
|---|---|---|---|---|
| 1.1 | MGUI Audit | Audit des contrôles MGUI | ✅ | `76b963a0` |
| 1.2 | MGUI Audit | Créer NumericField | ✅ | `b6ed3e55` |
| 1.3 | MGUI Audit | Créer Vector3Editor | ✅ | `1cd78e03` |
| 1.4 | MGUI Audit | Créer AssetSelector | ✅ | `c26b10cd` |
| 1.5 | MGUI Audit | Créer ColorEditor | ✅ | `837ee7d3` |
| 2.1 | Contrôles éditeur | Structure de base (Game1 + Desktop + DockHost) | ✅ | `65c9e960` |
| 2.2 | Contrôles éditeur | Project Launcher | ✅ | `20d27f29` |
| 2.3 | Contrôles éditeur | Content Browser | ✅ | `db022630` |
| 2.4 | Contrôles éditeur | Logs | ✅ | `01e3ef07` |
| 2.5 | Contrôles éditeur | World Viewport | ✅ | `4ebdf1e8` |
| 2.6 | Contrôles éditeur | Entities (hiérarchie) | ✅ | `feat(editor): add EntitiesPanel with entity hierarchy tree` |
| 2.7 | Contrôles éditeur | Entity Details (composants) | ✅ | `feat(editor): add EntityDetailsPanel with component list` |
| 2.8 | Contrôles éditeur | Property editors composants | ✅ | `feat(editor): add component property editors with registry` |
| 3.1 | Moteur | Événements World | ✅ | `feat(engine): ensure World exposes entity change events` |
| 3.2 | Moteur | Événements Entity | ✅ | `feat(engine): ensure Entity exposes component/child change events` |
| 3.3 | Moteur | AssetCatalog vérification | ✅ | `feat(engine): verify AssetCatalog events and data for editor` |
| 3.4 | Moteur | Rendu dans RenderTarget éditeur | ✅ | `docs(editor): verify custom RenderTarget rendering path` |
| 4.1 | Assemblage | Layout principal éditeur | ✅ | `feat(editor): assemble main editor layout with all panels` |
| 4.2 | Assemblage | Système de sélection centralisé | ✅ | `feat(editor): add centralized EditorSelection system` |
| 4.3 | Assemblage | Intégration Project Launcher | ✅ | `feat(editor): integrate ProjectLauncher into editor startup flow` |
| 4.4 | Assemblage | StatusBar | ✅ | `feat(editor): add status bar with panel toggles and info` |
| 4.5 | Assemblage | Persistance layout | ✅ | `feat(editor): add dock layout save/load persistence` |
