using System.Text;
using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T1.1: messages, bounded lines and untrusted discovery files.</summary>
public sealed class RemoteProtocolTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-protocol-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Request_RoundTripsThroughALine()
    {
        var parameters = RemoteMessageCodec.ToElement(new { entity_id = "abc", value = 3 });
        byte[] line = RemoteMessageCodec.Encode(new RemoteRequest { Id = 7, Method = "entity.get", Params = parameters });

        Assert.Equal((byte)'\n', line[^1]);
        Assert.True(RemoteMessageCodec.TryDecodeRequest(Encoding.UTF8.GetString(line, 0, line.Length - 1), out RemoteRequest request, out _));
        Assert.Equal(7, request.Id);
        Assert.Equal("entity.get", request.Method);
        Assert.Equal("abc", request.Params!.Value.GetProperty("entity_id").GetString());
    }

    [Fact]
    public void Response_RoundTrips_SuccessAndError()
    {
        byte[] success = RemoteMessageCodec.Encode(RemoteResponse.Success(3, RemoteMessageCodec.ToElement(new RemoteHelloResult { ProtocolVersion = 1, Token = "t" })));
        byte[] failure = RemoteMessageCodec.Encode(RemoteResponse.Failure(4, RemoteError.Refused(RemoteErrorReasons.PlayModeActive, "Stop play first.")));

        Assert.True(RemoteMessageCodec.TryDecodeResponse(RemoteMessageCodec.Describe(success), out RemoteResponse ok));
        Assert.Equal("t", RemoteMessageCodec.FromElement<RemoteHelloResult>(ok.Result!.Value)!.Token);
        Assert.Contains("\"protocol_version\":1", RemoteMessageCodec.Describe(success));

        Assert.True(RemoteMessageCodec.TryDecodeResponse(RemoteMessageCodec.Describe(failure), out RemoteResponse error));
        Assert.Equal(4, error.Id);
        Assert.Equal(RemoteErrorCodes.CommandRefused, error.Error!.Code);
        Assert.Equal(RemoteErrorReasons.PlayModeActive, error.Error.Data!.Reason);
    }

    [Theory]
    [InlineData("{ not json", RemoteErrorCodes.ParseError)]
    [InlineData("[1, 2]", RemoteErrorCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"x\"}", RemoteErrorCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"x\"}", RemoteErrorCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"\"}", RemoteErrorCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"x\",\"params\":[1]}", RemoteErrorCodes.InvalidParams)]
    public void InvalidRequests_GiveAnErrorResponse_WithoutThrowing(string line, int expectedCode)
    {
        Assert.False(RemoteMessageCodec.TryDecodeRequest(line, out _, out RemoteResponse error));
        Assert.Equal(expectedCode, error.Error!.Code);
    }

    [Fact]
    public async Task LineReader_ReadsLines_AndStripsCarriageReturns()
    {
        var reader = new RemoteLineReader(new MemoryStream(Encoding.UTF8.GetBytes("first\r\nsecond\néè\n")));

        Assert.Equal(new RemoteLine(RemoteLineStatus.Line, "first"), await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal(new RemoteLine(RemoteLineStatus.Line, "second"), await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal(new RemoteLine(RemoteLineStatus.Line, "éè"), await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal(RemoteLineStatus.EndOfStream, (await reader.ReadLineAsync(CancellationToken.None)).Status);
    }

    /// <summary>A line over the limit is reported and skipped; the reader keeps working on the next line.</summary>
    [Fact]
    public async Task LineReader_LineOverTheLimit_IsRejected_AndTheNextLineIsRead()
    {
        var payload = new MemoryStream();
        payload.Write(new byte[RemoteProtocol.MaxMessageBytes + 1].Select(_ => (byte)'a').ToArray());
        payload.Write("\nnext\n"u8);
        payload.Position = 0;
        var reader = new RemoteLineReader(payload);

        Assert.Equal(RemoteLineStatus.TooLong, (await reader.ReadLineAsync(CancellationToken.None)).Status);
        Assert.Equal(new RemoteLine(RemoteLineStatus.Line, "next"), await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LineReader_LineAtTheLimit_IsAccepted()
    {
        var reader = new RemoteLineReader(new MemoryStream(Encoding.UTF8.GetBytes("abcd\nabcde\n")), maxLineBytes: 4);

        Assert.Equal("abcd", (await reader.ReadLineAsync(CancellationToken.None)).Text);
        Assert.Equal(RemoteLineStatus.TooLong, (await reader.ReadLineAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public void Discovery_WrittenRecord_IsFound_WhenItsProcessRuns()
    {
        RemoteDiscoveryRecord record = CreateRecord(1234, startTicks: 500);
        RemoteDiscovery.Write(_directory, record);

        var found = Assert.Single(RemoteDiscovery.ReadAll(_directory, pid => pid == 1234 ? 500 : null));

        Assert.Equal(record.PipeName, found.PipeName);
        Assert.Equal(record.Token, found.Token);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    /// <summary>Security review S4: a forged or stale discovery file is ignored.</summary>
    [Theory]
    [InlineData("\\\\other\\pipe\\x")]
    [InlineData("casaengine-editor-xyz")]
    [InlineData("casaengine-editor-0123456789abcdef0123456789abcdef\\..\\x")]
    [InlineData("")]
    public void Discovery_ForgedPipeName_IsIgnored(string pipeName)
    {
        WriteRaw(1234, new RemoteDiscoveryRecord { ProcessId = 1234, ProcessStartUtcTicks = 500, PipeName = pipeName, Token = RemoteDiscovery.CreateToken() });

        Assert.Empty(RemoteDiscovery.ReadAll(_directory, _ => 500));
    }

    [Fact]
    public void Discovery_DeadProcessOrOtherStartTime_IsIgnored()
    {
        RemoteDiscovery.Write(_directory, CreateRecord(1234, startTicks: 500));

        Assert.Empty(RemoteDiscovery.ReadAll(_directory, _ => null));
        Assert.Empty(RemoteDiscovery.ReadAll(_directory, _ => 500 + TimeSpan.FromMinutes(1).Ticks));
    }

    [Fact]
    public void Discovery_FileNameNotMatchingTheProcess_OrBrokenJson_IsIgnored()
    {
        WriteRaw(99, CreateRecord(1234, startTicks: 500));
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "77.json"), "{ broken");

        Assert.Empty(RemoteDiscovery.ReadAll(_directory, _ => 500));
    }

    [Fact]
    public void Discovery_Delete_RemovesTheRecord()
    {
        RemoteDiscovery.Write(_directory, CreateRecord(1234, startTicks: 500));

        RemoteDiscovery.Delete(_directory, 1234);

        Assert.Empty(RemoteDiscovery.ReadAll(_directory, _ => 500));
    }

    [Fact]
    public void PipeNames_HaveTheEditorForm()
    {
        string name = RemoteDiscovery.CreatePipeName();

        Assert.True(RemoteDiscovery.IsValidPipeName(name));
        Assert.NotEqual(name, RemoteDiscovery.CreatePipeName());
        Assert.False(RemoteDiscovery.IsValidPipeName(name.ToUpperInvariant()));
    }

    private static RemoteDiscoveryRecord CreateRecord(int processId, long startTicks) => new()
    {
        ProcessId = processId,
        ProcessStartUtcTicks = startTicks,
        PipeName = RemoteDiscovery.CreatePipeName(),
        Token = RemoteDiscovery.CreateToken(),
        ProjectPath = "C:/projects/sample",
    };

    private void WriteRaw(int fileProcessId, RemoteDiscoveryRecord record)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(RemoteDiscovery.GetFilePath(_directory, fileProcessId), JsonSerializer.SerializeToUtf8Bytes(record, RemoteProtocol.JsonOptions));
    }
}
