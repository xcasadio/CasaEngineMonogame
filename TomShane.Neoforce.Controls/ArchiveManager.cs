using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;
using TomShane.Neoforce.Controls.Skins;
using TomShane.Neoforce.External.Zip;

namespace TomShane.Neoforce.Controls;

public class ArchiveManager : ContentManager, IArchiveManager
{
    private ZipFile _archive;

    public virtual string ArchivePath { get; }

    public bool UseArchive { get; set; }

    public ArchiveManager(IServiceProvider serviceProvider) : this(serviceProvider, null) { }

    public ArchiveManager(IServiceProvider serviceProvider, string archive) : base(serviceProvider)
    {
        if (archive != null)
        {
            _archive = ZipFile.Read(archive);
            ArchivePath = archive;
            UseArchive = true;
        }
    }

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
                    if (name.ToLower().StartsWith(path, StringComparison.InvariantCultureIgnoreCase))
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
                dirs[i] = parts[^1] + '\\';
            }

            return dirs;
        }
        return null;
    }
}