using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Editor.Diagnostics;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.Styling;
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
        public ContentItem? Item { get; private set; }
        public MGBorder Border { get; }
        public MGBorder PreviewHost { get; }
        public MGImage? PreviewImage { get; private set; }
        public MGTextBlock NameText { get; }

        public GridItemCard(MGBorder border, MGBorder previewHost, MGTextBlock nameText)
        {
            Border = border;
            PreviewHost = previewHost;
            NameText = nameText;
        }

        public void Bind(ContentItem item, Func<ContentItem, Texture2D?> previewSelector, Action<ContentItem, MGElement>? itemElementInitializer, int previewSize)
        {
            UpdatePresentation(item, previewSelector, previewSize);
            itemElementInitializer?.Invoke(item, Border);
        }

        public void UpdatePresentation(ContentItem item, Func<ContentItem, Texture2D?> previewSelector, int previewSize)
        {
            Item = item;
            NameText.Text = GetDisplayName(item.Name);
            SetPreview(previewSelector(item), previewSize);
        }

        private void SetPreview(Texture2D? previewTexture, int previewSize)
        {
            if (previewTexture == null)
            {
                if (PreviewImage != null)
                {
                    PreviewImage.Source = null;
                }

                return;
            }

            var textureData = new MGTextureData(EditorIcons.AsImage(previewTexture)!);
            if (PreviewImage == null)
            {
                PreviewImage = new MGImage(Border.SelfOrParentWindow, textureData, Stretch: Stretch.Uniform)
                {
                    PreferredWidth = previewSize,
                    PreferredHeight = previewSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                PreviewHost.SetContent(PreviewImage);
                return;
            }

            PreviewImage.Source = textureData;
        }
    }

    private static readonly Color SelectedBackgroundColor = EditorThemePalette.GridItemSelectedBackground;
    private static readonly Color HoverBackgroundColor = EditorThemePalette.GridItemHoverBackground;
    private static readonly Color IdleBackgroundColor = Color.Transparent;

    private readonly MGGrid _root;
    private readonly MGScrollViewer _scrollViewer;
    private readonly VirtualizingWrapPanel _itemsPanel;
    private readonly MGTextBlock _emptyStateText;
    private readonly Func<ContentItem, Texture2D?> _previewSelector;
    private readonly Action<ContentItem, MGElement>? _itemElementInitializer;
    private readonly Dictionary<string, GridItemCard> _cardsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<MGElement, GridItemCard> _cardsByElement = new();
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ContentItem> _items = new();
    private readonly int _thumbnailSize;
    private readonly int _previewSize;
    private readonly int _cardWidth;
    private readonly int _cardHeight;

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
        ArgumentNullException.ThrowIfNull(window);
        _thumbnailSize = Math.Max(48, thumbnailSize);
        _previewSize = Math.Max(40, _thumbnailSize - 12);
        _cardWidth = _thumbnailSize + 32;
        _cardHeight = _previewSize + 56;
        _previewSelector = previewSelector ?? throw new ArgumentNullException(nameof(previewSelector));
        _itemElementInitializer = itemElementInitializer;

        _itemsPanel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = _cardWidth,
            ItemHeight = _cardHeight,
            Spacing = 4,
            BufferRows = 2,
        };
        _itemsPanel.ItemGenerator = GenerateCardElement;
        _itemsPanel.ItemRecycler = RecycleCardElement;

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
        using var performancePhase = EditorPerformanceProbe.IsEnabled
            ? EditorPerformanceProbe.BeginPhase($"ContentBrowser.GridView.SetItems count={items?.Count ?? 0}")
            : default;

        _items.Clear();
        _cardsByPath.Clear();
        _selectedPaths.Clear();
        PressedItem = null;
        _itemsPanel.InvalidateData();

        if (items != null)
        {
            _items.AddRange(items);
        }

        _itemsPanel.TotalItemCount = _items.Count;

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

    public bool TryApplyAutomationScrollTarget(string target)
    {
        if (!string.Equals(target, "bottom", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_items.Count == 0)
        {
            return true;
        }

        if (_scrollViewer.ContentViewport.Height <= 0
            || !_itemsPanel.HasAttachedScrollViewer
            || _itemsPanel.FirstRealizedIndex < 0)
        {
            return false;
        }

        _scrollViewer.VerticalOffset = _scrollViewer.MaxVerticalOffset;
        return true;
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        return new[]
        {
            string.Create(CultureInfo.InvariantCulture,
                $"Grid viewport: {_scrollViewer.ContentViewport.Width}x{_scrollViewer.ContentViewport.Height} scroll={_scrollViewer.VerticalOffset:0.##}/{_scrollViewer.MaxVerticalOffset:0.##}"),
            $"Grid virtualization: attachedScrollViewer={_itemsPanel.HasAttachedScrollViewer} columns={_itemsPanel.CurrentColumnCount} realized={_itemsPanel.FirstRealizedIndex}-{_itemsPanel.LastRealizedIndex} items={_items.Count}",
        };
    }

    public void RestoreSelection(IReadOnlyList<ContentItem> items)
    {
        if (items == null || items.Count == 0)
        {
            ClearSelection();
            return;
        }

        _selectedPaths.Clear();
        int firstSelectedIndex = -1;
        foreach (var item in items)
        {
            if (item == null)
            {
                continue;
            }

            for (int index = 0; index < _items.Count; index++)
            {
                if (!string.Equals(_items[index].FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _selectedPaths.Add(_items[index].FullPath);
                if (firstSelectedIndex < 0)
                {
                    firstSelectedIndex = index;
                }

                break;
            }
        }

        UpdateAllCardVisualStates();
        if (firstSelectedIndex >= 0)
        {
            _itemsPanel.EnsureIndexVisible(firstSelectedIndex);
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

        card.UpdatePresentation(item, _previewSelector, _previewSize);
        UpdateCardVisualState(item.FullPath);
    }

    private MGElement GenerateCardElement(int index)
    {
        var item = _items[index];
        GridItemCard card;
        if (_itemsPanel.TryDequeueRecycledElement(out var recycledElement)
            && _cardsByElement.TryGetValue(recycledElement, out var recycledCard))
        {
            card = recycledCard;
        }
        else
        {
            card = CreateCard();
            _cardsByElement[card.Border] = card;
        }

        BindCard(card, item);
        return card.Border;
    }

    private void RecycleCardElement(int index, MGElement element)
    {
        if (!_cardsByElement.TryGetValue(element, out var card) || card.Item == null)
        {
            return;
        }

        _cardsByPath.Remove(card.Item.FullPath);
        card.Border.OverlayBrush = null;
    }

    private void BindCard(GridItemCard card, ContentItem item)
    {
        if (card.Item != null)
        {
            _cardsByPath.Remove(card.Item.FullPath);
        }

        card.Bind(item, _previewSelector, _itemElementInitializer, _previewSize);
        _cardsByPath[item.FullPath] = card;
        UpdateCardVisualState(item.FullPath);
    }

    private GridItemCard CreateCard()
    {
        var previewHost = new MGBorder(_scrollViewer.SelfOrParentWindow, new Thickness(0), MGUniformBorderBrush.Black)
        {
            Padding = new Thickness(0),
            CornerRadius = MGCornerRadius.Zero,
            PreferredHeight = _previewSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.GridItemPreviewBackground)),
        };

        var nameText = new MGTextBlock(_scrollViewer.SelfOrParentWindow, string.Empty)
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
            PreferredHeight = _cardHeight,
            MinWidth = _cardWidth,
            MinHeight = _cardHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(IdleBackgroundColor)),
        };
        border.SetContent(content);

        var card = new GridItemCard(border, previewHost, nameText);
        border.MouseHandler.LMBPressedInside += (_, _) =>
        {
            if (card.Item != null)
            {
                OnCardPressed(card.Item);
            }
        };
        border.MouseHandler.LMBDoubleClickedInside += (_, _) =>
        {
            if (card.Item != null)
            {
                OnCardDoubleClicked(card.Item);
            }
        };
        border.MouseHandler.DragStart += (_, e) =>
        {
            if (card.Item != null)
            {
                OnCardDragStart(card.Item, e);
            }
        };
        border.MouseHandler.RMBReleasedInside += (_, e) =>
        {
            if (card.Item != null)
            {
                OnCardRightClicked(card.Item, e.Position);
            }
        };
        border.MouseHandler.Entered += (_, _) =>
        {
            if (card.Item != null)
            {
                UpdateCardVisualState(card.Item.FullPath);
            }
        };
        border.MouseHandler.Exited += (_, _) =>
        {
            if (card.Item != null)
            {
                UpdateCardVisualState(card.Item.FullPath);
            }
        };

        return card;
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