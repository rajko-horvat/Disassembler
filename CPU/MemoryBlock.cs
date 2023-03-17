using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Disassembler.CPU
{
	public class MemoryBlock
	{
		private MemoryRegion oRegion;
		private byte[] abData;

		public MemoryBlock(ushort segment, ushort offset, int size)
			: this(MemoryRegion.ToAbsolute(segment, offset), size)
		{ }

		public MemoryBlock(int start, int size)
		{
			this.oRegion = new MemoryRegion(start, size);
			this.abData = new byte[size];
		}

		public MemoryRegion Region
		{
			get
			{
				return this.oRegion;
			}
		}

		public byte[] Data
		{
			get { return this.abData; }
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			return this.ReadByte(MemoryRegion.ToAbsolute(segment, offset));
		}

		public byte ReadByte(int address)
		{
			if (!this.oRegion.CheckBounds(address))
			{
				throw new Exception("Memory block address outside bounds");
			}

			return this.abData[this.oRegion.MapAddress(address)];
		}

		public ushort ReadWord(ushort segment, ushort offset)
		{
			return this.ReadWord(MemoryRegion.ToAbsolute(segment, offset));
		}

		public ushort ReadWord(int address)
		{
			if (!this.oRegion.CheckBounds(address))
			{
				throw new Exception("Memory block address outside bounds");
			}
			int iLocation = this.oRegion.MapAddress(address);

			return (ushort)((ushort)this.abData[iLocation] | (ushort)((ushort)this.abData[iLocation + 1] << 8));
		}

		public void WriteByte(ushort segment, ushort offset, byte value)
		{
			this.WriteByte(MemoryRegion.ToAbsolute(segment, offset), value);
		}

		public void WriteByte(int address, byte value)
		{
			if (!this.oRegion.CheckBounds(address))
			{
				throw new Exception("Memory block address outside bounds");
			}

			this.abData[this.oRegion.MapAddress(address)] = value;
		}

		public void WriteWord(ushort segment, ushort offset, ushort value)
		{
			this.WriteWord(MemoryRegion.ToAbsolute(segment, offset), value);
		}

		public void WriteWord(int address, ushort value)
		{
			if (!this.oRegion.CheckBounds(address))
			{
				throw new Exception("Memory block address outside bounds");
			}
			int iLocation = this.oRegion.MapAddress(address);

			this.abData[iLocation] = (byte)(value & 0xff);
			this.abData[iLocation + 1] = (byte)((value & 0xff00) >> 8);
		}

		public void Resize(int size)
		{
			this.oRegion = new MemoryRegion(this.oRegion.Start, size);
			Array.Resize(ref this.abData, size);
		}
	}
}
