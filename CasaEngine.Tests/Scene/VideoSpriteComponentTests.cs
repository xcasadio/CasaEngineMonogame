using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.Video;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The world video component (ADR-0130) without a graphics device: its JSON in both directions, the malformed fields, the
/// creation failures that happen before a player exists, the release of what was acquired, the hidden start and the
/// world loading path. The asset catalogue is global state, so this class runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class VideoSpriteComponentTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d1000000-0000-4000-8000-000000000001");
    private static readonly Guid SpriteLikeId = Guid.Parse("d1000000-0000-4000-8000-000000000002");
    private const string VideoName = "unit-film";

    private readonly List<string> _files = new();

    public VideoSpriteComponentTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "unit-film.mdecvideo", AssetType = "mdecvideo" });
        AssetCatalog.AddInternal(new AssetInfo(SpriteLikeId) { Name = "unit-sheet", FileName = "unit-sheet.sprite", AssetType = "sprite" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
        foreach (var file in _files)
        {
            File.Delete(file);
        }
    }

    private WorldVideoTestFiles.Loader NewLoader()
    {
        var path = WorldVideoTestFiles.Write(3, 10, 1);
        _files.Add(path);
        return new WorldVideoTestFiles.Loader(path);
    }

    private static JObject SaveEntity(Entity entity)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static Entity VideoEntity(string video) => new() { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = video } };

    // ---- JSON ----

    [Fact]
    public void SaveThenLoad_RoundTripsTheVideoAndTheColour_AndKeepsTheNameAtLoad()
    {
        var entity = VideoEntity(VideoName);
        ((VideoSpriteComponent)entity.RootComponent!).Color = new Color(10, 20, 30, 40);

        var node = SaveEntity(entity);
        var loaded = new Entity();
        loaded.Load(node);

        var component = Assert.IsType<VideoSpriteComponent>(loaded.RootComponent);
        Assert.Equal(VideoName, component.VideoName);
        Assert.Equal(new Color(10, 20, 30, 40), component.Color);
        Assert.False(component.HasPlayer);
        Assert.Equal("VideoSpriteComponent", (string?)node["root_component"]!["type"]);
        Assert.Equal(VideoName, (string?)node["root_component"]!["video"]);
        Assert.Equal(10, (int?)node["root_component"]!["color"]!["r"]);
    }

    [Fact]
    public void Save_OfAWhiteComponent_WritesNoColour_AndLoadGivesWhite()
    {
        var node = SaveEntity(VideoEntity(VideoName));
        Assert.Null(node["root_component"]!["color"]);

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(Color.White, Assert.IsType<VideoSpriteComponent>(loaded.RootComponent).Color);
    }

    [Fact]
    public void Load_OfAVideoGivenByIdentifier_KeepsTheText()
    {
        var node = SaveEntity(VideoEntity(VideoName));
        node["root_component"]!["video"] = VideoId.ToString();

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(VideoId.ToString(), Assert.IsType<VideoSpriteComponent>(loaded.RootComponent).VideoName);
    }

    [Fact]
    public void Load_OfAMissingVideo_NamesTheComponentAndTheField()
    {
        var node = SaveEntity(VideoEntity(VideoName));
        ((JObject)node["root_component"]!).Remove("video");

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("VideoSpriteComponent", error.Message);
        Assert.Contains("'video'", error.Message);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    [InlineData("true")]
    [InlineData("[\"a\"]")]
    public void Load_OfAMalformedVideo_NamesTheComponentTheFieldAndTheValue(string video)
    {
        var node = SaveEntity(VideoEntity(VideoName));
        node["root_component"]!["video"] = JToken.Parse(video);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("VideoSpriteComponent", error.Message);
        Assert.Contains("'video'", error.Message);
        Assert.Contains(JToken.Parse(video).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    [Theory]
    [InlineData("{\"r\":1,\"g\":2,\"b\":3}")]
    [InlineData("{\"r\":1,\"g\":2,\"b\":3,\"a\":256}")]
    [InlineData("{\"r\":-1,\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("{\"r\":1.5,\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("{\"r\":\"1\",\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("[1,2,3,4]")]
    [InlineData("\"white\"")]
    public void Load_OfAMalformedColour_NamesTheComponentTheFieldAndTheValue(string colour)
    {
        var node = SaveEntity(VideoEntity(VideoName));
        node["root_component"]!["color"] = JToken.Parse(colour);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("VideoSpriteComponent", error.Message);
        Assert.Contains("'color'", error.Message);
        Assert.Contains(JToken.Parse(colour).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    // ---- Creation failures that need no device ----

    [Fact]
    public void InitializeWithWorld_OfAnUnknownName_ThrowsAndNamesTheComponentTheFieldAndTheValue()
    {
        var harness = new WorldVideoHarness(NewLoader());
        var entity = VideoEntity("missing-film");

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains("VideoSpriteComponent", error.Message);
        Assert.Contains("'video'", error.Message);
        Assert.Contains("missing-film", error.Message);
        Assert.Null(error.InnerException);
        Assert.False(((VideoSpriteComponent)entity.RootComponent!).HasPlayer);
    }

    [Fact]
    public void InitializeWithWorld_OfAnAssetOfAnotherType_Throws_AndAcquiresNothing()
    {
        var loader = NewLoader();
        var harness = new WorldVideoHarness(loader);
        var entity = VideoEntity("unit-sheet");

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains("'video'", error.Message);
        Assert.Contains("unit-sheet", error.Message);
        Assert.Contains("sprite", error.Message);
        Assert.Equal(0, loader.Loads);
        Assert.Equal(0, harness.Manager.CollectUnreferenced());
    }

    [Fact]
    public void InitializeWithWorld_WhenTheAssetCannotBeLoaded_ThrowsWithTheOriginalExceptionInside()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.mdecvideo");
        var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(missing));
        var entity = VideoEntity(VideoName);

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains("'video'", error.Message);
        Assert.Contains(VideoName, error.Message);
        Assert.IsAssignableFrom<IOException>(error.InnerException);
        Assert.Equal(0, harness.Manager.CollectUnreferenced());
    }

    [Fact]
    public void InitializeWithWorld_WhenThereIsNoGraphicsDevice_ReleasesTheAssetItAcquired()
    {
        var loader = NewLoader();
        var harness = new WorldVideoHarness(loader);
        var entity = VideoEntity(VideoName);

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains(VideoName, error.Message);
        Assert.Equal(1, loader.Loads);
        // The hold was given back: the asset is pending, and collecting frees it.
        Assert.Equal(1, harness.Manager.CollectUnreferenced());
        Assert.False(((VideoSpriteComponent)entity.RootComponent!).HasPlayer);
    }

    [Fact]
    public void InitializeWithWorld_OfAComponentThatNamesNoVideo_Throws()
    {
        var harness = new WorldVideoHarness(NewLoader());
        var entity = new Entity { RootComponent = new VideoSpriteComponent() };

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains("'video'", error.Message);
    }

    // ---- Hidden start ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitializeWithWorld_HidesTheEntity_WhateverIsVisibleSays(bool writeIsVisible)
    {
        var harness = new WorldVideoHarness(NewLoader());
        var node = SaveEntity(VideoEntity("missing-film"));
        if (writeIsVisible)
        {
            node["is_visible"] = true;
        }

        var entity = new Entity();
        entity.Load(node);
        Assert.True(entity.IsVisible);

        Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.False(entity.IsVisible);
    }

    // ---- Component without a player ----

    [Fact]
    public void Play_WithoutAPlayer_Throws_AndTheOtherCallsDoNothing()
    {
        var harness = new WorldVideoHarness(NewLoader());
        var component = new VideoSpriteComponent { VideoName = VideoName };

        var error = Assert.Throws<InvalidOperationException>(() => component.Play());
        Assert.Contains(VideoName, error.Message);

        component.Stop();
        component.Update(0.1f);
        component.Draw(0f);
        component.Detach();
        component.Dispose();
        component.Dispose();

        Assert.False(component.IsPlaying);
        Assert.False(component.IsEnded);
        Assert.Empty(harness.Queue);
    }

    [Fact]
    public void Clone_KeepsTheNameAndTheColour_AndHasNoPlayer()
    {
        var component = new VideoSpriteComponent { VideoName = VideoName, Color = new Color(1, 2, 3, 4) };

        var clone = component.Clone();

        Assert.NotSame(component, clone);
        Assert.Equal(VideoName, clone.VideoName);
        Assert.Equal(new Color(1, 2, 3, 4), clone.Color);
        Assert.False(clone.HasPlayer);
    }

    // ---- World loading path ----

    [Fact]
    public void WorldLoad_OfAnEntityThatNamesAnUnknownVideo_LogsTheError_AndDoesNotAddTheEntity()
    {
        var harness = new WorldVideoHarness(NewLoader());
        var entity = VideoEntity("missing-film");
        var errors = new List<string>();
        Logs.AddLogger(new CapturingLogger(errors));
        try
        {
            harness.World.AddEntity(entity);
            harness.World.Update(1f / 60f);
        }
        finally
        {
            Logs.Close();
        }

        Assert.DoesNotContain(entity, harness.World.Entities);
        var logged = Assert.Single(errors, message => message.Contains("missing-film"));
        Assert.Contains("VideoSpriteComponent", logged);
        Assert.Equal(0, harness.Manager.CollectUnreferenced());
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _errors;

        public CapturingLogger(List<string> errors) => _errors = errors;

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => _errors.Add(msg);
    }
}
