using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.CPU
{
	public class CPUMemoryBlock
	{
		private uint uiSegment = 0;
		private byte[] abData;

		public CPUMemoryBlock(uint segment, int size)
		{
			this.uiSegment = segment;
			this.abData = new byte[size];
		}

		public CPUMemoryBlock(uint segment, byte[] data)
		{
			this.uiSegment = segment;
			this.abData = data;
		}

		public uint Segment
		{
			get { return this.uiSegment; }
			set { this.uiSegment = value; }
		}

		public int Size
		{
			get { return this.abData.Length; }
		}

		public byte ReadByte(ushort offset)
		{
			if (offset >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			return this.abData[offset];
		}

		public ushort ReadWord(ushort offset)
		{
			if (offset + 1 >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			return (ushort)((ushort)this.abData[offset] | (ushort)((ushort)this.abData[offset + 1] << 8));
		}

		public uint ReadDWord(ushort offset)
		{
			if (offset + 3 >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			return (uint)((uint)this.abData[offset] | (uint)((uint)this.abData[offset + 1] << 8) |
				(uint)((uint)this.abData[offset + 2] << 16) | (uint)((uint)this.abData[offset + 3] << 24));
		}

		public void WriteByte(ushort offset, byte value)
		{
			if (offset >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			this.abData[offset] = value;
		}

		public void WriteWord(ushort offset, ushort value)
		{
			if (offset + 1 >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			this.abData[offset] = (byte)(value & 0xff);
			this.abData[offset + 1] = (byte)((value & 0xff00) >> 8);
		}

		public void WriteDWord(ushort offset, uint value)
		{
			if (offset + 3 >= this.abData.Length)
			{
				throw new Exception("Memory block address outside bounds");
			}

			this.abData[offset] = (byte)(value & 0xff);
			this.abData[offset + 1] = (byte)((value & 0xff00) >> 8);
			this.abData[offset + 2] = (byte)((value & 0xff0000) >> 16);
			this.abData[offset + 3] = (byte)((value & 0xff000000) >> 24);
		}

		public void CopyData(ushort offset, byte[] data, int pos, int length)
		{
			Array.Copy(data, pos, this.abData, offset, length);
		}

		public void Resize(int size)
		{
			Array.Resize(ref this.abData, size);
		}
	}
}
