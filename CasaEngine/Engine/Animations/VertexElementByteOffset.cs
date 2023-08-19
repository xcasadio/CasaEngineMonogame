namespace CasaEngine.Engine.Animations;

/// <summary>
/// This is a helper struct for tallying byte offsets
/// </summary>
public struct VertexElementByteOffset
{
    public static int CurrentByteSize = 0;
    //[STAThread]
    public static int StartVector3() { CurrentByteSize = 0; var s = sizeof(float) * 3; CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Int() { var s = sizeof(int); CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Float() { var s = sizeof(float); CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Color() { var s = sizeof(int); CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Vector2() { var s = sizeof(float) * 2; CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Vector3() { var s = sizeof(float) * 3; CurrentByteSize += s; return CurrentByteSize - s; }
    public static int Vector4() { var s = sizeof(float) * 4; CurrentByteSize += s; return CurrentByteSize - s; }
}