using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    // This is a special type converter which will be associated with the AssetBuildParam class.
    // It converts an Employee object to string representation for use in a property grid.
    internal class AssetBuildParamConverter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType)
        {
            if (destType == typeof(string) && value is AssetBuildParam)
            {
                // Cast the value to an Employee type
                AssetBuildParam emp = (AssetBuildParam)value;

                // Return department and department role separated by comma.
                return emp.SubName + " : " + emp.Value;
            }
            return base.ConvertTo(context, culture, value, destType);
        }
    }

    // This is a special type converter which will be associated with the EmployeeCollection class.
    // It converts an EmployeeCollection object to a string representation for use in a property grid.
    internal class AssetBuildParamCollectionConverter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType)
        {
            if (destType == typeof(string) && value is AssetBuildParamCollection)
            {
                // Return department and department role separated by comma.
                return "Asset Build Parameters";
            }
            return base.ConvertTo(context, culture, value, destType);
        }
    }
}
