using System;

namespace TomShane.Neoforce.External.Zip;

internal class ZipFile : System.Collections.Generic.IEnumerable<ZipEntry>,
    IDisposable
{
    public string Name { get; private set; }


    // when this is set, we trim the volume (eg C:) off any fully-qualified pathname, 
    // before writing the ZipEntry into the ZipFile. 
    // We default this to true.  This allows Windows Explorer to read the zip archives properly. 
    public bool TrimVolumeFromFullyQualifiedPaths { get; set; } = true;

    private System.IO.Stream ReadStream
    {
        get
        {
            if (_readstream == null)
            {
                _readstream = System.IO.File.OpenRead(Name);
            }
            return _readstream;
        }
    }

    private System.IO.FileStream WriteStream
    {
        get
        {
            if (_writestream == null)
            {
                _writestream = new System.IO.FileStream(Name, System.IO.FileMode.CreateNew);
            }
            return _writestream;
        }
    }

    private ZipFile() { }


    public ZipFile(string newZipFileName)
    {
        // create a new zipfile
        Name = newZipFileName;
        if (System.IO.File.Exists(Name))
        {
            throw new Exception($"That file ({newZipFileName}) already exists.");
        }

        _entries = new System.Collections.Generic.List<ZipEntry>();
    }


    public void AddItem(string fileOrDirectoryName)
    {
        AddItem(fileOrDirectoryName, false);
    }

    public void AddItem(string fileOrDirectoryName, bool wantVerbose)
    {
        if (System.IO.File.Exists(fileOrDirectoryName))
        {
            AddFile(fileOrDirectoryName, wantVerbose);
        }
        else if (System.IO.Directory.Exists(fileOrDirectoryName))
        {
            AddDirectory(fileOrDirectoryName, wantVerbose);
        }

        else
        {
            throw new Exception($"That file or directory ({fileOrDirectoryName}) does not exist!");
        }
    }

    public void AddFile(string fileName)
    {
        AddFile(fileName, false);
    }

    public void AddFile(string fileName, bool wantVerbose)
    {
        var ze = ZipEntry.Create(fileName);
        ze.TrimVolumeFromFullyQualifiedPaths = TrimVolumeFromFullyQualifiedPaths;
        if (wantVerbose)
        {
            Console.WriteLine("adding {0}...", fileName);
        }

        ze.Write(WriteStream);
        _entries.Add(ze);
    }

    public void AddDirectory(string directoryName)
    {
        AddDirectory(directoryName, false);
    }

    public void AddDirectory(string directoryName, bool wantVerbose)
    {
        var filenames = System.IO.Directory.GetFiles(directoryName);
        foreach (var filename in filenames)
        {
            if (wantVerbose)
            {
                Console.WriteLine("adding {0}...", filename);
            }

            AddFile(filename);
        }
    }


    public void Save()
    {
        WriteCentralDirectoryStructure();
        WriteStream.Close();
        _writestream = null;
    }


    private void WriteCentralDirectoryStructure()
    {
        // the central directory structure
        var start = WriteStream.Length;
        foreach (var e in _entries)
        {
            e.WriteCentralDirectoryEntry(WriteStream);
        }
        var finish = WriteStream.Length;

        // now, the footer
        WriteCentralDirectoryFooter(start, finish);
    }


    private void WriteCentralDirectoryFooter(long startOfCentralDirectory, long endOfCentralDirectory)
    {
        var bytes = new byte[1024];
        var i = 0;
        // signature
        UInt32 endOfCentralDirectorySignature = 0x06054b50;
        bytes[i++] = (byte)(endOfCentralDirectorySignature & 0x000000FF);
        bytes[i++] = (byte)((endOfCentralDirectorySignature & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((endOfCentralDirectorySignature & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((endOfCentralDirectorySignature & 0xFF000000) >> 24);

        // number of this disk
        bytes[i++] = 0;
        bytes[i++] = 0;

        // number of the disk with the start of the central directory
        bytes[i++] = 0;
        bytes[i++] = 0;

        // total number of entries in the central dir on this disk
        bytes[i++] = (byte)(_entries.Count & 0x00FF);
        bytes[i++] = (byte)((_entries.Count & 0xFF00) >> 8);

        // total number of entries in the central directory
        bytes[i++] = (byte)(_entries.Count & 0x00FF);
        bytes[i++] = (byte)((_entries.Count & 0xFF00) >> 8);

        // size of the central directory
        var sizeOfCentralDirectory = (Int32)(endOfCentralDirectory - startOfCentralDirectory);
        bytes[i++] = (byte)(sizeOfCentralDirectory & 0x000000FF);
        bytes[i++] = (byte)((sizeOfCentralDirectory & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((sizeOfCentralDirectory & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((sizeOfCentralDirectory & 0xFF000000) >> 24);

        // offset of the start of the central directory 
        var startOffset = (Int32)startOfCentralDirectory;  // cast down from Long
        bytes[i++] = (byte)(startOffset & 0x000000FF);
        bytes[i++] = (byte)((startOffset & 0x0000FF00) >> 8);
        bytes[i++] = (byte)((startOffset & 0x00FF0000) >> 16);
        bytes[i++] = (byte)((startOffset & 0xFF000000) >> 24);

        // zip comment length
        bytes[i++] = 0;
        bytes[i++] = 0;

        WriteStream.Write(bytes, 0, i);
    }

    internal static ZipFile Read(string zipfilename)
    {
        return Read(zipfilename, false);
    }

    internal static ZipFile Read(string zipfilename, bool turnOnDebug)
    {

        var zf = new ZipFile();
        zf._debug = turnOnDebug;
        zf.Name = zipfilename;
        zf._entries = new System.Collections.Generic.List<ZipEntry>();
        ZipEntry e;
        while ((e = ZipEntry.Read(zf.ReadStream, zf._debug)) != null)
        {
            if (zf._debug)
            {
                Console.WriteLine("  ZipFile::Read(): ZipEntry: {0}", e.FileName);
            }

            zf._entries.Add(e);
        }

        // read the zipfile's central directory structure here.
        zf._direntries = new System.Collections.Generic.List<ZipDirEntry>();

        ZipDirEntry de;
        while ((de = ZipDirEntry.Read(zf.ReadStream, zf._debug)) != null)
        {
            if (zf._debug)
            {
                Console.WriteLine("  ZipFile::Read(): ZipDirEntry: {0}", de.FileName);
            }

            zf._direntries.Add(de);
        }

        return zf;
    }

    public System.Collections.Generic.IEnumerator<ZipEntry> GetEnumerator()
    {
        foreach (var e in _entries)
            yield return e;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    public void ExtractAll(string path)
    {
        ExtractAll(path, false);
    }


    public void ExtractAll(string path, bool wantVerbose)
    {
        var header = wantVerbose;
        foreach (var e in _entries)
        {
            if (header)
            {
                Console.WriteLine("\n{1,-22} {2,-6} {3,4}   {4,-8}  {0}",
                    "Name", "Modified", "Size", "Ratio", "Packed");
                Console.WriteLine(new String('-', 72));
                header = false;
            }
            if (wantVerbose)
            {
                Console.WriteLine("{1,-22} {2,-6} {3,4:F0}%   {4,-8} {0}",
                    e.FileName,
                    e.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
                    e.UncompressedSize,
                    e.CompressionRatio,
                    e.CompressedSize);
            }

            e.Extract(path);
        }
    }


    public void Extract(string filename)
    {
        this[filename].Extract();
    }


    public void Extract(string filename, System.IO.Stream s)
    {
        this[filename].Extract(s);
    }


    public ZipEntry this[String filename]
    {
        get
        {
            foreach (var e in _entries)
            {
                if (e.FileName == filename)
                {
                    return e;
                }
            }
            return null;
        }
    }

    // the destructor
    ~ZipFile()
    {
        // call Dispose with false.  Since we're in the
        // destructor call, the managed resources will be
        // disposed of anyways.
        Dispose(false);
    }

    public void Dispose()
    {
        // dispose of the managed and unmanaged resources
        Dispose(true);

        // tell the GC that the Finalize process no longer needs
        // to be run for this object.
        GC.SuppressFinalize(this);
    }


    protected virtual void Dispose(bool disposeManagedResources)
    {
        if (!_disposed)
        {
            if (disposeManagedResources)
            {
                // dispose managed resources
                if (_readstream != null)
                {
                    _readstream.Dispose();
                    _readstream = null;
                }
                if (_writestream != null)
                {
                    _writestream.Dispose();
                    _writestream = null;
                }
            }
            _disposed = true;
        }
    }


    private System.IO.Stream _readstream;
    private System.IO.FileStream _writestream;
    private bool _debug;
    private bool _disposed;
    private System.Collections.Generic.List<ZipEntry> _entries;
    private System.Collections.Generic.List<ZipDirEntry> _direntries;
}

// Example usage: 
// 1. Extracting all files from a Zip file: 
//
//     try 
//     {
//       using(ZipFile zip= ZipFile.Read(ZipFile))
//       {
//         zip.ExtractAll(TargetDirectory, true);
//       }
//     }
//     catch (System.Exception ex1)
//     {
//       System.Console.Error.WriteLine("exception: " + ex1);
//     }
//
// 2. Extracting files from a zip individually:
//
//     try 
//     {
//       using(ZipFile zip= ZipFile.Read(ZipFile)) 
//       {
//         foreach (ZipEntry e in zip) 
//         {
//           e.Extract(TargetDirectory);
//         }
//       }
//     }
//     catch (System.Exception ex1)
//     {
//       System.Console.Error.WriteLine("exception: " + ex1);
//     }
//
// 3. Creating a zip archive: 
//
//     try 
//     {
//       using(ZipFile zip= new ZipFile(NewZipFile)) 
//       {
//
//         String[] filenames= System.IO.Directory.GetFiles(Directory); 
//         foreach (String filename in filenames) 
//         {
//           zip.Add(filename);
//         }
//
//         zip.Save(); 
//       }
//
//     }
//     catch (System.Exception ex1)
//     {
//       System.Console.Error.WriteLine("exception: " + ex1);
//     }
//
//
// ==================================================================
//
//
//
// Information on the ZIP format:
//
// From
// http://www.pkware.com/business_and_developers/developer/popups/appnote.txt
//
//  Overall .ZIP file format:
//
//     [local file header 1]
//     [file data 1]
//     [data descriptor 1]  ** sometimes
//     . 
//     .
//     .
//     [local file header n]
//     [file data n]
//     [data descriptor n]   ** sometimes
//     [archive decryption header] 
//     [archive extra data record] 
//     [central directory]
//     [zip64 end of central directory record]
//     [zip64 end of central directory locator] 
//     [end of central directory record]
//
// Local File Header format:
//         local file header signature     4 bytes  (0x04034b50)
//         version needed to extract       2 bytes
//         general purpose bit flag        2 bytes
//         compression method              2 bytes
//         last mod file time              2 bytes
//         last mod file date              2 bytes
//         crc-32                          4 bytes
//         compressed size                 4 bytes
//         uncompressed size               4 bytes
//         file name length                2 bytes
//         extra field length              2 bytes
//         file name                       varies
//         extra field                     varies
//
//
// Data descriptor:  (used only when bit 3 of the general purpose bitfield is set)
//         local file header signature     4 bytes  (0x08074b50)
//         crc-32                          4 bytes
//         compressed size                 4 bytes
//         uncompressed size               4 bytes
//
//
//   Central directory structure:
//
//       [file header 1]
//       .
//       .
//       . 
//       [file header n]
//       [digital signature] 
//
//
//       File header:  (This is ZipDirEntry in the code above)
//         central file header signature   4 bytes  (0x02014b50)
//         version made by                 2 bytes
//         version needed to extract       2 bytes
//         general purpose bit flag        2 bytes
//         compression method              2 bytes
//         last mod file time              2 bytes
//         last mod file date              2 bytes
//         crc-32                          4 bytes
//         compressed size                 4 bytes
//         uncompressed size               4 bytes
//         file name length                2 bytes
//         extra field length              2 bytes
//         file comment length             2 bytes
//         disk number start               2 bytes
//         internal file attributes        2 bytes
//         external file attributes        4 bytes
//         relative offset of local header 4 bytes
//         file name (variable size)
//         extra field (variable size)
//         file comment (variable size)
//
// End of central directory record:
//
//         end of central dir signature    4 bytes  (0x06054b50)
//         number of this disk             2 bytes
//         number of the disk with the
//         start of the central directory  2 bytes
//         total number of entries in the
//         central directory on this disk  2 bytes
//         total number of entries in
//         the central directory           2 bytes
//         size of the central directory   4 bytes
//         offset of start of central
//         directory with respect to
//         the starting disk number        4 bytes
//         .ZIP file comment length        2 bytes
//         .ZIP file comment       (variable size)
//
// date and time are packed values, as MSDOS did them
// time: bits 0-4 : second
//            5-10: minute
//            11-15: hour
// date  bits 0-4 : day
//            5-8: month
//            9-15 year (since 1980)
//
// see http://www.vsft.com/hal/dostime.htm