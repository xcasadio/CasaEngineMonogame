using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Project;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

namespace CasaEngine.SimpleEditor
{
    public class Game1 : Game, IObservableUpdate
    {
        private readonly string _projectFilePath;
        private readonly GraphicsDeviceManager _graphics;

        private MainRenderer _mguiRenderer = null!;
        private MGDesktop _desktop = null!;
        private FontStashSharpTextEngine _fontTextEngine = null!;
        private MGWindow _mainWindow = null!;
        private MGDockPanel _rootPanel = null!;

        private EngineRuntimeContext _runtimeContext = null!;
        private HostedEditorGameAdapter _editorRuntime = null!;
        private WorldViewportPanel _worldViewportPanel = null!;
        private Win32WindowInputSource _windowInputSource = null!;

        public event EventHandler<TimeSpan>? PreviewUpdate;
        public event EventHandler<EventArgs>? EndUpdate;

        public Game1()
        {
            _projectFilePath = ResolveProjectFilePath();

            _graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef)
                    ? GraphicsProfile.HiDef
                    : GraphicsProfile.Reach,
            };

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "CasaEngine Simple Editor";
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1600;
            _graphics.PreferredBackBufferHeight = 900;
            _graphics.ApplyChanges();

            Window.Title = $"CasaEngine Simple Editor - {Path.GetFileNameWithoutExtension(_projectFilePath)}";
            Window.ClientSizeChanged += OnClientSizeChanged;

            InitializeMgui();
            InitializeEditorRuntime();
            QueueInitialWorldLoad();
            InitializeViewportWindow();

            base.Initialize();
        }

        protected override void LoadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

            _desktop.Update();
            _editorRuntime?.UpdateHost(gameTime);
            _worldViewportPanel?.UpdateInput(gameTime);

            base.Update(gameTime);

            EndUpdate?.Invoke(this, EventArgs.Empty);
        }

        protected override void Draw(GameTime gameTime)
        {
            _editorRuntime?.DrawHost(gameTime);
            _worldViewportPanel?.DrawViewport(gameTime);

            GraphicsDevice.Clear(Color.DimGray);
            _desktop.Draw();

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _worldViewportPanel?.Dispose();
                _editorRuntime?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeMgui()
        {
            _windowInputSource = new Win32WindowInputSource(() => Window.Handle);
            _mguiRenderer = new MainRenderer(new GameRenderHost<Game1>(this), _windowInputSource);
            _desktop = new MGDesktop(_mguiRenderer);
            _desktop.LoadDefaultResources();

            _fontTextEngine = new FontStashSharpTextEngine();
            const string familyName = "JetBrainsMono";
            string ttfDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"Content\fonts\JetBrainsMono"));

            byte[] regularBytes = File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-Regular.ttf"));
            var regular = new FontSystem();
            regular.AddFont(regularBytes);
            _fontTextEngine.AddFontSystem(familyName, CustomFontStyles.Normal, regular, regularBytes);

            var bold = new FontSystem();
            bold.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-Bold.ttf")));
            _fontTextEngine.AddFontSystem(familyName, CustomFontStyles.Bold, bold);

            var italic = new FontSystem();
            italic.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "JetBrainsMono-BoldItalic.ttf")));
            _fontTextEngine.AddFontSystem(familyName, CustomFontStyles.Italic, italic);

            _fontTextEngine.MatchSpriteFontSizing(_desktop.FontManager);
            _desktop.TextEngine = _fontTextEngine;
        }

        private void InitializeEditorRuntime()
        {
            var graphicsDeviceService = Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
            if (graphicsDeviceService == null)
            {
                throw new InvalidOperationException("The simple editor could not resolve the shared graphics device service.");
            }

            _runtimeContext = GameSettings.CreateRuntimeContext();
            _editorRuntime = new HostedEditorGameAdapter(_projectFilePath, graphicsDeviceService, _runtimeContext)
            {
                ExecutionPolicy = GameplayExecutionPolicies.EditorPreview,
            };

            _editorRuntime.InitializeHost();
            _editorRuntime.LoadContentHost();
        }

        private void QueueInitialWorldLoad()
        {
            if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
            {
                _editorRuntime.GameManager.SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);
            }
        }

        private void InitializeViewportWindow()
        {
            _mainWindow = new MGWindow(_desktop, 0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height)
            {
                WindowStyle = WindowStyle.None,
                BackgroundBrush = _desktop.Theme.GetBackgroundBrush(MGElementType.Window),
                TitleText = string.Empty,
                IsTitleBarVisible = false,
                IsCloseButtonVisible = false,
                IsDraggable = false,
                IsUserResizable = false,
            };

            _rootPanel = new MGDockPanel(_mainWindow)
            {
                Name = "SimpleEditorRootPanel",
            };

            _worldViewportPanel = new WorldViewportPanel(_mainWindow, GraphicsDevice, _editorRuntime, _windowInputSource);
            _rootPanel.TryAddChild(_worldViewportPanel.CreateContent(), Dock.Top);

            _mainWindow.SetContent(_rootPanel);
            _desktop.Windows.Add(_mainWindow);
        }

        private void OnClientSizeChanged(object? sender, EventArgs e)
        {
            if (_mainWindow == null)
            {
                return;
            }

            _mainWindow.Left = 0;
            _mainWindow.Top = 0;
            _mainWindow.WindowWidth = Window.ClientBounds.Width;
            _mainWindow.WindowHeight = Window.ClientBounds.Height;
        }

        private static string ResolveProjectFilePath()
        {
            var projectFilePath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Projects\SampleProject\SampleProject.json"));

            if (!File.Exists(projectFilePath))
            {
                throw new FileNotFoundException("The sample project file could not be found.", projectFilePath);
            }

            return projectFilePath;
        }
    }
}

