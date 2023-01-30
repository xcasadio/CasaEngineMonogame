
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


namespace XNAFinalEngine.UserInterface
{

    public class EventedList<T> : List<T>
    {


        public event EventHandler ItemAdded;
        public event EventHandler ItemRemoved;



        public EventedList() { }
        public EventedList(int capacity) : base(capacity) { }
        public EventedList(IEnumerable<T> collection) : base(collection) { }



        public new void Add(T item)
        {
            int c = Count;
            base.Add(item);
            if (ItemAdded != null && c != Count)
                ItemAdded.Invoke(this, new EventArgs());
        } // Add

        public new void Remove(T obj)
        {
            int c = Count;
            base.Remove(obj);
            if (ItemRemoved != null && c != Count)
                ItemRemoved.Invoke(this, new EventArgs());
        } // Remove

        public new void Clear()
        {
            int c = Count;
            base.Clear();
            if (ItemRemoved != null && c != Count)
                ItemRemoved.Invoke(this, new EventArgs());
        } // Clear

        public new void AddRange(IEnumerable<T> collection)
        {
            int c = Count;
            base.AddRange(collection);
            if (ItemAdded != null && c != Count)
                ItemAdded.Invoke(this, new EventArgs());
        } // AddRange

        public new void Insert(int index, T item)
        {
            int c = Count;
            base.Insert(index, item);
            if (ItemAdded != null && c != Count)
                ItemAdded.Invoke(this, new EventArgs());
        } // Insert

        public new void InsertRange(int index, IEnumerable<T> collection)
        {
            int c = Count;
            base.InsertRange(index, collection);
            if (ItemAdded != null && c != Count)
                ItemAdded.Invoke(this, new EventArgs());
        } // InsertRange

        public new int RemoveAll(Predicate<T> match)
        {
            int c = Count;
#if (WINDOWS)
            int ret = base.RemoveAll(match);
#else
                int ret = ExtensionMethods.RemoveAll(this, match); // The extension method does not work.
#endif
            if (ItemRemoved != null && c != Count)
                ItemRemoved.Invoke(this, new EventArgs());
            return ret;
        } // RemoveAll

        public new void RemoveAt(int index)
        {
            int c = Count;
            base.RemoveAt(index);
            if (ItemRemoved != null && c != Count)
                ItemRemoved.Invoke(this, new EventArgs());
        } // RemoveAt

        public new void RemoveRange(int index, int count)
        {
            int c = Count;
            base.RemoveRange(index, count);
            if (ItemRemoved != null && c != Count)
                ItemRemoved.Invoke(this, new EventArgs());
        } // RemoveRange    


    } // EventedList
} // XNAFinalEngine.UserInterface