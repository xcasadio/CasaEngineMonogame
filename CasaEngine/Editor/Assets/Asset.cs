using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Editor.Assets
{
    class Asset
    {
        private bool m_IsLoaded = false;
        private bool m_IsBuild = false;

        private BaseObject m_Item;

        public BaseObject LoadItem()
        {
            if (m_Item == null)
            {
                //m_Item = AssetFactory.Create()
            }

            return m_Item;
        }
    }
}
