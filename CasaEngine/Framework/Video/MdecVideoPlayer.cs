using CasaEngine.Core.Logging;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using CasaEngine.Framework.Video.Mdec;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;

namespace CasaEngine.Framework.Video;

/// <summary>
/// Plays a <see cref="MdecVideoAsset"/> into one <see cref="Texture2D"/> of the displayed size (ADR-0089). It is the
/// host side of MGUI's <see cref="IUIVideo"/>: the element advances it with the elapsed time, draws
/// <see cref="CurrentImage"/>, and reads <see cref="IsEnded"/>.
/// <para/>
/// The player reads the frames of the container through a <see cref="FileStream"/> into one buffer sized by the
/// longest frame, decodes on the calling thread into one reused RGBA buffer, and copies it to the texture with a single
/// <see cref="Texture2D.SetData{T}(T[])"/> per new frame. Nothing is allocated after construction.
/// <para/>
/// Clock: the time handed to <see cref="Advance"/> is summed in integer ticks. Frame <c>k</c> is shown while the elapsed
/// time is in <c>[k, k + 1)</c> periods of <c>denominator / numerator</c> seconds, computed without rounding the period.
/// A frame that is late is not decoded: only the frame due now is. Frame 0 is decoded by the constructor. The video
/// ends when the elapsed time covers every frame; the last frame stays displayed. A frame that cannot be read or
/// decoded ends the video and is logged once.
/// </summary>
public sealed class MdecVideoPlayer : IUIVideo
{
    private readonly MdecVideoAsset _asset;
    private readonly Texture2D _texture;
    private readonly CasaMonoGameImageResource _image;
    private readonly FileStream _stream;
    private readonly byte[] _frameBuffer;
    private readonly ushort[] _words;
    private readonly byte[] _rgba;
    private readonly int _macroblockCount;
    private readonly Action<MdecVideoPlayer> _disposed;

    // Elapsed time of the playback in ticks, kept at the end of the video once it is reached.
    private long _elapsedTicks;
    private readonly long _endTicks;
    private int _currentFrame = -1;
    private bool _errorLogged;
    private bool _isDisposed;

    /// <param name="graphicsDevice">Device that owns the texture.</param>
    /// <param name="asset">The loaded container; its file is opened here and kept open until <see cref="Dispose"/>.</param>
    /// <param name="disposed">Optional notification, called once when this player is disposed.</param>
    public MdecVideoPlayer(GraphicsDevice graphicsDevice, MdecVideoAsset asset, Action<MdecVideoPlayer> disposed = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(asset);

        _asset = asset;
        _disposed = disposed;
        _macroblockCount = MdecFrameDecoder.MacroblockCount(asset.Width, asset.Height);
        // The smallest tick count that covers every frame: ceil(frameCount * denominator * ticksPerSecond / numerator).
        var totalTicksTimesRate = (Int128)asset.FrameCount * asset.FrameRateDenominator * TimeSpan.TicksPerSecond;
        _endTicks = (long)((totalTicksTimesRate + asset.FrameRateNumerator - 1) / asset.FrameRateNumerator);

        var maxFrameLength = asset.MaxFrameLength;
        _frameBuffer = new byte[maxFrameLength];
        _words = new ushort[MdecBitstreamDecoder.MaxWordCount(maxFrameLength)];
        _rgba = new byte[asset.Width * asset.Height * 4];

        _texture = new Texture2D(graphicsDevice, asset.Width, asset.Height, false, SurfaceFormat.Color);
        try
        {
            _image = new CasaMonoGameImageResource(_texture);
            _stream = new FileStream(asset.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);

            ShowFrame(0);
        }
        catch
        {
            // The caller never gets the player, so nobody else can release what was created.
            _stream?.Dispose();
            _texture.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The texture, updated in place: the same instance for the whole life of the player. Null once disposed, so that
    /// an element still holding the player draws nothing instead of a disposed texture.
    /// </summary>
    public IUIImageResource CurrentImage => _isDisposed ? null : _image;

    /// <summary>
    /// The texture itself, for a host that draws it as a world sprite (ADR-0091): the same instance for the whole life
    /// of the player, updated in place. Null once disposed.
    /// </summary>
    internal Texture2D Texture => _isDisposed ? null : _texture;

    /// <summary>Always null: the whole texture is the picture.</summary>
    public Rectangle? CurrentSourceRect => null;

    /// <summary>True once the video reached its end or failed. Disposing the player does not change it.</summary>
    public bool IsEnded { get; private set; }

    /// <summary>Index of the frame currently in the texture.</summary>
    public int CurrentFrameIndex => _currentFrame;

    /// <summary>Number of frames decoded since construction (a late frame that was skipped is not counted).</summary>
    public int DecodedFrameCount { get; private set; }

    public void Advance(TimeSpan elapsed)
    {
        if (_isDisposed || IsEnded || elapsed.Ticks <= 0)
        {
            return;
        }

        _elapsedTicks += elapsed.Ticks;
        if (_elapsedTicks >= _endTicks)
        {
            // The last frame stays displayed. If the last frame was never shown (a late jump), show it first.
            _elapsedTicks = _endTicks;
            ShowFrame(_asset.FrameCount - 1);
            IsEnded = true;
            return;
        }

        // frame = floor(elapsed * numerator / (denominator * ticksPerSecond)); Int128 cannot overflow here.
        var frame = (int)((Int128)_elapsedTicks * _asset.FrameRateNumerator
            / ((Int128)_asset.FrameRateDenominator * TimeSpan.TicksPerSecond));
        ShowFrame(frame);
    }

    public void Restart()
    {
        if (_isDisposed)
        {
            return;
        }

        _elapsedTicks = 0;
        IsEnded = false;
        ShowFrame(0);
    }

    private void ShowFrame(int frame)
    {
        if (frame == _currentFrame || _isDisposed)
        {
            return;
        }

        try
        {
            var offset = _asset.GetFrameOffset(frame);
            var length = _asset.GetFrameLength(frame);
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.ReadExactly(_frameBuffer, 0, length);

            var wordCount = MdecBitstreamDecoder.Decode(_frameBuffer.AsSpan(0, length), frame, _macroblockCount, _words);
            MdecFrameDecoder.Decode(_words.AsSpan(0, wordCount), frame, _asset.Width, _asset.Height, _rgba);
            _texture.SetData(_rgba);

            _currentFrame = frame;
            DecodedFrameCount++;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            IsEnded = true;
            if (!_errorLogged)
            {
                _errorLogged = true;
                Logs.WriteError($"MDEC video '{_asset.FileName}': frame {frame} cannot be played, the video ends here: {exception.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _stream.Dispose();
        _texture.Dispose();
        _disposed?.Invoke(this);
    }
}
