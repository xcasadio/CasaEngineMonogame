using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public class AssetBuildParamCollectionPropertyDescriptor
        : PropertyDescriptor
    {
        private readonly AssetBuildParamCollection _collection;
        private readonly int _index = -1;

        public AssetBuildParamCollectionPropertyDescriptor(AssetBuildParamCollection coll, int idx) :
            base("#" + idx.ToString(), null)
        {
            _collection = coll;
            _index = idx;
        }

        public override AttributeCollection Attributes => new(null);

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType => _collection.GetType();

        public override string DisplayName
        {
            get
            {
                var emp = _collection[_index];
                return emp.Name;
            }
        }

        public override string Description
        {
            get
            {
                var b = _collection[_index];
                return b.Name + ":" + b.Value;
            }
        }

        public override object GetValue(object component)
        {
            return _collection[_index];
        }

        public override bool IsReadOnly => false;

        public override string Name => "#" + _index.ToString();

        public override Type PropertyType => _collection[_index].GetType();

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
