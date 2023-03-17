using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.CPU
{
	[Flags]
	public enum MemoryFlagsEnum
	{
		None = 0,
		Write = 1,
		Read = 2
	}

	public class MemoryRegion
	{
		private int iStart;
		private int iSize;
		private MemoryFlagsEnum eAccessFlags;

		public MemoryRegion(ushort segment, ushort offset, int size)
			: this(MemoryRegion.ToAbsolute(segment, offset), size, MemoryFlagsEnum.None)
		{
		}

		public MemoryRegion(ushort segment, ushort offset, int size, MemoryFlagsEnum access)
			: this(MemoryRegion.ToAbsolute(segment, offset), size, access)
		{
		}

		public MemoryRegion(int start, int size)
			: this(start, size, MemoryFlagsEnum.None)
		{
		}

		public MemoryRegion(int start, int size, MemoryFlagsEnum access)
		{
			this.iStart = start;
			this.iSize = size;
			this.eAccessFlags = access;
		}

		public MemoryFlagsEnum AccessFlags
		{
			get { return eAccessFlags; }
			set { eAccessFlags = value; }
		}

		public int Start
		{
			get
			{
				return this.iStart;
			}
		}

		public int Size
		{
			get
			{
				return this.iSize;
			}
		}

		public int End
		{
			get
			{
				return this.iStart + this.iSize - 1;
			}
		}

		public bool CheckBounds(ushort segment, ushort offset)
		{
			return this.CheckBounds(MemoryRegion.ToAbsolute(segment, offset), 1);
		}

		public bool CheckBounds(ushort segment, ushort offset, int size)
		{
			return this.CheckBounds(MemoryRegion.ToAbsolute(segment, offset), size);
		}

		public bool CheckBounds(int address)
		{
			return this.CheckBounds(address, 1);
		}

		public bool CheckBounds(int address, int size)
		{
			if (address >= this.iStart && address + size - 1 < this.iStart + this.iSize)
			{
				return true;
			}

			return false;
		}

		public bool CheckOverlap(ushort segment, ushort offset, int size)
		{
			return this.CheckOverlap(MemoryRegion.ToAbsolute(segment, offset), size);
		}

		public bool CheckOverlap(int address, int size)
		{
			if (address >= this.iStart || address < this.iStart + this.iSize ||
				(address + size - 1) >= this.iStart || (address + size - 1) < this.iStart + this.iSize)
			{
				return true;
			}
			return false;
		}

		public int MapAddress(ushort segment, ushort offset)
		{
			return this.MapAddress(MemoryRegion.ToAbsolute(segment, offset));
		}

		public int MapAddress(int address)
		{
			return address - this.iStart;
		}

		public static int ToAbsolute(ushort segment, ushort offset)
		{
			// 1MB limit!
			return ((int)((int)segment << 4) + (int)offset) & 0xfffff;
		}

		public static void AlignBlock(ref int address)
		{
			if ((address & 0xf) != 0)
			{
				address &= 0xffff0;
				address += 0x10;
			}
		}
	}
}
