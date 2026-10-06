using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>
/// One background thread that reads and rewinds the wav files of every <see cref="MusicPlayer"/> track,
/// so the game thread never touches the disk after <see cref="MusicPlayer.Play"/> returned (P13).
/// </summary>
/// <remarks>
/// Ownership: a <see cref="Channel"/> is handed to the worker by <see cref="Acquire"/> together with its
/// reader, and from then on only the worker reads or disposes that reader. The game thread only consumes
/// the channel's ring (single consumer) and never submits from the worker: backend voices stay driven by
/// the game thread alone.
/// The ring is made of fixed slots, each holding one block of at most the player's buffer size, with
/// monotonically increasing write (worker) and read (game thread) counters published with volatile
/// writes. Channels are pooled and never removed from the array the worker walks, so the steady loop
/// allocates nothing and takes no lock; the lock only guards <see cref="Acquire"/> growing the array.
/// </remarks>
internal sealed class StreamingWorker : IDisposable
{
    private const int FreeState = 0;
    private const int ActiveState = 1;
    private const int ReleasingState = 2;
    private const int IdleWaitMilliseconds = 250;
    private const int JoinTimeoutMilliseconds = 2000;

    private readonly int _slotSize;
    private readonly int _slotCount;
    private readonly object _acquireGate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;

    private Channel[] _channels = Array.Empty<Channel>();
    private volatile bool _isStopping;
    private bool _isDisposed;

    public StreamingWorker(int slotSizeInBytes, int slotCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slotSizeInBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(slotCount, 2);

        _slotSize = slotSizeInBytes;
        _slotCount = slotCount;

        _thread = new Thread(ThreadMain)
        {
            Name = "CasaEngine Audio Streaming",
            IsBackground = true,
        };
        _thread.Start();
    }

    /// <summary>True while the worker thread has not exited.</summary>
    public bool IsThreadAlive => _thread.IsAlive;

    /// <summary>
    /// Gives <paramref name="reader"/> to the worker. Game thread only. The reader must not be touched by the
    /// caller afterwards; <see cref="Release"/> makes the worker dispose it.
    /// </summary>
    public Channel Acquire(WavStreamReader reader, bool isLooped, string name)
    {
        Channel channel = null;

        lock (_acquireGate)
        {
            var channels = _channels;
            for (var i = 0; i < channels.Length; i++)
            {
                if (Volatile.Read(ref channels[i].State) == FreeState)
                {
                    channel = channels[i];
                    break;
                }
            }

            if (channel == null)
            {
                channel = new Channel(_slotSize, _slotCount);
                var grown = new Channel[channels.Length + 1];
                Array.Copy(channels, grown, channels.Length);
                grown[channels.Length] = channel;
                Volatile.Write(ref _channels, grown);
            }

            channel.Reset(reader, isLooped, name);
        }

        _wake.Set();
        return channel;
    }

    /// <summary>Asks the worker to dispose the channel's reader and recycle the channel. Game thread only.</summary>
    public void Release(Channel channel)
    {
        Interlocked.CompareExchange(ref channel.State, ReleasingState, ActiveState);
        _wake.Set();
    }

    /// <summary>Tells the worker that ring space was freed.</summary>
    public void NotifySpaceFreed() => _wake.Set();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isStopping = true;
        _wake.Set();

        if (_thread != Thread.CurrentThread && !_thread.Join(JoinTimeoutMilliseconds))
        {
            Logs.WriteWarning("Audio: the streaming worker did not stop within the timeout; its readers are left to the thread.");
            return;
        }

        // The thread ended: nothing can touch the readers any more.
        var channels = Volatile.Read(ref _channels);
        for (var i = 0; i < channels.Length; i++)
        {
            channels[i].DisposeReader();
        }

