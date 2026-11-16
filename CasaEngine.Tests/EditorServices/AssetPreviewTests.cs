using System.Buffers.Binary;
using System.Text;
using CasaEngine.EditorServices.AssetPreview;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>Classification and header reading of the read-only asset preview (plan P6), on small synthetic files.</summary>
public sealed class AssetPreviewTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "casa-asset-preview-" + Guid.NewGuid().ToString("N"));

    public AssetPreviewTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
    }

    [Theory]
    [InlineData("a.png", AssetPreviewKind.Image)]
    [InlineData("a.JPG", AssetPreviewKind.Image)]
    [InlineData("a.jpeg", AssetPreviewKind.Image)]
    [InlineData("a.bmp", AssetPreviewKind.Image)]
    [InlineData("a.gif", AssetPreviewKind.Image)]
    [InlineData("a.tga", AssetPreviewKind.Image)]
    [InlineData("a.dds", AssetPreviewKind.CubeMap)]
    [InlineData("a.wav", AssetPreviewKind.Audio)]
    [InlineData("a.ogg", AssetPreviewKind.Audio)]
    [InlineData("a.fnt", AssetPreviewKind.BitmapFont)]
    [InlineData("a.gltf", AssetPreviewKind.Model)]
    [InlineData("a.glb", AssetPreviewKind.Model)]
    public void Classify_KnownExtensionGivesItsKind(string fileName, AssetPreviewKind expected)
    {
        string path = WriteBytes(fileName, new byte[] { 1, 2, 3 });

        Assert.Equal(expected, AssetPreviewClassifier.Classify(path));
    }

    [Theory]
    [InlineData(".entity")]
    [InlineData(".world")]
    [InlineData(".gameMode")]
    [InlineData(".gameplayMode")]
    [InlineData(".buttonsMapping")]
    [InlineData(".texture")]
    [InlineData(".sprite")]
    [InlineData(".anim2d")]
    [InlineData(".tileMap")]
    [InlineData(".tileset")]
    [InlineData(".uiscreen")]
    [InlineData(".screen")]
    [InlineData(".model")]
    [InlineData(".staticModel")]
    [InlineData(".skeleton")]
    [InlineData(".skeletonAnim")]
    [InlineData(".retargetProfile")]
    [InlineData(".material")]
    [InlineData(".environment")]
    [InlineData(".shader")]
    [InlineData(".particle")]
    [InlineData(".cutscene")]
    [InlineData(".dialogue")]
    [InlineData(".sound")]
    [InlineData(".audioMixer")]
    public void Classify_RefusesEveryEngineAssetFormat(string extension)
    {
        string path = WriteBytes("asset" + extension, Encoding.UTF8.GetBytes("{}"));

        Assert.Equal(AssetPreviewKind.None, AssetPreviewClassifier.Classify(path));
        Assert.Equal(AssetPreviewKind.None, AssetPreviewReader.Read(path).Kind);
    }

    [Fact]
    public void Classify_RefusalIgnoresExtensionCase()
    {
        string path = WriteBytes("a.TEXTURE", Encoding.UTF8.GetBytes("{}"));

        Assert.Equal(AssetPreviewKind.None, AssetPreviewClassifier.Classify(path));
    }

    [Theory]
    [InlineData("TrackBeginner.Track")]
    [InlineData("LandscapeHeights.data")]
    [InlineData("Car.CombiModel")]
    public void Classify_AcceptsGameSpecificExtensions(string fileName)
    {
        string path = WriteBytes(fileName, Encoding.UTF8.GetBytes("1 2 3"));

        Assert.Equal(AssetPreviewKind.Text, AssetPreviewClassifier.Classify(path));
    }

    [Fact]
    public void Classify_MissingFileHasNoPreview()
    {
        Assert.Equal(AssetPreviewKind.None, AssetPreviewClassifier.Classify(Path.Combine(_folder, "missing.Track")));
        Assert.Equal(AssetPreviewKind.None, AssetPreviewClassifier.Classify(Path.Combine(_folder, "missing.png")));
        Assert.Equal(AssetPreviewKind.None, AssetPreviewClassifier.Classify(string.Empty));
    }

    [Fact]
    public void Classify_NulByteOrInvalidUtf8IsBinary()
    {
        Assert.Equal(AssetPreviewKind.Binary, AssetPreviewClassifier.Classify(WriteBytes("a.bin", new byte[] { 65, 0, 66 })));
        Assert.Equal(AssetPreviewKind.Binary, AssetPreviewClassifier.Classify(WriteBytes("b.bin", new byte[] { 0xFF, 0xFE, 0xFD })));
    }

    [Fact]
    public void Classify_MultiByteCharacterCutByTheSniffWindowStaysText()
    {
        // 'e acute' is two bytes; put the first one on the last byte of the 8 KB window.
        var bytes = new byte[AssetPreviewClassifier.SniffLength + 10];
        Array.Fill(bytes, (byte)'a');
        bytes[AssetPreviewClassifier.SniffLength - 1] = 0xC3;
        bytes[AssetPreviewClassifier.SniffLength] = 0xA9;

        Assert.Equal(AssetPreviewKind.Text, AssetPreviewClassifier.Classify(WriteBytes("long.txt", bytes)));
    }

    [Fact]
    public void Read_Png_GivesDimensionsFromIhdr()
    {
        var png = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 640);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 480);
        png[24] = 8;
        png[25] = 6;

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.png", png));

        Assert.Equal(AssetPreviewKind.Image, info.Kind);
        Assert.Equal(640, info.Width);
        Assert.Equal(480, info.Height);
        Assert.Equal("640 x 480", Value(info, "Dimensions"));
        Assert.Equal("RGBA", Value(info, "Colour type"));
        Assert.Equal(".png", Value(info, "Extension"));
        Assert.Equal(33, info.FileSize);
        Assert.Contains("33", Value(info, "Size"));
    }

    [Fact]
    public void Read_TruncatedPng_ReportsAProblemWithoutThrowing()
    {
        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.png", new byte[] { 1, 2, 3 }));

        Assert.Equal(AssetPreviewKind.Image, info.Kind);
        Assert.NotNull(info.Problem);
        Assert.Equal(0, info.Width);
    }

    [Fact]
    public void Read_Tga_GivesDimensions()
    {
        var tga = new byte[18];
        tga[2] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(12), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(14), 64);
        tga[16] = 32;

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.tga", tga));

        Assert.Equal(128, info.Width);
        Assert.Equal(64, info.Height);
        Assert.Equal("32", Value(info, "Bits per pixel"));
    }

    [Fact]
    public void Read_Bmp_GivesDimensionsAndUsesTheAbsoluteHeight()
    {
        var bmp = new byte[54];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 30);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), -20);
        BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(28), 24);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.bmp", bmp));

        Assert.Equal(30, info.Width);
        Assert.Equal(20, info.Height);
        Assert.Equal("24", Value(info, "Bits per pixel"));
    }

    [Fact]
    public void Read_Gif_GivesDimensions()
    {
        var gif = new byte[13];
        Encoding.ASCII.GetBytes("GIF89a").CopyTo(gif, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), 17);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8), 9);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.gif", gif));

        Assert.Equal(17, info.Width);
        Assert.Equal(9, info.Height);
    }

    [Fact]
    public void Read_Jpeg_GivesDimensionsFromTheFrameHeader()
    {
        // SOI, an APP0 segment of 4 bytes, then SOF0: length 17, precision 8, height 200, width 300.
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 200, 0x01, 0x2C };
        jpeg.AddRange(new byte[14]);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.jpg", jpeg.ToArray()));

        Assert.Equal(300, info.Width);
        Assert.Equal(200, info.Height);
    }

    [Fact]
    public void Read_JpegWithoutFrameHeader_ReportsSizeUnknown()
    {
        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9, 0, 0, 0, 0 }));

        Assert.Equal(0, info.Width);
        Assert.StartsWith("unknown", Value(info, "Dimensions"));
        Assert.Null(info.Problem);
    }

    [Fact]
    public void Read_DdsCubeMap_GivesSizeMipsFourCcAndCubeFlag()
    {
        byte[] dds = CreateDdsHeader(width: 256, height: 256, mipCount: 1, fourCc: "DXT1", rgbBitCount: 0, caps2: 0xFE00);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("sky.dds", dds));

        Assert.Equal(AssetPreviewKind.CubeMap, info.Kind);
        Assert.True(info.IsCubeMap);
        Assert.Equal(256, info.Width);
        Assert.Equal(256, info.Height);
        Assert.Equal("1", Value(info, "Mip count"));
        Assert.Equal("yes (6 faces)", Value(info, "Cube map"));
        Assert.Equal("DXT1", Value(info, "FourCC"));
    }

    [Fact]
    public void Read_DdsPlainTexture_GivesRgbBitCountAndNoCubeFlag()
    {
        byte[] dds = CreateDdsHeader(width: 64, height: 32, mipCount: 7, fourCc: null, rgbBitCount: 32, caps2: 0);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("t.dds", dds));

        Assert.False(info.IsCubeMap);
        Assert.Equal("7", Value(info, "Mip count"));
        Assert.Equal("no", Value(info, "Cube map"));
        Assert.Equal("32", Value(info, "RGB bit count"));
    }

    [Fact]
    public void Read_Wav_WalksPastAnExtraChunkBeforeData()
    {
        // 1 s of 16-bit stereo at 8000 Hz = 32000 bytes of data, preceded by a 5-byte (odd, padded) LIST chunk.
        byte[] wav = CreateWav(sampleRate: 8000, channels: 2, bitsPerSample: 16, dataLength: 32000, extraChunk: true);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.wav", wav));

        Assert.Equal(AssetPreviewKind.Audio, info.Kind);
        Assert.Equal("PCM", Value(info, "Format"));
        Assert.Equal("8000 Hz", Value(info, "Sample rate"));
        Assert.Equal("2", Value(info, "Channels"));
        Assert.Equal("16", Value(info, "Bits per sample"));
        Assert.Equal("1.000 s", Value(info, "Duration"));
    }

    [Fact]
    public void Read_WavWithADataSizeLargerThanTheFile_UsesWhatIsThere()
    {
        byte[] wav = CreateWav(sampleRate: 1000, channels: 1, bitsPerSample: 8, dataLength: 500, extraChunk: false);
        BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(40), 1_000_000);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("b.wav", wav));

        Assert.Equal("0.500 s", Value(info, "Duration"));
    }

    [Fact]
    public void Read_NotAWav_ReportsAProblem()
    {
        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("c.wav", new byte[40]));

        Assert.NotNull(info.Problem);
    }

    [Fact]
    public void Read_Ogg_GivesSizeOnly()
    {
        byte[] ogg = new byte[100];
        Encoding.ASCII.GetBytes("OggS").CopyTo(ogg, 0);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.ogg", ogg));

        Assert.Equal(AssetPreviewKind.Audio, info.Kind);
        Assert.Equal(".ogg", Value(info, "Extension"));
        Assert.Equal(100, info.FileSize);
        Assert.Null(info.Problem);
        Assert.Equal(3, info.Lines.Count);
    }

    [Fact]
    public void Read_BitmapFont_GivesFacePagesAndCharacterCount()
    {
        const string fnt =
            "info face=\"Game Font\" size=32 bold=0 italic=0 charset=\"\" unicode=1\r\n" +
            "common lineHeight=36 base=29 scaleW=256 scaleH=256 pages=2 packed=0\r\n" +
            "page id=0 file=\"font_0.png\"\r\n" +
            "page id=1 file=\"font_1.png\"\r\n" +
            "chars count=95\r\n" +
            "char id=32 x=0 y=0 width=0 height=0\r\n";

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteText("a.fnt", fnt));

        Assert.Equal(AssetPreviewKind.BitmapFont, info.Kind);
        Assert.Equal("Game Font", Value(info, "Face"));
        Assert.Equal("32", Value(info, "Font size"));
        Assert.Equal("36", Value(info, "Line height"));
        Assert.Equal("95", Value(info, "Characters"));
        Assert.Equal(new[] { "font_0.png", "font_1.png" }, info.PageFiles);
    }

    [Fact]
    public void Read_Gltf_CountsMeshesMaterialsImagesAndNodes()
    {
        const string json =
            "{\"asset\":{\"version\":\"2.0\"},\"meshes\":[{},{}],\"materials\":[{}],\"images\":[{},{},{}],\"nodes\":[{},{},{},{}]}";

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteText("a.gltf", json));

        Assert.Equal(AssetPreviewKind.Model, info.Kind);
        Assert.Equal("2.0", Value(info, "glTF version"));
        Assert.Equal("2", Value(info, "Meshes"));
        Assert.Equal("1", Value(info, "Materials"));
        Assert.Equal("3", Value(info, "Images"));
        Assert.Equal("4", Value(info, "Nodes"));
    }

    [Fact]
    public void Read_Glb_GivesVersionLengthAndCounts()
    {
        byte[] json = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"},\"meshes\":[{}],\"nodes\":[{},{}]}   ");
        var glb = new byte[20 + json.Length];
        Encoding.ASCII.GetBytes("glTF").CopyTo(glb, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)glb.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), (uint)json.Length);
        Encoding.ASCII.GetBytes("JSON").CopyTo(glb, 16);
        json.CopyTo(glb, 20);

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.glb", glb));

        Assert.Equal("2", Value(info, "GLB version"));
        Assert.Contains(glb.Length.ToString(), Value(info, "GLB length"));
        Assert.Equal("1", Value(info, "Meshes"));
        Assert.Equal("2", Value(info, "Nodes"));
        Assert.Equal("0", Value(info, "Materials"));
    }

    [Fact]
    public void Read_MalformedGltf_ReportsAProblemWithoutThrowing()
    {
        AssetPreviewInfo info = AssetPreviewReader.Read(WriteText("bad.gltf", "{ not json"));

        Assert.Equal(AssetPreviewKind.Model, info.Kind);
        Assert.NotNull(info.Problem);
    }

    [Fact]
    public void Read_Text_GivesLineCountEncodingAndFirstLines()
    {
        var builder = new StringBuilder();
        for (int i = 1; i <= 500; i++)
        {
            builder.Append("line ").Append(i).Append("\r\n");
        }

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteText("TrackBeginner.Track", builder.ToString()));

        Assert.Equal(AssetPreviewKind.Text, info.Kind);
        Assert.Equal("500", Value(info, "Lines"));
        Assert.Equal("UTF-8", Value(info, "Encoding"));
        string[] preview = info.TextPreview.Split('\n');
        Assert.Equal(AssetPreviewReader.MaxTextPreviewLines, preview.Length);
        Assert.Equal("line 1", preview[0]);
        Assert.Equal("line 400", preview[^1]);
    }

    [Fact]
    public void Read_TextWithBom_ReportsIt()
    {
        byte[] bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("abc\ndef")).ToArray();

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("a.data", bytes));

        Assert.Equal("UTF-8 with BOM", Value(info, "Encoding"));
        Assert.Equal("2", Value(info, "Lines"));
        Assert.Equal("abc\ndef", info.TextPreview);
    }

    [Fact]
    public void Read_SquareBinary_ReportsAPossibleMap()
    {
        var data = new byte[257 * 257];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251 + 1);
        }

        data[0] = 0;

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("LandscapeHeights.data", data));

        Assert.Equal(AssetPreviewKind.Binary, info.Kind);
        Assert.Equal(257, info.SquareMapSide);
        Assert.Equal("257 x 257, 8 bits per cell", Value(info, "Possible map"));
        Assert.Equal(66049, info.FileSize);
    }

    [Theory]
    [InlineData(66050)]
    [InlineData(1)]
    public void Read_NonSquareBinary_ReportsNoMap(int size)
    {
        var data = new byte[size];
        data[0] = 0;

        AssetPreviewInfo info = AssetPreviewReader.Read(WriteBytes("x.bin", data));

        Assert.Equal(AssetPreviewKind.Binary, info.Kind);
        Assert.Equal(0, info.SquareMapSide);
    }

    [Fact]
    public void Read_BinaryLargerThanTheMaximumSquare_ReportsNoMap()
    {
        // 8193 x 8193 would be 67 MB: the rule is checked on the size, so a sparse file of that length is enough.
        string path = Path.Combine(_folder, "big.bin");
        using (var stream = new FileStream(path, FileMode.Create))
        {
            stream.SetLength(8193L * 8193L);
        }

        Assert.Equal(0, AssetPreviewReader.Read(path).SquareMapSide);
    }

    [Fact]
    public void Downsample_KeepsASmallMapAndShrinksALargeOneWithNearestNeighbour()
    {
        var map = new byte[4 * 4];
        for (int i = 0; i < map.Length; i++)
        {
            map[i] = (byte)i;
        }

        byte[] same = AssetPreviewGrayscale.Downsample(map, 4, 512, out int sameSide);
        Assert.Equal(4, sameSide);
        Assert.Equal(map, same);

        byte[] half = AssetPreviewGrayscale.Downsample(map, 4, 2, out int halfSide);
        Assert.Equal(2, halfSide);
        Assert.Equal(new byte[] { 0, 2, 8, 10 }, half);
    }

    [Fact]
    public void Downsample_RejectsAMapThatIsTooShort()
    {
        Assert.Throws<ArgumentException>(() => AssetPreviewGrayscale.Downsample(new byte[3], 2, 512, out _));
    }

    private static string Value(AssetPreviewInfo info, string label)
    {
        foreach (AssetPreviewLine line in info.Lines)
        {
            if (line.Label == label)
            {
                return line.Value;
            }
        }

        throw new Xunit.Sdk.XunitException($"No line labelled '{label}'. Lines: " + string.Join("; ", info.Lines.Select(l => l.Label + "=" + l.Value)));
    }

    private string WriteBytes(string fileName, byte[] bytes)
    {
        string path = Path.Combine(_folder, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string WriteText(string fileName, string text)
    {
        string path = Path.Combine(_folder, fileName);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private static byte[] CreateDdsHeader(int width, int height, int mipCount, string fourCc, int rgbBitCount, uint caps2)
    {
        var dds = new byte[128];
        Encoding.ASCII.GetBytes("DDS ").CopyTo(dds, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(28), (uint)mipCount);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76), 32);
        if (fourCc != null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 0x4);
            Encoding.ASCII.GetBytes(fourCc).CopyTo(dds, 84);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 0x41);
            BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(88), (uint)rgbBitCount);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(112), caps2);
        return dds;
    }

    private static byte[] CreateWav(int sampleRate, int channels, int bitsPerSample, int dataLength, bool extraChunk)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write((short)bitsPerSample);
        if (extraChunk)
        {
            writer.Write(Encoding.ASCII.GetBytes("LIST"));
            writer.Write(5);
            writer.Write(new byte[] { 1, 2, 3, 4, 5, 0 });
        }

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
        writer.Flush();
        byte[] bytes = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(bytes.Length - 8));
        return bytes;
    }
}
