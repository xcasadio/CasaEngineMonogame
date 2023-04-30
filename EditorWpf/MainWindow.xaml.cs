using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Game;
using EditorWpf.Controls;
using EditorWpf.Controls.ContentBrowser;
using EditorWpf.Windows;

namespace EditorWpf
{
    public partial class MainWindow : Window
    {
        private readonly string _projectFileName;
        private bool _isWindowLoaded;
        private bool _isGameReadyToStart;
        public ContentBrowserControl ContentBrowserControl { get; }
        public LogsControl LogsControl { get; }

        public MainWindow(string projectFileName)
        {
            _projectFileName = projectFileName;
            Closing += OnClosing;
            Loaded += MainWindow_Loaded;

            ContentBrowserControl = new ContentBrowserControl();
            ContentBrowserControl.InitializeComponent();
            LogsControl = new LogsControl();
            LogsControl.InitializeComponent();

            InitializeComponent();

            WorldEditorControl.GameStarted += OnGameStarted;

            ContentBrowserControl.InitializeFromGameEditor(WorldEditorControl.GameEditor);
        }

        private void OnGameStarted(object? sender, EventArgs e)
        {
            _isGameReadyToStart = true;
            OpenProject(_projectFileName);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWorkSpace();

            _isWindowLoaded = true;
            OpenProject(_projectFileName);
        }

        private void OpenProject(string projectFileName)
        {
            if (!_isWindowLoaded || !_isGameReadyToStart)
            {
                //MessageBox.Show(this, "Open project only after the window is loaded and the game is initialized");
                return;
            }

            if (!File.Exists(projectFileName))
            {
                LogManager.Instance.WriteLineError($"Can't open project {projectFileName}");
                return;
            }

            LogManager.Instance.WriteLineInfo($"Project opened {projectFileName}");

            GameSettings.ProjectManager.Load(projectFileName);
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWorkSpace();
        }

        #region Layout

        private void ButtonSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            SaveWorkSpace();
        }

        private void ButtonLoadLayout_Click(object sender, RoutedEventArgs e)
        {
            LoadWorkSpace();
        }

        private void SaveWorkSpace()
        {
            try
            {
                string path = GetLayoutPath();

                foreach (var tab in tabControl.Items)
                {
                    if (tab is TabItem { Content: IEditorControl editorControl })
                    {
                        editorControl.SaveLayout(path);
                    }
                }

            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        private void LoadWorkSpace()
        {
            try
            {
                string path = GetLayoutPath(false);

                foreach (var tab in tabControl.Items)
                {
                    if (tab is TabItem { Content: IEditorControl editorControl })
                    {
                        editorControl.LoadLayout(path);
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        private static string GetLayoutPath(bool createDirectory = true)
        {
            string dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CasaEngine");

            if (Directory.Exists(dirPath) == false && createDirectory)
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }

        #endregion

        private void NewProject_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectWindow();

            if (dialog.ShowDialog() == true)
            {
                //CREATE hiera folders
                //create default settings
                //
                GameSettings.ProjectManager.CreateProject(dialog.ProjectName, dialog.ProjectPath);

                LogManager.Instance.WriteLineInfo($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
            }
        }

        private void OpenProject_OnClick(object sender, RoutedEventArgs e)
        {
            //select project

            //OpenProject(string projectFileName)
        }

        private void SaveProject_OnClick(object sender, RoutedEventArgs e)
        {
            // use full ??
        }

        private void ButtonOpenContentBrowser_OnClick(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is TabItem { Content: IEditorControl editorControl })
            {
                editorControl.ShowControl(ContentBrowserControl, "Content Browser");
            }
        }

        private void ButtonOpenLog_OnClick(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is TabItem { Content: IEditorControl editorControl })
            {
                editorControl.ShowControl(LogsControl, "Logs");
            }
        }
    }
}
