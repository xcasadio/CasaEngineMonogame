using System.Collections.Generic;
using System.ComponentModel;
using CasaEngine.Editor.ContentBrowser.Models;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public class ContentItemTests
{
    [Fact]
    public void Constructor_DeducesModelTypeFromExtension()
    {
        var item = new ContentItem("D:/Project/character.staticModel", false);

        Assert.Equal("character.staticModel", item.Name);
        Assert.Equal(".staticModel", item.Extension);
        Assert.Equal(ContentItemType.Model, item.Type);
    }

    [Fact]
    public void UpdatePath_RefreshesDerivedPropertiesAndRaisesNotifications()
    {
        var item = new ContentItem("D:/Project/texture.png", false);
        var changedProperties = new List<string>();
        item.PropertyChanged += OnPropertyChanged;

        item.UpdatePath("D:/Project/material.material");

        Assert.Equal("material.material", item.Name);
        Assert.Equal(".material", item.Extension);
        Assert.Equal(ContentItemType.Material, item.Type);
        Assert.Contains(nameof(ContentItem.FullPath), changedProperties);
        Assert.Contains(nameof(ContentItem.Name), changedProperties);
        Assert.Contains(nameof(ContentItem.Extension), changedProperties);
        Assert.Contains(nameof(ContentItem.Type), changedProperties);
        return;

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
            {
                changedProperties.Add(e.PropertyName);
            }
        }
    }
}