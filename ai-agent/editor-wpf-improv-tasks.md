# Editor WPF Improvement Tasks

Incremental cleanup and modernization of `CasaEngine.EditorUI` to follow WPF/MVVM best practices.
Each task is self-contained and must be committed before moving to the next.

---

## Task 1 — Implement `RelayCommand` and `AsyncRelayCommand`

**Goal:** Provide the foundational ICommand implementations that all subsequent MVVM work depends on.

**Steps:**
1. Create `CasaEngine.EditorUI/Commands/RelayCommand.cs` implementing `ICommand` (with `Action<object?>` execute, `Func<object?, bool>?` canExecute).
2. Create `CasaEngine.EditorUI/Commands/AsyncRelayCommand.cs` implementing `ICommand` (with `Func<object?, Task>` execute, `Func<object?, bool>?` canExecute), handling `IsExecuting` flag and re-entrancy prevention.
3. Both classes must be sealed, fully documented, and include null-argument validation.

**Files to create:**
- `Commands/RelayCommand.cs`
- `Commands/AsyncRelayCommand.cs`

**Commit message:** `EditorUI: add RelayCommand and AsyncRelayCommand implementations`

---

## Task 2 — Fix `NotifyPropertyChangeBase` and `ContentBrowserViewModel` INPC consistency

**Goal:** Ensure all ViewModels use the same property change notification base class.

**Steps:**
1. Review `Controls/NotifyPropertyChangeBase.cs` — ensure it has a `SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)` helper that returns `bool`.
2. Make `ContentBrowserViewModel` extend `NotifyPropertyChangeBase` instead of implementing `INotifyPropertyChanged` manually. Remove its own `OnPropertyChanged` method, use inherited `SetField` / `OnPropertyChanged`.
3. Verify no other ViewModels implement `INotifyPropertyChanged` manually — if they do, migrate them too.

**Files to modify:**
- `Controls/NotifyPropertyChangeBase.cs`
- `Controls/ContentBrowser/ContentBrowserViewModel.cs`

**Commit message:** `EditorUI: unify INPC via NotifyPropertyChangeBase in all ViewModels`

---

## Task 3 — Rename `*ModelView` classes to `*ViewModel`

**Goal:** Standardize naming conventions across all ViewModels.

**Steps:**
1. Rename `SpritesModelView` → `SpritesViewModel` (file + class + all references).
2. Rename `Animation2dAssetListModelView` → `Animation2dAssetListViewModel` (file + class + all references).
3. Rename `Animation2dSelectedListModelView` → `Animation2dSelectedListViewModel` (file + class + all references).
4. Fix typo: `ComponenTemplate` → `ComponentTemplate` property in `EntityDetailTemplateSelector.cs`.
5. Update all XAML and code-behind references.

**Files to modify:**
- `Controls/SpriteControls/SpritesModelView.cs` → rename to `SpritesViewModel.cs`
- `Controls/Animation2dControls/Animation2dAssetListModelView.cs` → rename to `Animation2dAssetListViewModel.cs`
- `Controls/EntityControls/ViewModels/Animation2dSelectedListModelView.cs` → rename to `Animation2dSelectedListViewModel.cs`
- `Controls/EntityControls/EntityDetailTemplateSelector.cs`
- Any XAML/code-behind files that reference the old names

**Commit message:** `EditorUI: standardize ViewModel naming and fix ComponenTemplate typo`

---

## Task 4 — Remove unused `RoutedUICommand` declarations from `App.xaml`

**Goal:** Clean up dead code in App.xaml (~40 unused command declarations).

**Steps:**
1. Search the entire `CasaEngine.EditorUI` project for usages of each `RoutedUICommand` defined in `App.xaml` (FlowGraph.*, Graphs.*, Functions.*, NamedVars.*, Scripts.*, GUI.Load, GUI.Save, EditCustomVariable, etc.).
2. Remove all `RoutedUICommand` declarations that have zero usages (no `CommandBinding`, no `Command="{x:Static ..."` in any XAML).
3. Keep only the commands that are actually bound somewhere (likely only `Save`-related commands).

