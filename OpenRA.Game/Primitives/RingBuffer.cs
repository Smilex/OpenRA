#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenRA.Primitives
{

	public class RingBufferEnumerator<T> : IEnumerator<T>
	{
		readonly RingBuffer<T> buffer;
		int curIndex;
		T curValue;

		public RingBufferEnumerator(RingBuffer<T> buffer)
		{
			this.buffer = buffer;
			curIndex = 0;
			curValue = buffer[curIndex];
		}

		public bool MoveNext()
		{
			var lesserOfTheTwo = Math.Min(buffer.Count, buffer.Used);
			++curIndex;
			if (curIndex >= lesserOfTheTwo)
			{
				return false;
			}
			else
			{
				// Set current box to next item in collection.
				curValue = buffer[curIndex];
			}

			return true;
		}

		public void Reset() { curIndex = 0; }

		void IDisposable.Dispose() { }

		public T Current
		{
			get { return curValue; }
		}

		object IEnumerator.Current
		{
			get { return Current; }
		}
	}
	public class RingBuffer<T> : ICollection<T>
	{
		readonly T[] data;
		int used;

		public RingBuffer(int size)
		{
			data = new T[size];
			used = 0;
		}

		public void Add(T item)
		{
			if (used < data.Length)
			{
				data[used] = item;
				used += 1;
			}
			else if (data.Length > 1)
			{
				for (var i = 1; i < data.Length; ++i)
				{
					data[i - 1] = data[i];
				}

				data[data.Length - 1] = item;
			}
			else
			{
				data[0] = item;
			}
		}

		public void Clear()
		{
			used = 0;
		}

		public bool Contains(T item)
		{
			var lesserOfTheTwo = Math.Min(data.Length, used);
			for (var i = 0; i < lesserOfTheTwo; ++i)
			{
				if (data[i].Equals(item))
				{
					return true;
				}
			}

			return false;
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			data.CopyTo(array, arrayIndex);
		}

		public bool Remove(T item)
		{
			var lesserOfTheTwo = Math.Min(data.Length, used);
			for (var i = 0; i < lesserOfTheTwo; ++i)
			{
				if (data[i].Equals(item))
				{
					for (var j = i + 1; j < lesserOfTheTwo; ++j)
					{
						data[j - 1] = data[j];
					}

					used -= 1;
					break;
				}
			}

			return false;
		}

		public int Count => Math.Min(data.Length, used);

		public bool IsReadOnly => data.IsReadOnly;

		public IEnumerator<T> GetEnumerator()
		{
			return new RingBufferEnumerator<T>(this);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new RingBufferEnumerator<T>(this);
		}

		public T this[int index]
		{
			get { return data[index]; }
			set { data[index] = value; }
		}

		public int Used => used;
	}
}
