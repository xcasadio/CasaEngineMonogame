﻿using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Editor.Controls.Animation2dControls;
using CasaEngine.Editor.Controls.Common;
using CasaEngine.Editor.Controls.EntityControls;
using CasaEngine.Editor.Controls.SpriteControls;
using CasaEngine.Editor.Controls.TileMapControls;
using CasaEngine.Editor.Controls.WorldControls;
using Microsoft.Xna.Framework;
using Point = System.Windows.Point;

namespace CasaEngine.Editor.Controls.ContentBrowser
{
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
        }

        public void InitializeFromGameEditor(GameEditor gameEditor)
        {
            _gameEditor = gameEditor;
            var contentBrowserViewModel = DataContext as ContentBrowserViewModel;
            contentBrowserViewModel.Initialize(gameEditor);
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LogManager.Instance.WriteLineDebug("ContentBrowser.ListBoxItem_MouseDoubleClick()");

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
                        if (Texture2DLoader.IsTextureFile(fileName))
                        {
                            var destFileName = Path.Combine(EngineEnvironment.ProjectPath, folderPath, Path.GetFileName(fileName));
                            if (!File.Exists(destFileName))
                            {
                                //copy file
                                File.Copy(fileName, destFileName, true);
                                LogManager.Instance.WriteLineTrace($"Copy {fileName} -> {destFileName}");

                                //create assetinfo
                                var textureAssetInfo = new AssetInfo();
                                textureAssetInfo.FileName = destFileName
                                    .Replace(EngineEnvironment.ProjectPath, string.Empty)
                                    .TrimStart(Path.DirectorySeparatorChar);
                                textureAssetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);

                                //Create texture asset
                                var texture = new Texture(textureAssetInfo.Id, _gameEditor.Game.GraphicsDevice);
                                texture.AssetInfo.Name = Path.GetFileNameWithoutExtension(destFileName);
                                texture.AssetInfo.FileName = Path.Combine(
                                    Path.GetDirectoryName(destFileName.Replace(EngineEnvironment.ProjectPath, string.Empty)), texture.AssetInfo.Name + Constants.FileNameExtensions.Texture)
                                    .TrimStart(Path.DirectorySeparatorChar);
                                //texture.Load(assetInfo, _gameEditor.Game.GameManager.AssetContentManager);
                                AssetSaver.SaveAsset(Path.Combine(EngineEnvironment.ProjectPath, texture.AssetInfo.FileName), texture);

                                GameSettings.AssetInfoManager.Add(texture.AssetInfo);
                                GameSettings.AssetInfoManager.Add(textureAssetInfo);
                                GameSettings.AssetInfoManager.Save();
                            }
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

        private void MenuItemCreateEntity_OnClick(object sender, RoutedEventArgs e)
        {
            //ask name
            var entity = new Entity();
            var inputTextBox = new InputTextBox();
            inputTextBox.Text = entity.Name;

            if (inputTextBox.ShowDialog() == true)
            {
                //add asset

                var folderItem = treeViewFolders.SelectedItem as FolderItem;
                var contentItem = new ContentItem(entity.AssetInfo);
                //folderItem.Contents.Add(contentItem);
                ListBoxFolderContent.SelectedItem = contentItem;

                //save entity
                entity.Name = inputTextBox.Text;
                entity.AssetInfo.FileName = Path.Combine(folderItem.FullPath, entity.Name + Constants.FileNameExtensions.Entity);

                GameSettings.AssetInfoManager.Add(entity.AssetInfo);
                AssetSaver.SaveAsset(entity.AssetInfo.FileName, entity);
                GameSettings.AssetInfoManager.Save();
            }
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
            e.Handled = false;
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
    }
}