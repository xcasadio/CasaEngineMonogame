using System.ComponentModel;
using System.Text.Json;

namespace CasaEngine.Framework.Entities.Components
{
    [DisplayName("GamePlay")]
    public class GamePlayComponent : Component
    {
        public static readonly int ComponentId = (int)ComponentIds.GamePlay;

        public IExternalComponent? ExternalComponent { get; set; }

        public GamePlayComponent(Entity entity) : base(entity, ComponentId)
        {
        }

        public override void Initialize()
        {
            ExternalComponent?.Initialize();
        }

        public override void Update(float elapsedTime)
        {
            ExternalComponent?.Update(elapsedTime);
        }

        public override void Draw()
        {
            ExternalComponent?.Draw();
        }

        public override void Load(JsonElement element)
        {
            throw new NotImplementedException();
        }
    }
}