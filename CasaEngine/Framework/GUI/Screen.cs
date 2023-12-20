using CasaEngine.Framework.Assets;
using System.Text.Json;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI
{
    public class Screen : EntityBase
    {
        private readonly List<Control> _controls = new();

        public ExternalComponent? ExternalComponent { get; set; }

        public IEnumerable<Control> Controls => _controls;

        public override void Initialize(CasaEngineGame game)
        {
            base.Initialize(game);

            foreach (var control in Controls)
            {
                control.Initialize(Game.GameManager.UiManager);
            }

            ExternalComponent?.Initialize(this);
        }

        protected override void UpdateInternal(float elapsedTime)
        {
            base.UpdateInternal(elapsedTime);

            ExternalComponent?.Update(elapsedTime);
        }

        protected override void DrawInternal()
        {
            base.DrawInternal();

            ExternalComponent?.Draw();
        }

        public void Add(Control control)
        {
            _controls.Add(control);

#if EDITOR
            Game.GameManager.UiManager.Add(control);
#endif
        }

        public void Remove(Control control)
        {
            _controls.Remove(control);

#if EDITOR
            Game.GameManager.UiManager.Remove(control);
#endif
        }

        public Control? GetControlByName(string name)
        {
            foreach (var control in _controls)
            {
                if (control.Name == name)
                {
                    return control;
                }
            }

            return null;
        }

        public override void Load(JsonElement element, SaveOption option)
        {
            base.Load(element, option);

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
