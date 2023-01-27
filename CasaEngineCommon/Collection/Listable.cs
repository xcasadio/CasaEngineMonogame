#region Using directives

using System;
using System.Collections.Generic;

#endregion

//TODO: Implement sorting

namespace CasaEngineCommon.Collection
{
	/// <summary>
	/// This interface defines if an entity can be saved on a Listable
	/// </summary>
	public interface IListable
	{
		#region Properties
		
		/// <summary>
		/// Gets or sets the name value
		/// </summary>
		string Name
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the ID value
		/// </summary>
		int ID
		{
			get;
		}

		#endregion
	}

	/// <summary>
	/// Base list used in the engine that can accept IListable items
	/// </summary>
	/// <typeparam name="T">Type of the elements of the list. Must implement IListable</typeparam>
	/// <remarks>
	/// The Listable allows to access elements by ID or by Name, so it works like a List or a Dictionary
	/// at the same time. IDs can´t be repeated, but Names can be, so each name key has a list for the
	/// elements to solve the collisions
	/// </remarks>
	public class Listable<T> where T : IListable
	{
		#region Fields
		
		/// <summary>
		/// Hash elements
		/// </summary>
		protected Dictionary<string, List<T>> hashData;
        
		/// <summary>
		/// List elements
		/// </summary>
		protected List<T> listData;

		#endregion

		#region Constructor
		
		/// <summary>
		/// Default constructor
		/// </summary>
        public Listable()
		{
			hashData = new Dictionary<string, List<T>>();
			listData = new List<T>();
		}

		#endregion

		#region Properties
		
		/// <summary>
		/// Gets the data of the Listable as a List. Useful for the AddRange method
        /// </summary>
        public List<T> List
        {
            get { return listData; }
        }

		/// <summary>
		/// Gets a List of elements by its name (the name is the key on the dictionary)
		/// </summary>
		/// <param name="name">The name used as key</param>
		/// <returns>The list of elements that are contained in that key</returns>
        public List<T> this[string name]
		{
			get { return hashData[name]; }
		}

		/// <summary>
		/// Gets and element by its index
		/// </summary>
		/// <param name="index">The index of the element to get</param>
		/// <returns>The element at the index</returns>
		public T this[int index]
		{
			get { return listData[index]; }
		}

		/// <summary>
		/// Gets the number of elements in the Listable
		/// </summary>
		public int Count
		{
			get { return listData.Count; }
		}

		#endregion

		#region Methods
		
		/// <summary>
		/// Tests if a name exists in the Listable, and if true, returns the first element
		/// </summary>
		/// <param name="name">Name to search (key)</param>
		/// <returns>The first element found, Default(T) if there wasn´t an element present</returns>
		public T Exists(string name)
		{
			if (hashData.ContainsKey(name))
				return hashData[name][0];

			else
				return default(T);
		}

		/// <summary>
		/// Adds an element to the Listable
		/// </summary>
		/// <param name="element">Element to add</param>
		public void Add(T element)
		{
			//If the key doesn´t exist, create a list for the elements that use this key
			if (!hashData.ContainsKey(element.Name))
				hashData[element.Name] = new List<T>();

			//Add the element to the dictionary and the list
			hashData[element.Name].Add(element);
			listData.Add(element);
		}

		/// <summary>
		/// Deletes an index from the Listable
		/// </summary>
		/// <param name="index">Index to delete</param>
		public void Delete(int index)
		{
			T element;
			List<T> list;

			//Get the element and delete it from the list
			element = listData[index];
			listData.RemoveAt(index);

			//Delete it from the list of the dictionary
			list = hashData[element.Name];
			list.RemoveAt(list.IndexOf(element));

			//If the list for the key is empty, delete it also
			if (list.Count == 0)
				hashData.Remove(element.Name);
		}

		/// <summary>
		/// Deletes a name key from the list
		/// </summary>
		/// <param name="name">Name key to delete</param>
		public void Delete(string name)
		{
			List<T> list;

			if (hashData.ContainsKey(name) == false)
				return;
			
			//Get the list of elements from this key
			list = hashData[name];

			//Delete all the elements from the list
			for (int i = 0; i < list.Count; i++)
				listData.RemoveAt(listData.IndexOf(list[i]));

			//Delete the key from the hash
			hashData.Remove(name);
		}

