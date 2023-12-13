using CasaEngine.Framework.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CasaEngine.Core.Design;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI
{
    public class Screen : Asset
    {
        public string Name
        {
            get => AssetInfo.Name;
            set => AssetInfo.Name = value;
        }

        public List<Control> Controls { get; } = new();


        public override void Load(JsonElement element, SaveOption option)
        {
            base.Load(element.GetProperty("asset"), option);
        }
    }
}
