using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.CPU
{
	public class CPUMemory
	{
		private BDictionary<uint, CPUMemoryBlock> aBlocks = new BDictionary<uint, CPUMemoryBlock>();

		public CPUMemory()
		{
		}

		public BDictionary<uint, CPUMemoryBlock> Blocks
		{
			get { return this.aBlocks; }
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			if (this.aBlocks.ContainsKey(segment))
			{
				return this.aBlocks.GetValueByKey(segment).ReadByte(offset);
			}

			Console.WriteLine("Attempt to read byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			return 0;
		}

		public ushort ReadWord(ushort segment, ushort offset)
		{
			if (this.aBlocks.ContainsKey(segment))
			{
				return this.aBlocks.GetValueByKey(segment).ReadWord(offset);
			}

			Console.WriteLine("Attempt to read word at 0x{0:x4}:0x{1:x4}", segment, offset);
			return 0;
		}

		public void WriteByte(ushort segment, ushort offset, byte value)
		{
			if (this.aBlocks.ContainsKey(segment))
			{
				this.aBlocks.GetValueByKey(segment).WriteByte(offset, value);
			}
			else
			{
				Console.WriteLine("Attempt to write byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			}
		}

		public void WriteWord(ushort segment, ushort offset, ushort value)
		{
			if (this.aBlocks.ContainsKey(segment))
			{
				this.aBlocks.GetValueByKey(segment).WriteWord(offset, value);
			}
			else
			{
				Console.WriteLine("Attempt to write byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			}
		}
	}
}
