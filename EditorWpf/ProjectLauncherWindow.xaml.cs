using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logger;
using EditorWpf.Windows;
using System.Windows;
using System.Windows.Input;
using CasaEngine.Engine;
using Newtonsoft.Json;
using CasaEngine.Framework.Game;

namespace EditorWpf
{
    public partial class ProjectLauncherWindow : Window
    {
        public ProjectLauncherWindow()
        {
            InitializeComponent();

            LoadMostRecentProjects();
        }

        private void LoadMostRecentProjects()
        {
            listBoxRecentProjects.ItemsSource = new List<string>();

            if (!File.Exists(Constants.FileNames.MostRecentProjectsFileName))
            {
                return;
            }

            JsonSerializer serializer = new JsonSerializer();
            using StreamReader sw = new StreamReader(Constants.FileNames.MostRecentProjectsFileName);
            using JsonReader reader = new JsonTextReader(sw);
            var projectList = serializer.Deserialize<List<string>>(reader);
            listBoxRecentProjects.ItemsSource = projectList;
        }


        private void SaveMostRecentProjects(string projectOpenedFileName)
        {
            var list = listBoxRecentProjects.ItemsSource;

            var projects = new HashSet<string>();
            projects.Add(projectOpenedFileName);

            foreach (string projectFileName in listBoxRecentProjects.ItemsSource)
            {
                projects.Add(projectFileName);
            }

            JsonSerializer serializer = new JsonSerializer();
            using StreamWriter sw = new StreamWriter(Constants.FileNames.MostRecentProjectsFileName);
            using JsonWriter writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
            serializer.Serialize(writer, projects);
        }

        private void ListBoxOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBoxRecentProjects.SelectedItem is not string fileName)
            {
                return;
            }

            OpenEditor(fileName);
        }

        private void ButtonCreateProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectWindow();

            if (dialog.ShowDialog() == true)
            {
                //CREATE hiera folders
                //create default settings
                //
                GameSettings.ProjectManager.CreateProject(dialog.ProjectName, dialog.ProjectPath);
                LogManager.Instance.WriteLine($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
                OpenEditor(Path.Combine(dialog.ProjectPath, dialog.ProjectName));
            }
        }

        private void ButtonLaunchEditor_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxRecentProjects.SelectedItem is not string fileName)
            {
                return;
            }

            if (!File.Exists(fileName))
            {
                return;
            }

            OpenEditor(fileName);
        }

        private void OpenEditor(string projectFileName)
        {
            SaveMostRecentProjects(projectFileName);
            var mainWindow = new MainWindow(projectFileName);
            mainWindow.Show();
            Close();
        }
    }
}
