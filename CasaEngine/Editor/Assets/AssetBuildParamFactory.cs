using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// 
    /// </summary>
    static public class AssetBuildParamFactory
    {
        /// <summary>
        /// 
        /// </summary>
        public enum AssetBuildParamType
        {
            Texture,
            Font,
            Audio,
            Video,
            Model,
            Effect,
            Xml
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="params_"></param>
        /// <param name="type_"></param>
        static public void SetBuildParams(ref AssetBuildParamCollection params_, AssetBuildParamType type_)
        {
            switch (type_)
            {
                case AssetBuildParamType.Texture:
                    params_.Add(new AssetBuildParamColor());
                    params_.Add(new AssetBuildParamColorKeyEnabled());
                    params_.Add(new AssetBuildParamGenerateMipmaps());
                    params_.Add(new AssetBuildParamPremultiplyAlpha());
                    params_.Add(new AssetBuildParamResizeToPowerOfTwo());
                    params_.Add(new AssetBuildParamTextureFormat());
                    break;

                case AssetBuildParamType.Effect:
                    params_.Add(new AssetBuildParamDebuggingOptions());
                    params_.Add(new AssetBuildParamDefines());
                    break;

                case AssetBuildParamType.Model:
                    params_.Add(new AssetBuildParamColor());
                    params_.Add(new AssetBuildParamColorKeyEnabled());
                    params_.Add(new AssetBuildParamGenerateMipmaps());
                    params_.Add(new AssetBuildParamPremultiplyAlpha());
                    params_.Add(new AssetBuildParamResizeToPowerOfTwo());
                    params_.Add(new AssetBuildParamTextureFormat());

                    params_.Add(new AssetBuildParamDefaultEffect());
                    params_.Add(new AssetBuildParamPremultiplyTextureAlpha());
                    params_.Add(new AssetBuildParamPremultiplyVertexColor());
                    params_.Add(new AssetBuildParamGenerateTangentFrames());
                    params_.Add(new AssetBuildParamScale());
                    params_.Add(new AssetBuildParamSwapWindingOrder());
                    params_.Add(new AssetBuildParamXRotation());
                    params_.Add(new AssetBuildParamYRotation());
                    params_.Add(new AssetBuildParamZRotation());
                    break;

                case AssetBuildParamType.Audio:
                    params_.Add(new AssetBuildParamCompressionQuality());
                    break;

                case AssetBuildParamType.Video:
                    params_.Add(new AssetBuildParamVideoSoundTrackType());
                    break;

                case AssetBuildParamType.Font:
                    throw new NotImplementedException();
                    break;

                case AssetBuildParamType.Xml:
                    throw new NotImplementedException();
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="params_"></param>
        static public AssetBuildParam Load(XmlElement el_)
        {
            AssetBuildParam param;

            string name = el_.SelectSingleNode("Name").InnerText;

            switch (name)
            {
                case "ColorKeyColor":
                    param = new AssetBuildParamColor(el_);
                    break;

                case "ColorKeyEnabled":
                    param = new AssetBuildParamColorKeyEnabled(el_);
                    break;

                case "GenerateMipmaps":
                    param = new AssetBuildParamGenerateMipmaps(el_);
                    break;

                case "PremultiplyAlpha":
                    param = new AssetBuildParamPremultiplyAlpha(el_);
                    break;

                case "ResizeToPowerOfTwo":
                    param = new AssetBuildParamResizeToPowerOfTwo(el_);
                    break;

                case "TextureFormat":
                    param = new AssetBuildParamTextureFormat(el_);
                    break;

                case "DebuggingOptions":
                    param = new AssetBuildParamDebuggingOptions(el_);
                    break;

                case "Defines":
                    param = new AssetBuildParamDefines(el_);
                    break;

                case "FirstCharacter":
                    param = new AssetBuildParamFirstCharacter(el_);
                    break;

                case "DefaultEffect":
                    param = new AssetBuildParamDefaultEffect(el_);
                    break;

                case "PremultiplyTextureAlpha":
                    param = new AssetBuildParamPremultiplyTextureAlpha(el_);
                    break;

                case "PremultiplyVertexColor":
                    param = new AssetBuildParamPremultiplyVertexColor(el_);
                    break;

                case "GenerateTangentFrames":
                    param = new AssetBuildParamGenerateTangentFrames(el_);
                    break;

                case "Scale":
                    param = new AssetBuildParamScale(el_);
                    break;

                case "SwapWindingOrder":
                    param = new AssetBuildParamSwapWindingOrder(el_);
                    break;

                case "XRotation":
                    param = new AssetBuildParamXRotation(el_);
                    break;

                case "YRotation":
                    param = new AssetBuildParamYRotation(el_);
                    break;

                case "ZRotation":
                    param = new AssetBuildParamZRotation(el_);
                    break;

                case "CompressionQuality":
                    param = new AssetBuildParamCompressionQuality(el_);
                    break;

                case "VideoSoundTrackType":
                    param = new AssetBuildParamVideoSoundTrackType(el_);
                    break;

                default:
                    throw new NotImplementedException();
            }

            return param;
        }
    }
}