		/// <summary>
		/// Deletes one element from the list
		/// </summary>
		/// <param name="element">Element to remove</param>
		public void Delete(T element)
		{
			List<T> list;

			//Eliminamos el elementos de la NameList
			//Delete the element from the list
			listData.Remove(element);

			//Get the list associated with the name key and delete from it the element
			list = hashData[element.Name];
			list.Remove(element);

			//If the list is empty, delete it
			if (list.Count == 0)
				hashData.Remove(element.Name);
		}

		/// <summary>
		/// Change the name of one element from the list
		/// </summary>
		/// <param name="index">Index to change</param>
		/// <param name="newName">New name for the element</param>
		public void Rename(int index, string newName)
		{
			List<T> list;

			//Get the list asociated with the name key and delete the element
			list = hashData[listData[index].Name];
			list.RemoveAt(list.IndexOf(listData[index]));

			//Change the name of the element on the list
			listData[index].Name = newName;
			
			//Move the element to its new name key
			if (!hashData.ContainsKey(listData[index].Name))
				hashData[listData[index].Name] = new List<T>();

			hashData[listData[index].Name].Add(listData[index]);
		}

        /// <summary>
        /// Clears the Listable
        /// </summary>
        public void Clear()
        {
            hashData.Clear();
            listData.Clear();
        }

		/// <summary>
		/// Returns the first element on the Listable that has a certain name
		/// </summary>
		/// <param name="name">Name searched</param>
		/// <returns>The first element with the name searched</returns>
		public T FindFirst(string name)
		{
			return this[name][0];
		}

		/// <summary>
		/// Finds an element by its ID
		/// </summary>
		/// <param name="id">The ID to search</param>
		/// <returns>The element that has the searched ID. default(T) otherwise</returns>
		public T FindByID(int id)
		{
			for (int i = 0; i < Count; i++)
				if (this[i].ID == id)
					return this[i];

			return default(T);
		}

		#endregion
	}

	/// <summary>
	/// Implements a special tipe of Listable designed for objects as values (everything can enter the Listable).
	/// It doesn´t allow repeated key values
	/// </summary>
	public class ListableString
	{
		#region Fields

		/// <summary>
		/// Hash elements
		/// </summary>
		protected Dictionary<string, object> hashData;
		
