using CasaEngine.Framework.Entities;

namespace CasaEngine.Editor.Assets
{
    internal class Asset
    {
        private bool _isLoaded = false;
        private bool _isBuild = false;

        private Entity _item;

        public Entity LoadItem()
        {
            if (_item == null)
            {
                //_Item = AssetFactory.Create()
            }

            return _item;
        }
    }
}
