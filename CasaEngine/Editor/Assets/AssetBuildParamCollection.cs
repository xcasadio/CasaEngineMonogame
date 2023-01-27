using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// 
    /// </summary>
    public class AssetBuildParamCollection
        : CollectionBase, ICustomTypeDescriptor
    {
        #region collection impl

        /// <summary>
        /// Adds an employee object to the collection
        /// </summary>
        /// <param name="emp"></param>
        public void Add(AssetBuildParam emp)
        {
            this.List.Add(emp);
        }

        /// <summary>
        /// Removes an employee object from the collection
        /// </summary>
        /// <param name="emp"></param>
        public void Remove(AssetBuildParam emp)
        {
            this.List.Remove(emp);
        }

        /// <summary>
        /// Returns an employee object at index position.
        /// </summary>
        public AssetBuildParam this[int index]
        {
            get
            {
                return (AssetBuildParam)this.List[index];
            }
        }

        #endregion

        // Implementation of interface ICustomTypeDescriptor 
        #region ICustomTypeDescriptor impl

        public String GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public String GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this, true);
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }


        /// <summary>
        /// Called to get the properties of this type. Returns properties with certain
        /// attributes. this restriction is not implemented here.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return GetProperties();
        }

        /// <summary>
        /// Called to get the properties of this type.
        /// </summary>
        /// <returns></returns>
        public PropertyDescriptorCollection GetProperties()
        {
            // Create a collection object to hold property descriptors
            PropertyDescriptorCollection pds = new PropertyDescriptorCollection(null);

            // Iterate the list of employees
            for (int i = 0; i < this.List.Count; i++)
            {
                // Create a property descriptor for the employee item and add to the property descriptor collection
                AssetBuildParamCollectionPropertyDescriptor pd = new AssetBuildParamCollectionPropertyDescriptor(this, i);
                pds.Add(pd);
            }
            // return the property descriptor collection
            return pds;
        }

        #endregion

        static public int Compare(AssetBuildParamCollection c1_, AssetBuildParamCollection c2_)
        {
            if (c1_.Count != c2_.Count)
            {
                return -1;
            }

            for (int i = 0; i < c1_.Count; i++)
            {
                if (c1_[i].Compare(c2_[i]) == false)
                {
                    return -1;
                }
            }

            return 0;
        }
    }
}
