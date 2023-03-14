using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Disassembler.Decompiler
{
	[ListBindable(false)]
	public class StackCollection : IEnumerable
	{
		private List<StackItem> items = new List<StackItem>();

		public StackCollection()
		{ }

		public virtual int Push(StackItem obj)
		{
			int num = this.items.Count;
			this.items.Add(obj);

			return num;
		}

		public virtual StackItem Pop()
		{
			int index = this.items.Count - 1;
			StackItem obj = this.items[index];
			this.items.RemoveAt(index);

			return obj;
		}

		public virtual StackItem Peek()
		{
			return this.items[this.items.Count - 1];
		}

		public virtual void Pop(int count)
		{
			this.items.RemoveRange(this.items.Count - 1, count);
		}

		public virtual bool Contains(StackItem obj)
		{
			return (this.items.IndexOf(obj) != -1);
		}

		public void CopyTo(StackItem[] array, int index)
		{
			this.items.CopyTo(array, index);
		}

		public int IndexOf(StackItem obj)
		{
			return this.items.IndexOf(obj);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.items.GetEnumerator();
		}

		// Properties
		public StackItem this[int index]
		{
			get
			{
				return this.items[index];
			}
		}

		public int Count
		{
			get
			{
				return this.items.Count;
			}
		}
	}
}
