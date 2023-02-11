using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public class AssetBuildParamCollectionPropertyDescriptor
        : PropertyDescriptor
    {
        private readonly AssetBuildParamCollection _collection = null;
        private readonly int _index = -1;

        public AssetBuildParamCollectionPropertyDescriptor(AssetBuildParamCollection coll, int idx) :
            base("#" + idx.ToString(), null)
        {
            this._collection = coll;
            this._index = idx;
        }

        public override AttributeCollection Attributes => new AttributeCollection(null);

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType => this._collection.GetType();

        public override string DisplayName
        {
            get
            {
                AssetBuildParam emp = this._collection[_index];
                return emp.Name;
            }
        }

        public override string Description
        {
            get
            {
                AssetBuildParam b = this._collection[_index];
                return b.Name + ":" + b.Value;
            }
        }

        public override object GetValue(object component)
        {
            return this._collection[_index];
        }

        public override bool IsReadOnly => false;

        public override string Name => "#" + _index.ToString();

        public override Type PropertyType => this._collection[_index].GetType();

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
