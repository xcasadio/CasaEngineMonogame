using CasaEngine.Framework.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI
{
    public class Screen : Asset
    {
        private CasaEngineGame _game;
        private readonly List<Control> _controls = new List<Control>();

        public string Name
        {
            get => AssetInfo.Name;
            set => AssetInfo.Name = value;
        }

        public IEnumerable<Control> Controls => _controls;


        public override void Load(JsonElement element, SaveOption option)
        {
            base.Load(element.GetProperty("asset"), option);
        }

        public void Initialize(CasaEngineGame game)
        {
            _game = game;
        }

        public void Add(Control control)
        {
            _controls.Add(control);

#if EDITOR
            _game.GameManager.UiManager.Add(control);
#endif
        }

        public void Remove(Control control)
        {
            _controls.Remove(control);

#if EDITOR
            _game.GameManager.UiManager.Remove(control);
#endif
        }
    }
}
