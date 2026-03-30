using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser;
using CasaEngine.Editor.ContentBrowser.Models;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public sealed class ContentBrowserItemQueryTests
{
    [Fact]
    public void GetVisibleItems_FiltersRecursivelyAndSortsDirectoriesFirst()
    {
        var root = new ContentItem(@"C:\Project\Content", true);
        var textures = new ContentItem(@"C:\Project\Content\Textures", true, root);
        var hero = new ContentItem(@"C:\Project\Content\Textures\hero.png", false, textures);
        var zebra = new ContentItem(@"C:\Project\Content\zebra.txt", false, root);
        var alpha = new ContentItem(@"C:\Project\Content\Alpha", true, root);

        textures.Children.Add(hero);
        root.Children.Add(zebra);
        root.Children.Add(textures);
        root.Children.Add(alpha);

        List<ContentItem> allItems = ContentBrowserItemQuery.GetVisibleItems(root, string.Empty, _ => true);
        Assert.Collection(
            allItems,
            item => Assert.Equal("Alpha", item.Name),
            item => Assert.Equal("Textures", item.Name),
            item => Assert.Equal("zebra.txt", item.Name));

        List<ContentItem> filteredItems = ContentBrowserItemQuery.GetVisibleItems(root, "hero", _ => true);
        var match = Assert.Single(filteredItems);
        Assert.Equal("hero.png", match.Name);
    }
}