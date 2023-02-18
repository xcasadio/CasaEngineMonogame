using System.Xml;
using CasaEngine.Core.Design;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget
{
    public
#if EDITOR
    partial
#endif
    class ScreenGadgetLabel
        : ScreenGadget
    {





        public ScreenGadgetLabel(XmlElement el, SaveOption opt)
            : base(el, opt)
        {

        }



#if EDITOR
        public
#else
        protected
#endif
        override void DrawGadget(float elapsedTime)
        {
            Renderer2DComponent.AddSprite2D(
                WhiteTexture,
                Location,
                0.0f,
                Scale,
                BackgroundColor,
                0.0001f,
                SpriteEffects.None);

            Renderer2DComponent.AddText2D(
                Font,
                Text,
                Location + Vector2.One * 5,
                0.0f,
                Scale,
                FontColor,
                0.0f);
        }

    }
}
