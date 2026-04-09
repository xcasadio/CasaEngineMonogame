using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CasaEngine.Framework.Common;
using CasaEngine.Framework.Configuration;
using CasaEngine.EditorServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Newtonsoft.Json;
using Thickness = MonoGame.Extended.Thickness;
using Orientation = MGUI.Core.UI.Orientation;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.ProjectLauncher;

/// <summary>
/// MGUI-based project launcher that lists recent projects and allows creating or
/// opening a project at application startup.
/// </summary>
public class ProjectLauncherWindow
{
    private const string RecentProjectsFile = "mostRecentProjects.json";

    private readonly MGWindow _parentWindow;
    private readonly Action<string> _requestOpenProject;
    private readonly Action<string, string> _requestCreateProject;
    private MGWindow _launcherWindow;
    private MGListBox<string> _recentList;
    private List<string> _recentProjects;

    public ProjectLauncherWindow(
        MGWindow parentWindow,
        Action<string> requestOpenProject,
        Action<string, string> requestCreateProject)
    {
        _parentWindow = parentWindow;
        _requestOpenProject = requestOpenProject;
        _requestCreateProject = requestCreateProject;
    }

    public void Show()
    {
        _recentProjects = LoadRecentProjects();

        var desktop = _parentWindow.Desktop;
        var screenBounds = desktop.ValidScreenBounds;

        const int Width = 700;
        const int Height = 480;
        int left = screenBounds.Left + (screenBounds.Width - Width) / 2;
        int top = screenBounds.Top + (screenBounds.Height - Height) / 2;

        _launcherWindow = new MGWindow(_parentWindow, left, top, Width, Height)
        {
            TitleText = "Open a project",
            IsCloseButtonVisible = true
        };
        _parentWindow.PushModalWindow(_launcherWindow);

        var outerStack = new MGStackPanel(_launcherWindow, Orientation.Vertical)
        {
            Spacing = 8
        };
        outerStack.Margin = new Thickness(12);

        outerStack.TryAddChild(new MGTextBlock(_launcherWindow, "[b]Recent Projects[/b]"));

        _recentList = new MGListBox<string>(_launcherWindow);
        _recentList.SetItemsSource(_recentProjects);
        _recentList.ItemTemplate = path => new MGTextBlock(_launcherWindow, Path.GetFileName(path) + "\n[i]" + path + "[/i]")
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        _recentList.PreferredHeight = 280;
        outerStack.TryAddChild(_recentList);

        var buttonRow = new MGStackPanel(_launcherWindow, Orientation.Horizontal)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var openButton = new MGButton(_launcherWindow, _ => OpenSelectedProject());
        openButton.SetContent(new MGTextBlock(_launcherWindow, "Open Selected"));
        openButton.PreferredWidth = 120;
        buttonRow.TryAddChild(openButton);

        var browseButton = new MGButton(_launcherWindow, _ => BrowseForProject());
        browseButton.SetContent(new MGTextBlock(_launcherWindow, "Browse"));
        browseButton.PreferredWidth = 90;
        buttonRow.TryAddChild(browseButton);

        var newButton = new MGButton(_launcherWindow, _ => ShowNewProjectForm(outerStack, buttonRow));
        newButton.SetContent(new MGTextBlock(_launcherWindow, "New Project"));
        newButton.PreferredWidth = 110;
        buttonRow.TryAddChild(newButton);

        outerStack.TryAddChild(buttonRow);
        _launcherWindow.SetContent(outerStack);

        _recentList.MouseHandler.LMBClickedInside += (_, e) =>
        {
            if (e.IsDoubleClick)
            {
                OpenSelectedProject();
            }
        };
    }

    private void OpenSelectedProject()
    {
        var selected = _recentList.SelectedValue;
        if (selected == null)
        {
            return;
        }

        TryOpenProject(selected);
    }

