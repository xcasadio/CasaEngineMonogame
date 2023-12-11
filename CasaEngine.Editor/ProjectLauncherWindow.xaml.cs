using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CasaEngine.Core.Logger;
using CasaEngine.Editor.Windows;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using CasaEngine.Engine;
using Newtonsoft.Json;
using CasaEngine.Framework.Game;
using MessageBox = System.Windows.MessageBox;

namespace CasaEngine.Editor;

public partial class ProjectLauncherWindow : Window
{
    private ObservableCollection<string> _projects = new();
    private const string MostRecentProjectsFileName = "mostRecentProjects.json";

    public ProjectLauncherWindow()
    {
        InitializeComponent();

        LoadMostRecentProjects();
    }

    private void LoadMostRecentProjects()
    {
        listBoxRecentProjects.ItemsSource = _projects;

        if (!File.Exists(MostRecentProjectsFileName))
        {
            return;
        }

        JsonSerializer serializer = new JsonSerializer();
        using StreamReader sw = new StreamReader(MostRecentProjectsFileName);
        using JsonReader reader = new JsonTextReader(sw);
        foreach (var file in serializer.Deserialize<List<string>>(reader))
        {
            _projects.Add(file);
        }
        listBoxRecentProjects.ItemsSource = _projects;
    }


    private void SaveMostRecentProjects()
    {
        var projects = new HashSet<string>();

        foreach (string projectFileName in _projects)
        {
            projects.Add(projectFileName);
        }

        JsonSerializer serializer = new JsonSerializer();
        using StreamWriter sw = new StreamWriter(MostRecentProjectsFileName);
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
            //create default settings
            GameSettings.ProjectSettings.CreateProject(dialog.ProjectName, dialog.ProjectPath);
            var projectFileName = Path.Combine(dialog.ProjectPath, dialog.ProjectName + Constants.FileNameExtensions.Project);
            _projects.Add(projectFileName);
            LogManager.Instance.WriteLineInfo($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
            OpenEditor(projectFileName);
        }
    }
    private void ButtonOpenProject_Click(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog();
        fileDialog.Title = "Open a project";
        fileDialog.CheckFileExists = true;
        fileDialog.Filter = "json files|*.json";

        if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _projects.Add(fileDialog.FileName);
            SaveMostRecentProjects();
            OpenEditor(fileDialog.FileName);
        }
    }

    private void ButtonLaunchEditor_Click(object sender, RoutedEventArgs e)
    {
        if (listBoxRecentProjects.SelectedItem is not string fileName)
        {
            return;
        }

        OpenEditor(fileName);
    }

    private void OpenEditor(string projectFileName)
    {
        if (!File.Exists(projectFileName))
        {
            if (MessageBox.Show(this,
                    $"The project file {projectFileName} doesn't exist. Remove it from the project list ?", "Error",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _projects.Remove(projectFileName);
                SaveMostRecentProjects();
            }

            return;
        }

        SaveMostRecentProjects();
        var mainWindow = new MainWindow(projectFileName);
        mainWindow.Show();
        Close();
    }
}