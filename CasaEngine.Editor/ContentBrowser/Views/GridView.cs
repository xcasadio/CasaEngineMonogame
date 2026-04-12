using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.ContentBrowser.Views;

public sealed class GridView : IContentView
{
    private sealed class GridItemCard
    {
        public ContentItem Item { get; }
        public MGBorder Border { get; }
        public MGImage? PreviewImage { get; }
        public MGTextBlock NameText { get; }

        public GridItemCard(ContentItem item, MGBorder border, MGImage? previewImage, MGTextBlock nameText)
        {
            Item = item;
            Border = border;
            PreviewImage = previewImage;
            NameText = nameText;
        }
    }

    private static readonly Color SelectedBackgroundColor = new(52, 96, 156, 180);
    private static readonly Color HoverBackgroundColor = new(50, 50, 58, 180);
    private static readonly Color IdleBackgroundColor = Color.Transparent;

    private readonly MGGrid _root;
    private readonly MGScrollViewer _scrollViewer;
    private readonly MGWrapPanel _itemsPanel;
    private readonly MGTextBlock _emptyStateText;
    private readonly Func<ContentItem, Texture2D?> _previewSelector;
    private readonly Action<ContentItem, MGElement>? _itemElementInitializer;
    private readonly Dictionary<string, GridItemCard> _cardsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ContentItem> _items = new();
    private readonly int _thumbnailSize;
    private readonly int _previewSize;
    private readonly int _cardWidth;

    public MGElement RootElement => _root;

    public ContentItem? PressedItem { get; private set; }

    public IReadOnlyList<ContentItem> SelectedItems => GetSelectedItems();

    public event Action<IReadOnlyList<ContentItem>>? SelectionChanged;
    public event Action<ContentItem>? FileDoubleClicked;
    public event Action<ContentItem>? DirectoryDoubleClicked;

    public GridView(
        MGWindow window,
        int thumbnailSize,
        Func<ContentItem, Texture2D?> previewSelector,
        Action<ContentItem, MGElement>? itemElementInitializer = null)
    {
        _thumbnailSize = Math.Max(48, thumbnailSize);
        _previewSize = Math.Max(40, _thumbnailSize - 12);
        _cardWidth = _thumbnailSize + 32;
        _previewSelector = previewSelector ?? throw new ArgumentNullException(nameof(previewSelector));
        _itemElementInitializer = itemElementInitializer;

        _itemsPanel = new MGWrapPanel(window, Orientation.Horizontal)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 4,
        };

        _scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _scrollViewer.SetContent(_itemsPanel);
        _scrollViewer.MouseHandler.LMBPressedInside += OnBackgroundPressed;

