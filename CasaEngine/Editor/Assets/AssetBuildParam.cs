using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Extension;

namespace CasaEngine.Editor.Assets
{
    [TypeConverter(typeof(AssetBuildParamConverter))]
    public abstract class AssetBuildParam
    {



        public string Name
        {
            get;
            private set;
        }

        internal string SubName
        {
            get;
            private set;
        }

        [Browsable(false)]
        public abstract string Value
        {
            get;
        }



        protected AssetBuildParam(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("AssetBuildParams() : name is null or empty");
            }

            SetName(name);
        }

        protected AssetBuildParam(XmlElement el)
        {
            Load(el);
        }



        private void SetName(string name)
        {
            SubName = name;
            Name = "ProcessorParameters_" + name;
        }

        public void Load(XmlElement el)
        {
            var node = el.SelectSingleNode("Name");
            SetName(node.InnerText);
            node = el.SelectSingleNode("Value");
            LoadValue(node.InnerText);
        }

        protected abstract void LoadValue(string val);

        public void Save(XmlElement el)
        {
            var node = el.OwnerDocument.CreateElementWithText("Name", SubName);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElementWithText("Value", Value);
            el.AppendChild(node);
        }

        public abstract bool Compare(AssetBuildParam para);

    }


    public class AssetBuildParamColor
        : AssetBuildParam
    {
        [Description("If the texture is color-keyed, pixels of this color are replaced with transparent black.")]
        public Color ColorKey
        {
            get;
            set;
        }

        public override string Value => ColorKey.R + ", " + ColorKey.G + ", " + ColorKey.B + ", " + ColorKey.A;

        public AssetBuildParamColor(XmlElement el)
            : base(el)
        { }

        public AssetBuildParamColor()
            : base("ColorKeyColor")
        {
            ColorKey = new Color(255, 0, 255);
        }

        protected override void LoadValue(string val)
        {
            var a = val.Split(',');

            ColorKey = new Color(
                byte.Parse(a[0]),
                byte.Parse(a[1]),
                byte.Parse(a[2]));
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamColor)para;

            if (o != null)
            {
                return ColorKey == o.ColorKey;
            }

            return false;
        }
    }

    public class AssetBuildParamColorKeyEnabled
        : AssetBuildParam
    {
        [Description("If enabled, the source texture is color keyed. Pixels matching the value of \"Color Key Color\" are replaced with transparent black.")]
        public bool ColorKeyEnabled
        {
            get;
            set;
        }

        public override string Value => ColorKeyEnabled.ToString();

        public AssetBuildParamColorKeyEnabled()
            : base("ColorKeyEnabled")
        {
            ColorKeyEnabled = true;
        }

        public AssetBuildParamColorKeyEnabled(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            ColorKeyEnabled = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamColorKeyEnabled)para;

            if (o != null)
            {
                return ColorKeyEnabled == o.ColorKeyEnabled;
            }

            return false;
        }
    }

    public class AssetBuildParamGenerateMipmaps
        : AssetBuildParam
    {
        [Description("If enabled, a full mipmap chain is generated from the source texture. Existing mipmaps are not replaced.")]
        public bool GenerateMipmaps
        {
            get;
            set;
        }

        public override string Value => GenerateMipmaps.ToString();

        public AssetBuildParamGenerateMipmaps()
            : base("GenerateMipmaps")
        {
            GenerateMipmaps = true;
        }

        public AssetBuildParamGenerateMipmaps(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            GenerateMipmaps = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamGenerateMipmaps)para;

            if (o != null)
            {
                return GenerateMipmaps == o.GenerateMipmaps;
            }

            return false;
        }
    }

    public class AssetBuildParamPremultiplyAlpha
        : AssetBuildParam
    {
        [Description("If enabled, the texture is converted to premultiplied alpha format.")]
        public bool PremultiplyAlpha
        {
            get;
            set;
        }

        public override string Value => PremultiplyAlpha.ToString();

        public AssetBuildParamPremultiplyAlpha()
            : base("PremultiplyAlpha")
        {
            PremultiplyAlpha = true;
        }

        public AssetBuildParamPremultiplyAlpha(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            PremultiplyAlpha = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamPremultiplyAlpha)para;

            if (o != null)
            {
                return PremultiplyAlpha == o.PremultiplyAlpha;
            }

            return false;
        }
    }

    public class AssetBuildParamResizeToPowerOfTwo
        : AssetBuildParam
    {
        [Description("If enabled, the texture is resized to the next largest power of two, maximizing compatibility. Many graphics cards do not support textures sizes that are not a power of two.")]
        public bool ResizeToPowerOfTwo
        {
            get;
            set;
        }

        public override string Value => ResizeToPowerOfTwo.ToString();

        public AssetBuildParamResizeToPowerOfTwo()
            : base("ResizeToPowerOfTwo")
        {
            ResizeToPowerOfTwo = true;
        }

        public AssetBuildParamResizeToPowerOfTwo(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            ResizeToPowerOfTwo = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamResizeToPowerOfTwo)para;

            if (o != null)
            {
                return ResizeToPowerOfTwo == o.ResizeToPowerOfTwo;
            }

            return false;
        }
    }

    public class AssetBuildParamTextureFormat
        : AssetBuildParam
    {
        public enum TextureFormat
        {
            NoChange,
            Color,
            DxtCompressed
        }

        [Description("Specifies the SurfaceFormat type of processed textures. Textures can either remain unchanged from the source asset, converted to the Color format, or DXT compressed.")]
        public TextureFormat Format
        {
            get;
            set;
        }

        public override string Value => Enum.GetName(typeof(TextureFormat), Format);

        public AssetBuildParamTextureFormat()
            : base("TextureFormat")
        {
            Format = TextureFormat.NoChange;
        }

        public AssetBuildParamTextureFormat(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Format = (TextureFormat)Enum.Parse(typeof(TextureFormat), val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamTextureFormat)para;

            if (o != null)
            {
                return Format == o.Format;
            }

            return false;
        }
    }



    public class AssetBuildParamDebuggingOptions
        : AssetBuildParam
    {
        public enum DebuggingOptions
        {
            Auto,
            Debug,
            Optimize
        }

        [Description("")]
        public DebuggingOptions Option
        {
            get;
            set;
        }

        public override string Value => Enum.GetName(typeof(DebuggingOptions), Option);

        public AssetBuildParamDebuggingOptions()
            : base("DebuggingOptions")
        {
            Option = DebuggingOptions.Auto;
        }

        public AssetBuildParamDebuggingOptions(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Option = (DebuggingOptions)Enum.Parse(typeof(DebuggingOptions), val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamDebuggingOptions)para;

            if (o != null)
            {
                return Option == o.Option;
            }

            return false;
        }
    }

    public class AssetBuildParamDefines
        : AssetBuildParam
    {
        [Description("")]
        public string Defines
        {
            get;
            set;
        }

        public override string Value => Defines;

        public AssetBuildParamDefines()
            : base("Defines")
        {
            Defines = string.Empty;
        }

        public AssetBuildParamDefines(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Defines = val;
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamDefines)para;

            if (o != null)
            {
                return Defines == o.Defines;
            }

            return false;
        }
    }



    public class AssetBuildParamFirstCharacter
        : AssetBuildParam
    {
        [Description("")]
        public string FirstCharacter
        {
            get;
            set;
        }

        public override string Value => FirstCharacter;

        public AssetBuildParamFirstCharacter()
            : base("FirstCharacter")
        {
            FirstCharacter = string.Empty;
        }

        public AssetBuildParamFirstCharacter(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            FirstCharacter = val;
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamFirstCharacter)para;

            if (o != null)
            {
                return FirstCharacter == o.FirstCharacter;
            }

            return false;
        }
    }



    public class AssetBuildParamDefaultEffect
        : AssetBuildParam
    {
        public enum DefaultEffect
        {
            BasicEffect,
            SkinnedEffect,
            EnvironmantEffect,
            DUalTextureEffect,
            AlphaTextureEffect
        }

        [Description("")]
        public DefaultEffect Effect
        {
            get;
            set;
        }

        public override string Value => Enum.GetName(typeof(DefaultEffect), Effect);

        public AssetBuildParamDefaultEffect()
            : base("DefaultEffect")
        {
            Effect = DefaultEffect.BasicEffect;
        }

        public AssetBuildParamDefaultEffect(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Effect = (DefaultEffect)Enum.Parse(typeof(DefaultEffect), val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamDefaultEffect)para;

            if (o != null)
            {
                return Effect == o.Effect;
            }

            return false;
        }
    }

    public class AssetBuildParamPremultiplyTextureAlpha
        : AssetBuildParam
    {
        [Description("")]
        public bool PremultiplyTextureAlpha
        {
            get;
            set;
        }

        public override string Value => PremultiplyTextureAlpha.ToString();

        public AssetBuildParamPremultiplyTextureAlpha()
            : base("PremultiplyTextureAlpha")
        {
            PremultiplyTextureAlpha = false;
        }

        public AssetBuildParamPremultiplyTextureAlpha(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            PremultiplyTextureAlpha = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamPremultiplyTextureAlpha)para;

            if (o != null)
            {
                return PremultiplyTextureAlpha == o.PremultiplyTextureAlpha;
            }

            return false;
        }
    }

    public class AssetBuildParamPremultiplyVertexColor
        : AssetBuildParam
    {
        [Description("")]
        public bool PremultiplyVertexColor
        {
            get;
            set;
        }

        public override string Value => PremultiplyVertexColor.ToString();

        public AssetBuildParamPremultiplyVertexColor()
            : base("PremultiplyVertexColor")
        {
            PremultiplyVertexColor = false;
        }

        public AssetBuildParamPremultiplyVertexColor(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            PremultiplyVertexColor = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamPremultiplyVertexColor)para;

            if (o != null)
            {
                return PremultiplyVertexColor == o.PremultiplyVertexColor;
            }

            return false;
        }
    }

    public class AssetBuildParamGenerateTangentFrames
        : AssetBuildParam
    {
        [Description("")]
        public bool GenerateTangentFrames
        {
            get;
            set;
        }

        public override string Value => GenerateTangentFrames.ToString();

        public AssetBuildParamGenerateTangentFrames()
            : base("GenerateTangentFrames")
        {
            GenerateTangentFrames = false;
        }

        public AssetBuildParamGenerateTangentFrames(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            GenerateTangentFrames = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamGenerateTangentFrames)para;

            if (o != null)
            {
                return GenerateTangentFrames == o.GenerateTangentFrames;
            }

            return false;
        }
    }

    public class AssetBuildParamScale
        : AssetBuildParam
    {
        [Description("")]
        public float Scale
        {
            get;
            set;
        }

        public override string Value => Scale.ToString();

        public AssetBuildParamScale()
            : base("Scale")
        {
            Scale = 1.0f;
        }

        public AssetBuildParamScale(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Scale = float.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamScale)para;

            if (o != null)
            {
                return Scale == o.Scale;
            }

            return false;
        }
    }

    public class AssetBuildParamSwapWindingOrder
        : AssetBuildParam
    {
        [Description("")]
        public bool SwapWindingOrder
        {
            get;
            set;
        }

        public override string Value => SwapWindingOrder.ToString();

        public AssetBuildParamSwapWindingOrder()
            : base("SwapWindingOrder")
        {
            SwapWindingOrder = false;
        }

        public AssetBuildParamSwapWindingOrder(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            SwapWindingOrder = bool.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamSwapWindingOrder)para;

            if (o != null)
            {
                return SwapWindingOrder == o.SwapWindingOrder;
            }

            return false;
        }
    }

    public class AssetBuildParamXRotation
        : AssetBuildParam
    {
        [Description("")]
        public float XRotation
        {
            get;
            set;
        }

        public override string Value => XRotation.ToString();

        public AssetBuildParamXRotation()
            : base("XRotation")
        {
            XRotation = 0.0f;
        }

        public AssetBuildParamXRotation(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            XRotation = float.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamXRotation)para;

            if (o != null)
            {
                return XRotation == o.XRotation;
            }

            return false;
        }
    }

    public class AssetBuildParamYRotation
        : AssetBuildParam
    {
        [Description("")]
        public float YRotation
        {
            get;
            set;
        }

        public override string Value => YRotation.ToString();

        public AssetBuildParamYRotation()
            : base("YRotation")
        {
            YRotation = 0.0f;
        }

        public AssetBuildParamYRotation(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            YRotation = float.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamYRotation)para;

            if (o != null)
            {
                return YRotation == o.YRotation;
            }

            return false;
        }
    }

    public class AssetBuildParamZRotation
        : AssetBuildParam
    {
        [Description("")]
        public float ZRotation
        {
            get;
            set;
        }

        public override string Value => ZRotation.ToString();

        public AssetBuildParamZRotation()
            : base("ZRotation")
        {
            ZRotation = 0.0f;
        }

        public AssetBuildParamZRotation(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            ZRotation = float.Parse(val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamZRotation)para;

            if (o != null)
            {
                return ZRotation == o.ZRotation;
            }

            return false;
        }
    }



    public class AssetBuildParamCompressionQuality
        : AssetBuildParam
    {
        public enum CompressionQuality
        {
            Low,
            Medium,
            Best
        }

        [Description("")]
        public CompressionQuality Quality
        {
            get;
            set;
        }

        public override string Value => Enum.GetName(typeof(CompressionQuality), Quality);

        public AssetBuildParamCompressionQuality()
            : base("CompressionQuality")
        {
            Quality = CompressionQuality.Best;
        }

        public AssetBuildParamCompressionQuality(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            Quality = (CompressionQuality)Enum.Parse(typeof(CompressionQuality), val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamCompressionQuality)para;

            if (o != null)
            {
                return Quality == o.Quality;
            }

            return false;
        }
    }



    public class AssetBuildParamVideoSoundTrackType
        : AssetBuildParam
    {
        public enum VideoSoundTrackType
        {
            Low,
            Medium,
            Best
        }

        [Description("")]
        public VideoSoundTrackType TrackType
        {
            get;
            set;
        }

        public override string Value => Enum.GetName(typeof(VideoSoundTrackType), TrackType);

        public AssetBuildParamVideoSoundTrackType()
            : base("VideoSoundTrackType")
        {
            TrackType = VideoSoundTrackType.Best;
        }

        public AssetBuildParamVideoSoundTrackType(XmlElement el)
            : base(el)
        { }

        protected override void LoadValue(string val)
        {
            TrackType = (VideoSoundTrackType)Enum.Parse(typeof(VideoSoundTrackType), val);
        }

        public override bool Compare(AssetBuildParam para)
        {
            var o = (AssetBuildParamVideoSoundTrackType)para;

            if (o != null)
            {
                return TrackType == o.TrackType;
            }

            return false;
        }
    }

}
