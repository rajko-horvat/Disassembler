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
		#region Data block
		#endregion

		private BDictionary<uint, CPUMemoryBlock> aBlocks = new BDictionary<uint, CPUMemoryBlock>();

		public CPUMemory()
		{
			/*CPUMemoryBlock oBlock = new CPUMemoryBlock((ushort)0x1010, 0, 0x3e00);
			oBlock.CopyData(0x1010, 0, this.abData, 0, 0x3e00);
			oBlock.Protected.Add(new CPUMemoryRegion((ushort)0x1020, 0, 0x2650));
			aBlocks.Add(oBlock);*/

			//oBlock = new MemoryBlock((ushort)0x12a7, 0, 0x1490);
			//oBlock.CopyData(0x12a7, 0, this.abData, 0x2970, 0x1490);
			/*oBlock.Protected.Add(new MemoryRegion((ushort)0x12a7, 0x9a, 1));
			oBlock.Protected.Add(new MemoryRegion((ushort)0x12a7, 0x9c, 2));*/
			//oBlock.Protected.Add(new MemoryRegion((ushort)0x12a7, 0xa4, 1));
			//oBlock.Protected.Add(new MemoryRegion((ushort)0x12a7, 0xa6, 0x200));
			//aBlocks.Add(oBlock);
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
