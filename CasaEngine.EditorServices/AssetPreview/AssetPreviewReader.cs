using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasaEngine.EditorServices.AssetPreview;

/// <summary>
/// Reads the header of a previewed file (plan P6) and returns the lines the editor shows. Pure: no graphics device, no
/// editor type. A header that cannot be read does not throw: it is reported in <see cref="AssetPreviewInfo.Problem"/>.
/// </summary>
public static class AssetPreviewReader
{
    /// <summary>Number of lines of a text file kept for the preview.</summary>
    public const int MaxTextPreviewLines = 400;

    /// <summary>Longest line kept in a text preview; a longer line is cut.</summary>
    public const int MaxTextPreviewLineLength = 2000;

    /// <summary>Largest side n of a binary file reported as a possible n x n map.</summary>
    public const int MaxSquareMapSide = 8192;

    private const int HeaderReadLength = 64 * 1024;

    /// <summary>
    /// Reads <paramref name="fullPath"/>. Returns an info with kind <see cref="AssetPreviewKind.None"/> when the classifier
    /// refuses the file (engine asset format, missing or unreadable file).
    /// </summary>
    public static AssetPreviewInfo Read(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        AssetPreviewKind kind = AssetPreviewClassifier.Classify(fullPath);
        if (kind == AssetPreviewKind.None)
        {
            return new AssetPreviewInfo
            {
                Kind = AssetPreviewKind.None,
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                Extension = Path.GetExtension(fullPath).ToLowerInvariant(),
                Problem = $"'{fullPath}' has no preview.",
            };
        }

        long length;
        try
        {
            length = new FileInfo(fullPath).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AssetPreviewInfo
            {
                Kind = AssetPreviewKind.None,
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                Extension = Path.GetExtension(fullPath).ToLowerInvariant(),
                Problem = $"Cannot read '{fullPath}': {exception.Message}",
            };
        }

        var info = new AssetPreviewInfo
        {
            Kind = kind,
            FullPath = fullPath,
            FileName = Path.GetFileName(fullPath),
            Extension = Path.GetExtension(fullPath).ToLowerInvariant(),
            FileSize = length,
        };
        info.AddLine("File", info.FileName);
        info.AddLine("Extension", info.Extension.Length == 0 ? "(none)" : info.Extension);
        info.AddLine("Size", FormatSize(length));

        try
        {
            switch (kind)
            {
                case AssetPreviewKind.Image:
                    ReadImage(info);
                    break;
                case AssetPreviewKind.CubeMap:
                    ReadDds(info);
                    break;
                case AssetPreviewKind.Audio:
                    if (info.Extension == ".wav")
                    {
                        ReadWav(info);
                    }
                    else
                    {
                        ReadOgg(info);
                    }

                    break;
                case AssetPreviewKind.BitmapFont:
                    ReadBitmapFont(info);
                    break;
                case AssetPreviewKind.Model:
                    ReadModel(info);
                    break;
                case AssetPreviewKind.Text:
                    ReadText(info);
                    break;
                case AssetPreviewKind.Binary:
                    ReadBinary(info);
                    break;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            info.Problem = $"Cannot read '{fullPath}': {exception.Message}";
            info.AddLine("Problem", exception.Message);
        }

        return info;
    }

    /// <summary>Formats a byte count as <c>1,234 bytes</c> plus a KB or MB approximation when it is large.</summary>
    public static string FormatSize(long bytes)
    {
        string exact = bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes";
        if (bytes >= 1024 * 1024)
        {
            return exact + " (" + (bytes / (1024.0 * 1024.0)).ToString("0.##", CultureInfo.InvariantCulture) + " MB)";
        }

        if (bytes >= 1024)
        {
            return exact + " (" + (bytes / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " KB)";
        }

        return exact;
    }

    private static byte[] ReadHead(string fullPath, int maxLength, out int count)
    {
        var buffer = new byte[maxLength];
        count = 0;
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        while (count < buffer.Length)
        {
            int read = stream.Read(buffer, count, buffer.Length - count);
            if (read <= 0)
            {
                break;
            }

            count += read;
        }

        return buffer;
    }

    private static void SetProblem(AssetPreviewInfo info, string message)
    {
        info.Problem = message;
        info.AddLine("Problem", message);
    }

    private static void SetDimensions(AssetPreviewInfo info, int width, int height)
    {
        info.Width = width;
        info.Height = height;
        info.AddLine("Dimensions", width.ToString(CultureInfo.InvariantCulture) + " x " + height.ToString(CultureInfo.InvariantCulture));
    }

    // ------------------------------------------------------------------ images

    private static void ReadImage(AssetPreviewInfo info)
    {
        byte[] head = ReadHead(info.FullPath, HeaderReadLength, out int count);
        var span = new ReadOnlySpan<byte>(head, 0, count);

        switch (info.Extension)
        {
            case ".png":
                ReadPng(info, span);
                break;
            case ".jpg":
            case ".jpeg":
                ReadJpeg(info, span);
                break;
            case ".bmp":
                ReadBmp(info, span);
                break;
            case ".gif":
                ReadGif(info, span);
                break;
            case ".tga":
                ReadTga(info, span);
                break;
        }
    }

    private static void ReadPng(AssetPreviewInfo info, ReadOnlySpan<byte> span)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        // 8 signature bytes, then the IHDR chunk: length(4) "IHDR"(4) width(4) height(4) depth(1) colour type(1).
        if (span.Length < 26 || !span[..8].SequenceEqual(signature) || !span.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            SetProblem(info, "Not a PNG file (signature or IHDR chunk missing).");
            return;
        }

        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(span[16..]);
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(span[20..]);
        SetDimensions(info, width, height);
        info.AddLine("Bit depth", span[24].ToString(CultureInfo.InvariantCulture));
        info.AddLine("Colour type", span[25] switch
        {
            0 => "Grayscale",
            2 => "RGB",
            3 => "Indexed",
            4 => "Grayscale + alpha",
            6 => "RGBA",
            _ => "Unknown (" + span[25].ToString(CultureInfo.InvariantCulture) + ")",
        });
    }

    private static void ReadJpeg(AssetPreviewInfo info, ReadOnlySpan<byte> span)
    {
        if (span.Length < 4 || span[0] != 0xFF || span[1] != 0xD8)
        {
            SetProblem(info, "Not a JPEG file (start-of-image marker missing).");
            return;
        }

        int position = 2;
        while (position + 4 <= span.Length)
        {
            if (span[position] != 0xFF)
            {
                position++;
                continue;
            }

            byte marker = span[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                position += 2;
                continue;
            }

            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(span[(position + 2)..]);
            bool isFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isFrame && position + 9 <= span.Length)
            {
                int height = BinaryPrimitives.ReadUInt16BigEndian(span[(position + 5)..]);
                int width = BinaryPrimitives.ReadUInt16BigEndian(span[(position + 7)..]);
                SetDimensions(info, width, height);
                return;
            }

            position += 2 + Math.Max(segmentLength, 2);
        }

        info.AddLine("Dimensions", "unknown (no frame header in the first 64 KB)");
    }

