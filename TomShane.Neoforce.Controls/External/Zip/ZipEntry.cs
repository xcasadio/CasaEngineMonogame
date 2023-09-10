using System;

namespace TomShane.Neoforce.External.Zip;

internal class ZipEntry
{

    private const int ZipEntrySignature = 0x04034b50;
    private const int ZipEntryDataDescriptorSignature = 0x08074b50;

    private bool _debug;

    private DateTime _lastModified;
    public DateTime LastModified => _lastModified;

    // when this is set, we trim the volume (eg C:\) off any fully-qualified pathname, 
    // before writing the ZipEntry into the ZipFile. 
    public bool TrimVolumeFromFullyQualifiedPaths { get; set; } = true;

    public string FileName { get; private set; }

    public Int16 VersionNeeded { get; private set; }

    public Int16 BitField { get; private set; }

    public Int16 CompressionMethod { get; private set; }

    public Int32 CompressedSize { get; private set; }

    public Int32 UncompressedSize { get; private set; }

    public Double CompressionRatio => 100 * (1.0 - 1.0 * CompressedSize / (1.0 * UncompressedSize));

    private Int32 _lastModDateTime;
    private Int32 _crc32;
    private byte[] _extra;

    private byte[] _filedata;
    private byte[] FileData
    {
        get
        {
            if (_filedata == null)
            {
            }
            return _filedata;
        }
    }

    private System.IO.MemoryStream _underlyingMemoryStream;
    private System.IO.Compression.DeflateStream _compressedStream;
    private System.IO.Compression.DeflateStream CompressedStream
    {
        get
        {
            if (_compressedStream == null)
            {
                _underlyingMemoryStream = new System.IO.MemoryStream();
                var leaveUnderlyingStreamOpen = true;
                _compressedStream = new System.IO.Compression.DeflateStream(_underlyingMemoryStream,
                    System.IO.Compression.CompressionMode.Compress,
                    leaveUnderlyingStreamOpen);
            }
            return _compressedStream;
        }
    }

    internal byte[] Header { get; private set; }

    private int _relativeOffsetOfHeader;


    private static bool ReadHeader(System.IO.Stream s, ZipEntry ze)
    {
        var signature = Shared.ReadSignature(s);

        // return null if this is not a local file header signature
        if (SignatureIsNotValid(signature))
        {
            s.Seek(-4, System.IO.SeekOrigin.Current);
            if (ze._debug)
            {
                Console.WriteLine("  ZipEntry::Read(): Bad signature ({0:X8}) at position {1}", signature, s.Position);
            }

            return false;
        }

        var block = new byte[26];
        var n = s.Read(block, 0, block.Length);
        if (n != block.Length)
        {
            return false;
        }

        var i = 0;
        ze.VersionNeeded = (short)(block[i++] + block[i++] * 256);
        ze.BitField = (short)(block[i++] + block[i++] * 256);
        ze.CompressionMethod = (short)(block[i++] + block[i++] * 256);
        ze._lastModDateTime = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;

        // the PKZIP spec says that if bit 3 is set (0x0008), then the CRC, Compressed size, and uncompressed size
        // come directly after the file data.  The only way to find it is to scan the zip archive for the signature of 
        // the Data Descriptor, and presume that that signature does not appear in the (compressed) data of the compressed file.  

        if ((ze.BitField & 0x0008) != 0x0008)
        {
            ze._crc32 = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
            ze.CompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
            ze.UncompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
        }
        else
        {
            // the CRC, compressed size, and uncompressed size are stored later in the stream.
            // here, we advance the pointer.
            i += 12;
        }

        var filenameLength = (short)(block[i++] + block[i++] * 256);
        var extraFieldLength = (short)(block[i++] + block[i++] * 256);

        block = new byte[filenameLength];
        n = s.Read(block, 0, block.Length);
        ze.FileName = Shared.StringFromBuffer(block, 0, block.Length);

        ze._extra = new byte[extraFieldLength];
        n = s.Read(ze._extra, 0, ze._extra.Length);

        // transform the time data into something usable
        ze._lastModified = Shared.PackedToDateTime(ze._lastModDateTime);

        // actually get the compressed size and CRC if necessary
        if ((ze.BitField & 0x0008) == 0x0008)
        {
            var posn = s.Position;
            var sizeOfDataRead = Shared.FindSignature(s, ZipEntryDataDescriptorSignature);
            if (sizeOfDataRead == -1)
            {
                return false;
            }

            // read 3x 4-byte fields (CRC, Compressed Size, Uncompressed Size)
            block = new byte[12];
            n = s.Read(block, 0, block.Length);
            if (n != 12)
            {
                return false;
            }

            i = 0;
            ze._crc32 = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
            ze.CompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
            ze.UncompressedSize = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;

            if (sizeOfDataRead != ze.CompressedSize)
            {
                throw new Exception("Data format error (bit 3 is set)");
            }

            // seek back to previous position, to read file data
            s.Seek(posn, System.IO.SeekOrigin.Begin);
        }

        return true;
    }