**Files to modify:**
- `App.xaml`

**Commit message:** `EditorUI: remove ~40 unused RoutedUICommand declarations from App.xaml`

---

## Task 5 — Remove dead code, stubs, and commented-out code

**Goal:** Clean up obvious dead code throughout the project.

**Steps:**
1. Remove empty method body `ListBoxFolderContentCreate_Click` in `ContentBrowserControl.xaml.cs` and its XAML reference if any.
2. Remove commented-out event subscriptions in `EntityListViewModel.cs`.
3. Remove test data and `//TODO remove` in `ButtonsMappingControl.OpenButtonsMapping`.
4. Remove commented-out methods in `ExternalToolManager.cs`.
5. Remove stubs `OpenProject_OnClick` and `SaveProject_OnClick` in `MainWindow.xaml.cs` if they are empty/comment-only (or add `// TODO` to clarify intent).
6. Remove duplicate `using` statements: `GizmoTools` in `WorldEditorViewModel.cs`, `Microsoft.Xna.Framework.Input` in `ViewportControl.cs`.
7. Remove or mark as TODO the never-called `SaveEverything` method in `WorldEditorControl.xaml.cs`.

**Files to modify:**
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`
- `Controls/EntityControls/ViewModels/EntityListViewModel.cs`
- `Controls/ButtonsMappingControl.xaml.cs`
- `Plugins/Tools/ExternalToolManager.cs`
- `MainWindow.xaml.cs`
- `Controls/WorldControls/ViewModels/WorldEditorViewModel.cs`
- `Controls/ViewportControl.cs`
- `Controls/WorldControls/WorldEditorControl.xaml.cs`

**Commit message:** `EditorUI: remove dead code, stubs, duplicate usings, and commented-out code`

---

## Task 6 — Standardize comments to English

**Goal:** All code comments in English for consistency.

**Steps:**
1. Search all `.cs` files in `CasaEngine.EditorUI` for French comments (look for words like "Met à jour", "récupère", "Affiche", "Dessine", "supprime", "si le", "paramètre", "renvoie", etc.).
2. Translate all French comments to English.
3. Also translate any French XML doc summaries (`<summary>`) to English.

**Files likely affected:**
- `Controls/EngineHost.cs`
- `Controls/ViewportControl.cs`
- `Inputs/RawMouseProvider.cs`
- Any other files with French comments

**Commit message:** `EditorUI: translate all French comments to English`

---

## Task 7 — Fix `ButtonsMappingControl.DockingManager` null issue

**Goal:** Fix the `DockingManager` property that always returns null, which will crash layout save/load operations.

**Steps:**
1. In `ButtonsMappingControl.xaml.cs`, the `DockingManager` property is an auto-property that is never initialized. Either:
   - a) Find the `DockingManager` in the XAML and reference it with `x:Name`, then assign it in `InitializeComponent()` or use a field.
   - b) If the control doesn't have a `DockingManager` in its XAML, reconsider whether it should extend `EditorControlBase` at all, or add the `DockingManager` to its XAML.
2. If this control doesn't use docking, remove the `EditorControlBase` inheritance and implement `IEditorControl` directly, or make `DockingManager` optional in the base class.

**Files to modify:**
- `Controls/ButtonsMappingControl.xaml.cs`
- `Controls/ButtonsMappingControl.xaml`
- Possibly `Controls/EditorControlBase.cs` (if making DockingManager optional)

**Commit message:** `EditorUI: fix ButtonsMappingControl.DockingManager null issue`

---

## Task 8 — Extract duplicated `EngineHost` startup pattern into base class

**Goal:** Eliminate the duplicated `EngineHost.Instance?.IsStarted` check pattern from 7+ files.

**Steps:**
1. In `EditorControlBase.cs` (or a new base class), add a pattern:
   ```csharp
   protected void WhenEngineHostStarted(Action callback)
   {
       if (EngineHost.Instance?.IsStarted == true)
           callback();
       else
           EngineHost.InstanceStarted += (_, _) => callback();
   }
   ```
2. Replace the duplicated pattern in all affected controls:
   - `GameEditorEntityControl.xaml.cs`
   - `GameEditorWorldControl.xaml.cs`
   - `SpriteEditorControl.xaml.cs`
   - `GameEditorSpriteControl.xaml.cs` (if applicable)
   - `Animation2dEditorControl.xaml.cs`
   - `GameEditorAnimation2dControl.xaml.cs` (if applicable)
   - `TileMapEditorControl.xaml.cs`
   - `GameEditorTileMapControl.xaml.cs` (if applicable)

**Commit message:** `EditorUI: extract WhenEngineHostStarted helper to eliminate duplication`

---

## Task 9 — Extract duplicated `LayoutSerializationCallback` switch into base class

**Goal:** Eliminate the duplicated layout serialization switch pattern from 6+ files.

**Steps:**
1. In `EditorControlBase.cs`, add a virtual method or dictionary-based approach for resolving standard anchorable content (Logs, Content Browser):
   ```csharp
   protected virtual void OnLayoutSerializationCallback(LayoutSerializationCallbackEventArgs args)
   {
       switch (args.Model.ContentId)
       {
           case "Logs": args.Content = new LogsControl(); break;
           case "ContentBrowser": args.Content = new ContentBrowserControl(); break;
       }
   }
   ```
2. Subclasses override and call `base.OnLayoutSerializationCallback(args)` before handling their own content IDs.
3. Update all `EditorControlBase` subclasses to remove the duplicated switch cases for "Logs" and "ContentBrowser" and use the base method instead.

**Files to modify:**
- `Controls/EditorControlBase.cs`
- `Controls/WorldControls/WorldEditorControl.xaml.cs`
- `Controls/EntityControls/EntityEditorControl.xaml.cs`
- `Controls/SpriteControls/SpriteEditorControl.xaml.cs`
- `Controls/Animation2dControls/Animation2dEditorControl.xaml.cs`
- `Controls/TileMapControls/TileMapEditorControl.xaml.cs`
- `Controls/ButtonsMappingControl.xaml.cs`

**Commit message:** `EditorUI: extract common LayoutSerializationCallback into EditorControlBase`

---

## Task 10 — Extract duplicated `SelectTreeViewItem<T>` into a shared utility

**Goal:** Remove the duplicated TreeView selection helper.

**Steps:**
1. Add a generic `SelectTreeViewItem<T>` method to `WpfUtils.cs`:
   ```csharp
   public static bool SelectTreeViewItem<T>(ItemsControl parent, T target) where T : class
   ```
2. Replace the duplicate implementations in:
   - `EntitiesControl.xaml.cs` (for `EntityViewModel`)
   - `EntityControl.xaml.cs` (for `ComponentViewModel`)
3. Also remove the duplicate `FindVisualChild<T>` in `ContentBrowserControl.xaml.cs` — use the existing `WpfUtils.FindElementWithType` or similar utility instead.

**Files to modify:**
- `Controls/WpfUtils.cs`
- `Controls/EntityControls/EntitiesControl.xaml.cs`
- `Controls/EntityControls/EntityControl.xaml.cs`
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`

