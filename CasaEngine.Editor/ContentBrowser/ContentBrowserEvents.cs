using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;

namespace CasaEngine.Editor.ContentBrowser;

public sealed class ContentBrowserEvents
{
    public event Action<ContentItem> FileSelected;
    public event Action<ContentItem> FileOpened;
    public event Action<ContentItem> FileDeleted;
    public event Action<ContentItem, string> FileRenamed;
    public event Action<ContentItem, ContentItem> FileMoved;
    public event Action<IReadOnlyList<ContentItem>> SelectionChanged;

    internal void RaiseFileSelected(ContentItem item) => FileSelected?.Invoke(item);
    internal void RaiseFileOpened(ContentItem item) => FileOpened?.Invoke(item);
    internal void RaiseFileDeleted(ContentItem item) => FileDeleted?.Invoke(item);
    internal void RaiseFileRenamed(ContentItem item, string oldName) => FileRenamed?.Invoke(item, oldName);
    internal void RaiseFileMoved(ContentItem item, ContentItem oldParent) => FileMoved?.Invoke(item, oldParent);
    internal void RaiseSelectionChanged(IReadOnlyList<ContentItem> items) => SelectionChanged?.Invoke(items);
}