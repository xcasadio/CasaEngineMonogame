using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Engine;
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
        public ContentBrowserControl()
        {
            InitializeComponent();
        }

        public void InitializeFromGameEditor(GameEditor gameEditor)
        {
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
            var extension = System.IO.Path.GetExtension(contentItem.Name);
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
                    var tiledMapEditorControl = window.GetEditorControl<TileMapEditorControl>();
                    window.ActivateEditorControl<TileMapEditorControl>();
                    tiledMapEditorControl.OpenMap(contentItem.FullPath);
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
    }
}
