using CasaEngine.Core.Logging;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.ContextualPanels;
using CasaEngine.Editor.Docking;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.ProjectLauncher;
using CasaEngine.Editor.Workspaces;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.UI.Backend.MonoGame;
using CasaEngine.Framework.Input;

using FontStashSharp;
using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor;

public class GameEditor : Game, IObservableUpdate
{
    private const string EditorLayoutDirectoryName = ".casaeditor";
    private const string EditorLayoutFileName = "layout.editor.json";
    private const string EditorThemeName = "CasaEditor.Dark";
    private const string EditorThemeAssetRelativePath = @"Content\UI\Themes\CasaEditor.Dark.Theme.xaml";
    private const string EditorControlTemplatesAssetRelativePath = @"Content\UI\Templates\CasaEditor.Dark.ControlTemplates.xaml";

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // ── MGUI core ──────────────────────────────────────────────────────
    private MGDesktop _desktop;
    private FontStashSharpTextEngine _fontStashSharpEngine;

    // ── Main editor window ─────────────────────────────────────────────
    private MGWindow _mainWindow;
    private MGDockPanel _rootPanel;
    private MGDockHost _dockHost;
    private MGMenuBar _menuBar;
    private EditorPanelRegistry _panelRegistry;

    // ── Editor panels ──────────────────────────────────────────────────
    private LoggerEditor _loggerEditor;
    private EngineRuntimeContext _editorRuntimeContext;
    private readonly EditorContextService _editorContext = EditorContextService.Current;
    private readonly EditorHistoryService _editorHistory = EditorHistoryService.Current;
    private readonly EditorDirtyStateService _editorDirtyState = EditorDirtyStateService.Current;
    private HostedEditorGameAdapter _editorRuntime;
    private WorldViewportPanel _worldViewportPanel;
    private MGElement _worldViewportContent;
    private ContextualDockPanelHost? _hierarchyPanelHost;
    private MGElement? _hierarchyContent;
    private EntitiesPanel _entitiesPanel;
    private MGElement _entitiesContent;
    private MaterialHierarchyPanel? _materialHierarchyPanel;
    private MGElement? _materialHierarchyContent;
    private ContextualDockPanelHost? _inspectorPanelHost;
    private MGElement? _inspectorContent;
    private EntityDetailsPanel _entityDetailsPanel;
    private MGElement _entityDetailsContent;
    private MaterialInspectorView? _materialInspectorView;
    private MGElement? _materialInspectorContent;
    private ContextualDockPanelHost? _toolboxPanelHost;
    private MGElement? _toolboxContent;
    private ContentBrowserPanel _contentBrowserPanel;
    private MGElement _contentBrowserContent;
    private readonly Dictionary<string, UIScreenPreviewPanel> _screenPreviewPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _screenPreviewPanelTitles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MaterialAssetInspectorPanel> _materialInspectorPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimationClipPreviewPanel> _animationClipPreviewPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldViewportPanel> _materialViewportPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _materialInspectorPanelTitles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _animationClipPreviewPanelTitles = new(StringComparer.Ordinal);
    private MaterialAssetInspectorPanel _activeMaterialInspectorPanel;
    private readonly EditorServices.ScreenEditor.Selection.UIScreenSelectionService _screenSelection = new();
    private readonly Dictionary<string, UICommandStack> _screenCommandStacks = new(StringComparer.Ordinal);
    private UIScreenHierarchyPanel? _screenHierarchyPanel;
    private MGElement? _screenHierarchyContent;
    private UIScreenInspectorPanel? _screenInspectorPanel;
    private MGElement? _screenInspectorContent;
    private UIScreenToolboxPanel? _screenToolboxPanel;
    private MGElement? _screenToolboxContent;
    // tracks the most-recently-opened screen for hierarchy-level edits
    private UIScreenPreviewPanel? _activeScreenPreviewPanel;
    private string? _nodeClipboard; // JSON-serialized node subtree for copy/paste
    private LogsPanel _logsPanel;
    private MGElement _logsContent;
    private Action? _pendingProjectLauncherAction;
    private FrameCachedWindowInputSource _windowInputSource;
    private readonly EditorSelection _editorSelection = EditorSelection.Current;
    private bool _isSynchronizingSelection;
    private readonly EditorAutomationOptions _automationOptions;
    private KeyboardState _previousShortcutKeyboardState;
    private bool _automationWorldLoaded;
    private bool _automationSelectionApplied;
    private bool _automationDiagnosticsCaptured;
    private TimeSpan _automationSelectionAppliedAt;
    private bool _automationAssetOpenAttempted;
    private bool _automationAssetOpened;
    private TimeSpan _automationAssetOpenedAt;
    private bool _automationMaterialEditAttempted;
    private bool _automationMaterialEdited;
    private TimeSpan _automationMaterialEditedAt;
    private readonly Dictionary<string, string> _automationEditedFileSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private bool _automationEditedFilesRestored;

    // ── IObservableUpdate (required by CasaGameRenderHost<Game1>) ──────
    public event EventHandler<TimeSpan> PreviewUpdate;
    public event EventHandler<EventArgs> EndUpdate;

    public GameEditor(EditorAutomationOptions? automationOptions = null)
    {
        _automationOptions = automationOptions ?? new EditorAutomationOptions();
        _editorHistory.HistoryChanged += OnEditorHistoryChanged;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.GraphicsProfile = GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef)
            ? GraphicsProfile.HiDef
            : GraphicsProfile.Reach;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "CasaEngine Editor";
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1600;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.ApplyChanges();

