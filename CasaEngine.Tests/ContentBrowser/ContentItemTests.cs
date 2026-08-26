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
    public void Constructor_DeducesTileMapTypeFromExtension()
    {
        var item = new ContentItem("D:/Project/maps/world.tileMap", false);

        Assert.Equal("world.tileMap", item.Name);
        Assert.Equal(".tileMap", item.Extension);
        Assert.Equal(ContentItemType.TileMap, item.Type);
    }

    [Theory]
    [InlineData("D:/Project/audio/click.sound", ".sound")]
    [InlineData("D:/Project/audio/click.wav", ".wav")]
    [InlineData("D:/Project/audio/theme.ogg", ".ogg")]
    public void Constructor_DeducesSoundTypeFromExtension(string path, string expectedExtension)
    {
        var item = new ContentItem(path, false);

        Assert.Equal(expectedExtension, item.Extension);
        Assert.Equal(ContentItemType.Sound, item.Type);
    }

    [Fact]
    public void Constructor_DoesNotAdvertiseMp3AsAPlayableSound()
    {
        // MonoGame DesktopGL cannot decode mp3, neither as a sound effect nor as music.
        var item = new ContentItem("D:/Project/audio/theme.mp3", false);

        Assert.Equal(ContentItemType.Unknown, item.Type);
    }

    [Fact]
    public void Constructor_DeducesSpriteTypeFromExtension()
    {
        var item = new ContentItem("D:/Project/sprites/hero.sprite", false);

        Assert.Equal("hero.sprite", item.Name);
        Assert.Equal(".sprite", item.Extension);
        Assert.Equal(ContentItemType.Sprite, item.Type);
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