    private static bool SignatureIsNotValid(int signature)
    {
        return signature != ZipEntrySignature;
    }


    public static ZipEntry Read(System.IO.Stream s)
    {
        return Read(s, false);
    }


    internal static ZipEntry Read(System.IO.Stream s, bool turnOnDebug)
    {
        var entry = new ZipEntry();
        entry._debug = turnOnDebug;
        if (!ReadHeader(s, entry))
        {
            return null;
        }

        entry._filedata = new byte[entry.CompressedSize];
        var n = s.Read(entry.FileData, 0, entry.FileData.Length);
        if (n != entry.FileData.Length)
        {
            throw new Exception("badly formatted zip file.");
        }
        // finally, seek past the (already read) Data descriptor if necessary
        if ((entry.BitField & 0x0008) == 0x0008)
        {
            s.Seek(16, System.IO.SeekOrigin.Current);
        }
        return entry;
    }



    internal static ZipEntry Create(String filename)
    {
        var entry = new ZipEntry();
        entry.FileName = filename;

        entry._lastModified = System.IO.File.GetLastWriteTime(filename);
        // adjust the time if the .NET BCL thinks it is in DST.  
        // see the note elsewhere in this file for more info. 
        if (entry._lastModified.IsDaylightSavingTime())
        {
            var adjustedTime = entry._lastModified - new TimeSpan(1, 0, 0);
            entry._lastModDateTime = Shared.DateTimeToPacked(adjustedTime);
        }
        else
        {
            entry._lastModDateTime = Shared.DateTimeToPacked(entry._lastModified);
        }

        // we don't actually slurp in the file until the caller invokes Write on this entry.

        return entry;
    }



    public void Extract()
    {
        Extract(".");
    }

    public void Extract(System.IO.Stream s)
    {
        Extract(null, s);
    }

    public void Extract(string basedir)
    {
        Extract(basedir, null);
    }


    internal System.IO.Stream GetStream()
    {
        var memstream = new System.IO.MemoryStream(FileData);

        if (CompressedSize == UncompressedSize)
        {
            return memstream;
        }

        return new System.IO.Compression.DeflateStream(
            memstream, System.IO.Compression.CompressionMode.Decompress);
    }

