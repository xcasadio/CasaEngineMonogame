using System;
using System.IO;
using System.Windows;
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
                using var reader = new StreamReader(fileName);
                layoutSerializer.Deserialize(reader);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
    }
}
