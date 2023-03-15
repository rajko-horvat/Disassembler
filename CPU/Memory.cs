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

		public Memory()
		{
		}

		public List<MemoryBlock> Blocks
		{
			get { return aBlocks; } 
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			return this.ReadByte(MemoryRegion.ToAbsolute(segment, offset));
		}

		public byte ReadByte(int address)
		{
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
			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address))
				{
					if (i == 0 && address < 0x12a70)
					{
						Console.WriteLine("Write byte at code block 0x{0:x4} = 0x{1:x2}", this.aBlocks[i].Region.MapAddress(address), value);
					}
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
			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (i == 0 && address < 0x12a70)
				{
					Console.WriteLine("Write word at code block 0x{0:x4} = 0x{1:x4}", this.aBlocks[i].Region.MapAddress(address), value);
				}

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

		public void WriteBlock(ushort segment, ushort offset, byte[] data, int pos, int length)
		{
			WriteBlock(MemoryRegion.ToAbsolute(segment, offset), data, pos, length);
		}

		public void WriteBlock(int address, byte[] data, int pos, int length) 
		{
			for (int i = 0; i < length; i++)
			{
				WriteByte(address + i, data[pos + i]);
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
					if (this.aBlocks[i].Protected.Count > 0)
						throw new Exception("Attempt to free the protected memory region");

					// found the block
					this.aBlocks.RemoveAt(i);
					return true;
				}
			}

			return false;
		}
	}
}
