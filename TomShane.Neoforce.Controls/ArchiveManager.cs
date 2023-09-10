using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;
using TomShane.Neoforce.External.Zip;

namespace TomShane.Neoforce.Controls;

/// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Class[@name="ArchiveManager"]/*' />          
public class ArchiveManager : ContentManager
{
    private ZipFile _archive;

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="ArchivePath"]/*' />          
    public virtual string ArchivePath { get; }

    public bool UseArchive { get; set; }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="ArchiveManager"]/*' />              
    public ArchiveManager(IServiceProvider serviceProvider) : this(serviceProvider, null) { }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="ArchiveManager1"]/*' />                  
    public ArchiveManager(IServiceProvider serviceProvider, string archive) : base(serviceProvider)
    {
        if (archive != null)
        {
            _archive = ZipFile.Read(archive);
            ArchivePath = archive;
            UseArchive = true;
        }
    }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="OpenStream"]/*' />
    protected override Stream OpenStream(string assetName)
    {
        if (UseArchive && _archive != null)
        {
            assetName = assetName.Replace("\\", "/");
            if (assetName.StartsWith("/"))
            {
                assetName = assetName.Remove(0, 1);
            }

            var fullAssetName = (assetName + ".xnb").ToLower();

            foreach (var entry in _archive)
            {
                var ze = new ZipDirEntry(entry);

                var entryName = entry.FileName.ToLower();

                if (entryName == fullAssetName)
                {
                    return entry.GetStream();
                }
            }
            throw new Exception("Cannot find asset \"" + assetName + "\" in the archive.");
        }

        return base.OpenStream(assetName);
    }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="GetAssetNames"]/*' />
    public string[] GetAssetNames()
    {
        if (UseArchive && _archive != null)
        {
            var filenames = new List<string>();

            foreach (var entry in _archive)
            {
                var name = entry.FileName;
                if (name.EndsWith(".xnb"))
                {
                    name = name.Remove(name.Length - 4, 4);
                    filenames.Add(name);
                }
            }
            return filenames.ToArray();
        }

        return null;
    }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="GetAssetNames1"]/*' />        
    public string[] GetAssetNames(string path)
    {
        if (UseArchive && _archive != null)
        {
            if (path != null && path != "" && path != "\\" && path != "/")
            {
                var filenames = new List<string>();

                foreach (var entry in _archive)
                {
                    var name = entry.FileName;
                    if (name.EndsWith(".xnb"))
                    {
                        name = name.Remove(name.Length - 4, 4);
                    }

                    var parts = name.Split('/');
                    var dir = "";
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        dir += parts[i] + '/';
                    }

                    path = path.Replace("\\", "/");
                    if (path.StartsWith("/"))
                    {
                        path = path.Remove(0, 1);
                    }

                    if (!path.EndsWith("/"))
                    {
                        path += '/';
                    }

                    if (dir.ToLower() == path.ToLower() && !name.EndsWith("/"))
                    {
                        filenames.Add(name);
                    }
                }
                return filenames.ToArray();
            }

            return GetAssetNames();
        }

        return null;
    }

    /// <include file='Documents/ArchiveManager.xml' path='ArchiveManager/Member[@name="GetFileStream"]/*' />
    public Stream GetFileStream(string filename)
    {
        if (UseArchive && _archive != null)
        {
            filename = filename.Replace("\\", "/").ToLower();
            if (filename.StartsWith("/"))
            {
                filename = filename.Remove(0, 1);
            }

            foreach (var entry in _archive)
            {
                var entryName = entry.FileName.ToLower();

                if (entryName.Equals(filename))
                {
                    return entry.GetStream();
                }
            }

            throw new Exception("Cannot find file \"" + filename + "\" in the archive.");
        }

        return null;
    }

    public string[] GetDirectories(string path)
    {
        if (UseArchive && _archive != null)
        {
            if (path != null && path != "" && path != "\\" && path != "/")
            {
                var dirs = new List<string>();

                path = path.Replace("\\", "/");
                if (path.StartsWith("/"))
                {
                    path = path.Remove(0, 1);
                }

                if (!path.EndsWith("/"))
                {
                    path += '/';
                }

                foreach (var entry in _archive)
                {
                    var name = entry.FileName;
                    if (name.ToLower().StartsWith(path.ToLower()))
                    {
                        var i = name.IndexOf("/", path.Length);
                        var item = name.Substring(path.Length, i - path.Length) + "\\";
                        if (!dirs.Contains(item))
                        {
                            dirs.Add(item);
                        }
                    }
                }
                return dirs.ToArray();
            }

            return GetAssetNames();
        }

        if (Directory.Exists(path))
        {
            var dirs = Directory.GetDirectories(path);

            for (var i = 0; i < dirs.Length; i++)
            {
                var parts = dirs[i].Split('\\');
                dirs[i] = parts[parts.Length - 1] + '\\';
            }

            return dirs;
        }
        return null;
    }

}