        _wake.Dispose();
    }

    private void ThreadMain()
    {
        while (!_isStopping)
        {
            var didWork = false;
            var channels = Volatile.Read(ref _channels);

            for (var i = 0; i < channels.Length; i++)
            {
                didWork |= channels[i].Service();
            }

            if (!didWork)
            {
                _wake.WaitOne(IdleWaitMilliseconds);
            }
        }
    }

    /// <summary>One track's ring and reader. Producer side is the worker, consumer side the game thread.</summary>
    internal sealed class Channel
    {
        private readonly int _slotSize;
        private readonly int _slotCount;
        private readonly byte[] _ring;
        private readonly int[] _slotLength;
        private readonly long[] _slotEndPosition;

        internal int State;

        private WavStreamReader _reader;
        private bool _isLooped;
        private string _name;
        private int _writeCount;
        private int _readCount;
        private int _endState; // 0 running, 1 end of stream, 2 failed

        public Channel(int slotSize, int slotCount)
        {
            _slotSize = slotSize;
            _slotCount = slotCount;
            _ring = new byte[slotSize * slotCount];
            _slotLength = new int[slotCount];
            _slotEndPosition = new long[slotCount];
        }

        /// <summary>True once the worker will produce nothing more: end of file (not looped) or read error.</summary>
        public bool IsProducerDone => Volatile.Read(ref _endState) != 0;

        /// <summary>
        /// Consumer side: the next ready block, or false when the ring is empty. The block stays valid until
        /// <see cref="Pop"/>.
        /// </summary>
        public bool TryPeek(out byte[] buffer, out int offset, out int length, out long endPosition)
        {
            var read = _readCount;
            if (read == Volatile.Read(ref _writeCount))
            {
                buffer = null;
                offset = 0;
                length = 0;
                endPosition = 0;
                return false;
            }

            var slot = read % _slotCount;
            buffer = _ring;
            offset = slot * _slotSize;
            length = _slotLength[slot];
            endPosition = _slotEndPosition[slot];
            return true;
        }

        /// <summary>Consumer side: frees the block returned by <see cref="TryPeek"/>.</summary>
        public void Pop() => Volatile.Write(ref _readCount, _readCount + 1);

        internal void Reset(WavStreamReader reader, bool isLooped, string name)
        {
            _reader = reader;
            _isLooped = isLooped;
            _name = name;
            _writeCount = 0;
            _readCount = 0;
            Volatile.Write(ref _endState, 0);
            Volatile.Write(ref State, ActiveState);
        }

        internal void DisposeReader()
        {
            var reader = _reader;
            _reader = null;
            reader?.Dispose();
        }

        /// <summary>Worker thread. Returns true when it did something that may need another pass.</summary>
        internal bool Service()
        {
            var state = Volatile.Read(ref State);

            if (state == FreeState)
            {
                return false;
            }

            if (state == ReleasingState)
            {
                DisposeReader();
                Volatile.Write(ref State, FreeState);
                return true;
            }

            if (Volatile.Read(ref _endState) != 0)
            {
                return false;
            }

            var didWork = false;

            try
            {
                while (Volatile.Read(ref _writeCount) - Volatile.Read(ref _readCount) < _slotCount
                       && Volatile.Read(ref State) == ActiveState)
                {
                    var slot = _writeCount % _slotCount;
                    var read = _reader.Read(_ring, slot * _slotSize, _slotSize);

                    if (read == 0)
                    {
                        if (!_isLooped)
                        {
                            Volatile.Write(ref _endState, 1);
                            return true;
                        }

                        _reader.Rewind();
                        read = _reader.Read(_ring, slot * _slotSize, _slotSize);

                        if (read == 0)
                        {
                            Volatile.Write(ref _endState, 1);
                            return true;
                        }
                    }

                    _slotLength[slot] = read;
                    _slotEndPosition[slot] = _reader.Position;
                    Volatile.Write(ref _writeCount, _writeCount + 1);
                    didWork = true;
                }
            }
            catch (Exception exception)
            {
                Logs.WriteError($"Audio: reading the streamed music '{_name}' failed, the track is stopped. {exception.GetType().Name}: {exception.Message}");
                Volatile.Write(ref _endState, 2);
                return true;
            }

            return didWork;
        }
    }
}
