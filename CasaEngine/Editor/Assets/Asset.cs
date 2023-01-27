using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// Handle all asset : buildable (texture, sound, movie, ...) and streamable
    /// (object created by the engine : sprite, animation, ...)
    /// </summary>
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
