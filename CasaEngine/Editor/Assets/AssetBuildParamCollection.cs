using System.Collections;
using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public class AssetBuildParamCollection
        : CollectionBase, ICustomTypeDescriptor
    {

        public void Add(AssetBuildParam emp)
        {
            List.Add(emp);
        }

        public void Remove(AssetBuildParam emp)
        {
            List.Remove(emp);
        }

        public AssetBuildParam this[int index] => (AssetBuildParam)List[index];


        // Implementation of interface ICustomTypeDescriptor 

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public string GetComponentName()
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
            for (int i = 0; i < List.Count; i++)
            {
                // Create a property descriptor for the employee item and add to the property descriptor collection
                AssetBuildParamCollectionPropertyDescriptor pd = new AssetBuildParamCollectionPropertyDescriptor(this, i);
                pds.Add(pd);
            }
            // return the property descriptor collection
            return pds;
        }


        static public int Compare(AssetBuildParamCollection c1, AssetBuildParamCollection c2)
        {
            if (c1.Count != c2.Count)
            {
                return -1;
            }

            for (int i = 0; i < c1.Count; i++)
            {
                if (c1[i].Compare(c2[i]) == false)
                {
                    return -1;
                }
            }

            return 0;
        }
    }
}
