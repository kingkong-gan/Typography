//MIT, 2014-present, WinterDev
//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software 
// is granted provided this copyright notice appears in all copies. 
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//---------------------------------------------------------------------------- 


namespace Typography.Text
{
    public readonly struct TextBufferSegment<T>
    {
        internal readonly TextBuffer<T> _arrList;
        public readonly int beginAt;
        public readonly int len;
        public TextBufferSegment(TextBuffer<T> arrList, int beginAt, int len)
        {
            _arrList = arrList;
            this.beginAt = beginAt;
            this.len = len;
        }

        public int Count => len;

        public static void UnsafeGetInternalArr(in TextBufferSegment<T> listSpan, out T[] internalArr)
        {
            internalArr = listSpan._arrList.UnsafeInternalArray;
        }

        public static readonly TextBufferSegment<T> Empty = new TextBufferSegment<T>();
#if DEBUG
        public override string ToString()
        {
            return beginAt + "," + len;
        }
#endif
    }

    public sealed class TextBuffer<T>
    {
        static readonly T[] s_empty = new T[0];

        int _currentSize;
        T[] _internalArray = s_empty;

        public TextBuffer()
        {
        }
        public TextBuffer(int cap)
        {
            Allocate(cap, 0);
        }
        public TextBuffer(TextBuffer<T> srcCopy, int plusSize)
        {
            Allocate(srcCopy.AllocatedSize, srcCopy.AllocatedSize + plusSize);
            if (srcCopy._currentSize != 0)
            {
                srcCopy._internalArray.CopyTo(_internalArray, 0);
            }
        }
        public void RemoveLast()
        {
            if (_currentSize != 0)
            {
                _currentSize--;
            }
        }
        //
        public int Count => _currentSize;
        //
        public int AllocatedSize => _internalArray.Length;
        //
        public void Clear()
        {
            _currentSize = 0;
        }


        // Set new capacity. All data is lost, size is set to zero.
        public void Clear(int newCapacity)
        {
            Clear(newCapacity, 0);
        }
        public void Clear(int newCapacity, int extraTail)
        {
            _currentSize = 0;
            if (newCapacity > AllocatedSize)
            {
                _internalArray = null;
                int sizeToAllocate = newCapacity + extraTail;
                if (sizeToAllocate != 0)
                {
                    _internalArray = new T[sizeToAllocate];
                }
            }
        }
        // Allocate n elements. All data is lost, 
        // but elements can be accessed in range 0...size-1. 
        public void Allocate(int size)
        {
            Allocate(size, 0);
        }

        void Allocate(int size, int extraTail)
        {
            Clear(size, extraTail);
            _currentSize = size;
        }

        /// <summary>
        ///  Resize keeping the content
        /// </summary>
        /// <param name="newSize"></param>
        public void AdjustSize(int newSize)
        {
            if (newSize > _currentSize && newSize > AllocatedSize)
            {
                //create new array and copy data to that 
                var newArray = new T[newSize];
                if (_internalArray != null)
                {
                    System.Array.Copy(_internalArray, newArray, _internalArray.Length);
                    //for (int i = _internalArray.Length - 1; i >= 0; --i)
                    //{
                    //    newArray[i] = _internalArray[i];
                    //}
                }
                _internalArray = newArray;
            }
        }



        public T[] ToArray()
        {
            T[] output = new T[_currentSize];
            System.Array.Copy(_internalArray, output, _currentSize);
            return output;
        }

        void EnsureSpaceForAppend(int newAppendLen)
        {
            int newSize = _currentSize + newAppendLen;
            if (_internalArray.Length < newSize)
            {
                //copy
                if (newSize < 100000)
                {
                    AdjustSize(newSize + (newSize / 2) + 16);
                }
                else
                {
                    AdjustSize(newSize + newSize / 4);
                }
            }
        }
        /// <summary>
        /// append element to latest index
        /// </summary>
        /// <param name="v"></param>
        public void Append(T v)
        {
            if (_internalArray.Length < (_currentSize + 1))
            {
                if (_currentSize < 100000)
                {
                    AdjustSize(_currentSize + (_currentSize / 2) + 16);
                }
                else
                {
                    AdjustSize(_currentSize + _currentSize / 4);
                }
            }
            _internalArray[_currentSize++] = v;
        }

        public void Append(T[] arr)
        {
            //append arr             
            Append(arr, 0, arr.Length);
        }
        public void Append(T[] arr, int start, int len)
        {
            EnsureSpaceForAppend(len);
            System.Array.Copy(arr, start, _internalArray, _currentSize, len);
            _currentSize = _currentSize + len;
        }
        public void CopyAndAppend(int srcIndex, int len)
        {
            EnsureSpaceForAppend(len);
            System.Array.Copy(_internalArray, srcIndex, _internalArray, _currentSize, len);
            _currentSize += len;
        }
        public T this[int i]
        {
            get => _internalArray[i];
            set => _internalArray[i] = value;
        }
        /// <summary>
        /// access to internal array,
        /// </summary>
        public T[] UnsafeInternalArray => _internalArray;

        //
        public int Length => _currentSize;

        //
        public TextBufferSegment<T> CreateSpan(int beginAt, int len) => new TextBufferSegment<T>(this, beginAt, len);



        //-------------------------------------------
        public void Insert(int index, T value)
        {
            //split to left-right
            if (index < 0 || index > _currentSize)
            {
                throw new System.NotSupportedException();
            }
            EnsureSpaceForAppend(_currentSize + 1);
            //
            //move data to right side

            for (int i = Length - 1; i >= index; --i)
            {
                _internalArray[i + 1] = _internalArray[i];
            }
            _internalArray[index] = value;
            _currentSize++;
        }
        public void Insert(int index, T[] values)
        {
            if (index < 0 || index > _currentSize)
            {
                throw new System.NotSupportedException();
            }
            //------------------
            int reqSpace = values.Length;
            EnsureSpaceForAppend(_currentSize + reqSpace);
            for (int i = Length - 1; i >= index; --i)
            {
                _internalArray[i + reqSpace] = _internalArray[i];
            }
            for (int i = 0; i < reqSpace; ++i)
            {
                _internalArray[index + i] = values[i];
            }
            System.Array.Copy(values, 0, _internalArray, index, reqSpace);
            _currentSize += reqSpace;
        }
        public void Remove(int index) => Remove(index, 1);
        public void Remove(int index, int len)
        {
            if (len < 1 || index < 0 || index > _currentSize)
            {
                throw new System.NotSupportedException();
            }

            int pos = index;
            int copy_count = _currentSize - (index + len);
            for (int i = 0; i < copy_count; ++i)
            {
                _internalArray[pos] = _internalArray[pos + len];
                pos++;
            }
            _currentSize -= len;
        }
    }
}
