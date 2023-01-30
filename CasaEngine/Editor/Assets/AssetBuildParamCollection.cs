using System.Collections;
using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public class AssetBuildParamCollection
        : CollectionBase, ICustomTypeDescriptor
    {

        public void Add(AssetBuildParam emp)
        {
            this.List.Add(emp);
        }

        public void Remove(AssetBuildParam emp)
        {
            this.List.Remove(emp);
        }

        public AssetBuildParam this[int index] => (AssetBuildParam)this.List[index];


        // Implementation of interface ICustomTypeDescriptor 

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


        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return GetProperties();
        }

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
