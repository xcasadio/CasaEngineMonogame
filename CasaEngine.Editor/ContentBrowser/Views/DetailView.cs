using System;
using System.Collections.Generic;
using CasaEngine.Editor.Diagnostics;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.ContentBrowser.Views;

public sealed class DetailView : IContentView
{
    private readonly MGWindow _window;
    private readonly Func<ContentItemType, Texture2D> _iconSelector;
    private readonly MGGrid _root;
    private readonly MGListView<ContentItem> _listView;
    private readonly List<ContentItem> _items = new();
    private readonly Action<ContentItem, MGElement> _itemElementInitializer;
    private readonly MGTextBlock _emptyStateText;
    private MGListViewColumn<ContentItem> _nameColumn;

    public MGElement RootElement => _root;

    public MGElement KeyboardFocusElement => _listView;

    public MGListView<ContentItem> ListView => _listView;

    public IReadOnlyList<ContentItem> SelectedItems
    {
        get
        {
            var selected = GetSelectedItem();
            return selected == null ? Array.Empty<ContentItem>() : new[] { selected };
        }
    }

    public event Action<IReadOnlyList<ContentItem>> SelectionChanged;
    public event Action<ContentItem> FileDoubleClicked;
    public event Action<ContentItem> DirectoryDoubleClicked;

    public DetailView(MGWindow window, Func<ContentItemType, Texture2D> iconSelector, Action<ContentItem, MGElement> itemElementInitializer = null)
    {
        _window = window;
        _iconSelector = iconSelector ?? throw new ArgumentNullException(nameof(iconSelector));
        _itemElementInitializer = itemElementInitializer;
        _root = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _root.AddRow(GridLength.CreateWeightedLength(1));
        _root.AddColumn(GridLength.CreateWeightedLength(1));

        _listView = new MGListView<ContentItem>(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowHeight = 28,
            SelectionMode = GridSelectionMode.Row,
        };

        //  Clicking an already-selected row must keep it selected, like the grid view does.
        //  The default toggle behaviour clears the selection on the second click of a double-click,
        //  which happens before the double-click handler runs and would leave it without an item to open.
        _listView.DataGrid.CanDeselectByClickingSelectedCell = false;

        _emptyStateText = new MGTextBlock(window, "This folder is empty")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsBold = true,
            Visibility = Visibility.Collapsed,
        };

        _root.TryAddChild(0, 0, _listView);
        _root.TryAddChild(0, 0, _emptyStateText);