    private static void ReadBmp(AssetPreviewInfo info, ReadOnlySpan<byte> span)
    {
        if (span.Length < 26 || span[0] != (byte)'B' || span[1] != (byte)'M')
        {
            SetProblem(info, "Not a BMP file (BM signature missing).");
            return;
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(span[14..]);
        if (headerSize == 12)
        {
            SetDimensions(info, BinaryPrimitives.ReadUInt16LittleEndian(span[18..]), BinaryPrimitives.ReadUInt16LittleEndian(span[20..]));
            info.AddLine("Bits per pixel", BinaryPrimitives.ReadUInt16LittleEndian(span[24..]).ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (span.Length < 30)
        {
            SetProblem(info, "BMP header is truncated.");
            return;
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(span[18..]);
        int height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(span[22..]));
        SetDimensions(info, width, height);
        info.AddLine("Bits per pixel", BinaryPrimitives.ReadUInt16LittleEndian(span[28..]).ToString(CultureInfo.InvariantCulture));
    }

    private static void ReadGif(AssetPreviewInfo info, ReadOnlySpan<byte> span)
    {
        if (span.Length < 10 || !span[..4].SequenceEqual("GIF8"u8))
        {
            SetProblem(info, "Not a GIF file (GIF8 signature missing).");
            return;
        }

        SetDimensions(info, BinaryPrimitives.ReadUInt16LittleEndian(span[6..]), BinaryPrimitives.ReadUInt16LittleEndian(span[8..]));
    }

    private static void ReadTga(AssetPreviewInfo info, ReadOnlySpan<byte> span)
    {
        // TGA has no signature: the 18-byte header is checked for plausible values.
        if (span.Length < 18)
        {
            SetProblem(info, "TGA header is truncated.");
            return;
        }

        int imageType = span[2];
        int width = BinaryPrimitives.ReadUInt16LittleEndian(span[12..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(span[14..]);
        int bitsPerPixel = span[16];
        bool plausibleType = imageType is 1 or 2 or 3 or 9 or 10 or 11;
        if (!plausibleType || width == 0 || height == 0)
        {
            SetProblem(info, "Not a valid TGA header (image type " + imageType.ToString(CultureInfo.InvariantCulture) + ").");
            return;
        }

        SetDimensions(info, width, height);
        info.AddLine("Bits per pixel", bitsPerPixel.ToString(CultureInfo.InvariantCulture));
    }

    // --------------------------------------------------------------------- dds

    private static void ReadDds(AssetPreviewInfo info)
    {
        byte[] head = ReadHead(info.FullPath, 148, out int count);
        var span = new ReadOnlySpan<byte>(head, 0, count);

        // "DDS " (4) + 124-byte header; offsets are those of the DDS_HEADER structure.
        if (span.Length < 128 || !span[..4].SequenceEqual("DDS "u8))
        {
            SetProblem(info, "Not a DDS file (header missing or truncated).");
            return;
        }

        int height = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
        int width = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
        uint mipMapCount = BinaryPrimitives.ReadUInt32LittleEndian(span[28..]);
        uint pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(span[80..]);
        uint fourCc = BinaryPrimitives.ReadUInt32LittleEndian(span[84..]);
        uint rgbBitCount = BinaryPrimitives.ReadUInt32LittleEndian(span[88..]);
        uint caps2 = BinaryPrimitives.ReadUInt32LittleEndian(span[112..]);

        const uint pixelFormatHasFourCc = 0x4;
        const uint caps2CubeMap = 0x200;
        const uint caps2AllFaces = 0xFC00;

        info.IsCubeMap = (caps2 & caps2CubeMap) != 0;
        SetDimensions(info, width, height);
        info.AddLine("Mip count", Math.Max(1u, mipMapCount).ToString(CultureInfo.InvariantCulture));
        info.AddLine("Cube map", info.IsCubeMap
            ? ((caps2 & caps2AllFaces) == caps2AllFaces ? "yes (6 faces)" : "yes (partial faces)")
            : "no");

        if ((pixelFormatFlags & pixelFormatHasFourCc) != 0)
        {
            Span<char> chars = stackalloc char[4];
            for (int i = 0; i < 4; i++)
            {
                byte value = (byte)(fourCc >> (8 * i));
                chars[i] = value >= 0x20 && value < 0x7F ? (char)value : '?';
            }

            string name = new string(chars);
            if (name == "DX10" && span.Length >= 148)
            {
                name += " (DXGI format " + BinaryPrimitives.ReadUInt32LittleEndian(span[128..]).ToString(CultureInfo.InvariantCulture) + ")";
            }

            info.AddLine("FourCC", name);
        }
        else
        {
            info.AddLine("RGB bit count", rgbBitCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    // --------------------------------------------------------------------- audio

    private static void ReadWav(AssetPreviewInfo info)
    {
        using var stream = new FileStream(info.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> riff = stackalloc byte[12];
        if (stream.Read(riff) < 12 || !riff[..4].SequenceEqual("RIFF"u8) || !riff[8..12].SequenceEqual("WAVE"u8))
        {
            SetProblem(info, "Not a RIFF/WAVE file.");
            return;
        }

        Span<byte> chunkHeader = stackalloc byte[8];
        byte[] format = null;
        long dataLength = -1;
        while (stream.Read(chunkHeader) == 8)
        {
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);
            long chunkStart = stream.Position;
            if (chunkHeader[..4].SequenceEqual("fmt "u8))
            {
                format = new byte[Math.Min(chunkSize, 40u)];
                stream.ReadExactly(format);
            }
            else if (chunkHeader[..4].SequenceEqual("data"u8))
            {
                // A streamed wav may declare a size larger than the file: keep what is there.
                dataLength = Math.Min(chunkSize, stream.Length - chunkStart);
            }

            if (format != null && dataLength >= 0)
            {
                break;
            }

            // Chunks are word aligned: an odd size is followed by one padding byte.
            long next = chunkStart + chunkSize + (chunkSize & 1);
            if (next > stream.Length)
            {
                break;
            }

            stream.Position = next;
        }

        if (format == null || format.Length < 16)
        {
            SetProblem(info, "The wav has no fmt chunk.");
            return;
        }

        int formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2));
        int sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(4));
        int byteRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(8));
        int bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(14));

        info.AddLine("Format", formatTag switch
        {
            1 => "PCM",
            2 => "MS ADPCM",
            3 => "IEEE float",
            0x11 => "IMA ADPCM",
            0xFFFE => "Extensible",
            _ => "tag " + formatTag.ToString(CultureInfo.InvariantCulture),
        });
        info.AddLine("Sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
        info.AddLine("Channels", channels.ToString(CultureInfo.InvariantCulture));
        info.AddLine("Bits per sample", bitsPerSample.ToString(CultureInfo.InvariantCulture));

        if (dataLength < 0)
        {
            info.AddLine("Duration", "unknown (no data chunk)");
        }
        else if (byteRate > 0)
        {
            double seconds = dataLength / (double)byteRate;
            info.AddLine("Duration", seconds.ToString("0.000", CultureInfo.InvariantCulture) + " s");
        }
        else
        {
            info.AddLine("Duration", "unknown (byte rate is 0)");
        }
    }

    private static void ReadOgg(AssetPreviewInfo info)
    {
        byte[] head = ReadHead(info.FullPath, 4, out int count);
        if (count < 4 || !head.AsSpan(0, 4).SequenceEqual("OggS"u8))
        {
            info.AddLine("Note", "The file does not start with an Ogg page.");
        }
    }

    // ---------------------------------------------------------------- bitmap font

    private static void ReadBitmapFont(AssetPreviewInfo info)
    {
        byte[] head = ReadHead(info.FullPath, 8, out int headCount);
        if (headCount >= 3 && head.AsSpan(0, 3).SequenceEqual("BMF"u8))
        {
            SetProblem(info, "Binary BMFont descriptors are not read: only the text format is.");
            return;
        }

        string face = null;
        string size = null;
        string lineHeight = null;
        string charCount = null;
        foreach (string line in File.ReadLines(info.FullPath))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("info ", StringComparison.Ordinal))
            {
                face ??= GetField(trimmed, "face");
                size ??= GetField(trimmed, "size");
            }
            else if (trimmed.StartsWith("common ", StringComparison.Ordinal))
            {
                lineHeight ??= GetField(trimmed, "lineHeight");
            }
            else if (trimmed.StartsWith("page ", StringComparison.Ordinal))
            {
                string pageFile = GetField(trimmed, "file");
                if (!string.IsNullOrEmpty(pageFile))
                {
                    info.AddPageFile(pageFile);
                }
            }
            else if (trimmed.StartsWith("chars ", StringComparison.Ordinal))
            {
                charCount ??= GetField(trimmed, "count");
            }
        }

        if (face == null && lineHeight == null)
        {
            SetProblem(info, "Not a BMFont text descriptor (no info or common line).");
            return;
        }

        info.AddLine("Face", face ?? "unknown");
        info.AddLine("Font size", size ?? "unknown");
        info.AddLine("Line height", lineHeight ?? "unknown");
        info.AddLine("Pages", info.PageFiles.Count == 0 ? "none" : string.Join(", ", info.PageFiles));
        info.AddLine("Characters", charCount ?? "unknown");
    }

    /// <summary>Value of <c>key=value</c> or <c>key="quoted value"</c> in a BMFont line, or null.</summary>
    private static string GetField(string line, string key)
    {
        string token = key + "=";
        int index = 0;
        while (true)
        {
            index = line.IndexOf(token, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            // The key must start a token: "face=" is not found inside "typeface=".
            if (index == 0 || line[index - 1] == ' ' || line[index - 1] == '\t')
            {
                break;
            }

            index += token.Length;
        }

        int valueStart = index + token.Length;
        if (valueStart >= line.Length)
        {
            return string.Empty;
        }

        if (line[valueStart] == '"')
        {
            int end = line.IndexOf('"', valueStart + 1);
            return end < 0 ? line[(valueStart + 1)..] : line[(valueStart + 1)..end];
        }

        int space = valueStart;
        while (space < line.Length && line[space] != ' ' && line[space] != '\t')
        {
            space++;
        }

        return line[valueStart..space];
    }

    // --------------------------------------------------------------------- model

    private static void ReadModel(AssetPreviewInfo info)
    {
        if (info.Extension == ".glb")
        {
            ReadGlb(info);
            return;
        }

        using var stream = new FileStream(info.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using JsonDocument document = JsonDocument.Parse(stream);
        AddGltfCounts(info, document.RootElement);
    }

    private static void ReadGlb(AssetPreviewInfo info)
    {
        using var stream = new FileStream(info.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> header = stackalloc byte[20];
        if (stream.Read(header) < 20 || !header[..4].SequenceEqual("glTF"u8))
        {
            SetProblem(info, "Not a GLB file (glTF magic missing).");
            return;
        }

        info.AddLine("GLB version", BinaryPrimitives.ReadUInt32LittleEndian(header[4..]).ToString(CultureInfo.InvariantCulture));
        info.AddLine("GLB length", BinaryPrimitives.ReadUInt32LittleEndian(header[8..]).ToString("N0", CultureInfo.InvariantCulture) + " bytes");

        uint jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        if (!header[16..20].SequenceEqual("JSON"u8) || jsonLength == 0 || jsonLength > 64 * 1024 * 1024)
        {
            return;
        }

        var json = new byte[jsonLength];
        stream.ReadExactly(json);
        using JsonDocument document = JsonDocument.Parse(json);
        AddGltfCounts(info, document.RootElement);
    }

    private static void AddGltfCounts(AssetPreviewInfo info, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            SetProblem(info, "The glTF JSON root is not an object.");
            return;
        }

        if (root.TryGetProperty("asset", out JsonElement asset)
            && asset.ValueKind == JsonValueKind.Object
            && asset.TryGetProperty("version", out JsonElement version))
        {
            info.AddLine("glTF version", version.ToString());
        }

        info.AddLine("Meshes", CountArray(root, "meshes"));
        info.AddLine("Materials", CountArray(root, "materials"));
        info.AddLine("Images", CountArray(root, "images"));
        info.AddLine("Nodes", CountArray(root, "nodes"));
    }

    private static string CountArray(JsonElement root, string name)
    {
        int count = root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Array
            ? element.GetArrayLength()
            : 0;
        return count.ToString(CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- text, binary

    private static void ReadText(AssetPreviewInfo info)
    {
        byte[] head = ReadHead(info.FullPath, 3, out int count);
        bool hasBom = count >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        info.AddLine("Encoding", hasBom ? "UTF-8 with BOM" : "UTF-8");

        var preview = new StringBuilder();
        int lineCount = 0;
        using (var reader = new StreamReader(info.FullPath, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (lineCount < MaxTextPreviewLines)
                {
                    if (lineCount > 0)
                    {
                        preview.Append('\n');
                    }

                    preview.Append(line.Length > MaxTextPreviewLineLength ? line[..MaxTextPreviewLineLength] + "..." : line);
                }

                lineCount++;
            }
        }

        info.TextPreview = preview.ToString();
        info.AddLine("Lines", lineCount.ToString(CultureInfo.InvariantCulture));
        if (lineCount > MaxTextPreviewLines)
        {
            info.AddLine("Preview", "first " + MaxTextPreviewLines.ToString(CultureInfo.InvariantCulture) + " lines");
        }
    }

    private static void ReadBinary(AssetPreviewInfo info)
    {
        info.AddLine("Content", "binary");

        long size = info.FileSize;
        long side = (long)Math.Round(Math.Sqrt(size));
        if (side >= 2 && side <= MaxSquareMapSide && side * side == size)
        {
            info.SquareMapSide = (int)side;
            info.AddLine(
                "Possible map",
                side.ToString(CultureInfo.InvariantCulture) + " x " + side.ToString(CultureInfo.InvariantCulture) + ", 8 bits per cell");
        }
    }
}
