﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CasaEngine.WpfControls;

public class TreeViewEx : TreeView
{
    public delegate void ItemMovedCallback(object item, object newParent);

    public ItemMovedCallback ItemMoved;

    object _draggedItem, _target;

    public TreeViewEx()
    {
        AllowDrop = true;

        ItemContainerStyle ??= new Style(typeof(TreeViewItem));
        ItemContainerStyle.Setters.Add(new EventSetter(DragDrop.DragOverEvent, new DragEventHandler(treeView_DragOver)));
        ItemContainerStyle.Setters.Add(new EventSetter(DragDrop.DropEvent, new DragEventHandler(treeView_Drop)));
        ItemContainerStyle.Setters.Add(new EventSetter(MouseMoveEvent, new MouseEventHandler(treeView_MouseMove)));
    }

    private void treeView_MouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Items.CurrentPosition == -1)
                {
                    return;
                }

                //_draggedItem = (TreeViewItem)(ItemContainerGenerator.ContainerFromIndex(Items.CurrentPosition));
                _draggedItem = SelectedItem;
                if (_draggedItem != null)
                {
                    var finalDropEffect = DragDrop.DoDragDrop(this, SelectedValue, DragDropEffects.Move);
                    //Checking target is not null and item is dragging(moving)
                    if (finalDropEffect == DragDropEffects.Move && _target != null)
                    {
                        // Check if the move is valid
                        if (_target is FrameworkElement framework && CheckDropTarget(_draggedItem, framework.DataContext))
                        {
                            CopyItem(_draggedItem, framework.DataContext);
                            _target = null;
                            _draggedItem = null;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void treeView_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            // Verify that this is a valid drop and then store the drop target
            var item = GetNearestContainer(e.OriginalSource as UIElement);
            if (item != null)
            {
                e.Effects = CheckDropTarget(_draggedItem, item.DataContext) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        catch (Exception)
        {
        }
    }

    private void treeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            var targetItem = GetNearestContainer(e.OriginalSource as UIElement);
            if (targetItem != null && _draggedItem != null)
            {
                _target = targetItem;
                e.Effects = DragDropEffects.Move;

            }
        }
        catch (Exception)
        {
        }
    }

    private bool CheckDropTarget(object sourceItem, object targetItem)
    {
        return !Equals(sourceItem, targetItem);
    }

    private void CopyItem(object sourceItem, object targetItem)
    {
        ItemMoved?.Invoke(sourceItem, targetItem);
    }

    public void AddChild(TreeViewItem sourceItem, TreeViewItem targetItem)
    {
        var item1 = new TreeViewItem();
        item1.Header = sourceItem.Header;
        targetItem.Items.Add(item1);
        foreach (TreeViewItem item in sourceItem.Items)
        {
            AddChild(item, item1);
        }
    }

    private TreeViewItem GetNearestContainer(UIElement element)
    {
        var container = element as TreeViewItem;
        while (container == null && element != null)
        {
            element = VisualTreeHelper.GetParent(element) as UIElement;
            container = element as TreeViewItem;
        }
        return container;
    }

    /*
    private void SelectTreeViewItem(object item)
    {
        Stack<object> parents = new();
        parents.Push(item);
        var root = item;

        while (root != null)
        {
            parents.Push(root);
            root = root.Parent;
        }

        ItemsControl itemsControl = this;

        while (parents.TryPop(out var entity))
        {
            if (itemsControl.ItemContainerGenerator.ContainerFromItem(entity) is TreeViewItem treeViewItem)
            {
                itemsControl = treeViewItem;
                treeViewItem.IsExpanded = true;
                treeViewItem.IsSelected = true;
                treeViewItem.BringIntoView();
            }
        }
    }*/
}