        _emptyStateText = new MGTextBlock(window, "This folder is empty")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsBold = true,
            Visibility = Visibility.Collapsed,
        };

        _root = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _root.AddRow(GridLength.CreateWeightedLength(1));
        _root.AddColumn(GridLength.CreateWeightedLength(1));
        _root.TryAddChild(0, 0, _scrollViewer);
        _root.TryAddChild(0, 0, _emptyStateText);
    }

    public void SetItems(IReadOnlyList<ContentItem> items)
    {
        _items.Clear();
        _cardsByPath.Clear();
        _selectedPaths.Clear();
        PressedItem = null;
        _ = _itemsPanel.TryRemoveAll();

        if (items != null)
        {
            _items.AddRange(items);
        }

        foreach (var item in _items)
        {
            var card = CreateCard(item);
            _cardsByPath[item.FullPath] = card;
            _itemsPanel.TryAddChild(card.Border);
        }

        UpdateEmptyState();
        SelectionChanged?.Invoke(Array.Empty<ContentItem>());
    }

    public void ClearSelection()
    {
        if (_selectedPaths.Count == 0)
        {
            return;
        }

        _selectedPaths.Clear();
        UpdateAllCardVisualStates();
        SelectionChanged?.Invoke(Array.Empty<ContentItem>());
    }

    public void RestoreSelection(IReadOnlyList<ContentItem> items)
    {
        if (items == null || items.Count == 0)
        {
            ClearSelection();
            return;
        }

        _selectedPaths.Clear();
        GridItemCard? firstSelectedCard = null;
        foreach (var item in items)
        {
            if (item == null)
            {
                continue;
            }

            if (_cardsByPath.TryGetValue(item.FullPath, out var card))
            {
                _selectedPaths.Add(card.Item.FullPath);
                firstSelectedCard ??= card;
            }
        }

        UpdateAllCardVisualStates();
        if (firstSelectedCard != null)
        {
            _scrollViewer.EnsureElementVisible(firstSelectedCard.Border);
        }

        SelectionChanged?.Invoke(GetSelectedItems());
    }

    public bool TryGetPrimarySelectionBounds(out Rectangle bounds)
    {
        foreach (var item in _items)
        {
            if (!_selectedPaths.Contains(item.FullPath))
            {
                continue;
            }

            if (_cardsByPath.TryGetValue(item.FullPath, out var card) && !card.Border.ActualLayoutBounds.IsEmpty)
            {
                bounds = card.Border.ActualLayoutBounds;
                return true;
            }
        }

        bounds = Rectangle.Empty;
        return false;
    }

    public void RefreshItemPresentation(ContentItem item)
    {
        if (item == null || !_cardsByPath.TryGetValue(item.FullPath, out var card))
        {
            return;
        }

        card.NameText.Text = GetDisplayName(item.Name);
        if (card.PreviewImage != null)
        {
            var previewTexture = _previewSelector(item);
            card.PreviewImage.Source = previewTexture == null ? null : new MGTextureData(EditorIcons.AsImage(previewTexture)!);
        }
    }

    private GridItemCard CreateCard(ContentItem item)
    {
        var previewTexture = _previewSelector(item);
        MGImage? previewImage = null;
        if (previewTexture != null)
        {
            previewImage = new MGImage(_scrollViewer.SelfOrParentWindow, EditorIcons.AsImage(previewTexture)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = _previewSize,
                PreferredHeight = _previewSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var previewHost = new MGBorder(_scrollViewer.SelfOrParentWindow, new Thickness(0), MGUniformBorderBrush.Black)
        {
            Padding = new Thickness(0),
            CornerRadius = MGCornerRadius.Zero,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(new Color(18, 18, 22))),
        };

        if (previewImage != null)
        {
            previewHost.SetContent(previewImage);
        }

        var nameText = new MGTextBlock(_scrollViewer.SelfOrParentWindow, GetDisplayName(item.Name))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = HorizontalAlignment.Center,
            WrapText = true,
            MaxLines = 2,
            Margin = new Thickness(4, 1, 4, 0),
            Padding = new Thickness(0, 0, 0, 0),
            LinePadding = 1,
        };

        var content = new MGStackPanel(_scrollViewer.SelfOrParentWindow, Orientation.Vertical)
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        content.TryAddChild(previewHost);
        content.TryAddChild(nameText);

        var border = new MGBorder(_scrollViewer.SelfOrParentWindow, new Thickness(0), MGUniformBorderBrush.Black)
        {
            Padding = new Thickness(2),
            Margin = new Thickness(0),
            CornerRadius = new MGCornerRadius(8),
            PreferredWidth = _cardWidth,
            MinWidth = _cardWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(IdleBackgroundColor)),
        };
        border.SetContent(content);

        _itemElementInitializer?.Invoke(item, border);
        border.MouseHandler.LMBPressedInside += (_, _) => OnCardPressed(item);
        border.MouseHandler.LMBDoubleClickedInside += (_, _) => OnCardDoubleClicked(item);
        border.MouseHandler.DragStart += (_, e) => OnCardDragStart(item, e);
        border.MouseHandler.RMBReleasedInside += (_, e) => OnCardRightClicked(item, e.Position);
        border.MouseHandler.Entered += (_, _) => UpdateCardVisualState(item.FullPath);
        border.MouseHandler.Exited += (_, _) => UpdateCardVisualState(item.FullPath);

        UpdateCardVisualState(item.FullPath);
        return new GridItemCard(item, border, previewImage, nameText);
    }

    private IReadOnlyList<ContentItem> GetSelectedItems()
    {
        var selectedItems = new List<ContentItem>();
        foreach (var item in _items)
        {
            if (_selectedPaths.Contains(item.FullPath))
            {
                selectedItems.Add(item);
            }
        }

        return selectedItems;
    }

    private void OnBackgroundPressed(object? sender, MGUI.Shared.Input.Mouse.BaseMousePressedEventArgs e)
    {
        var hovered = _scrollViewer.SelfOrParentWindow.HoveredElement;
        if (hovered == null)
        {
            ClearSelection();
            return;
        }

        foreach (var card in _cardsByPath.Values)
        {
            if (card.Border == hovered || card.Border.IsSelfOrAncestorOf(hovered))
            {
                return;
            }
        }

        ClearSelection();
    }

    private void OnCardPressed(ContentItem item)
    {
        PressedItem = item;
        _scrollViewer.Focus();

        bool isControlDown = IsControlDown();
        if (isControlDown)
        {
            if (_selectedPaths.Contains(item.FullPath))
            {
                _selectedPaths.Remove(item.FullPath);
            }
            else
            {
                _selectedPaths.Add(item.FullPath);
            }
        }
        else
        {
            _selectedPaths.Clear();
            _selectedPaths.Add(item.FullPath);
        }

        UpdateAllCardVisualStates();
        SelectionChanged?.Invoke(GetSelectedItems());
    }

    private void OnCardDoubleClicked(ContentItem item)
    {
        if (!_selectedPaths.Contains(item.FullPath))
        {
            _selectedPaths.Clear();
            _selectedPaths.Add(item.FullPath);
            UpdateAllCardVisualStates();
            SelectionChanged?.Invoke(GetSelectedItems());
        }

        if (item.IsDirectory)
        {
            DirectoryDoubleClicked?.Invoke(item);
            return;
        }

        FileDoubleClicked?.Invoke(item);
    }

    private void OnCardDragStart(ContentItem item, MGUI.Shared.Input.Mouse.BaseMouseDragStartEventArgs e)
    {
        PressedItem = item;
    }

    private void OnCardRightClicked(ContentItem item, Point position)
    {
        if (!_selectedPaths.Contains(item.FullPath))
        {
            _selectedPaths.Clear();
            _selectedPaths.Add(item.FullPath);
            UpdateAllCardVisualStates();
            SelectionChanged?.Invoke(GetSelectedItems());
        }
    }

    private void UpdateAllCardVisualStates()
    {
        foreach (var path in _cardsByPath.Keys)
        {
            UpdateCardVisualState(path);
        }
    }

    private void UpdateCardVisualState(string path)
    {
        if (!_cardsByPath.TryGetValue(path, out var card))
        {
            return;
        }

        bool isSelected = _selectedPaths.Contains(path);
        bool isHovered = card.Border.IsHovered;
        var backgroundColor = isSelected
            ? SelectedBackgroundColor
            : isHovered
                ? HoverBackgroundColor
                : IdleBackgroundColor;

        card.Border.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(backgroundColor));
    }

    private static bool IsControlDown()
    {
        var state = Keyboard.GetState();
        return state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl);
    }

    private static string GetDisplayName(string name)
    {
        const int maxLength = 28;
        if (string.IsNullOrWhiteSpace(name) || name.Length <= maxLength)
        {
            return name;
        }

        return string.Concat(name.AsSpan(0, maxLength - 1), "…");
    }

    private void UpdateEmptyState()
    {
        bool hasItems = _items.Count > 0;
        _scrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        _emptyStateText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }
}