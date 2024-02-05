using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.Animation2dControls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Controls.EntityControls;
using CasaEngine.EditorUI.Controls.GuiEditorControls;
using CasaEngine.EditorUI.Controls.SpriteControls;
using CasaEngine.EditorUI.Controls.TileMapControls;
using CasaEngine.EditorUI.Controls.WorldControls;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework;
using Point = System.Windows.Point;

namespace CasaEngine.EditorUI.Controls.ContentBrowser;

public partial class ContentBrowserControl : UserControl
{
    private GameEditor _gameEditor;
    private object? _dragAndDropData;

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

        AssetCatalog.AssetRenamed += OnAssetRenamed;
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

        var fileRelativePath = contentItem.Path + contentItem.FileExtension;

        switch (extension)
        {
            case Constants.FileNameExtensions.Entity:
                var entityControl = window.GetEditorControl<EntityEditorControl>();
                window.ActivateEditorControl<EntityEditorControl>();
                entityControl.OpenEntity(fileRelativePath);
                break;
            /*case Constants.FileNameExtensions.EntityFlowGraph:
                var entityControlFlowGraph = window.GetEditorControl<EntityEditorControl>();
                window.ActivateEditorControl<EntityEditorControl>();
                entityControlFlowGraph.OpenEntity(fileRelativePath);
                break;*/
            case Constants.FileNameExtensions.Sprite:
                var spriteControl = window.GetEditorControl<SpriteEditorControl>();
                window.ActivateEditorControl<SpriteEditorControl>();
                spriteControl.OpenSprite(fileRelativePath);
                break;
            case Constants.FileNameExtensions.Animation2d:
                var animation2dControl = window.GetEditorControl<Animation2dEditorControl>();
                window.ActivateEditorControl<Animation2dEditorControl>();
                animation2dControl.OpenAnimations2d(fileRelativePath);
                break;
            case Constants.FileNameExtensions.TileMap:
                var tileMapEditorControl = window.GetEditorControl<TileMapEditorControl>();
                window.ActivateEditorControl<TileMapEditorControl>();
                tileMapEditorControl.OpenMap(fileRelativePath);
                break;
            case Constants.FileNameExtensions.World:
                var worldEditorControl = window.GetEditorControl<WorldEditorControl>();
                window.ActivateEditorControl<WorldEditorControl>();
                worldEditorControl.OpenWorld(fileRelativePath);
                break;
            case Constants.FileNameExtensions.Screen:
                var guiEditorControl = window.GetEditorControl<GuiEditorControl>();
                window.ActivateEditorControl<GuiEditorControl>();
                guiEditorControl.OpenScreen(fileRelativePath);
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
                if (_gameEditor.Game.AssetContentManager.IsFileSupported(fileName))
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
                if (_gameEditor.Game.AssetContentManager.IsFileSupported(fileName))
                {
                    if (Texture2DLoader.IsTextureFile(fileName))
                    {
                        var destFileName = Path.Combine(EngineEnvironment.ProjectPath, folderPath, Path.GetFileName(fileName));
                        if (!File.Exists(destFileName))
                        {
                            //copy file
                            File.Copy(fileName, destFileName, true);
                            Logs.WriteTrace($"Copy {fileName} -> {destFileName}");

                            //create image file assetinfo
                            var imageFileAssetInfo = new AssetInfo();
                            imageFileAssetInfo.FileName = destFileName
                                .Replace(EngineEnvironment.ProjectPath, string.Empty)
                                .TrimStart(Path.DirectorySeparatorChar);
                            imageFileAssetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);
                            AssetCatalog.Add(imageFileAssetInfo);

                            //Create texture asset
                            var texture = new Texture(imageFileAssetInfo.Id, _gameEditor.Game.GraphicsDevice);
                            var textureAssetInfo = new AssetInfo(texture.Id);
                            textureAssetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);
                            var pathFileName = Path.GetDirectoryName(destFileName.Replace(EngineEnvironment.ProjectPath, string.Empty));
                            var textureFileName = textureAssetInfo.Name + Constants.FileNameExtensions.Texture;
                            textureAssetInfo.FileName = Path.Combine(pathFileName, textureFileName).TrimStart(Path.DirectorySeparatorChar);
                            AssetSaver.SaveAsset(Path.Combine(EngineEnvironment.ProjectPath, texture.FileName), texture);
                            AssetCatalog.Add(textureAssetInfo);

                            AssetCatalog.Save();

                            ListBoxFolderContent.SelectedItem = (DataContext as ContentBrowserViewModel).ContentItems[^1];
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"The file {Path.GetFileName(fileName)} already exists!", "File already exists", MessageBoxButton.OK);
                        }
                    }
                }
            }
        }
    }

    private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
    {
        AssetCatalog.Save();
    }

    private void ListBoxFolderContentCreate_Click(object sender, RoutedEventArgs e)
    {

    }

    private void MenuItemCreateEntity_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new AActor(), Constants.FileNameExtensions.Entity);
    }
    private void MenuItemCreateScreen_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new ScreenGui(), Constants.FileNameExtensions.Screen);
    }

    private void CreateAsset(AActor actor, string extension)
    {
        var inputTextBox = new InputTextBox();

        var assetInfo = new AssetInfo(actor.Id);

        inputTextBox.Text = assetInfo.Name;

        if (inputTextBox.ShowDialog() == true)
        {
            //add
            var folderItem = treeViewFolders.SelectedItem as FolderItem;
            var contentItem = new ContentItem(assetInfo);
            ListBoxFolderContent.SelectedItem = contentItem;

            //save asset
            assetInfo.Name = inputTextBox.Text;
            assetInfo.FileName = Path.Combine(folderItem.FullPath, assetInfo.Name + extension);
            AssetCatalog.Add(assetInfo);
            AssetSaver.SaveAsset(assetInfo.FileName, actor);
            AssetCatalog.Save();
        }
    }

    private void ListBoxFolderContentDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ListBoxFolderContent.SelectedItem == null)
        {
            return;
        }

        var contentItem = ListBoxFolderContent.SelectedItem as ContentItem;

        AssetCatalog.Remove(contentItem.AssetInfo.Id);
    }

    private void ListBoxFolderContent_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem listBoxItem)
        {
            return;
        }

        foreach (MenuItem menuItem in listBoxItem.ContextMenu.Items)
        {
            if (string.Equals(menuItem.Name, "Delete", StringComparison.InvariantCultureIgnoreCase))
            {
                menuItem.Visibility = Visibility.Collapsed;
            }
        }

        if (ListBoxFolderContent.SelectedItem is not FolderItem &&
            ListBoxFolderContent.SelectedItem is ContentItem contentItem)
        {
            if (contentItem.FileExtension == Constants.FileNameExtensions.Texture)
            {
                foreach (MenuItem menuItem in listBoxItem.ContextMenu.Items)
                {
                    if (menuItem.Name == "menuItemCreateSprites")
                    {
                        menuItem.Visibility = Visibility.Visible;
                    }
                }
            }
        }
    }

    private void MenuItemCreateSprites_Click(object sender, RoutedEventArgs e)
    {
        if (ListBoxFolderContent.SelectedItem is not FolderItem &&
            ListBoxFolderContent.SelectedItem is ContentItem contentItem)
        {
            //TODO create an editor which find sprites
            var assetContentManager = _gameEditor.Game.AssetContentManager;

            var texture = assetContentManager.Load<Texture>(contentItem.AssetInfo.Id);
            texture.Load(assetContentManager);
            var spriteData = new SpriteData();
            spriteData.PositionInTexture = texture.Resource.Bounds;
            spriteData.SpriteSheetAssetId = contentItem.AssetInfo.Id;

            var assetInfo = new AssetInfo(spriteData.Id)
            {
                Name = contentItem.AssetInfo.Name,
                FileName = contentItem.AssetInfo.FileName.Replace(Constants.FileNameExtensions.Texture, Constants.FileNameExtensions.Sprite)
            };
            AssetSaver.SaveAsset(Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName), spriteData);

            AssetCatalog.Add(assetInfo);
            AssetCatalog.Save();
        }
    }

    private void ListBox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem listBoxItem && listBoxItem.DataContext is ContentItem contentItem)
        {
            _dragAndDropData = contentItem.AssetInfo;
        }
    }

    private void ListBox_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragAndDropData = null;
    }

    private void ListBox_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragAndDropData != null && e.LeftButton == MouseButtonState.Pressed /* && detect moving ?? */
                                     && sender is DependencyObject dependencyObject)
        {
            DragDrop.DoDragDrop(dependencyObject, _dragAndDropData, DragDropEffects.Move);
            e.Handled = true;
        }
    }

    private static object GetDataFromListBox(ListBox source, Point point)
    {
        if (source.InputHitTest(point) is FrameworkElement { DataContext: ContentItem { AssetInfo: { } } contentItem })
        {
            return contentItem.AssetInfo;
        }

        return null;
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<AssetInfo, string> e)
    {
        if (DataContext is ContentBrowserViewModel contentBrowserViewModel)
        {
            foreach (var contentItem in contentBrowserViewModel.ContentItems)
            {
                if (contentItem is not FolderItem && contentItem.Name == e.Value2)
                {
                    contentItem.Name = e.Value.Name;
                }
            }
        }
    }

    private void MenuItemNewFolder_Click(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderItem folderItem })
        {
            var newFolderName = "New_Folder";
            int index = -1;

            while (folderItem.Contents.Any(x => string.Equals(x.Name, newFolderName)))
            {
                newFolderName = $"New_Folder_{++index}";
            }

            folderItem.Contents.Add(new FolderItem { Name = newFolderName });
        }
    }
}