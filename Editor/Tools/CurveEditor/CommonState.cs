//-----------------------------------------------------------------------------
// CommonState.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// CommonState.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;




namespace Editor.Tools.CurveEditor
{
    /// <summary>
    /// This generic class holds common stae of multiple states.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class CommonState<T> where T: struct
    {
        /// <summary>
        /// Gets common value.
        /// </summary>
        public T Value { get { return value; } }

        /// <summary>
        /// Gets common value string. 
        /// </summary>
        public string ValueString
        {
            get { return ( hasValue && hasSameValues ) ?
                value.ToString() : String.Empty; }
        }

        /// <summary>
        /// Gets HasSameValue that returns true if added states are common.
        /// </summary>
        public bool HasSameValue { get { return hasSameValues; } }

        /// <summary>
        /// Gets number of added states.
        /// </summary>
        public int Count { get { return count; } }

        public CommonState()
        {
            hasValue = false;
            hasSameValues = true;
            count = 0;
        }

        /// <summary>
        /// Add new state to group.
        /// </summary>
        /// <param name="value"></param>
        public void Add(T value)
        {
            if (!hasValue)
            {
                this.value = value;
                hasValue = true;
            }

            if ( !Object.Equals(this.value, value))
                hasSameValues = false;

            count++;
        }

        private T value;
        private bool hasValue;
        private bool hasSameValues;
        private int count;

    }
}
