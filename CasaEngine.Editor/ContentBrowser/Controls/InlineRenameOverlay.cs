using System;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.ContentBrowser.Controls;

public sealed class InlineRenameOverlay
{
    private static readonly IBorderBrush DefaultBorderBrush = new MGSolidFillBrush(new Color(82, 132, 204)).AsUniformBorderBrush();
    private static readonly IBorderBrush InvalidBorderBrush = Color.IndianRed.AsFillBrush().AsUniformBorderBrush();
    private static readonly VisualStateFillBrush PopupBackgroundBrush = new(new MGSolidFillBrush(new Color(24, 28, 36)));

    private readonly MGWindow _parentWindow;

    private MGWindow? _popupWindow;
    private MGTextBox? _textBox;
    private ContentItem? _item;
    private Func<ContentItem, string, bool>? _commitRename;
    private Action? _cancelRename;

    public bool IsOpen => _popupWindow != null;

    public InlineRenameOverlay(MGWindow parentWindow)
    {
        _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
    }

    public void Show(ContentItem item, Rectangle anchorBounds, Func<ContentItem, string, bool> commitRename, Action? cancelRename = null)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (commitRename == null)
        {
            throw new ArgumentNullException(nameof(commitRename));
        }

        Cancel();

        var popupBounds = GetPopupBounds(anchorBounds);
        var popupWindow = new MGWindow(_parentWindow, popupBounds.Left, popupBounds.Top, popupBounds.Width, popupBounds.Height)
        {
            IsTitleBarVisible = false,
            IsCloseButtonVisible = false,
            IsUserResizable = false,
            IsDraggable = false,
            MinWidth = popupBounds.Width,
            MaxWidth = popupBounds.Width,
            MinHeight = popupBounds.Height,
            MaxHeight = popupBounds.Height,
            BorderBrush = MGUniformBorderBrush.Transparent,
            BackgroundBrush = PopupBackgroundBrush,
        };

        var textBox = new MGTextBox(popupWindow, CharacterLimit: 255)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = item.Name,
            BorderBrush = DefaultBorderBrush.Copy(),
        };

        popupWindow.SetContent(textBox);
        popupWindow.WindowMouseHandler.ReleasedOutside += OnPopupReleasedOutside;
        textBox.KeyboardHandler.Pressed += OnTextBoxKeyPressed;

        _parentWindow.AddNestedWindow(popupWindow);

        _popupWindow = popupWindow;
        _textBox = textBox;
        _item = item;
        _commitRename = commitRename;
        _cancelRename = cancelRename;

        textBox.RequestFocus();
        textBox.SelectAll();
    }

    public void Cancel()
    {
        if (_popupWindow == null)
        {
            return;
        }

        _cancelRename?.Invoke();
        ClosePopup();
    }

    private void OnPopupReleasedOutside(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (e.IsLMB)
        {
            TryCommit();
        }
    }

    private void OnTextBoxKeyPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (_textBox == null || !ReferenceEquals(_textBox, sender))
        {
            return;
        }

        switch (e.Key)
        {
            case Keys.Enter:
                if (TryCommit())
                {
                    e.SetHandledBy(_textBox, true);
                }
                break;

            case Keys.Escape:
                Cancel();
                e.SetHandledBy(_textBox, true);
                break;
        }
    }

    private bool TryCommit()
    {
        if (_item == null || _textBox == null || _commitRename == null)
        {
            return false;
        }

        var proposedName = _textBox.Text.Trim();
        var validationError = ValidateName(_item, proposedName);
        if (validationError != null)
        {
            _textBox.BorderBrush = InvalidBorderBrush.Copy();
            _textBox.RequestFocus();
            return false;
        }

        _textBox.BorderBrush = DefaultBorderBrush.Copy();
        if (string.Equals(proposedName, _item.Name, StringComparison.Ordinal))
        {
            ClosePopup();
            return true;
        }

        if (!_commitRename(_item, proposedName))
        {
            _textBox.RequestFocus();
            return false;
        }

        ClosePopup();
        return true;
    }

    private static string? ValidateName(ContentItem item, string proposedName)
    {
        if (string.IsNullOrWhiteSpace(proposedName))
        {
            return "Name cannot be empty.";
        }

        if (proposedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "Name contains invalid characters.";
        }

        var parentDirectory = Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return "Cannot rename this item.";
        }

        var candidatePath = Path.Combine(parentDirectory, proposedName);
        var exists = item.IsDirectory ? Directory.Exists(candidatePath) : File.Exists(candidatePath);
        if (exists && !string.Equals(candidatePath, item.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return "An item with this name already exists.";
        }

        return null;
    }

    private static Rectangle GetPopupBounds(Rectangle anchorBounds)
    {
        var width = Math.Max(160, anchorBounds.Width + 24);
        var height = Math.Max(30, anchorBounds.Height + 8);
        var left = Math.Max(0, anchorBounds.Left - 4);
        var top = Math.Max(0, anchorBounds.Top - 4);
        return new Rectangle(left, top, width, height);
    }

    private void ClosePopup()
    {
        if (_popupWindow != null)
        {
            _popupWindow.WindowMouseHandler.ReleasedOutside -= OnPopupReleasedOutside;
            if (_textBox != null)
            {
                _textBox.KeyboardHandler.Pressed -= OnTextBoxKeyPressed;
            }

            _popupWindow.TryCloseWindow();
        }

        _popupWindow = null;
        _textBox = null;
        _item = null;
        _commitRename = null;
        _cancelRename = null;
    }
}