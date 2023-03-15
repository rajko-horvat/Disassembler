using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.CPU
{
	public class MemoryBlock
	{
		private MemoryRegion oRegion;
		private List<MemoryRegion> aProtected = new List<MemoryRegion>();
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

		public List<MemoryRegion> Protected
		{
			get
			{
				return this.aProtected;
			}
		}

		public bool IsProtected(ushort segment, ushort offset)
		{
			return this.IsProtected(MemoryRegion.ToAbsolute(segment, offset));
		}

		public bool IsProtected(int address)
		{
			for (int i = 0; i < this.aProtected.Count; i++)
			{
				if (this.aProtected[i].CheckBounds(address))
					return true;
			}

			return false;
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
			if (this.IsProtected(address))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
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
			if (this.IsProtected(address))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
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
			if (this.IsProtected(address))
			{
				Console.WriteLine("Attempt to write from protected area at 0x{0:x8}", address);
				return;
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
			if (this.IsProtected(address))
			{
				Console.WriteLine("Attempt to write from protected area at 0x{0:x8}", address);
				return;
			}
			int iLocation = this.oRegion.MapAddress(address);

			this.abData[iLocation] = (byte)(value & 0xff);
			this.abData[iLocation + 1] = (byte)((value & 0xff00) >> 8);
		}

		public void CopyData(ushort segment, ushort offset, byte[] data, int pos, int length)
		{
			this.CopyData(MemoryRegion.ToAbsolute(segment, offset), data, pos, length, true);
		}

		public void CopyData(ushort segment, ushort offset, byte[] data, int pos, int length, bool check)
		{
			this.CopyData(MemoryRegion.ToAbsolute(segment, offset), data, pos, length, check);
		}

		public void CopyData(int address, byte[] data, int pos, int length)
		{
			this.CopyData(address, data, pos, length, true);
		}

		public void CopyData(int address, byte[] data, int pos, int length, bool check)
		{
			if (check && this.IsProtected(address))
			{
				throw new Exception(string.Format("Attempt to write to protected area at 0x{0:x8}", address));
			}

			Array.Copy(data, pos, this.abData, this.oRegion.MapAddress(address), length);
		}

		public void Resize(int size)
		{
			this.oRegion = new MemoryRegion(this.oRegion.Start, size);
			Array.Resize(ref this.abData, size);
		}
	}
}
