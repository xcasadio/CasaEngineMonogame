using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls;
using CasaEngine.EditorUI.Controls.ContentBrowser;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Framework.Project;
using FlowGraph;
using TabItem = System.Windows.Controls.TabItem;

namespace CasaEngine.EditorUI;

public partial class MainWindow : Window
{
    private readonly string _projectFileName;
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

        OpenProject(_projectFileName);
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadWorkSpace();
    }

    private void OpenProject(string projectFileName)
    {
        if (!File.Exists(projectFileName))
        {
            Logs.WriteError($"Can't open project {projectFileName}");
            return;
        }

        Logs.WriteInfo($"Project opened {projectFileName}");

        ProjectSettingsHelper.Load(projectFileName);
        RegisterFlowGraphNodes();
    }

    private static void RegisterFlowGraphNodes()
    {
        NodeRegister.Clear();

        var dllFileName = Path.Combine(Environment.CurrentDirectory, "FlowGraph.dll");
        NodeRegister.Register(Assembly.LoadFrom(dllFileName));

        //TODO : register nodes from plugin
        dllFileName = Path.Combine(Environment.CurrentDirectory, "CasaEngine.FlowGraphNodes.dll");
        NodeRegister.Register(Assembly.LoadFile(dllFileName));
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
            Logs.WriteException(e);
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
            Logs.WriteException(e);
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
            ProjectSettingsHelper.CreateProject(dialog.ProjectName, dialog.ProjectPath);
            Logs.WriteInfo($"New project {dialog.ProjectName} created in {dialog.ProjectPath}");
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

    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
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

    public T? GetEditorControl<T>() where T : class
    {
        foreach (TabItem tabItem in tabControl.Items)
        {
            if (tabItem.Content is T control)
            {
                return control;
            }
        }

        return null;
    }

    public void ActivateEditorControl<T>() where T : class
    {
        foreach (TabItem tabItem in tabControl.Items)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (tabItem.Content is T control)
                {
                    tabControl.SelectedItem = tabItem;
                    tabItem.IsSelected = true;
                }
                else
                {
                    tabItem.IsSelected = false;
                }
            });
        }
    }

    private void MenuItemWindows_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        MenuItemHiddenWindows.Items.Clear();

        if (tabControl.SelectedItem is TabItem tabItem)
        {
            if (tabItem.Content is EditorControlBase editorControlBase)
            {
                foreach (var layoutAnchorable in editorControlBase.DockingManager.Layout.Hidden)
                {
                    var menuItem = new MenuItem
                    {
                        Header = layoutAnchorable.Title,
                        IsCheckable = true,
                        IsChecked = false,
                        DataContext = layoutAnchorable
                    };
                    menuItem.Checked += (o, args) =>
                    {
                        menuItem.IsChecked = true;
                        layoutAnchorable.IsVisible = true;
                    };
                    MenuItemHiddenWindows.Items.Add(menuItem);
                }
            }
        }
    }
}