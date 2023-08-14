namespace CasaEngine.Framework.Assets;

public static class IdManager
{
    public const int InvalidId = -1;
    private static int _uniqueIdCounter;
    private static long _id;

    public static long GetId()
    {
        _id = _uniqueIdCounter;
        _uniqueIdCounter++;
        return _id;
    }
}