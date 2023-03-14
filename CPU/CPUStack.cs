using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Disassembler.CPU
{
	public class CPUStack
	{
		private int iSize = 0xffff;
		private int iPosition = 0xffff;
		private byte[] aStack;

		public CPUStack()
			: this(0xffff)
		{ }

		public CPUStack(int size)
		{
			this.iSize = size;
			this.iPosition = size;
			this.aStack = new byte[this.iSize];
		}

		public ushort SP
		{
			get
			{
				return (ushort)(this.iPosition);
			}
			set
			{
				if ((value & 1) != 0)
				{
					throw new Exception("Stack alignment error");
				}
				this.iPosition = value;
				if (iPosition > this.iSize)
				{
					Console.WriteLine("Stack underflow");
					iPosition = this.iSize;
				}
			}
		}

		public void Push(ushort value)
		{
			if (this.iPosition - 2 < 0)
			{
				Console.WriteLine("Stack overflow");
				return;
			}
			this.aStack[this.iPosition--] = (byte)(value & 0xff);
			this.aStack[this.iPosition--] = (byte)((value & 0xff00) >> 8);
		}

		public void Push(uint value)
		{
			if (this.iPosition - 4 < 0)
			{
				Console.WriteLine("Stack overflow");
				return;
			}
			this.aStack[this.iPosition--] = (byte)(value & 0xff);
			this.aStack[this.iPosition--] = (byte)((value & 0xff00) >> 8);
			this.aStack[this.iPosition--] = (byte)((value & 0xff0000) >> 16);
			this.aStack[this.iPosition--] = (byte)((value & 0xff000000) >> 24);
		}

		public ushort PopWord()
		{
			if (this.iPosition + 2 >= this.iSize)
			{
				Console.WriteLine("Stack underflow");
				return 0;
			}

			return (ushort)(((ushort)this.aStack[this.iPosition++] << 8) | (ushort)this.aStack[this.iPosition++]);
		}

		public uint PopDWord()
		{
			if (this.iPosition + 4 >= this.iSize)
			{
				Console.WriteLine("Stack underflow");
				return 0;
			}

			return (uint)(((uint)this.aStack[this.iPosition++] << 24) | ((uint)this.aStack[this.iPosition++] << 16) |
				((uint)this.aStack[this.iPosition++] << 8) | ((uint)this.aStack[this.iPosition++]));
		}
	}
}
