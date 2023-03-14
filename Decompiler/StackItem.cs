using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public enum StackItemTypeEnum
	{
		Undefined,
		Register,
		Immediate,
		NearAddress,
		FarAddress
	}

	public class StackItem
	{
		private StackItemTypeEnum eType = StackItemTypeEnum.Undefined;
		private RegisterContext oRegister = null;

		public StackItem(StackItemTypeEnum context)
		{
			this.eType = context;
		}

		public StackItem(StackItemTypeEnum context, RegisterContext register)
		{
			this.eType = context;
			this.oRegister = register;
		}

		public StackItemTypeEnum Type
		{
			get
			{
				return this.eType;
			}
		}

		public RegisterContext Register
		{
			get
			{
				return this.oRegister;
			}
		}

		public int Size
		{
			get
			{
				int iSize = 0;

				switch (this.eType)
				{
					case StackItemTypeEnum.Register:
						iSize += 2;
						break;
					case StackItemTypeEnum.Immediate:
						iSize += 2;
						break;
					case StackItemTypeEnum.NearAddress:
						iSize += 2;
						break;
					case StackItemTypeEnum.FarAddress:
						iSize += 4;
						break;
				}

				return iSize;
			}
		}
	}
}
