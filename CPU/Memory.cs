using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Disassembler.CPU
{
	public class Memory
	{
		private List<MemoryBlock> aBlocks = new List<MemoryBlock>();
		private List<MemoryRegion> aMemoryRegions = new List<MemoryRegion>();

		public Memory()
		{
		}

		public List<MemoryBlock> Blocks
		{
			get { return aBlocks; } 
		}

		public List<MemoryRegion> MemoryRegions
		{
			get
			{
				return this.aMemoryRegions;
			}
		}

		public bool HasAccess(ushort segment, ushort offset, MemoryFlagsEnum access)
		{
			return this.HasAccess(MemoryRegion.ToAbsolute(segment, offset), access);
		}

		public bool HasAccess(int address, MemoryFlagsEnum access)
		{
			for (int i = 0; i < this.aMemoryRegions.Count; i++)
			{
				if (this.aMemoryRegions[i].CheckBounds(address))
				{
					if ((this.aMemoryRegions[i].AccessFlags & access) != access)
						return true;
					else
						return false;
				}
			}

			return false;
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			return this.ReadByte(MemoryRegion.ToAbsolute(segment, offset));
		}

		public byte ReadByte(int address)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Read))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
			}

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address))
				{
					return this.aBlocks[i].ReadByte(address);
				}
			}

			Console.WriteLine("Attempt to read byte at 0x{0:x8}", address);
			return 0;
		}

		public ushort ReadWord(ushort segment, ushort offset)
		{
			return this.ReadWord(MemoryRegion.ToAbsolute(segment, offset));
		}

		public ushort ReadWord(int address)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Read))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
			}

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address, 2))
				{
					return this.aBlocks[i].ReadWord(address);
				}
			}

			Console.WriteLine("Attempt to read word at 0x{0:x8}", address);
			return 0;
		}

		public void WriteByte(ushort segment, ushort offset, byte value)
		{
			this.WriteByte(MemoryRegion.ToAbsolute(segment, offset), value);
		}

		public void WriteByte(int address, byte value)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Write))
			{
				Console.WriteLine("Attempt to write to protected area at 0x{0:x8}", address);
				return;
			}

			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address))
				{
					this.aBlocks[i].WriteByte(address, value);
					bFound = true;
					break;
				}
			}

			if (!bFound)
				Console.WriteLine("Attempt to write byte 0x{0:x2} at 0x{1:x8}", value, address);
		}

		public void WriteWord(ushort segment, ushort offset, ushort value)
		{
			this.WriteWord(MemoryRegion.ToAbsolute(segment, offset), value);
		}

		public void WriteWord(int address, ushort value)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Write))
			{
				Console.WriteLine("Attempt to write to protected area at 0x{0:x8}", address);
				return;
			}

			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address, 2))
				{
					this.aBlocks[i].WriteWord(address, value);
					bFound = true;
					break;
				}
			}

			if (!bFound)
				Console.WriteLine("Attempt to write word 0x{0:x4} at 0x{1:x8}", value, address);
		}

		public void WriteBlock(ushort segment, ushort offset, byte[] srcData, int pos, int length)
		{
			WriteBlock(MemoryRegion.ToAbsolute(segment, offset), srcData, pos, length);
		}

		public void WriteBlock(int address, byte[] srcData, int pos, int length) 
		{
			for (int i = 0; i < length; i++)
			{
				WriteByte(address + i, srcData[pos + i]);
			}
		}

		public bool ResizeBlock(ushort segment, ushort para)
		{
			int iAddress = MemoryRegion.ToAbsolute(segment, 0);
			int iSize = (int)para << 4;

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.Start == iAddress)
				{
					// check for overlapping
					for (int j = 0; j < this.aBlocks.Count; j++)
					{
						if (j != i && this.aBlocks[j].Region.CheckOverlap(iAddress, iSize))
							return false;
					}

					// found the block
					this.aBlocks[i].Resize(iSize);
					return true;
				}
			}

			return false;
		}

		public bool AllocateBlock(int size, out ushort segment)
		{
			int iFreeMin = 0;
			int iFreeMax = 0xb0000;

			// just allocate next available block, don't search between blocks for now
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.End >= iFreeMin)
				{
					iFreeMin = this.aBlocks[i].Region.End + 1;
				}
			}

			// make sure that iFreeMin is 16 byte aligned
			MemoryRegion.AlignBlock(ref iFreeMin);

			// is there enough room for allocation
			if (iFreeMax - iFreeMin < size)
			{
				segment = (ushort)(((iFreeMax - iFreeMin) >> 4) & 0xffff);
				return false;
			}

			// allocate block
			segment = (ushort)((iFreeMin >> 4) & 0xffff);
			MemoryBlock mem = new MemoryBlock(iFreeMin, size);
			this.aBlocks.Add(mem);

			return true;
		}

		public bool AllocateParagraphs(ushort size, out ushort segment)
		{
			int iSize = (int)size << 4;
			int iFreeMin = 0;
			int iFreeMax = 0xb0000;

			// just allocate next available block, don't search between blocks for now
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.End >= iFreeMin)
				{
					iFreeMin = this.aBlocks[i].Region.End + 1;
				}
			}

			// make sure that iFreeMin is 16 byte aligned
			MemoryRegion.AlignBlock(ref iFreeMin);

			// is enough room for allocation
			if (iFreeMax - iFreeMin < iSize)
			{
				segment = (ushort)(((iFreeMax - iFreeMin) >> 4) & 0xffff);
				return false;
			}

			// allocate block
			segment = (ushort)((iFreeMin >> 4) & 0xffff);
			MemoryBlock mem = new MemoryBlock(iFreeMin, iSize);
			this.aBlocks.Add(mem);

			return true;
		}

		public bool FreeBlock(ushort segment)
		{
			int iAddress = MemoryRegion.ToAbsolute(segment, 0);

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.Start == iAddress)
				{
					// found the block
					this.aBlocks.RemoveAt(i);
					return true;
				}
			}

			return false;
		}
	}
}