**Commit message:** `EditorUI: extract generic SelectTreeViewItem and remove duplicated WpfUtils`

---

## Task 11 — Extract generic `AssetListViewModel<T>`

**Goal:** Eliminate near-identical asset list ViewModel classes.

**Steps:**
1. Create `Controls/Common/AssetListViewModel.cs` — a generic ViewModel that subscribes to `AssetCatalog` events, filters by file extension, and exposes `ObservableCollection<AssetInfoViewModel>`.
2. Make `SpritesViewModel` (renamed in Task 3) extend `AssetListViewModel` or become a thin wrapper.
3. Make `Animation2dAssetListViewModel` (renamed in Task 3) extend `AssetListViewModel` or become a thin wrapper.
4. Ensure both still work correctly with their respective editor controls.

**Files to create:**
- `Controls/Common/AssetListViewModel.cs`

**Files to modify:**
- `Controls/SpriteControls/SpritesViewModel.cs`
- `Controls/Animation2dControls/Animation2dAssetListViewModel.cs`

**Commit message:** `EditorUI: extract generic AssetListViewModel to eliminate duplication`

---

## Task 12 — Fix static event subscription memory leaks

**Goal:** Prevent memory leaks from static event subscriptions that are never unsubscribed.

**Steps:**
1. In `EntityViewModel.cs`: unsubscribe from `AssetCatalog.AssetRenamed` — either implement `IDisposable`, use `WeakEventManager`, or unsubscribe in a cleanup method.
2. In `SpritesViewModel.cs` (or whatever it is named after Task 3): same treatment for `AssetCatalog.AssetAdded/AssetRemoved/AssetCleared`.
3. In `Animation2dAssetListViewModel.cs`: same treatment.
4. In `ContentBrowserControl.xaml.cs`: unsubscribe from `AssetCatalog.AssetRenamed` in `Unloaded` event.
5. In `ContentBrowserViewModel.cs`: same treatment.
6. Prefer `WeakEventManager` pattern where `IDisposable` is impractical.

