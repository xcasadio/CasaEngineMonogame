using CasaEngine.Core.Log;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.ProjectLauncher;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CasaEngine.Editor
{
    public class Game1 : Game, IObservableUpdate
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // ── MGUI core ──────────────────────────────────────────────────────
        private MainRenderer _mguiRenderer;
        private MGDesktop _desktop;

        // ── Main editor window ─────────────────────────────────────────────
        private MGWindow _mainWindow;
        private MGDockHost _dockHost;
        private MGMenuBar _menuBar;

        // ── Editor panels ──────────────────────────────────────────────────
        private LoggerEditor _loggerEditor;
        private WorldViewportPanel _worldViewportPanel;

        // ── IObservableUpdate (required by GameRenderHost<Game1>) ──────────
        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
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

            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // ── Bootstrap MGUI ─────────────────────────────────────────────
            _mguiRenderer = new MainRenderer(new GameRenderHost<Game1>(this));
            _desktop = new MGDesktop(_mguiRenderer);

            // ── Register editor logger ─────────────────────────────────────
            _loggerEditor = new LoggerEditor();
            Logs.AddLogger(_loggerEditor);
            Logs.WriteInfo("CasaEngine Editor starting up");

            // ── Main window (borderless, fills the screen) ─────────────────
            _mainWindow = new MGWindow(_desktop, 0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight)
            {
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

            // ── Dock host ─────────────────────────────────────────────────
            _dockHost = new MGDockHost(_mainWindow);
            SetupInitialDockLayout();

            // ── Layout: menu bar docked at top, dock host fills rest ───────
            var rootPanel = new MGDockPanel(_mainWindow);
            rootPanel.TryAddChild(_menuBar, Dock.Top);
            rootPanel.TryAddChild(_dockHost, Dock.Top); // last child fills remaining space
            _mainWindow.SetContent(rootPanel);

            _desktop.Windows.Add(_mainWindow);

            // ── Show project launcher at startup ───────────────────────────
            var launcher = new ProjectLauncherWindow(_mainWindow);
            launcher.Show();

            base.Initialize();
        }

        private void BuildMenuBar()
        {
            // File menu
            _menuBar.AddItem("File", item =>
            {
                item.Submenu = new MGContextMenu(_mainWindow);
                item.Submenu.AddButton("New Project…", _ => OpenProjectLauncher());
                item.Submenu.AddButton("Open Project…", _ => OpenProjectLauncher());
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Save", _ => SaveCurrentProject());
                item.Submenu.AddSeparator();
                item.Submenu.AddButton("Exit", _ => Exit());
            });

            // Edit menu
            _menuBar.AddItem("Edit", item =>
            {
                item.Submenu = new MGContextMenu(_mainWindow);
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
                item.Submenu = new MGContextMenu(_mainWindow);
                item.Submenu.AddButton("Reset Layout", _ => SetupInitialDockLayout());
            });

            // Help menu
            _menuBar.AddItem("Help", item =>
            {
                item.Submenu = new MGContextMenu(_mainWindow);
                item.Submenu.AddButton("About CasaEngine", _ => { });
            });
        }

        private void SetupInitialDockLayout()
        {
            // Placeholder panels — will be replaced with real editor panels in later tasks
            var scenePanel = new DockPanelNode("panel_scene")
            {
                Title = "Scene",
                CanClose = false,
                CanFloat = false,
                ContentFactory = () =>
                {
                    _worldViewportPanel = new WorldViewportPanel(_mainWindow, GraphicsDevice);
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
            leftGroup.AddPanel(contentBrowserPanel, -1);
            leftGroup.SetActivePanel(contentBrowserPanel.Id);

            var rightGroup = new DockTabGroupNode();
            rightGroup.AddPanel(propertiesPanel, -1);
            rightGroup.SetActivePanel(propertiesPanel.Id);

            var bottomGroup = new DockTabGroupNode();
            bottomGroup.AddPanel(outputPanel, -1);
            bottomGroup.SetActivePanel(outputPanel.Id);

            var centerGroup = new DockTabGroupNode();
            centerGroup.AddPanel(scenePanel, -1);
            centerGroup.SetActivePanel(scenePanel.Id);

            // Vertical center-bottom split (80% center, 20% bottom)
            var centerBottomSplit = new DockSplitNode
            {
                Orientation = Orientation.Vertical,
                FirstChild = centerGroup,
                SecondChild = bottomGroup,
                SplitRatio = 0.8f,
                MinFirstSize = 200,
                MinSecondSize = 80
            };

            // Horizontal left | center split (20% left, 80% center-bottom)
            var leftCenterSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = leftGroup,
                SecondChild = centerBottomSplit,
                SplitRatio = 0.2f,
                MinFirstSize = 150,
                MinSecondSize = 400
            };

            // Horizontal center | right split (80% center, 20% right)
            var rootSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = leftCenterSplit,
                SecondChild = rightGroup,
                SplitRatio = 0.8f,
                MinFirstSize = 400,
                MinSecondSize = 150
            };

            _dockHost.LayoutModel.RootNode = rootSplit;
        }

        private void OpenProjectLauncher()
        {
            var launcher = new ProjectLauncherWindow(_mainWindow);
            launcher.Show();
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

            _desktop?.Update();

            base.Update(gameTime);

            EndUpdate?.Invoke(this, EventArgs.Empty);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Draw world viewport into its render target before MGUI renders
            _worldViewportPanel?.DrawViewport(gameTime);

            GraphicsDevice.Clear(Color.DimGray);

            _desktop?.Draw();

            base.Draw(gameTime);
        }
    }
}

