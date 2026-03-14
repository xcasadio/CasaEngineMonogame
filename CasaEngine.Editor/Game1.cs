using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.ProjectLauncher;
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
using System.Diagnostics;
using System.IO;

namespace CasaEngine.Editor
{
    public class Game1 : Game, IObservableUpdate
    {
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
        private Action? _pendingProjectLauncherAction;
        private MouseState? _lastLoggedDesktopMouseState;

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
            _mguiRenderer = new MainRenderer(new GameRenderHost<Game1>(this), new Win32WindowInputSource(() => Window.Handle));
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

            HookInputDiagnostics();

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

            // Placeholder panels — will be replaced with real editor panels in later tasks
            var scenePanel = new DockPanelNode("panel_scene")
            {
                Title = "Scene",
                CanClose = false,
                CanFloat = false,
                ContentFactory = () =>
                {
                    _worldViewportPanel = new WorldViewportPanel(_mainWindow, GraphicsDevice, _editorRuntime, () => Window.Handle);
                    return _worldViewportPanel.CreateContent();
                }
            };

            var propertiesPanel = new DockPanelNode("panel_properties")
            {
                Title = "Properties",
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => new MGTextBlock(_mainWindow, "Properties (TODO)")
            };

            var explorerPanel = new DockPanelNode("panel_explorer")
            {
                Title = "World Explorer",
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => new MGTextBlock(_mainWindow, "World Explorer (TODO)")
            };

            var contentBrowserPanel = new DockPanelNode("panel_content_browser")
            {
                Title = "Content Browser",
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => new ContentBrowserPanel(_mainWindow).CreateContent()
            };

            var outputPanel = new DockPanelNode("panel_output")
            {
                Title = "Output / Logs",
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => new LogsPanel(_mainWindow, _loggerEditor).CreateContent()
            };

            // Tab groups
            var leftGroup = new DockTabGroupNode();
            leftGroup.AddPanel(explorerPanel, -1);
            leftGroup.SetActivePanel(explorerPanel.Id);

            var rightGroup = new DockTabGroupNode();
            rightGroup.AddPanel(propertiesPanel, -1);
            rightGroup.SetActivePanel(propertiesPanel.Id);

            var bottomGroup = new DockTabGroupNode();
            bottomGroup.AddPanel(contentBrowserPanel, -1);
            bottomGroup.AddPanel(outputPanel, -1);
            bottomGroup.SetActivePanel(outputPanel.Id);

            var centerGroup = new DockTabGroupNode();
            centerGroup.AddPanel(scenePanel, -1);
            centerGroup.SetActivePanel(scenePanel.Id);

            // Horizontal left | center split (20% left, 80% center)
            var leftCenterSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = leftGroup,
                SecondChild = centerGroup,
                SplitRatio = 0.2f,
                MinFirstSize = 150,
                MinSecondSize = 400
            };

            // Horizontal work area | right split (80% work area, 20% right)
            var topAreaSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = leftCenterSplit,
                SecondChild = rightGroup,
                SplitRatio = 0.8f,
                MinFirstSize = 400,
                MinSecondSize = 150
            };

            // Vertical top area | bottom tabs so the bottom panel spans the full width.
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
        }

        private void OnProjectLoaded(object? sender, EventArgs e)
        {
            SynchronizeEditorRuntimeContext();
            EnsureDockHostInitialized();
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
            _rootPanel.TryAddChild(_dockHost, Dock.Top);
            SetupInitialDockLayout();
        }

        private void OpenProjectLauncher()
        {
            var launcher = new ProjectLauncherWindow(_mainWindow, QueueProjectOpen, QueueProjectCreate);
            launcher.Show();
        }

        private void HookInputDiagnostics()
        {
            _mainWindow.MouseHandler.LMBPressedInside += (_, e) =>
                Debug.WriteLine($"[EditorMainWindow] LMBPressedInside pos={e.Position} hovered={DescribeElement(_mainWindow.HoveredElement)} pressed={DescribeElement(_mainWindow.PressedElement)} modalCount={_desktop.ActiveModalWindows.Count}");
            _mainWindow.MouseHandler.LMBReleasedInside += (_, e) =>
                Debug.WriteLine($"[EditorMainWindow] LMBReleasedInside pos={e.Position} hovered={DescribeElement(_mainWindow.HoveredElement)} pressed={DescribeElement(_mainWindow.PressedElement)} modalCount={_desktop.ActiveModalWindows.Count}");
            _mainWindow.MouseHandler.LMBClickedInside += (_, e) =>
                Debug.WriteLine($"[EditorMainWindow] LMBClickedInside pos={e.Position} double={e.IsDoubleClick} hovered={DescribeElement(_mainWindow.HoveredElement)} modalCount={_desktop.ActiveModalWindows.Count}");

            _mainWindow.HoveredElementChanged += (_, e) =>
                Debug.WriteLine($"[EditorMainWindow] HoveredElementChanged old={DescribeElement(e.PreviousValue)} new={DescribeElement(e.NewValue)}");
            _mainWindow.PressedElementChanged += (_, e) =>
                Debug.WriteLine($"[EditorMainWindow] PressedElementChanged old={DescribeElement(e.PreviousValue)} new={DescribeElement(e.NewValue)}");
        }

        private static string DescribeElement(MGElement? element)
        {
            if (element == null)
            {
                return "<null>";
            }

            var name = string.IsNullOrWhiteSpace(element.Name) ? "<unnamed>" : element.Name;
            return $"{element.GetType().Name}:{name}";
        }

        private void LogDesktopMouseState()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            if (_lastLoggedDesktopMouseState is MouseState previousState
                && previousState.LeftButton == mouseState.LeftButton
                && previousState.MiddleButton == mouseState.MiddleButton
                && previousState.RightButton == mouseState.RightButton)
            {
                return;
            }

            Debug.WriteLine(
                $"[EditorDesktop] monoMouse=({mouseState.X},{mouseState.Y}) l={mouseState.LeftButton} m={mouseState.MiddleButton} r={mouseState.RightButton} " +
                $"hovered={DescribeElement(_mainWindow?.HoveredElement)} pressed={DescribeElement(_mainWindow?.PressedElement)} modalCount={_desktop?.ActiveModalWindows.Count ?? 0}");
            _lastLoggedDesktopMouseState = mouseState;
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
            // TODO: Implement project save
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

            _desktop.Update();
            _editorRuntime?.UpdateHost(gameTime);
            _worldViewportPanel?.UpdateInput(gameTime);
            ProcessPendingProjectLauncherAction();
            _editorRuntime?.UpdateHost(gameTime);

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

