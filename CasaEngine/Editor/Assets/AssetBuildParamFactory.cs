using System.Xml;

namespace CasaEngine.Editor.Assets
{
    static public class AssetBuildParamFactory
    {
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

        static public void SetBuildParams(ref AssetBuildParamCollection @params, AssetBuildParamType type)
        {
            switch (type)
            {
                case AssetBuildParamType.Texture:
                    @params.Add(new AssetBuildParamColor());
                    @params.Add(new AssetBuildParamColorKeyEnabled());
                    @params.Add(new AssetBuildParamGenerateMipmaps());
                    @params.Add(new AssetBuildParamPremultiplyAlpha());
                    @params.Add(new AssetBuildParamResizeToPowerOfTwo());
                    @params.Add(new AssetBuildParamTextureFormat());
                    break;

                case AssetBuildParamType.Effect:
                    @params.Add(new AssetBuildParamDebuggingOptions());
                    @params.Add(new AssetBuildParamDefines());
                    break;

                case AssetBuildParamType.Model:
                    @params.Add(new AssetBuildParamColor());
                    @params.Add(new AssetBuildParamColorKeyEnabled());
                    @params.Add(new AssetBuildParamGenerateMipmaps());
                    @params.Add(new AssetBuildParamPremultiplyAlpha());
                    @params.Add(new AssetBuildParamResizeToPowerOfTwo());
                    @params.Add(new AssetBuildParamTextureFormat());

                    @params.Add(new AssetBuildParamDefaultEffect());
                    @params.Add(new AssetBuildParamPremultiplyTextureAlpha());
                    @params.Add(new AssetBuildParamPremultiplyVertexColor());
                    @params.Add(new AssetBuildParamGenerateTangentFrames());
                    @params.Add(new AssetBuildParamScale());
                    @params.Add(new AssetBuildParamSwapWindingOrder());
                    @params.Add(new AssetBuildParamXRotation());
                    @params.Add(new AssetBuildParamYRotation());
                    @params.Add(new AssetBuildParamZRotation());
                    break;

                case AssetBuildParamType.Audio:
                    @params.Add(new AssetBuildParamCompressionQuality());
                    break;

                case AssetBuildParamType.Video:
                    @params.Add(new AssetBuildParamVideoSoundTrackType());
                    break;

                case AssetBuildParamType.Font:
                    throw new NotImplementedException();
                    break;

                case AssetBuildParamType.Xml:
                    throw new NotImplementedException();
                    break;
            }
        }

        static public AssetBuildParam Load(XmlElement el)
        {
            AssetBuildParam param;

            string name = el.SelectSingleNode("Name").InnerText;

            switch (name)
            {
                case "ColorKeyColor":
                    param = new AssetBuildParamColor(el);
                    break;

                case "ColorKeyEnabled":
                    param = new AssetBuildParamColorKeyEnabled(el);
                    break;

                case "GenerateMipmaps":
                    param = new AssetBuildParamGenerateMipmaps(el);
                    break;

                case "PremultiplyAlpha":
                    param = new AssetBuildParamPremultiplyAlpha(el);
                    break;

                case "ResizeToPowerOfTwo":
                    param = new AssetBuildParamResizeToPowerOfTwo(el);
                    break;

                case "TextureFormat":
                    param = new AssetBuildParamTextureFormat(el);
                    break;

                case "DebuggingOptions":
                    param = new AssetBuildParamDebuggingOptions(el);
                    break;

                case "Defines":
                    param = new AssetBuildParamDefines(el);
                    break;

                case "FirstCharacter":
                    param = new AssetBuildParamFirstCharacter(el);
                    break;

                case "DefaultEffect":
                    param = new AssetBuildParamDefaultEffect(el);
                    break;

                case "PremultiplyTextureAlpha":
                    param = new AssetBuildParamPremultiplyTextureAlpha(el);
                    break;

                case "PremultiplyVertexColor":
                    param = new AssetBuildParamPremultiplyVertexColor(el);
                    break;

                case "GenerateTangentFrames":
                    param = new AssetBuildParamGenerateTangentFrames(el);
                    break;

                case "Scale":
                    param = new AssetBuildParamScale(el);
                    break;

                case "SwapWindingOrder":
                    param = new AssetBuildParamSwapWindingOrder(el);
                    break;

                case "XRotation":
                    param = new AssetBuildParamXRotation(el);
                    break;

                case "YRotation":
                    param = new AssetBuildParamYRotation(el);
                    break;

                case "ZRotation":
                    param = new AssetBuildParamZRotation(el);
                    break;

                case "CompressionQuality":
                    param = new AssetBuildParamCompressionQuality(el);
                    break;

                case "VideoSoundTrackType":
                    param = new AssetBuildParamVideoSoundTrackType(el);
                    break;

                default:
                    throw new NotImplementedException();
            }

            return param;
        }
    }
}
