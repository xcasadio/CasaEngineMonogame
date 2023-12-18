using CasaEngine.Framework.Assets;
using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI
{
    public class Screen : Asset
    {
        private CasaEngineGame _game;
        private readonly List<Control> _controls = new();

        private ExternalComponent ExternalComponent { get; set; }

        public string Name
        {
            get => AssetInfo.Name;
            set => AssetInfo.Name = value;
        }

        public IEnumerable<Control> Controls => _controls;


        public void Initialize(CasaEngineGame game)
        {
            _game = game;

            foreach (var control in Controls)
            {
                control.Initialize(_game.GameManager.UiManager);
            }
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

        public override void Load(JsonElement element, SaveOption option)
        {
            base.Load(element.GetProperty("asset"), option);

            var externalComponentElement = element.GetProperty("external_component");
            if (externalComponentElement.GetString() != "null")
            {
                //ExternalComponent = ;
            }

            foreach (var controlElement in element.GetProperty("controls").EnumerateArray())
            {
                var typeName = controlElement.GetProperty("type").GetString();

                var control = ControlHelper.Load(typeName, controlElement);
                _controls.Add(control);
            }
        }

#if EDITOR
        public override void Save(JObject jObject, SaveOption option)
        {
            base.Save(jObject, option);

            if (ExternalComponent != null)
            {
                var externalComponentNode = new JObject();
                ExternalComponent.Save(externalComponentNode, option);
                jObject.Add("external_component", externalComponentNode);
            }
            else
            {
                jObject.Add("external_component", "null");
            }

            var controlsNode = new JArray();

            foreach (var control in _controls)
            {
                var controlNode = new JObject();
                control.Save(controlNode);
                controlsNode.Add(controlNode);
            }

            jObject.Add("controls", controlsNode);
        }
#endif
    }
}
