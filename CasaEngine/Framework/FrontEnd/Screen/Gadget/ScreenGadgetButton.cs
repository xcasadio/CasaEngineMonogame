using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

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

    public ScreenGadgetButton(JsonElement element, SaveOption option)
        : base(element, option)
    {

    }

    /*public override void Update(float elapsedTime_)
    {
        
    }*/

    public override void DrawGadget(float elapsedTime)
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

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        var spriteId = element.GetProperty("image").GetInt32();

        if (spriteId != int.MaxValue)
        {
            //Image = GameManager.Asset2dManager.GetSprite2DById(SpriteId);
            //GameManager.Asset2dManager.AddSprite2DToLoadingList(Image);
        }

        SizeImage = element.GetProperty("size_image").GetEnum<SizeImage>();
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

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //var SpriteId = Image == null ? int.MaxValue : Image.Id;
        //node = el.OwnerDocument.CreateElementWithText("Image", SpriteId.ToString());
        //el.AppendChild(node);
        jObject.Add("size_image", SizeImage.ConvertToString());
    }

#endif
}