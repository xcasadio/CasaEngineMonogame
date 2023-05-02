using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Engine;
using EditorWpf.Controls.Animation2dControls;
using EditorWpf.Controls.SpriteControls;
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
                case Constants.FileNameExtensions.SpriteSheet:
                    var spriteControl = window.GetEditorControl<SpriteEditorControl>();
                    window.ActivateEditorControl<SpriteEditorControl>();
                    spriteControl.LoadSpriteSheet(contentItem.Path);
                    break;
                case Constants.FileNameExtensions.Animation2d:
                    var animation2dControl = window.GetEditorControl<Animation2dEditorControl>();
                    window.ActivateEditorControl<Animation2dEditorControl>();
                    animation2dControl.LoadAnimations2d(contentItem.Path);
                    break;
                case Constants.FileNameExtensions.World:
                    var worldEditorControl = window.GetEditorControl<WorldEditorControl>();
                    window.ActivateEditorControl<WorldEditorControl>();
                    worldEditorControl.LoadWorld(contentItem.Path);
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
