using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.ContentBrowser
{
    public partial class ContentBrowserControl : UserControl
    {

        public ContentBrowserControl()
        {
            InitializeComponent();
        }

        public void InitializeFromGameEditor(GameEditorWorld gameEditorWorld)
        {
            var contentBrowserViewModel = DataContext as ContentBrowserViewModel;
            contentBrowserViewModel.Initialize(gameEditorWorld);
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
            //TODO
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
