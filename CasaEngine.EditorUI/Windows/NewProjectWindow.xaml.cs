using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace CasaEngine.Editor.Windows;

public partial class NewProjectWindow : Window
{
    public static readonly DependencyProperty ProjectNameProperty = DependencyProperty.Register(nameof(ProjectName), typeof(string), typeof(NewProjectWindow));
    public static readonly DependencyProperty ProjectPathProperty = DependencyProperty.Register(nameof(ProjectPath), typeof(string), typeof(NewProjectWindow));

    public string ProjectName
    {
        get => (string)GetValue(ProjectNameProperty);
        set => SetValue(ProjectNameProperty, value);
    }
    public string ProjectPath
    {
        get => (string)GetValue(ProjectPathProperty);
        set => SetValue(ProjectPathProperty, value);
    }

    public NewProjectWindow()
    {
        InitializeComponent();
    }

    private void ButtonSetProjectFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FolderBrowserDialog();
        dialog.AutoUpgradeEnabled = true;
        //dialog.RootFolder = Environment.SpecialFolder.ApplicationData;
        //dialog.InitialDirectory = "true";

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectPath = dialog.SelectedPath;
        }
    }

    private void ButtonOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ProjectName))
        {
            MessageBox.Show("The name of the project cannot be empty.");
            return;
        }

        if (string.IsNullOrEmpty(ProjectPath))
        {
            MessageBox.Show("The name of the project cannot be empty.");
            return;
        }

        if (!Directory.Exists(ProjectPath))
        {
            MessageBox.Show("The path of the project doesn't exist.");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}