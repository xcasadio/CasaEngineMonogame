﻿using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfo
{
    private string _name;

    public long Id { get; private set; }

    public string FileName { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (!string.IsNullOrEmpty(value) && _name != value)
            {
                _name = value;
            }
        }
    }

    public AssetInfo(bool initialize = true)
    {
        if (initialize)
        {
            Id = IdManager.GetId();
            _name = "Asset_" + Id;
        }
    }

    public virtual void Load(JsonElement element, SaveOption option)
    {
        Id = element.GetJsonPropertyByName("id").Value.GetInt64();
        Name = element.GetJsonPropertyByName("name").Value.GetString();
        FileName = element.GetJsonPropertyByName("file_name").Value.GetString();
    }

#if EDITOR
    public virtual void Save(JObject jObject, SaveOption option)
    {
        var assetObject = new JObject(
            new JProperty("id", Id),
            new JProperty("name", Name),
            new JProperty("file_name", FileName));

        jObject.Add("asset", assetObject);
    }
#endif
}