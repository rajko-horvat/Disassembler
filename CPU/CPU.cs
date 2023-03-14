using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.CPU
{
	public class CPU
	{
		// flags
		private CPUFlags oFlags = new CPUFlags();

		// registers
		private CPURegister oAX = new CPURegister();
		private CPURegister oBX = new CPURegister();
		private CPURegister oCX = new CPURegister();
		private CPURegister oDX = new CPURegister();
		private CPURegister oBP = new CPURegister();
		private CPURegister oSP = new CPURegister();
		private CPURegister oSI = new CPURegister();
		private CPURegister oDI = new CPURegister();
		private CPURegister oDS = new CPURegister();
		private CPURegister oES = new CPURegister();
		private CPURegister oSS = new CPURegister();

		// temporary registers
		private CPURegister oTemp = new CPURegister();

		// memory
		private BDictionary<uint, CPUMemoryBlock> aMemory = new BDictionary<uint, CPUMemoryBlock>();

		// stack
		private CPUMemoryBlock oStack = new CPUMemoryBlock(0x1000, 0xffff);

		public CPU()
		{
			this.oSP.Word = 0xffff;
			this.oSS.Word = 0x1000;
			this.aMemory.Add(0x1000, oStack);
		}

		// flags
		public CPUFlags Flags
		{
			get { return this.oFlags; }
		}

		// registers
		public CPURegister AX
		{ 
			get { return this.oAX; }
		}

		public CPURegister BX
		{
			get { return this.oBX; }
		}

		public CPURegister CX
		{
			get { return this.oCX; }
		}

		public CPURegister DX
		{
			get { return this.oDX; }
		}

		public CPURegister BP
		{
			get { return this.oBP; }
		}

		public CPURegister SP
		{
			get { return this.oSP; }
		}

		public CPURegister SI
		{
			get { return this.oSI; }
		}

		public CPURegister DI
		{
			get { return this.oDI; }
		}

		public CPURegister DS
		{
			get { return this.oDS; }
		}

		public CPURegister ES
		{
			get { return this.oES; }
		}

		public CPURegister SS
		{
			get { return this.oSS; }
		}

		public CPURegister Temp
		{
			get { return this.oTemp; }
		}

		// memory
		public BDictionary<uint, CPUMemoryBlock> Memory
		{
			get { return this.aMemory; }
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				return this.aMemory.GetValueByKey(segment).ReadByte(offset);
			}

			Console.WriteLine("Attempt to read byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			return 0;
		}

		public ushort ReadWord(ushort segment, ushort offset)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				return this.aMemory.GetValueByKey(segment).ReadWord(offset);
			}

			Console.WriteLine("Attempt to read word at 0x{0:x4}:0x{1:x4}", segment, offset);
			return 0;
		}

		public uint ReadDWord(ushort segment, ushort offset)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				return this.aMemory.GetValueByKey(segment).ReadDWord(offset);
			}

			Console.WriteLine("Attempt to read dword at 0x{0:x4}:0x{1:x4}", segment, offset);
			return 0;
		}

		public void WriteByte(ushort segment, ushort offset, byte value)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				this.aMemory.GetValueByKey(segment).WriteByte(offset, value);
			}
			else
			{
				Console.WriteLine("Attempt to write byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			}
		}

		public void WriteWord(ushort segment, ushort offset, ushort value)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				this.aMemory.GetValueByKey(segment).WriteWord(offset, value);
			}
			else
			{
				Console.WriteLine("Attempt to write byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			}
		}

		public void WriteDWord(ushort segment, ushort offset, uint value)
		{
			if (this.aMemory.ContainsKey(segment))
			{
				this.aMemory.GetValueByKey(segment).WriteDWord(offset, value);
			}
			else
			{
				Console.WriteLine("Attempt to dwrite byte at 0x{0:x4}:0x{1:x4}", segment, offset);
			}
		}

		// stack
		public void Push(ushort value)
		{
			if ((int)this.oSP.Word - 2 < 0)
			{
				Console.WriteLine("Stack overflow");
				return;
			}
			this.oStack.WriteByte(this.oSP.Word--, (byte)(value & 0xff));
			this.oStack.WriteByte(this.oSP.Word--, (byte)((value & 0xff00) >> 8));
		}

		public void Push(uint value)
		{
			if ((int)this.oSP.Word - 4 < 0)
			{
				Console.WriteLine("Stack overflow");
				return;
			}
			this.oStack.WriteByte(this.oSP.Word--, (byte)(value & 0xff));
			this.oStack.WriteByte(this.oSP.Word--, (byte)((value & 0xff00) >> 8));
			this.oStack.WriteByte(this.oSP.Word--, (byte)((value & 0xff0000) >> 16));
			this.oStack.WriteByte(this.oSP.Word--, (byte)((value & 0xff000000) >> 24));
		}

		public ushort PopWord()
		{
			if ((int)this.oSP.Word + 2 >= this.oStack.Size)
			{
				Console.WriteLine("Stack underflow");
				return 0;
			}

			return (ushort)(((ushort)this.oStack.ReadByte(this.oSP.Word++) << 8) | (ushort)this.oStack.ReadByte(this.oSP.Word++));
		}

		public uint PopDWord()
		{
			if ((int)this.oSP.Word + 4 >= this.oStack.Size)
			{
				Console.WriteLine("Stack underflow");
				return 0;
			}

			return (uint)(((uint)this.oStack.ReadByte(this.oSP.Word++) << 24) | ((uint)this.oStack.ReadByte(this.oSP.Word++) << 16) |
				((uint)this.oStack.ReadByte(this.oSP.Word++) << 8) | ((uint)this.oStack.ReadByte(this.oSP.Word++)));
		}

		// instructions
		public byte Not(byte value1)
		{
			byte res = (byte)(~value1);
			// Modifies flags: None

			return res;
		}

		public ushort Not(ushort value1)
		{
			ushort res = (ushort)(~value1);
			// Modifies flags: None

			return res;
		}

		public byte Or(byte value1, byte value2)
		{
			byte res = (byte)(value1 | value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Or(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 | value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Xor(byte value1, byte value2)
		{
			byte res = (byte)(value1 ^ value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Xor(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 ^ value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte And(byte value1, byte value2)
		{
			byte res = (byte)(value1 & value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort And(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 & value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public void Test(byte value1, byte value2)
		{
			byte res = (byte)(value1 & value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);
		}

		public void Test(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 & value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);
		}

		public byte Shl(byte value1, byte value2)
		{
			if (value2 == 0)
				return value1;

			byte res = (byte)(value1 << value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = (value2 > 8) ? false : ((value1 >> (8 - value2)) & 1) != 0;
			this.oFlags.O = ((res ^ value1) & 0x80) != 0;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Shl(ushort value1, byte value2)
		{
			if (value2 == 0)
				return value1;

			ushort res = (ushort)(value1 << value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = (value2 > 16) ? false : ((value1 >> (16 - value2)) & 1) != 0;
			this.oFlags.O = ((res ^ value1) & 0x8000) != 0;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Shr(byte value1, byte value2)
		{
			if (value2 == 0)
				return value1;

			byte res = (byte)(value1 >> value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = (value2 > 8) ? false : ((value1 >> (value2 - 1)) & 1) != 0;
			this.oFlags.O = ((value2 & 0x1f) == 1) ? (value1 > 0x80) : false;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Shr(ushort value1, byte value2)
		{
			if (value2 == 0)
				return value1;

			ushort res = (ushort)(value1 >> value2);
			// Modifies flags: CF OF PF SF ZF (AF undefined)
			this.oFlags.C = (value2 > 16) ? false : ((value1 >> (value2 - 1)) & 1) != 0;
			this.oFlags.O = ((value2 & 0x1f) == 1) ? (value1 > 0x8000) : false;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Inc(byte value1)
		{
			byte res = (byte)(value1 + 1);
			// Modifies flags: AF OF PF SF ZF
			this.oFlags.O = (res == 0x80);
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Inc(ushort value1)
		{
			ushort res = (ushort)(value1 + 1);
			// Modifies flags: AF OF PF SF ZF
			this.oFlags.O = (res == 0x8000);
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Dec(byte value1)
		{
			byte res = (byte)(value1 - 1);
			// Modifies flags: AF OF PF SF ZF
			this.oFlags.O = (res == 0x7f);
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Dec(ushort value1)
		{
			ushort res = (ushort)(value1 - 1);
			// Modifies flags: AF OF PF SF ZF
			this.oFlags.O = (res == 0x7fff);
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Add(byte value1, byte value2)
		{
			byte res = (byte)(value1 + value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (res < value1);
			this.oFlags.O = (((value1 ^ value2 ^ 0x80) & (res ^ value2)) & 0x80) != 0;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Add(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 + value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (res < value1);
			this.oFlags.O = (((value1 ^ value2 ^ 0x8000) & (res ^ value2)) & 0x8000) != 0;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Adc(byte value1, byte value2)
		{
			byte bCFlag = (byte)((this.oFlags.C) ? 1 : 0);
			byte res = (byte)(value1 + value2 + bCFlag);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (res < value1) || (bCFlag != 0 && (res == value1));
			this.oFlags.O = (((value1 ^ value2 ^ 0x80) & (res ^ value2)) & 0x80) != 0;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Adc(ushort value1, ushort value2)
		{
			ushort bCFlag = (ushort)((this.oFlags.C) ? 1 : 0);
			ushort res = (ushort)(value1 + value2 + bCFlag);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (res < value1) || (bCFlag != 0 && (res == value1));
			this.oFlags.O = (((value1 ^ value2 ^ 0x8000) & (res ^ value2)) & 0x8000) != 0;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public byte Sub(byte value1, byte value2)
		{
			byte res = (byte)(value1 - value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (value1 < value2);
			this.oFlags.O = (((value1 ^ value2) & (value1 ^ res)) & 0x80) != 0;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public ushort Sub(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 - value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (value1 < value2);
			this.oFlags.O = (((value1 ^ value2) & (value1 ^ res)) & 0x8000) != 0;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);

			return res;
		}

		public void Cmp(byte value1, byte value2)
		{
			byte res = (byte)(value1 - value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (value1 < value2);
			this.oFlags.O = (((value1 ^ value2) & (value1 ^ res)) & 0x80) != 0;
			this.oFlags.S = (res & 0x80) != 0;
			this.oFlags.Z = (res == 0);
		}

		public void Cmp(ushort value1, ushort value2)
		{
			ushort res = (ushort)(value1 - value2);
			// Modifies flags: AF CF OF PF SF ZF
			this.oFlags.C = (value1 < value2);
			this.oFlags.O = (((value1 ^ value2) & (value1 ^ res)) & 0x8000) != 0;
			this.oFlags.S = (res & 0x8000) != 0;
			this.oFlags.Z = (res == 0);
		}

		/// <summary>
		/// MOVS - Move String (Byte or Word) ES:DI = DS:SI
		/// </summary>
		public void RepMovsb()
		{
			do
			{
				this.WriteByte(this.oES.Word, this.oDI.Word, this.ReadByte(this.oDS.Word, this.oSI.Word));

				// Modifies flags: None
				if (this.oFlags.D)
				{
					this.oSI.Word--;
					this.oDI.Word--;
				}
				else
				{
					this.oSI.Word++;
					this.oDI.Word++;
				}
				this.oCX.Word--;
			}
			while (this.oCX.Word != 0);
		}

		/// <summary>
		/// MOVS - Move String (Byte or Word) ES:DI = DS:SI
		/// </summary>
		public void Movsb()
		{
			this.WriteByte(this.oES.Word, this.oDI.Word, this.ReadByte(this.oDS.Word, this.oSI.Word));
			// Modifies flags: None
			if (this.oFlags.D)
			{
				this.oSI.Word--;
				this.oDI.Word--;
			}
			else
			{
				this.oSI.Word++;
				this.oDI.Word++;
			}
		}

		/// <summary>
		/// MOVS - Move String (Byte or Word) ES:DI = DS:SI
		/// </summary>
		public void Movsw()
		{
			this.WriteWord(this.oES.Word, this.oDI.Word, this.ReadWord(this.oDS.Word, this.oSI.Word));
			// Modifies flags: None
			if (this.oFlags.D)
			{
				this.oSI.Word -= 2;
				this.oDI.Word -= 2;
			}
			else
			{
				this.oSI.Word += 2;
				this.oDI.Word += 2;
			}
		}

		/// <summary>
		/// MOVS - Move String (Byte or Word) ES:DI = DS:SI
		/// </summary>
		public void RepMovsw()
		{
			do
			{
				this.WriteWord(this.oES.Word, this.oDI.Word, this.ReadWord(this.oDS.Word, this.oSI.Word));
				// Modifies flags: None
				if (this.oFlags.D)
				{
					this.oSI.Word -= 2;
					this.oDI.Word -= 2;
				}
				else
				{
					this.oSI.Word += 2;
					this.oDI.Word += 2;
				}
				this.oCX.Word--;
			}
			while (this.oCX.Word != 0);
		}

		public void Div(ushort value)
		{
			if (value == 0)
				throw new Exception("Division by zero");
			uint num = ((uint)this.oDX.Word << 16) | (uint)this.oAX.Word;
			uint quo = num / value;
			ushort rem = (ushort)(num % value);
			ushort quo16 = (ushort)(quo & 0xffff);
			if (quo != (uint)quo16)
				throw new Exception("Division error");
			this.oDX.Word = rem;
			this.oAX.Word = quo16;
			// Modifies flags: (AF,CF,OF,PF,SF,ZF undefined)
			this.oFlags.C = false;
			this.oFlags.O = false;
			this.oFlags.S = false;
			this.oFlags.Z = false;
		}

		public void Mulb(byte value)
		{
			this.oAX.Word = (ushort)((ushort)this.oAX.Low * (ushort)value);
			this.oFlags.Z = this.oAX.Low == 0;
			if (this.oAX.High != 0)
			{
				this.oFlags.C = true;
				this.oFlags.O = true;
			}
			else
			{
				this.oFlags.C = false;
				this.oFlags.O = false;
			}
		}

		public void Mulw(ushort value)
		{
			uint tempu = (uint)this.oAX.Word * (uint)value;
			this.oAX.Word = (ushort)(tempu & 0xfffful);
			this.oDX.Word = (ushort)((tempu & 0xffff0000ul) >> 16);
			this.oFlags.Z = this.oAX.Word == 0;
			if (this.oDX.Word != 0)
			{
				this.oFlags.C = true;
				this.oFlags.O = true;
			}
			else
			{
				this.oFlags.C = false;
				this.oFlags.O = false;
			}
		}
	}
}