        // Load icons early so panel ContentFactory lambdas have textures available
        EditorIcons.Load(Content);

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _windowInputSource = new FrameCachedWindowInputSource(new Win32WindowInputSource(() => Window.Handle));
        const string familyName = "JetBrainsMono";
        string ttfDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"Content\fonts\JetBrainsMono"));
        var backend = CasaMonoGameBackendBootstrap.Create(new CasaGameRenderHost<GameEditor>(this), _windowInputSource);
        if (backend.Runtime is not IMonoGameDesktopBackend monoGameBackend)
        {
            throw new InvalidOperationException("Editor desktop requires a MonoGame-backed MGUI runtime.");
        }

        _fontStashSharpEngine = new FontStashSharpTextEngine();

        byte[] arialBytes = File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-Regular.ttf"));
        FontSystem arialNormal = new FontSystem();
        arialNormal.AddFont(arialBytes);
        _fontStashSharpEngine.AddFontSystem(familyName, CustomFontStyles.Normal, arialNormal, arialBytes);

        FontSystem arialBold = new FontSystem();
        arialBold.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-Bold.ttf")));
        _fontStashSharpEngine.AddFontSystem(familyName, CustomFontStyles.Bold, arialBold);

        FontSystem arialItalic = new FontSystem();
        arialItalic.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-BoldItalic.ttf")));
        _fontStashSharpEngine.AddFontSystem(familyName, CustomFontStyles.Italic, arialItalic);

        // Calibrate per-size advance widths to match SpriteFontTextEngine exactly.
        // Must be called after FontSizeScale is set (via AddFontSystem overload above).
        monoGameBackend.FontManager.DefaultFontFamily = familyName;
        _fontStashSharpEngine.MatchSpriteFontSizing(monoGameBackend.FontManager);
        backend.Runtime.TextEngine = _fontStashSharpEngine;

        _desktop = new MGDesktop(backend.Runtime);
        _desktop.LoadDefaultResources();

        // Register editor logger
        _loggerEditor = new LoggerEditor();
        Logs.AddLogger(_loggerEditor);
        Logs.AddLogger(new DebugLogger());
        Logs.Verbosity = LogVerbosity.Trace;

        TryLoadEditorThemeAssets();

        InitializeEditorRuntime();

        // Main window (borderless, fills the screen)
        _mainWindow = new MGWindow(_desktop, 0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight)
        {
            WindowStyle = WindowStyle.None,
            BackgroundBrush = _desktop.Theme.GetBackgroundBrush(MGElementType.Window),
            TitleText = string.Empty,
            IsTitleBarVisible = false,
            IsCloseButtonVisible = false,
            IsDraggable = false,
            IsUserResizable = false
        };

        // Resize main window with the game window
        Window.ClientSizeChanged += (s, e) =>
        {
            _mainWindow.Left = 0;
            _mainWindow.Top = 0;
            _mainWindow.WindowWidth = Window.ClientBounds.Width;
            _mainWindow.WindowHeight = Window.ClientBounds.Height;
        };

        // Layout shell: editor chrome is added only once a project has actually been opened.
        _rootPanel = new MGDockPanel(_mainWindow);
        _rootPanel.Name = "EditorRootPanel";
        _mainWindow.SetContent(_rootPanel);

        _desktop.Windows.Add(_mainWindow);

        HookViewportInputCoordination();

        _editorSelection.SelectionChanged += OnEditorSelectionChanged;
        _editorSelection.WorldSelectionChanged += OnEditorWorldSelectionChanged;
        _editorSelection.ComponentSelectionChanged += OnEditorComponentSelectionChanged;
        _screenSelection.SelectionChanged += OnScreenSelectionChanged;
        _screenSelection.MultiSelectionChanged += _ => OnScreenSelectionChanged(_screenSelection.SelectedNodeId);
        _editorDirtyState.DirtyStateChanged += OnDirtyStateChanged;

        EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;
        EditorAssetWriterService.AssetSaved += OnEditorAssetSaved;

        if (_automationOptions.HasAutomation)
        {
            QueueProjectOpen(_automationOptions.ProjectPath!);
        }
        else
        {
            ShowProjectLauncher();
        }

        base.Initialize();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RestoreAutomationEditedFilesIfNeeded();
            EditorAssetWriterService.AssetSaved -= OnEditorAssetSaved;
            _editorDirtyState.DirtyStateChanged -= OnDirtyStateChanged;
            if (_dockHost != null)
            {
                _dockHost.ActivePanelChanged -= OnDockHostActivePanelChanged;
                _dockHost.PanelRemoved -= OnDockHostPanelRemoved;
            }

            foreach (var materialInspectorPanel in _materialInspectorPanels.Values)
            {
                materialInspectorPanel.Dispose();
            }

            foreach (var animationClipPreviewPanel in _animationClipPreviewPanels.Values)
            {
                animationClipPreviewPanel.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void BuildMenuBar()
    {
        // File menu
        _menuBar.AddItem("File", item =>
        {
            item.Submenu = new MGContextMenu(_mainWindow, null);
            item.Submenu.AddButton("New Project", _ => OpenProjectLauncher());
            item.Submenu.AddButton("Open Project", _ => OpenProjectLauncher());
            item.Submenu.AddSeparator();
            item.Submenu.AddButton("Save", _ => SaveCurrentProject());
            item.Submenu.AddSeparator();
            item.Submenu.AddButton("Exit", _ => Exit());
        });

        // Edit menu
        _menuBar.AddItem("Edit", item =>
        {
            item.Submenu = new MGContextMenu(_mainWindow, null)
            {
                ItemsFactory = BuildEditMenu,
            };
        });

        // Windows menu
        _menuBar.AddItem("Windows", item =>
        {
            item.Submenu = new MGContextMenu(_mainWindow, null);
            item.Submenu.AddButton("Save Layout", _ => SaveDockLayout());
            item.Submenu.AddButton("Load Layout", _ => LoadDockLayout());
            item.Submenu.AddSeparator();
            item.Submenu.AddButton("Reset Layout", _ => ResetDockLayout());
        });

        // Help menu
        _menuBar.AddItem("Help", item =>
        {
            item.Submenu = new MGContextMenu(_mainWindow, null);
            item.Submenu.AddButton("About CasaEngine", _ => { });
        });
    }

    private void BuildEditMenu(MGContextMenu menu)
    {
        var undoButton = menu.AddButton(BuildHistoryMenuLabel("Undo", _editorHistory.UndoDescription), _ => ExecuteUndo());
        undoButton.IsEnabled = _editorHistory.CanUndo;

        var redoButton = menu.AddButton(BuildHistoryMenuLabel("Redo", _editorHistory.RedoDescription), _ => ExecuteRedo());
        redoButton.IsEnabled = _editorHistory.CanRedo;

        menu.AddSeparator();
        menu.AddButton("Duplicate", _ => ExecuteDuplicate());
        menu.AddSeparator();
        menu.AddButton("Cut", _ => ExecuteCut());
        menu.AddButton("Copy", _ => ExecuteCopy());
        menu.AddButton("Paste", _ => ExecutePaste());
    }

    private static string BuildHistoryMenuLabel(string prefix, string? description)
        => string.IsNullOrWhiteSpace(description) ? prefix : $"{prefix} {description}";

    private void SetupInitialDockLayout()
    {
        if (_dockHost == null)
        {
            return;
        }

        _panelRegistry ??= CreatePanelRegistry();
        ResetDockLayoutToDefault();
    }

    private void OnProjectLoaded(object? sender, EventArgs e)
    {
        _editorHistory.ClearAll();
        _editorDirtyState.ClearAll();
        SynchronizeEditorRuntimeContext();
        _editorSelection.Clear();
        _screenSelection.ClearSelection();
        _activeScreenPreviewPanel = null;
        SetActiveMaterialInspectorPanel(null);
        _editorContext.SetActiveDocument(EditorDocumentContext.Empty);
        _editorContext.ClearSelection();
        _editorHistory.Deactivate();
        _automationWorldLoaded = false;
        _automationSelectionApplied = false;
        _automationAssetOpenAttempted = false;
        _automationAssetOpened = false;
        _automationMaterialEditAttempted = false;
        _automationMaterialEdited = false;
        _automationDiagnosticsCaptured = false;
        PresentLoadedProject();
    }

    private void OnEditorAssetSaved(object? sender, EditorAssetSavedEventArgs e)
    {
        if (!e.RelativePath.EndsWith(".material", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MaterialAsset? savedMaterialAsset = TryGetSavedMaterialAssetForHotReload(e);
        RefreshSavedMaterialInspectorPanels(e);

        if (_editorRuntime == null)
        {
            return;
        }

        Guid materialAssetId = e.AssetId != Guid.Empty
            ? e.AssetId
            : ResolveCatalogMaterialAssetId(e.RelativePath);
        if (materialAssetId == Guid.Empty)
        {
            return;
        }

        var hotReloadMetrics = savedMaterialAsset != null
            ? _editorRuntime.ReloadMaterialAsset(materialAssetId, savedMaterialAsset)
            : _editorRuntime.ReloadMaterialAsset(materialAssetId);
        EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
            $"[Editor] Reloaded material asset='{e.RelativePath}' id='{materialAssetId}' affectedMaterials={hotReloadMetrics.AffectedMaterialCount} invalidatedRuntimeMaterials={hotReloadMetrics.InvalidatedRuntimeMaterialCount} invalidatedAuthoringMaterials={hotReloadMetrics.InvalidatedAuthoringMaterialCount} refreshedStaticModelComponents={hotReloadMetrics.RefreshedStaticModelComponentCount} recalculatedOverrideSlots={hotReloadMetrics.RecalculatedOverrideSlotCount} authoringCacheHits={hotReloadMetrics.AuthoringMaterialCacheHitCount} authoringCacheMisses={hotReloadMetrics.AuthoringMaterialCacheMissCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");
    }

    private void RefreshSavedMaterialInspectorPanels(EditorAssetSavedEventArgs e)
    {
        if (e.SaveSource == EditorAssetSaveSource.MaterialInspectorPanel)
        {
            return;
        }

        string normalizedRelativePath = NormalizeRelativePath(e.RelativePath);
        foreach (var materialInspectorPanel in _materialInspectorPanels.Values)
        {
            if (!IsMatchingMaterialInspectorPanel(materialInspectorPanel, e.AssetId, normalizedRelativePath))
            {
                continue;
            }

            if (materialInspectorPanel.ReloadFromDisk()
                && TryGetMaterialInspectorPanelId(materialInspectorPanel, out var panelId))
            {
                var historyContext = new EditorHistoryContext(EditorHistoryContextKind.Material, panelId);
                _editorHistory.Clear(historyContext);
                _editorDirtyState.MarkSaved(historyContext);
            }

            UpdateDockPanelTitleForMaterialInspector(materialInspectorPanel);
        }
    }

    private MaterialAsset? TryGetSavedMaterialAssetForHotReload(EditorAssetSavedEventArgs e)
    {
        if (e.SaveSource != EditorAssetSaveSource.MaterialInspectorPanel)
        {
            return null;
        }

        string normalizedRelativePath = NormalizeRelativePath(e.RelativePath);
        foreach (var materialInspectorPanel in _materialInspectorPanels.Values)
        {
            if (!IsMatchingMaterialInspectorPanel(materialInspectorPanel, e.AssetId, normalizedRelativePath))
            {
                continue;
            }

            if (materialInspectorPanel.LoadedMaterialAsset != null)
            {
                return materialInspectorPanel.LoadedMaterialAsset;
            }
        }

        return null;
    }

    private static bool IsMatchingMaterialInspectorPanel(MaterialAssetInspectorPanel materialInspectorPanel, Guid assetId, string normalizedRelativePath)
    {
        var loadedMaterialAsset = materialInspectorPanel.LoadedMaterialAsset;
        if (assetId != Guid.Empty
            && loadedMaterialAsset != null
            && (loadedMaterialAsset.AssetId == assetId || loadedMaterialAsset.Id == assetId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(materialInspectorPanel.LoadedRelativePath))
        {
            return false;
        }

        return string.Equals(
            NormalizeRelativePath(materialInspectorPanel.LoadedRelativePath),
            normalizedRelativePath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static Guid ResolveCatalogMaterialAssetId(string relativePath)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        var assetInfo = AssetCatalog.GetByFileName(normalizedRelativePath)
                        ?? AssetCatalog.GetByFileName(normalizedRelativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return assetInfo?.Id ?? Guid.Empty;
    }

    private void PresentLoadedProject()
    {
        EnsureShellChromeInitialized();
        EnsureDockHostInitialized();

        if (!TryLoadPersistedDockLayout(logOutcome: false))
        {
            ResetDockLayoutToDefault();
        }

        _ = GetOrCreateWorldViewportContent();
        _ = GetOrCreateHierarchyContent();
        _ = GetOrCreateInspectorContent();
        _ = GetOrCreateToolboxContent();
        _ = GetOrCreateContentBrowserContent();

        _contentBrowserPanel?.Refresh();
        ActivateWorldDocument();
        ActivateDockPanel(EditorPanelIds.ContentBrowser);
        LoadInitialWorldIntoEditorRuntime();
        UpdateWindowTitle();
    }

    private void EnsureShellChromeInitialized()
    {
        if (_menuBar == null)
        {
            _menuBar = new MGMenuBar(_mainWindow);
            BuildMenuBar();
            _rootPanel.TryAddChild(_menuBar, Dock.Top);
        }
    }

    private void UpdateWindowTitle()
    {
        Window.Title = string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.ProjectName)
            ? "CasaEngine Editor"
            : $"CasaEngine Editor - {GameSettings.ProjectSettings.ProjectName}";
    }

    private void TryLoadEditorThemeAssets()
    {
        string themeFilePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, EditorThemeAssetRelativePath));
        if (!File.Exists(themeFilePath))
        {
            Logs.WriteWarning($"Editor theme asset was not found at '{themeFilePath}'. Falling back to the default MGUI theme.");
            return;
        }

        try
        {
            _desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(themeFilePath));

            if (_desktop.Resources.TryGetTheme(EditorThemeName, out MGTheme editorTheme))
            {
                _desktop.Resources.DefaultTheme = editorTheme;
            }
            else
            {
                Logs.WriteWarning($"Editor theme '{EditorThemeName}' was not registered from '{themeFilePath}'. Falling back to the default MGUI theme.");
            }

            string controlTemplatesFilePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, EditorControlTemplatesAssetRelativePath));
            if (File.Exists(controlTemplatesFilePath))
            {
                _desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(controlTemplatesFilePath));
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"Failed to load editor theme assets from '{themeFilePath}': {ex.Message}");
        }
    }

    private void InitializeEditorRuntime()
    {
        var graphicsDeviceService = Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
        if (graphicsDeviceService == null)
        {
            throw new InvalidOperationException("The editor runtime could not resolve the shared graphics device service.");
        }

        _editorRuntimeContext = GameSettings.CreateRuntimeContext();
        if (_automationOptions.HasAutomation)
        {
            string automationProjectDirectory = Path.GetDirectoryName(_automationOptions.ProjectPath!)
                                                ?? Environment.CurrentDirectory;
            _editorRuntimeContext.ProjectPath = automationProjectDirectory;
            EngineEnvironment.ProjectPath = automationProjectDirectory;
        }

        _editorRuntimeContext.WindowInputSource = _windowInputSource;

        _editorRuntime = new HostedEditorGameAdapter(_automationOptions.HasAutomation ? _automationOptions.ProjectPath : null, graphicsDeviceService, _editorRuntimeContext)
        {
            ExecutionPolicy = GameplayExecutionPolicies.EditorPreview,
        };

        _editorRuntime.ContentPath = Path.Combine(AppContext.BaseDirectory, "Content");

        _editorRuntime.GameManager.WorldLoaded += OnAutomationWorldLoaded;

        _editorRuntime.InitializeHost();
        _editorRuntime.LoadContentHost();
    }

    private void SynchronizeEditorRuntimeContext()
    {
        if (_editorRuntimeContext == null)
        {
            return;
        }

        _editorRuntimeContext.ProjectPath = EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);
    }

    private void LoadInitialWorldIntoEditorRuntime()
    {
        if (_editorRuntime == null)
        {
            return;
        }

        var firstWorld = GameSettings.ProjectSettings.FirstWorldLoaded;
        if (string.IsNullOrWhiteSpace(firstWorld))
        {
            Logs.WriteWarning("No startup world is configured for the current project.");
            return;
        }

        _editorRuntime.GameManager.SetWorldToLoad(firstWorld);
    }

    private void EnsureDockHostInitialized()
    {
        if (_dockHost != null)
        {
            return;
        }

        _panelRegistry ??= CreatePanelRegistry();
        _dockHost = new MGDockHost(_mainWindow);
        _dockHost.Name = "EditorDockHost";
        _dockHost.ActivePanelChanged += OnDockHostActivePanelChanged;
        _dockHost.PanelRemoved += OnDockHostPanelRemoved;
        _rootPanel.TryAddChild(_dockHost, Dock.Top);
        SetupInitialDockLayout();
    }

    private MGElement GetOrCreateContentBrowserContent()
    {
        if (_contentBrowserPanel == null)
        {
            _contentBrowserPanel = new ContentBrowserPanel(_mainWindow);
            _contentBrowserPanel.FileOpened += OnContentBrowserFileOpened;
        }

        _contentBrowserContent ??= _contentBrowserPanel.CreateContent();
        return _contentBrowserContent;
    }

    private MGElement GetOrCreateWorldViewportContent()
    {
        if (_worldViewportPanel == null)
        {
            _worldViewportPanel = new WorldViewportPanel(_mainWindow, GraphicsDevice, _editorRuntime, _windowInputSource);
            _worldViewportPanel.SelectedEntityChanged += OnViewportSelectedEntityChanged;
        }

        _worldViewportContent ??= _worldViewportPanel.CreateContent();
        RefreshWorldSelectionViews();
        return _worldViewportContent;
    }

    private MGElement GetOrCreateHierarchyContent()
    {
        if (_hierarchyPanelHost == null)
        {
            _hierarchyPanelHost = new ContextualDockPanelHost(
                _mainWindow,
                _editorContext,
                EditorPanelRole.Hierarchy,
                "Hierarchy",
                "No hierarchy is available for the active editor.");

            _hierarchyPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Hierarchy,
                DocumentKind = EditorDocumentKind.World,
                Title = "Hierarchy",
                ContentFactory = GetOrCreateEntitiesContent,
                Refresh = _ => RefreshWorldHierarchyView(),
            });

            _hierarchyPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Hierarchy,
                DocumentKind = EditorDocumentKind.UIScreen,
                Title = "Hierarchy",
                ContentFactory = GetOrCreateScreenHierarchyContent,
                Refresh = _ => RefreshScreenViews(),
            });

            _hierarchyPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Hierarchy,
                DocumentKind = EditorDocumentKind.Material,
                Title = "Hierarchy",
                ContentFactory = GetOrCreateMaterialHierarchyContent,
                Refresh = _ => RefreshMaterialViews(),
            });
        }

        _hierarchyContent ??= _hierarchyPanelHost.CreateContent();
        _hierarchyPanelHost.Refresh();
        return _hierarchyContent;
    }

    private MGElement GetOrCreateEntitiesContent()
    {
        if (_entitiesPanel == null)
        {
            _entitiesPanel = new EntitiesPanel(_mainWindow, () => _editorRuntime?.GameManager.CurrentWorld);
            _entitiesPanel.SelectedEntityChanged += OnEntitiesPanelSelectionChanged;
            _entitiesPanel.SelectedWorldChanged += OnEntitiesPanelWorldSelectionChanged;
            _entitiesPanel.EntityDoubleClicked += OnEntitiesPanelEntityDoubleClicked;
        }

        _entitiesContent ??= _entitiesPanel.CreateContent();
        RefreshWorldHierarchyView();
        return _entitiesContent;
    }

    private MGElement GetOrCreateMaterialHierarchyContent()
    {
        _materialHierarchyPanel ??= new MaterialHierarchyPanel(_mainWindow);
        _materialHierarchyContent ??= _materialHierarchyPanel.CreateContent();
        _materialHierarchyPanel.SetInspectorPanel(_activeMaterialInspectorPanel);
        return _materialHierarchyContent;
    }

    private MGElement GetOrCreateInspectorContent()
    {
        if (_inspectorPanelHost == null)
        {
            _inspectorPanelHost = new ContextualDockPanelHost(
                _mainWindow,
                _editorContext,
                EditorPanelRole.Inspector,
                "Inspector",
                "No inspector is available for the active editor.");

            _inspectorPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Inspector,
                DocumentKind = EditorDocumentKind.World,
                Title = "Inspector",
                ContentFactory = GetOrCreateEntityDetailsContent,
                Refresh = _ => RefreshWorldInspectorView(),
            });

            _inspectorPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Inspector,
                DocumentKind = EditorDocumentKind.UIScreen,
                Title = "Inspector",
                ContentFactory = GetOrCreateScreenInspectorContent,
                Refresh = _ => RefreshScreenViews(),
            });

            _inspectorPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Inspector,
                DocumentKind = EditorDocumentKind.Material,
                Title = "Inspector",
                ContentFactory = GetOrCreateMaterialInspectorContent,
                Refresh = _ => RefreshMaterialViews(),
            });
        }

        _inspectorContent ??= _inspectorPanelHost.CreateContent();
        _inspectorPanelHost.Refresh();
        return _inspectorContent;
    }

    private MGElement GetOrCreateScreenHierarchyContent()
    {
        if (_screenHierarchyPanel == null)
        {
            _screenHierarchyPanel = new UIScreenHierarchyPanel(_mainWindow, _screenSelection);
            _screenHierarchyPanel.NodeDeleted += doc =>
            {
                _activeScreenPreviewPanel?.LoadDocumentDirectly(doc);
                RefreshScreenViews();
                SyncGlobalSelectionFromActiveDocument();
            };
            _screenHierarchyPanel.NodeDuplicateRequested += (doc, nodeId) =>
            {
                if (!TryGetActiveScreenCommandStack(out var commandStack))
                {
                    return;
                }

                var dupCmd = new DuplicateNodeCommand(doc, nodeId);
                commandStack.Execute(dupCmd);
                RefreshScreenPanelsAfterCommand();
                if (dupCmd.CreatedNode != null)
                {
                    _screenSelection.Select(dupCmd.CreatedNode.Id);
                }
            };
        }

        _screenHierarchyContent ??= _screenHierarchyPanel.CreateContent();
        AttachActiveScreenCommandStack();
        RefreshScreenViews();
        return _screenHierarchyContent;
    }

    private MGElement GetOrCreateScreenInspectorContent()
    {
        if (_screenInspectorPanel == null)
        {
            _screenInspectorPanel = new UIScreenInspectorPanel(_mainWindow, _screenSelection);
            _screenInspectorPanel.DocumentModified += doc =>
            {
                // Only called for structural changes (e.g. Name field edits visible in tree)
                _activeScreenPreviewPanel?.LoadDocumentDirectly(doc);
                RefreshScreenViews();
                SyncGlobalSelectionFromActiveDocument();
            };
            _screenInspectorPanel.PropertyModified += (doc, nodeId, propName, value) =>
            {
                // Fast path: patch the live MGElement directly
                if (_activeScreenPreviewPanel?.TryApplyPropertyUpdate(nodeId, propName, value) != true)
                    _activeScreenPreviewPanel?.RefreshPreviewOnly();
            };
        }

        _screenInspectorContent ??= _screenInspectorPanel.CreateContent();
        AttachActiveScreenCommandStack();
        RefreshScreenViews();
        return _screenInspectorContent;
    }

    private MGElement GetOrCreateMaterialInspectorContent()
    {
        _materialInspectorView ??= new MaterialInspectorView(_mainWindow);
        _materialInspectorContent ??= _materialInspectorView.CreateContent();
        _materialInspectorView.SetInspectorPanel(_activeMaterialInspectorPanel);
        return _materialInspectorContent;
    }

    private MGElement GetOrCreateToolboxContent()
    {
        if (_toolboxPanelHost == null)
        {
            _toolboxPanelHost = new ContextualDockPanelHost(
                _mainWindow,
                _editorContext,
                EditorPanelRole.Toolbox,
                "Toolbox",
                "No contextual tools are available for the active editor.");

            _toolboxPanelHost.Register(new ContextualPanelDefinition
            {
                Role = EditorPanelRole.Toolbox,
                DocumentKind = EditorDocumentKind.UIScreen,
                Title = "Toolbox",
                ContentFactory = GetOrCreateScreenToolboxContent,
                Refresh = _ => RefreshScreenViews(),
            });
        }

        _toolboxContent ??= _toolboxPanelHost.CreateContent();
        _toolboxPanelHost.Refresh();
        return _toolboxContent;
    }

    private MGElement GetOrCreateScreenToolboxContent()
    {
        if (_screenToolboxPanel == null)
        {
            _screenToolboxPanel = new UIScreenToolboxPanel(_mainWindow);
            // Phase 7.3 will wire ControlRequested to node insertion
            _screenToolboxPanel.ControlRequested += OnToolboxControlRequested;
        }

        _screenToolboxContent ??= _screenToolboxPanel.CreateContent();
        RefreshScreenViews();
        return _screenToolboxContent;
    }

    private UICommandStack GetOrCreateScreenCommandStack(string panelId)
    {
        if (!_screenCommandStacks.TryGetValue(panelId, out var commandStack))
        {
            commandStack = new UICommandStack(_editorHistory.GetOrCreate(new EditorHistoryContext(EditorHistoryContextKind.UIScreen, panelId)));
            _screenCommandStacks.Add(panelId, commandStack);
        }

        return commandStack;
    }

    private bool TryGetActiveScreenCommandStack(out UICommandStack? commandStack)
    {
        commandStack = null;

        if (_editorContext.ActiveDocument?.Kind != EditorDocumentKind.UIScreen
            || string.IsNullOrWhiteSpace(_editorContext.ActiveDocument.Id))
        {
            return false;
        }

        commandStack = GetOrCreateScreenCommandStack(_editorContext.ActiveDocument.Id);
        return true;
    }

    private void AttachActiveScreenCommandStack()
    {
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        _screenHierarchyPanel?.SetCommandStack(commandStack!);
        _screenInspectorPanel?.SetCommandStack(commandStack!);
    }

    private void OnToolboxControlRequested(EditorServices.ScreenEditor.Toolbox.UIControlRegistryEntry entry)
    {
        if (_activeScreenPreviewPanel == null)
        {
            return;
        }

        var document = _activeScreenPreviewPanel.CurrentDocument;
        if (document == null)
        {
            return;
        }

        // Insert after (or inside) the selected node
        UIScreenNode? parentNode = null;
        if (_screenSelection.SelectedNodeId.HasValue)
        {
            parentNode = document.FindNode(_screenSelection.SelectedNodeId.Value);
        }

        var cmd = new AddNodeCommand(document, entry, parentNode);
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(cmd);
        var newNode = cmd.CreatedNode;

        // Rebuild preview and sync tree / inspector
        _activeScreenPreviewPanel.LoadDocumentDirectly(document);

        // Select the new node
        if (newNode != null)
        {
            _screenSelection.Select(newNode.Id);
        }
    }

    private void ExecuteUndo()
    {
        var historyContext = _editorHistory.ActiveContext;
        if (!_editorHistory.Undo())
        {
            return;
        }

        RefreshViewsAfterHistoryCommand(historyContext);
    }

    private void ExecuteRedo()
    {
        var historyContext = _editorHistory.ActiveContext;
        if (!_editorHistory.Redo())
        {
            return;
        }

        RefreshViewsAfterHistoryCommand(historyContext);
    }

    private void ExecuteDuplicate()
    {
        if (_activeScreenPreviewPanel == null || !_screenSelection.SelectedNodeId.HasValue) return;
        var document = _activeScreenPreviewPanel.CurrentDocument;
        if (document == null) return;

        var cmd = new DuplicateNodeCommand(document, _screenSelection.SelectedNodeId.Value);
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(cmd);
        RefreshScreenPanelsAfterCommand();

        if (cmd.CreatedNode != null)
        {
            _screenSelection.Select(cmd.CreatedNode.Id);
        }
    }

    private void ExecuteCopy()
    {
        if (_activeScreenPreviewPanel == null || !_screenSelection.SelectedNodeId.HasValue) return;
        var document = _activeScreenPreviewPanel.CurrentDocument;
        if (document == null) return;
        var node = document.FindNode(_screenSelection.SelectedNodeId.Value);
        if (node == null) return;

        var clone = node.DeepClone();
        var tempDoc = new UIScreenDocument();
        tempDoc.SetRoot(clone);
        _nodeClipboard = new UIScreenXamlSerializer().Serialize(tempDoc);
    }

    private void ExecuteCut()
    {
        ExecuteCopy();
        if (_nodeClipboard == null || !_screenSelection.SelectedNodeId.HasValue) return;
        var document = _activeScreenPreviewPanel?.CurrentDocument;
        if (document == null) return;
        var node = document.FindNode(_screenSelection.SelectedNodeId.Value);
        if (node == null) return;
        var cmd = new RemoveNodeCommand(document, node);
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(cmd);
        _screenSelection.ClearSelection();
        RefreshScreenPanelsAfterCommand();
    }

    private void ExecutePaste()
    {
        if (string.IsNullOrWhiteSpace(_nodeClipboard) || _activeScreenPreviewPanel == null) return;
        var document = _activeScreenPreviewPanel.CurrentDocument;
        if (document == null) return;

        UIScreenDocument parsedDoc;
        try
        {
            parsedDoc = new UIScreenXamlParser().Parse(_nodeClipboard);
        }
        catch
        {
            return; // invalid clipboard content
        }

        if (parsedDoc.Root == null) return;
        var pasteNode = parsedDoc.Root.DeepClone();
        var cmd = new PasteNodeCommand(document, pasteNode, _screenSelection.SelectedNodeId);
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(cmd);
        RefreshScreenPanelsAfterCommand();
        _screenSelection.Select(cmd.InsertedNode.Id);
    }

    /// <summary>
    /// Rebuilds the active screen preview and syncs all panels after an undo/redo.
    /// </summary>
    private void RefreshScreenPanelsAfterCommand()
    {
        if (_activeScreenPreviewPanel?.CurrentDocument == null)
        {
            return;
        }

        var doc = _activeScreenPreviewPanel.CurrentDocument;
        _activeScreenPreviewPanel.LoadDocumentDirectly(doc);
    }

    private void RefreshViewsAfterHistoryCommand(EditorHistoryContext context)
    {
        switch (context.Kind)
        {
            case EditorHistoryContextKind.World:
                RefreshWorldSelectionViews();
                break;

            case EditorHistoryContextKind.UIScreen:
                RefreshScreenPanelsAfterCommand();
                break;

            case EditorHistoryContextKind.Material:
                RefreshMaterialViews();
                break;

            case EditorHistoryContextKind.AnimationClip:
                break;

            case EditorHistoryContextKind.ContentBrowser:
                break;
        }
    }

    private void OnEditorHistoryChanged(object? sender, EditorHistoryChangedEventArgs e)
    {
        if (e.ChangeKind == EditorHistoryStackChangeKind.Executed
            && e.Context.Kind == EditorHistoryContextKind.World)
        {
            RefreshWorldSelectionViews();
        }
    }

    private MGElement GetOrCreateEntityDetailsContent()
    {
        if (_entityDetailsPanel == null)
        {
            _entityDetailsPanel = new EntityDetailsPanel(_mainWindow);
            _entityDetailsPanel.SelectedComponentChanged += OnEntityDetailsSelectedComponentChanged;
        }

        _entityDetailsContent ??= _entityDetailsPanel.CreateContent();
        RefreshWorldInspectorView();
        return _entityDetailsContent;
    }

    private MGElement GetOrCreateLogsContent()
    {
        _logsPanel ??= new LogsPanel(_mainWindow, _loggerEditor);
        _logsContent ??= _logsPanel.CreateContent();
        return _logsContent;
    }

    private EditorPanelRegistry CreatePanelRegistry()
    {
        return new EditorPanelRegistry(new[]
        {
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.WorldViewport,
                Title = "World Viewport",
                Kind = EditorPanelKind.Document,
                CanClose = false,
                CanFloat = false,
                ContentFactory = GetOrCreateWorldViewportContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Hierarchy,
                Title = "Hierarchy",
                Kind = EditorPanelKind.Tool,
                CanClose = false,
                ContentFactory = GetOrCreateHierarchyContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Inspector,
                Title = "Inspector",
                Kind = EditorPanelKind.Tool,
                CanClose = false,
                ContentFactory = GetOrCreateInspectorContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Toolbox,
                Title = "Toolbox",
                Kind = EditorPanelKind.Tool,
                CanClose = false,
                ContentFactory = GetOrCreateToolboxContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.ContentBrowser,
                Title = "Content Browser",
                Kind = EditorPanelKind.Tool,
                ContentFactory = GetOrCreateContentBrowserContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Output,
                Title = "Output / Logs",
                Kind = EditorPanelKind.Tool,
                ContentFactory = GetOrCreateLogsContent,
            },
        });
    }

    private void OnDockHostActivePanelChanged(object? sender, DockPanelNode panel)
    {
        if (panel.Id == EditorPanelIds.Output)
        {
            _logsPanel?.Refresh();
        }

        if (panel.Id == EditorPanelIds.WorldViewport)
        {
            ActivateWorldDocument();
        }
        else if (TryGetUIScreenPreviewPanel(panel.Id, out var previewPanel))
        {
            ActivateScreenDocument(panel.Id, previewPanel);
        }
        else if (TryGetAnimationClipPreviewPanel(panel.Id, out var animationClipPreviewPanel))
        {
            ActivateAnimationClipDocument(panel.Id, animationClipPreviewPanel);
        }
        else if (TryGetMaterialAssetInspectorPanel(panel.Id, out var materialInspectorPanel))
        {
            ActivateMaterialDocument(panel.Id, materialInspectorPanel);
        }

        RefreshActiveHistoryContext(panel.Id);
    }

    private void ActivateDockPanel(string panelId)
    {
        var targetGroup = _dockHost?.LayoutModel?
            .GetAllTabGroups()
            .FirstOrDefault(group => group.Panels.Any(panel => panel.Id == panelId));
        if (targetGroup == null || targetGroup.ActivePanelId == panelId)
        {
            return;
        }

        targetGroup.SetActivePanel(panelId);
    }

    private void OpenProjectLauncher()
    {
        ShowProjectLauncher();
    }

    private void ShowProjectLauncher()
    {
        var launcher = new ProjectLauncherWindow(_mainWindow, QueueProjectOpen, QueueProjectCreate);
        launcher.Show();
    }

    private void HookViewportInputCoordination()
    {
        _desktop.HighPriorityMouseHandler.LMBPressedInside += (_, e) =>
        {
            _worldViewportPanel?.ReleaseInputIfOutside(e.Position);
            foreach (var materialViewportPanel in _materialViewportPanels.Values)
            {
                materialViewportPanel.ReleaseInputIfOutside(e.Position);
            }
        };
    }

    private void QueueProjectOpen(string fileName)
    {
        _pendingProjectLauncherAction = () =>
        {
            try
            {
                EditorProjectAuthoringService.LoadProject(fileName);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Failed to open project:\n{ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                OpenProjectLauncher();
            }
        };
    }

    private void QueueProjectCreate(string projectName, string path)
    {
        _pendingProjectLauncherAction = () =>
        {
            try
            {
                EditorProjectAuthoringService.CreateProject(projectName, path);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Failed to create project:\n{ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                OpenProjectLauncher();
            }
        };
    }

    private void ProcessPendingProjectLauncherAction()
    {
        var action = _pendingProjectLauncherAction;
        if (action == null)
        {
            return;
        }

        _pendingProjectLauncherAction = null;
        action();
    }

    private void SaveCurrentProject()
    {
        if (string.IsNullOrWhiteSpace(EditorProjectSession.CurrentProjectFilePath))
        {
            Logs.WriteWarning("No project is currently loaded.");
            return;
        }

        if (_editorContext.ActiveDocument?.Kind == EditorDocumentKind.Material
            && _editorContext.ActiveDocument.Id is { Length: > 0 } materialPanelId
            && TryGetMaterialAssetInspectorPanel(materialPanelId, out var materialInspectorPanel))
        {
            if (materialInspectorPanel.TrySaveLoadedAsset(out string? errorMessage))
            {
                _editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Material, materialPanelId));
                UpdateDockPanelTitle(materialPanelId, GetMaterialDocumentTitle(materialPanelId));
                Logs.WriteInfo($"Material saved: {materialInspectorPanel.LoadedRelativePath}");
            }
            else if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                Logs.WriteWarning(errorMessage);
            }

            return;
        }

        SaveDirtyMaterialInspectors();

        EditorProjectAuthoringService.SaveProject(_editorRuntime?.GameManager.CurrentWorld);
        EditorAssetCatalogService.Save();
        _editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport));
        Logs.WriteInfo($"Project saved: {EditorProjectSession.CurrentProjectFilePath}");
    }

    private void SaveDockLayout()
    {
        SavePersistedDockLayout();
    }

    private void LoadDockLayout()
    {
        if (!TryLoadPersistedDockLayout(logOutcome: true))
        {
            ResetDockLayoutToDefault();
        }

        SyncActiveEditorDocumentFromDockState();
    }

    private void ResetDockLayout()
    {
        ResetDockLayoutToDefault();
        SyncActiveEditorDocumentFromDockState();
    }

    private Func<MGElement> GetPanelContentFactory(string panelId)
    {
        if (_panelRegistry != null && _panelRegistry.TryGetDescriptor(panelId, out var descriptor))
        {
            return descriptor.ContentFactory;
        }

        if (_screenPreviewPanels.TryGetValue(panelId, out var previewPanel))
        {
            return previewPanel.CreateContent;
        }

        if (_animationClipPreviewPanels.TryGetValue(panelId, out var animationClipPreviewPanel))
        {
            return animationClipPreviewPanel.CreateContent;
        }

        if (_materialInspectorPanels.TryGetValue(panelId, out _))
        {
            return () => GetOrCreateMaterialViewportContent(panelId);
        }

        return () => CreateUnavailablePanelContent(panelId);
    }

    private MGElement CreateUnavailablePanelContent(string panelId)
    {
        var panel = new MGStackPanel(_mainWindow, Orientation.Vertical)
        {
            Spacing = 6,
            Padding = new MonoGame.Extended.Thickness(8),
        };

        panel.TryAddChild(new MGTextBlock(_mainWindow, $"Panel unavailable: {panelId}")
        {
            WrapText = true,
        });

        panel.TryAddChild(new MGTextBlock(_mainWindow, "The saved layout references a panel that is not registered in this editor build.")
        {
            WrapText = true,
        });

        return panel;
    }

    private string GetPersistedLayoutPath()
    {
        var projectDirectory = GetCurrentProjectDirectory();
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return null;
        }

        return Path.Combine(projectDirectory, EditorLayoutDirectoryName, EditorLayoutFileName);
    }

    private void SavePersistedDockLayout()
    {
        if (_dockHost == null)
        {
            return;
        }

        var layoutPath = GetPersistedLayoutPath();
        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            Logs.WriteWarning("Cannot save editor layout because no project directory is available.");
            return;
        }

        var layoutDirectory = Path.GetDirectoryName(layoutPath);
        if (!string.IsNullOrWhiteSpace(layoutDirectory))
        {
            Directory.CreateDirectory(layoutDirectory);
        }

        File.WriteAllText(layoutPath, _dockHost.SaveLayoutToJson(indented: true));
        Logs.WriteInfo($"Editor layout saved: {layoutPath}");
    }

    private bool TryLoadPersistedDockLayout(bool logOutcome)
    {
        if (_dockHost == null)
        {
            return false;
        }

        var layoutPath = GetPersistedLayoutPath();
        if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath))
        {
            if (logOutcome)
            {
                Logs.WriteWarning("No persisted editor layout was found.");
            }

            return false;
        }

        try
        {
            var json = File.ReadAllText(layoutPath);
            _dockHost.LoadLayoutFromJson(json, GetPanelContentFactory);
            _ = GetOrCreateLogsContent();

            if (logOutcome)
            {
                Logs.WriteInfo($"Editor layout loaded: {layoutPath}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"Failed to load persisted editor layout '{layoutPath}': {ex.Message}");
            return false;
        }
    }

    private void ResetDockLayoutToDefault()
    {
        if (_dockHost == null)
        {
            return;
        }

        _dockHost.LayoutModel.RootNode = CreateDefaultDockLayout();
    }

    private DockNode CreateDefaultDockLayout()
    {
        _panelRegistry ??= CreatePanelRegistry();
        return new EditorShellLayoutBuilder(_panelRegistry).CreateDefaultLayout();
    }

    private string GetCurrentProjectDirectory()
    {
        var projectFile = EditorProjectSession.CurrentProjectFilePath;
        if (!string.IsNullOrWhiteSpace(projectFile))
        {
            var directory = Path.GetDirectoryName(projectFile);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Environment.CurrentDirectory;
    }

    private void OnDockHostPanelRemoved(object? sender, DockPanelNode panel)
    {
        if (TryGetUIScreenPreviewPanel(panel.Id, out var previewPanel))
        {
            _screenPreviewPanels.Remove(panel.Id);
            _screenPreviewPanelTitles.Remove(panel.Id);
            _screenCommandStacks.Remove(panel.Id);
            _editorHistory.Remove(new EditorHistoryContext(EditorHistoryContextKind.UIScreen, panel.Id));
            _editorDirtyState.Remove(new EditorHistoryContext(EditorHistoryContextKind.UIScreen, panel.Id));
            if (ReferenceEquals(_activeScreenPreviewPanel, previewPanel))
            {
                _activeScreenPreviewPanel = null;
            }
        }

        if (TryGetMaterialAssetInspectorPanel(panel.Id, out var materialInspectorPanel))
        {
            materialInspectorPanel.DirtyStateChanged -= OnMaterialInspectorDirtyStateChanged;
            materialInspectorPanel.Dispose();
            _materialInspectorPanels.Remove(panel.Id);
            _editorHistory.Remove(new EditorHistoryContext(EditorHistoryContextKind.Material, panel.Id));
            _editorDirtyState.Remove(new EditorHistoryContext(EditorHistoryContextKind.Material, panel.Id));
            if (_materialViewportPanels.Remove(panel.Id, out var materialViewportPanel))
            {
                materialViewportPanel.Dispose();
            }

            _materialInspectorPanelTitles.Remove(panel.Id);
            if (ReferenceEquals(_activeMaterialInspectorPanel, materialInspectorPanel))
            {
                SetActiveMaterialInspectorPanel(null);
            }
        }

        if (TryGetAnimationClipPreviewPanel(panel.Id, out var animationClipPreviewPanel))
        {
            animationClipPreviewPanel.Dispose();
            _animationClipPreviewPanels.Remove(panel.Id);
            _animationClipPreviewPanelTitles.Remove(panel.Id);
            _editorHistory.Remove(new EditorHistoryContext(EditorHistoryContextKind.AnimationClip, panel.Id));
            _editorDirtyState.Remove(new EditorHistoryContext(EditorHistoryContextKind.AnimationClip, panel.Id));
        }

        SyncActiveEditorDocumentFromDockState();
        RefreshActiveHistoryContext();
    }

    private void RefreshActiveHistoryContext(string? activePanelId = null)
    {
        if (string.Equals(activePanelId, EditorPanelIds.ContentBrowser, StringComparison.Ordinal))
        {
            _editorHistory.SetActiveContext(EditorHistoryContext.ContentBrowser);
            return;
        }

        var context = EditorHistoryContext.FromDocument(_editorContext.ActiveDocument);
        if (context.IsEmpty)
        {
            _editorHistory.Deactivate();
            return;
        }

        _editorHistory.SetActiveContext(context);
    }

    private string? GetActiveDocumentPanelId()
    {
        if (_dockHost?.LayoutModel == null)
        {
            return null;
        }

        foreach (var group in _dockHost.LayoutModel.GetAllTabGroups())
        {
            if (group.IsDocumentArea && !string.IsNullOrWhiteSpace(group.ActivePanelId))
            {
                return group.ActivePanelId;
            }
        }

        return null;
    }

    private List<string> GetOpenDocumentPanelIds()
    {
        var result = new List<string>();
        if (_dockHost?.LayoutModel == null)
        {
            return result;
        }

        foreach (var group in _dockHost.LayoutModel.GetAllTabGroups())
        {
            if (!group.IsDocumentArea)
            {
                continue;
            }

            foreach (var panel in group.Panels)
            {
                if (!result.Contains(panel.Id))
                {
                    result.Add(panel.Id);
                }
            }
        }

        return result;
    }

    private DockPanelNode? CreateDocumentPanelNode(string panelId)
    {
        if (_panelRegistry != null
            && _panelRegistry.TryGetDescriptor(panelId, out var descriptor)
            && descriptor.Kind == EditorPanelKind.Document)
        {
            return new DockPanelNode(descriptor.Id)
            {
                Title = descriptor.Id == EditorPanelIds.WorldViewport ? GetWorldDocumentTitle() : descriptor.Title,
                DockableType = DockableType.Document,
                CanClose = descriptor.CanClose,
                CanFloat = descriptor.CanFloat,
                CanAutoHide = descriptor.CanAutoHide,
                ContentFactory = descriptor.ContentFactory,
            };
        }

        if (TryGetUIScreenPreviewPanel(panelId, out var previewPanel))
        {
            return new DockPanelNode(panelId)
            {
                Title = GetScreenDocumentTitle(panelId),
                DockableType = DockableType.Document,
                CanClose = true,
                CanFloat = true,
                CanAutoHide = false,
                ContentFactory = previewPanel.CreateContent,
            };
        }

        if (TryGetAnimationClipPreviewPanel(panelId, out var animationClipPreviewPanel))
        {
            return new DockPanelNode(panelId)
            {
                Title = GetAnimationClipDocumentTitle(panelId),
                DockableType = DockableType.Document,
                CanClose = true,
                CanFloat = true,
                CanAutoHide = false,
                ContentFactory = animationClipPreviewPanel.CreateContent,
            };
        }

        if (TryGetMaterialAssetInspectorPanel(panelId, out _))
        {
            return new DockPanelNode(panelId)
            {
                Title = GetMaterialDocumentTitle(panelId),
                DockableType = DockableType.Document,
                CanClose = true,
                CanFloat = true,
                CanAutoHide = false,
                ContentFactory = () => GetOrCreateMaterialViewportContent(panelId),
            };
        }

        return null;
    }

    private bool TryGetUIScreenPreviewPanel(string panelId, out UIScreenPreviewPanel previewPanel)
    {
        previewPanel = null!;

        if (!panelId.StartsWith(EditorPanelIds.UIScreenDocumentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return _screenPreviewPanels.TryGetValue(panelId, out previewPanel);
    }

    private bool TryGetMaterialAssetInspectorPanel(string panelId, out MaterialAssetInspectorPanel inspectorPanel)
    {
        inspectorPanel = null!;

        if (!panelId.StartsWith(EditorPanelIds.MaterialAssetDocumentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return _materialInspectorPanels.TryGetValue(panelId, out inspectorPanel);
    }

    private bool TryGetAnimationClipPreviewPanel(string panelId, out AnimationClipPreviewPanel previewPanel)
    {
        previewPanel = null!;

        if (!panelId.StartsWith(EditorPanelIds.AnimationClipAssetDocumentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return _animationClipPreviewPanels.TryGetValue(panelId, out previewPanel);
    }

    private bool TryGetActiveMaterialInspectorPanel(out MaterialAssetInspectorPanel inspectorPanel)
    {
        inspectorPanel = _activeMaterialInspectorPanel!;
        return inspectorPanel != null;
    }

    private void SetActiveMaterialInspectorPanel(MaterialAssetInspectorPanel? inspectorPanel)
    {
        _activeMaterialInspectorPanel = inspectorPanel;
        RefreshMaterialViews();
    }

    private MGElement GetOrCreateMaterialViewportContent(string panelId)
    {
        if (!TryGetMaterialAssetInspectorPanel(panelId, out var inspectorPanel))
        {
            return CreateUnavailablePanelContent(panelId);
        }

        return GetOrCreateMaterialViewportPanel(panelId, inspectorPanel).CreateContent();
    }

    private WorldViewportPanel GetOrCreateMaterialViewportPanel(string panelId, MaterialAssetInspectorPanel inspectorPanel)
    {
        if (!_materialViewportPanels.TryGetValue(panelId, out var viewportPanel))
        {
            viewportPanel = new WorldViewportPanel(_mainWindow, GraphicsDevice, _editorRuntime, _windowInputSource);
            _materialViewportPanels.Add(panelId, viewportPanel);
        }

        viewportPanel.SetWorldOverride(inspectorPanel.GetOrCreatePreviewWorld());
        viewportPanel.SetEnvironmentOverride(PreviewEnvironmentFactory.CreateNeutralPreview(Color.DimGray));
        return viewportPanel;
    }

    private void ActivateWorldDocument()
    {
        _editorContext.SetActiveDocument(new EditorDocumentContext(
            EditorDocumentKind.World,
            EditorPanelIds.WorldViewport,
            "World",
            _editorRuntime?.GameManager.CurrentWorld));
        SyncGlobalSelectionFromActiveDocument();
        RefreshActiveHistoryContext();
    }

    private void ActivateScreenDocument(string panelId, UIScreenPreviewPanel previewPanel)
    {
        _activeScreenPreviewPanel = previewPanel;
        RefreshScreenViews();
        _editorContext.SetActiveDocument(new EditorDocumentContext(
            EditorDocumentKind.UIScreen,
            panelId,
            _screenPreviewPanelTitles.TryGetValue(panelId, out var title) ? title : "UIScreen",
            previewPanel));
        AttachActiveScreenCommandStack();
        SyncGlobalSelectionFromActiveDocument();
        RefreshActiveHistoryContext();
    }

    private void ActivateMaterialDocument(string panelId, MaterialAssetInspectorPanel inspectorPanel)
    {
        SetActiveMaterialInspectorPanel(inspectorPanel);
        _editorContext.SetActiveDocument(new EditorDocumentContext(
            EditorDocumentKind.Material,
            panelId,
            _materialInspectorPanelTitles.TryGetValue(panelId, out var title) ? title : "Material",
            inspectorPanel));
        SyncGlobalSelectionFromActiveDocument();
        RefreshActiveHistoryContext();
    }

    private void ActivateAnimationClipDocument(string panelId, AnimationClipPreviewPanel previewPanel)
    {
        _editorContext.SetActiveDocument(new EditorDocumentContext(
            EditorDocumentKind.AnimationClip,
            panelId,
            _animationClipPreviewPanelTitles.TryGetValue(panelId, out var title) ? title : "Animation Clip",
            previewPanel));
        SyncGlobalSelectionFromActiveDocument();
        RefreshActiveHistoryContext();
    }

    private void SyncActiveEditorDocumentFromDockState()
    {
        var activeDocumentPanelId = GetActiveDocumentPanelId();
        if (!string.IsNullOrWhiteSpace(activeDocumentPanelId)
            && TryGetUIScreenPreviewPanel(activeDocumentPanelId, out var previewPanel))
        {
            ActivateScreenDocument(activeDocumentPanelId, previewPanel);
            return;
        }

        if (!string.IsNullOrWhiteSpace(activeDocumentPanelId)
            && TryGetMaterialAssetInspectorPanel(activeDocumentPanelId, out var materialInspectorPanel))
        {
            ActivateMaterialDocument(activeDocumentPanelId, materialInspectorPanel);
            return;
        }

        if (!string.IsNullOrWhiteSpace(activeDocumentPanelId)
            && TryGetAnimationClipPreviewPanel(activeDocumentPanelId, out var animationClipPreviewPanel))
        {
            ActivateAnimationClipDocument(activeDocumentPanelId, animationClipPreviewPanel);
            return;
        }

        ActivateWorldDocument();
    }

    private void RefreshScreenViews()
    {
        var activeDocument = _activeScreenPreviewPanel?.CurrentDocument;
        _screenHierarchyPanel?.SetDocument(activeDocument);
        _screenInspectorPanel?.SetDocument(activeDocument);
        _screenToolboxPanel?.SetDocument(activeDocument);
    }

    private void RefreshMaterialViews()
    {
        _materialHierarchyPanel?.SetInspectorPanel(_activeMaterialInspectorPanel);
        _materialInspectorView?.SetInspectorPanel(_activeMaterialInspectorPanel);
    }

    private DockTabGroupNode? GetDocumentDockGroup()
    {
        if (_dockHost?.LayoutModel == null)
        {
            return null;
        }

        return _dockHost.LayoutModel.GetAllTabGroups().FirstOrDefault(group => group.IsDocumentArea)
               ?? _dockHost.LayoutModel.GetAllTabGroups().FirstOrDefault(group => group.Panels.Any(panel => panel.Id == EditorPanelIds.WorldViewport))
               ?? _dockHost.LayoutModel.GetAllTabGroups().FirstOrDefault();
    }

    private void OnEntitiesPanelSelectionChanged(Entity? entity)
    {
        _editorSelection.SetSelectedEntity(entity);
    }

    private void OnEntitiesPanelWorldSelectionChanged(Framework.Scene.World.World? world)
    {
        _editorSelection.SetSelectedWorld(world);
    }

    private void OnEntitiesPanelEntityDoubleClicked(Entity entity)
    {
        _worldViewportPanel?.FocusEntity(entity);
    }

    private void OnContentBrowserFileOpened(ContentBrowser.Models.ContentItem item)
    {
        TryOpenEditorAsset(item.FullPath);
    }

    private bool TryOpenEditorAsset(string fullPath)
    {
        if (TryOpenUIScreenAsset(fullPath))
        {
            return true;
        }

        if (TryOpenAnimationClipAsset(fullPath))
        {
            return true;
        }

        return TryOpenMaterialAsset(fullPath);
    }

    private bool TryOpenUIScreenAsset(string fullPath)
    {
        if (!TryLoadUIScreenAsset(fullPath, out var screenAsset))
        {
            return false;
        }

        EnsureDockHostInitialized();

        var panelId = $"{EditorPanelIds.UIScreenDocumentPrefix}{screenAsset.Id:N}";
        if (!_screenPreviewPanels.TryGetValue(panelId, out var previewPanel))
        {
            previewPanel = new UIScreenPreviewPanel(_mainWindow);
            previewPanel.SetSelectionService(_screenSelection);
            previewPanel.DocumentLoaded += _ =>
            {
                if (ReferenceEquals(_activeScreenPreviewPanel, previewPanel))
                {
                    RefreshScreenViews();
                    SyncGlobalSelectionFromActiveDocument();
                }
            };
            previewPanel.NodePicked += id =>
            {
                if (id.HasValue)
                {
                    _screenSelection.Select(id.Value);
                }
                else
                {
                    _screenSelection.ClearSelection();
                }
            };
            previewPanel.NodeMoveRequested += OnScreenNodeMoveRequested;
            previewPanel.NodeResizeRequested += OnScreenNodeResizeRequested;
            previewPanel.ExportPngRequested += () => ExportPreviewAsPng(previewPanel);
            _screenPreviewPanels.Add(panelId, previewPanel);
        }

        previewPanel.LoadAsset(screenAsset, fullPath);
        var panelTitle = string.IsNullOrWhiteSpace(screenAsset.Name)
            ? Path.GetFileNameWithoutExtension(fullPath)
            : screenAsset.Name;
        _screenPreviewPanelTitles[panelId] = panelTitle;

        var existingPanel = _dockHost?.LayoutModel?.FindPanelById(panelId);
        if (existingPanel == null)
        {
            var panelNode = new DockPanelNode(panelId)
            {
                Title = GetScreenDocumentTitle(panelId),
                DockableType = DockableType.Document,
                CanClose = true,
                CanFloat = true,
                CanAutoHide = false,
                ContentFactory = previewPanel.CreateContent,
            };

            var targetGroup = GetDocumentDockGroup();
            if (targetGroup == null)
            {
                return false;
            }

            DockOperation.DockAsTab(_dockHost!.LayoutModel, panelNode, targetGroup);
        }
        else
        {
            existingPanel.Title = GetScreenDocumentTitle(panelId);
        }

        ActivateScreenDocument(panelId, previewPanel);
        ActivateDockPanel(panelId);
        return true;
    }

    private bool TryOpenMaterialAsset(string fullPath)
    {
        if (!TryLoadMaterialAsset(fullPath, out var materialAsset))
        {
            return false;
        }

        EnsureDockHostInitialized();

        Guid documentId = materialAsset.AssetId != Guid.Empty ? materialAsset.AssetId : materialAsset.Id;
        var panelId = $"{EditorPanelIds.MaterialAssetDocumentPrefix}{documentId:N}";
        bool createdPanel = false;
        if (!_materialInspectorPanels.TryGetValue(panelId, out var inspectorPanel))
        {
            inspectorPanel = new MaterialAssetInspectorPanel(_mainWindow, _editorRuntime, GraphicsDevice);
            inspectorPanel.DirtyStateChanged += OnMaterialInspectorDirtyStateChanged;
            _materialInspectorPanels.Add(panelId, inspectorPanel);
            createdPanel = true;
        }

        inspectorPanel.SetHistoryContextId(panelId);
        if (createdPanel)
        {
            inspectorPanel.LoadAsset(materialAsset, fullPath);
        }

        _ = GetOrCreateMaterialViewportPanel(panelId, inspectorPanel);

        var panelTitle = string.IsNullOrWhiteSpace(materialAsset.Name)
            ? Path.GetFileNameWithoutExtension(fullPath)
            : materialAsset.Name;
        _materialInspectorPanelTitles[panelId] = panelTitle;

        var existingPanel = _dockHost?.LayoutModel?.FindPanelById(panelId);
        if (existingPanel == null)
        {
            var panelNode = CreateDocumentPanelNode(panelId);
            var targetGroup = GetDocumentDockGroup();
            if (panelNode == null || targetGroup == null)
            {
                return false;
            }

            panelNode.Title = GetMaterialDocumentTitle(panelId);
            DockOperation.DockAsTab(_dockHost!.LayoutModel, panelNode, targetGroup);
        }
        else
        {
            existingPanel.Title = GetMaterialDocumentTitle(panelId);
        }

        ActivateMaterialDocument(panelId, inspectorPanel);
        ActivateDockPanel(panelId);
        EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
            $"[Editor] Opened material asset='{materialAsset.Name}', viewport='{panelId}'");
        return true;
    }

    private bool TryOpenAnimationClipAsset(string fullPath)
    {
        if (!TryLoadAnimationClipAsset(fullPath, out var animationClipAsset))
        {
            return false;
        }

        EnsureDockHostInitialized();

        Guid documentId = animationClipAsset.AssetId != Guid.Empty ? animationClipAsset.AssetId : animationClipAsset.Id;
        string panelId = $"{EditorPanelIds.AnimationClipAssetDocumentPrefix}{documentId:N}";
        if (!_animationClipPreviewPanels.TryGetValue(panelId, out var previewPanel))
        {
            previewPanel = new AnimationClipPreviewPanel(_mainWindow, GraphicsDevice, _editorRuntime);
            _animationClipPreviewPanels.Add(panelId, previewPanel);
        }

        previewPanel.LoadAsset(animationClipAsset, fullPath);
        string panelTitle = string.IsNullOrWhiteSpace(animationClipAsset.Name)
            ? Path.GetFileNameWithoutExtension(fullPath)
            : animationClipAsset.Name;
        _animationClipPreviewPanelTitles[panelId] = panelTitle;

        var existingPanel = _dockHost?.LayoutModel?.FindPanelById(panelId);
        if (existingPanel == null)
        {
            var panelNode = CreateDocumentPanelNode(panelId);
            var targetGroup = GetDocumentDockGroup();
            if (panelNode == null || targetGroup == null)
            {
                return false;
            }

            panelNode.Title = GetAnimationClipDocumentTitle(panelId);
            DockOperation.DockAsTab(_dockHost!.LayoutModel, panelNode, targetGroup);
        }
        else
        {
            existingPanel.Title = GetAnimationClipDocumentTitle(panelId);
        }

        ActivateAnimationClipDocument(panelId, previewPanel);
        ActivateDockPanel(panelId);
        EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
            $"[Editor] Opened animation clip asset='{animationClipAsset.Name}', viewport='{panelId}'");
        return true;
    }

    private void OnDirtyStateChanged(object? sender, EditorDirtyStateChangedEventArgs e)
    {
        RefreshHistoryContextTitle(e.Context);
    }

    private void OnMaterialInspectorDirtyStateChanged(MaterialAssetInspectorPanel inspectorPanel)
    {
        UpdateDockPanelTitleForMaterialInspector(inspectorPanel);
    }

    private bool TryGetMaterialInspectorPanelId(MaterialAssetInspectorPanel inspectorPanel, out string panelId)
    {
        foreach (var pair in _materialInspectorPanels)
        {
            if (ReferenceEquals(pair.Value, inspectorPanel))
            {
                panelId = pair.Key;
                return true;
            }
        }

        panelId = string.Empty;
        return false;
    }

    private void SaveDirtyMaterialInspectors()
    {
        foreach (var pair in _materialInspectorPanels)
        {
            var materialInspectorPanel = pair.Value;
            if (!materialInspectorPanel.IsDirty)
            {
                continue;
            }

            if (materialInspectorPanel.TrySaveLoadedAsset(out string? errorMessage))
            {
                _editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Material, pair.Key));
                UpdateDockPanelTitle(pair.Key, GetMaterialDocumentTitle(pair.Key));
                Logs.WriteInfo($"Material saved: {materialInspectorPanel.LoadedRelativePath}");
            }
            else if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                Logs.WriteWarning(errorMessage);
            }
        }
    }

    private void UpdateDockPanelTitleForMaterialInspector(MaterialAssetInspectorPanel inspectorPanel)
    {
        if (TryGetMaterialInspectorPanelId(inspectorPanel, out var panelId))
        {
            UpdateDockPanelTitle(panelId, GetMaterialDocumentTitle(panelId));
        }
    }

    private void RefreshHistoryContextTitle(EditorHistoryContext context)
    {
        switch (context.Kind)
        {
            case EditorHistoryContextKind.World:
                UpdateDockPanelTitle(EditorPanelIds.WorldViewport, GetWorldDocumentTitle());
                break;

            case EditorHistoryContextKind.UIScreen:
                if (_screenPreviewPanelTitles.ContainsKey(context.Id))
                {
                    UpdateDockPanelTitle(context.Id, GetScreenDocumentTitle(context.Id));
                }

                break;

            case EditorHistoryContextKind.Material:
                if (_materialInspectorPanelTitles.ContainsKey(context.Id))
                {
                    UpdateDockPanelTitle(context.Id, GetMaterialDocumentTitle(context.Id));
                }

                break;

            case EditorHistoryContextKind.AnimationClip:
                if (_animationClipPreviewPanelTitles.ContainsKey(context.Id))
                {
                    UpdateDockPanelTitle(context.Id, GetAnimationClipDocumentTitle(context.Id));
                }

                break;

            case EditorHistoryContextKind.ContentBrowser:
                UpdateDockPanelTitle(EditorPanelIds.ContentBrowser, GetContentBrowserTitle());
                break;
        }
    }

    private void UpdateDockPanelTitle(string panelId, string title)
    {
        var panelNode = _dockHost?.LayoutModel?.FindPanelById(panelId);
        if (panelNode != null && !string.Equals(panelNode.Title, title, StringComparison.Ordinal))
        {
            panelNode.Title = title;
        }
    }

    private string GetWorldDocumentTitle()
        => DecorateDirtyTitle(new EditorHistoryContext(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport), "World Viewport");

    private string GetScreenDocumentTitle(string panelId)
    {
        var title = _screenPreviewPanelTitles.TryGetValue(panelId, out var value) ? value : "UIScreen";
        return DecorateDirtyTitle(new EditorHistoryContext(EditorHistoryContextKind.UIScreen, panelId), title);
    }

    private string GetMaterialDocumentTitle(string panelId)
    {
        var title = _materialInspectorPanelTitles.TryGetValue(panelId, out var value) ? value : "Material";
        bool isDirty = _editorDirtyState.IsDirty(new EditorHistoryContext(EditorHistoryContextKind.Material, panelId));
        if (TryGetMaterialAssetInspectorPanel(panelId, out var inspectorPanel))
        {
            isDirty |= inspectorPanel.IsDirty;
        }

        return isDirty ? $"{title} *" : title;
    }

    private string GetAnimationClipDocumentTitle(string panelId)
    {
        return _animationClipPreviewPanelTitles.TryGetValue(panelId, out var value) ? value : "Animation Clip";
    }

    private string GetContentBrowserTitle()
        => DecorateDirtyTitle(EditorHistoryContext.ContentBrowser, "Content Browser");

    private string DecorateDirtyTitle(EditorHistoryContext context, string title)
        => _editorDirtyState.IsDirty(context) ? $"{title} *" : title;

    private static bool TryLoadUIScreenAsset(string fullPath, out UIScreenAsset screenAsset)
    {
        screenAsset = new UIScreenAsset();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fullPath));
            if (document["source_xaml_file"] == null)
            {
                return false;
            }

            screenAsset.Load(document);
            screenAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(screenAsset.FileName)
                            ?? AssetCatalog.GetByFileName(screenAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                screenAsset.Name = assetInfo.Name;
                screenAsset.AssetId = assetInfo.Id;
                screenAsset.FileName = assetInfo.FileName;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadMaterialAsset(string fullPath, out MaterialAsset materialAsset)
    {
        materialAsset = new MaterialAsset();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fullPath));
            if (document["definition_id"] == null && document["type"] == null)
            {
                return false;
            }

            materialAsset.Load(document);
            materialAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(materialAsset.FileName)
                            ?? AssetCatalog.GetByFileName(materialAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                materialAsset.Name = assetInfo.Name;
                materialAsset.AssetId = assetInfo.Id;
                materialAsset.FileName = assetInfo.FileName;
            }
            else
            {
                materialAsset.AssetId = materialAsset.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadAnimationClipAsset(string fullPath, out AnimationClipAsset animationClipAsset)
    {
        animationClipAsset = new AnimationClipAsset();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fullPath));
            if (document["skeleton_asset_id"] == null || document["joint_tracks"] == null)
            {
                return false;
            }

            animationClipAsset.Load(document);
            animationClipAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(animationClipAsset.FileName)
                            ?? AssetCatalog.GetByFileName(animationClipAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                animationClipAsset.Name = assetInfo.Name;
                animationClipAsset.AssetId = assetInfo.Id;
                animationClipAsset.FileName = assetInfo.FileName;
            }
            else
            {
                animationClipAsset.AssetId = animationClipAsset.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnViewportSelectedEntityChanged(Entity? entity)
    {
        if (_automationOptions.HasAutomation)
        {
            return;
        }

        _editorSelection.SetSelectedEntity(entity);
    }

    private void OnScreenSelectionChanged(DocumentNodeId? nodeId)
    {
        if (_activeScreenPreviewPanel != null)
        {
            _activeScreenPreviewPanel.SelectedNodeId = nodeId;
        }

        if (_editorContext.ActiveDocument?.Kind == EditorDocumentKind.UIScreen)
        {
            SyncGlobalSelectionFromActiveDocument();
        }
    }

    private void OnEditorSelectionChanged(Entity? entity)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            RefreshWorldSelectionViews();
            if (_editorContext.ActiveDocument?.Kind == EditorDocumentKind.World)
            {
                SyncGlobalSelectionFromActiveDocument();
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private void OnEditorWorldSelectionChanged(Framework.Scene.World.World? world)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            RefreshWorldSelectionViews();
            if (_editorContext.ActiveDocument?.Kind == EditorDocumentKind.World)
            {
                SyncGlobalSelectionFromActiveDocument();
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private void OnEditorComponentSelectionChanged(EntityComponent? component)
    {
        Logs.WriteTrace($"[WorldSelection] ComponentSelectionChanged entity={DescribeEntity(component?.Owner)} component={DescribeComponent(component)}");
        RefreshWorldInspectorView();
        if (_editorContext.ActiveDocument?.Kind == EditorDocumentKind.World)
        {
            SyncGlobalSelectionFromActiveDocument();
        }
    }

    private void OnEntityDetailsSelectedComponentChanged(EntityComponent? component)
    {
        Logs.WriteTrace($"[WorldSelection] Inspector selected component={DescribeComponent(component)}");
        _editorSelection.SetSelectedComponent(component);
    }

    private void RefreshWorldHierarchyView()
    {
        _entitiesPanel?.Update();
        _entitiesPanel?.SetSelectionState(_editorSelection.SelectedWorld, GetSelectedWorldEntity(), GetWorldSelectionCount());
    }

    private void RefreshWorldInspectorView()
    {
        _entityDetailsPanel?.SyncSelection(_editorSelection.SelectedWorld, GetSelectedWorldEntity(), _editorSelection.SelectedComponent);
    }

    private void RefreshWorldSelectionViews()
    {
        RefreshWorldHierarchyView();
        RefreshWorldInspectorView();

        if (!_automationOptions.HasAutomation)
        {
            _worldViewportPanel?.SetSelectedEntity(GetSelectedWorldEntity());
        }
    }

    private Entity? GetSelectedWorldEntity()
    {
        return _editorSelection.SelectedComponent?.Owner ?? _editorSelection.SelectedEntity;
    }

    private int GetWorldSelectionCount()
    {
        return _editorSelection.SelectedWorld != null || GetSelectedWorldEntity() != null ? 1 : 0;
    }

    private void SyncGlobalSelectionFromActiveDocument()
    {
        switch (_editorContext.ActiveDocument?.Kind ?? EditorDocumentKind.None)
        {
            case EditorDocumentKind.World:
                _editorContext.SetSelection(CreateWorldSelectionState());
                break;
            case EditorDocumentKind.UIScreen:
                _editorContext.SetSelection(CreateUIScreenSelectionState());
                break;
            case EditorDocumentKind.Material:
                _editorContext.SetSelection(CreateMaterialSelectionState());
                break;
            default:
                _editorContext.ClearSelection();
                break;
        }
    }

    private EditorSelectionState CreateWorldSelectionState()
    {
        if (_editorSelection.SelectedComponent != null)
        {
            return new EditorSelectionState(
                EditorSelectionKind.WorldComponent,
                _editorSelection.SelectedComponent,
                1,
                _editorSelection.SelectedComponent.GetType().Name);
        }

        if (_editorSelection.SelectedEntity != null)
        {
            int count = GetWorldSelectionCount();
            return new EditorSelectionState(
                EditorSelectionKind.WorldEntity,
                _editorSelection.SelectedEntity,
                count,
                $"{count} entit{(count == 1 ? "y" : "ies")} selected");
        }

        if (_editorSelection.SelectedWorld != null)
        {
            return new EditorSelectionState(
                EditorSelectionKind.WorldRoot,
                _editorSelection.SelectedWorld,
                1,
                string.IsNullOrWhiteSpace(_editorSelection.SelectedWorld.Name) ? "World selected" : _editorSelection.SelectedWorld.Name);
        }

        return EditorSelectionState.Empty;
    }

    private EditorSelectionState CreateUIScreenSelectionState()
    {
        int selectionCount = _screenSelection.MultiSelection.Count;
        if (selectionCount == 0 && _screenSelection.SelectedNodeId.HasValue)
        {
            selectionCount = 1;
        }

        if (selectionCount == 0)
        {
            return EditorSelectionState.Empty;
        }

        return new EditorSelectionState(
            EditorSelectionKind.UIScreenNode,
            _screenSelection.SelectedNodeId,
            selectionCount,
            $"{selectionCount} control{(selectionCount == 1 ? string.Empty : "s")} selected");
    }

    private EditorSelectionState CreateMaterialSelectionState()
    {
        var materialAsset = _activeMaterialInspectorPanel?.LoadedMaterialAsset;
        if (materialAsset == null)
        {
            return EditorSelectionState.Empty;
        }

        return new EditorSelectionState(
            EditorSelectionKind.MaterialAsset,
            materialAsset,
            1,
            $"Material {materialAsset.Name} selected");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        _windowInputSource.CaptureFrameInput();
        PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

        // ── Keyboard shortcuts ────────────────────────────────────────
        var kb = Keyboard.GetState();
        bool ctrl = kb.IsKeyDown(Keys.LeftControl)
                    || kb.IsKeyDown(Keys.RightControl);
        bool shift = kb.IsKeyDown(Keys.LeftShift)
                     || kb.IsKeyDown(Keys.RightShift);
        if (ctrl && shift && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.Z))
        {
            ExecuteRedo();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.Z))
        {
            ExecuteUndo();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.Y))
        {
            ExecuteRedo();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.D))
        {
            ExecuteDuplicate();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.C))
        {
            ExecuteCopy();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.X))
        {
            ExecuteCut();
        }
        else if (ctrl && IsShortcutJustPressed(kb, _previousShortcutKeyboardState, Keys.V))
        {
            ExecutePaste();
        }

        _previousShortcutKeyboardState = kb;

        foreach (var previewPanel in _screenPreviewPanels.Values)
        {
            previewPanel.Update();
        }

        foreach (var animationClipPreviewPanel in _animationClipPreviewPanels.Values)
        {
            animationClipPreviewPanel.Update(gameTime);
        }

        _contentBrowserPanel?.Update();

        _desktop.Update();
        ProcessPendingProjectLauncherAction();
        _editorRuntime?.UpdateHost(gameTime);
        _entitiesPanel?.Update();
        _worldViewportPanel?.UpdateInput(gameTime);
        foreach (var materialViewportPanel in _materialViewportPanels.Values)
        {
            materialViewportPanel.UpdateInput(gameTime);
        }
        RunAutomation(gameTime.TotalGameTime);

        base.Update(gameTime);

        EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    private void OnAutomationWorldLoaded(object? sender, EventArgs e)
    {
        _automationWorldLoaded = true;
    }

    private void RunAutomation(TimeSpan totalGameTime)
    {
        if (!_automationOptions.HasAutomation || _automationDiagnosticsCaptured || !_automationWorldLoaded)
        {
            return;
        }

        TryApplyAutomationAssetOpen(totalGameTime);

        if (!_automationSelectionApplied || !IsAutomationSelectionActive())
        {
            if (TryApplyAutomationSelection())
            {
                _automationSelectionApplied = true;
                _automationSelectionAppliedAt = totalGameTime;
            }

            return;
        }

        TryApplyAutomationMaterialEdit(totalGameTime);
        if (!string.IsNullOrWhiteSpace(_automationOptions.SetMaterialPropertyKey)
            && !_automationMaterialEditAttempted)
        {
            return;
        }

        TimeSpan readyAt = _automationSelectionAppliedAt;
        if (_automationAssetOpened && _automationAssetOpenedAt > readyAt)
        {
            readyAt = _automationAssetOpenedAt;
        }

        if (_automationMaterialEdited && _automationMaterialEditedAt > readyAt)
        {
            readyAt = _automationMaterialEditedAt;
        }

        if (totalGameTime - readyAt < TimeSpan.FromSeconds(_automationOptions.CaptureDelaySeconds))
        {
            return;
        }

        CaptureAutomationDiagnostics();
        RestoreAutomationEditedFilesIfNeeded();
        _automationDiagnosticsCaptured = true;

        if (_automationOptions.ExitAfterCapture)
        {
            Exit();
        }
    }

    private bool IsAutomationSelectionActive()
    {
        var world = _editorRuntime?.GameManager.CurrentWorld;
        if (world == null)
        {
            return false;
        }

        var desiredEntity = FindAutomationEntity(world);
        if (!ReferenceEquals(_editorSelection.SelectedEntity, desiredEntity))
        {
            return false;
        }

        var desiredComponent = desiredEntity == null ? null : FindAutomationComponent(desiredEntity);
        return ReferenceEquals(_editorSelection.SelectedComponent, desiredComponent);
    }

    private void TryApplyAutomationAssetOpen(TimeSpan totalGameTime)
    {
        if (_automationAssetOpenAttempted || string.IsNullOrWhiteSpace(_automationOptions.OpenAssetPath))
        {
            return;
        }

        _automationAssetOpenAttempted = true;

        string fullPath = ResolveAutomationAssetPath(_automationOptions.OpenAssetPath);
        if (TryOpenEditorAsset(fullPath))
        {
            _automationAssetOpened = true;
            _automationAssetOpenedAt = totalGameTime;
            EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                $"[Automation] Opened asset='{_automationOptions.OpenAssetPath}'");
            return;
        }

        EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
            $"[Automation] Unable to open asset='{_automationOptions.OpenAssetPath}' (resolved='{fullPath}')");
    }

    private void TryApplyAutomationMaterialEdit(TimeSpan totalGameTime)
    {
        if (_automationMaterialEditAttempted
            || string.IsNullOrWhiteSpace(_automationOptions.SetMaterialPropertyKey)
            || string.IsNullOrWhiteSpace(_automationOptions.SetMaterialPropertyValue))
        {
            return;
        }

        if (!TryGetActiveMaterialInspectorPanel(out var inspectorPanel))
        {
            return;
        }

        _automationMaterialEditAttempted = true;
        if (!TrySnapshotAutomationEditableFile(inspectorPanel, out string? snapshotError))
        {
            EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                $"[Automation] Refused to update material property '{_automationOptions.SetMaterialPropertyKey}': {snapshotError}");
            return;
        }

        if (inspectorPanel.TryApplyAutomationPropertyOverrideAndSave(
                _automationOptions.SetMaterialPropertyKey,
                _automationOptions.SetMaterialPropertyValue,
                out string statusMessage))
        {
            if (TryGetMaterialInspectorPanelId(inspectorPanel, out var panelId))
            {
                _editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Material, panelId));
                UpdateDockPanelTitle(panelId, GetMaterialDocumentTitle(panelId));
            }

            _automationMaterialEdited = true;
            _automationMaterialEditedAt = totalGameTime;
            EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                $"[Automation] Updated material property '{_automationOptions.SetMaterialPropertyKey}'='{_automationOptions.SetMaterialPropertyValue}'");
            return;
        }

        EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
            $"[Automation] Failed to update material property '{_automationOptions.SetMaterialPropertyKey}': {statusMessage}");
    }

    private bool TrySnapshotAutomationEditableFile(MaterialAssetInspectorPanel inspectorPanel, out string? errorMessage)
    {
        errorMessage = null;

        if (inspectorPanel.LoadedMaterialAsset == null || string.IsNullOrWhiteSpace(inspectorPanel.LoadedRelativePath))
        {
            errorMessage = "no loaded material file is associated with the active inspector.";
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, inspectorPanel.LoadedRelativePath);
        if (_automationEditedFileSnapshots.ContainsKey(fullPath))
        {
            return true;
        }

        if (!File.Exists(fullPath))
        {
            errorMessage = $"unable to snapshot '{fullPath}' because the file does not exist.";
            return false;
        }

        _automationEditedFileSnapshots.Add(fullPath, File.ReadAllText(fullPath));
        _automationEditedFilesRestored = false;
        return true;
    }

    private void RestoreAutomationEditedFilesIfNeeded()
    {
        if (_automationEditedFilesRestored || _automationEditedFileSnapshots.Count == 0)
        {
            return;
        }

        foreach (var pair in _automationEditedFileSnapshots)
        {
            string fullPath = pair.Key;
            string originalContent = pair.Value;

            if (!File.Exists(fullPath) || !string.Equals(File.ReadAllText(fullPath), originalContent, StringComparison.Ordinal))
            {
                File.WriteAllText(fullPath, originalContent);
            }

            ReloadRestoredAutomationAsset(fullPath);
        }

        _automationEditedFilesRestored = true;
    }

    private void ReloadRestoredAutomationAsset(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return;
        }

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath));
        if (!relativePath.EndsWith(".material", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var materialInspectorPanel in _materialInspectorPanels.Values)
        {
            if (string.IsNullOrWhiteSpace(materialInspectorPanel.LoadedRelativePath))
            {
                continue;
            }

            if (!string.Equals(NormalizeRelativePath(materialInspectorPanel.LoadedRelativePath), relativePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (materialInspectorPanel.ReloadFromDisk()
                && TryGetMaterialInspectorPanelId(materialInspectorPanel, out var panelId))
            {
                var historyContext = new EditorHistoryContext(EditorHistoryContextKind.Material, panelId);
                _editorHistory.Clear(historyContext);
                _editorDirtyState.MarkSaved(historyContext);
                UpdateDockPanelTitle(panelId, GetMaterialDocumentTitle(panelId));
            }
        }

        if (_editorRuntime == null)
        {
            return;
        }

        Guid materialAssetId = ResolveCatalogMaterialAssetId(relativePath);
        if (materialAssetId == Guid.Empty)
        {
            return;
        }

        _editorRuntime.ReloadMaterialAsset(materialAssetId);
        Logs.WriteInfo($"[Automation] Restored edited material '{relativePath}' after diagnostics capture.");
    }

    private static string ResolveAutomationAssetPath(string assetPath)
    {
        if (Path.IsPathRooted(assetPath))
        {
            return Path.GetFullPath(assetPath);
        }

        return Path.GetFullPath(Path.Combine(EngineEnvironment.ProjectPath, assetPath));
    }

    private bool TryApplyAutomationSelection()
    {
        var world = _editorRuntime?.GameManager.CurrentWorld;
        if (world == null)
        {
            return false;
        }

        var entity = FindAutomationEntity(world);
        if (entity == null)
        {
            EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                $"[Automation] Entity not found: '{_automationOptions.EntityName ?? "<first>"}' index={_automationOptions.EntityIndex}");
            return false;
        }

        bool selectionChanged = !ReferenceEquals(_editorSelection.SelectedEntity, entity);
        _editorSelection.SetSelectedEntity(entity);

        var component = FindAutomationComponent(entity);
        if (component != null)
        {
            selectionChanged |= !ReferenceEquals(_editorSelection.SelectedComponent, component);
            _editorSelection.SetSelectedComponent(component);
            if (selectionChanged)
            {
                EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                    $"[Automation] Selected entity='{entity.Name}', component='{component.GetType().Name}'");
            }
        }
        else if (!string.IsNullOrWhiteSpace(_automationOptions.ComponentName))
        {
            EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                $"[Automation] Component not found on entity='{entity.Name}': '{_automationOptions.ComponentName}'");
        }

        return true;
    }

    private Entity? FindAutomationEntity(Framework.Scene.World.World world)
    {
        var entities = EnumerateEntities(world.Entities).ToList();
        if (string.IsNullOrWhiteSpace(_automationOptions.EntityName))
        {
            return entities.ElementAtOrDefault(_automationOptions.EntityIndex);
        }

        string expectedName = NormalizeAutomationToken(_automationOptions.EntityName);
        return entities
            .Where(entity => NormalizeAutomationToken(entity.Name) == expectedName)
            .ElementAtOrDefault(_automationOptions.EntityIndex);
    }

    private EntityComponent? FindAutomationComponent(Entity entity)
    {
        if (string.IsNullOrWhiteSpace(_automationOptions.ComponentName))
        {
            return null;
        }

        string expectedName = NormalizeAutomationToken(_automationOptions.ComponentName);
        return EnumerateComponents(entity)
            .FirstOrDefault(component => ComponentMatches(component, expectedName));
    }

    private void CaptureAutomationDiagnostics()
    {
        string outputPath = ResolveAutomationDiagnosticsPath();
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var entries = EditorDiagnosticsBuffer.GetEntriesSnapshot();
        var builder = new StringBuilder();
        builder.AppendLine("CasaEngine Editor diagnostics");
        builder.AppendLine($"Captured at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine($"Project: {_automationOptions.ProjectPath}");
        builder.AppendLine($"Open asset: {_automationOptions.OpenAssetPath ?? "<none>"}");
        builder.AppendLine($"Set material property: {FormatAutomationMaterialEdit()}");
        builder.AppendLine($"Entity: {_automationOptions.EntityName ?? "<first>"} [{_automationOptions.EntityIndex}]");
        builder.AppendLine($"Component: {_automationOptions.ComponentName ?? "<none>"}");
        string? activeDocumentPanelId = GetActiveDocumentPanelId();
        var openDocumentPanelIds = GetOpenDocumentPanelIds();
        builder.AppendLine($"Active document panel: {activeDocumentPanelId ?? "<none>"}");
        builder.AppendLine($"Open document panels: {FormatDocumentPanelIds(openDocumentPanelIds)}");
        AppendWorldViewportDiagnostics(builder);
        AppendMaterialInspectorDiagnostics(builder, activeDocumentPanelId);
        AppendAutomationSelectionDiagnostics(builder);
        builder.AppendLine($"Entries: {entries.Count}");
        builder.AppendLine();

        foreach (var entry in entries)
        {
            builder.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Verbosity}] {entry.Message}");
        }

        File.WriteAllText(outputPath, builder.ToString());
        EditorDiagnosticsBuffer.Append(LogVerbosity.Info, $"[Automation] Diagnostics exported to '{outputPath}'");
    }

    private void AppendWorldViewportDiagnostics(StringBuilder builder)
    {
        if (_worldViewportPanel == null)
        {
            return;
        }

        var viewportStates = _worldViewportPanel.GetAutomationStateSnapshot();
        if (viewportStates.Count == 0)
        {
            return;
        }

        builder.AppendLine("World viewport state:");
        for (int i = 0; i < viewportStates.Count; i++)
        {
            builder.AppendLine($"  - {viewportStates[i]}");
        }
    }

    private void AppendMaterialInspectorDiagnostics(StringBuilder builder, string? activeDocumentPanelId)
    {
        if (!TryGetActiveMaterialInspectorPanel(out var inspectorPanel))
        {
            return;
        }

        var propertyStates = inspectorPanel.GetAutomationPropertyStateSnapshot();
        if (propertyStates.Count > 0)
        {
            builder.AppendLine("Material property states:");
            for (int i = 0; i < propertyStates.Count; i++)
            {
                builder.AppendLine($"  - {propertyStates[i]}");
            }
        }

        IReadOnlyList<string> previewStates = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(activeDocumentPanelId)
            && _materialViewportPanels.TryGetValue(activeDocumentPanelId, out var materialViewportPanel))
        {
            previewStates = materialViewportPanel.GetAutomationStateSnapshot();
        }
        else
        {
            previewStates = inspectorPanel.GetAutomationPreviewStateSnapshot();
        }

        if (previewStates.Count == 0)
        {
            return;
        }

        builder.AppendLine("Material preview state:");
        for (int i = 0; i < previewStates.Count; i++)
        {
            builder.AppendLine($"  - {previewStates[i]}");
        }
    }

    private string FormatAutomationMaterialEdit()
    {
        if (string.IsNullOrWhiteSpace(_automationOptions.SetMaterialPropertyKey)
            || string.IsNullOrWhiteSpace(_automationOptions.SetMaterialPropertyValue))
        {
            return "<none>";
        }

        return $"{_automationOptions.SetMaterialPropertyKey}={_automationOptions.SetMaterialPropertyValue}";
    }

    private void AppendAutomationSelectionDiagnostics(StringBuilder builder)
    {
        builder.AppendLine($"Selected entity: {DescribeEntity(_editorSelection.SelectedEntity)}");
        builder.AppendLine($"Selected component: {DescribeComponent(_editorSelection.SelectedComponent)}");

        switch (_editorSelection.SelectedComponent)
        {
            case StaticModelSubMeshComponent staticModelSubMeshComponent:
                AppendStaticModelSubMeshDiagnostics(builder, staticModelSubMeshComponent);
                break;

            case StaticModelComponent staticModelComponent:
                AppendStaticModelComponentDiagnostics(builder, staticModelComponent);
                break;
        }
    }

    private static void AppendStaticModelSubMeshDiagnostics(StringBuilder builder, StaticModelSubMeshComponent component)
    {
        builder.AppendLine("Resolved sub-mesh materials:");

        if (component.ModelMesh == null)
        {
            builder.AppendLine("  - <no model mesh>");
            return;
        }

        builder.AppendLine($"  - Default: {DescribeRuntimeMaterial(component.ModelMesh.Material)}");
        if (component.MaterialOverridesBySlotIndex == null || component.MaterialOverridesBySlotIndex.Count == 0)
        {
            builder.AppendLine("  - Overrides: <none>");
            return;
        }

        foreach (var pair in component.MaterialOverridesBySlotIndex)
        {
            builder.AppendLine($"  - Override slot {pair.Key}: {DescribeRuntimeMaterial(pair.Value)}");
        }
    }

    private static void AppendStaticModelComponentDiagnostics(StringBuilder builder, StaticModelComponent component)
    {
        builder.AppendLine("Resolved static model materials:");

        if (component.StaticModel == null)
        {
            builder.AppendLine("  - <no static model>");
            return;
        }

        for (int meshIndex = 0; meshIndex < component.StaticModel.Meshes.Count; meshIndex++)
        {
            var mesh = component.StaticModel.Meshes[meshIndex];
            builder.AppendLine($"  - Mesh {meshIndex}: {DescribeRuntimeMaterial(mesh.Material)}");
        }
    }

    private static string DescribeRuntimeMaterial(MaterialBase? material)
    {
        if (material == null)
        {
            return "<null>";
        }

        var builder = new StringBuilder();
        builder.Append(material.GetType().Name);
        builder.Append($" id={material.Id}");
        if (!string.IsNullOrWhiteSpace(material.Name))
        {
            builder.Append($" name={material.Name}");
        }

        switch (material)
        {
            case LitDiffuseMaterial litDiffuseMaterial:
                builder.Append($" diffuse_color={FormatColor(litDiffuseMaterial.DiffuseColor)}");
                builder.Append($" specular_power={litDiffuseMaterial.SpecularPower.ToString("0.###", CultureInfo.InvariantCulture)}");
                builder.Append($" base_color_asset={litDiffuseMaterial.BasColorAssetId}");
                break;

            case UnlitTextureMaterial unlitTextureMaterial:
                builder.Append($" tint={FormatColor(unlitTextureMaterial.Tint)}");
                builder.Append($" alpha={unlitTextureMaterial.Alpha.ToString("0.###", CultureInfo.InvariantCulture)}");
                builder.Append($" base_color_asset={unlitTextureMaterial.BasColorAssetId}");
                break;
        }

        return builder.ToString();
    }

    private static string FormatColor(Color color)
        => string.Create(CultureInfo.InvariantCulture, $"{color.R},{color.G},{color.B},{color.A}");

    private static string FormatDocumentPanelIds(IReadOnlyList<string> panelIds)
    {
        if (panelIds.Count == 0)
        {
            return "<none>";
        }

        return string.Join(", ", panelIds);
    }

    private string ResolveAutomationDiagnosticsPath()
    {
        if (!string.IsNullOrWhiteSpace(_automationOptions.DiagnosticsOutputPath))
        {
            return Path.GetFullPath(_automationOptions.DiagnosticsOutputPath);
        }

        string projectPath = _automationOptions.ProjectPath ?? Path.Combine(Environment.CurrentDirectory, "editor-diagnostics.txt");
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory;
        return Path.Combine(projectDirectory, "editor-diagnostics.txt");
    }

    private static IEnumerable<Entity> EnumerateEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;

            foreach (var child in EnumerateEntities(entity.Children))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<EntityComponent> EnumerateComponents(Entity entity)
    {
        if (entity.RootComponent != null)
        {
            foreach (var component in EnumerateSceneComponents(entity.RootComponent))
            {
                yield return component;
            }
        }

        foreach (var component in entity.Components)
        {
            yield return component;
            if (component is SceneComponent sceneComponent)
            {
                foreach (var child in sceneComponent.Children.SelectMany(EnumerateSceneComponents))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<SceneComponent> EnumerateSceneComponents(SceneComponent component)
    {
        yield return component;
        foreach (var child in component.Children)
        {
            foreach (var nested in EnumerateSceneComponents(child))
            {
                yield return nested;
            }
        }
    }

    private static bool ComponentMatches(EntityComponent component, string expectedName)
    {
        string typeName = NormalizeAutomationToken(component.GetType().Name);
        if (typeName == expectedName)
        {
            return true;
        }

        if (typeName.EndsWith("component", StringComparison.Ordinal) && typeName[..^"component".Length] == expectedName)
        {
            return true;
        }

        return expectedName.EndsWith("component", StringComparison.Ordinal)
               && expectedName[..^"component".Length] == typeName;
    }

    private static string NormalizeAutomationToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var characters = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(characters).ToLowerInvariant();
    }

    private static string DescribeEntity(Entity? entity)
    {
        return entity == null
            ? "<null>"
            : $"'{entity.Name}'";
    }

    private static string DescribeComponent(EntityComponent? component)
    {
        if (component == null)
        {
            return "<null>";
        }

        return $"'{component.GetType().Name}' owner={DescribeEntity(component.Owner)}";
    }

    protected override void Draw(GameTime gameTime)
    {
        _editorRuntime?.DrawHost(gameTime);

        // Refresh the viewport binding after the hosted runtime rendered its view.
        _worldViewportPanel?.DrawViewport(gameTime);
        foreach (var materialViewportPanel in _materialViewportPanels.Values)
        {
            materialViewportPanel.DrawViewport(gameTime);
        }
        foreach (var materialInspectorPanel in _materialInspectorPanels.Values)
        {
            materialInspectorPanel.RefreshPreviewAfterDraw();
        }
        foreach (var animationClipPreviewPanel in _animationClipPreviewPanels.Values)
        {
            animationClipPreviewPanel.RefreshPreviewAfterDraw();
        }

        GraphicsDevice.Clear(Color.DimGray);

        _desktop?.Draw();

        base.Draw(gameTime);
    }

    /// <summary>
    /// Q-08: Export the current preview surface to a PNG file chosen via SaveFileDialog.
    /// </summary>
    private void ExportPreviewAsPng(UIScreenPreviewPanel panel)
    {
        var document = panel.CurrentDocument;
        if (document == null) return;

        // Use WPF SaveFileDialog through interop
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export screen as PNG",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = document.Root?.Name ?? "screen",
        };

        if (dialog.ShowDialog() != true) return;

        // Render the preview surface to a RenderTarget2D, then save
        try
        {
            var surfaceBounds = panel.PreviewSurfaceBounds;
            if (surfaceBounds == null) return;

            int w = Math.Max(1, surfaceBounds.Value.Width);
            int h = Math.Max(1, surfaceBounds.Value.Height);

            using var rt = new RenderTarget2D(GraphicsDevice, w, h,
                false, SurfaceFormat.Color, DepthFormat.None);

            var prevTargets = GraphicsDevice.GetRenderTargets();
            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(Color.Transparent);

            // Re-run the MGUI desktop draw so the preview paints onto our target
            _desktop?.Draw();

            GraphicsDevice.SetRenderTargets(prevTargets);

            using var fs = File.OpenWrite(dialog.FileName);
            rt.SaveAsPng(fs, w, h);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExportPNG] {ex.Message}");
        }
    }

    private static bool IsShortcutJustPressed(KeyboardState current, KeyboardState previous, Keys key)
        => current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private static readonly HashSet<string> _layoutContainers =
        new(StringComparer.OrdinalIgnoreCase) { "StackPanel", "DockPanel", "Grid", "UniformGrid" };

    private void OnScreenNodeMoveRequested(DocumentNodeId nodeId, int deltaX, int deltaY)
    {
        var document = _activeScreenPreviewPanel?.CurrentDocument;
        if (document == null) return;
        var node = document.FindNode(nodeId);
        if (node == null) return;

        // R-02: inside a layout-driven container → reorder instead of margin move
        if (node.Parent != null && _layoutContainers.Contains(node.Parent.ControlType))
        {
            if (node.Parent.ControlType.Equals("StackPanel", StringComparison.OrdinalIgnoreCase))
            {
                var orientation = node.Parent.Properties.TryGetValue("Orientation", out var op)
                    ? op.SerializedValue ?? "Vertical" : "Vertical";
                bool isHorizontal = orientation.Equals("Horizontal", StringComparison.OrdinalIgnoreCase);
                int delta = isHorizontal ? deltaX : deltaY;
                int oldIdx = node.Parent.IndexOfChild(node);
                int newIdx = Math.Clamp(oldIdx + (delta > 0 ? 1 : -1), 0, node.Parent.Children.Count - 1);
                if (newIdx != oldIdx)
                {
                    if (!TryGetActiveScreenCommandStack(out var reorderCommandStack))
                    {
                        return;
                    }

                    reorderCommandStack.Execute(new MoveChildCommand(node, newIdx));
                    RefreshScreenPanelsAfterCommand();
                }
            }
            return;
        }

        var (left, top, right, bottom) = ParseMargin(
            node.Properties.TryGetValue("Margin", out var mv) ? mv.SerializedValue : null);

        left += deltaX;
        top += deltaY;

        // Q-01: snap to 32 px grid when enabled
        if (_activeScreenPreviewPanel?.SnapToGrid == true)
        {
            const int Grid = 32;
            left = (int)Math.Round((double)left / Grid) * Grid;
            top  = (int)Math.Round((double)top  / Grid) * Grid;
        }

        var newMargin = $"{left},{top},{right},{bottom}";
        var cmd = new SetPropertyCommand(node, "Margin", newMargin);
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(cmd);
        RefreshScreenPanelsAfterCommand();
    }

    private void OnScreenNodeResizeRequested(DocumentNodeId nodeId, ResizeAnchor anchor, int deltaX, int deltaY)
    {
        var document = _activeScreenPreviewPanel?.CurrentDocument;
        if (document == null) return;
        var node = document.FindNode(nodeId);
        if (node == null) return;

        // Use rendered bounds as baseline when no explicit size is set
        var renderedBounds = _activeScreenPreviewPanel!.GetElementBounds(nodeId);
        int baseW = renderedBounds?.Width ?? 100;
        int baseH = renderedBounds?.Height ?? 30;

        if (node.Properties.TryGetValue("Width", out var wv) && int.TryParse(wv.SerializedValue, out var parsedW))
            baseW = parsedW;
        if (node.Properties.TryGetValue("Height", out var hv) && int.TryParse(hv.SerializedValue, out var parsedH))
            baseH = parsedH;

        var (marginLeft, marginTop, marginRight, marginBottom) = ParseMargin(
            node.Properties.TryGetValue("Margin", out var mv) ? mv.SerializedValue : null);

        // Each anchor: deltaX/deltaY is raw mouse delta.
        // Anchors that pull a left/top edge adjust both Margin and Width/Height inversely.
        int newW = baseW, newH = baseH;
        switch (anchor)
        {
            case ResizeAnchor.BottomRight:
                newW = Math.Max(8, baseW + deltaX);
                newH = Math.Max(8, baseH + deltaY);
                break;
            case ResizeAnchor.BottomLeft:
                newW = Math.Max(8, baseW - deltaX);
                newH = Math.Max(8, baseH + deltaY);
                marginLeft += baseW - newW; // move left edge rightward if width clamped
                break;
            case ResizeAnchor.TopRight:
                newW = Math.Max(8, baseW + deltaX);
                newH = Math.Max(8, baseH - deltaY);
                marginTop += baseH - newH;
                break;
            case ResizeAnchor.TopLeft:
                newW = Math.Max(8, baseW - deltaX);
                newH = Math.Max(8, baseH - deltaY);
                marginLeft += baseW - newW;
                marginTop  += baseH - newH;
                break;
        }

        var commands = new List<IUIScreenCommand>
        {
            new SetPropertyCommand(node, "Width",  newW.ToString()),
            new SetPropertyCommand(node, "Height", newH.ToString()),
        };

        // Only update Margin when it differs (avoid dirtying the document needlessly)
        var originalMargin = ParseMargin(node.Properties.TryGetValue("Margin", out var omv) ? omv.SerializedValue : null);
        if (marginLeft != originalMargin.left || marginTop != originalMargin.top)
            commands.Add(new SetPropertyCommand(node, "Margin", $"{marginLeft},{marginTop},{marginRight},{marginBottom}"));

        // R-06: group as a single undoable composite
        if (!TryGetActiveScreenCommandStack(out var commandStack))
        {
            return;
        }

        commandStack.Execute(new CompositeCommand("Resize", commands.ToArray()));
        RefreshScreenPanelsAfterCommand();
    }

    private static (int left, int top, int right, int bottom) ParseMargin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (0, 0, 0, 0);
        var parts = value.Split(',');
        if (parts.Length == 1 && int.TryParse(parts[0].Trim(), out var u)) return (u, u, u, u);
        if (parts.Length == 2
            && int.TryParse(parts[0].Trim(), out var h)
            && int.TryParse(parts[1].Trim(), out var v)) return (h, v, h, v);
        if (parts.Length >= 4
            && int.TryParse(parts[0].Trim(), out var l)
            && int.TryParse(parts[1].Trim(), out var t)
            && int.TryParse(parts[2].Trim(), out var r)
            && int.TryParse(parts[3].Trim(), out var b)) return (l, t, r, b);
        return (0, 0, 0, 0);
    }
}