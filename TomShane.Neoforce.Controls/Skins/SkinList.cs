using System;
using System.Collections.Generic;

namespace TomShane.Neoforce.Controls.Skins;

public class SkinList<T> : List<T>
{
    public T this[string index]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                if (string.Equals(s.Name, index, StringComparison.InvariantCultureIgnoreCase))
                {
                    return this[i];
                }
            }
            return default(T);
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                if (string.Equals(s.Name, index, StringComparison.InvariantCultureIgnoreCase))
                {
                    this[i] = value;
                }
            }
        }
    }

    public SkinList()
    {
    }

    public SkinList(SkinList<T> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var t = new Type[1];
            t[0] = typeof(T);

            var p = new object[1];
            p[0] = source[i];

            Add((T)t[0].GetConstructor(t).Invoke(p));
        }
    }

}