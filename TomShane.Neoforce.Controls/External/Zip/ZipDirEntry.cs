using System;

namespace TomShane.Neoforce.External.Zip;

internal class ZipDirEntry
{

    internal const int ZipDirEntrySignature = 0x02014b50;

    private bool _debug;

    private ZipDirEntry() { }

    public DateTime LastModified { get; private set; }

    public string FileName { get; private set; }

    public string Comment { get; private set; }

    public Int16 VersionMadeBy { get; private set; }

    public Int16 VersionNeeded { get; private set; }

    public Int16 CompressionMethod { get; private set; }

    public Int32 CompressedSize { get; private set; }

    public Int32 UncompressedSize { get; private set; }

    public Double CompressionRatio => 100 * (1.0 - 1.0 * CompressedSize / (1.0 * UncompressedSize));

    private Int16 _bitField;
    private Int32 _lastModDateTime;

    private Int32 _crc32;
    private byte[] _extra;

    internal ZipDirEntry(ZipEntry ze) { }


    internal static ZipDirEntry Read(System.IO.Stream s)
    {
        return Read(s, false);
    }


    internal static ZipDirEntry Read(System.IO.Stream s, bool turnOnDebug)
    {

        var signature = Shared.ReadSignature(s);
        // return null if this is not a local file header signature
        if (SignatureIsNotValid(signature))
        {
            s.Seek(-4, System.IO.SeekOrigin.Current);
            if (turnOnDebug)
            {
                Console.WriteLine("  ZipDirEntry::Read(): Bad signature ({0:X8}) at position {1}", signature, s.Position);
            }

            return null;
        }

        var block = new byte[42];
        var n = s.Read(block, 0, block.Length);
        if (n != block.Length)
        {
            return null;
        }

        var i = 0;
        var zde = new ZipDirEntry();

        zde._debug = turnOnDebug;
        zde.VersionMadeBy = (short)(block[i++] + block[i++] * 256);
        zde.VersionNeeded = (short)(block[i++] + block[i++] * 256);
        zde._bitField = (short)(block[i++] + block[i++] * 256);
        zde.CompressionMethod = (short)(block[i++] + block[i++] * 256);
        zde._lastModDateTime = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
        zde._crc32 = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
        zde.CompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
        zde.UncompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;

        zde.LastModified = Shared.PackedToDateTime(zde._lastModDateTime);

        var filenameLength = (short)(block[i++] + block[i++] * 256);
        var extraFieldLength = (short)(block[i++] + block[i++] * 256);
        var commentLength = (short)(block[i++] + block[i++] * 256);
        var diskNumber = (short)(block[i++] + block[i++] * 256);
        var internalFileAttrs = (short)(block[i++] + block[i++] * 256);
        var externalFileAttrs = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
        var offset = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;

        block = new byte[filenameLength];
        n = s.Read(block, 0, block.Length);
        zde.FileName = Shared.StringFromBuffer(block, 0, block.Length);

        zde._extra = new byte[extraFieldLength];
        n = s.Read(zde._extra, 0, zde._extra.Length);

        block = new byte[commentLength];
        n = s.Read(block, 0, block.Length);
        zde.Comment = Shared.StringFromBuffer(block, 0, block.Length);

        return zde;
    }

    private static bool SignatureIsNotValid(int signature)
    {
        return signature != ZipDirEntrySignature;
    }

}