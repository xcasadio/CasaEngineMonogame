using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.ContentBrowser.Views;

public sealed class GridView : IContentView
{
    private readonly MGListBox<ContentItem> _listBox;
    private readonly List<ContentItem> _items = new();

    public MGElement RootElement => _listBox;

    public MGListBox<ContentItem> ListBox => _listBox;

    public IReadOnlyList<ContentItem> SelectedItems => GetSelectedItems();

    public event Action<IReadOnlyList<ContentItem>>? SelectionChanged;
    public event Action<ContentItem>? FileDoubleClicked;
    public event Action<ContentItem>? DirectoryDoubleClicked;

    public GridView(MGWindow window, Func<ContentItem, MGElement> itemTemplate)
    {
        _listBox = new MGListBox<ContentItem>(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ItemTemplate = itemTemplate,
            SelectionMode = ListBoxSelectionMode.Single,
        };

        _listBox.SelectionChanged += OnSelectionChanged;
        _listBox.MouseHandler.LMBDoubleClickedInside += OnMouseDoubleClickedInside;
    }

    public void SetItems(IReadOnlyList<ContentItem> items)
    {
        _items.Clear();
        if (items != null)
        {
            _items.AddRange(items);
        }

        _listBox.SetItemsSource(_items);
    }

    public void ClearSelection()
    {
        _listBox.ClearSelection();
    }

    public void RestoreSelection(IReadOnlyList<ContentItem> items)
    {
        if (items == null || items.Count == 0)
        {
            _listBox.ClearSelection();
            return;
        }

        _listBox.SelectItem(items[0], true);
    }

    private void OnSelectionChanged(object? sender, System.Collections.ObjectModel.ReadOnlyCollection<MGListBoxItem<ContentItem>> items)
    {
        SelectionChanged?.Invoke(GetSelectedItems());
    }

    private void OnMouseDoubleClickedInside(object? sender, MGUI.Shared.Input.Mouse.BaseMouseClickedEventArgs e)
    {
        var selected = _listBox.SelectedValue;
        if (selected == null)
        {
            return;
        }

        if (selected.IsDirectory)
        {
            DirectoryDoubleClicked?.Invoke(selected);
            return;
        }

        FileDoubleClicked?.Invoke(selected);
    }

    private IReadOnlyList<ContentItem> GetSelectedItems()
    {
        var selectedItems = new List<ContentItem>();
        foreach (var item in _listBox.SelectedDataItems)
        {
            selectedItems.Add(item);
        }

        return selectedItems;
    }
}