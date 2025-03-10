﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.Animation2dControls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Controls.EntityControls;
using CasaEngine.EditorUI.Controls.GameModeControls;
using CasaEngine.EditorUI.Controls.GuiEditorControls;
using CasaEngine.EditorUI.Controls.SpriteControls;
using CasaEngine.EditorUI.Controls.TileMapControls;
using CasaEngine.EditorUI.Controls.WorldControls;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Engine;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Objects;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Utils;

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
            case Constants.FileNameExtensions.GameMode:
                var gameModeEditorControl = window.GetEditorControl<GameModeEditorControl>();
                window.ActivateEditorControl<GameModeEditorControl>();
                gameModeEditorControl.OpenGameMode(fileRelativePath);
                break;
            case Constants.FileNameExtensions.ButtonsMapping:
                var buttonsMappingControl = window.GetEditorControl<ButtonsMappingControl>();
                window.ActivateEditorControl<ButtonsMappingControl>();
                buttonsMappingControl.OpenButtonsMapping(fileRelativePath);
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
            var destinationFolderPath = folderItem.FullPath;
            var assetContentManager = _gameEditor.Game.AssetContentManager;

            foreach (var fileName in fileNames)
            {
                ImportAssetFile(assetContentManager, fileName, destinationFolderPath);
            }

            AssetCatalog.Save();
            //ListBoxFolderContent.SelectedItem = (DataContext as ContentBrowserViewModel).ContentItems[^1];
        }
    }

    private void ImportAssetFile(AssetContentManager assetContentManager, string fullFileName, string destinationFolderPath)
    {
        if (assetContentManager.IsFileSupported(fullFileName))
        {
            var destFileName = Path.Combine(EngineEnvironment.ProjectPath, destinationFolderPath, Path.GetFileName(fullFileName));

            if (File.Exists(destFileName))
            {
                MessageBox.Show(Application.Current.MainWindow, $"The file {Path.GetFileName(fullFileName)} already exists!", "File already exists", MessageBoxButton.OK);
                return;
            }

            //create image file assetinfo
            var assetFromImportFile = new AssetInfo();
            assetFromImportFile.FileName = destFileName
                .Replace(EngineEnvironment.ProjectPath, string.Empty)
                .TrimStart(Path.DirectorySeparatorChar);
            assetFromImportFile.Name = Path.GetFileNameWithoutExtension(destFileName);
            string assetExtension;
            ObjectBase newAsset;

            var modelLoader = new ModelLoader();
            if (modelLoader.IsFileSupported(fullFileName))
            {
                var import3dFileOptionsWindow = new Import3dFileOptionsWindow();
                import3dFileOptionsWindow.Owner = Application.Current.MainWindow;

                if (import3dFileOptionsWindow.ShowDialog() == true)
                {
                    var riggedModel = (RiggedModel)modelLoader.LoadAsset(fullFileName, assetContentManager);

                    if (import3dFileOptionsWindow.ImportTextures)
                    {
                        ImportTexturesFromModel(riggedModel, assetContentManager, destinationFolderPath);
                    }

                    if (import3dFileOptionsWindow.ImportAnimations)
                    {
                        ImportAnimationsFromModel(riggedModel, assetContentManager, destinationFolderPath);
                    }

                    ImportAnimationsFromModel(riggedModel, assetContentManager, destinationFolderPath);

                    if (import3dFileOptionsWindow.ImportModel == false)
                    {
                        return;
                    }

                    var skinnedMesh = new SkinnedMesh();
                    skinnedMesh.Initialize(assetContentManager);
                    skinnedMesh.SetRiggedModel(riggedModel);
                    skinnedMesh.RiggedModelAssetId = assetFromImportFile.Id;
                    newAsset = skinnedMesh;
                    assetExtension = Constants.FileNameExtensions.Model;
                }
                else
                {
                    return;
                }
            }
            else if (Texture2DLoader.IsTextureFile(fullFileName))
            {
                newAsset = new Texture(assetFromImportFile.Id, _gameEditor.Game.GraphicsDevice);
                assetExtension = Constants.FileNameExtensions.Texture;
            }
            else
            {
                Logs.WriteInfo($"Can't import {fullFileName}, file '{Path.GetExtension(fullFileName)}' is not supported");
                return;
            }

            File.Copy(fullFileName, destFileName, true);
            Logs.WriteTrace($"Copy {fullFileName} -> {destFileName}");

            var newAssetInfo = new AssetInfo(newAsset.Id);
            newAssetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);
            var pathFileName = Path.GetDirectoryName(destFileName.Replace(EngineEnvironment.ProjectPath, string.Empty));
            var assetFullName = newAssetInfo.Name + assetExtension;
            newAssetInfo.FileName = Path.Combine(pathFileName, assetFullName).TrimStart(Path.DirectorySeparatorChar);
            AssetSaver.SaveAsset(Path.Combine(EngineEnvironment.ProjectPath, newAssetInfo.FileName), newAsset);
            AssetCatalog.Add(assetFromImportFile);
            AssetCatalog.Add(newAssetInfo);
        }
    }

    private void ImportTexturesFromModel(RiggedModel riggedModel, AssetContentManager assetContentManager,
        string destinationFolderPath)
    {
        foreach (var textureFileName in riggedModel.Meshes.SelectMany(x => new[] { x.TextureName, x.TextureNormalMapName, x.TextureHeightMapName, x.TextureReflectionMapName })
                     .Where(x => x != null).Distinct())
        {
            ImportAssetFile(assetContentManager, textureFileName, destinationFolderPath);
        }
    }

    private void ImportAnimationsFromModel(RiggedModel riggedModel, AssetContentManager assetContentManager, string destinationFolderPath)
    {
        foreach (var riggedAnimation in riggedModel.OriginalAnimations)
        {
            var animFileName = Path.Combine(destinationFolderPath, riggedAnimation.AnimationName + Constants.FileNameExtensions.SkeletonAnimation);
            AssetSaver.SaveSkeletonAnimationFromRiggedModel(animFileName, riggedAnimation);
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
        CreateAsset(new Entity(), Constants.FileNameExtensions.Entity);
    }

    private void MenuItemCreateScreen_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new ScreenGui(), Constants.FileNameExtensions.Screen);
    }

    private void MenuItemCreateWorld_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new World(), Constants.FileNameExtensions.World);
    }

    private void MenuItemCreateGameMode_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new GameMode(), Constants.FileNameExtensions.GameMode);
    }

    private void MenuItemCreateButtonsMapping_OnClick(object sender, RoutedEventArgs e)
    {
        CreateAsset(new ButtonsMapping(), Constants.FileNameExtensions.ButtonsMapping);
    }

    private void CreateAsset(ObjectBase objectBase, string extension)
    {
        var inputTextBox = new InputTextBox();

        var assetInfo = new AssetInfo(objectBase.Id);

        inputTextBox.Text = assetInfo.Name;

        if (inputTextBox.ShowDialog() == true)
        {
            //add
            var contentItem = new ContentItem(assetInfo);
            ListBoxFolderContent.SelectedItem = contentItem;
            var folderItem = treeViewFolders.SelectedItem as FolderItem;
            folderItem.AddContent(contentItem);

            //save asset
            assetInfo.Name = inputTextBox.Text;
            assetInfo.FileName = Path.Combine(folderItem.FullPath, assetInfo.Name + extension);
            AssetCatalog.Add(assetInfo);
            AssetSaver.SaveAsset(assetInfo.FileName, objectBase);
            AssetCatalog.Save();
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

    private void ListBoxFolderContentDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ListBoxFolderContent.SelectedItem == null)
        {
            return;
        }

        var contentItem = ListBoxFolderContent.SelectedItem as ContentItem;
        DeleteContentItem(contentItem);
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

            var newFolderItem = new FolderItem(newFolderName, folderItem);
            folderItem.AddContent(newFolderItem);
            Directory.CreateDirectory(Path.Combine(EngineEnvironment.ProjectPath, newFolderItem.FullPath));

            Logs.WriteDebug($"Create new folder '{newFolderItem.FullPath}'");
        }
    }

    private void TreeViewFolders_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            var textBox = FindVisualChild<TextBox>(e.OriginalSource as DependencyObject);

            if (textBox != null)
            {
                textBox.Visibility = Visibility.Visible;
            }
        }
    }

    private void MenuItemRenameFolder_Click(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderItem folderItem } treeViewItem)
        {
            var tempTextBox = FindVisualChild<TextBox>(treeViewItem);
            tempTextBox.Visibility = Visibility.Visible;
        }
    }

    private void MenuItemDeleteFolder_Click(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderItem folderItem } treeViewItem)
        {
            DeleteContentItem(folderItem);
        }
    }

    private static void DeleteContentItem(ContentItem contentItem)
    {
        if (MessageBox.Show(Application.Current.MainWindow,
                $"Do you want to delete {contentItem.Name} ?",
                "Delete",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            contentItem.Delete();
        }
    }

    private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(obj, i);

            if (child != null & child is T)
            {
                return (T)child;
            }

            T childOfChild = FindVisualChild<T>(child);

            if (childOfChild != null)
            {
                return childOfChild;
            }
        }

        return null;
    }

    private void TextBoxRename_LostFocus(object sender, RoutedEventArgs e)
    {
        (sender as FrameworkElement).Visibility = Visibility.Collapsed;
    }

    private void TextBoxRename_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape or Key.Return)
        {
            var treeViewItem = WpfUtils.FindParentWithType<TreeViewItem>(sender as FrameworkElement);
            treeViewItem.Focus();
        }
    }

    private void TreeViewFolders_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = WpfUtils.FindParentWithType<TreeViewItem>(e.OriginalSource as FrameworkElement);
        if (treeViewItem != null)
        {
            treeViewItem.Focus();
            e.Handled = true;
        }
    }
}