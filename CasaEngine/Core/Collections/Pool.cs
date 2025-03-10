﻿/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Core.Collections;

public class Pool<T> where T : new()
{
    public class Accessor
    {
        public int Index { get; internal set; }

        internal Accessor() { }
    }

    private Accessor[] _accessors;

    public T[] Elements;

    public int Count { get; private set; }

    public int Capacity
    {
        get => Elements.Length;
        set
        {
            if (value < Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pool: new size has to be bigger than active elements.");
            }

            ResizePool(value);
        }
    }

    public T this[Accessor accessor] => Elements[accessor.Index];

    public Pool(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Pool: Argument capacity must be greater than zero.");
        }

        Elements = new T[capacity];
        for (var i = 0; i < capacity; i++)
        {
            // If T is a reference type then the explicit creation is need it.
            Elements[i] = new T();
        }
        // They are created using another for sentence because we want memory locality.
        _accessors = new Accessor[capacity];
        for (var i = 0; i < capacity; i++)
        {
            _accessors[i] = new Accessor { Index = i };
        }
    }

    public Accessor Fetch()
    {
        if (Count >= Capacity)
        {
            ResizePool(Capacity + 25); // This could be a relative number.
        }
        Count++;
        return _accessors[Count - 1];
    }

    private void ResizePool(int newCapacity)
    {
        var oldCapacity = Capacity;
        var newElements = new T[newCapacity];
        // If T is a reference type then the explicit creation is need it.
        for (var i = 0; i < newCapacity; i++)
        {
            if (i < oldCapacity)
            {
                newElements[i] = Elements[i];
            }
            else
            {
                newElements[i] = new T();
            }
        }
        Elements = newElements;
        var newAccessors = new Accessor[newCapacity];
        for (var i = 0; i < newCapacity; i++)
        {
            if (i < oldCapacity)
            {
                newAccessors[i] = _accessors[i];
            }
            else
            {
                newAccessors[i] = new Accessor { Index = i };
            }
        }
        _accessors = newAccessors;
    }

    public void Release(Accessor accessor)
    {
        if (accessor == null)
        {
            throw new ArgumentNullException(nameof(accessor), "Pool: Accessor value cannot be null");
        }

        // To accomplish our second objective (memory locality) the last available element will be moved to the place where the released element resided.
        // First swap elements values.
        (Elements[accessor.Index], Elements[Count - 1]) = (Elements[Count - 1], Elements[accessor.Index]);

        // The indices have the wrong value.The last has to index its new place and vice versa.
        var accessorOldIndex = accessor.Index;
        accessor.Index = Count - 1;
        _accessors[Count - 1].Index = accessorOldIndex;

        // Also the accessor array has to be sorted. If not the fetch method will give a used accessor element.
        var lastActiveAccessor = _accessors[Count - 1]; // Accessor is a reference type.
        _accessors[Count - 1] = accessor;
        _accessors[accessorOldIndex] = lastActiveAccessor;

        Count--;
    }

    public void Swap(int i, int j)
    {
        (Elements[i], Elements[j]) = (Elements[j], Elements[i]);
        // The indices have the wrong value.The last has to index its new place and vice versa.
        var accessorOldIndex = _accessors[i].Index;
        _accessors[i].Index = j;
        _accessors[j].Index = accessorOldIndex;

        // Also the accessor array has to be sorted. If not the fetch method will give a used accessor element.
        (_accessors[i], _accessors[j]) = (_accessors[j], _accessors[i]);
    }
}