    private void BrowseForProject()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open a CasaEngine Project",
            Filter = "CasaEngine project files (*.json)|*.json",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            AddToRecent(dialog.FileName);
            TryOpenProject(dialog.FileName);
        }
    }

    private void TryOpenProject(string fileName)
    {
        if (!File.Exists(fileName))
        {
            MessageBox.Show(
                $"Project file not found:\n{fileName}\n\nIt will be removed from the recent list.",
                "File Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            _recentProjects.Remove(fileName);
            _recentList.SetItemsSource(_recentProjects.ToList());
            SaveRecentProjects();
            return;
        }

        AddToRecent(fileName);
        SaveRecentProjects();

        _launcherWindow.TryCloseWindow();
        _requestOpenProject(fileName);
    }

    private void ShowNewProjectForm(MGStackPanel outerStack, MGStackPanel buttonRow)
    {
        var formStack = new MGStackPanel(_launcherWindow, Orientation.Vertical) { Spacing = 6 };

        formStack.TryAddChild(new MGTextBlock(_launcherWindow, "[b]New Project[/b]"));

        var nameRow = new MGStackPanel(_launcherWindow, Orientation.Horizontal) { Spacing = 6 };
        nameRow.TryAddChild(new MGTextBlock(_launcherWindow, "Name:") { VerticalAlignment = VerticalAlignment.Center, PreferredWidth = 80 });
        var nameBox = new MGTextBox(_launcherWindow);
        nameBox.SetText("MyProject");
        nameBox.PreferredWidth = 260;
        nameRow.TryAddChild(nameBox);
        formStack.TryAddChild(nameRow);

        var pathRow = new MGStackPanel(_launcherWindow, Orientation.Horizontal) { Spacing = 6 };
        pathRow.TryAddChild(new MGTextBlock(_launcherWindow, "Path:") { VerticalAlignment = VerticalAlignment.Center, PreferredWidth = 80 });
        var pathBox = new MGTextBox(_launcherWindow);
        pathBox.SetText(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        pathBox.PreferredWidth = 200;
        pathRow.TryAddChild(pathBox);
        var folderButton = new MGButton(_launcherWindow, _ =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Select project folder" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                pathBox.SetText(dialog.SelectedPath);
            }
        });
        folderButton.SetContent(new MGTextBlock(_launcherWindow, "…"));
        folderButton.PreferredWidth = 28;
        pathRow.TryAddChild(folderButton);
        formStack.TryAddChild(pathRow);

        var createRow = new MGStackPanel(_launcherWindow, Orientation.Horizontal) { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

        var cancelBtn = new MGButton(_launcherWindow, _ =>
        {
            outerStack.TryRemoveChild(formStack);
        });
        cancelBtn.SetContent(new MGTextBlock(_launcherWindow, "Cancel"));
        cancelBtn.PreferredWidth = 80;
        createRow.TryAddChild(cancelBtn);

        var createBtn = new MGButton(_launcherWindow, _ =>
        {
            var name = nameBox.Text?.Trim();
            var path = pathBox.Text?.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Please enter a project name and path.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot create directory:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            var projectFile = Path.Combine(path, name + Constants.FileNameExtensions.Project);
            AddToRecent(projectFile);
            SaveRecentProjects();
            _launcherWindow.TryCloseWindow();
            _requestCreateProject(name, path);
        });
        createBtn.SetContent(new MGTextBlock(_launcherWindow, "Create"));
        createBtn.PreferredWidth = 80;
        createRow.TryAddChild(createBtn);

        formStack.TryAddChild(createRow);
        outerStack.TryAddChild(formStack);
    }

    private List<string> LoadRecentProjects()
    {
        if (!File.Exists(RecentProjectsFile))
        {
            return new List<string>();
        }

        try
        {
            var json = File.ReadAllText(RecentProjectsFile);
            return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void SaveRecentProjects()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_recentProjects.Distinct().ToList(), Formatting.Indented);
            File.WriteAllText(RecentProjectsFile, json);
        }
        catch
        {
        }
    }

    private void AddToRecent(string fileName)
    {
        _recentProjects.Remove(fileName);
        _recentProjects.Insert(0, fileName);
        _recentList.SetItemsSource(_recentProjects.ToList());
    }
}
