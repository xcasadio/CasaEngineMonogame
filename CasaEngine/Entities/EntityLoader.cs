using System.Text.Json;

namespace CasaEngine.Entities
{
    public static class EntityLoader
    {
        public static BaseObject Load(JsonElement element)
        {
            var baseObject = new BaseObject();
            baseObject.Load(element);
            return baseObject;
        }

        public static List<BaseObject> LoadFromArray(JsonElement element)
        {
            var baseObjects = new List<BaseObject>();

            foreach (var item in element.EnumerateArray())
            {
                baseObjects.Add(Load(item));
            }

            return baseObjects;
        }
    }
}
