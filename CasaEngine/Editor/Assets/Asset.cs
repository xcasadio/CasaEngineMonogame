using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Editor.Assets
{
    class Asset
    {
        private bool _isLoaded = false;
        private bool _isBuild = false;

        private BaseObject _item;

        public BaseObject LoadItem()
        {
            if (_item == null)
            {
                //_Item = AssetFactory.Create()
            }

            return _item;
        }
    }
}
