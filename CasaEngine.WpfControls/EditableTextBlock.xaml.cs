using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaEngine.WpfControls;

public partial class EditableTextBlock : UserControl
{
    public EditableTextBlock()
    {
        InitializeComponent();
        Focusable = true;
        FocusVisualStyle = null;
    }

    // We keep the old text when we go into editmode
    // in case the user aborts with the escape key
    private string oldText;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(EditableTextBlock),
            new PropertyMetadata(""));

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }
    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(EditableTextBlock),
            new PropertyMetadata(true));

    public bool IsInEditMode
    {
        get
        {
            if (IsEditable)
            {
                return (bool)GetValue(IsInEditModeProperty);
            }

            return false;
        }
        set
        {
            if (IsEditable)
            {
                if (value)
                {
                    oldText = Text;
                }

                SetValue(IsInEditModeProperty, value);
            }
        }
    }
    public static readonly DependencyProperty IsInEditModeProperty =
        DependencyProperty.Register(
            nameof(IsInEditMode),
            typeof(bool),
            typeof(EditableTextBlock),
            new PropertyMetadata(false));

    public string TextFormat
    {
        get => (string)GetValue(TextFormatProperty);
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                value = "{0}";
            }

            SetValue(TextFormatProperty, value);
        }
    }
    public static readonly DependencyProperty TextFormatProperty =
        DependencyProperty.Register(
            nameof(TextFormat),
            typeof(string),
            typeof(EditableTextBlock),
            new PropertyMetadata("{0}"));

    public string FormattedText => string.Format(TextFormat, Text);

    void TextBox_Loaded(object sender, RoutedEventArgs e)
    {
        TextBox txt = sender as TextBox;
        txt.Focus();
        txt.SelectAll();
    }

    void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        IsInEditMode = false;
    }

    void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            IsInEditMode = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsInEditMode = false;
            Text = oldText;
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        IsInEditMode = true;
        e.Handled = true;
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        IsInEditMode = true;
        e.Handled = true;
    }
}