using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Models;

public class SkinnedMesh : ObjectBase
{
    public RiggedModel RiggedModel { get; private set; }
    public Guid RiggedModelAssetId { get; set; } = Guid.Empty;

    public void Initialize(AssetContentManager assetContentManager)
    {
        if (_isInitialized)
        {
            return;
        }

        if (RiggedModelAssetId != Guid.Empty)
        {
            RiggedModel = assetContentManager.Load<RiggedModel>(RiggedModelAssetId);
        }

        _isInitialized = true;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        RiggedModelAssetId = element["rigged_model_asset_id"].GetGuid();
    }

    private bool _isInitialized;

    public void SetRiggedModel(RiggedModel riggedModel)
    {
        RiggedModel = riggedModel;
    }
}