    // pass in either basedir or s, but not both. 
    // In other words, you can extract to a stream or to a directory, but not both!
    private void Extract(string basedir, System.IO.Stream s)
    {
        string targetFile = null;
        if (basedir != null)
        {
            targetFile = System.IO.Path.Combine(basedir, FileName);

            // check if a directory
            if (FileName.EndsWith("/"))
            {
                if (!System.IO.Directory.Exists(targetFile))
                {
                    System.IO.Directory.CreateDirectory(targetFile);
                }

                return;
            }
        }
        else if (s != null)
        {
            if (FileName.EndsWith("/"))
            // extract a directory to streamwriter?  nothing to do!
            {
                return;
            }
        }
        else
        {
            throw new Exception("Invalid input.");
        }


        using (var memstream = new System.IO.MemoryStream(FileData))
        {

            System.IO.Stream input = null;
            try
            {

                if (CompressedSize == UncompressedSize)
                {
                    // the System.IO.Compression.DeflateStream class does not handle uncompressed data.
                    // so if an entry is not compressed, then we just translate the bytes directly.
                    input = memstream;
                }
                else
                {
                    input = new System.IO.Compression.DeflateStream(memstream, System.IO.Compression.CompressionMode.Decompress);
                }


                if (targetFile != null)
                {
                    // ensure the target path exists
                    if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(targetFile)))
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));
                    }
                }


                System.IO.Stream output = null;
                try
                {
                    if (targetFile != null)
                    {
                        output = new System.IO.FileStream(targetFile, System.IO.FileMode.CreateNew);
                    }
                    else
                    {
                        output = s;
                    }


                    var bytes = new byte[4096];
                    int n;

                    if (_debug)
                    {
                        Console.WriteLine("{0}: _FileData.Length= {1}", targetFile, FileData.Length);
                        Console.WriteLine("{0}: memstream.Position: {1}", targetFile, memstream.Position);
                        n = FileData.Length;
                        if (n > 1000)
                        {
                            n = 500;
                            Console.WriteLine("{0}: truncating dump from {1} to {2} bytes...", targetFile, FileData.Length, n);
                        }
                        for (var j = 0; j < n; j += 2)
                        {
                            if (j > 0 && j % 40 == 0)
                            {
                                Console.WriteLine();
                            }

                            Console.Write(" {0:X2}", FileData[j]);
                            if (j + 1 < n)
                            {
                                Console.Write("{0:X2}", FileData[j + 1]);
                            }
                        }
                        Console.WriteLine("\n");
                    }

                    n = 1; // anything non-zero
                    while (n != 0)
                    {
                        if (_debug)
                        {
                            Console.WriteLine("{0}: about to read...", targetFile);
                        }

                        n = input.Read(bytes, 0, bytes.Length);
                        if (_debug)
                        {
                            Console.WriteLine("{0}: got {1} bytes", targetFile, n);
                        }

                        if (n > 0)
                        {
                            if (_debug)
                            {
                                Console.WriteLine("{0}: about to write...", targetFile);
                            }

                            output.Write(bytes, 0, n);
                        }
                    }
                }
                finally
                {
                    // we only close the output stream if we opened it. 
                    if (output != null && targetFile != null)
                    {
                        output.Close();
                        output.Dispose();
                    }
                }

                if (targetFile != null)
                {
                    // We may have to adjust the last modified time to compensate
                    // for differences in how the .NET Base Class Library deals
                    // with daylight saving time (DST) versus how the Windows
                    // filesystem deals with daylight saving time. See 
                    // http://blogs.msdn.com/oldnewthing/archive/2003/10/24/55413.aspx for some context. 

                    // in a nutshell: Daylight savings time rules change regularly.  In
                    // 2007, for example, the inception week of DST changed.  In 1977,
                    // DST was in place all year round. in 1945, likewise.  And so on.
                    // Win32 does not attempt to guess which time zone rules were in
                    // effect at the time in question.  It will render a time as
                    // "standard time" and allow the app to change to DST as necessary.
                    //  .NET makes a different choice.

                    // -------------------------------------------------------
                    // Compare the output of FileInfo.LastWriteTime.ToString("f") with
                    // what you see in the property sheet for a file that was last
                    // written to on the other side of the DST transition. For example,
                    // suppose the file was last modified on October 17, during DST but
                    // DST is not currently in effect. Explorer's file properties
                    // reports Thursday, October 17, 2003, 8:45:38 AM, but .NETs
                    // FileInfo reports Thursday, October 17, 2003, 9:45 AM.

                    // Win32 says, "Thursday, October 17, 2002 8:45:38 AM PST". Note:
                    // Pacific STANDARD Time. Even though October 17 of that year
                    // occurred during Pacific Daylight Time, Win32 displays the time as
                    // standard time because that's what time it is NOW.

                    // .NET BCL assumes that the current DST rules were in place at the
                    // time in question.  So, .NET says, "Well, if the rules in effect
                    // now were also in effect on October 17, 2003, then that would be
                    // daylight time" so it displays "Thursday, October 17, 2003, 9:45
                    // AM PDT" - daylight time.

                    // So .NET gives a value which is more intuitively correct, but is
                    // also potentially incorrect, and which is not invertible. Win32
                    // gives a value which is intuitively incorrect, but is strictly
                    // correct.
                    // -------------------------------------------------------

                    // With this adjustment, I add one hour to the tweaked .NET time, if
                    // necessary.  That is to say, if the time in question had occurred
                    // in what the .NET BCL assumed to be DST (an assumption that may be
                    // wrong given the constantly changing DST rules).

#if !XBOX
                    if (LastModified.IsDaylightSavingTime())
                    {
                        var adjustedLastModified = LastModified + new TimeSpan(1, 0, 0);
                        System.IO.File.SetLastWriteTime(targetFile, adjustedLastModified);
                    }
                    else
                    {
                        System.IO.File.SetLastWriteTime(targetFile, LastModified);
                    }
#endif
                }

            }
            finally
            {
                // we only close the output stream if we opened it. 
                // we cannot use using() here because in some cases we do not want to Dispose the stream!
                if (input != null && input != memstream)
                {
                    input.Close();
                    input.Dispose();
                }
            }
        }
    }


    internal void WriteCentralDirectoryEntry(System.IO.Stream s)
    {
        var bytes = new byte[4096];
        var i = 0;
        // signature
        bytes[i++] = ZipDirEntry.ZipDirEntrySignature & 0x000000FF;
        bytes[i++] = (ZipDirEntry.ZipDirEntrySignature & 0x0000FF00) >> 8;
        bytes[i++] = (ZipDirEntry.ZipDirEntrySignature & 0x00FF0000) >> 16;
        bytes[i++] = (byte)((ZipDirEntry.ZipDirEntrySignature & 0xFF000000) >> 24);

        // Version Made By
        bytes[i++] = Header[4];
        bytes[i++] = Header[5];

        // Version Needed, Bitfield, compression method, lastmod,
        // crc, sizes, filename length and extra field length -
        // are all the same as the local file header. So just copy them
        var j = 0;
        for (j = 0; j < 26; j++)
            bytes[i + j] = Header[4 + j];

        i += j;  // positioned at next available byte

        // File Comment Length
        bytes[i++] = 0;
        bytes[i++] = 0;

        // Disk number start
        bytes[i++] = 0;
        bytes[i++] = 0;

        // internal file attrs            
        bytes[i++] = 1;
        bytes[i++] = 0;

        // external file attrs            
        bytes[i++] = 0x20;
        bytes[i++] = 0;
        bytes[i++] = 0xb6;
        bytes[i++] = 0x81;

        // relative offset of local header (I think this can be zero)
        bytes[i++] = (byte)(_relativeOffsetOfHeader & 0x000000FF);
        bytes[i++] = (byte)((_relativeOffsetOfHeader & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((_relativeOffsetOfHeader & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((_relativeOffsetOfHeader & 0xFF000000) >> 24);

        if (_debug)
        {
            Console.WriteLine("\ninserting filename into CDS: (length= {0})", Header.Length - 30);
        }

        // actual filename (starts at offset 34 in header) 
        for (j = 0; j < Header.Length - 30; j++)
        {
            bytes[i + j] = Header[30 + j];
            if (_debug)
            {
                Console.Write(" {0:X2}", bytes[i + j]);
            }
        }
        if (_debug)
        {
            Console.WriteLine();
        }

        i += j;

        s.Write(bytes, 0, i);
    }


    private void WriteHeader(System.IO.Stream s, byte[] bytes)
    {
        // write the header info

        var i = 0;
        // signature
        bytes[i++] = ZipEntrySignature & 0x000000FF;
        bytes[i++] = (ZipEntrySignature & 0x0000FF00) >> 8;
        bytes[i++] = (ZipEntrySignature & 0x00FF0000) >> 16;
        bytes[i++] = (byte)((ZipEntrySignature & 0xFF000000) >> 24);

        // version needed
        Int16 fixedVersionNeeded = 0x14; // from examining existing zip files
        bytes[i++] = (byte)(fixedVersionNeeded & 0x00FF);
        bytes[i++] = (byte)((fixedVersionNeeded & 0xFF00) >> 8);

        // bitfield
        Int16 bitField = 0x00; // from examining existing zip files
        bytes[i++] = (byte)(bitField & 0x00FF);
        bytes[i++] = (byte)((bitField & 0xFF00) >> 8);

        // compression method
        Int16 compressionMethod = 0x08; // 0x08 = Deflate
        bytes[i++] = (byte)(compressionMethod & 0x00FF);
        bytes[i++] = (byte)((compressionMethod & 0xFF00) >> 8);

        // LastMod
        bytes[i++] = (byte)(_lastModDateTime & 0x000000FF);
        bytes[i++] = (byte)((_lastModDateTime & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((_lastModDateTime & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((_lastModDateTime & 0xFF000000) >> 24);

        // CRC32 (Int32)
        var crc32 = new Crc32();
        UInt32 crc = 0;
        using (System.IO.Stream input = System.IO.File.OpenRead(FileName))
        {
            crc = crc32.GetCrc32AndCopy(input, CompressedStream);
        }
        CompressedStream.Close();  // to get the footer bytes written to the underlying stream

        bytes[i++] = (byte)(crc & 0x000000FF);
        bytes[i++] = (byte)((crc & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((crc & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((crc & 0xFF000000) >> 24);

        // CompressedSize (Int32)
        var isz = (Int32)_underlyingMemoryStream.Length;
        var sz = (UInt32)isz;
        bytes[i++] = (byte)(sz & 0x000000FF);
        bytes[i++] = (byte)((sz & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((sz & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((sz & 0xFF000000) >> 24);

        // UncompressedSize (Int32)
        if (_debug)
        {
            Console.WriteLine("Uncompressed Size: {0}", crc32.TotalBytesRead);
        }

        bytes[i++] = (byte)(crc32.TotalBytesRead & 0x000000FF);
        bytes[i++] = (byte)((crc32.TotalBytesRead & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((crc32.TotalBytesRead & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((crc32.TotalBytesRead & 0xFF000000) >> 24);

        // filename length (Int16)
        var length = (Int16)FileName.Length;
        // see note below about TrimVolumeFromFullyQualifiedPaths.
        if (TrimVolumeFromFullyQualifiedPaths && FileName[1] == ':' && FileName[2] == '\\')
        {
            length -= 3;
        }

        bytes[i++] = (byte)(length & 0x00FF);
        bytes[i++] = (byte)((length & 0xFF00) >> 8);

        // extra field length (short)
        Int16 extraFieldLength = 0x00;
        bytes[i++] = (byte)(extraFieldLength & 0x00FF);
        bytes[i++] = (byte)((extraFieldLength & 0xFF00) >> 8);

        // Tue, 27 Mar 2007  16:35

        // Creating a zip that contains entries with "fully qualified" pathnames
        // can result in a zip archive that is unreadable by Windows Explorer.
        // Such archives are valid according to other tools but not to explorer.
        // To avoid this, we can trim off the leading volume name and slash (eg
        // c:\) when creating (writing) a zip file.  We do this by default and we
        // leave the old behavior available with the
        // TrimVolumeFromFullyQualifiedPaths flag - set it to false to get the old
        // behavior.  It only affects zip creation.

        // actual filename
        var c = TrimVolumeFromFullyQualifiedPaths && FileName[1] == ':' && FileName[2] == '\\' ?
            FileName.Substring(3).ToCharArray() :  // trim off volume letter, colon, and slash
            FileName.ToCharArray();
        var j = 0;

        if (_debug)
        {
            Console.WriteLine("local header: writing filename, {0} chars", c.Length);
            Console.WriteLine("starting offset={0}", i);
        }
        for (j = 0; j < c.Length && i + j < bytes.Length; j++)
        {
            bytes[i + j] = BitConverter.GetBytes(c[j])[0];
            if (_debug)
            {
                Console.Write(" {0:X2}", bytes[i + j]);
            }
        }
        if (_debug)
        {
            Console.WriteLine();
        }

        i += j;

        // extra field (we always write nothing in this implementation)
        // ;;

        // remember the file offset of this header
        _relativeOffsetOfHeader = (int)s.Length;


        if (_debug)
        {
            Console.WriteLine("\nAll header data:");
            for (j = 0; j < i; j++)
                Console.Write(" {0:X2}", bytes[j]);
            Console.WriteLine();
        }
        // finally, write the header to the stream
        s.Write(bytes, 0, i);

        // preserve this header data for use with the central directory structure.
        Header = new byte[i];
        if (_debug)
        {
            Console.WriteLine("preserving header of {0} bytes", Header.Length);
        }

        for (j = 0; j < i; j++)
            Header[j] = bytes[j];

    }


    internal void Write(System.IO.Stream s)
    {
        var bytes = new byte[4096];
        int n;

        // write the header:
        WriteHeader(s, bytes);

        // write the actual file data: 
        _underlyingMemoryStream.Position = 0;

        if (_debug)
        {
            Console.WriteLine("{0}: writing compressed data to zipfile...", FileName);
            Console.WriteLine("{0}: total data length: {1}", FileName, _underlyingMemoryStream.Length);
        }
        while ((n = _underlyingMemoryStream.Read(bytes, 0, bytes.Length)) != 0)
        {

            if (_debug)
            {
                Console.WriteLine("{0}: transferring {1} bytes...", FileName, n);

                for (var j = 0; j < n; j += 2)
                {
                    if (j > 0 && j % 40 == 0)
                    {
                        Console.WriteLine();
                    }

                    Console.Write(" {0:X2}", bytes[j]);
                    if (j + 1 < n)
                    {
                        Console.Write("{0:X2}", bytes[j + 1]);
                    }
                }
                Console.WriteLine("\n");
            }

            s.Write(bytes, 0, n);
        }

        //_CompressedStream.Close();
        //_CompressedStream= null;
        _underlyingMemoryStream.Close();
        _underlyingMemoryStream = null;
    }
}