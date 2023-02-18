/*
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

namespace CasaEngine.Core.Helpers
{
    public class PoolWithoutAccessor<T> where T : new()
    {


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
        } // Capacity



        public PoolWithoutAccessor(int capacity)
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
        } // Pool



        public T Fetch()
        {
            if (Count >= Capacity)
            {
                ResizePool(Capacity + 25);
            }
            Count++;
            return Elements[Count - 1];
        } // Fetch



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
        } // ResizePool



        public void Release(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element), "Pool: Element value cannot be null");
            }

            // To accomplish our second objective (memory locality) the last available element will be moved to the place where the released element resided.
            var i = 0;
            while (i < Count && !Elements[i].Equals(element))
            {
                i++;
            }
            if (i == Count)
            {
                throw new ArgumentNullException(nameof(element), "Pool: Element value not found.");
            }

            var temp = Elements[i];
            Elements[i] = Elements[Count - 1];
            Elements[Count - 1] = temp;

            Count--;
        } // Release


    } // Pool
} // XNAFinalEngine.Helpers