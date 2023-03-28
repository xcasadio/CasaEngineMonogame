using System;
using System.IO;
using System.Windows;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using EditorWpf.Windows;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Closing += OnClosing;
            InitializeComponent();
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
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                LogManager.Instance.WriteLine($"Create a new project '{dialog.ProjectName}' in folder '{dialog.ProjectPath}'");

                //CREATE hiera folders
                //create default settings
                //
                EngineComponents.ProjectManager.ProjectFileOpened = Path.Combine(dialog.ProjectPath, dialog.ProjectName);

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
