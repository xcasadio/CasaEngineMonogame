using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EditorWpf.Controls.ContentBrowser;

public class ContentItem : INotifyPropertyChanged
{
    private ContentItem? _parent;
    private string _name;

    public string Path => Parent != null ? System.IO.Path.Combine(Parent.Path ?? ".", Name) : Name;
    public Type Type => GetType();

    public ContentItem? Parent
    {
        get => _parent;
        set
        {
            if (Equals(value, _parent))
            {
                return;
            }

            _parent = value;
            OnPropertyChanged();
        }
    }


    public string Name
    {
        get => _name;
        set
        {
            if (value == _name)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}