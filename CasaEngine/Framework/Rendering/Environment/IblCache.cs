using System.Diagnostics;
using CasaEngine.Core.Logging;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Cache of the image-based-lighting textures (ADR-0110): for every specular environment cube it holds the diffuse irradiance
/// cube and the prefiltered specular atlas generated from it by <see cref="IblCubeGenerator"/>, plus the BRDF table shared by
/// all of them.
/// <para/>
/// The key is the instance of the source cube. A shader binder that finds the textures missing registers a request with
/// <see cref="TryGet"/>; <see cref="Flush"/> executes the requests, and the render pipeline calls it at the start of
/// <see cref="RenderPipeline.Render"/>, before any view and with no view target bound (decision P9). Nothing is ever generated
/// while the binders run or while the environment is resolved, so the resolved environment (cached per view) never holds a
/// half-built result. The textures of a source that has been disposed are released at the next <see cref="Flush"/>; a source
/// that is never disposed keeps its entry until the cache is disposed (bounded by the number of distinct cubes of the game).
/// </summary>
public sealed class IblCache : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<TextureCube, Entry> _entries = new();
    private readonly List<TextureCube> _pending = new();
    private readonly List<TextureCube> _scratch = new();
    private Texture2D _brdfTable;
    private bool _generatorMissingLogged;
    private bool _disposed;

    private sealed class Entry
    {
        public bool SourceIsLinear;

        // True once the cube is used as an environment cube (irradiance wanted); a reflection probe cube only wants its atlas.
        public bool WantsIrradiance;
        public bool Queued;
        public TextureCube Irradiance;
        public Texture2D Atlas;
        public bool Failed;
    }

    public IblCache(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        _graphicsDevice = graphicsDevice;
    }

    /// <summary>Number of generations done since the creation (one per source: irradiance and atlas together).</summary>
    public int GenerationCount { get; private set; }

    /// <summary>
    /// True while every generation so far ran from <see cref="Flush"/> outside the view loop of the pipeline
    /// (<see cref="ViewLoopActive"/> false at that time). Meant for tests and the demos.
    /// </summary>
    public bool GeneratedOutsideViewLoop { get; private set; } = true;

    /// <summary>Set by the render pipeline while it renders its views.</summary>
    internal bool ViewLoopActive { get; set; }

    /// <summary>Number of sources that currently have an entry (generated, pending or failed).</summary>
    public int EntryCount => _entries.Count;

    /// <summary>Number of requests waiting for the next <see cref="Flush"/>.</summary>
    public int PendingRequestCount => _pending.Count;

    /// <summary>Duration of the CPU integration and upload of the BRDF table, in milliseconds (0 before it exists).</summary>
    public double BrdfTableMilliseconds { get; private set; }

    /// <summary>The BRDF table (<see cref="BrdfLookupTable"/>), created with the first generation; null before.</summary>
    public Texture2D BrdfTable => _brdfTable;

    /// <summary>
    /// The generated textures of <paramref name="source"/>. When they exist, returns true. Otherwise returns false and, the first
    /// time, registers a request that the next <see cref="Flush"/> executes. <paramref name="sourceIsLinear"/> is the
    /// <c>EnvironmentCubeIsLinear</c> value of the source, kept with the request.
    /// </summary>
    public bool TryGet(TextureCube source, bool sourceIsLinear, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdfTable)
    {
        if (!_disposed && source is not null && !source.IsDisposed)
        {
            if (_entries.TryGetValue(source, out Entry entry))
            {
                if (entry.Irradiance is not null && !entry.Irradiance.IsDisposed
                    && entry.Atlas is not null && !entry.Atlas.IsDisposed
                    && _brdfTable is not null && !_brdfTable.IsDisposed)
                {
                    irradiance = entry.Irradiance;
                    atlas = entry.Atlas;
                    brdfTable = _brdfTable;
                    return true;
                }

                // A cube first met as a reflection probe (atlas only) that is now used as an environment cube needs its irradiance too.
                if (!entry.WantsIrradiance)
                {
                    entry.WantsIrradiance = true;
                    Enqueue(source, entry);
                }
            }
            else
            {
                var added = new Entry { SourceIsLinear = sourceIsLinear, WantsIrradiance = true };
                _entries.Add(source, added);
                Enqueue(source, added);
            }
        }

        irradiance = null;
        atlas = null;
        brdfTable = null;
        return false;
    }

    /// <summary>
    /// The prefiltered atlas of a reflection probe cube (T7.1, ADR-0110): same generation and same queue as <see cref="TryGet"/>,
    /// but only the atlas is generated, the irradiance of a probe is never used. When the atlas exists, returns true; otherwise
    /// returns false and, the first time, registers a request that the next <see cref="Flush"/> executes (the shader samples the
    /// unprefiltered cube until then). <paramref name="sourceIsLinear"/> is kept with the request.
    /// </summary>
    public bool TryGetPrefilteredAtlas(TextureCube source, bool sourceIsLinear, out Texture2D atlas)
    {
        if (!_disposed && source is not null && !source.IsDisposed)
        {
            if (_entries.TryGetValue(source, out Entry entry))
            {
                if (entry.Atlas is not null && !entry.Atlas.IsDisposed)
                {
                    atlas = entry.Atlas;
                    return true;
                }
            }
            else
            {
                var added = new Entry { SourceIsLinear = sourceIsLinear };
                _entries.Add(source, added);
                Enqueue(source, added);
            }
        }

        atlas = null;
        return false;
    }

    private void Enqueue(TextureCube source, Entry entry)
    {
        if (entry.Queued)
        {
            return;
        }

        entry.Queued = true;
        _pending.Add(source);
    }

    /// <summary>
    /// Releases the textures of the disposed sources, then executes the pending requests with <paramref name="generator"/>.
    /// Without a generator (the convolution effect is not loaded) nothing is generated and one warning is logged; the requests
    /// stay registered. Must be called with no view target bound.
    /// </summary>
    public void Flush(IblCubeGenerator generator)
    {
        if (_disposed)
        {
            return;
        }

        ReleaseDisposedSources();

        if (_pending.Count == 0)
        {
            return;
        }

        if (generator is null)
        {
            if (!_generatorMissingLogged)
            {
                _generatorMissingLogged = true;
                Logs.WriteWarning("[IblCache] The IblConvolution effect is not loaded: the image-based lighting is not generated and the PBR ambient stays flat.");
            }

            return;
        }

        for (int i = 0; i < _pending.Count; i++)
        {
            TextureCube source = _pending[i];
            if (source.IsDisposed || !_entries.TryGetValue(source, out Entry entry) || entry.Failed)
            {
                continue;
            }

            Generate(generator, source, entry);
        }

        for (int i = 0; i < _pending.Count; i++)
        {
            if (_entries.TryGetValue(_pending[i], out Entry queued))
            {
                queued.Queued = false;
            }
        }

        _pending.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Logs.WriteInfo($"[IblCache] Closed after {GenerationCount} generation(s), {_entries.Count} entry(ies) released, all outside the view loop: {GeneratedOutsideViewLoop}.");
        foreach (Entry entry in _entries.Values)
        {
            ReleaseEntry(entry);
        }

        _entries.Clear();
        _pending.Clear();
        _brdfTable?.Dispose();
        _brdfTable = null;
    }

    private void Generate(IblCubeGenerator generator, TextureCube source, Entry entry)
    {
        try
        {
            if (_brdfTable is null)
            {
                long brdfStart = Stopwatch.GetTimestamp();
                _brdfTable = BrdfLookupTable.CreateTexture(_graphicsDevice);
                BrdfTableMilliseconds = Stopwatch.GetElapsedTime(brdfStart).TotalMilliseconds;
                Logs.WriteInfo($"[IblCache] Computed the BRDF table ({BrdfLookupTable.Size} x {BrdfLookupTable.Size}, {BrdfLookupTable.SampleCount} samples per texel) in {BrdfTableMilliseconds:F1} ms.");
            }

            bool generateIrradiance = entry.WantsIrradiance && entry.Irradiance is null;
            double irradianceMilliseconds = 0.0;
            if (generateIrradiance)
            {
                entry.Irradiance = generator.GenerateIrradiance(source, entry.SourceIsLinear);
                irradianceMilliseconds = generator.LastGenerationMilliseconds;
            }

            double atlasMilliseconds = 0.0;
            if (entry.Atlas is null)
            {
                entry.Atlas = generator.GeneratePrefiltered(source, entry.SourceIsLinear);
                atlasMilliseconds = generator.LastGenerationMilliseconds;
            }

            GenerationCount++;
            if (ViewLoopActive)
            {
                GeneratedOutsideViewLoop = false;
            }

            Logs.WriteInfo(generateIrradiance
                ? $"[IblCache] Generated the IBL textures of an environment cube: irradiance {irradianceMilliseconds:F1} ms, prefiltered atlas {atlasMilliseconds:F1} ms."
                : $"[IblCache] Generated the prefiltered atlas of a reflection probe cube: {atlasMilliseconds:F1} ms.");
        }
        catch (Exception exception)
        {
            // One failure per source, never retried: the shader keeps its flat ambient and nothing is thrown every frame.
            entry.Failed = true;
            ReleaseEntry(entry);
            Logs.WriteError($"[IblCache] The generation of the IBL textures failed, the PBR ambient stays flat for this cube: {exception.Message}");
        }
    }

    private void ReleaseDisposedSources()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        _scratch.Clear();
        foreach (KeyValuePair<TextureCube, Entry> pair in _entries)
        {
            if (pair.Key.IsDisposed)
            {
                _scratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < _scratch.Count; i++)
        {
            TextureCube source = _scratch[i];
            if (_entries.Remove(source, out Entry entry))
            {
                ReleaseEntry(entry);
            }

            _pending.Remove(source);
        }

        _scratch.Clear();
    }

    private static void ReleaseEntry(Entry entry)
    {
        entry.Irradiance?.Dispose();
        entry.Irradiance = null;
        entry.Atlas?.Dispose();
        entry.Atlas = null;
    }
}
