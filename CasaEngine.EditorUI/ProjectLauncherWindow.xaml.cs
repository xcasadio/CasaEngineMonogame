﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Project;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;

namespace CasaEngine.EditorUI;

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
            ProjectSettingsHelper.CreateProject(dialog.ProjectName, dialog.ProjectPath);
            _projects.Add(GameSettings.ProjectSettings.ProjectFileOpened);
            Logs.WriteInfo($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
            OpenEditor(GameSettings.ProjectSettings.ProjectFileOpened);
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