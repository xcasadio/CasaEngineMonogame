using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.ProjectLauncher;
using CasaEngine.Editor.Workspaces;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI.MGUI;
using CasaEngine.Framework.Input;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.FontStashSharp;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor
{
    public class Game1 : Game, IObservableUpdate
    {
        private const string EditorLayoutDirectoryName = ".casaeditor";
        private const string EditorLayoutFileName = "layout.json";

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // ── MGUI core ──────────────────────────────────────────────────────
        private MainRenderer _mguiRenderer;
        private MGDesktop _desktop;
        private FontStashSharpTextEngine _fontStashSharpEngine;

        // ── Main editor window ─────────────────────────────────────────────
        private MGWindow _mainWindow;
        private MGDockPanel _rootPanel;
        private MGDockHost _dockHost;
        private MGMenuBar _menuBar;
        private MGDockPanel _statusBar;
        private EditorPanelRegistry _panelRegistry;
        private WorldEditorWorkspace _worldWorkspace;
        private UIScreenEditorWorkspace _uiScreenWorkspace;
        private EditorWorkspaceManager _workspaceManager;
        private EditorWorkspaceId _activeWorkspaceId = EditorWorkspaceId.World;
        private MGButton _toggleContentBrowserButton;
        private MGButton _toggleLogsButton;
        private MGTextBlock _statusProjectText;
        private MGTextBlock _statusStatsText;
        private string _lastStatusProjectLabel;
        private string _lastStatusStatsLabel;

        // ── Editor panels ──────────────────────────────────────────────────
        private LoggerEditor _loggerEditor;
        private EngineRuntimeContext _editorRuntimeContext;
        private HostedEditorGameAdapter _editorRuntime;
        private WorldViewportPanel _worldViewportPanel;
        private MGElement _worldViewportContent;
        private EntitiesPanel _entitiesPanel;
        private MGElement _entitiesContent;
        private EntityDetailsPanel _entityDetailsPanel;
        private MGElement _entityDetailsContent;
        private ContentBrowserPanel _contentBrowserPanel;
        private MGElement _contentBrowserContent;
        private readonly Dictionary<string, UIScreenPreviewPanel> _screenPreviewPanels = new(StringComparer.Ordinal);
        private readonly CasaEngine.EditorServices.ScreenEditor.Selection.UIScreenSelectionService _screenSelection = new();
        private readonly UICommandStack _screenCommandStack = new();
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
        private bool _isSynchronizingEntitySelection;
        private Entity _selectedEntity;
        private TimeSpan _fpsSampleElapsed;
        private int _fpsSampleFrames;
        private int _currentFps;
        private readonly EditorAutomationOptions _automationOptions;
        private bool _automationWorldLoaded;
        private bool _automationSelectionApplied;
        private bool _automationDiagnosticsCaptured;
        private TimeSpan _automationSelectionAppliedAt;

        // ── IObservableUpdate (required by GameRenderHost<Game1>) ──────────
        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public Game1(EditorAutomationOptions? automationOptions = null)
        {
            _automationOptions = automationOptions ?? new EditorAutomationOptions();
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
            _mguiRenderer = new MainRenderer(new GameRenderHost<Game1>(this), _windowInputSource);
            _desktop = new MGDesktop(_mguiRenderer);
            _desktop.LoadDefaultResources();
            _fontStashSharpEngine = new FontStashSharpTextEngine();
            const string familyName = "JetBrainsMono";
            string ttfDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"Content\fonts\JetBrainsMono"));

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
            _fontStashSharpEngine.MatchSpriteFontSizing(_desktop.FontManager);
            _desktop.TextEngine = _fontStashSharpEngine;

            // ── Register editor logger ─────────────────────────────────────
            _loggerEditor = new LoggerEditor();
            Logs.AddLogger(_loggerEditor);
            Logs.AddLogger(new DebugLogger());
            Logs.Verbosity = LogVerbosity.Trace;

            InitializeEditorRuntime();

            // ── Main window (borderless, fills the screen) ─────────────────
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

            // ── Layout shell: editor chrome is added only once a project has actually been opened.
            _rootPanel = new MGDockPanel(_mainWindow);
            _rootPanel.Name = "EditorRootPanel";
            _mainWindow.SetContent(_rootPanel);

            _desktop.Windows.Add(_mainWindow);

            HookViewportInputCoordination();

            _editorSelection.SelectionChanged += OnEditorSelectionChanged;
            _editorSelection.ComponentSelectionChanged += OnEditorComponentSelectionChanged;
            _screenSelection.SelectionChanged += id => { if (_activeScreenPreviewPanel != null) _activeScreenPreviewPanel.SelectedNodeId = id; };

            EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;

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
                item.Submenu = new MGContextMenu(_mainWindow, null);
                item.Submenu.AddButton("Undo", _ => ExecuteUndo());
                item.Submenu.AddButton("Redo", _ => ExecuteRedo());
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Duplicate", _ => ExecuteDuplicate());
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Cut", _ => ExecuteCut());
                item.Submenu.AddButton("Copy", _ => ExecuteCopy());
                item.Submenu.AddButton("Paste", _ => ExecutePaste());
            });

            // Windows menu
            _menuBar.AddItem("Windows", item =>
            {
                item.Submenu = new MGContextMenu(_mainWindow, null);
                item.Submenu.AddButton("Save Layout", _ => SaveDockLayout());
                item.Submenu.AddButton("Load Layout", _ => LoadDockLayout());
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Reset Layout", _ => SetupInitialDockLayout());
            });

            // Help menu
            _menuBar.AddItem("Help", item =>
            {
                item.Submenu = new MGContextMenu(_mainWindow, null);
                item.Submenu.AddButton("About CasaEngine", _ => { });
            });
        }

        private void SetupInitialDockLayout()
        {
            if (_dockHost == null)
            {
                return;
            }

            _panelRegistry ??= CreatePanelRegistry();

            _worldWorkspace ??= new WorldEditorWorkspace(_panelRegistry);
            _uiScreenWorkspace ??= new UIScreenEditorWorkspace(_panelRegistry);
            _workspaceManager ??= CreateWorkspaceManager();
            _workspaceManager.ResetWorkspaceLayout(EditorWorkspaceId.World);
        }

        private void OnProjectLoaded(object? sender, EventArgs e)
        {
            SynchronizeEditorRuntimeContext();
            _editorSelection.Clear();
            _automationWorldLoaded = false;
            _automationSelectionApplied = false;
            _automationDiagnosticsCaptured = false;
            PresentLoadedProject();
        }

        private void PresentLoadedProject()
        {
            EnsureShellChromeInitialized();
            EnsureDockHostInitialized();

            if (!TryLoadPersistedDockLayout(logOutcome: false))
            {
                SetupInitialDockLayout();
            }

            _ = GetOrCreateWorldViewportContent();
            _ = GetOrCreateEntitiesContent();
            _ = GetOrCreateEntityDetailsContent();
            _ = GetOrCreateContentBrowserContent();

            _contentBrowserPanel?.Refresh();
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

            if (_statusBar == null)
            {
                _statusBar = CreateStatusBar();
                _rootPanel.TryAddChild(_statusBar, Dock.Bottom);
            }
        }

        private void UpdateWindowTitle()
        {
            Window.Title = string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.ProjectName)
                ? "CasaEngine Editor"
                : $"CasaEngine Editor - {GameSettings.ProjectSettings.ProjectName}";
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
            _dockHost.PanelAdded += OnDockHostPanelVisibilityChanged;
            _dockHost.PanelRemoved += OnDockHostPanelVisibilityChanged;
            _rootPanel.TryAddChild(_dockHost, Dock.Top);
            SetupInitialDockLayout();
            RefreshStatusBar();
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
            _worldViewportPanel.SetSelectedEntity(_editorSelection.SelectedEntity);
            return _worldViewportContent;
        }

        private MGElement GetOrCreateEntitiesContent()
        {
            if (_entitiesPanel == null)
            {
                _entitiesPanel = new EntitiesPanel(_mainWindow, () => _editorRuntime?.GameManager.CurrentWorld);
                _entitiesPanel.SelectedEntityChanged += OnEntitiesPanelSelectionChanged;
                _entitiesPanel.EntityDoubleClicked += OnEntitiesPanelEntityDoubleClicked;
            }

            _entitiesContent ??= _entitiesPanel.CreateContent();
            _entitiesPanel.SetSelectedEntity(_editorSelection.SelectedEntity);
            return _entitiesContent;
        }

        private MGElement GetOrCreateScreenHierarchyContent()
        {
            if (_screenHierarchyPanel == null)
            {
                _screenHierarchyPanel = new UIScreenHierarchyPanel(_mainWindow, _screenSelection);
                _screenHierarchyPanel.SetCommandStack(_screenCommandStack);
                _screenHierarchyPanel.NodeDeleted += doc =>
                {
                    _activeScreenPreviewPanel?.LoadDocumentDirectly(doc);
                    _screenInspectorPanel?.SetDocument(doc);
                };
                _screenHierarchyPanel.NodeDuplicateRequested += (doc, nodeId) =>
                {
                    var dupCmd = new DuplicateNodeCommand(doc, nodeId);
                    _screenCommandStack.Execute(dupCmd);
                    RefreshScreenPanelsAfterCommand();
                    if (dupCmd.CreatedNode != null)
                        _screenSelection.Select(dupCmd.CreatedNode.Id);
                };
            }

            _screenHierarchyContent ??= _screenHierarchyPanel.CreateContent();
            return _screenHierarchyContent;
        }

        private MGElement GetOrCreateScreenInspectorContent()
        {
            if (_screenInspectorPanel == null)
            {
                _screenInspectorPanel = new UIScreenInspectorPanel(_mainWindow, _screenSelection);
                _screenInspectorPanel.SetCommandStack(_screenCommandStack);
                _screenInspectorPanel.DocumentModified += doc =>
                {
                    // Only called for structural changes (e.g. Name field edits visible in tree)
                    _activeScreenPreviewPanel?.LoadDocumentDirectly(doc);
                    _screenHierarchyPanel?.SetDocument(doc);
                };
                _screenInspectorPanel.PropertyModified += (doc, nodeId, propName, value) =>
                {
                    // Fast path: patch the live MGElement directly
                    if (_activeScreenPreviewPanel?.TryApplyPropertyUpdate(nodeId, propName, value) != true)
                        _activeScreenPreviewPanel?.RefreshPreviewOnly();
                };
            }

            _screenInspectorContent ??= _screenInspectorPanel.CreateContent();
            return _screenInspectorContent;
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
            return _screenToolboxContent;
        }

        private void OnToolboxControlRequested(CasaEngine.EditorServices.ScreenEditor.Toolbox.UIControlRegistryEntry entry)
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
            _screenCommandStack.Execute(cmd);
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
            if (!_screenCommandStack.CanUndo)
            {
                return;
            }

            _screenCommandStack.Undo();
            RefreshScreenPanelsAfterCommand();
        }

        private void ExecuteRedo()
        {
            if (!_screenCommandStack.CanRedo)
            {
                return;
            }

            _screenCommandStack.Redo();
            RefreshScreenPanelsAfterCommand();
        }

        private void ExecuteDuplicate()
        {
            if (_activeScreenPreviewPanel == null || !_screenSelection.SelectedNodeId.HasValue) return;
            var document = _activeScreenPreviewPanel.CurrentDocument;
            if (document == null) return;

            var cmd = new DuplicateNodeCommand(document, _screenSelection.SelectedNodeId.Value);
            _screenCommandStack.Execute(cmd);
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
            _screenCommandStack.Execute(cmd);
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
            _screenCommandStack.Execute(cmd);
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

        private MGElement GetOrCreateEntityDetailsContent()
        {
            if (_entityDetailsPanel == null)
            {
                _entityDetailsPanel = new EntityDetailsPanel(_mainWindow);
                _entityDetailsPanel.SelectedComponentChanged += OnEntityDetailsSelectedComponentChanged;
            }

            _entityDetailsContent ??= _entityDetailsPanel.CreateContent();
            _entityDetailsPanel.SetSelectedEntity(_editorSelection.SelectedEntity);
            _entityDetailsPanel.SetSelectedComponent(_editorSelection.SelectedComponent);
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
                    Scope = EditorPanelScope.World,
                    Kind = EditorPanelKind.Document,
                    CanClose = false,
                    CanFloat = false,
                    ContentFactory = GetOrCreateWorldViewportContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.Entities,
                    Title = "Entities",
                    Scope = EditorPanelScope.World,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateEntitiesContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.EntityDetails,
                    Title = "Details",
                    Scope = EditorPanelScope.World,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateEntityDetailsContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.ContentBrowser,
                    Title = "Content Browser",
                    Scope = EditorPanelScope.Common,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateContentBrowserContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.Output,
                    Title = "Output / Logs",
                    Scope = EditorPanelScope.Common,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateLogsContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.UIScreenHierarchy,
                    Title = "Screen Hierarchy",
                    Scope = EditorPanelScope.UIScreen,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateScreenHierarchyContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.UIScreenInspector,
                    Title = "Screen Inspector",
                    Scope = EditorPanelScope.UIScreen,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateScreenInspectorContent,
                },
                new EditorPanelDescriptor
                {
                    Id = EditorPanelIds.UIScreenToolbox,
                    Title = "Screen Toolbox",
                    Scope = EditorPanelScope.UIScreen,
                    Kind = EditorPanelKind.Tool,
                    ContentFactory = GetOrCreateScreenToolboxContent,
                },
            });
        }

        private EditorWorkspaceManager CreateWorkspaceManager()
        {
            return new EditorWorkspaceManager(
                new IEditorWorkspace[] { _worldWorkspace, _uiScreenWorkspace },
                workspaceId => SavePersistedDockLayout(workspaceId),
                (workspaceId, logOutcome) => TryLoadPersistedDockLayout(workspaceId, logOutcome),
                layout => _dockHost.LayoutModel.RootNode = layout,
                () =>
                {
                    _activeWorkspaceId = _workspaceManager?.ActiveWorkspaceId ?? _activeWorkspaceId;
                    _ = GetOrCreateLogsContent();
                    RefreshStatusBar();
                });
        }

        private void OnDockHostActivePanelChanged(object? sender, DockPanelNode panel)
        {
            if (panel.Id == EditorPanelIds.Output)
            {
                _logsPanel?.Refresh();
            }

            RefreshStatusBar();
        }

        private void OnDockHostPanelVisibilityChanged(object? sender, DockPanelNode panel)
        {
            RefreshStatusBar();
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

        private MGDockPanel CreateStatusBar()
        {
            var statusBar = new MGDockPanel(_mainWindow)
            {
                Padding = new MonoGame.Extended.Thickness(8, 4, 8, 4),
                MinHeight = 32,
                MaxHeight = 32,
            };

            var leftStack = new MGStackPanel(_mainWindow, Orientation.Horizontal)
            {
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _toggleContentBrowserButton = CreateStatusBarButton("Content Browser", () => ToggleDockPanel(EditorPanelIds.ContentBrowser));
            _toggleLogsButton = CreateStatusBarButton("Logs", () => ToggleDockPanel(EditorPanelIds.Output));
            leftStack.TryAddChild(_toggleContentBrowserButton);
            leftStack.TryAddChild(_toggleLogsButton);

            var rightStack = new MGStackPanel(_mainWindow, Orientation.Horizontal)
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _statusProjectText = new MGTextBlock(_mainWindow, "Project: none")
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

            _statusStatsText = new MGTextBlock(_mainWindow, "FPS: 0 | Entities: 0")
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

            rightStack.TryAddChild(_statusProjectText);
            rightStack.TryAddChild(_statusStatsText);

            statusBar.TryAddChild(leftStack, Dock.Left);
            statusBar.TryAddChild(rightStack, Dock.Right);
            return statusBar;
        }

        private MGButton CreateStatusBarButton(string label, Action onClick)
        {
            var button = new MGButton(_mainWindow, _ => onClick())
            {
                PreferredHeight = 24,
                Padding = new MonoGame.Extended.Thickness(10, 2, 10, 2),
            };
            SetStatusBarButtonLabel(button, label);
            return button;
        }

        private void SetStatusBarButtonLabel(MGButton button, string label)
        {
            if (button.Content is MGTextBlock existingLabel)
            {
                if (!string.Equals(existingLabel.Text, label, StringComparison.Ordinal))
                {
                    existingLabel.SetText(label);
                }

                return;
            }

            button.SetContent(new MGTextBlock(_mainWindow, label)
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        private void ToggleDockPanel(string panelId)
        {
            if (_dockHost == null)
            {
                return;
            }

            if (_dockHost.FindPanel(panelId) != null)
            {
                _dockHost.RemovePanel(panelId);
            }
            else if (_dockHost.ShowDockable(panelId))
            {
                ActivateDockPanel(panelId);
            }

            RefreshStatusBar();
        }

        private bool IsDockPanelVisible(string panelId)
        {
            return _dockHost?.FindPanel(panelId) != null;
        }

        private void RefreshStatusBar()
        {
            if (_statusProjectText == null || _statusStatsText == null)
            {
                return;
            }

            string projectName = string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.ProjectName)
                ? "none"
                : GameSettings.ProjectSettings.ProjectName;
            string projectLabel = $"Project: {projectName}";
            if (!string.Equals(_lastStatusProjectLabel, projectLabel, StringComparison.Ordinal))
            {
                _statusProjectText.SetText(projectLabel);
                _lastStatusProjectLabel = projectLabel;
            }

            int entityCount = CountEntities(_editorRuntime?.GameManager.CurrentWorld);
            string statsLabel = $"FPS: {_currentFps:D3} | Entities: {entityCount:D6}";
            if (!string.Equals(_lastStatusStatsLabel, statsLabel, StringComparison.Ordinal))
            {
                _statusStatsText.SetText(statsLabel, SuppressLayoutChanged: true);
                _lastStatusStatsLabel = statsLabel;
            }

            if (_toggleContentBrowserButton != null)
            {
                SetStatusBarButtonLabel(
                    _toggleContentBrowserButton,
                    IsDockPanelVisible(EditorPanelIds.ContentBrowser) ? "Hide Content Browser" : "Show Content Browser");
            }

            if (_toggleLogsButton != null)
            {
                SetStatusBarButtonLabel(
                    _toggleLogsButton,
                    IsDockPanelVisible(EditorPanelIds.Output) ? "Hide Logs" : "Show Logs");
            }
        }

        private static int CountEntities(CasaEngine.Framework.World.World? world)
        {
            if (world == null)
            {
                return 0;
            }

            return world.Entities.Sum(CountEntityRecursive);
        }

        private static int CountEntityRecursive(Entity entity)
        {
            return 1 + entity.Children.Sum(CountEntityRecursive);
        }

        private void ShowProjectLauncher()
        {
            var launcher = new ProjectLauncherWindow(_mainWindow, QueueProjectOpen, QueueProjectCreate);
            launcher.Show();
        }

        private void HookViewportInputCoordination()
        {
            _desktop.HighPriorityMouseHandler.LMBPressedInside += (_, e) =>
                _worldViewportPanel?.ReleaseInputIfOutside(e.Position);
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

            EditorProjectAuthoringService.SaveProject();
            EditorAssetCatalogService.Save();
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
                SetupInitialDockLayout();
                RefreshStatusBar();
            }
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

        private static string GetLayoutFileName(EditorWorkspaceId workspaceId)
        {
            return workspaceId switch
            {
                EditorWorkspaceId.World => "layout.world.json",
                EditorWorkspaceId.UIScreen => "layout.uiscreen.json",
                _ => "layout.json",
            };
        }

        private string GetLegacyPersistedLayoutPath()
        {
            var projectDirectory = GetCurrentProjectDirectory();
            if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                return null;
            }

            return Path.Combine(projectDirectory, EditorLayoutDirectoryName, EditorLayoutFileName);
        }

        private string GetPersistedLayoutPath(EditorWorkspaceId workspaceId)
        {
            var projectDirectory = GetCurrentProjectDirectory();
            if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                return null;
            }

            return Path.Combine(projectDirectory, EditorLayoutDirectoryName, GetLayoutFileName(workspaceId));
        }

        private void SavePersistedDockLayout()
        {
            SavePersistedDockLayout(_activeWorkspaceId);
        }

        private void SavePersistedDockLayout(EditorWorkspaceId workspaceId)
        {
            if (_dockHost == null)
            {
                return;
            }

            var layoutPath = GetPersistedLayoutPath(workspaceId);
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
            Logs.WriteInfo($"Editor layout saved for workspace '{workspaceId}': {layoutPath}");
        }

        private bool TryLoadPersistedDockLayout(bool logOutcome)
        {
            return TryLoadPersistedDockLayout(_activeWorkspaceId, logOutcome);
        }

        private bool TryLoadPersistedDockLayout(EditorWorkspaceId workspaceId, bool logOutcome)
        {
            if (_dockHost == null)
            {
                return false;
            }

            var layoutPath = GetPersistedLayoutPath(workspaceId);
            if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath))
            {
                if (workspaceId == EditorWorkspaceId.World)
                {
                    var legacyLayoutPath = GetLegacyPersistedLayoutPath();
                    if (!string.IsNullOrWhiteSpace(legacyLayoutPath) && File.Exists(legacyLayoutPath))
                    {
                        layoutPath = legacyLayoutPath;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath))
            {
                if (logOutcome)
                {
                    Logs.WriteWarning($"No persisted editor layout was found for workspace '{workspaceId}'.");
                }

                return false;
            }

            try
            {
                var json = File.ReadAllText(layoutPath);
                _dockHost.LoadLayoutFromJson(json, GetPanelContentFactory);
                RefreshStatusBar();

                if (logOutcome)
                {
                    Logs.WriteInfo($"Editor layout loaded for workspace '{workspaceId}': {layoutPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Logs.WriteWarning($"Failed to load persisted editor layout '{layoutPath}': {ex.Message}");
                return false;
            }
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

        private void OnEntitiesPanelSelectionChanged(Entity? entity)
        {
            _editorSelection.SetSelectedEntity(entity);
        }

        private void OnEntitiesPanelEntityDoubleClicked(Entity entity)
        {
            _worldViewportPanel?.FocusEntity(entity);
        }

        private void OnContentBrowserFileOpened(ContentBrowser.Models.ContentItem item)
        {
            TryOpenUIScreenAsset(item.FullPath);
        }

        private bool TryOpenUIScreenAsset(string fullPath)
        {
            if (!TryLoadUIScreenAsset(fullPath, out var screenAsset))
            {
                return false;
            }

            EnsureDockHostInitialized();

            var panelId = $"panel_ui_screen_{screenAsset.Id:N}";
            if (!_screenPreviewPanels.TryGetValue(panelId, out var previewPanel))
            {
                previewPanel = new UIScreenPreviewPanel(_mainWindow);
                previewPanel.SetSelectionService(_screenSelection);
                previewPanel.DocumentLoaded += doc =>
                {
                    _screenHierarchyPanel?.SetDocument(doc);
                    _screenInspectorPanel?.SetDocument(doc);
                    _screenToolboxPanel?.SetDocument(doc);
                };
                previewPanel.NodePicked += id =>
                {
                    if (id.HasValue) _screenSelection.Select(id.Value);
                    else _screenSelection.ClearSelection();
                };
                previewPanel.NodeMoveRequested += OnScreenNodeMoveRequested;
                previewPanel.NodeResizeRequested += OnScreenNodeResizeRequested;
                previewPanel.ExportPngRequested += () => ExportPreviewAsPng(previewPanel);
                _screenPreviewPanels.Add(panelId, previewPanel);
            }

            previewPanel.LoadAsset(screenAsset, fullPath);
            _activeScreenPreviewPanel = previewPanel;

            var existingPanel = _dockHost?.FindPanel(panelId);
            if (existingPanel == null)
            {
                var panelNode = new DockPanelNode(panelId)
                {
                    Title = string.IsNullOrWhiteSpace(screenAsset.Name) ? Path.GetFileNameWithoutExtension(fullPath) : screenAsset.Name,
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
                existingPanel.Title = string.IsNullOrWhiteSpace(screenAsset.Name) ? existingPanel.Title : screenAsset.Name;
            }

            ActivateDockPanel(panelId);
            return true;
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

        private void OnViewportSelectedEntityChanged(Entity? entity)
        {
            if (_automationOptions.HasAutomation)
            {
                return;
            }

            _editorSelection.SetSelectedEntity(entity);
        }

        private void OnEditorSelectionChanged(Entity? entity)
        {
            _selectedEntity = entity;

            if (_isSynchronizingEntitySelection)
            {
                return;
            }

            _isSynchronizingEntitySelection = true;
            try
            {
                _entitiesPanel?.SetSelectedEntity(entity);
                if (!_automationOptions.HasAutomation)
                {
                    _worldViewportPanel?.SetSelectedEntity(entity);
                }
                _entityDetailsPanel?.SetSelectedEntity(entity);
            }
            finally
            {
                _isSynchronizingEntitySelection = false;
            }
        }

        private void OnEditorComponentSelectionChanged(CasaEngine.Framework.Entities.Components.EntityComponent? component)
        {
            _entityDetailsPanel?.SetSelectedComponent(component);
        }

        private void OnEntityDetailsSelectedComponentChanged(CasaEngine.Framework.Entities.Components.EntityComponent? component)
        {
            _editorSelection.SetSelectedComponent(component);
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
            var kb = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            bool ctrl = kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)
                     || kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
            if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Z))
            {
                ExecuteUndo();
            }
            else if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Y))
            {
                ExecuteRedo();
            }
            else if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D))
            {
                ExecuteDuplicate();
            }
            else if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C))
            {
                ExecuteCopy();
            }
            else if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.X))
            {
                ExecuteCut();
            }
            else if (ctrl && kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V))
            {
                ExecutePaste();
            }

            _fpsSampleElapsed += gameTime.ElapsedGameTime;
            _fpsSampleFrames++;
            if (_fpsSampleElapsed >= TimeSpan.FromSeconds(0.5))
            {
                _currentFps = (int)Math.Round(_fpsSampleFrames / _fpsSampleElapsed.TotalSeconds);
                _fpsSampleElapsed = TimeSpan.Zero;
                _fpsSampleFrames = 0;
            }

            foreach (var previewPanel in _screenPreviewPanels.Values)
            {
                previewPanel.Update();
            }

            _desktop.Update();
            ProcessPendingProjectLauncherAction();
            _editorRuntime?.UpdateHost(gameTime);
            _entitiesPanel?.Update();
            _worldViewportPanel?.UpdateInput(gameTime);
            RunAutomation(gameTime.TotalGameTime);
            RefreshStatusBar();

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

            if (!_automationSelectionApplied || !IsAutomationSelectionActive())
            {
                if (TryApplyAutomationSelection())
                {
                    _automationSelectionApplied = true;
                    _automationSelectionAppliedAt = totalGameTime;
                }

                return;
            }

            if (totalGameTime - _automationSelectionAppliedAt < TimeSpan.FromSeconds(_automationOptions.CaptureDelaySeconds))
            {
                return;
            }

            CaptureAutomationDiagnostics();
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

        private Entity? FindAutomationEntity(CasaEngine.Framework.World.World world)
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

        private CasaEngine.Framework.Entities.Components.EntityComponent? FindAutomationComponent(Entity entity)
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
            builder.AppendLine($"Entity: {_automationOptions.EntityName ?? "<first>"} [{_automationOptions.EntityIndex}]");
            builder.AppendLine($"Component: {_automationOptions.ComponentName ?? "<none>"}");
            builder.AppendLine($"Entries: {entries.Count}");
            builder.AppendLine();

            foreach (var entry in entries)
            {
                builder.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Verbosity}] {entry.Message}");
            }

            File.WriteAllText(outputPath, builder.ToString());
            EditorDiagnosticsBuffer.Append(LogVerbosity.Info, $"[Automation] Diagnostics exported to '{outputPath}'");
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

        private static IEnumerable<CasaEngine.Framework.Entities.Components.EntityComponent> EnumerateComponents(Entity entity)
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
                if (component is CasaEngine.Framework.Entities.Components.SceneComponent sceneComponent)
                {
                    foreach (var child in sceneComponent.Children.SelectMany(EnumerateSceneComponents))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static IEnumerable<CasaEngine.Framework.Entities.Components.SceneComponent> EnumerateSceneComponents(CasaEngine.Framework.Entities.Components.SceneComponent component)
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

        private static bool ComponentMatches(CasaEngine.Framework.Entities.Components.EntityComponent component, string expectedName)
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

        protected override void Draw(GameTime gameTime)
        {
            _editorRuntime?.DrawHost(gameTime);

            // Refresh the viewport binding after the hosted runtime rendered its view.
            _worldViewportPanel?.DrawViewport(gameTime);

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
                        _screenCommandStack.Execute(new MoveChildCommand(node, newIdx));
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
            _screenCommandStack.Execute(cmd);
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

            var commands = new System.Collections.Generic.List<IUIScreenCommand>
            {
                new SetPropertyCommand(node, "Width",  newW.ToString()),
                new SetPropertyCommand(node, "Height", newH.ToString()),
            };

            // Only update Margin when it differs (avoid dirtying the document needlessly)
            var originalMargin = ParseMargin(node.Properties.TryGetValue("Margin", out var omv) ? omv.SerializedValue : null);
            if (marginLeft != originalMargin.left || marginTop != originalMargin.top)
                commands.Add(new SetPropertyCommand(node, "Margin", $"{marginLeft},{marginTop},{marginRight},{marginBottom}"));

            // R-06: group as a single undoable composite
            _screenCommandStack.Execute(new CompositeCommand("Resize", commands.ToArray()));
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
}
