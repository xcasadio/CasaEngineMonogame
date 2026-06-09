using System;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime;

internal enum PreviewWorldUpdateMode
{
    Manual,
    Continuous,
}

internal sealed class PreviewWorldDriverOptions
{
    public required string WorldName { get; init; }

    public PreviewWorldUpdateMode UpdateMode { get; init; } = PreviewWorldUpdateMode.Manual;

    public bool RefreshAfterRebuild { get; init; } = true;

    public bool ClearWorldOnDispose { get; init; } = true;
}

internal sealed class PreviewWorldDriver : IDisposable
{
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly PreviewWorldDriverOptions _options;

    public PreviewWorldDriver(HostedEditorGameAdapter editorRuntime, PreviewWorldDriverOptions options)
    {
        ArgumentNullException.ThrowIfNull(editorRuntime);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorldName);

        _editorRuntime = editorRuntime;
        _options = options;
    }

    public World World { get; private set; }

    public World Rebuild(Action<World> populate)
    {
        ArgumentNullException.ThrowIfNull(populate);

        Clear();

        var world = new World
        {
            Name = _options.WorldName,
        };
        world.LoadContent(_editorRuntime);
        populate(world);

        World = world;
        if (_options.RefreshAfterRebuild)
        {
            RefreshNow();
        }

        return world;
    }

    public void RefreshNow()
    {
        World?.Update(0f);
    }

    public void Tick(GameTime gameTime)
    {
        ArgumentNullException.ThrowIfNull(gameTime);

        if (_options.UpdateMode != PreviewWorldUpdateMode.Continuous || World == null)
        {
            return;
        }

        World.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    public void Clear()
    {
        if (World == null)
        {
            return;
        }

        World.Clear();
        World = null;
    }

    public void Dispose()
    {
        if (_options.ClearWorldOnDispose)
        {
            Clear();
        }
        else
        {
            World = null;
        }
    }
}