using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// 
    /// </summary>
    [TypeConverter(typeof(AssetBuildParamConverter))]
    public abstract class AssetBuildParam
    {



        /// <summary>
        /// Gets
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        internal string SubName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        [Browsable(false)]
        public abstract string Value
        {
            get;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        protected AssetBuildParam(string name_)
        {
            if (string.IsNullOrWhiteSpace(name_) == true)
            {
                throw new ArgumentNullException("AssetBuildParams() : name is null or empty");
            }

            SetName(name_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected AssetBuildParam(XmlElement el_)
        {
            Load(el_);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        private void SetName(string name_)
        {
            SubName = name_;
            Name = "ProcessorParameters_" + name_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public void Load(XmlElement el_)
        {
            XmlNode node = el_.SelectSingleNode("Name");
            SetName(node.InnerText);
            node = el_.SelectSingleNode("Value");
            LoadValue(node.InnerText);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected abstract void LoadValue(string val_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public void Save(XmlElement el_)
        {
            XmlElement node = el_.OwnerDocument.CreateElementWithText("Name", SubName);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElementWithText("Value", Value);
            el_.AppendChild(node);
        }

        public abstract bool Compare(AssetBuildParam param_);

    }


    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamColor
        : AssetBuildParam
    {
        [Description("If the texture is color-keyed, pixels of this color are replaced with transparent black.")]
        public Color ColorKey
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return ColorKey.R + ", " + ColorKey.G + ", " + ColorKey.B + ", " + ColorKey.A; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamColor(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamColor()
            : base("ColorKeyColor")
        {
            ColorKey = new Color(255, 0, 255);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            string[] a = val_.Split(',');

            ColorKey = new Color(
                byte.Parse(a[0]),
                byte.Parse(a[1]),
                byte.Parse(a[2]));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamColor o = (AssetBuildParamColor)param_;

            if (o != null)
            {
                return ColorKey == o.ColorKey;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamColorKeyEnabled
        : AssetBuildParam
    {
        [Description("If enabled, the source texture is color keyed. Pixels matching the value of \"Color Key Color\" are replaced with transparent black.")]
        public bool ColorKeyEnabled
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return ColorKeyEnabled.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamColorKeyEnabled()
            : base("ColorKeyEnabled")
        {
            ColorKeyEnabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamColorKeyEnabled(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            ColorKeyEnabled = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamColorKeyEnabled o = (AssetBuildParamColorKeyEnabled)param_;

            if (o != null)
            {
                return ColorKeyEnabled == o.ColorKeyEnabled;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamGenerateMipmaps
        : AssetBuildParam
    {
        [Description("If enabled, a full mipmap chain is generated from the source texture. Existing mipmaps are not replaced.")]
        public bool GenerateMipmaps
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return GenerateMipmaps.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamGenerateMipmaps()
            : base("GenerateMipmaps")
        {
            GenerateMipmaps = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamGenerateMipmaps(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            GenerateMipmaps = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamGenerateMipmaps o = (AssetBuildParamGenerateMipmaps)param_;

            if (o != null)
            {
                return GenerateMipmaps == o.GenerateMipmaps;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamPremultiplyAlpha
        : AssetBuildParam
    {
        [Description("If enabled, the texture is converted to premultiplied alpha format.")]
        public bool PremultiplyAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return PremultiplyAlpha.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamPremultiplyAlpha()
            : base("PremultiplyAlpha")
        {
            PremultiplyAlpha = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamPremultiplyAlpha(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            PremultiplyAlpha = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamPremultiplyAlpha o = (AssetBuildParamPremultiplyAlpha)param_;

            if (o != null)
            {
                return PremultiplyAlpha == o.PremultiplyAlpha;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamResizeToPowerOfTwo
        : AssetBuildParam
    {
        [Description("If enabled, the texture is resized to the next largest power of two, maximizing compatibility. Many graphics cards do not support textures sizes that are not a power of two.")]
        public bool ResizeToPowerOfTwo
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return ResizeToPowerOfTwo.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamResizeToPowerOfTwo()
            : base("ResizeToPowerOfTwo")
        {
            ResizeToPowerOfTwo = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamResizeToPowerOfTwo(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            ResizeToPowerOfTwo = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamResizeToPowerOfTwo o = (AssetBuildParamResizeToPowerOfTwo)param_;

            if (o != null)
            {
                return ResizeToPowerOfTwo == o.ResizeToPowerOfTwo;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamTextureFormat
        : AssetBuildParam
    {
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Enum.GetName(typeof(TextureFormat), Format); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamTextureFormat()
            : base("TextureFormat")
        {
            Format = TextureFormat.NoChange;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamTextureFormat(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Format = (TextureFormat)Enum.Parse(typeof(TextureFormat), val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamTextureFormat o = (AssetBuildParamTextureFormat)param_;

            if (o != null)
            {
                return Format == o.Format;
            }

            return false;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamDebuggingOptions
        : AssetBuildParam
    {
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Enum.GetName(typeof(DebuggingOptions), Option); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamDebuggingOptions()
            : base("DebuggingOptions")
        {
            Option = DebuggingOptions.Auto;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamDebuggingOptions(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Option = (DebuggingOptions)Enum.Parse(typeof(DebuggingOptions), val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamDebuggingOptions o = (AssetBuildParamDebuggingOptions)param_;

            if (o != null)
            {
                return Option == o.Option;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamDefines
        : AssetBuildParam
    {
        [Description("")]
        public string Defines
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Defines; }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamDefines()
            : base("Defines")
        {
            Defines = string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamDefines(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Defines = val_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamDefines o = (AssetBuildParamDefines)param_;

            if (o != null)
            {
                return Defines == o.Defines;
            }

            return false;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamFirstCharacter
        : AssetBuildParam
    {
        [Description("")]
        public string FirstCharacter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return FirstCharacter; }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamFirstCharacter()
            : base("FirstCharacter")
        {
            FirstCharacter = string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamFirstCharacter(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            FirstCharacter = val_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamFirstCharacter o = (AssetBuildParamFirstCharacter)param_;

            if (o != null)
            {
                return FirstCharacter == o.FirstCharacter;
            }

            return false;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamDefaultEffect
        : AssetBuildParam
    {
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Enum.GetName(typeof(DefaultEffect), Effect); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamDefaultEffect()
            : base("DefaultEffect")
        {
            Effect = DefaultEffect.BasicEffect;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamDefaultEffect(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Effect = (DefaultEffect)Enum.Parse(typeof(DefaultEffect), val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamDefaultEffect o = (AssetBuildParamDefaultEffect)param_;

            if (o != null)
            {
                return Effect == o.Effect;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamPremultiplyTextureAlpha
        : AssetBuildParam
    {
        [Description("")]
        public bool PremultiplyTextureAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return PremultiplyTextureAlpha.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamPremultiplyTextureAlpha()
            : base("PremultiplyTextureAlpha")
        {
            PremultiplyTextureAlpha = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamPremultiplyTextureAlpha(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            PremultiplyTextureAlpha = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamPremultiplyTextureAlpha o = (AssetBuildParamPremultiplyTextureAlpha)param_;

            if (o != null)
            {
                return PremultiplyTextureAlpha == o.PremultiplyTextureAlpha;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamPremultiplyVertexColor
        : AssetBuildParam
    {
        [Description("")]
        public bool PremultiplyVertexColor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return PremultiplyVertexColor.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamPremultiplyVertexColor()
            : base("PremultiplyVertexColor")
        {
            PremultiplyVertexColor = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamPremultiplyVertexColor(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            PremultiplyVertexColor = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamPremultiplyVertexColor o = (AssetBuildParamPremultiplyVertexColor)param_;

            if (o != null)
            {
                return PremultiplyVertexColor == o.PremultiplyVertexColor;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamGenerateTangentFrames
        : AssetBuildParam
    {
        [Description("")]
        public bool GenerateTangentFrames
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return GenerateTangentFrames.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamGenerateTangentFrames()
            : base("GenerateTangentFrames")
        {
            GenerateTangentFrames = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamGenerateTangentFrames(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            GenerateTangentFrames = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamGenerateTangentFrames o = (AssetBuildParamGenerateTangentFrames)param_;

            if (o != null)
            {
                return GenerateTangentFrames == o.GenerateTangentFrames;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamScale
        : AssetBuildParam
    {
        [Description("")]
        public float Scale
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Scale.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamScale()
            : base("Scale")
        {
            Scale = 1.0f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamScale(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Scale = float.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamScale o = (AssetBuildParamScale)param_;

            if (o != null)
            {
                return Scale == o.Scale;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamSwapWindingOrder
        : AssetBuildParam
    {
        [Description("")]
        public bool SwapWindingOrder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return SwapWindingOrder.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamSwapWindingOrder()
            : base("SwapWindingOrder")
        {
            SwapWindingOrder = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamSwapWindingOrder(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            SwapWindingOrder = bool.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamSwapWindingOrder o = (AssetBuildParamSwapWindingOrder)param_;

            if (o != null)
            {
                return SwapWindingOrder == o.SwapWindingOrder;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamXRotation
        : AssetBuildParam
    {
        [Description("")]
        public float XRotation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return XRotation.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamXRotation()
            : base("XRotation")
        {
            XRotation = 0.0f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamXRotation(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            XRotation = float.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamXRotation o = (AssetBuildParamXRotation)param_;

            if (o != null)
            {
                return XRotation == o.XRotation;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamYRotation
        : AssetBuildParam
    {
        [Description("")]
        public float YRotation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return YRotation.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamYRotation()
            : base("YRotation")
        {
            YRotation = 0.0f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamYRotation(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            YRotation = float.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamYRotation o = (AssetBuildParamYRotation)param_;

            if (o != null)
            {
                return YRotation == o.YRotation;
            }

            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamZRotation
        : AssetBuildParam
    {
        [Description("")]
        public float ZRotation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return ZRotation.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamZRotation()
            : base("ZRotation")
        {
            ZRotation = 0.0f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamZRotation(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            ZRotation = float.Parse(val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamZRotation o = (AssetBuildParamZRotation)param_;

            if (o != null)
            {
                return ZRotation == o.ZRotation;
            }

            return false;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamCompressionQuality
        : AssetBuildParam
    {
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Enum.GetName(typeof(CompressionQuality), Quality); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamCompressionQuality()
            : base("CompressionQuality")
        {
            Quality = CompressionQuality.Best;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamCompressionQuality(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            Quality = (CompressionQuality)Enum.Parse(typeof(CompressionQuality), val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamCompressionQuality o = (AssetBuildParamCompressionQuality)param_;

            if (o != null)
            {
                return Quality == o.Quality;
            }

            return false;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamVideoSoundTrackType
        : AssetBuildParam
    {
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Gets
        /// </summary>
        public override string Value
        {
            get { return Enum.GetName(typeof(VideoSoundTrackType), TrackType); }
        }

        /// <summary>
        /// 
        /// </summary>
        public AssetBuildParamVideoSoundTrackType()
            : base("VideoSoundTrackType")
        {
            TrackType = VideoSoundTrackType.Best;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public AssetBuildParamVideoSoundTrackType(XmlElement el_)
            : base(el_)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        protected override void LoadValue(string val_)
        {
            TrackType = (VideoSoundTrackType)Enum.Parse(typeof(VideoSoundTrackType), val_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param_"></param>
        /// <returns></returns>
        public override bool Compare(AssetBuildParam param_)
        {
            AssetBuildParamVideoSoundTrackType o = (AssetBuildParamVideoSoundTrackType)param_;

            if (o != null)
            {
                return TrackType == o.TrackType;
            }

            return false;
        }
    }

}
