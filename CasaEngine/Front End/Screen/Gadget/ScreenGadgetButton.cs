using System.Xml;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine.Game;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public
#if EDITOR
    partial
#endif
    class ScreenGadgetButton
        : ScreenGadget
    {

        //public event EventHandler Click;



        public SizeImage SizeImage
        {
            get;
            set;
        }

        public Sprite2D Image
        {
            get;
            set;
        }



        public ScreenGadgetButton(XmlElement el, SaveOption opt)
            : base(el, opt)
        {

        }



        /*public override void Update(float elapsedTime_)
        {
            
        }*/

#if EDITOR
        public
#else
        protected
#endif
        override void DrawGadget(float elapsedTime)
        {
            Rectangle area = new Rectangle((int)Location.X, (int)Location.Y, Width, Height);

            Renderer2DComponent.AddSprite2D(
                WhiteTexture,
                Location,
                0.0f,
                new Vector2(Width, Height) /* * Scale*/,
                BackgroundColor,
                0.0003f,
                SpriteEffects.None);

            Renderer2DComponent.AddBox(
                Location.X,
                Location.Y,
                Width,
                Height,
                Color.Black,
                0.0002f);

            if (Image != null)
            {
                Renderer2DComponent.AddSprite2D(
                    Image.Id,
                    Location,
                    0.0f,
                    new Vector2((float)Width / (float)Image.PositionInTexture.Width, (float)Height / (float)Image.PositionInTexture.Height),
                    Color.White,
                    0.0004f,
                    SpriteEffects.None,
                    area);
            }

            Renderer2DComponent.AddText2D(
                Font,
                Text,
                Location + Vector2.One * 5,
                0.0f,
                Vector2.One,
                FontColor,
                0.0f,
                area);
        }

        public override void Load(XmlElement el, SaveOption opt)
        {
            base.Load(el, opt);

            int spriteId = int.Parse(el.SelectSingleNode("Image").InnerText);

            if (spriteId != int.MaxValue)
            {
                Image = Engine.Instance.Asset2DManager.GetSprite2DById(spriteId);
                Engine.Instance.Asset2DManager.AddSprite2DToLoadingList(Image);
            }

            SizeImage = (SizeImage)Enum.Parse(typeof(SizeImage), el.SelectSingleNode("SizeImage").InnerText);
        }

    }
}