        ConfigureColumns();
        _listView.SelectionChanged += OnSelectionChanged;
        _listView.MouseHandler.LMBDoubleClickedInside += OnMouseDoubleClickedInside;
    }

    public void SetItems(IReadOnlyList<ContentItem> items)
    {
        using var performancePhase = EditorPerformanceProbe.IsEnabled
            ? EditorPerformanceProbe.BeginPhase($"ContentBrowser.DetailView.SetItems count={items?.Count ?? 0}")
            : default;

        _items.Clear();
        if (items != null)
        {
            _items.AddRange(items);
        }

        _listView.SetItemsSource(_items);
        ApplyItemElementInitializers();
        UpdateEmptyState();
    }

    public void ClearSelection()
    {
        _listView.DataGrid.CurrentSelection = null;
        SelectionChanged?.Invoke(Array.Empty<ContentItem>());
    }

    public void RestoreSelection(IReadOnlyList<ContentItem> items)
    {
        if (items == null || items.Count == 0 || _listView.RowItems == null || _listView.RowItems.Count == 0 || _listView.DataGrid.Columns.Count == 0)
        {
            ClearSelection();
            return;
        }

        var targetPath = items[0].FullPath;
        for (var index = 0; index < _listView.RowItems.Count; index++)
        {
            var rowItem = _listView.RowItems[index];
            if (!string.Equals(rowItem.Data.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _listView.DataGrid.CurrentSelection = new GridSelection(
                _listView.DataGrid,
                new GridCell(rowItem.DataRow, _listView.DataGrid.Columns[0]),
                GridSelectionMode.Row);
            return;
        }

        ClearSelection();
    }

    public bool TryGetPrimarySelectionBounds(out Rectangle bounds)
    {
        var rowItem = GetSelectedRowItem();
        if (rowItem != null)
        {
            var rowContents = rowItem.GetRowContents();
            if (_nameColumn != null && rowContents.TryGetValue(_nameColumn, out var nameCell) && !nameCell.ActualLayoutBounds.IsEmpty)
            {
                bounds = nameCell.ActualLayoutBounds;
                return true;
            }

            foreach (var cell in rowContents.Values)
            {
                if (!cell.ActualLayoutBounds.IsEmpty)
                {
                    bounds = cell.ActualLayoutBounds;
                    return true;
                }
            }
        }

        bounds = Rectangle.Empty;
        return false;
    }

    private void ConfigureColumns()
    {
        var iconColumn = _listView.AddColumn(new ListViewColumnWidth(32), new MGTextBlock(_window, string.Empty), CreateIconCell);
        iconColumn.IsSortable = false;

        _nameColumn = _listView.AddColumn(new ListViewColumnWidth(2.4), new MGTextBlock(_window, "Name") { IsBold = true }, CreateNameCell);
        _nameColumn.IsSortable = true;
        _nameColumn.SortKeySelector = item => item.Name;

        var typeColumn = _listView.AddColumn(new ListViewColumnWidth(1.3), new MGTextBlock(_window, "Type") { IsBold = true }, CreateTypeCell);
        typeColumn.IsSortable = true;
        typeColumn.SortKeySelector = item => ContentItemDisplay.GetTypeLabel(item);

        var sizeColumn = _listView.AddColumn(new ListViewColumnWidth(110), new MGTextBlock(_window, "Size") { IsBold = true }, CreateSizeCell);
        sizeColumn.IsSortable = true;
        sizeColumn.SortKeySelector = item => item.Size;

        var modifiedColumn = _listView.AddColumn(new ListViewColumnWidth(170), new MGTextBlock(_window, "Modified") { IsBold = true }, CreateModifiedCell);
        modifiedColumn.IsSortable = true;
        modifiedColumn.SortKeySelector = item => item.LastModified;
    }

    private void OnSelectionChanged(object sender, GridSelection? selection)
    {
        SelectionChanged?.Invoke(SelectedItems);
    }

    private void OnMouseDoubleClickedInside(object sender, MGUI.Shared.Input.Mouse.BaseMouseClickedEventArgs e)
    {
        var selected = GetSelectedItem();
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

    private ContentItem GetSelectedItem()
    {
        var rowItem = GetSelectedRowItem();
        return rowItem?.Data;
    }

    private MGListViewItem<ContentItem> GetSelectedRowItem()
    {
        var selection = _listView.SelectedData;
        if (!selection.HasValue || _listView.RowItems == null)
        {
            return null;
        }

        var selectedRowIndex = _listView.DataGrid.GetRowIndex(selection.Value.Cell.Row);
        if (selectedRowIndex < 0 || selectedRowIndex >= _listView.RowItems.Count)
        {
            return null;
        }

        return _listView.RowItems[selectedRowIndex];
    }

    private MGElement CreateIconCell(ContentItem item)
    {
        Texture2D icon = _iconSelector(item.Type);
        if (icon == null)
        {
            return new MGTextBlock(_window, string.Empty);
        }

        return new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
        {
            PreferredWidth = 16,
            PreferredHeight = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private MGElement CreateNameCell(ContentItem item)
    {
        return new MGTextBlock(_window, item.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = false,
            MaxLines = 1,
        };
    }

    private MGElement CreateTypeCell(ContentItem item)
    {
        return new MGTextBlock(_window, ContentItemDisplay.GetTypeLabel(item))
        {
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = false,
            MaxLines = 1,
        };
    }

    private MGElement CreateSizeCell(ContentItem item)
    {
        return new MGTextBlock(_window, item.IsDirectory ? string.Empty : ContentItemDisplay.FormatSize(item.Size))
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            WrapText = false,
            MaxLines = 1,
        };
    }

    private MGElement CreateModifiedCell(ContentItem item)
    {
        return new MGTextBlock(_window, item.LastModified == default ? string.Empty : item.LastModified.ToString("yyyy-MM-dd HH:mm"))
        {
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = false,
            MaxLines = 1,
        };
    }

    private void ApplyItemElementInitializers()
    {
        if (_itemElementInitializer == null || _listView.RowItems == null)
        {
            return;
        }

        foreach (var rowItem in _listView.RowItems)
        {
            foreach (var cell in rowItem.GetRowContents().Values)
            {
                _itemElementInitializer(rowItem.Data, cell);
            }
        }
    }

    private void UpdateEmptyState()
    {
        bool hasItems = _items.Count > 0;
        _listView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        _emptyStateText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }
}