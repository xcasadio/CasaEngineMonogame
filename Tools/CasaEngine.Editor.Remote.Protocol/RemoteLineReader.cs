using System.Text;

namespace CasaEngine.Editor.Remote.Protocol;

public enum RemoteLineStatus
{
    Line,

    /// <summary>The line exceeded the limit; its bytes were skipped up to the next end of line.</summary>
    TooLong,

    EndOfStream,
}

public readonly record struct RemoteLine(RemoteLineStatus Status, string Text);

/// <summary>
/// Reads newline-terminated UTF-8 lines from a stream, never keeping more than <see cref="MaxLineBytes"/> bytes of
/// one line (ADR-0092): a longer line is reported as <see cref="RemoteLineStatus.TooLong"/> and skipped.
/// Not thread-safe; one reader per connection.
/// </summary>
public sealed class RemoteLineReader
{
    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[4096];
    private readonly MemoryStream _line = new();
    private int _offset;
    private int _count;

    public RemoteLineReader(Stream stream, int maxLineBytes = RemoteProtocol.MaxMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLineBytes, 1);
        _stream = stream;
        MaxLineBytes = maxLineBytes;
    }

    public int MaxLineBytes { get; }

    public async Task<RemoteLine> ReadLineAsync(CancellationToken cancellationToken)
    {
        _line.SetLength(0);
        bool tooLong = false;

        while (true)
        {
            if (_offset == _count)
            {
                _count = await _stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
                _offset = 0;
                if (_count == 0)
                {
                    return new RemoteLine(RemoteLineStatus.EndOfStream, string.Empty);
                }
            }

            int newline = Array.IndexOf(_buffer, (byte)'\n', _offset, _count - _offset);
            int end = newline < 0 ? _count : newline;
            int length = end - _offset;

            if (!tooLong)
            {
                if (_line.Length + length > MaxLineBytes)
                {
                    tooLong = true;
                    _line.SetLength(0);
                }
                else
                {
                    _line.Write(_buffer, _offset, length);
                }
            }

            _offset = newline < 0 ? _count : newline + 1;
            if (newline < 0)
            {
                continue;
            }

            if (tooLong)
            {
                return new RemoteLine(RemoteLineStatus.TooLong, string.Empty);
            }

            int byteCount = (int)_line.Length;
            byte[] bytes = _line.GetBuffer();
            if (byteCount > 0 && bytes[byteCount - 1] == (byte)'\r')
            {
                byteCount--;
            }

            return new RemoteLine(RemoteLineStatus.Line, Encoding.UTF8.GetString(bytes, 0, byteCount));
        }
    }
}
