using Genbox.VelcroPhysics.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Xceed.Wpf.AvalonDock.Layout;
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
                "Game Screen" => e.Content, // TODO
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
    }
}