**Files to modify:**
- `Controls/EntityControls/ViewModels/EntityViewModel.cs`
- `Controls/SpriteControls/SpritesViewModel.cs` (or its renamed version)
- `Controls/Animation2dControls/Animation2dAssetListViewModel.cs` (or its renamed version)
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`
- `Controls/ContentBrowser/ContentBrowserViewModel.cs`

**Commit message:** `EditorUI: fix static event subscription memory leaks with proper unsubscribe`

---

## Task 13 — Add error handling to file operations

**Goal:** Prevent silent failures and crashes from unhandled file I/O exceptions.

**Steps:**
1. In `ContentBrowserControl.xaml.cs`: wrap `File.Copy`, `modelLoader.LoadAsset`, `AssetSaver.SaveAsset` in `ImportAssetFile` with try/catch. Show `MessageBox.Show(...)` on error.
2. In `FolderItem.cs`: wrap `Directory.Delete` in try/catch.
3. In `ContentItem.cs`: wrap `File.Move` (name setter) and `File.Delete` in try/catch.
4. In `ProjectLauncherWindow.xaml.cs`: wrap JSON deserialization in `LoadMostRecentProjects` with try/catch.
5. Add a simple helper method (e.g., `UiErrorHandler.ShowError(string message, Exception ex)`) that logs and shows a dialog, to standardize error reporting.

**Files to create:**
- `Helpers/UiErrorHandler.cs` (optional — or inline the pattern)

**Files to modify:**
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`
- `Controls/ContentBrowser/FolderItem.cs`
- `Controls/ContentBrowser/ContentItem.cs`
- `ProjectLauncherWindow.xaml.cs`

**Commit message:** `EditorUI: add error handling to file operations with user-facing messages`

---

## Task 14 — Remove unsafe reflection in `EditorControlBase`

**Goal:** Remove fragile reflection call and fix null-safety issues.

**Steps:**
1. In `EditorControlBase.ShowControl` (line ~58-60): replace reflection call to `RemoveLogicalChild` with a safer approach. Options:
   - Use `LogicalTreeHelper` if available.
   - Use public API: `RemoveLogicalChild` is protected, so call it directly since `EditorControlBase` is a `UserControl`.
   - Or restructure so the logical child is managed by the content presenter.
2. Add null checks for `_game` and `_gizmoComponent` in `EntityControl.xaml.cs` and `EntitiesControl.xaml.cs` wherever they are dereferenced.
3. Add null checks for `DataContext as SomeType` casts throughout `EntitiesControl` and `EntityControl`.

