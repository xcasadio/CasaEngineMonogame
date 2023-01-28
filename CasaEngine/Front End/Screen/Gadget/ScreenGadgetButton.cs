using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Graphics2D;
using CasaEngine.Game;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class ScreenGadgetButton
        : ScreenGadget
    {

        //public event EventHandler Click;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public SizeImage SizeImage
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Sprite2D Image
        {
            get;
            set;
        }



        /// <summary>
        /// 
        /// </summary>
        public ScreenGadgetButton(XmlElement el_, SaveOption opt_)
            : base(el_, opt_)
        {

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        /*public override void Update(float elapsedTime_)
        {
            
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
#if EDITOR
        public
#else
        protected
#endif
        override void DrawGadget(float elapsedTime_)
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
                    Image.ID,
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            int spriteID = int.Parse(el_.SelectSingleNode("Image").InnerText);

            if (spriteID != int.MaxValue)
            {
                Image = Engine.Instance.Asset2DManager.GetSprite2DByID(spriteID);
                Engine.Instance.Asset2DManager.AddSprite2DToLoadingList(Image);
            }

            SizeImage = (SizeImage)Enum.Parse(typeof(SizeImage), el_.SelectSingleNode("SizeImage").InnerText);
        }

    }
}
