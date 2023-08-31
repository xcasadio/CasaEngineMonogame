using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using EditorWpf.Controls.Animation2dControls;
using EditorWpf.Controls.EntityControls;
using EditorWpf.Controls.SpriteControls;
using EditorWpf.Controls.TileMapControls;
using EditorWpf.Controls.WorldControls;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.ContentBrowser
{
    public partial class ContentBrowserControl : UserControl
    {
        private GameEditor _gameEditor;

        public AssetInfo? SelectedItem
        {
            get
            {
                if (ListBoxFolderContent.SelectedItem is not FolderItem &&
                    ListBoxFolderContent.SelectedItem is ContentItem contentItem)
                {
                    return contentItem.AssetInfo;
                }

                return null;
            }
        }

        public ContentBrowserControl()
        {
            InitializeComponent();
        }

        public void InitializeFromGameEditor(GameEditor gameEditor)
        {
            _gameEditor = gameEditor;
            var contentBrowserViewModel = DataContext as ContentBrowserViewModel;
            contentBrowserViewModel.Initialize(gameEditor);
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement frameworkElement)
            {
                return;
            }

            switch (frameworkElement.DataContext)
            {
                case FolderItem folderItem:
                    SelectTreeViewItem(folderItem);
                    break;
                case ContentItem contentItem:
                    TryToOpenFile(contentItem);
                    break;
            }
        }

        private void TryToOpenFile(ContentItem contentItem)
        {
            var extension = Path.GetExtension(contentItem.AssetInfo.FileName);
            var window = this.FindParent<MainWindow>();

            if (window == null)
            {
                return;
            }

            switch (extension)
            {
                case Constants.FileNameExtensions.Entity:
                    var entityControl = window.GetEditorControl<EntityEditorControl>();
                    window.ActivateEditorControl<EntityEditorControl>();
                    entityControl.LoadEntity(contentItem.FullPath);
                    break;
                case Constants.FileNameExtensions.Sprite:
                    var spriteControl = window.GetEditorControl<SpriteEditorControl>();
                    window.ActivateEditorControl<SpriteEditorControl>();
                    spriteControl.OpenSprite(contentItem.FullPath);
                    break;
                case Constants.FileNameExtensions.Animation2d:
                    var animation2dControl = window.GetEditorControl<Animation2dEditorControl>();
                    window.ActivateEditorControl<Animation2dEditorControl>();
                    animation2dControl.OpenAnimations2d(contentItem.FullPath);
                    break;
                case Constants.FileNameExtensions.TileMap:
                    var tileMapEditorControl = window.GetEditorControl<TileMapEditorControl>();
                    window.ActivateEditorControl<TileMapEditorControl>();
                    tileMapEditorControl.OpenMap(contentItem.FullPath);
                    break;
                case Constants.FileNameExtensions.World:
                    var worldEditorControl = window.GetEditorControl<WorldEditorControl>();
                    window.ActivateEditorControl<WorldEditorControl>();
                    worldEditorControl.LoadWorld(contentItem.FullPath);
                    break;
            }
        }

        private void SelectTreeViewItem(FolderItem folderItem)
        {
            Stack<FolderItem> parents = new();
            parents.Push(folderItem);
            var root = folderItem;

            while (root != null)
            {
                parents.Push(root);
                root = root.Parent;
            }

            ItemsControl itemsControl = treeViewFolders;

            while (parents.TryPop(out var folder))
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(folder) is TreeViewItem treeViewItem)
                {
                    itemsControl = treeViewItem;
                    treeViewItem.IsExpanded = true;
                    treeViewItem.IsSelected = true;
                    treeViewItem.UpdateLayout();
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            //if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            //{
            //    var data = e.Data.GetData(DataFormats.UnicodeText) as byte[];
            //    Debug.WriteLine(data);
            //}
            //if (e.Data.GetDataPresent(DataFormats.Bitmap))
            //{
            //    var data = e.Data.GetData(DataFormats.Bitmap);
            //    Debug.WriteLine(data);
            //}
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var fileNames = e.Data.GetData(DataFormats.FileDrop) as string[];

                foreach (var fileName in fileNames)
                {
                    if (_gameEditor.Game.GameManager.AssetContentManager.IsFileSupported(fileName))
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                }
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var fileNames = e.Data.GetData(DataFormats.FileDrop) as string[];
                var folderItem = treeViewFolders.SelectedItem as FolderItem;
                var folderPath = folderItem.FullPath;

                foreach (var fileName in fileNames)
                {
                    if (_gameEditor.Game.GameManager.AssetContentManager.IsFileSupported(fileName))
                    {
                        var destFileName = Path.Combine(folderPath, Path.GetFileName(fileName));
                        if (!File.Exists(destFileName))
                        {
                            File.Copy(fileName, destFileName);
                            LogManager.Instance.WriteLineTrace($"Copy {fileName} -> {destFileName}");

                            var assetInfo = new AssetInfo();
                            assetInfo.FileName = destFileName
                                .Replace(EngineEnvironment.ProjectPath, string.Empty)
                                .TrimStart(Path.DirectorySeparatorChar);
                            assetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);
                            GameSettings.AssetInfoManager.AddAndSave(assetInfo);
                        }
                    }
                }
            }
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            GameSettings.AssetInfoManager.Save();
        }

        private void ListBoxFolderContentCreate_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ListBoxFolderContentDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxFolderContent.SelectedItem == null)
            {
                return;
            }

            var contentItem = ListBoxFolderContent.SelectedItem as ContentItem;

            GameSettings.AssetInfoManager.Remove(contentItem.AssetInfo.Id);
        }

        private void ListBoxFolderContent_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {

        }
    }
}
