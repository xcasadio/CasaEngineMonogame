using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.ProjectLauncher;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
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
using System.IO;
using System.Linq;

namespace CasaEngine.Editor
{
    public class Game1 : Game, IObservableUpdate
    {
        private const string WorldViewportPanelId = "panel_world_viewport";
        private const string ContentBrowserPanelId = "panel_content_browser";
        private const string OutputPanelId = "panel_output";
        private const string EntitiesPanelId = "panel_entities";
        private const string EntityDetailsPanelId = "panel_entity_details";

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
        private LogsPanel _logsPanel;
        private MGElement _logsContent;
        private Action? _pendingProjectLauncherAction;
        private FrameCachedWindowInputSource _windowInputSource;
        private readonly EditorSelection _editorSelection = EditorSelection.Current;
        private bool _isSynchronizingEntitySelection;
        private Entity _selectedEntity;

        // ── IObservableUpdate (required by GameRenderHost<Game1>) ──────────
        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public Game1()
        {
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

            // ── Bootstrap MGUI ─────────────────────────────────────────────
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

            // ── Menu bar ─────────────────────────────────────────────────
            _menuBar = new MGMenuBar(_mainWindow);
            BuildMenuBar();

            // ── Layout: menu bar is available immediately, editor dock host
            // is added only once a project has actually been opened.
            _rootPanel = new MGDockPanel(_mainWindow);
            _rootPanel.Name = "EditorRootPanel";
            _rootPanel.TryAddChild(_menuBar, Dock.Top);
            _mainWindow.SetContent(_rootPanel);

            _desktop.Windows.Add(_mainWindow);

            HookViewportInputCoordination();

            _editorSelection.SelectionChanged += OnEditorSelectionChanged;
            _editorSelection.ComponentSelectionChanged += OnEditorComponentSelectionChanged;

            EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;

            // ── Show project launcher at startup ───────────────────────────
            var launcher = new ProjectLauncherWindow(_mainWindow, QueueProjectOpen, QueueProjectCreate);
            launcher.Show();

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
                item.Submenu.AddButton("Undo", _ => { });
                item.Submenu.AddButton("Redo", _ => { });
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Cut", _ => { });
                item.Submenu.AddButton("Copy", _ => { });
                item.Submenu.AddButton("Paste", _ => { });
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
            centerGroup.AddPanel(scenePanel, -1);
            centerGroup.SetActivePanel(scenePanel.Id);

            var entitiesGroup = new DockTabGroupNode();
            entitiesGroup.AddPanel(explorerPanel, -1);
            entitiesGroup.SetActivePanel(explorerPanel.Id);

            var detailsGroup = new DockTabGroupNode();
            detailsGroup.AddPanel(propertiesPanel, -1);
            detailsGroup.SetActivePanel(propertiesPanel.Id);

            var rightSideSplit = new DockSplitNode
            {
                Orientation = Orientation.Vertical,
                FirstChild = entitiesGroup,
                SecondChild = detailsGroup,
                SplitRatio = 0.52f,
                MinFirstSize = 180,
                MinSecondSize = 180,
            };

            var topAreaSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = centerGroup,
                SecondChild = rightSideSplit,
                SplitRatio = 0.76f,
                MinFirstSize = 500,
                MinSecondSize = 260,
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
            EnsureDockHostInitialized();
            _contentBrowserPanel?.Refresh();
            ActivateDockPanel(ContentBrowserPanelId);
            LoadInitialWorldIntoEditorRuntime();

            if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.ProjectName))
            {
                Window.Title = $"CasaEngine Editor - {GameSettings.ProjectSettings.ProjectName}";
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
            _editorRuntimeContext.WindowInputSource = _windowInputSource;

            _editorRuntime = new HostedEditorGameAdapter(null, graphicsDeviceService, _editorRuntimeContext)
            {
                ExecutionPolicy = GameplayExecutionPolicies.EditorPreview,
            };

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
            _rootPanel.TryAddChild(_dockHost, Dock.Top);
            SetupInitialDockLayout();
        }

        private MGElement GetOrCreateContentBrowserContent()
        {
            _contentBrowserPanel ??= new ContentBrowserPanel(_mainWindow);
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
            if (string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.ProjectFileOpened))
            {
                Logs.WriteWarning("No project is currently loaded.");
                return;
            }

            EditorProjectAuthoringService.SaveProject();
            EditorAssetCatalogService.Save();
            Logs.WriteInfo($"Project saved: {GameSettings.ProjectSettings.ProjectFileOpened}");
        }

        private void SaveDockLayout()
        {
            if (_dockHost == null)
            {
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CasaEngine layout files (*.json)|*.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                AddExtension = true,
                FileName = "layout.json",
                InitialDirectory = GetCurrentProjectDirectory(),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, _dockHost.SaveLayoutToJson(indented: true));
            Logs.WriteInfo($"Editor layout saved: {dialog.FileName}");
        }

        private void LoadDockLayout()
        {
            if (_dockHost == null)
            {
                return;
            }

            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "CasaEngine layout files (*.json)|*.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                CheckFileExists = true,
                InitialDirectory = GetCurrentProjectDirectory(),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var json = File.ReadAllText(dialog.FileName);
            _dockHost.LoadLayoutFromJson(json, GetPanelContentFactory);
            Logs.WriteInfo($"Editor layout loaded: {dialog.FileName}");
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
                _ => null,
            };
        }

        private string GetCurrentProjectDirectory()
        {
            var projectFile = GameSettings.ProjectSettings.ProjectFileOpened;
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

        private void OnViewportSelectedEntityChanged(Entity? entity)
        {
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
                _worldViewportPanel?.SetSelectedEntity(entity);
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

            _desktop.Update();
            ProcessPendingProjectLauncherAction();
            _editorRuntime?.UpdateHost(gameTime);
            _entitiesPanel?.Update();
            _worldViewportPanel?.UpdateInput(gameTime);

            base.Update(gameTime);

            EndUpdate?.Invoke(this, EventArgs.Empty);
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
    }
}

