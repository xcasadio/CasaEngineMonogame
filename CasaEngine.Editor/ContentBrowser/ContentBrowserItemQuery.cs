using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;

namespace CasaEngine.Editor.ContentBrowser;

public static class ContentBrowserItemQuery
{
    public static List<ContentItem> GetVisibleItems(ContentItem folder, string searchFilter, Predicate<ContentItem> shouldIncludeItem)
    {
        if (folder == null)
        {
            throw new ArgumentNullException(nameof(folder));
        }

        if (shouldIncludeItem == null)
        {
            throw new ArgumentNullException(nameof(shouldIncludeItem));
        }

        var visibleItems = new List<ContentItem>();
        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            CollectMatches(folder, searchFilter, shouldIncludeItem, visibleItems);
        }
        else
        {
            foreach (var item in folder.Children)
            {
                if (shouldIncludeItem(item))
                {
                    visibleItems.Add(item);
                }
            }
        }

        visibleItems.Sort(CompareItems);
        return visibleItems;
    }

    private static void CollectMatches(ContentItem folder, string searchFilter, Predicate<ContentItem> shouldIncludeItem, List<ContentItem> matches)
    {
        foreach (var item in folder.Children)
        {
            if (shouldIncludeItem(item) && item.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(item);
            }

            if (item.IsDirectory)
            {
                CollectMatches(item, searchFilter, shouldIncludeItem, matches);
            }
        }
    }

    private static int CompareItems(ContentItem left, ContentItem right)
    {
        if (left.IsDirectory != right.IsDirectory)
        {
            return left.IsDirectory ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}