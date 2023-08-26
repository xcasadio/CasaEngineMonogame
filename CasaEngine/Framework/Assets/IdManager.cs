namespace CasaEngine.Framework.Assets;

public static class IdManager
{
    public const long InvalidId = -1;
    private static long _uniqueIdCounter;

    public static long GetId()
    {
        var id = _uniqueIdCounter;
        _uniqueIdCounter++;
        return id;
    }

    public static void SetMax(long id)
    {
        _uniqueIdCounter = Math.Max(_uniqueIdCounter, id + 1);
    }
}