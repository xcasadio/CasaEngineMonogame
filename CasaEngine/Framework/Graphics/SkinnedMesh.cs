using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

public class SkinnedMesh : ObjectBase
{
    public RiggedModel RiggedModel { get; private set; }
    public Guid RiggedModelAssetId { get; set; } = Guid.Empty;

    public void Initialize(AssetContentManager assetContentManager)
    {
#if EDITOR
        if (_isInitialized)
        {
            return;
        }
#endif

        if (RiggedModelAssetId != Guid.Empty)
        {
            RiggedModel = assetContentManager.Load<RiggedModel>(RiggedModelAssetId);
        }

#if EDITOR
        _isInitialized = true;
#endif
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        RiggedModelAssetId = element["rigged_model_asset_id"].GetGuid();
    }

#if EDITOR

    private bool _isInitialized;

    public void SetRiggedModel(RiggedModel riggedModel)
    {
        RiggedModel = riggedModel;
    }

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("rigged_model_asset_id", RiggedModelAssetId);
    }
#endif
}