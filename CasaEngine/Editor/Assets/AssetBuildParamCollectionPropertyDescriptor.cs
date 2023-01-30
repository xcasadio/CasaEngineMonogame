using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public class AssetBuildParamCollectionPropertyDescriptor
        : PropertyDescriptor
    {
        private readonly AssetBuildParamCollection collection = null;
        private readonly int index = -1;

        public AssetBuildParamCollectionPropertyDescriptor(AssetBuildParamCollection coll, int idx) :
            base("#" + idx.ToString(), null)
        {
            this.collection = coll;
            this.index = idx;
        }

        public override AttributeCollection Attributes => new AttributeCollection(null);

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType => this.collection.GetType();

        public override string DisplayName
        {
            get
            {
                AssetBuildParam emp = this.collection[index];
                return emp.Name;
            }
        }

        public override string Description
        {
            get
            {
                AssetBuildParam b = this.collection[index];
                return b.Name + ":" + b.Value;
            }
        }

        public override object GetValue(object component)
        {
            return this.collection[index];
        }

        public override bool IsReadOnly => false;

        public override string Name => "#" + index.ToString();

        public override Type PropertyType => this.collection[index].GetType();

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return true;
        }

        public override void SetValue(object component, object value)
        {
            // this.collection[index] = value;
        }
    }
}
