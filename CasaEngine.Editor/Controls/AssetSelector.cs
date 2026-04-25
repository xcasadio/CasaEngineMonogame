using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Assets;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Thickness = MonoGame.Extended.Thickness;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Displays the name of a selected asset and a Browse button that opens a
/// picker window listing all assets from <see cref="AssetCatalog"/>.
/// </summary>
public class AssetSelector : MGStackPanel
{
    private readonly MGWindow _parentWindow;
    private readonly MGTextBlock _assetNameBlock;
    private readonly MGButton _browseButton;

    private Guid _assetId = Guid.Empty;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    /// <summary>The currently selected asset ID. Set to <see cref="Guid.Empty"/> for no selection.</summary>
    public Guid AssetId
    {
        get => _assetId;
        set
        {
            if (_assetId == value)
            {
                return;
            }

            _assetId = value;
            UpdateDisplayName();
        }
    }

    /// <summary>Optional filter applied to the asset list in the picker window.</summary>
    public Func<AssetInfo, bool> Filter { get; set; }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    public event EventHandler<Guid> AssetChanged;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public AssetSelector(MGWindow window)
        : base(window, Orientation.Horizontal)
    {
        _parentWindow = window;
        Spacing = 4;

        _assetNameBlock = new MGTextBlock(window, "[i]None[/i]")
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _browseButton = new MGButton(window, _ => OpenPickerWindow());
        if (EditorIcons.FolderOpen != null)
        {
            var img = new MGImage(window, EditorIcons.AsImage(EditorIcons.FolderOpen)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth  = 20,
                PreferredHeight = 20,
            };
            _browseButton.SetContent(img);
        }
        else
        {
            _browseButton.SetContent(new MGTextBlock(window, "Browse")
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        _browseButton.PreferredWidth = 28;

        TryAddChild(_assetNameBlock);
        TryAddChild(_browseButton);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdateDisplayName()
    {
        if (_assetId == Guid.Empty)
        {
            _assetNameBlock.Text = "[i]None[/i]";
        }
        else
        {
            var info = AssetCatalog.Get(_assetId);
            _assetNameBlock.Text = info != null ? info.Name : $"[c=Orange]Unknown ({_assetId:D8}…)[/c]";
        }
    }

    private void OpenPickerWindow()
    {
        if (_parentWindow?.Desktop == null)
        {
            return;
        }

        IEnumerable<AssetInfo> assets = AssetCatalog.AssetInfos;
        if (Filter != null)
        {
            assets = assets.Where(Filter);
        }

        var assetList = assets.OrderBy(a => a.Name).ToList();

        // Create a small picker window
        int winWidth  = 420;
        int winHeight = 500;
        int left = (_parentWindow.Desktop.ValidScreenBounds.Width  - winWidth)  / 2;
        int top  = (_parentWindow.Desktop.ValidScreenBounds.Height - winHeight) / 2;

        var pickerWindow = new MGWindow(_parentWindow.Desktop, left, top, winWidth, winHeight);
        pickerWindow.TitleText = "Select Asset";

        var content = new MGStackPanel(pickerWindow, Orientation.Vertical) { Spacing = 6, Padding = new Thickness(8) };

        var listBox = new MGListBox<AssetInfo>(pickerWindow);
        listBox.SetItemsSource(assetList);
        listBox.ItemTemplate = item => new MGTextBlock(pickerWindow, item.Name)
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        listBox.PreferredHeight = 400;

        var buttonRow = new MGStackPanel(pickerWindow, Orientation.Horizontal) { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

        var selectButton = new MGButton(pickerWindow, _ =>
        {
            var selected = listBox.SelectedValue;
            if (selected != null)
            {
                AssetId = selected.Id;
                AssetChanged?.Invoke(this, _assetId);
            }
            pickerWindow.TryCloseWindow();
        });
        selectButton.SetContent(new MGTextBlock(pickerWindow, "Select"));
        selectButton.PreferredWidth = 80;

        var cancelButton = new MGButton(pickerWindow, _ => pickerWindow.TryCloseWindow());
        cancelButton.SetContent(new MGTextBlock(pickerWindow, "Cancel"));
        cancelButton.PreferredWidth = 80;

        buttonRow.TryAddChild(selectButton);
        buttonRow.TryAddChild(cancelButton);

        content.TryAddChild(listBox);
        content.TryAddChild(buttonRow);
        pickerWindow.SetContent(content);

        // Mark currently selected item
        if (_assetId != Guid.Empty)
        {
            var current = assetList.FirstOrDefault(a => a.Id == _assetId);
            if (current != null)
            {
                listBox.SelectedValue = current;
            }
        }

        _parentWindow.Desktop.Windows.Add(pickerWindow);
    }
}
