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
        private const string WorldViewportPanelId = "panel_world_viewport";
        private const string ContentBrowserPanelId = "panel_content_browser";
        private const string OutputPanelId = "panel_output";
        private const string EntitiesPanelId = "panel_entities";
        private const string EntityDetailsPanelId = "panel_entity_details";
        private const string UIScreenHierarchyPanelId = "panel_ui_screen_hierarchy";
        private const string UIScreenInspectorPanelId  = "panel_ui_screen_inspector";
        private const string UIScreenToolboxPanelId    = "panel_ui_screen_toolbox";
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
        private Texture2D? _overlayPixel;
        private static readonly RasterizerState _scissorRasterizer = new() { ScissorTestEnable = true };
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
            _overlayPixel = new Texture2D(GraphicsDevice, 1, 1);
            _overlayPixel.SetData(new[] { Color.White });
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

            var scenePanel = new DockPanelNode(WorldViewportPanelId)
            {
                Title = "World Viewport",
                CanClose = false,
                CanFloat = false,
                ContentFactory = GetOrCreateWorldViewportContent,
            };

            var propertiesPanel = new DockPanelNode(EntityDetailsPanelId)
            {
                Title = "Details",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateEntityDetailsContent
            };

            var explorerPanel = new DockPanelNode(EntitiesPanelId)
            {
                Title = "Entities",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateEntitiesContent
            };

            var screenHierarchyPanel = new DockPanelNode(UIScreenHierarchyPanelId)
            {
                Title = "Screen Hierarchy",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateScreenHierarchyContent
            };

            var screenToolboxPanel = new DockPanelNode(UIScreenToolboxPanelId)
            {
                Title = "Screen Toolbox",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateScreenToolboxContent
            };

            var screenInspectorPanel = new DockPanelNode(UIScreenInspectorPanelId)
            {
                Title = "Screen Inspector",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateScreenInspectorContent
            };

            var contentBrowserPanel = new DockPanelNode(ContentBrowserPanelId)
            {
                Title = "Content Browser",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateContentBrowserContent
            };

            var outputPanel = new DockPanelNode(OutputPanelId)
            {
                Title = "Output / Logs",
                CanClose = true,
                CanFloat = true,
                ContentFactory = GetOrCreateLogsContent
            };

            // Tab groups
            var bottomGroup = new DockTabGroupNode();
            bottomGroup.AddPanel(contentBrowserPanel, -1);
            bottomGroup.AddPanel(outputPanel, -1);
            bottomGroup.SetActivePanel(contentBrowserPanel.Id);

            var centerGroup = new DockTabGroupNode();
            centerGroup.IsDocumentArea = true;
            centerGroup.AddPanel(scenePanel, -1);
            centerGroup.SetActivePanel(scenePanel.Id);

            var entitiesGroup = new DockTabGroupNode();
            entitiesGroup.AddPanel(explorerPanel, -1);
            entitiesGroup.AddPanel(screenHierarchyPanel, -1);
            entitiesGroup.AddPanel(screenToolboxPanel, -1);
            entitiesGroup.SetActivePanel(explorerPanel.Id);

            var detailsGroup = new DockTabGroupNode();
            detailsGroup.AddPanel(propertiesPanel, -1);
            detailsGroup.AddPanel(screenInspectorPanel, -1);
            detailsGroup.SetActivePanel(propertiesPanel.Id);

            var centerRightSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = centerGroup,
                SecondChild = detailsGroup,
                SplitRatio = 0.72f,
                MinFirstSize = 500,
                MinSecondSize = 260,
            };

            var topAreaSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = entitiesGroup,
                SecondChild = centerRightSplit,
                SplitRatio = 0.2f,
                MinFirstSize = 220,
                MinSecondSize = 700,
            };

            var rootSplit = new DockSplitNode
            {
                Orientation = Orientation.Vertical,
                FirstChild = topAreaSplit,
                SecondChild = bottomGroup,
                SplitRatio = 0.7f,
                MinFirstSize = 250,
                MinSecondSize = 120
            };

            _dockHost.LayoutModel.RootNode = rootSplit;
            _ = GetOrCreateLogsContent();
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
            ActivateDockPanel(ContentBrowserPanelId);
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
                    _activeScreenPreviewPanel?.LoadDocumentDirectly(doc);
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

        private void OnDockHostActivePanelChanged(object? sender, DockPanelNode panel)
        {
            if (panel.Id == OutputPanelId)
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

            _toggleContentBrowserButton = CreateStatusBarButton("Content Browser", () => ToggleDockPanel(ContentBrowserPanelId));
            _toggleLogsButton = CreateStatusBarButton("Logs", () => ToggleDockPanel(OutputPanelId));
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
                    IsDockPanelVisible(ContentBrowserPanelId) ? "Hide Content Browser" : "Show Content Browser");
            }

            if (_toggleLogsButton != null)
            {
                SetStatusBarButtonLabel(
                    _toggleLogsButton,
                    IsDockPanelVisible(OutputPanelId) ? "Hide Logs" : "Show Logs");
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
            return panelId switch
            {
                WorldViewportPanelId => GetOrCreateWorldViewportContent,
                EntitiesPanelId => GetOrCreateEntitiesContent,
                EntityDetailsPanelId => GetOrCreateEntityDetailsContent,
                ContentBrowserPanelId => GetOrCreateContentBrowserContent,
                OutputPanelId => GetOrCreateLogsContent,
                UIScreenHierarchyPanelId => GetOrCreateScreenHierarchyContent,
                UIScreenInspectorPanelId  => GetOrCreateScreenInspectorContent,
                UIScreenToolboxPanelId    => GetOrCreateScreenToolboxContent,
                _ => () => CreateUnavailablePanelContent(panelId),
            };
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
                    Logs.WriteWarning("No persisted editor layout was found for the current project.");
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
                ?? _dockHost.LayoutModel.GetAllTabGroups().FirstOrDefault(group => group.Panels.Any(panel => panel.Id == WorldViewportPanelId))
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

            DrawSelectionOverlay();

            base.Draw(gameTime);
        }

        private void DrawSelectionOverlay()
        {
            if (_activeScreenPreviewPanel == null || _overlayPixel == null)
            {
                return;
            }

            // Clipper le dessin aux bornes de la surface de preview pour éviter
            // que l'overlay ne déborde en dehors du panneau.
            var clipBounds = _activeScreenPreviewPanel.PreviewSurfaceBounds;
            var prevScissor = GraphicsDevice.ScissorRectangle;

            if (clipBounds.HasValue)
            {
                GraphicsDevice.ScissorRectangle = clipBounds.Value;
                _spriteBatch.Begin(rasterizerState: _scissorRasterizer);
            }
            else
            {
                _spriteBatch.Begin();
            }

            // ── Optional grid ────────────────────────────────────────────
            if (_activeScreenPreviewPanel.ShowGrid)
            {
                DrawPreviewGrid(_activeScreenPreviewPanel);
            }

            // ── Selection border + resize handle ─────────────────────────
            if (_screenSelection.SelectedNodeId.HasValue)
            {
                var bounds = _activeScreenPreviewPanel.GetElementBounds(_screenSelection.SelectedNodeId.Value);
                if (bounds.HasValue)
                {
                    var r = bounds.Value;
                    const int thickness = 2;
                    const int handleSize = 8;
                    var color = new Color(0, 120, 215, 200); // blue selection

                    // Top
                    _spriteBatch.Draw(_overlayPixel, new Rectangle(r.Left, r.Top, r.Width, thickness), color);
                    // Bottom
                    _spriteBatch.Draw(_overlayPixel, new Rectangle(r.Left, r.Bottom - thickness, r.Width, thickness), color);
                    // Left
                    _spriteBatch.Draw(_overlayPixel, new Rectangle(r.Left, r.Top, thickness, r.Height), color);
                    // Right
                    _spriteBatch.Draw(_overlayPixel, new Rectangle(r.Right - thickness, r.Top, thickness, r.Height), color);
                    // Resize handle (bottom-right corner square)
                    _spriteBatch.Draw(_overlayPixel, new Rectangle(r.Right - handleSize, r.Bottom - handleSize, handleSize, handleSize), color);
                }
            }

            _spriteBatch.End();

            // Restaurer l'état du scissor pour ne pas polluer les passes suivantes.
            if (clipBounds.HasValue)
            {
                GraphicsDevice.ScissorRectangle = prevScissor;
            }
        }

        private void DrawPreviewGrid(UIScreenPreviewPanel panel)
        {
            var surfaceBounds = panel.PreviewSurfaceBounds;
            if (surfaceBounds == null) return;

            var sb = surfaceBounds.Value;
            const int gridStep = 32;
            var gridColor = new Color(255, 255, 255, 30);

            for (int x = sb.Left; x <= sb.Right; x += gridStep)
            {
                _spriteBatch.Draw(_overlayPixel!, new Rectangle(x, sb.Top, 1, sb.Height), gridColor);
            }

            for (int y = sb.Top; y <= sb.Bottom; y += gridStep)
            {
                _spriteBatch.Draw(_overlayPixel!, new Rectangle(sb.Left, y, sb.Width, 1), gridColor);
            }
        }

        private void OnScreenNodeMoveRequested(DocumentNodeId nodeId, int deltaX, int deltaY)
        {
            var document = _activeScreenPreviewPanel?.CurrentDocument;
            if (document == null) return;
            var node = document.FindNode(nodeId);
            if (node == null) return;

            var (left, top, right, bottom) = ParseMargin(
                node.Properties.TryGetValue("Margin", out var mv) ? mv.SerializedValue : null);

            left += deltaX;
            top += deltaY;

            var newMargin = $"{left},{top},{right},{bottom}";
            var cmd = new SetPropertyCommand(node, "Margin", newMargin);
            _screenCommandStack.Execute(cmd);
            RefreshScreenPanelsAfterCommand();
        }

        private void OnScreenNodeResizeRequested(DocumentNodeId nodeId, int deltaW, int deltaH)
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

            var newW = Math.Max(8, baseW + deltaW).ToString();
            var newH = Math.Max(8, baseH + deltaH).ToString();

            _screenCommandStack.Execute(new SetPropertyCommand(node, "Width", newW));
            _screenCommandStack.Execute(new SetPropertyCommand(node, "Height", newH));
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
