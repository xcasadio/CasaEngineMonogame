using System.Collections.Generic;
using System.Linq;

namespace EditorWpf.Controls.ContentBrowser;

public class FolderItem : ContentItem
{
    public IEnumerable<ContentItem> Folders => Contents.Where(x => x is FolderItem);

    public List<ContentItem> Contents { get; } = new();
}