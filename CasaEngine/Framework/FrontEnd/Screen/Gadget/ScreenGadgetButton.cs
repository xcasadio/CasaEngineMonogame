using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget;

public class ScreenGadgetButton : ScreenGadget
{
    //public event EventHandler Click;

    public SizeImage SizeImage
    {
        get;
        set;
    }

    //public Sprite2D Image
    //{
    //    get;
    //    set;
    //}

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
        var area = new Rectangle((int)Location.X, (int)Location.Y, Width, Height);

        Renderer2dComponent.DrawRectangle(Location.X, Location.Y, Width, Height, BackgroundColor, 0.0003f);
        Renderer2dComponent.DrawRectangle(Location.X, Location.Y, Width, Height, Color.Black, 0.0002f);

        //if (Image != null)
        //{
        //    Renderer2dComponent.AddSprite2D(
        //        Image.Id,
        //        Location,
        //        0.0f,
        //        new Vector2(Width / (float)Image.PositionInTexture.Width, Height / (float)Image.PositionInTexture.Height),
        //        Color.White,
        //        0.0004f,
        //        SpriteEffects.None,
        //        area);
        //}

        Renderer2dComponent.DrawText(
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

        var spriteId = int.Parse(el.SelectSingleNode("Image").InnerText);

        if (spriteId != int.MaxValue)
        {
            //Image = GameManager.Asset2dManager.GetSprite2DById(SpriteId);
            //GameManager.Asset2dManager.AddSprite2DToLoadingList(Image);
        }

        SizeImage = (SizeImage)Enum.Parse(typeof(SizeImage), el.SelectSingleNode("SizeImage").InnerText);
    }

#if EDITOR
    public static int Num;

    public ScreenGadgetButton()
        : base("Button" + (Num++))
    {
        Width = 200;
        Height = 80;
        FontColor = Color.Black;
        BackgroundColor = Color.White;
    }

    public override void Save(XmlElement el, SaveOption opt)
    {
        XmlElement node;

        base.Save(el, opt);

        //var SpriteId = Image == null ? int.MaxValue : Image.Id;
        //node = el.OwnerDocument.CreateElementWithText("Image", SpriteId.ToString());
        //el.AppendChild(node);
        node = el.OwnerDocument.CreateElementWithText("SizeImage", Enum.GetName(typeof(SizeImage), SizeImage));
        el.AppendChild(node);
    }

    public override void Save(BinaryWriter bw, SaveOption opt)
    {
        base.Save(bw, opt);

        //var SpriteId = Image == null ? int.MaxValue : Image.Id;
        //bw.Write(SpriteId);
        bw.Write((int)SizeImage);
    }
#endif
}