**Files to modify:**
- `Controls/EditorControlBase.cs`
- `Controls/EntityControls/EntityControl.xaml.cs`
- `Controls/EntityControls/EntitiesControl.xaml.cs`

**Commit message:** `EditorUI: remove unsafe reflection and add null-safety guards`

---

## Task 15 — Break visual-tree coupling (AssetSelectorControl → MainWindow)

**Goal:** Remove tight coupling where controls walk the visual tree to find `MainWindow` and access its child controls.

**Steps:**
1. In `AssetSelectorControl.xaml.cs`: replace `this.FindParent<MainWindow>().ContentBrowserControl` with either:
   - An event/callback pattern (e.g., `AssetSelectionRequested` event).
   - A service interface (`IContentBrowserService`) that provides the needed functionality.
2. In `ContentBrowserControl.xaml.cs`: replace `window.GetEditorControl<T>()` visual-tree navigation with a similar service/event approach.
3. This may require introducing a simple mediator or event aggregator (a singleton or injected `IEventAggregator`).

**Files to modify:**
- `Controls/Common/AssetSelectorControl.xaml.cs`
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`

**Files to create (optional):**
- `Services/IContentBrowserService.cs`
- `Services/IEventAggregator.cs` (if needed — or use a simple event bus)

**Commit message:** `EditorUI: decouple AssetSelectorControl from MainWindow visual tree`

---

## Task 16 — Create `MainWindowViewModel` and migrate menu logic

**Goal:** Add a proper ViewModel for MainWindow.

**Steps:**
1. Create `MainWindowViewModel.cs` with commands for: NewProject, OpenProject, SaveProject, Exit.
2. Expose a `CurrentEditorView` property (for tab/view switching).
3. Move project loading logic from `MainWindow.xaml.cs` code-behind into the ViewModel.
4. Update `MainWindow.xaml` to bind menu items to ViewModel commands instead of Click handlers.
5. Set `DataContext = new MainWindowViewModel()` in the constructor (or via DI later).

**Files to create:**
- `MainWindowViewModel.cs`

**Files to modify:**
- `MainWindow.xaml`
- `MainWindow.xaml.cs`

**Commit message:** `EditorUI: create MainWindowViewModel and migrate menu commands to MVVM`

---

## Task 17 — Create `ProjectLauncherViewModel` and migrate logic

**Goal:** Add a proper ViewModel for the project launcher window.

**Steps:**
1. Create `ProjectLauncherViewModel.cs` with:
   - `ObservableCollection<string> RecentProjects`
   - `string SelectedProject` property
   - `RelayCommand OpenProjectCommand`
   - `RelayCommand CreateProjectCommand`
   - `RelayCommand LaunchEditorCommand`
   - Methods: `LoadMostRecentProjects()`, `SaveMostRecentProjects()`
2. Move all logic from `ProjectLauncherWindow.xaml.cs` code-behind into the ViewModel.
3. Update `ProjectLauncherWindow.xaml` bindings.

**Files to create:**
- `ProjectLauncherViewModel.cs`

**Files to modify:**
- `ProjectLauncherWindow.xaml`
- `ProjectLauncherWindow.xaml.cs`

**Commit message:** `EditorUI: create ProjectLauncherViewModel and migrate to MVVM`

---

## Task 18 — Migrate `GameEditorWorldControl` button handlers to commands

**Goal:** Replace gizmo mode Click handlers with ViewModel commands.

**Steps:**
1. In `WorldEditorViewModel.cs` (already exists), add:
   - `RelayCommand TranslateCommand`, `RotateCommand`, `ScaleCommand`
   - `GizmoMode SelectedGizmoMode` property (with INPC)
   - `RelayCommand LaunchGameCommand`
   - `RelayCommand TogglePhysicsDebugCommand`
   - Logic for entity creation (currently in `CreateEntity` code-behind method)
2. Update `GameEditorWorldControl.xaml` to bind buttons to these commands.
3. Remove corresponding Click handlers from code-behind.
4. Move the `DataTrigger` styles for gizmo mode buttons into a shared `ResourceDictionary` at `Resources/GizmoButtonStyles.xaml`.

**Files to modify:**
- `Controls/WorldControls/ViewModels/WorldEditorViewModel.cs`
- `Controls/WorldControls/GameEditorWorldControl.xaml`
- `Controls/WorldControls/GameEditorWorldControl.xaml.cs`

**Files to create:**
- `Resources/GizmoButtonStyles.xaml` (optional, for shared styles)

**Commit message:** `EditorUI: migrate GameEditorWorldControl to ViewModel commands`

---

## Task 19 — Extract content browser business logic into a service

**Goal:** Move the 450+ lines of import/creation logic out of `ContentBrowserControl.xaml.cs`.

**Steps:**
1. Create `Services/ContentBrowserService.cs` with methods:
   - `ImportAssetFile(string sourcePath, string targetFolder)`
   - `ImportTexturesFromModel(string modelPath, string targetFolder)`
   - `ImportAnimationsFromModel(string modelPath, string targetFolder)`
   - `CreateAsset(AssetType type, string folder)`
   - All file I/O should be wrapped in try/catch with meaningful error returns.
2. Move the import pipeline logic from `ContentBrowserControl.xaml.cs` into this service.
3. Add commands to `ContentBrowserViewModel` that call the service methods.
4. Update `ContentBrowserControl.xaml.cs` to thin code-behind: only visual logic (drag-drop visual feedback, context menu assembly).

**Files to create:**
- `Services/ContentBrowserService.cs`

**Files to modify:**
- `Controls/ContentBrowser/ContentBrowserControl.xaml.cs`
- `Controls/ContentBrowser/ContentBrowserViewModel.cs`

**Commit message:** `EditorUI: extract content browser business logic into ContentBrowserService`

---

## Task 20 — Extract entity management logic from `EntitiesControl` and `EntityControl` code-behind

**Goal:** Move entity copy/paste/delete, component creation, and gizmo management out of code-behind.

**Steps:**
1. Create or extend `EntityListViewModel` with commands:
   - `CopyEntityCommand`, `PasteEntityCommand`, `DeleteEntityCommand`
   - `DuplicateEntityCommand`
   - Selection management (currently in code-behind).
2. Create or extend `EntityViewModel` or a service with:
   - `AddComponentCommand`
   - `RemoveComponentCommand`
   - `RenameEntityCommand`
3. Move gizmo selection logic into the ViewModel or a `GizmoService`.
4. Reduce `EntitiesControl.xaml.cs` from ~302 lines to ~50 (visual-only).
5. Reduce `EntityControl.xaml.cs` from ~305 lines to ~50.

**Files to modify:**
- `Controls/EntityControls/ViewModels/EntityListViewModel.cs`
- `Controls/EntityControls/ViewModels/EntityViewModel.cs`
- `Controls/EntityControls/EntitiesControl.xaml.cs`
- `Controls/EntityControls/EntitiesControl.xaml`
- `Controls/EntityControls/EntityControl.xaml.cs`
- `Controls/EntityControls/EntityControl.xaml`

**Commit message:** `EditorUI: extract entity management logic into ViewModels`

---

## Task 21 — Clean up `WpfUtils.cs` overlapping methods

**Goal:** Consolidate the 727-line utility file with many near-identical visual tree traversal methods.

**Steps:**
1. Audit all methods in `WpfUtils.cs` — identify pairs/groups that do the same thing with different signatures.
2. Keep the most general-purpose versions. Remove or refactor overlapping methods:
   - Consolidate `FindParentWithType` / `FindVisualParentWithType` / `FindAncestor`
   - Consolidate `FindParentWithDataContext` / `FindParentWithDataContextAndName` / `FindParentWithTypeAndDataContext`
3. Update all callers to use the surviving methods.
4. Add XML doc comments to the remaining methods.

**Files to modify:**
- `Controls/WpfUtils.cs`
- Any files that reference removed/renamed methods

**Commit message:** `EditorUI: consolidate WpfUtils visual-tree traversal methods`

---

## Task 22 — Add shared `ResourceDictionary` for common styles/templates

**Goal:** Eliminate inline style/template duplication across XAML files.

**Steps:**
1. Create `Resources/SharedStyles.xaml` resource dictionary.
2. Move common DataTemplates from `ContentBrowserControl.xaml` into the resource dictionary.
3. Move the repeated `DataTrigger` gizmo button styles from `GameEditorWorldControl.xaml`.
4. Add the resource dictionary to `App.xaml`'s `Application.Resources` merged dictionaries.
5. Update XAML files to reference styles by `StaticResource` key.

**Files to create:**
- `Resources/SharedStyles.xaml`

**Files to modify:**
- `App.xaml`
- `Controls/ContentBrowser/ContentBrowserControl.xaml`
- `Controls/WorldControls/GameEditorWorldControl.xaml`

**Commit message:** `EditorUI: add SharedStyles.xaml and extract common styles/templates`

---

## Task 23 — Fix `EntityComponentControl` forced binding updates

**Goal:** Remove the per-frame `GetBindingExpression().UpdateTarget()` hack.

**Steps:**
1. Investigate why the underlying model doesn't raise `PropertyChanged` for the position/rotation/scale values displayed in `EntityComponentControl`.
2. If the source is a `SceneComponent` or `RootNodeComponent` that changes every frame (from engine), either:
   - a) Make the ViewModel poll the engine values and raise `PropertyChanged` via a timer/dispatcher (not every frame), OR
   - b) Use a `DispatcherTimer` at a reasonable interval (e.g., 10 Hz) instead of every-frame polling, OR
   - c) Make the engine component raise change notifications properly.
3. Remove the `OnFrameComputed` manual `UpdateTarget()` calls.

**Files to modify:**
- `Controls/EntityControls/EntityComponentControl.xaml.cs`
- Possibly `Controls/EntityControls/ViewModels/RootNodeComponentViewModel.cs` or `SceneComponentViewModel.cs`

**Commit message:** `EditorUI: replace forced binding updates with proper INPC in EntityComponentControl`

---

## Task 24 — Evaluate and clean up the Plugins/Tools system

**Goal:** Determine if the external tools plugin system is used, and clean it up or remove it.

**Steps:**
1. Search the entire solution for usages of `CustomEditor`, `ExternalTool`, `ExternalToolManager`, `IExternalTool`, `ElementRegister`.
2. If they are used:
   - Remove commented-out methods in `ExternalToolManager.cs`
   - Add proper error handling
   - Document the plugin interface
3. If they are NOT used:
   - Delete the entire `Plugins/Tools/` directory
   - Remove any references from `.csproj`

**Files to evaluate:**
- `Plugins/Tools/CustomEditor.cs`
- `Plugins/Tools/ElementRegister.cs`
- `Plugins/Tools/ExternalTool.cs`
- `Plugins/Tools/ExternalToolManager.cs`
- `Plugins/Tools/IExternalTool.cs`

**Commit message:** `EditorUI: clean up or remove unused Plugins/Tools system`

---

## Dependencies

```
Task 1 (RelayCommand) ──► Task 16, 17, 18, 19, 20 (all command migrations)
Task 2 (INPC base)    ──► Task 11 (generic AssetListViewModel)
Task 3 (rename)       ──► Task 11 (generic AssetListViewModel), Task 12 (event leaks)
Tasks 4-10            ──  Independent, can be done in any order
Task 15 (decouple)    ──  Independent
Task 21-24            ──  Independent
```

Execute tasks **in order** unless you have a specific reason to reorder. Tasks 1-3 are foundational. Tasks 4-14 are independent cleanups. Tasks 15-24 are larger MVVM migrations.