		/// <summary>
		/// List elements
		/// </summary>
		protected List<object> listData;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		public ListableString()
		{
			hashData = new Dictionary<string, object>();
			listData = new List<object>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the data of the ListableString as a List. Useful for the AddRange method
		/// </summary>
		public List<object> List
		{
			get { return listData; }
		}

		/// <summary>
		/// Gets an element by its key
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>The element that is contained in that key</returns>
		public object this[string key]
		{
			get { return hashData[key]; }
		}

		/// <summary>
		/// Gets and element by its index
		/// </summary>
		/// <param name="index">The index of the element to get</param>
		/// <returns>The element at the index</returns>s
		public object this[int index]
		{
			get { return listData[index]; }
		}

		/// <summary>
		/// Gets the number of elements in the ListableString
		/// </summary>
		public int Count
		{
			get { return listData.Count; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Tests if a key exists in the ListableString
		/// </summary>
		/// <param name="name">Key to search</param>
		/// <returns>True if the key exists, false if otherwise</returns>
		public bool Exists(string name)
		{
			return hashData.ContainsKey(name);
		}

		/// <summary>
		/// Adds an element to the ListableString
		/// </summary>
		/// <param name="key">Key associated to the element</param>
		/// <param name="element">Element to add</param>
		/// <returns>True if the element was added, false if otherwise</returns>
		public bool Add(String key, object element)
		{
			if (hashData.ContainsKey(key))
				return false;

			//Add the object to the dictionary and the list
			hashData[key] = element;
			listData.Add(element);

			return true;
		}

		/// <summary>
		/// Removes an element from the ListableString using its key
		/// </summary>
		/// <param name="key">Key to delete</param>
		/// <returns>True if the element was deleted, false if otherwise</returns>
		public bool Delete(string key)
		{
			object elemento;

			if (hashData.ContainsKey(key) == false)
				return false;

			//Remove the element from the list and the dictionary
			elemento = hashData[key];

			hashData.Remove(key);
			listData.Remove(elemento);

			return true;
		}

		/// <summary>
		/// Clears the ListableString
		/// </summary>
		public void Clear()
		{
			hashData.Clear();
			listData.Clear();
		}

		#endregion
	}

	/// <summary>
	/// Implements a collection that can be used as a List or a Dictionary. It doesn´t allow repeated elements.
	/// </summary>
	/// <typeparam name="T">Class that the list will contain. Must implement IListable</typeparam>
	public class ListableUnique<T> where T : IListable
	{
		#region Fields

		/// <summary>
		/// Couter value to be able to add repeated elements (when a value its repeated the counter is added
		/// to it to make a new unique name)
		/// </summary>
        static int idForRepeatedElements;

		/// <summary>
		/// Field where we´ll save the data to access it as a Dictionary
		/// </summary>
		internal Dictionary<string, T> hashData;

		/// <summary>
		/// Field where we´ll save the data to access it as a List
		/// </summary>
		protected List<T> listData;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		public ListableUnique()
		{
			hashData = new Dictionary<string, T>();
			listData = new List<T>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Return the data as a List. Useful for the AddRange method.
		/// </summary>
		/// <value>The list of elements of the collection</value>
		public List<T> List
		{
			get { return listData; }
		}

		/// <summary>
		/// Name indexer. To use the collection as a Dictionary.
		/// </summary>
		/// <value>The T element which key is "name"</value>
		public T this[string name]
		{
			get { return hashData[name]; }
		}

		/// <summary>
		/// Index indexer. To use the container as a List.
		/// </summary>
		/// <value>The T element in the index position</value>
		public T this[int index]
		{
			get { return listData[index]; }
		}

		/// <summary>
		/// Gets the number of elements in the collection.
		/// </summary>
		/// <value>The count of elements</value>
		public int Count
		{
			get { return listData.Count; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Checks if the element is contained in the collection.
		/// </summary>
		/// <param name="name">Key asociated with the element we want to check</param>
		/// <returns>The element if it exists in the collection. default(T) if not</returns>
		public T Exists(string name)
		{
			if (hashData.ContainsKey(name))
				return hashData[name];

			else
				return default(T);
		}

		/// <summary>
		/// Adds an element to the collection.
		/// </summary>
		/// <param name="element">Element to add</param>
		/// <remarks>If the name was present on the collection, the name will be modified to make
		/// a new unique name</remarks>
		public void Add(T element)
		{
			//Check if the element exists
			if (hashData.ContainsKey(element.Name))
			{
				element.Name += idForRepeatedElements.ToString();
				idForRepeatedElements++;
			}

			hashData[element.Name] = element;
			listData.Add(element);
		}

		/// <summary>
		/// Deletes an index from the collection.
		/// </summary>
		/// <param name="index">Index we want to delete</param>
		public void Delete(int index)
		{
			//Check
			if (listData.Count < index)
				return;

			hashData.Remove(listData[index].Name);
			listData.RemoveAt(index);
		}

		/// <summary>
		/// Deletes a key from the collection
		/// </summary>
		/// <param name="name">Key we want to delete</param>
		public void Delete(string name)
		{
			//Check
			if (hashData.ContainsKey(name) == false)
				return;

			listData.Remove(hashData[name]);
			hashData.Remove(name);
		}

		/// <summary>
		/// Deletes an element from the collection
		/// </summary>
		/// <param name="element">Element we want to delete</param>
		public void Delete(T element)
		{
			hashData.Remove(element.Name);
			listData.Remove(element);
		}

		/// <summary>
		/// Changes the name of an element of the collection
		/// </summary>
		/// <param name="index">Position of the element we want to rename</param>
		/// <param name="name">New name of the element</param>
		public void Rename(int index, string name)
		{
			//Check
			if (index > listData.Count)
				return;

			//Delete the old element from the dicionary
			hashData.Remove(listData[index].Name);

			//Rename the element and add it again to the dicionary
			listData[index].Name = name;
			hashData[name] = listData[index];
		}

		/// <summary>
		/// Clears the collection
		/// </summary>
		public void Clear()
		{
			hashData.Clear();
			listData.Clear();
		}

		#endregion
	}
}
