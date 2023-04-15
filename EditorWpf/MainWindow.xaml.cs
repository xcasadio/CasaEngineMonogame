using System;
using System.IO;
using System.Windows;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Game;
using EditorWpf.Controls;
using EditorWpf.Windows;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf
{
    public partial class MainWindow : Window
    {
        private readonly string _projectFileName;
        private bool _isWindowLoaded;
        private bool _isGameReadyToStart;

        public MainWindow(string projectFileName)
        {
            GameEditor.GameStarted += OnGameGameStarted;
            _projectFileName = projectFileName;
            Closing += OnClosing;
            Loaded += MainWindow_Loaded;

            InitializeComponent();
        }

        private void OnGameGameStarted(object? sender, EventArgs e)
        {
            _isGameReadyToStart = true;
            OpenProject(_projectFileName);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
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

            LogManager.Instance.WriteLine($"Project opened {projectFileName}");
            GameEditor.Game.GameManager.ProjectManager.Load(GameEditor.Game, projectFileName);
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
                var fileName = GetLayoutFileName();
                XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(dockingManager1);
                using var writer = new StreamWriter(fileName);
                layoutSerializer.Serialize(writer);
                LogManager.Instance.WriteLineDebug($"Save Layout '{fileName}'");

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
                var fileName = GetLayoutFileName(false);

                if (!File.Exists(fileName))
                {
                    return;
                }

                XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(dockingManager1);
                layoutSerializer.LayoutSerializationCallback += LayoutSerializer_LayoutSerializationCallback;
                using var reader = new StreamReader(fileName);
                layoutSerializer.Deserialize(reader);
                LogManager.Instance.WriteLineDebug($"Load Layout '{fileName}'");
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        private void LayoutSerializer_LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Settings" => SettingsControl,
                "Entities" => EntitiesControl,
                "Details" => EntityControl,
                "Game Screen" => GameScreenControl,
                "Place Actors" => PlaceActorsControl,
                "Logs" => LogsControl,
                "Content Browser" => ContentBrowserControl,
                _ => e.Content
            };
        }

        private static string GetLayoutFileName(bool createDirectory = true)
        {
            string dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CasaEngine");

            if (Directory.Exists(dirPath) == false && createDirectory)
            {
                Directory.CreateDirectory(dirPath);
            }

            var fileName = Path.Combine(dirPath, "layout.xml");
            return fileName;
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
                GameEditor.Game.GameManager.ProjectManager.CreateProject(GameEditor.Game, dialog.ProjectName, dialog.ProjectPath);

                LogManager.Instance.WriteLine($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
            }
        }

        private void OpenProject_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void SaveProject_OnClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
