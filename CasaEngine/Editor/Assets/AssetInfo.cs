using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using Microsoft.Xna.Framework;
using CasaEngine;
using CasaEngine.Editor.Builder;
using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// 
    /// </summary>
    public struct AssetInfo
    {
        static private int FreeID = 0;

        public int ID; 
        public string Name;
        public string FileName;
        public AssetType Type;

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection Params;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        /// <param name="name_"></param>
        /// <param name="type_"></param>
        /// <param name="fileName_"></param>
        public AssetInfo(int id_, string name_, AssetType type_, string fileName_)
        {
            ID = id_;
            Name = name_;
            Type = type_;
            FileName = fileName_;
            Params = new AssetBuildParamCollection();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is AssetInfo)
            {
                AssetInfo a = (AssetInfo) obj;
                return a.Name.Equals(Name) 
                    && a.Type.Equals(Type) 
                    && a.FileName.Equals(FileName);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Name.GetHashCode() + Type.GetHashCode() + ID.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        public void SetID(int id_)
        {
            ID = id_;

            if (ID >= FreeID)
            {
                FreeID = ID + 1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetNewID()
        {
            this.ID = ++FreeID;
        }
    }
}
